# Hudson: public-room binding boundary extension

**FINAL: seven NEW cases executed, 7 PASS / 0 FAIL / 0 SKIP / 0 retry.** No production source or existing test/helper was revised. This is a source-only TestServer extension, not a live image, new scored-match run or qualification admission. **Qualification remains0/120.**

## Concrete current handoff

`session-files/qualification/2026-09-12/hudson-actions/public-binding-boundaries-02/final-manifest.json`

- Manifest SHA256: **`3edb576efe37c35449b67071d0251f3883fd1e6c6250fe1cbe3bd330fa740648`**.
- Actual TRX: `results/public-room-boundaries.trx`, SHA256 **`2308f5de70707d62f68248648557d3507639c60b77f387b4f5771c05031f5f48`**.
- Complete1967-file seal: `evidence.sha256`, SHA256 **`78fd4cf9c5e31287fd3dbf1e90d95a7783dd4c0256f12309f06d6131ac2d1f60`**; all entries matched.
- NEW source: `src/backend/tests/Mahjong.Autotable.Api.Tests/RulesQualification/HudsonPublicRoomBindingBoundaryTests.cs`, SHA256 **`417cab24794f2d61705d16924f0554a0649afdef200096b56c9e480134c744cb`**.
- Exact full addition is `new-test.diff`; the bounded first-version correction is `legacy-intent.patch` and `revision-scope.json`.

One grouped command ran **September14,2026,17:02:32-17:03:37UTC**, including compilation. It used the captured existing test project, Release/net10.0, isolated artifacts/results, one MSBuild node, unchanged serial xUnit configuration, exact filter `FullyQualifiedName~HudsonPublicRoomBindingBoundaryTests`, and no retry. Full command is `results/test.command.txt`. Existing dependencies were restored only after the recorded NETSDK1004 missing-assets failure.

## Exact execution pin

| Input | SHA256 |
| --- | --- |
| Runtime | `14b12539b672f3e0f45364e1dbcda5ab703d5c727c606255a206ae7553691fd2` |
| Public-room runtime partial | `948237a651beabfde4d474e277776e2b68fe0fb209f8f9f026f87549251d2cbd` |
| WS endpoint | `752c673ad62b3aa09193d0bd94b9236622e3352a2a49d5b01d8405283c08697f` |
| Shared engine | `0c8b838b71746d4e772c651bd278a7d883b650871849f71c6cbfbe780286636f` |
| Loaded API DLL | `2b6f549c3f25e81654c7af83f1c091470cb7c8c949ecc510cc7194aeb20620ad` |
| Unchanged shared Hudson fixture | `39531682d5967dce8014c560d95cc52b637f47e74134ac18313055fe036aa4ca` |
| Unchanged existing scored-restart test | `b2d14d342b316f36f5ba5a8b726635a27ccab83c3d61d03a8e1c4e0cb1d704d5` |

All1111 captured files and the complete inventory remained unchanged. MVC content-root metadata points to the captured API project; its built API DLL matches the dependency beside the executing test assembly. Both trial snapshots have identical production source inputs; separate builds have separately recorded DLL identities and are not claimed to be the same immutable image. Filesystem write protection is not claimed. No owned runtime DB/WAL/SHM files remain.

## Seven actual outcomes

All cases create the original public alias and binding through actual signed WS clients. Four humans seat normally with seed20261652/BaseUnit7, auto-deal, actually discard55, and advance to turn2/version5/wall54 with inventory108. This is not a direct binding insertion or a fabricated win/hand. The original credentials are retained verbatim across the same database/signing-key factory restart.

| New case | Observed result |
| --- | --- |
| Actual-created binding, stored runtime row missing | Both JOIN and NEW reject `room-snapshot-unavailable`, close1011, emit no JOINED/full projection, retain the original binding and create zero replacement rows. An unrelated healthy alias remains usable. |
| Actual-created binding, malformed stored JSON | Both JOIN and NEW reject `room-snapshot-invalid`, close1011, expose no private canary, retain the original binding/corrupt JSON/version/timestamp and create zero replacement rows. Healthy alias control succeeds. |
| Legacy alias binding absent | Plain JOIN with the same alias/seed/original signed owner rejects `legacy-room-binding-unavailable`, preserving all storage. Only genuine NEW followed by seat creation creates a distinct runtime; the old snapshot remains unchanged. No guessed restoration. |
| four_player after restart | Actual origin seat echo and peer cosmetic UPDATE; no runtime binding for the relay room, new Changsha rows or mutation of the owner's persisted game. |
| three_player after restart | Same complete relay/storage/identity invariants pass. |
| bamboo after restart | Same complete relay/storage/identity invariants pass. |
| minefield after restart | Same complete relay/storage/identity invariants pass. |

For negative storage cases, the old host's **ApplicationStopped** event supplies the actual offline damage boundary before the replacement factory starts. The callback verifies its database path lies under this fixture's isolated `test-data/hudson-own-turn` directory. Missing-state fault injection disables foreign keys only in that offline isolated connection so the real binding is retained while its snapshot row is removed. This is explicit negative-test database damage, not qualification gameplay injection. No BindRuntimeGameForTest or seed/owner/timestamp matching is used.

## Original trial and exact correction retained

Initial `public-binding-boundaries-01` ran **16:53:07-16:54:02UTC**: **7 executed /6PASS/1FAIL/0skip/0retry**, source6262dfd1. The failure was a NEW-test assumption that ordinary ambiguous legacy JOIN should succeed and permit fresh seat-driven creation. Actual source correctly refused with `legacy-room-binding-unavailable`; runtime PublicRooms:175-196 distinguishes ambiguous recovery from explicitNew, and the endpoint retains NEW intent for the same room.

Only the NEW legacy method was changed: assert that exact refusal and unchanged storage, then send genuine NEW before the original distinct-runtime/old-snapshot-preservation assertions. Every other byte outside that method, including the other six case bodies and all private helpers/time budgets, remains identical. The correction strengthens explicit user-intent and no-guess checks; it is not a production correction, hidden retry, old-fixture rewrite or self-approval of a rejected artifact.

Initial manifest **`b001b2eff2dc27e8aa7a2c83ab54cbcfd0f4cab01c5c3b2745c6908d8c0d931d`**, TRX **`87904056a9d10917b87da6ed435b904af2204ba705e666934b9a590ed9691fab`**,1964-file seal **`28371fedfd3e65c8c93ec73b8efdee0a270f8fcf120a77bfc63ecdc2dd96666d`** remain immutable records of that first result. No result was relabeled.

## Existing requested scored recovery is separate, not duplicated

The existing `HudsonPublicRoomRestartIntegrationTests.cs` already covers the requested same-cookie/same-DB/key, nonzero-scored progressed-state recovery, zero new runtime rows, wrong-owner/occupied-seat/cross-room private projection, second owner and actual resumed discard. It was not edited or rerun here. Its exact PASS inside the preserved169-case packet is copied only as provenance in `prior-scored-recovery-provenance.json`:45 normal WS actions reached hand2/turn6/v82/wall51/BaseUnit7/scores[0,0,-42,42], rows2->2 across restart, then owner discard12/v84. That execution used runtime641e7514, so it is not an eighth current case or a result transferred to14b12539.

The current source already contains the durable binding/public recovery seam. This new work does not implement it again. Retained pre-fix Apone/Hudson alias-loss evidence stays RED at its old pin.

## Remaining gates

Independent review of NEW test417cab24 and its bounded correction remains required; no fixture/source/image review is self-approved. Frozen review packets, Drake's separate corruption suite, existing relay suite, prior169 result and old counting/UI artifacts were neither replayed nor changed. Source/image/cohort admission HOLD remains. A future granted integrated selector is `FullyQualifiedName~HudsonPublicRoomRestartIntegrationTests|FullyQualifiedName~HudsonPublicRoomBindingBoundaryTests`; it was not executed as an8-case set here. The separate missing-WinDeclared notification RED is unchanged. No Docker, live host, frontend rebuild, primary8950/key/database, Git/index or counted-game operation occurred.


## CURRENT UPDATE: explicit both-table strengthening is HARNESS RED

The later concrete seam requirement prompted **additive** assertions in the NEW boundary file: compare exact ChangshaGames IDs, total binding counts, binding identity and version directly before/after restart, plus stable authoritative state including wall/hands/events/scores/creation conditions and saved human identities. Every prior source line/assertion remains in order; no timer/skip/retry change, production edit or frozen-helper revision occurred. Current NEW test SHA **`6d15f8aacb175150875f4645688838424a40cdbf1192d9437a9c4c87c4d09fd5`** supersedes417 for the strengthened source;417's7/7 remains its separate historical result.

**Current execution: September14,2026,17:57:30-17:58:26UTC;7 executed /6PASS/1FAIL/0SKIP/0retry.** Missing-state, legacy no-guess/explicit-NEW, and all four relay variants passed the stronger metrics. The corrupt-JSON case stopped at **line58, `Assert.Equal(1, faultsApplied)`, expected1/actual0**, after factory restart and before damaged-state assertions or either recovery request. Its ApplicationStopped fault-injection action did not reach the completion counter. The output does not distinguish a never-invoked callback from a callback failure before the counter; the underlying cause is **not established**. This is a **harness setup failure**, not evidence of corrupt-state acceptance by the product. No current corrupted-snapshot PASS is claimed and no retry was performed.

**Helper reliability REJECT/HOLD:** the affected NEW fault-injection helper is Hudson-authored. Hudson is now locked out of further revision/advice for that rejected helper cycle. A DIFFERENT eligible revision author is required; **Drake proposed**, pending coordinator eligibility confirmation and an exact-file/helper grant. This record grants no write authority. All current assertions and budgets remain frozen; none is removed to hide the setup failure. The previously completed fault callbacks in01/02 remain genuine historical observations, not a replacement for the failed current setup.

Primary current packet: `session-files/qualification/2026-09-12/hudson-actions/public-binding-boundaries-03/final-manifest.json`, SHA **`edb08a1d878cb06aeed293a4976e6fce870b67f8e721f5df39d3ca4bd91e9c47`**. TRX **`d42eca04f02c028cb67fc1b14068557b57eccfe18ecef02a98a7c7057204704d`**. Full1965-file seal **`c4e835b6a0c7982e475cea8e79cf7ca21b28ccdad512950e9a354b8d5b5bb736`**, all entries matched. Loaded API DLL **`da62eaa4cbd41b1487e9ae1bd363e686cd7c78ba97cc014bbb9726632893920b`**. Production source remains runtime14b12539/public-room partial948237a6/endpoint752c673a/engine0c8, identical to02; all1111 captured input files and protected tests match, with no remaining owned runtime database files. Physical filesystem write protection is not claimed.

The new seam's atomic initial ONE-SaveChanges/publication boundary, cross-provider ordinal/unique-runtime constraints, conflicting later creation-config writes, bot-fill reservations versus ordinary disconnect release, and first viewer snapshot versus resumed bot/timer ordering remain **unproven by these seven cases**. Matching a described API seam does not grant those proofs or production review. Existing scored-state/ownership169 evidence remains separate, the missing-WinDeclared product RED is unchanged, and source/image/cohort admission remains HOLD with **qualification0/120**.


## September14 additional owner55 report and changed source: no acceptance transfer

Bishop reported55/55 (one scored recovery,26auth,3disconnect-release,16creation-latching,9stale-default controls). No owner manifest/pin was supplied in that notification, and Hudson did not replay or aggregate those55. The independent current gate remains03's **6PASS/1harness-setupFAIL**, with rejected helper6d15 frozen and Hudson's revision/advice lockout intact.

The public-room production partial is now **in flight**: at18:14:11UTC it hashed `0d70dc34ac6e2c1755707357717ecb474a5738863718075447d8ef48bcd198cb`; at18:18:11.813UTC it hashed `46fd8aea30ac8969053680b30cee29928efb69c2a55672623da55c1a37318a09`. Neither matches the tested `948237a651beabfde4d474e277776e2b68fe0fb209f8f9f026f87549251d2cbd`. Main runtime14b12539 and endpoint752c673a being unchanged does not make the source cut unchanged. No behavior or approval is inferred for either new partial hash.

Concrete Hudson coverage boundaries: the existing scored-state case has the outsider arrive **after** legitimate owner reassertion, not before; no default-bot-fill reservation control is claimed; the four relay cases use a **distinct relay alias**, not the same public alias as Changsha; no dedicated case-distinct-alias control is claimed. Current missing-state refusal passes, while corrupted-state acceptance is blocked at fixture setup. The prior legacy test specifically used a recovered signed owner whose binding was removed, not a clean fresh-unknown-alias scenario; those cases must not be conflated or relabeled. No new code, helper revision/advice, test execution or live/cohort operation occurred for this receipt.

Exact receipt: `session-files/qualification/2026-09-12/hudson-actions/recovery-seam-receipt-20260914T181411Z.json`, SHA `ecc343b7c74f9b30f7ab791adc616481cacccf4e7af6d0aef64ba1961a3b8468`. Both readbacks: `recovery-source-readbacks-20260914T181411Z.json` in the same directory, SHA `934720bde5408645aef5ec5110a09e62e1331394c4c921dfa46aa8606f6e2ffd`. Appropriate independent helper revision/authority and a coherent complete source/test cut remain prerequisites; no pending gate is cleared. Qualification0/120.


## September14 legacy safety clarification: requested control already exists

Bishop's11:33:52PDT clarification matches the already-present `LostLegacyAliasBinding_IsNotGuessedFromMatchingSeedOrSignedOwner` in frozen NEW source6d15. The existing03 raw result for that case is PASS: actual signed WS creation/discard produced active runtime7d662ebe-b71e-445f-a7e9-af66c5ad7aed atturn2/v5/wall54/unit7/inventory108; the recorded binding alone was removed; ordinary same-alias JOIN by the recovered original owner rejected `legacy-room-binding-unavailable`/1011 without new rows or inferred mapping. Only genuine NEW followed by seat creation produced different runtime823e8fda-11dc-4440-aeec-5ce4997ec088, retaining the old snapshot/version/timestamp unchanged.

This is already the requested legacy-ambiguity/no-silent-substitution plus explicit-NEW control, not recovered legacy identity or a clean ordinary-user fresh-alias case. No duplicate test, source edit, replay or advice on the rejected helper was provided. Receipt: `session-files/qualification/2026-09-12/hudson-actions/legacy-ambiguity-coverage-receipt-20260914T183352Z.json`, SHA `f614eb39f2c5ed30087d2d28a16bd3992663b02523a13c2402abb13139ffd85d`.

Overall03 remains6PASS/1separate-corrupt-fixture-setupFAIL, not7green. Rejected helper6d15 is unchanged and Hudson's revision/advice lockout remains. Newly identified first-outsider/default-bot-fill/same-alias Relay/case-distinct and changing-source acceptance are not credited by this receipt. No source/image/cohort gate is cleared;0/120.


## Separate healthy isolation scope now complete; rejected helper cycle unchanged

The later explicit request for healthy reservation/mode/ordinal controls was implemented in a **separate** NEW `HudsonPublicRoomIsolationIntegrationTests.cs`, SHAec0d745abc420e1778951806e548086dc9b139dcfa8bed3770380d8ff0c153af. Final source-bound execution: **6/6PASS,0fail/skip/retry**, including outsider BEFORE owner, default-fill/conflicting-config retention of all recovered humans, all four Relay variants sharing the SAME public alias with Changsha and actual two-direction/store/real-discard isolation, and case-distinct aliases with exact separate keys/runtimes. No rejected-helper reference, fault injection, database damage, revision or advice was involved.

Current healthy packet: `session-files/qualification/2026-09-12/hudson-actions/public-room-isolation-04/final-manifest.json`, SHAe2d6c98b100246d9e5fb20f55bbc0428052112025f5bbfb31c1d0b600238a020; TRXaf62dc4e187fe7eb7385992acddb772a0097dcf6f043e01b7905a1e4e7c8c210. Full handoff: `sessions/2026-09-14-hudson-public-room-isolation.md`. Three earlier NEW-oracle failures are separately preserved; final6/6 is a complete fresh matrix, not accumulated passes.

This execution used engine4070693b/public partial46fd8aea/runtime14b12539/endpoint752c673a, so no0c8/948 approval is transferred. The current workspace rejected-helper file changed externally to observed5ec392d0 while its captured6d15 remained unused by this selector; Hudson neither changed nor consumed that revision. That rejection cycle, source/fixture/image review and0/120 remain unchanged. No damaged-snapshot retest or approval is implied by the six healthy passes.
