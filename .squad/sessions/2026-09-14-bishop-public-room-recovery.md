# Bishop — durable public-room recovery source handoff

**Implementation coherent; final source-pinned focused75/75 PASS,0skip,input drift0. Independent source/schema review and Apone's real isolated Compose restart/recreate acceptance are still required. No shipping approval or cohort credit.** September14,2026.

## Root correction
Apone's retained result d47c7487097db92d87815bb95f2f598b9dc2f19ff30fbfbefde38d81b07b3996 proved keys/volume/runtime rows survived, but process-local alias maps did not. The correction persists an EXACT public alias -> runtime association in a dedicated AutotableRoomBindings table. It never identifies a prior game by seed, owner, similar hand or timestamp.

- Initial binding and initial ChangshaGames snapshot commit in one EF SaveChanges transaction BEFORE runtime publication. Binding failures propagate explicitly; they cannot be acknowledged as recovered/fresh success.
- RoomKey is lowercase SHA256 of UTF-8 RoomId; original RoomId is retained and compared ordinally. This preserves distinct-case room identities on case-insensitive providers. RoomKey is primary, RuntimeGameId unique/concurrency-checked. Binding deliberately survives a missing target row so that recovery fails closed.
- JOIN/NEW restore known binding before ownership calculation, JOINED and private projection. Recovered rooms set isFirst=false. Full initial projection comes from the exact existing runtime snapshot, not a redeal. Standard signed-user seat reassertion reattaches without a new row.
- Recovered human seats are explicitly reserved to saved identities until real reconnect/release; empty transport dictionaries and bot filling cannot seize those saved seats. Ordinary same-process disconnect-release behavior remains unchanged.
- Persisted resolved BotDifficulty is restored with seed/BaseUnit/hand cap/deal mode. Room creation latches config before first save; later visitor URL settings cannot reconfigure the table. Existing explicit service-level strategy changes persist.
- After the exact first snapshot, recovery resumes normal timers/bot/turn work. It does not redraw an already-ready14-effective hand; a persisted13-effective pre-draw transition receives its actual required draw. Claim timer resumption uses the remaining stored deadline. Terminal saved rooms can be rehydrated through a known binding rather than silently creating a new game.
- Relay collaborative stores/broadcasts are separate from authoritative Changsha even for the same alias. Relay variants do not recover/intercept the Changsha runtime. Own-turn/claim opaque runtime ID, version, physical-option and privacy wire contracts are unchanged.

## Explicit failure and legacy boundary
Known binding with missing/corrupt/mismatched state, invalid strategy/config or unavailable store returns sender-only actionRejected/current {action:'room',reason:<bounded room reason>} and closes1011; no JOINED or new runtime fallback.

**Pre-fix public aliases are unrecoverable automatically** because they were never persisted. A trusted exact mapping can migrate one; a matching seed/hand/player is NOT such a mapping. Unknown JOIN by a signed owner of active recovered unbound runtime fails `legacy-room-binding-unavailable` instead of silently creating a substitute. Explicit ordinary NEW may intentionally create fresh. An older UI that uses JOIN for both new creation and recovery can need an explicit-new flow or trusted migration for that legacy owner. No frontend change or credential/room/seed substitution was made. Post-fix recorded rooms and ordinary new rooms without ambiguous legacy ownership keep their normal flow. Existing stored bindings remain readable even if snapshot writes are disabled; new rooms/progress are durable only with PersistSnapshots enabled.

## Provider-safe schema / files
19 production/schema/doc paths changed for this addition. Existing EF persistence pattern reused, no new framework/dependency. New entity/DbSet/config, SQLite existing-store bootstrap, generated provider migrations/model snapshots for Sqlite/Postgres/SqlServer. All three new migration SQL scripts generated successfully using isolated artifacts; scripts were NOT executed against live databases. SQLite fresh/restart stores were exercised by the real integration fixture.

Primary SHA256 (paths beneath src/backend/src/Mahjong.Autotable.Api/):
- Autotable/AutotableWsEndpoint.cs:752c673ad62b3aa09193d0bd94b9236622e3352a2a49d5b01d8405283c08697f
- Changsha/Runtime/ChangshaGameRuntime.cs:0e0e2c3c3f28abc73012df8f5fd064849843b8927a3fe321ca496971ff826c0f
- Changsha/Runtime/ChangshaGameRuntime.PublicRooms.cs:c3d773921e0e4494d3b65e72e2695c487b9a822df3307d10941fe64fea3b7c03
- Changsha/Runtime/PublicRoomCreation.cs:fc5a6b3e2be96b24d58d598c8b7277024f8a0a2280769ceacd599ae84bef10ce
- Changsha/Runtime/ChangshaGameInstance.cs:c0ee22ac405de41d474c7cf83875629ca783b7be025cd4aea46c3026fd8280f1
- Changsha/ChangshaDomain.cs:e818e5eaa2e205c437816d4d88d96ab38ca171954c6c65ffd659d91ed7fcdfd6 (adds only persisted BotDifficulty; existing LastDrawSeatIndex/DiscardsThisHand/BaseUnit names retained)
- Data/Entities/AutotableRoomBinding.cs:b6a0cdbe774319950d7e9f7907f8902feb54ca691aa049c74f51128cc776dd47
- Data/AppDbContext.cs:40ef8bf9e9f3949fdb7ef5d305a62615878302d14fdf193a6b3c556bddf2645c
- Data/DatabaseBootstrapper.cs:74539d398e949dfc2f94da45a4e8cb63c72611bf8c7a622ebd591a555e16df8f
- docs/public-room-recovery.md:113928d7b1f2205c86702b152aa0be60b43d3199dfebeee973573fceec0922e8 (repo-relative).
All19 exact hashes, including six generated migration/Designer files and three model snapshots, are in the manifest below.

## Actual independent regression / exact proof
Hudson NEW RulesQualification/HudsonPublicRoomRestartIntegrationTests.cs SHAb2d14d342b316f36f5ba5a8b726635a27ccab83c3d61d03a8e1c4e0cb1d704d5 uses normal signed WS actions to reach hand2, nonzero scores and a changed wall/turn. It creates a second foreign-owner room, confirms persisted rows/state, disposes/recreates host/runtime/manager with SAME DB and verbatim signed credentials, rejoins SAME public aliases and performs standard seat reassertion. It asserts exact same runtime IDs, progressed state/config and ZERO new ChangshaGames rows, correct own/foreign/cross-room projection, denied foreign discard, and successful continued real owner discard with inventory intact. No BindRuntimeGameForTest, seed-equal proxy, fabricated ownership or live browser was used.

Final75/75 scope: new actual restart case1; protected authorization26; relay20; creation/latching16; stale-default lifecycle9; ordinary disconnect-release3. No input drift. Extra independent adverse coverage (first outsider before owner reassertion, default bot-fill reservation protection, known broken snapshot, same-alias mode isolation, case-distinct aliases and legacy ambiguity/explicit-NEW controls) was requested from Hudson and is NOT falsely included in the75 cases. This remains an independent-review coverage item.

Evidence root session-files/qualification/2026-09-12/bishop-actions/public-room-recovery/:
- review-ready-manifest.json SHA3fee58a759a978afdf7191bc45bd59403251ddc9168698b4df3457a8b07858c4
- review-ready.patch SHA8a6302ed78c735362e375605c98bea6573ef89b5ca2db05b0b82b9d63b8b4227 (full addition against preserved pre-task dirty inputs)
- results/public-room-review-ready.trx SHA9d6e7c62a57bafaee10f2b0a31e1830c09475e6249f909cea9a22f882e284a40
- compiled API DLL SHAb0ed4b46aca630aee715586eb52f960ba074bf4f0887b455e50a449de1dffc4b
- review-ready-inputs.json pins all source/schema/doc/new-test inputs; schema/manifest.json identifies all three generated provider scripts and their hashes.
Earlier implementation-stage runs/manifests remain retained, not overwritten as final proof.

No primary8950/key/DB/volume/container mutation, live migration, frontend source/build, root Docker/build-script edit, branch/staging/commit/push, or new agent. Restored dependencies only after explicit NETSDK1004; all artifacts/obj/results/temp/CLI-home isolated. Existing approved UI opaque ID/version/private-option contract preserved. Apone should rerun real isolated source-built Compose acceptance ONLY after independent review of this entire addition; qualification remains0 and other pending rule/QA boundaries are not implicitly closed.
