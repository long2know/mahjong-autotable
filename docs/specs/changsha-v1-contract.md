# Changsha v1 Rule Contract (Iteration: Draw/Discard Core Loop)

## 1) Scope for this iteration

This contract defines the first executable `changsha-v1` gameplay slice for backend implementation alignment.

Included now:
- 4-seat round initialization with fixed dealer for round start.
- Deterministic wall construction and dealing.
- Dealer/turn/draw/discard loop.
- Human and bot seats using one legal-action validation pipeline.
- Exhaustive-draw stop condition and explicit error semantics.

Excluded now (explicitly deferred): claim windows, pung/chow/kong resolution, win declaration validation, fan/scoring settlement, and dealer continuation rules across hands.

## 2) Initial supported gameplay flow

### 2.1 Round bootstrap
1. Create round with `seatCount = 4` and `ruleProfile = changsha-v1`.
2. Assign seats (`0..3`) and mark each seat as `human` or `bot`.
3. Assign `dealerSeat` (default seat `0` for first iteration unless table contract overrides).
4. Build deterministic wall from the round RNG seed.
5. Deal 13 tiles to each seat in seat order.
6. Dealer performs initial draw to 14 tiles.
7. Enter `AwaitDiscard` phase with `activeSeat = dealerSeat`.

### 2.2 Core turn loop (no claim interrupts in v1)
For each turn:
1. `activeSeat` must discard exactly one tile from concealed hand.
2. After valid discard, if live wall has tiles:
   - Advance to next seat clockwise.
   - Next seat draws one tile from live wall.
   - Enter `AwaitDiscard` for that seat.
3. If live wall is exhausted before required draw, round ends as exhaustive draw.

### 2.3 Dealer behavior in this iteration
- Dealer is used to determine round opener only.
- Dealer retention/rotation after hand end is out-of-scope for this iteration.

## 3) Backend state model contract (required fields)

Authoritative state payload must include at least:

### 3.1 Round metadata
- `ruleProfile: string` (`changsha-v1`)
- `roundId: string`
- `stateVersion: int` (monotonic, +1 per committed action/event)
- `phase: enum` (`NotStarted | AwaitDiscard | Ended`)
- `status: enum` (`Active | Ended`)
- `dealerSeat: int` (`0..3`)
- `activeSeat: int` (`0..3`)
- `turnNumber: int` (starts at 1 on dealer opening discard)

### 3.2 Seating and hands
- `seats[4]`:
  - `seatIndex: int`
  - `seatType: enum` (`Human | Bot`)
  - `playerId: string`
  - `concealedTiles: tileId[]` (authoritative hidden hand)
  - `discardPile: tileId[]`

### 3.3 Wall and tile accounting
- `wallTiles: tileId[]` (ordered deterministic wall for replay)
- `liveWallDrawIndex: int` (next draw pointer)
- `tilesRemaining: int` (derived or stored, must be consistent)
- `dealtTileCount: int` (for invariant checks)

### 3.4 Action/audit metadata
- `actionSequence: long` (strictly increasing)
- `lastAction`:
  - `sequence`
  - `seatIndex` (or `system`)
  - `actionType` (`RoundStart | Draw | Discard | RoundEnd`)
  - `payload`
- `rng`:
  - `algorithmId: string`
  - `seed: string`
- `integrity`:
  - `stateHash: string` (hash of canonical state serialization)

## 4) Legal actions and validation rules (human + bot)

## 4.1 Action model
- External seat action in this iteration: `Discard(tileId)`.
- Draw is a server-owned transition event, not a client-authored action.
- Bot turns must submit through the same action command/validator used by humans.

## 4.2 Shared validation rules
Reject action if any check fails:
1. Round is not `Active`.
2. Phase is not `AwaitDiscard`.
3. Acting seat is not `activeSeat`.
4. Seat is not part of the round.
5. Submitted `tileId` is not present in acting seat concealed hand.
6. Action sequence/version token is stale (optimistic concurrency failure).

## 4.3 Successful discard transition
On accepted `Discard(tileId)`:
1. Remove tile from acting seat concealed hand.
2. Append tile to acting seat discard pile.
3. Emit `Discard` event with incremented `actionSequence` and `stateVersion`.
4. If next-seat draw is possible:
   - Set `activeSeat = (activeSeat + 1) mod 4`.
   - Draw one tile for new active seat.
   - Emit `Draw` event.
   - Increment `turnNumber`.
5. Else emit `RoundEnd(reason=LiveWallExhausted)` and set `status=Ended`, `phase=Ended`.

## 4.4 Seat-type constraints
- Seat type (`Human` vs `Bot`) does not change legality rules.
- Any bot scheduler endpoint is orchestration only; it must not bypass rule validation.

## 5) Stop/end conditions and error semantics

## 5.1 End conditions in this iteration
- `LiveWallExhausted`: required draw cannot occur because no live-wall tiles remain.
- `AbortInvalidState`: invariant violation detected (tile conservation, bad active seat, invalid hand count).

## 5.2 Error semantics
Use deterministic error codes for rejected actions:
- `ROUND_NOT_ACTIVE`
- `INVALID_PHASE`
- `NOT_ACTIVE_SEAT`
- `SEAT_NOT_FOUND`
- `TILE_NOT_IN_HAND`
- `CONCURRENCY_CONFLICT`
- `STATE_INVARIANT_BROKEN`

Error payload must include:
- `code`
- `message`
- `stateVersion`
- `actionSequence`
- `correlationId`

## 6) Deterministic and replay constraints

1. All randomization must derive from stored seed + declared algorithm.
2. Canonical wall order must be reproducible from replay inputs.
3. Server transition logic must be deterministic given:
   - initial state
   - ordered accepted actions
4. Persist append-only event log with strict ordering key (`actionSequence`).
5. Persist or derive canonical state hash per committed transition.
6. `OccurredUtc` timestamps are audit metadata only and must not affect legal transition outcomes.
7. Replay command must reproduce final `stateHash` exactly or fail integrity check.

## 7) Explicitly out-of-scope Changsha rules (for now)

Deferred to later phases:
- Discard claim windows (`chi/chow`, `peng/pung`, `gang/kong`, claim priority arbitration).
- Concealed/exposed/add-a-tile kong handling and supplemental draws.
- Robbing-kong behavior.
- Win declaration (`hu`) eligibility checks.
- Fan/point calculation and settlement ledger updates.
- Dealer continuation/rotation policy across hands.
- Round/game match progression across multiple hands.
- Timeout/default-action product policy finalization (except generic safe fallback requirement).

## 8) Implementation alignment acceptance criteria (this contract)

This contract is satisfied for the current phase when:
1. Backend state includes required contract fields (or equivalent typed structure with complete mapping).
2. A full 4-seat hand can run deterministically through draw/discard-only loop to `LiveWallExhausted`.
3. Humans and bots both pass through identical discard validation.
4. Illegal actions return explicit contract error codes.
5. Replay from seed + accepted action log reproduces matching final state hash.
