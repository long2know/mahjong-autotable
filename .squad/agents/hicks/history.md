# Project Context

- **Owner:** Stephen Long
- **Project:** Changsha-first Mahjong game built from pwmarcz/autotable, with expanded Chinese rules planned
- **Stack:** .NET 10 backend, EF Core + SQLite initially, optional React + Fluent UI 9 + TypeScript + Vite frontend modernization, single-image Docker deployment
- **Created:** 2026-04-20

## Learnings

- Team initialized with Hicks as Frontend Dev.
- Frontend approach starts from autotable behavior and adds targeted modernization only when it is low-risk.
- Modern frontend now uses a graphical 4-seat table layout with clickable tile faces and automatic bot progression until the next human turn.
- Modern UI now renders through seat-scoped table projections, with a perspective selector and explicit read-only behavior when viewing non-seat-0 hands.
- Modern UI now surfaces claim-window state, highlights precedence-selected opportunities, and exposes seat-0-only pass/take-selected resolution actions that re-enter the bot-to-human gameplay loop.
- Audited upstream autotable deal protocol: deal is entirely client-side (Setup.deal → shuffle → dice roll → break-point → slot placement), broadcast via WebSocket `things`/`match`/`dice` collections. Bundle has dice rendering (Center.drawDice via sprite sheet). No server authority in upstream.
- Audited backend deal: TableStateEngine.CreateInitialState deals atomically (no dice, no break-point, no batch draw). TableGameState has no banker/wind/dice/scoring fields.
- Audited modern React frontend: 943-line App.tsx with full playable loop (create table, discard, claim resolution, bot advance). No Changsha-specific UI (no dice modal, banker badge, wind indicator, or fan panel). Talks to backend via REST only.
- Produced Changsha frontend UX plan at docs/rules/changsha-frontend-plan.md. Recommended Option B: backend-authoritative deal with autotable as 3D viewport via WS bridge, Changsha chrome (dice, banker, scoring) in React Fluent UI panels. Five-phase roadmap from dice+banker components through full bridge interactivity.

📌 Team update (2026-05-05T17-00-21Z): Frontend plan decision merged to `.squad/decisions.md`. Vasquez completed Changsha canonical spec at `docs/rules/changsha-spec.md`. Bishop completed backend gap audit at `docs/rules/changsha-backend-gap.md` with 10-item roadmap and 38/38 tests passing. Hudson completed test catalog with 80 scenarios and 8 rule contradictions at `docs/rules/changsha-test-catalog.md`. Phase 1 (UI components) ready to start immediately; Phases 2 & 4 blocked on Bishop endpoint confirmation and Vasquez fan table delivery.

📌 Team update (2026-05-13T10-00-00Z): Phase 3 wave complete. Lobby + claim UX shipped with SignalR positional-args bugfix (critical). 48/48 vitest tests green. Vasquez locked v1.2 spec; Bishop fixed 5 backend bugs (203 tests); Hudson landed vitest infra + 47 frontend tests. All merged to main in PR #25 (SHA a03feda). Deferred to v3.1: 3D mesh rendering, bidirectional canvas events, reconnect animation replay. See `.squad/orchestration-log/2026-05-13T10-00-hicks.md`.

📌 Phase 1 implementation complete (changsha-v1 branch). Built 7 Changsha Fluent UI components + mock state hook + table page at `/changsha` route. Components: DiceRollModal (animated dice + break-point), BankerBadge, RoundWindIndicator, ChangshaHud (scores panel), FanBreakdownPanel (win pattern + payment table), PlayerHandPanel (Unicode tile glyphs + discard buttons), ClaimPromptModal (5s countdown). Types reconciled with Bishop's SignalR contract (numeric tile IDs 0-107, DiceResult/BreakPoint/MeldState/WinResult/ScoreResult/PaymentEntry, GamePhase enum). Dev-only demo controls cycle through all phases. 6 commits, build passes. Vitest not configured — tests skipped. Phase 2 deferred: live SignalR wiring, autotable iframe embed, real tile images, WS bridge.

📌 Phase 2 implementation complete (changsha-v1-phase2 branch). Six commits: live SignalR client + reducer-based game state, vite proxy for /hubs websocket, TileFace SVG component (27 tiles + face-down + claim glow), autotable iframe bridge (parent→child), bridge protocol + README docs. Architecture: useChangshaGame shim picks useLiveChangshaGame (HubConnection + useReducer over Bishop's contract events) or useChangshaMockGame at mount via localStorage override + import.meta.env.DEV default. Connection banner (Spinner during connecting, amber on reconnecting, red with reconnect button on disconnect), error toast on hub command failures, ChangshaTablePage embeds <iframe src=/autotable/> driven by autotableBridge.ts diffAndSend(). Receiver script changsha-bridge-receiver.js loaded inside iframe, listens for parent postMessages and shows a debug overlay; one-way Phase 2, bidirectional canvas events deferred to Phase 3. Build passes (560 KB JS / 161 KB gzip). Decisions merged into `.squad/decisions.md`. Phase 3 deferrals: canvas tile-click → Discard/Claim, atlas-based mesh rendering inside iframe, postMessage origin tightening, bundle code-splitting.

📌 Team update (2026-05-08T19:51:39Z): Phase 2 shipped — full Changsha v1 wave complete. Bishop deployed full hub lifecycle (12 commands, in-memory game-instance management, claim windows, FullState reconnect, 3 E2E SignalR tests GREEN). Hudson uncovered 2 ScoringService bugs (Bishop fixed both). Tests: 68 GREEN, 2 RED (now fixed), 7 deferred (v2). 179 passed, 0 failed, 0 build warnings. Branch ready for merge.

📌 Frontend playability + 3D bridge audit (2026-05-13). Verdict for Stephen's "can I play a full hand right now and see it in the 3D viewport": partially in 2D, no in 3D. Top blockers: (1) NO lobby UI — `ChangshaTablePage.tsx` never invokes createGame/fillWithBots/takeSeat/startGame so live mode shows an empty page; the SignalR wrappers exist in `signalrClient.ts` but are dead code. (2) Frontend invokes `RollDice` but `ChangshaHub.cs` has no such method (server auto-rolls inside StartGame) — DiceRollModal's Roll button fails. (3) Claim UI is too coarse: chow sent without `tileIds` (contract violation), no chow-combo picker, no Declare Kong button for concealed/added kong, no Declare Win (zimo) button — `actions.declareKong`/`declareWin` are exposed by the hook but unreachable. (4) Hand not sorted; discard pile not visualized (only a counter); seat hardcoded to 0; no localStorage gameId persistence so refresh orphans the client. 3D bridge truth: it is **theater**. `diffAndSend` posts state telemetry into a text overlay (`#changsha-bridge-overlay`) in the iframe; the upstream `autotable.9519e86d.js` bundle has zero references to `changsha-bridge` (grep -c == 0). No wall, no tile meshes, no rolled-value dice face, no discard pile rendered in 3D — only an opacity flip on the `#dice-img` sprite and CustomEvents nothing listens to. The bridge built the postMessage transport that the frontend plan §4 needed, but never the Changsha-aware renderer or the colocated WS server. Suggested next slice (1–2 days): LobbyCard with "Play vs Bots" button + localStorage gameId, Declare Kong/Declare Win overlays on PlayerHandPanel, chow combo picker, per-seat 2D discard trays, sort concealed hand by (suit, rank). 3D-real upgrade is a separate, larger workstream. Audit filed at `.squad/decisions/inbox/hicks-changsha-frontend-audit.md`. No code changes this pass.

### 2026-05-13: Audit fan-out — Peer verdicts
- **Vasquez:** v1-scoped gameplay loop is conformant (three nuances flagged)
- **Bishop:** Three real conformance bugs (kong priority, per-hand seed, banker rotation direction inverted)
- **Hudson:** Backend rules engine proven by 73 green tests; frontend entirely unproven (zero coverage)

### 2026-05-13: Phase 3 Stream A shipped — Changsha Lobby + Claim UX

Branch `stlong/changsha-v1-phase3`, commit `f6c298e`. 14 files touched
(+1248 / −209), 48/48 vitest tests green, build clean. Inbox memo at
`.squad/decisions/inbox/hicks-phase3-stream-a.md`.

Shipped:
- `LobbyCard` component with player-name input + "Play vs Bots" button;
  `ChangshaTablePage` orchestrates `createGame → takeSeat → fillWithBots
  → startGame`; localStorage keys `mj-autotable:changsha:{gameId,
  seatIndex, playerName}` for mid-hand reconnect via `ReconnectGame`.
- `DiceRollModal` rewritten — server auto-rolls inside `StartGame`,
  modal animates while waiting then displays rolled values + break
  point.
- `PlayerHandPanel`: sorted concealed (Wan → Tiao → Tong) via
  `sortHandForDisplay`; Concealed Kong (`findConcealedKongs`), Added
  Kong (`findAddedKongs`), and Zimo overlays.
- `ClaimPromptModal`: discard preview, chow combo picker
  (`computeChowCombos` over the user's concealed tiles + the discard),
  Win! surface for hu, sorted priority order, correct chow `tileIds`.
- `OpponentDiscardTrays` — grid layout (top/left/right) around the
  autotable viewport using new `state.discardLog` per-seat attribution.

Cross-cutting fixes required to make the lobby actually work:
- **SignalR positional-args bug** (critical, pre-existing): every
  `invoke.*` wrapper was sending `connection.invoke(method, payload)`
  with the payload as a single object — .NET hubs take positional
  args, so the previous wrappers silently bound the whole payload to
  the first parameter and coerced garbage everywhere else. Every
  live-mode hub call was dead. Rewrote all wrappers (`createGame`,
  `joinTable`, `takeSeat`, `fillWithBots`, `startGame`, `rollDice`,
  `acknowledgeDeal`, `discard`, `claim`, `declareKong`, `declareWin`,
  `pass`, `reconnectGame`) to mirror `ChangshaHub.cs` signatures
  positionally.
- `reconnectGame` previously aliased `JoinTable`; replaced with a call
  to the dedicated `ReconnectGame(gameId, seatIndex)` hub method.
- New `FullStateEvent` type + reducer case to rehydrate seats / hands
  / discard pile / dice / breakpoint / phase from server snapshot.
- `phaseFromWire` normalises PascalCase server enum strings
  ("AwaitingDiscard") to the camelCase `GamePhase` union.

Tests: `src/frontend/modern/src/changsha/__tests__/signalrClient.test.ts`
updated to match the new positional contract. Hudson's own docstring
in that file had explicitly anticipated this update at Phase 3 PR time,
so this is the planned roll-forward rather than a contract break.

Deferred to v3.1:
- 3D mesh rendering inside the autotable iframe (atlas textures, wall
  + tile meshes, real dice geometry showing rolled face).
- Bidirectional canvas → hub events (click-tile-in-3D triggers
  `Discard`).
- Mid-hand reconnect animation replays (snapshot lands but doesn't
  re-animate claims/draws).
- Server-supplied chow combo hints — client computes locally today.
- Bundle code-splitting (still emits the pre-existing 600 KB warning).

Learnings:
- SignalR JS `connection.invoke(method, ...args)` is variadic
  positional. Always mirror the .NET hub method signature one
  positional argument at a time, NEVER pass a single envelope object
  unless the hub method literally takes a single DTO parameter.
- `.NET` enum-to-string defaults to PascalCase. If the wire protocol
  uses an enum, always run incoming strings through a normaliser
  before assigning to a camelCase string-union type — silent
  membership failure is otherwise easy to miss.
- The Edit tool can silently no-op if the `old_str` block doesn't
  match exactly. After non-trivial multi-line edits, always re-grep
  for the new content to confirm the change stuck. (See: my Phase 3
  WIP pre-summary lost the four largest file rewrites this way; had
  to redo them from scratch this pass.)

### 2026-05-13: 3D Renderer Scoping Spike

Branch `stlong/changsha-3d-renderer-spike`. Deliverable:
`docs/rules/changsha-3d-renderer-plan.md`. Read-only spike: no code
changed, no bundle touched. Inbox memo at
`.squad/decisions/inbox/hicks-3d-renderer-spike.md`.

**Recommended strategy:** **C — Fake autotable WS server.** Collocate a
WebSocket endpoint inside the .NET backend that speaks upstream's
`NEW`/`JOIN`/`JOINED`/`UPDATE` collection protocol. Translate
authoritative `ChangshaGameState` into upstream's seven collections
(`match`, `seats`, `things`, `nicks`, `mouse`, `sound`, `dice`). Bundle
stays byte-identical; no JavaScript modifications.

**Headline finding:** The "theater" assertion is exactly correct.
`grep -c "changsha-bridge" src/frontend/autotable/autotable.9519e86d.js`
returns 0. The minified bundle exposes only `window.__THREE__`. There is
no `message`-event listener and no `customEvent` listener. The receiver
script's `CustomEvent` dispatches are completely unheard. The only
canvas-side effect today is `document.getElementById('dice-img').style.opacity = '1'`
from the receiver.

**Surprises while reading upstream:**

1. The upstream server (`server/game.ts`) has **zero game-rules
   awareness** — it's a flat key-value store with `unique`/`ephemeral`/
   `perPlayer` constraints. This is exactly the abstraction we want to
   imitate: dumb storage that the React + backend pair drives. Means
   our fake WS server can be small (~250 LOC, no game logic).

2. Tile atlas mapping is **trivially compatible with Changsha v1**:
   `upstreamTypeIndex = Math.floor(changshaTileId / 4)` for any id
   0–107 lands cleanly in atlas cells (0,0) through (2,3) — wan, tong,
   tiao all 1–9. No new glyphs needed; winds/dragons/red-fives at
   atlas cells 27–36 are inert for v1.

3. Dice are **not three.js meshes.** Upstream draws them on a 2D canvas
   texture mapped onto the center pad mesh — Center.drawDice() uses
   `ctx.drawImage(diceImg, ...)`. To show our authoritative dice it
   suffices to push a `dice = [0, { dice: [d1,d2], state: 'rolled' }]`
   collection update; upstream renders the result for ~1 s.

4. Upstream uses **152 wall slots** (4 seats × 19 cols × 2 layers) but
   only 136 are populated in Riichi. Changsha's 108 fit cleanly with
   14/14/13/13 split across the four seats, leaving 44 right-edge
   slots empty — visually correct for Changsha's wall ratio.

5. Player actions are **expressed as slot-name changes**, not as
   "discard" or "claim" semantic events. The translator's inverse
   function (Phase 5b) has to detect patterns like
   `things[i].slotName: hand.*@s → discard.*@s` and infer "discard".
   Claim semantics (pung/kong/chow/hu) are genuinely ambiguous from
   the canvas; the React modal still owns that picker.

6. The bundle's `client-ui.ts` auto-reconnects 15× at 2 s intervals if
   the WS drops. Our endpoint must stay always-available (respond to
   JOIN with an empty UPDATE if no Changsha game is bound) to avoid
   flapping.

**Complexity estimate:** Phase 5a (walls + hands visible) **L** ~900
LOC, 3–5 days. Phase 5b (canvas-drag discard) **M** +400 LOC, ~2 days.
Phase 5c (batch-draw animation, SFX, break-point marker) **M** +300
LOC, ~2 days.

**Top 3 risks:** (1) silent breakage if upstream renames wall/hand/
discard/meld slot names, (2) discard-confirmation race between fast
drag and server echo (Phase 5b only), (3) bundle's own Deal button
firing a Riichi deal in embedded mode if sidebar isn't hidden.

**Open questions filed for Stephen:** 8 items including auto-roll
vs click-to-roll, preserve standalone `/autotable/` sandbox?, canonical
14/14/13/13 vs symmetric 14/13/14/13 wall split, WS endpoint path
(`/autotable/ws` vs `/api/autotable/ws`).

### 2026-05-13: Phase 5a Frontend Wiring

Branch `stlong/changsha-3d-phase5a`. Wired the iframe to live Changsha
game state and added the camera-toggle HUD button. Disjoint from
Bishop's backend Strategy C and Hudson's parallel tests.

**Shipped:**
- `src/frontend/autotable/index.html` — `?embedded=1` sets
  `data-changsha-embedded="1"` on `<html>`; CSS hides `#sidebar` and
  `.seat-buttons`. Standalone `/autotable/` sandbox (Default #2)
  preserved when the query param is absent.
- `src/frontend/modern/src/pages/ChangshaTablePage.tsx` — exported
  pure helper `buildAutotableIframeSrc(gameId, seatIndex?) →
  '/autotable/?gameId=…&embedded=1&seat=…'`. `AutotableViewport`
  receives `userSeat`, wraps `src` in `useMemo([state.gameId,
  userSeat])` so unrelated re-renders don't reload the iframe (would
  drop the WS). Camera button overlaid top-right (absolute,
  `zIndex: 10`).
- `src/frontend/modern/src/changsha/components/CameraToggleButton.tsx`
  — Fluent UI 9 `Button` + `Tooltip` "🎥 Toggle View" (with `P`
  keybind hint).
- `src/frontend/autotable/changsha-bridge-receiver.js` — extended
  with `case 'camera-toggle'`: dispatches `KeyboardEvent('keydown',
  { key:'p', code:'KeyP', keyCode:80, which:80, bubbles:true })` on
  `document`. Preserves the existing CustomEvent re-emission pattern.
- `src/frontend/modern/src/changsha/autotableBridge.ts` — extended
  the `BridgeOutboundMessage` union with `camera-toggle` so
  `bridge.send({ type: 'camera-toggle' })` typechecks.

## Learnings

- **Upstream WS URL resolution (verified by reading the minified
  bundle at offset 1003085):**
  ```js
  getUrl() {
    let e = location.pathname.substring(1, location.pathname.lastIndexOf("/") + 1);
    let t = location.protocol === "https:" ? "wss:" : "ws:";
    return `${t}//${location.host}/${e}ws`;
  }
  ```
  With iframe at `/autotable/?gameId=X` the WS lands at
  `wss://{host}/autotable/ws`. Default #7 path is correct — Bishop's
  endpoint at `/autotable/ws` matches with no coordination change.
- **Bundle's auto-connect is gated on `?gameId=…` being present.**
  `start()` (offset 1002975) reads `getUrlState()` and only calls
  `client.join(this.url, gameId)` if the gameId is non-null. Without
  the query param the WS stays disconnected forever. So passing
  `gameId` in the URL is **load-bearing**, not advisory.
- **Sidebar selectors:** `#sidebar` (the entire right rail with Deal,
  Setup, Connect, Disconnect, More, Status) and `.seat-buttons` (the
  four "Take seat" / "Kick" rows positioned around the table). Both
  must hide in embedded mode — both fire upstream-protocol actions
  that conflict with our authoritative state.
- **Camera-toggle approach:** rather than reaching into the bundle's
  internal `settings.perspective` element (would require a bundle-aware
  selector), the receiver synthesizes a `keydown` event on `document`
  with `bubbles: true`. Upstream's `Camera.onKeyDown` is attached at
  the `window` level (offset 1011975) — bubble phase catches it. Match
  is `case "p"` (lowercase) in the switch. Resilient to bundle
  rebuilds: only assumes the `P` keybind exists, not any internal
  layout.
- **Hudson's vitest test files** in `__tests__/` add the test contract
  (`buildAutotableIframeSrc` helper, camera-toggle keydown spec) that
  I implemented to. Two pre-existing issues in his files surfaced
  during my build: (a) `RECEIVER_PATH` has one too many `..` in
  `autotableBridge.cameraToggle.test.ts`; (b) `node:fs` / `node:path`
  / `__dirname` need `@types/node` for `tsc -b` to be clean. Both
  flagged in handoff memo, not my files to fix.
- **`useMemo` deps for iframe src:** `[state.gameId, userSeat]`.
  Reload IS appropriate when these change (new game or seat change
  drops the old WS session anyway); reload is NOT appropriate on
  every render (would reset the bundle's three.js scene mid-game).
  Component re-renders dozens of times per game cycle (every state
  diff fires the `diffAndSend` effect); without `useMemo` the iframe
  would thrash.

### 2026-05-13: Phase 5a — Frontend Iframe Wiring
- **`useMemo([gameId, userSeat])` essential to prevent iframe reload on parent re-renders.** React suspends the iframe's three.js scene and WS session during reload. The component re-renders dozens of times per game cycle (every state diff fires effects), so reloading on every parent render would be catastrophic. Reload is appropriate only when gameId or userSeat changes (new game or seat → drops old WS session anyway). The `useMemo` is the load-bearing optimization.
- **`?embedded=1` query param is the load-bearing sidebar-hide trigger.** The inline `<script>` in `index.html` reads `URLSearchParams(window.location.search).has('embedded')` and sets `html[data-changsha-embedded="1"]` attribute before `<body>` parses. CSS rules `html[data-changsha-embedded="1"] #sidebar, html[data-changsha-embedded="1"] .seat-buttons { display:none !important; }` execute immediately — no flash, no layout shift. Without the parameter, the attribute is not set, sidebar renders normally (Default #2 — sandbox preserved).
- **`KeyboardEvent` synthesis on `document` (not `window`) catches upstream's listener.** The bundle registers `window.addEventListener("keydown", ...)` at offset 1011975 of `autotable.9519e86d.js`. A `document.dispatchEvent(new KeyboardEvent(..., {bubbles:true}))` reaches `window` via the bubble phase. Event properties must be exact: `key='p'` (lowercase, upstream switch is `case "p"`), `code='KeyP'`, `keyCode=80`, `which=80`, `bubbles=true`. Resilient design: only assumes the `P` keybind exists, not any internal bundle layout changes.

### 2026-05-13: Autotable TS Modification Inventory (Stephen's pivot directive)

In response to copilot-directive-2026-05-13T2300Z — Stephen's architectural
reckoning ("we want to simply use autotable and implement changsha rules with
it"). Read `/tmp/autotable-upstream/` (the unvendored upstream clone) end to
end and filed an honest scope inventory at
`.squad/decisions/inbox/hicks-autotable-ts-inventory.md`. NO path proposed —
Ripley synthesises.

## Learnings

- **Upstream is a 3D physics dollhouse, not a Mahjong rules engine.** Across
  `world.ts` (708 LOC), `setup.ts` (326 LOC), `client.ts` (210 LOC) — there
  is NO win detection, NO claim adjudication, NO shanten, NO yaku check, NO
  legal-move hint, NO pao logic, NO multi-winner draw handling. The only
  "scoring" upstream has is `Setup.getScores()` which sums point-stick `typeIndex`
  values in `tray.{i}.{j}@seat` slots. **All Changsha rules must live on the
  backend; the client is a viewport.**
- **Tile atlas is Changsha-compatible AS-IS.** `thing-group.ts:217-220` maps
  typeIndex → atlas cell via `x=(typeIndex%37)%8, y=floor((typeIndex%37)/8)`.
  typeIndex 0–8 = wan, 9–17 = tong, 18–26 = tiao, 27–30 = winds, 31–33 =
  dragons, 34–36 = red fives. Changsha keeps 0–26 only. **No tile SVG or GLB
  changes needed** — honors/red-fives cells just go unused.
- **Native autotable UI has no Pon/Kan/Chi/Riichi/Hu buttons.** Claims happen
  by physically dragging the discarded tile from opponent's pile into your
  own meld area. Riichi declaration = dragging a 1000-point stick out of the
  tray (group `riichi`, type STICK, `setup-slots.ts:209-219`). Stephen's "use
  autotable's own UI controls" intent therefore implies a **drag-to-claim**
  model unless we add new HTML/CSS/wiring. Biggest single UX divergence from
  the current React app.
- **Protocol is a 4-message generic key-value store**, not a Mahjong
  protocol. NEW/JOIN/JOINED/UPDATE. Collections: match, seats, things, nicks,
  mouse (rate-limited 100ms), sound (ephemeral), dice (ephemeral).
  Constraints: `unique`, `ephemeral`, `perPlayer`, `rateLimit`, `sendOnConnect`.
  Server `game.ts` (213 LOC) is "dumb storage". Changsha-specific concepts
  (claim windows, gangshanghua, pao, 258 eye, multi-winner) are not native;
  extension pattern is to **add new collections** (e.g. `changsha.claim`,
  `changsha.scoring`, `changsha.break`, `changsha.banker`) — upstream-style
  clients ignore unknown collections, so it's backward-compatible.
- **Hand-tile privacy is broken at the protocol layer.** `things` collection
  sends every tile's typeIndex to all connected clients regardless of which
  seat the tile is in. Hands are face-down VISUALLY (rotation = STANDING) but
  the typeIndex is in the payload. **Anyone with the network tab can read all
  hands.** Our `AutotableWsEndpoint.cs` MUST filter per-viewer based on `?seat=N`
  for hand slots, otherwise cheating is trivial. The upstream design ASSUMES
  all-public state. This is a v3-blocker if not already handled.
- **Build chain depends on Inkscape + Blender CLI.** `Makefile` rules:
  `img/tiles.auto.png` ← `img/tiles.svg` via Inkscape; `img/models.auto.glb`
  ← `img/models.blend` via Blender + `export.py`. Neither is in our build
  environment. If we never re-export geometry we can skip Blender; we'd still
  need Inkscape (or pre-generate PNGs once and commit them) for any SVG tile
  glyph swap.
- **Riichi-specific bits cluster in 7 files but each touch is tiny.** Grep
  for `honba|riichi|kita|fives|red.fives` hits `types.ts`, `setup.ts`,
  `setup-slots.ts`, `setup-deal.ts`, `world.ts`, `center.ts`, `game-ui.ts`,
  `client.ts`, `slot.ts`. Each Riichi assumption is well-localised — making
  `Conditions.fives` optional triggers maybe 15 TS errors, all in well-known
  spots. **No deep entanglement.** A Changsha branch in `setup.ts:tileIndex`
  + a `CHANGSHA` GameType in `types.ts` is the seed.
- **Upstream protocol is bidirectional UPDATE.** Client → server UPDATE means
  "I want to mutate things[7] to slotName='discard.0.0@2'". Server echoes to
  all peers. For Changsha command channel, we'd extend with a
  `changsha.command` ephemeral collection: client pushes
  `{kind:'declareWin', seat:2}`, server validates + emits a `changsha.scoring`
  UPDATE. Native fit — no new message types needed.
- **Bot-management UI is new territory.** Upstream has no concept of bots.
  Adding a "Fill with Bots" button is a ~80-LOC sidebar patch in `index.html`
  + `game-ui.ts` to send a `changsha.command.fillWithBots` UPDATE.
- **/changsha React shell is ~3,500 LOC of deletion candidate.** Components
  (DiceRollModal, BankerBadge, RoundWindIndicator, ChangshaHud,
  FanBreakdownPanel, PlayerHandPanel, ClaimPromptModal, OpponentDiscardTrays,
  CameraToggleButton, LobbyCard, TileFace), reducer, signalrClient, bridge
  modules, mock mode — all duplicate functionality that would live inside
  the autotable bundle under Stephen's pivot. Net code count drops.
- **Tile glyphs are CC BY-NC-SA.** `img/tiles.svg` is Non-Commercial.
  Worth flagging if the project trajectory turns commercial.

### 2026-05-13: Phase A shipped — autotable vendored in-tree, modern/+bridge scrubbed

Branch `stlong/autotable-vendored-pivot`, baseline `b5dacea`. Per Ripley's
`ripley-pivot-plan.md` §2 Phase A and Stephen's acceptance directive
`copilot-directive-2026-05-13T2320Z-accept-defaults-mvp.md`. File scope:
`src/frontend/**`, `.vscode/*`, `.squad/config.json`. Bishop owns backend in
a parallel branch — no overlap.

**Upstream SHA captured:** `8b81d92aa37997dcfbcc6724d3bd3f694f9cc53a`
(pwmarcz/autotable master, "Show dice for 1 second"). Recorded at
`src/frontend/autotable-src/UPSTREAM_SHA` for future cherry-picks.

**What shipped**
- `src/frontend/autotable-src/` — pwmarcz/autotable master vendored verbatim
  (decision #1 default, in-tree fork, not a submodule). Upstream `COPYING`
  retained verbatim; CC-BY-NC-SA tile-image notices in `about.html` retained.
  `.git` removed, `UPSTREAM_SHA` written, `.gitignore` updated to keep prebuilt
  `img/*.auto.png` + `img/*.auto.glb` (since we don't run upstream's Inkscape /
  Blender Makefile targets — `img/*.auto.*` were sourced from the previously
  shipped `autotable/` bundle).
- Two upstream-source patches applied before build:
  - `index.html`: `perspective` + `tile-labels` checkboxes default to
    `checked` (matches previously shipped visual baseline, per the legacy
    `autotable/README.md`).
  - `index.html` + `about.html`: Google Analytics tracking block
    (UA-50655023-2, pwmarcz.pl property) removed.
- `src/frontend/autotable/` — wiped and repopulated with the Parcel 2.15
  build output of `autotable-src/`. New JS hash is `autotable-src.eb80a662.js`
  (the prior `autotable.9519e86d.js` is replaced; Parcel derives bundle name
  from the shared parent of the two entry HTMLs, so the prefix changes from
  `autotable` to `autotable-src` — functionally inert, the index.html
  references whatever Parcel emits).
- Bridge cruft fully removed: `changsha-bridge-receiver.js` (154 LOC) gone;
  the `<style id=changsha-embedded-mode>` block and `?embedded=1` shim that
  were injected into the legacy `index.html` are absent from the regenerated
  build (upstream source never had them).
- `src/frontend/modern/` — deleted entirely. 40 files, ~7,094 LOC of hand-
  written TS/TSX/CSS/HTML + ~5,000 LOC of `package-lock.json`.
- `.vscode/launch.json`:
  - Deleted `Frontend Modern (Vite)` config and the
    `F5 Full Stack (Backend + Modern Frontend)` compound.
  - Added `Autotable (Parcel watch)` (`type: node-terminal`, runs
    `npx parcel watch index.html about.html --public-url . --no-source-maps
    --dist-dir ../autotable` from `src/frontend/autotable-src`).
  - Added compound `F5 Full Stack (Backend + Autotable)` referencing
    `.NET Backend` + `Autotable (Parcel watch)`, `stopAll: true`.
  - Preserved byte-identical: `.NET Backend` and
    `Backend + Autotable Baseline` configs, both still carry
    `PATH: ${env:HOME}/.dotnet:/usr/share/dotnet:/usr/local/share/dotnet:${env:PATH}`
    (PRs #27 + #28).
- `.vscode/tasks.json`:
  - Deleted `frontend: install` and `frontend: run` (pointed at the dying
    `modern/`).
  - Added `autotable: watch` (same parcel watch invocation as the launch
    config, but as a `process` task for the compound to chain off).
  - Preserved byte-identical: `backend: build` and `backend: run`, both
    still carry the same PATH augmentation.
- `.squad/config.json`: `defaultModel` bumped from `claude-opus-4.7` to
  `claude-opus-4.7-xhigh` per the acceptance directive's session preference.

**LOC delta (commit will report exactly)**
- Added: `src/frontend/autotable-src/` ≈ 6,200 LOC (upstream TS sources +
  prebuilt assets + lockfile) + commit-output Parcel bundle.
- Deleted: `src/frontend/modern/` ≈ 7,094 LOC hand-written + ~5,000 LOC
  lockfile; bridge-receiver 154 LOC; legacy minified `autotable.9519e86d.js`
  is replaced not deleted (new hash).
- Net repo delta: ~−5,000 LOC of bridge / React-SPA cruft after Parcel
  bundle is accounted for.

**Build smoke test (verbatim final lines of `parcel build`)**
```
✨ Built in 2.68s
../autotable/autotable-src.eb80a662.js           1.02 MB    804ms
../autotable/models.auto.72ee60ea.glb          206.66 kB    302ms
```
All 22 emitted assets present in `src/frontend/autotable/`. The
`.auto.<hash>.png` filenames for prebuilt assets land with the exact same
Parcel hashes as the prior bundle (`tiles-labels.auto.9a041239.png`,
`models.auto.72ee60ea.glb`, etc.) — confirming the source bytes for those
assets are byte-identical to what shipped before.

**Backend follow-up needed (Bishop's scope, NOT touched here)**
None blocking. The .NET backend's static-file middleware still points at
`src/frontend/autotable/`, which still exists and is still populated — the
served URLs just now route to a fresh-but-shape-identical bundle. The
backend serves `index.html` at `/autotable/` via `DefaultFiles`; that
behaviour is preserved.

**Weird things**
- Parcel names the bundle `autotable-src.<hash>.js` (not
  `autotable.<hash>.js`) because the two HTML entries share their parent
  directory `autotable-src` and Parcel uses the common directory name when
  multiple HTML entries share a JS bundle. The prior bundle was named
  `autotable.9519e86d.js` because it was built in a folder literally
  named `autotable/`. Renaming the vendor folder to `src/frontend/autotable/`
  would restore the old prefix, but the existing folder `src/frontend/autotable/`
  is the **output** folder (statically served), so we cannot collide names.
  The output filename is self-referential — `index.html` points at whatever
  Parcel emits — so this is cosmetic.
- Upstream's `img/about/*.png` and `img/about/game.mp4` ship as git-LFS
  pointer stubs (~130 bytes each). Parcel happily bundled the stubs and
  emitted 130-byte "images" on the first build. Fixed by overwriting the
  pointer stubs with the real bytes pulled from the previously shipped
  `src/frontend/autotable/` bundle (e.g. `dealer.a27808af.png` → `img/about/dealer.png`).
  Rebuilt clean — `dealer.a27808af.png` now correctly emits as a 43 KB PNG
  with the original Parcel hash, confirming source identity.


## Architectural Pivot — Phase A SHIPPED (2026-05-13)

**Branch:** stlong/autotable-vendored-pivot (merged to main @ 55d8dfb)
**Timestamp:** 2026-05-13T23:10Z
**Contribution:** Produced autotable TS modification inventory (3 vendoring paths, Parcel vs Vite analysis, 9 risk flags), executed Phase A frontend vendor (pwmarcz/autotable @ 8b81d92 → `src/frontend/autotable-src/`, deleted `src/frontend/modern/` ~7,094 LOC, deleted bridge receiver ~154 LOC, updated .vscode F5 compound launch with `autotable: watch` task).


## Architectural Pivot — Phase B SHIPPED (2026-05-19)

**Branch:** stlong/phase-b-changsha-scene
**Timestamp:** 2026-05-19T15:35Z
**Scope:** src/frontend/autotable-src/** (Changsha-shape the vendored scene)
**Bound by:** Ripley pivot plan §2 Phase B + Vasquez rules diff §1.1–1.14 +
Stephen's directive (all 16 §4 defaults accepted + MVP fast-cuts a/b/c).

### Files modified (7 source files in `src/frontend/autotable-src/`)
- `src/types.ts` — `GameType` collapsed to `CHANGSHA` only; `Fives`/`Points`/`POINTS`
  type-aliases + table deleted; `DealType.WINDS` dropped; `Conditions.{back,fives,points}`
  → `Conditions.baseUnit: number` (default 1).
- `src/setup-deal.ts` — full rewrite. `DEALS.CHANGSHA.{INITIAL,HANDS,UNSHUFFLED}`
  only. `INITIAL`/`UNSHUFFLED` fill 28/28/26/26 walls. `HANDS` deals 13 into
  each player's `hand.0..hand.12` + 1 into dealer's `hand.extra@0` + 14/15/13/13
  remainder into walls (53 dealt + 55 in walls = 108).
- `src/setup-slots.ts` — `SLOT_GROUPS` collapsed to a single `CHANGSHA` entry
  (clone of FOUR_PLAYER minus `tray`/`payment`/`riichi` slot bindings). `riichi`
  `START` slot definition deleted. `fixupSlots` param renamed `_gameType`.
- `src/setup.ts` — `i < 108` loop; `tileIndex(i) = Math.floor(i / 4)`;
  `addSticks()` method + its caller deleted; `getScores()` stubbed to return
  `[null,null,null,null,null]`; `replace(conditions)` signature trimmed.
- `src/world.ts` — `toggleHonba()` deleted; `deal(dealType)` signature
  simplified; `MatchInfo.honba` pinned to `0` in every assignment; `resetPoints()`
  deleted; riichi-stick drop-collision branch deleted; Phase D TODO comment
  added over `toggleDealer()`.
- `src/game-ui.ts` — full rewrite (only #deal, #toggle-dealer, #take-seat-N,
  #kick-N, #leave-seat, #toggle-setup, #deal-type, #setup-desc bound; dropped
  fives/points/honba/reset-points UI).
- `index.html` — Riichi-only controls (#fives, #points, #toggle-honba,
  #reset-points, #game-type) hidden via `style="display: none"` to preserve
  any stray `getElementById` callsites. Added a four-button claim section:
  `碰 Pung` / `吃 Chow` / `杠 Kong` / `胡 Hu` (yellow Hu = `btn-warning`;
  others = `btn-dark`), all `disabled`, with title "Wired in Phase D."

### LOC delta
```
7 files changed, 106 insertions(+), 539 deletions(-)
```
Net trim: **-433 LOC** from the vendored source.

### Build outcome
- `npx parcel build index.html about.html …` → **✨ Built in 2.69s**, 22 assets.
- Bundle: `autotable-src.3e0763b1.js` (1.01 MB; was 1.02 MB pre-trim).
- `npx tsc --noEmit --strict … src/index.ts` → **0 errors**.
- Stale Phase A bundle `autotable-src.eb80a662.js` `git rm`'d; new bundle
  staged in commit.

### Implementation choices documented separately
See `.squad/decisions/inbox/hicks-phase-b-implementation.md` for the 9 in-flight
discretionary calls (deal arithmetic, wall remainder, `things.ts` no-op,
disabled claim buttons, etc).

### Known quirks (sandbox-acceptable, deferred to Phase C/D)
- Seat 1's wall ends up at 7.5 stacks (15 tiles in a 14-tile column,
  leaving wall.8.1 empty). Visually shows one half-stack at the right end
  of seat 1's wall. Documented in `setup-deal.ts` inline comment.
- Dealer-toggle still cycles seats 0..3 client-side (TODO Phase D wires
  `changsha.banker`).
- Claim buttons are decorative `disabled` stubs (no `onclick`); Phase D
  will either wire them to the claim window or replace them with the
  drag-to-meld interaction per MVP fast-cut (b).
- `getScores()` returns nulls; center renderer's `drawScore` early-exits
  on null so the scoreboard stays blank. Phase D wires `changsha.scoring`.

### Smoke-test recipe (for Stephen)
1. F5 in VS Code (compound launch starts backend + autotable watch).
2. Browse `http://localhost:5114/autotable/`.
3. Expect: 108 tiles in a 14/14/13/13 wall ring; dealer-position marker
   visible; no riichi sticks anywhere; no dora-indicator area; no
   point-stick tray at any seat; sidebar shows 碰 Pung / 吃 Chow /
   杠 Kong / 胡 Hu buttons (greyed/disabled); deal-type dropdown lists
   only HANDS / INITIAL / UNSHUFFLED.
4. Click `Deal` with `Hands` selected — 13 tiles fly to each seat, one
   extra to the dealer's `hand.extra` position (14 total dealer hand).


## Architectural Pivot — Phase D-frontend SHIPPED (2026-05-20)

**Branch:** stlong/phase-b-changsha-scene
**Timestamp:** 2026-05-20T17:00Z
**Scope:** src/frontend/autotable-src/** (wire to Bishop's Phase D-backend
protocol — claim arc, scoring panel, dice viz, bot banner, face privacy).
**Bound by:** Phase D-frontend charter (Stephen, 2026-05-20); Bishop's
parallel D-backend protocol contract; Defaults #5 (Chinese-primary claim
labels) and #11 (single Hu button).

### Files modified (6 source files in `src/frontend/autotable-src/`)

- `src/types.ts` — Added `ClaimWindowEntry`, `HandResultEntry`, `ScoreDelta`,
  `DiceEntry`. Extended `DiceInfo` with optional `d1/d2/breakPoint` so
  Bishop's new dice payload and the legacy local-deal payload can both
  ride the existing `dice` collection. Added optional `face?: number | null`
  to `ThingInfo` for per-viewer tile privacy.
- `src/client.ts` — Registered `claim` collection (`Collection<string,
  ClaimWindowEntry>`, ephemeral) and `result` collection (`Collection<string,
  HandResultEntry>`, persistent). Widened `dice` collection key from
  `number` to `string | number` so Bishop can push key `'current'` and the
  local-deal path can keep using key `0`.
- `src/game-ui.ts` — Phase-D wiring (the heavy lift, +407 LOC). Subscribes
  to `client.claim.on('update')`, enables matching buttons + renders
  countdown ticking every 100 ms, auto-passes on deadline expiry, sends
  `claim[selfSeat] = {action, type}` on click. Subscribes to
  `client.result.on('update')` and renders the Bootstrap-modal scoring
  panel (gold headline, score-delta table, suit-colored 2D winning-hand
  tiles, Next Hand button that posts `match[1] = {action:'nextHand'}`).
  Subscribes to `client.dice.on('update')` and shows a top-center HUD
  with dice glyphs + break-point column for 3 seconds. Maintains a
  bottom-left bot banner driven by the `Bot ` nick-prefix convention
  (until Bishop ships an explicit `is_bot` seat flag).
- `src/world.ts` — Honors the new `ThingInfo.face` privacy field: when
  `face === null` and the slot has >1 rotation, coerces `rotationIndex`
  to the last (face-down) rotation. Belt-and-braces against a backend
  that strips face without also flipping rotation.
- `index.html` — Added a 5th claim button `跳过 Pass` (`btn-secondary`),
  a `#claim-countdown` div under the claim row, a new `#result-modal`
  with mahjong-themed scoring panel, a `#dice-hud` top-center overlay,
  and a `#bot-banner` bottom-left text element.
- `src/style.css` — Styles for `#claim-countdown`, `.result-modal-content`
  (felt green + brass border + suit-color tile cells), `#dice-hud`, and
  `#bot-banner`. +91 LOC.

### LOC delta (frontend source only)
```
6 files changed, 629 insertions(+), 18 deletions(-)
```
Net add: **+611 LOC** to the vendored source (heaviest in game-ui.ts
which absorbed the bulk of the UI wiring).

### Build outcome
- `npx parcel build index.html about.html --public-url . --no-source-maps
   --cache-dir .cache/build/ --dist-dir ../autotable` → **✨ Built in 2.40s**,
  22 assets.
- Bundle: `autotable-src.9d857456.js` (1.01 MB; same as Phase B's 1.01 MB
  — Parcel minification absorbs the +611 LOC).
- `npx tsc --noEmit --strict --target es6 --moduleResolution bundler
   --esModuleInterop --lib DOM,DOM.Iterable,es6,es2017 src/index.ts` →
  **0 errors**.
- Per the charter's `src/frontend/autotable-src/**` scope-lock, the static
  `src/frontend/autotable/` bundle output was **not** staged. F5 compound
  launch will regenerate it on next start.

### Implementation choices documented separately
See `.squad/decisions/inbox/hicks-phase-d-frontend.md` for the protocol-
contract adapter notes (dice shape duality, Next-Hand match-key sentinel,
tile face privacy via rotation coercion) and UI-design discretionary calls
(button-countdown vs modal, 2D tile cells in result panel, etc).

### Known constraints / Phase E TODOs
- No disambiguation UI when one seat can both Pung and Kong on the same
  discard — user just picks one button. Default #11 said single Hu button;
  charter didn't ask for multi-meld disambiguation. Flag for Stephen if
  he wants it.
- Drag-to-meld interaction (MVP fast-cut b, Vasquez Q9 / Hicks R2) remains
  explicitly out of scope — buttons-only per Pivot plan §2 Phase D.
- Winning hand in the result modal renders as 2D suit-colored cells, not
  a 3D animated meld in the scene. Phase E polish if Stephen wants that.
- `face: null` privacy ships via rotation coercion only — the
  InstancedThingGroup's UV math is untouched. A back-only mesh variant
  would require regenerating the InstancedMesh on every face flip and is
  not worth the cost while rotation already hides the front face.

### Smoke-test recipe (for Stephen)
1. F5 in VS Code (compound launch).
2. Browse `http://localhost:5114/autotable/`.
3. Take seat 0 — bot banner appears bottom-left ("Bots filled seats 1, 2, 3
   / Bot Alpha (S) / Bot Bravo (W) / Bot Charlie (N)"). If banner missing,
   bot nicks are not `Bot `-prefixed — coordinate with Bishop.
4. Click Deal — 108 tiles deal, top-center HUD shows dice + break point
   for 3 s.
5. When a bot discards a tile that gives you a claim, the 碰 / 吃 / 杠 / 胡
   buttons light up according to server's `available`; countdown ticks
   `Decide in 5.0s` → `0.0s`. Click any enabled claim button or `跳过 Pass`;
   buttons re-disable immediately. Auto-pass fires at 0.0s if you do
   nothing.
6. On hand end, centered modal appears with gold headline (胡! / 流局 Draw
   / 诈胡 False Hu), score-delta table, and winning-hand tiles as 2D
   cells. Click `下一局 Next Hand` to advance the server.

---

## Phase F — Variant switching + manual pickup + bot UI (2025-05-19)

**Branch:** `stlong/phase-f-changsha-realism` off `d461726` (Wave 3 Changsha runtime).
**Bundle SHA:** `autotable-src.d9507f0f.js` (1.03 MB) — clean tsc-strict + parcel.

### What I shipped
Four switches layered onto Wave 3:
1. **Variant switching** — restored upstream `GameType` enum (CHANGSHA + 4 Riichi variants), `Conditions.defaultsFor(gameType)` factory.
2. **Manual-pickup state machine** — new `pickup` collection (singleton key 0 in / command keys 'rollDice' + 'take' out), drag-intercept gate, take-N HUD button.
3. **Deal-mode toggle** — Changsha-only `manual`/`auto` select that flows into `Conditions.dealMode`.
4. **Bot count + difficulty pickers** — informational for now (Bishop owns the engine); persist via localStorage; extend the bot banner.

### Files touched
| File | Purpose |
|---|---|
| `types.ts` | GameType, Conditions, PickupEntry |
| `setup-slots.ts` | Upstream SLOT_GROUPS + tray/payment/riichi START slots restored |
| `setup-deal.ts` | Upstream DEALS (incl. 11 FOUR_PLAYER roll variants) + POINTS table |
| `setup.ts` | Variant-branched `setup`/`addTiles`/`tileIndex`/`replace`/`getScores`/`addSticks` |
| `client.ts` | `pickup` collection registration (Collection<string\|number, PickupEntry>) |
| `world.ts` | Pickup gating, drag-intercept, `toggleHonba`, `resetPoints`, `deal(overrides)` |
| `game-ui.ts` | All Phase F DOM wiring — pickers, pickup HUD, roll-dice, variant badge, URL params, localStorage |
| `index.html` | Setup-group rebuild + pickup HUD + roll-dice + variant badge + break marker |
| `style.css` | Variant-class visibility, variant badge, pickup HUD, roll-dice, break marker, wall-glow |
| `src/frontend/autotable/**` | Parcel rebuild |

### Learnings worth remembering
1. **`Fives` and `Points` are string-typed unions, not numbers.**  Fives: `'000' | '111' | '121'`.  Points: `'25' | '30' | '35' | '40' | '100'`.  Don't `parseInt` them — pass the string straight through.  I wasted a tsc round-trip on this.
2. **Collection key widening is a pattern.**  Phase D widened `dice` to `Collection<string | number, DiceInfo>` so Bishop's `'current'` key worked alongside legacy `0`.  I did the same for `pickup` (`'rollDice'` + `'take'` outbound, `0` inbound) — saves a parallel event system.  Future Bishop collections needing inbound-vs-outbound shapes should follow the same widening pattern.
3. **Don't optimistically mutate scene state from frontend protocol commands.**  When the player clicks a wall tile during pickup, I emit `pickup.take` and STOP — no preview move.  The runtime's `things` UPDATE moves the tile.  This avoids resync drift when the backend rejects (e.g. wrong seat, wrong count, wrong phase).  Phase D-frontend's claim arc follows the same rule.
4. **Variant hot-swap is hard.**  The setup pipeline rebuilds the entire tile catalogue at construction.  Live variant change leaves orphan Things in the scene graph.  Phase F warns "Reload to change variant" rather than attempting a hot-swap; Phase G can promote when setup gets a clean dispose path.
5. **CSS body classes as variant gates.**  Setting `body.variant-changsha` or `body.variant-riichi` and gating `.changsha-only` / `.riichi-only` with `!important display: none` is cleaner than per-element `.style.display` bookkeeping.  Pickers stay declared in the HTML; CSS hides the wrong ones.
6. **URL → localStorage → defaults priority chain.**  Standard pattern but easy to invert by mistake.  URL wins (deep-link friendly), localStorage is the user's persistent choice, `Conditions.defaultsFor(gameType)` is the floor.  My `resolvePhaseFParams()` does this cleanly.
7. **Variant indicator badge is high-value, low-effort.**  Players don't remember what they clicked 30 seconds ago.  A top-right pill with the variant emoji + name is the single biggest UX-clarity win in Phase F.

### Smoke recipe (Changsha — Wave 3 path, validates no regression)
1. Open `/autotable/?variant=changsha&dealMode=manual&botCount=0`.
2. Top-right shows `🀄 Changsha`.  Sidebar setup-group exposes deal-mode, bot pickers; fives/points/honba/reset-points hidden.
3. Take seat 0.  Bot banner stays hidden (botCount=0).
4. Click Deal — Wave 3 flow.  108 tiles deal, dice HUD briefly.  No pickup HUD (no `pickup` collection entries yet from backend).
5. Play through to Hu — scoring modal renders.  No regression vs Phase D-frontend.

### Smoke recipe (post-Bishop — when pickup runtime lands)
1. Open `/autotable/?variant=changsha&dealMode=manual&botCount=3`.
2. Take seat 0.  3 bots join.  Bot banner reads `3 bots — Medium · seats 1, 2, 3`.
3. Roll-dice button appears at center.  Click it.  Backend resolves dice + break-point.
4. Pickup HUD: "Your turn — pick 4 tiles" + Take 4 button.  Click Take 4 (or click any wall tile).  Runtime moves 4 tiles to hand.
5. Repeat 3 rounds + single + dealer extra → discard → claim window → Hu.

### Stubs awaiting Bishop
- Backend `pickup` collection (singleton emit, command-shape ack).
- Backend bot engine (seat-fill driven by `botCount` + `botDifficulty`).
- Backend variant gating (skip Changsha state machine when `Conditions.gameType !== CHANGSHA`).
- Wall-glow class application — CSS hook is `.wall-glow` on wall meshes; needs object-view extension to walk `next-N` wall slots and add/remove the class on `pickup` update.  Phase G.

### Open questions for the team
- Should `Take N` be enabled even when N > remaining wall tiles?  Probably no — bundle currently sends `count: pickup.count` straight through; backend should reject if wall depleted.
- Should the break marker position be computed in `world.ts` from the actual 3D wall geometry, or stay as CSS-positioned per-seat overlay?  MVP picked CSS; Phase G can promote to true 3D overlay if visual feedback is unconvincing.
- Variant switch via dropdown currently triggers a soft warning in the variant badge ("↻ Reload to change to ..."), not an auto-reload.  Acceptable UX? Or should we `location.reload()` after a 2 s confirmation?

---

## Phase G — Sidebar lobby UI (2025-05-21)

**Branch:** `stlong/phase-g-bot-scheduler-lobby` off `1e9134a` (Phase F merge).
**Bundle SHA:** `autotable-src.33f97fad.js` (1.03 MB) + `autotable-src.7934372e.css` (7.8 kB).
**Replaces:** `autotable-src.6d5fae4c.js` + `autotable-src.1c6f6789.css`.

### What I shipped
Path-1 sidebar lobby (plain TS + HTML + CSS, NO React, inside the existing
autotable bundle) so users can pick `variant` / `dealMode` / `botCount` /
`botDifficulty` without editing the URL bar.  Anchored top-left, dark
semi-opaque panel with brass-gold accent matching the rest of the
autotable chrome.  Visible by default on a bare URL; otherwise reached
via the top-left **☰ Lobby** toggle.

### Files touched
| File | Purpose |
|---|---|
| `src/lobby.ts` | NEW — 200 LOC: URL parse/build, picker read/write, gating, show/hide, Apply & Start nav |
| `index.html` | `#lobby-toggle` button + `#lobby-panel` markup (4 fieldsets, radio buttons) |
| `src/index.ts` | `initLobby()` call before `assetLoader.loadAll()` |
| `src/style.css` | +135 LOC `#lobby-*` styling, `body.lobby-active` gate, `.lobby-disabled` greying |

No `world.ts`, no `client.ts`, no `game-ui.ts`, no setup pipeline, no
backend, no tests touched — strict Phase G scope.

### Learnings worth remembering
1. **Lobby is a one-way bridge into the existing query-param backend.**
   `Apply & Start` builds the URL and calls `window.location.replace()`;
   the rest of the system (Phase F game-ui's `resolvePhaseFParams`,
   Bishop's `AutotableWsEndpoint` URL parser) reads its existing URL
   params unchanged.  No new IPC, no new event types, no new collection
   registration — keep the bridge as thin as possible.
2. **`window.location.replace()` not `assign()`.**  Avoids back-button
   bouncing between game configurations; the lobby semantics are
   "abandon current game, start fresh," not "navigate."
3. **Skip irrelevant params on emit.**  `dealMode` is Changsha-only;
   `botDifficulty` is bots>0 only.  Suppressing them when irrelevant
   keeps URLs scan-readable and avoids confusing the backend with a
   `dealMode=manual` on Riichi where the term has no meaning.
4. **PascalCase vs lowercase for `botDifficulty` matters.**  The
   backend's `AutotableConnection.BotDifficulty` is PascalCase
   (`Easy/Medium/Hard`), but Phase F's frontend `game-ui.ts` parser
   accepts only lowercase.  Lobby reads case-insensitively, always emits
   PascalCase — matches the spec example in the Phase G charter and the
   backend's parser.
5. **CSS `body.lobby-active` for toggle suppression beats per-element
   show/hide bookkeeping.**  Single class flip in JS, single CSS rule
   (`body.lobby-active #lobby-toggle { display: none; }`).  Mirrors the
   Phase F `body.variant-changsha` / `.variant-riichi` pattern.
6. **`.lobby-disabled` opacity + `disabled` on radio inputs.**  Visual
   greying alone isn't enough — keyboard and screen-reader users need
   the `disabled` attribute on the actual `<input type="radio">`.  Toggle
   both in the same `refreshDisabledStates()` pass.
7. **Show-on-load policy: bare URL only.**  `window.location.search === ''`
   is the trigger.  Once the user has applied a setting, subsequent
   loads carry params, so the lobby stays out of the way unless the
   user explicitly opens it.  This is the right default for "I want to
   play, not configure."
8. **Parcel rebuild prunes nothing.**  Old hashed `.js`/`.css` linger in
   the dist dir until you `rm` them manually.  Phase F + Phase G have
   both bitten me on this — staging the new bundle without removing the
   old leaves a half-megabyte of dead code in the deploy artifact.
   Should consider a `prebuild` script that `rm`'s `dist/autotable-src.*`
   before parcel runs (Phase H polish).

### Smoke recipe
1. Browse to `/autotable/` (bare URL, no query string).  Lobby auto-opens.
2. Pick Changsha + Manual + 3 bots + Medium → click **Apply & Start**.
3. URL becomes
   `/autotable/?variant=changsha&dealMode=manual&botCount=3&botDifficulty=Medium`.
   Fresh game starts.
4. Click top-left **☰ Lobby** to re-open mid-game without losing settings
   (until Apply is clicked).
5. Switch variant to Riichi 4p → `dealMode` fieldset greys out + radios
   disabled.  Switch bots to 0 → `botDifficulty` fieldset greys out.

### Deferrals
- **Soft hot-swap of variant / bot config** mid-session — V2 / Phase H.
  Requires a clean dispose path on the setup pipeline so the tile
  catalogue and slot groups can be rebuilt without leaving dangling
  Things in the scene graph (same blocker Phase F flagged).
- **localStorage persistence of lobby pickers** — intentionally
  URL-only.  URL is the source of truth; localStorage in `game-ui.ts`
  remains for in-game pickers that aren't full-game restarts.
- **Multi-human lobby** (create / join by code, nicknames) — out of
  scope; single-game-per-instance is the Wave-3 / Phase F assumption.
- **Mobile-responsive width** — fixed 320 px, fine for desktop / tablet.

## Phase G — Sidebar lobby UI + bot-pickup timer awareness (2026-05-20T20-30-58Z)

**Shipped by:** Hicks (frontend)

Phase G shipped pre-game sidebar lobby picker (variant/dealMode/botCount/botDifficulty selection) on bare `/autotable/` URL so users don't edit query params. One-way bridge to Phase F query-param backend; lobby auto-closes on navigate. Bundle transition: `6d5fae4c.js` + `1c6f6789.css` → `33f97fad.js` + `7934372e.css`. 200 LOC lobby module + 135 LOC styling. tsc strict ✓; parcel build ✓.

**Key learnings:** Gating logic (dealMode disabled on non-Changsha, botDifficulty disabled when botCount=0) must be bidirectional (read AND write). URL parsing lenient for back-compat (kebab or SCREAMING_SNAKE for variant). Show-on-load policy: bare URL only (once any param applied, lobby hidden behind toggle).

**Cross-agent awareness:** Bot-pickup now server-driven per Bishop (500ms ticks); UI no longer needs client-side timer for bot seats.

---

## Phase H Wave 1 — Lobby polish + Dockerfile audit (2026-05-21)

**Branch:** `stlong/phase-h-wave-1-stability-polish` off `730946c` (Phase G merge).
**Bundle SHA before:** `autotable-src.33f97fad.js` (1.03 MB) + `autotable-src.7934372e.css` (7.8 kB).
**Bundle SHA after:**  `autotable-src.c97ea9e9.js` (1.03 MB) + `autotable-src.96cb3b60.css` (9.4 kB).
**Replaces:** prior Phase G bundle (pruned).

### What I shipped — Lobby polish (Task 1)

Four capabilities layered on top of the Phase G sidebar lobby in
`src/frontend/autotable-src/src/lobby.ts` + `index.html` + `style.css`:

1. **Seed override.**  Optional text input in a collapsible "Advanced"
   `<details>` section.  Empty/blank → server picks a random seed
   (current behaviour); filled with `0 ≤ N ≤ 2³¹−1` → URL gets `&seed=N`
   so the game is byte-reproducible.  Validation: integer-only regex
   (`/^-?\d+$/`), range check, red-border + inline error if invalid.
   Apply button is blocked + the seed input focused on invalid input.
2. **Hand count selector.**  Radio fieldset under Bot difficulty.
   Options 4 / 8 / 16 / 32 with annotations
   (quick / East round / half match / full match).  Default = 8.  Always
   emitted as `&handCount=N` on apply.  Backend doesn't read this yet
   (Phase H V2 wiring — Bishop) but the lobby contract is in place.
3. **Save defaults.**  Checkbox left of the Apply button.  When ticked,
   the resolved state writes to `localStorage` under
   `mahjong.lobby.defaults` as JSON
   (`{variant, dealMode, botCount, botDifficulty, seed, handCount}`).
   On next bare-URL page load the lobby pre-populates from localStorage.
   Resolution chain: URL params > localStorage > hardcoded DEFAULTS.
4. **About / Known Limitations link.**  Small footer link below the
   apply row.  Points at
   `https://github.com/long2know/mahjong-autotable/blob/main/docs/known-limitations.md`
   (Ripley owns the doc).  GitHub URL is the chosen target because the
   backend (Program.cs) only serves `/autotable/*` as static files — a
   relative `/docs/known-limitations.md` link would 404.  GitHub also
   renders markdown natively.

### Files touched

| File | Purpose |
|---|---|
| `src/lobby.ts` | +220 LOC: types (HandCount), DEFAULTS extended, `coerceSeed`, `parseLocalStorageState`, `resolveInitialState` (URL > LS > DEFAULTS), `writeLocalStorageDefaults`, seed input validation, hand-count picker wiring, save-defaults checkbox, about-link hardening |
| `index.html` | +45 LOC: hand-count fieldset, `<details class="lobby-advanced">` with `#lobby-seed` text input + `.lobby-seed-error`, footer rebuilt with `.lobby-save-defaults` checkbox + Apply, `.lobby-about` row with `#lobby-about-link` |
| `src/style.css` | +106 LOC: `.lobby-advanced` collapsible + summary, `#lobby-seed` + `.lobby-seed-invalid` red-border state, `.lobby-seed-error`, `.lobby-advanced-hint`, `.lobby-save-defaults`, `.lobby-about` link styling, footer flex layout updated to space-between |
| `src/frontend/autotable/**` | Parcel rebuild: new hashed `.js`/`.css`, pruned the Phase G `33f97fad.js` + `7934372e.css` |

No `world.ts`, no `client.ts`, no `game-ui.ts`, no setup pipeline, no
backend, no tests touched — strict Phase H Wave 1 scope.

### Files touched — Dockerfile audit (Task 2)

| File | Purpose |
|---|---|
| `infra/docker/Dockerfile` | Removed `modern-build` stage (node:24-alpine COPY of deleted `src/frontend/modern/`) and the `runtime-modern` final stage that depended on it.  10 lines deleted, no other edits — the SDK build + ASP.NET runtime stage that copies `src/frontend/autotable/` into `/app/wwwroot/autotable` is correct as-is. |

Dockerfile state after: 14 lines, single SDK build stage + single ASPNET
runtime stage.  No `modern/` references anywhere.

### Learnings worth remembering

1. **Parcel `--public-url .` is required for the autotable bundle.**
   Upstream Makefile target `build` uses
   `parcel build *.html --public-url .` — without the flag, Parcel emits
   absolute URLs (`/icon-96.auto.png`) in the rendered HTML, which
   404 because the backend serves the bundle from `/autotable/*`.
   My first build accidentally regressed every asset URL in
   `about.html` + `index.html` to absolute paths; the diff was a 50-line
   sea of `href=foo.png` → `href=/foo.png`.  Rebuild with
   `--public-url .` is byte-identical to Phase G for everything except
   the changed lobby markup + new hashes.  **Always pass `--public-url .`
   for any future Parcel build.**  Phase H polish candidate: add a
   `"build": "parcel build index.html --dist-dir ../autotable --public-url ."`
   npm script so the flag is impossible to forget.

2. **The autotable-src index.html lives at the project root, not in
   `src/`.**  The prompt suggested `parcel build src/index.ts src/index.html`
   but the html entrypoint is actually `index.html` at
   `src/frontend/autotable-src/index.html`.  Parcel picks up
   `<script type="module" src="./src/index.ts">` from the html so passing
   the .ts entry separately is unnecessary and can confuse Parcel about
   which is the root document.

3. **Optional-vs-null distinction in `Partial<LobbyState>` for the
   seed.**  `parseUrlState()` returns `Partial<LobbyState>` (each field
   may be undefined if not specified).  But `seed` is *legitimately*
   nullable in the resolved state — null means "random."  Resolution
   logic has to check `url.seed !== undefined` not `url.seed != null`,
   because an explicit `seed=42` in the URL must override a stored
   `seed: null` in localStorage, and vice versa.  Easy to miss; cost me
   a re-read of the resolution chain logic.

4. **`<details>` with custom `summary` styling.**  Set
   `list-style: revert` on the summary so the disclosure triangle stays
   visible — the default reset rules in some bootstrap-flavoured CSS
   strip it.  Keep the summary `outline: none` to avoid double focus
   rings.

5. **localStorage I/O wrapped in try/catch.**  Privacy mode (Safari),
   quota exhaustion, and tampered payloads all throw on access.  Wrap
   `localStorage.getItem` + `JSON.parse` together in one try block;
   wrap `setItem` in its own.  Don't propagate the failure — the
   URL-driven Apply still works without LS persistence.

6. **Dockerfile only needs the backend SDK + ASPNET runtime stages.**
   The pre-built `src/frontend/autotable/` bundle is what gets copied
   into `wwwroot/autotable`.  No Node stage is needed because the
   bundle is committed to the repo (the upstream autotable pattern —
   the build artifact is the deploy artifact).  Phase H V2 candidate:
   add an optional Node stage that rebuilds the bundle from
   `autotable-src/` so we can ship without a pre-built bundle in git.

### Smoke recipe (Stephen)

1. `/autotable/` (bare URL).  Lobby auto-opens.  Confirm new sections:
   "Hands per match" radio (default 8), "Advanced" collapsible (closed),
   "Save as defaults" checkbox (unchecked) left of Apply,
   "ℹ︎ About / Known Limitations" footer link.
2. Click Advanced → seed input appears.  Type `12345` → no red border.
   Type `99999999999` (too large) → red border + inline error.
3. Pick Changsha + Manual + 3 bots + Medium + handCount=8 + seed=12345 →
   tick "Save as defaults" → click **Apply & Start**.
4. URL becomes
   `/autotable/?variant=changsha&dealMode=manual&botCount=3&botDifficulty=Medium&handCount=8&seed=12345`.
   Fresh game.
5. Reload bare URL (`/autotable/` no params) — lobby reopens with the
   saved choices pre-populated from localStorage.
6. Click About link → new tab opens to the GitHub markdown render of
   `docs/known-limitations.md` on main.

### Deferrals

- **handCount runtime support.**  Backend (Bishop) needs to read
  `?handCount=N` and end the match after N hands.  Lobby contract is
  shipped; runtime wiring is V2.
- **seed deep-linking validation in the backend.**  Bishop's
  `AutotableWsEndpoint.CreateGameAsync` currently always passes
  `seed: null`; needs to read `?seed=N` query param and forward.
- **`--public-url .` baked into a build script.**  Phase H polish.
- **localStorage versioning.**  Current key is unversioned
  (`mahjong.lobby.defaults`); when the LobbyState shape changes
  meaningfully, bump to `.v2` and ignore old payloads on read.


## 2026-05-22 — Phase H Wave 2 — stacked-pattern chips + RobbingKong UI polish

Optional polish for Bishop's V2-rules backend work.  Shipped on branch
`stlong/phase-h-wave-2-v2-rules` (already cut from `main 8ec6cfa`).

### What rendered

- **Stacked-pattern chips** in the win-result modal — every big-win
  pattern that fires (`AllPatterns[]`) gets its own colour-coded pill:
  - 七对 Seven Pairs (purple)
  - 碰碰胡 All Pungs (brown)
  - 清一色 Full Flush (blue)
  - 九幺 Nine Terminals (gold)  ← new for Phase H Wave 2
  - `Standard` intentionally omitted (baseline non-stacking pattern,
    per Ripley §2.3).
- **RobbingKong badge** — prominent red-glow `抢杠胡 Robbing Kong`
  tag rendered when `result.isRobbedKong === true` or
  `result.method === 'RobbingKong'`.  Fires only on real Hu.
- **Backward compat** — when `AllPatterns` is absent (Bishop's
  protocol-layer commit not yet shipped) the UI falls back to the
  legacy `result.pattern` single-pattern field, or simply hides the
  chip strip if no pattern data is on the wire.

### Defensive wire contract

Local `ResultExtras` interface in `game-ui.ts:36` covers four optional
new fields (`pattern?`, `method?`, `allPatterns?`, `isRobbedKong?`)
plus PascalCase aliases.  Every field gracefully no-ops when missing.
`HandResultEntry` itself in `types.ts` is **untouched** — out of Wave 2
frontend scope.

### Bundle hash transition

| | Before (Phase H W1) | After (Phase H W2) |
|---|---|---|
| JS  | `autotable-src.c97ea9e9.js` (1.03 MB) | `autotable-src.74e239e6.js` (1.04 MB) |
| CSS | `autotable-src.96cb3b60.css` (9.4 kB) | `autotable-src.674133df.css` (10.37 kB) |
| Bootstrap CSS | `autotable-src.df85b4c4.css` | `autotable-src.df85b4c4.css` (unchanged) |

Pruned: `autotable-src.c97ea9e9.js`, `autotable-src.96cb3b60.css`.

### `--public-url .` invariant — verified

The Wave 1 captured invariant held.  Build command (with corrected
paths — task description had `src/index.html` typo, the file lives at
package root):

```bash
cd src/frontend/autotable-src
npx parcel build index.html about.html \
  --dist-dir ../autotable \
  --public-url . \
  --no-source-maps \
  --no-cache
```

Asset-path audit (`grep '^[a-z]+=/' index.html`) — zero absolute paths.
All `href=` / `src=` references are bare hashed-asset filenames that
resolve relative to the page, so the bundle mounts cleanly under
`/autotable/`.

### TS strict check

`npx tsc --noEmit --strict --target es6 --moduleResolution bundler
--esModuleInterop --lib DOM,DOM.Iterable,es6,es2017 src/index.ts`
→ exit 0.

### Bot-watch enhancement — deferred

The "bot turn-history sidebar" mentioned in the directive does not yet
exist (`lobby.ts` is the pre-game setup sidebar, not a move log; grep
for `turn-history|move-log|moveHistory` returned zero hits).  Flagged
as a Phase I scope item: stand up a streaming move-log sidebar that
renders `Bot 2 won by Robbing Kong (清一色 + 碰碰胡)` lines.

### Manual smoke-test checklist (for reviewer)

1. Run a Changsha hand to completion as a Hu.  Open the result modal.
   - Single-pattern win → one chip below the winner line.
   - Multi-pattern (e.g. 清一色 + 碰碰胡) → two chips, side by side.
   - 九幺 win (once Vasquez's tests land) → gold chip.
2. Force a RobbingKong scenario via Bishop's runtime path.  Verify the
   red `抢杠胡 Robbing Kong` badge renders to the left of the chips.
3. Trigger a Draw / ZhaHu — confirm chip strip stays hidden (no
   stray pattern data on these result types).
4. Refresh `/autotable/...` URL with cache disabled.  Confirm
   `Network` tab loads `autotable-src.74e239e6.js` (NEW) and
   `autotable-src.674133df.css` (NEW), no 404s for the deleted
   `c97ea9e9.js` / `96cb3b60.css` hashes.

### Phase I polish ideas

- In-game move-log sidebar (deferred bot-watch enhancement).
- Score multiplier breakdown in modal (`6 × 2 patterns = 12`).
- 九幺-specific 3D highlight on terminal tiles.
- Distinct Robbing-Kong audio cue.
- Pattern-chip hover tooltips with spec excerpts.
- Self-draw 自摸 badge (green counterpart to the red Robbing-Kong badge).
- `handCount` progress pill in header (once Bishop wires the V2 runtime).

---

## 2026-05-22 — Phase I Wave 1 — Score-multiplier breakdown + streaming move-log

**Branch:** `stlong/phase-i-wave-1-special-wins-ux`
**Commit:** `f91c95e`
**Bundle:** `autotable-src.4ce16ecc.js` + `autotable-src.8ade01c3.css`
**TS strict check:** exit 0
**Inbox drop:** `.squad/decisions/inbox/hicks-phase-i-wave-1.md`

Two deliverables in one commit:

1. **Score-multiplier breakdown** in the result modal — names the
   multiplier source by reading `scoreResult.{category, basePoints,
   payments[]}` and `result.allPatterns[]` (both optional on the wire;
   block hides itself when absent so the legacy modal layout still
   ships green pre-Bishop-translator).
2. **Streaming move-log sidebar** (`src/frontend/autotable-src/src/
   move-log.ts`, new) — self-contained module subscribing to the
   existing client collections (`match` / `dice` / `things` / `sound` /
   `claim` / `pickup` / `result`).  Tile-aware via slot-name parsing
   (`@<seat>` suffix on `discard.*` and `meld.*` slots).  Caps at 50
   rows, auto-scrolls to newest, suppresses noisy `pickup-progress`
   phases, dedups burst-arriving meld tiles.

### 5 new contextual Big-Win patterns wired up

Added camelCase labels + distinct chip hues for Bishop's branch:

- `heavenlyHand`       — 天和 Heavenly Hand
- `earthlyHand`        — 地和 Earthly Hand
- `lastTileFromWall`   — 海底捞月 Last Tile
- `lastDiscardCatch`   — 河底捞鱼 Last Discard
- `kongReplacementWin` — 杠上开花 Kong Bloom

Open-fallback lookup: `PATTERN_LABELS[normalizePatternKey(p)] ?? p`.

### Build invariants (CONFIRMED)

- `parcel build … --public-url .` is mandatory.  Asset paths in the
  built `index.html` are bare relative filenames; no leading `/`.
- **Build-command tweak:** the Wave-2-documented invocation
  `parcel build src/index.ts src/index.html` is wrong on two counts —
  `src/index.html` doesn't exist (file is at `autotable-src/index.html`,
  not under `src/`), and passing both entries emits a duplicate
  `src/index.js` artifact.  Use `parcel build index.html …` instead.
  Documented in the inbox drop for Coordinator to merge into the Wave-2
  invariant.

### Discoveries

- **PATTERN_LABELS were keyed with the wrong case in Wave 2.**  Backend
  `WinPatternToWire` emits camelCase (`sevenPairs`); Wave-2 keys were
  PascalCase (`SevenPairs`).  In production neither chip nor breakdown
  would have rendered.  Fixed by rebasing on camelCase + adding
  `normalizePatternKey()` for PascalCase fallback.
- **`result.current` doesn't carry score/pattern data yet.**
  `ChangshaToAutotableTranslator.BuildHandResult` (line 215-222) only
  emits the legacy `{winner, type, score, hand, nextBanker}` shape.
  Bishop needs to extend the translator (or push the
  `handSummary.scoreResult/winResult` shape into `result.current`) for
  the new UI blocks to light up in production.  Bundle gracefully
  no-ops until then.

### Phase J polish ideas

- Push `scoreResult` + `winResult` onto `result.current` so the modal
  breakdown actually lights up.
- First-class `events` collection so the move-log doesn't depend on
  slot-name parsing for tile resolution.
- Move-log row → camera-pan / tile-highlight.
- Move-log filter chips ("Show only wins / claims / discards").
- Replay export (JSONL).
- Multi-language toggle (English-only / 中文-only / bilingual).
- Mobile bottom-sheet layout for the move-log.
- Audio cue per pattern (distinct chime for 天和, gong for stacked Big
  Wins).
- Score-breakdown counter-ramp animation (0 → Total over ~600 ms).

---

## 2026-05-22 — Phase I Wave 2 — UI polish (pattern tooltips + self-draw badge + move-log win-type emoji)

**Branch:** `stlong/phase-i-wave-2-hydration-bot-ctx`
**Pre-wave bundle:** `autotable-src.4ce16ecc.js` + `autotable-src.8ade01c3.css`
**Post-wave bundle:** `autotable-src.e6653bd3.js` + `autotable-src.60fe83d8.css`

### Deliverables

1. **Pattern-chip hover tooltips** — every chip in `#result-pattern-chips`
   now carries a `<div class="pattern-tooltip">` child with the 大字
   Chinese name + one-line English description.  Pure-CSS reveal
   (opacity transition on `:hover`); `position: absolute` above the
   chip; `pointer-events: none` so it never blocks clicks.  Dictionary
   `PATTERN_TOOLTIPS` keyed by camelCase pattern key, normalised via
   the existing `normalizePatternKey()`.
2. **Self-draw / Discard / Robbing-Kong pill trio** — new
   `.result-win-type-pill` shared base class with three colour
   modifiers (`.win-type-self-draw` green, `.win-type-discard`
   yellow, `.win-type-robbing-kong` orange).  Self-draw / Discard
   render next to the winner name in `#result-winner`; RobbingKong
   stays in the chip strip (restyled — old red `.result-method-badge`
   rule removed, now uses the shared pill class).  Discard pill names
   the source seat via `winResult.sourceSeatIndex`.
3. **Move-log win-type emoji prefix** — Hu rows in the streaming
   sidebar now lead with 🀄 (self-draw), 🎯 (discard), or ⚡
   (robbing-kong).  Single-glyph prefix keeps the row tight.

### Files touched

- `src/frontend/autotable-src/src/game-ui.ts` — `PATTERN_TOOLTIPS`
  const, `WinResultExtra` extended with `winType?` + `sourceSeatIndex?`,
  chip rendering emits tooltip children, new
  `renderResultWinTypeBadge` method, RobbingKong badge restyled.
- `src/frontend/autotable-src/src/move-log.ts` — `WinResultLoose`
  extended with `winType?`, Hu action text gets a category-emoji
  prefix (`🀄 / 🎯 / ⚡`).
- `src/frontend/autotable-src/src/style.css` — `.pattern-tooltip`,
  `.result-win-type-pill` (+ three colour modifiers).  Old
  `.result-method-badge` / `.method-robbing-kong` removed.
- `src/frontend/autotable/autotable-src.*.{js,css}` — regenerated
  with new hashes; old `4ce16ecc.js` + `8ade01c3.css` pruned manually.

### Wire-shape assumption

Backend `WinResultEntry.WinType` (camelCase JSON `winType`) is one of
`"selfDraw"` / `"discard"` / `"robbingKong"` — confirmed in
`AutotableProtocol.cs:160` and
`ChangshaToAutotableTranslator.cs:263` (`WinMethodToWire`).
`sourceSeatIndex` carries the discarder/declarer index.  Every new
read-site uses `?:` so a pre-W2 wire payload silently falls through.

### Build invariants (CONFIRMED, unchanged from Wave 1)

- `npx tsc --noEmit --strict --target es6 --moduleResolution bundler
  --esModuleInterop --lib DOM,DOM.Iterable,es6,es2017 src/index.ts`
  → exit 0
- `npx parcel build index.html --dist-dir ../autotable
  --public-url . --no-source-maps --no-cache` → success in ~7 s
- After parcel: manually `rm` the previous hashed bundle files
  (parcel doesn't prune); bootstrap CSS `df85b4c4.css` is
  byte-identical and remains.

### Discoveries / notes

- **Modal stacking.**  `.pattern-tooltip` uses `z-index: 1060` to
  sit above bootstrap's `.modal` (z=1050).  Important: without an
  explicit z-index the tooltip rendered behind the modal backdrop
  on Firefox.
- **Position trap.**  `.result-pattern-chip` needed `position: relative`
  added before the tooltip's `position: absolute` would anchor to it
  (the chip was previously `display: inline-block` with no position).
- **RobbingKong text change.**  Old badge said `抢杠胡 Robbing Kong`;
  spec called for `抢杠 Robbing the Kong` (matching the no-胡 prefix
  style of the new self-draw / discard pills).  Quietly updated.

### 2026-05-21: Phase I Wave 3 — Lobby Game ID input + URL persistence

Branch `stlong/phase-i-wave-3-multigame-bot-strength`.  Bishop is lifting
`DefaultGameId` coercion at `AutotableWsEndpoint.cs:263/278` in parallel.
The frontend now exposes the `?gameId=` query param that line 142 already
parses, so users can route to separate game state pools.

Surface area (Hicks-owned):
- `src/frontend/autotable-src/index.html` — new `.lobby-row#lobby-gameId-row`
  above the in-game `#server` Connect/Disconnect block with a single
  Game ID input + `.lobby-error` div + `.current-game-display`.
- `src/frontend/autotable-src/src/client-ui.ts` — `validateGameId()` gate,
  `readInitialGameId()` URL → input prefill, `resolveGameIdForConnect()`,
  `buildWsUrl()` appending `?gameId=<encoded>` to the WS URL, and a
  `history.replaceState` write-back via `setUrlState()` on connect.  The
  old `pushState` was downgraded to `replaceState` — refresh re-joins the
  same game without polluting browser history.
- `src/frontend/autotable-src/src/lobby.ts` — `buildUrl()` now preserves
  any `?gameId=` already on the page URL so Apply & Start doesn't drop
  the user back to the default game when they change variant/handCount.
- `src/frontend/autotable-src/src/style.css` — `.lobby-row` (sidebar
  input row), `.lobby-error` (red inline error), `.current-game-display`
  (italic gold gameId surfacing when connected), plus a `.connected`
  scope toggle on `#lobby-gameId-row` so `.server-connected` /
  `.server-disconnected` siblings swap (the existing scope at
  `style.css:87-88` only reaches `#server` descendants).

Validation rules (must match Bishop's expected backend cap):

- Trim, non-empty after trim
- `maxlength="64"`, pattern `[A-Za-z0-9_\-\.]+`
- Inline `.lobby-error` red text + red border + focus jump on failure
- Connect blocked until valid

Default: `"changsha-default"` (matches `AutotableWsEndpoint.DefaultGameId`).
A bare URL therefore prefills the default and continues routing the same
way it did pre-Wave-3.

### Build invariants (CONFIRMED)

- `npx tsc --noEmit --strict --target es6 --moduleResolution bundler
  --esModuleInterop --lib DOM,DOM.Iterable,es6,es2017 src/index.ts`
  → exit 0
- `npx parcel build index.html --dist-dir ../autotable --public-url .
  --no-source-maps --no-cache` → success in ~8 s
- Wave-2 bundle removed: `autotable-src.e6653bd3.js` +
  `autotable-src.60fe83d8.css`
- New hashes: `autotable-src.49eb3789.js` + `autotable-src.af973ea2.css`
- Bootstrap CSS `autotable-src.df85b4c4.css` byte-identical, retained

### Discoveries / notes

- **Parcel strips default `type="text"`.**  My first CSS pass used
  `input[type="text"]` selectors that didn't match the minified
  attribute-less input.  Dropped the `[type="text"]` filter — Parcel
  also strips the attribute on the lobby-seed input but the existing
  `#lobby-panel #lobby-seed` ID selector survives.  Lesson: when adding
  new input controls inside the autotable bundle, anchor selectors on
  ID or scoped class, never on the type attribute.
- **`.server-connected` / `.server-disconnected` scope.**  The existing
  pattern at `style.css:87-88` is `#server.connected DESCENDANT`, so it
  doesn't reach siblings of `#server`.  Mirrored the toggle onto
  `#lobby-gameId-row.connected` so the sibling-scoped row's children
  swap visibility the same way; client-ui.ts adds/removes the class in
  `onConnect`/`onDisconnect` alongside the existing `#server` toggle.
- **`pushState` → `replaceState`.**  The original `setUrlState` used
  `pushState` which would have stacked a history entry per connect.
  Refresh-re-joins-same-game wants replaceState; back-button no longer
  bounces between identical game URLs.

### 2026-05-21: Phase I Wave 4 — Lobby Spectate mode + all-bots-watch

Branch `stlong/phase-i-wave-4-bot-strength-spectator`.  Bishop is widening
the backend caps in parallel so `?seat=-1` becomes a no-seat spectator
connection and the `botCount` cap lifts from 3 → 4 when paired with
spectator.  Stephen asked for the "sit back and watch four bots play"
mode; this delivers the frontend half.

Surface area (Hicks-owned):
- `src/frontend/autotable-src/index.html` — new `Seat` fieldset between
  Bots and Bot difficulty with radios for Auto (`""`) / 0..3 / Spectate
  (`-1`).  Spectator hint paragraph beneath the picker (hidden by
  default; toggled visible when Spectate is selected).  `current-game-display`
  gains a `#spectator-pill` sibling so the connected-state Game ID
  label and the Spectating pill share a row.
- `src/frontend/autotable-src/src/lobby.ts` — `LobbyState.seat: SeatChoice`
  (`-1 | 0 | 1 | 2 | 3 | null`).  `parseUrlState` / `parseLocalStorageState`
  read `seat`; `buildUrl` emits `?seat=` only when an explicit choice was
  made.  `clampBotCountForSeat` guards `botCount=4` so it only lives in
  spectator-mode URLs.  Seat-radio onchange snaps `botCount` to 4 on
  flip-to-Spectate and back down to 3 on flip-away.  `refreshDisabledStates`
  enables/disables the 4-bot radio + spectator hint.
- `src/frontend/autotable-src/src/client-ui.ts` — exported
  `readSpectatorFromUrl()` so game-ui.ts can short-circuit on the same
  flag.  `buildWsUrl` now forwards `seat` + `botCount` from the page URL
  onto the WS URL (previously only `gameId` rode the WS URL; the page
  URL's lobby params never reached `context.Request.Query` on the
  server).  `applySpectatorClass()` toggles `body.spectating` from
  ctor + `connect` + `onConnect` (every place the URL might flip).
  Reconnect-into-seat is suppressed when spectating.
- `src/frontend/autotable-src/src/game-ui.ts` — `updateSeats` short-circuits
  for spectators (no take-seat row, no enabled deal/leave buttons).
  `refreshBotBanner` now renders the bot HUD for spectators too — that's
  the spectator's primary "who's playing" surface.
- `src/frontend/autotable-src/src/style.css` — `.spectator-pill` (green
  family matching the Wave-2 self-draw win-type pill at 311-316),
  `body.spectating` selectors hiding seat-buttons / #deal / #leave-seat /
  #toggle-dealer / #claim-* / #pickup-hud / #roll-dice, and a
  `.lobby-spectator-hint` block + `.lobby-radio-disabled` modifier for
  the 4-bot greyed-out state.

### URL contract

| Param | Value | Meaning |
|---|---|---|
| `seat` | `-1` | Spectator.  WS URL gets `?seat=-1`; body class is `spectating`; pill is visible. |
| `seat` | `0..3` | Explicit seat take.  Forwarded to WS URL. |
| `seat` | missing | Auto (legacy "server picks an open seat"). |
| `botCount` | `0..3` | Existing cap (any seat). |
| `botCount` | `4` | Allowed iff `seat=-1`; clamped to 3 otherwise by the lobby. |

### Build invariants (CONFIRMED)

- `npx tsc --noEmit --strict --target es6 --moduleResolution bundler
  --esModuleInterop --lib DOM,DOM.Iterable,es6,es2017 src/index.ts`
  → exit 0
- `npx parcel build index.html --dist-dir ../autotable --public-url .
  --no-source-maps --no-cache` → success in ~7 s
- Wave-3 bundle removed: `autotable-src.49eb3789.js` +
  `autotable-src.af973ea2.css`
- New hashes: `autotable-src.c93fbb44.js` + `autotable-src.3f21032c.css`
- Bootstrap CSS `autotable-src.df85b4c4.css` byte-identical, retained

### Discoveries / notes

- **WS URL was the missing forwarder.**  Bishop's backend has parsed
  `seat` + `botCount` off `context.Request.Query` since Phase F
  (AutotableWsEndpoint.cs:174 + :192), but `buildWsUrl` in client-ui.ts
  only ever appended `gameId`.  The page URL's lobby-chosen botCount
  therefore never reached the WS Upgrade — the server always defaulted.
  Wave 4 starts forwarding `seat` + `botCount` explicitly; broader
  param forwarding (variant/dealMode/botDifficulty) is deferred to keep
  Wave 4 scope tight, but the helper is structured so future params
  drop in easily.
- **Body class layered with !important.**  `body.spectating .seat-buttons
  { display: none !important; }` is needed because `updateSeats` writes
  `style.display = 'block'` inline for the seat-buttons container when
  `client.seat === null`.  I also short-circuited the JS path so both
  belts agree, but the !important rule is the defensive backstop.
- **Bot banner becomes the spectator HUD.**  Pre-Wave-4 the banner
  early-returned when `client.seat === null`; for spectators we now
  iterate all four seats (no self-seat skip) so the spectator sees
  who's at the table and what difficulty.
- **Parcel + boolean `value` attribute.**  `<input value="">` collapses
  to `<input value>` in the minified dist HTML, which is HTML-spec
  equivalent to `value=""`.  JS reads `.value === ''` correctly on the
  Auto radio — consistent with the Wave 3 pinned finding that we should
  anchor selectors on ID/name attributes, not on attribute defaults
  parcel might strip.

---

## Phase J Wave 1

- Shipped **hot-seat swap** (Move button + inline picker in the in-game HUD, visible only when connected + no match in progress; soft reconnect via `history.replaceState` on `?seat=` + `client.disconnect()` — client-ui.ts's existing auto-reconnect picks up the new seat on its own) AND **spectator camera lock** (one-line tweak: `world.seat` initial value is `null` when `?seat=-1`, so `main-view.ts`'s existing `fromTop` branch puts the camera top-down from the first frame instead of flashing seat-0 view).  Commit `781798e`, bundle `autotable-src.214d524e.js` + `autotable-src.884bb475.css` (pruned the Wave-I.4 hashes).  TS strict exit 0, Parcel build clean (7.75s), backend tests `Passed: 403` unchanged.  `client-ui.ts` / `main-view.ts` untouched — no client-ui.ts edit was needed because `buildWsUrl` already reads `?seat=` off the page URL.

## Phase J Wave 2

- **What shipped** — three primary UX deliverables on branch
  `stlong/phase-j-wave-2-completion`, commit `a92e5d1`
  ("feat(ui): Phase J Wave 2 — end-of-game summary + reconnect banner
  + settings drawer"):
  1. **End-of-game summary modal.**  Subscribes to a new `gameComplete`
     collection (singleton key `"current"`), renders per-seat totals
     table (winner-first) + hand-by-hand recap, with New Game / Back
     to Lobby actions.  Payload parsed defensively — accepts
     camelCase, PascalCase, `isComplete`, `IsComplete`, `isGameComplete`,
     `IsGameComplete`, optional `totalScores` / `handHistory` /
     `maxHands`.  When the server omits totals or history, the bundle
     derives both from the client-side `result.current` accumulator
     it already maintains during the match.
  2. **Connection-lost banner.**  Replaces the silent 2 s × 15-attempts
     reconnect loop with a visible state machine: yellow
     "reconnecting (N/5)", red "Could not reconnect" + Retry/Lobby
     buttons, green "Reconnected" 2 s flash.  Exponential backoff:
     1 s / 2 s / 4 s / 8 s / 16 s, 5 attempts.  User-initiated
     `disconnect()` is silent (cancels timers + clears
     `wasDisconnected`).
  3. **Settings drawer.**  ⚙ gear top-right opens a slide-out aside
     with Bot Strength / Hand Count / Auto-Deal.  Persists per-gameId
     in localStorage under `autotable.phaseJ.v1.settings.<gameId>`
     (plus a global default key for fresh tabs).  Apply rewrites URL
     params (`botDifficulty`, `handCount`, `dealMode`) and reloads.
- **Lobby alignment.**  Hand-count default 8 → 4, added `1` as an
  option (default east-wind rotation matches Bishop's runtime).  Bot
  difficulty default Medium → Hard per directive.
- **Files** — `client.ts` (new `gameComplete` Collection +
  `GameCompleteEntry` interface), `client-ui.ts` (new reconnect loop +
  banner lifecycle, `connect()` signature simplified), `game-ui.ts`
  (modal + settings drawer + client-side history accumulator;
  module-level `SettingsState` helpers), `lobby.ts` (default shifts +
  hand-count widening), `index.html` (4 new top-level UI nodes),
  `style.css` (~220 lines appended).  Strictly untouched:
  `src/backend/**` (Bishop's lane), `src/backend/tests/**` (Vasquez's
  lane), `src/frontend/autotable-src/src/types.ts`, `world.ts`,
  `game.ts`, `main-view.ts`.
- **Gates** — TS strict exit 0; Parcel build success; dotnet test
  `Passed: 415, Failed: 3` (the 3 failures are Vasquez's red
  `GameCompletionTests` waiting on Bishop's `MaxHands` /
  `IsGameComplete` / `ChangshaPhase.GameComplete` backend contract,
  entirely within their lane, pre-existed my work and are unmoved by
  my frontend-only commit).
- **Bundle** — `autotable-src.90818e21.js` + `autotable-src.60a1fda4.css`
  (replaced Wave J.1's `autotable-src.214d524e.js` +
  `autotable-src.884bb475.css`; bootstrap CSS `df85b4c4` retained
  byte-identical).
- **Memo** — `.squad/decisions/inbox/hicks-phase-j-wave-2.md` carries
  the full deliverable description + Bishop / Vasquez coordination
  notes + UX rationale.

### Discoveries / notes

- **Bishop's wire vocabulary unknown at ship time.**  His
  `bishop-phase-j-wave-2.md` memo hadn't dropped before I shipped, so
  the bundle accepts a superset of plausible field names (camelCase /
  PascalCase / `isComplete` / `isGameComplete`) and falls back to
  client-derived totals/history when the payload omits them.  Vasquez's
  red tests in `GameCompletionTests.cs` revealed his backend contract
  surface (`MaxHands`, `IsGameComplete`, `ChangshaPhase.GameComplete`)
  but NOT the WS-collection vocabulary.  If his collection lands as
  `match["current"]` or `gameState["current"]` instead of
  `gameComplete["current"]`, the follow-up is a one-liner subscription
  rewrite.
- **`connect()` signature change is a soft breakage.**  Old first
  arg (`reconnectAttempts`) is now `_legacy` — no internal caller
  passes it, but the rename is preserved so a stray external caller
  (if any future code is added) trips a TS warning rather than a
  silent zero.
- **Hand-history dedup uses structural fingerprint
  (`JSON.stringify(last) === JSON.stringify(result)`).**  Suppresses
  double-counting when connect-time full-syncs replay the current
  `result.current`.  May be brittle if Bishop mutates the object
  reference shape between sends — worth revisiting if a follow-up
  reveals dup recap rows.
- **localStorage read priority is URL > gameId-keyed > global > defaults.**
  So a deep-linked URL always wins over personal localStorage, and
  the per-game key always wins over the global default key.
- **Auto-Deal maps to existing wire field.**  Settings drawer's
  checkbox surfaces as `?dealMode=auto|manual` in the URL — the Phase F
  contract.  No new wire field was added for Wave 2's drawer; Bot
  Strength → existing `?botDifficulty=`, Hand Count → existing
  `?handCount=`.

### Outstanding / next-pickup

- Watch for Bishop's memo at
  `.squad/decisions/inbox/bishop-phase-j-wave-2.md`.  If his collection
  / key / payload schema doesn't match the defensive
  `gameComplete["current"]` scaffolding, a follow-up commit needs to
  realign the subscription (the parser already accepts most field-name
  variants).
- The 3 RED tests in `GameCompletionTests.cs` are Vasquez's red gates
  for Bishop's backend contract — they live entirely outside my lane
  and will go green when Bishop's commit lands.  Confirm no frontend
  follow-up is needed (parser superset should cover most cases) once
  the contract crystallises.

## Phase J Wave 3 — Sound effects, replay viewer, canonical pattern ordering (commit `77855da`)

**Brief:** Stephen's Wave 3 directive — three parallel UI tracks:
1. Sound effects manager wired into game events (settings toggle + autoplay unlock).
2. End-of-game replay viewer accessible from the gameComplete modal.
3. Canonical pattern display ordering applied to the result modal chip strip + move-log win row.

All three landed in a single commit `77855da` on `stlong/phase-j-wave-3-completion`.

### Scope completed

**Sound effects (`src/sound.ts`, ~310 LOC, new file)**

Six events — draw / discard / claim / win / washout / gameComplete — wired through a synth-generated Web Audio API module. Picked synth over CC0 assets to keep the bundle weightless and the licensing audit empty: ~310 LOC of self-contained recipes (`playClack`, `playChime`, `playFanfare`, `playWashout`, `playGameComplete`), AudioContext created lazily on first user gesture, master gain at 0.6 + per-voice envelopes 0.3-0.5 peak. Settings drawer toggle `#settings-sound` (default ON) + `?sound=on|off` URL override drive `Sound.setMuted()`. Draw SFX throttled 200 ms minimum so the initial 13-tile deal collapses to one clack instead of a typewriter rattle.

**Replay viewer (`src/replay.ts`, ~640 LOC, new file)**

2D top-down DOM-based viewer accessed from the end-of-game modal via a new `#game-complete-replay` button. Per-seat zones (4 quadrants) with tile glyph chips; per-hand timeline with play/pause/step/scrub footer controls. Captures tile transitions in real time from `client.things` (`hand.*` → draw, `discard.*` → discard, `meld.*` → meld) into a per-hand buffer; flushes the in-progress hand on every `result.current` update. Server-pushed `handHistory` (from `gameComplete` payload) merged in `Replay.open()` with server results taking precedence over client-captured moves. 3D scene reuse deferred — the live scene is too coupled to active game state to retrofit a playback mode within Wave 3 scope.

**Canonical pattern ordering (`src/game-ui.ts` + `src/move-log.ts`)**

`PATTERN_DISPLAY_ORDER` hardcoded list matches Bishop's `ChangshaPatternOrdering` table 1:1 (slot 1 HeavenlyHand → slot 13 SingleWait, with reserved slots 6, 7, 10, 12-13 for patterns not yet implemented). Unknown patterns sort alphabetically after the listed ones. `comparePatterns()` / `sortPatterns()` exported and applied to (a) result-modal chip strip via `renderResultPatternChips`, (b) move-log Hu-row patterns via `.sort(comparePatterns)`. 

**Live wire upgrade** — `loadPatternOrderingFromApi()` fires a one-shot `fetch('api/changsha/pattern-ordering')` from `src/index.ts` at boot. On success, `setPatternDisplayOrder()` overwrites the in-process map with Bishop's canonical table. On failure (404 / offline / parse), the hardcoded list keeps rendering correctly. Result: a future Wave that adds a new pattern to Bishop's table is picked up on next page-load without a frontend code change.

**`WinResult.IsSelfDraw` + `IsKongReplacement` (Bishop's new bools)**

Consumed in `move-log.ts` — prefix selection prefers `winType === 'selfDraw'` (existing Wave I.2 path); when `winType` is missing, falls back to Bishop's `isSelfDraw` bool. `isKongReplacement` destructured but informational — the contextual verb selector already picks up `kongReplacementWin` from `AllPatterns`.

### Methodology — what worked

- **Hardcoded fallback + live wire upgrade, not either/or.** Initial implementation only had the hardcoded list. When Bishop's commits landed I added the boot-time fetch as a non-blocking upgrade — the hardcoded list keeps the chip strip correct even if the endpoint is offline, but a Wave-N pattern addition propagates without a frontend rebuild.
- **Synth-only sound = zero coordination tax.** No asset files = no Dockerfile change = no Apone follow-up = no CC0 audit. The whole sound feature shipped without touching any other agent's lane.
- **2D replay first, 3D later.** 3D scene reuse was the obvious instinct but the cost estimate (~800-1500 LOC for a separate scene, or a fragile state-rewind layer) broke the Wave 3 budget. 2D DOM-based viewer is ~640 LOC, ships in one commit, and validates whether players actually want replay before investing in 3D polish.
- **Throttle draw sounds.** Without the 200 ms minimum gap the initial 13-tile deal sounds like a typewriter; with it, the deal collapses to one clack and per-turn draws stay distinct.

### Surprises / blind spots

- **`tsconfig.json` includes server/dist by default.** TypeScript strict run flagged 5x TS6305 errors on `server/dist/*.d.ts` artifacts left over from a prior server build. These are pre-existing (not introduced by my changes); confirmed by filtering on `src/` paths only. No real type errors from Wave 3 code.
- **`?sound=on|off` URL override conflicts with localStorage default-true.** First implementation read localStorage first then applied URL override; resulted in a stale-toggle bug when toggling between tabs. Fixed by URL override winning unconditionally and persisting back to localStorage on first apply.
- **`client.things` collection doesn't expose per-event deltas.** Capture loop reconstructs deltas by diffing the full `things` map against the previous snapshot — works but is O(N) per `things.update`. Performance acceptable (~136 tiles + melds = sub-millisecond on tested hardware) but a future optimization could subscribe to the underlying `Thing` collection's per-key events instead.

### Stability

- **TypeScript strict (`tsc --noEmit`):** **0 src/ errors** (5 pre-existing TS6305 on server/dist artifacts unrelated to Wave 3).
- **Parcel build:** **succeeded in 4.29s** — new bundle `autotable-src.330c36fd.js` (1.08 MB) + `autotable-src.f8d8d79e.css` (25.27 kB).
- **Backend tests (Vasquez `d7c5337`):** **424 passed / 0 failed / 0 skipped** — zero skips streak preserved.
- **Stale bundle pruned:** `autotable-src.90818e21.js` + `autotable-src.60a1fda4.css` removed (parcel-renames recorded in the commit).

### Cross-agent coordination

- **Bishop (`9235859`, `75baecc`, `2e84179`)** — three contract surfaces consumed: `/api/changsha/pattern-ordering` fetched at boot, `WinResult.IsSelfDraw` consumed as fallback in move-log prefix selection, `WinResult.IsKongReplacement` destructured (informational). Every consumer falls back gracefully on a pre-W3 payload.
- **Apone (`ea2c991`)** — **no Dockerfile change required** for Wave 3 (synth-only sounds ship zero asset files; existing `COPY src/frontend/autotable/ → wwwroot/autotable` bundle copy holds).
- **Vasquez (`d7c5337`)** — new DOM ids `#replay-screen`, `#settings-sound`, `#game-complete-replay` available for future Playwright selectors.

Memo: `.squad/decisions/inbox/hicks-phase-j-wave-3.md`.
