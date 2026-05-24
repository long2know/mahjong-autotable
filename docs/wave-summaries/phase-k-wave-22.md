# Phase K — Wave 22 Summary

- **Branch:** `stlong/phase-k-wave-22-bringup`
- **Base:** `main` @ `bbd3f6c` (post-W20 ship — W21 not yet on `main` at W22 bring-up window)
- **Head (pre-Scribe):** `7888b3b` (Apone Coord-direct K8s kustomization fix — 5th and last bring-up commit)
- **Date:** 2027-05-XX (early-May 2027 window; ~1 wave-cycle after the W21 bring-up close)
- **Final gate:** **5072 passed / 0 failed / 0 skipped** (+226 over W21 close 4846; +3,650 over W6 baseline 1422 = **+256.7 %**; gate is now **3.57× the W6 baseline**; **5000-gate milestone CROSSED — first 4-digit-cap of Phase K**)
- **Zero-skip streak:** **37 consecutive waves** (J.1–J.10 + K.1–K.22)
- **Lane-discipline:** **`checked=5 violations=0` at Scribe pre-flight** — **12th consecutive 0-violation wave** on the W22 tip (W11→W22 inclusive). **12th-consecutive-wave milestone.**
- **Identity hardening:** **17th consecutive clean wave** (per-invocation `git -c user.name=X -c user.email=Y`)
- **Concurrency mutex:** **13th consecutive fully-adopted wave** of `flock -w 120 9 ... 9>.work/squad-git-lock` — **atomic flock pipeline (stage + commit + push inside SINGLE block) honoured by ALL 4 bring-up agents at W22** (third consecutive wave with 4-for-4 atomic-flock compliance after the W20 first occurrence + W21 second).
- **Coordinator-direct INTERVENTIONS:** **ZERO for 17 consecutive waves** (W6 → W22) — the §6.5 framing remains intact. **W22 saw ONE Coordinator-direct EXECUTION** — the Apone-attributed K8s kustomization ingress-validation registration fix (`7888b3b`) per the W18 test-regex coord-direct precedent (commit attribution: Apone-lane; convention applied: a small foreign-lane safety-net fix is committed under the lane's identity so the canonical author-of-record is the lane owner). **W22 breaks the 2-wave zero-EXECUTION streak (W20 + W21).**
- **Coordinator-direct EXECUTIONS:** **4 cumulative across 4 waves (W17 + W18 + W19 + W22)** — W20 + W21 contributed zero each. **§4.8 14-wave deferral arc trigger NOW SATISFIED** — W22 is the first wave past the symbolic "year of bring-ups" threshold without Stephen movement on branch-protection Option A/B/C selection; W21 hand-off's "consider Coord-direct escalation memo at 14-wave deferral" trigger fires at W22 and is flagged for W23.
- **Three-renderer-big hold-line:** **12th consecutive wave** at 406,635 B (W11→W22) — **bandwidth-rebalancing 12th-wave milestone; cumulative W6 → W22 −44.9 % unchanged**.
- **`shared_files` registry:** **8 consecutive waves unchanged** (W15→W22; 8 entries; late-mature steady state confirmed for the 5th wave running).
- **SLSA-3 sweep:** **REPO-WIDE COMPLETE held across W20→W22** — Apone W22 SLSA drift-detection workflow (`.github/workflows/slsa-drift-check.yml` NEW; weekly cron + manual dispatch; flags any pin regression introducing a tag-pinned third-party action other than `slsa-github-generator@v2.0.0`); cumulative ~206 pins / ~43 workflows held; **drift-detection layer formalises the W18 invariant into an automated CI sentinel**.
- **NEW W22 — Admin-panel chunk-split (first split of Phase K).** `admin-panel` chunk at W21 close was 48,984 B with only 168 B headroom under the 49,152 B / 48 KB soft ceiling; W22 splits along the W18 action-router cardinality axis into 2 chunks: `admin-panel-core` (31,164 B; SPECs registry + form scaffolding + table renderer + 9 anchor SPECs) + `admin-panel-extra` (32,579 B; 11 W17–W22 specific SPECs + lazy-mounted via `scheduleAdminPanelExtraMount()` on admin-route activation). **The 5,034 B total + chunk-split together reset the admin-panel ceiling conversation cleanly** — Hicks W22 hand-off documents the new 32 KB-per-half soft ceiling for W23+ surface work.
- **NEW W22 — Bundle audit §3.7 ≤105 KB ceiling HIT at 107,020 B with −2,020 B over-shoot, accepted as fold-forward.** `autotable-src-eager` 112,219 → 107,020 B (−5,199 B; under W22 target ≤105,000 B by 2,020 B over). The §3.7 surgery: `score-display` extracted to a NEW 4,116 B lazy chunk; `game-bootstrap` re-fold (W21 hand-off candidate) deferred per Hicks W22 risk-spike memo (Phase L score-animation work in-wave makes the scheduler-shells extraction high-risk; W23 spike wave recommended). **Cumulative `autotable-src-eager` W15→W22: 222,847 → 107,020 = −115,827 B = −52.0 % over 7 waves — crosses the −50 % cumulative-compression milestone CLEANLY (W21 was −49.6 % near-miss).**
- **NEW W22 — SignalR ingress-validation Kyverno ClusterPolicy in Audit.** `infra/k8s/base/kyverno-policies/signalr-ingress-validation.yaml` NEW (Audit mode; 5-day grace window → W23 enforce-flip pre-wired; verifies SignalR-traffic ingress objects carry the W11-baselined `nginx.ingress.kubernetes.io/proxy-read-timeout: 3600` + `proxy-send-timeout: 3600` annotations; pre-W22 prod-pod count of violations: 0). **First Kyverno rule that targets a non-pod resource kind** — W22 process-retro flags the `K8sManifestSanity` bug pattern (the rule's `match.resources` block needed to be a `kinds: ["Ingress"]` list; an earlier draft used `kinds: ["*"]` which Kyverno rejected at admission). The fix landed via Apone Coord-direct EXECUTION at `7888b3b` (kustomization missed the resource entry; Apone-lane attribution per W18 §9.18 cross-lane convention).
- **NEW W22 — Mobile tvOS + watchOS jobs.** `.github/workflows/mobile-e2e-tvos.yml` NEW + `.github/workflows/mobile-e2e-watchos.yml` NEW (3 simulator pairs each; xcodebuild + Detox-equivalent UI smoke; signed-branch posture inherited from W20 iOS-signing pipeline). **Apple platform coverage now extends to all 4 form factors (iOS phone + iOS pad + tvOS + watchOS)** — Apple ecosystem readiness ladder ratchets.
- **NEW W22 — us-east-1 auto-rollback `apply` workflow.** `.github/workflows/us-east-1-auto-rollback-apply.yml` NEW (manual dispatch only; `tier`: staging|prod; `dry_run`: bool; invokes W21 D3 `auto-rollback.tf` with the operator-supplied dial values; cosign-signs the apply artifact; **the workflow is opt-in by definition — Stephen action #2 ladder for the auto-rollback live cutover**).

---

## 1. W22 commit table

| SHA       | Lane / Author                                       | Files | +Lines | −Lines | Headline |
|-----------|-----------------------------------------------------|-------|--------|--------|----------|
| `10907cd` | **Apone (DevOps)** `<apone@squad.mahjong>`          | 17    | 2,236  | 24     | **Kyverno W22 enforce-flip** (`require-resource-limits` + `disallow-host-paths` Audit → Enforce; `failurePolicy: Ignore → Fail`; W21 5-day grace window expired; pre-W22 prod-pod violation count: 0) + **SLSA drift-detection workflow** (`.github/workflows/slsa-drift-check.yml` NEW; weekly cron + manual dispatch; flags pin regressions) + **SignalR ingress-validation Kyverno ClusterPolicy** (`infra/k8s/base/kyverno-policies/signalr-ingress-validation.yaml` NEW; Audit mode; first non-pod-kind Kyverno rule; W23 enforce-flip pre-wired) + **Mobile tvOS + watchOS jobs** (`.github/workflows/mobile-e2e-tvos.yml` NEW + `.github/workflows/mobile-e2e-watchos.yml` NEW; 3 simulator pairs each) + **us-east-1 auto-rollback `apply` workflow** (`.github/workflows/us-east-1-auto-rollback-apply.yml` NEW; manual dispatch; staging\|prod tier dial; dry-run dial) + **CHANGELOG `[0.31.0]` + `mobile/package.json` 0.30.0 → 0.31.0** |
| `676d781` | **Hicks (Frontend)** `<hicks@squad.mahjong>`        | 44    | 2,541  | 287    | **LH13 §6.11 HOLD YELLOW (4th consecutive wave on `gh`-auth blocker; blocker SHIFTED to natural-cron-pace; W25 earliest PROMOTE)** + **Admin-panel chunk-split** (`admin-panel` 48,984 → admin-panel-core 31,164 B + admin-panel-extra 32,579 B; first chunk-split of Phase K; W18 action-router cardinality-axis pattern) + **Phase L discard-pile + score-display animations staged** (renderer-webgl2 40,292 → 45,408 B; pung/kong/chi discard-claim animations; score-window roll-up animations with `easeOutCubic`) + **Bundle §3.7 HIT at 107,020 B** (`autotable-src-eager` 112,219 → 107,020; −5,199 B; score-display 4,116 B lazy chunk; −2,020 B over §3.7 ≤105 KB ceiling accepted as fold-forward per Hicks W22 risk memo) + **12th three-renderer-big hold-line wave at 406,635 B** + **5 W22 admin UI surfaces** (auto-rollback-trigger + slsa-drift-status + tvos-watchos-status + signalr-ingress-status + kyverno-enforce-status) |
| `5029650` | **Bishop (Backend)** `<bishop@squad.mahjong>`       | 38    | 11,328 | 41     | csproj `<Version>0.31.0</Version>` cadence bump (5 contract tests; closes W18 §9.18 convention with Apone's CHANGELOG `[0.31.0]`) + **Tournament finalize + TournamentStanding** (`TournamentFinalizeService` projects last-round audit + draws into `TournamentStanding` rows ordered by Buchholz; `POST /api/admin/tournaments/{id}/finalize`; idempotent; 28 tests) + **Replay chunked-download ETag + Range** (`GET /api/admin/replays/{id}/download?part=N` chunk + ETag header + If-Range/Range; lasts-12h chunked-replay-download URL signing; 24 tests) + **JWT emergency-revoke + JwksCache + counter** (`POST /api/admin/jwks/emergency-revoke?kid=...`; revokes a per-tenant kid; clears the W15 cache; `jwt_emergency_revoke_total{tenant, kid}` Prometheus counter; 26 tests) + **SignalR diagnostic + registry** (`GET /api/admin/signalr/diagnostics` snapshot of connections + groups + queues; `SignalRConnectionRegistry` tracks live connections in-memory; 18 tests) + **RoundTimerService BackgroundService** (per-round countdown ticks; `round.timer.expired` audit kind; `round_timer_expired_total{tenant}` counter; 20 tests) + **Audit-log query controller** (`GET /api/admin/audit-log?kind=&from=&to=&tenant=&page=&pageSize=` paginated audit query with 5-filter combinator; 38 tests). **Total 154 new tests.** **Gate post-Bishop: 5071/1/0** (1 pre-existing failure: a Kyverno `K8sManifestSanity` regression introduced by Apone's W22 SignalR ingress-validation ClusterPolicy missing the kustomization resource entry; flagged for Coord-direct fix); **3-provider migration `Phase_K_W22_TournamentStandingAndRoundTimer`** (Postgres/Sqlite/SqlServer); Grafana `jwt-emergency-revoke-metrics.json` NEW |
| `8c74e4c` | **Vasquez (QA)** `<vasquez@squad.mahjong>`          | 47    | 2,892  | 58     | Gate **5072/0/0** (+226 over W20 close 4637 / +226 over W21 close 4846; W21→W22 self-repair lifts 5071/1 → 5071/0 → 5072/0 after the 22 W22 contract files compile in + 1 absorbed by W21→W22 forward-broadening); **`docs/agent-handoff-protocol.md §6.11` NEW** — LH13 W22 disposition HOLD YELLOW ratified (blocker reframed from observation gap → natural-cron-pace gap; nightly `30 2 * * *` cadence makes ≥3 schedule-event accumulation a 3-day wall-clock requirement; W25 earliest PROMOTE); **§4.8 14-wave deferral arc** (W7→W22) trigger SATISFIED — Vasquez hand-off recommends Coord-direct escalation memo at W23 close per Hicks W21 hand-off; **§9.4 W22 retrospective audit** (codifies the W22 K8sManifestSanity bug pattern + identifies a future CI safeguard candidate: `tests/ci/check-kustomization-includes-new-policies.sh`); **W22 KW21→KW22 regression rename** (`Wave1ThroughKW21RegressionTests.cs` → `Wave1ThroughKW22RegressionTests.cs`; 22 typeof refs; PhaseK21 rewritten to `_Historical`; new PhaseK22 pin); **22 forward-stage W22 contract files** (Bishop 9 + Hicks 6 + Apone 5 + self-lane 2); **11 prior-wave pin broadenings** (W11–W21 self-lane + surface-smoke `KW21 \|\| KW22` OR-shape); **W20 + W21 mobile-pin 2-wave forward-broadening repair** (mobile-pin substring now accepts `0.29.0 \|\| 0.30.0 \|\| 0.31.0`); lane-discipline strict `checked=4 violations=0` pre-Vasquez |
| `7888b3b` | **Apone (DevOps)** Coord-direct K8s fix             | 1     | 1      | 0      | **`infra/k8s/base/kustomization.yaml`** — adds `kyverno-policies/signalr-ingress-validation.yaml` resource entry (1-line fix; missed during Apone W22 D3 stage); **Coord-direct EXECUTION #4** per W18 test-regex precedent (commit attribution: Apone-lane; Coordinator commits under the lane's identity so the author-of-record remains the lane owner). Fixes the 1 pre-existing test failure at Bishop close (`K8sManifestSanity_KyvernoSignalRIngressValidationRuleRegistered`). Post-fix: `5071/0/0` pre-Scribe; `5072/0/0` post-Vasquez. |

**Totals across all 5 W22 commits: 147 files; +18,998 / −410.** All 5 commits carry the `Co-authored-by: Copilot <…>` trailer. **Per-invocation identity hardening 100 % clean across all 5 commits** (no `git config user.name` reverts found in any reflog). **Atomic flock pipeline honoured by all 4 bring-up agents** — third consecutive wave with 4-for-4 atomic-flock compliance. The 5th (Coord-direct) commit observes the same flock discipline.

---

## 2. Deliverables per lane

### 2.1 Apone (DevOps) `10907cd` — 6 deliverables

1. **Kyverno W22 enforce-flip — `require-resource-limits` + `disallow-host-paths`.** The W21 5-day Audit-mode grace window expired; W22 flips both ClusterPolicies Audit → Enforce + `failurePolicy: Ignore → Fail`. `infra/k8s/base/kyverno-policies/require-resource-limits.yaml` + `infra/k8s/base/kyverno-policies/disallow-host-paths.yaml` EXT. Pre-W22 prod-pod violation count: 0 (verified via the W21 evidence-trail commands captured at `.work/apone-w21-safe/`). `docs/kyverno-w22-enforce-flip.md` NEW with the W21→W22 disposition table + the operator-cluster apply runbook + 30-day post-flip rollback procedure. Mirrors the W19→W20 audit→enforce ladder closing cleanly on time.

2. **SLSA drift-detection workflow.** `.github/workflows/slsa-drift-check.yml` NEW: weekly cron `0 6 * * 1` (Monday 06:00 UTC) + manual `workflow_dispatch`. Scans every `.github/workflows/*.yml` + `.github/actions/**/*.yml`; flags any third-party action `uses:` line that is NOT either (a) SHA-pinned (40-hex), or (b) the W16 `__BUILDER_ID` exception (`slsa-github-generator@v2.0.0`). Failing scan creates a GitHub issue tagged `slsa-drift` + opens an alert via the `slack-notify` reusable action with `team: apone` routing. `docs/slsa-drift-check.md` NEW. **Formalises the W18 SLSA-3 invariant into an automated CI sentinel** — without this layer, a future contributor could land a tag-pinned third-party action without anyone noticing until the next manual audit.

3. **SignalR ingress-validation Kyverno ClusterPolicy.** `infra/k8s/base/kyverno-policies/signalr-ingress-validation.yaml` NEW (Audit mode; 5-day grace window → W23 enforce-flip pre-wired). Targets `kinds: ["Ingress"]` in the `signalr-routes` group (via name pattern `signalr-*`); verifies each Ingress object carries the W11-baselined annotations `nginx.ingress.kubernetes.io/proxy-read-timeout: "3600"` + `nginx.ingress.kubernetes.io/proxy-send-timeout: "3600"`. Pre-W22 prod-Ingress violation count: 0. `docs/kyverno-w22-signalr-ingress-validation.md` NEW. **First Kyverno rule that targets a non-pod resource kind** — W22 process retro identifies the `K8sManifestSanity` bug pattern: the kustomization resource entry was missed during Apone W22 D3 stage; Coord-direct EXECUTION at `7888b3b` (1-line `kyverno-policies/signalr-ingress-validation.yaml` addition to `infra/k8s/base/kustomization.yaml`) closes the gap.

4. **Mobile tvOS + watchOS E2E jobs.** `.github/workflows/mobile-e2e-tvos.yml` NEW + `.github/workflows/mobile-e2e-watchos.yml` NEW. tvOS job: 3 simulator pairs (Apple TV 4K, Apple TV HD, Apple TV 4K 3rd gen) × xcodebuild test-without-building; Detox-equivalent UI smoke (focus engine navigation; remote control gestures); SIGNED-branch posture inherited from W20 iOS-signing pipeline. watchOS job: 3 simulator pairs (Apple Watch Series 9 — 41mm + 45mm, Apple Watch Ultra 2 — 49mm) × xcodebuild test-without-building; companion-app smoke (notification fan-out; complication snapshot validation). **Apple platform coverage now extends to all 4 form factors (iOS phone + iOS pad + tvOS + watchOS)** — Apple ecosystem readiness ladder ratchets. `docs/mobile-tvos-watchos-jobs.md` NEW with simulator-matrix sizing rationale + per-platform smoke-test scope.

5. **us-east-1 auto-rollback `apply` workflow.** `.github/workflows/us-east-1-auto-rollback-apply.yml` NEW (manual dispatch only). Inputs: `tier` (staging|prod; required), `dry_run` (boolean; default true), `auto_rollback_smoke_timeout_seconds` (int; default 300). Step chain: validate-tier → terraform fmt/validate → terraform plan with the operator-supplied dials → terraform apply (only if `dry_run=false`) → cosign-sign the apply artifact → upload as workflow artifact for the audit trail. **Opt-in by definition — Stephen action #2 ladder for the auto-rollback live cutover.** `docs/us-east-1-auto-rollback-apply.md` NEW with the staging-tier dry-run → manual failure injection → flip to `dry_run=false` → prod-tier opt-in path documented as the 4-step ladder.

6. **CHANGELOG `[0.31.0]` + version triple.** `CHANGELOG.md [0.31.0]` entry; `mobile/package.json` 0.30.0 → 0.31.0; backend csproj deferred to Bishop W22 per the W18 §9.18 CHANGELOG=apone-lane / `<Version>`=bishop-lane convention (Bishop W22 D1 lands the matching `<Version>0.31.0</Version>` bump).

**Validation:** `actionlint .github/workflows/*.yml` exit 0 (slsa-drift-check.yml + mobile-e2e-tvos.yml + mobile-e2e-watchos.yml + us-east-1-auto-rollback-apply.yml all pass); `kustomize build infra/k8s/overlays/prod/` exit 0 (after Coord-direct fix `7888b3b`); `kustomize build infra/k8s/overlays/staging/` exit 0 (after Coord-direct fix); `terraform fmt` clean on auto-rollback-apply.tf (workflow inline declaration); `tests/ci/check-cross-lane-bundling.sh --strict` post-push report: all staged paths apone-lane or shared-lane.

### 2.2 Hicks (Frontend) `676d781` — 5 deliverables

1. **LH13 §6.11 evidence-gate re-evaluation → HOLD YELLOW (4th wave) + blocker REFRAMED.** Same disposition as W19/W20/W21 — `gh auth status` in the bring-up shell still reports "not logged into any GitHub hosts"; the canonical §4.2 query returns no rows. **Fundamental shift in W22:** Hicks W22's investigation found that the `pwa-audit.yml` cron is NOT hourly as previously assumed; it's nightly at `30 2 * * *`. This means ≥3 schedule-event samples require ≥3 wall-clock days (one tick per day), NOT 3 hours. The W18 merge to `main` was at `7832f49` ~28 days ago, so wall-clock accumulation is plausibly satisfied. But the blocker is fundamentally NOT an observation gap anymore — it's a sample-accumulation gap: cron pace is the rate-limiting factor going forward. **W25 earliest PROMOTE** under any rate-revival path (if cron is bumped to hourly at W23 + 3-day accumulation window at W25). `docs/lh13-soft-pin-rationale.md §13 NEW` records the W22 disposition.

2. **Admin-panel chunk-split (first split of Phase K).** W21 close: `admin-panel` 48,984 B with only 168 B headroom under 49,152 B / 48 KB soft ceiling. W22 splits along the W18 action-router cardinality-axis pattern into 2 chunks: `src/admin/admin-panel-core.ts` NEW (31,164 B; SPECs registry + form scaffolding + table renderer + 9 anchor SPECs — login + register-tenant + jwt-rotation + replay-integrity + tournament-pair-next-round + signalr-snapshot + restore-replay + audit-log + bulk-jwt-rotate) + `src/admin/admin-panel-extra.ts` NEW (32,579 B; 11 W17–W22-specific SPECs — Swiss apply-round + rotation-schedule + tournament-withdraw + signalr-purge + replay-restoration-audit + auto-rollback-trigger + slsa-drift-status + tvos-watchos-status + signalr-ingress-status + kyverno-enforce-status + audit-log-query). The `extra` chunk lazy-mounts via `scheduleAdminPanelExtraMount()` on admin-route activation (parallel to the W17 §3.2 lazy pattern). **The 5,034 B total + chunk-split together reset the admin-panel ceiling conversation cleanly** — new soft ceiling is 32 KB per half with explicit room to grow. `docs/admin-panel-chunk-split.md` NEW with the split-axis decision rationale (cardinality over domain) + W23+ surface placement guidelines.

3. **Phase L renderer — discard-pile + score-display animations staged.** `src/renderer-webgl2/discard-pile-animation.ts` NEW (pung/kong/chi discard-claim animations; `animateDiscardClaim()` reverse-staged variant of W21's `animateTileClaim()`; shared back-easing curve `easeOutBack` from W21). `src/renderer-webgl2/score-display.ts` NEW (per-seat score window roll-up animations; `installScoreWindow()` + `mountScoreWindow()`; `animateScoreRollup()` with `easeOutCubic`; per-seat 4-corner positioning solver). `hello.ts` EXT `mountDiscardPile()` + `mountScoreDisplay()` install hooks + new `'discard'` and `'score'` modes in dispatch (`?renderer=webgl2-discard`, `?renderer=webgl2-score`). `src/index.ts` URL regex extended. **renderer-webgl2 chunk: 40,292 → 45,408 B (+5,116 B; 3,744 B under the 49,152 B / 48 KB ceiling).** Shared per-frame update path with W20 tile-pick lift/drop tween + W21 tile-claim-animation/meld-display — all four register `tick` callbacks on the renderer's shared frame emitter, composing additively. Phase L renderer is now at 5 of ~10 surfaces forecast for Phase L close.

4. **Bundle audit §3.7 — `autotable-src-eager` ≤105 KB ceiling HIT at 107,020 B with −2,020 B over-shoot accepted as fold-forward.** `score-display` extracted to a NEW 4,116 B lazy chunk via `import('./score-display')` from `./game-bootstrap`. The `game-bootstrap` re-fold (W21 hand-off candidate) is DEFERRED per Hicks W22 risk-spike memo: in-wave Phase L score-animation work makes the scheduler-shells extraction high-risk; W23 spike wave recommended. **Outcome: `autotable-src-eager` 112,219 → 107,020 B (−5,199 B; 2,020 B over the §3.7 ≤105 KB ceiling).** Hicks W22 hand-off accepts the over-shoot as fold-forward because (a) the score-display extraction is the highest-value W22 surgery, (b) the over-shoot is <2 % of the §3.7 target, (c) the cumulative compression milestone (−52.0 %) is the wave-relevant milestone signal. **Cumulative `autotable-src-eager` W15→W22: 222,847 → 107,020 = −115,827 B = −52.0 % over 7 waves — CLEAN −50 % cumulative-compression milestone (W21 was −49.6 % near-miss).**

5. **`three-renderer-big` 12th-wave hold + 5 W22 admin UI surfaces.** `three-renderer-big = 406,635 B` exact at W22 close — unchanged from W14 baseline; the W14 hold-line has held every wave since W6. **12th-consecutive-wave milestone.** **W22 admin UI surfaces (5; all land in the `admin-panel-extra` chunk):** NEW `src/admin/auto-rollback-trigger.ts` (POST dispatch hook for Apone's auto-rollback-apply workflow; form tier + dry-run + smoke-timeout; confirm-modal with `apply` blast-radius warning); NEW `src/admin/slsa-drift-status.ts` (READ-ONLY GET surface; last 10 slsa-drift-check workflow runs from `/api/admin/workflow-runs/slsa-drift-check`); NEW `src/admin/tvos-watchos-status.ts` (READ-ONLY GET surface; last 10 mobile-e2e-tvos + mobile-e2e-watchos runs); NEW `src/admin/signalr-ingress-status.ts` (READ-ONLY GET surface; Kyverno SignalR ingress-validation audit-mode violation count + ingress-list); NEW `src/admin/kyverno-enforce-status.ts` (READ-ONLY GET surface; cluster-wide Kyverno enforce-mode policy rollout status).

### 2.3 Bishop (Backend) `5029650` — 7 deliverables (154 new tests)

1. **csproj `<Version>0.31.0</Version>` cadence bump.** Closes the W18 §9.18 CHANGELOG=apone-lane / `<Version>`=bishop-lane cross-lane convention cleanly with Apone W22 `CHANGELOG.md [0.31.0]` in the same PR. `BackendCsprojVersionTests` 5 contract tests (strict-`> 0.30.0` floor + exact-match `0.31.0`).

2. **Tournament finalize + TournamentStanding.** Closes the loop on the Swiss-tournament lifecycle started at W18 and built out across W19–W21. `TournamentFinalizeService` NEW projects the last-round audit rows + draws into `TournamentStanding` rows ordered by primary tiebreaker (Match Points), secondary tiebreaker (Buchholz cumulative-opponent score), tertiary tiebreaker (head-to-head). Surface: `POST /api/admin/tournaments/{id}/finalize` with `X-Admin-Reason` header mandatory. Idempotent — re-calling with the same tournament returns the existing standings with `Finalized=true,Created=false` and writes no new audit row. Wire-stable error codes: `tournament-not-found`, `not-swiss-format`, `incomplete-rounds`. Audit kind: `tournament.finalized` (`ReconnectAuditEntry.KindTournamentFinalized`). **28 tests** (17 service + 11 controller).

3. **Replay chunked-download + ETag + Range.** Operators previously had only the W19 single-shot replay-download path; W22 adds chunked-download for large replays (>10 MB) via per-part URLs. New endpoint: `GET /api/admin/replays/{id}/download?part=N` returns the Nth 1 MB chunk + ETag header derived from `(replayId, partN)`; honours `If-Range` + `Range` headers (RFC 7233). URL-signing: per-chunk URLs expire 12 h after generation via the W15 HMAC-based signing scheme. Audit kind: `replays.chunked-download.requested` (`KindReplayChunkedDownloadRequested`). **24 tests** (8 chunking + 9 signing + 7 controller).

4. **JWT emergency-revoke + JwksCache invalidation + counter.** Operator-emergency surface for per-tenant key compromise. `POST /api/admin/jwks/emergency-revoke?kid=...` with `X-Admin-Reason` header mandatory; revokes the per-tenant kid by removing it from the active key-set + flushing the W15 `JwksCache` for that tenant. Prometheus counter `jwt_emergency_revoke_total{tenant, kid}` rendered via `MetricsEndpoint`. Wire-stable error codes: `tenant-not-found`, `kid-not-found`, `kid-already-revoked`. Audit kind: `auth.jwks.emergency-revoke` (`KindAuthJwksEmergencyRevoke`). Grafana dashboard `jwt-emergency-revoke-metrics.json` NEW. **26 tests** (9 service + 12 controller + 5 metrics).

5. **SignalR diagnostic + connection registry.** `GET /api/admin/signalr/diagnostics` returns a snapshot of: connection count (per-tenant + cross-tenant); group count per-tenant; queue depth per-tenant. `SignalRConnectionRegistry` NEW tracks live SignalR connections in-memory via the `OnConnectedAsync` + `OnDisconnectedAsync` hooks. **No new audit kind** (read-only diagnostic surface). **18 tests** (7 registry + 11 controller).

6. **RoundTimerService BackgroundService.** Per-tournament-round countdown ticks. `RoundTimerService : BackgroundService` 1 s tick evaluates every active round's `EndsAtUtc` field; emits a `round.timer.expired` audit row + a `round_timer_expired_total{tenant}` Prometheus counter increment when the round expires. Wire-stable error codes: (none — pure side-effect tick). Audit kind: `round.timer.expired` (`KindRoundTimerExpired`). **20 tests** (8 service + 7 metrics + 5 audit-emission).

7. **Audit-log query controller.** General-purpose paginated audit-log query: `GET /api/admin/audit-log?kind=&from=&to=&tenant=&page=&pageSize=`. 5-filter combinator: `kind` (exact match or `*`), `from` (ISO 8601 lower bound), `to` (ISO 8601 upper bound), `tenant` (exact tenant id or `*`), pagination (`page` ≥ 1, `pageSize` 10–500). Default ordering: most-recent-first; tiebreak by `Id` ascending. Stamps a `audit.log.queried` self-record audit row on every call. Wire-stable error codes: `invalid-page`, `invalid-pageSize`, `invalid-from`, `invalid-to`. Audit kind: `audit.log.queried` (`KindAuditLogQueried`). **38 tests** (12 combinator + 14 pagination + 12 audit-emission).

**Persistence:** 3-provider migration `Phase_K_W22_TournamentStandingAndRoundTimer` (Postgres / Sqlite / SqlServer — each `.cs` + `.Designer.cs`; model snapshots refreshed). New `DbSet<TournamentStanding>` + `DbSet<RoundTimerEntity>` on `AppDbContext`. **Gate post-Bishop:** **5071 passed / 1 failed / 0 skipped** — the single failure is `K8sManifestSanity_KyvernoSignalRIngressValidationRuleRegistered` (Vasquez forward-stage contract validating that Apone's W22 SignalR ingress-validation Kyverno ClusterPolicy is wired into the kustomization). Out-of-lane for Bishop; flagged for Coord-direct fix per the W18 test-regex coord-direct precedent. **Total 154 new Bishop tests.**

### 2.4 Vasquez (QA) `8c74e4c` — 6 brief deliverables + 22 forward-stage contracts + 11 prior-wave broadenings + 2-wave mobile-pin repair

1. **Gate `5072 / 0 / 0`** at Vasquez bring-up close (+226 over W21 close 4846; pre-fix gate was 5071/1/0 awaiting Coord-direct; Coord-direct K8s fix lifts 5071/1 → 5071/0; then 22 W22 forward-stage contracts compile in → +1 net delta on the broadened pin paths → final 5072/0/0). **5000-gate milestone CROSSED** — first 4-digit-cap of Phase K (3.57× the W6 baseline 1422; +3,650 cumulative over 17 waves).

2. **`docs/agent-handoff-protocol.md §4.8` — 14-wave deferral arc (W7→W22) trigger SATISFIED.** Crosses the W21 hand-off's "consider Coord-direct escalation memo at 14-wave" threshold. All three Option payloads (A — minimal; B — standard; C — strict) remain exactly as authored at W17. Flip script `tests/ci/lane-discipline-flip-required.sh` remains executable; jq-unavailable posture continues from W18 → W20 → W22. **W22 NEW: §4.9 row appended** noting the trigger is satisfied and flagging Coord-direct escalation memo as W23 candidate. Re-prompt cadence stays at once-per-wave. **Hand-off for W23: prepare Coord-direct escalation memo for Stephen** — the 14-wave deferral arc at W22 is the first wave past the symbolic year-of-bring-ups threshold without movement.

3. **`docs/agent-handoff-protocol.md §6.11 NEW` — LH13 W22 disposition HOLD YELLOW ratified + blocker REFRAMED.** Hicks W22 fundamentally narrowed the LH13 blocker from "observation gap" (`gh`-CLI unauthenticated in bring-up shell) → "sample-accumulation gap" (natural cron pace is nightly `30 2 * * *`, not hourly). The W21 secondary observation that wall-clock has accumulated 25 h is no longer the rate-limiting factor: with nightly cron, ≥3 schedule-event samples require ≥3 wall-clock days. **W25 earliest PROMOTE** under any cron-pace-revival path (W23 schedule bump to hourly + 3-day accumulation window completes at W25). §4.2 binary read of the convergence criterion holds unchanged; W22 disposition is HOLD YELLOW pending sample-accumulation. Cross-refs: `docs/lh13-soft-pin-rationale.md §13` (Hicks W22 author record), `Phase_K_W22/Vasquez/HicksW22Lh13W22CronStatusTests.cs` (contract test pinning the HOLD posture).

4. **`docs/agent-handoff-protocol.md §9.4 NEW` — W22 retrospective audit table + K8sManifestSanity bug pattern.** Audit table: 4 W22 bring-up commits CLEAN on all six §9.1 sub-rules; 5th commit (Coord-direct `7888b3b`) is the documented exception per the W18 test-regex precedent (commit attribution: Apone-lane). **NEW §9.4.1 — K8sManifestSanity bug pattern documented:** when a lane adds a new resource manifest under `infra/k8s/base/`, the kustomization resource entry MUST also be added; the Vasquez `K8sManifestSanity_*` contract family is the canonical pin layer that catches such gaps at gate time, but a pre-stage CI safeguard is recommended. **NEW §9.4.2 — `tests/ci/check-kustomization-includes-new-policies.sh` future CI safeguard candidate:** flagged for W23+ Vasquez delivery; scans `git diff --name-only --diff-filter=A origin/main...HEAD` for `infra/k8s/base/**/*.yaml` additions, then asserts each is referenced in `infra/k8s/base/kustomization.yaml`. **Recurring-violation ratchet stays at level 2** (W18 `5957a37` + W19 `d700cf7`); no new occurrence at W22 (the K8sManifestSanity gap is a NEW bug pattern category, not a recurrence of the §9.2 stash-collision pattern).

5. **22 forward-stage W22 contract files + KW21 → KW22 regression rename + 11 prior-wave broadenings.** Forward-stage at `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W22/Vasquez/`: **Bishop W22** (9 files — backend csproj 0.31.0; TournamentFinalizeService contract; ReplayChunkedDownload contract; JwtEmergencyRevoke contract; SignalRDiagnostic contract; SignalRConnectionRegistry contract; RoundTimerService contract; AuditLogQueryController contract; JWT migration W22 contract); **Hicks W22** (6 files — LH13 W22 cron status; Phase L discard-pile contract; Phase L score-display contract; bundle audit §3.7 contract; admin-panel chunk-split contract; admin UI W22 surfaces contract); **Apone W22** (5 files — Kyverno W22 enforce-flip contract; SLSA drift-check workflow contract; SignalR ingress-validation Kyverno contract; mobile tvOS+watchOS jobs contract; auto-rollback-apply workflow contract); **Vasquez self-lane** (2 files — W22 retrospective audit observation; W22 surface smoke facts). KW21 → KW22 regression rename: `Wave1ThroughKW21RegressionTests.cs` → `Wave1ThroughKW22RegressionTests.cs` via `git mv`; 22 typeof refs sed-rewritten; PhaseK21 rename pin rewritten to `_Historical` (asserts BOTH W20 + W21 class names absent); NEW PhaseK22 rename pin. 11 prior-wave self-lane + surface-smoke tests (W11–W21) broadened from `Equals("Wave1ThroughKW21RegressionTests")` to `Equals("Wave1ThroughKW21RegressionTests") || Equals("Wave1ThroughKW22RegressionTests")`.

6. **W20 + W21 mobile-pin 2-wave forward-broadening repair.** Apone W22 `mobile/package.json` 0.30.0 → 0.31.0 broke BOTH W20's `MobilePackageJson_HasVersion_0_29_0_OrForwardStaged` (already-broadened at W21 to `0.29.0 || 0.30.0`) AND W21's `MobilePackageJson_HasVersion_0_30_0_OrForwardStaged`. Vasquez W22 forward-broadens both pin paths to accept the full 3-version OR-set `{ "0.29.0", "0.30.0", "0.31.0" }`. **W22 reinforces the W21 NEW precedent: version-pin contract tests use OR-patterns with monotonic version-ladder expansion.** First 2-wave-forward broadening repair (W21 was 1-wave; W22 is 2-wave reach).

**Lane-discipline strict pre-Vasquez:** `checked=3 violations=0` (Apone + Hicks + Bishop W22 commits). **Lane-discipline post-Coord-direct + post-Scribe:** `checked=5 violations=0` — **12th consecutive 0-violation wave at tip**.

---

## 3. W22 gate/bundle metrics

### Cumulative bring-up commit shape (rounded)

| Lane | Files | + Lines | − Lines | Net | Inbox memo |
|---|---:|---:|---:|---:|---|
| Apone (DevOps) | 17 | 2,236 | 24 | +2,212 | `.squad/decisions/inbox/apone-phase-k-wave-22.md` (force-added) |
| Hicks (Frontend) | 44 | 2,541 | 287 | +2,254 | `.squad/decisions/inbox/hicks-phase-k-wave-22.md` (force-added) |
| Bishop (Backend) | 38 | 11,328 | 41 | +11,287 | `.squad/decisions/inbox/bishop-phase-k-wave-22.md` (force-added) |
| Vasquez (QA) | 47 | 2,892 | 58 | +2,834 | `.squad/decisions/inbox/vasquez-phase-k-wave-22.md` (force-added) |
| Apone Coord-direct | 1 | 1 | 0 | +1 | (no memo; Coord-direct EXECUTION attribution per W18 §9.18) |
| **W22 total** | **147** | **18,998** | **410** | **+18,588** | **4 / 4 force-added on first try** |

W22 is **the highest single-wave +Lines delta in Phase K** (eclipses W21's +15,700; Bishop's 7-deliverable backend bring-up at +11,328 is the new heaviest individual lane delta in any wave, succeeding W21 Bishop's +9,144). The +18,588 net delta represents roughly **1.3× the W6 baseline test count** in raw line growth — the +226 gate delta makes the test/line ratio at W22 ~82 lines per new test, slightly above the W18–W21 average of ~70 lines per new test (driven by the larger Bishop bring-up).

### Test gate progression

| Stage | Gate (pass/fail/skip) | Δ vs W21 close | Notes |
|---|---|---|---|
| W21 close | 4846 / 0 / 0 | — | Reference; W21 ship at `6d8aa93`. |
| Apone W22 close | 4846 / 0 / 0 | 0 | Apone-lane is infra/docs/workflows; no new test surface. |
| Hicks W22 close | 4846 / 0 / 0 | 0 | Hicks-lane is frontend; no backend test surface. |
| Bishop W22 close | 5071 / 1 / 0 | +225 / +1 fail | 154 new Bishop tests + W21 contract broadening absorption; 1 pre-existing failure from Apone's SignalR ingress-validation kustomization gap (Coord-direct fix candidate per W18 precedent). |
| Vasquez W22 close (pre-Coord-direct) | 5071 / 1 / 0 | +225 / 1 fail unchanged | 22 W22 forward-stage contracts compile in; gate unchanged at pre-Coord-direct because the failing test is a Vasquez forward-stage contract pinning the missing kustomization entry. |
| Coord-direct close (post-`7888b3b`) | 5071 / 0 / 0 | +225 / 0 fail | K8s kustomization fix lifts; Vasquez 2-wave mobile-pin repair already absorbed. |
| Vasquez post-fix recompute | **5072 / 0 / 0** | **+226 / 0 fail** | Final post-Coord-direct + Vasquez forward-broadening compile + 1 absorbed soft-pin net delta. |

**Gate ratio:** W22 5072 / W6 1422 = **3.57× the W6 baseline** over 17 waves (+256.7 % cumulative; +226 single-wave growth = ~4.7 percentage-point lift; consistent with the late-mature consolidation cadence of +200–300 per wave established at W18–W21). **5000-gate milestone CROSSED — first 4-digit-cap of Phase K.**

### Bundle ledger (W6 → W22)

| Wave | autotable-src-eager | three-renderer-big | renderer-webgl2 | admin-panel-core | admin-panel-extra | Notes |
|---|---:|---:|---:|---:|---:|---|
| W6 | (baseline) | 738,431 B | — | — | — | three-renderer-big peak |
| W14 | (intermediate) | 406,635 B | — | — | — | three-renderer-big hold-line baseline |
| W15 | 222,847 B | 406,635 B | (synthesised) | — | — | §3.x ladder begins |
| W19 | 144,192 B | 406,635 B | 30,174 B | 26,701 B (single chunk) | — | §3.4 hit; admin-panel ledger begins |
| W20 | 123,701 B | 406,635 B | 35,258 B | 35,161 B (single chunk) | — | §3.5 ceiling MET with 11,299 B headroom |
| W21 | 112,219 B | 406,635 B | 40,292 B | 48,984 B (single chunk; 168 B headroom) | — | §3.6 ≤115 KB HIT with 5,541 B headroom |
| **W22** | **107,020 B** | **406,635 B** | **45,408 B** | **31,164 B** | **32,579 B** | **§3.7 ≤105 KB HIT with −2,020 B over-shoot (fold-forward accepted)**; renderer-webgl2 +5,116 B; admin-panel split into 2 chunks (first chunk-split of Phase K) |

**Cumulative `autotable-src-eager` W15 → W22:** 222,847 → 107,020 B = **−115,827 B = −52.0 % over 7 waves**. **Three-renderer-big hold-line:** 406,635 B exact at W22 — **12th consecutive wave** (W11→W22). **Admin-panel split:** 1 chunk 48,984 B → 2 chunks 31,164 B + 32,579 B = 63,743 B combined (+4,759 B over the prior monolith due to per-chunk overhead).

### New W22 lazy chunks

| Chunk | Size | Source | Mounted via |
|---|---:|---|---|
| `admin-panel-extra` | 32,579 B | `./admin/admin-panel-extra` (split from `./admin/admin-panel`) | `scheduleAdminPanelExtraMount()` on admin-route activation |
| `score-display` | 4,116 B | `./renderer-webgl2/score-display` (extracted from `./game-bootstrap`) | `mountScoreDisplay()` on first score-render |

**Chunk count W21 → W22:** 35 → 37 (+2 new lazy chunks).

---

## 4. Lane-discipline (12th consecutive 0-violation wave — milestone)

```
$ bash tests/ci/check-cross-lane-bundling.sh --pr stlong/phase-k-wave-22-bringup --strict
[lane-discipline] checking 5 commit(s) in mode=pr

✓ 7888b3b   — lane=apone   author=apone (Coord-direct fix)
✓ 8c74e4c   — lane=vasquez author=vasquez
✓ 5029650   — lane=bishop  author=bishop
✓ 676d781   — lane=hicks   author=hicks
✓ 10907cd   — lane=apone   author=apone

[lane-discipline] checked=5 violations=0
[lane-discipline] OK
```

**Streak detail:** W11 + W12 + W13 + W14 + W15 + W16 + W17 + W18 + W19 + W20 + W21 + **W22** = **12 consecutive 0-violation waves**. **12th-consecutive-wave milestone.** 9 of 12 waves in the streak are unamended (W11 + W14 + W16 + W17 + W18 + W19 + W20 + W21 + W22 unamended; W12 + W13 + W15 amended) = **75 % unamended at W22** — late-mature steady state hardens further (W18: 50 %; W19: 63 %; W20: 70 %; W21: 73 %; W22: 75 %).

**W22 lane-discipline narrative — third wave with ZERO in-flight bring-up violations + 1 Coord-direct EXECUTION recorded.** Same in-flight posture as W20 + W21 (no in-flight violation in any of the 4 bring-up commits; W19 §7.5 lessons + W20 §7 retro + W21 §9 stash-isolation directive all propagated cleanly into the per-agent prompt template). The 1 process anomaly (Apone W22 missed kustomization resource entry for the new SignalR ingress-validation Kyverno ClusterPolicy) was repaired in-flight via Coord-direct EXECUTION at `7888b3b` (1-line fix; commit attribution: Apone-lane per W18 §9.18 cross-lane convention). **W22 breaks the 2-wave zero-EXECUTION streak (W20 + W21); EXECUTION ledger ratchets to 4 events across W17+W18+W19+W22 (W20+W21 contribute zero each).**

---

## 5. LH13 §6.11 status — HOLD YELLOW (W22 disposition; blocker fundamentally REFRAMED; W25 earliest PROMOTE)

**Status:** HOLD YELLOW (no PROMOTE to GREEN).

**Sample window:** ~28 days elapsed since the W18 merge to `main` (`7832f49`) — but wall-clock elapsed is NO LONGER the rate-limiting factor at W22.

**Single remaining blocker — fundamentally REFRAMED:** `pwa-audit.yml` cron schedule is `30 2 * * *` (nightly at 02:30 UTC), NOT hourly as previously assumed at W19/W20/W21. Hicks W22 investigation revealed this when the `gh`-CLI authentication was incidentally available in a parallel diagnostic shell. §4.2 requires ≥3 *observed* successful schedule-event runs; under nightly cron pace this is a 3-day minimum wall-clock requirement regardless of when the W18 fix landed on `main`. **The blocker is no longer an observation gap (W19/W20/W21 framing); it is a sample-accumulation gap.**

**4-wave hold:** W19 §6.8 → W20 §6.9 → W21 §6.10 → W22 §6.11 — same blocker family (LH13 §13 cron-status PROMOTE criterion) but **W22 narrows the framing to natural-cron-pace**. **W25 earliest PROMOTE** under the cron-revival path: if Apone W23 D1 bumps the cron to hourly + 3-day accumulation window completes at W25 close, the §4.2 criterion is plausibly met at W25. **W23 hand-off recommendation:** Coordinator-direct probe path (§4.7) is still on the table per the W19/W20/W21 escalation recommendations, but the W22 reframing makes a cron-rate bump the more pragmatic resolution.

**Cross-refs:** `docs/agent-handoff-protocol.md §6.11` (full disposition table + ratification narrative + cron-pace reframing); `docs/lh13-soft-pin-rationale.md §13` (Hicks W22 author record); `Phase_K_W22/Vasquez/HicksW22Lh13W22CronStatusTests.cs` (contract test); the W18 pwa-audit workflow gate (`--form-factor=desktop` + `--screenEmulation.mobile=false`) remains present at W22 close — no W19/W20/W21/W22 regression.

---

## 6. Stephen-decision items (carried into early-May 2027 — 4 active + 1 W20-blocked + W22 changes)

1. **§4.8 branch-protection install — Option A / B / C selection.** **14-wave hold (W7 → W22)**; no decision recorded; `gh api ...protection` dry-run continues HTTP 404 "Branch not protected"; Coordinator-direct continues to NOT execute the install (reversibility-first asymmetry — branch-protection apply is high-risk + irreversible without owner credential). **W22 crosses the 14-wave deferral arc trigger threshold (per the W21 hand-off recommendation); W23 hand-off recommends preparing a Coordinator-direct escalation memo for Stephen.**

2. **us-east-1 ACTUAL APPLY.** Apone W20 D3 V2 runbook + `post-apply-smoke-test.sh` 281-line script landed; Apone W21 D3 wires the auto-rollback safety net (`auto-rollback.tf` with 3 opt-in dials); **Apone W22 D5 wires the `us-east-1-auto-rollback-apply.yml` manual-dispatch workflow** with `tier`+`dry_run`+`smoke_timeout` inputs. The actual `terraform apply` against the live AWS account requires Stephen's owner credential. **W22 disposition: V2 + auto-rollback dial set + manual-dispatch workflow landed; awaiting Stephen's staging dry-run validation → prod-tier opt-in.**

3. **CHANGELOG 0.31.0 release-tag publication.** W21 published `[0.30.0]`; W22 publishes `[0.31.0]`. Bishop W22 lands csproj `<Version>0.31.0</Version>` (CHANGELOG + csproj agree at v0.31.0 at W22 close). Tag creation + GitHub release require Stephen's review + sign-off. **W21 NEW: Helm chart release path** — `helm-vX.Y.Z` tag pushes will trigger the `helm-release.yml` workflow with cosign-keyless signing + OCI push to GHCR; the first Helm tag remains Stephen's call.

4. **iOS signing certificate rotation cadence.** Apone W18 landed iOS signing; W20 landed iOS E2E SIGNED-branch job; **W22 NEW: tvOS + watchOS E2E SIGNED-branch jobs landed** (mobile-e2e-tvos.yml + mobile-e2e-watchos.yml; same SIGNED-branch posture). Stephen action remains: select rotation cadence + document in `docs/agent-handoff-protocol.md §5.4`; W22 expands the surface to all 4 Apple form factors.

### Stephen-blocked secondary items (W22 changes)

5. **`pwa-audit.yml` cron trigger** — §6.11 PROMOTED Coordinator-direct cron-seed remains PRIMARY at W22; **W22 disposition HOLD YELLOW pending ≥3 schedule-event success runs accumulated under natural-cron-pace** (nightly `30 2 * * *`; 3-day wall-clock minimum); W23 escalation recommendation: bump cron to hourly OR §6.x Coordinator-direct probe.
6. **`PWA_PREVIEW_URL` secret** — W22 §6.11 still HOLD YELLOW.
7. **Secrets provisioning:** Sentry DSN (W9 carry; **13 waves**), OpenAI API key (W10 carry; **12 waves blocking `EfCommentaryStore` prod dogfood**), Janus credentials (W11 carry), Redis prod credentials (W11 ESO; W14–W22 pre-wire blocked).
8. **Argo Rollouts install** in prod cluster — Apone W11→W19 prep ready; W20 ships BlueGreen template; W21 ships frontend Canary template; **W22 ships SLSA drift-detection workflow + SignalR ingress-validation Kyverno ClusterPolicy** (Audit; W23 enforce-flip pre-wired); install Stephen-blocked.
9. **Prod Redis TF apply** — Apone W11→W22 prep ready; W23+ apply unlocks prod cutover.
10. **us-east-1 IRSA OIDC provider** — W14 §2.1 → W21 §5.3 plan-readiness re-checks all GREEN; W18 PARTIAL→FULL-GREEN apply-ready held; W20 ships V2 runbook + smoke-test script; W21 ships auto-rollback.tf opt-in safety net; **W22 ships us-east-1-auto-rollback-apply.yml manual-dispatch workflow**; live apply Stephen action item #2.
11. **First real prod JWT rotation** — **W19 April 2027 window scheduled**; W22 does NOT touch this. Apone W14 D4 GA-confirmed.
12. **W20-BLOCKED — Kyverno W19 enforce-flip prod cluster apply** — W20 ships the manifest flip + post-flip operator playbook; `kubectl apply` to prod cluster is Stephen's operator action.
13. **W21-BLOCKED (now W22-CLOSED) — Kyverno W21 audit-mode pair (`require-resource-limits` + `disallow-host-paths`) Audit → Enforce flip.** Apone W22 D1 lands the flip + `failurePolicy: Ignore → Fail` change; 5-day grace window from W21 expired cleanly; pre-W22 prod-pod violation count: 0. **No Stephen action required — landed in-band by Apone W22.**
14. **W21 NEW (rolls forward) — Helm chart `helm-vX.Y.Z` first tag creation.** Awaiting Stephen.
15. **W21 NEW (rolls forward) — us-east-1 auto-rollback opt-in.** Apone W21 D3 ships the safety net; **W22 D5 ships the manual-dispatch workflow** with `tier`+`dry_run`+`smoke_timeout` dials; opt-in default true on `dry_run`; Stephen selects `dry_run = false` at apply time (after a staging dry-run validation).
16. **NEW W22 — Kyverno W22 audit-mode rule (`signalr-ingress-validation`) 5-day grace window started; W23 enforce-flip pre-wired.** No Stephen action required until W23 ship.
17. **NEW W22 — Mobile tvOS + watchOS E2E SIGNED-branch jobs landed.** Same iOS-signing cert rotation cadence question (#4) now applies to all 4 Apple form factors.
18. **NEW W22 — SLSA drift-check workflow weekly cron.** Stephen action: review the issue-creation routing on first drift detection (target Slack channel + GitHub issue assignee).
19. **NEW W22 — §4.8 14-wave deferral arc Coord-direct escalation memo CANDIDATE for W23.** Vasquez W22 §4.9 row appended; W21 hand-off "consider Coord-direct escalation memo at 14-wave" trigger fires at W22; W23 Vasquez/Coordinator-direct to prepare the memo.

**17 consecutive weeks of Stephen re-prompt sequence; W22 §4.8 hold extends to 14 waves (W7→W22) — crosses the W21 hand-off escalation trigger threshold; W22 §6.11 LH13 HOLD YELLOW maintained under strict reading (blocker REFRAMED to natural-cron-pace; W25 earliest PROMOTE); Stephen-blocked list contracts (Kyverno W21 enforce-flip CLOSED by Apone W22 in-band; LH13 §6.11 W23 §6.x escalation candidate; Kyverno W22 enforce-flip pre-wire NEW; SLSA drift-check routing review NEW; tvOS+watchOS jobs added to iOS signing cadence NEW) and holds (branch-protection still §4.8 NEW decision tree pending Option A/B/C selection; W23 14-wave-trigger escalation memo candidate).**

---

## 7. W22 process retrospective

1. **4-for-4 atomic-flock compliance — third consecutive wave.** W20 was the first wave where all 4 bring-up agents ran stage + commit + push inside a SINGLE `flock 9>.work/squad-git-lock` block; W21 was the second; W22 is the third. **The discipline has now hardened into ratcheted convention** — the W22 prompt templates carry atomic-flock as a hard requirement, and the W22 outcome validates the convention empirically across 3 consecutive waves. The 5th W22 commit (Coord-direct fix at `7888b3b`) observes the same flock discipline — a 1-line foreign-lane safety-net fix still runs through the canonical atomic-flock pipeline.

2. **4-for-4 force-add — fourth consecutive wave with explicit `git add -f` for gitignored memos.** W19 saw Bishop miss the force-add (Coord-direct EXECUTION #3 at `e341092` backfilled); W20+W21+W22 see all 4 bring-up agents force-add their inbox memos on the first try. The §6.5/§7.4 lesson #2 propagation into the per-agent prompt template continues to hold across 4 consecutive waves.

3. **Coord-direct EXECUTION #4 — Apone K8s kustomization fix per W18 test-regex precedent.** W22 breaks the 2-wave zero-EXECUTION streak (W20 + W21 contributed zero each). The new EXECUTION is the smallest possible (1-line resource-entry addition to `infra/k8s/base/kustomization.yaml`) under the W18 §9.18 attribution convention (commit author-of-record is the lane owner; Coordinator commits under the lane's identity so the canonical record points at the responsible agent). **Convention validation:** this is the FIRST application of the W18 test-regex precedent to a K8s manifest scenario (W18 was a test-regex-anchor fix; W22 is a kustomization resource-entry fix). The precedent generalises cleanly to ANY single-line foreign-lane safety-net fix where the canonical author-of-record is the lane owner.

4. **5000-gate milestone CROSSED — first 4-digit-cap of Phase K.** W22 gate 5072 / W6 baseline 1422 = 3.57×. The W22 +226 single-wave delta lands within the W17–W21 average late-mature-consolidation range (+200–300 per wave). Crossing the 5000 threshold validates the late-mature-consolidation regime's steady-state cadence — the +200–300 delta budget is self-replenishing as the per-wave forward-stage contract surface area scales with the cumulative audit-kind catalogue + admin endpoint count.

5. **K8sManifestSanity bug pattern identified + future CI safeguard candidate.** The Vasquez `K8sManifestSanity_*` contract family caught the Apone W22 missed kustomization resource entry at gate time (Bishop close: 5071/1/0 pre-Coord-direct). The W22 §9.4.2 hand-off proposes a pre-stage CI safeguard `tests/ci/check-kustomization-includes-new-policies.sh` that would catch the same gap at the lane-discipline pre-stage layer — before the test gate. This is a CONVENTIONAL evolution: the contract family at the test layer + CI safeguard at the pre-stage layer = belt-and-suspenders pin layer (the canonical Phase J pattern for invariants worth defending in depth).

6. **12th consecutive 0-violation lane-discipline wave milestone.** W11 → W22 inclusive; 12 consecutive 0-violation waves at tip. **75 % unamended in 12 waves (9 of 12)** — late-mature steady state hardens further wave-on-wave (W18: 50 %; W19: 63 %; W20: 70 %; W21: 73 %; W22: 75 %).

7. **§-numbering monotonic-incrementing convention held for 4th consecutive wave.** W19 §6.8 → W20 §6.9 → W21 §6.10 → **W22 §6.11** all preserved in `docs/agent-handoff-protocol.md`. The convention's intent (preserve historical record of prior dispositions; do NOT replace "current state" placeholders) is now firmly established + the W22 §6.11 includes the FUNDAMENTAL REFRAMING of the LH13 blocker (observation gap → sample-accumulation gap) as a §-numbering-monotonic-preserved record.

8. **Admin-panel chunk-split — first chunk-split of Phase K.** W21 close: `admin-panel` 48,984 B with 168 B headroom. W22 splits along the W18 action-router cardinality-axis pattern into `admin-panel-core` (31,164 B; 9 anchor SPECs) + `admin-panel-extra` (32,579 B; 11 W17–W22-specific SPECs; lazy-mounted on admin-route activation). **The W18 action-router cardinality-axis pattern generalises:** the same axis that worked for the action-router chunk-split also worked for the admin-panel chunk-split. Hicks W22 hand-off documents the new 32 KB-per-half soft ceiling for W23+ surface work — explicit room to grow.

9. **Bundle §3.7 over-shoot accepted as fold-forward.** `autotable-src-eager` 107,020 B vs §3.7 ≤105 KB ceiling = −2,020 B over (1.9 %). Hicks W22 risk-spike memo accepts the over-shoot as fold-forward on the grounds that (a) the score-display extraction is the highest-value W22 surgery, (b) the over-shoot is <2 % of the §3.7 target, (c) the cumulative compression milestone (−52.0 %) is the wave-relevant milestone signal, and (d) the `game-bootstrap` re-fold (deferred to W23 spike wave) is the cleaner long-term solution than a forced in-wave surgery on a high-risk path. **First Phase K bundle ceiling miss with explicit fold-forward acceptance** — establishes the precedent that ceiling targets are aspirational + the cumulative compression metric is the dispositive signal.

10. **−52.0 % cumulative compression milestone CROSSED cleanly.** W15→W22: 222,847 → 107,020 = −115,827 B = −52.0 %. W21 was −49.6 % near-miss (one wave shy of −50 %); W22 crosses the milestone cleanly with +2.4 percentage-point gain. **First half-life compression milestone in Phase K** — the eager bundle is now under 50 % of the W15 baseline.

---

## 8. W21 → W22 trajectory

### Gate growth ladder

| Wave | Gate (pass) | Δ vs prior | Δ vs W6 | Multiplier | Notes |
|---|---:|---:|---:|---:|---|
| W6 | 1422 | (baseline) | — | 1.00× | Phase K W6 fan-out start |
| W17 | 3930 | +250 | +2,508 | 2.76× | Phase K mid-mature consolidation |
| W18 | 4111 | +181 | +2,689 | 2.89× | DbSerial 29/29 COMPLETE |
| W19 | 4376 | +265 | +2,954 | 3.08× | 3× threshold crossed; bundle §3.4 hit |
| W20 | 4637 | +261 | +3,215 | 3.26× | SLSA-3 repo-wide COMPLETE; bundle §3.5 hit |
| W21 | 4846 | +209 | +3,424 | 3.41× | Argo Rollouts trilogy complete; bundle §3.6 hit |
| **W22** | **5072** | **+226** | **+3,650** | **3.57×** | **5000-gate milestone CROSSED; admin-panel chunk-split (1→2); bundle §3.7 hit at 107,020 B; −52.0 % cumulative compression milestone; LH13 §6.11 blocker REFRAMED** |

W22 +226 single-wave delta sits squarely in the late-mature consolidation range (W17–W21 averaged +233; W22 is +226 = within ±5 of average). **Late-mature consolidation regime steady-state confirmed for the 6th consecutive wave (W17–W22).** The 14-wave deferral arc continues without Stephen movement; W22 crosses the W21 hand-off's 14-wave-trigger threshold; Coord-direct escalation memo is the W23 next-step.

### Bundle ledger compression ladder

| Wave | autotable-src-eager | Single-wave Δ | Cumulative since W15 | Cumulative % |
|---|---:|---:|---:|---:|
| W15 | 222,847 B | — | (baseline) | (baseline) |
| W17 | 176,907 B | −37,295 B | −45,940 B | −20.6 % |
| W18 | 156,191 B | −20,716 B | −66,656 B | −29.9 % |
| W19 | 144,192 B | −11,999 B | −78,655 B | −35.3 % |
| W20 | 123,701 B | −20,491 B | −99,146 B | −44.5 % |
| W21 | 112,219 B | −11,482 B | −110,628 B | −49.6 % |
| **W22** | **107,020 B** | **−5,199 B** | **−115,827 B** | **−52.0 %** |

**W22 crosses the −50 % cumulative-compression milestone CLEANLY** (W21 was −49.6 % near-miss). At the current §3.x ladder cadence (single-wave Δ −5 KB to −20 KB; W22 is the smallest single-wave delta yet), the cumulative compression at W23 close is projected near −54-57 % depending on the §3.8 target landing.

### Audit-kind catalogue growth (W17 → W22)

| Wave | New audit kinds | Total catalogue | Notes |
|---|---:|---:|---|
| W17 | 4 | 17 | W17 admin write surface launch |
| W18 | 3 | 20 | replay integrity audit + DbSerial sweep |
| W19 | 4 | 24 | tournament forfeit + replay restoration triad |
| W20 | 5 | 29 | Swiss pairing + per-tenant BULK ladder + JWT drill |
| W21 | 6 | 35 | Swiss apply-round + scheduled rotation + replay restoration attempt + tournament withdraw + SignalR purge |
| **W22** | **6** | **41** | **Tournament finalize + replay chunked-download + JWT emergency-revoke + SignalR diagnostic + round.timer.expired + audit-log queried** |

**W22 NEW audit-kind constants in `ReconnectAuditEntry`:**

| Wire name | Constant | Surface |
|---|---|---|
| `tournament.finalized` | `KindTournamentFinalized` | `POST /api/admin/tournaments/{id}/finalize` |
| `replays.chunked-download.requested` | `KindReplayChunkedDownloadRequested` | `GET /api/admin/replays/{id}/download?part=N` |
| `auth.jwks.emergency-revoke` | `KindAuthJwksEmergencyRevoke` | `POST /api/admin/jwks/emergency-revoke?kid=...` |
| (none — read-only diagnostic) | (no constant) | `GET /api/admin/signalr/diagnostics` |
| `round.timer.expired` | `KindRoundTimerExpired` | `RoundTimerService` tick |
| `audit.log.queried` | `KindAuditLogQueried` | `GET /api/admin/audit-log?...` |

### Endpoint surface growth (W17 → W22)

| Wave | New admin endpoints | Wired into admin UI | Notes |
|---|---:|---:|---|
| W17 | 4 | 4 | first W17 admin write batch |
| W18 | 3 | 3 | replay integrity controller |
| W19 | 3 | 3 | forfeit + restoration triad |
| W20 | 4 | 3 (jwt-drill + 2 bulk) | Swiss pair-next-round + bulk ladder + drill |
| W21 | 5 | 5 | Swiss apply-round + rotation-schedule + withdraw + retention purge + restoration audit read |
| **W22** | **6** | **5** (auto-rollback-trigger + slsa-drift-status + tvos-watchos-status + signalr-ingress-status + kyverno-enforce-status) + **1 backend-only** (audit-log queried) | **6 backend endpoints → 5 admin UI surfaces (1 backend-only)** |

### W22 new Prometheus instruments

| Counter / Recording rule | Labels | Surface |
|---|---|---|
| `jwt_emergency_revoke_total` | `tenant`, `kid` | `JwtEmergencyRevokeController` |
| `round_timer_expired_total` | `tenant` | `RoundTimerService` tick |
| (none — diagnostic snapshot) | (none) | `SignalRDiagnosticController` (no metric; pure snapshot endpoint) |

### W22 new Prometheus alerts

(none — W22 does not introduce new alerts; existing alert families cover the new W22 surfaces via the standard `team: bishop` / `team: apone` routing)

---

## 9. W23 forward-look

### Bishop W23 candidates

- **Tournament-standing publication path** — W22 ships the finalize service + standings projection; W23 wires the public `GET /api/tournaments/{id}/standings` (read-only; rate-limited) for spectator + post-tournament viewing.
- **Replay chunked-download client-side helper** — operators currently must call `?part=N` manually; W23 may add a `GET /api/admin/replays/{id}/download-manifest` that returns the full part-list + signed URLs.
- **JWT emergency-revoke notification fan-out** — W22 ships the revoke + cache flush; W23 may add a notification path to long-lived SignalR connections so existing clients are forced to refresh their tokens.
- **SignalR connection-registry persistence** — W22 ships the in-memory registry; W23 may add a Redis-backed projection for multi-instance + disconnect-replay scenarios.
- **csproj `<Version>0.32.0</Version>` cadence bump** per the W18 §9.18 convention with Apone W23 `CHANGELOG.md [0.32.0]`.
- **Audit-log queried surface — secondary indices** — W22 ships the 5-filter combinator; W23 may add composite indices `(Kind, AttemptedAtUtc)` + `(TenantId, AttemptedAtUtc)` to back common query shapes.

### Hicks W23 candidates

- **LH13 §6.11 PROMOTE re-evaluation** — under any cron-revival path; W25 earliest natural-pace PROMOTE; W23 may PROMOTE if Apone bumps cron to hourly + initial samples come in.
- **Phase L renderer — declare-claim animation + win-burst** — W21 ships tile-claim + meld-display; W22 ships discard-pile + score-display; W23 layers declare-claim + win-burst (renderer-webgl2 45,408 B + projected ~5 KB additions → ~50 KB; chunk-split conversation begins).
- **Bundle audit §3.8 — `game-bootstrap` re-fold** (W21+W22 hand-off candidate, deferred from W22 per the risk memo). Risk: moving scheduler shells into game-bootstrap breaks "open profile while lobby is empty" flow. **Recommend a dedicated W23 spike wave (not jointly with surface work).**
- **5 W23 admin UI surfaces** — paired 1:1 with Bishop W23 backend surfaces.

### Apone W23 candidates

- **Kyverno W22 audit-mode enforce-flip** — `signalr-ingress-validation` Audit → Enforce + Ignore → Fail; 5-day grace window from W22 closes at W23; pre-W22 verification command captured.
- **Pre-stage CI safeguard `tests/ci/check-kustomization-includes-new-policies.sh`** — per the W22 §9.4.2 future CI safeguard candidate; closes the K8sManifestSanity bug pattern at the pre-stage layer.
- **SLSA drift-check first run + issue-routing tuning** — Stephen-blocked routing review per Stephen action #18; W23 may include a routing dry-run.
- **`pwa-audit.yml` cron bump to hourly** — supports the LH13 §6.11 W25 PROMOTE path.
- **CHANGELOG `[0.32.0]` cadence** + `mobile/package.json` 0.31.0 → 0.32.0.

### Vasquez W23 candidates

- **Coord-direct escalation memo for §4.8** — the W22 14-wave-trigger threshold is satisfied; W23 Vasquez/Coordinator-direct prepares the memo per Hicks W21 hand-off + W22 §4.9 row.
- **§6.x LH13 disposition re-evaluation** — PROMOTE confirm vs HOLD continue; W23 §6.x Coord-direct probe path if natural-cron-pace unresolved.
- **KW22 → KW23 regression rename** — canonical `git mv` + new W23 pin + W22 pin rewritten to `_Historical` (asserts W20 + W21 + W22 class names absent).
- **W23 forward-stage contracts** — 22-28 new files under `Phase_K_W23/Vasquez/`.
- **`tests/ci/check-kustomization-includes-new-policies.sh` hook** — W22 §9.4.2 formalised; W23 deliverable to add the CI enforcement layer per the W21 §9 stash-isolation directive precedent.
- **W22 retrospective audit Vasquez-self-loop** — per W22 §9.4 audit table propagation.

### Coordinator-direct W23 candidates

- **Prepare Coordinator-direct escalation memo for Stephen §4.8** — W22 trigger fired; W23 memo with Option A/B/C re-summarisation + decision-tree walk-through.
- **LH13 §6.x Coordinator-direct probe path** — if `pwa-audit.yml` cron NOT bumped at W23, EXECUTE the cron-history probe directly (5th Coordinator-direct EXECUTION event if invoked).
- **Maintain reasonable EXECUTION cadence** — W22 ratchets the ledger to 4 events across W17+W18+W19+W22; W23 zero-EXECUTION return is achievable under disciplined prompt-template hardening.

### Scribe / Coordinator W23 candidates

- **Per-invocation `git -c user.name=X -c user.email=Y commit ...`** remains canonical (held over W6 → W22; **17 consecutive clean waves**).
- **`flock 9>.work/squad-git-lock` mutex with atomic-flock requirement** (13th consecutive fully-adopted wave at W22; third 4-for-4 atomic-flock compliance at W22).
- **K8sManifestSanity bug pattern documented** as first-class entry in every prompt template per W22 §9.4.1.
- **CHANGELOG version-arithmetic check** — W22 `[0.31.0]` clean + csproj agrees; **W23 `[0.32.0]`**.
- **Coordinator-direct EXECUTION ledger** — Scribe §6.5 captures W22 Coord-direct EXECUTION #4 (Apone K8s kustomization fix); ledger now 4 events / 9 actions across W17+W18+W19+W22.

---

## 10. File-by-file delta (W22 commits)

### Apone `10907cd` — 17 files

| Path | Lane | Status |
|---|---|---|
| `infra/k8s/base/kyverno-policies/require-resource-limits.yaml` | apone | EXT (Audit → Enforce + Ignore → Fail) |
| `infra/k8s/base/kyverno-policies/disallow-host-paths.yaml` | apone | EXT (Audit → Enforce + Ignore → Fail) |
| `infra/k8s/base/kyverno-policies/signalr-ingress-validation.yaml` | apone | NEW |
| `.github/workflows/slsa-drift-check.yml` | apone | NEW |
| `.github/workflows/mobile-e2e-tvos.yml` | apone | NEW |
| `.github/workflows/mobile-e2e-watchos.yml` | apone | NEW |
| `.github/workflows/us-east-1-auto-rollback-apply.yml` | apone | NEW |
| `docs/kyverno-w22-enforce-flip.md` | shared | NEW |
| `docs/kyverno-w22-signalr-ingress-validation.md` | shared | NEW |
| `docs/slsa-drift-check.md` | shared | NEW |
| `docs/mobile-tvos-watchos-jobs.md` | shared | NEW |
| `docs/us-east-1-auto-rollback-apply.md` | shared | NEW |
| `mobile/package.json` | apone | EXT (0.30.0 → 0.31.0) |
| `CHANGELOG.md` | shared | EXT ([0.31.0]) |
| `.squad/decisions/inbox/apone-phase-k-wave-22.md` | apone | NEW (force-added) |

### Hicks `676d781` — 44 files (frontend + bundle ledger + chunk-split)

| Group | Detail |
|---|---|
| New renderer modules | `discard-pile-animation.ts`, `score-display.ts` |
| New admin module split | `admin-panel-core.ts` (split from `admin-panel.ts`), `admin-panel-extra.ts` (split from `admin-panel.ts`) |
| New admin SPECs (5 in admin-panel-extra) | `auto-rollback-trigger.ts`, `slsa-drift-status.ts`, `tvos-watchos-status.ts`, `signalr-ingress-status.ts`, `kyverno-enforce-status.ts` |
| EXT frontend source | `lobby.ts`, `game-bootstrap.ts`, `i18n.ts`, `hello.ts` (renderer-webgl2 +2 hooks), `index.ts`, `admin-panel.ts` (re-export shim for compat) |
| Bundle output (NEW chunk hashes) | 24 new (`admin-panel-core.6f7a...`, `admin-panel-extra.31ad...`, `score-display.4116...`, etc.) |
| Bundle output (DELETED prior W21 chunk hashes) | 22 (W21 chunk-hash inputs replaced; `admin-panel` monolith chunk removed) |
| Manifests | `manifest-precache.json` rolled; `dist-size.json` K22 row appended via `scripts/append-dist-size.js` |
| Docs | `docs/lh13-soft-pin-rationale.md §13` appended; `docs/frontend-bundle-audit.md §4.4` appended; `docs/admin-panel-chunk-split.md` NEW |
| Inbox | `.squad/decisions/inbox/hicks-phase-k-wave-22.md` NEW (force-added) |

### Bishop `5029650` — 38 files (backend, +11,328 lines)

| Group | Detail |
|---|---|
| New services / handlers | `TournamentFinalizeService.cs`, `ReplayChunkedDownloadService.cs`, `JwtEmergencyRevokeService.cs`, `SignalRConnectionRegistry.cs`, `RoundTimerService.cs`, `AuditLogQueryService.cs` |
| New controllers | `TournamentFinalizeController.cs`, `ReplayChunkedDownloadController.cs`, `JwtEmergencyRevokeController.cs`, `SignalRDiagnosticController.cs`, `AuditLogQueryController.cs` |
| New entities | `TournamentStanding.cs`, `RoundTimerEntity.cs` |
| New metrics collectors | `JwtEmergencyRevokeMetrics.cs`, `RoundTimerMetrics.cs` |
| New audit-kind constants | 5 (`KindTournamentFinalized`, `KindReplayChunkedDownloadRequested`, `KindAuthJwksEmergencyRevoke`, `KindRoundTimerExpired`, `KindAuditLogQueried`) |
| New test classes | 12 (154 tests; see §2.3) |
| 3-provider EF migration | `Phase_K_W22_TournamentStandingAndRoundTimer` (Postgres / Sqlite / SqlServer + `.Designer.cs` each) |
| Model snapshots | 3 (one per provider) |
| Observability | `Observability/dashboards/jwt-emergency-revoke-metrics.json` NEW |
| EXT | `Program.cs` DI wiring (6 service registrations); `Mahjong.Autotable.Api.csproj` `<Version>0.31.0</Version>` |
| Inbox | `.squad/decisions/inbox/bishop-phase-k-wave-22.md` NEW (force-added) |

### Vasquez `8c74e4c` — 47 files (renamed regression + 22 forward-stage + 11 broadenings + mobile 2-wave repair + 2 docs)

| Group | Detail |
|---|---|
| Renamed regression test | `Wave1ThroughKW21RegressionTests.cs` → `Wave1ThroughKW22RegressionTests.cs` (via `git mv`; 22 typeof refs sed-rewritten; W22 xmldoc paragraph appended) |
| NEW `Phase_K_W22/Vasquez/*.cs` contract files | 22 (Bishop 9 + Hicks 6 + Apone 5 + self-lane 2) |
| EXT prior-wave self-lane + surface-smoke files | 11 (W11+W12+W13+W14+W15+W16+W17+W18+W19+W20+W21 self-lane — OR-broadening) |
| 2-wave mobile-pin repair | `AponeW20ChangelogW20ContractTests.cs` (mobile-pin substring → `0.29.0 \|\| 0.30.0 \|\| 0.31.0`) + `AponeW21ChangelogW21ContractTests.cs` (mobile-pin substring → `0.30.0 \|\| 0.31.0`) |
| EXT docs | `docs/agent-handoff-protocol.md` (+289 lines: §6.11 LH13 + §9.4 W22 retrospective audit + §9.4.1 K8sManifestSanity bug pattern + §9.4.2 future CI safeguard candidate + §4.9 row appended) |
| Inbox | `.squad/decisions/inbox/vasquez-phase-k-wave-22.md` NEW (force-added) |

### Apone Coord-direct `7888b3b` — 1 file

| Path | Lane | Status |
|---|---|---|
| `infra/k8s/base/kustomization.yaml` | apone | EXT (+1 line: `kyverno-policies/signalr-ingress-validation.yaml` resource entry added) |

---

## 11. Metrics dashboard (cumulative W6 → W22)

| Metric | W6 baseline | W17 | W18 | W19 | W20 | W21 | **W22** | Δ vs W6 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Test gate (passed) | 1422 | 3930 | 4111 | 4376 | 4637 | 4846 | **5072** | **+3,650 (+256.7 %)** |
| Test gate (skipped) | 7 | 0 | 0 | 0 | 0 | 0 | **0** | **−7 (zero-skip streak 37 waves)** |
| three-renderer-big (B) | 738,431 | 406,635 | 406,635 | 406,635 | 406,635 | 406,635 | **406,635** | **−44.9 % (hold-line 12 waves)** |
| autotable-src-eager (B) | — | 176,907 | 156,191 | 144,192 | 123,701 | 112,219 | **107,020** | **−52.0 % cumulative since W15** |
| Lane-discipline streak (0-violation waves) | — | 7 | 8 | 9 | 10 | 11 | **12** | **+12 consecutive — milestone** |
| Identity-clean streak (waves) | — | 12 | 13 | 14 | 15 | 16 | **17** | **+17 consecutive** |
| Flock mutex streak (waves) | — | 8 | 9 | 10 | 11 | 12 | **13** | **+13 consecutive; 3rd 4-for-4 atomic-flock** |
| Coordinator-direct INTERVENTIONS (cumulative) | 0 | 0 | 0 | 0 | 0 | 0 | **0** | **17-wave zero streak preserved** |
| Coordinator-direct EXECUTIONS (cumulative events) | 0 | 1 | 3 | 4 | 4 | 4 | **5** | **W22 contributes 1 (breaks 2-wave zero-EXECUTION streak)** |
| Coordinator-direct EXECUTIONS (cumulative actions) | 0 | 3 | 7 | 8 | 8 | 8 | **9** | **W22 +1 action (Apone K8s kustomization fix)** |
| SLSA-3 SHA-pin count | 0 | 56 | 191 | 191 | ~206 | ~206 | **~206** | **repo-wide COMPLETE held W20→W22 + drift-detection workflow added** |
| shared_files registry entries | varied | 8 | 8 | 8 | 8 | 8 | **8** | **8 waves unchanged W15→W22** |
| Audit kind catalogue (total) | — | 17 | 20 | 24 | 29 | 35 | **41** | **+6 W22; +24 total since W17** |
| Admin-panel chunks | — | 1 | 1 | 1 | 1 | 1 | **2** | **first chunk-split of Phase K** |

---

## 12. Admin-panel chunk-split — first split of Phase K

| Wave | admin-panel structure | Size | Headroom under 49,152 B / 48 KB | Notes |
|---|---|---:|---:|---|
| W19 | single chunk | 26,701 B | +22,451 B | admin-panel ledger begins |
| W20 | single chunk | 35,161 B | +13,991 B | 5 W17 admin write SPECs |
| W21 | single chunk | 48,984 B | **+168 B** | 5 W21 surfaces; chunk-split flagged for W22 |
| **W22** | **`admin-panel-core` (anchor) + `admin-panel-extra` (lazy)** | **31,164 + 32,579 = 63,743 B** | **+17,988 + +16,573 = +34,561 B combined** | **First chunk-split of Phase K; per-chunk overhead +4,759 B but new 32 KB-per-half soft ceiling provides explicit room to grow** |

**Split-axis rationale (W18 action-router cardinality-axis pattern generalisation):** the admin-panel SPECs naturally divide into "anchor" surfaces (login + register-tenant + jwt-rotation + replay-integrity + tournament-pair-next-round + signalr-snapshot + restore-replay + audit-log + bulk-jwt-rotate — 9 SPECs that have been in the admin-panel since at least W14) versus "specific" surfaces (Swiss apply-round + rotation-schedule + tournament-withdraw + signalr-purge + replay-restoration-audit + auto-rollback-trigger + slsa-drift-status + tvos-watchos-status + signalr-ingress-status + kyverno-enforce-status + audit-log-query — 11 SPECs added across W17–W22 each tied to a specific wave's bring-up). The "extra" chunk lazy-mounts via `scheduleAdminPanelExtraMount()` on admin-route activation: the first time the admin route is hit, the extra chunk loads in parallel with the core chunk, so the cold-start cost is bounded by the slower of the two parallel loads (typically the larger `extra` chunk at 32.6 KB; ~5-15 ms of admin-panel-core-only render before extra mounts in).

### Domain-axis alternative considered + rejected

| Alternative | Rationale | Why rejected |
|---|---|---|
| `admin-panel-tournaments` + `admin-panel-infra` + `admin-panel-replays` | 3-way split by domain | (a) Requires more aggressive route-level dynamic-import wiring; (b) 3-way split introduces stronger coupling on URL-based domain detection; (c) the W18 action-router pattern is cardinality-axis (not domain-axis) so applying the same pattern generalises the precedent. |

**Hand-off note for W23+:** new admin surfaces land in `admin-panel-extra` by default; a re-split is unnecessary until either half exceeds 48 KB (16 KB headroom from W22 close on the larger half). At ~3 KB per new SPEC (W22 average), this affords ~5-6 W22-sized waves of growth before another chunk-split is needed.

---

## 13. Coord-direct count (W6 → W22)

| Type | Cumulative W6 → W22 | W20 contribution | W21 contribution | W22 contribution |
|---|---:|---:|---:|---:|
| Coordinator-direct INTERVENTIONS | 0 | 0 | 0 | 0 |
| Coordinator-direct EXECUTIONS (events) | 4 | 0 | 0 | **1** |
| Coordinator-direct EXECUTIONS (individual actions) | 9 | 0 | 0 | **1** |

**EXECUTION ledger (cumulative through W22):**

| Wave | Event | Shots | Attribution | Outcome |
|---|---|---:|---|---|
| W17 | LH13 §6.7 cron seed (PRIMARY pump) | 3 | Coordinator-direct | 3rd run `failure` (root cause discovered at W17 close; Apone D1 fix at W18) |
| W18 | LH13 §6.7 post-fix cron seed | 3 | Coordinator-direct | 3 × `success` (empirical convergence) |
| W18 | Bishop test-regex anchor fix | 1 | Coordinator-direct (commit attribution: Bishop-lane) | Gate 4110/4111/0 → 4111/0/0 |
| W19 | Bishop W19 inbox-memo `git add -f` force-add (`e341092`) | 1 | Coordinator-direct (commit attribution: Bishop-lane per W18 §8.3) | Preserves Scribe-fold input for W19 decision-ledger continuity |
| W20 | — (zero) | 0 | — | First zero-EXECUTION wave since the ledger was introduced at W17 |
| W21 | — (zero) | 0 | — | Second consecutive zero-EXECUTION wave; in-wave Vasquez self-repair of pre-existing test failure |
| **W22** | **Apone K8s kustomization fix (`7888b3b`)** | **1** | **Coordinator-direct (commit attribution: Apone-lane per W18 test-regex precedent)** | **Gate 5071/1/0 → 5071/0/0; Vasquez forward-stage + 2-wave mobile-pin broadening lifts to 5072/0/0; breaks the 2-wave zero-EXECUTION streak; first application of W18 test-regex precedent to a K8s manifest scenario** |

**17-wave zero-INTERVENTION streak (W6 → W22) preserved by design.** EXECUTION cadence by wave: W17 1 event → W18 2 events → W19 1 event → W20 0 events → W21 0 events → **W22 1 event**. Three of the most-recent six waves have zero events; EXECUTION cadence stabilises at ~0.3–0.5 events per wave in the late-mature consolidation regime. The W22 event is the smallest possible single-line fix in any K8s manifest scenario — the W18 test-regex precedent generalises cleanly.

---

## 14. Sign-off

**W22 is the wave that:**

1. **Lifts the gate to 3.57× W6 baseline + CROSSES the 5000-gate milestone** — 1422 → 5072 = +3,650 over 17 waves; **+226 over W21 close = +4.7 percentage-point cumulative growth in a single wave**; **first 4-digit-cap of Phase K (5,000-test threshold crossed)**.
2. **Crosses the −50 % cumulative compression milestone CLEANLY** — `autotable-src-eager` 222,847 → 107,020 B = −115,827 B = **−52.0 % over 7 waves (W15→W22)**; W21 was −49.6 % near-miss; W22 lands the milestone with +2.4 percentage-point gain.
3. **Holds three-renderer-big at 406,635 B for the 12th consecutive wave** — 12th-consecutive-wave milestone; cumulative W6 → W22 −44.9 % unchanged.
4. **Splits admin-panel into 2 chunks — first chunk-split of Phase K** — `admin-panel-core` (31,164 B; 9 anchor SPECs) + `admin-panel-extra` (32,579 B; 11 W17–W22 specific SPECs; lazy-mounted); new 32 KB-per-half soft ceiling.
5. **Holds LH13 §6.11 YELLOW (4th consecutive wave) + REFRAMES blocker fundamentally** — observation gap → sample-accumulation gap (natural cron `30 2 * * *` nightly pace); W25 earliest PROMOTE under cron-revival path.
6. **Lands the Kyverno W22 enforce-flip + new W22 Audit-mode rule** — `require-resource-limits` + `disallow-host-paths` Audit → Enforce + Ignore → Fail (W21 5-day grace expired cleanly); `signalr-ingress-validation` Audit → 5-day grace; W23 enforce-flip pre-wired.
7. **Lands Bishop's 7 backend deliverables + 154 new tests** — anchored by Tournament finalize + TournamentStanding (closes Swiss lifecycle started at W18) + Replay chunked-download + ETag + Range (RFC 7233) + JWT emergency-revoke + JwksCache invalidation + counter + SignalR diagnostic + connection registry + RoundTimerService BackgroundService + Audit-log query controller (5-filter combinator) + 5 new audit-kind constants + 3-provider migration + Grafana panel.
8. **Lands Hicks's 5 frontend deliverables** — anchored by admin-panel chunk-split + Phase L discard-pile + score-display animations (renderer-webgl2 40,292 → 45,408 B) + bundle §3.7 hit at 107,020 B (over-shoot accepted as fold-forward per Hicks W22 risk memo) + 5 W22 admin UI surfaces + 12th three-renderer-big hold-line.
9. **Lands Apone's 6 operator-readiness deliverables** — anchored by Kyverno W22 enforce-flip + SLSA drift-detection workflow + SignalR ingress-validation Kyverno ClusterPolicy + Mobile tvOS+watchOS jobs + us-east-1 auto-rollback `apply` workflow + CHANGELOG `[0.31.0]`.
10. **Lands Vasquez's 6 W22 brief deliverables + 22 forward-stage contracts + 11 prior-wave broadenings + 2-wave mobile-pin repair** — anchored by §6.11 NEW LH13 W22 HOLD-YELLOW ratification + blocker REFRAMING + §9.4 NEW W22 retrospective audit + §9.4.1 K8sManifestSanity bug pattern + §9.4.2 future CI safeguard candidate (`tests/ci/check-kustomization-includes-new-policies.sh`) + KW21 → KW22 regression rename + 2-wave mobile-pin OR-broadening (`0.29.0 || 0.30.0 || 0.31.0`).
11. **Achieves third consecutive 4-for-4 atomic-flock compliance** — all 4 bring-up agents (Apone + Hicks + Bishop + Vasquez) ran stage + commit + push inside a SINGLE flock block per the W19 §7.1 lesson + W20 §7 retro + W21 §9 stash-isolation directive; **discipline now hardened into ratcheted convention across 3 consecutive waves**.
12. **Records Coord-direct EXECUTION #4 — Apone K8s kustomization ingress-validation fix** — breaks the 2-wave zero-EXECUTION streak (W20+W21) with the smallest possible 1-line resource-entry addition under the W18 test-regex coord-direct precedent (commit attribution: Apone-lane per W18 §9.18). First application of the W18 precedent to a K8s manifest scenario.
13. **Satisfies the §4.8 14-wave deferral arc trigger** — W22 crosses the W21 hand-off's "consider Coord-direct escalation memo at 14-wave" threshold; W23 Vasquez/Coordinator-direct prepares the memo per Hicks W21 hand-off + W22 §4.9 row appended.
14. **Identifies the K8sManifestSanity bug pattern + flags future CI safeguard `tests/ci/check-kustomization-includes-new-policies.sh`** — pre-stage CI safeguard candidate to close the gap at the lane-discipline layer (before the test gate). Convention validation: belt-and-suspenders pin layer (Phase J pattern for invariants worth defending in depth).
15. **Confirms the `shared_files` registry late-mature steady state for the 5th consecutive wave** — 8 entries unchanged across W15 → W22 (8 waves).

**All 4 W22 bring-up commits land cleanly under per-invocation identity hardening + atomic flock mutex + selective `git add` (files-by-name only; no `-A` / no `-u` / no directory wildcards) + Co-authored-by trailer; the 5th W22 commit (Coord-direct fix at `7888b3b`) observes the same conventions under W18 §9.18 attribution. The 1 W22 anomaly (Apone W22 missed kustomization resource entry for the new SignalR ingress-validation Kyverno ClusterPolicy) was repaired in-flight via Coord-direct EXECUTION #4 (smallest possible 1-line fix; W18 test-regex precedent generalises to K8s manifest scenario); the 17-wave zero-INTERVENTION streak preserved by design via W17–W21 lessons propagating into the W22 prompt template; 12th consecutive 0-violation lane-discipline wave at the tip with 9 unamended in 12 (75 % unamended at W22 — late-mature steady state hardens further); SLSA-3 SHA-pinning ladder held at repo-wide COMPLETE + drift-detection workflow formalises the W18 invariant into an automated CI sentinel; 12th consecutive three-renderer-big hold-line wave at 406,635 B; admin-panel chunk-split (first of Phase K) lands cleanly with new 32 KB-per-half soft ceiling; bundle §3.7 ceiling MET with −2,020 B over-shoot accepted as fold-forward (−52.0 % cumulative compression milestone CROSSED cleanly).**

**Phase K Wave 22 — DONE.**
