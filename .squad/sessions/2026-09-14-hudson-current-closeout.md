# Hudson current backend feature/fixture closeout

**Fresh result: 169 executed / 169 PASS / 0 FAIL / 0 SKIP / 0 retry, from one immutable current source capture.** No historical counts are aggregated into this run. No selected production fix remains failing in these 169 cases.

## One concrete current manifest

`session-files/qualification/2026-09-12/hudson-actions/current-closeout-01/final-manifest.json`

- Manifest SHA256: **`154dc81c3565c884e6030b386cc1ed37dbfbd7ef46966bd1f7d8da3f837ca618`**.
- Actual TRX: `results/current-closeout.trx`, SHA256 **`a46297c16f143ffd21ab111f0d5f614234d2f4672eb5ba7efaee6e5da50b5c93`**.
- Source capture: `source-capture.json`, SHA256 **`3dddb21c0ac833e0e7ca854b5a70f8af73662730561db042d95266887f7e7379`**.
- Full **1975-file** evidence seal: `evidence.sha256`, SHA256 **`2dbee805f8701643bf4f0d135861b09206322d80690f45412f07918d3390a415`**, all entries matched.

The source snapshot was copied and verified against the workspace at **2026-09-14 14:29:08-14:29:16 UTC**: 1108 files, 15,866,870 bytes. It is a byte-identical, read-only copy of backend source/tests and current static content, excluding generated output/runtime databases. The actual fresh grouped command ran **14:35:45-14:41:43 UTC**. Every captured byte and the complete snapshot inventory remained unchanged afterward. The generated MVC test-host manifest points to the captured API content root, and the API DLL beside the executing test assembly matches the captured project build. A later live-workspace runtime edit did not enter this run.

Tested core pin: engine **`0c8b838b71746d4e772c651bd278a7d883b650871849f71c6cbfbe780286636f`**; runtime **`641e75140200726f50738b2e5ffd9d07c5b2c5e9afc4f9d37dd1d9dfd07fead3`**; endpoint **`752c673ad62b3aa09193d0bd94b9236622e3352a2a49d5b01d8405283c08697f`**; aligned availability **`eee17c46c41b76725eeee565a8ec1b947c4bef5ebf234bbf318a42bf9c5e10ff`**. The earlier published5491877/35-case owner checkpoint was not substituted for this current capture.

## Fresh selected outcomes

| Scope | Current passing cases |
| --- | ---: |
| Own-turn WS, including legal post-Pung Kong and fake self-Hu negative | 45 |
| BaseUnit and whole-command overflow | 31 |
| Strict claim context, invalid choice/retry and old-window callbacks | 17 |
| Actual stock-Medium Pung/Chow autonomous continuation | 2 |
| All-tier post-meld own turns and genuine-draw wins | 12 |
| Deliberately faulty proposal fallback/recording | 1 |
| Public alias same-room restart recovery and ownership/privacy | 1 |
| Current-profile post-claim Kong parity | 3 |
| Zero-wall and offered-action boundaries | 8 |
| Deferred invalid Chow and runtime added-Kong all-pass continuation | 2 |
| Natural shared-Kong effective-hand controls | 26 |
| Five authorized old self-draw fixture repairs | 5 |
| Three legacy-claim branches plus ten original defect rows | 13 |
| Three authorized malformed PungPromotion fixture repairs | 3 |
| **Total** | **169** |

The formerly blocked normal WS Kong case now advertises `[40,41,42,43]` with `hu=false`, rejects structural self-Hu without a draw, executes the real post-Pung Kong, draws back tile104, advances v3->v5 and conserves108. Recovery preserves hand2/turn6/v82/wall51/BaseUnit7/scores[0,0,-42,42] across fresh hosts and the exact original signed credential, rebinds the same runtime, keeps rows2->2, passes foreign/cross-room privacy and then accepts actual owner discard12/v84. Actual stock continuation, faulty/custom-proposal replacement recording, zero-wall terminal handling and offered-action rejection are all exercised—not inferred from source or proposal-only checks.

The all-tier controls retain their stated setup boundary: legal meld choices are controlled, bot own-turn discards/wins are autonomous, and Easy is not falsely described as autonomously selecting Chow. Injected-proposal cases are not claimed as stock occurrences. Directed in-process fixtures are not qualification matches.

## Exact-diff and historical preservation

`authorized-diff-preservation.json` rechecks the exact captured test bytes against original before-copies. The ten original defect bodies remain byte-identical. Only previously authorized setup/precondition/policy changes are present: five old self-draw fixtures, one legacy-claim self-draw setup branch, three promotion setups, the specific post-claim Kong policy case, and the single stock-bot proposal expression. All original downstream behavior/inventory/10-second assertions remain; no new rebaseline or test/source edit was made for this closeout. Exact diffs and prior authority/evidence references are copied into `provenance/` and indexed by `review-provenance.json`.

Original74/76, overflow2/2, 78/424 ledgers, bot RED/precondition failures and public-room2->3 failure remain unchanged at their historical pins. They do not contribute to the new169 result. This is now an actual single current-source run, not a mixed-version latest-outcome ledger.

## Remaining external gates and cleanup

Independent source/exact-diff review and candidate admission are not self-approved. No Docker, live primary, browser or cohort action occurred. **Qualification remains0/120; host execution is still an external prerequisite.** Root packaging R2 was reported approved and was not rerun or modified here.

The captured static index names `autotable-src.69dad4da.js`, SHA256 `82730a85afa8e1cf73406a7dee6a17d83847e40d7433818b8e046c75b086ca59`, not the older b237/7a4ee030 entry. This is explicitly a captured-content identity, not frontend review or live-image approval. The later workspace runtime SHA14b12539... also is not part of the tested641e7514 capture.

All test-host execution completed. The existing RuntimeRulePartials fixtures left16 orphaned WAL/SHM files after deleting their databases; exact owned paths/hashes were recorded and only those sidecars were removed. No fixture database files remain. The initial stale-filename postprocessor failure and raw diagnostics are retained; no test was rerun or counted from postprocessing. No production/test source, old harness, historical evidence, dependency manifest, Git/index or live environment was changed.


## September14: two independent exact-fixture approvals recorded

Vasquez's persisted FINAL fixture-only APPROVE records were read through `sessions/2026-09-14-vasquez-fixture-review.md` and matched their exact on-disk hashes: frozen review `57a34614071463307f3dad8acfc5d98503df7fe6f4eb64ca522bd8649afed388`; separate nonfrozen-five review `b2ebe3969e6848f6d1535f85d5163939f63d7b7a8fd2d61fe8104e74e4f715c2`.

At **2026-09-14 16:17:57UTC**, all three current files match the approved test hashes, and the same approved bytes are present in this preserved169-case source capture:

- `CanonicalRuleQualificationTests.cs`: `f0ebf41a518b1b7374bbd9e0254adc37259feaf1fdfee2c670377f7bc4e4dde4`; approval covers only the authorized legacy-claim self-draw setup, with all ten original discriminators and other bytes preserved.
- `SpecialContextWinsTests.cs`: `d410d639a4cec61e74e14dee5649da6ca0c07dd96d056f779e0d0392131c8d16`.
- `ScoringOptionsCharacterizationTests.cs`: `d92a09bc9ee757b143b50fef82cde2bf3907327fa2f91f5a72d6c80df096d3aa`; together the latter two approvals cover the five original authorized nonfrozen setup repairs.

Exact review/subject/diff/execution-pin receipt: `session-files/qualification/2026-09-12/hudson-actions/fixture-review-receipt-20260914T161757Z.json`, SHA256 **`5562af6e4e00d7f7070cd16a036c4f411ab165254a13261b817d5c9451b1fbaf`**, with adjacent checksum. This is an added review receipt, not an edit to the immutable169-case manifest.

Vasquez's independently corroborated13/13 packet remains at runtime45d043e7/endpoint8cd5b64b; the separate5/5 remains at old engine3151be59/runtime3766ec17. They are not aggregated into a new18-case run, not added to169 and not transferred to different production pins. No replay was performed. Test-diff approval does not reapprove the rejected historical guard, alter separately recorded WS/bot/Pung-promotion review scopes, waive the newer claim-notification RED, clear rejected UI/counting-harness lockouts or admit any image/current-runtime/cohort. Replay/admission HOLD and qualification0/120 remain.


## September14 adopted Kong disposition: existing WS case already aligned

Read binding `sessions/2026-09-14-vasquez-kong-boundary.md`: the effective14 post-Pung/Chow Kong policy is **already ADOPTED** by the coordinator's2026-09-14T05:59:12Z decision, not policy-pending. Canonical concealed/added candidates retain their owned active/no-window/nonterminal/effective14 and game/version/tile/meld guards without an extra own-draw prerequisite. SelfHu still requires genuine own draw; no pre-draw13 permission, marker fabrication, Pass-Hu relaxation or legacy-claim-channel expansion follows.

The authorized NEW WS case is already `PungWithoutOwnDraw_AllowsLegalKongButRejectsSelfHu` in `HudsonOwnTurnWsIntegrationTests.cs`, SHA **b3aa25bdf2104d648902620bc375303897a72f990d52bac708d506dcccec0195**. At17:21:54UTC current file and this preserved169 capture match exactly; shared fixture39531682 and engine0c8 remain unchanged. Its existing raw TRX PASS shows effective14/structuralWin=true/ownDraw=null; fake SelfHu rejected unchanged; advertised concealed quartet[40,41,42,43] with hu=false atv3; real WS Kong consumes the quartet, draws back104, advances tov5 and conserves108. This specific case proves the concealed branch, not an additional AddedKong execution.

No code change or replay was needed. Exact policy/source/prior-result receipt: `session-files/qualification/2026-09-12/hudson-actions/kong-disposition-receipt-20260914T172154Z.json`, SHA **e69e3a6ac31d706d244b36e00f82bd0edb8f7c5009462f9fda4876a82bb7360f**. The prior PASS remains at this169 packet's runtime641e7514/endpoint752c673a and adds no new count or production approval. Independent source review, coherent-freeze/replay/image/cohort HOLD and qualification0/120 are unchanged; no broader frozen rebaseline or source-review disposition is inferred.


## September14 WS policy diff approved; bot correction already completed and separately reviewed

Received/read the exact Vasquez WS-only approval record `vasquez/post-claim-policy-review-2026-09-14/disposition.json`, SHA **f619a1536c3cdde76d7462a3a0891d11d4b0207f4521cd0a2e9cbb2708b72191**. Current WS file matches approved **b3aa25bdf2104d648902620bc375303897a72f990d52bac708d506dcccec0195**; exact diff **eddd7a1d3941efebd1d6f3a609ea950935caeb0e4e812d050eb46f9ca2df912b**. This independently approves only the adopted-policy method change, with the other44 literal rows/helpers unchanged—not production source or a new run.

The reviewed historical3-case packet stays **3executed/0PASS/3FAIL** at df0cdcfb availability/runtimec07f50de: the WS metadataNULL failure did not reach its Kong/replacement suffix; both old10f9 bot rows stopped at ExpectedDeclareWin/ActualDiscard before their continuation wait. No ten-second-expiry, fresh-stall or continuation credit is invented, and none is relabeled green.

The requested bot edit is **already present**: current source **7c1571434ecf26ae556089fbaa3d1f537356b0503a4c15a7f988eae032756971**, line117 exactly `Assert.Equal(BotActionType.Discard, proposed.Action.Type);`. Its exact one-line diff is **6153caf080da90616fbf5d2459c0716281a81df5a8ae9956837f33e438745f54**. The later independently persisted Vasquez review, `bot-oracle-authority-and-postfix-2026-09-14/disposition.json`, SHA **7acb3fabc8b6bc3ad33ce4b416a8d39c54b76e16b8d6e43a265c858aeb1e20de**, already technically APPROVES that exact revision and corroborates complete2/2 automatic Pung/Chow -> discard92 -> next-human draw102/v8/inventory108 evidence at runtime0e0e2c3c.

Authority chronology remains intact: the intervening04:00PDT freeze superseded earlier(b) while it applied; the later specific05:23:23.166PDT coordinator release recorded in the owned handoff is the authority for the completed edit. Neither this notification nor the older grant is used as a retroactive freeze bypass. The original10f9 and both RED generations remain preserved. This169-case packet also later executed the WS suffix and both bot rows successfully at its own641e7514 pin; that is separate evidence, not a rewrite of the old3/3 failures.

No new edit, replay, source/image approval or helper-lockout change occurred. Exact linked receipt: `session-files/qualification/2026-09-12/hudson-actions/ws-policy-approval-bot-closure-20260914T193742Z.json`, SHA **11da17ed85767a333bd244476127f153a0558ba47a6aa0caeff208244297d213**. All independent production/coherent-freeze/live/cohort holds and qualification0/120 remain.


## September14 grant(c) exact-diff review closed

Vasquez's `pung-promotion-review.json` matches **d38681c328e1646910af8e8dedb6a7e462dafa13ab01b37658b6db6b38b899f3**. Current `PungPromotionToKongTests.cs` and this169 capture both match approved **31a0c78fa0d9a6fc04d78b21807e919c9e8c785b57abd9cf223a2bd99ff83a20**, exact diff **23aeea54f875628d18e90fb9efc11b874a6026243199309128b7fa0b6f290a36**. Grant(c)'s review is closed only for the three authorized methods: all8 original assertions remain verbatim/in order and every outside segment is unchanged.

The reviewer-corroborated3/3 TRXed370025 and859-file packet stay at their original approved0c8 execution pin; no new replay, whole-class/78-suite green or old-history rewrite occurs. One method uses conserved directed setup plus real transitions, and two use seed-driven traces without direct hand/wall writes. These are acquisition/effective-hand fixture repairs, not an added own-draw requirement for valid post-Pung/Chow Kongs.

Exact receipt: `session-files/qualification/2026-09-12/hudson-actions/pung-promotion-approval-receipt-20260914T195246Z.json`, SHA **d6d56d742054f82f31dee5de639cd2eea038b9b7374a3f79b1934e71b48631c8**. No source/test edit, runtime/image approval, unrelated helper-lockout clearance or qualification credit; other review/coherent-freeze/live/cohort holds remain.


## September14 19:58:44UTC bot-status readback

The incoming claim that line117 still expectsDeclareWin is stale. Current bot test remains **7c1571434ecf26ae556089fbaa3d1f537356b0503a4c15a7f988eae032756971** with exact `Assert.Equal(BotActionType.Discard, proposed.Action.Type);`, immediately followed by the retained actual autonomous-progress wait. Its one-line diff/full2/2 and independent7acb3fab review are already recorded above; no further edit or replay was needed.

All four supplied strategy hashes match disk. The supplied runtime0e0e2c3c does **not**: current main runtime hashes **d594cd359dc835a8da84acb77221aa79924815f697a9403cada40acb5784d0a4**. Earlier2/2,33-case RED and169 outcomes retain their own pins; no current-runtime approval is inferred. Exact fresh receipt: `session-files/qualification/2026-09-12/hudson-actions/bot-continuation-status-20260914T195616Z.json`, SHA **8d0d3f4c61f04e1565081e5a5e1f93500eb86c60ccd987d97e7a9fd9c491c068**. No code/strategy/helper edit, synthetic replacement, new test execution, lockout change or qualification credit.


## September14 20:07:34UTC: real bot-stall RED retained; supplied post-fix task already complete

Bishop accepted the original3766ec17 scheduled-bot failure as a genuine continuation defect. The retained compact evidence still hashes **2b63526daf908cccc5d3d28f960baf9ccb162922e867464f8311c3ab13670f20**: both actual Pung/Chow rows stop after melded11/effective14/no-own-draw within the unchanged10s budget. This is distinct from later proposal-only RED generations and remains unchanged.

Fresh current readback again confirms bot test **7c1571434ecf26ae556089fbaa3d1f537356b0503a4c15a7f988eae032756971**, line117=Discard, with its real continuation wait intact. The full2/2 post-fix execution already ran **12:26:46-12:27:43UTC on the exact supplied0e0e2c3c runtime**, TRX **96f951f730897ca2a3ecddd37a1158d2cd26f898748105cded43a6e41bd4b982**, and is independently reviewed by7acb3fab. Both rows physically discard92 and draw102 for next human/v8/inventory108 while retaining Pass-Hu/no-score/no-error checks. No further edit or replay is needed to produce that already-existing exact-pin proof.

Current workspace main runtime remains **d594cd359dc835a8da84acb77221aa79924815f697a9403cada40acb5784d0a4**, not0e0e2c3c; no result is transferred to it. No strategy substitution, source/helper edit, history rewrite, new approval or qualification credit occurred in this status reconciliation.


## September14 cross-difficulty evidence corroboration, not formal review approval

Vasquez's qualified12-case evidence record matches **1d90f629214c2ddd960658dd90c7f2220b783ce4839c2143243c78ad928e2968**. Current and169-captured `HudsonBotDrawGateIntegrationTests.cs` both remain **c1f38423b1bf5463fbac9d03a9fadc7985c75abef79748b961983f261b2a3ff4**. Record only **bounded evidence corroboration**, not a test/source APPROVE, rejection, new rule or lockout. The historical12/12 raw TRXa81e8823/858-file packet retains its0e0e2c3c/752c673a/0c8 pin and12:16UTC comparison date; no replay/current-source transfer occurred.

Required final-handoff precision: the8 post-claim rows use fixture-controlled legal Runtime.ClaimAsync choices after genuine signed discard; subsequent bot discard/next-human draw are autonomous. They do not prove autonomous meld selection or Easy choosing Chow. The4 genuine front-draw0 controls autonomously win/score/cap1-complete. Gain+4 and version8 are **logged observations**, not exact-value assertions; tests assert positive payout/zero sum and version advance. All tiers choosing92 does not independently distinguish erroneous cross-strategy dispatch, despite difficulty identity and expected-choice checks. Empty diagnostics cover only captured ChangshaGameRuntime warning/error logs—not every application logger or exercised timeout/fallback paths.

Exact receipt: `session-files/qualification/2026-09-12/hudson-actions/bot-draw-evidence-qualification-receipt-20260914T201301Z.json`, SHA **37afc3a98dd6ae5b7c90aea2b065b67ecde3bc9408abbba2b481242061cf48ec**. The record's protected10f9 belongs to that historical checkpoint; later authorized7c157 and its full2/2 review remain separate. No source/test edit, aggregate relabeling, approval/lockout change, image/cohort admission or match credit;0/120.


## September14 ratified-alignment target inventory: already-aligned fixtures, no new rebaseline

Current WS b3aa, sole granted frozen setup f0eb, shared fixture3953, CurrentProfile parity81fdd and Drake effective-handf3baa all match this preserved169 capture. Current availability adapter **eee17c46c41b76725eeee565a8ec1b947c4bef5ebf234bbf318a42bf9c5e10ff** also matches the aligned capture. The superseded strict-rejection WS case and granted legacy self-draw setup are already corrected and independently reviewed; no additional frozen edit or rule decision is needed.

Exact retained selector groups are45 WS,3 CurrentProfile Pung-concealed/Pung-added/Chow-concealed parity,26 natural effective-hand controls and3 original legacy-claim branches, all passed **within the single historical169 invocation**. This is a subset inventory, not a new77-case run. The3 parity rows use actual WS melds and advertised metadata, then the dedicated runtime declaration; they do not pretend every Kong branch was executed through the new ownTurn WS command. They also do **not** establish newly changed SignalR caller-identity forwarding or unchanged RPC arity; that remains a distinct acceptance boundary.

Current engine is4070693b, not this packet's0c8, and no runtime/source approval transfers. Grouped selector, exact hashes, historical row counts and limits are preserved in `session-files/qualification/2026-09-12/hudson-actions/kong-alignment-readiness-20260914T201948Z.json`, SHA **1001d160e261989a4da65badbf9e8c36645bfe4e73b3cba3b85000427e168b84**. No test replay, source/helper change, lockout clearance or qualification credit; await the actual coherent final source/test handoff and existing review/admission gates.


## September14 NEW legacy-Hub caller/arity slice completed separately

The previously uncredited legacy-Hub declaration/actual-caller boundary now has a NEW standalone5-case proof: `HudsonLegacyHubOwnTurnIntegrationTests.cs`, SHA853aed9daadc40762b2a754153c50f2dfd0101938a8aa997bb7fc39b69d7761f. **5/5PASS,0fail/skip/retry**, command20:44:03-20:44:53UTC, on Hubbb293ac6/runtime d594cd35/engine4070693b/availabilityeee17/endpoint752. Other seated user and signed spectator are atomically rejected in all five otherwise-legal owner scenarios; identical owner RPCs succeed with2-argument Hu after actual13->14/10->11/7->8 draws and3-argument legacy concealed/added Kong with one held representative. Full live-state/version/event equality after rejection, usable Hub connections, correct owner success, real replacement/draw accounting and108 are asserted.

Manifest `session-files/qualification/2026-09-12/hudson-actions/legacy-hub-own-turn-01/final-manifest.json`, SHA32a957a8314ec849ac41a0daa7aa971af8fbf845ad3792b9357c22af633505ad; TRX34f15c1518e973146578d7cc19a9e33f0558cc01a90019706a19acc6133c805f; full handoff `sessions/2026-09-14-hudson-legacy-hub-own-turn.md`. All1132 captured inputs/1984 sealed files matched; no owned DB leftovers.

This is5 cases with10 negative and5 positive RPC invocations, not15 cases or an addition to98/169. No old test/shared fixture/rejected helper was changed or replayed; no production/source/image approval or0c8 transfer. Fresh-draw legacy controls do not newly prove every post-Pung/Chow/pre-draw branch or persisted-error atomicity. Independent fixture/source/admission and other rejection cycles remain held;0/120.
