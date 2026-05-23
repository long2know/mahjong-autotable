# Apone — Phase K Wave 5 decision memo

> Author: Apone (DevOps)
> Date: 2026-05-28
> Branch: `stlong/phase-k-wave-5-bringup`

## Mission

Continue the Wave-4 supply-chain + secrets posture work into Wave-5
by **(a)** unifying SLSA provenance + SBOM under one in-toto
predicate, **(b)** tightening the Kyverno admission policy to
require the SLSA attestation, **(c)** mirroring the Wave-4 prod
JWT-keys ExternalSecret into staging, **(d)** shipping a
retroactive secrets-history sweep workflow, **(e)** automating
HSTS preload-readiness probing, and **(f)** unblocking the Wave-6
"<30 min clean prod env" target with a Terraform bootstrap module.

## Decisions

### 1. Generic SLSA generator over container generator

**Decision.** Replace
`generator_container_slsa3.yml@v2.0.0` with
`generator_generic_slsa3.yml@v2.0.0`. Pass a base64-encoded
`sha256sum`-format subjects list containing the image manifest
digest AND the CycloneDX SBOM file digest.

**Alternatives considered.**
- Keep the container generator + emit a SECOND attestation for
  the SBOM via cosign's `attest-blob` subcommand. Rejected: two
  parallel attestation flows is exactly the audit-gap we're
  closing. An auditor reading the SLSA predicate could not
  cryptographically confirm the SBOM in their hand came from the
  same build run.
- Use a homegrown `intoto-attest` wrapper. Rejected: defeats the
  SLSA L3 isolated-builder guarantee (the homegrown wrapper
  would run on the same runner as the build).

**Trade-offs.**
- The generic generator does NOT auto-publish to the OCI registry
  as a sidecar attestation. We mitigate with a follow-up
  `cosign attest --type slsaprovenance1` job (`attest-oci`).
- Wave-4 attestations (single-subject, container generator)
  remain in Rekor forever and remain verifiable with the
  Wave-4 `slsa-verifier verify-image` invocation. Forward
  artefacts use the Wave-5 `verify-artifact` shape per the
  updated `docs/slsa-provenance.md` §6.

### 2. Kyverno `attestations:` block — content pin in addition to subject pin

**Decision.** The new `attestations:` block in
`infra/k8s/policies/kyverno-cosign-verify.yaml` requires
`predicateType https://slsa.dev/provenance/v1`, with three CEL
`conditions:` evaluating
`buildDefinition.externalParameters.workflow.{repository,path}`
AND `runDetails.builder.id` (regex). Inside `attestations:` we
ALSO pin the attestor identity to the
`slsa-github-generator/.../generator_generic_slsa3.yml@refs/tags/v*`
subject.

**Why both signer pin AND content pin.** Belt-AND-suspenders.
A predicate signed by the correct generator for a DIFFERENT
repo (e.g. someone else's fork) would pass the signer-identity
check but fail the `workflow.repository` content check. A
predicate with this repo's fields but signed by a non-generator
identity would fail the subject pin. The two layers together
exclude both attack surfaces.

**Rollback.** If the SLSA workflow flakes during an emergency
hotfix, the operator comments out the `attestations:` block,
reapplies the ClusterPolicy, ships the hotfix, then restores
the block. The cosign-signature gate (`attestors:`) still fires
so admission falls back to the Wave-3 floor, not to "no
verification".

### 3. Staging mirrors prod for JWT-keys data plane

**Decision.** Wave-N+1 mirroring rule: any prod-only data plane
shipped in wave N must be mirrored into staging in wave N+1.
Wave-4 shipped `mahjong-jwt-keys` (prod) → Wave-5 ships
`mahjong-jwt-keys-staging`. Same 15-min refresh, same
rotation-state-named SSM parameters under
`/mahjong/staging/auth/jwt/`. Wired into the staging
kustomization as `resources:` + `envFrom` patch.

**Why mirror at all.** Bishop's `jwt-rotation-smoke.sh`
targets staging by default; without the array-binding
ExternalSecret the smoke would only ever exercise the
omnibus's singular-key fallback. Wave-5 closes the
gap so the array-binding code path is exercised in staging
BEFORE it's needed in prod.

### 4. `workflow_dispatch`-only for history sweep

**Decision.** `.github/workflows/secrets-history-sweep.yml`
runs ONLY on `workflow_dispatch`. NOT on PR / push / schedule.

**Rationale.**
- The scan walks the full commit graph (`fetch-depth: 0`) →
  5-30 minutes runtime on a mature repo. Running on every PR
  would burn runner minutes re-scanning history that hasn't
  changed.
- Historical-commit findings require a rotate-then-purge
  response (a non-trivial operator action) — should always
  be intentional, never accidental.
- The W4 `secrets-scan.yml` already covers forward motion +
  nightly drift. The sweep is a quarterly / post-incident
  gate, not a per-PR gate.

### 5. Sticky-issue alerting pattern for cron probes

**Decision.** `hsts-readiness-check.yml` uses a sticky-issue
mechanism: on failure, search for an issue by EXACT title
match, open if absent / update if present / re-open if closed.
On recovery, close with a comment.

**Rationale.** A naïve "create issue on failure" workflow
spams an issue per failed run (one per day during an ongoing
outage). The sticky pattern gives a stable issue number to
reference + automatic close-on-recovery. Reusable template for
future cron-driven probes (proposed Wave-6 JWT-rotation soak,
multi-region health check, etc.).

### 6. Terraform module — bare-minimum AWS footprint, no cluster add-ons

**Decision.** `infra/terraform/` provisions VPC + EKS + RDS +
ECR + GitHub-Actions OIDC role. Cluster add-ons (ALB
controller, cert-manager, ESO, Kyverno) are deliberately NOT
in the terraform module — they ship via `helm install` in the
post-bootstrap runbook (`README.md` §3).

**Rationale.**
- Terraform manages cluster infrastructure; helm manages
  cluster workloads. Mixing the two in one tfstate makes
  add-on upgrades require `terraform apply` cycles (slow +
  drift-prone).
- Each add-on has its own IAM/CRD coupling that's clearer to
  audit per-helm-chart than buried in a 600-line terraform
  file.
- The "<30 min env" target separates infra provision (~25
  min, terraform-bottlenecked by EKS) from add-on install
  (~5 min, parallelisable helm calls). Together inside the
  budget.

**State backend bootstrap.** Chicken-and-egg problem: terraform
can't create the bucket it stores its own state in.
Operator-driven one-time `aws s3api create-bucket` + `aws
dynamodb create-table` per environment, then `terraform init
-backend-config=backend-${env}.hcl`. Documented in
`README.md` §1.1.

## Lock-step invariants (canonical signer URLs)

Six files must update together if `sign-image.yml` OR
`slsa-provenance.yml` is renamed:

1. `.github/workflows/sign-image.yml` (the signer itself).
2. `.github/workflows/verify-signature.yml` (default
   `expected-identity-pattern`).
3. `infra/k8s/policies/kyverno-cosign-verify.yaml`
   `attestors:` block.
4. `infra/k8s/policies/kyverno-cosign-verify.yaml`
   `attestations:` block (NEW Wave-5).
5. `infra/k8s/overlays/prod/kyverno-enforce-patch.yaml`
   `attestors:` block.
6. `--source-uri` arg in `docs/slsa-provenance.md` §4.

## Out-of-scope / Wave-6 handoff

- **Tighten the GitHub-Actions OIDC role to least-privilege.**
  Current `mahjong-${env}-github-deploy` role has broad
  `ecr:*` / `eks:Describe*` / `ssm:Get*` grants for the
  bootstrap. Wave-6 audit hardening should narrow these to the
  exact actions the deploy workflow needs.
- **Multi-region terraform module** — current module is single-
  region; a DR-region equivalent ships as a Wave-6+ extension
  by copying `prod.tfvars` → `dr-us-west-2.tfvars` with a
  non-overlapping `/16`.
- **Cluster add-ons in helm chart of charts** — a meta-chart
  could ensure idempotent install ordering of ESO →
  cert-manager → AWS-LBC → Kyverno. Manual sequence in
  `README.md` §3 for now; meta-chart is a Wave-6+ DX
  improvement.
- **Route53 + ACM + WAF** — domain-bound; ship in a separate
  terraform module once `mahjong.example.com` is registered.
- **Pre-prod canary** — proposed Hudson Wave-6 work to lean on
  the now-symmetric staging surface for array-binding
  regression detection.

## Tests + linters run

- `actionlint` (v1.7.7) on `.github/workflows/{slsa-provenance,sbom,secrets-history-sweep,hsts-readiness-check}.yml`
  → all clean.
- `python3 -c "import yaml; yaml.safe_load_all(...)"` on
  `infra/k8s/policies/kyverno-cosign-verify.yaml`,
  `infra/k8s/overlays/staging/{jwt-keys-secret,kustomization}.yaml`
  → all parse clean.
- `terraform fmt -recursive` on `infra/terraform/` → applied.
- `terraform validate` (v1.9.8) on `infra/terraform/` →
  Success! The configuration is valid.
- `bash -n` on inline shell in all new workflows → clean.
- Backend gate: baseline 1232 / 0 / 0 preserved (`src/backend/**`
  untouched).
