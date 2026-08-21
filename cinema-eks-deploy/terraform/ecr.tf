# По одному ECR-репозиторию на каждый собственный сервис Cinema.
locals {
  ecr_repositories = [
    "cinema-api",
    "cinema-apigateway",
    "cinema-readservice",
    "cinema-masternode",
    "cinema-loadbalancer",
    "cinema-grpcservice",
  ]
}

resource "aws_ecr_repository" "this" {
  for_each = toset(local.ecr_repositories)

  name                 = each.value
  image_tag_mutability = "MUTABLE"
  force_delete         = true # demo: позволяет terraform destroy удалить репо с образами

  image_scanning_configuration {
    scan_on_push = true
  }
}

# Оставляем только последние 10 образов на репозиторий.
resource "aws_ecr_lifecycle_policy" "this" {
  for_each   = aws_ecr_repository.this
  repository = each.value.name

  policy = jsonencode({
    rules = [{
      rulePriority = 1
      description  = "Keep last 10 images"
      selection = {
        tagStatus   = "any"
        countType   = "imageCountMoreThan"
        countNumber = 10
      }
      action = { type = "expire" }
    }]
  })
}
