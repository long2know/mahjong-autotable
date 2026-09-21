# Hudson: authorized non-frozen draw-provenance fixture repair

**FINAL scoped execution: 5 executed / 5 PASS / 0 FAIL / 0 SKIP / 0 retry.** Actual grouped run: September 14, 2026, 05:26:56-05:27:53 UTC. This fulfills Burke's narrowly authorized fixture request; it is not engine work, source approval, live qualification, or a claim that the combined 424-case suite is green.

## Changes limited to the two permitted files

1. `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/Acceptance/SpecialContextWinsTests.cs`: only `HeavenlyHand_NonDealerSelfDraw_DoesNotQualify_EvenOnFirstAction` changes. Conserved physical-tile swaps prepare the same hand as 13 held tiles plus its exact wall completion tile. Actual `DrawTile` produces tile 14 and its event/provenance. The negative still has dealer 0, winner 1, `TurnNumber == 1`, and no discard; it has not been weakened into a later-turn negative. Every byte outside this method and both original expected assertions remain unchanged.
2. `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/Scoring/ScoringOptionsCharacterizationTests.cs`: only fixture helpers change. The Standard and AllPungs+FullFlush setups now preserve the real dealt inventory, place 13 held tiles and the exact wall completion tile through swaps, perform an actual dealer discard/all-pass transition, and draw for seat 1 before declaring/scoring. No fabricated `LastDrawSeatIndex` assignment. All four complete original test bodies and all payment/fan/stacking/BasePoints/zero-sum expectations are byte-identical. Existing SpecPure/HouseRules characterizations remain unit-only; no production preset/config changes.

Both setups assert the real 13-to-14 draw and conservation of all 108 unique physical IDs. Original file copies, resulting frozen copies, diffs, and byte-scope checks are retained.

## Exact current fixture hashes

| File | SHA256 |
| --- | --- |
| `SpecialContextWinsTests.cs` | `d410d639a4cec61e74e14dee5649da6ca0c07dd96d056f779e0d0392131c8d16` |
| `ScoringOptionsCharacterizationTests.cs` | `d92a09bc9ee757b143b50fef82cde2bf3907327fa2f91f5a72d6c80df096d3aa` |

Original hashes were respectively `0a581c51752cbb60756cfabc43d4627b6c212eb930f60a643d70927b71b1264e` and `c984ab7d6fc790ab617ad478ea0ab0135d1d96b2342defe76a1aa81313298203`.

## Execution and sealed evidence

Evidence root: `session-files/qualification/2026-09-12/hudson-actions/nonfrozen-fixture-repair-01/`.

- `handoff.json` SHA256 `31b5bf0024ce1a3591d0f05235edfff36e300fd7223fb8444e8b5be018cb21ef`.
- Separate **855-file** seal `evidence.sha256` SHA256 `6d30c1e509bef70c4ca44f07902651b713e28ead36079ff87b2f31c59cea831d`; all entries matched.
- Actual five-case TRX `results/nonfrozen-fixture-repair.trx` SHA256 `89edf83435b9d32ded20585dfe360ac7c42cedefe66784afbb23d2cdc14b9bd5`.
- Full case names/times/outcomes in `trx-summary.json` SHA256 `967b5e030a949088999ec57bc4f101e94d5411adf295aa58c2c334800f90ddcd`.
- Exact command in `command.txt`: existing test project, Release, isolated fresh `--artifacts-path`, `--no-restore`, `-m:1`, single filter `FullyQualifiedName~SpecialContextWinsTests.HeavenlyHand_NonDealerSelfDraw_DoesNotQualify_EvenOnFirstAction|FullyQualifiedName~ScoringOptionsCharacterizationTests`.
- **998 source/config/test inputs stable START/END**; source manifest SHA256 `d3cfea84df5b9922121e80e892b961fdffcede1a7b59b1873fafd415677c570b`.
- A fresh-path no-restore build first established NETSDK1004; only then were existing manifests restored into this new artifact directory. No dependency/tool installation or manifest change.

## Boundaries retained

The frozen sixth case `CanonicalRuleQualificationTests.CurrentClaimChannel_OmitsOtherwiseLegalOwnTurnActions(self-draw)` was not edited or executed; disposition remains with coordinator/auditor. All three frozen audit hashes remain unchanged. No production, browser/live8950, Docker, Git/index, or locked UI-counting-harness work occurred; no skips/timeouts/retries or expected predicates were weakened.

The earlier 858-file evidence seal still matches. The separate 76-case WS/BaseUnit run and its two overflow-disconnect failures remain immutable and were not rerun or relabeled by this five-case result. **Zero completed-match credit and zero qualification SQL rows touched.**
