# Phase K Wave 5 — Apone (DevOps).
# Phase K Wave 6 — Apone (DevOps) — narrowed.
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
# ## Wave 6 — least-privilege narrowing
#
# Wave 5 shipped the role with broad `ecr:*` + `ssm:Get*` grants
# for bootstrap velocity. Wave 6 narrows each grant to the
# minimum API set the deploy workflow actually exercises. See
# `modules/github-oidc/least-privilege.tf` for the per-action
# rationale; the policy below mirrors that documentation.

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

# Phase K Wave 6 least-privilege policy — replaces the Wave-5
# bootstrap policy. See `modules/github-oidc/least-privilege.tf`
# for the per-action rationale; this file is the active grant.
data "aws_iam_policy_document" "github_deploy_inline" {
  # ECR push — the eight discrete actions a `docker push` to ECR
  # actually invokes. No read-side (`Describe*`, `List*`) — the
  # deploy workflow does not pull images, it only pushes; the
  # nightly mirror to GHCR uses a separate cross-account role.
  statement {
    sid    = "ECRPushNarrow"
    effect = "Allow"
    actions = [
      # Authentication: required by `aws ecr get-login-password`.
      "ecr:GetAuthorizationToken",
      # Layer-level push handshake — the eight actions docker
      # daemon calls in sequence during a push to ECR.
      "ecr:BatchCheckLayerAvailability",
      "ecr:GetDownloadUrlForLayer",
      "ecr:BatchGetImage",
      "ecr:PutImage",
      "ecr:InitiateLayerUpload",
      "ecr:UploadLayerPart",
      "ecr:CompleteLayerUpload",
    ]
    # `GetAuthorizationToken` needs `*` (it's a non-resource action
    # per the AWS docs). The remaining seven actions are resource-
    # scoped to the mahjong-autotable repository ARN.
    resources = ["*"]
  }

  # ECR repo-scoped: PutImage / *LayerUpload accept a repo ARN.
  # We keep the `*` resource on the top statement above so
  # `GetAuthorizationToken` works; this second statement adds
  # the resource-scoped denial-by-omission for the per-repo
  # subset (NotResource pattern is fragile — explicit resource
  # whitelist is the safer form).
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

  # EKS describe — read-only; required by `aws eks update-kubeconfig`
  # in the deploy workflow.
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
    sid    = "STSCallerIdentity"
    effect = "Allow"
    actions = [
      "sts:GetCallerIdentity",
    ]
    resources = ["*"]
  }

  # SSM read — Wave 5 was `ssm:Get*` (covers DescribeParameters,
  # GetParameters, GetParameterHistory, etc.). Wave 6 narrows to
  # `GetParameter` only — the single API the deploy workflow
  # uses (via ESO + the smoke-test scripts). Resource ARN is
  # scoped to `/mahjong/{env}/*` so the deploy role cannot read
  # any other org-namespaced parameter.
  statement {
    sid    = "SSMGetMahjongParam"
    effect = "Allow"
    actions = [
      "ssm:GetParameter",
    ]
    resources = [
      "arn:aws:ssm:${var.region}:${data.aws_caller_identity.current.account_id}:parameter/mahjong/${var.environment}/*",
    ]
  }

  # PassRole — Wave 5 had no PassRole grant. Wave 6 adds an
  # EXPLICIT, narrow grant for `aws eks update-kubeconfig` /
  # IRSA-bound service operations: the role can only PassRole on
  # the specific deployment-time roles named in
  # `var.passrole_target_roles` (defaults to none — operator
  # opts in per env tfvars). Without this guard, a future grant
  # that uses `iam:PassRole = "*"` would be a privilege
  # escalation vector.
  dynamic "statement" {
    for_each = length(var.passrole_target_roles) > 0 ? [1] : []
    content {
      sid    = "PassRoleNarrow"
      effect = "Allow"
      actions = [
        "iam:PassRole",
      ]
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
