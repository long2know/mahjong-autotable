# Hicks ORIGINAL application entry alignment — implemented, rebuilt once, HOLD for independent review

This is the explicit application-entry grant, NOT freshHicks54dd2e44's independent UI-qualification harness scope. No overlap with that namespace or revision advice from Ferro/original Hudson. Prior approved b23716-source/85-asset bundle, manifests/diff and31+47 source proof are preserved under `entry-creation-03/before/`; no silent retag.

## Concrete change / existing producer seam
Three production entry files only: session-url.ts, lobby.ts, client-ui.ts. Existing New Game/Quick Match/changed-Apply actions mint the same fresh aliases/config and now carry frontend-only `createGame=<exact target alias>` for Changsha. The marker is not sent as a backend parameter. ClientUi invokes existing `BaseClient.new(wsUrl)`, whose actual first WS packet is `{type:"NEW"}`; alias/config remain on the normal WS URL. Ordinary existing links and reconnects remain `{type:"JOIN",gameId}`. Relay remains JOIN/no new marker.

JOINED alone and unbound match-only FULL are not creation confirmation. Same-alias intent survives an unconfirmed retry/reload. Only a connected authoritative FULL for the same alias with runtime turn metadata consumes the marker, preserving all configuration/hash; subsequent reconnect/reload is JOIN. Existing backend HandleNewAsync restores known bindings before JOINED/private projection rather than resetting; retries keep the SAME alias/credentials/config. No inferred legacy mapping, new-room fallback on a failed ordinary JOIN, or credential/seed substitution. Room-level rejection is surfaced through existing entry status/retry UI.

Bishop's existing producer code and `sessions/2026-09-14-bishop-public-room-recovery.md` provide the exact NEW/JOINED/full/binding semantics. He was asked to confirm the same-alias lost-ack/idempotency edge; no new producer field/API or backend edit was invented. His additional real legacy-owner/explicit-NEW adverse coverage is not falsely included in frontend unit proof.

## Actual focused checks
New `entry-creation.contract.spec.ts` uses the existing runner and executes actual eager header/QuickMatch/Apply callbacks, ClientUi and BaseClient, with Node DOM/socket doubles. No browser or live server.
- Immutable b237 source negative control:9FAIL/5PASS/0skip.
- Current source:14/14PASS, same test bytes. Combined with unchanged31claim+47existing entry/R2/default source controls:92PASS/0FAIL/0SKIP/0flaky.
- Strict production and control TypeScript: exit0.
- Scoped lint: preexisting ClientUi1error/1warning,0introduced; new test/lobby/session helper clean. No whole-repo all-clean claim.
- Normal `npm run build` performed ONCE, exit0,85 generated files. All other application UI/action/renderer source bytes and every existing helper/spec remain unchanged. Fourteen original entry/cold/reconnect/owned-seat methods also match exactly.

## Exact delta SHA256
Under `src/frontend/autotable-src/`:
```text
src/client-ui.ts 88e03e6ced5c02a6e66e2f3fa1636c7e1967418e5f2941919faf71b41b8dc874
src/lobby.ts 15e56749ed72defea98479aa7b6488a8a57c9ee0b36fe4ad460772a68d26fadd
src/session-url.ts ad98fe5db95ee9e246cdad53765700f0d21ba668614385e3933ed45180ee24a3
tests/e2e/entry-creation.contract.spec.ts 588e6e98a8b6adbc9e90ba5548b735f9fbb474ad1ae6e2ff3c6fbe5e594e695e
```

## Complete new review input
Evidence namespace: `session-files/qualification/2026-09-12/hicks-actions/entry-creation-03/`.
- `implementation-results.json` SHA256 **9b244d4d32d1f49d67150f41fbd5b02e356248813360666c19a2d5c1da8fb286**: all139 application source/config inputs, current16-feature source inventory, all85 generated assets, source proof/producer contract/limits.
- `entry-creation.patch` SHA256 **5099d07dd151ea75a6c91170f29ad496ae80c1e3128878745c55c13814cdf1af**: three-file application delta plus new focused controls against b237.
- `artifact-hashes.json` SHA256 **62f62d7bc9b9213b6f41e02a3e338bad66bcfaeaae9b00776ba854f58ca2650e**.
- Entry `autotable-src.69dad4da.js` SHA256 **82730a85afa8e1cf73406a7dee6a17d83847e40d7433818b8e046c75b086ca59**.
- Client chunk `client-ui.09b723e9.js` SHA256 **4a875655078759e044f73435beea1c7540cc8140997286d17e18a23f620555a6**.
- Generated index SHA256 **e1635961465d992c0521943ff01be2edd5e92920a3ca07a4b3775ae617c9b5bd**.

No backend edit, image publication, oldC03/browser/live access, Git/keys/container/dependency/model change or qualification credit. Source/protocol doubles do not establish signed-owner DB idempotency or live play. Independent source review plus coordinator-directed new-candidate integration remain required. **HOLD these new application source/bundle bytes. Do not update UI-harness consumer eligibility until coordinator approves this revision; do not rebuild on queued peer ACKs.**
