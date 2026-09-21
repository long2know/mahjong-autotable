# Hudson FINAL current native targeted consolidation

**CURRENT RESULT: RED —65 executed /63PASS /2FAIL /0SKIP /0 retry, from ONE coherent current source/test capture and ONE test invocation.** No historical run contributes to this count. The policy and stock-bot expectation are settled; the actual remaining execution blocker is the missing SignalR **WinDeclared** notification on successful discard-Hu and rob-Kong-Hu claims.

## One immutable exact test-diff/source/result handoff

`session-files/qualification/2026-09-12/hudson-actions/native-targeted-final-02/final-manifest.json`

- Manifest SHA256: **`ba45270bcf076221013dc9218bcac31b95ee68546971748f91e7e6184b416afe`**.
- Actual TRX: `results/native-targeted-final.trx`, SHA256 **`b49316c8fbcac25d6994c3f114eb5cbfe8b6623682902f142a89b77e268556d4`**.
- Complete2017-file seal: `evidence.sha256`, SHA256 **`b90d77283570cf90348a513fbf28f7a22028a2100ce835b07b880aa5392ee47b`**, all entries matched.
- Source capture: `source-capture.json`, SHA256 **`9418f8e675ea4f35042a9231419595ac2546c043b21a9cbd3ea9d25e43436287`**; all1134 file hashes/sizes and the complete inventory remained unchanged.
- Loaded API DLL: **`c3515198b59e6d7daca6f001ce5694988bbf9d627eafd03f248ef3bc7aff5061`**.
- Test DLL: **`36064bfaf797fd85ef48b7cb5affee1e4924a7bebc2dea561a29c65b97cf6269`**.

Exact full command is `results/test.command.txt`; executed **September14,2026,21:27:55-21:36:50UTC**, using the mirrored existing test project, Release/net10.0, one MSBuild node, unchanged serial xUnit configuration and retries0. `selection.json` records every selector. `case-results.json` records each actual case/outcome/error/seed observation; `current-observations.json` retains meaningful per-case traces. This is the authorized native slice, not a full backend suite or image qualification.

## Exact current source cut, not an old checkpoint

| Input | Captured SHA256 |
| --- | --- |
| Runtime | `9d180a27b57841da39f27da0ed2e54bbaa8bffb1be94f20882fef2b3c6a1d411` |
| Public-room partial | `5ec41a5b8f5f67862c5d96a02e7eef650914662742b3b430ba241646a9dfef1d` |
| Engine | `4070693bb631a491bf54ba49119d3665b34f1f215584255b24f2e6a43674e12b` |
| Hub | `bb293ac65e051a336fb8809cc5b34669eedc8390c06606831bb9f0e0b5eda256` |
| Availability adapter | `eee17c46c41b76725eeee565a8ec1b947c4bef5ebf234bbf318a42bf9c5e10ff` |
| WS endpoint | `752c673ad62b3aa09193d0bd94b9236622e3352a2a49d5b01d8405283c08697f` |
| Replay recording input | `6372a90fb5036d165278f0eb4ff4c6cd31a8d69c712647482494ef332224844a` |

The first attempted capture detected an in-flight replay-source write and stopped after97 files, before any build/test execution. It remains sealed as an incomplete0-test capture under `native-targeted-final-01`; it was not repaired into a mixed tree. The second complete capture was stable against the workspace before execution. After execution, all captured bytes remained identical, the MVC content-root pointed inside that capture and the API beside the test assembly matched its project build. A later workspace AppDbContext change is recorded separately and never substituted. Physical filesystem write protection is not claimed. No owned runtime DB/WAL/SHM files remain.

## Actual current matrix

| Selected scope | Executed | PASS | FAIL |
| --- | ---: | ---: | ---: |
| Stock default-Medium Pung/Chow full10s continuation | 2 | 2 | 0 |
| Deliberately faulty own-Hu proposal fallback/recording | 1 | 1 | 0 |
| All-tier post-claim own turns and genuine draws | 12 | 12 | 0 |
| Actual WS post-Pung Kong + nonvacuous fake-Hu negative | 1 | 1 | 0 |
| Pung/Chow advertised/dedicated-runtime Kong parity | 3 | 3 | 0 |
| Strict claim context, invalid choice/retry, old-window callbacks | 17 | 17 | 0 |
| Whole-command own-Hu overflow | 2 | 2 | 0 |
| Discard/rob-Hu winner/payer overflow + positive settlements | 6 | 4 | 2 |
| Scored hand2 exact-runtime restart and ownership/privacy | 1 | 1 | 0 |
| Independently revised broken-binding/legacy/helper controls | 9 | 9 | 0 |
| First-outsider/default-fill/same-alias mode/ordinal isolation | 6 | 6 | 0 |
| Legacy-Hub actual caller and2/3-argument compatibility | 5 | 5 | 0 |
| **TOTAL** | **65** | **63** | **2** |

The four discard/rob-Hu overflow negatives pass exact rejection/resync, complete live/persisted state/version, empty pending queue, same uncancelled claim window/timer, unchanged scores and genuine same-socket Pass or unchanged timeout continuation with inventory108. The stock rows reach actual autonomous held discard and next-human real draw within their original10-second budget. The separate injected strategy case reaches the recorded deterministic replacement decision and actual progress, not just a proposal check.

## Actual remaining blocker: claim-Hu WinDeclared omission

Both `CurrentClaimHu_SettlesNormallyAndTransfersTheWinningTileExactlyOnce` rows fail at unchanged **HudsonClaimSettlementOverflowIntegrationTests.cs:189**, test SHA **`5c247a633c4f140ecd1cd391f715c842389d85a307c231172582aa6eb7b12210`**. Real versioned discard/rob-Kong Hu reaches authoritative GameComplete and passes the preceding exact winner/source/method/tile-transfer and2/7 payment assertions. The connected/fenced SignalR observer receives **ScoringComplete, GameEnded, GameCompleted**, but no **WinDeclared**.

Captured runtime9d180a27 `ResolveClaimWindowAsync:1808-1825` applies the accepted Hu, emits ClaimMade, scores and emits ScoringComplete without calling the existing WinDeclared emitter; its sole explicit caller is the own-self-draw path at1361. The retained `changsha-signalr-contract.md` describes the WinDeclared payload and win-before-scoring lifecycle. This is a current product notification omission, not policy-pending, incorrect overflow atomicity, a new bot stall, room-helper setup failure or disconnected observer.

The two positive rows' later final inventory/persisted-terminal-state assertions were **not reached** and are not claimed as passed. The four genuine overflow cases do reach their own full continuation/inventory/persistence suffixes. No assertion, timeout, retry or skip was changed to conceal the positive failures. The already-required different-author production revision process remains necessary; Drake is the proposed independent revision agent only subject to coordinator eligibility/assignment. Hudson makes no production correction or source approval.

## Exact lineage, ownership and review limits

- `provenance/stock-one-assertion.diff` SHA **6153caf080da90616fbf5d2459c0716281a81df5a8ae9956837f33e438745f54** identifies the only authorized117 expectation change. Current7c157 test bytes reconstruct original10f9 exactly by reversing that unique expression. Every real setup/10s/continuation/inventory/Pass-Hu/score/log byte remains. Both RED generations are retained.
- `provenance/post-claim-policy-only.diff` SHA **eddd7a1d3941efebd1d6f3a609ea950935caeb0e4e812d050eb46f9ca2df912b** and the independent review bind b3aa's single adopted-policy WS revision. No05:59 policy-pending claim or new frozen rebaseline occurs.
- Frozen room helper **5ec392d0** is Drake's independent revision, not Hudson self-repair. Its nine cases pass in this actual current invocation. Copied handoff/manifest/helper patch preserve author identity and the supplied **Ripley exact-version re-review/HOLD** status. Execution is not original-author approval, and no revision/advice was provided.
- Full frozen test-byte diffs are indexed by `test-diff-index.json`; they are not claims of edits made during this consolidation. No test/helper or production source was edited here.
- All-tier evidence keeps its qualifications: controlled genuine meld selection versus autonomous own turn, common92 not independently discriminating cross-strategy dispatch, gain4/version8 observations versus broader assertions, runtime-warning/error-only diagnostics. The deliberately faulty strategy is separate.

Canonical13, the five old self-draw repairs and three promotion repairs remain settled and were neither reworked nor selected. UIr6, protocolv7, shared3.7 and active frontend-creation work remain separate. No Docker wait, frontend rebuild, primary host/key/database, live migration, Git/index or qualification-row operation occurred. Earlier269/169/98/75 and separate native passes are not aggregated into65. Source/helper review and image/live/cohort admission remain distinct gates. **Qualification0/120.**


## Later delivered owned-scope overlay remains historical; current implementation witness is here

Vasquez's integration receipt `review-receipts-2026-09-14/hudson-owned-feature-fixture-01.json` matches6bf1bb137f22bfdbc5d35c482bf7ae57a8384276eb2e2695dd01cc2cf5bd388f and concerns the older114130e2 owned-scope index, not this later coherent execution. Its exact review overlays remain valid only for their named test/fixture scopes; no broader BaseUnit/claim/recovery or source approval is inferred.

The normal-WS post-Pung implementation positive is **actually executed and PASS in this newer65-case run**: raw output has effective14/structuralWin=true/ownDraw=null, unchanged fake-Hu rejection, advertised quartet40/41/42/43 withhu=false atv3, and normal WS Kong/back104/v5/inventory108. This is a new implementation witness at the captured9d180a27/4070 cut, not a relabel of the older metadataNULL RED. No additional replay occurred for this receipt. Current overall remains63/65, with only the two positive claim-Hu WinDeclared notifications failing. The authoritative current manifest isba45270b above; the older index and all review/evidence boundaries remain untouched.


## Delivered Vasquez v2 admission and zero-test replay blocker kept separate

Verified `vasquez/final-coherent-rules-2026-09-14/outcome.json` SHA066450317d07d89a5b66c75ba3426c93d7cdce5b872f701036adc7f8cd7d4f88. It records genuine269/269 owner evidence at the older14b/0c8/c3d v2 cut and a separate Vasquez replay attempt stopped by source drift **before restore, with0 tests and no new TRX**. That attempt is an input-integrity blocker, not a rules failure or passing replay. Neither the269 evidence nor the0-test attempt is merged with this later65-case9d180a27/5ec41a5b/4070 execution.

The later actual coherent source/test capture and complete WS post-Pung/stock continuation witnesses are already published here at manifestba45270b. Current native overall remains63/65 with the two WinDeclared notification failures; it is not a source/image approval or an automatic grant for another reviewer's final replay. The reported profile document f0a6fbbe remains the review-record version; current on-disk profile hash read back asaf68ddd9a722622d6bbd1944966698f94d8396b3249afcdfb10db38ccedcef63 and was not restored, edited or silently substituted. No new test or code activity, approval transfer or qualification credit followed this delivery.


## Later explicit cd42 grant: new67-case execution, not a relabel of this older65-case packet

The coordinator subsequently selected the frozen `drake-recovery-revision/authorized-v2-base/final-manifest.json` **cd42c12c119571c1782a0d25a27d69896634f4c37023d4ebffb75002da993cd8** for isolated native QA. Hudson reconstructed all349 production inventory bytes and all55 protected pins from matching retained source, then ran a separate67-case invocation on14b12539/948237a6/752c673a/0c8: **63PASS/4FAIL/0SKIP**, September15,00:37:59.596-00:43:25.143UTC. The original65-case9d/4070 result above remains unchanged and contributes no count to67.

Fresh final: `sessions/2026-09-15-hudson-authorized-native-cd42.md`; manifest `hudson-actions/native-authorized-cd42-02/final-manifest.json` **9204fa5b29411abd2589df214f1cff48796e0eb341f4f604525998ec04cbc9e6**, actual TRX **d13f5c5c16af1b46869b0552f3ca88528be7a6bc6f01a86a9dfac7b02d39e81d**. Product WinDeclared omissions reproduce2/2. NEW lost-confirmation probe ed8942e4 has2 fixture failures at321 (nonexistent turn.current.gameId) before retry/no-redeal/continued-discard suffix; Hudson rejects/holds that new helper cycle and is locked out of its next revision/advice/approval. Different coordinator-granted owner required, Drake proposed. No production wire change requested. Other selected existing controls pass, including four overflow continuations, real stock2, faulty strategy1, frozen independent binding9, healthy isolation6 and legacy-Hub5. Existing helper5ec review/HOLD remains separate. No browser/image/source approval or qualification credit.
