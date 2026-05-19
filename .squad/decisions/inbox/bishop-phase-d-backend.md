# Bishop — Phase D-backend decisions (Changsha-driven autotable scene)

**Branch:** `stlong/phase-b-changsha-scene`
**Scope:** wire the existing Changsha runtime to the C-relay so the Changsha rules engine drives the autotable scene end-to-end. Runtime is now the source of truth for `match`/`seats`/`nicks`/`dice`/`things`/`claim`/`result`; bundle clients can still write cosmetic collections (`mouse`/`sound`/ad-hoc `things`) but cannot overwrite runtime-owned entries.
**Verification:** build 0 warnings 0 errors; full suite `Passed: 259 / Failed: 0 / Skipped: 9 / Total: 268`. Acceptance subset (`Category=Acceptance`): `Passed: 65 / Failed: 0 / Skipped: 1` (only `Hu_ThirteenOrphans_SpecGap_Skipped` remains — deferred to Phase E).
**LOC:** 957 insertions / 145 deletions across 13 files.

---

## 1. Runtime-vs-Client precedence (`AutotableGameState.ApplyUpdate`)

**Decision:** runtime writes always win over client writes for the same (`kind`, `key`) pair.

- `ApplyUpdate(IEnumerable<CollectionEntry>, UpdateSource source)` now takes an `UpdateSource` enum (`Client=0`, `Runtime=1`).
- A per-(kind, key) source-attribution dictionary is maintained alongside the entry store; `Client` writes that target a `Runtime`-owned key are silently dropped (excluded from the returned `applied` list, so they don't propagate to the relay broadcast either).
- Runtime overwrites any prior client value for the same key — that's the entire point of "rules engine is source of truth".
- `RemovePlayerEntries` cleans both stores in lock-step so per-player tombstones don't leave dangling source attributions.
- Test/diagnostic hook: `GetSource(kind, key)` for asserting attribution.

**Rationale:** the upstream pwmarcz/autotable assumes the relay is cosmetic. Changsha demands an authoritative server. The 2-source merge gives us a clean answer that doesn't require a separate "secured collections" namespace — bundle code stays unchanged.

## 2. Single-game-per-instance (Default #8, Stephen)

**Decision:** all `NEW`/`JOIN`/`UPDATE` resolve to the deterministic relay gameId `"changsha-default"`. Phase E will widen.

- `AutotableWsEndpoint.DefaultGameId = "changsha-default"`.
- `HandleNewAsync` and `HandleJoinAsync` ignore any client-supplied gameId; both coerce to `DefaultGameId`.
- A runtime Changsha game is lazily created on first seat-take via `EnsureRuntimeBoundAsync(relayGameId)`. Bindings live in `_runtimeBinding` (relay → runtime) and `_relayBinding` (runtime → relay) — both populated atomically under `_bindingLock`.
- Reverse-mapping is required so `IChangshaGameRuntime.StateChanged` (which fires with the runtime gameId) can find the relay gameId to broadcast to.

**Why "changsha-default" and not just a single Guid?** Determinism for test setup + a clean URL for the Stephen smoke test. The relay gameId is deliberately distinct from the runtime gameId (which still defaults to `Guid.NewGuid()`) so the runtime contract — "gameId is server-allocated" — stays untouched.

## 3. Auto-bot-fill query param (Default #6)

**Decision:** `?bots=true` (default ON) on the WS upgrade URL. Stephen smoke test → just connect, click Take Seat, get a playable hand. `?bots=false` for multi-human play and the E2E test.

- `AutotableConnection.AutoBotFill` is set from the query param at WS upgrade time and remains constant for that connection.
- On seat-take, the endpoint calls `IChangshaGameRuntime.TakeSeatAsync` then (when `AutoBotFill`) `FillEmptySeatsWithBotsAsync`. The runtime's StartGame call is deferred to the "Deal" command — taking a seat doesn't start the hand even with bots filled, so a fifth client can still observe the table before the hand starts.

## 4. Inbound UPDATE branching (collection-name routing)

**Decision:** Inside `HandleUpdateAsync`, route by `entry.Kind`:
- `seats` → `runtime.TakeSeatAsync` (+ optional `FillEmptySeatsWithBotsAsync`). Pass-through relay so the seat-marker animates immediately on other clients before runtime echoes the canonical seat back.
- `claim` (Changsha) → `runtime.ClaimAsync` / `runtime.PassAsync` based on `value.action` (Pung/Chow/Kong/Hu/Pass). No relay — runtime re-broadcasts.
- `match` → if `value.dealCommand == "start"` AND runtime phase is `Seating`, call `runtime.StartGameAsync`. Pass-through relay so the bundle's match overlay updates locally.
- `result` (Changsha) → server-emitted only; ignore client writes.
- everything else (`mouse`/`sound`/`dice`/`things`/`nicks`/`ephemeral`/`unique`/`perPlayer`) → existing relay-store + broadcast.

**Why pass-through some, not others?** `seats` lives in both worlds — bundle uses it for the seat-marker label, runtime uses it for authoritative seat ownership. Relaying the inbound entry to peers gives instant feedback; the runtime's next `StateChanged` confirms (and possibly corrects) it. `claim` is purely runtime-owned — relaying would just leak a click into other clients' UIs.

## 5. Translator extensions (`claim` + `result` collections)

**Decision:** add two Changsha-owned collections to the autotable wire protocol.

- `ChangshaCollectionKinds.Claim = "claim"`; key = seat index (int); value = `ClaimWindowEntry { Eligible: string[], Tile: int, DiscardSeat: int, DeadlineMs: long }` or `null` (closed).
- `ChangshaCollectionKinds.Result = "result"`; key = `"current"` (string); value = `HandResultEntry { Type, Winner, Loser, Score, Reason, Points }` or `null` (cleared on new hand).
- Translator emits `claim[seat]` for every Hu/Pung/Kong/Chow opportunity when `state.Phase == AwaitingClaim` and `state.ClaimWindow != null`. Empty/closed windows are not emitted (so a stale claim doesn't persist).
- Translator emits `result["current"]` when `state.Phase == EndHand`, with `Type` ∈ `Hu` / `Draw` / `ZhaHu` (false Hu).

**Why two new collections and not overload `match`?** Separation of concerns — Hicks's Phase D-frontend can subscribe to `claim` for the claim-window banner and `result` for the result modal independently of the `match` HUD. Keeping the kinds disjoint also means client-side `set([kind])` watchers don't have to filter.

## 6. Per-viewer privacy filter (`FilterEntriesForViewer`)

**Decision:** at every broadcast boundary (`SendFullSnapshotAsync`, `BroadcastToOthersAsync`, `BroadcastToAllAsync`), strip `face` and force `rotationIndex = 2` (face-down) on any `things` entry whose `slotName` starts with `hand.<X>@` where X ≠ viewerSeat.

- Wall (`wall.*`), discards (`discard.*`), and melds (`meld.*`) are unmodified — they're public game state.
- Viewer's own hand (`hand.<viewerSeat>@*`) is unmodified.
- The filter is structural (no info leak) for the slot-name field, but the bundle's thing-index → typeIndex mapping (with `fives='000'`) still encodes the face intrinsically. Documented as a Phase E concern (see §10).

**Why strip face and force rotation?** Defense-in-depth: any client that respects the `face` field gets `null`; any client that respects `rotationIndex` sees a face-down tile. The few clients that read `thingIndex` directly to derive face still get the right visual orientation (face-down) and an explicit hint that the face is hidden.

## 7. StateChanged → translate → apply(Runtime) → broadcast loop

**Decision:** the runtime's `StateChanged` event drives a per-connection translation, store-update (with `UpdateSource.Runtime`), and broadcast pass.

- `OnStateChanged(runtimeGameId)` looks up `_relayBinding[runtimeGameId]` → relay gameId, then re-uses `SendFullSnapshotAsync` per connection.
- `SendFullSnapshotAsync`, when a runtime is bound, calls `ChangshaToAutotableTranslator.Translate(state, viewerSeat, viewerPlayerId)` → `gameState.ApplyUpdate(entries, UpdateSource.Runtime)` → snapshot → filter for viewer → send.
- The per-viewer translation is now sourced from a single canonical store (the `AutotableGameState` with attribution). Late joiners get the same snapshot as anyone who has been connected since hand 1.
- The `ChangshaToAutotableTranslator` itself remains stateless and pure — it's still safe to call from non-WS test code.

**Why apply Runtime entries to the store at broadcast time, not on a separate timer?** No risk of stale broadcasts. The downside: every `StateChanged` triggers N translator calls + N JSON serializations. For 1-4 connections per game this is trivial. If/when Phase E scales to multiplayer rooms with observers, swap in a shared snapshot cache here.

## 8. False-Hu (诈胡) handling

**Decision:** add `RecordFalseHu(state, seat)` to `ChangshaGameStateMachine` as a side-effect-only API.

- Penalty per Baidu §"诈胡处罚" Big-Win equivalent — 6 units per opponent (`ScoringService.FalseHuPenaltyPerOpponent = 6`), totalling -18 for the offender / +6 to each of the three others.
- Does NOT throw, does NOT advance the hand. The runtime contract for `DeclareSelfDrawWin` on a non-winning hand still throws `InvalidOperationException` (existing test `Player_DeclaresHuOnNonWinningHand_RuntimeRejects` continues to pass). The frontend is responsible for translating "Hu clicked, runtime rejected" into "show toast + assess penalty" by calling `RecordFalseHu` separately.
- Audit log: `state.FalseHuPenalties` accumulates a `FalseHuPenalty` record per offence with offender seat, payments, and a timestamp (in event order via `state.EventLog`).

**Why a separate static method and not auto-apply inside `DeclareSelfDrawWin`?** Frontend flow control. A misclick that throws should not silently dock 18 points — the user wants a confirm dialog. Phase D-frontend (Hicks) owns that UX; backend gives them the primitive.

## 9. 过胡 (missed-win) per-draw decay

**Decision:** `DrawTile` removes the active seat from `state.MissedWinSeats` after a successful draw.

- Per Baidu §过水: the lockout is "until your next draw." Drawing clears it; self-draw was never blocked anyway.
- `HashSet.Remove` is a no-op if absent — safe to call unconditionally.
- **Side-effect:** the bot harness's deterministic seeds now produce longer hands (more Hu opportunities → more claim windows). `BotMatchHarness.RunUntilHandFinished` step budget bumped 800 → 4000.

## 10. Determinism fix for `HashCode.Combine` flake

**Decision:** replace `new Random(HashCode.Combine(state.Seed, state.HandNumber))` with a Knuth-style mix `(uint)Seed * 2654435761u + (uint)HandNumber`.

- `HashCode.Combine` is intentionally randomized per-process (DoS mitigation) — that breaks the seed-determinism contract for the bot harness. Tests passed ~80% of the time in parallel xUnit runs depending on which seeds happened to terminate within 4000 steps with the process-specific hash.
- The Knuth mix gives the same diversity (the 2654435761 multiplier is ⌊2³² / φ⌋, the same constant used in Hash Map's `hashInt` and Java's `IdentityHashMap`) while being a pure function of `(Seed, HandNumber)`.

**Verified:** 5 consecutive full-suite runs all green at 259/0/9/268 after the fix.

## Open Questions / Phase E concerns

1. **Multi-game lobby.** `DefaultGameId` is hardcoded. Phase E needs HTTP `POST /lobby/games` returning a gameId + a `?gameId=` resolution rule for `JOIN`. Suggest: lobby allocates a 6-letter slug; `JOIN` accepts any allocated slug; `NEW` becomes a deprecated alias for the lobby endpoint.
2. **Thing-index → face encoding.** The current 108-tile mapping makes tile-id reveal the face. Phase E needs randomized wall ordering so the index itself reveals nothing. Until then, the `face`-stripping + `rotationIndex` override is structural, not informational.
3. **Match[0] "Deal" trigger handshake.** The current heuristic is `dealCommand: "start"`. Hicks's Phase D-frontend may settle on a different shape — coordinate the wire format. The handler is otherwise defensive (no-op outside `Seating` phase).
4. **Reconnect / replay.** A WS disconnect during AwaitingDiscard mid-hand currently leaves the runtime hand intact (good) but the bundle's local thing-id index resets on reconnect (bundle behavior). The "rebuild local store from server UPDATE(full=true)" path works but the client may see a 200-ms blank screen. Phase E: short pre-connect "loading…" banner OR delta-since-version protocol.
5. **Per-game cleanup on last disconnect.** Today the `AutotableGameState` is cleared but the bound runtime game stays (Changsha runtime has its own grace window). When the runtime times out, `_runtimeBinding` would point at a missing game. Acceptable for Phase D-backend (no leak — runtime cleans up; binding entry is small) but Phase E should explicitly unbind on `IChangshaGameRuntime.HandleDisconnectAsync` cascade-cleanup.

## Stephen smoke-test recipe

1. `dotnet run --project src/backend/src/Mahjong.Autotable.Api`
2. Open `http://localhost:5000/autotable/` in a browser.
3. Wait for the bundle to load (3D scene, "Connect" button visible).
4. Click **Connect** — bundle opens WS to `/autotable/ws?bots=true`.
5. Click **Take Seat** (any seat). Three bots fill the other seats.
6. Click **Deal** (or whichever button Hicks wires to send `match[0].dealCommand="start"`).
7. Dice roll → tiles dealt to all 4 hands (other hands face-down, yours face-up).
8. Drag a tile to discard. Bots draw + discard automatically.
9. If a bot can claim (Pung/Chow/Kong/Hu), the claim window appears (`claim` collection update).
10. The hand ends when someone declares Hu or the wall exhausts. `result["current"]` populates with type + winner + score.

If step 7 or 10 fails, the failure mode is either (a) Hicks's Phase D-frontend hasn't wired the trigger yet, or (b) the translator's `match[0]` override is being clobbered — both are diagnosable from the WS frame log.
