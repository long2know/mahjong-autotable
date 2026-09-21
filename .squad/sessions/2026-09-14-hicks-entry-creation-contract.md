# Hicks ORIGINAL application entry alignment — concrete existing NEW seam

Narrow coordinator grant only; this is application code, NOT freshHicks54dd's UI qualification-harness namespace. Current b23716-source/85-asset inputs and prior31+47 proof/manifests were preserved under `session-files/qualification/2026-09-12/hicks-actions/entry-creation-03/before/` before editing.

## Implemented application contract
- Fresh Changsha New Game/Quick Match/changed Apply links carry frontend-only `createGame=<exact minted gameId>`. The intent is bound to the target alias, not a guessed runtime identity. The query marker is NOT forwarded to the WS endpoint.
- ClientUi consumes this declared intent using the EXISTING `BaseClient.new(wsUrl)` seam: first actual packet is `{type:"NEW"}`, with the alias and variant/deal/bots/difficulty/seed/handCount/BaseUnit on the normal WS URL. Backend HandleNewAsync honors that query gameId and sets ExplicitNewRoomId; response remains JOINED plus FULL snapshots.
- Unmarked ordinary links and rejoin/reconnect stay JOIN. Relay remains JOIN/no new marker. An intent for another alias does not become NEW. No automatic NEW fallback on legacy recovery failure and no alias/credential substitution.
- JOINED or an unbound match-only FULL is NOT creation confirmation. Intent remains on URL across lost-ack retry/reload, always targeting the SAME alias/config. Only a connected authoritative FULL for that alias with runtime turn metadata consumes the marker via replaceState. Subsequent retry/reload uses ordinary JOIN.
- The actual producer sends full runtime snapshots through SendFullSnapshotAsync on creation/state changes. Its documented NEW-to-known-binding path restores existing runtime/config/progress rather than resetting. Bishop was asked for confirmation/targeted idempotency proof for the same-alias lost-ack edge; no producer change is assumed or made by Hicks.
- Room-level actionRejected errors are surfaced through existing ClientUi status/retry UI, preserving fail-closed recovery.

## Current checks (pre-build)
Three production files only: session-url.ts, lobby.ts, client-ui.ts. New existing-runner entry-creation.contract.spec.ts executes actual eager header/QuickMatch/Apply callbacks, ClientUi and BaseClient with Node-only DOM/socket doubles; it is NOT live/backend recovery qualification.
- Preserved b237 source:9 expected discriminating FAIL/5 PASS/0skip.
- New source:14/14 PASS; combined with unchanged31claim+47entry/R2/default controls:92PASS/0FAIL/0skip.
- Strict production/control typechecks exit0.
- Scoped lint retains exactly the preexisting ClientUi1error/1warning;0introduced. Other application UI/action/renderer bytes and every existing helper/spec remain unchanged.

Single normal npm regeneration and final complete source/bundle hashes will follow the coherent seam handoff; no oldC03 browser, live image, Git, key or container operation. No review/qualification approval is claimed.
