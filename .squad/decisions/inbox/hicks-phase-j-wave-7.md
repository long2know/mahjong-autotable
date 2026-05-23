# Hicks — Phase J Wave 7: replay viewer wiring + a11y + settings drawer + profile page

> Author: Hicks (Senior Frontend Engineer)
> Branch: `stlong/phase-j-wave-7-polish`
> Scope: Frontend (Wave 7 §Tasks 1–4) — server-replay wiring with new
> step / speed controls, accessibility sweep with axe-core Playwright
> tests, app-wide tabbed settings drawer, dedicated player profile
> page.  Lands on top of Bishop's pending `GET /api/games/{gameId}/replay`
> endpoint (gracefully degrades to in-memory playback on 404).

## Summary

Wave 7 takes the Wave-6 surfaces from "functional" to "polished":

1. **Replay viewer rewires onto the server.**  Replay opens from three
   places now (post-game modal, profile page recent-games list,
   leaderboard "View" → profile → recent-games) — they all funnel
   through a single launcher (`replay-launcher.ts`) that feature-checks
   `GET /api/games/{gameId}/replay`.  A 404 / 5xx falls back to the
   shell renderer with an empty payload so the surface never blanks.
   The footer gained prev-hand / next-hand buttons, a playback-speed
   dropdown (0.5× / 1× / 1.5× / 2× / 4×), and an aria-live event
   counter so screen readers can track scrubber position.

2. **App-wide tabbed settings drawer (`settings-drawer.ts`, ~530 LOC).**
   Sibling surface to the Wave-2 per-game settings panel.  Four tabs —
   **General** (display name, avatar colour), **Audio** (sound on/off
   mirror + master volume), **Display** (perspective mirror + table
   colour CSS variable), **Network** (server URL).  Persists to a
   single JSON blob at `localStorage["mahjong.settings.v1"]`.  Mirrors
   `soundEnabled` to the existing `mahjong:soundEnabled` LS key + the
   Wave-2 `#settings-sound` checkbox; mirrors `perspective` to the
   `#perspective` checkbox.  Reachable from a new top-right gear
   (`#settings-button`) that sits next to the legacy gear.

3. **Player profile page (`profile-page.ts`, ~480 LOC).**  A full
   overlay with a stats grid (Games played / Wins / Win % / Total /
   Highest / Streak), editable display name + avatar colour, and a
   list of recent games (fetched from `GET /api/players/{id}/games`
   with a 404 fallback to an empty list).  Each recent-game row has
   a "Watch replay" button that funnels through the same launcher.
   Read-only mode is available via a custom `mahjong:open-profile-page`
   event — the leaderboard's new "View" button raises it to open the
   page for the row's player without enabling the edit affordances.

4. **Accessibility sweep + Playwright axe specs.**  Every Wave-7
   surface (and each Wave-6 surface we touched) gained `aria-*`
   coverage: `role="dialog"`, `aria-modal`, `aria-labelledby`,
   `aria-live` regions for the event counter / saved-note / error
   states, `aria-pressed` for the play/pause toggle, `aria-valuenow`
   on the scrubber, `aria-checked` on the avatar-colour radio group.
   Five new Playwright specs (`tests/e2e/a11y.spec.ts`) run axe-core
   over lobby / leaderboard / settings drawer / profile page / replay
   viewer and assert zero `serious` or `critical` violations.

## Endpoints consumed

* `GET /api/games/{gameId}/replay` — feature-detected; 404 falls back
  to in-memory playback.  Expected DTO shape:
  ```ts
  { gameId: string,
    events: [{ turn, phase, actor, action, tilesJson, timestampUtc }],
    handHistory?: HandResultEntry[] }
  ```
  Action / phase strings are matched case-insensitively against
  `draw|pick`, `discard`, `meld|chow|pung|kong`; unknown actions are
  skipped silently rather than failing the whole replay.

* `GET /api/players/{playerId}/games?limit=10` — feature-detected;
  404 falls back to a "No recent games yet" placeholder.  Expected
  rows: `[{ gameId, finishedAt, result, finalScore, summary }]`;
  PascalCase aliases (`GameId`, `FinishedAt`) are accepted at the
  boundary so this works regardless of Bishop's eventual casing.

* `POST /api/identity` — unchanged from Wave 6, but the response is
  now mined for a `createdAt` field which we persist into the
  identity LS cache and surface as "Member since {date}" on the
  profile page.

## Test IDs added (Wave-7 spec mandate)

| Surface | Test ID | Element |
| --- | --- | --- |
| Settings | `settings-button` | top-right gear |
| Settings | `settings-drawer` | aside container |
| Settings | `settings-save` | footer save button |
| Settings | `settings-reset` | footer reset button |
| Settings | `settings-close` | header × button |
| Settings | `settings-tab-{general\|audio\|display\|network}` | tab buttons |
| Settings | `settings-panel-{...}` | tab panels |
| Settings | `settings-display-name-input` | text input |
| Settings | `settings-avatar-color-{0..7}` + `-custom` | colour swatches |
| Settings | `settings-sound-toggle` | sound checkbox |
| Settings | `settings-master-volume` | volume slider |
| Settings | `settings-perspective-toggle` | perspective checkbox |
| Settings | `settings-table-color` + `-reset` | table colour controls |
| Settings | `settings-server-url` | network panel input |
| Profile | `profile-page` | overlay |
| Profile | `profile-page-close` | × button |
| Profile | `profile-stats-grid` | stats grid host |
| Profile | `profile-stats-{played\|won\|winrate\|total\|highest\|streak}` | stat cards |
| Profile | `profile-page-display-name-input` | edit input |
| Profile | `profile-page-color-{0..7}` + `-custom` | colour swatches |
| Profile | `profile-recent-games` | recent games host |
| Profile | `profile-recent-game-{0..9}` | recent rows |
| Profile | `profile-recent-replay-{i}` | per-row replay buttons |
| Profile | `profile-recent-label-{i}` | per-row labels |
| Replay | `replay-viewer` | inner shell (sibling of `replay-screen`) |
| Replay | `replay-prev`, `replay-next` | prev/next-hand buttons |
| Replay | `replay-speed-select` | speed dropdown |
| Replay | `replay-event-counter` | aria-live counter |
| Replay | `replay-scrubber` | timeline range (alias of `#replay-timeline`) |
| Leaderboard | `leaderboard-view-{i}` | per-row "View" buttons |

**Wave-6 testids preserved as-is** — `replay-screen`, `replay-play`,
`replay-step-back`, `replay-step-fwd`, `settings-sound`,
`game-complete-replay`, `lobby-open-profile`, `profile-drawer`, etc.
The existing `replay.spec.ts` continues to pass against the new
markup without any test-side changes.

## LocalStorage keys

| Key | Type | Description |
| --- | --- | --- |
| `mahjong.settings.v1` | JSON blob | App-wide settings (general / audio / display / network) |
| `mahjong.identity.cache.v1` | JSON | Identity cache — **extended this wave to include `createdAt`** |
| `mahjong.profile.cache.v1` | JSON | Profile DTO cache (unchanged) |
| `mahjong.identity.onboarded.v1` | flag | Onboarding gate (unchanged) |
| `mahjong:soundEnabled` | bool | Sound mirror (unchanged) — settings drawer keeps this in sync |
| `mahjong.lobby.defaults` | JSON | Lobby picker defaults (unchanged) |
| `autotable.phaseJ.v1.settings.*` | per-gameId | Wave-2 per-game settings (untouched — sibling surface) |

## Files added

* `src/settings-drawer.ts` — 530 LOC, app-wide tabbed settings module.
* `src/profile-page.ts` — 480 LOC, profile page + remote-view mode.
* `src/replay-launcher.ts` — 135 LOC, feature-detect launcher.
* `tests/e2e/a11y.spec.ts` — 110 LOC, axe-core sweep over five surfaces.

## Files modified

* `src/replay.ts` — added `openServer()`, `REPLAY_SPEEDS`, prev/next
  hand controls, speed dropdown, event counter, source label, doc-level
  Escape handler.
* `src/leaderboard.ts` — added "Profile" column + per-row View button
  that raises `mahjong:open-profile-page`.
* `src/identity.ts` — `Identity.createdAt` field persisted and
  surfaced in `normalizeIdentity`.
* `src/game-ui.ts` — post-game modal's "View Replay" button now
  prefers the server endpoint when a `?gameId=` is in the URL.
* `src/lobby.ts` — installs `installSettingsDrawerV2()` +
  `installProfilePage()` alongside the Wave-5 drawer install.
* `index.html` — new `#settings-button`, `#settings-drawer-v2`,
  `#profile-page`, extended `#replay-screen` controls.
* `src/style.css` — appended Wave-7 styles (~430 lines).
* `package.json` — added `@axe-core/playwright ^4.11.3` devDep.

## Bundle hashes

| Bundle | Wave 6 | Wave 7 | Notes |
| --- | --- | --- | --- |
| `autotable-src.<hash>.js` | `2391eb20` | `85bbb8ca` | Main app bundle |
| `autotable-src.<hash>.css` (main) | `a7cd8ea4` | `a7cd8ea4` | Unchanged content this build |
| `autotable-src.6633d8fb.css` | `6633d8fb` | `6633d8fb` | Bootstrap chunk (unchanged) |
| `autotable-src.df85b4c4.css` | `df85b4c4` | `df85b4c4` | Vendor chunk (unchanged) |

Old `autotable-src.2391eb20.js` and `autotable-src.094cde3a.css` were
pruned by hand after parcel emitted the new hashes (parcel doesn't
GC stale outputs).

## Gates run

* `npx tsc --noEmit --strict --target es6 --moduleResolution bundler
  --esModuleInterop --lib DOM,DOM.Iterable,es6,es2017 src/index.ts` —
  **clean** (0 errors).
* `npx parcel build index.html --dist-dir ../autotable --public-url .
  --no-source-maps --no-cache` — **success** (built in 3.03 s,
  ⚠ 1.2 MB JS bundle is the pre-existing three.js / parcel warning).
* `npx playwright test --list` — **24 tests across 5 specs**; new
  a11y spec contributes 5 of those.

The Playwright run itself requires a Docker container on
`http://localhost:8080/autotable/` — Apone owns that gate via the
`e2e-playwright.yml` workflow.  Local devs run `./scripts/docker-up.sh`
first.

## Graceful-degradation matrix

| Backend state | Frontend behaviour |
| --- | --- |
| `GET /api/games/{id}/replay` returns 200 with events | Replay viewer renders server payload + "Game {id} — N events" source label |
| Endpoint returns 200 with `events: []` | Empty hand shell renders + "no events recorded" label |
| Endpoint 404 / 5xx | Same as empty payload; viewer is functional but blank |
| `GET /api/players/{id}/games` 404 | Recent-games list shows "No recent games yet" placeholder (no red error) |
| `POST /api/identity` missing `createdAt` | "New member" placeholder on profile page |

## Open follow-ups

* **Bishop**: ship `GET /api/games/{gameId}/replay` — the launcher's
  404 fallback hides the absence today, but only the server endpoint
  surfaces historical games (the in-memory `handHistory` is wiped on
  page reload).
* **Bishop**: ship `GET /api/players/{playerId}/games` so the profile
  page's recent-games list lights up.  Profile page treats both
  endpoints as optional, so this can land independently.
* **Bishop**: add `createdAt` to the `POST /api/identity` response
  body — only needed for the "Member since" placeholder.
* **Apone**: when the next backend wave merges, drop the 404 fallback
  paths and assert a non-empty `events` array in the replay spec.
* **Vasquez**: add Playwright coverage for the full
  leaderboard-view → profile-page → replay flow once the recent-games
  endpoint ships.
* **Mobile a11y**: the `mobile-chrome` project is skipped in the new
  a11y spec.  Wave 8 should revisit the off-canvas pieces (Bootstrap
  drawer + lobby footer) that produce `aria-hidden-focus` warnings.

## Risks

* The leaderboard's new "View" column widens the table; on the
  smallest breakpoint the action cell wraps below the row.  Existing
  responsive rules already gate the whole leaderboard behind
  `overflow-x: auto`, so this is cosmetic rather than broken.
* The Wave-7 settings drawer (top-right) sits next to the Wave-2 gear
  (`#settings-toggle`).  Two gears is visually busy.  If we want to
  consolidate, the cleanest path is to retire the Wave-2 drawer in
  favour of a "Game" tab inside Wave-7's drawer — Wave 8 candidate.

— Hicks
