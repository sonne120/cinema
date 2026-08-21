variable "region" {
  description = "AWS регион для всех ресурсов"
  type        = string
  default     = "eu-central-1"
}

variable "environment" {
  description = "Имя окружения (demo / staging / prod)"
  type        = string
  default     = "demo"
}

variable "project" {
  description = "Префикс имён ресурсов"
  type        = string
  default     = "cinema"
}

variable "vpc_cidr" {
  description = "CIDR блок VPC"
  type        = string
  default     = "10.0.0.0/16"
}

variable "cluster_version" {
  description = "Версия Kubernetes для EKS"
  type        = string
  default     = "1.30"
}

variable "node_instance_types" {
  description = "Типы инстансов для managed node group"
  type        = list(string)
  # m5.large: 2 vCPU / 8 GiB — достаточно под все stateless-сервисы + Mongo rs + Kafka демо-контура
  default     = ["m5.large"]
}

variable "node_desired_size" {
  type    = number
  default = 3
}

variable "node_min_size" {
  type    = number
  default = 2
}

variable "node_max_size" {
  type    = number
  default = 5
}

# --- RDS SQL Server (единственный stateful-компонент, вынесенный из кластера) ---

variable "rds_instance_class" {
  description = "Класс инстанса RDS SQL Server"
  type        = string
  default     = "db.t3.large" # SQL Server Web edition требует минимум t3.large / t3.xlarge в ряде регионов
}

variable "rds_allocated_storage" {
  description = "Размер диска RDS, ГБ"
  type        = number
  default     = 20
}

variable "rds_engine_version" {
  description = "Версия SQL Server"
  type        = string
  default     = "16.00" # SQL Server 2022
}

variable "rds_master_username" {
  description = "Мастер-пользователь RDS. Внимание: RDS запрещает 'sa' как имя мастер-пользователя."
  type        = string
  default     = "cinema_admin"
}

variable "rds_multi_az" {
  description = "Включить Multi-AZ (для demo — false, дешевле)"
  type        = bool
  default     = false
}
