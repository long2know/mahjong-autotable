# Frontend DOM selectors

This document is the **source of truth** for stable `data-testid` selectors on
the Changsha Mahjong frontend. Future Playwright / Cypress integration tests
(scheduled for a post-Phase-J phase) target these IDs, so:

- Hicks-managed code MUST keep these selectors stable across renames /
  refactors.
- Adding a new testid to the contract means appending here in the same PR.
- Removing or renaming a testid is a breaking change — open a coordinated
  Hicks ↔ Vasquez memo before doing it.

Format conventions (Phase J Wave 4):

- All testids are **kebab-case**, prefixed with their surface
  (`lobby-`, `connection-banner-`, `hud-`, …) so a CSS selector
  `[data-testid^="lobby-"]` finds an entire surface.
- Dynamic / indexed testids carry the `{0..N}` suffix in this doc; at
  runtime they are filled in by template-literal interpolation in TS
  (e.g., `` `lobby-player-chip-${i}` ``).
- Each entry cites the file + line of origin so a future grep keeps the
  doc in sync.

> **Maintenance note.** The catalog is captured as of Phase J Wave 4
> (Hicks's working-tree commit `feat(frontend): wave-4 reconnect banner +
> mobile drawers + testid surface`). When you change a testid:
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

## Public matchmaking lobby *(Phase J Wave 5 — partial)*

Hicks's `src/matchmaking.ts` poll loop (5s cadence — see
`MATCHMAKING_POLL_MS`) reads `GET /api/matchmaking/lobby` and emits
`update` events with the `PublicGame` array. As of Phase J Wave 5 the
TypeScript module does not yet attach `data-testid` attributes to the
rendered chips — the surface table below reserves the contract names
that the Phase J Wave 6 acceptance tests will target so the testids land
in the same PR as the host UI.

| Selector | Element (proposed) | Purpose | Source |
|---|---|---|---|
| `data-testid="lobby-public-section"` | `<section>` | Wrapper around the "Public games" list — only visible when the poll loop is active. | *reserved* — will be added when Hicks ships the list-host markup |
| `data-testid="lobby-public-list"` | `<ul>` / `<div>` | Container holding the per-game chips; one child per `PublicGame` entry from the poll cache. | *reserved* |
| `data-testid="lobby-public-game-{0..N}"` | `<li>` / `<div>` | One chip per public game. Index is the lobby cache position (newest-first, capped at `MAX_PUBLIC_GAMES_RENDERED = 50`). | *reserved* |
| `data-testid="lobby-public-game-name-{0..N}"` | `<span>` | The friendly host-supplied `publicName` (≤64 chars per `ChangshaGameRuntime.SetGamePublicAsync`). | *reserved* |
| `data-testid="lobby-public-game-host-{0..N}"` | `<span>` | The host's `creatorDisplayName` (resolved through `PlayerProfileService`). | *reserved* |
| `data-testid="lobby-public-game-seats-{0..N}"` | `<span>` | The `seatedCount / maxSeats` text. **NOTE — wire-shape watchpoint:** the controller returns `seatedCount` + `maxSeats`; `matchmaking.ts:PublicGame` aliases these to `seatsTaken` + `seatsTotal` via `isPublicGame`. See Vasquez's Phase J Wave 5 memo for the rename roadmap. | *reserved* |
| `data-testid="lobby-public-game-join-{0..N}"` | `<button>` | Per-chip "Join" CTA — invokes the SignalR `TakeSeat` flow on the selected game. | *reserved* |
| `data-testid="lobby-join-random"` | `<button>` | "Join any public game" shortcut — invokes the SignalR `JoinRandom` RPC (`MatchmakingService.JoinRandomAsync` picks a random public-seating game). | *reserved* |
| `data-testid="lobby-set-public-toggle"` | `<input type="checkbox">` | Host-only checkbox in the lobby that flips `SetGamePublic`. Visible only when the current connection id matches `state.CreatorPlayerId`. | *reserved* |
| `data-testid="lobby-public-name-input"` | `<input type="text">` | Friendly public-name input bound to the `SetGamePublic` `publicName` argument. Server trims + caps at 64 chars. | *reserved* |

> **Wire contract reminder.** Each `PublicGame` entry from
> `/api/matchmaking/lobby` is `{ gameId, publicName, creatorDisplayName,
> seatedCount, maxSeats, variant, createdAt }`. The frontend type-guard
> (`matchmaking.ts:isPublicGame`) currently expects `seatsTaken` +
> `seatsTotal` instead — the wire shipped by Bishop is the source of
> truth; `matchmaking.ts` is the regression risk. Backend wire-shape
> assertions live in `MatchmakingLobbyEndpointTests`.

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
| `data-testid="stats-panel"` | `<div class="stats-panel">` | Root grid container. Carries the panel testid so callers can scope-query into the rows. | `src/frontend/autotable-src/src/stats.ts:17, 137` |
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
