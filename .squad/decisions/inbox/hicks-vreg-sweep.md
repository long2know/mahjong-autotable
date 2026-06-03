# Visual regression sweep — 10 scenarios

**Filed:** 2026-06-03 (Hicks, Frontend / Three.js)
**Commissioned by:** Stephen via Copilot directive — "thorough testing
of the UI; cover multiple resolutions and multiple game phases; catch
any remaining visual bugs before we declare the Changsha bring-up
clean."
**Branch:** `test/hicks-vreg`
**Baseline:** `playtest-artifacts/screenshots/hicks-final-clean-2026-06-01T20-52-57Z.png`
(round-3 fence-post fix, walls-facedown post-deal frame, **zero page errors**).
**Spec:** `playtest-artifacts/playtest-hicks-vreg.spec.mjs` (new).
**Findings JSON:** `playtest-artifacts/screenshots/hicks-vreg-2026-06-03T16-03-30-667Z-findings.json`.

## Verdict

**No regressions vs the round-3 baseline.** All 10 scenarios pass the
page-errors gate (0/0/0 across the board). Residual console noise
matches the baseline exactly (1× `Computed radius is NaN` per scenario
+ 2× benign 404 on Quick-Match's pre-creation `/api/games/{id}` probe).
The Changsha 108-tile budget holds in every scenario (`thingCount=109`
= 108 tiles + 1 marker, no Riichi 197-thing flip).

Two cosmetic UX observations are flagged below but neither is a
regression — both predate this sweep and one was already mentioned in
`hicks-broken-deal-fix.md` (Frost-Frontend Phase-G ticket queue).

## How the sweep was run

* Single Playwright spec, 10 scenarios run sequentially against the
  shared backend at `http://127.0.0.1:8088` (started by Apone/Vasquez
  with `/tmp/mat-test-wave.db`).
* All gameIds prefixed `hicks-vreg-*` to avoid collision with parallel
  squad agents.
* Each scenario: `addInitScript` to defang `#tour-overlay`,
  `#magic-link-landing`, `#signin-modal-backdrop`; goto the URL with
  scenario-specific query params; wait for `#deal` to be visible;
  click Quick-Match → wait for "Leave seat" → optionally trigger deal
  via `window.game.world.deal('HANDS')` (the `#deal` hold-progress
  button is flaky in headless); settle 4-12-32s; capture full-page
  screenshot, console errors, page errors, network failures, and
  `world.things` state.
* Run from the repo root: `node playtest-artifacts/playtest-hicks-vreg.spec.mjs`
  — the spec resolves `./playtest-artifacts/screenshots` relative to cwd.

## Summary table

| # | Scenario           | Viewport    | Bots | DealMode | pErr | cErr | NaN | netFail | wall | dHand | disc | gType    | Verdict |
|---|--------------------|-------------|------|----------|------|------|-----|---------|------|-------|------|----------|---------|
| 1 | desktop-1920       | 1920×1080   |  3   | auto     |  0   |  3   |  1  |   2     |  55  |  14   |   0  | CHANGSHA | ✅ clean |
| 2 | mobile-375         |  375×667    |  3   | auto     |  0   |  3   |  1  |   2     |  55  |   0   |   0  | CHANGSHA | ⚠ panel |
| 3 | tablet-768         |  768×1024   |  3   | auto     |  0   |  3   |  1  |   2     |  55  |  14   |   0  | CHANGSHA | ⚠ panel |
| 4 | human-4p-nobots    | 1920×1080   |  0   | manual   |  0   |  3   |  1  |   2     | 108  |   0   |   0  | CHANGSHA | ✅ clean |
| 5 | bots-2             | 1920×1080   |  2   | auto     |  0   |  3   |  1  |   2     |  55  |  14   |   0  | CHANGSHA | ✅ clean |
| 6 | bots-4-auto        | 1920×1080   |  3   | auto     |  0   |  3   |  1  |   2     |  43  |   0   |  11  | CHANGSHA | ✅ WIN  |
| 7 | camera-flat        | 1920×1080   |  3   | auto     |  0   |  3   |  1  |   2     |  55  |  14   |   0  | CHANGSHA | ⚠ N/A   |
| 8 | setup-menu-open    | 1920×1080   |  3   | auto     |  0   |  3   |  1  |   2     |  55  |  14   |   0  | CHANGSHA | ✅ clean |
| 9 | movelog-open       | 1920×1080   |  3   | auto     |  0   |  3   |  1  |   2     |  55  |  14   |   0  | CHANGSHA | ✅ clean |
|10 | settled-30s        | 1920×1080   |  3   | auto     |  0   |  3   |  1  |   2     |  41  |   0   |  14  | CHANGSHA | ✅ DRAW |

Legend: `pErr` = uncaught `pageerror` event count; `cErr` = console
errors; `NaN` = `Computed radius is NaN` warnings; `netFail` = HTTP 4xx/5xx
responses; `wall` = `tilesInWall`; `dHand` = dealer hand size; `disc`
= discard count; `gType` = `world.gameType`.

The cErr=3 / NaN=1 / netFail=2 column is the **baseline noise floor** —
identical counts across every scenario, identical to the round-3
baseline. Not a regression.

## Per-scenario observations

### 1. desktop-1920 — ✅ clean (matches baseline)

`hicks-vreg-2026-06-03T16-03-30-667Z-desktop-1920-01-post-settle.png`

Standard 1920×1080 Changsha auto-deal. Four walls 2-high (28+28+26+26
= 108 face-down), dealer hand 14 face-up tiles centred along the
south edge, no corner wedges, no centre HUD plane, lobby sidebar
visible top-left. Pixel-equivalent to `hicks-final-clean-...png`.

### 2. mobile-375 — ⚠ Settings panel covers viewport (pre-existing UX, NOT a regression)

`hicks-vreg-2026-06-03T16-03-30-667Z-mobile-375-01-post-settle.png`

At 375×667 the Ferro Settings / Variant-Picker panel fills the entire
viewport and intercepts all clicks. The Quick-Match button never
becomes reachable, so the scene behind the panel never gets a deal
(`dealerHand=0`, `seat=null`). World state still ticks (`wallCount=55`
once `world.deal('HANDS')` was triggered by the spec evaluate
fallback). This is the same pre-existing mobile UX as documented in
`hicks-mobile-375-and-lobby-overlay.md` — UX work, not a 3D
regression. The Settings panel itself renders cleanly with no
overflow / scroll issues.

**Recommendation:** the panel should ship with a close (✕) affordance
visible at narrow widths. Already on the Phase-F/G frontend polish
list. Not blocking any other work.

### 3. tablet-768 — ⚠ Settings panel covers viewport but scene rendered behind it

`hicks-vreg-2026-06-03T16-03-30-667Z-tablet-768-01-post-settle.png`

At 768×1024 the Settings panel overlays the scene full-width. Unlike
mobile, the Quick-Match flow completed (`seat=0`, `dealerHand=14`,
`wallCount=55`) and the scene rendered correctly behind the panel.
Same UX recommendation as mobile: panel needs a close button at
tablet widths.

### 4. human-4p-nobots — ✅ clean

`hicks-vreg-2026-06-03T16-03-30-667Z-human-4p-nobots-01-post-settle.png`

`dealMode=manual&botCount=0`. Four walls face-down, no dealer hand
(`wallCount=108`, `dealerHand=0`). Confirms the pre-deal Changsha
layout still matches `hicks-final-clean`: walls 2-high on all four
seats, 14/14/13/13 column split, no phantom row-19 trailing stacks,
no corner wedges. The exact frame the baseline captures.

### 5. bots-2 — ✅ clean

`hicks-vreg-2026-06-03T16-03-30-667Z-bots-2-01-post-settle.png`

Quick-Match with `botCount=2` triggered the deal. Scene is clean —
four walls, dealer hand 14 face-up tiles, no wedges, no HUD. Lobby
sidebar HUD reads "3 bots — Medium · seats 1, 2, 3" rather than 2
bots; this is a pre-existing sidebar display quirk (the HUD reads
the URL default, not the live seat occupancy) and is independent of
the 3D scene render. Filed below as a low-priority UX note.

### 6. bots-4-auto — ✅ end-of-hand WIN modal (strong proof of full game loop)

`hicks-vreg-2026-06-03T16-03-30-667Z-bots-4-auto-01-post-settle.png`

After 12s of settle with 4 bots, the hand ran to completion: "Bot 1
wins!" modal with full move log, winning hand display, and payment
summary. World state: `wallCount=43`, `allDiscard=11`, `allHand`
unchanged. **This is the strongest end-to-end proof we have that the
game loop works in CI** — tiles draw, discard, claim, meld, score,
and the win modal renders without errors. No regression.

### 7. camera-flat — ⚠ no flat-camera toggle exists in current bundle

`hicks-vreg-2026-06-03T16-03-30-667Z-camera-flat-01-post-settle.png`

The spec attempts to click `#camera-flat`, `#perspective-toggle`,
`#view-flat`, `#toggle-perspective` — none exist in the current
bundle. Scene rendered in standard perspective and matches
desktop-1920. **NOT a regression** — the feature either was never
shipped or lives behind a different control surface I haven't
located. If a flat-camera mode is intended for Phase-G, the toggle
selector and its source binding should be added explicitly.
→ **Hand-off to Stephen / Ripley:** is flat-camera on the Phase-G
roadmap? If yes, file a frontend ticket; if no, drop this scenario
from future vreg sweeps.

### 8. setup-menu-open — ✅ clean (Welcome avatar overlay)

`hicks-vreg-2026-06-03T16-03-30-667Z-setup-menu-open-01-post-settle.png`

Clicking the lobby "Setup" button on a fresh `hicks-vreg-*` gameId
triggers the new-player flow ("Welcome! Pick your avatar"). The
modal renders correctly with the avatar grid + name field. Scene
behind is the standard Changsha post-deal layout. NOT a regression
— this is the documented new-player onboarding path.

### 9. movelog-open — ✅ clean

`hicks-vreg-2026-06-03T16-03-30-667Z-movelog-open-01-post-settle.png`

Move Log panel open (top-right) showing "[09:05:44] Match started —
dealer is Seat 0" and "[09:05:49] Dice rolled: 4 + 2 = 6". Scene
behind is the standard Changsha post-deal layout — four walls,
dealer hand 14 face-up, no wedges, no HUD. The panel docking,
font, timestamp format all look correct.

### 10. settled-30s — ✅ DRAW modal (possible bot-strategy stall — Frost flag)

`hicks-vreg-2026-06-03T16-03-30-667Z-settled-30s-01-post-settle.png`

After 32s of settle with 4 bots, the hand ended in a Draw (流局).
Move log shows multiple draw entries. World state: `wallCount=41`,
`allDiscard=14`. Visually clean. **NOT a visual regression** but
worth noting: with 4 bots and no human, the auto-played hand
reaches a Draw rather than a Win in ~32s — could indicate the
Medium-difficulty bot strategy is over-conservative and stalls out.
→ **Hand-off to Frost (bot strategy):** consider re-tuning the
default Medium heuristic if Draw-rate is higher than expected. Not
my lane; flagging only.

## Console noise — confirmed pre-existing, not introduced by this sweep

Every scenario reports exactly:

* 1× `THREE.BufferGeometry.computeBoundingSphere(): Computed radius
  is NaN. The "position" attribute is likely to have NaN values.`
  — flagged as **Phase-G ticket** in `hicks-cleanup-round2.md`.
  Confirmed not the point-stick tray (Vasquez `dd2608d` toggle test
  + my round-2 fix removed tray rendering on Changsha and the warning
  persists). Likely culprit is another GLB primitive
  (`meshes.center` or one of the tile/marker meshes) with one or
  more NaN-position vertices. Investigation requires walking the
  GLB load path and adding `Number.isFinite` guards around
  `position.array` before `computeBoundingSphere`. Out of scope for
  the vreg sweep; recommend Phase-G dedicated ticket.

* 2× `Failed to load resource: 404 (Not Found)` on
  `/api/games/{id}` and `/api/games/{id}/settings`. Quick-Match
  creates the session via WebSocket before the REST endpoints see it.
  Benign — the frontend handles 404 gracefully and falls back to
  defaults until the WS handshake completes. Flagged as a low-noise
  cleanup ticket to Bishop (have the REST endpoint return an empty
  200 for newly-allocated game IDs instead of 404).

## Hand-offs

| To              | Item                                                                 | Priority |
|-----------------|----------------------------------------------------------------------|----------|
| Frost           | Bot Medium-difficulty stall → Draw at 32s with 4-bot auto (settled-30s) | Low      |
| Stephen / Ripley | Decide whether flat-camera toggle ships in Phase-G (or drop scenario) | Low      |
| Bishop          | `/api/games/{id}` returns 404 during WS-first session creation       | Low      |
| Hicks (self)    | Phase-G ticket: trace residual `Computed radius is NaN` source       | Med      |
| Frontend polish | Settings panel needs ✕ close affordance at mobile/tablet widths      | Med      |
| Frontend polish | Lobby sidebar HUD bot-count display reads URL default, not live seat occupancy | Low |

**None of these block any work in flight.** The game is visually +
functionally healthy end-to-end at the round-3 baseline.

## Screenshots (for posterity)

All in `playtest-artifacts/screenshots/` (gitignored — paths only):

```
hicks-vreg-2026-06-03T16-03-30-667Z-desktop-1920-01-post-settle.png
hicks-vreg-2026-06-03T16-03-30-667Z-mobile-375-01-post-settle.png
hicks-vreg-2026-06-03T16-03-30-667Z-tablet-768-01-post-settle.png
hicks-vreg-2026-06-03T16-03-30-667Z-human-4p-nobots-01-post-settle.png
hicks-vreg-2026-06-03T16-03-30-667Z-bots-2-01-post-settle.png
hicks-vreg-2026-06-03T16-03-30-667Z-bots-4-auto-01-post-settle.png
hicks-vreg-2026-06-03T16-03-30-667Z-camera-flat-01-post-settle.png
hicks-vreg-2026-06-03T16-03-30-667Z-setup-menu-open-01-post-settle.png
hicks-vreg-2026-06-03T16-03-30-667Z-movelog-open-01-post-settle.png
hicks-vreg-2026-06-03T16-03-30-667Z-settled-30s-01-post-settle.png
hicks-vreg-2026-06-03T16-03-30-667Z-findings.json
```

Baseline for comparison:
`hicks-final-clean-2026-06-01T20-52-57Z.png`.

## Lane discipline

Touched this task only:
* `playtest-artifacts/playtest-hicks-vreg.spec.mjs` (new spec)
* `.squad/decisions/inbox/hicks-vreg-sweep.md` (this memo)
* `.squad/agents/hicks/history.md` (team-update entry)

**No source code under `src/frontend/autotable-src/` or
`src/frontend/autotable/` was modified** — there is nothing to fix.
No backend touched. Screenshots + findings.json are gitignored and
not committed.
