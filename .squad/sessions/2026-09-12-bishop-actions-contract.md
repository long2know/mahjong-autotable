# Bishop — own-turn / Chow / base-unit integration contract

Implementation work performed September 14, 2026 UTC against the September 12 audit grant; qualification remains 0 and live8950 is untouched. Source is not independently approved yet.

## Own-turn (separate from discard claims)
Server emits `['ownTurn', seatNumber, {gameId, stateVersion, hu, concealedKongs, addedKongs}]` ONLY to the actual owning human viewer. `gameId` is the runtime state's identity, NOT the URL relay alias. `concealedKongs` is an array of exact four-physical-ID arrays, `addedKongs` an array of individual physical fourth IDs. Every nonowner/unavailable seat receives explicit null; unbound destination-room JOIN retracts cached actions too. Collection can be registered ephemeral, but privacy/tombstones do not depend on registration.

Ordinary client commands:
- `['ownTurn', seat, {gameId, expectedVersion: stateVersion, action:'hu'}]`
- `['ownTurn', seat, {gameId, expectedVersion: stateVersion, action:'concealedKong', tileIds:[a,b,c,d]}]`
- `['ownTurn', seat, {gameId, expectedVersion: stateVersion, action:'addedKong', tileIds:[fourthId]}]`

Both numeric and integer-string seat keys are accepted. Commands require actual current seat ownership, matching runtime game identity, matching version and a current legal action checked under the dedicated runtime method's lock. Effective concealed+3*melds must equal14 (13/7 pre-draw intermediates are not ready). Hu uses Burke's CanDeclareSelfDrawWin/actual-draw provenance; Kong candidates use Burke's authoritative public candidate validators. No reinterpretation of old `claim Hu/Kong` after a phase transition. No raw command echo. Rejection uses existing `actionRejected/current`, including stale-game, stale-version, invalid-own-turn-command, own-turn-not-available and existing ownership reasons; corrective viewer snapshot follows when a game is bound.

`things`, `claim` (now containing private choices), and `ownTurn` are excluded from shared runtime snapshot storage and reattached from the current viewer's own translation, preserving existing occupied-seat/cross-room entitlement checks.

## Chow choices
Existing claim metadata adds optional `chowOptions:number[][]` ONLY for the owning claimant with a legal Chow opportunity. Each item contains the exact TWO concealed partner IDs, excluding the discarded tile; one deterministic physical pair per distinct legal sequence. Existing outbound `['claim',seat,{action:'claim',type:'Chow',tileIds:[a,b]}]` selects it. Omitted tileIds retains legacy compatibility. Other viewers never receive the partner arrays.

## Base unit
Accepted Burke's `ChangshaBaseUnit.MaxValue = int.MaxValue/(16*12) = 11184810`; default1, positive integer only. Canonical maximum bounds 16 hands times12 winner units; Burke owns checked payment/aggregate arithmetic. No MaxScorePerHand, preset or house-rule activation.

WS: `?baseUnit=N`, validated for Changsha (bad/fractional/noninteger/out-of-range/multiple values close1008); first runtime creator latches it. Later valid JOIN/reconnect settings cannot change the table's multiplier. State/snapshot `BaseUnit` defaults1 for old JSON. Translator exposes numeric `match.conditions.baseUnit`; no new phaseF wire object. Runtime appends optional `baseUnit=1` to CreateGameAsync. Legacy SignalR CreateGame(ruleSet,botSeatIndexes,seed) keeps THREE arguments/default1; explicit config uses CreateGameWithConfig({ruleSet,botSeatIndexes,seed,baseUnit}), with additive baseUnit in creation response/GameCreated/FullState.

Domain coordination: BaseUnit=1, LastDrawSeatIndex:int? and DiscardsThisHand:int are Bishop-owned carriers. Burke's pure engine owns setting/clearing provenance, lifetime discard accounting and scaling. Initial OwnDrawSeatIndex/OwnDrawTileId proposal was replaced before validation; it is NOT the implemented contract.

Hudson owns new actual WS integration tests; Bishop is running focused existing compatibility/authorization/translator tests with isolated artifacts. Hicks owns UI/URL/lobby/action/choice implementation. Ferro/Frost have received the exact ordinary contract above. No source freeze, live pilot admission, 120-match qualification or independent approval is claimed here.
