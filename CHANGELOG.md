# Changelog

All notable changes to `mahjong-autotable` are recorded here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
This project uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html);
each Phase J wave corresponds to a minor bump on the 0.x line. Phase K
opens at 0.10.0 (J shipped ten waves; the version number tracks the
wave count).

The list below was reconstructed retroactively at Wave 8 from the
merged-PR history (`gh pr list --base main --state merged --json
number,title,mergedAt`) plus the project's wave-decision memos in
`.squad/decisions/`. Phase J Waves 4–10 were back-filled at Phase K
Wave 1 (the Wave 8 backfill stopped at J3). Pre–Phase F entries are
summarised; the `mahjong-autotable` engine started life as a fork of
`pwmarcz/autotable` and only the deltas relevant to the Changsha
rebuild are tracked here.

## [Unreleased]

Working branch: `stlong/phase-k-wave-6-bringup`. Phase K Wave 6
in flight (DevOps lane shipping multi-region DR replication
module + GitHub-OIDC least-privilege narrowing + production
coturn k8s manifests with NetworkPolicy + Trivy severity-tuned
gate with allowlist + tag-driven mobile internal-testing
promotion + SLSA-verifier pre-merge gate on `deploy:prod` PRs).
Other lane deliverables outstanding.

## [0.15.0] — Phase K Wave 6 — 2026-06-04 (PR pending)

**Theme:** Multi-region DR (us-east-1 → us-west-2 warm pair) +
IAM least-privilege hardening + production coturn k8s data plane
+ Trivy severity-tuned gate (HIGH+CRITICAL block, 30-day
allowlist) + tag-driven mobile internal-testing promotion to
TestFlight + Play Console + SLSA-verifier pre-merge gate on
deploy:prod PRs.

### Added (Phase K Wave 6 — PR pending)
- **Terraform `modules/dr-replication/` — cross-region DR module.**
    New reusable module instantiated by the secondary-region env
    (`envs/dr-us-west-2/`). Wires three cross-region resources
    onto the existing single-region stack: (1) RDS Postgres
    cross-region read replica (`replicate_source_db` = primary
    ARN, replica encrypted with secondary-region KMS — AWS forbids
    cross-region CMK sharing, so the secondary env's `main.tf`
    provisions its own CMK; backup retention 7d so a promoted
    replica is immediately backup-protected; deletion-protection
    on by default for DR-prod), (2) account-level ECR replication
    rule (PREFIX_MATCH filter scoped to `mahjong-autotable` repo;
    typical replication lag 1-5 min; secondary-region ECR
    repository auto-created on first replication event — no
    pre-creation needed), (3) Route 53 PRIMARY + SECONDARY
    failover records sharing one FQDN + an HTTPS health check
    against the primary's `/health` (Bishop's endpoint). Module
    pins TTL<60s via a variable validator (W6 invariant — clients
    must pick up failover within ≈2 min). Two AWS provider
    aliases (`aws.primary` + `aws.secondary`) so every resource
    is explicitly placed; no default-provider fall-through. Six
    outputs documented for the rehearsal runbook
    (`replica_db_arn`, `primary_health_check_id`,
    `failover_record_fqdn`, …). (Apone)
- **Terraform `envs/dr-us-west-2/` — DR env stack.** New
    secondary-region (us-west-2) Terraform stack. VPC CIDR pinned
    to **10.1.0.0/16** (non-overlapping with the primary's
    10.0.0.0/16 — future VPC peering / Transit Gateway works
    without renumbering). Three private subnets across us-west-2's
    first three AZs (no public subnets in DR-warm — ingress
    lands when a promotion fires). Provisions the secondary-region
    DB subnet group + SG + KMS key, then instantiates the
    `modules/dr-replication` module passing both provider aliases.
    Reads primary stack outputs via `terraform_remote_state` so
    the primary DB ARN + KMS ARN don't have to be hand-plumbed.
    Backend bootstrap follows the same chicken-and-egg pattern
    as the primary — `backend.example.hcl` + the runbook in
    `docs/terraform.md` §2. (Apone)
- **Terraform `modules/github-oidc/` — reusable OIDC module +
    least-privilege grants.** New module replacing the inline
    W5-style grants for future envs (the primary env's flat
    `iam-github-oidc.tf` is also W6-narrowed in place). `ecr:*`
    narrowed to the eight discrete actions a `docker push`
    actually invokes (push-only; no `Describe*`/`List*`) scoped
    to the repository ARN. `ssm:Get*` narrowed to `ssm:GetParameter`
    only on `parameter/mahjong/<env>/*` (drops GetParameterHistory
    which leaks rotation history; drops DescribeParameters which
    leaks parameter names = org-structure intel). `iam:PassRole`
    introduced as an opt-in dynamic block guarded by
    `iam:PassedToService` (W5 had no PassRole; W6 adds the
    grant in fenced form so future widenings can't be a silent
    privilege-escalation vector). Companion `least-privilege.tf`
    documents per-action rationale (no resources/policies — pure
    documentation that lives next to the policy it audits). The
    `least-privilege.tf` + `main.tf` files are W6 lock-step:
    ANY policy widening MUST update the rationale in the SAME
    commit. (Apone)
- **`infra/k8s/base/coturn-{deployment,configmap,secret}.yaml` —
    production coturn data plane.** Three new k8s manifests
    deploying coturn 4.6 as a 2-replica AZ-spread Deployment
    behind an NLB Service, with HMAC-mode authentication
    (`use-auth-secret` + `lt-cred-mech`) using the
    `coturn-static-auth-secret` ExternalSecret (sourced from
    SSM `/mahjong/<env>/turn/auth_secret`). Bishop's W3
    `/api/turn` endpoint shares the same HMAC key so credential
    minting + validation work symmetrically; one rotation
    rolls both sides. `coturn-configmap.yaml` pins
    `listening-port=3478`, `tls-listening-port=5349`,
    `fingerprint`, `min-port=49152`, `max-port=65535` (IANA
    ephemeral range) + drops `lt-cred-mech`/`no-cli`/`no-loopback-peers`
    hardening + 1080 quota cap. A new `NetworkPolicy
    coturn-relay-ports` admits the relay range (49152-65535 UDP)
    + the three control-plane ports (3478 UDP+TCP, 5349 TCP);
    egress wide-open (a TURN server's job is to NAT-traverse to
    arbitrary peers). Pod-level security: `runAsNonRoot=true`,
    `runAsUser=998`, `readOnlyRootFilesystem=true`, `capabilities
    drop ALL`. RollingUpdate pinned `maxSurge=1, maxUnavailable=0`
    so refreshes always spin a fresh pod first. NLB annotations
    + `externalTrafficPolicy: Local` preserve the client source
    IP (coturn needs it to mint relay candidates). The W2
    single-replica `turn-server.yaml` stays in place for staging;
    the W6 `coturn-*` resources land alongside in prod (parallel
    names — `coturn-*` not `turn-server-*` — so the cutover is
    operator-driven blue-green). (Apone)
- **`.github/workflows/mobile-internal-testing.yml` — tag-driven
    TestFlight + Play Internal promotion.** New workflow,
    triggers on `mobile-v*.*.*` tags. Five-job shape: `prepare`
    (tag regex validation + version extraction) → `build-web-bundle`
    (npm ci + npm run build of the autotable frontend that the
    Capacitor shell wraps) → `android` (gradle bundleRelease
    SIGNED + fastlane supply → Play Internal Testing,
    `release_status: draft` so the operator gates the
    promotion-to-testers click) → `ios` (CocoaPods + gym +
    pilot SIGNED via App Store Connect API key → TestFlight) →
    `notify` (Slack webhook). Code-signing secrets soft-fail
    (fork PRs without secrets log a warning and skip the upload
    job; operator-driven tag pushes from main always have them).
    Ephemeral keychain provisioned per run for iOS cert import;
    Provisioning Profile UUID auto-extracted from the
    `.mobileprovision` plist + installed at the canonical macOS
    path. Companion `docs/mobile-release.md` (NEW) covers the
    full release-flow diagram, signing-identity setup runbook
    (App Store Connect API key, distribution `.p12`,
    provisioning profile, Play keystore, Play service-account
    JSON, Slack webhook), TestFlight + Play tester-management
    runbook, and a troubleshooting table. (Apone)
- **`.github/workflows/verify-slsa-on-deploy.yml` — pre-merge
    SLSA verification gate on `deploy:prod` PRs.** New workflow,
    label-gated. Installs `slsa-verifier` v2.6.0 (the SAME binary
    the admission webhook bundles for in-cluster verification),
    resolves the image digest from `infra/k8s/overlays/prod/kustomization.yaml`'s
    `images:` block, runs `slsa-verifier verify-image
    <image>@<digest> --source-uri github.com/long2know/mahjong-autotable
    --print-provenance > slsa-provenance.json`. Sticky PR
    comment communicates the pass/fail to reviewers without
    needing to drill into the Actions tab; the verified
    predicate JSON uploads as a workflow artefact (30-day
    retention). Belt-AND-suspenders for the Wave-5 Kyverno
    `attestations:` block: the same predicate is verified at
    CI time AND at admission time; a regression in either layer
    is caught by the other. `docs/slsa-provenance.md` §7a (NEW)
    documents the two-layer model + the `slsa-verifier` binary's
    role inside the admission webhook container. (Apone)
- **`.github/trivy-allowlist.yaml` (NEW) + container-scan
    threshold tuning.** PR gate tightened from W3's CRITICAL-only
    to HIGH+CRITICAL (the W6 block-merge floor); daily cron
    relaxed to full-severity sweep (LOW+MEDIUM+HIGH+CRITICAL) +
    non-blocking — visibility, not gating. New CVE allowlist
    file with W6-invariant schema: every entry MUST carry
    `id` + `justification` + `added` + `expires`; expiry capped
    at 30 days (`allowlist-check` job fails the workflow if an
    entry's expiry is in the past OR more than 30d in the
    future). Trivy's native `.trivyignore` is rendered from the
    YAML allowlist at scan time so we get human-readable
    justification + Trivy-native suppression in one source of
    truth. Wave-6 ships with the allowlist EMPTY — `allowed: []`
    — establishing the schema baseline. (Apone)
- **`docs/terraform.md` (NEW).** Cross-module reference covering
    the W5+W6 module layout (`infra/terraform/` flat primary +
    `modules/dr-replication/` + `modules/github-oidc/` +
    `envs/dr-us-west-2/`), the apply-order rule (primary stack
    first; DR reads primary via `terraform_remote_state`), the
    W6 OIDC narrowing summary table, AND **§4 "DR rehearsal"**
    — quarterly drill runbook: pre-flight checks (replica
    replication-status confirmation, ECR image-delivery
    confirmation, Route 53 health-check status), the
    non-destructive failover (invert the Route 53 health check
    via `aws route53 update-health-check --inverted`; ~90s
    propagation × 30s TTL = ≈2 min total failover time), the
    DESTRUCTIVE annual full-rehearsal (`aws rds promote-read-replica`
    — one-way; replica must be re-provisioned via terraform
    after), the restore step (un-invert the health check), and
    the post-rehearsal report template (time-to-DNS-cut,
    time-to-200-from-secondary, anomalies). 5-min total
    failover SLO documented. (Apone)
- **`docs/retro-2026-05.md` (NEW).** May 2026 monthly retro —
    what shipped (W5 SLSA+SBOM unified predicate + Kyverno
    attestations + Terraform bootstrap + W6 DR + OIDC narrowing
    + coturn k8s + …), what's WIP (Bishop's W6 backend lane,
    Hicks's frontend, the test-gate ascent past 1345), lessons
    learned (the W5 `.git/config` race incident — Apone's
    `b346157` absorbed Hicks's frontend work because a concurrent
    agent rewrote `.git/config` between `git config user.name`
    and the `git commit`; **W6 mitigation pattern**: per-invocation
    `git -c user.name=… -c user.email=… commit` ONLY, never the
    stateful `git config` form). Establishes the monthly retro
    cadence + template for future months. (Apone)

### Changed (Phase K Wave 6 — PR pending)
- **`infra/terraform/iam-github-oidc.tf` — narrowed in place.**
    The primary env's inline OIDC role policy now matches the
    `modules/github-oidc/` shape (push-only ECR verbs scoped to
    the repo ARN, `ssm:GetParameter` only on the per-env path,
    opt-in PassRole). `variables.tf` gains the two new
    `passrole_target_roles` / `passrole_target_services`
    variables. Apply-time: `terraform plan -var-file=prod.tfvars`
    will show DELETIONS of the W5-broad statements + ADDITIONS
    of the W6-narrowed ones — review carefully before apply.
    Outputs unchanged (the role ARN is stable; only the inline
    policy changes). (Apone)
- **`.github/workflows/container-scan.yml` — threshold tuning.**
    PR/push runs default to HIGH+CRITICAL (was CRITICAL-only in
    W3); cron runs default to full-severity sweep (LOW+MEDIUM+HIGH+CRITICAL)
    with the gate step non-blocking. New `allowlist-check` job
    runs FIRST + fails the workflow on expired allowlist entries.
    Sticky PR comment + STEP_SUMMARY tables updated to show LOW
    counts. Trivy gating + JSON + SARIF passes all consume the
    rendered `.trivyignore` so the YAML allowlist becomes the
    single source of truth. (Apone)
- **`docs/turn-server-setup.md` — §9 "k8s deployment" (NEW).**
    Documents the W6 production-shape coturn manifests
    (`infra/k8s/base/coturn-*.yaml`), the differences from the
    W2 single-replica `turn-server.yaml` (AZ-spread, HMAC mode by
    default, wider relay port range, NetworkPolicy, NLB
    annotations, readOnlyRootFilesystem), the apply runbook (SSM
    seed → kubectl apply -k → verify two pods in different AZs
    → smoke-test with turnutils_uclient), and the cutover
    procedure (the W2 resources stay for staging; the W6
    resources land in parallel in prod; W2 prod resources
    decommissioned after a 24h cool-down). (Apone)
- **`docs/slsa-provenance.md` — §7a (NEW).** Documents the
    `slsa-verifier` v2 binary's role inside the admission
    webhook container (second-pass verification beyond Kyverno's
    cosign-via-policy integration; defends against a future
    Kyverno or cosign upstream regression) AND the W6
    `verify-slsa-on-deploy.yml` pre-merge gate (same binary,
    same predicate, same source URI verified at BOTH CI time
    AND admission time). (Apone)

### Notes (Phase K Wave 6 — PR pending)
- **W5 git-config race incident (lessons learned in W6).**
    Apone's W5 `b346157` accidentally absorbed Hicks's frontend
    work because the Wave-5 `commit-tree` recovery used the
    stateful `git config user.name "Apone (DevOps)"` form, and
    a concurrent agent rewrote `.git/config` to its own identity
    between the `git config` call and the `git commit`. The
    commit landed under the WRONG author. **W6 mitigation**:
    every commit in this wave uses `git -c user.name="Apone (DevOps)"
    -c user.email="apone@squad.mahjong" commit -m …` (atomic
    per-invocation override; no time window where the config
    state can be raced). All git operations wrapped in `flock`
    on a shared lock file so two agents cannot run a
    commit+push pair concurrently. The pattern is documented in
    `docs/retro-2026-05.md` as a permanent reference + in
    `.squad/agents/apone/history.md` Wave-6 entry. (Apone)
- **Backend gate preserved.** This wave's scope is pure DevOps
    + docs + infra (`src/**` untouched). The W5 1345/0/0 backend
    gate carries forward; `dotnet test` not re-run. (Apone)
- **Lock-step invariant updates.** The signer-identity invariant
    in `docs/admission-policy.md` §7.1 (now SIX files since W5)
    is not touched in W6 because the SLSA workflow + Kyverno
    policy + image digest list are unchanged. The OIDC policy +
    its rationale comment in `modules/github-oidc/least-privilege.tf`
    is a NEW lock-step pair: ANY widening of the inline
    `github_deploy_inline` policy MUST land alongside an
    updated rationale paragraph in `least-privilege.tf`. (Apone)
- **DR rehearsal SLO.** 5-min total failover time
    (health-check trip → DNS resolver cache flush → first
    successful `/health` 200 from us-west-2). Documented in
    `docs/terraform.md` §4.5; reported in the May 2026 retro;
    re-reported every quarter at the rehearsal cadence. (Apone)

## [0.14.0] — Phase K Wave 5 — 2026-05-28 (PR pending)

**Theme:** Supply-chain ring-5 (unified provenance+SBOM
multi-subject predicate; Kyverno requires the SLSA attestation
alongside the cosign signature) + staging brought to parity with
prod on JWT-keys ESO data plane + retroactive secrets-history
sweep workflow (closing the historical-commit blind spot of the
W4 PR-diff scanner) + automated HSTS preload-readiness probe
with sticky-issue alerting + Terraform bootstrap module for
"fresh prod env in <30 min" (VPC + EKS + RDS + ECR + GitHub
OIDC), unblocking the Wave-6 DR rehearsal target.

### Added (Phase K Wave 5 — PR pending)
- **Unified SLSA L3 in-toto provenance + SBOM under a single
    multi-subject predicate.** Rewrote
    `.github/workflows/slsa-provenance.yml` to invoke the
    GENERIC SLSA generator
    (`slsa-framework/slsa-github-generator/.github/workflows/generator_generic_slsa3.yml@v2.0.0`)
    in place of the container-specific Wave-4 generator. New
    pipeline shape: `resolve-digest` (unchanged) → `build-sbom`
    (Syft against the published image; computes the
    base64-encoded sha256sum-format subjects list with TWO
    subjects: image manifest digest + CycloneDX SBOM file
    digest) → `provenance` (multi-subject generic generator
    producing a single `provenance-and-sbom.intoto.jsonl`) →
    `attest-oci` (`cosign attest --type slsaprovenance1` so the
    Wave-5 Kyverno `attestations:` block discovers the
    predicate via standard OCI-sidecar lookup) →
    `attach-to-release` (uploads both the predicate AND the
    SBOM as Release assets atomically on tag pushes). Auditors
    now have ONE Sigstore-signed statement that
    cryptographically binds the image and the SBOM to the SAME
    build run, not two parallel attestation flows requiring
    cross-trust. Wave-4 attestations remain in Rekor and
    remain verifiable with the Wave-4 invocation. Verification
    + migration runbook updated at `docs/slsa-provenance.md` §6
    (`slsa-verifier verify-artifact` against the SBOM subject,
    `verify-image` against the image subject — both pass
    against the same predicate). (Apone)
- **Kyverno `attestations:` block requiring the SLSA-v1 predicate.**
    Extended `infra/k8s/policies/kyverno-cosign-verify.yaml` to
    add an `attestations:` block alongside the existing
    `attestors:` clause. Admission now requires BOTH a cosign
    keyless signature from this repo's `sign-image.yml`
    workflow AND a SLSA-v1 provenance predicate produced by
    this repo's Wave-5 `slsa-provenance.yml`. The
    `conditions:` block pins three CEL-evaluated values
    against the decoded predicate:
    `buildDefinition.externalParameters.workflow.repository`,
    `buildDefinition.externalParameters.workflow.path`, and
    `runDetails.builder.id` (regex). Attestors-in-attestations
    re-asserts the subject pin to the `slsa-github-generator`
    reusable workflow's URL — belt-AND-suspenders. Operator
    runbook + negative test + rollback procedure documented at
    `docs/admission-policy.md` §6 (NEW Wave-5 section).
    (Apone)
- **Staging overlay `mahjong-jwt-keys-staging` ExternalSecret.**
    New `infra/k8s/overlays/staging/jwt-keys-secret.yaml` —
    staging-equivalent of the Wave-4 prod
    `mahjong-jwt-keys` ESO surface. Same shape (three
    rotation-state-named SSM SecureString parameters,
    `auth__jwtsigningkeys__{0,1,2}` env-var KEYS feeding
    Bishop's `Auth.JwtSigningKeys` array binding,
    15-min refresh interval) targeting
    `/mahjong/staging/auth/jwt/key-{active,previous,archive}`
    via `aws-secrets-manager-staging` ClusterSecretStore. Wired
    into `infra/k8s/overlays/staging/kustomization.yaml` as
    both a `resources:` entry AND an `envFrom` deployment
    patch (mirrors prod). Closes the Wave-4 handoff item that
    left staging falling back to the omnibus's singular
    `Auth__JwtSigningKey` — staging now exercises the
    array-binding code path so Bishop's
    `jwt-rotation-smoke.sh` can hard-assert the multi-key
    fallback against staging too. (Apone)
- **`secrets-history-sweep.yml` workflow + runbook.** New
    `.github/workflows/secrets-history-sweep.yml` —
    `workflow_dispatch`-only retroactive `gitleaks detect`
    sweep over the full commit graph from any ref (default
    `main`). SARIF uploaded to Code Scanning under a
    DISTINCT category (`secrets-history-sweep`) so findings
    don't overlay the W4 `gitleaks` category. SARIF + log
    also uploaded as workflow artefact for offline triage
    (90-day retention). Closes the W4 PR-diff scanner's
    historical-commit blind spot. Operator runbook +
    rotate-then-purge procedure + per-secret-class rotation
    table + force-push history-rewrite (`git filter-repo`)
    procedure at NEW `docs/secrets-scanning.md`. (Apone)
- **`hsts-readiness-check.yml` workflow + sticky-issue alerting.**
    New `.github/workflows/hsts-readiness-check.yml` —
    daily 13:00 UTC cron + `workflow_dispatch`. `curl -I`s
    the production origin and asserts the response includes
    EXACTLY `Strict-Transport-Security: max-age=63072000;
    includeSubDomains; preload`. On failure: opens (or updates)
    a sticky GitHub issue with the observed value, expected
    value, triage steps, and workflow-run link; on recovery,
    auto-closes the issue with a recovery comment. The probe
    is the early-warning system both BEFORE the manual
    submission to <https://hstspreload.org/> (Stephen action;
     14-day all-green-runs gate) and AFTER (a post-submission
    regression is a P0 with a 6-week-removal cost). Probe URL
    overridable via repo variable `HSTS_PROBE_URL` or
    dispatch input. `docs/hsts-preload.md` §3a (NEW) covers
    the operator runbook. (Apone)
- **`infra/terraform/` bootstrap module (NEW directory).**
    Bare-minimum Terraform module to provision a Mahjong stack
    in a fresh AWS account: 1 × VPC (10.0.0.0/16, 3 public +
    3 private subnets across 3 AZs; per-AZ NAT in prod, single
    NAT in staging; S3 gateway endpoint), 1 × EKS cluster
    (1.30; managed node group with mixed-instance Spot
    fallback; CoreDNS + kube-proxy + VPC-CNI + EBS-CSI addons;
    IRSA OIDC enabled; secret-encryption KMS key), 1 × RDS
    Postgres (db.t4g.small staging / db.t4g.medium prod;
    gp3 auto-scaling 20→100 GB; encrypted; multi-AZ in prod;
    deletion protection in prod; auto-generated 32-char
    master password surfaced as sensitive terraform output for
    operator-driven SSM seeding), 1 × ECR repository
    (image-scan-on-push; lifecycle policy keeping last 30
    tagged images + expiring untagged after 14 days), and 1
    GitHub-Actions OIDC IAM role (`mahjong-${env}-github-deploy`)
    with the trust policy scoped to this repo + main / `v*` /
    `environment:${env}` subjects. Per-environment tfvars
    (`staging.tfvars`, `prod.tfvars`). State backend stanza
    intentionally empty so `terraform init` consumes
    `backend-${env}.hcl` per-env. Quick-start, total-time
    budget (~27-32 min apply), post-bootstrap helm install
    sequence (ESO, AWS-LBC, cert-manager, Kyverno), ECR mirror
    procedure, and teardown steps at NEW
    `infra/terraform/README.md`. Validates clean against
    `terraform validate` v1.9.8. Unblocks the Wave-6
    DR-rehearsal acceptance criterion "<30 min to spin up a
    clean prod env". (Apone)

### Changed (Phase K Wave 5 — PR pending)
- **`.github/workflows/sbom.yml` header annotation.** Clarified
    the workflow's relationship to the new unified SLSA
    predicate: this workflow continues to OWN the PR-time CVE
    gate (Trivy CRITICAL,HIGH + SARIF → Code Scanning) and the
    per-PR dependency-graph SBOM; the SIGNED, AUDITOR-VERIFIABLE
    SBOM for every release artefact now lives in
    `slsa-provenance.yml` as part of the multi-subject
    predicate. The Wave-5 unified predicate is the canonical
    source of truth for "what shipped"; this workflow remains
    the PR-blocking CVE layer. (Apone)
- **`docs/slsa-provenance.md` §6.** Rewrote the "Bumping the
    SLSA generator version" section to cover both the v2.0.0
    pin maintenance AND the Wave-4 → Wave-5 generator
    migration (container-specific → generic generator;
    single-subject → multi-subject predicate; backward
    compatibility for Wave-4 artefacts in Rekor). (Apone)
- **`docs/admission-policy.md` §6.** Renumbered + expanded
    to cover the Wave-5 SLSA-attestation requirement.
    NEW §6.1 (Wave-5 SLSA attestation), §6.2 (negative test
    for image-without-predicate), §6.3 (rollback procedure
    if the SLSA workflow flakes during an emergency hotfix);
    §6.4 / §6.5 preserve the Wave-3/4 observability
    content. (Apone)
- **`docs/hsts-preload.md` §3.** Tightened the submission
    pre-condition: 14 consecutive green runs of
    `hsts-readiness-check.yml` are now the gate before
    clicking submit (in addition to the existing 14-day
    pre-submission dry-run). Added §3a covering the new
    daily probe + sticky-issue alerting. (Apone)
- **`infra/k8s/overlays/staging/kustomization.yaml`.** Added
    `jwt-keys-secret.yaml` to `resources:` and an `envFrom`
    deployment patch mounting `mahjong-jwt-keys-staging`
    (`optional: true` so a fresh staging cluster without ESO
    bootstrapped still starts via the omnibus fallback). Same
    JSON-patch shape as the Wave-4 prod overlay. (Apone)

### Notes (Phase K Wave 5)
- **Backend gate:** `dotnet test src/backend/Mahjong.Autotable.slnx
    --nologo` baseline preserved at 1232 / 0 / 0 (Wave-5
    scope is pure DevOps + docs + infra; `src/backend/**`
    source code untouched).
- **Five-layer supply-chain enforcement** (workflow → release-gate
    → admission-signature → admission-attestation → SLSA
    provenance). The canonical signer-identity regex stays as
    the cross-layer invariant — any rename of `sign-image.yml`
    OR `slsa-provenance.yml` is now a SIX-file coordinated
    change (`sign-image.yml`, `verify-signature.yml`,
    `kyverno-cosign-verify.yaml` `attestors:` + `attestations:`
    blocks, `kyverno-enforce-patch.yaml`, and the
    `--source-uri` arg in `docs/slsa-provenance.md` §4).
- **Pattern lock — multi-subject in-toto predicates.** Future
    artefact classes (release-notes blob, runtime config blob,
    helm chart `tgz`) can be added as additional subjects to
    the same Wave-5 predicate without changing the generator
    invocation — just append a line to the
    `sha256sum`-formatted subjects list in `build-sbom`. ONE
    predicate per build, MANY subjects.
- **Pattern lock — Wave-N+1 staging-mirror policy.** Any new
    prod-only data-plane that ships in wave N (e.g. Wave-4's
    prod `jwt-keys-secret.yaml`) MUST be mirrored to staging
    in wave N+1 (Wave-5's staging counterpart) so the
    rotation-rehearsal surface stays one wave behind the prod
    surface, not 5 waves behind.
- **Pattern lock — sticky-issue alerting on probe workflows.**
    The HSTS readiness probe's sticky-issue mechanism is the
    template for future cron-driven health checks (e.g. the
    proposed JWT-rotation soak in Wave-6+); search by exact
    issue-title string for idempotent open/update/close
    semantics. Avoids the duplicate-issue spam common with
    naïve `gh issue create`-on-failure patterns.

## [0.13.0] — Phase K Wave 4 — 2026-05-27 (PR pending)

**Theme:** Supply-chain ring-4 (SLSA provenance) + zero-touch JWT
key rotation (ESO) + Kyverno enforce hard-pin + HSTS preload +
in-repo secrets scanning. Wave 4 closes the Wave-3 "future" list:
SLSA in-toto predicates land as the fourth supply-chain ring on
top of cosign signatures + verify gates + SBOM signing + Kyverno
admission; the `Auth.JwtSigningKeys` array binding (W3 schema)
now has its production ESO data plane; Kyverno prod gets a
fail-safe second policy that cannot be downgraded by a misedit
of the global default; HSTS preload header lands on the prod
Ingress for the manual submission to https://hstspreload.org/;
and `gitleaks` joins GitGuardian as the in-repo secrets-scan
layer.

### Added (Phase K Wave 4 — PR pending)
- **SLSA Level 3 in-toto provenance for every published image.**
    New `.github/workflows/slsa-provenance.yml` triggers on
    push-to-main, `v*.*.*` tag pushes, and workflow_dispatch.
    Resolves the manifest-list digest the same way `sign-image.yml`
    does, then calls the official `slsa-framework/slsa-github-generator/.github/workflows/generator_container_slsa3.yml@v2.0.0`
    reusable workflow to produce an in-toto-shaped provenance
    predicate signed via GitHub OIDC + Sigstore Fulcio, recorded
    in Rekor, AND attached to the OCI registry as a sidecar
    artefact. On tag pushes, the `attach-to-release` job
    additionally uploads the bundle to the matching GitHub
    Release as `provenance.intoto.jsonl`. Operator + auditor
    verification runbook (`slsa-verifier` CLI usage, decoded
    predicate shape, failure-mode triage, generator bump
    procedure) at `docs/slsa-provenance.md`. (Apone)
- **ESO `mahjong-jwt-keys` ExternalSecret for the W3 `Auth.JwtSigningKeys` array.**
    New `infra/k8s/overlays/prod/jwt-keys-secret.yaml` —
    SEPARATE `ExternalSecret` (distinct from the omnibus
    `mahjong-autotable` secret) materialising three indexed env
    vars (`auth__jwtsigningkeys__{0,1,2}`) from three
    rotation-state-named SSM SecureString parameters
    (`/mahjong/prod/auth/jwt/key-{active,previous,archive}`).
    The 15-minute `refreshInterval` is tighter than the omnibus
    1 h so emergency JWT rotations propagate within minutes. The
    prod kustomization mounts the resulting Secret via
    `envFrom: { secretRef: { name: mahjong-jwt-keys, optional: true } }`
    so Bishop's W4/W5 code-side binding picks up the array
    automatically once it lands. `docs/jwt-rotation.md` §1 +
    §3 + §4 + §7 rewritten to reflect the
    rotation-state-named SSM convention (the operator never has
    to compute "which numeric index holds value X today?"). (Apone)
- **Kyverno prod hard-pin `ClusterPolicy`.** New
    `infra/k8s/overlays/prod/kyverno-enforce-patch.yaml` adds a
    SECOND cluster policy (`enforce-prod-mahjong-images`) scoped
    exclusively to `mahjong-prod`, with
    `validationFailureAction: Enforce` and the same canonical
    `sign-image.yml` signer-identity regex as the Wave-3 default.
    Acts as a fail-safe alongside the Wave-3 policy: a misedit of
    the Wave-3 per-namespace override cannot accidentally let
    unsigned images into prod. Multiple policies on the same
    image just compose (both must verify before admission).
    `docs/admission-policy.md` §5.3 (NEW) codifies the
    end-to-end canary procedure (build unsigned image → deploy to
    staging: ADMIT + warn → deploy to prod: REJECT). (Apone)
- **HSTS preload header on prod Ingress.** New
    `infra/k8s/overlays/prod/hsts-patch.yaml` sets
    `Strict-Transport-Security: max-age=63072000; includeSubDomains; preload`
    on the production origin via nginx-ingress
    `configuration-snippet`. `force-ssl-redirect: true` and
    `ssl-redirect: true` are also pinned here so a global
    ConfigMap edit cannot weaken prod inadvertently. Manual
    submission runbook at `docs/hsts-preload.md` (NEW) — the
    chromium HSTS preload list is operator-driven, not
    CI-automated; the doc covers prerequisites, the
    2-week pre-submission dry-run, the
    https://hstspreload.org/ form-submission flow, and the
    post-submission monitoring + removal procedure. (Apone)
- **`gitleaks` secrets-scanning workflow.** New
    `.github/workflows/secrets-scan.yml` runs gitleaks on every
    PR + push to `main` + nightly cron (03:00 UTC, offset from
    container-scan's 04:00). HIGH-confidence findings fail the
    gate; SARIF uploaded to GitHub Code Scanning under category
    `gitleaks` (distinct from Trivy's `trivy-container-scan` and
    `trivy-image`). Coexists with the README-recommended
    GitGuardian app as defense-in-depth — two layers, two failure
    modes, same `report and block` floor. Concurrency-grouped on
    `secrets-scan-${{ github.ref }}` so PR refreshes cancel
    in-flight prior runs. (Apone)
- **`docs/slsa-provenance.md` + `docs/hsts-preload.md` (NEW).**
    Operator + auditor runbooks for the two new external-touching
    surfaces (`slsa-verifier` CLI usage; chromium HSTS preload
    submission). (Apone)

### Changed (Phase K Wave 4)
- `infra/k8s/overlays/prod/kustomization.yaml`: now lists
    `kyverno-enforce-patch.yaml` as a resource AND uses
    `patches: [- target: Ingress, path: hsts-patch.yaml]` to apply
    the HSTS strategic-merge AND adds a JSON-patch that appends
    `secretRef: { name: mahjong-jwt-keys, optional: true }` to
    the deployment's `envFrom` list. (Apone)
- `docs/jwt-rotation.md` §1: rewritten to document the Wave-4
    `mahjong-jwt-keys` ESO and the rotation-state-named SSM
    parameters. §3 + §4 rotation runbook commands updated to use
    `aws ssm put-parameter --name /mahjong/prod/auth/jwt/key-*`
    instead of the prior index-shaped pattern. §5 emergency
    rotation updated likewise. §7 migration table updated:
    Apone W4 row marked complete; W6 row dropped (work landed
    in W4). (Apone)
- `docs/admission-policy.md`: new §5.3 covers the Wave-4
    canary procedure (staging ADMIT-with-warn → prod REJECT).

### Notes (Phase K Wave 4)
- **Backend gate untouched.** Wave-4 DevOps scope is pure
    workflow + infra + docs — no `src/**` edits. The
    1152/0/0 backend baseline from Wave-3 is preserved.
- **No `git add -A`.** Selective adds only:
    `.github/workflows/{slsa-provenance,secrets-scan}.yml`,
    `infra/k8s/overlays/prod/{jwt-keys-secret,kyverno-enforce-patch,hsts-patch,kustomization}.yaml`,
    `docs/{slsa-provenance,hsts-preload,jwt-rotation,admission-policy}.md`,
    `CHANGELOG.md`, `.squad/decisions/inbox/apone-phase-k-wave-4.md`,
    `.squad/agents/apone/history.md`.
- **Out-of-scope / DO NOT STAGE this wave:**
    `.copilot/skills/error-recovery/`, `.github/workflows/squad-*.yml`,
    `.tool-actionlint/`, `.work/`.

## [0.12.0] — Phase K Wave 3 — 2026-05-26 (PR #49)

**Theme:** Supply-chain hardening + zero-downtime auth rotation +
TURN-over-TLS. Wave 3 closes the three Wave-2 "future Phase K wave"
handoff items in one go: Kyverno admission policy for cosign
enforcement, `Auth:JwtSigningKeys` fallback list, and the deferred
`turns:` TLS listener. Also adds container-scan PR gate +
pre-publish SBOM signature verification + nightly JWT-rotation
smoke + PWA-asset presence gate.

### Added (Phase K Wave 3 — PR #49)
- **Kyverno cosign admission policy.**
    `infra/k8s/policies/kyverno-cosign-verify.yaml` —
    `ClusterPolicy` named `verify-mahjong-images` REFUSES to admit
    any Pod / Deployment / StatefulSet / DaemonSet / Job / CronJob
    whose `image:` field matches
    `ghcr.io/long2know/mahjong-autotable:*` unless the image
    carries a valid cosign keyless signature whose Fulcio cert was
    issued to this repo's `sign-image.yml` workflow on `main` or
    `v*.*.*`, with Rekor entry verifying. Action mode is per-
    namespace: **Enforce** in `mahjong-prod`, **Audit** in
    `mahjong-staging` (and globally for any new namespace —
    fail-safe default). `mutateDigest: true` rewrites tags to
    digests post-verify so a pod is pinned to the exact attested
    bits. `failurePolicy: Fail` blocks new rollouts on Sigstore
    outage (existing pods keep running). Closes the Wave-1/-2
    "verify enforced ONLY in CI" gap — admission-layer
    enforcement now refuses unsigned images at the cluster
    boundary. Operator runbook + Kyverno Helm install +
    positive/negative test instructions in
    `docs/admission-policy.md`. (Apone)
- **`Auth:JwtSigningKeys` fallback-list schema.**
    `appsettings.json` ships a new `Auth.JwtSigningKeys: []`
    forward-compat array (with `//` documentation key explaining
    `[0]` = active signer, `[1..N]` = previous keys accepted for
    validation). Closes the Wave-1/-2 "Wave-9 fallback-key list
    (planned)" carry-over. `docs/jwt-rotation.md` (NEW) covers the
    full lifecycle: schema, code-side contract (Bishop's W4/W5
    deliverable), rotation cadence (annual, 30-day grace; emergency
    immediate), SSM-shift rotation procedure, smoke validation
    via `tests/smoke/jwt-rotation-smoke.sh` (NEW — boots image
    with key0 → mints token → restarts with keys[0]=key1 +
    keys[1]=key0 → asserts old token still validates AND new
    tokens signed under key1), and the wave-by-wave migration
    path. Smoke is FORWARD-COMPATIBLE — soft-passes when
    `/api/auth/token` / `/api/auth/validate` return 404 (until
    Bishop's binding lands), matching the established `pwa-smoke`
    / `csp-report-smoke` / `chat-flow-smoke` shape. (Apone)
- **TLS for `turns:` on port 5349.**
    `infra/k8s/base/turn-server.yaml` now passes
    `--cert /etc/tls/tls.crt --pkey /etc/tls/tls.key` to coturn
    and mounts a new `tls` volume from a `tls-cert-turn` Secret.
    New `infra/k8s/overlays/prod/turn-tls-secret.yaml` ships an
    `ExternalSecret` bound to `aws-secrets-manager-prod` that
    materialises the Secret (`type: kubernetes.io/tls`) from SSM
    parameters `/mahjong/prod/turn/tls/{crt,key}`. Closes the
    Wave-2 "Phase L follow-up" deferral (corporate firewalls
    blocking plain `:3478` UDP/TCP can now negotiate via
    `turns:` on 5349). Operator runbook updated in
    `docs/turn-server-setup.md` §1.4 (cert provisioning via
    cert-manager+LE or ACM, SSM upload, rotation cadence). (Apone)
- **Container-scan PR gate + nightly cron.**
    `.github/workflows/container-scan.yml` — Trivy image scan on
    EVERY PR (no path filter — CRITICAL CVEs published against
    indirect deps MUST surface even on a touch-nothing PR) + push
    on `main` + nightly cron (04:00 UTC, offset from
    `sbom.yml`'s Monday-09:00 cadence). Hard-gates on CRITICAL by
    default; configurable to HIGH / MEDIUM via
    `workflow_dispatch` input for triage reruns. SARIF uploaded
    to GitHub Code Scanning (`category: trivy-container-scan` —
    distinct from `sbom.yml`'s `trivy-image` so findings don't
    overlay). Sticky PR comment via
    `marocchino/sticky-pull-request-comment@v2` (header
    `container-scan`) with CRITICAL+HIGH+MEDIUM counts and gate
    verdict — reviewers see the latest scan result inline without
    conversation noise on rerun. Coexists with `sbom.yml` (SBOM-
    focused, CRITICAL+HIGH gate, weekly cron) — two workflows,
    distinct purposes. (Apone)
- **SBOM signed by cosign + verified in pre-publish gate.**
    `release.yml` adds a new `verify-sbom` job between
    `verify-signature` and `release`. Generates an SPDX SBOM from
    the EXACT digest-qualified image just smoke-tested + signature-
    verified, signs the SBOM with cosign keyless OIDC
    (`sign-blob --output-signature sbom.spdx.json.sig
    --output-certificate sbom.spdx.json.pem`), then verifies the
    signature with `cosign verify-blob --certificate-identity-regexp
    "…/release.yml@refs/tags/v*"`. Block-release on missing /
    invalid signature. The signed SBOM bundle (json + sig + cert)
    is attached as artefacts to the workflow AND as assets on the
    GitHub Release page so downstream auditors can pull all three
    without re-running CI. Closes the Wave-1/-2 "SBOM generated
    but not signed" gap. (Apone)
- **PWA-asset presence gate in `docker-smoke.yml`.** New step
    builds the production image and runs
    `docker run --rm <image> sh -c 'ls /frontend/autotable/{sw.js,manifest.webmanifest,manifest-precache.json}'`
    — HARD-FAILS if any of the three Wave-3 PWA artefacts Hicks
    is shipping aren't in the runtime tree. Coexists with the
    Wave-2 `pwa-smoke.yml` (exercises the SW lifecycle in
    chromium); this gate is the per-file-presence floor that
    catches the case where the SW JS shipped but the precache
    manifest didn't. (Apone)
- **JWT-rotation smoke wired into `docker-smoke.yml`.** Same
    nightly cadence as the other smoke scripts; soft-passes
    today, auto-tightens to a hard assertion when Bishop's
    `/api/auth/{token,validate}` surface ships in W4/W5. (Apone)
- **`docs/admission-policy.md` + `docs/jwt-rotation.md` (NEW).**
    Operator runbooks for the two big new policy surfaces. (Apone)

### Changed (Phase K Wave 3)
- `release.yml`: new `verify-sbom` job between `verify-signature`
    and `release`; `release` job's `needs:` is now `[smoke,
    verify-signature, verify-sbom]`; `release` step attaches the
    signed SBOM bundle as Release assets; permissions unchanged
    on the existing jobs (the new job adds `id-token: write` for
    keyless OIDC). (Apone)
- `infra/k8s/base/turn-server.yaml`: coturn args extended with
    `--cert/--pkey`; new `tls` volume mounting the `tls-cert-turn`
    Secret at `/etc/tls/`. (Apone)
- `src/backend/src/Mahjong.Autotable.Api/appsettings.json`: new
    top-level `Auth.JwtSigningKeys: []` array (forward-compat
    schema; Bishop binds in W4/W5). (Apone)
- `docs/turn-server-setup.md` §1.4: rewritten from "Phase L
    follow-up" placeholder to operator-actionable cert-
    provisioning + rotation runbook. (Apone)

## [0.11.0] — Phase K Waves 1 + 2 — 2026-05-25 (PRs #47 + #48)

**Theme:** Production bring-up. Wave 1 (PR #47) shipped supply-chain
signing, nightly load regression alerting, multi-arch post-merge smoke,
CSP-strict rollout coordination, secret-rotation runbook. Wave 2
(PR #48) shipped PR-time multi-arch runtime gate, TURN/STUN k8s
overlay, Capacitor mobile shell scaffold, PWA service-worker smoke,
Microsoft OAuth production secret docs, and a reusable cosign verify
workflow wired into `release.yml` as a pre-publish gate.

K1 was a bring-up wave that did not advance the version cursor (the
preamble's "Phase K opens at 0.10.0" convention); K2 is the first
K-wave to bump minor. Both waves ship under the same release tag.

### Added (Phase K Wave 1 — PR #47)
- **cosign keyless image signing.** `.github/workflows/sign-image.yml`
    fires on `docker-build` workflow success on `main` (and on
    `v*.*.*` tag pushes). Uses GitHub OIDC as the keyless signing
    identity (`id-token: write`), resolves the manifest-list digest,
    signs via Sigstore Fulcio, records the signature in Rekor, and
    immediately verifies with `cosign verify --certificate-identity-regexp …`.
    Documented in `docs/image-signing.md` (full verification runbook
    for operators + auditors). (Apone)
- **Nightly load-test cron.** `.github/workflows/load-test-nightly.yml`
    runs daily at 02:00 UTC: brings up the production-shaped
    docker-compose stack, waits for `/health`, runs
    `tests/load/lobby-flood.js` via the new
    `tests/load/run-and-compare.sh` wrapper, appends a row to
    `docs/load-test-results-history.md`, and ALERTS (email +
    Sentry event) if any workload's p99 latency regresses by >25 %
    vs the prior recorded run. Threshold + duration tunable via
    workflow_dispatch inputs. (Apone)
- **Multi-arch runtime smoke.** `.github/workflows/multi-arch-smoke.yml`
    runs after `docker-build` succeeds on `main`. Matrix: `linux/amd64`
    natively + `linux/arm64` via QEMU. Per-arch smoke checks: `/health`
    200 with the four-field shape, `POST /api/identity` mints a
    cookie, `GET /api/auth/providers` registers (forward-compat
    soft-pass on 404), `POST /api/csp-report` returns 204, and the
    runtime CSP header honours `Security:CspStrictStyles=true`
    (no `'unsafe-inline'` in `style-src`). (Apone)
- **CSP-report endpoint smoke.** `tests/smoke/csp-report-smoke.sh`
    posts a synthetic violation in BOTH the legacy
    `application/csp-report` and modern `application/reports+json`
    envelopes and confirms DB persistence by tailing the runtime's
    structured `CSP violation` warn log line. (Apone)
- **Secret-rotation runbook.** `docs/secret-rotation.md` covering OAuth
    client secrets (Google + GitHub — quarterly), DB connection
    strings (annual), Sentry DSN (compromise-only), reconnect-token
    signing key + magic-link signing key (never — rotation invalidates
    all live sessions). Cross-references ESO/Vault/AWS-Secrets-Manager
    flows from Wave 5/6 docs. (Apone)

### Added (Phase K Wave 2 — PR #48)
- **PR-time multi-arch runtime gate.**
    `.github/workflows/multi-arch-runtime.yml` runs on every PR
    (paths-filtered to `Dockerfile`, `src/backend/**`,
    `src/frontend/autotable-src/**`, the workflow itself) plus pushes
    on `main`. Builds the multi-stage Dockerfile for `linux/amd64`
    (native) + `linux/arm64` (QEMU) independently, loads each per-arch
    image into the local Docker daemon
    (`docker buildx build --output type=docker`),
    `docker run --platform=<p>`, then curls `/health` and asserts
    200 + `"status":"healthy"`. Posts a sticky PR comment
    (header: `multi-arch-runtime`) with the matrix verdict so
    reviewers see arch-specific breakage BEFORE merge. Complements
    Wave 1's post-merge `multi-arch-smoke.yml`. (Apone)
- **TURN / STUN k8s overlay.** `infra/k8s/base/turn-server.yaml`
    deploys `coturn/coturn:4.6` as a Deployment + LoadBalancer Service
    + ConfigMap + ExternalSecret stub. The base manifest ships
    deliberately-broken stub credentials (`mahjong/local/turn/*`
    SSM family — does not exist) so an accidental
    `kubectl apply -k base/` against a real cluster fails fast.
    Dedicated overlay at `infra/k8s/overlays/turn/` (Kustomize)
    fills in the realm + external-ip placeholders and repoints the
    ExternalSecret at the real `aws-secrets-manager-prod`
    ClusterSecretStore + `/mahjong/prod/turn/*` SSM key family. Twin
    convenience templates at
    `infra/k8s/overlays/{prod,staging}/turn-server-patch.yaml`
    + `turnserver-{prod,staging}.conf` for env-specific tuning.
    Operator runbook at `docs/turn-server-setup.md`. (Apone)
- **Capacitor mobile shell.** New `mobile/` top-level directory
    (Capacitor 6.1.x): `package.json` + `capacitor.config.json`
    (`appId: io.mahjong.autotable`, `webDir: ../src/frontend/autotable`)
    + operator runbook (`mobile/README.md`). New
    `.github/workflows/mobile-build.yml` — builds the web bundle once,
    then independent `android` (ubuntu, gradlew
    assembleRelease+bundleRelease) + `ios` (macos, xcodebuild Release
    `CODE_SIGNING_ALLOWED=NO`) jobs produce unsigned artefacts; a
    `release` job creates a `mobile-<run_number>` GitHub prerelease
    with both attached on pushes to `main`. App-store submission +
    signing identities are operator action. `.gitignore` excludes
    `mobile/{ios,android,node_modules,build,.gradle}` since
    `npx cap add` regenerates them deterministically. (Apone)
- **PWA service-worker smoke.** `tests/smoke/pwa-smoke.{sh,js}` —
    Playwright (chromium-only) Node probe that boots the production
    image on port 18093, navigates to `/`, checks `/sw.js`
    (soft-pass on 404 — forward-compat for Hicks's still-in-flight
    SW artefact), waits for
    `navigator.serviceWorker.getRegistration()` to yield an activated
    worker, then reloads and asserts
    `navigator.serviceWorker.controller != null` (the SW-took-control
    canonical assertion). Workflow at
    `.github/workflows/pwa-smoke.yml` (paths-filtered to the PWA
    surface + smoke files + Dockerfile). (Apone)
- **OAuth production setup runbook (Google + GitHub + Microsoft).**
    `docs/oauth-production-setup.md` — operator-facing playbook for
    provisioning OAuth client IDs/secrets in each of the three
    providers, mapping them to the canonical SSM key families
    (`/mahjong/prod/oauth/{google,github,microsoft}/{client_id,client_secret[,tenant_id]}`),
    quarterly rotation procedure, post-rotation validation
    checklist, Microsoft-specific quirks (`oid` claim is the
    stable PK; `tid=9188040d-…` distinguishes personal MSA
    accounts; `email` scope required for the `mail` claim on
    consumer accounts). Microsoft section unblocks Bishop's
    Wave 3 OAuth middleware. (Apone)
- **Cosign verify reusable workflow + pre-publish gate.**
    `.github/workflows/verify-signature.yml` — reusable
    `workflow_call` interface with `image-digest` (required),
    `expected-issuer` + `expected-identity-pattern` + `cosign-version`
    (defaults pinned to this repo's `sign-image.yml`). Wired into
    `release.yml` as a new `verify-signature` job between `smoke`
    (which now exposes the manifest-list digest as an output) and
    `release` — the release tag's GitHub Release is NOT cut for an
    unsigned image. Single source of truth for the expected-identity
    regex + cosign version pin; callers tomorrow (Argo CD pre-sync
    gates, Kyverno k8s admission policies) dial in via the same
    reusable. (Apone)

### Changed (Phase K Wave 2)
- `release.yml`: `smoke` job exposes new `outputs.image-digest`
    (resolved via `docker buildx imagetools inspect --format
    '{{.Manifest.Digest}}'`); new `verify-signature` job calls
    `./.github/workflows/verify-signature.yml`; `release` job's
    `needs:` is now `[smoke, verify-signature]`. Existing
    `permissions: contents: write, packages: read` covers the new
    job (no changes needed). (Apone)
- `.gitignore`: excludes Capacitor's regenerated platform
    directories (`mobile/{ios,android,node_modules,build,.gradle,*.tgz}`).
    (Apone)

## [0.10.0] — Phase J Wave 10 — 2026-05-24 (PR #46)

**Theme:** Final-pass polish — flake fixes, CSP Round 2 canary knob,
production runbook, end-to-end load test, multi-arch Docker image,
docs review. Phase J's tenth-and-final wave; J ships green at
**820/0/0** backend tests, zero-skip streak preserved.

### Added
- **Multi-arch Docker image (`linux/amd64` + `linux/arm64`).** Wave 4
    carry-over closed. `.github/workflows/docker-build.yml` adds
    `docker/setup-qemu-action@v3`, `PLATFORMS: linux/amd64,linux/arm64`
    env, and passes `platforms:` through to `docker/build-push-action@v6`.
    Manifest list digest surfaced in the workflow summary. Docs:
    `docs/docker.md` Wave-10 multi-arch section; `docs/sbom.md`
    cross-reference. (Apone)
- **End-to-end load test harness.** `tests/load/lobby-flood.js` (Node
    + `ws@^8` — no k6 dep). Three workloads: 100-concurrent lobby
    polling, 25-concurrent WS join, 5-concurrent 4-bot tournaments.
    Smoke run results in `docs/load-test-results.md` (Lobby p99
    525 ms / Join p99 555 ms / Tournament p99 2,520 ms, 0 % error
    rate on Debug build). (Apone)
- **Production deployment runbook.** `docs/production-deployment-runbook.md`
    (~26 KB): pre-flight checklist, image build/publish, DB init via
    the pre-rollout k8s Job, rolling update + readiness gates,
    rollback procedure, monitoring/alerting (Prometheus + Sentry +
    JSON logs), incident response playbooks (DB outage, rate-limit
    storm, OAuth provider down, magic-link queue stall, CSP
    regression). (Apone)
- **Docs index.** `docs/README.md` — landing page mapping each
    operator/dev/QA need to the right doc. (Apone)
- **CSP Round 2 — `style-src 'unsafe-inline'` canary knob.**
    `Observability/SecurityHeadersMiddleware.cs` gains
    `Security:CspStrictStyles` (default OFF). When set, `style-src`
    drops `'unsafe-inline'` while adjacent directives are byte-for-byte
    preserved. Constants intentionally remain permissive (pinned by
    Vasquez's `CspStyleSrcNoUnsafeInlineTests` contract suite). (Apone)
- **Tournament mode.** Multi-table tournaments + bracket UI + per-table
    auto-bot fill at start. (Bishop)
- **Replay v2 normaliser.** Forward-compat schema upgrade for the
    `Replay` table; old single-game replays auto-migrate to the
    multi-hand envelope. (Bishop)
- **Audit-log pruning service.** Background hosted service deletes
    `AuditEntry` rows older than `Audit:RetentionDays` (default 90).
    Configurable per provider. (Bishop)
- **Bot decision reasoning surface.** Each bot's pickup/discard
    decision is surfaced via `/api/games/{id}/bot-reasoning` for
    spectator transparency. (Bishop)
- **Database health detail.** `GET /health/detail` surfaces per-provider
    DB pool stats + last-migration timestamp. (Bishop)
- **e2e Playwright multi-arch sanity test.** Stand-alone test ensures
    the multi-arch image runs to completion under QEMU. (Vasquez)

### Fixed
- **`LateJoin_ReceivesAccumulatedSnapshot_OfPriorUpdates` flake** —
    `AutotableConnectionManager.GetStoredEntryCount(gameId)` aggregated
    across all `kind`s; translator `match` + `seat:N` entries inflated
    the count before Alice's `UPDATE things` ever landed. Fix: new
    `AutotableGameState.CountFor(string kind)` per-kind probe + new
    `GetStoredEntryCount(gameId, kind)` overload + `WaitForAsync` now
    THROWS on deadline expiry instead of silent-falsing. Pinned by a
    50× regression-gate test. (Apone)

### Build invariant
Backend gate: **820 / 0 / 0**. Zero-skip streak: **13 consecutive
green waves**.

## [0.9.0] — Phase J Wave 9 — 2026-05-23 (PR #45)

**Theme:** Reconnect-token rotation + table chat + i18n pattern
resources + CSP tightening + audit log + flake fix.

### Added
- **Reconnect-token rotation.** Bishop's `/api/reconnect/*` surface
    issues a new opaque token on every WS reconnect, invalidates the
    previous one server-side, and rejects reuse attacks. Persisted as
    `ReconnectToken` + `ReconnectAuditEntry`. Smoke test:
    `tests/smoke/token-rotation-smoke.sh`. (Bishop; smoke: Apone)
- **Table chat.** `POST /api/chat/send` + `GET /api/games/{id}/chat`
    + SignalR hub event. Per-route rate limit; profanity filter
    (`ChatProfanityFilter`); persisted as `ChatMessage`. Smoke test:
    `tests/smoke/chat-flow-smoke.sh`. (Bishop; smoke: Apone)
- **i18n pattern resources.** Yaku names + UI strings extracted to
    `Resources/Strings.{en,zh-CN,ja}.resx`. Frontend resource-key
    pattern in `src/frontend/autotable-src/i18n/`. (Bishop / Hicks)
- **CSP tightening.** `SecurityHeadersMiddleware` gains four operator
    knobs (`Security:CspStrict`, `Security:UseScriptNonces`,
    `Security:CspReportOnly`, `Security:CspReportUri`). Defaults
    backwards-compatible. `POST /api/csp-report` sink (`Observability/
    CspReportEndpoint.cs`) accepts legacy + Reporting-API envelopes;
    persists to `CspViolations` table (per-provider EF migration). (Apone)
- **Audit log + pre-rollout k8s migration Job.** `--migrate` CLI
    intercept in `Program.cs` runs EF migrations from a one-shot k8s
    `Job` with Argo CD `sync-wave: -1` + `hook: PreSync`. (Apone)
- **SBOM + Trivy CRITICAL/HIGH gate.** `.github/workflows/sbom.yml`:
    CycloneDX + SPDX SBOMs via `anchore/sbom-action@v0`, Trivy gate
    with `severity: CRITICAL,HIGH` + `exit-code: 1` + `ignore-unfixed:
    true`, SARIF upload to GitHub code-scanning, PR-summary comment.
    Docs: `docs/sbom.md`. (Apone)

### Fixed
- **`HotSeatSwap_PlayerToPlayer_PreservesGameState` flake** —
    tightened `bobSeated` `WaitForAsync` predicate to wait for both
    Bob's seat take AND Alice's seat release before asserting, so the
    post-take `FillEmptySeatsWithBotsAsync` doesn't race the assertion.
    Pure test-side fix. (Apone)

### Build invariant
Backend gate: **728 / 1 / 0** (one Bishop-owned profanity-filter
in-flight; resolved at Wave 10).

## [0.8.0] — Phase J Wave 8 — 2026-05-22

**Theme:** Production hardening.

### Added
- **Sentry SDK (backend + frontend).** `Sentry.AspNetCore` 6.5.0 wired
    through `Observability/SentryConfiguration.cs`; SignalR hub-method
    breadcrumbs via `SentryHubFilter`. Disabled by default — set
    `Sentry__Dsn` to enable. Frontend equivalent via `src/sentry.ts`
    + `@sentry/browser` 8.x, gated on `<meta name="sentry-dsn">`. See
    `docs/sentry.md`. (Apone)
- **Security headers middleware.** `SecurityHeadersMiddleware` stamps
    `X-Frame-Options`, `X-Content-Type-Options`, `Referrer-Policy`,
    and a Three.js-compatible `Content-Security-Policy` on every
    response. Parcel-hashed bundles get
    `Cache-Control: public, max-age=31536000, immutable`; index.html
    gets `no-cache`. (Apone)
- **Cloudflare-aware rate limiting.** `RateLimiting/RateLimitingExtensions.cs`
    now prefers `CF-Connecting-IP` over `X-Forwarded-For` when present
    so the rate limiter partitions per real client behind Cloudflare.
    (Apone)
- **Release workflow** (`.github/workflows/release.yml`) — on every
    `v*.*.*` tag push: waits for the ghcr.io image, runs the build +
    auth smoke, then creates a GitHub Release with the matching
    CHANGELOG section or auto-generated notes. (Apone)
- **Auth-flow smoke test** (`tests/smoke/auth-flow-smoke.sh`) — mints a
    `mahjong_pid` cookie via `POST /api/identity`, asserts idempotent
    refresh, probes `/api/auth/providers` and `/api/auth/me` (skips
    gracefully if the surface isn't yet registered). Wired into
    `docker-smoke.yml` nightly. (Apone)
- **Parcel + npm BuildKit cache mounts** in the Dockerfile — `npm ci`
    re-uses `/root/.npm`, parcel re-uses `/src/frontend/autotable-src/.parcel-cache`.
    CI rebuilds with no source changes drop from ~90s to ~20s. (Apone)
- **External Secrets templates** for staging/prod
    (`infra/k8s/overlays/{staging,prod}/secret-template.yaml`) — ESO
    `ExternalSecret` CRDs pointed at AWS Secrets Manager. (Apone)
- **Local dev secret generator** (`scripts/generate-dev-secrets.sh` +
    `appsettings.Development.example.json`). Idempotent; emits a
    `.env.dev` with strong random JWT/cookie keys. (Apone)
- **Docs:** `docs/sentry.md`, `docs/cloudflare.md`,
    `docs/secret-management.md`. (Apone)
- **Auth surface (preview).** OAuth (Google, GitHub), magic-link, and
    dev-login under `/api/auth/*`, plus persistence migrations for
    Sqlite / Postgres / SqlServer. (Bishop)
- **Rule presets surface (preview).** `POST /api/rule-presets` etc.
    with backend validation + frontend rule-presets pane. (Bishop)

### Build invariant
Backend gate: ≥554 tests passing. Wave 8 expanded the suite to **617
green** with the observability surface; the auth/rule-preset surface
adds further pending tests that gate Bishop's parallel work.

## [0.7.0] — Phase J Wave 7 — 2026-05-21 (PR #43)

**Theme:** Replay endpoint, accessibility, settings drawer, multi-DB,
Kubernetes overlays.

- Replay endpoint (`GET /api/replays/{gameId}`) + viewer (Bishop)
- Accessibility audit + WCAG 2.1 AA fixes (Hudson)
- Settings drawer + theme switching (Hicks)
- Multi-database support (Sqlite / Postgres / SqlServer) via
    `Persistence__Provider` (Bishop)
- k8s base manifests + staging/prod overlays (Apone)
- See `.squad/decisions/inbox/apone-phase-j-wave-7.md` for the deploy
    memo.

## [0.6.0] — Phase J Wave 6 — 2026-05-20 (PR #42)

**Theme:** Persistent player IDs + leaderboard + rate limiting + auth
UI + Playwright specs.

- `mahjong_pid` cookie minted by `POST /api/identity` (Bishop)
- Per-player leaderboard (`GET /api/leaderboard/top`) (Bishop)
- ASP.NET rate limiter: fixed-window anonymous + token-bucket api
    (Apone)
- Auth-aware UI shell (sign-in / sign-out chrome) (Hicks)
- Playwright e2e harness + first specs (Vasquez)

## [0.5.0] — Phase J Wave 5 — 2026-05-19 (PR #41)

**Theme:** Multiplayer matchmaking, profiles, stats, observability,
Playwright E2E.

- Public matchmaking lobby + Quick Match (Hicks)
- Player profile + display name + avatar color (Bishop)
- Personal stats panel (Bishop)
- Prometheus `/metrics` exposition + JSON structured logging (Apone;
    see `docs/observability.md`)
- Playwright config + first cross-browser specs (Vasquez)
- Secret audit (`docs/secrets.md`)

## [0.4.0] — Phase J Wave 4 — 2026-05-19 (PR #40)

**Theme:** Mobile responsiveness, reconnect tokens, CI hardening,
seed 40595, GameComplete reconciliation.

- Responsive layout + touch input (Hicks)
- Rejoin-token URL parameter (`?rejoin=…`) + server-side validation
    (Bishop)
- GitHub Actions: docker-build.yml, docker-smoke.yml, e2e-playwright.yml
    (Apone)
- Hand-50 seed 40595 fully passes with all rule presets (Hudson)
- GameComplete event reconciles the move-log against the server
    snapshot (Bishop)

## [0.3.0] — Phase J Wave 3 — 2026-05-18 (PR #39)

**Theme:** Docker deployment, sound, replay (foundation), WinResult
surfaces, /health.

- Multi-stage Dockerfile (parcel + dotnet publish + aspnet:10.0
    runtime; UID 1000 non-root; `/data` volume) (Apone)
- `GET /health` 4-field probe + Docker `HEALTHCHECK` (Bishop)
- Sound effects pipeline (Hicks)
- WinResult panel + move-log groundwork (Bishop)
- Replay-event recording (Bishop)

## [0.2.0] — Phase J Wave 2 — 2026-05-17 (PR #38)

**Theme:** Disconnect cleanup, N-hand game completion, UX polish.

- Disconnect cleanup: idle seats freed (Bishop)
- N-hand games (configurable hand count) with proper end-of-game
    flow (Hudson)
- "Concede" + "Resign" interactions (Hicks)

## [0.1.0] — Phase J Wave 1 — 2026-05-16 (PR #37)

**Theme:** Shanten claim gate, hot-seat swap, spectator camera lock.

- Shanten gating on Pong / Chow / Kong claims (Hudson)
- Hot-seat swap mid-game (Bishop)
- Spectator camera lock-on-table (Hicks)

## Earlier (Phases A–I) — not version-tagged

Phases A through I shipped on `main` without semver tags. Highlights:

- **Phase I** (PRs #33–#36): special-context wins (天和/地和/海底/河底/杠上开花),
    proper shanten counter, spectator/all-bots-watch mode, multi-game
    WebSocket routing, persistence hydration, result-modal pattern
    breakdown.
- **Phase H** (PRs #31–#32): V2 rules — NineTerminals, RobbingKong,
    stacked Big Wins, V2 design groundwork.
- **Phase G** (PR #30): bot pickup scheduler, sidebar lobby,
    privacy-mask cleanup.
- **Phase F** (PR #29): Changsha realism — manual pickup, variant
    switching, 3-tier bot engine.
- **Phases A–E**: initial Changsha rebuild on top of the
    `pwmarcz/autotable` engine, scoring & yaku catalogue, swap-call
    discipline, gang/chi/pong/ron implementations.

[Unreleased]: https://github.com/long2know/mahjong-autotable/compare/v0.15.0...HEAD
[0.15.0]: https://github.com/long2know/mahjong-autotable/compare/v0.14.0...v0.15.0
[0.14.0]: https://github.com/long2know/mahjong-autotable/compare/v0.13.0...v0.14.0
[0.13.0]: https://github.com/long2know/mahjong-autotable/compare/v0.12.0...v0.13.0
[0.12.0]: https://github.com/long2know/mahjong-autotable/compare/v0.11.0...v0.12.0
[0.11.0]: https://github.com/long2know/mahjong-autotable/compare/v0.10.0...v0.11.0
[0.10.0]: https://github.com/long2know/mahjong-autotable/compare/v0.9.0...v0.10.0
[0.9.0]: https://github.com/long2know/mahjong-autotable/compare/v0.8.0...v0.9.0
[0.8.0]: https://github.com/long2know/mahjong-autotable/compare/v0.7.0...v0.8.0
[0.7.0]: https://github.com/long2know/mahjong-autotable/compare/v0.6.0...v0.7.0
[0.6.0]: https://github.com/long2know/mahjong-autotable/compare/v0.5.0...v0.6.0
[0.5.0]: https://github.com/long2know/mahjong-autotable/compare/v0.4.0...v0.5.0
[0.4.0]: https://github.com/long2know/mahjong-autotable/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/long2know/mahjong-autotable/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/long2know/mahjong-autotable/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/long2know/mahjong-autotable/releases/tag/v0.1.0
