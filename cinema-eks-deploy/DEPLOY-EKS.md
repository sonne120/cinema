# Deploy Cinema to AWS EKS

Terraform stands up the infrastructure, a Kustomize overlay repoints the manifests in `k8s/` at
AWS, and GitHub Actions builds and rolls it. Local development is unaffected — `docker compose up -d`
at the repository root stays the way to run the stack on a workstation.

## What runs where

Everything stateful stays in the cluster except SQL Server, which moves to RDS: a replicated write
store is better off outside ephemeral pods.

```
                       ┌──────────────── AWS ─────────────────┐
  Internet ── ALB ──►  │  EKS cluster (managed node group)     │
   (ALB Ingress        │   ├─ api-gateway, cinema-api ×3       │
    Controller)        │   ├─ loadbalancer, read-service ×3    │
                       │   ├─ master-node                      │
                       │   ├─ Mongo rs0 ×3 (StatefulSet)       │
                       │   ├─ Kafka + Zookeeper                │
                       │   ├─ Redis, Consul                    │
                       │   └─ Seq / Jaeger / Prometheus        │
                       │                                       │
                       │   RDS SQL Server ◄── sqlserver-       │
                       │   (outside the cluster)  service      │
                       │                      (ExternalName)   │
                       └───────────────────────────────────────┘
```

| Path | Purpose |
|---|---|
| `terraform/` | VPC, EKS, managed node group, ECR, RDS SQL Server, AWS Load Balancer Controller |
| `k8s-eks/` | Kustomize overlay on `k8s/`: images → ECR, SQL → RDS, Ingress → ALB |
| `../.github/workflows/deploy-eks.yml` | build → push to ECR → roll the cluster |

The base manifests in `k8s/` are **not modified**; the overlay layers on top of them.

**Prerequisites:** `aws` CLI (authenticated), `terraform >= 1.5`, `kubectl`, `kustomize >= 5`,
`docker`, and an AWS account with VPC/EKS/RDS/IAM/ECR permissions.

## Deploy

**1 — Infrastructure.** Takes 15–20 minutes, mostly EKS and RDS.

```bash
cd terraform
cp terraform.tfvars.example terraform.tfvars    # optional: region, instance sizes
terraform init && terraform apply
eval "$(terraform output -raw configure_kubectl)"   # points kubectl at the cluster
kubectl get nodes
```

**2 — Images.** Five services; run from the repository root.

```bash
REGISTRY=$(cd cinema-eks-deploy/terraform && terraform output -raw ecr_registry)
aws ecr get-login-password --region eu-central-1 | docker login --username AWS --password-stdin "$REGISTRY"

for pair in cinema-api:Dockerfile cinema-apigateway:Dockerfile.gateway \
            cinema-readservice:Dockerfile.readservice \
            cinema-masternode:Dockerfile.masternode \
            cinema-loadbalancer:Dockerfile.loadbalancer; do
  repo=${pair%%:*}; file=${pair#*:}
  docker build -f "$file" -t "$REGISTRY/$repo:latest" .
  docker push "$REGISTRY/$repo:latest"
done
```

**3 — Overlay and apply.** The overlay ships placeholders so no secret is committed.

```bash
cd cinema-eks-deploy/k8s-eks
RDS_ENDPOINT=$(cd ../terraform && terraform output -raw rds_endpoint)
RDS_PASSWORD=$(aws secretsmanager get-secret-value \
  --secret-id "$(cd ../terraform && terraform output -raw rds_secret_arn)" \
  --query SecretString --output text | jq -r .password)

for svc in cinema-api cinema-apigateway cinema-readservice cinema-masternode cinema-loadbalancer; do
  kustomize edit set image "${svc}=${REGISTRY}/${svc}:latest"
done
sed -i "s/RDS_ENDPOINT/${RDS_ENDPOINT}/g"            kustomization.yaml
sed -i "s/REPLACE_WITH_RDS_PASSWORD/${RDS_PASSWORD}/g" kustomization.yaml

cd ../..
kustomize build cinema-eks-deploy/k8s-eks | kubectl apply -f -
kubectl -n cinema get pods -w      # wait for Running/Ready
kubectl -n cinema get ingress      # ALB address appears in ADDRESS after 2-3 min
```

Check it:

```bash
ALB=$(kubectl -n cinema get ingress cinema-alb -o jsonpath='{.status.loadBalancer.ingress[0].hostname}')
curl -i "http://$ALB/health"
```

## CI/CD

[`.github/workflows/deploy-eks.yml`](../.github/workflows/deploy-eks.yml) does steps 2 and 3 on every
push to `master`/`main`. Authentication is OIDC, so no long-lived AWS keys are stored.

One-time setup:

1. An **IAM role for GitHub OIDC**, trusting `token.actions.githubusercontent.com`, allowed to push
   to ECR and call `eks:DescribeCluster`, and granted cluster access (an EKS access entry, or
   `aws-auth`).
2. **Repository secrets** — *Settings → Secrets and variables → Actions*:

   | Secret | Value |
   |---|---|
   | `AWS_DEPLOY_ROLE_ARN` | the role from step 1 |
   | `RDS_ENDPOINT` | `terraform output -raw rds_endpoint` |
   | `RDS_PASSWORD` | from Secrets Manager, ARN in `terraform output rds_secret_arn` |

3. **Repository variables**, optional: `AWS_REGION` (default `eu-central-1`) and `EKS_CLUSTER`
   (default `cinema-demo`).

The registry is not a secret — the workflow reads it from the `amazon-ecr-login` output. Until
`AWS_DEPLOY_ROLE_ARN` is set the workflow skips with a notice instead of failing, so a repository
with no AWS account attached stays green.

## Teardown

```bash
kustomize build cinema-eks-deploy/k8s-eks | kubectl delete -f -
cd cinema-eks-deploy/terraform && terraform destroy
```

Delete the Ingress **before** `terraform destroy`, or the Load Balancer Controller leaves orphaned
ELBs and target groups and the VPC deletion blocks on them.

## Notes

- **RDS rejects `sa`** as a master login, so the overlay rewrites the connection strings to
  `cinema_admin`. The host stays `sqlserver-service` — an `ExternalName` record pointing at RDS — so
  in-cluster DNS is unchanged.
- **NetworkPolicy is removed** by the overlay. It only admitted traffic from the `ingress-nginx`
  namespace, which blocks ALB IP-target health checks. For production, scope a policy to the ALB
  subnets or security group instead.
- **HTTPS is not enabled.** The ALB listens on HTTP:80 only; issue an ACM certificate and uncomment
  `certificate-arn` / `listen-ports` in `k8s-eks/ingress-alb.yaml`.
- **Secrets are demo values.** `k8s/secrets.yaml` holds plaintext placeholders. For production use
  External Secrets Operator with AWS Secrets Manager, where Terraform already stores the RDS password.
- **Mongo stays in-cluster** as a StatefulSet; DocumentDB is not used.
- **The gRPC service is not deployed.** Terraform creates a `cinema-grpcservice` ECR repository, but
  `k8s/` defines a `cinema-grpc-ingress` with no Deployment behind it, so there is nothing to run the
  image. Add the Deployment and Service first, then a matrix entry in the workflow.
