# Frontend DOM selectors

This document is the **source of truth** for stable `data-testid` selectors on
the Changsha Mahjong frontend. Future Playwright / Cypress integration tests
(scheduled for a post-Phase-J phase) target these IDs, so:

- Hicks-managed code MUST keep these selectors stable across renames /
  refactors.
- Adding a new testid to the contract means appending here in the same PR.
- Removing or renaming a testid is a breaking change — open a coordinated
  Hicks ↔ Vasquez memo before doing it.

Format conventions (Phase J Wave 5):

- All testids are **kebab-case**, prefixed with their surface
  (`lobby-`, `connection-banner-`, `hud-`, …) so a CSS selector
  `[data-testid^="lobby-"]` finds an entire surface.
- Dynamic / indexed testids carry the `{0..N}` suffix in this doc; at
  runtime they are filled in by template-literal interpolation in TS
  (e.g., `` `lobby-player-chip-${i}` ``).
- Each entry cites the file + line of origin so a future grep keeps the
  doc in sync.

> **Maintenance note.** The catalog is captured as of Phase J Wave 5
> (Hicks's working-tree commit `feat(frontend): phase j wave 5 — public
> matchmaking lobby + profile drawer + stats panel`). When you change a
> testid:
> 1. Update the citation lines below.
> 2. Bump the relevant section's "Phase" annotation.
> 3. Run `grep -rn 'data-testid' src/frontend/autotable-src/` to detect
>    drift before commit.

---

## Lobby

Pre-game seat selection / rule-set picker. The lobby is hidden by
`body.lobby-active` while a game is in flight; the toggle button in
the top-left re-opens it for new-game configuration.

| Selector | Element | Purpose | Source |
|---|---|---|---|
| `data-testid="lobby-toggle"` | `<button id="lobby-toggle">` | Top-left hamburger that opens / closes the lobby panel mid-session. | `src/frontend/autotable-src/index.html:429` |
| `data-testid="lobby-players-section"` | `<section>` | Wrapper around the joined-players strip; visible in the lobby panel header. | `src/frontend/autotable-src/index.html:693` |
| `data-testid="lobby-players-strip"` | `<div>` | Horizontal flex container holding one chip per joined player. | `src/frontend/autotable-src/index.html:697` |
| `data-testid="lobby-players-empty"` | `<div>` | Placeholder rendered inside `lobby-players-strip` when no humans have joined yet. Dynamically injected. | `src/frontend/autotable-src/src/lobby.ts:707, 726` |
| `data-testid="lobby-player-chip-{0..3}"` | `<div class="lobby-player-chip">` | Per-seat joined-player chip. `chipIndex` is the seat the player took (0=East, 1=South, 2=West, 3=North). Dynamically injected; `data-seat` attribute carries the same seat number for non-test selectors. | `src/frontend/autotable-src/src/lobby.ts:802` |
| `data-testid="lobby-seat-preview"` | `<div class="lobby-seat-preview">` | The 4-cell seat-grid preview shown above the lobby's apply button. | `src/frontend/autotable-src/index.html:705` |
| `data-testid="lobby-seat-preview-{0..3}"` | `<div class="lobby-seat-preview-cell">` | One preview cell per seat. Reflects the current rule-set's bot-mix preview state. Dynamically injected. | `src/frontend/autotable-src/src/lobby.ts:668` |
| `data-testid="lobby-quick-match"` | `<button>` | "Quick Match" CTA that bypasses fine-grained picker and starts a default 4-bot game. | `src/frontend/autotable-src/index.html:720` |
| `data-testid="lobby-variant-fieldset"` | `<fieldset>` | Rule-set variant picker (e.g., changsha-v1, changsha-v2). | `src/frontend/autotable-src/index.html:731` |
| `data-testid="lobby-bot-difficulty-fieldset"` | `<fieldset>` | Bot strength tier picker (Easy / Medium / Hard). | `src/frontend/autotable-src/index.html:775` |
| `data-testid="lobby-hand-count-fieldset"` | `<fieldset>` | N-hand cap picker. Defaults to 4 (one east-wind rotation); higher values raise `ChangshaGameState.MaxHands`. | `src/frontend/autotable-src/index.html:788` |
| `data-testid="lobby-open-settings"` | `<button>` | Opens the in-game settings drawer (top-right slide-out) without leaving the lobby. | `src/frontend/autotable-src/index.html:803` |
| `data-testid="lobby-apply"` | `<button>` | Primary CTA — "Apply" / "Start Game". Submits the lobby form and constructs the new-game URL with the chosen variant / difficulty / hands. | `src/frontend/autotable-src/index.html:838` |

## Mobile drawers

Phase J Wave 4 — under the 768px breakpoint the move-log slides off-canvas;
the toggle button reveals it as a drawer. Hidden on desktop.

| Selector | Element | Purpose | Source |
|---|---|---|---|
| `data-testid="mobile-move-log-toggle"` | `<button id="move-log-toggle">` | Hamburger that opens / closes the move-log drawer on mobile widths. | `src/frontend/autotable-src/index.html:445` |

## Reconnect / disconnect banner

Phase J Wave 2 introduced the banner; Phase J Wave 4 added the
copy-rejoin-link button and the toast region. The banner is anchored at
the top-center of the viewport and uses `display: none` until the
WS-reconnect lifecycle surfaces.

| Selector | Element | Purpose | Source |
|---|---|---|---|
| `data-testid="connection-banner"` | `<div id="connection-banner">` | Root of the reconnect banner. State-classes (`.connecting`, `.failed`, `.recovered`) control color. | `src/frontend/autotable-src/index.html:464` |
| `data-testid="connection-banner-retry"` | `<button id="connection-banner-retry">` | Manual-retry button. Triggers an immediate reconnect attempt outside the exponential-backoff loop. | `src/frontend/autotable-src/index.html:470` |
| `data-testid="reconnect-copy-link"` | `<button id="connection-banner-copy-link">` | **Phase J Wave 4.** Revealed after the first failed retry. Copies a `?rejoin=<token>` URL to the clipboard so the player can resume the session in a fresh browser tab. Token shape: base64url-encoded `{ v, gameId, playerId, seat, savedAt }` (see `src/reconnect.ts`). Falls back to `document.execCommand` when `navigator.clipboard` is unavailable. | `src/frontend/autotable-src/index.html:480` |
| `data-testid="connection-banner-lobby"` | `<button id="connection-banner-lobby">` | Bail-out button — closes the banner and returns to the lobby form. | `src/frontend/autotable-src/index.html:486` |
| `data-testid="toast-region"` | `<div id="toast-region">` | **Phase J Wave 4.** Bottom-center transient-notification region (rejoin-failed, copy-link-confirmed, etc.). Toasts auto-hide after a short interval. | `src/frontend/autotable-src/index.html:495` |

## In-game HUD *(reserved — not yet shipped)*

Hicks's in-game HUD (discard button, claim chips, draw indicator) is not
yet `data-testid`-instrumented. When Wave 5 / Wave 6 adds those surfaces,
append them under this section with the `hud-` prefix:

- `data-testid="hud-discard-button"` — discard the highlighted tile.
- `data-testid="hud-claim-chip-{type}"` — claim CTA per type
  (`pung` / `kong` / `chow` / `win`).
- `data-testid="hud-draw-button"` — manual-draw button (manual-pickup mode).

**Do NOT** add these to production code until the contract entry exists
here — empty placeholders give a false signal to the integration suite.

## Result modal *(reserved — not yet shipped)*

Hicks's end-of-hand and end-of-game modals will need stable selectors so
the integration suite can assert win-pattern chip rendering against the
`/api/changsha/pattern-ordering` contract pinned by
`PatternOrderingEndpointTests`. Proposed surface:

- `data-testid="result-modal"` — modal root.
- `data-testid="result-modal-pattern-chip-{wireName}"` — one chip per
  entry in `winResult.allPatterns`. The `{wireName}` segment matches the
  camelCase keys returned by `/api/changsha/pattern-ordering` (e.g.,
  `heavenlyHand`, `allPungs`, `sevenPairs`, `fullFlush`). Letting Cypress
  assert `chips[0]` is `heavenlyHand` and `chips[n]` is `standard`
  validates the wire-ordering contract end-to-end.
- `data-testid="result-modal-final-scores"` — score-board container.
- `data-testid="result-modal-final-scores-row-{0..3}"` — per-seat score
  row.

## Game-over summary modal *(reserved — pairs with `GameCompleted` SignalR event)*

Fires once per game (asserted by
`GameCompletionLifecycleTests.GameCompletedEvent_Fires_OnceOnly`) when
`IsGameComplete` flips true. Proposed surface for Hicks's end-of-game
modal:

- `data-testid="game-over-modal"` — root.
- `data-testid="game-over-winner-seat"` — winner display (seat + score).
- `data-testid="game-over-final-scores"` — full-seat breakdown.
- `data-testid="game-over-rematch"` — start-new-game CTA.

## Public matchmaking lobby *(Phase J Wave 5)*

Hicks's `src/matchmaking.ts` poll loop (5s cadence — see
`MATCHMAKING_POLL_MS`) reads `GET /api/matchmaking/lobby` and emits
`update` events with the `PublicGame` array. Phase J Wave 5 — Hicks's
`installPublicGamesPane()` in `lobby.ts` now mounts the list host +
per-card chips so every testid in the table below resolves at runtime
when the "Public Games" tab is active.

| Selector | Element | Purpose | Source |
|---|---|---|---|
| `data-testid="lobby-my-game-tab"` | `<button class="lobby-tab">` | "My Game" tab — re-shows the existing Quick-Match + seat picker pane. | `src/frontend/autotable-src/index.html:694` |
| `data-testid="lobby-public-games-tab"` | `<button class="lobby-tab">` | "Public Games" tab — toggles the matchmaking browser pane and starts/stops the 5 s matchmaking poll loop. | `src/frontend/autotable-src/index.html:699` |
| `data-testid="lobby-public-section"` | `<section id="lobby-tab-public-games">` | Wrapper around the "Public games" list — only visible while the poll loop is active. | `src/frontend/autotable-src/index.html:917` |
| `data-testid="lobby-public-list"` | `<div class="public-games-list">` | Container holding the per-game chips; one child per `PublicGame` entry from the poll cache. | `src/frontend/autotable-src/index.html:937` |
| `data-testid="lobby-public-game-{0..49}"` | `<div class="public-game-card">` | One chip per public game. Index is the lobby cache position (newest-first, capped at `MAX_PUBLIC_GAMES_RENDERED = 50`). | `src/frontend/autotable-src/src/lobby.ts:1016` |
| `data-testid="lobby-public-game-name-{0..49}"` | `<div class="public-game-card-name">` | The friendly host-supplied `publicName` (≤64 chars per `ChangshaGameRuntime.SetGamePublicAsync`); falls back to `"<host>'s game"` when null. | `src/frontend/autotable-src/src/lobby.ts:1022` |
| `data-testid="lobby-public-game-host-{0..49}"` | `<span class="public-game-card-meta-creator">` | The host's `creatorDisplayName` (resolved through `PlayerProfileService`). | `src/frontend/autotable-src/src/lobby.ts:1030` |
| `data-testid="lobby-public-game-seats-{0..49}"` | `<span class="public-game-card-meta-seats">` | The `seatedCount / maxSeats` text. The wire shape is Bishop's `LobbyGameDto` (`seatedCount` + `maxSeats`); `matchmaking.ts:normalizePublicGame` consumes those keys directly. | `src/frontend/autotable-src/src/lobby.ts:1034` |
| `data-testid="lobby-public-game-join-{0..49}"` | `<button class="public-game-card-join">` | Per-chip "Join" CTA — calls `navigateToGame(gameId)` which rewrites the URL and reloads into the chosen game. Disabled when `seatedCount === maxSeats`. | `src/frontend/autotable-src/src/lobby.ts:1051` |
| `data-testid="lobby-join-random"` | `<button id="lobby-join-random">` | "Join any public game" shortcut — invokes the SignalR `JoinRandom` RPC (`MatchmakingService.JoinRandomAsync` picks a random public-seating game). | `src/frontend/autotable-src/index.html:923` |
| `data-testid="lobby-set-public-toggle"` | `<input type="checkbox" id="lobby-make-public-toggle">` | Host-only checkbox in the lobby that flips `SetGamePublic`. Sender must be the host of the current `?gameId=…`. | `src/frontend/autotable-src/index.html:884` |
| `data-testid="lobby-public-name-input"` | `<input id="lobby-make-public-name">` | Friendly public-name input bound to the `SetGamePublic` `publicName` argument. Server trims + caps at 64 chars; blank is sent as `null`. | `src/frontend/autotable-src/index.html:889` |

> **Wire contract reminder.** Each `PublicGame` entry from
> `/api/matchmaking/lobby` is `{ gameId, publicName, creatorDisplayName,
> seatedCount, maxSeats, variant, createdAt }`.
> `matchmaking.ts:normalizePublicGame` consumes the wire shape as-is
> (post-Wave-5 alignment with Bishop's `LobbyGameDto`). Backend
> wire-shape assertions live in `MatchmakingLobbyEndpointTests`.

## Profile drawer *(Phase J Wave 5)*

Hicks's `src/profile.ts` mounts the per-player profile drawer (display
name + avatar colour edit + preview). The drawer surfaces a single
`data-testid` (the colour-preset buttons) plus a stable family of
DOM `id="..."` selectors — both are documented here because the
Wave 5 Playwright suite uses the `id` selectors directly (no
`data-testid` indirection) for the drawer-open/close + name-validation
flows.

| Selector | Element | Purpose | Source |
|---|---|---|---|
| `data-testid="profile-avatar-color-preset-{0..N}"` | `<button class="profile-avatar-preset">` | Colour-preset buttons in the avatar picker. Index reflects palette position. | `src/frontend/autotable-src/src/profile.ts:470` |
| `id="profile-drawer"` | `<aside>` / `<div>` | Drawer root — toggled visible via `profile-drawer-open` class. | `src/frontend/autotable-src/src/profile.ts:447, 586, 597` |
| `id="profile-drawer-close"` | `<button>` | Close-drawer CTA. | `src/frontend/autotable-src/src/profile.ts:451` |
| `id="profile-display-name-input"` | `<input type="text">` | Name editor; service rejects empty / >32 chars / leading-trailing whitespace (`PlayerProfileService.UpdateDisplayNameAsync`). | `src/frontend/autotable-src/src/profile.ts:452, 590` |
| `id="profile-display-name-error"` | `<span>` / `<div>` | Inline validation message; populated when `UpdateProfile` raises a `HubException` (translated from `ArgumentException`). | `src/frontend/autotable-src/src/profile.ts:453` |
| `id="profile-avatar-presets"` | `<div>` | Host element for the 16-entry colour-preset grid. | `src/frontend/autotable-src/src/profile.ts:454` |
| `id="profile-avatar-color-custom"` | `<input type="color">` | Free-form `#RRGGBB` picker — service-validated via the `^#[0-9A-Fa-f]{6}$` regex. | `src/frontend/autotable-src/src/profile.ts:455` |
| `id="profile-preview-avatar"` | `<div>` | Live preview chip — colour reflects the in-flight edits before save. | `src/frontend/autotable-src/src/profile.ts:456` |
| `id="profile-preview-name"` | `<span>` | Live preview of the in-flight display-name string. | `src/frontend/autotable-src/src/profile.ts:457` |
| `id="profile-save"` | `<button>` | Commit edits via the `UpdateProfile` hub RPC. Disabled while validation fails. | `src/frontend/autotable-src/src/profile.ts:458` |
| `id="profile-reset"` | `<button>` | Reset edits to the server-side current value. | `src/frontend/autotable-src/src/profile.ts:459` |
| `id="profile-saved-note"` | `<span>` | "Saved ✓" toast shown after a successful `UpdateProfile`. | `src/frontend/autotable-src/src/profile.ts:460` |
| `id="lobby-open-profile-avatar"` | `<div>` | Lobby header avatar chip — clicking opens the drawer. Reflects the current profile colour. | `src/frontend/autotable-src/src/profile.ts:612` |
| `id="lobby-open-profile-label"` | `<span>` | Display-name label next to the lobby header avatar. | `src/frontend/autotable-src/src/profile.ts:613` |
| `data-testid="lobby-open-profile"` | `<button id="lobby-open-profile">` | Open-profile shortcut button rendered in the lobby footer settings row. Mirrors the profile-toggle on-canvas chip but lives in the lobby's settings-shortcut strip so it remains reachable while no game is active. | `src/frontend/autotable-src/index.html:967` |
| `data-testid="profile-drawer"` | `<aside id="profile-drawer">` | Supplemental testid on the drawer root for Wave 5 Playwright selectors that prefer `data-testid` over `id`. The drawer's `id` remains the contract anchor (it doubles as `aria-controls` / `aria-labelledby` target). | `src/frontend/autotable-src/index.html:997` |
| `data-testid="profile-display-name-input"` | `<input id="profile-display-name-input">` | Supplemental testid on the display-name editor. | `src/frontend/autotable-src/index.html:1014` |
| `data-testid="profile-avatar-color-custom"` | `<input id="profile-avatar-color-custom">` | Supplemental testid on the free-form colour picker. | `src/frontend/autotable-src/index.html:1031` |
| `data-testid="profile-save"` | `<button id="profile-save">` | Supplemental testid on the Save button. | `src/frontend/autotable-src/index.html:1040` |
| `data-testid="profile-reset"` | `<button id="profile-reset">` | Supplemental testid on the Reset-to-default button. | `src/frontend/autotable-src/index.html:1043` |

> **Drawer-id vs. testid policy.** Wave 5 Hicks made a deliberate choice
> to use plain DOM `id` selectors (instead of `data-testid`) for the
> profile drawer because the drawer is also referenced from inline
> `aria-controls` / `aria-labelledby` attributes that themselves require
> `id` (not `data-testid`). Vasquez accepts this exception — the `id`
> family above is treated as contract-grade for Wave 5+ tests. A future
> wave may unify on `data-testid` if accessibility tooling lands a
> first-class `aria-controls`/`testid` bridge.

## Player stats panel *(Phase J Wave 5)*

Hicks's `src/stats.ts` renders the player stats grid in two surfaces:
the lobby's per-player stats card (`#lobby-stats-panel` host) and the
post-game modal's "Your stats" delta section. The shared `STATS_TESTIDS`
constant is the source of truth; both surfaces consume the same builder
function (`buildPanel`) so a single fact-set covers both renders.

| Selector | Element | Purpose | Source |
|---|---|---|---|
| `data-testid="lobby-stats-panel"` | `<div id="lobby-stats-panel">` | Host element for the lobby's "Your stats" card. Populated by `lobby.ts:renderLobbyStatsPanel` whenever the profile cache updates. | `src/frontend/autotable-src/index.html:903` |
| `data-testid="stats-panel"` | `<div class="stats-grid">` | Root grid container. Carries the panel testid so callers can scope-query into the rows. | `src/frontend/autotable-src/src/stats.ts:17, 137` |
| `data-testid="stats-games-played"` | `<span>` (value cell) | `PlayerStats.GamesPlayed` integer. Delta span (when shown) carries the change vs the pre-game snapshot. | `src/frontend/autotable-src/src/stats.ts:19, 64` |
| `data-testid="stats-games-won"` | `<span>` (value cell) | `PlayerStats.GamesWon` integer. | `src/frontend/autotable-src/src/stats.ts:20, 71` |
| `data-testid="stats-win-rate"` | `<span>` (value cell) | `gamesWon / gamesPlayed * 100`, 1-decimal-place pct (e.g. `42.9%`); `—` when no games yet. | `src/frontend/autotable-src/src/stats.ts:21, 78` |
| `data-testid="stats-longest-streak"` | `<span>` (value cell) | `PlayerStats.LongestWinStreak` integer (all-time best consecutive-win run). | `src/frontend/autotable-src/src/stats.ts:22, 88` |
| `data-testid="stats-current-streak"` | `<span>` (value cell) | `PlayerStats.CurrentWinStreak` integer (resets to 0 on any loss). | `src/frontend/autotable-src/src/stats.ts:23, 95` |
| `data-testid="stats-highest-score"` | `<span>` (value cell) | `PlayerStats.HighestSingleGameScore` integer (largest positive `finalScores[me]` ever recorded). | `src/frontend/autotable-src/src/stats.ts:24, 102` |

> **Backend contract.** The six counters above are populated by
> `PlayerProfileService.RecordGameCompletedAsync` (one call per
> game-completed transition; bot-prefixed player ids are skipped).
> Counter semantics, including the win-streak reset on loss + the bot
> filter, are pinned by Vasquez's `PlayerStatsAggregationTests`.

## Onboarding *(Phase J Wave 6)*

Hicks's `src/identity.ts` mounts the first-visit onboarding card: the
DOM block lives inline in `index.html` and stays
`style="display: none"` until the bootstrap detects a missing
`mahjong_pid` cookie (Bishop's persistent-id contract — see
`PersistentPlayerIdTests`). The card collects a display name and avatar
colour, then POSTs to `/api/identity` and lets the backend mint the
cookie. The Onboarding flow shares the avatar-preset palette pattern
with the Wave 5 Profile drawer; the only static testids live on the
card root + form controls, while the colour-preset buttons carry a
templated testid for indexed access.

| Selector | Element | Purpose | Source |
|---|---|---|---|
| `data-testid="onboarding-card"` | `<section id="onboarding-card">` | Card root. Toggled from `display: none` → visible by `identity.ts:openOnboardingCard` after the first-visit cookie check. | `src/frontend/autotable-src/index.html:724` |
| `data-testid="onboarding-display-name-input"` | `<input id="onboarding-display-name-input" type="text">` | Display-name editor. Backend rejects empty / >32 chars / leading-trailing whitespace (mirrors `PlayerProfileService.UpdateDisplayNameAsync`). | `src/frontend/autotable-src/index.html:741` |
| `id="onboarding-display-name-error"` | `<span>` | Inline validation message; populated when the name fails the same rules the server applies (so the user gets feedback before the POST). | `src/frontend/autotable-src/src/identity.ts:305` |
| `id="onboarding-avatar-presets"` | `<div role="radiogroup">` | Host for the colour-preset buttons (one `<button>` per palette entry). Built by `identity.ts:renderAvatarPresets`. | `src/frontend/autotable-src/src/identity.ts:306` |
| `data-testid="onboarding-avatar-color-preset-{0..N}"` | `<button class="onboarding-avatar-preset">` | Colour-preset buttons in the avatar picker; index reflects palette position (same indexing convention as the Profile drawer's `profile-avatar-color-preset-{idx}`). | `src/frontend/autotable-src/src/identity.ts:346` |
| `data-testid="onboarding-avatar-color-custom"` | `<input id="onboarding-avatar-color-custom" type="color">` | Free-form `#RRGGBB` picker — same validation surface as the Profile drawer's custom-colour input. | `src/frontend/autotable-src/index.html:758` |
| `id="onboarding-preview-avatar"` | `<div>` | Live avatar preview; the chip's background colour reflects the in-flight preset/custom selection before the user commits. | `src/frontend/autotable-src/src/identity.ts:309` |
| `data-testid="onboarding-continue"` | `<button id="onboarding-continue">` | Primary CTA — submits the form, POSTs `/api/identity`, dismisses the card on 200. | `src/frontend/autotable-src/index.html:765` |
| `data-testid="onboarding-skip"` | `<button id="onboarding-skip">` | Secondary CTA — dismisses the card without sending; the user gets the server-assigned default name + colour on first hub connect (path falls through to the cookie-mint codepath in `PlayerIdentityService.ResolveOrMint`). | `src/frontend/autotable-src/index.html:770` |

> **Backend contract.** The form sends `POST /api/identity` with a JSON
> body `{ displayName?, avatarColor? }`. The server returns the full
> `PlayerProfile` envelope `{ playerId, displayName, avatarColor,
> createdAt, lastSeenAt }` and sets the `mahjong_pid` cookie
> (`HttpOnly; SameSite=Lax; Max-Age=31536000`). The cookie value is a
> 32-char lowercase hex (`Guid.NewGuid().ToString("N")`); the same id
> is returned in the body and on the hub's `ProfileLoaded` broadcast.
> Wire contract is pinned by Vasquez's `PersistentPlayerIdTests`.

## Leaderboard *(Phase J Wave 6)*

Hicks's `src/leaderboard.ts` mounts the lobby's leaderboard pane (the
third tab in the lobby tab-strip, after "My Game" and "Public Games").
The pane reads `GET /api/leaderboard?sort&limit&offset&minGames` and
renders a paged table; the sort + min-games controls are static, the
row testids are templated by 0-based index, and the table also exposes
`data-rank` + `data-player-id` attributes on each `<tr>` for tests that
want to scope-query by content rather than index.

| Selector | Element | Purpose | Source |
|---|---|---|---|
| `data-testid="lobby-leaderboard-tab"` | `<button>` | Tab-strip button that activates the leaderboard pane. Wires up to `lobby.ts:setActiveLobbyTab('leaderboard')`. | `src/frontend/autotable-src/index.html:715` |
| `data-testid="lobby-leaderboard-section"` | `<section id="lobby-tab-leaderboard">` | Pane root. Toggled from `display: none` → visible by the tab handler. Hosts every child below. | `src/frontend/autotable-src/index.html:1024` |
| `data-testid="leaderboard-sort-select"` | `<select id="leaderboard-sort-select">` | Sort axis chooser. Values are the wire strings `gamesWon` / `totalScore` / `winRate` / `longestStreak` / `highestScore` (case-insensitive on the server; the frontend ships canonical camelCase). | `src/frontend/autotable-src/index.html:1033` |
| `data-testid="leaderboard-min-games-input"` | `<input type="number" id="leaderboard-min-games-input">` | Minimum-games-played filter. Default value `5` mirrors `LeaderboardService.DefaultMinGames`. Set to `0` to include everyone (admin / debug view). | `src/frontend/autotable-src/index.html:1039` |
| `id="leaderboard-error"` | `<div>` | Error banner — populated when the fetch fails (network, 5xx, or 429 from Apone's rate limiter). Hidden until needed. | `src/frontend/autotable-src/src/leaderboard.ts:394` |
| `id="leaderboard-loading"` | `<div>` | "Loading leaderboard…" placeholder shown while the fetch is in flight. | `src/frontend/autotable-src/src/leaderboard.ts:396` |
| `id="leaderboard-empty"` | `<div>` | Empty-state placeholder shown when `rows.length === 0` (e.g., the min-games threshold filters everyone out). | `src/frontend/autotable-src/src/leaderboard.ts:395` |
| `data-testid="leaderboard-table"` | `<div>` (table host) | Outer host for the rendered `<table>` — `leaderboard.ts:renderTable` clears + repaints this on every refresh. The `<table>` itself is unwrapped child content. | `src/frontend/autotable-src/index.html:1058` |
| `data-testid="leaderboard-prev-page"` | `<button id="leaderboard-prev-page">` | Pagination — previous page. Disabled when `offset === 0`. | `src/frontend/autotable-src/index.html:1063` |
| `data-testid="leaderboard-paging-summary"` | `<span id="leaderboard-paging-summary">` | Paging summary text (e.g. `Rows 21-30 of 60`). Updated alongside the table render. | `src/frontend/autotable-src/index.html:1067` |
| `data-testid="leaderboard-next-page"` | `<button id="leaderboard-next-page">` | Pagination — next page. Disabled when `offset + rows.length >= total`. | `src/frontend/autotable-src/index.html:1071` |
| `id="leaderboard-row-{0..N}"` | `<tr class="leaderboard-row">` | One row per leaderboard entry on the current page. `idx` is the 0-based offset within the page, NOT the global rank. The `<tr>` also exposes `data-rank` (global 1-based) and `data-player-id` for content-based scoping. Cardinality is `0..limit` (default 50, max 100). | `src/frontend/autotable-src/src/leaderboard.ts:507` |

> **Backend contract.** The pane consumes Bishop's
> `GET /api/leaderboard?sort=<axis>&limit=<n>&offset=<m>&minGames=<k>`
> envelope `{ total: int, rows: [{ rank, playerId, displayName,
> avatarColor, gamesPlayed, gamesWon, winRate, totalScore,
> highestSingleGameScore, longestWinStreak }] }`. Defaults are
> `limit=50` (`MaxLimit=100`), `offset=0`, `minGames=5`; sort defaults
> to `gamesWon` and silently falls back to `gamesWon` on unknown axis.
> All four query knobs + the row shape are pinned by Vasquez's
> `LeaderboardEndpointTests`. Apone's `token-bucket-api` policy (30
> tokens, 5/sec refill) is in front of this endpoint when
> `RateLimiting:Enabled=true` — rejection shape is pinned by
> `RateLimitingTests`.

## Phase J Wave 7 — Replay viewer, settings drawer (tabbed v2), profile page

Selectors added by Hicks's Wave 7 work surface three coordinated UX
features: a refreshed replay viewer (with prev/next-hand navigation
and a speed selector); a tabbed settings drawer (the v2 layout
replacing the Wave-3 monolithic form); and a full-overlay player
profile page that surfaces stats, recent games, display-name +
avatar-colour editing.

### Replay viewer

| Selector | Element | Purpose | Source |
|---|---|---|---|
| `data-testid="replay-screen"` | `<div id="replay-screen">` | Outer overlay root — visibility is gated by `replay.ts:open()/close()`. Tests assert it `.toBeVisible()` after the post-game modal's `[data-testid="game-complete-replay"]` button is clicked. | `src/frontend/autotable-src/index.html:774` |
| `data-testid="replay-viewer"` | `<div class="replay-shell">` | Inner shell — host for every viewer-scope control below. | `src/frontend/autotable-src/index.html:776` |
| `data-testid="replay-close"` | `<button>` | Close button; flips the `replay-screen` overlay back to hidden. | `src/frontend/autotable-src/index.html:792` |
| `data-testid="replay-prev"` | `<button>` | Previous-hand navigation (NEW in Wave 7). Jumps the timeline to the first move of the prior hand. | `src/frontend/autotable-src/index.html:813` |
| `data-testid="replay-step-back"` | `<button>` | Single-move step backwards. Disabled at move 0. | `src/frontend/autotable-src/index.html:818` |
| `data-testid="replay-play"` | `<button>` | Play / pause toggle. The button surface flips ▶ ↔ ⏸ via the class state. | `src/frontend/autotable-src/index.html:823` |
| `data-testid="replay-step-fwd"` | `<button>` | Single-move step forwards. Disabled at the last move. | `src/frontend/autotable-src/index.html:829` |
| `data-testid="replay-next"` | `<button>` | Next-hand navigation (NEW in Wave 7). Jumps the timeline to the first move of the following hand. | `src/frontend/autotable-src/index.html:834` |
| `data-testid="replay-speed-select"` | `<select>` | Playback speed multiplier (1×/2×/4×). Persisted in localStorage at `autotable.phaseJ.v1.replay.speed`. | `src/frontend/autotable-src/index.html:841` |
| `data-testid="replay-scrubber"` | `<input type="range">` | Timeline scrubber — value maps 0..(N-1) of the move index. Bidirectionally bound to the play head; dragging updates the table at the next animation frame. | `src/frontend/autotable-src/index.html:847` |
| `data-testid="replay-event-counter"` | `<span>` | "Move N / M" status text. Stays in sync with the scrubber regardless of which control moved the play head. | `src/frontend/autotable-src/index.html:854` |

> **Reference E2E spec.** `tests/e2e/replay.spec.ts` (Hicks Wave 6 +
> extended Wave 7) covers open → step-fwd → play → close. The
> prev/next-hand affordances are new in Wave 7; the spec gains a
> follow-up assertion when the move counter and scrubber re-base after
> a `[data-testid="replay-prev"]` / `[data-testid="replay-next"]` click.

### Settings drawer (v2 tabbed)

| Selector | Element | Purpose | Source |
|---|---|---|---|
| `data-testid="lobby-open-settings"` | `<button id="lobby-open-settings">` | Lobby shortcut that opens the drawer. Wave-3 entry point; unchanged by the v2 refactor. | `src/frontend/autotable-src/index.html:1293` |
| `data-testid="settings-button"` | `<button id="settings-button">` | Top-bar ⚙ shortcut (visible inside the game shell). Mirrors `lobby-open-settings` behaviour. | `src/frontend/autotable-src/index.html:456` |
| `data-testid="settings-drawer"` | `<aside id="app-settings-drawer-v2">` | Drawer root — `.settings-open` class toggles the off-canvas visual. The `tablist` + `panels` hosts inside are populated by `settings.ts`. | `src/frontend/autotable-src/index.html:575` |
| `data-testid="settings-close"` | `<button>` | Drawer close button (the × glyph in the header). Removes the `.settings-open` class. | `src/frontend/autotable-src/index.html:580` |
| `data-testid="settings-sound"` | `<input type="checkbox">` | Sound toggle (lives inside the General/Audio tab panel). Mirrored to `localStorage.mahjong:soundEnabled` by `lobby.ts:installSoundEnabledMirror`. | `src/frontend/autotable-src/index.html:550` |
| `data-testid="settings-reset"` | `<button id="settings-reset">` | Reset-to-defaults action. Reverts every field in every panel without persisting. | `src/frontend/autotable-src/index.html:596` |
| `data-testid="settings-save"` | `<button id="settings-save">` | Save action — writes the canonical JSON payload at `autotable.phaseJ.v1.settings.*` and flashes the saved-note element. | `src/frontend/autotable-src/index.html:601` |

> **Reference E2E specs.** `tests/e2e/sound-toggle.spec.ts` (Wave 6,
> single-toggle round-trip) + `tests/e2e/settings-drawer.spec.ts`
> (Wave 7, full open / save / reload / reset / close lifecycle).

### Profile page (full overlay)

| Selector | Element | Purpose | Source |
|---|---|---|---|
| `data-testid="lobby-open-profile"` | `<button id="lobby-open-profile">` | Avatar chip in the lobby header. Click opens the full-overlay profile page (NOT the legacy `data-testid="profile-drawer"` slide-in, which is the Wave-5 surface kept around until Wave 8 migration). | `src/frontend/autotable-src/index.html:1284` |
| `data-testid="profile-page"` | `<div id="profile-page">` | Overlay root; `aria-hidden` toggles `true` ↔ `false` on close / open. Tests assert visibility via the aria attribute rather than a CSS class to stay decoupled from the visual implementation. | `src/frontend/autotable-src/index.html:613` |
| `data-testid="profile-page-close"` | `<button id="profile-page-close">` | Close button — flips `aria-hidden` back to `true`. | `src/frontend/autotable-src/index.html:632` |
| `data-testid="profile-stats-grid"` | `<div id="profile-stats-grid">` | Career-stats grid. Populated by `profile-page.ts:renderStatsGrid()` from the `/api/me/stats` payload. Cardinality 0..N children. | `src/frontend/autotable-src/index.html:642` |
| `data-testid="profile-page-display-name-input"` | `<input type="text" maxlength="32">` | Editable display name. Commits on blur; the canonical write target is `/api/me/profile` (PlayerProfileService). Vasquez's `NegativePathTests` pins the > 32 char rejection at the API boundary. | `src/frontend/autotable-src/index.html:653` |
| `data-testid="profile-page-color-custom"` | `<input type="color">` | Custom avatar-colour picker. Wave 7 also surfaces an 8-preset radio group (the `<div id="profile-page-color-presets">` sibling, no testid — DOM children are dynamic). The custom input's value flows into `PlayerProfile.AvatarColor` on save. | `src/frontend/autotable-src/index.html:670` |
| `data-testid="profile-recent-games"` | `<div id="profile-recent-games">` | Recent-games list — populated from `/api/me/games?limit=10` (Bishop Wave 7). Cardinality 0..10 children. Empty state surfaces via `#profile-recent-games-error`. | `src/frontend/autotable-src/index.html:690` |

> **Reference E2E spec.** `tests/e2e/profile-page.spec.ts` (Wave 7,
> Vasquez) covers open → edit name → reload → state persists → close.
> The `[data-testid="profile-stats-grid"]` and
> `[data-testid="profile-recent-games"]` contents are not asserted
> directly because a fresh test browser has no completed-game
> history; only the containers' presence is checked.

> **Backend contract — replay endpoint.** The viewer's `Watch Replay`
> click loads from Bishop's `GET /api/games/{gameId}/replay`. Envelope:
> `{ gameId, createdAt, events: [{ turn, phase, actor, action,
> tilesJson, timestampUtc }] }`. Wire shape pinned by Vasquez's
> `GameReplayEndpointTests`; phase bucket vocabulary (Setup / Deal /
> Discard / Claim / Hu / Other) is the canonical replay-event
> taxonomy — adding a new bucket REQUIRES coordinating with the
> frontend's icon mapping at `replay.ts:phaseGlyph()`.

## Stability contract

A test relying on a selector from this document gets the following
guarantees from Hicks's surface:

1. **Identity** — the testid is the **only** test-relevant attribute.
   Tests SHOULD NOT key off CSS class names, `id` attributes, ARIA
   labels, or DOM-tree position. These are subject to refactor without
   notice; the testid is not.
2. **Cardinality** — each non-templated testid appears **exactly once**
   in the live DOM. Templated testids (e.g.,
   `lobby-player-chip-{0..3}`) appear 0..N times depending on game
   state; document the cardinality bound in the table above.
3. **Lifetime** — once an element is reachable by its testid, it remains
   reachable for the entire surface it belongs to (e.g., a lobby chip
   does not get re-rendered with a different testid mid-frame). Hicks
   uses `setAttribute` once at element creation, not on every update.
4. **Naming** — kebab-case, surface-prefixed, ASCII-only. No
   punctuation, no Unicode. Indexed segments use zero-based integers.

When in doubt, file a memo at
`.squad/decisions/inbox/hicks-vasquez-<topic>.md` before changing a
testid in this document.

---

## Phase J Wave 8 — Auth, rule presets, spectator follow, motion + theme

### Auth surfaces (`signin-modal`, `magic-link-landing`, `auth-cluster`, `profile-linked-accounts`)

| testid                              | element     | cardinality | notes                                                      |
|-------------------------------------|-------------|-------------|------------------------------------------------------------|
| `signin-button`                     | `<button>`  | 0..1        | top-right header. Hidden when user is signed in.           |
| `logout-button`                     | `<button>`  | 0..1        | top-right header. Hidden when user is signed out.          |
| `auth-status-chip`                  | `<span>`    | 0..1        | shows current email + primary provider.                    |
| `signin-modal`                      | `<div>`     | 0..1        | backdrop + card. Visible only when sign-in flow is active. |
| `signin-modal-close`                | `<button>`  | 0..1        | dismisses the modal.                                       |
| `signin-provider-google`            | `<button>`  | 0..1        | starts Google OAuth flow.                                  |
| `signin-provider-github`            | `<button>`  | 0..1        | starts GitHub OAuth flow.                                  |
| `signin-email-input`                | `<input>`   | 0..1        | magic-link email field.                                    |
| `signin-email-submit`               | `<button>`  | 0..1        | requests a magic-link email.                               |
| `signin-email-error`                | `<span>`    | 0..1        | shows validation / server-side errors.                     |
| `signin-email-success`              | `<div>`     | 0..1        | "Check your email" panel.                                  |
| `signin-placeholder`                | `<div>`     | 0..1        | "Auth coming soon" placeholder when /providers 404s.       |
| `magic-link-landing`                | `<div>`     | 0..1        | full-screen overlay rendered when `?auth=<token>` lands.   |
| `magic-link-landing-success`        | `<div>`     | 0..1        | success message panel.                                     |
| `magic-link-landing-failure`        | `<div>`     | 0..1        | failure message panel.                                     |
| `magic-link-landing-continue`       | `<button>`  | 0..1        | dismisses the landing overlay.                             |
| `profile-linked-accounts`           | `<section>` | 0..1        | linked-accounts section inside the profile page.           |
| `profile-linked-account-{provider}` | `<div>`     | 0..3        | one row per linked provider (google / github / email).     |
| `profile-link-{provider}`           | `<button>`  | 0..3        | "Link" button for an unlinked provider.                    |
| `profile-unlink-{provider}`         | `<button>`  | 0..3        | "Unlink" button for a linked provider.                     |

### Rule presets

| testid                                       | element    | cardinality | notes                                                  |
|----------------------------------------------|------------|-------------|--------------------------------------------------------|
| `lobby-rule-preset-select`                   | `<select>` | 0..1        | dropdown in the lobby "Rule preset" fieldset.          |
| `lobby-create-preset-link`                   | `<a>`      | 0..1        | opens the settings-drawer rule-presets tab.            |
| `settings-tab-rule-presets`                  | `<button>` | 0..1        | tab strip entry in the Wave-7 settings drawer.         |
| `settings-panel-rule-presets`                | `<div>`    | 0..1        | panel host. `renderEditorPanel()` populates the body.  |
| `rule-preset-picker`                         | `<select>` | 0..1        | preset chooser inside the editor.                      |
| `rule-preset-new-button`                     | `<button>` | 0..1        | clones the current preset for editing.                 |
| `rule-preset-edit-name`                      | `<input>`  | 0..1        | display name (custom presets only).                    |
| `rule-preset-edit-handLimit`                 | `<input>`  | 0..1        | max hand limit.                                        |
| `rule-preset-edit-maxScorePerHand`           | `<input>`  | 0..1        | per-hand cap.                                          |
| `rule-preset-edit-allowWashout`              | `<input>`  | 0..1        | washout enabled flag (checkbox).                       |
| `rule-preset-edit-allowKongRobbing`          | `<input>`  | 0..1        | kong-robbing flag (checkbox).                          |
| `rule-preset-edit-allowConcealedKongPromotion` | `<input>` | 0..1        | concealed-kong promotion flag (checkbox).              |
| `rule-preset-save`                           | `<button>` | 0..1        | persists the draft via Bishop's POST/PUT.              |
| `rule-preset-delete`                         | `<button>` | 0..1        | deletes the current custom preset.                     |
| `rule-preset-status`                         | `<div>`    | 0..1        | inline save / error status text.                       |

### Spectator follow-seat (`?seat=-1` mode only)

| testid                          | element    | cardinality | notes                                                |
|---------------------------------|------------|-------------|------------------------------------------------------|
| `spectator-follow-panel`        | `<div>`    | 0..1        | floating bottom-right panel. Visible only when spectating. |
| `spectator-follow-seat-{0..3}`  | `<button>` | 0..4        | "Follow Seat N" button per seat.                     |
| `spectator-follow-topdown`      | `<button>` | 0..1        | reverts to the top-down camera.                      |
| `spectator-show-all-toggle`     | `<input>`  | 0..1        | local "show all hands" hint toggle.                  |

Keyboard shortcuts: 1/2/3/4 follow seats 0..3; 0 or Esc returns to
top-down. Shortcuts are inert outside spectator mode and ignored when
typing in an input or contenteditable element.

### Display preferences (settings drawer Display tab)

| testid                     | element    | cardinality | notes                                                |
|----------------------------|------------|-------------|------------------------------------------------------|
| `settings-motion-select`   | `<select>` | 0..1        | Auto / Reduced / Full.                               |
| `settings-theme-select`    | `<select>` | 0..1        | Auto / Light / Dark.                                 |

The page chrome reflects these picks via `<body>` classes
`reduced-motion`, `full-motion`, `theme-light`, `theme-dark`. Tests
that assert chrome appearance should key off these classes (NOT the
testids, which only locate the controls).

### Master bot tier

| testid                                | element    | cardinality | notes                                            |
|---------------------------------------|------------|-------------|--------------------------------------------------|
| `lobby-bot-difficulty-master`         | `<input>`  | 0..1        | Master radio in the lobby Bot difficulty fieldset. |

The non-lobby surfaces (`#bot-difficulty`, `#settings-bot-strength`)
add a Master option without a separate testid because each select
exposes its `value` attribute directly.

---

### Phase J Wave 8 Playwright coverage — Vasquez

The following e2e specs key off the Wave 8 testids above and live in
`src/frontend/autotable-src/tests/e2e/`. Each spec is
*reflection-defensive*: a missing `data-testid` is logged as a
`soft-pass` annotation rather than a failure, so contract drift before
Hicks's surface lands does not break the gate.

| Spec                          | Surface under test                                        |
|-------------------------------|-----------------------------------------------------------|
| `signin-modal.spec.ts`        | header sign-in chip + modal (providers, dev-login, close) |
| `magic-link.spec.ts`          | `?auth=<token>` landing (success / failure / continue)    |
| `rule-presets.spec.ts`        | lobby dropdown + settings drawer rule-preset editor       |
| `spectator-follow.spec.ts`    | `?seat=-1` floating follow-panel + keyboard shortcuts     |
| `reduced-motion.spec.ts`      | `prefers-reduced-motion: reduce` → body class + CSS clamp |
| `dark-mode.spec.ts`           | `prefers-color-scheme: dark` → body theme-dark + luma     |

When you remove or rename a Wave 8 testid, search
`src/frontend/autotable-src/tests/e2e/` for the literal string before
landing the change — the soft-pass annotation will otherwise hide the
regression from CI.

## Phase J Wave 9 testids — chat / i18n / replay-audit (Vasquez)

Wave 9 ships five new surfaces with selectors that follow the kebab-case,
surface-prefixed contract.  The testids below are *already present* in
`index.html` and Hicks's modules at HEAD; any future rename must update
the matching e2e spec referenced in the next subsection.

| Surface         | testid                       | Where wired                                 |
|-----------------|------------------------------|---------------------------------------------|
| Chat            | `chat-panel`                 | `index.html` (#chat-panel container)        |
| Chat            | `chat-toggle`                | `index.html` (collapse / expand button)     |
| Chat            | `chat-channel-select`        | `index.html` (table / spectators / private) |
| Chat            | `chat-recipient-select`      | `index.html` (private DM recipient picker)  |
| Chat            | `chat-unavailable`           | `index.html` (offline / 404 banner)         |
| Chat            | `chat-messages`              | `index.html` (rendered message log)         |
| Chat            | `chat-input`                 | `index.html` (composer textarea — `maxlength=280`) |
| Chat            | `chat-char-count`            | `index.html` (live `0/280` counter)         |
| Chat            | `chat-send`                  | `index.html` (Send button)                  |
| Chat            | `chat-message-{i}`           | `chat.ts` (per-row id)                      |
| Chat            | `chat-message-{i}-author`    | `chat.ts` (sender pill)                     |
| Chat            | `chat-message-{i}-body`      | `chat.ts` (body text node)                  |
| i18n            | `settings-language-select`   | `settings-drawer.ts` (lang picker)          |
| CSP             | _N/A (header-level)_         | `SecurityHeadersMiddleware.cs` (`DefaultCsp`)|
| Replay (audit)  | `replay-audit-tab`           | `index.html` (admin-only tab, hidden initially) |
| Replay (audit)  | `replay-audit-row-{i}`       | `audit.ts` (per-row id)                     |
| Replay (audit)  | `replay-audit-row-{i}-source`| `audit.ts` (human / bot / system cell)      |
| Replay (audit)  | `replay-audit-row-{i}-duration` | `audit.ts` (durationMs cell)             |
| Replay (audit)  | `replay-audit-row-{i}-score` | `audit.ts` (bot decision score cell)        |
| Reconnect       | `reconnect-copy-link`        | `index.html` (Wave 4, reused for Wave 9 rotation hygiene) |

The CSP row is intentionally header-level — there is no DOM selector to
pin; `csp-headers.spec.ts` reads the `Content-Security-Policy` HTTP
header directly off the root document.

### Phase J Wave 9 Playwright coverage — Vasquez

The Wave 9 e2e specs live alongside the Wave 8 ones in
`src/frontend/autotable-src/tests/e2e/`.  All follow the
reflection-defensive pattern established by `magic-link.spec.ts` —
missing surface → `test.info().annotations.push({ type: 'soft-pass', ... })`
and an early return rather than a hard failure.

| Spec                       | Surface under test                                       |
|----------------------------|----------------------------------------------------------|
| `chat-panel.spec.ts`       | chat-panel mount, composer 280-char cap, channel picker, graceful 404 |
| `i18n-switch.spec.ts`      | settings-language-select → body[lang] flips between en/zh-Hans/zh-Hant |
| `csp-headers.spec.ts`      | root-document `Content-Security-Policy` shape, `unsafe-eval` ban, nonce/strict-dynamic preference |
| `admin-audit-tab.spec.ts`  | `replay-audit-tab` admin gate (hidden non-admin, visible admin, row render, 403 grace) |
| `token-rotation.spec.ts`   | `mahjong:session:v1` localStorage blob present, no token leak in DOM, reload preserves playerId |

Soft-pass annotation contract — keep these stable so CI summaries
remain searchable:

  - `chat-panel surface not yet wired`
  - `settings-language-select not yet wired`
  - `CSP header not yet emitted on root document`
  - `replay-audit-tab not yet wired`
  - `no session/reconnect blob persisted yet`

When you remove or rename a Wave 9 testid, run
`grep -rn '<testid>' src/frontend/autotable-src/tests/e2e/` before
merging — the soft-pass annotations will silently hide regressions.

## Phase J Wave 10 testids — tournaments / avatar / audit-why / spectator chat (Vasquez)

Wave 10 ships four new surfaces. The testids below are documented for
Hicks's bundle to wire up; the e2e specs listed in the next subsection
soft-pass while the surfaces are forward-staged.

| Surface             | testid                                    | Where wired                                       |
|---------------------|-------------------------------------------|---------------------------------------------------|
| Tournaments         | `lobby-tournament-card`                   | `index.html` (lobby tournament panel container)   |
| Tournaments         | `lobby-tournament-list`                   | `index.html` (rendered list of tournaments)       |
| Tournaments         | `lobby-tournament-name`                   | `index.html` (create-form name input)             |
| Tournaments         | `lobby-tournament-create`                 | `index.html` (create-form submit button)          |
| Tournaments         | `tournament-register-btn`                 | tournament card row (per tournament)              |
| Tournaments         | `tournament-registration-status`          | tournament card row (Registered / Open badge)     |
| Tournaments         | `tournament-start-btn`                    | tournament card row (creator-only)                |
| Tournaments         | `tournament-matches-table`                | tournament detail view (after start)              |
| Tournaments         | `tournament-leaderboard`                  | tournament detail view (standings table)          |
| Avatar migration    | `avatar-migration-modal`                  | `index.html` (legacy `#808080` colour prompt)     |
| Avatar migration    | `avatar-migration-pick-{name}`            | colour swatches inside the modal (e.g. `-emerald`)|
| Avatar migration    | `avatar-migration-dismiss`                | modal Dismiss / Close button                      |
| Audit why-expand    | `replay-audit-row-{i}-why`                | `audit.ts` (per-row toggle)                       |
| Audit why-expand    | `replay-audit-row-{i}-reasoning`          | `audit.ts` (expanded reasoning panel)             |
| Audit why-expand    | `replay-audit-row-{i}-reasoning-line-{j}` | `audit.ts` (per-line list-item)                   |
| Audit why-expand    | `[data-strategy]` (attribute)             | `audit.ts` (strategy badge attribute on header)   |
| Spectator chat      | _(re-uses Wave 9 `chat-*` testids)_       | `chat.ts` — spectator channel default + filter    |

### Phase J Wave 10 Playwright coverage — Vasquez

| Spec                            | Surface under test                                        |
|---------------------------------|-----------------------------------------------------------|
| `tournament-flow.spec.ts`       | lobby card mount, create → register → start → leaderboard |
| `avatar-migration.spec.ts`      | `#808080` migration modal, picker persist, dismiss-safe   |
| `csp-no-inline-styles.spec.ts`  | `style-src` lacks `'unsafe-inline'` + DOM has no inline   |
| `audit-why-expand.spec.ts`      | `replay-audit-row-{i}-why` toggle, reasoning lines render |
| `spectator-chat.spec.ts`        | spectator channel default, composer enabled, table-leak   |

Canonical soft-pass annotation strings (keep stable for CI summary scraping):

  - `lobby-tournament-card not yet wired`
  - `lobby-tournament-create form not yet wired`
  - `tournament-register-btn not yet wired`
  - `tournament-start-btn not yet wired`
  - `tournament-matches-table not yet wired`
  - `tournament-leaderboard not yet wired`
  - `avatar-migration-modal not yet wired`
  - `avatar-migration-pick-* not yet wired`
  - `avatar-migration-dismiss not yet wired`
  - `style-src still carries unsafe-inline (CspStrictStyles=false)`
  - `inline style attributes still present`
  - `replay-audit-row-{i}-why toggle not yet wired`
  - `replay-audit-row-{i}-reasoning panel not yet wired`
  - `reasoning-line {i} testids not yet wired`
  - `reasoning data-strategy attribute not yet wired`
  - `chat-panel not yet wired for spectator viewport`
  - `chat-channel-select did not default to spectators`
  - `spectator backfill not yet rendered`
  - `chat-input disabled for spectators (Wave 10 brief expects enabled)`
  - `table channel leaking into spectators view (Wave 10 contract not yet enforced)`

When you remove or rename a Wave 10 testid, run
`grep -rn '<testid>' src/frontend/autotable-src/tests/e2e/` before
merging — the soft-pass annotations will silently hide regressions.

## Phase K Wave 1 testids — tournament SVG bracket / match history / rated leaderboard / onboarding tour (Hicks)

Coverage for the Phase K Wave 1 frontend bring-up.

### Tournaments — SVG bracket + sortable standings + watch-finals pin
- `tournament-bracket-svg` — the SVG host inside `#tournament-bracket`
  (replaces the Wave-10 `<pre>` dump).  Renders single-elim brackets;
  for round-robin / Swiss formats the host is hidden and the
  standings table takes over.
- `tournament-bracket-match-{R}-{N}` — clickable `<g>` cell for round
  R (1-based), match index N (1-based within the round).  Click /
  Enter / Space toggle the inline detail row below the bracket.
- `tournament-bracket-match-{R}-{N}-expand` — the chevron / `+` glyph
  inside the match cell that mirrors the cell's expanded state.
- `tournament-standings-table` — the standings table host inside
  `#tournament-standings`.  Headers cycle through asc/desc/off on
  click; the `<th>` for the active column gets `.sorted-asc` /
  `.sorted-desc`.
- `tournament-standings-row-{N}` — one row per player, N is the
  current sort position (1-based).
- `tournament-watch-finals-{tournamentId}` — the "Watch finals" pin
  rendered on a final-round complete match.  Calls
  `openReplayForGame(gameId)` (lazy-imports `./replay-launcher`).

SignalR contract: `tournaments.ts` subscribes to the
`TournamentMatchCompleted` hub event (alias
`TournamentMatchCompletedV1`) and refreshes the active tournament
detail in-place when it fires.

### Match history export modal (profile → Recent games)
- `profile-history-link` — auto-injected "📥 Match history" button
  inside `#profile-recent-games`.
- `history-modal` — the modal dialog scaffold; mounted lazily by
  `history.ts:installHistoryModal()`.
- `history-date-range` — `<select>` with options 7 / 30 / 90 / 365 /
  custom.  When custom, two `<input type="date">` siblings become
  visible (`history-date-from`, `history-date-to`).
- `history-format-toggle` — radio group with values `json` and `csv`.
- `history-download` — primary action button; triggers blob download
  via `URL.createObjectURL`.
- `history-recent-table` — preview table of the most recent 20
  matches.  Columns sort on header click.
- `history-recent-row-{N}` — one row per match preview.

Wire contract: `GET /api/games?playerId=&format=json|csv&from=<ISO>&to=<ISO>`.
The modal feature-detects 404 → shows "Match-history export is not yet
available" status banner and disables the Download button.

### Leaderboard — ELO rating toggle + season picker + delta arrows
- `leaderboard-rating-toggle` — checkbox that swaps the leaderboard
  data source from `/api/leaderboard` → `/api/ratings/leaderboard`.
  Mode persists in LS `mahjong.leaderboard.rating.v1`.
- `leaderboard-season-select` — `<select>` listing
  current / last / all-time.  Persists in LS
  `mahjong.leaderboard.rating.season.v1`.
- `leaderboard-rating-status` — `aria-live="polite"` badge that
  surfaces "Ratings unavailable — showing stats." when the ratings
  endpoint 404s (falls back to the stats endpoint, persists
  `mode='stats'`).
- `leaderboard-rating-delta-{N}` — per-row delta cell rendered only
  in rating mode.  Carries `▲`/`▼`/`—` plus `.lb-delta-up` /
  `.lb-delta-down` / `.lb-delta-zero` classes.

### Onboarding tour overlay
- `tour-overlay` — fixed-inset overlay with SVG dim mask + spotlight
  cutout.  Gated by LS flag `mahjong.tour.completed.v1`.
- `tour-spotlight` — the SVG `<rect>` cutout; geometry is computed
  per-step from `getBoundingClientRect()` of the step's target.
- `tour-step-{1..8}` — the tour card carries the active step's
  testid; rotates as the user advances.
- `tour-prev` / `tour-next` — navigation buttons.  Prev is disabled
  on step 1; Next becomes "Done ✓" on step 8.
- `tour-skip` — closes the tour and marks the LS flag complete.

Steps:
  1. lobby tab strip — `lobby-my-game-tab`
  2. profile chip — `lobby-open-profile`
  3. rule preset selector — `lobby-rule-preset-select` (fallback
     `lobby-variant-fieldset`)
  4. bot strength tiers — `lobby-bot-difficulty-fieldset`
  5. chat panel — `chat-panel`
  6. settings drawer — `settings-button`
  7. tournaments tab — `lobby-tournaments-tab` (secondary highlight
     on `leaderboard-rating-toggle`); auto-activates the Tournaments
     tab so the highlight has a target.
  8. centred "you're ready to play" card — no spotlight target.

Keyboard support: ←/→ navigate, Enter advances, Esc closes without
marking complete (resumable on next visit).

### Phase K Wave 1 Playwright coverage — Vasquez

The Wave-10 soft-pass annotation pattern still applies — any selector
above that has not yet been wired by a downstream module is listed
here so the e2e suite can stay green during the rollout:

- `tournament-bracket-svg renders only when bracket is single-elim`
- `tournament-bracket-match-* click expands inline detail`
- `tournament-watch-finals-* hidden until the final-round match is complete`
- `history-modal endpoint feature-detect (no /api/games yet on staging)`
- `leaderboard-rating-toggle falls back to stats on 404`
- `tour-overlay only fires when LS flag is unset`

When you remove or rename a Phase K Wave 1 testid, run
`grep -rn '<testid>' src/frontend/autotable-src/tests/e2e/` before
merging — the soft-pass annotations will silently hide regressions.

## Phase K Wave 2 testids — lobby bundle split + voice chat + drag-drop seeding + PWA + server-authoritative tour (Hicks)

Coverage for the Phase K Wave 2 frontend bring-up.  Lobby initial
bundle now < 500 kB; renderer + Client + three / signalr-bound voice
code is deferred behind dynamic imports.

### Lobby bundle budget

The Wave-1 eager bundle (`autotable-src.<hash>.js`) was 1.318 MB.
Wave 2 split out the renderer chain into `game-bootstrap.<hash>.js`
(loaded only when `window.location.search` is non-empty — i.e., the
user has crossed the lobby boundary).  Post-split sizes:

| Asset                                 | Size       | Trigger |
|---|---|---|
| `autotable-src.<hash>.js` (eager)     | **208.4 kB** | Always (lobby + matchmaking + identity + i18n + Sentry shim) |
| `game-bootstrap.<hash>.js` (lazy)     | 1.11 MB    | First non-empty `?…` on URL (Quick Match / Apply / `?gameId=`) |
| `esm.<hash>.js` (Sentry vendor)       | 395 kB     | Only when `<meta name="sentry-dsn">` is non-empty |
| `tournaments.<hash>.js` (lazy)        | 23.8 kB    | Tournaments tab hover/focus/click |
| `history.<hash>.js` (lazy)            | 12.3 kB    | Profile-page open |
| `tour.<hash>.js` (lazy)               | 9.5 kB     | First visit (skipped when server says done) |
| `chat.<hash>.js` (lazy)               | 12.2 kB    | `?gameId=` on URL after game bootstrap |
| `audit.<hash>.js` (lazy)              | 7.4 kB     | Admin probe + replay-tab activation |
| `voice.<hash>.js` (lazy)              | 5.6 kB     | `?voice=1` on a game URL |

The eager CSS budget (`autotable-src.<hash>.css` × 3) is unchanged at
~216 kB.  A cold lobby visitor downloads 208 kB JS + 216 kB CSS +
icons = under 500 kB total transfer before the lobby paints.

`utils.ts` was split into `utils.ts` (three.js-bound) +
`dom-utils.ts` (pure DOM).  Lobby-chain modules now import
`setElHidden` / `showEl` / `hideEl` from `dom-utils` so three is no
longer pulled into the eager graph.

### Voice chat panel
- `voice-panel` — root `<aside>` mounted by `voice.ts` when a player
  joins a table with voice opt-in (`?voice=1` on the URL until
  Bishop's hub broadcasts a per-game `voiceEnabled` flag).
- `voice-mic-toggle` — primary mic button.  Toggles `aria-pressed`,
  swaps "🎙️ Mute" ↔ "🔴 Live".  Disabled + `voice-mic-denied` class
  when `getUserMedia` is rejected.
- `voice-peer-{connectionId}` — per-peer status pill carrying
  "Connecting" / "Connected" / "Failed".  Class
  `voice-peer-status-{state}` mirrors the text.
- `voice-volume-{connectionId}` — `<input type="range">` (0–1, step
  0.05) bound to the peer's `<audio>.volume`.

Wire contract:
  - ICE servers fetched from `GET /api/turn` →
    `{ iceServers: [{ urls, username?, credential? }, …] }`.
    On 404 / error we fall back to a public STUN server so the mesh
    still works on benign NATs.
  - `VoiceHub` (SignalR, `/hubs/voice`) signals via
    `Offer` / `Answer` / `IceCandidate` / `PeerJoined` / `PeerLeft`
    events; client → server methods are `SendOffer`, `SendAnswer`,
    `SendIceCandidate`.  Up to 4 peers per table (full mesh).

### Tournament drag-drop seeding (admin)
- `tournament-seeding-panel` — admin-only seeding surface above the
  bracket SVG.  Renders when (a) the admin probe
  (`GET /api/auth/me` → `role:'admin'` / `roles:['admin']`) succeeds,
  (b) the tournament status is `open` / `registration-open`, and (c)
  the format is single-elim.  Hidden otherwise.
- `tournament-seed-row-{N}` — one draggable `<li>` per seed.  HTML5
  drag-drop reorders the list; `aria-grabbed` flips on `dragstart`/
  `dragend`.  Internal `data-seed-index` + `data-player-id`
  attributes carry the canonical ordering.
- `tournament-seeding-save` — POST `/api/tournaments/{id}/seed` with
  `{ seeds: [playerId, …] }`.  Disabled during the request; on
  success re-opens the tournament detail so the bracket reflects the
  server's canonical layout.
- `tournament-seeding-status` — error/status pill that surfaces
  beneath the Save button when the POST fails.  Auto-removes after
  4 s.

### Replay finals deep-link
- `openReplayForGame(gameId, { finals: true })` — new options arg on
  the launcher.  Auto-scrolls to the last hand + final move + stamps
  `?finals=true` on the URL so a shared link reopens at the finals.
- `?finals=true` URL flag is also honoured when a replay is opened
  without the option (covers cold-link visits).
- All tournament replay entry points (the SVG cell finals pin, the
  detail strip Watch-replay button, and the round-robin/Swiss row
  ▶ buttons) pass `{ finals: true }`.

### Server-authoritative onboarding status
- `tour.ts` probes `GET /api/players/me/onboarding-status` on
  `installOnboardingTour()`.  When the server reports `completed:
  true`, the LS flag is mirrored locally so future visits skip the
  tour even when offline.
- On tour completion `endTour(true)` posts the same endpoint with
  `{ completed: true, completedAtUtc: "<iso>" }`.  POST failure is
  silently ignored — LS is the authoritative offline fallback.
- 404 / network error → falls through to the Wave-1 LS-only path so
  the rollout is safe to merge ahead of Bishop's backend.

### PWA — manifest + service worker + offline lobby
- `manifest.webmanifest` (shipped unhashed at the dist root):
  standalone display, `theme_color #1e2a36`, three icon sizes drawn
  from `img/icon-{16,32,96}.auto.png`.  Linked from `index.html`
  alongside an Apple-touch-icon shim.
- `sw.js` (shipped unhashed at the dist root) caches:
  - cache-first for parcel content-hash assets (`.<8hex>.{js,css,…}`)
    and anything under `/img/`.  Old hashed files survive until the
    next install cycle's `activate` purge.
  - network-first with cache fallback for `/api/games/public` so a
    returning user with a dead connection still sees the last-known
    lobby (and the offline banner appears).
  - network-only for the rest of `/api/*` + `/hubs/*` so auth +
    matchmaking + voice never serve stale data.
  - network-first with a cached index.html fallback so the SPA shell
    boots offline.
- `pwa-offline-banner` — `<div role="status">` injected into
  `<body>` by `pwa.ts`; toggles `hidden` on `online`/`offline`
  transitions and re-broadcasts as `mahjong:offline` / `mahjong:online`
  CustomEvents so other modules (history, matchmaking) can attach.
- `pwa-install-prompt` — `<button>` mounted when Chrome/Edge fires
  `beforeinstallprompt`.  Clicking it invokes the deferred native
  prompt; the button removes itself afterward.

### Phase K Wave 2 Playwright coverage — Vasquez

Soft-pass annotations for selectors whose downstream surface hasn't
yet been wired:

- `voice-mic-toggle hidden until ?voice=1 is on the URL`
- `voice-peer-* requires VoiceHub (Bishop's /hubs/voice)`
- `tournament-seeding-panel hidden when admin probe returns false`
- `tournament-seed-row-* drag-drop reorders + Save POSTs /seed`
- `pwa-install-prompt only fires on Chrome/Edge after beforeinstallprompt`
- `pwa-offline-banner toggles with navigator.onLine`
- `tour completes once when /api/players/me/onboarding-status is 200 { completed:true }`
- `replay deep-link ?finals=true auto-scrolls to last hand`

When you remove or rename a Phase K Wave 2 testid, run
`grep -rn '<testid>' src/frontend/autotable-src/tests/e2e/` before
merging — same drift-detection note as Wave 1.

## Phase K Wave 3 testids — scene split + voice-enabled flag + Microsoft OAuth + seed auto-save + SW pre-cache (Hicks)

Coverage for the Phase K Wave 3 frontend bring-up.  The Wave-2
game-bootstrap chunk (which still carried three.js) is now itself
split: `game-bootstrap.<hash>.js` is a three-free HUD shell, and the
heavy renderer lives in a fresh `scene.<hash>.js` chunk loaded by
dynamic-import after the shell paints.

### Lobby + game bundle budget (post-Wave-3)

The Wave-2 game chunk was 1.11 MB (three.js + AssetLoader + Game +
MoveLog + lobby client attach all eager inside `game-bootstrap.ts`).
Wave 3 splits those out into a dedicated scene module:

| Asset                                 | Size       | Trigger |
|---|---|---|
| `autotable-src.<hash>.js` (eager)     | **214 kB** | Always (lobby + matchmaking + identity + i18n + Sentry shim + auth modal + toast helper) |
| `game-bootstrap.<hash>.js` (lazy)     | **166 kB** | First non-empty `?…` on URL (HUD/chat/voice wiring, no three.js) |
| `scene.<hash>.js` (lazy)              | **922 kB** | Dynamic-imported by `game-bootstrap.ts` after the shell `data-testid="game-shell-ready"` lands — owns three.js, AssetLoader, Game, MoveLog, lobby-client attach |
| `toast.<hash>.js` (lazy)              | ~1.2 kB    | Imported on demand by voice/tournaments error paths |
| `esm.<hash>.js` (Sentry vendor)       | 395 kB     | Only when `<meta name="sentry-dsn">` is non-empty |
| `tournaments.<hash>.js` (lazy)        | ~25 kB     | Tournaments tab hover/focus/click |
| `chat.<hash>.js` (lazy)               | ~16 kB     | `?gameId=` on URL after game bootstrap |

Cold lobby visitor still downloads <500 kB of JS+CSS before the
lobby paints.  A player who follows a `?gameId=` link pays an
additional 166 kB to mount the HUD shell (which paints the
`game-shell-ready` testid) and then the 922 kB scene chunk streams
in concurrently with the lobby tiles loading.

### Scene split — `game-shell-ready` + `game-scene-ready`

- `game-shell-ready` — set as `data-testid` on `<body>` (and
  dispatched as a `mahjong:game-shell-ready` event) by
  `markShellReady()` in `game-bootstrap.ts` once the three-free shell
  has mounted.  Playwright should `await page.waitForSelector('[data-testid="game-shell-ready"]')`
  for HUD-only assertions.
- `game-scene-ready` — set as `data-testid` on `<body>` (and
  dispatched as a `mahjong:game-scene-ready` event) by `scene.ts`
  after the first `requestAnimationFrame` post-mount.  Use this
  rather than `game-shell-ready` for assertions that depend on
  tiles being painted on the canvas.

The two testids are additive — both stay attached for the life of
the page so re-entry into the lobby and back doesn't clear them.

### Voice — per-game `voiceEnabled` flag

Wave 2 gated voice on `?voice=1`; Wave 3 wires it to a per-game
server flag fetched via `GET /api/games/{id}/settings`
(`{ voiceEnabled: bool, viewerIsOwner: bool, ... }`).

- `voice-mic-toggle` — when `voiceEnabled === false` the button
  carries `disabled` + `aria-disabled="true"` + a tooltip
  ("Voice not enabled for this table").  Hover/focus surfaces the
  tooltip; click is a no-op.
- `voice-enable-toggle` — owner-only toggle in the settings drawer's
  Network panel (only renders when `viewerIsOwner === true`).
  Optimistic: flips immediately, POSTs
  `/api/games/{id}/settings/voice` `{ enabled: bool }`, rolls back
  + toasts on non-2xx.  Dispatches `mahjong:voice-enabled`
  CustomEvent (with `{ detail: { enabled } }`) on success so the
  in-flight voice module live-flips the mic without page reload.
- `voice-enable-hint` — sibling `<span class="settings-hint">`
  carrying "When enabled, players join WebRTC voice automatically."
- Toast surfaces on hub-side rejection:
  - `"voice not enabled"` (sent by `VoiceHub.JoinVoice` when the
    server-side flag is off) → "Voice is not enabled for this
    table."
  - `"spectators cannot join voice"` → "Spectators can listen but
    not speak."

### Sign-in modal + Microsoft OAuth

`auth.ts` now mounts a sign-in modal scaffold lazily on first
import (the Wave-2 build had no modal markup in `index.html`; Wave
3's `ensureAuthMarkup()` injects the modal, header chip, and
magic-link landing during module initialisation).

- `signin-button` — header CTA that opens the modal.
- `signin-modal` — root `<div role="dialog" aria-modal="true">`.
- `signin-provider-google` — Google sign-in row (POST flow,
  unchanged from Wave 2's design).
- `signin-provider-microsoft` — **new in Wave 3.** Microsoft brand
  4-tile inline SVG icon (Microsoft brand colours
  `#f25022 / #7fba00 / #00a4ef / #ffb900`).  Clicking it does a
  direct `window.location.href = '/api/auth/login?provider=microsoft&returnUrl=…'`
  (the redirect-via-cookie flow Bishop wired in Wave 3 backend) —
  intentionally different from the POST-then-redirect Google flow
  because Microsoft's Entra token endpoint expects a GET handshake
  with the `returnUrl` round-tripped as a state cookie.
- `signin-provider-github` — GitHub OAuth row (unchanged).
- `signin-provider-email` — magic-link row (unchanged).
- `signin-error` — error pill beneath the provider list (used by
  both OAuth + magic-link failures).
- `auth-header-chip` — top-right pill showing the authenticated
  user's name + provider badge.  Provider badge for Microsoft is
  `🟦 Microsoft`.

### Tournament seeding — auto-POST on each drop

Wave-2 had a manual "Save" button.  Wave 3 auto-POSTs the new
ordering on every successful drop with rollback on failure.

- `tournament-seeding-panel` — unchanged (admin-only).
- `tournament-seed-row-{N}` — drop handler now calls `persistSeeds()`
  which POSTs `/api/tournaments/{id}/seed` with the **Wave-3 wire
  shape** `{ seeds: [{ playerId, seedNumber }, …] }`.  `seedNumber`
  is 1-based.  On HTTP failure the previous ordering is restored,
  the list re-renders, and a toast surfaces "Seed order could not
  be saved — restored previous order."
- `tournament-seeding-save` — still present but functionally a
  belt-and-braces no-op for the current state (manual save remains
  for keyboard reorder users in Wave 4).
- `tournament-seeding-status` — error pill (unchanged).

### Offline-friendly onboarding tour

`tour.ts` Wave 2 probed `GET /api/players/me/onboarding-status`
synchronously before deciding whether to show the tour.  Wave 3
races the probe against a 300 ms timer so an offline visitor isn't
held at a blank page:

- Probe failure or timeout → tour starts immediately, completion
  POST is silently dropped (`offlineFallback = true`).  LS flag
  remains the authoritative offline source of truth.
- Probe success → Wave-2 server-authoritative behaviour preserved.
- No new testids; `onboarding-tour-overlay` continues to drive the
  Playwright assertions.

### Service worker pre-cache manifest

`scripts/generate-sw-manifest.js` (chained from `npm run build:post`
after `parcel build`) emits `manifest-precache.json` at the dist
root and copies the latest `sw.js` into the dist.  The script also
prunes superseded hashed chunks left behind by previous parcel runs
(so the dist doesn't accumulate stale `game-bootstrap.<oldhash>.js`
across waves).

- `manifest-precache.json` shape:
  ```json
  {
    "generatedAt": "<iso>",
    "version": "autotable-v3",
    "assets": ["./index.html", "./autotable-src.<hash>.js", …]
  }
  ```
- `sw.js` `install` handler fetches the manifest with
  `cache: 'no-store'` and calls `cache.addAll(reachable)` after a
  HEAD-probe pass (so one missing entry doesn't fail the whole
  install transaction).  Cache version bumped to `autotable-v3`;
  `activate` purges any cache prefixed `autotable-` that isn't the
  current version — so the Wave-2 → Wave-3 upgrade evicts stale
  precache entries on the first navigation.
- Wave-2 runtime caching strategies (cache-first for hashed assets,
  network-first for `/api/games/public`, network-only for the rest
  of `/api/*` + `/hubs/*`) are unchanged.

### Phase K Wave 3 Playwright coverage — Vasquez

Soft-pass annotations for selectors whose downstream surface
hasn't yet been wired or whose backend ships in a parallel PR:

- `game-shell-ready precedes game-scene-ready (shell within 1 s, scene within 5 s)`
- `voice-mic-toggle disabled + tooltip when voiceEnabled=false`
- `voice-enable-toggle visible only when viewerIsOwner=true`
- `voice-enable-toggle optimistic flip + rollback + toast on POST failure`
- `signin-provider-microsoft renders Microsoft 4-tile SVG icon`
- `signin-provider-microsoft click → window.location.href = /api/auth/login?provider=microsoft`
- `tournament-seed-row drag → auto-POST { seeds: [{ playerId, seedNumber }, …] }`
- `tournament-seed-row POST failure → ordering reverts + toast`
- `tour starts within 300 ms even when /api/players/me/onboarding-status is offline`
- `sw install pre-caches eager + shell + icons via manifest-precache.json`

When you remove or rename a Phase K Wave 3 testid, run
`grep -rn '<testid>' src/frontend/autotable-src/tests/e2e/` before
merging — same drift-detection note as Wave 1.
