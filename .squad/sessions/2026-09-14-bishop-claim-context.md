# Bishop — additive versioned claim context

**Implementation present; existing focused compatibility125/125 PASS, zero skips. NEW strict stale-context WS regressions are assigned to Hudson and still pending. Independent review/live admission remain required; qualification0.** This is a new source delta after the retained September12 own-turn handoff, not a claim that its prior source approval covers these changes.

## Exact contract for Hicks/Hudson
Own-turn contract unchanged: `['ownTurn', seatNumber, {gameId, expectedVersion, action:'hu'|'concealedKong'|'addedKong', tileIds?}]`; Hu omits IDs, concealed sends exact4, added exact1. Copy runtime gameId and availability.stateVersion. Success is authoritative state/version or tombstone, not an invented accepted/result DTO; rejection uses actionRejected/current plus corrective full snapshot.

Claim metadata now adds **gameId:string and stateVersion:int** alongside existing available/deadline/source/tile/chowOptions. Keys remain STRING seats. Normal new choice:
`['claim','0',{action:'claim',type:'Chow',gameId:'<runtime identity>',expectedVersion:42,tileIds:[a,b]}]`.
Pass may use the same context with `{action:'pass',type:null,gameId,expectedVersion}`. Both context fields must be supplied together; omitting BOTH retains legacy compatibility. A genuine new claim window increments game StateVersion; runtime gameId distinguishes rooms/new games, so deadline0/source/tile collisions do not authorize an old selection. Freeze chooser context at opening; do not silently promote an old pair to a newer version.

Rejection reasons include invalid-claim-context, stale-game, stale-version, invalid-claim-command, invalid-claim-choice and claim-not-available, in the existing actionRejected/current envelope. Contextual runtime failures now notify the sender rather than only logging. A successfully submitted claim still waits for normal arbitration/authoritative window resolution; no new immediate accepted receipt was added. BaseUnit unchanged.

## Backend changes
- Protocol/translator add optional wire fields compatibly; ordinary real claim metadata supplies both. Owner-only Chow partner privacy and private collection reattachment are unchanged.
- Shared context parser is used by own-turn (required) and claim/pass (optional both-or-neither). WS forwards expectedVersion and actual connection player ID into runtime ClaimAsync/PassAsync.
- Runtime rechecks ownership/version under lock. Explicit Chow choices are validated by the existing state-machine resolver on an isolated snapshot BEFORE queuing a response or cancelling timers, so bad choices reject without wedging arbitration and correct retry can succeed. No duplicate Chow algorithm or engine-file edits.
- Delayed public resolution, bot response and timeout work is bound to its original claim-window instance, preventing old work from operating on a subsequent window.
- Existing unversioned/no-tileIds compatibility retained. No frontend, frozen test/harness, Docker, live8950 or Git publication operations.

## Source SHA256 (under src/backend/src/Mahjong.Autotable.Api/)
- Autotable/AutotableProtocol.cs: 9ace2b923a4e0bcffa938a6bfc23131511b78f4596f2d84fce10058a025c4470
- Autotable/AutotableWsEndpoint.cs: 8cd5b64bbb64c39c5239a9b6bb5fd1323c809b00d58e261e0c20af7394161792
- Autotable/ChangshaToAutotableTranslator.cs: c98d77ddacaf2fdb92503818948a334784f70affcedbb0cef094306473024f35
- Changsha/Runtime/ChangshaGameRuntime.cs: b600cf06d9197c3d55acee1ccdd419df5d6acef77a9c518f40ebd4209f371f65

Evidence root `session-files/qualification/2026-09-12/bishop-actions/claim-context/`:
- review-manifest.json SHA fd2e67f78e90c3493ce939913703f825cd08969bdeef8516ba5c4533eeac2fdb
- source-delta.patch SHA 17387412205171cfb109c1f4bd7438f5d3b1ba0c980afcfbc05d370099dada7b
- results/claim-context-existing-01.trx SHA b91a9e651b89f50469e4eee199d8060cfb5422aae5293e18c55e87e8f754a3c8:124PASS/0FAIL/0skip (45 ownTurn,26 authorization,9 legacy human claims,37 translator,6 bot Chow,1 Hu finalization).
- results/claim-preflight-01.trx SHA499bfa7a0f148f80ef0c2409aa86780bb7ceafce8f5320d63fc0fa2473c48e17:1PASS, RuntimeInvalidChow_RejectsBeforeQueuing_AndValidClaimCanStillComplete.

These existing cases are NOT proof of every new versioned-claim discriminator. Hudson's NEW RulesQualification cases must cover identical deadline0-window stale context, changed gameId on same socket, malformed/partial context, current-context valid choice/pass, and invalid pair -> unchanged state/window -> correct retry. No test-owner files were edited or exclusions used. API/test assets/temp/results isolated beneath this evidence root; restore only followed retained NETSDK1004. Protected authorization/seat methods compare byte-identical to the prior own-turn baseline; protected audit/auth test hashes unchanged. No counted-game or independent approval claim.
