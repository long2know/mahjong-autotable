# Phase K Wave 6 — Apone (DevOps).
#
# Reusable github-oidc module — provides the OIDC provider + the
# deploy role with the W6 least-privilege grants. New consumers
# (e.g. `envs/dr-us-west-2/` once the W6 helm CI lands) should
# instantiate this module instead of copy-pasting from
# `infra/terraform/iam-github-oidc.tf` (which is the W5 in-place
# narrowed copy — the deploy role for the primary env. Both
# converge to the same policy shape; the module is the canonical
# reusable form for future envs).
#
# See `least-privilege.tf` in this directory for the per-action
# rationale documentation that pairs with the policy below.

terraform {
  required_version = ">= 1.5.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.50"
    }
    tls = {
      source  = "hashicorp/tls"
      version = "~> 4.0"
    }
  }
}

data "aws_caller_identity" "current" {}

data "tls_certificate" "github_oidc" {
  url = "https://token.actions.githubusercontent.com"
}

resource "aws_iam_openid_connect_provider" "github" {
  count           = var.create_oidc_provider ? 1 : 0
  url             = "https://token.actions.githubusercontent.com"
  client_id_list  = ["sts.amazonaws.com"]
  thumbprint_list = [data.tls_certificate.github_oidc.certificates[0].sha1_fingerprint]

  tags = merge(var.tags, {
    Name = "github-actions-oidc"
  })
}

locals {
  # If the caller provided an existing OIDC-provider ARN, use it
  # (an account can only have one provider per issuer URL — re-creating
  # it would fail). Otherwise consume the one we just created.
  oidc_provider_arn = var.create_oidc_provider ? aws_iam_openid_connect_provider.github[0].arn : var.existing_oidc_provider_arn
}

data "aws_iam_policy_document" "github_deploy_assume" {
  statement {
    actions = ["sts:AssumeRoleWithWebIdentity"]
    principals {
      type        = "Federated"
      identifiers = [local.oidc_provider_arn]
    }
    condition {
      test     = "StringEquals"
      variable = "token.actions.githubusercontent.com:aud"
      values   = ["sts.amazonaws.com"]
    }
    condition {
      test     = "StringLike"
      variable = "token.actions.githubusercontent.com:sub"
      values   = var.github_oidc_subjects
    }
  }
}

resource "aws_iam_role" "github_deploy" {
  name                 = "mahjong-${var.environment}-github-deploy"
  description          = "Role assumed by GitHub Actions for ${var.github_org}/${var.github_repo} (env=${var.environment}) — W6 least-privilege"
  assume_role_policy   = data.aws_iam_policy_document.github_deploy_assume.json
  max_session_duration = 3600

  tags = merge(var.tags, {
    Name = "mahjong-${var.environment}-github-deploy"
  })
}

data "aws_iam_policy_document" "github_deploy_inline" {
  # ECR push (non-resource auth + resource-scoped layer push).
  statement {
    sid     = "ECRGetAuthToken"
    effect  = "Allow"
    actions = ["ecr:GetAuthorizationToken"]
    # GetAuthorizationToken is a NON-RESOURCE action per the AWS
    # docs; the ARN syntax is `*` (anything else triggers a Terraform
    # validation error at apply-time).
    resources = ["*"]
  }

  statement {
    sid    = "ECRPushRepoScoped"
    effect = "Allow"
    actions = [
      "ecr:BatchCheckLayerAvailability",
      "ecr:GetDownloadUrlForLayer",
      "ecr:BatchGetImage",
      "ecr:PutImage",
      "ecr:InitiateLayerUpload",
      "ecr:UploadLayerPart",
      "ecr:CompleteLayerUpload",
    ]
    resources = [
      "arn:aws:ecr:${var.region}:${data.aws_caller_identity.current.account_id}:repository/${var.ecr_repository_name}",
    ]
  }

  statement {
    sid    = "EKSDescribe"
    effect = "Allow"
    actions = [
      "eks:DescribeCluster",
      "eks:ListClusters",
      "eks:DescribeNodegroup",
      "eks:DescribeAddon",
    ]
    resources = ["*"]
  }

  statement {
    sid       = "STSCallerIdentity"
    effect    = "Allow"
    actions   = ["sts:GetCallerIdentity"]
    resources = ["*"]
  }

  statement {
    sid     = "SSMGetMahjongParam"
    effect  = "Allow"
    actions = ["ssm:GetParameter"]
    resources = [
      "arn:aws:ssm:${var.region}:${data.aws_caller_identity.current.account_id}:parameter/mahjong/${var.environment}/*",
    ]
  }

  dynamic "statement" {
    for_each = length(var.passrole_target_roles) > 0 ? [1] : []
    content {
      sid       = "PassRoleNarrow"
      effect    = "Allow"
      actions   = ["iam:PassRole"]
      resources = var.passrole_target_roles
      condition {
        test     = "StringEquals"
        variable = "iam:PassedToService"
        values   = var.passrole_target_services
      }
    }
  }
}

resource "aws_iam_role_policy" "github_deploy" {
  name   = "mahjong-${var.environment}-github-deploy"
  role   = aws_iam_role.github_deploy.id
  policy = data.aws_iam_policy_document.github_deploy_inline.json
}
