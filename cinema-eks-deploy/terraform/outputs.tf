output "region" {
  value = var.region
}

output "cluster_name" {
  value = module.eks.cluster_name
}

output "configure_kubectl" {
  description = "Команда для настройки kubectl на этот кластер"
  value       = "aws eks update-kubeconfig --region ${var.region} --name ${module.eks.cluster_name}"
}

output "account_id" {
  value = data.aws_caller_identity.current.account_id
}

output "ecr_registry" {
  description = "Базовый URL ECR-реестра"
  value       = "${data.aws_caller_identity.current.account_id}.dkr.ecr.${var.region}.amazonaws.com"
}

output "ecr_repository_urls" {
  value = { for k, r in aws_ecr_repository.this : k => r.repository_url }
}

output "rds_endpoint" {
  description = "Хост RDS SQL Server (для sqlserver-service ExternalName)"
  value       = aws_db_instance.sqlserver.address
}

output "rds_master_username" {
  value = var.rds_master_username
}

output "rds_secret_arn" {
  description = "ARN секрета с логином/паролем RDS в Secrets Manager"
  value       = aws_secretsmanager_secret.rds.arn
}

data "aws_caller_identity" "current" {}
