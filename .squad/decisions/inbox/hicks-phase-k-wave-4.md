# Hicks — Phase K Wave 4 memo

**Branch:** `stlong/phase-k-wave-4-bringup`
**Date:** 2026-06-14
**Author:** Hicks (Frontend Engineer)
**Scope:** scene chunk split into `scene-shell` + `scene-effects`,
unified `game-state` reactive cache (replaces ad-hoc per-module
`/api/games/{id}/settings` probes), tournament sparse-mode seeding UI
(unseeded rows + 400 validation toast), inline 24×24 Microsoft brand
SVG, typed `VoiceHubResult` with reason-to-toast map.
**Build gate:** `parcel build` clean (~8 s); `tsc --noEmit --module
esnext` zero new errors beyond the Wave-3 baseline.

---

## Headline — scene chunk peeled into shell + effects

Wave 3 produced a single 922 kB `scene.<hash>.js` containing
three.js + AssetLoader + Game + World + ClientUi + MoveLog + the
~100 kB `GameUi` modal/settings/replay graph.  Wave 4 splits that
into:

1. **`scene-shell.<hash>.js` (renderer-critical)** — three.js +
   AssetLoader + Game (with `installGameUi()` lazy-injection) +
   World + ClientUi + MainView.  Mints `data-testid="scene-shell-ready"`
   after first WebGL frame composites; continues to mint
   `data-testid="game-scene-ready"` alongside for Wave-3 spec
   back-compat.
2. **`scene-effects.<hash>.js` (NEW deferred chunk)** — `GameUi` +
   `MoveLog`.  Dynamic-imported from `scene-shell.ts` immediately
   after first-frame, so the heavy DOM modals stream in parallel
   with the user's first interactions.  Mints
   `data-testid="scene-effects-ready"` once installation completes.

### Post-Wave-4 chunk budget

| Asset | Wave 3 | Wave 4 | Δ |
|---|---|---|---|
| Eager JS (`autotable-src.<hash>.js`)     | 214.1 kB | **218.7 kB** | +4.6 kB (game-state singleton wired into client) |
| Game shell (`game-bootstrap.<hash>.js`)  | 166.0 kB | **169.9 kB** | +3.9 kB (preloadGameBootstrap hook + game-state import) |
| Renderer shell (`scene-shell.<hash>.js`) (RENAMED) | 922 kB | **886.4 kB** | −35.6 kB (game-ui + move-log peeled) |
| Renderer effects (`scene-effects.<hash>.js`) (NEW) | — | 59.7 kB | game-ui + move-log + lazy-deps subgraph |
| `game-state.<hash>.js` (NEW)             | — | 1.94 kB | Singleton cache lazy-imported by voice + settings-drawer |
| **Total bytes on game URL** (shell + effects + game-state) | 1.09 MB | **1.12 MB** | +2.4 % bytes, but ~−40 kB on the renderer-critical first-paint chain |

**Honest size accounting.**  The total transfer rose slightly (~30 kB
across all chunks) because the game-state singleton + scene-effects
boundary code introduce per-module wrappers that parcel can't fully
inline across dynamic-import boundaries.  In exchange:

- `scene-shell` shed 35.6 kB of dead weight (modals/replay/settings
  that the user does not need to render their first frame).
- `scene-effects` streams in *parallel* with the tile-texture
  network round-trips, so the wall-clock cost is hidden behind
  GLB/PNG downloads.
- `voiceEnabled` and `ownerId` are cached once per session and
  pushed live via SignalR `GameJoined` instead of being re-fetched
  from each of `voice.ts` + `settings-drawer.ts` (the Wave-3 design
  re-fetched twice per page).

**Why scene-shell didn't hit the 500 kB target.**  three.js alone is
~575 kB minified, and AssetLoader's GLB pipeline pulls another
~120 kB of three.js extras.  Hitting 500 kB would require a third
layer that lazy-imports three (e.g. boot in 2D-fallback mode and
swap in WebGL after first interaction), which exceeds the Wave-4
scope.  Logged as **Wave 5 followup**.

Old `scene.<oldhash>.js` is auto-pruned by `generate-sw-manifest.js`
(see SW pre-cache section of Wave-3 memo) — its index.html → JS-of-JS
walker now follows the new shell → effects dynamic-import edge so the
effects chunk stays in the live set.

---

## game-state — single source of truth for per-table flags

Wave 3 wired the per-game `voiceEnabled` flag by giving each consumer
(`voice.ts`, `settings-drawer.ts`) its own `fetch('/api/games/{id}/settings')`
probe.  Two consumers + a planned third for the owner-only HUD chrome
meant we'd fan out into three independent fetches per page-load, none
of which could share a cache.

Wave 4 introduces `src/game-state.ts`, a tiny synchronous module
exporting:

```ts
interface GameState {
  gameId: string;
  ownerId: string | null;
  voiceEnabled: boolean;
  viewerIsOwner: boolean;
}
function getGameState(gameId: string): GameState | null;
function loadGameState(gameId: string): Promise<GameState | null>;
function subscribeGameState(fn: (s: GameState | null) => void): () => void;
function updateGameState(patch: Partial<GameState>): void;
function resetGameState(): void;
```

`client.ts` calls `loadGameState(gameId)` on connect (with the
in-flight dedup logic the module owns), and subscribes to the SignalR
`GameJoined` event so owner-handoff (Wave-K-3 was a stretch goal —
now real because Bishop is shipping owner-handoff in Wave 4) and
`voiceEnabled` flips arrive live without polling.

Consumers (`voice.ts`, `settings-drawer.ts`) call `getGameState` for
the cached snapshot, `loadGameState` on cold paths, and
`subscribeGameState` for live updates.  `shutdownVoice()` unsubscribes
+ `clearReconnectSession()` calls `resetGameState()`.

Fallback chain inside `loadGameState`:

1. `GET /api/games/{id}` — the canonical per-game read (Bishop's
   Wave-4 endpoint shape).
2. `GET /api/games/{id}/settings` — Wave-3 fallback for deployments
   that haven't shipped the canonical read yet.

---

## Sparse-mode seeding (Wave 4)

Wave 3 wired drag-drop seeding for `seeded` players already placed
into round-1 match slots.  Bishop's Wave-4 update lets admins POST
sparse seedings (`seedNumber: 0` marks an unseeded entry).

`buildSeedingPanel` in `tournaments.ts` was rewritten to:

- Render every registered player (seeded slots from round-1 matches
  ∪ `detail.players` minus already-seeded).
- Insert an "Unseeded" divider (`tournament-seeding-unseeded-divider`)
  between seeded rows (`#1..#N`) and unseeded rows (rank "—").
- Promote/demote rows via drag across the divider.
- POST entries shaped `{ playerId, seedNumber: number }` where
  unseeded rows carry `seedNumber: 0`.
- Surface the toast `"Tournament must have unique sequential seeds 1..N."`
  on 400 (matches Bishop's controller copy verbatim).
- Roll the optimistic state back to last-saved on any HTTP failure.

`postSeed` now returns `{ ok: boolean; status: number }` (via the
new private `doPostStatus` helper) instead of a bare boolean so the
400-validation branch can render its own toast copy.

`TournamentDetail.players: BracketSlot[]` is normalised by
`normalizeSlots()` from Bishop's `players` array (with backwards
compat for `registrations` / `entries` keys).

---

## Inline Microsoft brand SVG (Wave 4)

`auth.ts#microsoftIconSvg()` was rewritten as a self-contained 24×24
inline SVG: four 10×10 squares (#F25022 / #7FBA00 / #00A4EF /
#FFB900) with a 1 px gap on a `23×23` viewBox.  Accessibility moved
into the SVG itself:

- `role="img"`
- `aria-label="Microsoft"`
- `<title>Microsoft</title>` child element (matches WAI-ARIA SVG name
  computation algorithm).

Wrapper span at `auth.ts:572` no longer carries `aria-hidden="true"`
since the SVG itself is now the accessible name source — screen
readers were previously skipping the entire button label.

---

## Typed `VoiceHubResult` + reason map (Wave 4)

Bishop's voice hub now returns a typed result instead of a free-text
string.  `voice.ts` adds:

```ts
interface VoiceHubResult { ok: boolean; reason: string }
function voiceReasonToText(reason: string): string;
function readVoiceResult(raw: unknown): VoiceHubResult;
```

`voiceReasonToText` maps the six Wave-4 codes
(`voice-not-enabled` / `not-seated` / `spectator` / `rate-limited` /
`target-not-found` / `unauthorized`) to user-facing copy, with
tolerant casing/punctuation handling (`Spectator`, `voice_not_enabled`,
`Voice-Not-Enabled` all map identically).

`readVoiceResult` accepts three wire shapes for forward/backward
compat:

1. `null` / `undefined` — treat as `{ ok: true, reason: '' }`.
2. `string` (Wave-3 legacy) — `"ok"` ⇒ ok, anything else ⇒
   `{ ok: false, reason: <string> }`.
3. `{ ok, reason }` (Wave-4 typed) — accepted as-is, with PascalCase
   aliases tolerated.

`toast.ts#showVoiceToast` keeps the Wave-3 substring heuristic for
back-compat with deployments that haven't switched to typed results.

---

## Cross-team handoffs

- **Bishop** — `GameJoined` SignalR payload must include
  `{ gameId, ownerId, voiceEnabled }` (Wave-4 contract).  We
  graceful-degrade to `{ ownerId }` only, but live `voiceEnabled`
  flips depend on the full payload.  Wave-3 `/api/games/{id}/settings`
  fallback path is retained so we don't regress against pre-Wave-4
  Bishop builds.
- **Vasquez** — new testids: `scene-shell-ready`, `scene-effects-ready`,
  `tournament-seeding-unseeded-divider`, sparse seed rows tagged
  `data-seeded="false"`.  Existing `game-scene-ready` continues to
  fire from `scene-shell.ts` (no spec breakage).  Sparse-seeding
  Playwright spec deferred to Vasquez Wave-5 backlog.
- **Apone (BE)** — sparse seed wire is unchanged from Bishop's draft:
  `POST /api/tournaments/{id}/seed` with body `{ seeds: [{ playerId,
  seedNumber }, …] }` where `seedNumber: 0` marks unseeded.

## Wave 5 notes (followups)

- Lazy-import three.js into a third chunk so `scene-shell` falls
  below 500 kB.  Requires AssetLoader → World refactor to defer
  GLB/Texture loaders.
- Replace `data-testid="game-scene-ready"` callers (Vasquez specs)
  with `scene-shell-ready` and remove the back-compat marker emit.
- Add keyboard-accessible re-ordering to the sparse seeding panel
  (currently mouse drag only — Wave-3 backlog item still standing).
