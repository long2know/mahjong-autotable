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
