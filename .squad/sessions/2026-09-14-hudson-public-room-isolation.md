# Hudson: disjoint healthy recovery and same-alias mode acceptance

**FINAL: six NEW cases executed, 6 PASS / 0 FAIL / 0 SKIP / 0 retry.** These are separate healthy-room tests; the rejected fault-injection helper was neither referenced, executed, revised nor advised on. No database damage or synthetic binding/state setup was used. No source/image/cohort approval is granted; qualification remains **0/120**.

## Exact current packet

`session-files/qualification/2026-09-12/hudson-actions/public-room-isolation-04/final-manifest.json`

- Manifest SHA256: **`e2d6c98b100246d9e5fb20f55bbc0428052112025f5bbfb31c1d0b600238a020`**.
- Actual TRX: `results/public-room-isolation.trx`, SHA256 **`af62dc4e187fe7eb7385992acddb772a0097dcf6f043e01b7905a1e4e7c8c210`**.
- Full1976-file seal: `evidence.sha256`, SHA256 **`a2cde8a6aeea1a0780bf01c272be43ef0213b5b79f9a3f8daab5b68bffdaabd4`**, all entries matched.
- NEW file: `src/backend/tests/Mahjong.Autotable.Api.Tests/RulesQualification/HudsonPublicRoomIsolationIntegrationTests.cs`, SHA256 **`ec0d745abc420e1778951806e548086dc9b139dcfa8bed3770380d8ff0c153af`**.
- Loaded API DLL: **`680917ff7fba431b289ddfb5a9600e1689b741fe68acd871deae16dac3be3a4b`**.

One grouped final command ran **September14,2026,19:19:45-19:21:39UTC**, including compilation. It used the captured existing test project, Release/net10.0, one MSBuild node, unchanged serial xUnit configuration and exact filter `FullyQualifiedName~HudsonPublicRoomIsolationIntegrationTests`. Full command is `results/test.command.txt`. No old review packet or rejected-helper case was replayed. Existing dependencies were restored only after recorded NETSDK1004 failures in the fresh artifact directories.

## Captured production and ownership qualifiers

This is a new source cut, **not** the previously supplied0c8 engine approval:

| Captured input | SHA256 |
| --- | --- |
| Engine | `4070693bb631a491bf54ba49119d3665b34f1f215584255b24f2e6a43674e12b` |
| Public-room runtime partial | `46fd8aea30ac8969053680b30cee29928efb69c2a55672623da55c1a37318a09` |
| Main runtime | `14b12539b672f3e0f45364e1dbcda5ab703d5c727c606255a206ae7553691fd2` |
| WS endpoint | `752c673ad62b3aa09193d0bd94b9236622e3352a2a49d5b01d8405283c08697f` |
| Stable shared Hudson fixture | `39531682d5967dce8014c560d95cc52b637f47e74134ac18313055fe036aa4ca` |
| Unchanged old scored-recovery test | `b2d14d342b316f36f5ba5a8b726635a27ccab83c3d61d03a8e1c4e0cb1d704d5` |

All1121 captured files and the full inventory remained unchanged; MVC content-root metadata points inside the captured API project and the test dependency matches the captured project build. Separate trial builds have separate DLL identities; no identical-image claim is made. Filesystem write protection is not claimed. No owned runtime database files remain.

During preparation of04, the separate current-workspace rejected-helper file changed externally from captured6d15 to observed **`5ec392d0cb63b7b3e6efb1e8cffac7e534ec7ad6b7d59ed0e29739700e228618`**. The raw failing workspace guard is preserved in03 and the distinction is recorded in04's `workspace-protected-inputs.json`. Hudson did not inspect it for revision, modify, revert or substitute it. The captured old helper is not referenced or executed by this disjoint filter. No clearance of that rejection cycle is inferred from the external edit.

## Six complete outcomes

All cases use actual signed WS room creation, four human seats, normal auto-deal and real discard55 to reach turn2/version5/wall54 with108 tiles. Original credentials are reused verbatim across the same isolated DB/signing-key factory restart. Initial creation is hard/auto/seed20261652/cap4/baseUnit7.

| Case | Actual result |
| --- | --- |
| First outsider, before any owner reassertion; default bot-fill and conflicting config | First connection's seat0 request cannot acquire a reserved human seat or expose private hands/actions. Original owners0/1 then reassert with omitted bot-count controls and conflicting easy/manual/seed42/cap1/unit11 values. Four saved humans, hard/auto/seed20261652/cap4/unit7, exact game state, game IDs and binding records remain unchanged. Active actor1 remains14 tiles with no extra draw; a further outsider seat1 request fails. |
| Same public alias, four_player Relay and Changsha | Correct initial reference parity, exact origin setup, literal privacy-correct peer setup, Relay store/marker retention, no Relay data in Changsha frames/state, no Changsha marker or real authoritative discard in Relay frames/store. |
| Same public alias, three_player | The same complete two-direction/store/authoritative-action assertions pass. |
| Same public alias, bamboo | The same complete two-direction/store/authoritative-action assertions pass. |
| Same public alias, minefield | The same complete two-direction/store/authoritative-action assertions pass. |
| Case-distinct aliases, identical seed and saved owners | Lower/upper aliases have independently computed distinct SHA256 UTF8 lowercase-hex keys and different runtimes. Both rows/bindings remain2 across restart. A real discard advances only the chosen alias; the other exact state remains unchanged. |

For every same-alias variant, a real authorized Changsha discard26 advances state to version7 while Relay retains its own marker and receives no authoritative control/data. Changsha's cosmetic marker reaches its same-mode peer, proving positive liveness, but neither Relay connection receives it. Mode isolation is established through actual deltas and retained stores, not just a final refreshed snapshot. The relay origin receives unmodified input; the non-owning peer receives only the documented foreign-hand rotation2/face:null projection for thing7, with every other entry exact.

## Three earlier oracle failures preserved, no retroactive green

Each earlier six-case trial executed2PASS/4FAIL/0skip/0retry; none supplies cases to the final6/6 result:

1. **01/ec1207:** a neutral pre-setup CHANGSHA/baseUnit1 match entry was incorrectly classified as recovered data. The actual saved game used unit7. Captured `BuildMatch(null)` explains the neutral defaults. Correction uses a real fresh-unbound Relay reference, without relaxing post-setup isolation checks.
2. **02/c8d968:** the second same-alias peer's legitimate replay of the first peer's stored ephemeral declarations was compared to a first-peer reference. Correction pairs first/first and second/second reference histories; exact entry equality is retained.
3. **03/74e88:** initial references and raw origin setup passed, but a foreign Relay peer was incorrectly expected to receive a face-up own-hand payload. Captured endpoint:2125-2207 defines foreign-hand masking. Correction supplies only the literal expected thing7 projection; all other origin/peer entries and downstream isolation assertions remain exact.

All source versions, failed raw TRXs, commands, exact diffs, scope statements and complete seals are linked by04's manifest. The first-outsider and case-distinct method bodies stayed unchanged through these revisions. No runtime/source change, automatic retry, timeout increase, skip, rejected-helper revision or synthetic gameplay success was used.

## Remaining boundaries

The NEW fixture and its oracle corrections require independent exact-version review. Engine4070/public partial46fd approval is not inherited from0c8/948. The older scored hand2/nonzero-score recovery proof remains separate and was not replayed. The new setup has zero scores; it is not a counted natural match.

Initial ONE-SaveChanges publication atomicity, first viewer snapshot ordering versus resumed bots/timers, ordinary same-process disconnect-release semantics and non-SQLite provider execution are not newly proved by these six cases. The separate rejected fault-injection helper cycle and missing-WinDeclared product RED remain outside this packet and are not cleared. No source/image/live/cohort admission, primary8950 operation, frontend/dist rebuild, Git/index action or qualification-row credit occurred.


## September14 owner75 checkpoint reconciliation: exact cut remains distinct

Read `sessions/2026-09-14-bishop-public-room-recovery.md` and verified the supplied manifest3fee58a7, patch8a6302ed and raw TRX9d6e7c62. The raw owner TRX genuinely contains75 executed/75PASS/0FAIL/0skip. This is corroboration of retained evidence, not a new replay or full-addition source/schema review by Hudson.

A hash-only comparison of the manifest's19 production/schema/doc paths at19:46:56UTC and the linked receipt shows **11 matching /8 different** in the workspace. Differences are `docs/public-room-recovery.md`, `ChangshaGameInstance.cs`, `ChangshaGameRuntime.PublicRooms.cs`, main `ChangshaGameRuntime.cs`, `AppDbContext.cs`, and all three provider model snapshots. Current main runtime is **d594cd359dc835a8da84acb77221aa79924815f697a9403cada40acb5784d0a4**, not the published0e0e2c3c or this healthy packet's14b12539. Current public partial46fd8aea differs from publishedc3d77392. The current engine4070693b also is not an implicit transfer of the earlier0c8 approval.

Owner75 remains a valid result at its declared checkpoint. This independent healthy6/6 remains valid at its own captured4070/46fd/14b/752 cut; the two are **not a combined81-case current acceptance**. The existing main hand2 recovery proof, helper rejection cycle and other pending QA remain separate. Full-addition review must be bound to the actual intended source before any Compose/live/cohort admission; no replay, code edit or approval occurred here.

Exact19-path/counter/source comparison receipt: `session-files/qualification/2026-09-12/hudson-actions/recovery-checkpoint-reconciliation-20260914T194656Z.json`, SHA **6c978b4dc10bb6b6f53f23b15acbfc1de4e9ce13d6ca761a5dbc31d985f48831**. Qualification remains0/120.
