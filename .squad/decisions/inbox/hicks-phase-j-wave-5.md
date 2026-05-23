# Hicks — Phase J Wave 5: public matchmaking lobby + profile drawer + stats panel

> Author: Hicks (Senior Frontend Engineer)
> Branch: `stlong/phase-j-wave-5-completion`
> Scope: Frontend (Wave 5 §Tasks 1 + 2 + 3) — public matchmaking lobby
> UI, profile drawer, player stats display.  Three surfaces, one
> commit, zero new ad-hoc dependencies (`@microsoft/signalr` was added
> by Apone's DevOps commit).

## Summary

Wave 5 turns Bishop's backend (matchmaking REST endpoint + SignalR
profile/matchmaking RPCs + `PlayerProfile` + `PlayerStats`) into three
user-facing surfaces:

1. **Public matchmaking lobby pane.** Tab strip splits the existing
   "My Game" content from a new "Public Games" browser.  The browser
   polls `GET /api/matchmaking/lobby` every 5 s while active, renders
   up to 50 cards from Bishop's `LobbyGameDto`, and surfaces a "Join
   Random" button that invokes the `JoinRandom` SignalR RPC.  Host-
   only "Make public" toggle wires up `SetGamePublic` (with optional
   friendly name, ≤64 chars).

2. **Profile drawer.** Right-edge slide-in panel with display-name
   editor (1..32 chars, server-validated), 8 avatar-colour presets +
   free-form `<input type="color">`, live preview chip + name, Save /
   Reset CTAs, "Saved ✓" inline note.  Backed by a SignalR-listening
   profile store (`profile.ts`) that subscribes to the hub's
   `ProfileLoaded` server event, caches the resolved profile, and
   provides a `snapshotStatsForGame()` helper for the post-game modal.

3. **Stats panel.** Two surfaces consume the shared `stats.ts`
   builders — the lobby's "Your stats" card (`#lobby-stats-panel`)
   shows current career counters; the post-game modal gains a
   `game-complete-stats-delta` section that renders the same counters
   with per-row Δ vs the pre-game snapshot.

## Wire contract (verified against Bishop's `ChangshaHub.cs`)

- `GET /api/matchmaking/lobby` (REST) → `{ games: LobbyGameDto[] }`
  where each entry is
  `{ gameId, publicName: string | null, creatorDisplayName,
     seatedCount, maxSeats, variant, createdAt }`.
- SignalR hub at `/hubs/changsha`:
  - Server→Client event `'ProfileLoaded'(dto)` fires from
    `OnConnectedAsync` — DTO shape `{ playerId, displayName,
    avatarColor, createdAt, lastSeenAt, stats: { gamesPlayed,
    gamesWon, totalScore, highestSingleGameScore, longestWinStreak,
    currentWinStreak, lastGameAt } }`.
  - Client→Server `'UpdateProfile'(displayName, avatarColor?)` →
    returns the updated `ProfileDto`.  Throws `HubException` on
    validation failure (translated to inline error in the drawer).
  - Client→Server `'SetGamePublic'(gameId, isPublic, publicName?)` →
    `{ success: bool, isPublic: bool, publicName: string | null }`.
    Throws when the caller isn't the host.
  - Client→Server `'JoinRandom'(variant?)` →
    `{ matched: true, gameId, seatIndex }` or `null` when no joinable
    public game is available.

The frontend stats stat-name normalisation in `profile.ts` maps
Bishop's verbose names (`longestWinStreak` → `longestStreak`, etc.)
so `stats.ts:STATS_TESTIDS` stays free of `WinStreak`/`SingleGame`
suffixes and the post-game delta builder sees a flat shape.

## File inventory

### New files (`src/frontend/autotable-src/src/`)

| File | Purpose |
|---|---|
| `hub.ts` | SignalR connection singleton + `invokeHub` wrapper.  URL strategy: `/hubs/changsha` same-origin in prod; `http://localhost:5000/hubs/changsha` in dev; overridable via `?hub=<url>` query param. |
| `profile.ts` | Profile store (in-memory cache + `EventEmitter`), drawer mount, validation, pre-game snapshot helper, idempotent SignalR `ProfileLoaded` subscription. |
| `matchmaking.ts` | REST poller (5 s, `MATCHMAKING_POLL_MS`, capped at 50 cards) + SignalR `joinRandom` / `setGamePublic` wrappers.  AbortController-based cancel on tab-off. |
| `stats.ts` | `formatStats` + `formatStatsDelta` DocumentFragment builders.  Shared `STATS_TESTIDS` table consumed by both lobby + post-game surfaces. |
| `main.css` | Wave-5 surfaces: tab strip, public-game cards, make-public, profile drawer, stats grid.  Layered after `style.css` so its rules win. |

### Modified files

| File | Change |
|---|---|
| `client.ts` | Connect handler kicks off `getHubConnection() + loadProfile + snapshotStatsForGame`; gameComplete-flag handler triggers `refreshProfile`; profile.displayName mirrored into `client.nicks[playerId]`; `clearReconnectSession` now also tears down the hub. |
| `client-ui.ts` | `setupPostGameStatsPanel()` listens on `gameComplete` + `onProfile` to render the delta section.  Tolerates missing pre-game snapshot. |
| `lobby.ts` | Install hooks: `installProfileDrawer/Toggle`, `installLobbyTabs`, `installPublicGamesPane`, `installMakePublicToggle`, `installLobbyStatsPanel`.  Player chip rendering now uses `resolveDisplayName / resolveAvatarColor` so the local player's profile overrides the WS-broadcast nick + the djb2 hue fallback. |
| `index.html` | Wave-5 markup: main.css `<link>`; `#game-complete-stats-delta` placeholder inside the game-complete modal; lobby tab strip + my-game pane wrapper; public-games pane (Join Random, error/empty states, card list host); make-public section (toggle, name input, status); lobby stats panel host; open-profile shortcut button in lobby-settings-shortcut; full profile drawer (`<aside id="profile-drawer">`). |
| `tests/selectors.md` | Wave-5 public-matchmaking section moved out of "*reserved*" into live contract with source-line citations; player-stats panel section gains a `lobby-stats-panel` row; profile-drawer section gains rows for the supplemental `data-testid` attrs I shipped alongside the existing `id`-based selectors. |

### Built bundles (`src/frontend/autotable/`)

- New: `autotable-src.4c6071a7.js` (1.17 MB), `autotable-src.3501ce9a.css` (7.4 kB).
- Pruned (stale): `autotable-src.0b7c71c7.js`.
- Unchanged (kept): `autotable-src.094cde3a.css` (Wave-4 carryover), `autotable-src.df85b4c4.css` (bootstrap).

## Test-id inventory (Wave 5 surface)

Verified live in the built `index.html` + `lobby.ts:buildPublicGameCard`:

**Lobby tabs / public games**
- `lobby-my-game-tab`, `lobby-public-games-tab`
- `lobby-public-section`, `lobby-public-list`
- `lobby-public-game-{0..49}` (card root)
- `lobby-public-game-name-{0..49}` (publicName fallback to "<host>'s game")
- `lobby-public-game-host-{0..49}` (creatorDisplayName)
- `lobby-public-game-seats-{0..49}` ("N / M" text)
- `lobby-public-game-join-{0..49}` (Join button; `disabled` when seats are full)
- `lobby-join-random` (Join Random button)
- `lobby-set-public-toggle` (host-only checkbox)
- `lobby-public-name-input` (friendly name input)

**Stats**
- `lobby-stats-panel` (lobby host)
- `stats-panel` (grid root inside both surfaces)
- `stats-games-played`, `stats-games-won`, `stats-win-rate`,
  `stats-longest-streak`, `stats-current-streak`, `stats-highest-score`

**Profile**
- `lobby-open-profile` (shortcut button in lobby footer)
- `profile-drawer` (root), `profile-display-name-input`,
  `profile-avatar-color-custom`, `profile-save`, `profile-reset`
  (testids supplement the existing `id` contract documented in
  `tests/selectors.md`; the `id` family remains the primary anchor
  because the drawer also doubles as an `aria-controls`/
  `aria-labelledby` target)
- `profile-avatar-color-preset-{0..7}` (8 preset buttons emitted by
  `profile.ts:installProfileDrawer` from `AVATAR_COLOR_PRESETS`)

## Stability

- **TypeScript strict** (`npx tsc --noEmit --strict --target es6
  --moduleResolution bundler --esModuleInterop --lib
  DOM,DOM.Iterable,es6,es2017 src/index.ts`): **exit 0, no
  diagnostics**.
- **Parcel build** (`npx parcel build ... --no-source-maps --no-cache`):
  succeeded in ~3 s; output bundles listed above.  Stale `0b7c71c7.js`
  pruned in the same commit.
- **Backend tests** (`dotnet test src/backend/Mahjong.Autotable.slnx`):
  **445 / 0 / 0 green** — same as before Hicks's commit (no backend
  changes; run confirms the wire-shape contract suite Bishop landed in
  Wave 5 still passes against the docs the frontend consumes).

## Design notes

- **Identity mismatch.** Profile `playerId` is SignalR's
  `Context.ConnectionId`; the autotable WebSocket connection has its
  own playerId.  These are two parallel identities for the same
  person; bridging the two for *remote* players is out of scope for
  Wave 5.  Resolution: the lobby's profile-aware chip renderer
  overrides only the LOCAL user's nick/colour — remote chips continue
  to use the WS-broadcast `nicks` collection.  `client.ts` writes
  `nicks[localPlayerId] = profile.displayName` on every `onProfile`
  event so other players see the updated display name through the
  existing WS broadcast.
- **`@microsoft/signalr` dependency.** Already in `package.json` from
  Apone's DevOps commit (`process` polyfill added there too because
  signalr's source uses Node `process.platform`).  Hicks's commit adds
  no new dependencies.
- **Lobby tab activation toggles the matchmaking poll.** The
  My-Game tab actively stops the 5 s poll loop so the REST endpoint
  isn't hammered while users are tweaking pickers.
- **Drawer-id vs. testid.** The profile drawer mixes `id`-based and
  `data-testid`-based selectors.  The `id` set is authoritative
  (drawer + close button + form inputs all double as
  `aria-controls`/`aria-labelledby` anchors); the supplemental
  `data-testid` overlay was added on the most-clicked elements (Save,
  Reset, drawer root, display-name input, custom color) so future
  Playwright suites can pick the convention that fits.
- **First-game-in-tab stats delta.** When the pre-game snapshot is
  missing (a fresh tab with no prior `snapshotStatsForGame()` call),
  the post-game modal renders the current stats with no Δ badges
  instead of leaving the section blank.

## Cross-agent coordination

- **Bishop:** Wire contract verified against `ChangshaHub.cs` line-
  by-line.  No request to change Bishop's DTO names — `profile.ts`
  normalises `longestWinStreak` → `longestStreak`, etc., so the front-
  end stats shape stays terse without touching backend.
- **Vasquez:** Selectors catalog (`tests/selectors.md`) now covers
  every Wave-5 testid with file:line citations.  The Public Matchmaking
  section moved out of the "*reserved*" block; the Stats and Profile
  sections gained the rows my markup actually ships.
- **Apone:** No-op this wave — Apone's DevOps commit already provided
  the `@microsoft/signalr` + `process` polyfill dependencies the
  frontend needed.  The Playwright smoke spec in `tests/e2e/smoke.spec.ts`
  uses only testids that exist in Wave-4-era HEAD; Wave-5 testids land
  fresh for Wave-6 acceptance suites to target.

## Open questions / future work

- **Reset-to-default vs. server canonical.** `profile-reset` currently
  reverts the form's in-flight edits to the *server's current value*
  (no `DeleteProfile`-style RPC).  If the product wants a true reset
  (restore display name to "Player <connId-prefix>", regenerate
  avatar) it'd be a Wave-6 server RPC + UI confirmation flow.
- **Public-name persistence.** Hosts that flip "Make public" off then
  on lose their friendly name.  Acceptable for V1; future polish could
  cache the last public-name client-side.
- **Avatar colour propagation to remote chips.** Same identity-mismatch
  rationale above — the only way to color remote chips by their
  profile colour is to extend the WS `nicks` broadcast into a
  `{ nick, color }` payload, which is a coordinated Bishop+Hicks
  change for a future wave.
