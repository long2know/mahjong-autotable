# Hudson — Phase 3 Stream C: Frontend Test Infrastructure

**Date:** 2026-05-13
**Branch:** `stlong/changsha-v1-phase3`
**Status:** Vitest infra landed. First wave of 47 frontend tests GREEN.

---

## What shipped

### Tooling
| Package | Version | Why |
| --- | --- | --- |
| `vitest` | ^4.1.6 | Test runner (peer-compatible with vite 6). |
| `@vitest/ui` | ^4.1.6 | Local dev UI (optional, ergonomic). |
| `jsdom` | ^29.1.1 | DOM environment for the bridge's `window.postMessage` path and React component tests. |
| `@testing-library/react` | ^16.3.2 | Hook + component rendering (React 19 compatible). |
| `@testing-library/jest-dom` | ^6.9.1 | DOM matchers (`toBeInTheDocument`, etc.). |
| `@testing-library/user-event` | ^14.6.1 | User-interaction simulation for future component tests. |

### Configuration
- `vite.config.ts` — added `test` block (jsdom env, `globals: false`, setupFiles, include glob).
- `src/test/setup.ts` — registers jest-dom matchers, polyfills `window.matchMedia` (Fluent UI 9 needs it during render).
- `package.json` scripts: `test` (single-run), `test:watch`, `test:ui`.

### Tests added (47 across 4 files)

#### `changshaReducer.test.ts` — 19 tests
- `GameCreated`: gameId/phase=lobby/seat array populated; missing seats fall back to defaults.
- `PlayerSeated`: nick + isBot updated, other seats untouched.
- `GameStarted + DiceRolled + BreakPointSet`: dealer, round wind, hand number, dice tuple `{die1,die2,sum}`, breakPoint coords; phase=dealing.
- `TilesDealt`: local-seat receives explicit tileIds; remote-seat is count-only; phase transitions to awaitingDiscard on isComplete.
- `TileDiscarded`: tile leaves concealed, enters shared discardPile, phase=awaitingClaim.
- `ClaimWindowOpen`: pendingClaims populated, phase=awaitingClaim.
- `ClaimMade` (pung): meld appended, tiles removed from concealed, activeSeat = claimer, phase=awaitingDiscard.
- `ClaimMade` (kong): exposedKong meld with 4 tiles.
- `ClaimMade` (chow): explicit tileIds move from concealed to exposed meld; unused tiles stay.
- `WinDeclared`: lastWin captured, phase=scoring.
- `ScoringComplete`: seat scores updated, phase=endHand.
- `BankerRotated`: bankerSeat updated, phase=rotatingBanker (winner-becomes-dealer asserted via the `reason` field).
- `HandFinished`: dealer + per-hand state clear, phase=rollingDice (or endGame if `isGameOver`).
- `RoundChanged`: prevalentWind + roundNumber.
- `GameEnded`: phase=endGame, finalScores applied.
- `reset`: returns initial state.

#### `autotableBridge.test.ts` — 5 tests
- Outbound queues until iframe posts `{type:'ready'}`, then flushes.
- Envelope is `{proto:'changsha-bridge/1', type, ...}` on every send (tests pin the proto sentinel).
- Inbound only fires when `ev.source === iframe.contentWindow` (foreign sources ignored).
- Malformed / wrong-proto / garbage data dropped silently; `isReady` stays false.
- `dispose()` detaches the window message listener.

#### `signalrClient.test.ts` — 19 tests
- All invoke wrappers (createGame, joinTable, takeSeat, startGame, rollDice, acknowledgeDeal, discard, claim with+without tileIds, pass, declareKong, declareWin, reconnectGame) pinned to method-name + payload-object shape.
- `attachServerEventHandlers` registers `conn.on` only for handlers supplied; forwards payloads; teardown removes all listeners; one handler throwing does not break the others (logs once via console.error).
- `describeConnectionState` maps every HubConnectionState to the public ConnectionStatus enum; explicitly asserts Disconnected ≠ idle (the live hook relies on this for UI state).

#### `useChangshaMockGame.test.ts` — 4 tests
- Hook mounts in jsdom + React 19; returns the expected action surface (9 functions).
- `dealMock` produces the canonical 14/13/13/13 split (banker gets 14), wall remaining = 108 − 53.
- `discard` removes the local tile, appends to shared discardPile.
- `resetDemo` returns state to seating phase with empty hands and empty discard pile.

---

## How to run

```
cd src/frontend/modern
npm test          # CI mode — single run
npm run test:watch  # watch mode
npm run test:ui   # browser-based UI (vitest)
```

Latest verified run: **47 passed / 0 failed / 0 todo** in ~3.7s.

---

## What's still uncovered (recommended next-wave tests)

Ranked by risk per effort. Numbers in `[brackets]` are estimated days.

1. **`useLiveChangshaGame` hook** `[1.0d]`
   The single biggest remaining blind spot. Holds the SignalR connection lifecycle, dispatch loop, claim-window timer, reconnect path. Currently untestable in isolation because the HubConnection is constructed in module scope. **Refactor first** — inject a connection factory, then add tests for:
   - First-mount opens connection and registers handlers.
   - Server event dispatches reducer action with correct payload.
   - `reconnect()` re-invokes JoinTable / replays via FullState (after Hicks's WIP lands).
   - Disconnection surfaces `connectionStatus = 'disconnected'`.
   - Component unmount tears down handlers + connection.

2. **`autotableBridge.diffAndSend`** `[0.5d]`
   The function that translates ChangshaGameState diffs into outbound messages. Today untested. Phase 3 will exercise it harder once the 3D scene receiver becomes real, but it should be unit-tested for: dice/breakPoint/discardPile/dealing transitions and the reset-on-gameId-change case.

3. **Component tests for the visible UI** `[1.0d]`
   Tests should cover `ChangshaTablePage` (mode toggle, child rendering), `DiceRollModal` (open/close + dice display), `PlayerHandPanel` (tile rendering, click → discard handler call), `BankerBadge` + `RoundWindIndicator` (correct labels + glyphs), `ClaimPromptModal` (button visibility per opportunity type, chow tile-pair selection — once Hicks ships it), `FanBreakdownPanel` (score-table rendering). Requires Hicks's Phase 3 component refactor to land first.

4. **`tileUtils.ts` pure helpers** `[0.2d]`
   `tileFromId`, `tileGlyph`, `tileLabel`, `generateFullTileSet`, `windLabel`, `windEnglish`. Pure functions, low-risk, but cheap to lock. Catches accidental tile-id-arithmetic regressions if Hicks ever changes the encoding.

5. **`useChangshaGame` mode picker** `[0.2d]`
   `shouldUseMock` reading localStorage → mock vs live; localStorage absent → falls back to `import.meta.env.DEV`. Currently untested; trivial to lock.

6. **Bridge security — clobber-resistance** `[0.5d]`
   The bridge filters by `ev.source === iframe.contentWindow`. Add a test for the malicious-iframe scenario where a sibling iframe spoofs a source. May require harder thought about whether to switch to `targetOrigin` enforcement in production (currently `*`).

7. **Reducer phase-machine invariants** `[0.5d]`
   Property-style test: from any reachable phase, every event preserves: `seats.length === 4`, `hands.length ≤ 4`, `wallRemaining ∈ [0, 108]`, `discardPile.length + sum(concealed) + sum(meld tiles) ≤ 108`. Catches accidental over-counting bugs.

---

## Blockers / Coordination notes

- **Hicks's Phase 3 Stream B WIP** currently in the working tree (uncommitted) reshapes:
  - `signalrClient.ts` invoke wrappers → positional args, adds `fillWithBots`, `reconnectGame(gameId, seatIndex)`.
  - `changshaReducer.ts` → adds `FullState` action + `phaseFromWire` normalization + tileIds on PendingClaim.
  - `types.ts` → adds `tileIds` to PendingClaim.
  - `useLiveChangshaGame.ts` → uses the new APIs (currently inconsistent with committed signalrClient signatures, breaks `tsc -b`).
  - New components: `LobbyCard.tsx`, `OpponentDiscardTrays.tsx` (untracked).
  When Hicks commits and pushes those changes, **his PR must update my tests** to match: `signalrClient.test.ts` invoke assertions, `changshaReducer.test.ts` ClaimWindowOpen tileIds assertion, and add a FullState action test. The contract is documented in the test file headers ("when Hicks's PR lands, update X").

- **No CI workflow added this pass.** `npm test` from `src/frontend/modern/` is the local + reviewer command. CI integration is a follow-up task (suggest adding a `frontend-tests` job to `.github/workflows/squad-ci.yml` that runs `npm ci && npm test` in the modern frontend directory).

---

## Skeptical notes

- The 47 tests cover **state-transition correctness** and **wire-protocol contract** for the bridge and SignalR client. They do **not** prove the live runtime works against a real .NET hub — that needs an E2E spike against a running backend.
- The mock hook tests pin the action surface but do not assert any real Mahjong rule (the mock generates random hands). Rule conformance is exclusively the backend's responsibility — fine for this stream's scope.
- Vitest 4 is brand new (released within the past quarter). If we hit instability, drop to `vitest@^3.x` — it's the well-trodden line.
- `@testing-library/react@16` was specifically released for React 19 compatibility (the prior `13.x` line did not support React 19's stricter dev-mode checks). Pin floor `^16.3` to avoid regressions.

---

## Recommendation

Merge this stream's infra commit independently of Hicks's Phase 3 Stream B. The 47 tests pin a meaningful baseline of frontend correctness and create the framework for everything in the "next wave" list above. Future PRs touching the reducer, bridge, or SignalR client will surface contract drift here rather than at runtime.
