# Phase K Wave 5 — Apone (DevOps).
#
# ECR repository for the mahjong-autotable container image.
#
# The current production deploy uses GHCR (ghcr.io/long2know/...).
# This ECR repository is the AWS-native mirror so an isolated /
# air-gapped AWS account that cannot reach ghcr.io directly can
# still pull images. The mirror pipeline is documented in the
# README §4; for the bootstrap, the repo + lifecycle policy are
# the necessary resources, the mirror script is operator-driven.

resource "aws_ecr_repository" "mahjong" {
  name                 = var.ecr_repository_name
  image_tag_mutability = "MUTABLE"
  force_delete         = false

  image_scanning_configuration {
    scan_on_push = true
  }

  encryption_configuration {
    encryption_type = "AES256"
  }

  tags = {
    Name = var.ecr_repository_name
  }
}

# Lifecycle policy:
#   * Keep the most-recent N tagged images (default N=30).
#   * Expire untagged images after 14 days (rebuild churn).
#
# AWS evaluates rules in `rulePriority` order (lowest first); the
# first match wins. We put the untagged-expiry rule FIRST (priority
# 1) so an untagged layer doesn't slip through the keep-last-N
# count (which only sees tagged images).
resource "aws_ecr_lifecycle_policy" "mahjong" {
  repository = aws_ecr_repository.mahjong.name

  policy = jsonencode({
    rules = [
      {
        rulePriority = 1
        description  = "Expire untagged images after 14 days"
        selection = {
          tagStatus   = "untagged"
          countType   = "sinceImagePushed"
          countUnit   = "days"
          countNumber = 14
        }
        action = { type = "expire" }
      },
      {
        rulePriority = 2
        description  = "Keep last ${var.ecr_keep_last_n_images} tagged images"
        selection = {
          tagStatus      = "tagged"
          tagPatternList = ["*"]
          countType      = "imageCountMoreThan"
          countNumber    = var.ecr_keep_last_n_images
        }
        action = { type = "expire" }
      },
    ]
  })
}
