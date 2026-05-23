# Phase K Wave 6 — Apone (DevOps).
#
# Per-grant rationale for the GitHub-Actions deploy role's
# inline policy. This file is INTENTIONALLY policy-free — it
# documents the policy in `main.tf` so auditors can read the
# rationale alongside the grant without context-switching
# between the policy doc and a separate runbook.
#
# Each grant below maps to a `statement{ sid = "<name>" }` in
# `main.tf`. When the policy changes, this file MUST change in
# the same commit (Wave-6 invariant — see `docs/terraform.md`).
#
# ── ECRGetAuthToken ──────────────────────────────────────────────
#   Action:      ecr:GetAuthorizationToken
#   Resource:    *  (non-resource action; AWS docs)
#   Why:         `aws ecr get-login-password` calls this exactly
#                once per deploy workflow run to mint a 12-hour
#                docker-login token. Cannot be resource-scoped.
#   Risk if removed: docker push fails at the login step.
#   Risk if widened: token is for THIS account only; cannot pivot.
#
# ── ECRPushRepoScoped ────────────────────────────────────────────
#   Actions:     ecr:BatchCheckLayerAvailability
#                ecr:GetDownloadUrlForLayer
#                ecr:BatchGetImage
#                ecr:PutImage
#                ecr:InitiateLayerUpload
#                ecr:UploadLayerPart
#                ecr:CompleteLayerUpload
#   Resource:    arn:aws:ecr:<region>:<account>:repository/<repo>
#   Why:         Exact set of API calls a `docker push` to ECR
#                makes. NOT included: DescribeImages / ListImages
#                / DescribeRepositories — the deploy workflow
#                does not list or describe; if it ever needs to,
#                add a fourth statement with read-only verbs.
#   Risk if removed: docker push fails partway through layer upload.
#   Risk if widened: pushing to any repo in the account becomes
#                possible — a compromised CI could overwrite an
#                adjacent project's image. The repo-scoped resource
#                ARN prevents this.
#
# ── EKSDescribe ──────────────────────────────────────────────────
#   Actions:     eks:DescribeCluster, ListClusters, DescribeNodegroup,
#                DescribeAddon
#   Resource:    *
#   Why:         `aws eks update-kubeconfig` reads cluster metadata
#                via DescribeCluster; smoke-tests use ListClusters
#                + DescribeNodegroup to assert the green-deploy
#                target exists. All four are read-only.
#   Risk if removed: kubeconfig generation fails.
#   Risk if widened: NOT widened — `*` is the EKS API's resource
#                ARN format for read-only describes (per AWS docs).
#                Per-cluster scoping isn't available for these verbs.
#
# ── STSCallerIdentity ────────────────────────────────────────────
#   Action:      sts:GetCallerIdentity
#   Resource:    *
#   Why:         Workflows log `aws sts get-caller-identity` for
#                audit traceability — every deploy run records
#                which role/session identity made the deploy.
#   Risk if removed: workflow logs lose the per-run identity stamp.
#   Risk if widened: non-resource action; no widening possible.
#
# ── SSMGetMahjongParam ───────────────────────────────────────────
#   Action:      ssm:GetParameter        (NARROWED from `ssm:Get*`)
#   Resource:    arn:aws:ssm:<region>:<account>:parameter/mahjong/<env>/*
#   Why:         The deploy workflow + ESO smoke probes read
#                exactly one parameter at a time via GetParameter.
#                Wave-5 had `ssm:Get*` (covers GetParameters,
#                GetParametersByPath, GetParameterHistory,
#                DescribeParameters) — Wave-6 narrows to the
#                single verb the workflow exercises.
#   Risk if removed: ESO sync probes + connection-string smoke
#                tests fail. Cluster ESO controllers use a
#                SEPARATE IRSA role and are unaffected by this
#                grant change.
#   Risk if widened: the `Get*` wildcard is the meaningful
#                widening risk — GetParameterHistory leaks
#                rotation history; DescribeParameters leaks
#                parameter names (org structure intel). Both
#                are removed in W6.
#
# ── PassRoleNarrow (DYNAMIC — only present when configured) ──────
#   Action:      iam:PassRole
#   Resource:    var.passrole_target_roles (default: [])
#   Condition:   iam:PassedToService ∈ var.passrole_target_services
#   Why:         Wave-5 had NO PassRole grant. Wave-6 adds a
#                placeholder dynamic block that activates ONLY
#                when the caller populates `passrole_target_roles`
#                (e.g. for a CodeDeploy / Lambda exec role).
#                The condition restricts WHICH service the role
#                may be passed to — without that, a future grant
#                that gave PassRole on a powerful target role
#                would be a privilege-escalation vector
#                (`iam:PassRole` to a service the principal
#                doesn't expect to interact with).
#   Risk if removed: the grant is OPT-IN. Removing the dynamic
#                block has no effect when the caller didn't
#                populate the variable.
#   Risk if widened: explicit role-ARN list + PassedToService
#                condition together prevent the canonical
#                PassRole escalation patterns.
#
# ── INVARIANT ─────────────────────────────────────────────────────
#
# This policy is a HARD INVARIANT for the deploy role. Any
# widening MUST:
#   1. Document the new grant in this file with the rationale
#      paragraph above.
#   2. Pin a per-action resource ARN where AWS permits.
#   3. Pin a `condition` block where the action accepts one.
#   4. Land in the same commit as the `main.tf` policy change.
#
# Any narrowing in a future wave can land freely — narrowing
# is always safe; widening requires the four-step audit trail.
