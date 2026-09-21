# Hudson backend action/BaseUnit regression handoff

**FINAL for this disjoint test assignment: 76 executed / 74 PASS / 2 FAIL / 0 SKIP / 0 retry. RED on settlement-overflow handling; no source approval or live-game qualification is claimed.** Actual execution was September 14, 2026, 05:04:01-05:11:15 UTC (the September 12 path is the assignment/evidence namespace).

## Actual grouped result

- `HudsonOwnTurnWsIntegrationTests`: **45 PASS / 0 FAIL**. Authenticated self-Hu after real draws (14/11/8 concealed including all four seats), concealed/added Kong physical IDs and back draws, actual Hu/Pass robbery-window resolution, pre-draw and post-Pung rejection, preserved legacy claim behavior, malformed/stale/wrong-owner/spectator/reconnect/cross-room controls (including same-owner equal-version rooms), registration-independent owner-only availability/tombstones, and three exact private Chow options with invalid/explicit selected-pair controls.
- `HudsonBaseUnitIntegrationTests`: **29 PASS / 2 FAIL**. Default 1 and configured values through max **11,184,810**, numeric wire value, canonical payment/total scaling, creation latching through later seats/reconnect/client config forgery, 12 invalid WS queries, old three-argument SignalR RPC plus new DTO, five invalid DTO values, and three persisted runtime/factory-restart/signed-reconnect rows pass.
- Both failures are `OwnTurnSettlementOverflow_IsExplicitlyRejectedWithoutPartialMutation` with `winnerOverflow: True/False`. After actual draw and advertised owner Hu at version 2, boundary totals `[2147483647,-2147483647,0,0]` or `[1,-2147483648,2147483647,0]` produce **an abrupt WS disconnect**, not an explicit `actionRejected`/resync. Error: `IOException: The remote end closed the connection`, inner disposed `TestWebSocket`.
- Reproduced rooms: `hudson-own-681d0c85ff4449f2a21355b2690b5165` / runtime `21134403-e861-43cb-81fe-c2731a06819a`, and `hudson-own-83ca35c883ef43fa86bf80ead3f7acae` / runtime `e195cd92-ddda-42b5-9f0e-0173234b1ca0`.
- Source diagnosis supports an uncaught checked-settlement overflow: the tested runtime declares/emits the win before Score, and the WS handler does not catch OverflowException. **The post-fault snapshot assertions were not reached**, so this evidence does not claim observed score wrapping, an exact residual phase, or an ordinary live-game failure.
- Bishop (`84e0d4ad-a2e2-4c16-a867-c264adce9917`) and Burke (`403154cf-209d-4cd8-9b65-c07f661742b9`) received the concrete contract question and actual failure evidence. Production correction/atomicity remains theirs; no assertion or timeout was weakened.

## Exact new test hashes

All three files are NEW under `src/backend/tests/Mahjong.Autotable.Api.Tests/RulesQualification/`:

| File | SHA256 |
| --- | --- |
| `HudsonOwnTurnWsFixture.cs` | `39531682d5967dce8014c560d95cc52b637f47e74134ac18313055fe036aa4ca` |
| `HudsonOwnTurnWsIntegrationTests.cs` | `db3276e046dcad57bfd059e1c90ddcaaa85691b7c8dbbd62384a639c11f3f980` |
| `HudsonBaseUnitIntegrationTests.cs` | `9a69cb8d26215dd9b4c85608c18cd11ad48ab3639636dde1b0d1bbc06ea6ba48` |

## Sealed reproducible evidence

Root: `session-files/qualification/2026-09-12/hudson-actions/`.

- Detailed handoff: `final-handoff.json`, SHA256 `0a0c3d31ac3fb04caad3690056b2431bf81026be31c6ca40d7a9614ec5cf46dd`.
- Full **858-file** evidence seal (including built assemblies): `evidence.sha256`, SHA256 `703e58dc943e84df2e9d95e0b4c97f147196986ddcda6845bda0e0002f0f0919`; all entries matched.
- Actual nonzero TRX: `targeted-04/results/hudson-actions.trx`, SHA256 `47050a7b2d5f6a03b86befba2c1fffb56515cabd4499256adc4faf64580fd79f`.
- All 76 case outcomes/times/failure messages: `targeted-04/parsed-trx.json`, SHA256 `0e79cb0802efc05285a0c7489ffac984d30eaa7818e1a2fe3a90e29c24e3fc57`.
- Exact original command: `targeted-04/command.txt`. Project `src/backend/tests/Mahjong.Autotable.Api.Tests/Mahjong.Autotable.Api.Tests.csproj`; filter `FullyQualifiedName~HudsonOwnTurnWsIntegrationTests|FullyQualifiedName~HudsonBaseUnitIntegrationTests`; .NET 10.0.100, Release, `--no-restore`, isolated `--artifacts-path`, `-m:1`, existing xUnit configuration, no retries.
- **998 source/config/test inputs matched START/END**; manifest `targeted-04/source-inputs-start.sha256`, SHA256 `56aa5bebdc7bfad0a718fe1147937f0b113a9fb346422faec2af476b9437bf84`. Frozen new test copies and exact wire commands/rejections are retained.
- Actual test API DLL SHA256 `023dae8ff5530a5e1f14988805c5e2ce5592ed0c40c16a868d069f4a4d5b81b8`; test DLL SHA256 `50f1fce96340fc3f8a6983582214c81d8a79241b7e0c9a540a8f1e3b7ac76456`.
- Tested runtime source SHA256 `b8ece9de42c837e13652793674f78fa3cef9c8da7a42458e849bbdb11e615c6c`; endpoint source SHA256 `af76306cc7f3b24e651e6625b992e89ac7cedae14ba0ad89c94fbfe80c37e9f8`.

## Preservation and limits

The initial no-restore command returned 0 **without any TRX/execution and was not credited**. A separate no-restore build established NETSDK1004; only then were the existing manifests restored into isolated artifacts. Those setup logs are preserved. A postprocessing `python` alias failure is also retained; installed `python3` produced the valid separately named parsed summary without installation.

Vasquez's three frozen audit SHA256s remain exactly `9c3306ad...ac01b`, `7175e9a5...422f4`, `dc5401e7...f04a5e` (full values in the detailed handoff/fingerprints). No production source, protected audit/helper/spec, prior evidence, UI counting harness/v3, git state, container, dist, or live server was changed by Hudson. No browser was used; test factories/connections disposed and no own fixture databases remain.

These are conserved-inventory authenticated **in-process fixtures**, not live UI games and not portable-image acceptance. **0 qualification match credit; 0 qualification SQL rows touched.** Restart coverage proves persisted runtime hydration plus signed SignalR reconnect, not restoration of the nonpersisted WS relay-room alias map; no test binding was injected. No canonical/provider/full-backend suite or preset/house-rule behavior was added or run. Independent review remains with the coordinator/reviewer; any future correction requires a fresh targeted evidence/artifact directory, without overwriting this RED record.

## September 14, 2026: later scope/contract clarification (received 06:16:37 UTC)

**No new source edit or test execution.** Burke's later source-pinned contract narrows active authority to NEW `RulesQualification/` tests and explicitly says his earlier legacy-fixture request did not grant authority outside that path. The two legacy-file changes were already completed at 05:26-05:27 UTC; they remain disclosed and preserved, not self-approved. **Coordinator assignment/disposition is required.** Hudson will not edit or automatically revert either legacy path under the narrowed grant. Current hashes: `SpecialContextWinsTests.cs` = `d410d639a4cec61e74e14dee5649da6ca0c07dd96d056f779e0d0392131c8d16`; `ScoringOptionsCharacterizationTests.cs` = `d92a09bc9ee757b143b50fef82cde2bf3907327fa2f91f5a72d6c80df096d3aa`. Their preserved five-case result does not confer authorization or review approval.

**Policy qualifier:** the historical PASS for `HudsonOwnTurnWsIntegrationTests.PungWithoutOwnDraw_PreservesDiscardClaimButCannotDeclareOwnKong` is only an observation of Bishop's earlier supplied endpoint policy. Burke now flags the extra endpoint all-action own-draw gate versus preserved pure Kong eligibility after Pung as unresolved. This case must **not** be treated as final current-rule proof pending explicit disposition. No raw result count is rewritten, and no new assertion, skip, weakening or policy choice is introduced. Fresh-draw Kong controls and the actual post-meld self-Hu bot-continuation defect remain separate.

**Compile diagnostic:** the current test file still hashes to `db3276e046dcad57bfd059e1c90ddcaaa85691b7c8dbbd62384a639c11f3f980` and matches the previously compiled 45-pass frozen source byte-for-byte. It asserts `ClaimType == TableClaimType.Hu` at lines 144-146; the reported CanHu/CanPung/CanKong/CanChow compiler errors refer to the earlier draft, not current source. Burke's newer runtime all-pass probe is not executed or credited by Hudson.

Full new immutable disposition: `session-files/qualification/2026-09-12/hudson-actions/scope-contract-disposition-01.json`, SHA256 `c59d82d9317a5dbd889aadd89ec6c8cd1e3f3068fb2e2a08f81ddeaaa213ec52`; separate `.sha256` seal retained. Burke received these concrete scope/policy/source discrepancies for coordinator disposition. All prior evidence, frozen audit files, shared helpers and the locked UI qualification harness remain unchanged. No production/image approval or completed-match credit is claimed.

## Repeated compiler diagnostic: exact owner-evidence reconciliation

The September 14, 2026, 06:36:39 UTC message referenced Bishop's **older** `source-review-manifest.json`, recorded at **05:00:19 UTC**, which binds test SHA256 `aed529896f65896fa0b767758f2b66a184a0cc37396d18688d0c6028fb55c278` and the earlier CS1061 draft. The current owned test is SHA256 `db3276e046dcad57bfd059e1c90ddcaaa85691b7c8dbbd62384a639c11f3f980`; its file modification time is 05:01:13 UTC, and its enum assertion matches the already compiled Hudson snapshot.

Bishop also has a **later** `final-review-manifest.json`, recorded at **05:20:14 UTC**, which binds the corrected `db3276...f980` test. Read-only parsing of his actual retained TRXs confirms that compilation subsequently succeeded:

- `actions-final-focused-02.trx`: **175 executed / 173 PASS / 2 FAIL / 0 SKIP**, SHA256 `f07d301430e0ef577fc9056e0139b7a743b3b97a688c095589bfe68685963d3f`. The two failures are the then-unfixed boundary settlement cases, not compiler failures.
- `actions-settlement-final-01.trx`: **47 executed / 47 PASS / 0 FAIL / 0 SKIP**, SHA256 `5082bc2122a00e27bc8252b9b80f378d80cbfb249f4521ee37cb6e5b19755650`. Both unchanged overflow rows are present and pass in this later **owner-executed** scope.

These owner runs overlap and are not added together or counted as new Hudson execution. They do not constitute fresh independent Hudson retesting, source/image approval, a policy disposition for post-Pung Kong, or mass-match credit. The previously sealed Hudson RED evidence remains unchanged. No source edit, alias, exclusion, test rerun or rebuild was necessary to resolve this specific stale compilation claim.

New immutable comparison/copies: `session-files/qualification/2026-09-12/hudson-actions/compile-diagnostic-reconciliation-01/reconciliation.json`, SHA256 `97f1c982228c7849feb2e3784439474557c7179329d56a49cb2fbdb3fad97d9f`; six-file seal `befd03364a6da825871f3023a2076d33897453b7376de4c518355f33b608f8ea`. Original Bishop manifests/logs/TRXs were read-only and unchanged. This establishes the old-manifest/new-manifest mismatch rather than merely repeating that the owned source is fixed.

## Kong-policy decision escalation and reported all-pass progress

Burke's September 14, 2026, 06:45 UTC update requests an explicit coordinator/Vasquez reconciliation of the current-rule post-Pung/Chow Kong gate and frozen-fixture lineage. Hudson is forwarding this narrowly scoped decision request to the existing Vasquez rules agent (`910240df-acdb-40b7-8584-667cd6ea94e7`), not selecting a rule or assigning new engine work. The pure Kong predicates and stricter outer ownTurn availability are **not declared equivalent**. The historical post-Pung Kong endpoint case remains policy-pending. No aliases, additional domain fields, second BaseUnit helper, frozen-fixture edits, self-Hu fallback, validator change, or test rerun are introduced.

Burke separately reports **1 executed / 1 passed / 0 skipped** for `RuntimeAddedKong_AllHuPass_DrawsOneReplacementAndAcceptsDeclarersDiscard`, using the shared authenticated fixture read-only. Supplied TRX SHA256: `54f543d81e6cb22869cb384f5e48e34b397ef87f4f0857d1bd7d6712ae4f2e90`; supplied updated owner-manifest SHA256: `008379ef25a3cc2a785620d356b5aeee18e7f399c769001a3a907aea644ccf4d`. This is **owner-reported progress**, not new independent Hudson execution or shipping approval, and is not added to qualification match counts. It is distinct from the post-Pung/Chow readiness disagreement and the previously reproduced bot self-Hu continuation stall.

## NEW independent Hu-overflow command retest: 2/2 PASS

**Fresh Hudson execution, not another owner-report reconciliation:** September 14, 2026, **07:35:43-07:36:29 UTC**. Only `HudsonBaseUnitIntegrationTests.OwnTurnSettlementOverflow_IsExplicitlyRejectedWithoutPartialMutation` ran: **2 executed / 2 PASS / 0 FAIL / 0 SKIP / 0 retry**. This independently qualifies the dedicated own-turn winner-overflow and payer-underflow rejection slice on the exact new source pin below; it is not global/source-review/shipping approval.

The two existing rows were strengthened inside the NEW `RulesQualification` file only. Boundary totals are placed before the real runtime draw, with actual persistence enabled, so the pre-command live state and SQLite snapshot must first agree. Both cases received exactly the sender-only `actionRejected/current` envelope with `action=hu`, `reason=score-overflow`, `requestedSeat=0`, `ownedSeat=0`, `full=false`, and no extra exception/credential fields. The server's corrective full snapshot arrived **before** the explicit test JOIN fence and retained version **2**. Complete authoritative JSON (including EventSequence/EventLog), current win/score, phase, inventories and four totals remained identical; persisted StateJson/StateVersion/UpdatedUtc also stayed unchanged.

A signed SignalR group observer fenced by ordinary JoinTable/FullState received **zero** WinDeclared/ScoringComplete/HandFinished/GameCompleted/GameEnded success events. A spectator received no rejection; WS result/gameComplete success entries were absent. After these assertions, **the same WS socket really discarded tile 44**, the next human seat **1 drew tile 17**, and the version advanced to **4** with unchanged boundary totals and all **108** physical IDs conserved. The group observer received that real TileDiscarded event, providing a positive liveness control for its earlier no-success-event assertions.

Actual rooms: winner overflow `hudson-own-9b91abfcaeff44dda3163cd91cf52709` / runtime `feac17b5-e91b-474f-839c-d69d48c07a2e`; payer underflow `hudson-own-f461cc74dd874ddda6bf8eb6a8353928` / runtime `22e0090b-9f39-4bc2-aef1-1b82085eb1c6`. Both use fixture seed **20260912**. No hand or match was completed or counted.

### Exact pins and immutable proof

- NEW current `HudsonBaseUnitIntegrationTests.cs` SHA256 **`8d681f6be0d2501f7e05c952a0d2152d290d867898d9fcf7fbb592bfcd9e141b`** (previous `9a69cb8d...ea6ba48` retained). Only the overflow method and two BCL imports changed; every other method/helper is byte-identical. The shared WS fixture stays `39531682d5967dce8014c560d95cc52b637f47e74134ac18313055fe036aa4ca`.
- Tested runtime SHA256 **`b600cf06d9197c3d55acee1ccdd419df5d6acef77a9c518f40ebd4209f371f65`**; endpoint **`8cd5b64bbb64c39c5239a9b6bb5fd1323c809b00d58e261e0c20af7394161792`**; engine **`0c8b838b71746d4e772c651bd278a7d883b650871849f71c6cbfbe780286636f`**. All **1001** inputs matched START/END; six frozen diagnostic copies match the compilation manifest.
- Proof root: `session-files/qualification/2026-09-12/hudson-actions/overflow-atomic-command-01/`.
- `handoff.json` SHA256 **`ce0a0e852e4dd3a6a977f080f2dbfa8a4a72d94dd9a5fdf979c61f651110e810`**.
- Actual two-case TRX `results/overflow-atomic-command.trx` SHA256 **`a5e475402710027fb5595f580ffc9bb16d1e206bf222d7c7df01a597dff0c429`**.
- Compact actual envelopes/continued-input outcomes: `observed-outcomes.json`, SHA256 `5760a88a4d5bc8c1915d623ed29afe2630ddad6791c5731586dd2e19c5d77ff0`.
- Separate **860-file** seal `evidence.sha256`, SHA256 **`4627160f47974dbb92dda69728509f4b7b22b437e6867990c6fe0cab34123434`**; every entry matched. Exact command, diff, original/frozen tests, complete TRX output and source/assembly fingerprints are retained.

No production or out-of-`RulesQualification` source was edited. The three frozen audits, shared helpers, locked UI/counting artifacts and all prior failure seals remain unchanged. All own sockets/factories closed; no own fixture DB remains. Restore followed actual fresh-path NETSDK1004; no package manifest, existing timeout, retry or skip changed. **Zero qualification SQL changes and zero completed-match credit.**

This retest does not qualify accepted discard/rob-Kong Hu settlement, deferred-Chow F04, bot-meld continuation, the unresolved post-Pung Kong policy, or the entire 76/78-case suite. The separate F04 owner report is preserved at `f04-deferred-chow-owner-report-01.json` (SHA256 `7c5ad1581c57888d9ee63fe3357a06d8a8c0d9dff794f4fa742b456b4dff0993`); it was not duplicated or executed here. Prior original RED evidence remains immutable rather than being rewritten as green.

## Hicks source/bundle report: no browser or candidate acceptance implied

Received September 14, 2026, 07:51:47 UTC: Hicks reports source/bundle completion at `sessions/2026-09-12-hicks-actions.md`, with the selector/wire contract in the sibling contract key. Owner-reported generated entry: `autotable-src.d3557f24.js`, SHA256 `5e802eac652a9a8b32219cde8b50547f74596516e0b7f2dff8b17c21a758a526`; action chunk: `rule-action-controls.e520b7c0.js`, SHA256 `a937955f9106a4309dc9a9e7be7dbdc4f5b3cf62a4673ee41b3f6e1f90eb3854`. Owner manifest: `session-files/qualification/2026-09-12/hicks-actions/final-source-and-bundle-manifest.json`, SHA256 `3e3e28a383e0c69674a4edca6801c32f32efbf34d9369773c6056a4a6d42981b`, reporting 16 source and 85 dist hashes.

These are **owner-reported build/source artifacts**, not a Hudson-verified live candidate. Hicks reports TS/build exit 0, 47 source-only controls passing, and retained scoped lint **14 errors / 1 warning, 0 introduced**; the legacy Chow rejection/window-token limitation remains disclosed. Independent source review is explicitly pending in this message, and Hicks reports no image/container/live-target change.

The reference to scheduled browser qualification is **not an execution grant**. Hudson has not started browsers, modified dist or a rejected qualification harness, or updated any match row. Reviewed immutable live-image/source binding and an explicit coordinator execution grant are still required before any such run; no approval or literal-human/live-UI/match-count credit is inferred from these bundle hashes. The independent two-case backend overflow result remains a separate in-process slice.

## Additional Burke overflow replay: retain its distinct intermediate pin

Received September 14, 2026, 08:16 UTC: Burke reports an additional **2 executed / 2 PASS / 0 skipped** replay of the original overflow rows using test SHA256 `9a69cb8d...ea6ba48`, unchanged shared fixture `39531682...aa4ca`, runtime `3766ec17b93f8dd8f2eff2534465c510659aca8837d5c32dd84162c2d0e83a9e` and endpoint `889f4d81d89a1519a0d1511fe0afe027e38907987709e89cd3fa7afb3b26cfb1`. Owner evidence: `session-files/qualification/2026-09-12/burke-rules/results/09-current-ws-overflow.{trx,json,inputs.json}`; supplied TRX SHA256 `7cc571d67eb0a94957ab9b7a3acd119a87a06d5071d7edbafc7436812ca2f319`; supplied owner-manifest SHA256 `8954fb09e7feccb07f2afc9070f13933ea04aed2581b888d480914039d99acf8`.

This is recorded as **Burke-reported execution at that intermediate source/test checkpoint**, not a new Hudson run and not the later strengthened test/source pin. Hudson's independent run completed at **07:36:29 UTC** using strengthened test `8d681f6b...d9e141b`, runtime `b600cf06...371f65`, endpoint `8cd5b64b...161792`, and additionally asserted exact sender-only rejection, success-event silence, unchanged persisted state and actual same-socket discard/next-draw continuation. Both replay histories remain separate from the original targeted-04 real RED; no source, assertion, old evidence or qualification count changes occur in this update. F04, other outstanding gates and image approval are not inferred closed.

## NEW independent public-room restart regression: baseline RED

**One new case executed / 0 PASS / 1 FAIL / 0 SKIP / 0 retry**, September 14, 2026, **08:40:16-08:41:13 UTC**. The failure independently reproduces Apone's application reconnect bug; it is not a persistence-file/key failure, timeout, or formal review rejection. Post-fix execution awaits a coherent Bishop endpoint/persistence recovery pin and will use a fresh evidence directory without rewriting this baseline.

New source: `src/backend/tests/Mahjong.Autotable.Api.Tests/RulesQualification/HudsonPublicRoomRestartIntegrationTests.cs`, SHA256 **`b2d14d342b316f36f5ba5a8b726635a27ccab83c3d61d03a8e1c4e0cb1d704d5`**. The shared fixture remains read-only, SHA256 `39531682d5967dce8014c560d95cc52b637f47e74134ac18313055fe036aa4ca`. No production, old helper, frozen audit, live/Docker/primary, root-script or rejected qualification-harness changes.

### Actual nontrivial persisted state and failure

Four genuinely authenticated WS seats played through **45 normal gameplay commands**, using seed **20261652**, cap 4 and BaseUnit 7, with no direct gameplay-state or binding injection. The checkpoint is **hand 2 / turn 6 / version 82 / active seat 0 with its own draw / wall 51 / scores `[0,0,-42,42]`**, with all 108 physical IDs conserved. It cannot pass by recreating an identical initial seeded hand.

Public alias `hudson-resume-0630dd9d6ba54f2fbeaac7944bab29cf` originally mapped to runtime **`0c695d25-258d-49c9-91b3-c4837e38f13b`**. A second genuinely created/dealt foreign-owner room supplied the privacy control and second stored runtime **`b1f2c6fd-9479-4426-9fbc-dd78f0031f44`**.

The test disposed the original peers and replaced the WebApplicationFactory/TestServer, runtime and connection manager with distinct instances, using the **same isolated SQLite store/key**. It reuses the **host-one signed credential verbatim**, not a newly minted post-restart credential. Before alias rejoin, both existing rows and both full gameplay-core snapshots hydrated unchanged. The same signed owner's public-alias JOIN plus standard seat reassertion then bound the room to **NEW runtime `f5762dfd-9ada-4979-9c7d-ef6b954bf698`** and increased runtime rows **2 -> 3**, retaining both originals. The precise failure is original-runtime-ID equality at line 93.

The test contains the required legitimate-owner projection, foreign observer/occupied-seat non-disclosure, second owner recovery, same-socket cross-room stale-entitlement rejection, and actual resumed-discard controls. **Those suffix assertions were not reached after the baseline ID failure and are not credited as passing.** The post-fix test also requires creation-latched BaseUnit despite different rejoin query values, exact preserved wall/turn/scores/owners, and no new runtime DB rows.

### Sealed reproducible evidence

Root: `session-files/qualification/2026-09-12/hudson-actions/public-room-restart-01/`.

- `handoff.json` SHA256 **`ae711c2208b60a2979f5ee6f99d2998dbfee2dcd6464cf7e92b07a7b3817b437`**.
- Actual one-case TRX `results/public-room-restart.trx` SHA256 **`725c3aa239ae0a1bb85415cc828625513360ec9fb9874403c9246b9d1ee3b800`**.
- Compact actual before/after state/ID/row observations: `observed-failure.json`, SHA256 `cd317deb67f164f133aa4aadd1f570dbd492c917cab37dc473b1ef4af935c2f4`.
- Separate **855-file** evidence seal SHA256 **`b41608dec1b15acae627f3a02803f63f627e3cf6b04d1a9b311d833e4ac521d9`**; all entries matched.
- Tested runtime SHA256 **`5491877bc9138a0ddd860f5072ae38506d35dc236853ffcdeb962a068a241dfe`**; endpoint **`8cd5b64bbb64c39c5239a9b6bb5fd1323c809b00d58e261e0c20af7394161792`**. All **1002** inputs matched START/END; four diagnostic source copies match the compilation manifest.
- Exact command in `command.txt`: existing test project, Release, fresh isolated artifacts, `--no-restore`, `-m:1`, only filter `FullyQualifiedName~HudsonPublicRoomRestartIntegrationTests`.
- Apone's original `room-binding-result.json` was read/hash-verified as **`d47c7487097db92d87815bb95f2f598b9dc2f19ff30fbfbefde38d81b07b3996`** and copied with his blocker report into read-only provenance. No old evidence was edited or relabeled.

Bishop owns recovery implementation. All own sockets/hosts closed and the isolated fixture DB was removed. A real missing-assets NETSDK1004 failure preceded restore; no dependency manifest, existing timeout/skip/retry, Git/index or live environment was changed. **One scored fixture hand is not a completed match: zero qualification SQL updates and zero credit toward 120.**

## NEW additive claim-context WS regressions: 12/12 PASS

Fresh Hudson execution September 14, 2026, **10:08:54-10:09:51 UTC**: **12 executed / 12 PASS / 0 FAIL / 0 SKIP / 0 retry**. Only NEW `RulesQualification/HudsonClaimContextWsIntegrationTests.cs` was added, SHA256 **`6e95ea37d7492f6369cfdc45a1a3e5a67ef4febb010fe00d366b29e4f586036d`**. Every preexisting test/helper fingerprint captured at start stayed unchanged; no production or rejected UI/counting artifact work.

Coverage: two real discard-created windows in the same runtime have identical legacy source/tile/deadline0/available/chowOptions but versions **5 -> 18** across actual authenticated Hu, next-hand deal, and normal safe rotation. The original packet gets `stale-version` without mutation, then the current pair commits. Two genuinely owned rooms both at version **5** reject the source runtime ID after same-socket JOIN with `stale-game`, then accept the destination packet. Six malformed-context rows (game-only, version-only, null game/version, string version, negative version) explicitly reject and leave the current window usable. A correct string-seat/current-context command honors the middle physical pair. Three both-context-absent rows preserve explicit, omitted and empty tileIds compatibility. Every accepted Chow proves correct physical meld/held inventory and no extra draw.

Fixture limits are explicit: conserved swaps arrange directed dealt hands, and `OpenedAtUnixMs=0` models a legacy/rehydrated unknown deadline after a real discard creates each window. No StateVersion or draw-entitlement assignment, binding injection, fake success receipt or production test hook. The deadline-zero test is not a timer-expiry or restart claim. Its single directed scored hand is not a completed match and receives **zero qualification credit**.

Evidence root: `session-files/qualification/2026-09-12/hudson-actions/claim-context-01/`.
- `handoff.json` SHA256 **`54003ca688c23670dcf3b120cfe38d7bc3ba7d319ee1a3555afb30f69edab076`**.
- Actual TRX `results/claim-context.trx` SHA256 **`0a471c456c3d0860c4acb7693a234b5e6b8759ad196a19ed51e38ac7fe14c9df`**.
- Compact observed envelopes/IDs/versions: `observed-outcomes.json`, SHA256 `de71972a3499e2290b0576aac28c857e0a04adbcb1bfe7eb08cf3003b99ce023`.
- Separate **856-file** seal SHA256 **`b014716df896dd51ff0a10b799c71fde81198426b22fc0c65f9a0d16cc679512`**; all entries matched.
- Tested endpoint **`624222c8e56bd40b4cd70604828ab45eff3bd80ccef3b39fb4a158bb97a51f95`**, protocol **`9ace2b923a4e0bcffa938a6bfc23131511b78f4596f2d84fce10058a025c4470`**, translator **`c98d77ddacaf2fdb92503818948a334784f70affcedbb0cef094306473024f35`**, runtime **`96300e95a417e231aa34faed9fa84163f4a573ba9ea56dfea2e1e1186da7eff5`**. All **1003** source/config/test inputs matched START/END; seven frozen diagnostic copies match the compilation manifest.

All own peers/factories closed and no fixture database remains. The grouped command, raw output, source/DLL identities, and prior-file checks are sealed. No browser/live/Docker/Git action, new package, existing timeout/skip/retry change, or qualification SQL update. This is scoped execution proof, not source/image approval. Legacy commands intentionally do not gain context-aware replay protection. Deferred invalid-Chow competitor/timer behavior, other runtime gaps, UI chooser behavior, permissive post-claim Kong alignment and public-room recovery remain separate gates; no prior result is relabeled by these 12 passes.

## Requested invalid-choice/callback additions: authored, execution blocked at API compilation

Five cases were added to the NEW `HudsonClaimContextWsIntegrationTests.cs`: four current-context invalid tile selections (duplicate/non-sequence -> `invalid-claim-choice`; out-of-range/non-array -> `invalid-claim-command`) behind a distinct outstanding Hu competitor, plus a deterministic stale-resolver callback case. They require unchanged full state, pending queue, live window/timer identity, and successful normal current-choice/Hu-pass continuation. The callback case uses test-only reflection, following existing repository precedent, to replay the real private resolver with an old captured window reference after genuine hand rollover. No pending state is injected, and this is not claimed as a naturally observed timing race.

**No existing source line was modified or removed:** the diff is insertions only; all original 12 test bodies and helpers remain byte-identical. Previous file SHA256 `6e95ea37d7492f6369cfdc45a1a3e5a67ef4febb010fe00d366b29e4f586036d` is preserved; NEW current hash **`ecd697ee45f061d28a8d5004822bc24ba3ac6e606664f60d459624aea80dfba9`**. The planned grouped class contains 17 cases.

The attempt at **September 14, 2026, 10:29:02-10:29:20 UTC** stopped before test execution because the in-flight production recovery partial failed to compile:
- `ChangshaGameRuntime.PublicRooms.cs:112` **CS0103**: `WriteSnapshotAsync` does not exist.
- `ChangshaGameRuntime.PublicRooms.cs:182` **CS1501**: no three-argument `OpenClaimWindowAsync` overload.

**Actual executed: 0; no TRX.** This is a compilation-incoherent recovery checkpoint, not a failed result from the new cases or a formal review rejection. API runtime source pin `70aa2aa4aac510b74a4cb6af64f800148f34ba9ce385fd6dd8787ef77d42e88c`; partial pin `a3b62ec1460cf359634b475c33fa85c4de5556a3fdf8499d7d6160c86959ebc6`; endpoint `624222c8e56bd40b4cd70604828ab45eff3bd80ccef3b39fb4a158bb97a51f95`. All 1006 recorded source inputs were stable during the failed attempt. No older binary was substituted and no production source was changed by Hudson.

Sealed packet: `session-files/qualification/2026-09-12/hudson-actions/claim-choice-callback-01/`. `handoff.json` SHA256 **`03a0b6ba4dc99de55ecafe59c998e16edee084b8a99bcb62236a7b277d560854`**; additions diff **`24f99c4cfff8a931893c761978418847ce0bb1a89fd1d59945735155eeab62c7`**; additions-only proof **`9f7ae8b56b766cf9b5999d075dc5399d56ddaf578686efcfc931ab7a3f97395e`**; compiler log **`02a4d4c128dfc75fe83cf99d74456bcd0ff0ea2236ad54f1fbcdd1f45aee1aa3`**; 59-file seal **`6f9081d64869f77e971fe47e13f55c5ef8fc6699d24fdb672c12a01538554984`**.

The prior 12/12 evidence remains unchanged and does not count as execution of these additions. Bishop owns production coherence; a new coherent pin is required for the same 17-case grouped execution in fresh evidence. Shared/frozen tests, rejected UI/counting artifacts, live environments, Git/index and qualification rows are untouched. No new game, source-review or image approval credit.

## NEW coherent execution: restart PASS, 16 claim cases PASS, callback fixture corrected and focused replay PASS

The regression files already existed; no duplicate strict-context file was created. Once the missing recovery methods were present, Hudson executed the unchanged 17-case claim class together with the unchanged authorized public-room restart case.

**Actual grouped run, September 14, 2026, 10:40:49-10:42:26 UTC: 18 executed / 17 PASS / 1 FAIL / 0 SKIP / 0 retry.** Claim class: 16 PASS, 1 new callback-fixture setup failure. Public-room restart: **1 PASS**, using its exact baseline test SHA256 `b2d14d342b316f36f5ba5a8b726635a27ccab83c3d61d03a8e1c4e0cb1d704d5`.

### Public-room restart gate: actual unchanged post-fix success

After 45 normal WS gameplay commands, checkpoint **hand 2 / turn 6 / v82 / wall51 / BaseUnit7 / scores[0,0,-42,42]** survived fresh host/runtime/manager replacement with the same DB/key and exact original signed credential. Alias `hudson-resume-25e1542161ec44208a42e101efee0c5c` rebound to the SAME runtime **`e46d5796-0780-40de-b1ea-517ba234f6bd`**. Both original runtime rows stayed the complete set (**2 -> 2**, not the baseline 2 -> 3). Different query units did not override creation config. The full legitimate-owner, foreign observer/occupied-seat, second-owner recovery, same-socket cross-room stale-entitlement and privacy assertions all ran and passed. Returning to the source allowed real owner discard **12**, advancing to **v84** without a new runtime row; persistence and all 108 IDs remained correct. The original baseline RED remains immutable. This authorized post-fix restart test was run once and was not repeated during callback repair.

Grouped pin: runtime **`ff757d17bca9c883cf6792f1bf70fb16842ca99f217d9a1e3d76902d2564b18f`**, recovery partial **`1f82a4724622b994806537963ffa7e91c46d787b5657bf6267b64cf38a9c6a25`**, endpoint **`edd0f7918a0b7f840ecede4cbe90dac17530adf3ce442818ce5edf5d0a1c1fd4`**; all **1012** inputs stable START/END. Evidence `claim-recovery-coherent-01/`: TRX **`a71f438850dd195a92bd87cd8ed36af283a75effc39e1dea5d7eebc92e7a5ef6`**, handoff **`dc3134732ca3834d8ff29531fe7fcac73d312dda0460c6071b4cad8aa93d1c66`**, 856-file seal **`0e7d7d4e683030ea64eda0e3adba479bd211a28b19a656586d5843c1489ffeea`**.

### Callback fixture correction and exact focused outcome

All four new invalid-current-choice rows passed in the grouped run, including unchanged pending queue/window/uncancelled timer behind an outstanding Hu, followed by actual current-choice/pass completion. The only failure occurred BEFORE invoking the stale resolver: the new callback scenario won at seat 2, but reused an existing helper specialized to dealer 1. This was a deterministic **fixture assumption**, not a product callback failure or a flake. Only this new setup and a new private rotation helper changed to use the runtime's actual dealer and real discards/draws. No dealer/draw-entitlement/version assignment, timeout change, assertion weakening, or change to the other 16 case bodies/old helpers.

**Focused callback run, 10:46:41-10:47:21 UTC: 1 executed / 1 PASS / 0 FAIL / 0 SKIP / 0 retry.** Actual old/new window versions **5 -> 16**; replay of the old captured window reference left the new pending choice, complete state and uncancelled current timer untouched. A real current Hu pass then completed the queued Chow. This remains a deterministic test-only reflection replay of the real resolver, not a claim of a naturally occurring callback race.

Current `HudsonClaimContextWsIntegrationTests.cs` SHA256 **`2722b7305de17947e29a6b0e2497b3af19d1c630727f0d7bc6b145db16b1466a`**. Exact correction diff **`82bb2298aace4fc3371eedbf58fea755e147b2770982f88078aeae1412f5d048`**, scope proof **`a01ac89d97a7464b5af74cb58aad455d6a4d2b51818cd8b7306cbca0fb235bdd`**. Evidence `claim-callback-focused-01/`: TRX **`cc34644efc39a709da0b9de72fa787c93b435edd09008ca36d5b3bea00b710e5`**, handoff **`ceb1449e9b71ecce519933f364825c11e1675ab27d7fac17bac9482d287d838a`**, 858-file seal **`17a4d8918f079b3805ced52656907d0f3b7224d2f4cd2a87b566b908b5720eb8`**.

**Source-drift qualification:** the focused run's raw END guard failed for one later workspace endpoint edit; that failed log is preserved, not ignored or rewritten. Its actual Portable PDB document checksum is **`64c45c5ffe86add79df9953494b2799f9e86dc0dda224621021a057f9b3bc0b1`**, matching the frozen compile-start endpoint. The PDB GUID/stamp match the API DLL CodeView record, and the API DLL beside the executed test assembly is byte-identical to that project output. Thus the actual compiled endpoint is precisely bound despite the later workspace change. All other inputs matched. `endpoint-drift-reconciliation.json` SHA256 **`bb91d60566c151a87d327560efc1fd274d8c760362ce3e5965b5f1b8361befb0`**; no binary substitution, new dependency, test relaxation or additional rerun was used.

Accounting stays versioned: grouped **17/18**, then corrected callback **1/1**; **not 18/18 rerun at the final test/source hash**. The separate endpoint checkpoints are retained. No old evidence, production, shared/frozen fixture, rejected UI/counting artifact, live primary, Docker or Git/index change by Hudson. All own test hosts/sockets closed and no fixture DB remains. **Zero qualification SQL changes or completed-match credit.** Independent review/image/UI qualification remain separate; permissive post-Pung/Chow Kong availability alignment was still pending at the last scoped readback.

## Explicit post-claim policy-case alignment; exact new results and protected bot-oracle boundary

Under the September 14, 2026, **04:00 PDT / 11:00 UTC** user grant, Hudson revised ONLY the superseded policy method in `HudsonOwnTurnWsIntegrationTests.cs`: old `PungWithoutOwnDraw_PreservesDiscardClaimButCannotDeclareOwnKong` -> new `PungWithoutOwnDraw_AllowsLegalKongButRejectsSelfHu`. Old source **`db3276e046dcad57bfd059e1c90ddcaaa85691b7c8dbbd62384a639c11f3f980`** and its original 45-pass evidence remain immutable. NEW file SHA256 **`b3aa25bdf2104d648902620bc375303897a72f990d52bac708d506dcccec0195`**. Every byte outside that method is unchanged: all other 44 cases, helpers, pre-draw/ownership/version/privacy controls are preserved. Canonical remains its separately authorized narrow hash `f0ebf41a...e4dde4`; Boundary/OwnTurn classification and original ten defect bodies remain unchanged.

The case reuses the same read-only `pung-no-draw` transition fixture as Vasquez's `CurrentProfileKongGateParityTests`, not a duplicate full fixture or a parity-suite revision. Five local conserved swaps make the post-Pung hand genuinely structurally winning while retaining quad `[40,41,42,43]`; it has effective14 and no own draw. This isolates the separate negative: a real WS fake self-Hu is rejected with unchanged state. The intended positive now requires advertised legal Kong and actual normal WS consumption/back-replacement, rather than the superseded hidden/rejected expectation. Independent exact-diff review remains required.

### Actual grouped execution, September 14, 11:08:40-11:09:41 UTC

**3 executed / 0 PASS / 3 FAIL / 0 SKIP / 0 retry**, with different classifications:

1. **Policy case: current WS alignment RED.** Game `1f97edd9-08d6-44bf-883b-73b786dfe1a8` reached real post-Pung effective14/structuralWin=true/ownDraw=null; fake Hu rejection and unchanged-state assertions passed, and the pure concealed-Kong candidate was legal. Actual `ownTurn[0]` was **null**, so the advertised-choice assertion failed (expected Object). The Kong command/replacement suffix was not reached or credited. Bishop must align availability with the explicitly ratified retained policy.
2. **Two frozen stock-bot cases: changed strategy precondition, not a reproduced stall.** The real `RunBotTurnAsync` body had changed and contained canonical Hu validation/discard fallback, so the user's source-revision prerequisite for replay was met. The unchanged bot test remained SHA256 **`10f9b705e4a000c56ff30ef13cfe737571c32930145fc5d75b67b8b0e78d332d`**, including its 10-second budget and all autonomous-action assertions. Both actual Pung/Chow melds occurred, but line 117 expected the stock strategy to propose DeclareWin; current Medium now itself uses `CanDeclareSelfDrawWin` and proposes **Discard**. The test therefore stopped BEFORE its autonomous-discard/next-draw wait. No timeout or old bot-turn exception was observed. Neither successful continuation nor a regression is inferred from that proposal alone. **No protected assertion was edited, weakened, or bypassed; no bad strategy was reintroduced. Coordinator/independent disposition is needed for this now-obsolete proposal precondition.**

### Exact evidence and pins

Root: `session-files/qualification/2026-09-12/hudson-actions/post-claim-kong-policy-01/`.
- `handoff.json` SHA256 **`f23ab428ee57d170e84e71970720eea8697fa2536f8fe770002217684512f94b`**.
- Actual three-case TRX **`8a3438c3b61a96e5b925533c9044e7cc8708d64c53df81190294e25b98a51abb`**.
- Exact policy-only diff **`eddd7a1d3941efebd1d6f3a609ea950935caeb0e4e812d050eb46f9ca2df912b`**; scope proof **`c60d2572e88a92bee036ddcfd4eec7340a01886a09e50008a9e4e692b1e881fc`**.
- Compact actual observations **`2cffdd323ad668060ca4826e2eecb14b5173aadadf4f17d928a8506b5474499a`**.
- 862-file seal **`2f7c9a53e09cb1eaf217e2cfafaa55f3e0dd306d1b63c22b45ff384e83b3a0b5`**, all entries matched.
- Tested runtime **`c07f50decb202dd203f7a80dddc1f379fa6ecf0ed436e1ceea0cbd4bf827ea87`**, endpoint **`752c673ad62b3aa09193d0bd94b9236622e3352a2a49d5b01d8405283c08697f`**, availability **`df0cdcfb42f683d1bceceeaf650805e512b71a6dfa1203095125376d2f15983a`**, Medium strategy **`d1f8e0d54f1cba4fbd7121e0b978d4ce19eedd08a0837b0a698d509e5d8e8657`**. All **1012** inputs matched START/END.

Vasquez's parity suite, the shared transition fixture, frozen bot file and old failure seals remain unchanged. No production, rejected harness, live/Docker/Git/index or qualification-row changes by Hudson. No formal reviewer rejection or self-approval is issued. **Zero completed-match/image approval credit.**

## Separate additional three-method Pung/Kong fixture repair: 3/3 PASS

Under the explicit September 14, 2026, **04:17 PDT / 11:17 UTC** grant, only the three named methods in `Changsha/Acceptance/PungPromotionToKongTests.cs` changed. Original SHA256 **`1ec8801cdf94bdd9e1558d0d4a0cc391b175198d4ba6bf768411c078a0a2895d`** is retained immutably; new file SHA256 **`31a0c78fa0d9a6fc04d78b21807e919c9e8c785b57abd9cf223a2bd99ff83a20`**. Every byte outside those methods and every original promotion/replacement assertion is unchanged. No shared/global helper or production edit, direct provenance/phase/turn assignment, skip, timeout or guard relaxation.

- `Pung_Then_DrawMatchingTile_PromoteToAddedKong`: keeps seed13/dealer0 and the original Tong-7 assertion. Conserved placement is followed by an actual public Pung, a subsequent full turn cycle, then a genuine front draw of physical63 at ten -> eleven concealed with one meld. Exact back replacement and 108-ID conservation are asserted.
- `ConcealedKong_FourMatchingTiles_PromotesToConcealedKong`: uses the recorded seed0/dice42/dealer-discard56 natural path. Seat1 draws from13 ->14 while naturally holding a Tong-8 quartet; the original concealed-Kong/count/wall assertions remain, with stronger exact back-draw and inventory checks. No hand/wall writes in this setup.
- `Pung_With_FourthTileInWall_PlayerDraws_It_PromoteAllowed`: replays the exact recorded `added-draw-1` public-transition prefix and performs its final real front draw of physical26. A real Wan-7 Pung has ten concealed before that draw and eleven afterward; promotion and exact replacement then pass. No manual appended fourth tile or hand/wall edits.

**Fresh grouped execution, 11:27:59-11:28:39 UTC: 3 executed / 3 PASS / 0 FAIL / 0 SKIP / 0 retry.** Only these three cases ran. All **1012** source inputs were stable START/END, including unchanged StateMachine **`0c8b838b71746d4e772c651bd278a7d883b650871849f71c6cbfbe780286636f`**, Drake's 26-case source, Boundary/OwnTurn classification, the prior five self-draw fixtures and the separate frozen legacy-claim rebaseline.

Independent review packet: `session-files/qualification/2026-09-12/hudson-actions/pung-promotion-fixture-repair-01/`.
- Handoff SHA256 **`4f6482f0bf66b47dea25a7b465abb5b8125eafc4a17974c2da2f8ef05e0997dd`**.
- Exact diff **`23aeea54f875628d18e90fb9efc11b874a6026243199309128b7fa0b6f290a36`**; three-method/body/assertion scope proof **`c0bee3ec9869bf6a1c835126c7237c4b063c60f226d4c91010c62b4f35b19a22`**.
- Actual three-case TRX **`ed3700252c6df750e0850a15db1ee66e37b04f37435c4a2ed8c727d26e062f0c`**; case summary **`0427746e06635a4901e8475e2cb8ae91c5fb35854c973e6f003917ab9fe29be3`**.
- Separate **859-file** seal **`384d39d6843ca1c555066d129cdc46d8ae3231603577b88c7f81e494b8c4b91d`**, all entries matched.

Drake's handoff/natural traces were read-only and copied as provenance; no rejected engine or harness revision/advice was performed. The earlier five nonfrozen self-draw fixes and one frozen legacy-claim setup case keep their own unchanged evidence/review scopes. **The historical 78-case run was not rerun or declared all green.** No live/Docker/primary, Git/index, qualification SQL or game/image credit. Independent exact-diff review remains required.

## NEW cross-difficulty bot draw gates: 12/12 PASS, frozen Medium oracle untouched

Fresh independent execution September 14, 2026, **11:53:46-11:54:48 UTC**: **12 executed / 12 PASS / 0 FAIL / 0 SKIP / 0 retry**. New file only: `RulesQualification/HudsonBotDrawGateIntegrationTests.cs`, SHA256 **`c1f38423b1bf5463fbac9d03a9fadc7985c75abef79748b961983f261b2a3ff4`**. The old Medium regression remains exactly **`10f9b705e4a000c56ff30ef13cfe737571c32930145fc5d75b67b8b0e78d332d`**; no stale synthetic/frozen test, prior helper, source guard, strategy or domain declaration was edited.

Eight cases cover Pung/Chow post-meld states across **Easy/Medium/Hard/Master**. Each real public claim leaves a structurally winning effective14 hand with no own draw and a genuine pass-Hu flag. All selected strategies decline invalid self-Hu, autonomously discard physical **92** exactly as their own strategy computes, and advance the next human's actual draw **102** to **v8**, conserving all108 tiles. Runtime logs are empty: no error, timeout or discard-fallback downgrade. The test sends no bot discard.

**Setup-selection limit:** the legal Pung/Chow setup choice is controlled through public `Runtime.ClaimAsync` after a real signed WS discard; automatic claim selection is deliberately delayed in the new isolated host. This preserves Easy's rule that it never selects Chow. These rows prove autonomous **own-turn behavior after a real legal meld**, not autonomous Easy Chow selection. Roles are created normally (three signed humans plus bot seat1); no role or draw-entitlement marker is manually changed. Directed tile setup is conserved and happens before the public transitions.

Four positive controls independently prove that each difficulty retains real winning play: an actual normal front draw of tile **0** grants bot1 own-draw entitlement; the runtime autonomously declares **SelfDraw Hu**, scores **+4** with four-seat sum0, and reaches cap1 GameComplete. No bot win is test-issued. These are directed in-process cap1 fixtures, **not four-hand qualification matches**, and count as zero toward120.

Evidence root: `session-files/qualification/2026-09-12/hudson-actions/bot-draw-gates-01/`.
- Handoff **`95944fe808cef973020044f601d2b0a407336bd1aee333d82d6dc37232bfe64b`**.
- Actual12-case TRX **`a81e8823f5c18f9960f161d0963c163b9f7aefc71de3e98585ec572c50289dbd`**.
- Concrete discard/draw/win/log output **`20903889455c7b3bca731523a951ea55da4f7e1f8750769606650e213a0d7904`**.
- Separate858-file seal **`9269313a87b32ede9c127700d4714ad392bf745a0e2ab3c37eb32e4739269da9`**, all entries matched.
- Runtime pin **`0e0e2c3c3f28abc73012df8f5fd064849843b8927a3fe321ca496971ff826c0f`**, endpoint **`752c673ad62b3aa09193d0bd94b9236622e3352a2a49d5b01d8405283c08697f`**; all1013 inputs stable START/END. Exact four strategy/domain/source/DLL hashes and commands are in the handoff and fingerprint manifest.

All prior test hashes and old failure/precondition evidence seals remain unchanged. All own peers/hosts closed and no fixture DB remains. No production, rejected harness, live/Docker/Git/index or qualification SQL changes, no source/image review approval or game credit. The new eight post-claim controls do not retroactively turn the frozen oracle's incompatible proposal assertion into a passing result.

## Owner's 33-case bot replay and preserved-oracle scope conflict

Bishop's September 14, 2026, 12:06 UTC message requests changing the frozen Medium test's old-bug proposal assertion from DeclareWin to Discard. Read-only inspection of the actual owner TRX confirms **33 executed / 31 PASS / 2 FAIL / 0 skipped**, SHA256 **`4ca3be450acfabfcd938699953a88de0e155d35f1c4e029cc960ef52842017be`**. Both failures are exactly the proposal precondition (`Expected DeclareWin; Actual Discard`), not demonstrated timeout/stall regressions.

The coordinator's explicit 04:00 PDT instruction required preserving the bot assertion/test SHA. The working `HudsonBotMeldContinuationTests.cs` still matches **`10f9b705e4a000c56ff30ef13cfe737571c32930145fc5d75b67b8b0e78d332d`**. Hudson has not treated this peer request as permission to override that specific freeze; an explicit coordinator disposition is needed before changing it. No test was edited or rerun in this update, and no new source-review lockout is claimed.

The requested separate new all-tier controls are already implemented and independently executed: `HudsonBotDrawGateIntegrationTests.cs` **`c1f38423b1bf5463fbac9d03a9fadc7985c75abef79748b961983f261b2a3ff4`**, **12/12 PASS** at the sealed `bot-draw-gates-01/` checkpoint. They retain structural-win/no-own-draw discrimination, actual autonomous strategy-selected discards, next draws, inventory/pass-Hu checks, and actual-own-draw autonomous winning controls for all four tiers. The meld-selection setup is controlled (Easy never autonomously chooses Chow), while the discard/win actions are autonomous. This is distinct evidence, not a retroactive green label for the frozen Medium test or Bishop's 33-case run. No match/image approval credit.

## Latest wire clarification: compatibility retained; Kong policy conflict not adopted

Bishop's September 14, 2026, 12:18 UTC clarification confirms numeric required ownTurn version/gameId, optional both-or-neither claim context, UPDATE/actionRejected envelopes, origin-only denial/resync, and empty-Hu-tileIds compatibility. Hudson's existing malformed-Hu case supplies a nonempty array; no assertion rejecting `tileIds: []` was introduced. No requestId/accepted DTO or top-level ACTION_REJECTED message is assumed.

Item 4's blanket own-draw requirement for all ownTurn availability still conflicts with the coordinator's explicitly ratified current-profile Kong retention. Legal owned active/no-window/effective14 post-Pung/Chow Kongs must not require a new draw; actual-own-draw remains mandatory for SELF-HU. The intentionally revised positive policy case stays intact; it is not reverted to the superseded WS-only strict expectation. The last pinned policy test's null-advertisement failure remains valid evidence of the implementation mismatch.

The requested change to the frozen Medium proposal assertion remains subject to the separate explicit coordinator unfreeze request **`5bfc58c1-f0fd-4469-bd97-8d3504ba9ed6`**. The protected file remains `10f9b705...d332d`; separate new all-tier runtime controls are already independently 12/12 passing with their disclosed controlled-meld setup. No code change, new execution, approval, or qualification credit follows from this clarification.

## Explicit Medium precondition alignment completed: full autonomous 2/2 PASS

The coordinator's September 14, 2026, **05:23:23 PDT / 12:23:23 UTC** grant resolves authorization request **`5bfc58c1-f0fd-4469-bd97-8d3504ba9ed6`**. Exactly ONE expression changed in `HudsonBotMeldContinuationTests.cs`: `Assert.Equal(BotActionType.DeclareWin, proposed.Action.Type)` -> `Assert.Equal(BotActionType.Discard, proposed.Action.Type)`. Every other byte—including structural-win/no-own-draw/Pass-Hu setup, actual autonomous claim/discard/next-draw checks, inventories, phase/version/scores/errors and the **10-second** budget—is unchanged. Original **`10f9b705e4a000c56ff30ef13cfe737571c32930145fc5d75b67b8b0e78d332d`** and both real REDs remain immutable. NEW test SHA256 **`7c1571434ecf26ae556089fbaa3d1f537356b0503a4c15a7f988eae032756971`**.

**Fresh exact two-case execution, 12:26:46-12:27:43 UTC: 2 executed / 2 PASS / 0 FAIL / 0 SKIP / 0 retry.** In BOTH stock-Medium Pung and Chow cases, the real bot autonomously claimed the meld, reached structurally winning effective14/no-own-draw state at v6, then autonomously discarded held physical **92**. The next human actually drew **102**, advancing to **v8**, with all108 IDs, preserved Pass-Hu, no fabricated bot draw, no score changes and no bot-turn error. The existing once-per-game omitted-Chow-choice warning remains disclosed; it is not a turn failure. No manual bot discard or win was substituted, and execution did not stop at the proposal assertion.

Pin: runtime **`0e0e2c3c3f28abc73012df8f5fd064849843b8927a3fe321ca496971ff826c0f`**, Medium **`d1f8e0d54f1cba4fbd7121e0b978d4ce19eedd08a0837b0a698d509e5d8e8657`**, endpoint **`752c673ad62b3aa09193d0bd94b9236622e3352a2a49d5b01d8405283c08697f`**. All **1013** inputs stable START/END; all other owned/frozen/shared test hashes unchanged.

Evidence `session-files/qualification/2026-09-12/hudson-actions/bot-meld-postfix-alignment-01/`: handoff **`c0486c184d04ff32731bdd37cbdb66ef3c3185ac9e218d7015939cdc66dab138`**; exact one-expression diff **`6153caf080da90616fbf5d2459c0716281a81df5a8ae9956837f33e438745f54`**; scope proof **`1f80c54d8def681164a9c6fd1e590c2d50ff958bef4aa18d10d4097c3b8c888c`**; actual TRX **`96f951f730897ca2a3ecddd37a1158d2cd26f898748105cded43a6e41bd4b982`**; 856-file seal **`0a53ef9acb66c2a2ef33743d96eb22c0d6bba2c6d7d8f228586094781d30493a`**. All prior evidence seals remain valid. All own sockets/hosts closed; no fixture DB remains.

The all-tier genuine-own-draw winning requirement was already independently covered by `bot-draw-gates-01/` **12/12 PASS**, including four actual autonomous Hu/score controls for Easy/Medium/Hard/Master; those controls were not duplicated or rerun here.

## Complete owned feature/fixture handoff delivered

Consolidated current hashes, scoped results, exact evidence references and remaining blocker: **`session-files/qualification/2026-09-12/hudson-actions/owned-feature-fixture-handoff-01.json`**, SHA256 **`114130e214eb7a0414b6bc3fdf8872f0005f6bf63fa3211e0594522a1425854b`**. It keeps own-turn/BaseUnit, claim context/callback, unchanged restart recovery, stock/all-tier bots, narrow frozen legacy-claim rebaseline, prior five self-draw repairs and the separate three promotion repairs distinct. There is no invented aggregate or claim that all scopes reran at one final hash.

**Remaining implementation blocker:** the explicitly adopted legal ready14 post-Pung/Chow Kong policy still lacked owner WS availability at its last pinned test (`post-claim-kong-policy-01/`); the real normal-WS Kong positive is not credited. Independent source/exact-diff review and live-image admission remain separate. This completed owned handoff does not wait for a live image and claims **zero qualification SQL changes, four-hand match credit, literal-human UI or source/image approval**.

## NEW deliberately-invalid proposal control: fallback recording and autonomous progression PASS

The stock-Medium precondition had already been explicitly aligned and its full autonomous cases passed2/2; no repeat edit or rerun was made. A separate NEW file now covers the requested faulty/custom proposal boundary: `RulesQualification/HudsonBotInvalidProposalIntegrationTests.cs`, SHA256 **`e534345d09b4c8234c7d9045018efbdeea8e6fbeb7b47f7e22b0873387421cd7`**.

**Actual execution September14, 2026, 13:01:03-13:01:44 UTC: 1 executed / 1 PASS / 0 FAIL / 0 SKIP / 0 retry**, unchanged10-second progress budget. After a real signed-human discard and actual scheduled Pung, a test-only per-game strategy deliberately returns an unavailable DeclareWin with bogus tile107/score1234. The runtime was actually invoked once, rejected that intent through canonical readiness, and autonomously discarded held **92**, followed by the next human's real draw **102**, reaching **v8**. No bot win/discard was manually issued; no role/draw marker was fabricated; Pass-Hu, scores and108 inventory remained correct.

Under the runtime lock, the recorded `LastBotDecisions[1]` was verified as **Discard / Action.TileId92 / Tile92 / Score0**, NOT the invalid proposal. Its reasoning contains the original fixture reason followed by exact `runtime: self-draw unavailable; deterministic discard fallback`. The explicit warning was observed, with no bot-turn error or timeout. This is intentionally **injected-invalid-proposal evidence**, not claimed stock-bot misbehavior or a 120-match occurrence.

Proof root: `session-files/qualification/2026-09-12/hudson-actions/injected-bot-proposal-01/`.
- Handoff **`e3defdbcc32d9af71ba153edbcf1e96cd1d715cb623b198caa5d80db7f9c07ae`**.
- Actual TRX **`d1f49119ed9f1b908503c20e41ff0b913ab0a01f070582969438dbac06beb950`**.
- Observed outcome/recording/warning **`166585df1029e7a104483975aef9966e5975609cf5e3dad7595afd76992fd8b1`**.
- 853-file seal **`55e3ac12977a538c609fedc039018b2566683b5e6de48d5583fdcc5643fbf9c6`**, all matched.
- Actual runtime pin **`641e75140200726f50738b2e5ffd9d07c5b2c5e9afc4f9d37dd1d9dfd07fead3`**; all1014source inputs stable START/END. Source/DLL/test/command identities are sealed.

Existing stock file remains7c157143…56971 and all-tier file remainsc1f38423…a3ff4, with their prior2/2 and12/12 evidence untouched. No production/Burke/frontend/old-harness or frozen-fixture edit, live/Docker/Git action, new agent/factory, source/image approval or qualification credit. All own sockets/host closed and isolated DB removed.

Updated consolidated owned handoff is versioned separately: **`owned-feature-fixture-handoff-02.json`**, SHA256 **`24a76705a3537f2da634ef404188498d3f6f649dff4c6de5b3921c3c16b1e6c3`**. Previous handoff01 remains immutable; no aggregate all-at-one-pin passing claim or changed status for the separate post-claim WS Kong alignment blocker.

## Authoritative new current closeout pointer

The final bounded closeout is now **one fresh current source-capture run:169 executed /169PASS /0FAIL /0SKIP /0retry**, not the older mixed-version ledger. It includes the previously blocked normal-WS post-claim Kong positive, all three current-profile parity rows, strict claim/retry/old-window cases, stock and injected-proposal bot progression, zero-wall/offered boundaries, whole-command overflow, unchanged public-alias recovery, the original10 defect bodies and every explicitly authorized fixture correction. No selected production failure remains in this tested capture.

Current handoff: **`sessions/2026-09-14-hudson-current-closeout.md`**. One concrete source/test/diff/result manifest: **`session-files/qualification/2026-09-12/hudson-actions/current-closeout-01/final-manifest.json`**, SHA256 **`154dc81c3565c884e6030b386cc1ed37dbfbd7ef46966bd1f7d8da3f837ca618`**. Actual TRX SHA256 **`a46297c16f143ffd21ab111f0d5f614234d2f4672eb5ba7efaee6e5da50b5c93`**; full1975-file seal **`2dbee805f8701643bf4f0d135861b09206322d80690f45412f07918d3390a415`**.

Inputs were captured byte-identically at2026-09-14 14:29UTC into a read-only snapshot; execution14:35:45-14:41:43UTC used that snapshot and its verified content root. All1108 captured files stayed unchanged. Later workspace changes are not silently included. Original74/76, overflow2/2, bot/recovery REDs and78/424 histories remain intact and are not combined into169. No new source/test rebaseline or live/browser/cohort operation. Independent review/admission and host execution remain separate; **qualification0/120**. The manifest explicitly distinguishes captured frontend69dad4da from the referenced olderb237/7a4ee030 hold pin, without claiming frontend approval.


## September 14, 2026: separate claim-Hu settlement extension FINAL

Fresh grouped6-case run at runtime14b12539: **4 PASS / 2 FAIL / 0 SKIP / 0 retry**. All four authenticated discard/rob-Kong overflow rejections preserve whole live/persisted state, pending/window/timer identity and actual Pass/unchanged30-second timeout continuation. Both positive settlements reach GameComplete with exact2/7 payments but lack contractual SignalR WinDeclared; captured claim resolver omits the emitter. No test weakening, production correction, browser or qualification operation occurred. Different eligible runtime revision author required; Drake proposed only pending coordinator grant. Full handoff: `sessions/2026-09-14-hudson-claim-settlement-overflow.md`. Manifest: `session-files/qualification/2026-09-12/hudson-actions/claim-settlement-overflow-01/final-manifest.json`, SHA256 `fd57eb6c1ca7cc011d360cf8369c999c98e1b19587cdf0abc2e60fb2f8f2681b`. Original169/169 remains separate and unchanged;0/120 remains. Evidence explicitly preserves the source-capture read-only-declaration discrepancy: actual files0777, full bytes/inventory stable, no filesystem-immutability claim.


## September14 public-binding boundary extension FINAL

NEW `HudsonPublicRoomBindingBoundaryTests.cs` SHA417cab24794f2d61705d16924f0554a0649afdef200096b56c9e480134c744cb: **7/7PASS,0fail/skip/retry** on captured runtime14b12539/public-room partial948237a6/endpoint752c673a/engine0c8. Actual-created persisted aliases with missing/corrupt snapshots reject JOIN and NEW without replacement; legacy ordinary JOIN refuses ambiguous recovery and genuine NEW creates a distinct runtime; all four relay variants preserve real forwarding/seat echo and unchanged persisted Changsha state after restart. Original credentials are reused; offline fault injection occurs only after the old test host stops in its isolated database.

Current manifest: `session-files/qualification/2026-09-12/hudson-actions/public-binding-boundaries-02/final-manifest.json`, SHA3edb576efe37c35449b67071d0251f3883fd1e6c6250fe1cbe3bd330fa740648; TRX2308f5de70707d62f68248648557d3507639c60b77f387b4f5771c05031f5f48. Full handoff: `sessions/2026-09-14-hudson-public-binding-boundaries.md`.

Initial seven-case run6P/1unsupported-implicit-JOIN-expectation failure remains preserved in public-binding-boundaries-01. Only the NEW legacy method was corrected to require exact refusal and explicit NEW; other six case bodies, all helpers and production source are identical. Existing scored-state/ownership testb2d remains unchanged; its169-case PASS is provenance, not an eighth current case. No frozen packet/Drake corruption/existing relay replay, old evidence rewrite, production/dist/live/Git action or acceptance transfer. Independent NEW fixture review and admission HOLD remain; missing-WinDeclared RED unchanged, qualification0/120.


## FINAL current native consolidation — one actual65-case run, not historical aggregation

Completed the latest authorized native slice on one fresh, stable1134-file capture. **65 executed/63PASS/2FAIL/0SKIP/0retry** at2026-09-14 21:27:55-21:36:50UTC. Current source pin: runtime9d180a27/public partial5ec41a5b/engine4070/Hubbb293/availabilityeee17/endpoint752. Stock full10s continuation2, faulty-proposal fallback1, all-tier12, WS policy1+parity3, claim-context17, own-Hu overflow2, all four discard/rob-Hu overflow negatives, scored recovery1, independent room helper9, same-alias isolation6 and legacy-Hub5 pass in THIS run.

The only two execution failures are current positive discard/rob-Kong Hu missing SignalR WinDeclared at unchanged testline189; real terminal state, exact winning tile and2/7 payments already pass, but the later final inventory/persistence suffixes are not reached. Fenced observer gets ScoringComplete/GameEnded/GameCompleted. This is the actual current product notification blocker, not policy pending or old bot/helper failure. No tests/assertions/timers/source were edited or retried. Drake's frozen5ec helper passed9 here but its supplied Ripley review/HOLD is not cleared by original-author execution.

Primary immutable manifest: `session-files/qualification/2026-09-12/hudson-actions/native-targeted-final-02/final-manifest.json`, SHA **ba45270bcf076221013dc9218bcac31b95ee68546971748f91e7e6184b416afe**. TRX **b49316c8fbcac25d6994c3f114eb5cbfe8b6623682902f142a89b77e268556d4**;2017-file seal **b90d77283570cf90348a513fbf28f7a22028a2100ce835b07b880aa5392ee47b**. Full source/test/diff/actual-result handoff is `sessions/2026-09-14-hudson-native-targeted-final.md`. First incomplete in-flight capture is retained with0 tests; all current captured inputs and bound API/content root matched, no owned DB leftovers. Later workspace AppDbContext change was not substituted.

Settled Canonical13/five/three-promotion fixtures were not selected or reworked. UIr6/protocolv7/shared3.7/frontend creation, Docker/live and other lanes remain separate. No old run contributes to65, no source/helper/image approval or qualification credit;0/120.
