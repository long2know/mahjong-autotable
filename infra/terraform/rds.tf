# Phase K Wave 5 — Apone (DevOps).
#
# RDS Postgres instance + a dedicated DB subnet group across the
# three private subnets. Master password is generated locally and
# surfaced as a sensitive output for the operator to put into SSM
# (NOT auto-pushed — terraform should never own the credential
# rotation surface for runtime secrets; the seed lives in the
# tfstate, ESO does the runtime delivery).

resource "aws_db_subnet_group" "this" {
  name        = "mahjong-${var.environment}-db"
  description = "RDS subnet group spanning the three private subnets"
  subnet_ids  = aws_subnet.private[*].id

  tags = {
    Name = "mahjong-${var.environment}-db"
  }
}

resource "aws_security_group" "db" {
  name        = "mahjong-${var.environment}-db"
  description = "Postgres ingress from EKS workers only"
  vpc_id      = aws_vpc.this.id

  tags = {
    Name = "mahjong-${var.environment}-db"
  }
}

# Postgres ingress: from anywhere inside the VPC ONLY (worker
# nodes can reach RDS; nothing outside the VPC can). Tighter
# ingress (per-pod / per-namespace) belongs at the k8s
# NetworkPolicy layer, not the SG layer.
resource "aws_security_group_rule" "db_ingress_pg" {
  type              = "ingress"
  from_port         = 5432
  to_port           = 5432
  protocol          = "tcp"
  cidr_blocks       = [var.vpc_cidr]
  security_group_id = aws_security_group.db.id
  description       = "Postgres ingress from inside the VPC"
}

resource "aws_security_group_rule" "db_egress_all" {
  type              = "egress"
  from_port         = 0
  to_port           = 0
  protocol          = "-1"
  cidr_blocks       = ["0.0.0.0/0"]
  security_group_id = aws_security_group.db.id
  description       = "All egress (RDS reaches AWS service endpoints)"
}

# 24-char random password — exceeds the AWS-recommended 12-char
# floor for RDS; encoded as alphanumeric to avoid the long tail
# of escaping bugs across CLI / k8s envFrom / SSM parameter values.
resource "random_password" "db_master" {
  length  = 32
  special = false
  upper   = true
  lower   = true
  numeric = true
}

resource "aws_kms_key" "rds" {
  description             = "RDS storage-encryption key for mahjong-${var.environment}"
  deletion_window_in_days = 7
  enable_key_rotation     = true

  tags = {
    Name = "mahjong-${var.environment}-rds-storage"
  }
}

resource "aws_kms_alias" "rds" {
  name          = "alias/mahjong-${var.environment}-rds-storage"
  target_key_id = aws_kms_key.rds.key_id
}

resource "aws_db_instance" "this" {
  identifier             = "mahjong-${var.environment}"
  engine                 = "postgres"
  engine_version         = var.db_engine_version
  instance_class         = var.db_instance_class
  allocated_storage      = var.db_allocated_storage_gb
  max_allocated_storage  = var.db_max_allocated_storage_gb
  storage_type           = "gp3"
  storage_encrypted      = true
  kms_key_id             = aws_kms_key.rds.arn
  db_name                = var.db_name
  username               = var.db_master_username
  password               = random_password.db_master.result
  port                   = 5432
  db_subnet_group_name   = aws_db_subnet_group.this.name
  vpc_security_group_ids = [aws_security_group.db.id]
  multi_az               = var.db_multi_az
  publicly_accessible    = false
  deletion_protection    = var.db_deletion_protection
  skip_final_snapshot    = !var.db_deletion_protection
  # Final snapshot only matters when deletion_protection drops to
  # false; suffix with a timestamp so re-creation does not collide.
  final_snapshot_identifier = "mahjong-${var.environment}-final-${formatdate("YYYYMMDDhhmm", timestamp())}"
  backup_retention_period   = var.db_backup_retention_days
  backup_window             = "03:00-04:00"
  maintenance_window        = "Mon:04:00-Mon:05:00"
  apply_immediately         = false

  # Performance Insights + enhanced monitoring help debug
  # production incidents; cheap enough to enable from day 1.
  performance_insights_enabled    = true
  performance_insights_kms_key_id = aws_kms_key.rds.arn
  monitoring_interval             = 60
  monitoring_role_arn             = aws_iam_role.rds_monitoring.arn

  tags = {
    Name = "mahjong-${var.environment}"
  }

  # `final_snapshot_identifier` uses `timestamp()` which is an
  # impure function — telling terraform to ignore changes here
  # prevents spurious plan diffs on every `terraform plan` run.
  lifecycle {
    ignore_changes = [final_snapshot_identifier, password]
  }
}

# Enhanced-monitoring role — RDS assumes it to publish metrics to
# CloudWatch every 60 s.
data "aws_iam_policy_document" "rds_monitoring_assume" {
  statement {
    actions = ["sts:AssumeRole"]
    principals {
      type        = "Service"
      identifiers = ["monitoring.rds.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "rds_monitoring" {
  name               = "mahjong-${var.environment}-rds-monitoring"
  assume_role_policy = data.aws_iam_policy_document.rds_monitoring_assume.json
}

resource "aws_iam_role_policy_attachment" "rds_monitoring" {
  role       = aws_iam_role.rds_monitoring.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonRDSEnhancedMonitoringRole"
}
