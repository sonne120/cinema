module "eks" {
  source  = "terraform-aws-modules/eks/aws"
  version = "~> 20.24"

  cluster_name    = local.name
  cluster_version = var.cluster_version

  # Публичный API-эндпоинт кластера (для demo удобнее; в prod ограничьте по CIDR).
  cluster_endpoint_public_access = true

  # Даёт создателю кластера права admin через EKS access entries.
  enable_cluster_creator_admin_permissions = true

  vpc_id     = module.vpc.vpc_id
  subnet_ids = module.vpc.private_subnets

  # Базовые аддоны кластера.
  cluster_addons = {
    coredns                = {}
    kube-proxy             = {}
    vpc-cni                = {}
    eks-pod-identity-agent = {}
  }

  eks_managed_node_groups = {
    default = {
      instance_types = var.node_instance_types
      capacity_type  = "ON_DEMAND"

      desired_size = var.node_desired_size
      min_size     = var.node_min_size
      max_size     = var.node_max_size

      # Диск под образы и эфемерные тома Mongo/Kafka демо-контура.
      disk_size = 50

      labels = {
        role = "general"
      }
    }
  }

  tags = {
    "kubernetes.io/cluster/${local.name}" = "shared"
  }
}
