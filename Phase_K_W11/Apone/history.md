# Apone — Phase K Wave 11 history (wave-scoped)

> Wave-scoped excerpt of the persistent history at
> `.squad/agents/apone/history.md`. The full chronological
> record is the source of truth.

## Phase K Wave 11 — DevOps bring-up

Branch: `stlong/phase-k-wave-11-bringup`
Bringup-on commit (W10 close): `0c95748` (PR #56 — gate
2108/0/0).

### Deliverables (six)

1. **Prod Redis Terraform stack.**
   `infra/terraform/envs/prod/{main,variables,outputs,backend.example.hcl,terraform.tfvars.example}.tf`
   — NEW. Edge module with BLOCK-mode WAF + 90-day CloudFront
   log retention + ACM cert in us-east-1; Redis module at the
   prod tier (`cache.r6g.large`, `replica_count=1`, multi-AZ,
   `snapshot_retention_limit=7`, CMK KMS via
   `alias/mahjong-prod-elasticache`, AUTH token + TLS in
   transit, Sunday off-peak maintenance). Operator-fill
   `backend.example.hcl` (`mahjong-tfstate-prod` /
   `mahjong-tflock-prod`) + `terraform.tfvars.example`.
   Outputs include sensitive omnibus `redis_connection_string`
   + split-form `redis_auth_token`. `terraform validate`
   clean.

   Plus `infra/k8s/overlays/prod/redis-connection-string-secret.yaml`
   (NEW) — ESO ExternalSecret with 15-min refresh, mounting
   `Idempotency__Redis__ConnectionString` from SSM SecureString
   `/mahjong/prod/redis/connection-string`. Out-of-band (NOT
   in `kustomization.yaml` `resources:`; applied manually via
   `kubectl apply -f` once the prod EKS cluster bootstraps).

2. **Argo Rollouts auth-aware ingress.**
   `infra/k8s/overlays/prod/argo-rollouts-ingress-auth.yaml`
   (NEW). nginx-ingress `auth-url` / `auth-signin`
   subrequest pattern. The auth boundary is the existing
   oauth2-proxy + dex OIDC chain at
   `auth.mahjong.example.com/oauth2/*` (the same chain that
   fronts the prod app — see `docs/oauth-production-setup.md
   §4`). Path rewrite `/argo-rollouts(/|$)(.*)` → `/$2` so the
   dashboard SPA serves from `/` internally. TLS+HSTS
   inheritance via the prod ingress class. Host
   `mahjong.example.com`. Supersedes the W10 §4.3 placeholder
   warning.

3. **Terraform CLI pin bump.**
   `.github/workflows/dr-rehearsal.yml` `terraform_version`
   `1.9.8` → `1.10.5`. The bump is single-line because the
   modules pin only a range floor
   (`required_version = ">= 1.5.0"`) — the exact pin lives
   only in CI. New `docs/terraform.md §6 "Version policy"`
   codifies the discipline:

     - Range floor in modules → forward-compatible for
       operators running a newer CLI locally.
     - Exact pin in CI workflows → deterministic plan/apply
       in the lane that gates merges.
     - Quarterly bump cadence anchored on Wave bring-up
       (W8 = 1.9.8, W11 = 1.10.5, W14 = TBD).
     - Out-of-band CVE bumps owned by DevOps; tracked in the
       next monthly retro.
     - Lock file (`.terraform.lock.hcl`) committed per env
       stack, not per module.
     - `setup-terraform@v3` major-version pin (HashiCorp
       publishes — stable semantics).

4. **JWT rotation rehearsal harness.**
   `.github/workflows/jwt-rotation-rehearsal.yml` (NEW).
   `workflow_dispatch` only. Hard gate at step 1:
   `target_env != staging → exit 1` (no prod opt-in; prod
   stays operator-only). Inputs: `target_env` (must be
   `staging`), `new_key_label` (becomes the new JWKS `kid`),
   `archive_cleanup` (boolean — default false; deletes
   archived keys > 180 days old when true). Steps:

     1. Hard gate → refuse if not staging.
     2. AWS OIDC role assumption
        (`mahjong-staging-rotation-rehearsal`).
     3. Generate fresh RSA-4096 with `openssl genpkey`.
     4. SSM promote: active → previous; new → active;
        previous → `archive/<UTC-timestamp>`.
     5. Force ESO refresh (annotation tick on the
        ExternalSecret).
     6. Rolling restart `Deployment mahjong-autotable` in
        `mahjong-staging` namespace.
     7. JWKS validation loop (5-min timeout) asserting old
        kid PRESENT + new kid PRESENT + total keys ≥ 3
        (the W10 §3 invariant).
     8. Optional archive cleanup (when input is true).
     9. Emit `docs/jwt-rotation-rehearsal-YYYY-MM-DD.md`
        artefact with timings + JWKS state diff.

   Initial heredoc-based artefact generation broke YAML
   parsing (the `EOF` terminator inside a `run: |` block
   collides with YAML indentation rules — must be at column
   zero, which `run: |` makes impossible). Switched to
   `printf` calls with explicit newlines. Actionlint clean.

   Plus `docs/jwt-rotation-rehearsal.md` (NEW) — 8-section
   operator runbook: purpose / prereqs / trigger / what
   happens / dry-run guidance / failure-mode table / post-
   rehearsal review checklist / cross-refs.

5. **Multi-region prod-health-check matrix.**
   `.github/workflows/prod-health-check.yml` (REWRITTEN). The
   W10 single-region pattern (5-min cron, 3-strike-open,
   2-recovery-close, GitHub issue lifecycle) generalised to a
   `strategy.matrix.region` fan-out across `us-east-1`,
   `us-west-2`, `eu-west-1`, `ap-southeast-1`. Each matrix
   leg:

     - Resolves per-region target via
       `vars.PROD_BASE_URL_<REGION>` (yellow-flag step-summary
       if unset; falls back to global default).
     - Probes the same endpoints (`/healthz`, `/readyz`,
       `/metrics`, `/.well-known/jwks.json`) with the same
       assertions as W10.
     - Emits `verdict-<region>.json` via
       `actions/upload-artifact@v4`.

   Aggregator job downloads with `pattern: verdict-*` +
   `merge-multiple: true`, parses each verdict, maintains
   per-region HTML state markers in the prod-health-check
   issue body:

       <!-- prod-health-check:state region=X strikes=N recoveries=M -->

   Opens the issue when ANY single region's `strikes` hits
   `STRIKE_THRESHOLD=3`. Closes only when ALL four regions
   show `recoveries >= RECOVERY_THRESHOLD=2`. Actionlint
   clean.

   Plus `docs/edge-region-probes.md` (NEW) — 8-section
   operator runbook: purpose / topology / per-region target
   resolution / state-marker decoding / failure-mode playbook
   (1-region / 2-region / 4-region patterns, each with a
   first-look + an action path) / CloudFront edge mapping /
   manual reproduction / cross-refs.

6. **CHANGELOG + retro + memo + history.**

     - `CHANGELOG.md` — `[Unreleased]` flipped from W10 to
       W11; new `[0.20.0] — Phase K Wave 11 — 2026-09-XX (PR
       pending)` entry with Added / Changed / Fixed
       subsections covering all six deliverables.
     - `docs/retro-2026-09.md` (NEW) — September monthly
       retro. Template consistent with August (what shipped
       → WIP → lessons (4 entries) → action items table
       (carry into October 2026) → metric movement → cadence
       → cross-refs).
     - `Phase_K_W11/Apone/charter.md` + `history.md` —
       wave-scoped artefacts.
     - `.squad/decisions/inbox/apone-phase-k-wave-11.md` —
       six-decision memo (D1 prod Redis, D2 Argo auth
       ingress, D3 TF CLI bump, D4 JWT rehearsal, D5
       multi-region probe, D6 CHANGELOG / retro).

### Validation sweep (executed before commit)

```bash
export PATH="$PWD/.work/apone-w11-tools:$PATH"

# Workflows.
actionlint .github/workflows/jwt-rotation-rehearsal.yml \
           .github/workflows/prod-health-check.yml \
           .github/workflows/dr-rehearsal.yml
# → all clean.

# Terraform — modules + env stacks. Edge + DR-replication only
# validate from caller env stacks (they declare
# `configuration_aliases = [aws.us_east_1]` so standalone
# init complains about a "removed" provider alias).
for d in infra/terraform \
         infra/terraform/modules/{redis,github-oidc} \
         infra/terraform/envs/{staging,prod,dr-us-west-2}; do
    rm -rf "$d"/.terraform "$d"/.terraform.lock.hcl "$d"/terraform.tfstate*
    (cd "$d" && terraform fmt -check && \
        terraform init -backend=false -input=false >/dev/null && \
        terraform validate)
done
# → all clean.

# Kustomize.
kustomize build infra/k8s/overlays/prod/ >/dev/null
kustomize build infra/k8s/overlays/staging/ >/dev/null
# → both clean.
```

### Concurrent-agent observations this wave

The W10 retro noted that `.tool-*/` directories under the
repo root occasionally got wiped mid-wave by a concurrent
process (suspected `git stash --include-untracked` running
in the working tree). W11 reproduced the same pattern at
roll-out time — `.tool-terraform/`, `.tool-actionlint/`,
`.tool-helm/`, `.tool-kustomize/` were all wiped between
sequential bash commands. Workaround in force: install all
tools into `.work/apone-w11-tools/` (a path NOT covered by
the wiping pattern — `.work/` lives inside the repo tree
and is owned by the squad-git-lock infrastructure). PATH
prepend is the one-liner `export
PATH="$PWD/.work/apone-w11-tools:$PATH"`.

Defence in force: `.work/apone-w11-safe/` per-batch backup
directory was created at wave start (W10 carry-forward); no
restoration was actually needed this wave because the wiping
pattern targeted the `.tool-*/` dirs and not `infra/` or
`.github/`.

### Decisions worth carrying forward

- **Range-floor + exact-pin is the right TF version
  policy.** The W11 CLI bump was a one-line workflow edit
  because modules pin only `required_version = ">= 1.5.0"`.
  The exact pin lives only in CI. Operators running 1.11 or
  1.12 locally are not blocked; CI gets deterministic plans.
  Documented in `docs/terraform.md §6`.
- **Rehearse before the first quarterly rotation.** The W10
  retro flagged the first JWT rotation lands end-Sep. W11
  ships the rehearsal harness BEFORE the first real
  rotation. Generalisation: every new RECURRING operator
  drill should ship with a rehearsal harness before its
  first real execution.
- **Out-of-band ESO manifests are a feature.** Two
  ExternalSecret files (`jwt-keys-secret.yaml` and now
  `redis-connection-string-secret.yaml`) are intentionally
  NOT in `kustomization.yaml` `resources:` — they bind to
  env-specific KMS keys + SSM paths that don't exist in
  dev / preview environments. Documented in the file
  headers and `docs/redis-cluster.md §11.4`.
- **Multi-region probes need a failure-mode playbook.** The
  open-on-ANY-region / close-on-ALL-regions issue lifecycle
  is the right default, but the operator's first-look
  diverges sharply between 1-region trip (regional CDN
  problem) and 4-region trip (global outage). The runbook
  ships the per-pattern playbook so the on-call SRE doesn't
  waste minutes on the wrong first-look.
- **YAML heredoc inside `run: |` doesn't work.** Multi-line
  `cat <<EOF` inside a `run: |` block requires the `EOF`
  terminator at column 0, which YAML indentation rules
  forbid. Use `printf` with explicit `\n` escapes (or
  multiple `echo` lines) when generating multi-line artefact
  content from a workflow step.
- **The W10 `.tool-*/` wiping pattern persists.** Apone W11
  pre-emptively installed tools into `.work/apone-w11-tools/`
  (path NOT subject to the wiping). Apone W12 should keep
  the same `.work/apone-wN-tools/` convention; consider
  proposing a global agent-handoff-protocol guidance update.

### Handoffs into Wave 12

- **Prod Redis stack `terraform apply`.** The W11 stack is
  `terraform validate` clean but not yet `terraform
  apply`-ed — the apply is blocked on the prod EKS cluster
  cutover (the cluster, not Redis, is the W12 blocker).
- **Prod kustomization wiring.** Once the prod EKS cluster
  is live + the ExternalSecret is applied + the Secret
  `mahjong-redis-prod` is materialised, wire
  `envFrom: secretRef: mahjong-redis-prod` into the prod
  Deployment patch.
- **Prod Redis load-test re-baseline.** W10's load-test ran
  against `cache.t4g.micro`. Hudson re-runs the test suite
  against the new `cache.r6g.large` shape post-cutover.
- **Per-region R53 records for the matrix targets.** W11
  defaults the four `vars.PROD_BASE_URL_<REGION>` repo
  variables to the same root URL. W12 ships per-region R53
  records + flips the matrix to region-pinned endpoints for
  higher-resolution edge signal.
- **NetworkPolicy for the argo-rollouts dashboard.** The
  W11 auth-aware ingress is the auth boundary but does NOT
  prevent in-cluster bypass. W12 ships a `NetworkPolicy`
  denying ingress to the dashboard Service from outside the
  `argo-rollouts` namespace.
- **Second JWT rotation rehearsal run** ahead of the Q4
  prod rotation (mid-December 2026).
- **W14 Terraform CLI bump.** Per the new quarterly cadence
  (`docs/terraform.md §6`), the W14 anchor is the next
  bump. Likely 1.11.x → pinned at 1.11.x's stable patch.

### Apone-lane scope discipline (per W6 invariant)

This wave touched ONLY DevOps-lane paths:
`.github/workflows/{jwt-rotation-rehearsal,prod-health-check,dr-rehearsal}.yml`,
`infra/terraform/envs/prod/{main,variables,outputs,backend.example.hcl,terraform.tfvars.example}.tf`,
`infra/k8s/overlays/prod/{redis-connection-string-secret,argo-rollouts-ingress-auth}.yaml`,
`docs/{redis-cluster,argo-rollouts-setup,terraform,jwt-rotation-rehearsal,edge-region-probes,retro-2026-09}.md`,
`CHANGELOG.md`, `.squad/agents/apone/history.md`,
`.squad/decisions/inbox/apone-phase-k-wave-11.md`,
`Phase_K_W11/Apone/{charter,history}.md`. NO `src/**`, NO
`tests/**`, NO mobile source code, NO Helm chart touches
(W11 is post-W9 chart cutover; no chart-level work this
wave). Pre-push `git status --short` verification will
confirm zero out-of-lane staging.
