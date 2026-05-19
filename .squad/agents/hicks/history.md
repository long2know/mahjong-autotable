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
