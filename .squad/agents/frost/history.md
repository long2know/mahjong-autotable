# Frost — History

## Core Context

**Project:** Changsha Mahjong (mahjong-autotable). .NET 10 backend + autotable-derived TS frontend. Single-page mahjong table with WebSocket + SignalR transport.

**User:** Stephen Long. Standing directives:
1. "No pauses — keep iterating until 100% done done."
2. All agents use `claude-opus-4.7-xhigh`.
3. **Playability-first** (since 2026-05-24): STOP wave-mill, use Playwright to verify, ship playable prototype.

**Joined:** 2026-05-25, during a late Phase K push to add a parallel backend dev so playability can advance faster.

**Stack notes:**
- Backend: `src/backend/Mahjong.Autotable.slnx` — .NET 10, `dotnet test` gates every commit
- Frontend: `src/frontend/autotable-src/` — TS + Parcel, builds to `src/frontend/autotable/`
- Persistence: EF Core, **multi-provider** (Sqlite/Postgres/SqlServer subclasses) — per-provider migrations live under `Persistence/Migrations/{Sqlite,Postgres,SqlServer}/`
- Test command: `dotnet test src/backend/Mahjong.Autotable.slnx --nologo`
- Test count baseline: 5073/0/0 as of PR #80
- Backend port for local/playtest: 8088 (NOT 8080)
- Local backend startup (verified):
  ```bash
  cd src/backend/src/Mahjong.Autotable

## Learnings (summarized 2026-07-27T01-56-23-811-07-00)

> Full history (31224 B, 20 entries) preserved verbatim in `history-archive.md`. Most-recent entries retained below.

📌 Team update (2026-05-29T20:00:00Z): Wave M — Backend deal-emit verdict for Stephen's "dealing seems very whacky" screenshot. **VERDICT: backend mixed — one real bug found + fixed; remaining visual symptoms are frontend (Hicks's lane).** Evidence: live WS capture against the running backend with Stephen's exact URL params (`dealMode=auto&botCount=3&botDifficulty=Hard&handCount=4&seat=0`) — hand counts correct (14/13/13/13), discards correctly empty, but wall counts were **28 / 27 / 0 / 0** (all 55 post-deal wall tiles packed into seats 0+1, seats 2+3 walls physically empty). **Root cause:** `AutotableSlotMap.EnumerateWallSlotsInOrder` was seat-major, so packing the 55-tile remainder col-major within seat 0 first stuffed it before reaching seats 2/3. **Fix:** flipped to col-major-across-seats — for each col yield every seat × 2 layers, so 55 tiles now distribute ~13-14 per seat, all 2-high stacks. Pre-deal synthesized 108-tile wall is unchanged (every slot still filled). **Tests:** +3 regression tests (`WallTiles_DistributedAcrossAllFourSeats`, `WallTiles_StackedTwoHighAtEverySeat`, `NoPhantomDiscards_BeforeAnyDiscardEvent`) pin the contract going forward. Full suite: 5263/5267 pass; 2 failures (`MultiGameRoutingTests.LateJoin_…`, `VasquezW9SelfLaneTests.NightlyCron…`) are **pre-existing on baseline** (verified by `git stash` baseline run), unrelated to deal-emit. Other Stephen symptoms (only 1 hand tile visible, corner wedges, floating labels) confirmed via WS dump to NOT be backend bugs — handed off to Hicks. Memo: `.squad/decisions/inbox/frost-backend-deal-emit-verdict.md`. **Pitfall for all squad members:** translator slot enumeration order is load-bearing whenever the authoritative collection is smaller than the slot capacity — seat-major orders silently strand entire seats once the source list is partial. Default to col-major-across-seats when downstream rendering must remain visually balanced.

## Team updates

📌 **2026-06-01** — Broken-deal response: Backend fix — column-major wall enumeration (was seat-major, packed all 55 tiles into seats 0+1) — commit `99c1af0`.

📌 **2026-06-01T13:41Z** — Wave N continued — `wall.13.0@2` fence-post final diagnosis & regression tests (commit `165166d`). Stephen reported a page error after my `99c1af0` (backend col-major enum) collided with Hicks's `b4c82ec` (frontend per-seat `row(13)`/`row(14)` setup-slots layout). Task brief proposed "option 1: backend caps per-seat". **Diagnosis: backend was already capping correctly** — `EnumerateWallSlotsInOrder` skips `col >= WallStackCount(seat)` and `WallSlot` throws on out-of-range col. No backend code path can emit `wall.13.0@2`. Real root cause: **frontend `src/frontend/autotable-src/src/setup-deal.ts` DEALS.CHANGSHA table** (Hicks's lane) still uses `['wall.1.0', 2, 26]` for seats 2/3, which walks `slotNames[2..27]` = `wall.1.0 .. wall.13.1`. With Hicks's per-seat split, slots `wall.13.{0,1}@{2,3}` don't exist → `setup.ts:256` throws `slot not found: wall.13.0@2`. Pitfall captured: Playwright `pageerror` parses thrown strings into `name`+`message` at the first `:` — `err.message` is the LAST template-literal interpolation, not the whole thrown string. Empirically verified via a 10-line repro. Backend-side regression (this commit): 5 new tests + 1 helper in `AutotableTranslatorTests.cs` pin both pre-deal (synthesized) and post-deal (authoritative) wall paths against the over-limit slot patterns the task brief called out; iterator-direct test `EnumerateWallSlotsInOrder_NeverYields_OverLimitTuples` pins the iterator independently. All 35 translator tests pass. Lane discipline kept — did NOT touch frontend. Hand-off memo `.squad/decisions/inbox/frost-wall-fence-post-fix.md` contains the 6-line fix for Hicks's setup-deal.ts and the bundle-rebuild + playtest verification recipe. Hicks subsequently applied the patch in round 3 (commit `ff096ff`) and validated across 3 playtests: **ZERO page errors end-to-end. Game is visually + functionally playable.** My regression tests now guarantee this fence-post can't slip back in via backend.


---

## Waves N & O — Scoring/rules audit + live scoring wire-proof (2026-06-03/04)

> Long-form preserved verbatim in `history-archive.md` (folded 2026-08-07T09-21). Compact summaries retained:

📌 Scoring thoroughness audit (2026-06-03): 26 new `FanCalculator` tests; 1 prod bug fixed — spurious situational fans + `ConcealedHand` emitted on non-winning hands, now gated on `detection.IsWin` (committed `87e53c8`). Wash-hand 洗胡 verified N/A for Changsha.

📌 Live scoring wire-proof (2026-06-04): 6 new wire-serialization tests + CDP-tapped Playwright spec PASS — `FanCalculator` fires in real 4-bot games and fans reach the WebSocket payload end-to-end (state machine → translator → `AutotableProtocol` → SignalR mirror; Draw omits `scoreResult`). Memo: `frost-scoring-live-wiring.md`.

📌 Team update (2026-07-27T01-56-23-811-07-00): The SqlServer live-EventLog-enumeration race was root-fixed in Bishop's PR #133 (`d4ff49b`, `TryGetSnapshotCopyAsync` deep copy); your #126 SqlServer integration merged in the wave. This spawn: independently investigate the PR #136 SqlServer failure. — recorded by Scribe (decisions.md §2026-07-27).

📌 Team update (2026-07-27T01-56-23-811-07-00, late-arrival addendum): **PR #136 SqlServer failure = known WS-discard-race timing flake** (isolation 5/5 pass; same-commit CI pass+fail; `PersistSnapshots=false` → zero hot-path DB I/O) — NOT a #136 regression or persistence defect. Justified rerun; WS-race hardening handed to **Bishop** (consider `DbSerial` for the WS-manual-deal acceptance family). — recorded by Scribe.

📌 Team update (2026-07-27T02-51-38-764-07-00): Your PR #136 `Test (SqlServer)` completion-gate investigation is **finalized**: the failure was a known **WS-discard-race / test-isolation timing flake** (5/5 isolation pass; same-commit CI pass+fail; `PersistSnapshots=false` -> zero hot-path DB I/O) — NOT a #136 regression or persistence defect. Justified rerun **PASSED** -> #136 db-providers now fully green (Sqlite/Postgres/SqlServer/drift). WS-race hardening handoff stands with Bishop. Remaining #136 merge blocker = #137 (e2e/playability). — recorded by Scribe (decisions.md §2026-07-27).


📌 Review-of-record (2026-07-27T14-54): **APPROVE PR #156** (author Bishop) — backend half of #153, "retire stale persistent `changsha-default` game for seat-seeking newcomers". Head `2bdc5ef` (parent `507268f`). Independent read-only review; **no merge**.

Change: 2 prod files (+105/-4) + 1 test (+306). `EnsureRuntimeBoundAsync` gains `resettingConnection` (passed ONLY from the numeric seat-take path); for `DefaultGameId` only, `ShouldRetireStaleDefault` retires the bound runtime via `RemoveGameAsync` + mints fresh when ALL hold: durable identity, past `Seating`, newcomer not already seated, and no other live connection. New `TryGetSeatForPlayer` keys off durable `ChangshaSeatState.PlayerId` (survives reconnect), bot-excluded.

Independently verified in an isolated `/data/frost-156-review` worktree (existing worktrees untouched; removed after): **genuine RED→GREEN** — reverting only the two prod files to parent `507268f` (test kept) fails PRIMARY `FreshBrowser_...GetsFreshTable_NotStaleState` (`ridB==ridA`, B inherited stale `AwaitingDiscard`); at head all **4/4 GREEN** in Release. Atomicity: whole check→remove→create under `_bindingLock`; fast-path bypassed for `(resettingConnection && DefaultGameId)` ⇒ no double-runtime/TOCTOU. Connection registration precedes `RunReadLoopAsync` ⇒ concurrently-reconnecting co-player counted by `ConnectionsInGame`, never erased. `RemoveGameAsync` marks row terminal; `ProviderHydrationMatrixTests` proves terminal rows skipped by `HydrateAsync` across Sqlite/Postgres/SqlServer. Process restart safe (`_runtimeBinding` empty ⇒ fresh, guard never runs). Creator-wins/explicit-`?gameId=` untouched. All 17 CI checks green at head (not rerun-flake). Frontend PR #155 compatible — additive; either merge order safe (recommend #156 as safety net before/with #155).

Non-blocking: no truly-parallel concurrent seat-take race test (Live test is sequential); safe by construction — suggest a `Task.WhenAll` follow-up in a later wave (Bishop's lane).

Comment: https://github.com/long2know/mahjong-autotable/pull/156#issuecomment-5097302951 · Decision memo: `.squad/decisions/inbox/Frost-approve-pr-156-retire-stale-persistent-changsha-de.md`

📌 Review-of-record re-affirmation (2026-08-05T17:21 PT): **REAFFIRM APPROVE** for two test-only PRs after clean merge-commits of current main `0dfd96a` onto previously-approved heads (READ-ONLY; no edit/push/merge; dirty pre-existing worktree untouched).

- **PR #145** (`squad/142-manual-deal-pickup-flake`) new head `71331519b4926cfa730cf877b88cdbd70977b678` = true merge of approved `32b29c0` + main `0dfd96a`. All 5 PR-owned test files byte-identical (blob-hash) to approved head; `git diff 0dfd96a..71331519` = only those 5 test files (+322/−20), zero prod/harness/bundle drift. 19 check-runs terminal green on exact head (Test Sqlite/Postgres/SqlServer, e2e, playability-gate, pre-commit, migration-drift, gitleaks, Trivy HIGH, /health amd64+arm64); slsa/sticky-Trivy skipping/neutral. MERGEABLE/CLEAN. **APPROVE `71331519`.** Comment: https://github.com/long2know/mahjong-autotable/pull/145#issuecomment-5198960823
- **PR #151** (`squad/137-bot-chow-advance-guard`) new head `a7a987daca0645ee45fa6e8f29342d1f432fc4c6` = true merge of approved `2d4d801` + main `0dfd96a`. `BotChowAdvancesTests.cs` byte-identical (blob `283436a`); `git diff 0dfd96a..a7a987da` = only that 1 file (+230), zero prod/bundle drift. All check-runs terminal green on exact head. MERGEABLE/CLEAN. **APPROVE `a7a987da`.** Comment: https://github.com/long2know/mahjong-autotable/pull/151#issuecomment-5198962468

Blockers: none. Shared reviewer identity ⇒ no formal GitHub approval; the pinned comments are the review of record. Did NOT merge.

📌 Cross-agent (2026-08-07T09-21, recorded by Scribe): Your RV-2 FINAL (`Frost-RV2-FINAL-SC4-single-trigger-slot.md`) now ALIGNS with the parent-locked DEFINITIVE ruling — manual-pickup match key = `pickup.targetSlots` (single-trigger, top-first, len-1), superseding your OWN prior "count-based targetSlots confirm" AND "targetHandles pin". The pickup-key question is therefore CLOSED / CONVERGED across ALL parties: code (Hicks/Bishop slot-based), parent (locked), rules (Vasquez), review (you). Your G19/privacy handle analysis stays valid but re-points to SC-2 (`things` = opaque per-viewer HANDLES), which remains OPEN/BLOCKING in Bishop's lane — the ONLY place opaque handles belong. See decisions.md §2026-08-07T09-06 (SC-4 v4 FINAL, CLOSED/CONVERGED).
