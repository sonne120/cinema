![C#](https://img.shields.io/badge/C%23-512BD4)
![.NET](https://img.shields.io/badge/.NET-8-512BD4)
![Microservices](https://img.shields.io/badge/architecture-microservices-blue)
![Language](https://img.shields.io/github/languages/top/sonne120/cinema)
![Stars](https://img.shields.io/github/stars/sonne120/cinema)
![Forks](https://img.shields.io/github/forks/sonne120/cinema)
![Last commit](https://img.shields.io/github/last-commit/sonne120/cinema)

# Cinema Booking System

> **Cinema Booking System — distributed .NET 8 booking platform: CQRS over SQL Server + MongoDB, transactional outbox to Kafka, saga orchestration with compensation.**

> **Write side: every command persists the aggregate change and its integration event in one SQL Server transaction, so there is no dual write — MasterNode relays the outbox to Kafka and deletes on ack.**

> **Read side: ReadService x3 consumes Kafka with auto-commit off and projects into a 3-node MongoDB replica set; a read-only gRPC service answers queries straight from Mongo, never touching the write path.**

> **Service discovery: all six services register themselves in Consul and re-assert on a timer, so a Consul restart repopulates the catalog without restarting anything.**

Microservice platform where bookings enter over **REST/gRPC through a gateway and YARP load
balancer**, flow through a **SQL Server outbox to Kafka to MongoDB** path, and are queried from the
read model — a CQRS split with SQL Server as the write side and MongoDB as the query side. Ticket
purchase runs as an **orchestrated saga**: four steps, each with a compensating action replayed in
reverse when a step fails.

**Contents:**
[Architecture](#architecture) ·
[Ticket purchase saga](#ticket-purchase-saga) ·
[Service discovery](#service-discovery) ·
[Observability](#observability) ·
[Projects](#projects) ·
[Run it](#run-it) ·
[Tests](#tests) ·
[Kubernetes](#kubernetes) ·
[AWS (EKS)](#aws-eks)

## Architecture

```
     WRITE PATH:  clients → gateway → load balancer → Cinema API ×2 → SQL Server outbox

                         ┌────────────────────────────────┐
                         │          Client apps           │  HTTPS · JWT bearer
                         │       web · mobile · CLI       │  commands + queries
                         └───────────────┬────────────────┘
                                         ▼
                         ┌────────────────────────────────┐
                         │  API Gateway  ·  :5005         │  single entry point
                         │  Ocelot routing · JWT auth     │  REST in, gRPC out
                         │  Polly retry + circuit breaker │  Swagger UI
                         │  gRPC clients: write · read    │
                         └────┬───────────────────────┬───┘
     write (gRPC / h2c)       │                       └────▶  read (gRPC) — jumps
                              ▼                               to Cinema gRPC :7080
   ┌────────────────┐    ┌────────────────────────────────┐   in the READ PATH
   │  Consul :8500  │───▶│  LoadBalancer  ·  :5003        │  route table built
   │                │    │  YARP reverse proxy            │  from Consul, not
   │  service       │    │  round-robin over live pods    │  from static config
   │  registry +    │    └───────────────┬────────────────┘
   │  health checks │                    ▼
   │                │    ┌────────────────────────────────┐
   │                │◀───│  Cinema API × 2 :5001 / :5002  │  Clean Architecture
   └────────────────┘    │  REST + gRPC · MediatR · DDD   │  CQRS write side
        register         │  Ticket Saga orchestrator      │
                         └───────────────┬────────────────┘
                                         │ one transaction: state + event
                                         ▼
                         ┌────────────────────────────────┐
                         │  SQL Server  ·  :1433          │  CinemaDb
                         │  aggregates · saga state       │  write model
                         │  OutboxMessages (transactional)│
                         └───────────────┬────────────────┘
                                         │ claim batch (READPAST, UPDLOCK)
                                         ▼
                         ┌────────────────────────────────┐
                         │  MasterNode  ·  :5010          │  polls the outbox
                         │  outbox processor              │  of both API
                         │  TPL batches · delete on ack   │  instances
                         └────┬──────────────────────┬────┘
                   project    │                      │  publish
                              ▼                      ▼
              ┌───────────────────┐   ┌────────────────────────────────┐
              │  MasterDb (SQL)   │   │  Kafka  ·  :9092               │
              │  reservations     │   │  cinema.reservations           │
              │  showtimes        │   │  cinema.showtimes              │
              └───────────────────┘   │  cinema.domain.events          │
                                      └───────────────┬────────────────┘
                                                      │
      READ PATH (CQRS):  Kafka → ReadService ×3 → MongoDB rs0 → gRPC query service 
                                                      │ consume · manual commit
                                                      ▼
                         ┌────────────────────────────────┐
                         │  ReadService × 3               │  group
                         │  Kafka consumer workers        │  cinema-read-
                         │  project → read models         │  service-group
                         └───────────────┬────────────────┘
                                         ▼  upsert
                         ┌────────────────────────────────┐
                         │  MongoDB replica set  rs0      │  CinemaReadDb
                         │  mongo1 :27017 (primary)       │  showtimes ·
                         │  mongo2 :27018 · mongo3 :27019 │  reservations
                         └───────────────┬────────────────┘
                                         │ query
   API Gateway                           ▼
   read (gRPC) ────────▶ ┌────────────────────────────────┐
                         │  Cinema gRPC  ·  :7080 → 5001  │  serves the
                         │  read-only query service       │  gateway's queries
                         │  HTTP/2 · never writes         │  straight from Mongo
                         └────────────────────────────────┘

      OBSERVABILITY:  every service exports logs, traces and metrics (OpenTelemetry) 

     ┌──────────────────────┐  ┌──────────────────────┐  ┌──────────────────────┐
     │  Seq  ·  :5341       │  │  Jaeger  ·  :16686   │  │  Prometheus  ·  :9090│
     │  structured logs     │  │  OTLP traces :4317   │  │  scrapes /metrics    │
     │  (Serilog sink)      │  │  (OpenTelemetry)     │  │  from every service  │
     └──────────────────────┘  └──────────────────────┘  └──────────────────────┘
```

1. **Ingress.** Clients hit the API Gateway on `:5005`: Ocelot routing, JWT bearer validation,
   Swagger, and Polly (retry + circuit breaker) wrapped around two gRPC clients — a write client
   aimed at the load balancer, and a read client aimed at `cinema-grpc`.
2. **Load balancing and discovery.** Write calls travel over gRPC (HTTP/2 plaintext) to the
   LoadBalancer on `:5003`, a YARP proxy whose route table comes from Consul
   (`ConsulProxyConfigProvider`): the API instances register themselves, and the balancer
   round-robins across the healthy ones.
3. **Write side.** Cinema API ×2 (`:5001`, `:5002`) serves REST and gRPC from one Clean
   Architecture stack (MediatR handlers → domain aggregates). Every command writes the aggregate
   change and its integration event into `OutboxMessages` in a single SQL Server transaction, so
   there is no dual write and nothing is lost while Kafka is down.
4. **Saga.** Ticket purchase is orchestrated by `TicketPurchaseSaga`: four steps with
   compensation, saga state persisted in SQL Server. See [Ticket purchase saga](#ticket-purchase-saga).
5. **Outbox relay.** MasterNode (`:5010`) polls the outbox of both API instances and claims
   batches with `WITH (READPAST, UPDLOCK, ROWLOCK)`, so replicas never grab the same rows. Each
   batch is fanned out with `Parallel.ForEachAsync` grouped by event type: rows are projected into
   the master SQL database, published to Kafka, then deleted. A failed batch is released
   (`ProcessedOnUtc = NULL` plus the error) and retried.
6. **Kafka.** Events are keyed by aggregate id and routed by type to `cinema.reservations`,
   `cinema.showtimes`, or `cinema.domain.events`, carrying `event-id`, `event-type`,
   `aggregate-type`, and `aggregate-id` headers.
7. **Read side (CQRS).** ReadService ×3 consumes `cinema.domain.events` in the
   `cinema-read-service-group` consumer group with auto-commit off — the offset is committed only
   after the projection succeeds — and upserts denormalized documents into the MongoDB replica set
   (`CinemaReadDb`: showtimes, reservations).
8. **Queries.** Cinema gRPC (`:7080`) serves read-only queries from MongoDB over HTTP/2. The
   gateway calls it directly, so query traffic never touches SQL Server or the write path.

## Ticket purchase saga

Orchestrated saga with per-step compensation, replayed in reverse for steps that actually ran:

```
   ┌───────────────────┐  ┌───────────────────┐  ┌───────────────────┐  ┌───────────────────┐
   │ 1. Reserve seats  │─▶│ 2. Take payment   │─▶│ 3. Confirm booking│─▶│ 4. Issue ticket   │
   └─────────┬─────────┘  └─────────┬─────────┘  └─────────┬─────────┘  └─────────┬─────────┘
             │                      │                      │                      │  on failure
             ▼                      ▼                      ▼                      ▼  or timeout
       release seats           refund via             revert the             cancel the
    expire reservation       payment gateway       confirmation flag       issued ticket
             ▲                      ▲                      ▲                      │
             └──────────────────────┴──────────────────────┴──────────────────────┘
                        compensation runs in reverse order → Compensated
```

The steps are `ReserveSeatsStep` → `ProcessPaymentStep` → `ConfirmReservationStep` →
`IssueTicketStep`. All four succeed → the saga ends **Completed**; any step failure or a timeout
→ compensation runs backwards and the saga ends **Compensated**. State and step progress live in
SQL Server (`SagaStateRepository`), and every step emits a saga event on the bus.

## Service discovery

<details>
<summary><b>One registry, three kinds of health check</b> — because not every service can answer an HTTP GET</summary>

Services register themselves through `Cinema.ServiceDiscovery`; the load balancer builds its YARP
route table from the catalog rather than from static config. Scaling a tier is a Compose change,
and a failed instance leaves the rotation on its own check.

The check has to match the service's shape — a check that cannot pass is worse than no
registration at all:

| Service | Instances | Check | Why |
|---|---|---|---|
| `cinema-api` | 2 | `HTTP` | plain `/health` endpoint |
| `cinema-gateway` | 1 | `HTTP` | plain `/health` endpoint |
| `cinema-loadbalancer` | 1 | `HTTP` | plain `/health` endpoint |
| `cinema-masternode` | 1 | `HTTP` | plain `/health` endpoint |
| `cinema-grpc` | 1 | `TCP` | Kestrel is HTTP/2-only, so Consul's HTTP/1.1 probe can never succeed |
| `cinema-read-service` | 3 | `TTL` | worker host with no listening socket — it pushes a heartbeat instead |

Registration is re-asserted on a timer rather than done once at startup. Consul may be unreachable
when a service boots, and a Consul restart drops every registration it was holding; a one-shot
registration leaves the catalog silently empty in both cases. Each tick checks whether its own id
is still present and re-registers if not — `ServiceRegister` is an upsert, so this is idempotent.

Instances use a stable id (`{service}-{host}`), so a restarting container replaces its own
entry instead of leaving a critical orphan behind.

</details>

## Observability

<details>
<summary><b>Three tools, three questions</b> — logs, traces, metrics</summary>

| Tool | Answers | Port |
|---|---|---|
| **Seq** | *what happened, exactly* — structured log events (Serilog) | `:5341` |
| **Jaeger** | *where did one request spend its time* — OTLP traces | `:16686` |
| **Prometheus** | *how is the system behaving over time* — scraped metrics | `:9090` |

Every service exports OTLP traces to `jaeger:4317` and writes structured logs to `seq:5341`.
Prometheus scrapes `/metrics` from the API instances, gateway, load balancer and master node.

Two services are intentionally absent from Prometheus: `cinema-read-service` is a worker with no
HTTP listener, and `cinema-grpc` is h2c-only — the same constraint that forces a TCP check in
Consul. Both still push traces over OTLP.

Jaeger runs as `all-in-one` with in-memory storage, so traces do not survive a container restart.
That is deliberate for local development; a persistent backend (Elasticsearch or Cassandra) would
be the production swap.

</details>

## Projects

<details>
<summary><b>Project map</b> — what each project does</summary>

- **`Cinema.Api`** x2: REST + gRPC write side. MediatR handlers over domain aggregates; every
  command writes state and its integration event in one transaction.
- **`Cinema.ApiGateway`**: single entry point. Ocelot routing, JWT validation, Swagger, Polly
  retry + circuit breaker around gRPC clients for the write and read paths.
- **`Cinema.LoadBalancer`**: YARP reverse proxy whose route table is built from the Consul
  catalog, round-robin across healthy API instances.
- **`Cinema.MasterNode`**: outbox processor. Claims batches with `READPAST, UPDLOCK, ROWLOCK`,
  projects into the master database, publishes to Kafka, deletes on ack.
- **`Cinema.ReadService`** x3: Kafka consumer workers with auto-commit off, projecting
  denormalized documents into MongoDB.
- **`Cinema.GrpcService`**: read-only query service over HTTP/2, served straight from MongoDB.
- **`Cinema.Domain`**: aggregates, value objects, domain events. No dependencies.
- **`Cinema.Application`**: MediatR commands/queries, saga orchestration, port interfaces.
- **`Cinema.Infrastructure`**: EF Core, repositories, Kafka producer, JWT, outbox.
- **`Cinema.Persistence.Read`**: MongoDB read-model repositories.
- **`Cinema.Contracts`**: DTOs shared across service boundaries.
- **`Cinema.ServiceDiscovery`**: Consul registration and lookup. Standalone rather than part of
  `Cinema.Infrastructure` so the gateway and load balancer can register without depending on
  EF Core, SQL Server and the Mongo driver.

</details>

## Run it

```bash
docker compose up -d
```

Infrastructure, observability and the services all live in the single `docker-compose.yml`.

| Service | URL |
|---------|-----|
| API Gateway | http://localhost:5005 |
| Cinema API (instance 1 / 2) | http://localhost:5001 / http://localhost:5002 |
| Load Balancer | http://localhost:5003 |
| Master Node | http://localhost:5010 |
| gRPC service (HTTP/2 only) | http://localhost:7080 |
| Swagger | http://localhost:5001/swagger |
| Consul UI | http://localhost:8500 |
| Kafka UI | http://localhost:8090 |
| Mongo Express | http://localhost:8081 |
| Seq (Logs) | http://localhost:5341 |
| Jaeger (Traces) | http://localhost:16686 |
| Prometheus | http://localhost:9090 |

<details>
<summary><b>Smoke test</b> — 30 assertions across every tier, in one command</summary>

```bash
./smoke-test.sh        # full output
./smoke-test.sh -q     # only failures and the summary
```

Each check asserts an expected value and the script exits non-zero if any fails, so it works in CI
or a pre-push hook. Covers:

| Section | Asserts |
|---|---|
| infrastructure | Consul, Seq, Jaeger, Prometheus, Kafka UI, Mongo Express all answer |
| service health | `/health` on all six services, including h2c for gRPC |
| consul registrations | 9 instances, 9 passing, and the right split of `http` / `tcp` / `ttl` checks |
| observability | Prometheus targets up, Jaeger receiving traces, Seq storing events |
| auth flow | register, login, authorized call, 401 without a token, 403 for a Customer on a manager route, logout |
| mongo replica set | exactly one PRIMARY and two SECONDARY members |

A tracked defect is reported as `KNOWN` rather than `FAIL`, so a documented issue stays visible
without making the suite red.

Register and call an endpoint by hand:

```bash
curl -s -X POST http://localhost:5001/api/auth/register \
  -H 'Content-Type: application/json' \
  -d '{"email":"demo@cinema.local","password":"P@ssw0rd123!","firstName":"Demo","lastName":"User"}'

TOKEN=$(curl -s -X POST http://localhost:5001/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"demo@cinema.local","password":"P@ssw0rd123!"}' | jq -r .accessToken)

curl -s http://localhost:5001/api/auth/profile -H "Authorization: Bearer $TOKEN"
```

The gRPC service speaks HTTP/2 only, so curl needs prior knowledge:

```bash
curl -s --http2-prior-knowledge http://localhost:7080/health
```

</details>

## Tests

```bash
dotnet test Cinema.sln
```

121 tests across five projects — `Cinema.Domain.UnitTests` (94) carries the aggregate and
value-object rules, with `Application`, `Infrastructure`, `MasterNode` unit tests and
`Cinema.Api.IntegrationTests` (16) covering the HTTP surface.

## Kubernetes

```bash
kubectl apply -k k8s/
kubectl get pods -n cinema
```

Manifests in [`k8s/`](k8s/) cover the services, config, secrets, ingress, HPA autoscaling and the
observability stack.

## AWS (EKS)

Terraform stands up VPC, EKS, ECR and RDS; a Kustomize overlay repoints the cluster manifests at
AWS. Everything lives in [`cinema-eks-deploy/`](cinema-eks-deploy/) and nothing about it affects
local development — `docker compose up -d` stays the way to run the stack on a workstation.

What the overlay changes relative to `k8s/`: SQL Server moves out of the cluster to **RDS**
(`sqlserver-service` becomes an `ExternalName` record, so in-cluster DNS is unchanged), images point
at **ECR**, the nginx Ingress is replaced by an **AWS ALB**, and the NetworkPolicy is dropped
because it blocks ALB IP-target health checks. Mongo, Kafka, Redis, Consul and the observability
stack stay in the cluster.

```bash
cd cinema-eks-deploy/terraform
terraform init && terraform apply          # ~15-20 min, mostly EKS and RDS
aws eks update-kubeconfig --region eu-central-1 --name cinema-demo
```

Then push to `master` and CI does the rest.

<details>
<summary><b>CI deployment</b> — three secrets, then it is automatic</summary>

[`.github/workflows/deploy-eks.yml`](.github/workflows/deploy-eks.yml) builds the five service
images, pushes them to ECR and rolls the cluster. Authentication is OIDC, so no long-lived AWS keys
are stored in the repository.

Set these under *Settings → Secrets and variables → Actions*:

| Secret | Where it comes from |
|---|---|
| `AWS_DEPLOY_ROLE_ARN` | the IAM role GitHub assumes — see [DEPLOY-EKS.md](cinema-eks-deploy/DEPLOY-EKS.md) |
| `RDS_ENDPOINT` | `terraform output -raw rds_endpoint` |
| `RDS_PASSWORD` | Secrets Manager, ARN in `terraform output rds_secret_arn` |

Region and cluster come from the repository *variables* `AWS_REGION` and `EKS_CLUSTER`, defaulting
to `eu-central-1` and `cinema-demo`.

Until `AWS_DEPLOY_ROLE_ARN` is set the workflow skips itself with a notice rather than failing, so
the repository stays green without an AWS account attached.

To deploy by hand instead, [DEPLOY-EKS.md](cinema-eks-deploy/DEPLOY-EKS.md) walks through the build,
the overlay substitutions and `kustomize build | kubectl apply`.

</details>

<details>
<summary><b>Teardown</b> — order matters</summary>

```bash
kustomize build cinema-eks-deploy/k8s-eks | kubectl delete -f -
cd cinema-eks-deploy/terraform && terraform destroy
```

Delete the Ingress **before** `terraform destroy`. Otherwise the Load Balancer Controller leaves
orphaned ELBs and target groups behind, and the VPC deletion blocks on them.

</details>
