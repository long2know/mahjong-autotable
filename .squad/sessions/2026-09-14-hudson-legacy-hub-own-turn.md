# Hudson: NEW legacy-Hub own-turn caller/arity controls

**FINAL:5 executed /5PASS /0FAIL /0SKIP /0retry.** This closes the specifically tested legacy-Hub caller/argument-compatibility slice with real owner-positive controls; it is not a production-source review or image/cohort admission. No existing test/helper, production source or rejected fault-injection artifact was revised or executed. Qualification remains **0/120**.

## Concrete handoff

`session-files/qualification/2026-09-12/hudson-actions/legacy-hub-own-turn-01/final-manifest.json`

- Manifest SHA256: **`32a957a8314ec849ac41a0daa7aa971af8fbf845ad3792b9357c22af633505ad`**.
- Actual TRX: `results/legacy-hub-own-turn.trx`, SHA256 **`34f15c1518e973146578d7cc19a9e33f0558cc01a90019706a19acc6133c805f`**.
- Complete1984-file seal: `evidence.sha256`, SHA256 **`6302d9ea24eb92462f6d56f24645f1a670dff8741867c4a6656769821175a080`**, all entries matched.
- NEW source: `src/backend/tests/Mahjong.Autotable.Api.Tests/RulesQualification/HudsonLegacyHubOwnTurnIntegrationTests.cs`, SHA256 **`853aed9daadc40762b2a754153c50f2dfd0101938a8aa997bb7fc39b69d7761f`**.
- Full addition diff: `new-test.diff`; all case output/commands/source/DLL identities and qualifications are indexed by the manifest.

One grouped command ran **September14,2026,20:44:03-20:44:53UTC**, including compilation, with the captured existing project, Release/net10.0, one MSBuild node, unchanged serial xUnit configuration and filter `FullyQualifiedName~HudsonLegacyHubOwnTurnIntegrationTests`. The exact command is `results/test.command.txt`. Restore occurred only after the recorded NETSDK1004 missing-assets failure. No retry or test-only runtime-delay change.

## Exact source/artifact cut

| Input | SHA256 |
| --- | --- |
| Hub | `bb293ac65e051a336fb8809cc5b34669eedc8390c06606831bb9f0e0b5eda256` |
| Runtime | `d594cd359dc835a8da84acb77221aa79924815f697a9403cada40acb5784d0a4` |
| Engine | `4070693bb631a491bf54ba49119d3665b34f1f215584255b24f2e6a43674e12b` |
| Availability | `eee17c46c41b76725eeee565a8ec1b947c4bef5ebf234bbf318a42bf9c5e10ff` |
| WS endpoint | `752c673ad62b3aa09193d0bd94b9236622e3352a2a49d5b01d8405283c08697f` |
| Loaded API DLL | `2332543b4e3308dbd5b9c80b215565f400a946c1b07ef20dbf39736aa7c5ce1f` |
| Stable shared fixture | `39531682d5967dce8014c560d95cc52b637f47e74134ac18313055fe036aa4ca` |

All1132 captured input files and the complete inventory remained unchanged. MVC content-root metadata points inside the captured API project, and the API dependency beside the test assembly matches that project build. No owned runtime database files remain. Filesystem write protection and prior0c8/runtime approval transfer are not claimed.

## Five complete rows

A real signed WS user owns seat0; a different signed WS user actually owns seat1. An additional signed Hub spectator has no seat. The unchanged conserved shared fixture establishes a real runtime draw and a legal active-seat0 action. Both wrong callers invoke the exact same RPC and arguments as the subsequent valid owner.

Every row first proves that the other seated owner and the spectator each receive a HubException, preserve the complete serialized live state/version/events and remain on a usable Hub connection that accepts another JoinTable call. Then the actual owner succeeds, so missing method/arity cannot masquerade as the negative control.

| Row | Valid-owner completion |
| --- | --- |
| Hu14 | Real13->14 front draw0; `DeclareWin(gameId,seatIndex)` wins for0, positive zero-sum score, cap1 GameComplete/v6, no extra draw, inventory108. |
| Hu11 | Real10->11 front draw20 after an existing meld; same full Hu/score/completion invariants. |
| Hu8 | Real7->8 front draw20 after two melds; same full Hu/score/completion invariants. |
| Concealed Kong | `DeclareKong(gameId,seatIndex,tileIds)` with one held representative16 selects the real16/17/18/19 quartet; one back replacement107/v4, correct concealed count, unchanged front index/scores, inventory108. |
| Added Kong | Same three-argument RPC with held fourth19 promotes the existing Pung to exact16/17/18/19; one back replacement107/v4, unchanged front index/scores, inventory108. |

This is5 cases containing10 actor-negative invocations and5 owner-positive invocations, not15 separate test cases. Each RPC has the existing argument count:2 for DeclareWin and3 for DeclareKong. No actor identity parameter was added by the test.

## Boundaries preserved

The setup is directed/conserved and the draws/declarations execute through the real runtime/Hub. No custom bot strategy, draw-marker forgery, binding injection or room-fault callback is used. This is live-state actor atomicity, not persisted-snapshot error atomicity or exact textual Hub-error-message coverage. Positive declarations begin after genuine own draws; the separately held post-Pung/Chow/pre-draw and legacy-claim-channel proofs are not replayed or implicitly expanded.

Independent exact-version review of the NEW fixture and intended source cut remains required. The earlier source/fixture/image/cohort holds and the unrelated rejected fault-helper cycle are unchanged. The five results are not added to98/169/75/6-case records from other cuts. No source/old-test edit, frontend build, Docker/live/primary8950 action, Git/index operation or qualification credit occurred.
