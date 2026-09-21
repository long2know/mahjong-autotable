# Drake FINAL — manual-start runtime classification (September 9, 2026)

**Outcome: backend start/roll/scheduler failure NOT reproduced; no runtime source-edit grant needed.** C01 has already started the retained manual room and is waiting for its human dealer's roll. The missing DOM control versus driver/precondition question belongs to Ferro/Dietrich; this diagnostic does not self-classify their UI as defective or provide browser acceptance.

## Actual observations

- Input report `session-files/completion-proof/2026-09-09/dietrich/dietrich-lifecycle-2026-09-09T23-22-55-099Z/renderer-observation.json` verified SHA256 `841000deda6eca084f8be1fdf0a16f9577c925efce099ba2d963e5b0f6c90bf7`. It covers September 9, 23:22:55.267–23:26:30.447 UTC; recorded actions/sent/discards/hand ends are zero. Its terminal assertion requires a real discard after a hand boundary, a lifecycle never exercised. Last scene confirms seat0, manual conditions, hand0/wall108; phase/dealer were not captured.
- Scoped C01 logs confirm one connection to exactly that isolated room at 23:22:57.490 UTC, ChangshaRuntime/manual/3 Medium/seed94209, and close at 23:26:29.497 UTC. Initial logged seat=null is expected before authoritative seat acquisition, not proof of a failed seat grant. Only this room/connection's allowed startup/close messages were retained; information-level logs do not establish action history.
- **Retained-room read-only WS snapshot, September 10 at 00:08:57.699 UTC (September 9, 17:08:57 PDT): `turn.current={phase:"RollingDice",activeSeat:null,awaitingDiscard:false}`, `match[0].dealer=0`, authoritative `conditions.dealMode="manual"`, `pickup.current=null`, four empty hands, wall108. Seats: persistent human0 + bot1/2/3.** Same state two seconds later. This was a new spectator socket with JOIN only, no seat/start/roll/take/discard UPDATE or reuse of the old owner's identity. The old human had disconnected; the snapshot does not claim a currently live owner connection.
- This snapshot is explicitly AFTER the original 210-second run, not a retroactively captured phase. Disconnect code preserves manual phase/dealer/persistent seat identity while removing the transport binding.

## Executed positive runtime controls

Used unchanged `.frost_uat_scratch/frostws.py` with `CAND_WS` pinned to C01 and bytecode writes disabled. Two bounded passes, four new unique diagnostic rooms total; final evidence uses:

1. `drake-start-0-d1f049c2facd42`, manual/seed94209/seat0/3 Medium: own JOINED identity's seat grant confirmed. **Without any explicit start command**, server frame sequence was initial unbound auto conditions → Seating/auto → RollingDice/manual. It remained correctly parked for four seconds with human dealer0. One diagnostic `pickup/rollDice {seatIndex:0}` was accepted; next observed frame was BreakPointMarked, own pickup count4, exactly one public target slot. No human pickup/discard was injected.
2. `drake-start-1-0b630550fd2242`, same config except human1: server also entered RollingDice/manual without a start command; bot dealer0 autonomously rolled and took its first four. Within the four-second observation, phase was PickupRound1, pickup seat1/count4/target len1, dealer hand4/wall104. No explicit roll was sent by the human1 socket.

Both control assertions passed on both bounded passes. **Raw-WS control is runtime diagnosis only, not UI/gameplay acceptance.** Final recorder correctly labels `human-{seat}` synthetic creation keys as placeholders, not additional human owners; earlier coarse-label evidence is retained and identified as superseded instrumentation, not rewritten.

## Root boundary from exact C01 source

Inspected frozen `session-files/completion-proof/2026-09-09/apone/c01-source/`, not Burke's current endpoint edits:
- `Autotable/AutotableWsEndpoint.cs:844–858,1295–1308`: human seat acquisition + bot fill invokes server start when every seat is occupied; applies manual mode before StartGame. No UI Start command is needed. `:1210` forwards the authorized owner roll.
- `Changsha/ChangshaStateMachine.cs:44–56`: initial dealer0; StartGame transitions Seating → RollingDice.
- `Changsha/Runtime/ChangshaGameRuntime.cs:796–843,854–882,1661–1678`: manual start publishes state; a human dealer deliberately does not auto-roll, while a bot dealer is scheduled; authorized roll begins the manual pickup chain.
- `Autotable/ChangshaToAutotableTranslator.cs:212–246,490–504`: phase is offered in `turn.current`, dealer/mode in `match[0]`; pickup is intentionally null until a pickup phase. A live pickup designation is NOT a precondition for offering the initial roll.

**Routing:** Ferro/Dietrich should reconcile missing/ignored roll affordance with confirmed seat0 + dealer0 + RollingDice/manual. The supplied renderer run never reached the hand-boundary/discard precondition. These data close the claimed runtime-start block, but do not distinguish a hidden/missing real roll control from the driver overlooking it; no frontend audit or browser was performed in this lane.

## Evidence and preservation

Root: `session-files/completion-proof/2026-09-09/drake/manual-start/`.
- `classification-summary.json` SHA256 `a045860db8061fb4e1ef1902c8c43111ee5d90a31b43ad7c74818f2b5c7fd671`.
- `ws-start-classification-20260910T000857Z.json`, per-room JSON, `ws-probe-final.log`, reproducible `probe-manual-start.py`, `dietrich-scoped-container-logs.json`, `candidate-source-seal.json`, `manual-start-evidence.sha256`.
- Live image unchanged `113c10b315f41483bdd0e1b0879ffdee08399e4f8b2dd91fc37671284ba10461`; live DLL freshly SHA256-verified `836821f3819d881fc1ee7bc1a480f98b16adf32e7b37e21449b70d04f1b4cec5`; same container/start time, restart count0. Runtime source matches the C01 frozen copy.
- No production/test/config/dist edits, no browser, restart, source/index/branch mutation, direct DB/data operation, other-server/game access, credentials/private-face logging or subagents. Authorized new-room commands use ordinary runtime persistence; no database manipulation bypasses the application.
- **Approved S11 remains byte-identical**: `cd8a447006d670b3fdf72be42fe3dfc980af8c005f7547a3677152ea8b6ef239`.

C01 remains RED for the separate cross-room privacy defect. These startup controls do not clear F7, certify C01/C02, or replace Hudson's real-browser acceptance.
## Corroborating browser-lane report — received September 9, 2026, 17:23 PDT

Dietrich reports two separately authorized fresh same-input C01 rooms (`dietrich-physical-2026-09-09T23-52-03-481Z-0` and `-1`) both healthy: authoritative RollingDice, dealer0, client/world seat0, pickup null, and a displayed/visible/enabled Roll control with nonzero rectangle; no page errors. The original 210-second missing-control observation was not reproduced. Owner report: `session-files/completion-proof/2026-09-09/dietrich/c01-physical-2026-09-09T23-52-03-481Z/physical-diagnosis.json`, SHA256 **`bc4f7db16119de9cd63a88d761b613754b718fe3a585eedf6f9b719a1713ba4e`**, freshly hash-verified by Drake. The browser observations are Dietrich-executed, not Drake-executed; Drake did not duplicate the frontend audit.

This corroborates the runtime classification: no reproduced start/roll block and no established current roll-control regression. The original renderer-run failure still lacks its at-failure phase/dealer, so historical attribution remains unresolved rather than rewritten as a proven backend or UI bug. Dietrich's separately browser-confirmed hidden-mesh fix/review is independent and is NOT inferred to explain the manual stall. No source/S11 edits or additional browser runs follow from this update.

### Ferro corroboration — September 9, 2026, 17:24 PDT

Ferro independently read/hash-verified the SAME Dietrich physical-diagnosis artifact (`bc4f7db16119de9cd63a88d761b613754b718fe3a585eedf6f9b719a1713ba4e`) and reports two rooms, each sampled twice, with RollingDice/dealer0/confirmed seat0 and an enabled visible Roll control (119.5×122.4 CSS px). His receipt is `session-files/completion-proof/2026-09-09/ferro/c01-manual-phase-receipt.json`. This is a corroborating evidence review, NOT another browser execution or additional independent run count. Ferro concludes no UI correction is justified by current facts; original r3 inactivity remains unwaived/unattributed. No further runtime or UI source grant is requested by Drake.

## Final classification / retained acceptance gate

Coordinator's latest facts agree with the independently verified receipts: **original r3 is an unreproduced historical liveness failure with insufficient phase/dealer capture; attribution remains unresolved.** It is not classified as a backend defect, a proven harness mistake, or an established current UI regression. Healthy runtime controls and Dietrich's two healthy real-UI observations do not erase the original failure.

The final **C02 seed94209 manual real-UI probe remains required/pending** and will cover this boundary on the corrected candidate; no C01 diagnostic result substitutes. Dietrich's separately confirmed/fixed 108 visible hidden physical meshes remains a distinct renderer defect with source review pending, not an inferred cause of r3. No further diagnostic/source action by Drake; approved S11 stays frozen.

## CURRENT causal update — confirmed late-UI subscriber defect

Ferro's authorized **September 9, 17:24:18–17:24:30 PDT** C01 browser diagnosis now establishes a NEW reproducible UI initialization failure, superseding earlier statements that current evidence justified no UI correction. Normal delivery exposes enabled Roll and a real click advances RollingDice → BreakPointMarked/pickup4. Delaying only the unchanged original scene-effects module until real RollingDice/dealer0/confirmed seat0, then allowing GameUi to install, leaves Roll hidden/display:none for the entire five-second observation, with no page errors or game/DOM/control injection.

Authoritative owner handoff read through runtime: `sessions/2026-09-09-ferro-late-ui-roll-blocker.md`. Owner-executed results SHA256 `95d5c3eff3a0dc8f460c40dda7115f88bf4b386120cd4ae863723a5f561643bd`; probe SHA256 `557ba0ba633bde4e4d559f9ec4272122a6d9d2f926b63a22e0ee5fb123f7d71d`; evidence `session-files/completion-proof/2026-09-09/ferro/c01-late-ui-roll-01/`. This is Ferro browser proof, not additional Drake execution. Ferro identifies missing initial hydration of already-existing authoritative Roll/pickup state in the lazily mounted GameUi and proposes a game-ui.ts-only correction plus a new narrowly scoped regression, pending coordinator author/grant/review handling.

**Runtime conclusion remains unchanged:** server start and owner/bot roll paths are sound in the executed controls. This UI causal ordering reproduces r3's symptom, but r3 itself lacked timing/phase/dealer capture, so its exact historical cause remains unproved. The hidden-mesh defect remains independent. No source change/review approval is issued by Drake; approved S11 remains frozen. Final C02 real-UI acceptance must cover the reviewed late-UI correction as well as the seed94209 manual-start boundary.
