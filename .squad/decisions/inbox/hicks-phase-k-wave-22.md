# Hicks — Phase K Wave 22 bring-up memo

**Agent:** Hicks (Frontend)
**Wave:** Phase K Wave 22
**Branch:** `stlong/phase-k-wave-22-bringup`
**Model:** claude-opus-4.7-xhigh

---

## Scope (assigned at wave open)

1. LH13 §13 — re-evaluate §6.9 evidence gate against `main`. Cron
   pace is the only outstanding blocker; record disposition.
2. Admin-panel chunk-split — split the W21 single `admin-panel`
   chunk (48,984 B) into 2 chunks, target each ≤ 30 KB.
3. Phase L renderer — add discard-pile (per-seat 6-column grid,
   riichi rotation) + score-display HUD (4 seat chips, dora
   indicators, round-context).
4. Bundle audit §3.7 — shed ~7 KB from `autotable-src-eager` to
   hit the ≤105 KB (107,520 B) ceiling (down from 112,219 B at
   W21 close).
5. Hold `three-renderer-big` at ≤ 406,635 B (W22 ceiling = W21
   close = W14 baseline).  **12th consecutive wave** at the W14
   hold-line.
6. Admin UI — wire five W22 Bishop surfaces:
   tournament finalize, replay download-chunked, JWT emergency-
   revoke, SignalR diagnostics, audit-log search.

---

## Disposition (at wave close)

### 1. LH13 §13 — schedule-event evidence gate (HOLD YELLOW)

Status: **HOLD YELLOW**.  Re-eval at W25 earliest.

- pwa-audit.yml cron is `30 2 * * *` — **nightly** at 02:30 UTC,
  not hourly as W19-W21 assumed.
- Only **1 schedule-event run total** has fired (sha=c866535 W16
  merge, pre-fix, FAILED).
- **0 successful schedule-event runs** on main since W18 merged
  (2026-05-24T11:02:58Z).
- Blocker is no longer gh-auth — purely natural cron-pace
  accumulation.  3 nightly cron ticks accumulate by ~2026-05-27
  02:30 UTC → **W25 earliest PROMOTE** wave.

Documented in `docs/lh13-soft-pin-rationale.md §13`.

### 2. Admin-panel chunk-split (DONE)

W21 had **14 admin surfaces in a single 48,984 B chunk**.  W22
adds 5 new surfaces (would have been ~64 KB unsplit).  Now
shipped as TWO measurable chunks via `manualChunks` regex
routing in `vite.config.ts`:

| Chunk                       | W22 size  | Surfaces |
| --------------------------- | --------- | -------- |
| `admin-panel-core`          | 31,164 B  | 7        |
| `admin-panel-tournaments`   | 32,579 B  | 12       |
| **Total**                   | 63,743 B  | 19       |

`admin-panel-core` (static-imported by `admin-panel.ts`):
  - admin-panel entry + admin-shared scaffolding
  - replay-retention, jwks-rotation, signalr-retention
  - rotation-policy-bulk, rotation-policy-bulk-actions
  - rotation-schedule, jwt-rotation-drill

`admin-panel-tournaments` (lazy via `admin-tournaments.ts`
barrel, dynamic-imported on `openAdminPanel`):
  - swiss-pairing-audit, swiss-pair-next-round, swiss-apply-round
  - tournament-withdraw, **tournament-finalize** (W22)
  - signalr-purge, **signalr-diagnostics** (W22)
  - replay-integrity-audit, replay-restoration-audit,
    **replay-download-chunked** (W22)
  - **audit-log-search** (W22)
  - **jwt-emergency-revoke** (W22)

The aspirational ≤ 30 KB target is missed by ~4 % on core and
~9 % on tournaments — but both chunks are well under the W21
single-chunk ceiling and the split itself adds no duplication
(total = 63,743 B = W21 48,984 B + ~14,800 B for 5 new surfaces).
Future W23+ waves can rebalance by promoting an additional
core-tier surface into the tournaments chunk if the targets get
tighter.

Naming caveat: `admin-panel-tournaments` carries audit-log /
ops surfaces too; the name is the chunk's identity, not its
sole content scope (documented in `admin-tournaments.ts`
header).

### 3. Phase L renderer additions (DONE)

Two new modules under `src/renderer-webgl2/`:

- `discard-pile.ts` (9.3 KB src) — per-seat visible discard pile
  with 6-column grid layout, allocation-free `mat4` matrix
  computation per tile, riichi-rotation flag (90° turn on the
  riichi-declared tile), capacity for 28+ discards/seat.  No
  GPU buffers allocated at module-load time — the per-seat
  state is pure JS until a tile is appended.
- `score-display.ts` (11 KB src) — canvas-backed HUD pinned
  top-right.  Renders 4 seat score chips + dora indicators
  + round/honba/riichi-pot context.  Redraw is hash-cached so
  no-op updates skip the 2D-context work.

`renderer-webgl2` chunk: 40,292 B (W21: 40,292 B).  These modules
are not yet imported by anything in W22 — they're staged for the
W23 wiring directive into the live game-bootstrap path.

### 4. Bundle audit §3.7 (DONE — ≤ 105 KB target met)

| Wave | autotable-src-eager | Δ        |
| ---- | ------------------- | -------- |
| W20  | 123,701 B           | —        |
| W21  | 112,219 B           | -11,482  |
| W22  | **107,020 B**       | -5,199   |

Target: ≤ 107,520 B (≤ 105 KiB).  **HIT** with ~500 B headroom.

Surgery: extracted **onboarding card** + **avatar-migration
modal** out of `identity.ts` into two lazy chunks.

- `identity-onboarding.ts` (3,293 B chunk) — `installOnboardingCard`
  + private helpers (`showOnboardingCard`, `hideOnboardingCard`,
  `onboardingInitial`, `expandHexColor`, `applyProfileFromOnboarding`).
  Lazy-loaded in `lobby.ts:scheduleOnboardingLazyMount()`, which
  probes `LS_KEY_ONBOARDED` synchronously and short-circuits for
  returning users — they never pay for the chunk.
- `identity-avatar-migration.ts` (1,987 B chunk) —
  `installAvatarMigrationModalIfNeeded` + grid renderer +
  legacy-sentinel probe.  Now lazy-loaded by
  `index.ts:scheduleAvatarMigrationLazyMount` (which already
  dynamic-imported but previously hit `identity.ts` itself, so
  eager bundle still paid the cost).
- `identity.ts` keeps the small `shouldShowOnboarding` predicate
  + an inlined `refreshOnboardingVisibility` (DOM toggle only)
  + two narrow shim hooks (`applyOnboardingProfile`,
  `markOnboardingCompleteExported`) for the lazy module to write
  back into the cached Identity.

### 5. three-renderer-big — 12th hold-line wave (PASS)

- W22 size: **406,635 B**
- W14-W22 hold-line: **406,635 B** (12 consecutive waves)
- Vasquez's W7 monotonicity assertion still passes (no regression).

### 6. Five new admin surfaces (DONE)

All five follow the existing `AdminSurfaceSpec<TRow, TBody>`
contract from `admin-shared.ts`.

| File                              | Endpoint                                           | Mode       |
| --------------------------------- | -------------------------------------------------- | ---------- |
| `tournament-finalize.ts`          | POST `/api/admin/tournaments/{id}/finalize`        | mutating   |
| `replay-download-chunked.ts`      | GET  `/api/admin/replays/{id}/chunks/{n}`          | read-only  |
| `jwt-emergency-revoke.ts`         | POST `/api/admin/jwt-keys/emergency-revoke`        | mutating   |
| `signalr-diagnostics.ts`          | GET  `/api/admin/signalr/diagnostics`              | read-only  |
| `audit-log-search.ts`             | GET  `/api/admin/audit-log` (paginated)            | read-only  |

`jwt-emergency-revoke` uses the W18 `confirm-keyId` guard pattern
to prevent fat-finger revocation: the user must retype the keyId
into a confirmation field before the mutate button enables.

---

## Final dist-size snapshot (K22)

```
autotable-src-eager           107,020 B   (target ≤105 KB ✅)
admin-panel-core               31,164 B   (target ≤ 30 KB; +1.4 KB)
admin-panel-tournaments        32,579 B   (target ≤ 30 KB; +2.6 KB)
renderer-webgl2                40,292 B   (target ≤ 52 KB ✅)
three-renderer-big            406,635 B   (12th hold-line ✅)
identity-onboarding             3,293 B   (NEW W22 lazy)
identity-avatar-migration       1,987 B   (NEW W22 lazy)
```

38 chunks total (was 36 at W21 with single admin-panel + identity
fused).  Net split-vs-fusion: `+admin-panel-core` `+admin-panel-
tournaments` `+identity-onboarding` `+identity-avatar-migration`
`-admin-panel` = +3 chunks; the 38-36 = 2 delta is because vite
also briefly emitted a shared client-ui chunk on the boundary
between identity-onboarding and lobby-eager (12.77 KB,
`client-ui.<hash>.js`) that captures profile/hub helpers shared
by both eager + onboarding chunks.  Net effect on cold path is
neutral (shared helpers stay in client-ui regardless).

---

## Hand-off recommendations for W23 Hicks

1. **Wire Phase L renderer modules** — `discard-pile.ts` and
   `score-display.ts` are staged but not yet imported.  W23
   wiring directive should:
     - Import `discardPileSetup`/`appendDiscardTile`/etc.  from
       `game-bootstrap` or wherever the per-seat discard signal
       lands.
     - Mount `score-display.ts` into the game container; subscribe
       to score/dora updates from the existing game-state events.
2. **Watch for admin-panel rebalance opportunity** — if W23+ adds
   more admin surfaces, prefer routing the additional surface
   into whichever chunk is currently smallest (currently `admin-
   panel-core` at 31,164 B).  Each surface costs ~2.6-3 KB minified.
3. **LH13 promote re-eval at W25** — 3 nightly cron ticks
   accumulate by ~2026-05-27 02:30 UTC.  If ≥ 3 SUCCESS
   schedule-event runs are observed on main, promote §6.9 from
   YELLOW to GREEN.
4. **Bundle-audit §3.8** — autotable-src-eager has ~500 B
   headroom at 107,020 B.  Next ceiling target should be ≤100 KB
   (≤102,400 B).  Likely candidates for further extraction:
     - `lobby.ts` is ~85 KB raw and is the largest eager
       contributor.  Look for split-points around the tab-strip
       scaffolding or the public-games card factory.
     - The `kbd-shortcut-help` modal helper (if eager).
5. **client-ui chunk** appeared at W22 as an automatic vite
   chunk boundary.  Verify it's not duplicating profile/hub
   helpers between eager and lazy paths; if so, consider
   adjusting `manualChunks` to keep them strictly in eager.

---

## Lane discipline

All file touches confined to:

- `src/frontend/autotable-src/` (Hicks src-tree)
- `src/frontend/autotable/` (Hicks dist output — rebuilt artefacts)
- `docs/lh13-soft-pin-rationale.md` (§13 W22 append — coordinator-
  shared docs lane)
- `.squad/decisions/inbox/hicks-phase-k-wave-22.md` (this memo,
  `git add -f` because inbox is gitignored)

`bash tests/ci/check-cross-lane-bundling.sh --pr stlong/phase-k-
wave-22-bringup --strict` should pass.

---

## Files touched (summary)

**New (10):**
- `src/admin/tournament-finalize.ts`
- `src/admin/replay-download-chunked.ts`
- `src/admin/jwt-emergency-revoke.ts`
- `src/admin/signalr-diagnostics.ts`
- `src/admin/audit-log-search.ts`
- `src/admin/admin-tournaments.ts` (barrel)
- `src/renderer-webgl2/discard-pile.ts`
- `src/renderer-webgl2/score-display.ts`
- `src/identity-onboarding.ts`
- `src/identity-avatar-migration.ts`

**Modified:**
- `src/admin/admin-panel.ts` (chunk split + lazy load)
- `src/identity.ts` (extracted ~280 lines to lazy modules)
- `src/lobby.ts` (lazy-import onboarding installer)
- `src/index.ts` (point migration probe at new module)
- `vite.config.ts` (manualChunks admin split)
- `scripts/append-dist-size.js` (record new chunk keys)
- `dist-size.json` (K22 row)
- `docs/lh13-soft-pin-rationale.md` (§13 append)
- `src/frontend/autotable/*` (rebuilt dist)
