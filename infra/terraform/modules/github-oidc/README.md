# `github-oidc/` — GitHub Actions OIDC federation module

> Phase K Wave 6 — Apone (DevOps).

Reusable Terraform module that provisions the GitHub-Actions OIDC
provider (when not already present in the account) plus the IAM role
GitHub Actions assumes via `aws-actions/configure-aws-credentials`.

The role's inline policy is the **Wave-6 least-privilege grant set**;
see `least-privilege.tf` in this directory for the per-action
rationale.

## Usage

```hcl
module "deploy_role" {
  source = "../../modules/github-oidc"

  environment         = "staging"
  region              = "us-east-1"
  github_org          = "long2know"
  github_repo         = "mahjong-autotable"
  ecr_repository_name = "mahjong-autotable"

  # By default the module creates a new OIDC provider; if the
  # account already has one (the primary env's `iam-github-oidc.tf`
  # creates it), set:
  create_oidc_provider       = false
  existing_oidc_provider_arn = "arn:aws:iam::<account>:oidc-provider/token.actions.githubusercontent.com"

  github_oidc_subjects = [
    "repo:long2know/mahjong-autotable:ref:refs/heads/main",
    "repo:long2know/mahjong-autotable:ref:refs/tags/v*",
    "repo:long2know/mahjong-autotable:environment:staging",
  ]

  # Opt-in PassRole grant if the env needs to pass a deployment
  # role to a service.
  passrole_target_roles    = []
  passrole_target_services = ["eks.amazonaws.com"]
}
```

## What the policy grants (W6 least-privilege)

| Statement | Action(s) | Resource | Notes |
|-----------|-----------|----------|-------|
| `ECRGetAuthToken` | `ecr:GetAuthorizationToken` | `*` | Non-resource action. |
| `ECRPushRepoScoped` | 7 × layer-push verbs | repo ARN | Exactly the calls `docker push` makes. |
| `EKSDescribe` | 4 × read-only describes | `*` | EKS doesn't support per-cluster scoping for describes. |
| `STSCallerIdentity` | `sts:GetCallerIdentity` | `*` | Audit identity stamp. |
| `SSMGetMahjongParam` | `ssm:GetParameter` (no wildcard) | `parameter/mahjong/<env>/*` | Narrowed from W5's `ssm:Get*`. |
| `PassRoleNarrow` | `iam:PassRole` | per-env opt-in | Conditioned on `iam:PassedToService`. |

## Where it's used

* `infra/terraform/iam-github-oidc.tf` — primary env's W5-in-place
  narrowed copy (kept flat for `terraform plan` continuity; the
  policy is identical).
* Future envs (staging, dr-*) — call this module for the canonical
  reusable form.
