# Phase K — Wave 21 Summary

- **Branch:** `stlong/phase-k-wave-21-bringup`
- **Base:** `main` @ `bbd3f6c` (post-W20 ship)
- **Head (pre-Scribe):** `6d8aa93` (Vasquez QA bring-up — 4th and last bring-up commit)
- **Date:** 2027-04-XX (late-April 2027 window; ~25 h elapsed since the W18 merge to `main` at the W21 LH13 re-evaluation)
- **Final gate:** **4846 passed / 0 failed / 0 skipped** (+209 over W20 close 4637; +3,424 over W6 baseline 1422 = **+240.8 %**; gate is now **3.41× the W6 baseline**)
- **Zero-skip streak:** **36 consecutive waves** (J.1–J.10 + K.1–K.21)
- **Lane-discipline:** **`checked=4 violations=0` at Scribe pre-flight** — **11th consecutive 0-violation wave** on the W21 tip (W11→W21 inclusive). **11th-consecutive-wave milestone.**
- **Identity hardening:** **16th consecutive clean wave** (per-invocation `git -c user.name=X -c user.email=Y`)
- **Concurrency mutex:** **12th consecutive fully-adopted wave** of `flock -w 120 9 ... 9>.work/squad-git-lock` — **atomic flock pipeline (stage + commit + push inside SINGLE block) honoured by ALL 4 bring-up agents at W21** (second consecutive wave with 4-for-4 atomic-flock compliance after the W20 first occurrence).
- **Coordinator-direct INTERVENTIONS:** **ZERO for 16 consecutive waves** (W6 → W21) — the §6.5 framing remains intact. **W21 saw NO Coordinator-direct EXECUTIONs either**; every agent landed clean and the one pre-existing test failure (Apone's mobile-package.json bump breaking Vasquez's W20 substring pin) was self-repaired in-lane by Vasquez via forward-broadening.
- **Coordinator-direct EXECUTIONS:** **3 cumulative across 3 waves (W17 + W18 + W19); W20 contributed zero; W21 contributes zero** — recipe propagation continues to hold at 2 consecutive zero-EXECUTION waves.
- **Three-renderer-big hold-line:** **11th consecutive wave** at 406,635 B (W11→W21) — **bandwidth-rebalancing 11th-wave milestone; cumulative W6 → W21 −44.9 % unchanged**.
- **`shared_files` registry:** **7 consecutive waves unchanged** (W15→W16→W17→W18→W19→W20→W21; 8 entries; late-mature steady state confirmed for the 4th wave running).
- **SLSA-3 sweep:** **REPO-WIDE COMPLETE held across W20→W21** (Vasquez-lane sweep + apone-lane sweep = ~206 pins / ~43 workflows; `slsa-github-generator@v2.0.0` remains the only tag-pinned exception per the W16 `__BUILDER_ID` regex contract). No new pins required at W21 because no new workflows were added net of the existing pin coverage; the helm-release.yml NEW workflow uses cosign-keyless signing which is verified via OIDC certificate-identity-regexp (not a pinned third-party action SHA).
- **NEW W21 — Argo Rollouts complete trilogy:** **W19 install runbook + RBAC + namespace prereqs → W20 backend BlueGreen template (333-line manifest + 305-line doc with 8-row Canary↔BlueGreen decision matrix) → W21 frontend Canary template (`infra/k8s/base/argo-rollouts/frontend-canary.yaml` NEW with 4 weight steps 5/25/50/100 + 10-min pause between each + AnalysisRun gates + `frontend-canary-error-rate` AnalysisTemplate with 0.5 % error-rate threshold over 1m rolling window).** Every workload class (backend + frontend) now has a documented progressive-delivery strategy template wired by W21.
- **NEW W21 — Helm release pipeline:** `.github/workflows/helm-release.yml` triggers on `helm-v*` tag push; builds chart with `helm package` → `helm push` to `oci://ghcr.io/long2know/charts`; keyless cosign sign via GitHub OIDC; in-job `cosign verify` against the OIDC certificate-identity-regexp `helm-release.yml@refs/tags/helm-v.*`. **First time the Helm chart has a signed release path.**
- **NEW W21 — Vasquez §9 stash-isolation directive codified.** The W20 Apone mid-task `git reset --hard` lesson formalised into a 5-sub-rule standing directive (`docs/agent-handoff-protocol.md §9.1` NEW). The W20 incident-recovery convention (renamed-stash via `<agent>-w<N>-…-shield-…`) is now a hard prerequisite carried by every bring-up agent's prompt template.

---

## 1. W21 commit table

| SHA       | Lane / Author                                       | Files | +Lines | −Lines | Headline |
|-----------|-----------------------------------------------------|-------|--------|--------|----------|
| `55fc04e` | **Apone (DevOps)** `<apone@squad.mahjong>`          | 13    | 2,008  | 21     | **Argo Rollouts frontend Canary template** (5/25/50/100 weight steps + 10-min AnalysisRun gates + 0.5 % error-rate AnalysisTemplate over 1m rolling) + **Kyverno W21 audit-mode pair** (`require-resource-limits` 2-sub-rule + `disallow-host-paths`; 5-day grace started; W22 enforce-flip pre-wired) + **us-east-1 auto-rollback.tf** (opt-in safety net with 3 dials: `enable_auto_rollback` / `auto_rollback_dry_run` / `auto_rollback_smoke_timeout_seconds`) + **Helm chart release pipeline** (`helm-release.yml` NEW; cosign-keyless signed; OCI push to `ghcr.io/long2know/charts`) + **SignalR churn observability** (`signalr:churn_rate_5m` recording rule + `SignalrChurnHigh` + `SignalrChurnCritical` alerts with `team: apone` label) + **CHANGELOG `[0.30.0]`** + **`mobile/package.json` 0.29.0 → 0.30.0** |
| `47d0fe5` | **Hicks (Frontend)** `<hicks@squad.mahjong>`        | 38    | 2,184  | 195    | **LH13 §6.9 HOLD YELLOW** (3rd consecutive wave on the same `gh`-auth blocker; sample-window arithmetic no longer applies at W21; recommends §6.x Coordinator-direct probe path for W22 if unresolved) + **Phase L tile-claim-animation + meld-display + `mountMeld`** (`renderer-webgl2` 35,258 → 40,292 B; +5,034 B; 8,860 B under 49 KB ceiling; pung/kong/chi staggered fan-in with `easeOutBack`; per-seat meld row layout via `appendMeld` / `layoutMeldRow` / `nextMeldOriginXZ`) + **Bundle §3.6 ≤115 KB ceiling HIT at 112,219 B** (`autotable-src-eager` 123,701 → 112,219 B = −11,482 B; 5,541 B under ≤115 KB target; profile-drawer extracted to NEW 3,871 B lazy chunk; zh-Hans + zh-Hant lazified as NEW 4,437 B + 4,434 B JSON chunks) + **three-renderer-big 11th hold-line wave** at 406,635 B + **Admin UI 5 W21 surfaces** (swiss-apply-round + rotation-schedule + tournament-withdraw + signalr-purge + replay-restoration-audit READ-ONLY; `admin-panel` 35,161 → 48,984 B; only 168 B headroom under ≤48 KB ceiling — chunk-split flagged for W22) |
| `f0028a1` | **Bishop (Backend)** `<bishop@squad.mahjong>`       | 31    | 9,144  | 24     | csproj `<Version>0.30.0</Version>` cadence bump (5 contract tests; closes W20 §9.18 convention with Apone's CHANGELOG `[0.30.0]`) + **Swiss apply-round service + admin endpoint** (`SwissApplyRoundService` projects W20 audit rows into `TournamentMatch` rows; `POST /api/admin/tournaments/{id}/swiss-apply-round`; idempotent re-call returns existing matches with `Created=false`; 22 tests) + **Scheduled per-tenant JWKS rotation** (`RotationScheduleEntity` + `RotationScheduleAdminController` + `SimpleCronMatcher` 5/6-field parser + `RotationScheduledExecutorService : BackgroundService` 60s tick; `jwt_scheduled_rotation_total{tenant,status}` counter with `success`/`error`/`skipped`; 31 tests) + **Replay restoration audit log** (`ReplayRestorationAttempt` entity + `GET /api/admin/replays/{id}/restoration-audit` last-10 rows; outcomes `read`/`restored`/`not-found`/`integrity-failure`/`unauthorised`; 10 tests) + **JWT validator anomaly metrics** (`jwt_validator_anomaly_total{tenant,reason}` with `clock-skew` / `invalid-issuer` / `expired-too-soon`; new 5-arg `JwtValidationService` constructor; 15 tests) + **Tournament withdraw-player flow** (`POST /api/admin/tournaments/{id}/withdraw-player` sets `Seed = -1` sentinel + drops in-flight matches + preserves completed history; 14 tests) + **SignalR retention manual-purge controller** (`POST /api/admin/signalr/retention-purge?tenant=&before=` bulk-delete with optional tenant scope; `signalr_manual_purge_total{tenant}` counter; 18 tests). **Gate post-Bishop: 4754/1/0** (1 pre-existing failure: Vasquez's W20 mobile-pin substring `0.29.0` broken by Apone's W21 bump; flagged for Vasquez); **3-provider migration `Phase_K_W21_RotationScheduleAndReplayRestoration`** (Postgres/Sqlite/SqlServer); Grafana `jwt-validator-metrics.json` panels 9 + 10 added |
| `6d8aa93` | **Vasquez (QA)** `<vasquez@squad.mahjong>`          | 42    | 2,364  | 46     | Gate **4846/0/0 (+91 from forward-stage contracts; net +209 vs W20 close 4637; W20 mobile-pin self-repair lifts 4754/1 → 4755/0 → 4846/0 after the 25 W21 contract files compile in)**; **`docs/agent-handoff-protocol.md §6.10` NEW** — LH13 W21 disposition HOLD YELLOW ratified (narrows W20's two compounding reasons to a single `gh`-auth observation gap at W21); **`docs/agent-handoff-protocol.md §9` NEW** — top-level **stash-isolation directive** + W21 retrospective audit table (codifies W20 Apone-mid-task-reset lesson into 5 sub-rules; §9.3 shows all four W21 commits CLEAN on all six rules; **recurring-violation ratchet stays at level 2** with no W21 occurrence); **§4.8 13-wave deferral arc** (W7→W21) UNCHANGED — crosses symbolic year-of-bring-ups threshold without §4.9 escalation; **KW20 → KW21 regression rename** (`Wave1ThroughKW20RegressionTests.cs` → `Wave1ThroughKW21RegressionTests.cs` via `git mv`; 19 typeof refs sed-rewritten; PhaseK20 pin to `_Historical`; new PhaseK21 pin); **25 W21 forward-stage contract files** (Bishop 9 + Hicks 7 + Apone 5 + self-lane 4); **13 prior-wave pin broadenings** (W11–W20 self-lane + surface-smoke tests gain `KW20 || KW21` OR-shape); **W20 mobile-pin forward-broadening repair** (`AponeW20ChangelogW20ContractTests.MobilePackageJson_HasVersion_0_29_0_OrForwardStaged` now accepts `0.29.0 || 0.30.0`); lane-discipline strict `checked=3 violations=0` pre-Vasquez |

**Totals across all 4 W21 commits: 124 files; +15,700 / −286.** All 4 commits carry the `Co-authored-by: Copilot <…>` trailer. **Per-invocation identity hardening 100 % clean across all 4 commits** (no `git config user.name` reverts found in any reflog). **Atomic flock pipeline honoured by all 4 bring-up agents** — second consecutive wave with 4-for-4 atomic-flock compliance.

---

## 2. Deliverables per lane

### 2.1 Apone (DevOps) `55fc04e` — 6 deliverables

1. **Argo Rollouts Canary template for the frontend.** `infra/k8s/base/argo-rollouts/frontend-canary.yaml` NEW: 4 explicit weight steps (5 → 25 → 50 → 100); 10-minute pause between each; AnalysisRun gate between each pause and the next setWeight; total nominal duration ~30 minutes from first canary pod Ready to 100 % promotion. The `frontend-canary-error-rate` AnalysisTemplate: 15 s interval × 40 iterations = 10 minutes; pass when error rate < 0.005 (0.5 %) for 38 of 40 samples; query `nginx_ingress_controller_requests` error rate over 1m rolling; `failureLimit: 2` absorbs single noisy intervals; `inconclusiveLimit: 4` tolerates Prometheus NaN returns. Why 0.5 %: the W17 LH13 baseline was 0.12 % over 24 h; 0.5 % gives a ~4× safety margin while still catching the worst-case regressions. `docs/argo-rollouts-frontend-canary.md` NEW. Companion to the W20 backend BlueGreen — every workload class now has at least one strategy template wired by W21.

2. **Kyverno W21 audit-mode pair (3rd + 4th rules).** Two new ClusterPolicies in Audit mode (5-day grace window → W22 enforce flip): `infra/k8s/base/kyverno-policies/require-resource-limits.yaml` (2 sub-rules; `?*` wildcard requires non-empty value on `resources.limits.{cpu, memory}`; pre-W21 prod-pod count of violations: 0 — W20 `deployment.yaml` + coturn + migrate Job all declare limits) + `infra/k8s/base/kyverno-policies/disallow-host-paths.yaml` (`X(...)` negation on `volumes[*].hostPath`; pre-W21 prod-pod count of violations: 0 — no hostPath volumes in prod). `docs/kyverno-w21-additional-rules.md` NEW with pre-W21 verification commands captured into `.work/apone-w21-safe/` for the W22 enforce-flip evidence trail. Mirrors the W19 → W20 audit → enforce ladder for `disallow-lateral-movement` + `require-network-policy`.

3. **us-east-1 actual-apply auto-rollback safety net.** `infra/terraform/regional-eks/us-east-1/auto-rollback.tf` NEW with 3 operator-controlled dials: `var.enable_auto_rollback` (default false; opt-in only); `var.auto_rollback_dry_run` (default false; when true the rollback branch only LOGS); `var.auto_rollback_smoke_timeout_seconds` (default 300; timeout for the W20 V2 smoke-test invocation). Operator opt-in path in `docs/us-east-1-apply-runbook.md §7` W21 hand-off (staging dry-run → manual failure injection → flip to `dry_run = false` → apply with `enable_auto_rollback = true`). Opt-out is the default. Closes the W20 V2 runbook ↔ smoke-test loop with an automated rollback trigger.

4. **Helm chart release pipeline.** `.github/workflows/helm-release.yml` NEW triggers on `helm-v[0-9]+.[0-9]+.[0-9]+` tag push (e.g. `helm-v0.6.0`). Parallel to `release.yml` (which triggers on `v*` for app image releases). Step chain: tag-pattern validation → `helm lint helm/mahjong` → `helm template` against default + staging + prod values → `helm package --version <tag-derived>` → `helm push <pkg>.tgz oci://ghcr.io/long2know/charts` → keyless cosign sign via GitHub OIDC → in-job `cosign verify` against the OIDC certificate-identity-regexp `helm-release.yml@refs/tags/helm-v.*`. `docs/helm-release.md` NEW with consumer-side verify path documented at §5. **First time the Helm chart has a signed release path.**

5. **SignalR observability — churn-rate recording rule + alert pair.** `infra/k8s/overlays/prod/prometheus-rules-signalr.yaml` NEW: the recording rule `signalr:churn_rate_5m = clamp_min(-delta(signalr_connections_active[5m]) / 5, 0)` derives from the existing W11 `signalr_connections_active` gauge; two alerts both labelled `team: apone` (SignalrChurnHigh — `> 10 disconnects/min for 5m`, severity warning, Slack `#alerts-apone`; SignalrChurnCritical — `> 30 disconnects/min for 3m`, severity critical, PagerDuty DevOps on-call). Translation of the W21 spec's "P95" → the 5-minute rolling-average rate (idiomatic for a gauge-derived window; a true `histogram_quantile(0.95, ...)` would require a histogram source). `docs/signalr-observability-w21.md` NEW. Closes the W11/W16 SignalR observability gap — alerts previously existed only as W16 30-day error-budget burns; W21 surfaces near-real-time churn signals.

6. **CHANGELOG `[0.30.0]` + version triple.** `CHANGELOG.md [0.30.0]` entry; `mobile/package.json` 0.29.0 → 0.30.0; backend csproj deferred to Bishop W21 per the W18 §9.18 CHANGELOG=apone-lane / `<Version>`=bishop-lane convention (Bishop W21 D1 lands the matching `<Version>0.30.0</Version>` bump).

**Validation:** `actionlint .github/workflows/*.yml` exit 0 (helm-release.yml passes); `kustomize build infra/k8s/overlays/prod/` exit 0; `kustomize build infra/k8s/overlays/staging/` exit 0; `terraform fmt` clean on auto-rollback.tf (locally validated); `tests/ci/check-cross-lane-bundling.sh --strict` post-push report: all staged paths apone-lane or shared-lane.

### 2.2 Hicks (Frontend) `47d0fe5` — 5 deliverables

1. **LH13 §6.9 evidence-gate re-evaluation → HOLD YELLOW (3rd wave).** Identical disposition to W19 and W20. `gh auth status` in the bring-up shell still reports "not logged into any GitHub hosts"; the canonical §4.2 `gh run list --workflow=pwa-audit.yml --event=schedule ...` returns no rows under that posture. **Timing distinction from W20:** at W20 the wall-clock gap between the W18 merge and the W20 bring-up window (~97 min) was still below the §4.2 ≥3 hourly-cron-tick threshold; at W21 the gap has widened well past that threshold (~25 h elapsed) — the sample-window-size sub-condition is now plausibly satisfied. But because the observation channel remains closed, the disposition is unchanged. **Recommendation:** if the `gh`-auth gap is unresolved at W22 again, escalate to the §6.x Coordinator-direct probe path (per the W19 hand-off) rather than continuing to inherit YELLOW indefinitely. `docs/lh13-soft-pin-rationale.md §12 NEW` records the W21 disposition.

2. **Phase L renderer — tile-claim animation + meld-display.** `src/renderer-webgl2/tile-claim-animation.ts` NEW (pung/kong/chi claim animations with staggered fan-in; exports `animateTileClaim()` 3-tile pung + 4-tile kong + 3-tile chi variants, `easeOutBack()` one back-easing curve shared across all claim types, `meldSlotMatrix()` positional matrix solver). `src/renderer-webgl2/meld-display.ts` NEW (per-seat meld row layout for the open-meld fan; exports `appendMeld()` push a new meld onto a seat's row, `layoutMeldRow()` recompute X offsets after add/remove, `nextMeldOriginXZ()` returns the world-space anchor for the next meld, helper types `MeldRowState` / `MeldKind` / `MeldFanLayout`). `hello.ts` EXT `mountMeld()` install hook + new `'meld'` mode in dispatch (`?renderer=webgl2-meld`). `src/index.ts` URL regex extended. **renderer-webgl2 chunk: 35,258 → 40,292 B (+5,034 B; under the 49,152 B / 48 KB ceiling by 8,860 B; well within trajectory budget).** Shared per-frame update path with W20 tile-pick lift/drop tween — both register a `tick` callback on the renderer's shared frame emitter, so the animation graph composes additively.

3. **Bundle audit §3.6 — `autotable-src-eager` ≤115 KB ceiling HIT at 112,219 B with 5,541 B headroom.** Two surgeries jointly landed the §3.6 target. **Surgery A:** `profile-drawer` extraction — `installProfileDrawer` + `installProfileToggle` extracted from `./profile` into a new `./profile-drawer.ts` module (~3.9 KB minified); lazy-loaded from `./lobby` via `scheduleProfileDrawerLazyMount()` on first chip hover/focus/click (parallel to the W17 §3.2 lazy pattern). `./profile` retains the data-layer API (`getProfile`, `onProfile`, `setDisplayName`, `setAvatarColor`, `resetProfile`, validators, mutators) + a NEW `flushPendingDisplayName()` helper. **Surgery B:** i18n zh-* catalog lazification — `zh-Hans` (4,882 B raw) and `zh-Hant` (4,879 B raw) split into their own dynamic-import chunks via `import('./i18n/zh-Hans.json')` / `import('./i18n/zh-Hant.json')`; `en` stays bundled as the fallback. The synchronous `t()` API is preserved; `installI18n()` and `setLanguage()` both call `ensureCatalog(activeLocale)` which loads the chunk and re-emits the locale-change event. zh-* users see ~10–30 ms of English strings at lobby cold start; HTTP/2 modulepreload hints emitted by Vite typically land the chunk within the same RTT as the eager bundle. **Outcome: `autotable-src-eager` 123,701 → 112,219 B (−11,482 B; 5,541 B under the §3.6 ≤115 KB ceiling).** **Cumulative `autotable-src-eager` W15→W21: 222,847 → 112,219 = −110,628 B = −49.6 % over 6 waves.**

4. **`three-renderer-big` 11th-wave hold.** `three-renderer-big = 406,635 B` exact at W21 close — unchanged from W14 baseline; the W14 hold-line has held every wave since W6. No upstream three.js bumps, no renderer-graph mutations, no new addons imported. The W21 Phase L renderer expansion (tile-claim-animation + meld-display) lives entirely in the `renderer-webgl2` chunk; the `three-renderer-big` chunk is fully isolated from Phase L work by `vite.config.ts` manualChunks routing (`src/renderer-webgl2/*` → `renderer-webgl2`). **11th-consecutive-wave milestone.**

5. **Admin UI — 5 W21 Bishop operator surfaces.** All five follow the W17 `AdminSurfaceSpec<TRow, TBody>` pattern + land in the `admin-panel` chunk: NEW `src/admin/swiss-apply-round.ts` (POST trigger for Bishop's apply-round endpoint; two-field form tournament-id + round-number; dry-run toggle; confirm-modal); NEW `src/admin/rotation-schedule.ts` (POST per-tenant rotation-schedule reconcile; form tenant-id + key-rotation-cron + next-run-at override; dry-run + confirm-modal); NEW `src/admin/tournament-withdraw.ts` (POST tournament withdraw-player; form tournament-id + player-id + withdrawal-reason; confirm-modal with forfeit warning copy); NEW `src/admin/signalr-purge.ts` (POST SignalR retention purge; form purge-cutoff-iso datetime-local picker + dry-run toggle; confirm-modal with row-count preview); NEW `src/admin/replay-restoration-audit.ts` (READ-ONLY GET surface; last 200 replay restoration audit log entries; no mutation surface). EXT `admin-panel.ts` (+5 new SPECs). **`admin-panel` chunk 35,161 → 48,984 B (+13,823 B; ONLY 168 B headroom under the 49,152 B / 48 KB soft ceiling).** Hand-off note flags W22 admin work to plan a chunk-split (admin-panel-tournaments + admin-panel-infra) before the next surface lands.

### 2.3 Bishop (Backend) `f0028a1` — 7 deliverables

1. **csproj `<Version>0.30.0</Version>` cadence bump.** Closes the W18 §9.18 CHANGELOG=apone-lane / `<Version>`=bishop-lane cross-lane convention cleanly with Apone W21 `CHANGELOG.md [0.30.0]` in the same PR. `BackendCsprojVersionTests` 5 contract tests (strict-`> 0.29.0` floor + exact-match `0.30.0`).

2. **Swiss apply-round service + admin endpoint.** Closes the loop on the W20 preview path. `SwissApplyRoundService` NEW projects W20 `SwissPairingAuditEntry` rows (one per board) into `TournamentMatch` rows so the live tournament UI surfaces the pairings. Idempotent — re-calling with the same `(tournamentId, round)` returns the existing matches (`Created = false`) and writes no new audit row. Wire-stable error codes: `tournament-not-found`, `not-swiss-format`, `round-not-paired`, `round-out-of-range`. Surface: `POST /api/admin/tournaments/{id}/swiss-apply-round` with `X-Admin-Reason` header mandatory and body `{ Round: int }`. Audit kind: `tournament.swiss-pairing.applied` (`ReconnectAuditEntry.KindTournamentSwissRoundApplied`). 22 tests (13 service + 9 controller).

3. **Per-tenant scheduled JWKS rotation cadence.** Adds an admin-gated cron-style scheduling surface on top of the W15 per-tenant rotation policy. New entity `RotationScheduleEntity (TenantId, CronExpression, Enabled, Notes, ...)` with `TenantId` natural key + `(Enabled, UpdatedAtUtc)` operator-dashboard index. `RotationScheduleAdminController` — `POST /api/admin/per-tenant-jwks-rotation-policies/{tenantId}/schedule`, create-or-replace semantics, X-Admin-Reason mandatory, audit kind `auth.jwks.rotation.scheduled`. `RotationScheduledExecutorService : BackgroundService` — 60 s tick, evaluates every enabled schedule via the `SimpleCronMatcher` parser (5/6-field cron, supports `*`, ranges `A-B`, steps `*/N` + `A-B/N`, comma-lists, wildcards); per-tick metric stamp `jwt_scheduled_rotation_total{tenant, status}` with statuses `success` / `error` / `skipped`; per-tick idempotency (a schedule that already ran this UTC minute is skipped); successful executes advance the matching `PerTenantJwksRotationPolicy.RotationStartUtc` 30 days forward and stamp `auth.jwks.rotation.scheduled.executed`. 31 tests (8 matcher + 11 controller + 7 executor + 5 metrics).

4. **Replay restoration attempt audit log.** Operators previously had only the W19 integrity-audit checksum projection. W21 adds a per-replay restoration-attempt trail. New entity `ReplayRestorationAttempt (ReplayId, OperatorId, Outcome, DetailMessage, AttemptedAtUtc)` with `(ReplayId, AttemptedAtUtc)` + `AttemptedAtUtc` indexed. Surface: `GET /api/admin/replays/{replayId}/restoration-audit` — returns the last 10 rows (most-recent-first) and stamps a self-record `read` attempt + `replays.restoration.attempt` audit row on every call. Outcome wire-names: `read`, `restored`, `not-found`, `integrity-failure`, `unauthorised`. 10 controller tests.

5. **JWT validator anomaly counter.** New Prometheus counter `jwt_validator_anomaly_total{tenant, reason}` exposed via `MetricsEndpoint`. Reasons: `clock-skew` (`iat > now + 60s`), `invalid-issuer` (`iss` claim mismatches `expectedIssuer`; opt-in — null issuer skips the check), `expired-too-soon` (`exp < now AND now - exp ≤ 300s`). The validator takes the collector as a nullable side-channel so legacy tests that wire only the issuer still work (no-op recording). New 5-arg `JwtValidationService` constructor; existing 1/2/3-arg overloads remain so prior call sites are unmodified. Grafana dashboard `jwt-validator-metrics.json` gains panels 9 (anomalies-by-reason) + 10 (scheduled-rotations-by-status), each filterable by `$tenant`. 15 tests.

6. **Tournament withdraw-player flow.** Admin-gated mid-event withdraw surface. Sets the matching `TournamentRegistration.Seed = -1` sentinel so downstream Swiss + round-robin pairing services exclude the player from future rounds (the W19 forfeit sentinel pattern). In-progress / pending matches involving the player are dropped so the W21 apply-round + W20 swiss-pair-next-round surfaces can re-pair them. Completed matches are untouched — historical record preserved. Surface: `POST /api/admin/tournaments/{id}/withdraw-player` with `X-Admin-Reason` header mandatory and body `{ PlayerId, Reason? }`. Wire-stable error codes: `tournament-not-found`, `player-not-registered`, `already-withdrawn`. Audit kind: `tournament.player.withdrawn`. 14 tests.

7. **SignalR retention manual-purge controller.** Targeted operator surface for post-incident cleanup, distinct from the W17 automatic retention sweep. New `SignalRManualPurgeMetrics` Prometheus counter `signalr_manual_purge_total{tenant}` rendered by `MetricsEndpoint`. Per-tenant scoping when `tenant` query param is supplied; cross-tenant when omitted. Surface: `POST /api/admin/signalr/retention-purge?tenant=&before=ISO8601` with `X-Admin-Reason` header mandatory. The cutoff `before` must be ISO 8601 and strictly in the past. Bulk-deletes from `SignalRSequenceEntries` where `CreatedAt < before` (and optionally `TenantId = tenant`). Audit kind: `signalr.retention.manual-purge`. 18 tests (5 metrics + 13 controller).

**Persistence:** 3-provider migration `Phase_K_W21_RotationScheduleAndReplayRestoration` (Postgres / Sqlite / SqlServer — each `.cs` + `.Designer.cs`; model snapshots refreshed). New `DbSet<RotationScheduleEntity>` + `DbSet<ReplayRestorationAttempt>` on `AppDbContext`. **Gate post-Bishop:** **4754 passed / 1 failed / 0 skipped** — the single remaining failure is the pre-existing W20 contract test `AponeW20ChangelogW20ContractTests.MobilePackageJson_HasVersion_0_29_0_OrForwardStaged` broken by Apone's W21 `mobile/package.json` bump from 0.29.0 → 0.30.0 (the Vasquez paired contract pinned the substring `0.29.0`). Out-of-lane for Bishop; flagged for Vasquez W21 cleanup.

### 2.4 Vasquez (QA) `6d8aa93` — 6 brief deliverables + 25 forward-stage contracts + 13 prior-wave broadenings

1. **Gate `4846 / 0 / 0`** at Vasquez bring-up close (+91 from forward-stage soft-pins; net +209 vs W20 close 4637; Bishop pre-Vasquez baseline 4754/1/0 → 4755/0/0 after the W20 mobile-pin self-repair → 4846/0/0 after the 25 W21 contract files compile in).

2. **`docs/agent-handoff-protocol.md §4.8` UNCHANGED — 13-wave deferral arc (W7→W21) continues.** Crosses the symbolic "year of bring-ups" threshold without §4.9 escalation. All three Option payloads (A — minimal; B — standard; C — strict) remain exactly as authored at W17. Flip script `tests/ci/lane-discipline-flip-required.sh` remains executable; jq-unavailable posture continues from W18 → W20 → W21. No §4.9 row added at W21. Re-prompt cadence stays at once-per-wave. Hand-off note for W22: if Stephen has still not selected by W22 close, consider a Coordinator-direct escalation memo — the 14-wave deferral arc at W22 will be a fair trigger.

3. **`docs/agent-handoff-protocol.md §6.10 NEW` — LH13 W21 disposition HOLD YELLOW ratified.** Hicks W21 narrowed W20's two compounded HOLD reasons to a SINGLE remaining reason at W21: `gh`-CLI unauthenticated in the bring-up shell. The W20 secondary reason (sample-window-size arithmetic) no longer applies at W21 (~25 h elapsed since the W18 merge to `main` — well past the 3-hour minimum for 3 hourly cron ticks). §4.2 still requires ≥3 *observed* successful schedule-event runs; observation channel remains closed. Cross-refs: `docs/lh13-soft-pin-rationale.md §12` (Hicks W21 author record), `Phase_K_W21/Vasquez/HicksW21Lh13W21CronStatusTests.cs` (contract test pinning the HOLD posture). Hand-off to Hicks W22: per Hicks W21's §12 recommendation, if the `gh`-auth gap is unresolved at W22 *again*, the §6.x coordinator-driven probe path (§4.7) is the recommended escalation.

4. **`docs/agent-handoff-protocol.md §9 NEW` — top-level stash-isolation directive + W21 retrospective audit.** Standing directive distilled from the W20 audit cycle: "never touch other agents' working tree state mid-wave". 5 sub-rules: (1) **Stash-only-your-own** — agents only stash/pop/drop their own wave entries, identified by the `<agent>-w<N>-…` subject prefix; (2) **Never sweeping working-tree mutations** mid-wave if any other agent's stash entry is present in `git stash list`; (3) **Never `git stash pop` an entry you did not author** — stale stashes get pruned in the next Vasquez retrospective; (4) **Shield other agents' untracked surface** at bring-up via `git stash --include-untracked -m "<agent>-w<N>-<other-agent>-shield-…"`; (5) **Pre-pipeline diff verification** is mandatory, with a W21 corollary: the cached diff must NOT include foreign-lane file deletes either. §9.3 W21 audit table: all four W21 commits CLEAN on all six rules. Vasquez W21 explicitly created a Hicks-frontend shield stash at rebase time (`vasquez-w21-hicks-frontend-shield-1779635691`) to absorb Hicks's leftover frontend bundle hash-rename byproducts — exactly the §9.1 rule 4 pattern. **Recurring-violation ratchet stays at level 2** (W18 `5957a37` + W19 `d700cf7`); no new occurrence at W21; no §4.9 Stephen-decision opened.

5. **25 forward-stage W21 contract files + KW20 → KW21 regression rename + 13 prior-wave broadenings.** Forward-stage at `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W21/Vasquez/`: **Bishop W21** (9 files — backend csproj 0.30.0; SwissApplyRoundService contract; RotationScheduleEntity contract; RotationScheduleAdminController contract; JWKS migration contract; ReplayRestorationAttempt contract; JwtValidatorAnomalyMetrics contract; JwtValidator dashboard W21 contract; TournamentWithdrawPlayerController contract; SignalRRetentionManualPurge contract); **Hicks W21** (7 files — LH13 W21 cron status; Phase L tile-claim-animation contract; Phase L meld-display contract; bundle audit §3.6 contract; profile-drawer lazy contract; i18n lazy catalogs contract; admin UI W21 surfaces contract); **Apone W21** (5 files — Argo Rollouts frontend Canary contract; Kyverno audit rules W21 contract; auto-rollback.tf contract; CHANGELOG W21 contract; rules docs contract); **Vasquez self-lane** (4 files — branch-protection W21 Stephen-decision status; W21 retrospective audit observation; W21 surface smoke facts; W21 self-lane master). KW20 → KW21 regression rename: `Wave1ThroughKW20RegressionTests.cs` → `Wave1ThroughKW21RegressionTests.cs` via `git mv`; 19 typeof refs sed-rewritten; PhaseK20 rename pin rewritten to `_Historical` (asserts BOTH W19 + W20 class names absent); NEW PhaseK21 rename pin. 13 prior-wave self-lane + surface-smoke tests (W11–W20) broadened from `Equals("Wave1ThroughKW20RegressionTests")` to `Equals("Wave1ThroughKW20RegressionTests") || Equals("Wave1ThroughKW21RegressionTests")`.

6. **W20 mobile-pin forward-broadening repair (W21 NEW precedent).** Bishop W21 close reported `4754 passed / 1 failed`, the failure being `AponeW20ChangelogW20ContractTests.MobilePackageJson_HasVersion_0_29_0_OrForwardStaged` broken by Apone's W21 bump of `mobile/package.json` from 0.29.0 → 0.30.0. Bishop documented this as out-of-lane. Vasquez W21 repairs the soft-pin in-lane by forward-broadening the substring check from `0.29.0` to ANY of `{ "0.29.0", "0.30.0" }`. Post-repair full-suite run: `4755 / 4755 / 0`. **W21 NEW convention — forward-broadening precedent for version-pin contract tests:** W22+ version-pin contract tests should follow the same OR-pattern from the outset rather than hard-pinning a single version literal. Also broadens 13 prior-wave regression-class hard-pins (W11-W20) under the same precedent.

**Lane-discipline strict pre-Vasquez:** `checked=3 violations=0` (Apone + Hicks + Bishop W21 commits). **Lane-discipline post-Scribe:** `checked=4 violations=0` — **11th consecutive 0-violation wave at tip**.

---

## 3. W21 gate/bundle metrics

### Cumulative bring-up commit shape (rounded)

| Lane | Files | + Lines | − Lines | Net | Inbox memo |
|---|---:|---:|---:|---:|---|
| Apone (DevOps) | 13 | 2,008 | 21 | +1,987 | `.squad/decisions/inbox/apone-phase-k-wave-21.md` (force-added) |
| Hicks (Frontend) | 38 | 2,184 | 195 | +1,989 | `.squad/decisions/inbox/hicks-phase-k-wave-21.md` (force-added) |
| Bishop (Backend) | 31 | 9,144 | 24 | +9,120 | `.squad/decisions/inbox/bishop-phase-k-wave-21.md` (force-added) |
| Vasquez (QA) | 42 | 2,364 | 46 | +2,318 | `.squad/decisions/inbox/vasquez-phase-k-wave-21.md` (force-added) |
| **W21 total** | **124** | **15,700** | **286** | **+15,414** | **4 / 4 force-added on first try** |

W21 is **the highest single-wave +Lines delta in Phase K** (Bishop's 7-deliverable backend bring-up at +9,144 is the heaviest individual lane delta in the wave; the prior high was W17 Bishop at +6,250). The +15,414 net delta represents roughly **1.0× the W6 baseline test count** in raw line growth — the +209 gate delta makes the test/line ratio at W21 ~74 lines per new test, consistent with the W18–W20 average of ~70 lines per new test for the consolidation-regime baseline.

### Test gate progression

| Stage | Gate (pass/fail/skip) | Δ vs W20 close | Notes |
|---|---|---|---|
| W20 close | 4637 / 0 / 0 | — | Reference; W20 ship at `bbd3f6c`. |
| Apone W21 close | 4637 / 0 / 0 | 0 | Apone-lane is infra/docs/workflows; no new test surface. |
| Hicks W21 close | 4637 / 0 / 0 | 0 | Hicks-lane is frontend; no backend test surface. |
| Bishop W21 close | 4754 / 1 / 0 | +118 / +1 fail | 118 new Bishop tests; 1 pre-existing failure from Apone's mobile/package.json bump (out-of-lane for Bishop). |
| Vasquez W21 close (post-repair) | **4846 / 0 / 0** | **+209 / 0 fail** | W20 mobile-pin repair lifts to 4755/0; 25 W21 forward-stage contracts add ~91 soft-pin tests. |

**Gate ratio:** W21 4846 / W6 1422 = **3.41× the W6 baseline** over 16 waves (+240.8 % cumulative; +209 single-wave growth = ~4.5 percentage-point lift; consistent with the late-mature consolidation cadence of +200–300 per wave established at W18–W20).

### Bundle ledger (W6 → W21)

| Wave | autotable-src-eager | three-renderer-big | renderer-webgl2 | admin-panel | Notes |
|---|---:|---:|---:|---:|---|
| W6 | (baseline) | 738,431 B | — | — | three-renderer-big peak |
| W14 | (intermediate) | 406,635 B | — | — | three-renderer-big hold-line baseline |
| W15 | 222,847 B | 406,635 B | (synthesised) | — | §3.x ladder begins |
| W16 | (audit) | 406,635 B | 19,017 B | — | tile-mesh + canonical atlas |
| W17 | 176,907 B | 406,635 B | 24,743 B | (intermediate) | scene-graph + picking; eager −37,295 B |
| W18 | 156,191 B | 406,635 B | 27,891 B | (intermediate) | §3.3 1.7× target |
| W19 | 144,192 B | 406,635 B | 30,174 B | 26,701 B | §3.4 hit; admin-panel ledger begins |
| W20 | 123,701 B | 406,635 B | 35,258 B | 35,161 B | §3.5 ceiling MET with 11,299 B headroom |
| **W21** | **112,219 B** | **406,635 B** | **40,292 B** | **48,984 B** | **§3.6 ≤115 KB HIT with 5,541 B headroom**; renderer-webgl2 +5,034 B; admin-panel +13,823 B with 168 B headroom |

**Cumulative `autotable-src-eager` W15 → W21:** 222,847 → 112,219 B = **−110,628 B = −49.6 % over 6 waves**. **Three-renderer-big hold-line:** 406,635 B exact at W21 — **11th consecutive wave** (W11→W21).

### New W21 lazy chunks

| Chunk | Size | Source | Mounted via |
|---|---:|---|---|
| `profile-drawer` | 3,871 B | `./profile-drawer` (extracted from `./profile`) | `scheduleProfileDrawerLazyMount()` on chip hover/focus/click |
| `zh-Hans` (i18n JSON) | 4,437 B | `./i18n/zh-Hans.json` (extracted from eager bundle) | `ensureCatalog('zh-Hans')` on locale activation |
| `zh-Hant` (i18n JSON) | 4,434 B | `./i18n/zh-Hant.json` (extracted from eager bundle) | `ensureCatalog('zh-Hant')` on locale activation |

**Chunk count W20 → W21:** 32 → 35 (+3 new lazy chunks).

---

## 4. Lane-discipline (11th consecutive 0-violation wave — milestone)

```
$ bash tests/ci/check-cross-lane-bundling.sh --pr stlong/phase-k-wave-21-bringup --strict
[lane-discipline] checking 4 commit(s) in mode=pr

✓ 6d8aa93347 — lane=vasquez author=vasquez
✓ f0028a1535 — lane=bishop  author=bishop
✓ 47d0fe5e72 — lane=hicks   author=hicks
✓ 55fc04e914 — lane=apone   author=apone

[lane-discipline] checked=4 violations=0
[lane-discipline] OK
```

**Streak detail:** W11 + W12 + W13 + W14 + W15 + W16 + W17 + W18 + W19 + W20 + **W21** = **11 consecutive 0-violation waves**. **11th-consecutive-wave milestone.** 8 of 11 waves in the streak are unamended (W11 + W14 + W16 + W17 + W18 + W19 + W20 + W21 unamended; W12 + W13 + W15 amended) = **73 % unamended at W21** — late-mature steady state hardens further (W18: 50 %; W19: 63 %; W20: 70 %; W21: 73 %).

**W21 lane-discipline narrative — second wave with ZERO in-flight violations.** Same posture as W20 (no in-flight violation in any of the 4 bring-up commits; W19 §7.5 lessons + W20 §7 retro + W21 §9 stash-isolation directive all propagated cleanly into the per-agent prompt template). The 1 process anomaly (Hicks's leftover frontend bundle hash-rename byproducts at Vasquez rebase time) was shielded in-flight via the §9.1 rule 4 pattern (Vasquez stash named `vasquez-w21-hicks-frontend-shield-1779635691`) — exactly the convention codified by §9.1 the same wave.

---

## 5. LH13 §6.10 status — HOLD YELLOW (W21 disposition; §6.9 promoted to §6.10 per Vasquez monotonic §-numbering convention)

**Status:** HOLD YELLOW (no PROMOTE to GREEN).

**Sample window:** ~25 hours elapsed since the W18 merge to `main` (`7832f49`) — well past the §4.2 3-hour minimum for 3 hourly cron ticks. The sample-window-size sub-condition is now plausibly satisfied.

**Single remaining blocker:** `gh` CLI unauthenticated in the bring-up shell. `gh auth status` returns "not logged into any GitHub hosts"; the canonical §4.2 query `gh run list --workflow=pwa-audit.yml --event=schedule ...` returns no rows under that posture. Per §4.2's binary read of the convergence criterion, an unobserved sample is treated as a 0-count sample.

**3-wave hold:** W19 §6.8 → W20 §6.9 → W21 §6.10 — same blocker (`gh`-CLI unauth observation gap) carried unchanged across 3 consecutive waves. **W22 hand-off recommendation:** if the `gh`-auth gap is unresolved at W22 *again*, escalate to the §6.x Coordinator-direct probe path (per the W19 hand-off) rather than continuing to inherit YELLOW indefinitely. The wall-clock data is overwhelmingly likely to show ≥3 consecutive `schedule`-event successes by now; the bottleneck is purely the bring-up shell's inability to read the run history.

**Cross-refs:** `docs/agent-handoff-protocol.md §6.10` (full disposition table + ratification narrative); `docs/lh13-soft-pin-rationale.md §12` (Hicks W21 author record); `Phase_K_W21/Vasquez/HicksW21Lh13W21CronStatusTests.cs` (contract test); the W18 pwa-audit workflow gate (`--form-factor=desktop` + `--screenEmulation.mobile=false`) remains present at W21 close — no W19 / W20 / W21 regression.

**§-numbering convention reminder:** Vasquez uses monotonic-incrementing sub-section numbers per wave to preserve the historical record of prior dispositions (W19 §6.8 records W19 HOLD; W20 §6.9 records W20 HOLD; W21 §6.10 records W21 HOLD; all preserved in `docs/agent-handoff-protocol.md`).

---

## 6. Stephen-decision items (carried into late-April 2027 — 4 active + 1 W20-blocked + W21 unchanged)

1. **§4.8 branch-protection install — Option A / B / C selection.** **13-wave hold (W7 → W21)**; no decision recorded; `gh api ...protection` dry-run continues HTTP 404 "Branch not protected"; Coordinator-direct continues to NOT execute the install (reversibility-first asymmetry — branch-protection apply is high-risk + irreversible without owner credential). **W21 crosses the symbolic "year of bring-ups" threshold; W22 enters 14-wave deferral with Coordinator-direct escalation memo candidate.**

2. **us-east-1 ACTUAL APPLY.** Apone W20 D3 V2 runbook + `post-apply-smoke-test.sh` 281-line script landed; Apone W21 D3 wires the **auto-rollback safety net** (`auto-rollback.tf` with 3 opt-in dials). The actual `terraform apply` against the live AWS account requires Stephen's owner credential. **W21 disposition: V2 + auto-rollback package landed; awaiting Stephen.**

3. **CHANGELOG 0.29.0 + 0.30.0 release-tag publication.** W20 published `[0.29.0]`; W21 publishes `[0.30.0]`. Bishop W21 lands csproj `<Version>0.30.0</Version>` (CHANGELOG + csproj agree at v0.30.0 at W21 close). Tag creation + GitHub release require Stephen's review + sign-off. **W21 NEW: Helm chart release path** — `helm-vX.Y.Z` tag pushes will trigger the `helm-release.yml` workflow with cosign-keyless signing + OCI push to GHCR; the first Helm tag is Stephen's call.

4. **iOS signing certificate rotation cadence.** Apone W18 landed iOS signing; W20 landed iOS E2E SIGNED-branch job. Stephen action remains: select rotation cadence + document in `docs/agent-handoff-protocol.md §5.4`.

### Stephen-blocked secondary items (W21 changes)

5. **`pwa-audit.yml` cron trigger** — §6.10 PROMOTED Coordinator-direct cron-seed remains PRIMARY at W21; **W21 disposition HOLD YELLOW pending ≥3 schedule-event success runs observable** (gh-CLI unauthenticated blocker carries; W22 escalation recommendation: §6.x Coordinator-direct probe).
6. **`PWA_PREVIEW_URL` secret** — W21 §6.10 still HOLD YELLOW.
7. **Secrets provisioning:** Sentry DSN (W9 carry; **12 waves**), OpenAI API key (W10 carry; **11 waves blocking `EfCommentaryStore` prod dogfood**), Janus credentials (W11 carry), Redis prod credentials (W11 ESO; W14–W21 pre-wire blocked).
8. **Argo Rollouts install** in prod cluster — Apone W11→W19 prep ready; W20 ships BlueGreen template; **W21 ships frontend Canary template — every workload class now has a strategy template wired**; install Stephen-blocked.
9. **Prod Redis TF apply** — Apone W11→W21 prep ready; W22+ apply unlocks prod cutover.
10. **us-east-1 IRSA OIDC provider** — W14 §2.1 → W21 §5.3 plan-readiness re-checks all GREEN; W18 PARTIAL→FULL-GREEN apply-ready held; **W20 ships V2 runbook + smoke-test script; W21 ships auto-rollback.tf opt-in safety net**; live apply Stephen action item #2.
11. **First real prod JWT rotation** — **W19 April 2027 window scheduled**; W21 does NOT touch this. Apone W14 D4 GA-confirmed.
12. **W20-BLOCKED — Kyverno W19 enforce-flip prod cluster apply** — W20 ships the manifest flip + post-flip operator playbook; `kubectl apply` to prod cluster is Stephen's operator action.
13. **NEW W21 — Kyverno W21 audit-mode pair (`require-resource-limits` + `disallow-host-paths`) 5-day grace window started; W22 enforce-flip pre-wired.** No Stephen action required until W22 ship.
14. **NEW W21 — Helm chart `helm-vX.Y.Z` first tag creation.** Awaiting Stephen.
15. **NEW W21 — us-east-1 auto-rollback opt-in.** Apone W21 D3 ships the safety net with 3 dials; opt-in default false; Stephen selects `enable_auto_rollback = true` at apply time (after a staging dry-run validation).

**16 consecutive weeks of Stephen re-prompt sequence; W21 §4.8 hold extends to 13 waves (W7→W21) — crosses the symbolic "year of bring-ups" threshold; W21 §6.10 LH13 HOLD YELLOW maintained under strict reading (gh-CLI unauthenticated observation gap; W22 §6.x escalation recommendation); Stephen-blocked list contracts (LH13 §6.10 W22 escalation; Kyverno W21 enforce-flip pre-wire NEW; Helm chart first tag NEW; us-east-1 auto-rollback opt-in NEW) and holds (branch-protection still §4.8 NEW decision tree pending Option A/B/C selection; W22 14-wave deferral arc may trigger Coordinator-direct escalation memo).**

---

## 7. W21 process retrospective

1. **4-for-4 atomic-flock compliance — second consecutive wave.** W20 was the first wave where all 4 bring-up agents ran stage + commit + push inside a SINGLE `flock 9>.work/squad-git-lock` block; W21 is the second. **The discipline has now ratcheted permanently** — the W21 prompt templates carry atomic-flock as a hard requirement, and the W21 outcome validates the requirement empirically.

2. **4-for-4 force-add — third consecutive wave with explicit `git add -f` for gitignored memos.** W19 saw Bishop miss the force-add (Coordinator-direct EXECUTION #3 at `e341092` backfilled); W20 saw Bishop force-add without prompting; W21 sees all 4 bring-up agents force-add their inbox memos on the first try. The §6.5/§7.4 lesson #2 propagation into the per-agent prompt template continues to hold across 3 consecutive waves.

3. **Vasquez §9 stash-isolation directive — codified the W20 incident lesson formally.** The W20 Apone mid-task `git reset --hard` wiped Hicks's in-progress untracked tree; W20 recovery via the renamed-stash convention was ad-hoc. **W21 codifies the convention into a 5-sub-rule standing directive at `docs/agent-handoff-protocol.md §9.1`.** Sub-rule 4 (shield other agents' untracked surface at bring-up via `git stash --include-untracked -m "<agent>-w<N>-<other-agent>-shield-…"`) was empirically demonstrated by Vasquez W21 itself — the `vasquez-w21-hicks-frontend-shield-1779635691` stash absorbed Hicks's leftover frontend bundle hash-rename byproducts at rebase time. **Convention recursion:** the directive that codifies the recovery pattern was first applied by the wave that codified it.

4. **Self-repair of 1 pre-existing test failure in-lane by Vasquez forward-broadening.** Bishop W21 close reported 4754/1 — the failure being Apone's W21 mobile/package.json bump breaking Vasquez's W20 substring pin (`MobilePackageJson_HasVersion_0_29_0_OrForwardStaged`). Bishop documented this as out-of-lane and flagged it for Vasquez. Vasquez W21 repaired the soft-pin in-lane by forward-broadening the substring check from `0.29.0` to `0.29.0 || 0.30.0`. **No Coordinator-direct EXECUTION needed.** **W21 NEW convention: forward-broadening precedent for version-pin contract tests** — W22+ version-pin tests should use OR-patterns from the outset rather than hard-pinning a single version literal. This is the FIRST in-wave self-repair of a cross-wave version-pin breakage by the QA lane.

5. **§-numbering monotonic-incrementing convention held for 3rd consecutive wave.** W19 §6.8 → W20 §6.9 → **W21 §6.10** all preserved in `docs/agent-handoff-protocol.md`. The convention's intent (preserve historical record of prior dispositions; do NOT replace "current state" placeholders) is now firmly established.

6. **Zero-EXECUTION wave for the second consecutive wave (W20 + W21).** Coordinator-direct EXECUTION ledger holds at 3 events / 8 actions across W17 + W18 + W19; W20 + W21 contribute 0 events / 0 actions. **The 16-wave zero-INTERVENTION streak (W6→W21) preserved by design.** The framework's intent — make EXECUTION rare + reversible + lane-attributed — is realised across 2 consecutive waves of zero need.

7. **W21 manifest visual-regression rename — NO-OP.** Vasquez W21 brief asked for a `manifest-screenshots-visual-Wave1ThroughKW20.spec.ts` → `…KW21.spec.ts` rename; at W21 close inspection, no `Wave1ThroughKW<N>` suffix family exists. The unsuffixed `manifest-screenshots-visual.spec.ts` continues to carry the cross-wave visual baseline; documented as NO-OP at W21 (per the same observation at W20).

8. **Argo Rollouts trilogy complete.** W19 install runbook + RBAC + namespace prereqs → W20 backend BlueGreen template + decision matrix → **W21 frontend Canary template + AnalysisRun gates**. Every workload class now has a documented progressive-delivery strategy template wired by W21. The cutover (Stephen-blocked Rollouts install) unlocks the full progressive-delivery toolkit immediately.

---

## 8. W20 → W21 trajectory

### Gate growth ladder

| Wave | Gate (pass) | Δ vs prior | Δ vs W6 | Multiplier | Notes |
|---|---:|---:|---:|---:|---|
| W6 | 1422 | (baseline) | — | 1.00× | Phase K W6 fan-out start |
| W17 | 3930 | +250 | +2,508 | 2.76× | Phase K mid-mature consolidation |
| W18 | 4111 | +181 | +2,689 | 2.89× | DbSerial 29/29 COMPLETE |
| W19 | 4376 | +265 | +2,954 | 3.08× | 3× threshold crossed; bundle §3.4 hit |
| W20 | 4637 | +261 | +3,215 | 3.26× | SLSA-3 repo-wide COMPLETE; bundle §3.5 hit |
| **W21** | **4846** | **+209** | **+3,424** | **3.41×** | **Argo Rollouts trilogy complete; bundle §3.6 hit; stash-isolation directive codified** |

W21 +209 single-wave delta is at the lower end of the late-mature consolidation range (W17–W20 averaged +239); **late-mature consolidation regime steady-state ≈ +200–300 per wave** confirmed for the 5th consecutive wave (W17–W21). The 13-wave deferral arc continues without Stephen movement; Phase K consolidation maturity is increasingly determined by the per-wave forward-stage contract surface area rather than net-new feature pressure.

### Bundle ledger compression ladder

| Wave | autotable-src-eager | Single-wave Δ | Cumulative since W15 | Cumulative % |
|---|---:|---:|---:|---:|
| W15 | 222,847 B | — | (baseline) | (baseline) |
| W17 | 176,907 B | −37,295 B | −45,940 B | −20.6 % |
| W18 | 156,191 B | −20,716 B | −66,656 B | −29.9 % |
| W19 | 144,192 B | −11,999 B | −78,655 B | −35.3 % |
| W20 | 123,701 B | −20,491 B | −99,146 B | −44.5 % |
| **W21** | **112,219 B** | **−11,482 B** | **−110,628 B** | **−49.6 %** |

W21 crosses the **−50 % cumulative-compression milestone** (-49.6 %; one wave shy of −50 % depending on the trailing digit rounding). At the current §3.x ladder cadence (single-wave Δ −11 KB to −20 KB), the cumulative compression at W22 close is projected near −51-53 % depending on the §3.7 target landing.

### Audit-kind catalogue growth (W17 → W21)

| Wave | New audit kinds | Total catalogue | Notes |
|---|---:|---:|---|
| W17 | 4 | 17 | W17 admin write surface launch |
| W18 | 3 | 20 | replay integrity audit + DbSerial sweep |
| W19 | 4 | 24 | tournament forfeit + replay restoration triad |
| W20 | 5 | 29 | Swiss pairing + per-tenant BULK ladder + JWT drill |
| **W21** | **6** | **35** | **Swiss apply-round + scheduled rotation (2) + replay restoration attempt + tournament withdraw + SignalR manual purge** |

**W21 NEW audit-kind constants in `ReconnectAuditEntry`:**

| Wire name | Constant | Surface |
|---|---|---|
| `tournament.swiss-pairing.applied` | `KindTournamentSwissRoundApplied` | `POST /api/admin/tournaments/{id}/swiss-apply-round` |
| `auth.jwks.rotation.scheduled` | `KindAuthJwksRotationScheduled` | `POST /api/admin/per-tenant-jwks-rotation-policies/{tenantId}/schedule` |
| `auth.jwks.rotation.scheduled.executed` | `KindAuthJwksRotationScheduledExecuted` | `RotationScheduledExecutorService` tick |
| `tournament.player.withdrawn` | `KindTournamentPlayerWithdrawn` | `POST /api/admin/tournaments/{id}/withdraw-player` |
| `replays.restoration.attempt` | `KindReplayRestorationAttempt` | `GET /api/admin/replays/{id}/restoration-audit` |
| `signalr.retention.manual-purge` | `KindSignalRManualPurge` | `POST /api/admin/signalr/retention-purge?tenant=&before=` |

### Endpoint surface growth (W17 → W21)

| Wave | New admin endpoints | Wired into admin UI | Notes |
|---|---:|---:|---|
| W17 | 4 | 4 | first W17 admin write batch |
| W18 | 3 | 3 | replay integrity controller |
| W19 | 3 | 3 | forfeit + restoration triad |
| W20 | 4 | 3 (jwt-drill + 2 bulk) | Swiss pair-next-round + bulk ladder + drill |
| **W21** | **5** | **5** (Swiss apply + rotation-schedule + withdraw + retention purge + restoration audit read) | **5 deliverables paired admin UI 1:1** |

### W21 new Prometheus instruments

| Counter / Recording rule | Labels | Surface |
|---|---|---|
| `jwt_scheduled_rotation_total` | `tenant`, `status` (success / error / skipped) | `RotationScheduledExecutorService` tick |
| `jwt_validator_anomaly_total` | `tenant`, `reason` (clock-skew / invalid-issuer / expired-too-soon) | `JwtValidationService` (new 5-arg ctor) |
| `signalr_manual_purge_total` | `tenant` | `SignalRRetentionManualPurgeController` |
| `signalr:churn_rate_5m` (recording rule) | (none — gauge-derived) | `prometheus-rules-signalr.yaml` overlay |

### W21 new Prometheus alerts

| Alert | Threshold | For | Severity | Routing |
|---|---|---|---|---|
| SignalrChurnHigh | `signalr:churn_rate_5m > 10` disconnects/min | 5m | warning | Slack `#alerts-apone` |
| SignalrChurnCritical | `signalr:churn_rate_5m > 30` disconnects/min | 3m | critical | PagerDuty (DevOps on-call) |

All Apone-lane alerts carry the `team: apone` label per W18 §9.4 alert-label convention.

---

## 9. W22 forward-look

### Bishop W22 candidates

- **Auto-start-match flow** on Swiss apply-round projections — W21 ships the projection from audit rows to TournamentMatch rows; W22 wires the `in-progress` push + notify-table dispatch.
- **`SimpleCronMatcher` extensions** if operators ask for `L` / `W` / `#` extensions — swap to Cronos library; the matcher is wrapped behind a single static so the swap is a 30-line patch.
- **Per-tenant scheduled rotation P95 panel** — W21 ships the counter; W22 adds dashboard P95 + alerting against execution latency.
- **csproj `<Version>0.31.0</Version>` cadence bump** per the W18 §9.18 convention with Apone W22 `CHANGELOG.md [0.31.0]`.
- **Tournament withdraw-player follow-up** — W22 may add an admin-driven `un-withdraw` endpoint for accidental withdrawal recovery.

### Hicks W22 candidates

- **LH13 §6.10 PROMOTE re-evaluation** — at ~3+ days post-W18-merge by W22; if Coordinator-direct probe path lands (per W19 hand-off escalation recommendation), the §4.2 observation channel finally opens. Otherwise continue YELLOW with §6.11 record.
- **Admin-panel chunk-split** — at 48,984 B vs 49,152 B ceiling (168 B headroom), W22 admin surface work MUST chunk-split. Two reasonable axes documented in Hicks W21 hand-off: by domain (admin-panel-tournaments + admin-panel-infra + admin-panel-replays) or by cardinality (W18 action-router pattern).
- **Bundle audit §3.7** — `game-bootstrap` re-fold targeting the next 8-12 KB. Risk: moving scheduler shells into game-bootstrap breaks "open profile while lobby is empty" flow. Recommend a separate spike wave (not jointly with surface work).
- **Phase L renderer — declare-claim animation + score animations + win-burst** — W21 ships tile-claim + meld-display (renderer-webgl2 40,292 B; 8,860 B under 48 KB ceiling). W22 layers the next renderer surfaces before the chunk-split conversation needs to start.

### Apone W22 candidates

- **Kyverno W21 audit-mode enforce-flip** — `require-resource-limits` + `disallow-host-paths` Audit → Enforce + Ignore → Fail; 5-day grace window from W21 closes at W22; pre-W21 verification commands captured into `.work/apone-w21-safe/` for the W22 evidence trail.
- **Argo Rollouts AnalysisRun retention bump** — today defaults to 5 runs; W22 may bump to 20 for forensics.
- **Per-tenant SignalR churn threshold dialing** (tier-1 vs tier-3) — requires per-tenant ruling.
- **W22 staging-tier auto-rollback dry-run validation** BEFORE prod opt-in.
- **CHANGELOG `[0.31.0]` cadence** + `mobile/package.json` 0.30.0 → 0.31.0.

### Vasquez W22 candidates

- **§4.8 14-wave deferral arc** — Coordinator-direct escalation memo candidate if Stephen movement absent at W22 close.
- **§6.11 LH13 disposition re-evaluation** — PROMOTE confirm vs HOLD continue; W22 §6.x Coordinator-direct probe path escalation if `gh`-auth unresolved.
- **KW21 → KW22 regression rename** — canonical `git mv` + new W22 pin + W21 pin rewritten to `_Historical` (asserts W19 + W20 + W21 class names absent).
- **W22 forward-stage contracts** — 20-26 new files under `Phase_K_W22/Vasquez/`.
- **`tests/ci/check-stash-name-shape.sh` hook** — W21 §9 stash-isolation directive formalised; W22 deliverable to add the CI enforcement layer.
- **W21 retrospective audit Vasquez-self-loop** — per W21 §9.3 audit table propagation.

### Coordinator-direct W22 candidates

- **Prep Coordinator-direct escalation memo for Stephen §4.8** — 14-wave deferral arc at W22 = 1 wave beyond the symbolic year-of-bring-ups threshold; escalation memo is a fair trigger.
- **LH13 §6.x Coordinator-direct probe path** — if `gh`-auth still unresolved at W22, EXECUTE the cron-history probe directly (4th Coordinator-direct EXECUTION event since the ledger opened at W17).
- **Maintain zero-INTERVENTION discipline** — 17th consecutive wave at W22 close if held.

### Scribe / Coordinator W22 candidates

- **Per-invocation `git -c user.name=X -c user.email=Y commit ...`** remains canonical (held over W6 → W21; **16 consecutive clean waves**).
- **`flock 9>.work/squad-git-lock` mutex with atomic-flock requirement** (12th consecutive fully-adopted wave at W21; second 4-for-4 atomic-flock compliance at W21).
- **Renamed-stash + stash-isolation directive** as first-class step in every prompt template per W20 §7.2 + W21 §9.1.
- **CHANGELOG version-arithmetic check** — W21 `[0.30.0]` clean + csproj agrees; **W22 `[0.31.0]`**.
- **Coordinator-direct EXECUTION ledger** — Scribe §6.5 captures any W22 Coordinator-direct EXECUTIONs; cumulative ledger holds at 3 events / 8 actions across W17+W18+W19 with W20 + W21 contributing zero.

---

## 10. File-by-file delta (W21 commits)

### Apone `55fc04e` — 13 files

| Path | Lane | Status |
|---|---|---|
| `infra/k8s/base/argo-rollouts/frontend-canary.yaml` | apone | NEW |
| `infra/k8s/base/kyverno-policies/require-resource-limits.yaml` | apone | NEW |
| `infra/k8s/base/kyverno-policies/disallow-host-paths.yaml` | apone | NEW |
| `infra/terraform/regional-eks/us-east-1/auto-rollback.tf` | apone | NEW |
| `infra/k8s/overlays/prod/prometheus-rules-signalr.yaml` | apone | NEW |
| `.github/workflows/helm-release.yml` | apone | NEW |
| `docs/argo-rollouts-frontend-canary.md` | shared | NEW |
| `docs/kyverno-w21-additional-rules.md` | shared | NEW |
| `docs/helm-release.md` | shared | NEW |
| `docs/signalr-observability-w21.md` | shared | NEW |
| `mobile/package.json` | apone | EXT (0.29.0 → 0.30.0) |
| `CHANGELOG.md` | shared | EXT ([0.30.0]) |
| `.squad/decisions/inbox/apone-phase-k-wave-21.md` | apone | NEW (force-added) |

### Hicks `47d0fe5` — 38 files (frontend + bundle ledger)

| Group | Detail |
|---|---|
| New renderer modules | `tile-claim-animation.ts`, `meld-display.ts` |
| New admin SPECs | `swiss-apply-round.ts`, `rotation-schedule.ts`, `tournament-withdraw.ts`, `signalr-purge.ts`, `replay-restoration-audit.ts` |
| New module extraction | `profile-drawer.ts` (extracted from `profile.ts`) |
| New i18n catalog plumbing | (no new file — `i18n.ts` EXT for lazy `zh-*` loader) |
| EXT frontend source | `lobby.ts`, `profile.ts`, `i18n.ts`, `hello.ts` (renderer-webgl2), `index.ts`, `admin-panel.ts` |
| Bundle output (NEW chunk hashes) | 22 new (`action-router.832ac03a.js`, `admin-panel.19219ae9.js`, …, `zh-Hans.1b7ceb94.js`, `zh-Hant.c26934d4.js`, etc.) |
| Bundle output (DELETED prior W20 chunk hashes) | 22 (W20 chunk-hash inputs replaced) |
| Manifests | `manifest-precache.json` rolled; `dist-size.json` K21 row appended via `scripts/append-dist-size.js` |
| Docs | `docs/lh13-soft-pin-rationale.md §12` appended; `docs/frontend-bundle-audit.md §4.3` appended |
| Inbox | `.squad/decisions/inbox/hicks-phase-k-wave-21.md` NEW (force-added) |

### Bishop `f0028a1` — 31 files (backend, +9,144 lines)

| Group | Detail |
|---|---|
| New services / handlers | `SwissApplyRoundService.cs`, `RotationScheduledExecutorService.cs`, `SimpleCronMatcher.cs` |
| New controllers | `SwissApplyRoundController.cs`, `RotationScheduleAdminController.cs`, `ReplayRestorationAuditController.cs`, `TournamentWithdrawPlayerController.cs`, `SignalRRetentionManualPurgeController.cs` |
| New entities | `RotationScheduleEntity.cs`, `ReplayRestorationAttempt.cs` |
| New metrics collectors | `JwtScheduledRotationMetrics.cs`, `JwtValidatorAnomalyMetrics.cs`, `SignalRManualPurgeMetrics.cs` |
| EXT validator | `JwtValidationService.cs` (new 5-arg constructor) |
| New audit-kind constants | 6 (`KindTournamentSwissRoundApplied`, `KindAuthJwksRotationScheduled`, `KindAuthJwksRotationScheduledExecuted`, `KindTournamentPlayerWithdrawn`, `KindReplayRestorationAttempt`, `KindSignalRManualPurge`) |
| New test classes | 11 (118 tests; see §2.3) |
| 3-provider EF migration | `Phase_K_W21_RotationScheduleAndReplayRestoration` (Postgres / Sqlite / SqlServer + `.Designer.cs` each) |
| Model snapshots | 3 (one per provider) |
| Observability | `Observability/dashboards/jwt-validator-metrics.json` panels 9 + 10 added |
| EXT | `Program.cs` DI wiring (5 service registrations); `Mahjong.Autotable.Api.csproj` `<Version>0.30.0</Version>` |
| Inbox | `.squad/decisions/inbox/bishop-phase-k-wave-21.md` NEW (force-added) |

### Vasquez `6d8aa93` — 42 files (renamed regression + 25 forward-stage + 13 broadenings + 2 docs)

| Group | Detail |
|---|---|
| Renamed regression test | `Wave1ThroughKW20RegressionTests.cs` → `Wave1ThroughKW21RegressionTests.cs` (via `git mv`; 19 typeof refs sed-rewritten; W21 xmldoc paragraph appended) |
| NEW `Phase_K_W21/Vasquez/*.cs` contract files | 25 (Bishop 9 + Hicks 7 + Apone 5 + self-lane 4) |
| EXT prior-wave self-lane + surface-smoke files | 13 (W11+W12+W13+W14+W15+W16+W17+W18+W19+W20 self-lane + W11+W12+W20 surface-smoke — OR-broadening) |
| Self-repair | `AponeW20ChangelogW20ContractTests.cs` (mobile-pin substring → `0.29.0 || 0.30.0`) |
| EXT docs | `docs/agent-handoff-protocol.md` (+251 lines: §6.10 LH13 + §9 stash-isolation directive + W21 retrospective audit table) |
| Inbox | `.squad/decisions/inbox/vasquez-phase-k-wave-21.md` NEW (force-added) |

---

## 11. Metrics dashboard (cumulative W6 → W21)

| Metric | W6 baseline | W17 | W18 | W19 | W20 | **W21** | Δ vs W6 |
|---|---:|---:|---:|---:|---:|---:|---:|
| Test gate (passed) | 1422 | 3930 | 4111 | 4376 | 4637 | **4846** | **+3,424 (+240.8 %)** |
| Test gate (skipped) | 7 | 0 | 0 | 0 | 0 | **0** | **−7 (zero-skip streak 36 waves)** |
| three-renderer-big (B) | 738,431 | 406,635 | 406,635 | 406,635 | 406,635 | **406,635** | **−44.9 % (hold-line 11 waves)** |
| autotable-src-eager (B) | — | 176,907 | 156,191 | 144,192 | 123,701 | **112,219** | **−49.6 % cumulative since W15** |
| Lane-discipline streak (0-violation waves) | — | 7 | 8 | 9 | 10 | **11** | **+11 consecutive — milestone** |
| Identity-clean streak (waves) | — | 12 | 13 | 14 | 15 | **16** | **+16 consecutive** |
| Flock mutex streak (waves) | — | 8 | 9 | 10 | 11 | **12** | **+12 consecutive; 2nd 4-for-4 atomic-flock** |
| Coordinator-direct INTERVENTIONS (cumulative) | 0 | 0 | 0 | 0 | 0 | **0** | **16-wave zero streak preserved** |
| Coordinator-direct EXECUTIONS (cumulative) | 0 | 1 | 3 | 4 | 4 | **4** | **W21 contributes 0 (2nd zero-EXECUTION wave)** |
| SLSA-3 SHA-pin count | 0 | 56 | 191 | 191 | ~206 | **~206** | **repo-wide COMPLETE held W20→W21** |
| shared_files registry entries | varied | 8 | 8 | 8 | 8 | **8** | **7 waves unchanged W15→W21** |
| Audit kind catalogue (total) | — | 17 | 20 | 24 | 29 | **35** | **+6 W21; +18 total since W17** |

---

## 12. Argo Rollouts trilogy — COMPLETE at W21

| Wave | Deliverable | Surface |
|---|---|---|
| W11–W19 (prep) | Install runbook + RBAC + namespace prereqs + Helm posture | `docs/argo-rollouts-install-runbook.md` + `infra/k8s/base/argo-rollouts/` namespace + RBAC manifests |
| W20 | Backend BlueGreen template | `infra/k8s/base/argo-rollouts/backend-bluegreen.yaml` (333 lines; out-of-band) + `docs/argo-rollouts-backend-bluegreen.md` (305 lines; 8-row Canary↔BlueGreen decision matrix) |
| **W21** | **Frontend Canary template** | `infra/k8s/base/argo-rollouts/frontend-canary.yaml` (4 weight steps + 10-min pause + AnalysisRun gates + 0.5 % error-rate AnalysisTemplate) + `docs/argo-rollouts-frontend-canary.md` |

**Every workload class now has at least one strategy template wired by W21.** The cutover (Stephen-blocked Rollouts install in prod cluster) unlocks the full progressive-delivery toolkit immediately: backend gets BlueGreen-mode (Deployment-scale-to-0 cutover), frontend gets Canary (5/25/50/100 weight ladder with automated error-rate gates).

### Frontend Canary AnalysisRun gate shape

| Phase | Weight | Pause | AnalysisRun gate | Total elapsed |
|---:|---:|---|---|---:|
| 1 | 5 % | 10 min | `frontend-canary-error-rate` (15 s × 40 iter; pass when 38/40 samples < 0.5 % error) | 10 min |
| 2 | 25 % | 10 min | same | 20 min |
| 3 | 50 % | 10 min | same | 30 min |
| 4 | 100 % | (promotion) | — | ~30 min cumulative |

**Why 0.5 % error-rate threshold:** the W17 LH13 baseline was 0.12 % over 24 h; 0.5 % gives a ~4× safety margin while still catching the worst-case regressions (chunk-hash mismatch on a canary push would push error rate above 1 % within the first minute). `failureLimit: 2` absorbs single noisy intervals; `inconclusiveLimit: 4` tolerates Prometheus NaN returns during traffic lulls.

---

## 13. Coord-direct count (W6 → W21)

| Type | Cumulative W6 → W21 | W20 contribution | W21 contribution |
|---|---:|---:|---:|
| Coordinator-direct INTERVENTIONS | 0 | 0 | 0 |
| Coordinator-direct EXECUTIONS (events) | 3 | 0 | 0 |
| Coordinator-direct EXECUTIONS (individual actions) | 8 | 0 | 0 |

**EXECUTION ledger (cumulative, unchanged at W21):**

| Wave | Event | Shots | Attribution | Outcome |
|---|---|---:|---|---|
| W17 | LH13 §6.7 cron seed (PRIMARY pump) | 3 | Coordinator-direct | 3rd run `failure` (root cause discovered at W17 close; Apone D1 fix at W18) |
| W18 | LH13 §6.7 post-fix cron seed | 3 | Coordinator-direct | 3 × `success` (empirical convergence) |
| W18 | Bishop test-regex anchor fix | 1 | Coordinator-direct (commit attribution: Bishop-lane) | Gate 4110/4111/0 → 4111/0/0 |
| W19 | Bishop W19 inbox-memo `git add -f` force-add (`e341092`) | 1 | Coordinator-direct (commit attribution: Bishop-lane per W18 §8.3) | Preserves Scribe-fold input for W19 decision-ledger continuity |
| W20 | — (zero) | 0 | — | First zero-EXECUTION wave since the ledger was introduced at W17 |
| **W21** | **— (zero)** | **0** | **— (all 4 agents shipped clean + self-sufficient; in-wave Vasquez self-repair of pre-existing test failure)** | **Second consecutive zero-EXECUTION wave** |

**16-wave zero-INTERVENTION streak (W6 → W21) preserved by design.** EXECUTION cadence by wave: W17 1 event → W18 2 events → W19 1 event → W20 0 events → **W21 0 events**. Two consecutive zero-EXECUTION waves validate the empirical hypothesis that per-agent prompt-template hardening converts one-shot incidents into permanent process improvements.

---

## 14. Sign-off

**W21 is the wave that:**

1. **Lifts the gate to 3.41× W6 baseline** — 1422 → 4846 = +3,424 over 16 waves; **+209 over W20 close = +4.5 percentage-point cumulative growth in a single wave**.
2. **Hits the §3.6 bundle ceiling with 5,541 B of headroom** — `autotable-src-eager` 123,701 → 112,219 B; **−11,482 B; 6-wave cumulative −110,628 B = −49.6 % (crosses the −50 % milestone near-miss)**.
3. **Holds three-renderer-big at 406,635 B for the 11th consecutive wave** — 11th-consecutive-wave milestone; cumulative W6 → W21 −44.9 % unchanged.
4. **Completes the Argo Rollouts trilogy** — W19 install runbook + W20 backend BlueGreen + W21 frontend Canary = every workload class has a documented progressive-delivery strategy template.
5. **Holds LH13 §6.10 YELLOW** — 3rd consecutive wave on the same `gh`-auth observation gap; W22 recommends §6.x Coordinator-direct probe escalation rather than continuing to inherit YELLOW.
6. **Lands the Kyverno W21 audit-mode pair** — `require-resource-limits` + `disallow-host-paths` Audit → 5-day grace; W22 enforce-flip pre-wired; mirrors W19 → W20 ladder.
7. **Lands Bishop's 7 backend deliverables** — anchored by Swiss apply-round service (closing the W20 propose-pair / W21 apply-pair loop) + Scheduled per-tenant JWKS rotation (entity + admin controller + cron matcher + BackgroundService + Prom counter) + Replay restoration audit log + JWT validator anomaly metrics (wired into JwtValidationService via new 5-arg constructor) + Tournament withdraw-player + SignalR retention manual purge + 6 new audit-kind constants + 3-provider migration + Grafana panels 9 + 10.
8. **Lands Hicks's 5 frontend deliverables** — anchored by Phase L tile-claim-animation + meld-display NEW (pung/kong/chi staggered fan-in + per-seat meld row layout; renderer-webgl2 35,258 → 40,292 B) + bundle §3.6 surgery (profile-drawer + zh-Hans + zh-Hant lazified; eager 123,701 → 112,219 B) + 5 new admin UI W21 surfaces + 11th three-renderer-big hold-line.
9. **Lands Apone's 6 operator-readiness deliverables** — anchored by Argo Rollouts frontend Canary template + Kyverno W21 audit-mode pair + us-east-1 auto-rollback safety net + Helm chart release pipeline (cosign signed; OCI push to ghcr.io) + SignalR churn observability (P95 churn alerts with `team: apone`) + CHANGELOG `[0.30.0]`.
10. **Lands Vasquez's 6 W21 brief deliverables + 25 forward-stage contracts + 13 prior-wave broadenings** — anchored by §9 NEW top-level stash-isolation directive (codifies W20 Apone-mid-task-reset lesson into 5 sub-rules) + §6.10 NEW LH13 W21 HOLD-YELLOW ratification + W20 mobile-pin forward-broadening repair (W21 NEW precedent) + KW20 → KW21 regression rename.
11. **Achieves second consecutive 4-for-4 atomic-flock compliance** — all 4 bring-up agents (Apone + Hicks + Bishop + Vasquez) ran stage + commit + push inside a SINGLE flock block per the W19 §7.1 lesson; **discipline now ratcheted permanently**.
12. **Achieves second consecutive zero-EXECUTION wave (W20 + W21)** — Coordinator-direct EXECUTION ledger holds at 3 events / 8 actions across W17+W18+W19; W20 + W21 contribute zero each. The W17–W19 lessons + W20 §7 retro + W21 §9 stash-isolation directive all propagated cleanly into the per-agent prompt template.
13. **Self-repairs 1 pre-existing test failure in-lane** — Vasquez W21 forward-broadens the W20 mobile-pin substring contract from `0.29.0` to `0.29.0 || 0.30.0`; **W21 NEW convention: forward-broadening precedent for version-pin contract tests**; first in-wave QA self-repair of a cross-wave version-pin breakage.
14. **Confirms the `shared_files` registry late-mature steady state for the 4th consecutive wave** — 8 entries unchanged across W15 → W21 (7 waves).
15. **Codifies the W20 stash-collision incident into the W21 §9 stash-isolation directive** — convention recursion: the directive that codifies the recovery pattern was first applied by the wave that codified it (Vasquez `vasquez-w21-hicks-frontend-shield-1779635691` shield stash demonstrates §9.1 rule 4).

**All 4 W21 bring-up commits land cleanly under per-invocation identity hardening + atomic flock mutex + selective `git add` (files-by-name only; no `-A` / no `-u` / no directory wildcards) + Co-authored-by trailer. The 1 W21 anomaly (Hicks's leftover frontend bundle hash-rename byproducts at Vasquez rebase time) is shielded in-flight via the §9.1 rule 4 pattern (Vasquez stash `vasquez-w21-hicks-frontend-shield-1779635691`); the 16-wave zero-INTERVENTION streak preserved by design via W17–W20 lessons propagating into the W21 prompt template; 11th consecutive 0-violation lane-discipline wave at the tip with 8 unamended in 11 (73 % unamended at W21 — late-mature steady state hardens further); SLSA-3 SHA-pinning ladder held at repo-wide COMPLETE; 11th consecutive three-renderer-big hold-line wave at 406,635 B; Argo Rollouts trilogy COMPLETE; bundle §3.6 ceiling MET with 5,541 B headroom (−49.6 % cumulative compression milestone near-miss).**

**Phase K Wave 21 — DONE.**
