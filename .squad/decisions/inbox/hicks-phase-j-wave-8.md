# Hicks — Phase J Wave 8 (frontend completion)

**Branch:** `stlong/phase-j-wave-8-completion`
**Bundle hashes (Parcel build at completion):**
- JS: `autotable-src.5d56642c.js` (1.23 MB)
- CSS: `autotable-src.df85b4c4.css` + `autotable-src.1a66bab2.css` + `autotable-src.6633d8fb.css`

## Scope shipped

This wave delivers the five Wave-8 frontend tracks called out in the
standing directive — all gated behind feature-detected endpoints so the
frontend ships safely whether or not Bishop's matching backend changes
are merged yet.

### 1. Authentication UI (`src/auth.ts`)

A new `auth.ts` module owns:
- Sign-in modal with three panels (OAuth provider list / email
  magic-link / "Auth coming soon" placeholder).
- Magic-link landing overlay triggered by `?auth=<token>` on the
  page URL.
- Top-right `auth-cluster` chip / sign-in / logout cluster.
- "Linked accounts" section of the Wave-7 profile page.

**Bishop endpoints consumed (all feature-detected — 404 → placeholder):**

| Method | Path                                  | Use                                                    |
|--------|---------------------------------------|--------------------------------------------------------|
| GET    | `/api/auth/providers`                 | initial feature detect; provider intersection          |
| GET    | `/api/auth/me`                        | session bootstrap + post-OAuth re-fetch                |
| POST   | `/api/auth/oauth/{provider}/start`    | returns `{ authorizeUrl }`; we navigate there          |
| POST   | `/api/auth/email/start`               | magic-link send                                        |
| GET/POST | `/api/auth/email/verify?token=…`    | called from the landing overlay                        |
| POST   | `/api/auth/link/{provider}`           | re-uses OAuth start with link intent                   |
| POST   | `/api/auth/unlink/{provider}`         | unlinks an identity                                    |
| POST   | `/api/auth/logout`                    | clears the cookie session                              |
| POST   | `/api/auth/dev-login`                 | **Development env only** — E2E shortcut                |

**LS keys:**
- `mahjong.auth.last-email.v1` — remembers the last email used so the
  modal pre-populates on revisits.
- `mahjong.auth.cache.v1` — best-effort cache of
  `{ authenticated, email, primaryProvider }` so the auth chip paints
  immediately on load. Always re-validated by `/api/auth/me`.

### 2. Rule preset selector + editor (`src/rule-presets.ts`)

- Lobby gets a "Rule preset" fieldset with a `<select>` and a
  "+ Create custom preset" link.
- Wave-7 settings drawer gains a new **Rule presets** tab. The editor
  exposes:
  - Name (custom presets only)
  - `handLimit` (integer)
  - `maxScorePerHand` (integer)
  - `allowWashout` (bool)
  - `allowKongRobbing` (bool)
  - `allowConcealedKongPromotion` (bool)
- Built-in `classic-changsha` is always present (read-only) even when
  Bishop's `/api/rule-presets` 404s.
- The lobby URL gains `&rulePreset=<id>` only when a non-builtin
  preset is selected.

**Bishop endpoints consumed:**

| Method | Path                          | Use                          |
|--------|-------------------------------|------------------------------|
| GET    | `/api/rule-presets`           | list (404 → builtin only)    |
| POST   | `/api/rule-presets`           | create (auth required)       |
| PUT    | `/api/rule-presets/{id}`      | update                       |
| DELETE | `/api/rule-presets/{id}`      | delete                       |

**LS key:** `mahjong.rule-preset.selected.v1` (last-chosen preset id).

### 3. Master bot tier

Three places gained a Master option:
- `#bot-difficulty` (game-ui top of-page select)
- `#settings-bot-strength` (Wave-2 settings drawer)
- `#lobby-bot-difficulty-fieldset` (lobby card group) — new testid
  `lobby-bot-difficulty-master`.

`BotDifficulty` / `BotStrength` unions in `lobby.ts` and `game-ui.ts`
now include `'Master'`. Per the standing graceful-degradation rule,
servers without the Master tier deployed will treat the value as Hard.

Tier tooltips were added across all three surfaces.

### 4. Spectator follow-seat (`src/spectator-follow.ts`)

A fixed bottom-right floating panel appears only when
`document.body.classList.contains('spectating')` is true (i.e.,
URL has `?seat=-1`). It exposes:
- Four "Seat N" buttons → poke `world.seat = 0..3`, switching the
  camera to that seat's POV.
- "Top-down" button → resets `world.seat = null`.
- "Show all hands" checkbox → toggles `body.spectator-show-all`. CSS
  removes peer-hand opacity. Canonical tile reveal still lives on the
  backend; this toggle is a best-effort local hint.
- Keyboard shortcuts: `1` / `2` / `3` / `4` follow seat, `0` / `Esc`
  return to top-down. Inert outside spectator mode and ignored when
  typing in inputs.

### 5. Reduced-motion + dark/light theme (`src/theme.ts`)

A new `theme.ts` module persists two prefs in a single LS blob
(`mahjong.display.v1`):
- `motion: 'auto' | 'reduced' | 'full'`
- `theme:  'auto' | 'light' | 'dark'`

The display panel (Wave-7 settings drawer) adds:
- `settings-motion-select`
- `settings-theme-select`

`installDisplayPreferences()` runs in `lobby.ts:initLobby()` before
the auth and rule-presets UIs, applies `body.reduced-motion`,
`body.full-motion`, `body.theme-light`, `body.theme-dark` based on
the user's pick **or** the OS media-query when set to Auto. A
`change` listener on both media queries re-paints the body classes
live (so flipping macOS dark mode while the page is open just works).

CSS additions:
- `@media (prefers-reduced-motion: reduce)` block + matching
  `body.reduced-motion` block neutralise transitions/animations
  page-wide. The 3D canvas (three.js Animation class) is untouched
  per scope.
- `body.theme-light` overrides chrome backgrounds + form-control
  palettes for lobby, settings drawers, profile page, sign-in modal,
  magic-link landing. Dark stays as the existing baseline.

## Graceful-degradation matrix

| Wave-8 feature                | Bishop endpoint                  | When 404                                                |
|-------------------------------|----------------------------------|---------------------------------------------------------|
| Sign-in modal providers       | `GET /api/auth/providers`        | shows "Auth coming soon" placeholder panel              |
| Auth chip / linked accounts   | `GET /api/auth/me`               | chip hidden; profile section shows "Sign in to link"    |
| Email magic-link              | `POST /api/auth/email/start`     | UI displays error text from response or generic message |
| Rule preset picker            | `GET /api/rule-presets`          | dropdown shows single Classic Changsha entry            |
| Rule preset save/delete       | `POST/PUT/DELETE`                | inline status row shows "Server doesn't support this"   |
| Master tier on backend        | (game runtime ignores value)     | server falls back to Hard                               |
| Spectator full reveal         | (no endpoint yet)                | `body.spectator-show-all` only — peer hand opacity      |

## Files changed

**Added (5 new modules):**
- `src/frontend/autotable-src/src/auth.ts`
- `src/frontend/autotable-src/src/rule-presets.ts`
- `src/frontend/autotable-src/src/spectator-follow.ts`
- `src/frontend/autotable-src/src/theme.ts`

**Modified:**
- `src/frontend/autotable-src/index.html` — auth cluster, signin
  modal, magic-link landing, profile-linked-accounts, lobby rule-preset
  fieldset, Master option in three difficulty selectors.
- `src/frontend/autotable-src/src/lobby.ts` — install hooks for auth /
  rule-presets / spectator-follow / display prefs; `BotDifficulty`
  union; `rulePreset` URL param emission.
- `src/frontend/autotable-src/src/settings-drawer.ts` — `'rule-presets'`
  in `SettingsTab`; new tab + panel; Motion + Theme selects in the
  Display tab.
- `src/frontend/autotable-src/src/game-ui.ts` — `BotStrength` union
  + JSON / URL validation widened to include `'Master'`.
- `src/frontend/autotable-src/src/style.css` — auth modal, magic-link
  landing, profile-linked-accounts, rule-preset editor, spectator
  follow panel, reduced-motion media + body-class rules, theme-light
  palette.
- `src/frontend/autotable-src/tests/selectors.md` — appended a Wave-8
  section documenting every new testid.

## Gates run

| Gate         | Command                                                                                                                                                          | Result |
|--------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------|--------|
| TypeScript   | `npx tsc --noEmit --strict --target es6 --moduleResolution bundler --esModuleInterop --lib DOM,DOM.Iterable,es6,es2017 src/index.ts`                             | clean  |
| Parcel build | `npx parcel build index.html --dist-dir ../autotable --public-url . --no-source-maps --no-cache`                                                                 | ✅ Built in 10.90s |
| Playwright   | `npx playwright test --list` (from `tests/e2e/`)                                                                                                                  | 36 tests in 7 files |

Playwright runtime requires the Docker container at
`localhost:8080/autotable/` (Apone owns spin-up). The spec set parses
cleanly post-change.

## Notes for sibling agents

- **Bishop** — When the auth endpoints land, the `availableProviders`
  intersection in `auth.ts` will pick them up automatically. If
  providers other than `google` / `github` / `email` need surfacing,
  edit `KNOWN_PROVIDERS` near line ~165. The `rule-preset.ownerId`
  field is used only to decide whether the editor is read-only; a
  null `ownerId` plus `isBuiltin === true` means it can't be edited
  or deleted.
- **Vasquez** — All Wave 8 testids are now documented in
  `tests/selectors.md`. The 5-rule contract at the bottom of the file
  still applies (testid is the only test-relevant attribute, etc.).
- **Apone** — `installDisplayPreferences()` runs before any other
  Wave-8 install hook so the chrome paints with the user's theme
  pick before the OAuth modal can flash. No reload-and-restart is
  needed when motion/theme picks change — listeners repaint live.

## Open follow-ups

1. **Linked-accounts unlink confirmation** — currently a plain
   `confirm()` dialog. If we move to a polished confirm dialog
   component later, the testids stay stable.
2. **Spectator show-all canonical reveal** — if Bishop adds a
   `spectatorReveal` WebSocket message we'll wire the toggle to it;
   the LS / body-class shim is forward-compatible.
3. **Theme tokens** — the theme-light palette currently uses ad-hoc
   selectors. If/when we adopt CSS custom properties for the whole
   chrome (the Wave-7 settings drawer already started this), the
   `body.theme-*` rules collapse to a single `:root` override block.
