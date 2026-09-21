# Hudson: bot meld-without-draw continuation regression

**FINAL scoped verdict: RED. Two actual runtime cases executed; 0 PASS / 2 FAIL / 0 SKIP / 0 retry.** Run: September 14, 2026, 05:55:44-05:56:45 UTC. Both unchanged 10-second progress deadlines expired after the real bot meld and a captured production bot-turn exception. This is not an engine implementation or a live qualification run.

## What actually happened

The new two-row `HudsonBotMeldContinuationTests.BotMeldWithoutOwnDraw_DiscardsAndAdvancesInsteadOfStoppingOnSelfHu` creates real signed-cookie WS human seats 0/2/3, fills only seat 1 with the shipped default Medium bot, and performs a real runtime auto-deal. Conserved swaps arrange the directed 13-tile Pung/Chow wait; a fixture precondition marks the bot's existing missed-win lockout. The signed dealer sends the actual WS discard. **No bot claim, win, discard, private scheduler or scripted strategy is invoked by the test.**

In each row the runtime actually schedules the bot's correct sole available claim:

| Row | Actual room | Runtime ID | Actual bot meld |
| --- | --- | --- | --- |
| Chow | `hudson-bot-meld-3f6a5d8abd6d4302b2b488c14e0898fb` | `4b59cc7b-b0d2-4d5a-a7a1-5e427746f8d3` | `[0,4,8]` |
| Pung | `hudson-bot-meld-427c06292f4e4468bb27b8ae53974b4e` | `74fe7a18-2d73-4558-ba14-420616ac90a2` | `[0,1,2]` |

Both use seed **20260914**, BaseUnit 1, cap 4, stock runtime timing (bot claim 250ms, bot turn 350ms, decision budget 2000ms, claim window 5000ms). Neither completes a hand or match.

The immutable post-meld snapshot is **AwaitingDiscard / active seat 1 / StateVersion 6 / LastDrawSeatIndex null**, with 11 concealed tiles `[12,16,20,36,40,44,84,88,92,52,53]` plus the real meld, wall 55, empty river, unchanged pass-Hu lockout, and no CurrentWin/CurrentScore. Assertions establish the structural win but reject self-draw legality and conserve all 108 IDs. The unchanged Medium strategy proposes DeclareWin on this shape.

The captured runtime ERROR is **`Bot turn failed for game ... seat 1`**, with `InvalidOperationException: Self-draw requires an actual own draw on the current turn.` Stack: `ChangshaStateMachine.DeclareSelfDrawWin:659 -> ChangshaGameRuntime.DeclareWinAsync:1261 -> RunBotTurnAsync:1884`. The scheduled runtime dispatch therefore hits the engine's correct provenance guard, logs the exception, and produces **zero bot discards and zero next-human draws** before the 10-second deadline. Final snapshots remain exactly at version 6. This proves the continuation defect, not merely Hu rejection or a speculative timeout.

The Chow row additionally preserves the existing once-per-game omitted-tileIds/lowest-rank compatibility warning; its actual meld is correct, and that warning is not classified as the new defect.

## New test and exact evidence pins

Only NEW source file: `src/backend/tests/Mahjong.Autotable.Api.Tests/RulesQualification/HudsonBotMeldContinuationTests.cs`, SHA256 **`10f9b705e4a000c56ff30ef13cfe737571c32930145fc5d75b67b8b0e78d332d`**.

Evidence root: `session-files/qualification/2026-09-12/hudson-actions/bot-meld-continuation-02/`.

- `handoff.json` SHA256 `7e9d878b1b1ff3b57738bb337061be2166b4c00187ca34108817dca1cfd60762`.
- Separate **855-file** seal `evidence.sha256` SHA256 `a68d3306ae2aa25f5a8f7c7b188b748c7e80e6644a245097bb800d9edcd61cf9`; all entries matched.
- Actual nonzero two-case TRX `results/bot-meld-continuation.trx` SHA256 `b4f5e94cfbb6b15bfcfca61ad246b4b651e90a557686d9e11311cc44deda60ee`.
- `compact-observations.json` SHA256 `2b63526daf908cccc5d3d28f960baf9ccb162922e867464f8311c3ab13670f20`: precise final states, observed transition sequence, case errors and full runtime exception stacks.
- `trx-summary.json` SHA256 `dec019866aea822104d1ebf9f859108f0512377eb8f88d5f1bb9c1a3ceeb7a82`: exact case names/times and complete captured output.
- All **999 backend/test/config inputs matched START/END**; manifest SHA256 `a034df87e94a1841e86d57ac94e75b51a3de77fa8b8e630f9d488211b4103ff8`. All six frozen diagnostic source copies match the compilation-start manifest.
- Tested runtime source SHA256 **`3766ec17b93f8dd8f2eff2534465c510659aca8837d5c32dd84162c2d0e83a9e`**.
- Actual API DLL SHA256 `5105b6e6342b8eaed915e6a30574a7c5015c2648490441e4affde19cea6da50c`; test DLL SHA256 `bfcfcfd613654e84c87c9eeb1739826bd27efc1f16f3707f82bf7a3c31123ac5`.
- Exact command `command.txt`: existing test project, Release, fresh isolated artifacts, `--no-restore`, `-m:1`, single filter `FullyQualifiedName~HudsonBotMeldContinuationTests`. VSTest counts both progress-deadline exceptions as Failed, not runner Timeout; do not misread `timeout=0` as absence of stalled progress.

## Preserved development failure

`bot-meld-continuation-01/` remains immutable **HARNESS-only**: first test version SHA256 `06bc0c4f496e501d167c11e860fbd913d6cdf6099aee26cba0e9134b24c9ec1d` incorrectly expected one version increment after a discard opening a claim window. Actual `tile-discarded` plus `claim-window-open` require two increments, so both rows stopped before awaiting the bot. Its original TRX SHA256 `a672838d45ee0ba8feb4432d6869834984cf24fdda696cf79cadff8ca8b09552` and seal SHA256 `874b2fc30f4193d760cad4c65e04f6b94d18792c6928d08739ccae9e1e9d7e89` are retained. The only correction was exact `initialVersion + 2` plus an additional exact two-event-name assertion. No continuation predicate, deadline, retry, policy or production change.

## Ownership and preservation

Bishop (`84e0d4ad-a2e2-4c16-a867-c264adce9917`) owns the minimal production runtime fallback; Burke (`403154cf-209d-4cd8-9b65-c07f661742b9`) coordinates rules. Both received the actual two-case failure, source hash, IDs and runtime stack immediately after classification. Hudson has not changed their engine/runtime files or claimed source approval.

The shared WS fixture is read-only and remains SHA256 `39531682d5967dce8014c560d95cc52b637f47e74134ac18313055fe036aa4ca`. All three frozen audit hashes are unchanged. Prior 858-file WS evidence and 855-file legacy-fixture-repair seals still match. No locked UI harness, existing helper, production file, Git/index, container, dist, live server, or browser was changed. Own TestServer connections/factories were disposed and no fixture databases remain. Dependencies were restored only after actual fresh-path NETSDK1004 failures; manifests and packages were not changed.

**Zero qualification match credit; zero qualification SQL rows touched.** A future runtime correction requires a newly pinned targeted run, preserving this RED evidence and the test's actual discard/next-human-draw requirement. The separate overflow-disconnect failures, Burke's added-Kong all-pass case, and frozen-audit disposition are not closed or relabeled here.
