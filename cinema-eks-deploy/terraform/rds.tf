# --- RDS SQL Server: единственный stateful-компонент вне кластера ---

resource "random_password" "rds" {
  length  = 20
  special = true
  # SQL Server не любит некоторые спецсимволы в пароле мастер-пользователя.
  override_special = "!#$%*()-_=+[]{}"
}

# Пароль храним в Secrets Manager (не в state в открытом виде на проде).
resource "aws_secretsmanager_secret" "rds" {
  name = "${local.name}/rds/sqlserver"
}

resource "aws_secretsmanager_secret_version" "rds" {
  secret_id = aws_secretsmanager_secret.rds.id
  secret_string = jsonencode({
    username = var.rds_master_username
    password = random_password.rds.result
    host     = aws_db_instance.sqlserver.address
    port     = 1433
  })
}

resource "aws_db_subnet_group" "this" {
  name       = "${local.name}-db"
  subnet_ids = module.vpc.private_subnets
}

# SG: доступ к 1433 только из подсетей VPC (то есть из подов кластера).
resource "aws_security_group" "rds" {
  name        = "${local.name}-rds"
  description = "Allow SQL Server access from within the VPC"
  vpc_id      = module.vpc.vpc_id

  ingress {
    description = "SQL Server from VPC"
    from_port   = 1433
    to_port     = 1433
    protocol    = "tcp"
    cidr_blocks = [var.vpc_cidr]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

resource "aws_db_instance" "sqlserver" {
  identifier     = "${local.name}-sqlserver"
  engine         = "sqlserver-web" # Web edition — самый дешёвый лицензионный вариант
  engine_version = var.rds_engine_version
  instance_class = var.rds_instance_class
  license_model  = "license-included"

  allocated_storage = var.rds_allocated_storage
  storage_type      = "gp3"
  storage_encrypted = true

  username = var.rds_master_username
  password = random_password.rds.result

  db_subnet_group_name   = aws_db_subnet_group.this.name
  vpc_security_group_ids = [aws_security_group.rds.id]
  multi_az               = var.rds_multi_az
  publicly_accessible    = false

  skip_final_snapshot = true  # demo
  deletion_protection = false # demo
  apply_immediately   = true

  # Note: базы CinemaDb / CinemaMasterDb приложение создаёт само (EF migrations).
}
