# Hicks — production human-action UI / current backend wire contract

Implemented frontend wiring is aligned to Bishop's current `AutotableProtocol.cs`, `TryHandleOwnTurnActionAsync`, `RejectSeatActionAsync`, and `ChangshaBaseUnit.cs`. Independent source review and new-candidate browser qualification are still required. No live UI acceptance is claimed.

## Stable selectors
- Owner-only region `#own-turn-actions`; buttons `#own-turn-hu`, `#own-turn-concealed-kong`, `#own-turn-added-kong` (same data-testid values). Unavailable declarations are hidden; pending declarations are disabled/aria-busy.
- Native chooser `#rule-action-choice-dialog[data-kind="chow|concealedKong|addedKong"]`; title `#rule-action-choice-title`; `#rule-action-choices > .rule-action-choice[data-tile-ids]`; cancel `#rule-action-choice-cancel`. `data-tile-ids` is the comma-separated exact physical partner/quad/singleton from server metadata. Native Enter/Space activates; dialog Escape cancels without passing. Existing global reserved claim/camera-key mapping is unchanged.
- Creation `#lobby-base-unit` (same testid), inline `#lobby-base-unit-error`, current authoritative unit `#lobby-current-base-unit`.

## Exact normal WS shapes
- Owner metadata: `ownTurn[seat] = {gameId, stateVersion, hu, concealedKongs:number[][], addedKongs:number[]}`; null tombstones retract. `gameId` here is the opaque RUNTIME id and can differ from the URL/WS room alias.
- Self-draw: `UPDATE entries:[["ownTurn",seat,{gameId:<metadata.gameId>,expectedVersion:<metadata.stateVersion>,action:"hu"}]]`, full:false.
- Concealed/added: same envelope with action `"concealedKong"` plus the exact four `tileIds`, or `"addedKong"` plus the exact one-element `tileIds`. No claim fallback; endpoint invokes dedicated DeclareWinAsync/DeclareKongAsync.
- Chow: owner `claim[seat].chowOptions:number[][]`; a chosen option sends `UPDATE entries:[["claim",seat,{action:"claim",type:"Chow",tileIds:<chosen pair>}]]`, full:false. One option is direct; multiple require a real choice. No metadata/options means Chow is not advertised; the backend's legacy lowest-choice compatibility is not used by UI.
- Rejection: `actionRejected["current"]={action,reason,requestedSeat,ownedSeat}`, followed by a full snapshot. UI surfaces the reason and resets pending through authoritative refresh. Success settles on changed/retracted metadata. A 15s no-confirmation notice permits an explicit retry but never automatically resends.
- Metadata only grants availability. UI additionally checks current connection/URL room alias/full-snapshot ownership, current seat, turn phase, completion and captured context. Pre-draw 13/7-tile intermediate phases never invent Hu/Kong availability. Choosers close on ownership, connection, phase, tombstone, deadline or option-context changes.

## Base unit
- Frontend mirrors current server range **1..11184810**, default **1** (`floor(Int32.MaxValue/(16*12))`). Query field is `baseUnit`; active value is `match[0].conditions.baseUnit`.
- Apply/Quick Match validate and carry the creation unit; changed unit is game-defining and creates a fresh room. Header New Game preserves/stamps it using the existing URL helper. Reloaded existing rooms keep their server unit; the active readout/URL reconcile to that received value. Scores are not multiplied on the frontend. Presets/caps/house rules remain untouched.

## Current evidence / boundaries
`session-files/qualification/2026-09-12/hicks-actions/`: exact pre-grant source snapshots, protected hashes, source logs. Existing browser-free URL/activation + approved Ferro R2 source-only controls **47 PASS /0 FAIL /0 SKIP**, strict production typecheck exit0. Scoped lint still has the known14 errors/1 warning; baseline comparison running. Npm regeneration is the next step and owned solely by Hicks. No browser, image/container/key/volume operation, test/harness edit, branch/staging/commit/push, or self-approval.

Chow metadata currently has deadline/source/tile/options but no mandatory window/version token. UI invalidates against those authoritative fields and every transition/tombstone. Bishop was asked about optional stronger version/window binding; no nonexistent mandatory server field is assumed. Frontend accepts gameId/stateVersion only if the producer supplies them.

## Final detailed-contract alignment supersedes pending notes above
See `sessions/2026-09-12-hicks-actions-contract-alignment.md` for current hashes and exact behavior. Unconfirmed flights remain pending across timeout/equal or lower versions; the15s notice offers explicit `#rule-action-reload` recovery via real reload, not re-enabled action submission. Claim arbitration waits are labeled honestly. Own keys are numeric; legacy claim keys are strings with no unsupported game/version fields. Unbound placeholder match unit1 does not reconcile the creation URL until runtime turn metadata exists. Earlier permissive-timeout wording is superseded, with original evidence retained.
