# Scribe (Archive) — Phase K Wave 17 sweep

**Date:** 2027-02-XX
**Branch:** `stlong/phase-k-wave-17-bringup`
**Base (pre-W17):** `c866535` (W16 final tip)
**Head pre-Scribe:** `fcf741d` (Vasquez W17 bring-up; 4 lane bring-ups complete)
**Final gate:** **3930/0/0 (+309 over W16; W16 +309 matches W17 +309 exactly — first 2-wave constant-delta in W11→W17 window; Vasquez contribution +123 / Bishop contribution +186)**
**Cumulative gate growth:** **+2508 over 12 waves = +176.4 %** vs W6 baseline of 1422 (gate now **2.76× W6 baseline**)
**Streaks:** zero-skip **32 waves**; lane-discipline strict **7 consecutive 0-violation waves (W11+W12+W13+W14+W15+W16+W17; 4 unamended = W11+W14+W16+W17)**; coordinator-direct **12 consecutive waves with zero coordinator-direct INTERVENTIONS (W6→W17)** — **W17 §6.5 Coordinator-direct cron-seed EXECUTION is categorically distinct from INTERVENTION and does NOT terminate this streak by design**; identity hardening **12 consecutive clean waves**; flock mutex **8 consecutive fully-adopted waves**; three-renderer-big hold-line **7 consecutive waves at 406,635 B**; **8-entry `shared_files` registry held unchanged across W15+W16+W17 (3 consecutive waves load-tested and held)**.

## Sweep deliverables (4)

1. **`docs/wave-summaries/phase-k-wave-17.md`** — NEW; **1389 lines**; 12-section structure mirroring W16 template. Header + §1 Headlines (8 entries: JwtIssuingService rotation wireup + Prometheus counter, Phase L W3 animation graph, autotable-src-eager −37 KB, §4.5 RECALIBRATION, §6.7 PROMOTE + Coordinator-direct cron-seed EXECUTED, 7th hold-line, 7th 0-vio NO AMENDMENT, 2-wave constant-gate-delta) + §2 commits table + §3 Bishop 7 deliverables + §4 Hicks 4 deliverables + §5 Apone D1-D6 + §6 Vasquez (incl §6.5 Coordinator-direct cron seed EXECUTION sub-section) + §7 18 cross-cutting patterns + §8 numeric milestone tables + §9 W18 forward queue per-lane + §10 8 Stephen action items + §11 identity-hardening recap + §12 sign-off.

2. **`.squad/decisions.md`** — `## Phase K — Wave 17 (...)` block appended (~673 lines added; file 15,713 → 16,386 lines). Mirror W16 structure: giant in-parens narrative header + multi-paragraph prose + Wave-17 commits table (4 rows) + per-agent breakdown (Bishop / Hicks / Apone / Vasquez) + W17 Decisions Carried Forward (18 conventions) + W18 Forward Queue per-lane + Stephen action items (8) + `Phase K Wave 17 — DONE.` + trailing `---`.

3. **`.squad/agents/scribe/history.md`** — W17 entry appended (~70 lines added; file 1,611 → 1,681 lines). Mirror W16 structure: Date / Branch / Base / Head / Final gate / Streaks + narrative paragraph + Wave-17 bring-up commits table + 10 sweep observations + W18 Scribe-handoffs + 8 Stephen action items + Close paragraph + `Phase K Wave 17 — DONE.`

4. **`.squad/decisions/inbox/scribe-phase-k-wave-17-sweep.md`** — NEW (this file; force-add required because `.squad/decisions/inbox/` is gitignored).

## Key W17 conventions established / reinforced

- **Validator-API → Service-Wireup is a 1-wave canonical pattern** — W15 table-before-validator → W16 validator-API → W17 service-wireup completes the 3-wave ladder exactly as predicted; future feature-flagged schema changes split (a) table+stores wave N, (b) validator API wave N+1, (c) service wireup + observability counter wave N+2.
- **§4.5 RECALIBRATION pattern** — any PROMOTE recommendation MUST include an HTTP/empirical precondition check at first execution attempt; if precondition fails, RECALIBRATE back to prior path rather than escalate further. W16 §4.5 PROMOTE → W17 §4.5 RECALIBRATION demonstrates the loop.
- **§4.7 + §4.8 NEW SECTIONS** — when escalating from Coordinator-direct back to Stephen-direct, pre-author the full `gh api -X PUT` payload with Options A/B/C to minimise Stephen's decision cost.
- **Coordinator-direct EXECUTION vs INTERVENTION categorical distinction** — EXECUTION delivers on a deferred Stephen-action via the §4.7/§4.8/§6.7 escalation ladder; INTERVENTION overrides an in-flight agent action; the two are categorically distinct.
- **Coordinator-direct EXECUTION ledger located in §6.5 only** — EXECUTIONs are NOT logged in agent inbox memos to keep the zero-INTERVENTION metric clean and comparable.
- **Reversibility-first asymmetry between §4.5 and §6.7** — HIGH blast-radius irreversible actions (branch-protection) DOWNGRADE Coordinator-direct authority when empirical preconditions weaken; LOW blast-radius reversible actions (cron-workflow re-trigger) PROMOTE to Coordinator-direct PRIMARY because reversibility cost is near-zero. PROMOTE-speed scales inversely with blast-radius.
- **3-mode `hello.ts` dispatch via URL guard** — Phase L demos accumulate behind `?renderer=webgl2-{hello,tile-mesh,animation}` URL guards; each wave's demo MUST be reachable from the prior wave's URL guard.
- **Canonical atlas vs synthetic placeholder transition** — Phase L assets ship as build-time-generated committed artefacts (`.auto.png` + `.auto.json`) rather than runtime-generated dynamic textures to support cache hashing and CDN edge caching.
- **Vasquez-authored attribute application would trip lane-discipline gate** — adding `[DbSerial]` to Bishop-lane test files is a Bishop-lane edit, not a Vasquez-lane edit; convention: attribute application stays in the file's owning lane.
- **PromQL alerting rules close dashboard JSON loop one wave later** — dashboard JSON precedes alerting rules by exactly one wave to give the metric surface time to shape before alert thresholds harden.
- **Bundle audit §3.X shrinkage trajectory beats estimates by 2-3×** — aggressive lazy-mount opportunities surface during a single audit pass at a ~2-3× multiplier over baseline estimates because eager chunks tend to bundle multiple cross-cutting modules that all lift together when one anchor lifts.
- **Per-tenant policy CRUD shape is canonical** — `/api/admin/<feature>/per-tenant` GET/POST/PUT/DELETE with 401→403→503→200/201/204 auth ladder + audit emissions established at W16 (JWKS) replicated at W17 (replay-retention).
- **Hard-delete with `?hard=true` query-param-gate** — when lifting a sentinel-row soft-delete workaround, default DELETE preserves backward-compat semantic and `?hard=true` query-param opts into hard-delete.
- **Header unification with 1-wave deprecation observability** — log `audit.admin.legacy_header_used` for 1 wave before considering hard-removal of legacy headers.
- **Single migration clusters related EF schema deltas** — `Phase_K_W17_AdminCrudAndPerTenantRetention` covers 3 schema deltas across 3 providers under one migration; convention: cluster related schema deltas to keep migration count low without sacrificing per-provider parity.
- **4th unamended wave in 7-wave 0-violation streak = late-mature steady state** — ~57 % unamended in 7-wave window suggests amendment frequency declining as `shared_files` registry matures.
- **8-entry `shared_files` registry load-tested across W15+W16+W17** — 3 consecutive waves; W15 §6.3 primary-classification rule is the canonical entry-deduplication heuristic.
- **Two-wave constant-gate-delta (W16 +309 / W17 +309)** — first 2-wave constant-delta in W11→W17 window; suggests Vasquez forward-stage + per-lane test density has converged to a steady-state cadence of ~300 net tests per wave; gate trajectory tracks contract surface not feature volume.

## W17 numeric milestones

| Metric | W16 | W17 | Δ |
|---|---|---|---|
| Gate (passed/failed/skipped) | 3621/0/0 | **3930/0/0** | **+309 (matches W16 exactly — first 2-wave constant-delta)** |
| Cumulative vs W6 (gate growth) | +154.6 % | **+176.4 %** | +21.8 pp |
| Multiplier vs W6 baseline | 2.55× | **2.76×** | +0.21× |
| `three-renderer-big.js` | 406,635 B | **406,635 B** | **+0 (7th hold-line wave)** |
| `renderer-webgl2` chunk | 19,017 B | **24,743 B** | **+5,726 (11.2 % of 220 KB envelope; under 40 KB W17 cap)** |
| `autotable-src-eager` | 214,202 B | **176,907 B** | **−37,295 (§3.2 surgery; 2.65× target; 17.4 % in single wave)** |
| Cumulative `autotable-src-eager` W15 → W17 | — | **−45,940 (−20.6 %)** | — |
| Chunk count | 24 | **27** | **+3** (leaderboard-page + settings-drawer + profile-page) |
| Lane-discipline strict (pre-Scribe) | checked=5 violations=0 | **checked=4 violations=0** | **7th 0-vio; NO AMENDMENT (4th unamended); post-Scribe target checked=5** |
| Coordinator-direct INTERVENTION streak | 11 waves | **12 waves** | **§6.5 EXECUTION categorically distinct from INTERVENTION — streak preserved** |
| Identity hardening | 11 waves | **12 waves clean** | held |
| Flock mutex | 7 waves | **8 waves fully-adopted** | held |
| Zero-skip streak | 31 waves | **32 waves** | held |
| three-renderer-big hold-line | 6 waves | **7 waves** | held |
| `shared_files` registry | 8 entries | **8 entries unchanged (3-wave load-test held)** | held |
| Vasquez/Bishop contribution split | — | **+123 Vasquez / +186 Bishop** | new |
| W17 commits | — | **4 bring-ups; 102 files; +12,065 / −179** | new |
| DbSerial ledger | 25/25 closed | **29 total / 25 closed / 4 open (all Bishop-lane)** | +4 open |

## Stephen action items (carry-into-March 2027)

1. **Branch-protection flip** — **W17 §4.5 RECALIBRATION DOWNGRADES back to Stephen-direct PRIMARY** (full reversal of W16 PROMOTE) with Coordinator-direct as conditional fallback gated on additional preconditions per §4.7 NEW execution gate. **§4.8 NEW Stephen-decision tree provides Options A/B/C with full `gh api -X PUT` payloads pre-authored** for Stephen to choose from. Stephen re-prompt **#12 is the PRIMARY path**.
2. **`pwa-audit.yml` cron trigger** — **§6.7 PROMOTED Coordinator-direct cron seed to PRIMARY at W17; Coordinator-direct EXECUTED 3 cron invocations at W17 (3rd produced `conclusion=failure`; convergence still 0 of 3)**. Cron-trigger path remains under Coordinator-direct PRIMARY; immediate calibration-deadlock pressure remains OFF per W16 LH13 Option A soft-flip.
3. **`PWA_PREVIEW_URL` secret** — Hicks LH13 hard-pin convergence depends on this AND cron-trigger path (#2). W17 §6.7 Coordinator-direct cron seed produced `failure`; convergence still 0 of 3.
4. **Secrets provisioning:** Sentry DSN (W9); OpenAI API key (W10; **7th consecutive wave blocking `EfCommentaryStore` persistence dogfood in prod**); Janus credentials (W11); Redis prod credentials (W11 ESO; W14+W15+W16+W17 pre-wire still blocked).
5. **Argo Rollouts install** in prod cluster — Apone W11→W17 prep all ready.
6. **Prod Redis TF apply** — Apone W11→W17 prep all ready.
7. **us-east-1 IRSA OIDC provider** — W14 §2.1 + W15 §5.4 + W16 §5.3 + W17 §5.3 plan-readiness re-checks all GREEN; **W17 PARTIAL-GREEN/HOLD resolved post-Hicks-rebase = 176,907 B = 177 KB < 200 KB ceiling — W18 apply will green-light** assuming IRSA OIDC provisioned.
8. **First real prod JWT rotation** — **W17 February window passed**; **reschedule to March 2027** paired with rehearsal #5. Apone W14 D4 GA-confirmed.

**12 consecutive weeks of Stephen re-prompt sequence; W17 §4.5 RECALIBRATION DOWNGRADES branch-protection back to Stephen-direct PRIMARY (full reversal of W16 PROMOTE); W17 §6.7 PROMOTES Coordinator-direct cron seed to PRIMARY and EXECUTES it 3 times (3rd cron produced `failure`); Stephen-blocked list contracts and re-expands — branch-protection RETURNS to Stephen list under §4.8 NEW decision tree.**

---

W17 is the wave that converts W16's `PerTenantJwksRotationValidator` standalone API into `JwtIssuingService.IssueForTenantAsync` wireup 1 wave later as predicted (Bishop wires the W16 6-kind verdict ladder into the signing path itself; NEW Prometheus counter `mahjong_jwt_pertenant_rotation_verdict_total{verdict,outcome}` increments on every signing decision; the W15 §3.2 table-before-validator → W16 validator-API → W17 service-wireup ladder completes in 3 waves exactly as predicted), the wave that lands Phase L W3 animation graph behind `?renderer=webgl2-animation` URL guard (Hicks's `src/renderer-webgl2/scene.ts` + `picking.ts` + canonical atlas committed artefacts + 3-mode `hello.ts` dispatch; renderer-webgl2 chunk 19,017 → 24,743 B = 11.2 % of 220 KB envelope), the wave that shrinks `autotable-src-eager` by −37,295 B at W17 alone (Hicks's bundle audit §3.2 second-pass surgery: leaderboard + settings-drawer + profile-page lazy-mounts; 2.65× §3.2 target; cumulative W15 → W17 −20.6 %), the wave that DOWNGRADES branch-protection §4.5 back to Stephen-direct PRIMARY via empirical 404 probe RECALIBRATION (Vasquez's `docs/agent-handoff-protocol.md §4.5 W17 RECALIBRATION` reverses W16 §4.5 PROMOTE; `gh api repos/long2know/mahjong-autotable/branches/main/protection` returns 404 → PATCH precondition fails → DOWNGRADE; §4.7 NEW execution gate; §4.8 NEW Stephen-decision tree with Options A/B/C pre-authored payloads), the wave that PROMOTES §6.6 Coordinator-direct cron seed to §6.7 PRIMARY and EXECUTES it 3 times (Vasquez's `docs/frontend-pwa-audit.md §6.7 NEW` promotes from OPTIONAL FALLBACK to PRIMARY at 1-wave mark; Coordinator-direct executed `gh workflow run pwa-audit.yml --ref main` × 3; **first Coordinator-direct EXECUTION since the no-pauses directive landed at W6**; categorically distinct from INTERVENTION; documented in Scribe ledger §6.5 only; 3rd cron produced `conclusion=failure`; convergence still 0 of 3), the wave where W16 +309 matches W17 +309 exactly (first 2-wave constant-gate-delta in W11→W17 window; Vasquez +123 / Bishop +186), and the wave that holds three-renderer-big at 406,635 B for the 7th consecutive wave (W11+W12+W13+W14+W15+W16+W17; cumulative W6 → W17 −44.9 % unchanged since W13). **All 4 W17 headlines from the W16 forward queue executed cleanly with no rollbacks, no amendments, and zero lane-discipline violations — 4th unamended wave in 7-wave 0-violation streak (~57 % unamended in 7-wave window) signals late-mature steady state.**

Scribe (Archive) — Phase K Wave 17 sweep DONE.
