# Hudson: current claim-Hu settlement extension

**FINAL: RED. One actual grouped run executed 6 cases: 4 PASS / 2 FAIL / 0 SKIP / 0 retry.** This is separate from the completed 169/169 closeout and all historical results. No live/browser/image/cohort admission occurred; qualification remains **0/120**.

## Exact evidence packet

`session-files/qualification/2026-09-12/hudson-actions/claim-settlement-overflow-01/final-manifest.json`

- Manifest SHA256: `fd57eb6c1ca7cc011d360cf8369c999c98e1b19587cdf0abc2e60fb2f8f2681b`.
- TRX: `results/claim-settlement-overflow.trx`, SHA256 `11dfcffb7c3128b75f5fe5d1cc3c903b6be183ad6820e055d9e9f7a83d0a6285`.
- Complete 1973-file hash seal: `evidence.sha256`, SHA256 `608cd1e9e8b85d21c2f2a0c8b45ba7f1205b4d75dee046ffde9b3e9b2a40f8bb`; all entries matched.
- Full added test diff: `new-test.diff`; exact commands, input/compiled-output hashes, every case/output/error, diagnosis and limitations are indexed by the manifest.

The source was captured on **September 14, 2026, 15:33:14-15:33:28 UTC**. The single Release command, including compilation, ran **15:45:38-15:47:46 UTC**, using the captured project, isolated artifacts/results, one MSBuild node and the unchanged serial xUnit configuration. Filter: `FullyQualifiedName~HudsonClaimSettlementOverflowIntegrationTests`; full command is `results/test.command.txt`. WS setup uses `variant=changsha&bots=false&botCount=0&dealMode=manual&handCount=1&seed=20260912`, with unique signed owners/rooms. Directed conserved fixtures are not qualification gameplay.

Captured inputs:

| Input | SHA256 |
| --- | --- |
| NEW HudsonClaimSettlementOverflowIntegrationTests.cs | `5c247a633c4f140ecd1cd391f715c842389d85a307c231172582aa6eb7b12210` |
| Unchanged HudsonOwnTurnWsFixture.cs | `39531682d5967dce8014c560d95cc52b637f47e74134ac18313055fe036aa4ca` |
| ChangshaGameRuntime.cs | `14b12539b672f3e0f45364e1dbcda5ab703d5c727c606255a206ae7553691fd2` |
| AutotableWsEndpoint.cs | `752c673ad62b3aa09193d0bd94b9236622e3352a2a49d5b01d8405283c08697f` |
| Shared StateMachine | `0c8b838b71746d4e772c651bd278a7d883b650871849f71c6cbfbe780286636f` |
| API DLL loaded beside the test assembly | `5099989e67e0ecc90f54cc7b26ecd8fc2e06debce9bc909764de006960fd847a` |
| Test DLL | `c9d915d2f38adb30b5d67989f4a4acc3b67ffc2b2b2f40111b2ba527e1205d62` |

## Four complete overflow/continuation passes

| Actual row | Outcome and continuation |
| --- | --- |
| Discard-Hu winner overflow | PASS; real same-socket Pass, next actor1/front draw, v2 to v4 |
| Rob-Kong-Hu winner overflow | PASS; real same-socket Pass, original Pung promoted once, actor0/back replacement, v4 to v7 |
| Discard-Hu payer underflow | PASS; actual unchanged 30-second timeout, no test Pass, next actor1/front draw, v2 to v4 |
| Rob-Kong-Hu payer underflow | PASS; actual unchanged 30-second timeout, no test Pass, one promotion/back replacement, v4 to v7 |

Every row requires exact sender-only `actionRejected/current`, action `claim`, reason `score-overflow`, plus automatic full resync before the explicit JOINED fence. The entire serialized live state/version and SQLite StateJson/version/update timestamp remain identical. The actual window and uncancelled timer references, empty pending queue, scores and no-penalty state are preserved. No observed scoring/completion success is published. Actual continuation then preserves all108 physical tiles, consumes the correct front/back tile without an extra draw, and matches persisted state. The existing30000ms timer and new35000ms continuation wait were not changed.

## Two positive-control failures: precise product boundary

Both authenticated normal Hu commands reach authoritative GameComplete and pass all assertions before line189: correct winner1/source0, exact Discard or RobbingKong method/flags, winning tile0 or19 transferred exactly once, unchanged wall/back count, retained original Pung on robbery, exact single payment0->1 of2 or7, and corresponding zero-sum totals.

Both then fail `Assert.Contains("WinDeclared", signals.Events)` at **HudsonClaimSettlementOverflowIntegrationTests.cs:189**. The same connected SignalR observer, fenced before and after by real JoinTable/FullState, receives exactly **ScoringComplete, GameEnded, GameCompleted**, but no WinDeclared.

Read-only diagnosis of the exact captured runtime: `ResolveClaimWindowAsync` resolves Hu, emits ClaimMade, then scores and emits ScoringComplete; it does not call the existing one-object `EmitWinDeclaredAsync`. At this pin the emitter's sole call is in self-draw `DeclareWinAsync:1370`. The test's one-JsonElement subscription matches the real emitter shape. This is a **claim-Hu notification-contract defect**, not failed score arithmetic, overflow mutation, a disconnected observer or a compile error. The contract explicitly describes the WinDeclared payload and per-hand win-before-scoring lifecycle at `docs/rules/changsha-signalr-contract.md:236-244,379-385`; frozen contract SHA `9fd77c982225d392f77e8163c67e298593a4dc6b124da0614b10d03039768da9`.

No assertion was removed, reordered, weakened, skipped or retried to turn this green. The positive cases' later whole-inventory and terminal-persistence assertions did **not execute**, so complete positive acceptance is not claimed. WinDeclared absence alone is nondiscriminating for the negatives at this pin; the other whole-command/queue/timer/persistence/success-event and actual-progress assertions do execute and pass.

**Revision routing:** Hudson makes no production edit. A DIFFERENT eligible runtime revision author is required for the rejected notification path; proposed **Drake**, subject to coordinator eligibility confirmation and a file-specific grant. This handoff grants no write authority, and the rejected path's author must not self-revise or advise its replacement. The unchanged new test is ready for a separately captured, separately evidenced retest after an authorized correction.

## Provenance and environment qualifiers

All1110 captured files and their full inventory remain byte-identical before/after execution. The MVC test-host manifest points inside the capture, and the loaded API dependency matches its captured project build. Branch and HEAD remained unchanged. No owned runtime DB/WAL/SHM files remain; no cleanup was needed.

The original source-capture field `sourceFilesReadOnly=true` is **not filesystem-enforcement evidence**: all1110 files actually report mode0777. This discrepancy and the failed permission assertion are preserved in `postprocessing-attempt-02.json`; the final manifest explicitly distinguishes hash-stable bytes from filesystem write protection. The original declaration was not rewritten. A prior metadata attempt also used an unavailable `python` alias; the installed `python3` completed postprocessing, with that127 failure retained. Neither event reran or changed tests. The initial NETSDK1004 missing-assets bootstrap failure is preserved; existing dependencies were restored only after it.

The earlier169/169 packet at runtime641e7514 remains byte-identical, separately referenced, and was not rerun or aggregated. The new6-case result uses runtime14b12539. No production/shared-fixture/UI/counting artifact, historical evidence, dependency manifest, branch/index, live process or deployed target was changed by this closeout. Independent review and candidate admission remain external.


## Incoming Bishop52-case checkpoint reconciled, September14 at16:10:29UTC

The supplied `bishop-actions/claim-settlement/review-manifest.json` matches its reported SHA `eef471f948291c05654b237fda7906fa4ec839ad9a3e873b40b02c827dacdf03` and records runtime96300e95/endpoint624222c8. Those are **not the actual current disk pin**. Current runtime14b12539/endpoint752c673a and NEW test5c247a63 still match the already executed six-case capture above. The owned manifest remainsfd57eb6c and TRX11dfcffb; no new invocation or source change was needed.

The handoff's statement that dedicated Hudson cases have not executed is superseded by the completed **6 executed /4PASS/2FAIL/0skip/0retry** result. Four full atomic overflow and real Pass/timeout continuations pass. Two normal settlements remain RED solely at the missing contractual WinDeclared notification; later positive inventory/persistence assertions remain unexecuted. Bishop's reported52 and the earlier169 are separate checkpoints, not additions to these six or proof at arbitrary different hashes. Independent notification revision routing, source review/admission HOLD and0/120 are unchanged.

New exact reconciliation record: `session-files/qualification/2026-09-12/hudson-actions/claim-settlement-handoff-20260914T161029Z.json`, SHA256 `0109edf2f0a2a61de092d7d6e002a39545bf0a4434049340b8e1736a657250ba`, with adjacent checksum. No production/test/history edit, browser/live action or additional acceptance count.
