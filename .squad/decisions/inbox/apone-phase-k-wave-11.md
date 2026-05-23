# Apone — Phase K Wave 11 memo

**Branch:** `stlong/phase-k-wave-11-bringup`
**Date:** 2026-09-XX
**Author:** Apone (DevOps / Platform Engineer)
**Scope:** Prod Redis Terraform env stack + prod ESO
ExternalSecret, Argo Rollouts auth-aware ingress, Terraform
CLI pin bump 1.9.8 → 1.10.5 + new `docs/terraform.md §6`
version policy, JWT rotation rehearsal workflow + operator
runbook, multi-region prod-health-check matrix + operator
runbook, CHANGELOG bump to **0.20.0** + retro 2026-09.

---

## Decisions

### D1 — Prod Redis Terraform env stack at `cache.r6g.large`

**Why:** the W10 retro action item #1 calls out "Prod Redis
stack instantiation (multi-AZ + KMS rotation review)" as the
W11 entry point. The W10 Redis MODULE is prod-ready
(documented in `infra/terraform/modules/redis/README.md`); the
remaining work is the env-stack wiring + a deliberate prod-
tier shape decision.

**Shape decision:**

* **`node_type = cache.r6g.large`.** Graviton2 + memory-
  optimised. Sized against Hudson's W10 load-test baseline.
  CloudWatch `Evictions` is the bump-trigger if sustained
  pressure shows up; until then `r6g.large` is the sweet
  spot for the W10 idempotency-cache hot-set.
* **`replica_count = 1`.** Multi-AZ requires ≥ 1 replica.
  One replica in a second AZ is the prod baseline. Bump to
  2 only if read fan-out surfaces in the metrics (the W10
  IdempotencyStore is write-heavy — 1 replica is right).
* **`multi_az_enabled = true`.** Automatic failover ON.
* **`snapshot_retention_limit = 7`.** 7-day daily snapshots.
  Snapshots are a debug aid (post-mortem on a corrupted key
  space), not a recovery surface — idempotency keys have
  5-min TTL.
* **CMK KMS — `alias/mahjong-prod-elasticache`.** Customer-
  managed key for SOC-2 / annual rotation compliance.
* **AUTH token + TLS in transit.** Mirrors staging — the
  runtime auth path is identical across envs.

**ESO wiring decision:** the prod overlay ExternalSecret at
`infra/k8s/overlays/prod/redis-connection-string-secret.yaml`
uses the **omnibus connection-string** shape (`Idempotency__Redis__ConnectionString`
mounts the full `host:port,password=...` blob) — same as the
W10 staging Secret. The split-form (`/mahjong/prod/redis/{host,port,auth-token}`)
is the **rotation path** (W10 §3) and stays canonical for that
flow.

**Out-of-band manifest decision:** the prod
`redis-connection-string-secret.yaml` is NOT listed in
`infra/k8s/overlays/prod/kustomization.yaml` `resources:`. It
binds to a prod-only SSM path + CMK KMS that don't exist in
dev / preview overlays — kustomizing it would force per-env
parallel manifests (more files, more drift). The file's own
header documents the pattern. Identical to the W4
`jwt-keys-secret.yaml` precedent.

---

### D2 — Argo Rollouts auth-aware ingress

**Why:** the W10 §4.3 placeholder explicitly warned against
ingress-fronted dashboard access pending an auth-aware proxy.
The W10 retro action item #2 assigned the design to Vasquez
for W11. Apone+Vasquez collab decision (early W11): rather
than introduce a new identity provider (Pomerium, separate
oauth2-proxy instance), **reuse the existing prod
oauth2-proxy + dex OIDC chain** that fronts the production
app. The chain already covers @squad.mahjong + the allow-
listed external observers (`docs/oauth-production-setup.md
§4`).

**Manifest:** `infra/k8s/overlays/prod/argo-rollouts-ingress-auth.yaml`.

* `nginx.ingress.kubernetes.io/auth-url` → `https://auth.mahjong.example.com/oauth2/auth`
* `nginx.ingress.kubernetes.io/auth-signin` → `https://auth.mahjong.example.com/oauth2/start?rd=$escaped_request_uri`
* Host: `mahjong.example.com` (shares the prod app's host).
* Path: `/argo-rollouts(/|$)(.*)` → `rewrite-target: /$2`.
* TLS via the prod ingress class's wildcard cert; HSTS inherits
  from the parent ingress.
* Namespace: `argo-rollouts` (separate from `mahjong-prod`).
* Out-of-band (NOT in any `kustomization.yaml`); applied
  manually via `kubectl apply -f`.

**NetworkPolicy deferred to W12.** The auth-aware ingress is
the auth boundary at the cluster edge but does NOT prevent
in-cluster bypass (a pod in another namespace could hit the
Service directly). A `NetworkPolicy` denying ingress to the
dashboard Service from outside the `argo-rollouts` namespace
closes that gap — W12 candidate.

---

### D3 — Terraform CLI pin bump 1.9.8 → 1.10.5 + version policy

**Why:** the W10 retro action item #3 called out the TF CLI
pin at 1.9.8 (W8 vintage) as stale. The policy question was:
how high to bump (e.g. 1.15.x → bleeding edge) vs how
conservative (e.g. 1.10.x → one minor up).

**Decision:** bump to `1.10.5` (one minor up from 1.9.8) +
codify the policy.

* **Range floor in modules** stays at `>= 1.5.0`. Forward-
  compatible with operators running 1.11 / 1.12 / 1.15
  locally — they aren't blocked.
* **Exact pin in CI workflows** at `1.10.5`. The current
  surface is one file: `.github/workflows/dr-rehearsal.yml`.
* **Quarterly cadence anchored on Wave bring-up.** W8 = 1.9.8;
  W11 = 1.10.5; W14 = TBD (likely 1.11.x). One minor per
  quarter keeps the squad close to upstream without chasing
  every patch.
* **Out-of-band on CVE.** If HashiCorp ships a security
  patch, bump immediately outside the quarterly window. Owner:
  DevOps lane (Apone).
* **Lock file per env stack** (`.terraform.lock.hcl`).
  Already in force; documented for completeness.
* **`setup-terraform@v3` major-pin** — HashiCorp-published
  action, stable semantics within v3.x.y. Dependabot tracks
  the major bump.

Documented at `docs/terraform.md §6` (new section; Cross-refs
renumbered §6 → §7).

**Why NOT 1.15.x:** the W10 retro mentioned 1.15.x as the
target. Looking at the actual upstream cadence at W11 cut,
1.10 is the current STABLE minor; 1.11+ are unreleased or
fresh-cut. Picking the **current minor's stable patch**
follows the squad's "baseline = current minor's most recent
patch" rule — any breaking patches in the minor are already
shaken out; later patches are too recent for the lock-file
ecosystem to have caught up. The retro's `1.15.x` was an
optimistic forecast that didn't survive the Q3 release
calendar.

---

### D4 — JWT rotation rehearsal harness (staging-only)

**Why:** the W10 retro action item #4 + the W10 §3 90-day
cadence put the first prod JWT rotation at end-Sep 2026. The
on-call SRE walks into the rotation having never executed it
under the new cadence — high risk for a flubbed sequence
(any of the SSM-promote / ESO-sync / pod-restart / JWKS-
publish steps can drop the previous kid and invalidate
every live session).

**Decision:** ship a staging-only rehearsal workflow that
exercises the EXACT rotation sequence from
`docs/jwt-ssm-runbook.md §4`, so the on-call SRE can
practice the week before the real prod rotation.

**Workflow:** `.github/workflows/jwt-rotation-rehearsal.yml`.

* `workflow_dispatch` only (no auto-cron).
* **Hard gate at step 1: `target_env != staging → exit 1`.**
  No prod opt-in. Prod stays operator-manual.
* Inputs: `target_env` (must equal `staging`), `new_key_label`
  (becomes the new JWKS `kid`), `archive_cleanup` (bool —
  default false; deletes archived keys > 180 days when true).
* Steps mirror `docs/jwt-ssm-runbook.md §4` 1:1:
  1. Hard gate.
  2. AWS OIDC role
     (`mahjong-staging-rotation-rehearsal`).
  3. RSA-4096 key-pair generation.
  4. SSM promote (active → previous; new → active; previous →
     archive/`UTC-timestamp`).
  5. Force ESO refresh.
  6. Rolling restart `mahjong-autotable` Deployment.
  7. JWKS validation (5-min loop): old kid present + new kid
     present + total keys ≥ 3.
  8. Optional archive cleanup.
  9. Emit `docs/jwt-rotation-rehearsal-YYYY-MM-DD.md`
     artefact.

**Operator runbook:** `docs/jwt-rotation-rehearsal.md` (NEW).
8 sections including a failure-mode table (one row per
workflow step — symptom / cause / recovery), dry-run
guidance, post-rehearsal review checklist.

**Why staging-only:** the prod rotation is operator-manual on
purpose. The runbook owner is the on-call SRE, not the
workflow. The rehearsal builds muscle memory; it does NOT
replace the runbook. Prod opt-in is intentionally absent —
the hard gate prevents an accidental dispatch from rotating
prod keys in a window the on-call SRE didn't schedule.

**Heredoc-inside-`run: |`** doesn't work — YAML's
indentation rules forbid the `EOF` terminator at column 0,
which is what `cat <<EOF` needs. Use `printf` with explicit
newlines (or multiple `echo` lines).

---

### D5 — Multi-region prod-health-check matrix (4 regions)

**Why:** the W10 retro action item #7 was originally
targeted for W12 ("Synthetic edge probe (per-region — extends
prod-health-check)") but the squad pulled it into W11 to
deliver alongside the prod stack cutover. Single-region
probes give strong origin signal but zero edge signal — a
regional CloudFront PoP outage in ap-southeast-1 is invisible
to a us-east-runner probe.

**Decision:** generalise the W10 single-region workflow into
a 4-region matrix.

**Workflow:** `.github/workflows/prod-health-check.yml`
(REWRITTEN).

* `strategy.matrix.region: [us-east-1, us-west-2, eu-west-1, ap-southeast-1]`.
* Per-region target: `vars.PROD_BASE_URL_<REGION>`. W11
  default: each variable points at the same root URL
  (`https://mahjong.example.com`). W12 hand-off: ship per-
  region R53 records + flip to region-pinned endpoints.
* Same probe shape as W10 (`/healthz`, `/readyz`, `/metrics`,
  `/.well-known/jwks.json` + the same assertions).
* Each leg emits `verdict-<region>.json` via
  `actions/upload-artifact@v4`.
* **Aggregator job** downloads all verdicts with `pattern:
  verdict-*` + `merge-multiple: true`, parses each, maintains
  per-region HTML state markers:

      <!-- prod-health-check:state region=X strikes=N recoveries=M -->

* Issue lifecycle:
  - **Open** when ANY region's `strikes` hits
    `STRIKE_THRESHOLD=3`.
  - **Close** only when ALL four regions show `recoveries >=
    RECOVERY_THRESHOLD=2`.

**Operator runbook:** `docs/edge-region-probes.md` (NEW).
8 sections including the failure-mode playbook — 1-region /
2-region / 4-region patterns each get a first-look + an
action path. The per-pattern playbook is the runbook's
value-add — the on-call SRE's response is sharply different
between "1/4 tripped" (regional CDN problem) and "4/4 tripped"
(global outage).

**Why anchor on the existing `vars.PROD_BASE_URL_*` pattern:**
the W10 workflow consumed `vars.PROD_BASE_URL` (single
variable). The matrix variant uses one variable per region.
Operators provision the four variables in repo Settings →
Variables; an unset variable triggers a yellow-flag step-
summary warning + falls back to the global default so the
probe still runs.

---

### D6 — CHANGELOG bump to 0.20.0 + retro 2026-09

**Why:** wave-count-tracks-version (W10 = 0.19.0; W11 =
0.20.0). The W10 retro called out the `0.18.0` typo in
Stephen's W10 prompt; W11 sidesteps the same risk by reading
the W10 `CHANGELOG.md [0.19.0]` entry directly and bumping
to `0.20.0`.

**Changes:**

* `CHANGELOG.md` — `[Unreleased]` flipped to point at W11
  branch; new `[0.20.0] — Phase K Wave 11 — 2026-09-XX (PR
  pending)` entry with Added / Changed / Fixed subsections
  covering D1-D5.
* `docs/retro-2026-09.md` (NEW) — September monthly retro.
  Template consistent with August (what shipped → WIP →
  lessons (4 entries: §3.1 TF version policy, §3.2
  rehearse-before-first-rotation, §3.3 out-of-band ESO is a
  feature, §3.4 multi-region probes need failure-mode
  playbook) → action items table (carry into October) →
  metric movement → cadence → cross-refs).
* Wave hand-off artefacts: `Phase_K_W11/Apone/{charter,
  history}.md`.

---

## Carry-forward invariants

* **Range-floor + exact-pin TF version policy.** Codified at
  `docs/terraform.md §6`. Future TF CLI bumps follow the
  quarterly cadence anchored on wave bring-up.
* **Out-of-band ESO manifest pattern.** Codified across
  `docs/redis-cluster.md §11.4` + the file headers of
  `jwt-keys-secret.yaml` + `redis-connection-string-secret.yaml`.
  Future env-specific Secret-bound ExternalSecrets follow
  the same pattern.
* **Rehearse before the first quarterly drill.** The W11
  JWT rehearsal harness is the first instance. Future
  recurring operator drills (e.g. annual RDS major-version
  bump) should ship with a matching rehearsal harness BEFORE
  the first real execution.
* **Multi-region synthetics need a fan-out failure-mode
  playbook.** Codified at `docs/edge-region-probes.md §5`.
  Future fan-out synthetics (e.g. a multi-region DR-replica
  probe, a multi-region S3 endpoint probe) follow the same
  per-pattern playbook structure.
* **Identity discipline.** Per-command git env;
  `flock -w 120 9 ... 9>.work/squad-git-lock`; Co-authored-by
  trailer. Same as W10.

---

## Handoffs into Wave 12

* **Prod Redis stack `terraform apply`.** Blocked on prod
  EKS cluster cutover (cluster, not Redis, is the W12
  blocker).
* **Prod kustomization wiring.** Wire `envFrom: secretRef:
  mahjong-redis-prod` into the prod Deployment patch once
  the ExternalSecret materialises the Secret.
* **Prod Redis load-test re-baseline.** Hudson re-runs the
  W10 test suite against `cache.r6g.large`.
* **Per-region R53 records.** Provision four region-pinned
  endpoints + flip the matrix targets in repo Settings →
  Variables.
* **NetworkPolicy for argo-rollouts dashboard.** Close the
  in-cluster bypass gap.
* **Second JWT rotation rehearsal run** ahead of Q4 prod
  rotation (mid-December 2026).
* **W14 Terraform CLI bump** per the new quarterly cadence.

---

## Files modified

* `.github/workflows/dr-rehearsal.yml` (TF pin bump 1.9.8 →
  1.10.5).
* `.github/workflows/jwt-rotation-rehearsal.yml` (NEW).
* `.github/workflows/prod-health-check.yml` (REWRITTEN —
  single-region → 4-region matrix).
* `infra/terraform/envs/prod/main.tf` (NEW).
* `infra/terraform/envs/prod/variables.tf` (NEW).
* `infra/terraform/envs/prod/outputs.tf` (NEW).
* `infra/terraform/envs/prod/backend.example.hcl` (NEW).
* `infra/terraform/envs/prod/terraform.tfvars.example` (NEW).
* `infra/k8s/overlays/prod/redis-connection-string-secret.yaml` (NEW).
* `infra/k8s/overlays/prod/argo-rollouts-ingress-auth.yaml` (NEW).
* `docs/redis-cluster.md` (new §11 — prod sizing + ESO).
* `docs/argo-rollouts-setup.md` (new §5 — auth-aware ingress;
  subsequent sections renumbered §5 → §6 / §6 → §7 / §7 → §8
  / §8 → §9).
* `docs/terraform.md` (new §6 — version policy; §6 → §7).
* `docs/jwt-rotation-rehearsal.md` (NEW).
* `docs/edge-region-probes.md` (NEW).
* `docs/retro-2026-09.md` (NEW).
* `CHANGELOG.md` (`[Unreleased]` flip + `[0.20.0]` entry).
* `Phase_K_W11/Apone/charter.md` (NEW).
* `Phase_K_W11/Apone/history.md` (NEW).
* `.squad/agents/apone/history.md` (W11 entry appended).

NO `src/**`, NO `tests/**`, NO mobile source code, NO Helm
chart touches. Within lane.
