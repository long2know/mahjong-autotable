# Phase K Wave 5 — Apone (DevOps).
#
# IAM OIDC federation between GitHub Actions and this AWS account.
# Creates:
#
#   * `aws_iam_openid_connect_provider.github` — the OIDC provider
#     pointing at `token.actions.githubusercontent.com`.
#   * `aws_iam_role.github_deploy` — the role GitHub Actions
#     workflows assume via `aws-actions/configure-aws-credentials`.
#
# Trust policy is scoped to `repo:${var.github_org}/${var.github_repo}`
# AND to `ref:refs/heads/main` OR `ref:refs/tags/v*` (configurable
# via var.github_oidc_subjects). Any PR / fork / branch attempt
# to assume the role fails at the sts.AssumeRoleWithWebIdentity
# layer — before any AWS API call ever executes.
#
# The role's policy is intentionally broad for the bootstrap so
# `terraform apply` + helm-install + kubectl can all run from CI.
# Tighten to least-privilege as the deploy workflow stabilises
# (Wave-6 audit hardening).

# GitHub Actions OIDC thumbprint — the SHA-1 of the leaf cert
# from https://token.actions.githubusercontent.com/.well-known/openid-configuration.
# This MUST be kept up to date if GitHub rotates the cert.
data "tls_certificate" "github_oidc" {
  url = "https://token.actions.githubusercontent.com"
}

resource "aws_iam_openid_connect_provider" "github" {
  url             = "https://token.actions.githubusercontent.com"
  client_id_list  = ["sts.amazonaws.com"]
  thumbprint_list = [data.tls_certificate.github_oidc.certificates[0].sha1_fingerprint]

  tags = {
    Name = "github-actions-oidc"
  }
}

data "aws_iam_policy_document" "github_deploy_assume" {
  statement {
    actions = ["sts:AssumeRoleWithWebIdentity"]
    principals {
      type        = "Federated"
      identifiers = [aws_iam_openid_connect_provider.github.arn]
    }
    condition {
      test     = "StringEquals"
      variable = "token.actions.githubusercontent.com:aud"
      values   = ["sts.amazonaws.com"]
    }
    # Subject pin — `sub` claim must match the github_oidc_subjects
    # list. The list of patterns supports `*` wildcards (StringLike).
    condition {
      test     = "StringLike"
      variable = "token.actions.githubusercontent.com:sub"
      values   = var.github_oidc_subjects
    }
  }
}

resource "aws_iam_role" "github_deploy" {
  name                 = "mahjong-${var.environment}-github-deploy"
  description          = "Role assumed by GitHub Actions for ${var.github_org}/${var.github_repo} (env=${var.environment})"
  assume_role_policy   = data.aws_iam_policy_document.github_deploy_assume.json
  max_session_duration = 3600

  tags = {
    Name = "mahjong-${var.environment}-github-deploy"
  }
}

# Broad bootstrap policy — gives the role enough to run
# `terraform apply` + EKS kubectl + ECR push. Tighten in
# Wave-6 once the deploy surface is stable.
data "aws_iam_policy_document" "github_deploy_inline" {
  statement {
    sid = "ECRPush"
    actions = [
      "ecr:GetAuthorizationToken",
      "ecr:BatchCheckLayerAvailability",
      "ecr:BatchGetImage",
      "ecr:CompleteLayerUpload",
      "ecr:DescribeImages",
      "ecr:DescribeRepositories",
      "ecr:GetDownloadUrlForLayer",
      "ecr:InitiateLayerUpload",
      "ecr:ListImages",
      "ecr:PutImage",
      "ecr:UploadLayerPart",
    ]
    resources = ["*"]
  }

  statement {
    sid = "EKSDescribe"
    actions = [
      "eks:DescribeCluster",
      "eks:ListClusters",
      "eks:DescribeNodegroup",
      "eks:DescribeAddon",
    ]
    resources = ["*"]
  }

  statement {
    sid = "STSCallerIdentity"
    actions = [
      "sts:GetCallerIdentity",
    ]
    resources = ["*"]
  }

  # Read SSM parameters under the mahjong/* root so ESO + the
  # deploy workflow can confirm parameter existence. NO write
  # — parameter writes are operator-only via aws CLI.
  statement {
    sid = "SSMReadMahjongParams"
    actions = [
      "ssm:GetParameter",
      "ssm:GetParameters",
      "ssm:GetParametersByPath",
      "ssm:DescribeParameters",
    ]
    resources = [
      "arn:aws:ssm:${var.region}:${data.aws_caller_identity.current.account_id}:parameter/mahjong/*",
    ]
  }
}

resource "aws_iam_role_policy" "github_deploy" {
  name   = "mahjong-${var.environment}-github-deploy"
  role   = aws_iam_role.github_deploy.id
  policy = data.aws_iam_policy_document.github_deploy_inline.json
}
