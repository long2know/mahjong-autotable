# Hicks — Phase J Wave 3 Memo
**Author:** Hicks (senior frontend engineer)
**Branch:** `stlong/phase-j-wave-3-completion`
**Commit:** `77855da` (frontend deliverables)
**Date:** Phase J Wave 3 close

## Scope delivered

Stephen's Wave 3 brief called for three parallel UI tracks; all three landed in a single commit:

1. **Sound effects** — Web Audio API synth, six events (draw / discard / claim / win / washout / gameComplete) wired into the game loop with settings toggle + URL override.
2. **2D replay viewer** — accessed from the end-of-game modal via a new "View Replay" button; per-hand timeline with play/pause/step/scrub controls.
3. **Canonical pattern display ordering** — chip strip on the result modal + move-log win row both sort patterns through the canonical Wave-3 ordering (HeavenlyHand → EarthlyHand → contextual → structural Big Wins → alphabetical).

## Key technical choices

### Synth-only sound (no CC0 assets)

| Option | Trade-off |
|---|---|
| Ship MP3/OGG assets | +50-300 kB bundle, Dockerfile asset-copy churn, CC0 licensing audit, attribution paperwork |
| **Web Audio API synth** ✅ | Zero binary assets, CC0-by-construction, no Dockerfile change, ~310 LOC self-contained module |

Picked synth — fastest to deliver, easiest to maintain, leaves room for future asset upgrades without touching the call-sites (the synth methods sit behind a `Sound.play(name)` API).

**Recipes** (in `src/sound.ts`):
- **clack** (draw/discard): white-noise burst + 800 Hz sine, 80 ms decay envelope.
- **chime** (claim): 660 / 880 Hz sine partials, 150 ms decay.
- **fanfare** (win): triangle wave arpeggio C5-E5-G5-C6, 50 ms note spacing.
- **washout**: sawtooth glissando 440 → 110 Hz over 600 ms (descending failure cue).
- **gameComplete**: rolled C-major chord (C4-E4-G4-C5) triangle waves, 300 ms.

**Autoplay unlock**: AudioContext is created lazily on first `click`/`touch`/`keydown`. Settings toggle (`#settings-sound`, default ON) calls `Sound.setMuted(!checked)`; URL override `?sound=on|off` also wired.

**Draw-sound throttling**: initial 13-tile deal collapses to one clack via a 200 ms minimum gap — without this the deal sounds like a typewriter on rapid-fire.

### 2D replay viewer (3D reuse deferred)

The live 3D scene is heavily coupled to active game state (`client.things`, dragging, collision physics). Replicating that into a "playback" mode would require either:
- A separate scene with its own asset/light/camera setup (~800-1500 LOC), OR
- A state-rewind layer over the live scene that wouldn't survive a back-to-lobby cycle.

Both estimates broke the Wave 3 budget. Built a **2D top-down DOM-based viewer** instead (`src/replay.ts`, ~640 LOC):
- Per-seat zones (4 quadrants) with tile glyph chips (unicode 🀙🀚 etc.).
- Captures tile transitions in real time from `client.things` (`hand.*` → draw, `discard.*` → discard, `meld.*` → meld) into a per-hand buffer.
- Flushes the in-progress hand on every `result.current` update; the closed hand becomes selectable in the dropdown.
- Server-pushed `handHistory` (from `gameComplete` payload) merged in `Replay.open()` with server results taking precedence over client-captured moves.
- Footer controls: step-back / play-pause / step-forward / timeline scrubber.

3D reuse is queued for a future polish wave. The 2D viewer is the entry point — once players prove they want richer replay, the 3D upgrade can layer over the same data buffer.

### Canonical pattern ordering — wired BOTH ways

**Hardcoded fallback** (`PATTERN_DISPLAY_ORDER` in `src/game-ui.ts`) matches Bishop's `ChangshaPatternOrdering` table 1:1:

| Slot | Pattern wire key | Notes |
|---|---|---|
| 1 | `heavenlyHand` | 天和 |
| 2 | `earthlyHand` | 地和 |
| 3 | `lastTileFromWall` | 海底捞月 |
| 4 | `lastDiscardCatch` | 河底捞鱼 |
| 5 | `kongReplacementWin` | 杠上开花 |
| 6 | `robbedKong` / `robbingKong` | 抢杠胡 (lives on `IsRobbedKong`) |
| 7 | `nineGates` | 九莲宝灯 — reserved |
| 8 | `nineTerminals` | 九幺 |
| 9 | `allPungs` | 碰碰胡 |
| 10 | `allConcealed` | 门前清 — reserved |
| 11 | `sevenPairs` | 七对子 |
| 12 | `selfDraw` | 自摸 — lives on `IsSelfDraw` |
| 13 | `singleWait` | 独张 — reserved |
| 100 | `fullFlush` | alphabetical tail |
| 101 | `standard` | alphabetical tail |

**Live wire upgrade** — `loadPatternOrderingFromApi()` fires a one-shot `fetch('api/changsha/pattern-ordering')` from `src/index.ts` at boot. On success, `setPatternDisplayOrder()` overwrites the in-process map with Bishop's canonical table. On failure (404 / offline / parse), the hardcoded list keeps rendering correctly.

Result: even if a future Wave adds a new pattern to Bishop's table, the frontend picks it up at next page-load without a code change.

### `WinResult.IsSelfDraw` + `IsKongReplacement` integration (Bishop's new bools)

Consumed in `move-log.ts`:

- **Prefix selection** prefers `winType === 'selfDraw'` (existing Wave I.2 path); when `winType` is missing falls back to Bishop's `isSelfDraw` bool, then to the `winType === 'discard'` heuristic.
- **`isKongReplacement` bool is informational** — the contextual verb selector already picks up `kongReplacementWin` from `AllPatterns`, so the bool is destructured but unused (kept available for any future move-log enrichment).

## File map

**New files:**
- `src/frontend/autotable-src/src/sound.ts` (~310 LOC) — synth sound manager.
- `src/frontend/autotable-src/src/replay.ts` (~640 LOC) — 2D replay viewer.
- `src/frontend/autotable-src/sounds/CREDITS.md` — synth-only license note + future-asset placeholder.

**Modified:**
- `src/frontend/autotable-src/src/game-ui.ts` — Sound + Replay wiring, `PATTERN_DISPLAY_ORDER`, `comparePatterns`/`sortPatterns`/`loadPatternOrderingFromApi`/`setPatternDisplayOrder` exports, `SettingsState.sound` field, `?sound=on|off` URL override, result-modal chip-strip sort.
- `src/frontend/autotable-src/src/move-log.ts` — imports `comparePatterns`, sorts Hu-row patterns through canonical order, consumes `WinResult.IsSelfDraw`/`IsKongReplacement` bools.
- `src/frontend/autotable-src/src/index.ts` — fire-and-forget `loadPatternOrderingFromApi()` at boot.
- `src/frontend/autotable-src/src/style.css` — ~215 LOC appended for `.replay-screen`, `.replay-shell`, `.replay-header`, `.replay-board`, `.replay-seat`, `.replay-tile-chip`, `.replay-footer`, `.replay-timeline`, mobile media query.
- `src/frontend/autotable-src/index.html` — `#settings-sound` checkbox row, `#game-complete-replay` button, `#replay-screen` overlay container.

**Bundle:**
- New: `autotable-src.330c36fd.js` + `autotable-src.f8d8d79e.css`
- Pruned: `autotable-src.90818e21.js` + `autotable-src.60a1fda4.css`

## Gates

| Gate | Result |
|---|---|
| TypeScript strict | **0 errors** |
| Parcel build | **succeeds** (4.29s) |
| Backend tests | **424 passed / 0 failed / 0 skipped** (Vasquez `d7c5337`) |

## Coordination

- **Bishop (`9235859 → 75baecc → 2e84179`)** — three contract surfaces consumed:
  - `GET /api/changsha/pattern-ordering` → fetched at boot via `loadPatternOrderingFromApi()`.
  - `WinResult.IsSelfDraw` → fallback for `winType` in move-log prefix selection.
  - `WinResult.IsKongReplacement` → destructured (informational; verb selector uses `AllPatterns`).
  - **No regression risk** — every consumer falls back gracefully on a pre-W3 payload.
- **Apone (`ea2c991`)** — single-image Docker landed. **No Dockerfile change required** for Wave 3 — synth-only sounds ship zero asset files, the existing `COPY src/frontend/autotable/ → wwwroot/autotable` bundle copy holds.
- **Vasquez (`d7c5337`)** — new DOM ids available for future selectors:
  - `#replay-screen` — overlay root
  - `#settings-sound` — sound toggle checkbox
  - `#game-complete-replay` — "View Replay" button in end-of-game modal
- **Stephen (PM)** — recommend Wave 4 follow-ups:
  - Add unit test for `loadPatternOrderingFromApi()` (mock fetch, assert order takes effect).
  - Add Playwright smoke for sound toggle + replay viewer open/close.
  - Consider 3D replay layer once 2D viewer has player feedback.

## Out of scope / deferred

- **3D replay scene** — deferred; 2D viewer ships first to validate the feature.
- **CC0 sound asset library** — not needed for Wave 3; placeholder in `sounds/CREDITS.md` for future swap.
- **Replay export / share** — no URL / clipboard / download yet.
- **Move-log integration of `IsKongReplacement` bool** — bool consumed but informational; verb selector already covers it via `AllPatterns`.

## Bundle hashes for verification

```
autotable-src.330c36fd.js   1.08 MB
autotable-src.f8d8d79e.css  25.27 kB
```

Both referenced in `src/frontend/autotable/index.html`; stale `60a1fda4.css` + `90818e21.js` removed from the working tree (parcel-renames recorded in the commit).
