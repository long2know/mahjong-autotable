# IMPLEMENTED — independent entry/seat revision R2 (Ferro)

**Reviewer-ready; independent rereview pending.** Source-only work completed September10 UTC (September9 PDT). No browser, server/HTTP probe, dist/image build, restart, live injection, new agent/factory, Git mutation or backend/client-transport edit. Hicks was not contacted and provided no advice/code for this revision; his exact rejected artifact was used only as the permitted starting input.

## Minimal production correction

Only `src/game-ui.ts` production bytes changed relative to rejected R1 (+5/-1): move initial connection-state classification until after the existing resolved `phaseF` is available. An intentionally local relay table is non-Changsha, has NO `gameId` query parameter and NO remembered `client.lastGameId`. Only that local mode remains active while not connected. Requested or remembered server sessions, and offline Changsha, stay retracted until connected. Empty `gameId` still counts as connection intent, matching unchanged `ClientUi.start()`'s null/presence test. Actual disconnect/connect handlers remain unchanged and continue retracting/restoring cues.

Cold-first-click eager binding, initial connected seat/chrome hydration, offline Changsha nonownership, and existing initial Roll/Pickup hydration remain intact. **87 non-constructor GameUi members are byte-identical** to rejected R1. `client-ui.ts`, `lobby.ts`, `index.ts` are wholly byte-identical. All **17 original top-level test statements, including the entire original nine-case browser describe, are byte-identical**; no assertion/timeout/skip was weakened.

## All five resulting hashes

| File (relative to `src/frontend/autotable-src/`) | SHA256 |
|---|---|
| `src/client-ui.ts` | `4aad08d638cad6e6fae5831c6dbfa91cfe546d398dbe02f8983e6784ab23bb1b` |
| `src/game-ui.ts` | **`b47d41b766ea06e351fec881dce499fb1783aeb35f7527c47d9323e9ad5bc011`** |
| `src/lobby.ts` | `14e287e225417ff907a7cb085da2b3399f89a9b3adae6aebf1862a7d250d7bd3` |
| `src/index.ts` | `d6486e5eb80096c0ffd7c41772e10a371c158e7f3080962e214786bbf9547ddd` |
| `tests/e2e/entry-seat-initialization.spec.ts` | **`10a3508327ac17a0d6f93072967997e5a3bd3c1f947d70c00c9a690adcc82c93`** |

Exact BEFORE five-file snapshots/hashes, AFTER snapshots, minimal production diff and preservation assertions: `session-files/completion-proof/2026-09-09/ferro/entry-seat-revision-r2/` (`before/`, `after/`, both manifests, `game-ui-revision.patch`). All supplied rejected hashes matched before any edit; GameUi before was `a7a1883ecaad1dda67231378e027159dd971ce5fe6716689a78a09679ad5ee93`.

**Review manifest:** `entry-seat-revision-r2/review-manifest.json`, SHA256 **`178e67c1a9b3a5a239459a61b565cd4111fe45514ff57e58eef9b1a5bdf2b3ed`**.

## Discriminating source regression / actual results

Added **13 browser-free source contracts** to the granted new spec, leaving the original nine browser cases intact. Harness executes the actual constructor, initial subscriptions, seat/HUD/banner methods and body cursor-class updates with a minimal DOM/transport shell and resolved-mode/normalized-world fixtures. It does not copy the flag logic or run a browser. Unrelated setup/renderer infrastructure is stubbed; no live geometry/pixel/gameplay claim.

Coverage: offline Four Player/Three Player/Bamboo/Minefield; empty/requested/remembered server IDs; connected relay; offline/connected Changsha; actual registered disconnect/reconnect callbacks; connected late empty-seat chrome; cached Roll/pickup initialization and a pickup tombstone with no emitted actions or duplicate label children.

**Same final new test hash before and after production correction:**
- Rejected R1 source: **9 source controls PASS /4 offline-relay RED /0skip/0flaky**, retries0,5.12s. All four actual constructor observations showed `connectionLost=true`, hidden/empty banner, discard cursor false, no commands. `baseline-red-final/results.json` and annotations retain those facts.
- Corrected R2: **13/13 new source contracts PASS**, plus **33/33 existing pure turn-cue contracts PASS** = **46 PASS /0fail/0skip/0flaky**, retries0,2.06s. Offline relay now has `connectionLost=false`, visible `Your turn — click a tile to discard`, cursor class true, no commands; disconnected controls stay retracted. `after-contracts/results.json`.
- Strict TypeScript across all four granted source files/new spec: **PASS**, with the repository's existing `src/hls-light.d.ts` included.
- New spec ESLint: **0 errors/0 warnings**. Exact rejected/current GameUi lint findings match after mapping inserted/deleted line offsets (13 errors/0 warnings); broader named UI subset remains **14 errors/1 warning**, no new findings. Both baseline/current exit1 are retained, not waived. `lint-comparison.json` + raw JSON logs.
- Original browser cases **collected only: nine per project,18 total**. No old/new browser case executed. Source-only runs used an empty lane-owned `PLAYWRIGHT_BROWSERS_PATH` in addition to source-only selectors; no browser installation attempted.

## Commands / validation limits

From `src/frontend/autotable-src/`, with unique lane-owned TMPDIR/report/output and empty browser path:
`npm run e2e -- entry-seat-initialization.spec.ts --project=chromium --workers=1 --retries=0 --grep 'Ferro R2 source-only' --reporter=json --output=<before-output>`
`npm run e2e -- entry-seat-initialization.spec.ts turn-cue.contract.spec.ts --project=chromium --workers=1 --retries=0 --grep 'Ferro R2 source-only|turn-cue' --reporter=json --output=<after-output>`
`npx --no-install tsc --noEmit --strict --target es6 --module esnext --moduleResolution bundler --types vite/client,node --lib DOM,DOM.Iterable,es6,es2017 src/hls-light.d.ts src/client-ui.ts src/game-ui.ts src/lobby.ts src/index.ts tests/e2e/entry-seat-initialization.spec.ts`

Two resolved setup attempts remain honest evidence: the first additive block was initially nested in `prepare()` so no tests collected (`before-contracts/`, NOT counted as product RED); registration was corrected before valid before/after runs. An initial targeted tsc command omitted the existing hls-light ambient declaration and produced TS7016; corrected by including that existing input, not adding dependencies/declarations. No assertion/timeout/skip masking.

**Rereview is now the next gate.** No advice from locked-out Hicks, no self-approval, no change to his other fixes, protected S11/claim-key/helpers, renderer, transport or separately approved relay-origin backend. Hudson retains exclusive core-game browser execution on immutable18209. Coordinator alone may build a future image after independent rereview. This revision is not deployed/live/browser green yet.
