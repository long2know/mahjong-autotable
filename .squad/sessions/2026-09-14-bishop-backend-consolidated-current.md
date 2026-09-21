# Bishop - one completed consolidated current-input run, HOLD with explicit failures/drift

**One exact captured production/schema/test input set was compiled and executed:511 cases,506PASS/5FAIL/0skip. This is NOT a clean final gate or a seal of the later working tree.** No historical52/75/4/95/269 counts were aggregated, no failed case was excluded/retried, and no fixture or timeout was changed. The complete result is ready for independent failure/source review, not release approval.

## Authoritative artifact
Root: `session-files/qualification/2026-09-12/bishop-actions/backend-consolidated-current-01/`.
- **consolidation-handoff.json SHA4b48fa1a277e9647c2fc6d6dc72792f35da9a10217fc1d476f4ae49de5c6ef8a**: full production/schema/test hashes, actual per-case/class outcomes, binaries, provenance, later drift and remaining gaps.
- **input-manifest.json SHA0da65c293f6a0dffd792ade2adaf7468af32344893d91f4315ff6c28666e7160**:1045 exact inputs;364 production/resource files,680 test/resource files,127 explicitly indexed schema inputs. `source/` retains the actual bytes without substitutions.
- **results/backend-consolidated-current.trx SHAbbda3f22534d536e5bd93647be9bfae876e733e6bb3593ca5d5b7fa490e2dd49**. Actual TRX run September14 19:44:37-20:42:53 UTC;511EXECUTED/506PASS/5FAIL/0skip/0abort. Raw log and structured actual-results.json retained.
- Executed API DLL SHA f62555ac11f7ed0a29f7998cb294a38f952e9c1d30b441b0fd2318ac5ef9d8b4; identical API copy beside tests. Tests DLL4da973b9179de0e92888b8b61eeaba6afd6f9adac838a151ae9dd4225a513293. Matching PDB/dependency hashes are recorded.

The existing project/runner was used on a byte-identical retained current-input snapshot so active peer work could not enter the run midway. Build/obj/temp/results/CLI home were isolated. Actual NETSDK1004 preceded restore; nonzero TRX counters were parsed. SDK10.0.100/VSTest18.0.1. All1045 captured inputs remained unchanged through execution. No working production source, held frontend, package, domain predicate or fixture was edited by this consolidation; no live8950/Docker/AppArmor operation occurred.

## Exact executed source, not the old held scalar labels
- Endpoint752c673ad62b3aa09193d0bd94b9236622e3352a2a49d5b01d8405283c08697f.
- Own-turn adaptereee17c46c41b76725eeee565a8ec1b947c4bef5ebf234bbf318a42bf9c5e10ff.
- Main runtimed594cd359dc835a8da84acb77221aa79924815f697a9403cada40acb5784d0a4.
- Public-room recovery46fd8aea30ac8969053680b30cee29928efb69c2a55672623da55c1a37318a09.
- State machine4070693bb631a491bf54ba49119d3665b34f1f215584255b24f2e6a43674e12b, NOT0c8b838. This includes the separately granted replay instrumentation (grant-context18:28:41 UTC after independent design approval); it is not falsely represented as unchanged/approved0c8 source or as a Bishop consolidation edit.
- Adjudicatorff55d352, scorer6c794935, BaseUnit helpere135ce5a remain byte-matched. Current SlotMap79f4afd8/Translatore6f59543 are separate geometry-lane inputs, not claimed authored or independently approved here.

Source-delta.json lists all differences or files absent from the earlier349-file inventory; absent baseline hashes do not necessarily mean a new repository file. Full exact current schema/model/migration inputs are included. The Gorman46fd recovery revision and replay/schema/geometry lanes retain their own review requirements.

## Five actual failures - no premature root-cause claim
1. AuthenticatedSelfHu_UsesActualDrawAndOwnTurnRoute: seat0/hu-11, seat2/hu-14, seat3/hu-14. Each fails with OperationCanceledException at the existing WS BarrierAsync receive after the legal ownTurn Hu request. Do not infer that all self-Hu cases failed or attribute these to a specific new source change without triage.
2. CurrentClaimHu_SettlesNormallyAndTransfersTheWinningTileExactlyOnce: discard-Hu and rob-Hu positives. Each fails because the captured SignalR events include ScoringComplete/GameEnded/GameCompleted but NOT WinDeclared. These two are NOT OperationCanceledException failures and do NOT prove scoring never occurred.

The full assertions and error detail remain in the unchanged fixture/actual TRX. Root cause is unadjudicated; no assertion weakening, timeout increase or success-shaped fallback was used.

## What this one run actually covers
All17 claim-context/invalid-choice/callback cases,4 compound claim-overflow preservation/continuation rows,31 BaseUnit cases,10 original runtime partials, separate F04,3 Kong parity rows and the policy WS positive/fake-Hu case,2 real stock-Medium continuation rows,12 scoped all-tier controls and the injected faulty-proposal case passed. Recovery included95 Drake+77 Frost+27 Gorman cases, the unchanged progressed restart/privacy case,9 binding-boundary cases and6 isolation/reservation/bot-fill/relay cases. Existing26 seat-authorization and20 relay cases passed. These counts are subsets of THIS511-case TRX, not historical aggregates; per-class details are in the manifest.

## Remaining gaps / HOLD
- Five actual failures require owner/reviewer triage before a clean acceptance claim.
- During the frozen run, FOUR working production files and TWO replay test files changed, and ReplayV3ReachableCases.json plus NEW HudsonLegacyHubOwnTurnIntegrationTests.cs were added. after-inputs.json and post-run-working-inventory.json contain exact old/new hashes. The frozen1045-input run is coherent, but is NOT validation of those later bytes or the newly added legacy-hub cases.
- The game-boundary selection does not replace independent replay instrumentation/conformance, schema-widening or geometry review; it does not validate live/provider migrations.
- The binding-boundary fixture proves original-cookie/new-connection normal NEW after legacy rejection on the rejected alias, preserving the old runtime and creating a distinct new one. It does not establish the real UI fresh-alias click path or a full already-committed-target lost-ack NEW/reload sequence. Those exact gaps and final browser reachability remain as specified in the legacy-entry handoff.
- Original Hicks's entry alignment remains a separately reviewed source/bundle input; b237 JOIN-only baseline is not silently made compatible. No current compatible candidate, source approval, image approval, container workaround, browser run or qualification credit is claimed. Counted games remain0.

**HOLD the captured cut and its actual RED result for independent review.** Do not overwrite old artifacts, transfer506 passes to later source, or present this as a green current release freeze.
