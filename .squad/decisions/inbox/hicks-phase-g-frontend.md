# Phase G Frontend — Sidebar Lobby UI (Hicks)

**Branch:** `stlong/phase-g-bot-scheduler-lobby`
**Bundle SHA:** `autotable-src.33f97fad.js` (1.03 MB) + `autotable-src.7934372e.css` (7.8 kB)
**Replaces:** `autotable-src.6d5fae4c.js` + `autotable-src.1c6f6789.css`

## What shipped

Path-1 sidebar lobby (plain TS + HTML + CSS, no React, inside the existing
autotable bundle) so users can pick variant / dealMode / botCount /
botDifficulty without editing the URL bar.

Anchored top-left, semi-opaque dark panel with brass-gold accents matching
the rest of the autotable chrome. Visible by default on a bare URL
(`/autotable/`); otherwise hidden behind a top-left "☰ Lobby" toggle.

## Files

| File | Change |
|---|---|
| `src/frontend/autotable-src/src/lobby.ts` | **NEW** — 200 LOC module: URL parsing, picker state read/write, gating, show/hide, Apply & Start navigation |
| `src/frontend/autotable-src/index.html` | Added `#lobby-toggle` button + `#lobby-panel` markup with four `<fieldset>`s (radio buttons) |
| `src/frontend/autotable-src/src/index.ts` | Added `initLobby()` call before `assetLoader.loadAll()` (lobby is independent of Game lifecycle) |
| `src/frontend/autotable-src/src/style.css` | +135 LOC of `#lobby-*` styling, `body.lobby-active` toggle suppression, `.lobby-disabled` greyed-out state |
| `src/frontend/autotable/**` | Parcel rebuild — new hashed JS + CSS, stale `6d5fae4c.js` / `1c6f6789.css` pruned |

No `world.ts`, no `client.ts`, no setup pipeline, no backend, no tests touched.

## Picker → query-param mapping

The lobby is a one-way bridge into the existing Phase F query-param
backend. Apply & Start builds the URL and calls `window.location.replace`;
the rest of the system reads its existing URL params unchanged.

| Lobby picker | Values (default **bold**) | URL emitted | Notes |
|---|---|---|---|
| Variant | `changsha` (**bold**), `four-player`, `three-player`, `bamboo`, `minefield` | `?variant=changsha` (kebab-case) | Accepts SCREAMING_SNAKE on read for back-compat with `game-ui.ts`'s parser |
| Deal mode | `manual` (**bold**), `auto` | `&dealMode=manual` | **Only emitted for `variant=changsha`** — Riichi variants ignore it, so we keep the URL tidy |
| Bot count | `0`, `3` (**bold**), `4` (spectator) | `&botCount=3` | Default of 3 matches the Phase F backend default. `4` shows "(spectator)" hint since seat 0 is also a bot |
| Bot difficulty | `Easy`, `Medium` (**bold**), `Hard` | `&botDifficulty=Medium` | PascalCase to match `AutotableConnection.BotDifficulty`. **Only emitted when `botCount > 0`** |

### Gating logic (visual + functional)

- `dealMode` fieldset → `.lobby-disabled` greyed + `disabled` on radios when `variant !== 'changsha'`
- `botDifficulty` fieldset → same treatment when `botCount === 0`

Refresh fires on any variant or bot-count change.

## URL parsing

Re-uses the same lenient parser strategy as `game-ui.ts`:

- Variant accepts kebab-case (`changsha`, `four-player`) or
  SCREAMING_SNAKE (`FOUR_PLAYER`) — lowercase + `_→-` normalisation
- `?bots=true` aliases `?botCount=3` (Phase F back-compat)
- Bot difficulty case-insensitive on read; always emitted PascalCase on write

## Show-on-load policy

Lobby auto-opens when `window.location.search === ''` (bare URL only).
Once any param has been applied, subsequent loads go straight into the
game and the lobby is reached only via the toggle.

`window.location.replace()` (not `assign`) so the browser back-button
doesn't bounce the user between game configurations.

## Deferrals (V2 / Phase H)

- **Soft hot-swap** — currently always full page reload. Mid-session
  variant / bot-count swap would require disposing the setup pipeline's
  tile catalogue cleanly; out of Phase G scope.
- **localStorage persistence of lobby pickers** — Phase F's
  `autotable.phaseF.v1.*` keys still work for `game-ui.ts`'s in-game
  pickers; the lobby reads URL only by design (URL is the source of
  truth; localStorage is a stale convenience cache).
- **Multi-human lobby** ("create / join by code", nicknames) — single-
  game-per-instance is the Wave-3 + Phase F assumption.
- **Mobile responsive layout** — panel is 320 px fixed-width; fine for
  desktop / tablet.

## Verification

- `npx tsc --noEmit --strict --target es6 --moduleResolution bundler --esModuleInterop --lib DOM,DOM.Iterable,es6,es2017 src/index.ts` → exit 0.
- `npx parcel build index.html about.html --public-url . --no-source-maps --dist-dir ../autotable` → ✨ Built in 7.32 s, 22 assets.
- `src/frontend/autotable/index.html` references `autotable-src.33f97fad.js` and `autotable-src.7934372e.css` (verified with `grep -o`).
- Stale `6d5fae4c.js` and `1c6f6789.css` removed from dist.

## Smoke recipe

1. Browse to `/autotable/` (bare, no query string). Lobby auto-opens.
2. Pick Changsha + Manual + 3 bots + Medium → click **Apply & Start**.
3. URL becomes `/autotable/?variant=changsha&dealMode=manual&botCount=3&botDifficulty=Medium`. Fresh game starts.
4. Click top-left **☰ Lobby** button to re-open without losing the running game until Apply is clicked.
5. Switch variant to Riichi 4p → dealMode fieldset greys out. Switch bots to 0 → bot-difficulty fieldset greys out.
