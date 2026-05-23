# Apone — Phase K Wave 6 decision memo

> Author: Apone (DevOps)
> Date: 2026-06-04
> Branch: `stlong/phase-k-wave-6-bringup`

## Mission

Continue the Wave-5 platform-bringup work into Wave-6 by **(a)**
adding multi-region DR (us-east-1 primary → us-west-2 warm pair)
to the Terraform module set, **(b)** narrowing the GitHub-Actions
OIDC role to least-privilege (Wave-5 left it broad for
bootstrap), **(c)** shipping the production-shape coturn k8s
manifests with `NetworkPolicy` covering the relay port range,
**(d)** tuning the container-scan severity threshold + adding a
CVE allowlist with hard 30-day expiry, **(e)** wiring tag-driven
mobile internal-testing promotion to TestFlight + Play Console,
and **(f)** adding a pre-merge SLSA-verifier gate on PRs that
carry the `deploy:prod` label — belt-and-suspenders for the
Wave-5 Kyverno `attestations:` block.

## Decisions

### 1. DR module — separate from primary, two provider aliases

**Decision.** Add `infra/terraform/modules/dr-replication/` (a
reusable module) and `infra/terraform/envs/dr-us-west-2/` (a
secondary-region env that instantiates it). The module accepts
TWO AWS provider aliases (`aws.primary` + `aws.secondary`) via a
`configuration_aliases` block; the env passes
`provider "aws" { alias = "primary"; region = "us-east-1" }` and
`provider "aws" { alias = "secondary"; region = "us-west-2" }`
explicitly. Every resource inside the module specifies its
provider explicitly — no default-provider fall-through.

**Alternatives considered.**
- **One terraform stack covering both regions.** Rejected:
    blast radius of a `terraform apply` mistake doubles; state
    file size doubles; backend bootstrap is region-coupled.
- **Module accepts a single provider; pick region via input
    variable.** Rejected: cross-region resources (RDS replica
    sourcing from primary, ECR replication rule, Route 53
    health check against primary endpoint) genuinely require
    TWO providers active simultaneously. Single-provider would
    force every cross-region operation into a hand-rolled
    `aws_*` data source workaround.
- **Pre-create the us-west-2 ECR repo via terraform.** Rejected:
    ECR replication auto-creates the destination repo on the
    first replication event. Pre-creating is a no-op that adds
    drift risk if the replication-created repo's tags diverge.

**Trade-offs.**
- The DR env reads primary stack outputs via
    `terraform_remote_state`. Adds a coupling between the two
    backends but keeps the primary DB ARN + KMS ARN from being
    hand-plumbed (and going stale if either is replaced).
- RDS cross-region replicas need their own KMS CMK in the
    secondary region (AWS forbids cross-region CMK reuse). The
    `envs/dr-us-west-2/main.tf` provisions a dedicated CMK.
    Operator must update `db_replica_kms_key_arn` consumers if
    the CMK is ever rotated.

### 2. Route 53 failover with TTL < 60s — pinned via variable validator

**Decision.** The DR module's `aws_route53_record` failover pair
uses TTL = 30 seconds, pinned by a variable validator (`condition =
var.failover_record_ttl < 60`). The single FQDN is shared between
PRIMARY and SECONDARY records; AWS resolves to PRIMARY while the
health check is green and switches to SECONDARY on health-check
trip.

**Why TTL < 60s.** With TTL = 30s and a typical 30s recursive
resolver cache + 30s health-check evaluation period + 30s DNS
propagation, the worst-case time-to-cut on a clean failover is
~90s; first successful `/health` 200 from us-west-2 within ~2
min total. The 5-min SLO documented in `docs/terraform.md` §4.5
has 3x headroom against this worst case.

**Why variable validator instead of hardcoded.** A future
operator may want to raise the TTL for cost reasons (Route 53
charges per query); the validator makes 60s the absolute
ceiling. Raising it past 60s requires editing the module
itself, which forces an audit conversation.

### 3. GitHub-OIDC narrowing — eight ECR verbs, push-only, repo-scoped

**Decision.** `ecr:*` in the inline GitHub-Actions deploy role
narrowed to EIGHT discrete verbs that `docker push` actually
invokes:
`BatchCheckLayerAvailability`,
`BatchGetImage`,
`CompleteLayerUpload`,
`InitiateLayerUpload`,
`PutImage`,
`UploadLayerPart`,
`GetAuthorizationToken` (must be on `*` — AWS API constraint),
`DescribeRepositories` (idempotency check for the push step).
All except `GetAuthorizationToken` scoped to the repository ARN
(`arn:aws:ecr:<region>:<account>:repository/mahjong-autotable`).
`ssm:Get*` narrowed to `ssm:GetParameter` ONLY, scoped to
`arn:aws:ssm:<region>:<account>:parameter/mahjong/<env>/*`.
`iam:PassRole` introduced as an OPT-IN dynamic block guarded by
a `Condition: { StringEquals: { iam:PassedToService: [<services>] } }`.

**Alternatives considered.**
- **Keep W5's `ecr:*` for ease.** Rejected: `ecr:*` includes
    destructive verbs (`DeleteRepository`, `BatchDeleteImage`)
    that have no business in a deploy role.
- **Use AWS-managed `AmazonEC2ContainerRegistryPowerUser`.**
    Rejected: still includes `BatchDeleteImage`. Also pinning
    against an AWS-managed policy means the role's effective
    grants can change underneath us when AWS updates the
    managed policy.
- **`ssm:GetParameters` (plural) and `ssm:GetParametersByPath`.**
    Rejected: the deploy workflow fetches keys one at a time;
    `GetParameter` is sufficient. Adding `ByPath` would
    accidentally enable bulk enumeration.

**Trade-offs.**
- A future deploy workflow that needs to `ssm:GetParameters`
    (batch lookup) MUST widen the policy + document why in
    `modules/github-oidc/least-privilege.tf`. Friction is
    intentional.
- The `iam:PassRole` block ships in fenced form (opt-in)
    rather than as a hardcoded `Effect: Deny`. A future env that
    needs to pass a role to e.g. an EKS service account can
    flip the variable; explicit beats implicit.

### 4. Coturn manifests parallel-named to W2, not replacing

**Decision.** The W6 coturn manifests are named
`coturn-deployment.yaml`, `coturn-configmap.yaml`,
`coturn-secret.yaml` and produce resources prefixed `coturn-` (NOT
`turn-server-`). The W2 `turn-server.yaml` resources remain
untouched in this wave.

**Alternatives considered.**
- **In-place rewrite of `turn-server.yaml`.** Rejected:
    in-place changes to a deployed Service breaks the cutover.
    A blue-green migration needs both deployments live
    simultaneously so traffic can be cut over via DNS / load
    balancer routing without an outage window.
- **Rename W2 resources first, then ship W6.** Rejected: the
    rename is itself a cutover that requires an outage window.

**Trade-offs.**
- Two coturn deployments in prod during the cutover window
    (24-48 h typically). Slight cost increase; the parallel
    capacity is also a load shedding cushion if the cutover
    has issues.
- Operator MUST decommission the W2 `turn-server.yaml`
    resources after the 24-h cooldown. Documented in
    `docs/turn-server-setup.md` §9.

**HMAC mode + relay port range.** W6 manifests pin
`use-auth-secret` + `lt-cred-mech` (Bishop's W3 `/api/turn`
endpoint already mints HMAC credentials with the same key) and
expose the IANA ephemeral range 49152-65535 UDP for relay. The
NetworkPolicy admits this range; egress is wide-open (TURN's
job is to NAT-traverse to arbitrary peers).

### 5. Trivy allowlist with 30-day expiry cap — workflow-enforced

**Decision.** `.github/trivy-allowlist.yaml` carries the schema
(`allowed: [<entries>]`; every entry MUST have `id` +
`justification` + `added` + `expires`); the
`.github/workflows/container-scan.yml` `allowlist-check` job
FAILS the workflow on any entry with `expires` in the past OR
`expires` > 30 days from today.

**Why fail on future-too-far-out and not just past-expiry.**
Allowing a 6-month expiry would silently let allowlist entries
go stale; forcing 30-day renewal means every CVE allowance is
re-justified monthly, which catches "we forgot to upgrade the
base image" sooner.

**Rendered to `.trivyignore` at scan time.** The trivy CLI
doesn't natively consume a YAML allowlist; the workflow renders
the YAML to Trivy's native `.trivyignore` format before invoking
trivy. The YAML stays the single source of truth (human-readable
justification per entry); `.trivyignore` is regenerated each run.

**Ship empty.** Wave 6 ships with `allowed: []`. Establishing
the SCHEMA is more important than seeding entries — the first
real entry will go through CR. The schema baseline means
W7+ can land allowance entries via narrow PR diffs.

### 6. SLSA verifier pre-merge — `deploy:prod` label only

**Decision.** `.github/workflows/verify-slsa-on-deploy.yml`
triggers on PR `labeled` / `synchronize` / `reopened` events;
a `gate` job short-circuits unless the PR carries the
`deploy:prod` label.

**Why label-gated and not all-PRs.** SLSA verification fetches a
sigstore certificate + Rekor entry — network calls + non-trivial
runtime. Running on every PR (including dependency-update PRs
that don't touch deploy artefacts) burns runner minutes without
signal.

**Why `deploy:prod` specifically.** Prod is where the Kyverno
admission policy fires. Pre-merge verification ensures the
SLSA-attestation gate at admission time will NOT fail on the
post-merge deploy. Staging admission gate is intentionally
weaker so staging can experiment.

**Belt-AND-suspenders rationale.** The SAME `slsa-verifier`
binary runs in two places:
1. **CI pre-merge** (this workflow) — verifies the predicate
    before the policy is even touched.
2. **Admission time** (the W6+ webhook bundling), and in any
    case via Kyverno's cosign-via-policy integration which
    references the same predicate.

A regression in either layer is caught by the other. A future
Kyverno or cosign upstream regression would fail admission;
the pre-merge gate would have caught it before merge.

### 7. Mobile internal-testing tag prefix `mobile-v*.*.*`

**Decision.** Mobile releases use a DISTINCT tag prefix
(`mobile-v1.0.0`, etc.) from backend releases (`v0.15.0`, etc.).
The mobile workflow's `on: push: tags:` filter is
`mobile-v*.*.*`; backend release workflows filter `v*.*.*` and
intentionally do NOT match the mobile prefix.

**Why distinct prefixes.** Mobile and backend release cadences
will diverge; backend ships per-wave (currently roughly weekly),
mobile ships less frequently (Apple/Google review cycles + tester
feedback windows). Sharing a tag prefix would force every backend
tag to trigger a mobile build (~20 min, costly) or every mobile
tag to trigger a backend release (semantically wrong).

**Why the order matters.** The filter `v*.*.*` does NOT match
`mobile-v*.*.*` (no glob prefix match) — so backend workflows
correctly ignore mobile tags. The reverse — `mobile-v*.*.*`
doesn't match `v*.*.*` either — so mobile workflows correctly
ignore backend tags. Both filters can coexist on the same repo
without ambiguity.

## Lock-step invariants

### 6.1 Signer-URL canonical list (carried over from W5, unchanged)

Six files must update together if `sign-image.yml` OR
`slsa-provenance.yml` is renamed. List unchanged from W5 memo
§"Lock-step invariants" (no signer-URL changes in W6).

### 6.2 OIDC policy + least-privilege rationale — NEW W6 lock-step

Two files (each a `.tf` file in `modules/github-oidc/`):

1. `modules/github-oidc/main.tf` — the inline
    `github_deploy_inline` policy.
2. `modules/github-oidc/least-privilege.tf` — the rationale
    document (no resources, pure comments + a single
    `aws_iam_policy_document data source` that goes UNUSED but
    captures the rationale-by-action mapping).

Rule: ANY widening of the policy in `main.tf` (additional
verbs, additional resources, conditions weakened) MUST land
alongside an updated rationale paragraph in
`least-privilege.tf` IN THE SAME COMMIT. A widening without a
rationale update is a review-blocker.

This is the SAME pattern as W5's six-file signer list (one
shared edit unit across multiple files) but with a tighter
two-file scope.

### 6.3 Trivy allowlist + workflow check — NEW W6 lock-step

Two files:

1. `.github/trivy-allowlist.yaml` — the entries.
2. `.github/workflows/container-scan.yml` `allowlist-check`
    job — the schema enforcement.

Rule: any schema change to the YAML (new required field,
relaxed cap) MUST land alongside the matching update to the
`allowlist-check` job's validation logic IN THE SAME COMMIT.
Otherwise the workflow either silently passes invalid entries
or rejects valid ones.

## Hand-off

### To Bishop (backend, W6)

- The `coturn-static-auth-secret` ExternalSecret sources from
    SSM `/mahjong/<env>/turn/auth_secret`. Your `/api/turn`
    HMAC credential minting code path MUST read the SAME SSM
    parameter (you do this in W3 — confirm it survives the W6
    operator-driven rotation cadence; one rotation rolls
    coturn + your endpoint atomically).
- The mobile internal-testing workflow expects the autotable
    frontend bundle to build via `npm ci && npm run build` from
    repo root. If you change the build command, the
    `build-web-bundle` job in
    `.github/workflows/mobile-internal-testing.yml` needs the
    matching change.

### To Vasquez (audit)

- New audit anchors from W6:
    - `modules/github-oidc/least-privilege.tf` — the rationale
        document. Cite this when reviewing the policy in
        `main.tf`; the two files are a lock-step pair.
    - `docs/terraform.md` §4 — DR rehearsal report template.
        The first quarterly DR rehearsal report will land in
        `docs/retro-2026-06.md` §3a.
    - The SLSA-verifier `--print-provenance` output captured
        as a workflow artefact on every `deploy:prod` PR
        (30-day retention). Citable per-PR for audit trail.
- The signer-identity canonical list is UNCHANGED from W5
    (no signer-URL changes in W6). Six files still.

### To Operator (Stephen)

- **DR env bootstrap.** Per `docs/terraform.md` §2:
    - One-time: create the secondary S3 state bucket +
        DynamoDB lock table in us-west-2.
    - `terraform init -backend-config=backend-dr-us-west-2.hcl`
        in `envs/dr-us-west-2/`.
    - `terraform plan -var-file=terraform.tfvars` — review
        carefully (DR is destructive on apply if mis-configured).
    - `terraform apply`. Expect ~25-30 min (RDS replica init
        dominates).
- **First DR rehearsal.** Quarterly cadence; first one is
    due by 2026-06-30. Runbook in `docs/terraform.md` §4. Report
    in `docs/retro-2026-06.md` §3a.
- **OIDC narrowing apply.** `terraform plan -var-file=prod.tfvars`
    in `infra/terraform/` will show DELETIONS of the W5-broad
    statements + ADDITIONS of the W6-narrowed ones. Review
    carefully before apply. Outputs unchanged (role ARN
    stable; only inline policy changes).
- **Coturn cutover.**
    1. `kubectl apply -k infra/k8s/overlays/<env>/`
        (W6 manifests land alongside W2; both deployments active).
    2. Smoke-test W6 endpoint with `turnutils_uclient` per
        `docs/turn-server-setup.md` §9.4.
    3. Cut DNS / LB routing from W2 to W6 (operator-driven,
        controlled).
    4. 24 h cooldown.
    5. `kubectl delete -f infra/k8s/base/turn-server.yaml`
        (W2 resources gone).
- **Mobile signing setup.** Per `docs/mobile-release.md` §3.
    Provision App Store Connect API key, distribution `.p12`,
    provisioning profile UUID, Play keystore + service-account
    JSON, Slack webhook. Set repo secrets. Tag a
    `mobile-v0.1.0-rc1` to validate the workflow before the
    first real release.
- **Container-scan allowlist.** Currently empty; expected to
    stay empty unless a specific CVE-with-no-fix-available
    needs a documented 30-day window. Any allowance entry MUST
    carry `justification` + `expires` ≤ 30d.

### To future Phase K wave (W7+)

- **Helm chart-of-charts** for post-bootstrap add-ons
    (ESO / cert-manager / AWS-LBC / Kyverno) — idempotent install
    ordering, single `helm install` command. W5 deferred to W6,
    W6 deferred to W7 — increasingly overdue.
- **Route 53 + ACM + WAF terraform module.** Domain-bound;
    ship once `mahjong.example.com` (or whatever the real
    domain is) is registered.
- **Signature-preserving GHCR→ECR mirror workflow.** Use
    `crane copy` / `cosign copy`. The naive `docker pull && docker push`
    breaks cosign + SLSA. Documented in
    `infra/terraform/README.md` §4 as a known gap.
- **External-Testing promotion automation.** W6 stops at
    Internal Testing (TestFlight + Play Internal). The
    promote-to-external step should be a separate
    `workflow_dispatch`-only workflow with approvals (don't
    auto-promote on Internal Testing soak alone).
- **Image-mirror signature preservation** — see above; this
    item is repeated because it is both a W7 deliverable and a
    cross-cutting supply-chain concern.
- **Pre-commit hook for the six-file signer-URL lock-step.**
    Grep for the canonical URL; fail if any one file drifts.

## Tests + linters run

- `actionlint` (v1.7.7) on
    `.github/workflows/container-scan.yml` (modified),
    `.github/workflows/mobile-internal-testing.yml` (NEW),
    `.github/workflows/verify-slsa-on-deploy.yml` (NEW) →
    all clean.
- `python3 -c "import yaml; yaml.safe_load_all(...)"` on
    `infra/k8s/base/coturn-{deployment,configmap,secret}.yaml`,
    `.github/trivy-allowlist.yaml` → all parse clean.
- `terraform fmt -recursive` on `infra/terraform/` → applied.
- `terraform validate` (v1.9.8) on `infra/terraform/` (primary
    stack) and on `infra/terraform/envs/dr-us-west-2/` → both
    `Success! The configuration is valid.`
- `bash -n` on inline shell in all new workflows → clean.
- Backend gate: baseline 1345 / 0 / 0 from Wave 5 preserved
    (`src/backend/**` untouched).

## Lock-step with W5 git-config race incident

Every commit in this wave uses the W6 mitigation pattern
documented in `docs/retro-2026-05.md` §3.1: per-invocation
`git -c user.name="Apone (DevOps)" -c user.email="apone@squad.mahjong" commit`
inside a `flock`-wrapped commit + push pair, preceded by
`git status --short | head -20` to verify only DevOps-lane
paths are staged. Author verified post-commit with
`git log -1 --pretty='%an <%ae>'`.
