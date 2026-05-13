> **⚠️ SUPERSEDED — see `.squad/decisions/inbox/ripley-pivot-plan.md`.**
> The architecture described below was abandoned in the pivot to autotable-vendored Changsha-native. Kept for archaeology only; will be hard-deleted in Phase E.
---

# Changsha 3D Renderer — Scoping Spike

> Author: Hicks (Frontend Dev) · Status: spike (read-only) · Date: 2026-05-13
> Branch: `stlong/changsha-3d-renderer-spike` · Phase 3 baseline: PR #25 / a03feda

This document scopes the work required to make the **perspective 3D
autotable view** at `/changsha` reflect actual Changsha game state. Today
that view is theater: the bundled three.js scene at
`src/frontend/autotable/autotable.9519e86d.js` is hard-wired to the
upstream `pwmarcz/autotable` WebSocket protocol and has zero awareness of
the Changsha SignalR contract. The bridge layer in front of it (postMessage
parent → child) only updates a debug `<div>` overlay.

---

## 1. Executive Summary

**The gap.** The autotable bundle at `/autotable/` renders a three.js
scene driven exclusively by the upstream `Client` (`base-client.ts`)
listening on a WebSocket. Our React app at `/changsha` embeds the bundle
in an iframe and posts JSON messages of shape
`{ proto: 'changsha-bridge/1', type, ... }` to it. The receiver script
`changsha-bridge-receiver.js` re-emits those as `changsha-bridge:*`
CustomEvents on `window`. **The bundle never registers a listener for
any of them** — verified by grepping the minified bundle:
`grep -c "changsha-bridge" src/frontend/autotable/autotable.9519e86d.js` ⇒ 0.
The only side effect the receiver achieves on the canvas today is flipping
the opacity of the `#dice-img` sprite via DOM mutation.

**Recommendation.** Strategy **C — Fake autotable WS server** (collocate a
WebSocket endpoint inside `Mahjong.Autotable.Api` that speaks upstream's
`NEW`/`JOIN`/`JOINED`/`UPDATE` collection protocol and translates
authoritative Changsha state into `things`/`match`/`dice` collection
mutations). Confidence: **high** for MVP wall + dealt-hand rendering;
**medium** for animated batch-draw and bidirectional canvas → hub events.

**Complexity.** **L** (~3–5 days backend + ~1 day frontend wiring for
Phase 5a; another **M** for each follow-on phase). Most of the work is
backend WS plumbing and a slot-name translation table.

**Why C over the others, in one paragraph.** Upstream's renderer is
already a complete, well-debugged three.js application driven by exactly
seven collections. Re-implementing that as a React/three.js component
(Strategy D) is a 2–4 week rewrite with no UX upside — Stephen explicitly
values the upstream perspective look. Patching the minified bundle
(Strategy B) is fragile across rebuilds and tightly couples our React
runtime to one specific bundle hash. Forking `client.ts` and rebuilding
(Strategy A) loses byte-identity with upstream and pulls Parcel into our
build. Strategy C keeps the bundle byte-identical (it thinks it's talking
to upstream's server), centralises the translation in TypeScript-free
C# code we already own, and reuses the existing tested upstream rendering
of the wall, hands, discards, dice, and dealer indicator.

---

## 2. Upstream Inventory

### 2.1 Static assets in `src/frontend/autotable/`

| File | Size | Role |
|---|---:|---|
| `autotable.9519e86d.js` | 1015529 B | Minified Parcel bundle of upstream `src/index.ts` |
| `autotable.26d3665b.css` | 1995 B | Layout CSS for `#main`, seat-buttons, sidebar |
| `about.315e95c8.css` | 143887 B | Bootstrap + about page |
| `models.auto.72ee60ea.glb` | 206656 B | three.js mesh source: `tile`, `stick`, `center`, `tray`, `marker` |
| `tiles-labels.auto.9a041239.png` | 73644 B | 512×512 tile-face atlas (labelled variant) |
| `table.60230825.jpg` | 106201 B | Table-felt repeat texture (512×512) |
| `dice.auto.391822b5.png` | 73644 B (claimed; actually 384×64 sprite) | Six-face dice sprite strip drawn on the center canvas |
| `dealer.a27808af.png` | 43477 B | "Dealer" marker tile face |
| `winds.d327f3d8.png` | 24415 B | Wind tile sprites for the wind-deal mode |
| `round.4be01226.png`, `pay.cb36a415.png`, `unseat.c79b32aa.png` | small | Icons |
| `discard.c3151c81.wav`, `stick.207ef49b.wav` | small | SFX |
| `Segment7Standard.f1d05002.otf` | 10464 B | Score-readout digital font |
| `game.332493fc.mp4` | 951257 B | Splash video on the about page (not loaded in normal flow) |
| `index.html`, `about.html` | small | Entry pages |
| `changsha-bridge-receiver.js` | 4881 B | **Our** addition; loaded after the bundle |

Confirmed via `file(1)`:
- `tiles-labels.auto.9a041239.png` — PNG, **512×512**, 8-bit RGB.
- `models.auto.72ee60ea.glb` — glTF binary, version 2, **206656 B**.
- `dice.auto.391822b5.png` — PNG, **384×64**, 8-bit colormap (6 dice faces of 64×64).
- `table.60230825.jpg` — JPEG, 512×512, repeated 3× across the table mesh.

### 2.2 Bundle public surface

`grep` over `autotable.9519e86d.js` confirms:
- **Window globals:** only `window.__THREE__` (set by three.js itself, not by upstream's code).
- **DOM listeners registered by the bundle:** standard `mousedown/up/move`, `keydown/up/press`, `change`, `click`, `contextmenu`, `resize`, `webglcontext*` (renderer), `DOMContentLoaded` and `load`. **No** `customEvent` / `bridge` / `changsha` listeners. **No** `message` listener.
- The bundle is fully self-contained: `ClientUi` instantiates `BaseClient` and connects to `ws://<host>/<path>` derived from `window.location` (see upstream `client-ui.ts` `getUrl()`).

The bundle therefore offers no JavaScript API. The only external surfaces
are (a) the WebSocket it opens, and (b) the DOM elements it manipulates
(`#dice-img`, `#center`, `#status-text`, the per-seat take-seat buttons,
the deal button).

### 2.3 Scene graph

From upstream `src/main-view.ts`, `src/world.ts`, `src/object-view.ts`:

- World size: a single constant `World.WIDTH = 174` (arbitrary units; tiles are 6×9×4 in `Size.TILE`).
- Coordinate origin: `(0,0,0)` is one corner of the square table. The view group is translated by `(WIDTH/2, WIDTH/2, 0)` and rotated by `seat × π/2` so the local seat is always at the bottom.
- Camera, perspective mode: `PerspectiveCamera(fov 30, aspect 1.5, near 0.1, far 1000)`; position `(0, -WIDTH*1.44, WIDTH*1.05) ≈ (0, -250.6, 182.7)`; rotated `(0.3π − lookDown × 0.2, 0, 0)`. Zoom is scalar along a `(0, 1.37, -1)` axis.
- Camera, orthographic mode: `OrthographicCamera(±WIDTH*1.2/2, ±WIDTH*1.2/3, 0.1, 1000)`; position `(0, -53*lookDown − WIDTH, 174)`; rotation `(π/4, 0, 0)`. Toggle via `P` key.
- Static meshes added at startup (`object-view.ts addStatic`):
  - Table: `PlaneGeometry(WIDTH+Size.TILE.y, WIDTH+Size.TILE.y)` centred on `(WIDTH/2, WIDTH/2, 0)`.
  - Center pad: `loader.makeCenter()` placed at `(WIDTH/2, WIDTH/2, 0.75)` — the canvas-textured score / dealer / dice surface.
  - Trays: 4 seats × 6 stick trays merged into one geometry, positioned along the inner edge of each seat.
- Dynamic meshes are owned by three `ThingGroup`s (`TileThingGroup`, `StickThingGroup`, `MarkerThingGroup`). Each group keeps one `InstancedMesh` plus an `Array<Mesh>` of cloned originals; `setSimple` writes the per-instance matrix, `setCustom` swaps to a real mesh for hover/select/held/drop animation. This is exactly the abstraction we'd hook into to render Changsha state.
- Selection outline: `OutlinePass` driven by `objectView.selectedObjects`.

### 2.4 Tile atlas analysis (critical for Strategy C / D)

`src/thing-group.ts`:

```ts
const TILE_DU = 32 / 256;   // 0.125  — atlas column width in UV
const TILE_DV = 40 / 256;   // 0.156… — atlas row height in UV

getOffset(typeIndex: number): Vector3 {
  const x = (typeIndex % 37) % 8;          // atlas column
  const y = Math.floor((typeIndex % 37) / 8); // atlas row
  const back = Math.floor(typeIndex / 37);    // 0 = standard, 1 = alt back
  return new Vector3(x * TILE_DU, y * TILE_DV, back * TILE_DV);
}
```

The atlas is 512×512 pixels, but each tile face occupies a 32×40 cell and
only the **upper-left 256×256 quadrant** is addressed for faces (8 cells
wide). The rest of the atlas holds back colors (selected by the `back`
offset in the shader's vMapUv.y path).

**Upstream typeIndex → tile face:**
- 0–8: man / wàn (萬) ranks 1–9
- 9–17: pin / tǒng (筒) ranks 1–9
- 18–26: sou / tiáo (条) ranks 1–9
- 27–30: winds (E/S/W/N) — **unused by Changsha v1**
- 31–33: dragons (white/green/red) — **unused by Changsha v1** (per spec lock 2026-05-13)
- 34–36: red 5-man / red 5-pin / red 5-sou — **unused by Changsha v1**

**Changsha tile id (0–107) → upstream typeIndex:**
```
typeIndex = Math.floor(tileId / 4)
```
This maps cleanly because Changsha and upstream agree on suit ordering
(wan, tong, tiao) and rank ordering (1–9), and the upstream atlas places
those three suits in the first 27 cells (rows 0–3, columns 0–2). The
remaining cells (winds, dragons, red fives) are not needed.

**Verification:** atlas column 0, row 0 = 1wan; column 0, row 1 = 9wan;
column 1, row 1 = 1tong; column 2, row 3 = 9tiao. Spot-check against the
PNG visually if eyes need confirmation, but the math from
`thing-group.ts:217-221` is unambiguous.

### 2.5 Wall layout (upstream)

From `src/setup-slots.ts`:
- `start('wall'), row(19), stack(), seats()` ⇒ each seat builds **19 columns × 2 layers = 38 slots**. Four seats × 38 = **152 slots** total.
- Upstream Riichi uses 136 tiles → 16 wall slots left empty; the unused
  slots tend to be at the right edge per the standard riichi deal.
- Slot name: `wall.{col}.{layer}@{seat}` where `col ∈ [0,18]`, `layer ∈ {0,1}`, `seat ∈ {0,1,2,3}`.
- Origin of the seat-0 wall: `(30, 20, 0)` in world units. `row()` strides by `Size.TILE.x = 6` per column; `stack()` strides by `Size.TILE.z = 4` per layer. Tiles face down (`Rotation.FACE_DOWN`).

For Changsha's 108 tiles we need 108 of the 152 slots populated. A clean
mapping is in §5.2.

---

## 3. Bridge Gap Analysis

### 3.1 Upstream WebSocket protocol (from `server/protocol.ts` + `server/game.ts`)

```ts
// Client → server
{ type: 'NEW' }
{ type: 'JOIN', gameId: string }
{ type: 'UPDATE', entries: Entry[], full: false }

// Server → client
{ type: 'JOINED', gameId: string, playerId: string, isFirst: boolean }
{ type: 'UPDATE', entries: Entry[], full: boolean }

type Entry = [kind: string, key: string | number, value: any | null];
```

Lifecycle:
1. Client opens a WebSocket and sends `NEW` (server allocates a 5-char `gameId`) or `JOIN { gameId }`.
2. Server responds with `JOINED { gameId, playerId, isFirst }`. `isFirst` is true for the first client of a game and causes the client to (a) send its initial values for `sendOnConnect` collections, (b) declare `unique`/`ephemeral`/`perPlayer` constraints.
3. Server pushes `UPDATE { entries, full: true }` to bring the new client to current state (omits ephemeral collections).
4. Any further mutation by any client is broadcast as `UPDATE { entries, full: false }` to all connected clients.

Notable server behavior (`server/game.ts`):
- Server is a flat key-value store partitioned by `kind`. It has zero game-rules awareness.
- `unique` constraint: server rejects an update that would create two values sharing the same `unique` field (e.g. two `things` in the same `slotName`).
- `ephemeral` collections (`sound`, `dice`) are broadcast but not persisted, so reconnecting clients don't see them.
- `perPlayer` collections are auto-cleared when a player disconnects.

### 3.2 Upstream client-state model (`src/client.ts`)

Seven `Collection<K,V>` instances on the `Client` class, ordered:
1. `match: Collection<number, MatchInfo>` — sendOnConnect (always key=0)
   - `{ dealer: 0|1|2|3, honba: number, conditions: Conditions }`
2. `seats: Collection<string, SeatInfo>` — unique:"seat", perPlayer
   - per-playerId `{ seat: 0|1|2|3|null }`
3. `things: Collection<number, ThingInfo>` — unique:"slotName", sendOnConnect
   - per-thingIndex `{ slotName, rotationIndex, claimedBy, heldRotation: {x,y,z,w}, shiftSlotName }`
4. `nicks: Collection<string, string>` — perPlayer
5. `mouse: Collection<string, MouseInfo>` — rateLimit:100, perPlayer
6. `sound: Collection<number, SoundInfo>` — ephemeral (key=0)
7. `dice: Collection<number, DiceInfo>` — ephemeral (key=0)
   - `{ dice: [d1, d2], state: 'rolled' | 'ignore' }`

Mutations flow as `[kind, key, value]` triples. The renderer subscribes
to `collection.on('update', ...)` and re-derives scene state from the
union of all entries. Key insight: **a tile is `things[index]` and its
position is `slotName`.** Move it, the renderer animates the move.

### 3.3 Our bridge layer

`src/frontend/modern/src/changsha/autotableBridge.ts:135-189` — `diffAndSend`:
- Compares two `ChangshaGameState` snapshots and sends one or more of:
  `hello`, `reset`, `phase`, `dice`, `breakPoint`, `tilesDealt`, `tileDiscarded`.
- All messages use the postMessage envelope `{ proto: 'changsha-bridge/1', type, ... }`.

`src/frontend/autotable/changsha-bridge-receiver.js:63-114`:
- Receives `window.message` events, validates `proto`, and for the message types it knows about:
  - Updates an internal `sceneState` (tile counts, dice, breakPoint, discards).
  - Mutates the debug overlay (`#changsha-bridge-overlay`).
  - **For dice:** toggles `document.getElementById('dice-img').style.opacity = '1'`.
  - **For `tilesDealt`, `tileDiscarded`, `claimMade`:** dispatches a `CustomEvent` named `changsha-bridge:{type}` on `window`.

**Theater confirmation.** Listener inventory for the bundle:
```
$ grep -c "changsha-bridge" src/frontend/autotable/autotable.9519e86d.js
0
$ grep -oE "addEventListener\\([\"'][^\"']+[\"']" src/frontend/autotable/autotable.9519e86d.js | sort -u
addEventListener("change"
addEventListener("click"
addEventListener("contextmenu"
…
addEventListener("resize"
addEventListener("…")   # none with "changsha", "bridge", "message", or "tilesDealt"
```
Zero listeners. The CustomEvents fire into the void. The dice opacity
flip is the only observable canvas-side change.

### 3.4 Net consequence

The bundle continues to display whatever the upstream sandbox renders on
init (no game → walls plus tile-back pattern; if a user manually clicks
Deal, a Riichi 136-tile deal). It never reflects our 108-tile Changsha
deal, our dice values (other than the dice sprite fading in), our
discards, or our claims.

---

## 4. Strategy Options

### A. Fork upstream `client.ts`

**Description.** Clone the upstream repo, replace `BaseClient`'s
WebSocket plumbing with a postMessage / SignalR proxy, keep
`Client`'s seven `Collection`s, keep `World` / `MainView` / `ObjectView`
unchanged, rebuild with upstream's Parcel toolchain, ship the new
bundle to `src/frontend/autotable/`.

**Files touched.** `src/base-client.ts`, possibly `src/client-ui.ts`
(connection lifecycle), `package.json` (Parcel deps), our
`src/frontend/autotable/*` (replace all assets with new build).

**Asset reuse.** 100% — same atlas, same GLB, same code that already
works.

**Pros.** Most architecturally honest. Server still upstream-shaped;
React app drives bundle through a typed object port.

**Cons.** Pulls in Parcel/Yarn + 50+ upstream npm deps as a vendored
build pipeline. Bundle hash changes ⇒ loses byte-identity with upstream
(important per `src/frontend/autotable/README.md`). Future upstream
updates merge through git instead of a re-mirror.

**Risk.** Medium-high — first-time Parcel setup in this repo.

**Effort.** ~600–900 LOC delta (mostly replacing `BaseClient` and the
build/CI scaffolding). **L** (~4–5 days for an experienced TS dev).

---

### B. Patch the bundle (DOM-event bridge listener)

**Description.** Author a second JS file (separate from
`changsha-bridge-receiver.js`) that reaches into the bundle's scene by
accessing globals the bundle exposes. **Problem:** the bundle exposes
no globals beyond `window.__THREE__`. The `Client`, `World`,
`ObjectView` instances are closed over inside Parcel's IIFE.

To make this strategy viable we'd have to **monkey-patch** the bundle:
intercept `WebSocket` (or `EventTarget.prototype.dispatchEvent`) and
inject synthetic `UPDATE` payloads. The bundle does instantiate its
`WebSocket` only after the user clicks the Connect button in the
sidebar (which we don't render in the iframe), so we'd also have to
auto-click Connect. **Or** patch `BaseClient.prototype.connect` via a
prototype override before the bundle runs — but the class is not on
`window`, so we'd need a `WebSocket` proxy that intercepts the open
call and replies with fabricated server frames.

**Files touched.** New file in `src/frontend/autotable/`, plus an
`index.html` `<script>` tag.

**Asset reuse.** 100%.

**Pros.** Bundle stays byte-identical. Lives entirely on the frontend.

**Cons.** Extremely fragile. Anything Parcel does on next build (variable
renaming, IIFE rewrap, terser inlining) can break our patch. Also
requires re-implementing `Client`'s `unique`/`ephemeral` constraint
logic on our side because we'd be replying as the "server".

**Risk.** High — silent breakage on bundle rebuild.

**Effort.** ~400–700 LOC of WebSocket-proxy gymnastics; **M** (~3 days)
for happy path but fragile; full Strategy A or C is preferable for
production.

---

### C. Fake autotable WS server (recommended)

**Description.** Add a `/autotable/ws` WebSocket endpoint to
`Mahjong.Autotable.Api` that speaks upstream's `NEW`/`JOIN`/`JOINED`/
`UPDATE` protocol. When a Changsha React client opens a game, the
backend pairs that game with an autotable game id; the WS endpoint
emits collection mutations (`things`, `match`, `dice`, `seats`,
`nicks`) derived from the authoritative Changsha state. The autotable
bundle connects to `/autotable/ws` unchanged.

**Files touched.**
- New: `src/backend/src/Mahjong.Autotable.Api/Autotable/AutotableWsEndpoint.cs` (~200 LOC)
- New: `src/backend/src/Mahjong.Autotable.Api/Autotable/AutotableProtocol.cs` (~80 LOC; record types for `Entry`, `JoinedMessage`, `UpdateMessage`)
- New: `src/backend/src/Mahjong.Autotable.Api/Autotable/ChangshaToAutotableTranslator.cs` (~300 LOC; pure-functional `ChangshaGameState → Entry[]`)
- New: `src/backend/src/Mahjong.Autotable.Api/Autotable/AutotableSlotMap.cs` (~120 LOC; wall/hand/discard/meld slot name calc — see §5)
- Edit: `src/backend/src/Mahjong.Autotable.Api/Program.cs` (~5 LOC; map WS endpoint)
- Edit: `src/frontend/autotable/index.html` (~10 LOC; remove the React-mode-incompatible deal button visibility, or leave for sandbox use)
- Edit: `src/frontend/modern/src/pages/ChangshaTablePage.tsx` (~30 LOC; pass `gameId` as a URL parameter to the iframe `src` so the bundle issues a `JOIN` for the right game)
- New: `src/backend/test/Mahjong.Autotable.Api.Tests/AutotableTranslatorTests.cs` (~150 LOC)

Total: ~900 LOC new / ~50 LOC modified.

**Asset reuse.** 100% — bundle stays byte-identical (preserves the README
guarantee), atlas/GLB unchanged.

**Pros.**
- Bundle thinks it's talking to upstream. Zero JS modification.
- Authoritative state lives in the backend; the only client-side glue is
  the URL parameter passed to the iframe.
- We can test the translator with backend unit tests (no headless
  browser needed for the protocol).
- Reuses upstream's existing tile-movement animation, dice rendering
  (push a `dice` collection update with `state: 'rolled'`), dealer marker,
  outline pass.
- Free upstream feature parity for future strategies (multi-spectator,
  per-seat camera) without re-implementing.

**Cons.**
- New WebSocket surface in the .NET backend (alongside SignalR).
  Mitigation: ASP.NET Core supports raw WebSockets via
  `app.UseWebSockets()` + `HttpContext.WebSockets.AcceptWebSocketAsync()`
  cleanly; no new package needed.
- Bidirectional canvas events (drag-discard) require interpreting the
  bundle's outbound `UPDATE` traffic — see §6.
- The bundle's Deal button can still trigger a client-side Riichi deal.
  Mitigation: hide it in `index.html` when the iframe is embedded
  (query-param check) or ignore client-initiated deal updates server-side.

**Risk.** Medium — requires careful slot-name translation; otherwise the
protocol is small (4 message types) and stateless.

**Effort.** ~900 LOC. **L** (3–5 days for backend WS + translator; ~1 day
for frontend wiring + tests).

---

### D. Thin three.js overlay in React

**Description.** Reuse only the assets (GLB + atlas PNG). Write a new
React component (Three.js or `@react-three/fiber`) that loads the GLB,
samples the atlas, builds a wall + 4 hand groups + 4 discard piles, and
drives them directly from `ChangshaGameState`. Drop the iframe entirely.

**Files touched.**
- New: `src/frontend/modern/src/changsha/three/AutotableScene.tsx` (~600 LOC)
- New: `src/frontend/modern/src/changsha/three/TileMeshFactory.ts` (~250 LOC; replicates upstream's UV-offset shader)
- New: `src/frontend/modern/src/changsha/three/SlotLayout.ts` (~200 LOC; wall/hand/discard positions in three.js coordinates)
- Edit: `src/frontend/modern/src/pages/ChangshaTablePage.tsx` to embed `<AutotableScene>` instead of an iframe
- New: `package.json` adds `three`, `@types/three`, optionally
  `@react-three/fiber`
- Drop: iframe + `autotableBridge.ts` + `changsha-bridge-receiver.js`
  for the `/changsha` route (preserve at `/autotable/` if we want the
  sandbox)

Total: ~1100–1400 LOC new / drop ~300 LOC bridge code.

**Asset reuse.** Partial — GLB and atlas reused, but the surrounding
geometry (table, trays, center pad, OutlinePass shader, score readout
canvas) all re-implemented.

**Pros.**
- One stack, one build pipeline (Vite), no Parcel.
- Bundle size shrinks (no second-app overhead).
- We get React state ↔ scene wiring for free.

**Cons.**
- Re-implements 3 KLOC of mature three.js code (movement physics, drop
  shadows, mouse-tracker animation, dice sprite, score readout). A lot
  of UX polish for free disappears.
- New three.js bundle adds ~600 KB to the modern app, partially offset by
  removing the iframe.
- Largest by total effort.

**Risk.** Medium-low (well-understood tech) but **high schedule risk**
because every upstream nuance (instanced meshes, hover outlines, drop
shadows, drag-snap, push semantics) has to be re-built.

**Effort.** **XL** (~10–15 days).

---

### E. Hybrid — C for renderer, D for HUD overlays

**Description.** Strategy C drives the in-iframe 3D scene as today.
Strategy D-style React overlays (already shipped: dice modal, banker
badge, fan panel) handle the Changsha-specific chrome that upstream can't
express (wind, scoring, fan breakdown). This is **what Phase 3 already
delivers as a 2D overlay** — extending it to ship Strategy C makes the
3D scene meaningful.

**Recommendation:** ship C as the next phase; D is a future optimization
only if the iframe boundary becomes a bottleneck. Hybrid is the realistic
end-state.

---

## 5. Asset / Atlas / Coordinate Mapping

### 5.1 Tile atlas mapping (for Strategy C or D)

Upstream uses the same atlas position for the same tile face index. For
Changsha tile ids:

```
upstreamTypeIndex = Math.floor(tileId / 4)   // 0..26
atlasColumn       = upstreamTypeIndex % 8
atlasRow          = Math.floor(upstreamTypeIndex / 8)
```

| Changsha tile id range | Suit | Face range | upstream typeIndex | atlas col,row |
|---|---|---|---|---|
| 0–35 (4 of each) | wan / man | 1–9 | 0–8 | (0..7, 0..1) — last cell wraps to (0,1) |
| 36–71 | tong / pin | 1–9 | 9–17 | (1, 1) … (1, 2) |
| 72–107 | tiao / sou | 1–9 | 18–26 | (2, 2) … (2, 3) |

No new tile glyphs are needed. Winds, dragons, and red fives all live at
typeIndex 27–36 but Changsha v1 never references them
(`docs/rules/changsha-spec.md` v1.2 §1).

### 5.2 Wall layout (108 tiles in upstream's 152-slot frame)

Per spec §3.1, the canonical Changsha wall has 54 stacks split as
14+14+13+13 across the four walls. Upstream allocates 19 wall columns ×
2 layers per seat (38 slots × 4 = 152). Recommended placement:

- **Seats 0 and 2 (East and West):** stacks 0..13 (14 stacks × 2 layers = 28 tiles).
- **Seats 1 and 3 (South and North):** stacks 0..12 (13 stacks × 2 layers = 26 tiles).
- **Per-seat slot names:** `wall.{col}.{layer}@{seat}` for col ∈ [0, n−1], layer ∈ {0,1}.

Total: 28+26+28+26 = **108 ✓**. Right edge columns 13/14..18 stay empty
on each seat. Visually this matches the canonical Changsha wall ratio
without rewriting upstream's `setup-slots.ts`.

The "break point" from §5.4 maps to one of these stacks; we either set
the corresponding tile's `shiftSlotName` to draw the visual break gap,
or just rely on `things` removal order (tiles get pulled into hand slots
in dealing order, which already creates a visible gap).

### 5.3 Coordinate basis & camera

- Units: arbitrary upstream units; `World.WIDTH = 174` (≈ 17.4 cm if you
  pretend 1 unit = 1 mm; this is a renderer-only choice).
- Origin: `(0, 0, 0)` at one corner; view-group rotation puts local seat
  at the −y edge.
- Camera "perspective" (Stephen's preferred view): position
  `(0, -250.6, 182.7)` looking ~54° down at the table center. FOV 30°.
  Aspect 1.5.
- Camera "flat" (orthographic): toggle via `P`; position
  `(0, -174, 174)`, rotation `(45°, 0, 0)`. Useful for screenshots.
- The bundle exposes both; we don't need to choose.

### 5.4 Dice positioning

Upstream draws dice as a **2D canvas overlay** on the center-pad texture
(`src/center.ts` `drawDie()`). The dice are not three.js meshes. They:
- Appear for 1000 ms after the `dice` collection's `state` becomes
  `'rolled'`.
- Render as two 40-px sprites at center-pad coords `(-44, -20)` and
  `(4, -20)`, rotated 45° relative to the center pad.

For Strategy C, pushing `dice = [0, { dice: [d1, d2], state: 'rolled' }]`
shows our authoritative dice values for ~1 s — no extra work.

### 5.5 Draw / break-point visualization

The cleanest representation is "the wall just looks correct after the
deal." For Strategy C:
- Backend emits `TilesDealt` per seat in batch order.
- Translator removes those tile entries from wall slots and places them
  into `hand.{0..12}@{seat}` slots **on each batch**, with a server-side
  delay between batches (e.g. 350 ms) so the bundle animates the moves.
- Break-point is implicit: tiles disappear from the wall at the right
  position because that's where we delete them from.

For an explicit indicator we could position a `MARKER` on the break-stack
slot (`marker@{seat}` with rotated origin) — Phase 5c polish.

---

## 6. Bidirectional Canvas → Hub

### 6.1 How upstream surfaces user actions today

The bundle has **no concept of "discard"** or "claim". Player actions are
expressed entirely as movement of `things` between slots:

| Player action | Upstream representation |
|---|---|
| Pick up a tile | Drag start → `things[i].claimedBy = mySeat`, `heldRotation` set |
| Move it | `things[i].slotName` updated to target slot via drag |
| Drop in own discard | `things[i].slotName = "discard.r.c@mySeat"`, `claimedBy = null` |
| Claim a pung | Drag the discarded tile + two from your hand into `meld.0..3@mySeat` slots |
| Declare riichi | Move a 1000-point stick from tray to `riichi@mySeat` |

The upstream `Client.things` collection update fires for every such move.

### 6.2 Routing canvas actions to Changsha hub commands

Under Strategy C, the WS endpoint already sees every `UPDATE` the
bundle sends. The translator can look for these patterns:

| Detected pattern | Hub command |
|---|---|
| `things[i].slotName` ⇒ `discard.*@s` from `hand.*@s` | `Discard(gameId, s, tileId)` |
| `things[i].slotName` ⇒ `meld.*@s` from `discard.*@s'` (other seat) | `Claim(gameId, s, type, tileIds)` — but **claim type is ambiguous from canvas alone** (pung vs kong vs chow needs context) |
| `things[i].slotName` ⇒ `meld.*@s` × 4 same rank from hand | `DeclareKong(gameId, s, tileIds)` (concealed) |
| Player drops a marker into the win zone | `DeclareWin(gameId, s)` |

**Verdict:** discard is trivially canvas-driven; claim/declare are not,
because mahjong claim semantics require the player to pick a claim type
from a small set of mutually-exclusive options. Stephen's directive
explicitly leaves "declare pung/kong/chow" buttons in the React layer
on top of the canvas — that's the right call.

### 6.3 Concrete bidirectional event list (Phase 5b)

| Event | Direction | Surface |
|---|---|---|
| Discard via drag of hand-tile to own discard zone | Canvas → SignalR | Translator detects, calls `Discard` |
| Discard via click on hand-tile in React panel | DOM → SignalR | Already shipped (Phase 3) |
| Declare Concealed/Added Kong | React button | Already shipped (Phase 3) |
| Declare Win (Zimo) | React button | Already shipped (Phase 3) |
| Accept claim opportunity (pung/kong/chow/hu) | React modal | Already shipped (Phase 3) |
| Pass | React button or auto-timeout | Already shipped (Phase 3) |
| Hover tile in canvas → highlight matching tile in React hand | Optional polish | Phase 5c |

**Why we don't surface canvas-click → claim directly:** the React
`ClaimPromptModal` already presents the choice clearly with chow-combo
picker, win surface, sorted priority. Replacing that with a tile-pick
gesture would be regressive UX.

### 6.4 Phase 2 deferred — bidirectional canvas events: re-costed

Original Phase 2 deferral marked "bidirectional canvas events" as a v3.1
item. Re-cost under Strategy C:

| Scope | LOC | Effort |
|---|---:|---|
| Translator detects discard pattern (`hand → discard.*`) and calls `Discard` | ~80 | S |
| Validate active-seat-only (ignore other seats' canvas moves) | ~30 | trivial |
| Backend round-trip: `TileDiscarded` event → translator re-pushes `things` to confirm slot | ~50 | S |
| Reconcile race: user drags before server confirms previous discard | ~80 | M |
| Tests (vitest for translator; backend unit tests) | ~150 | M |

Total ~400 LOC; **M** (~2 days). Deferred to Phase 5b — not part of MVP.

---

## 7. Risk Register (for recommended Strategy C)

### 7.1 Upstream fragility
- **Bundle hash lock.** We never touch the bundle, so future upstream
  re-mirrors don't break our integration as long as the upstream
  protocol stays at the `[kind, key, value]` shape. Upstream has not
  changed that protocol in 3+ years (last commit on `pwmarcz/autotable`
  at time of writing is from 2024). **Risk: low.**
- **Slot-name churn.** If upstream changes `setup-slots.ts` (e.g. renames
  `wall.x.y@s`), our translator breaks silently. Mitigation: add a
  startup self-test that JOINs as a synthetic client, observes the
  initial `UPDATE`, and verifies the expected slot names exist.
  **Risk: low–medium.**

### 7.2 Asset compatibility
- **No glyph additions needed for v1.** Changsha v1 dropped 紅中 (red
  dragon) per spec lock 2026-05-13 (`docs/rules/changsha-spec.md` §1).
  All 27 face glyphs we need are already in the atlas.
- **Future expansion.** If v2 reintroduces winds/dragons for other
  Chinese variants, atlas slots 27–33 are already populated.
  **Risk: low.**

### 7.3 Test coverage
- **Translator is pure-functional** (`ChangshaGameState → Entry[]`).
  Vitest-style backend xunit tests give comprehensive coverage with no
  rendering needed. Recommended assertions: post-deal entry list size,
  per-seat hand slot names, dice collection on roll, dealer marker
  position.
- **End-to-end 3D.** Headless WebGL is painful. Recommended: snapshot
  the WS frame stream into a fixture, replay through a stub renderer in
  vitest, assert frame ordering and final `things` map. Stop short of
  pixel comparisons.
- **Existing 251 tests** are unaffected — backend hub contract and
  Changsha state-machine logic are not modified by Strategy C.

### 7.4 Performance
- Upstream comfortably handles 4 players × 144 things × 60 fps.
  Adding our 108 tiles is well under the upstream baseline.
- WebSocket throughput: a full game generates ~200 `things` mutations
  + ~20 `dice`/`match` mutations. Trivial.
- **Risk: low.** Multiple concurrent games scale linearly with one WS
  connection per game per spectator.

### 7.5 Browser / WebGL compatibility
- The bundle already requires WebGL 1 (no fallback). Our React app
  doesn't require WebGL outside the iframe.
- **Risk: pre-existing — not introduced by this strategy.**

### 7.6 Failure modes specific to Strategy C
- **Bundle's auto-reconnect.** `client-ui.ts` retries 15 times at 2 s
  intervals. If our endpoint flaps, the bundle will look broken.
  Mitigation: backend keeps the WS endpoint always-available even when
  no Changsha game is active (replies to `JOIN` with an empty UPDATE).
- **Origin / iframe sandboxing.** The current iframe uses `src="/autotable/"`
  same-origin. WebSocket is allowed. No CSP changes needed.
- **Bundle initiates its own client-side Riichi deal if user clicks Deal.**
  Mitigation: hide the deal button + sidebar when embedded
  (CSS `body[data-embedded] #sidebar { display: none }` — set
  `data-embedded` via the iframe URL parameter).

---

## 8. Recommendation + Phased Path

**Strategy: C — Fake autotable WS server.**

### Phase 5a (MVP) — "Tiles appear after the deal"

**Goal:** When Stephen creates a Changsha game and the deal completes,
the iframe shows a Changsha-correct wall with 108 face-down tiles plus
13 face-down tiles in each seat's hand area plus 14 in seat 0's hand
(face-up if seat 0 is the user).

**Concrete file deltas:**
- New `src/backend/src/Mahjong.Autotable.Api/Autotable/AutotableProtocol.cs` (~80 LOC).
- New `src/backend/src/Mahjong.Autotable.Api/Autotable/AutotableSlotMap.cs` (~120 LOC).
- New `src/backend/src/Mahjong.Autotable.Api/Autotable/ChangshaToAutotableTranslator.cs` (~250 LOC).
- New `src/backend/src/Mahjong.Autotable.Api/Autotable/AutotableWsEndpoint.cs` (~250 LOC).
- Edit `src/backend/src/Mahjong.Autotable.Api/Program.cs` (~10 LOC; `UseWebSockets()` + endpoint map).
- Edit `src/frontend/autotable/index.html` (~5 LOC; hide deal button + sidebar when `?embedded=1` is present).
- Edit `src/frontend/modern/src/pages/ChangshaTablePage.tsx` (~20 LOC; pass `?embedded=1&gameId={gameId}` to iframe `src`).
- New `src/backend/test/Mahjong.Autotable.Api.Tests/AutotableTranslatorTests.cs` (~200 LOC).

Total: ~935 LOC new / ~35 modified.

**Test plan.**
- Backend unit tests: `Translator.Translate(state)` returns expected
  `Entry[]` for:
  - Empty lobby (no things).
  - Post-deal: 108 wall things + 53 hand things assigned to seat slots.
  - Post-discard: one thing moves to `discard.0.0@activeSeat`.
  - Banker indicator: `match[0].dealer = bankerSeat`.
- Integration: open WS in a backend `WebApplicationFactory` test, send
  `{ type: 'JOIN', gameId }`, assert `JOINED` + a full `UPDATE` with the
  expected entry count.
- Manual smoke: open `/changsha`, click "Play vs Bots", observe wall
  appear with 108 tiles, observe hand fill with 13 face-up tiles for
  seat 0 (face-down for others).

**Exit criteria.**
- ✅ All 251 existing tests still pass.
- ✅ New translator + endpoint tests pass (target +15–20 tests).
- ✅ Manual: after `StartGame → Deal` completes, the iframe shows 108
  Changsha tiles in the wall and 53 in the four hands. Dice show our
  rolled values for ~1 s. Banker badge on the correct seat in the
  upstream center pad.
- ✅ The React HUD (banker badge, dice modal, fan panel) continues to
  render correctly — no regression.

### Phase 5b (Interaction) — "Click-to-discard via canvas"

**Goal:** Player can drag a tile from their hand to their discard zone
in the 3D scene; backend treats it as a `Discard` command. Claims still
flow through the React modal.

**Concrete file deltas (additions on top of 5a):**
- Edit `AutotableWsEndpoint.cs`: receive client `UPDATE`s, route through
  translator's inverse function (~80 LOC).
- New `ChangshaFromAutotableInterpreter.cs` (~150 LOC; recognise
  hand → discard pattern, validate seat ownership, call `Discard`).
- Edit translator: emit "claim awarded → meld slot" entries on
  `ClaimMade` so the bundle animates the meld assembly (~50 LOC).
- New tests for the interpreter (~120 LOC).

**Exit criteria.**
- ✅ User drags a tile from `hand.*@0` to `discard.0.0@0` → backend
  records the discard, next turn begins, bot or player responds.
- ✅ Claim opportunities still flow through the React modal (no canvas
  picker for chow/pung/kong/hu).
- ✅ Claim resolution: when seat 1 claims a pung, the discarded tile +
  two hand tiles snap into `meld.0.0..2@1`.

### Phase 5c (Polish) — "Smooth animation + sound"

**Goal:** Batch-draw animation paces the initial deal; dice roll feels
deliberate; wall-break has a visible gap; discard SFX plays.

**Concrete deltas:**
- Translator pipelines the deal as a sequence of `UPDATE` frames with
  server-side timing (350 ms between batches) instead of one mega-frame.
- Push `sound = [0, { type: 'DISCARD', seat, side }]` when our state
  reports a discard — upstream's `SoundPlayer` plays the WAV.
- Push a `MARKER` thing at the break-stack position right after dice
  resolve.
- React HUD: surface "dealing… {batch}/4" status during the animation.

**Exit criteria.**
- ✅ Deal animates over ~2.5 s in 4 batches.
- ✅ Audible click on each discard.
- ✅ Break-point marker visible for the first deal of each hand.

### Phase 5d (Optional) — "Per-seat camera + spectator view"

Stretch. Not required for the Phase 5 ask. Camera rotation already works
upstream via the seat-selection mechanism.

---

## 9. Open Questions for Stephen

1. **Banker dice roll: auto or button-press?**
   Phase 3 made it auto (server rolls inside `StartGame`). For 3D, do we
   want a visual "click to roll" with a brief manual delay before the
   wall break animates, or keep it auto? (Current spec is silent.)

2. **Should we keep the sandbox at `/autotable/` operational standalone?**
   The clean way is to hide the deal button only when `?embedded=1` is
   set in the URL. Standalone `/autotable/` would still let upstream's
   Riichi flow run. Confirm we want to preserve the sandbox.

3. **Flat 2D fallback?**
   Upstream supports orthographic ("flat") mode via the `P` key. Should
   the React HUD expose a perspective ↔ flat toggle, or trust users to
   discover the keyboard shortcut?

4. **Discard pile visualization preference.**
   Spec describes "the discard pile" as one of three Changsha layout
   choices: (a) per-seat stack near the player, (b) one central pile,
   (c) per-seat trays radially arranged. Upstream uses (c) with 3 rows
   of 6 tiles. Phase 3 React UI uses (a). Confirm we want the 3D scene
   to follow (c) — upstream's natural representation.

5. **Mid-hand reconnect replay.**
   Server `FullStateEvent` rehydrates state on reconnect. Should the 3D
   scene replay the discard / claim animations on reconnect, or snap
   straight to the current state? Phase 3 snaps; Phase 5c could replay.

6. **Should the wall layout follow the canonical 14/14/13/13 split
   (Recommendation A in §5.2), or a simpler symmetric 14/13/14/13?**
   Both produce 108 tiles; the asymmetric split matches spec wording
   verbatim. The symmetric version is one line of code simpler.

7. **Concurrent games.**
   Strategy C's WS endpoint scales 1 connection per spectator per game.
   Do we want any throttling / max-concurrent-games policy beyond what
   the upstream `Game.expiryTime` (2 h idle) already gives us?

8. **Naming.**
   Should the WS endpoint live at `/autotable/ws`, `/api/autotable/ws`,
   or under a Changsha-specific path? Affects iframe URL construction.

---

## Appendix A — File reference summary

**Read-only this pass:**
- `/data/source/mahjong-autotable/src/frontend/autotable/autotable.9519e86d.js` (minified — grepped only)
- `/data/source/mahjong-autotable/src/frontend/autotable/changsha-bridge-receiver.js`
- `/data/source/mahjong-autotable/src/frontend/autotable/index.html`
- `/data/source/mahjong-autotable/src/frontend/modern/src/changsha/autotableBridge.ts`
- `/data/source/mahjong-autotable/src/frontend/modern/src/changsha/types.ts`
- `/data/source/mahjong-autotable/src/frontend/modern/src/pages/ChangshaTablePage.tsx`
- `/data/source/mahjong-autotable/docs/rules/changsha-autotable-bridge.md`
- `/data/source/mahjong-autotable/docs/rules/changsha-spec.md`

**Upstream clone (read-only at `$HOME/autotable-upstream/`):**
- `server/protocol.ts`, `server/game.ts`, `server/server.ts`
- `src/base-client.ts`, `src/client.ts`, `src/client-ui.ts`
- `src/world.ts`, `src/setup.ts`, `src/setup-slots.ts`, `src/setup-deal.ts`
- `src/main-view.ts`, `src/object-view.ts`, `src/thing-group.ts`, `src/thing.ts`
- `src/center.ts`, `src/asset-loader.ts`, `src/types.ts`

**Files this spike proposes to add (none added this pass — Phase 5a):**
- `src/backend/src/Mahjong.Autotable.Api/Autotable/AutotableProtocol.cs`
- `src/backend/src/Mahjong.Autotable.Api/Autotable/AutotableSlotMap.cs`
- `src/backend/src/Mahjong.Autotable.Api/Autotable/ChangshaToAutotableTranslator.cs`
- `src/backend/src/Mahjong.Autotable.Api/Autotable/AutotableWsEndpoint.cs`
- `src/backend/test/Mahjong.Autotable.Api.Tests/AutotableTranslatorTests.cs`

**Files this spike proposes to edit (Phase 5a):**
- `src/backend/src/Mahjong.Autotable.Api/Program.cs`
- `src/frontend/autotable/index.html`
- `src/frontend/modern/src/pages/ChangshaTablePage.tsx`

**Files this spike will NOT touch:**
- `src/frontend/autotable/autotable.9519e86d.js` and all bundled assets — preserved byte-identical with upstream per `src/frontend/autotable/README.md`.
