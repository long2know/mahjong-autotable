# Phase J Wave 1 — Hot-seat swap + spectator camera lock (Hicks)

**Branch:** `stlong/phase-j-wave-1-hardening`
**Baseline bundle (Wave I.4):** `autotable-src.c93fbb44.js` + `autotable-src.3f21032c.css`
**Shipped bundle (Wave J.1):** `autotable-src.214d524e.js` + `autotable-src.884bb475.css`
**Commit:** `781798e` (`feat(ui): Phase J Wave 1 — hot-seat swap + spectator camera lock`)

## What shipped

### Task 1 — Hot-seat swap UI (PRIMARY) — ✅ shipped

A new **Move** button + inline picker in the sidebar HUD lets a connected
player swap seats without rejoining the page.  Lives between the Game-ID
row and the Leave-seat row.

- **Visibility:** shown only when the WS is connected AND no match has
  been dealt yet.  Gate is `client.connected() && client.match.get(0) ===
  null`.  Listeners on `connect` / `disconnect` / `match.update`
  re-evaluate; opening Deal flips `match` to non-null and the row
  disappears (matches the requested visual test: connect → see Move; hit
  Deal → Move vanishes).
- **Picker:** five buttons — `Seat 0`, `Seat 1`, `Seat 2`, `Seat 3`,
  `Spectate`.  Current seat is disabled.  Seats occupied by other
  players are disabled.  `Spectate` is disabled when already in
  spectator mode.  `seats.update` listener keeps these states in sync.
- **Soft reconnect:** clicking an option rewrites `?seat=` on the page
  URL via `history.replaceState` (sticky `?gameId=` preserved by
  definition — we never touch it), clears our local `seats` entry to
  avoid client-ui.ts re-applying the stale seat on reconnect, then calls
  `client.disconnect()`.  client-ui.ts's existing auto-reconnect kicks
  in after `RECONNECT_DELAY` (~2 s) and picks the new seat off the URL
  via `buildWsUrl()`.  After JOIN, the existing `onConnect` flow
  re-evaluates `readSpectatorFromUrl()` so the body class + pill
  reflect the new state automatically.
- **No client-ui.ts touch required.**  The existing connection
  primitives (`?seat=` query → `buildWsUrl` → `connect`) were enough.

### Task 2 — Spectator camera lock (SECONDARY) — ✅ shipped (tiny tweak)

The 3D camera already goes top-down when `world.seat === null`
(`main-view.ts:updateCamera` has a `fromTop` branch that sets the
camera to `(0, 0, 400)` perspective / `(0, 0, 100)` ortho looking
straight down at the table origin).  For a spectator, `client.seat`
ends up null after the first `seats` update because the spectator's
playerId is never in the seats collection.

The only gap was a brief flash of the seat-0 first-person view between
page load and the first WS `seats` update, because `world.seat = 0`
was the hard-coded initial value.  Fixed with a one-line tweak:

```ts
// src/frontend/autotable-src/src/world.ts
seat: number | null = readSpectatorFromUrl() ? null : 0;
```

`readSpectatorFromUrl` is the same helper that already drives the
spectator pill + body class (exported from client-ui.ts).  Non-spectator
behaviour is byte-identical (initial value is still `0`).

`main-view.ts` was NOT modified — the existing `fromTop` branch did
the work.  No orbit-controls exist (verified via `grep OrbitControls` →
no matches), so nothing to disable.

## Files touched

| File | Change |
|---|---|
| `src/frontend/autotable-src/index.html` | Added `#move-seat-row` containing `#move-seat-btn` + `#move-seat-panel` with five `.move-seat-option` buttons (Seat 0..3 + Spectate).  Initial inline `display: none`. |
| `src/frontend/autotable-src/src/game-ui.ts` | Added `moveSeatRow / moveSeatBtn / moveSeatPanel / moveSeatOptions` elements + `setupMoveSeatPicker` / `refreshMoveSeatVisibility` / `refreshMoveSeatPicker` / `softReconnectWithSeat` methods.  Wired into the constructor's setup chain. |
| `src/frontend/autotable-src/src/style.css` | `.move-seat-panel` (dark inline flex grid) + `.move-seat-option` (50/50 flex) + disabled-button greyout.  No new colour family — reuses the existing dark/primary/secondary Bootstrap variants. |
| `src/frontend/autotable-src/src/world.ts` | Imported `readSpectatorFromUrl` from `./client-ui`; `seat` initial value derived from URL. |
| `src/frontend/autotable/autotable-src.214d524e.js` | New JS bundle (Parcel). |
| `src/frontend/autotable/autotable-src.884bb475.css` | New CSS bundle (Parcel). |
| `src/frontend/autotable/index.html` | Parcel-regenerated; references new bundle hashes. |
| `src/frontend/autotable/autotable-src.{c93fbb44.js,3f21032c.css}` | Pruned (replaced by the new hashes). |

Untouched (out of lane): backend (Bishop), tests (Vasquez),
`client-ui.ts`, `main-view.ts`, `setup.ts`, `lobby.ts`.

## Gate

- **TS strict** (`npx tsc --noEmit --strict --target es6 --moduleResolution bundler --esModuleInterop --lib DOM,DOM.Iterable,es6,es2017 src/index.ts`): exit 0
- **Parcel build:** succeeded, 7.75s, no warnings related to my changes (only an out-of-date browserslist notice on caniuse-lite which is pre-existing).
- **`dotnet test src/backend/Mahjong.Autotable.slnx`:** `Passed: 403, Failed: 0` — frontend changes don't touch the backend; +1 over the directive's baseline of 402 reflects Bishop's `361d805` commit on the same branch.

## Deferred / blockers

Nothing deferred — both tasks shipped.  No blockers for Bishop or
Vasquez:

- **Bishop:** no backend contract change.  The soft-reconnect path
  reuses the existing `?seat=` query that the backend already parses
  at `AutotableWsEndpoint.cs:174`.
- **Vasquez:** the Move button has a deterministic visibility rule
  (`connected && match===null`) that's easy to assert; the picker's
  per-option disabled state mirrors `client.seat` + `seatPlayers`,
  also deterministic.  The soft-reconnect path triggers a normal
  `client.disconnect()` → reconnect cycle, so existing reconnect
  coverage applies.

## Visual description (no screenshots — bundle-only delivery)

- **Sidebar before Deal, seated as Seat 0:**
  Game-ID row · ⏎ Move (full-width dark button, just above Leave-seat) ·
  Leave-seat row · setup-desc · …
- **Move button clicked:** a dark panel slides in beneath the button
  with a 2×3 grid of small buttons: `Seat 0` (greyed, current),
  `Seat 1` / `Seat 2` / `Seat 3` (primary blue if open, greyed if held
  by another player), `Spectate` (secondary grey, full-width on the
  last row).
- **Click `Seat 2`:** panel closes, status banner shows "Trying to
  reconnect…" briefly (~2 s), then HUD reappears in seat-2 first-person
  view.
- **Click `Spectate`:** panel closes, reconnects with `?seat=-1`,
  spectator pill appears next to `Game: <id>`, seat-buttons / Leave /
  Deal / claims / pickup HUD collapse, camera locks to top-down view
  (no seat-0 flash thanks to the `world.seat` initial-value tweak).
- **After Deal:** Move row vanishes (`refreshMoveSeatVisibility`
  observes `match.get(0) !== null` and sets `display: none`).
