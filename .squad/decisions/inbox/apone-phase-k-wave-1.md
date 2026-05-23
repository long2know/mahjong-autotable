# Apone — Phase K Wave 1 memo

**Branch:** `stlong/phase-k-wave-1-bringup`
**Date:** 2026-05-24
**Author:** Apone (DevOps / Platform Engineer)
**Scope:** Supply-chain hardening (cosign keyless sign + verify), nightly load-test cron with regression alerting, multi-arch runtime smoke, CSP-strict-styles production rollout coordination, CHANGELOG backfill (J4–J10) + 0.10.0 bump, secret-rotation runbook.
**Test gate:** `dotnet test src/backend/Mahjong.Autotable.slnx --nologo` → **832 / 0 / 0** (baseline preserved — Wave-1 scope is pure DevOps + docs, no backend code touched).

---

## What shipped

### Task 1 — Cosign keyless image signing

**New workflow** `.github/workflows/sign-image.yml` triggered via
`workflow_run` after `docker-build` succeeds on `main` (plus
`workflow_dispatch` for one-off re-signs and tag-push promotions
flowing through the same upstream build).

- Installs `sigstore/cosign-installer@v3` pinned at cosign 2.4.1
  (modern keyless-by-default).
- Resolves the **manifest-list digest** (not a per-arch digest) via
  `docker buildx imagetools inspect … --format '{{.Manifest.Digest}}'`
  so a single signature covers both `linux/amd64` AND `linux/arm64`
  per-arch images that Wave 10 multi-arch builds publish.
- `cosign sign --yes` against the digest, using GitHub OIDC (`id-token:
  write`) as the keyless identity — no long-lived keys in the repo or
  in GHCR.
- Immediately verifies with `cosign verify --certificate-identity-regexp
  '^https://github\.com/long2know/mahjong-autotable/\.github/workflows/sign-image\.yml@refs/(heads/main|tags/v.*)$'
  --certificate-oidc-issuer 'https://token.actions.githubusercontent.com'`.
  Mismatch ⇒ workflow goes RED ⇒ alert.

**Why a separate workflow** (vs appending to `docker-build.yml`):

1. **Failure isolation.** Transient Fulcio outage shouldn't fail an
   otherwise-successful build; signing can be re-run independently.
2. **OIDC scope.** `id-token: write` is confined to the signing job's
   blast radius, never granted to the build pipeline.

**Docs:** `docs/image-signing.md` — operator + auditor verification
runbook. Covers verify-by-digest (production gate) AND verify-by-tag
(CI smokes), Rekor transparency-log evidence trail, failure modes,
and the audit checklist.

### Task 2 — Nightly load-test cron

**New workflow** `.github/workflows/load-test-nightly.yml` (daily
02:00 UTC; `workflow_dispatch` for ad-hoc runs).

- Brings up the production-shaped `docker-compose.yml` stack on a
  GitHub-hosted runner, waits for `/health`, runs the Wave-10
  `tests/load/lobby-flood.js` via a new helper.
- **Helper:** `tests/load/run-and-compare.sh`:
  - Persists each run's JSON output under `.work/loadtest/result-<ts>.json`.
  - Maintains a `latest.json` symlink that points to the prior run.
  - On each new run, parses the prior JSON, computes `(curr - prev) / prev * 100`
    for each workload's p99 latency, and exits **`2`** when any
    workload regresses by more than `$REGRESSION_PCT` (default 25 %).
  - Appends a Markdown row to `docs/load-test-results-history.md` with
    p99 numbers, max error rate, regression verdict, and a "ref:
    <prior-file>" note for traceability.
- **Alerting on regression:**
  - **Sentry event** — the helper itself POSTs a `level: error`
    Sentry event to the project DSN (if `$SENTRY_DSN` is set) so
    existing alert rules fan out automatically.
  - **Email** — `dawidd6/action-send-mail@v3` step in the workflow
    (only fires when `secrets.SMTP_USERNAME` is set; degrades to
    workflow-summary note otherwise).
  - Workflow ends RED on regression even if alerts couldn't fire,
    so the Actions dashboard surfaces the failure unconditionally.

**Wrapper exit-code contract:**

| RC | Meaning | Workflow outcome |
| --- | --- | --- |
| 0 | run completed, no regression beyond threshold | ✅ green |
| 1 | setup / runtime / parse failure | ❌ red — investigate |
| 2 | p99 regression beyond threshold | ❌ red — alert fires |

**History file** (`docs/load-test-results-history.md`) is **created on
first run** — auto-bootstrapped header with the table schema, then
each subsequent run appends one row. The file is uploaded as a
workflow artefact alongside the raw JSON for the 30-day retention
window.

### Task 3 — Multi-arch runtime smoke

**New workflow** `.github/workflows/multi-arch-smoke.yml` triggered
via `workflow_run` after `docker-build` succeeds on `main`.

Matrix:

| platform | runner | how |
| --- | --- | --- |
| `linux/amd64` | `ubuntu-latest` | native |
| `linux/arm64` | `ubuntu-latest` | QEMU via `docker/setup-qemu-action@v3` |

> **Native `ubuntu-24.04-arm` was considered** but GitHub's arm64-hosted
> runner isn't yet whitelisted to this repo at the time of this wave;
> QEMU is the portable fallback. To upgrade to native later, swap
> `runner: ubuntu-latest` → `runner: ubuntu-24.04-arm` and set
> `use_qemu: false` for the arm64 row. No other changes needed.

Per-arch smoke checks (each one a separate workflow step so the
failure is precise):

1. **Resolve per-arch digest** — `docker buildx imagetools inspect`
   with a `jq` query to pull the platform-specific digest out of the
   manifest list. Falls back to the tag if the image is single-arch.
2. **Pull + `docker run`** — `--platform linux/{amd64,arm64}` +
   `-e Security__CspStrictStyles=true` + `-e Security__CspReportUri=/api/csp-report`.
   Running with the strict-styles knob ON exercises the **production
   config Bishop is wiring up** in Phase K (see Task 4).
3. **`/health` 200 + four-field shape** (`status` / `buildSha` /
   `uptime` / `version`).
4. **`POST /api/identity`** → cookie minted + `playerId` in body.
5. **`GET /api/auth/providers`** → 200 (Bishop's surface) or 404
   (forward-compat soft pass).
6. **CSP header assertion** — `curl -sSI /health` → grep the
   `Content-Security-Policy(-Report-Only)?` line, confirm `style-src`
   does NOT carry `'unsafe-inline'`. Proves the strict-styles knob is
   honoured at runtime.
7. **`POST /api/csp-report`** → 204 + container-log `CSP violation`
   line within 5 s (proves persistence path is wired).

### Task 4 — CspStrictStyles ENFORCE in production config + CSP-report smoke

**Coordination contract with Bishop:** he owns the
`appsettings.Production.json` / overlay edit that sets
`Security:CspStrictStyles=true` (the knob ships default-OFF as Wave
10 documented; production is the canary path). My scope is:

1. **`tests/smoke/csp-report-smoke.sh`** (new) — synthetic violations
   in BOTH envelopes (legacy `application/csp-report` + modern
   `application/reports+json`). Confirms 204 and tails the container
   log for the `CSP violation` warn line that `CspReportEndpoint`
   emits inside the same scope that calls `SaveChangesAsync` (so the
   log line is a safe proxy for "row hit the DB").
2. **`multi-arch-smoke.yml`** exercises the production-config flag
   by running the image with `Security__CspStrictStyles=true` and
   asserting the runtime CSP header strips `'unsafe-inline'` from
   `style-src` (proves Bishop's setting works on both architectures).

**Open dependency:** Bishop's production config commit. Until that
lands, the runtime default is OFF and prod will still emit the
permissive CSP — the smoke confirms only that the IMAGE supports the
knob, not that prod has flipped it. This is the deliberate canary-
rollout pattern locked in Wave 9/10.

### Task 5 — CHANGELOG retroactive update + 0.10.0

`CHANGELOG.md` updated:

- Phase J Wave 9 added (was missing — the file stopped at Wave 8).
  Theme: reconnect-token rotation + table chat + i18n + CSP
  tightening + audit log + flake fix.
- Phase J Wave 10 added: tournament mode + replay v2 + multi-arch
  Docker + load-test harness + production runbook + flake fix.
- **Version cursor advanced to `0.10.0`** (J shipped 10 waves; the
  version tracks the wave count, per the preamble convention).
- `[Unreleased]` section now reflects Phase K Wave 1 work in
  progress (this branch).
- Reference link footnotes updated (`v0.10.0` / `v0.9.0` compare
  URLs added).

### Task 6 — Production secret-rotation runbook

`docs/secret-rotation.md` (new). Single-document handbook covering:

- **Rotation matrix** — cadence, blast radius, rollback budget per
  secret class.
- **OAuth client secrets** (Google + GitHub, quarterly): two-value
  overlap window via the provider console + AWS Secrets Manager
  promotion + ESO force-sync + rolling restart + validation via
  `auth-flow-smoke.sh`.
- **DB connection strings** (annual): `ALTER USER … WITH PASSWORD`
  → Secrets Manager update → ESO sync → rolling restart → drop old
  user after 7-day rollback window.
- **Sentry DSN** (never except compromise): DSN is public-ish; cost
  of rotation > benefit.
- **Reconnect-token signing key** (never except compromise):
  rotation invalidates all live sessions — single-key signer, no
  overlap window. Announcement + forced sign-out is the only safe
  procedure.
- **Magic-link signing key** (never except compromise): same shape
  as reconnect-token key.
- **Validation summary**, **audit/retention**, **calendar** with
  recommended Q1/Q2/Q3/Q4 rotation dates.

References ESO / Vault / AWS-Secrets-Manager flows from Wave 5/6
(`secret-management.md`, `secrets.md`). Cross-links
`production-deployment-runbook.md`, `kubernetes.md`, `image-signing.md`.

---

## Patterns locked for future DevOps work

- **Cosign keyless via `workflow_run`.** OIDC-signing workflows
  should be **separate** from the build workflow so `id-token: write`
  is confined to the signing blast radius. Trigger via
  `workflow_run: types: [completed]` + `if: github.event.workflow_run.conclusion == 'success'`.
- **Sign the manifest list, not the per-arch image.** One signature
  covers both `linux/amd64` and `linux/arm64`. The per-arch images
  inherit the attestation via the manifest list.
- **Verification regex anchors at the workflow path.** If you rename
  `sign-image.yml`, every consumer's verify regex breaks — keep the
  filename stable. The regex accepts BOTH `refs/heads/main` AND
  `refs/tags/v.*` so tag-push builds (release rehearsals) verify the
  same way as rolling main builds.
- **Load-test wrapper exit-code contract.** RC=0 pass, RC=1 setup
  failure, RC=2 regression. The CI workflow uses `set +e` + an
  explicit `exit 0 / exit 1` mapping so the regression case can run
  cleanup + artefact upload before the workflow goes RED via a final
  "fail on regression" step. Pattern: defer the actual failure until
  after every alerting/observability step has fired.
- **Symlink-based "latest prior result" pointer.** Cron workflows
  that compare-to-previous shouldn't try to read GitHub's artefact
  store mid-run (it's slow and rate-limited). Local on-disk
  state + workflow-artefact upload is the right shape; the artefact
  is the persistence boundary, the local file is the working state.
- **Forward-compat smoke pattern (now extended to CSP).** Smoke
  scripts that probe a surface which may not yet be GA soft-pass on
  404 and hard-fail on 5xx / invariant-violation. Five smokes now
  follow this pattern: `docker-build`, `auth-flow`, `chat-flow`,
  `token-rotation`, **`csp-report`** (Wave-1).
- **Per-script unique smoke ports** — 18080 docker-build, 18081
  auth-flow, 18082 chat-flow, 18083 token-rotation, **18084
  csp-report (Wave-1)**.
- **CHANGELOG version cursor = wave count.** Each phase's wave count
  advances the minor version. Phase J = 0.1.0 → 0.10.0. Phase K
  will open at 0.11.0 (first K wave merged) and advance per K
  wave merged thereafter. The preamble paragraph explains the
  pattern so future devs don't second-guess it.
- **Secret-rotation matrix** — cadence column drives the rotation
  calendar; blast-radius column drives the maintenance-window
  decision. "Never except compromise" is a deliberate cadence (not
  a missing one); document the trigger conditions instead of the
  schedule.

---

## Open items / handoff

1. **Bishop** owns the production `appsettings.Production.json`
   overlay edit that flips `Security:CspStrictStyles=true`. My
   `multi-arch-smoke.yml` already runs with the knob ON to prove the
   image supports it; once Bishop's config lands the runtime CSP in
   prod tightens automatically.
2. **Hicks** owns the inline-style-free bundle. When that lands,
   Bishop can flip `Security:CspReportOnly=true` first (24 h canary),
   then flip `CspReportOnly=false` + `CspStrictStyles=true` to
   enforce. The canary path is documented in
   `docs/load-test-results.md` (re-used surface — same canary knob
   pattern).
3. **Operator** action needed BEFORE the first `sign-image.yml`
   run on prod: ensure GHCR is OIDC-whitelisted (it is, by default,
   for the same-org actor — no special setup, but the FIRST run is
   the place to validate via the verify step's exit code).
4. **Operator** action for nightly-load-test alerts: configure repo
   secrets `SMTP_*` (or `ALERT_EMAIL_TO`) + `SENTRY_DSN` if not
   already set. Without these the workflow still goes RED on
   regression and the artefact is preserved; only the proactive
   notifications need the secrets.
5. **CHANGELOG link footnotes** assume git tag conventions
   (`v0.10.0`, `v0.9.0`). When Stephen actually tags 0.10.0 the
   compare URLs will resolve; until then they 404. Per
   Keep-a-Changelog convention this is fine.

---

## File summary

**New:**

- `.github/workflows/sign-image.yml` — cosign keyless signing workflow.
- `.github/workflows/load-test-nightly.yml` — nightly load-test cron + alerting.
- `.github/workflows/multi-arch-smoke.yml` — per-arch runtime smoke after build.
- `tests/load/run-and-compare.sh` — load-test wrapper with regression compare + history append.
- `tests/smoke/csp-report-smoke.sh` — CSP-report endpoint synthetic-violation smoke.
- `docs/image-signing.md` — cosign + Rekor verification runbook.
- `docs/secret-rotation.md` — production secret-rotation runbook.

**Modified:**

- `CHANGELOG.md` — added Phase J Wave 9 + Wave 10 entries, bumped `[Unreleased]` to reflect Phase K Wave 1, version cursor advanced to 0.10.0, link footnotes updated.
- `.squad/agents/apone/history.md` — Phase K Wave 1 entry (this wave).

**NOT mine — left untouched in working tree (per author hygiene):**
prior `.github/workflows/squad-*.yml` (orchestration noise from
earlier sessions), `.copilot/skills/error-recovery/`,
`.tool-actionlint/`, `.work/`. No `src/backend/**`, `src/frontend/**`,
`Dockerfile`, `appsettings.*` changes — Wave-1 scope is pure DevOps +
docs by design. Bishop owns the production-config flip; Hicks owns
the frontend inline-style cleanup; coordination contracts are in §
Open items.
