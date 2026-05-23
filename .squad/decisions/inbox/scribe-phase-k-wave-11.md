# Scribe — Phase K Wave 11 sweep

**Date:** 2026-09-XX
**Branch:** `stlong/phase-k-wave-11-bringup` (cut from `main`
@ `0c95748` / Phase K Wave 10 squash-merge PR #56)
**Author:** Scribe (Archive) `<scribe@squad.mahjong>`
**Co-author trailer:** `Copilot <223556219+Copilot@users.noreply.github.com>`
**Model:** `claude-opus-4.7-xhigh` (Stephen's standing directive — DO NOT downgrade).

This memo records the Scribe (Archive) sweep folding the
4-file Phase K Wave 11 inbox (`bishop-` lives at
`.work/bishop-w11-safe/memo.md`, plus `hicks-` / `apone-` /
`vasquez-phase-k-wave-11.md`) into the canonical
`.squad/decisions.md` archive + a new wave-summary doc + the
Scribe per-agent history.

## Wave 11 headlines

- **Test gate `2403 / 0 / 0`** (+295 vs W10 baseline 2108) —
  **a new largest single-wave delta of Phase K** (W10 was
  +228, W8 +200, W9 +174). Phase K trajectory:
  **W6 1422 → W7 1506 → W8 1706 → W9 1880 → W10 2108 →
  W11 2403 (+981 over 6 waves; 69 % growth)**.
- **Three-renderer big chunk 466.40 KB** — <475 KB stretch
  **BEAT by 9 KB**; 6-wave monotonic-decrease
  `740 → 579 → 532 → 507 → 497 → 466 KB`; **cumulative
  −37.0 %**. ShaderChunk barrel surgery via the new Vite
  plugin `stripUnusedShaderChunks()` is the lever.
- **Lane-discipline strict-mode `checked=4 violations=0`** —
  **FIRST 0-VIOLATION LANE-DISCIPLINE WAVE.** Vasquez's
  `shims_shared` (4-author) + `pwa_audit_workflow_shared`
  (2-author) lane-map broadenings closed both W10 hand-offs;
  no new findings.
- **Sixth consecutive wave with zero identity drift + zero
  coordinator fix-up.** All 4 agent rollup commits correctly
  authored at the `%an <%ae>` level. `.work/squad-git-lock`
  cutover holds at the **second consecutive fully-adopted
  wave**.
- **26-wave zero-skip streak preserved** (J.1 → J.10 + K.1
  → K.11).

## Commit roll-up (4 commits, all correctly authored)

| Lane    | SHA       | Author                                          |
|---------|-----------|-------------------------------------------------|
| Bishop  | `8260849` | `Bishop (Backend) <bishop@squad.mahjong>`       |
| Vasquez | `29f55eb` | `Vasquez (QA) <vasquez@squad.mahjong>`          |
| Apone   | `df6888b` | `Apone (DevOps) <apone@squad.mahjong>`          |
| Hicks   | `5617029` | `Hicks (Frontend) <hicks@squad.mahjong>`        |

## Sweep deliverables

1. **`.squad/decisions.md`** — appended `## Phase K — Wave 11`
   section after the Wave 10 entry. ~690 lines added.
2. **`docs/wave-summaries/phase-k-wave-11.md`** (NEW) — mirrors
   the W10 wave-summary structure (headlines, per-lane
   deliverables, test gate, bundle metrics, identity hardening,
   lane-discipline strict-mode, W12 forward queue, Stephen
   action items, sign-off).
3. **`.squad/decisions/inbox/scribe-phase-k-wave-11.md`** (this
   file; force-added since `.squad/decisions/inbox/` is
   gitignored).
4. **`.squad/agents/scribe/history.md`** — appended W11 entry
   mirroring W10's structure.

## W12 forward queue (~30 items consolidated from 4 inbox memos)

- **Bishop (7):** DutchSwissPairingService retirement; TileReference codec reserved-byte usage; FIDE C.04 `floatAttempts < b.Count` cap refinement; commentary entity naming consistency; DI optional ctor params pattern documentation; `CommentaryStorageOptions.DefaultRetentionDays = 7` retention; RFC 7662 §2.2 transport-vs-token error invariant.
- **Hicks (8):** PMREMGenerator-adjacent ShaderChunk strip (~8-12 KB); UniformsLib unused-entry strip (~3-5 KB); `shadowmap_*` chunk body strip (~6 KB); LH13 workflow threshold edit after ≥ 3 real-CI cron data points; `secrets.PWA_PREVIEW_URL` provisioning (Apone owns); W10 placeholder screenshot copy block removal (W13); visual-regression spec for W11 captures (Vasquez); `?action=replay` once Drake's replay-by-id endpoint lands.
- **Apone (7):** prod Redis stack `terraform apply` (blocked on EKS cluster cutover); prod kustomization wiring; prod Redis load-test re-baseline; per-region R53 records; NetworkPolicy for argo-rollouts dashboard; second JWT rehearsal run ahead of Q4 prod rotation; W14 TF CLI bump (~1.11.x).
- **Vasquez (4):** DbSerial migration; LH13 workflow gate edit; visual-regression for Hicks W11 captures; ongoing branch-protection re-prompt to Stephen.
- **Lane-discipline cross-cutting:** maintain 0-violation stretch goal through W12.

## Stephen action items (carry-into-October 2026)

1. Branch-protection flip (`lane-discipline / check`
   required-status-check on `main`; W9+W10+W11 hand-off still
   pending; repo-admin only). §4.1 walkthrough is correct.
2. `secrets.PWA_PREVIEW_URL` provisioning for `pwa-builder.yml`.
3. Sentry + Cloudflare DSN provisioning (carry-over).
4. OpenAI API key provisioning (now blocks `EfCommentaryStore`
   prod dogfood).
5. Janus SFU sizing + endpoint provisioning.
6. Prod EKS cluster cutover (unblocks Apone's prod Redis
   `terraform apply`).
7. Q3 2026 JWT rotation rehearsal (operator dry-run via
   `jwt-rotation-rehearsal.yml` ahead of end-of-September real
   rotation).

## Standing directives reaffirmed

- `claude-opus-4.7-xhigh` for all agent runs including Scribe
  + mechanical roles.
- No pauses; fan out and keep iterating until 100 % done done.
- Race-safe identity binding via per-invocation
  `git -c user.name=X -c user.email=Y commit ...`.
- `flock -w 120 9 ... 9>.work/squad-git-lock` mutex (cutover
  COMPLETE at W10; 2nd consecutive fully-adopted wave at W11).
- `.squad/decisions/inbox/` force-add via `git add -f`.
- Selective `git add` per-lane.
- Three-renderer hard-asserted under 475 KB stretch (W11
  BEAT by 9 KB).
- OpenAI commentary fail-open mandatory.
- Janus SFU fail-open with readiness 3-level circuit-breaker
  (W10) + mountpoint-eviction SignalR metric tie-in (W11).
- CI pre-commit gate parity mandatory.
- `tableId ≡ gameId` W9 identity decision still held at W11.
- Three independent canary gates with NO aggregation logic.
- Mobile hotfix uses separate 2-reviewer environment with
  three durable audit-trail markers.
- `scripts/check_invariants.py` is the cross-file invariant
  extension point.
- `git fetch + rebase` INSIDE the flock critical section.
- Every cutover-plan section MUST include a `git grep
  <old-path>` step before declaring complete.
- CHANGELOG version-arithmetic check goes in every
  changelog-bump pattern.
- `[Collection("DbSerial")]` is canonical for SQLite-heavy
  contract tests.
- `maxmemory-policy=allkeys-lru` is canonical for the
  idempotency cache.
- `TileReference(string TileId, string Suit, int Rank)` is
  the canonical commentary tile-ref shape; W11 adds the
  4-byte binary codec for SignalR wire.
- FIDE C.04 backtracking is the canonical Swiss pairing
  algorithm behind `ISwissTiebreakStrategy` (W11; replaces
  W10 Dutch-Swiss single-swap pass).
- `signalr_envelope_age_seconds` histogram is the canonical
  p99 envelope-age surface.
- `EfCommentaryStore` is the canonical commentary
  persistence surface.
- RFC 7662 §2.2: per-token errors return HTTP 200
  `{ active: false }`; only transport errors return 4xx.
- Out-of-band ESO manifest pattern (don't list in
  `kustomization.yaml resources:` if it binds to env-specific
  SSM paths + CMK KMS that don't exist in dev/preview).
- Rehearse before the first quarterly drill (W11 JWT
  rehearsal harness is the first instance; hard gate on
  `target_env != staging` is the canonical safety pattern).
- Range-floor + exact-pin TF version policy (quarterly
  cadence anchored on wave bring-up).
- Multi-region synthetics need a fan-out failure-mode
  playbook.

## Sign-off

Phase K Wave 11 closes **2403 / 0 / 0** with +295 over W10
baseline — **a new largest single-wave delta of Phase K**.
Three-renderer big chunk **466.40 KB** — <475 KB stretch
**BEAT by 9 KB**; 6-wave monotonic-decrease cumulative
**−37.0 %**. **Sixth consecutive identity-clean wave + FIRST
0-VIOLATION lane-discipline wave.** 26-wave zero-skip streak
preserved. ~30-item W12 forward queue captured. Branch ready
for PR against `main`.

— Scribe (Archive), Phase K Wave 11 sweep
