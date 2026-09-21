# Bishop — F-RELAY-ORIGIN-SEAT source correction

**Outcome: implemented; targeted source proof GREEN; independent review and new immutable-candidate browser acceptance still required.** September 10, 2026 UTC / September 9 PDT. No browser, image/dist build, server restart, live candidate mutation, staging, commit, or delegation.

## Proven root / boundary
- Retained Ferro diagnostic/master hashes verified. Genuine desktop selection sends handshake `variant=four-player`, then the bound Take handler sends `seats[JOINED.playerId]={seat:0}`. That variant correctly selects Relay; this is not Hicks's missing initial seat-row hydration or cold handler problem.
- `src/client.ts` Collection.update (connected branch) only queues/sends; Collection.onUpdate updates the map. Client.onSeats then derives local ownership. `src/base-client.ts` emits collection updates only on inbound UPDATE. Thus no origin echo means no local ownership or enabled Setup.
- Vendored `server/game.ts:update()` calls sendAll, INCLUDING the sender. The .NET relay branch instead called only BroadcastToOthersAsync; its “already applied locally” comment was false.
- Correction: after existing relay ApplyUpdate and peer broadcast, send the **accepted `relayApplied` entries** to the originating connection as non-full UPDATE. No optimistic frontend ownership, global echo, or Changsha command echo was introduced. The Changsha dispatch/validation, per-viewer projection, occupied-seat and cross-room fixes are unchanged.
- Origin confirmation is sent directly, not through the Changsha peer privacy filter: filtering its own relay echo would corrupt hand rotations after Setup. Existing peer delivery/projection and no-runtime snapshot initialization remain untouched; this is not a claim of complete relay gameplay acceptance.

## Changed files / exact SHA256
1. `src/backend/src/Mahjong.Autotable.Api/Autotable/AutotableWsEndpoint.cs` — one relay-only SendJsonAsync plus corrected contract comment: `de4152b7d79b5fcbb74018fff63fcc203cf37f05c6162b9141b2f0ed0944a197`.
2. `src/backend/src/Mahjong.Autotable.Api/Autotable/AutotableGameState.cs` — contract documentation only: `e99370fe2a4f5b1093838959405e3a4ba98bf491d4dea2a8151f49794fb66902`.
3. `src/backend/tests/Mahjong.Autotable.Api.Tests/Autotable/AutotableWsRelayTests.cs` — two new theories / 12 cases: `52b8655ae0194c4a2db07227da70c4663194807696b120d236de8b230859c8c5`.

All pre-existing endpoint bytes outside this precise delta were verified by removing our additions/comment replacement and reproducing approved SHA `7543e6a4d6d50102af3bb737f4d7a21cd01c5e38fd2e3838ce282cb5a6d45768`. Approved `AutotableWsSeatAuthorizationTests.cs` remains byte-identical at `a3ad5f00cff2f9bd913011170193457baf881f7b472debaf6da98e3f268381c3`. No frontend, frozen E2E/helper, or Hicks-owned file edits.

## Actual regression proof
- **RED: 12 executed / 12 failed / 0 skipped** against unchanged approved endpoint: each failed waiting for origin UPDATE (OperationCanceledException), not an unrelated setup assertion.
- **GREEN: 46 executed / 46 passed / 0 skipped**: 20 relay-class cases (12 new + 8 existing), 26 unchanged authorization-class cases. New cases cover four_player/four-player/three_player/three-player/bamboo/minefield: lone-origin seat confirmation, bidirectional peer updates, move/leave/tombstone, late-join seat replay, and exact unmodified origin Setup transaction including hand rotation; no runtime binding.
- Existing positive owner/reconnect controls, spectator/wrong-owner rejections, occupied-seat reconnect privacy and cross-room tests all passed. Legacy `Update_SenderDoesNotReceiveOwnMessageBack` remains green because its helper opens **Changsha**, not a relay variant.
- SDK assets were isolated under the evidence directory. Initial --no-restore test exit0 produced NO TRX and was explicitly rejected; isolated build then failed NETSDK1004. Only then restored. Both real runs have verified nonzero TRX counts. Targeted diff --check passed.

Evidence root `session-files/completion-proof/2026-09-09/bishop-relay-fix/`:
- `source-delta-against-approved.patch`: SHA `7a20edeeb8e550f9aa610754bd942577774ec0bcd6b6f8292e86dc21e79c1e9d`.
- `validation-manifest.json`: SHA `f45b8d9c60bbe34f5b5922da4ca7e28a41e12e0bd9fc03b73b0c4af6d8bc5a4d` (source/frontend-contract hashes, all case outcomes, logs and tested DLL hashes).
- `red-results/relay-origin-red.trx`: SHA `2fd0de7b40c08298e7d0620fc35a4fb80631b25af45ddab51065d5c63fe4c6fe`.
- `green-results/relay-origin-authority-green.trx`: SHA `8803d7946c3285678c4b0607af0e2bd682b8d56c749c8e2627971d02bfd12af4`.

Actual GREEN selection: `dotnet test src/backend/tests/Mahjong.Autotable.Api.Tests/Mahjong.Autotable.Api.Tests.csproj --artifacts-path "$OUT/artifacts" --no-restore --filter '(FullyQualifiedName~AutotableWsRelayTests&FullyQualifiedName!~Stability50x)|FullyQualifiedName~AutotableWsSeatAuthorizationTests' --logger 'trx;LogFileName=relay-origin-authority-green.trx' --results-directory "$OUT/green-results" --verbosity quiet -p:UseSharedCompilation=false -nodeReuse:false`, with OUT=absolute evidence root and TMPDIR/TMP/TEMP/DOTNET_CLI_HOME confined below it. No full-suite/provider/stability run.

**Live limitation:** immutable `18209` was not changed and still contains the diagnosed defect. After independent review, coordinator must assemble a new pin; Hudson should repeat genuine variant-picker → Take → confirmed own seat → usable Setup/Deal on desktop/tablet, alongside Hicks's entry/seat-row regression acceptance. In-memory transport success is not a browser completion claim.
