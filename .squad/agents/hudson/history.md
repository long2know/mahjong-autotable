# Project Context

- **Owner:** Stephen Long
- **Project:** Changsha-first Mahjong game built from pwmarcz/autotable, with expanded Chinese rules planned
- **Stack:** .NET 10 backend, EF Core + SQLite initially, optional React + Fluent UI 9 + TypeScript + Vite frontend modernization, single-image Docker deployment
- **Created:** 2026-04-20

## Learnings

- Team initialized with Hudson as Tester.
- Quality focus areas: rule correctness under edge conditions, API contract stability, and end-to-end gameplay regression.

## Work Log

### 2026-07-27: Independent READ-ONLY diagnosis of #152 & #153 on main `507268f`
**Method:** isolated worktree at `507268f`; own .NET backend serving the **committed** `src/frontend/autotable/` bundle (port 8099); real headless Chromium (Playwright) with genuine pointer clicks + read-only WS frame capture. No client.update / state injection / synthetic DOM / direct emitDiscard. Posted evidence comments to #152 (issuecomment-5096546277) and #153 (issuecomment-5096546486).

**Canonical wall math (docs/rules/changsha-spec.md):** 108 tiles / 54 stacks; deal 3×4 + dealer extra → dealer 14, others 13 (53 dealt); **wall remaining after deal = 55**; break = 2 dice CCW, draw CCW contiguously from break point.

**#152 — high-confidence findings:**
- Wall **count is CORRECT**: reproduced live 14/13/13/13 hands, **wall=55**. Deal is canonical.
- Fragmentation root cause = translation defect, NOT count bug: `AutotableSlotMap.EnumerateWallSlotsInOrder()` packs draw-order `state.Wall` **column-major across seats** (wall[0]→s0c0L0, wall[1]→s0c0L1, wall[2]→s1c0L0…). After deal, measured `wallColsPerSeat={0:[0..7],1:[0..7],2:[0..6],3:[0..6]}` of 14/13 ⇒ **4 offset half-walls**; far halves empty. Wall re-packs every mutation ⇒ tiles scatter/jump across seats, so depletion is NOT a contiguous perimeter sweep. Authoritative wall IDs vs rendered slots diverge by design (documented balanced-fill choice; spec §2.3 says physical geometry is rendering-only). Rendering/UX defect, not rules.
- HUD overlap (measured bbox @1920): `#lobby-toggle`(x12..103,y12..48) overlaps `#deal`(x16..204,y16..47). Reproduced @1920 & 1366; not @390 mobile. Top-right: two gear buttons (x1726,x1824)+move-log close(x1875) crowd header; Move Log panel under icons. Deal/Take-Seat still clickable headless (not hard-blocked) but z-order defect real.

**#153 — high-confidence findings (config/seat-binding + persistence + UX, NOT engine):**
- Bare `?variant=changsha` handshake = `ws?gameId=changsha-default&variant=changsha` — no botCount/dealMode/seat forwarded (`buildWsUrl` only forwards URL-present params). Runtime defaults dealMode=**manual**.
- Bots ARE seated: `AutoBotFill` default true → `FillEmptySeatsWithBotsAsync` fills **all** empty seats (ignores botCount). Captured Bot 1/2/3; overlay "3 bots — Medium · seats 1,2,3." Bots perform their manual-deal round pickups.
- **Engine HEALTHY**: fresh-gameId all-bots auto (`seat=-1&botCount=4&dealMode=auto`) → all 4 bots, atomic 14/13/13/13 wall55, full auto-play with discards + Pung/Chow claims + **multiple complete hands** (wall 55→6→new hand 53).
- **Seated human**: real pointer discard of tileId68(6筒) **accepted server-side**; bots responded (Bot1 Chow+discard, Bot2, Bot3), turn returned to human and correctly waited. No engine stall.
- Repeated `.take-seat` after seating = **0 outbound frames** (honest, not dup-seat). "Repeated pickup" = bounded manual ceremony (exactly 14/13/13/13, no over-draw).
- Real defects causing "stall" perception: (1) default manual deal + human=dealer must discard first (no obvious cue) + lobby-default `auto` vs runtime-default `manual` mismatch; (2) **persistent `changsha-default`** — new connection JOINs stale/already-dealt/stalled game (observed leftover dealer, 0 discards for 47s); (3) discard fires only on mousedown over a hand tile while holding 14th tile (non-obvious affordance).

**Owner routing:** #152 wall-slot translation + HUD z-order → Frontend/Hicks; #153 default dealMode/seat-binding + `changsha-default` stale-state reuse + discard cue → Backend/Bishop + Frontend. Engine/rules NOT at fault for either.


### 2026-07-27 (late): PR #155 revision — CI GREEN, 0 flaky (ready for Ripley re-review)
Head `1dc32dd` (parent 7589fa9), pushed fast-forward to `squad/153-default-game-ux`. PR MERGEABLE, OPEN, NOT merged.
- **RED repro (retries=0):** unmodified `dealer-turn-affordance.spec.ts` failed **6/24 (~25%)** — exact `Expected > 2, Received 2 / Timeout 45000ms` #153 symptom. Root cause: `readDiscardCount() > outcome.discardAfter` is invalid — (a) fast Hard bots complete the go-around inside `discardByPointer`'s 1200ms settle so `discardAfter` already counts them; (b) bot Pung/Chow/Kong removes the claimed tile from the shared discard pile → total is non-monotonic (observed 3→5→4→5 while melds 3→9).
- **Fix (test-only):** new read-only `readBotActivity` helper; poll bounded authoritative NON-LOCAL-seat signal (bot discard OR meld/claim OR result/gameComplete + latched hand-end observer) vs a pre-discard baseline. Human discard still proven server-accepted via `outcome.ok` + authoritative pile growth (dealer's mandatory first discard). No src/bundle edits — Ferro's verified-solid URL/new-game logic preserved.
- **GREEN validation, retries=0:** fixed spec **30/30** and **24/24**; playability-gate **4/4**; eslint clean; strict src tsc exit 0; bundle-in-sync OK.
- **CI at head 1dc32dd:** `e2e` **pass — 317 passed / 267 skipped / 0 flaky** (rejected run was "1 flaky / 316 passed"); `playability-gate` **pass**; visual gate (BLOCKING), pre-commit, bundle-sync, lint, amd64 build/health all pass; **23 pass, 5 skipping, 0 fail**. The `linux/arm64` build/health job needed 2 re-runs (Docker-registry timeout, then 25-min QEMU-emulation cap under shared-runner load; normally ~11 min — same branch passed it 11m23s an hour prior; amd64 builds the same Dockerfile in ~1m11s) → confirmed transient infra, not the revision; now PASS (10m51s).

### 2026-07-27 (later): PR #155 — integrated origin/main (c7d0cbe) via real merge commit
- Confirmed remote PR head still `1dc32dd` before merging; origin/main = `c7d0cbeea857e039fd5178f66c15faa04e942ea8` (PR #154 #152 wall/HUD + PR #156 default-game lifecycle).
- `git merge --no-ff origin/main` into `hudson/155-deterministic-bot-response`: SOURCE auto-merged cleanly (disjoint files — #154 backend/css/specs vs #155 URL/session + my test assertion). ONLY conflict was generated dist `manifest-precache.json`; resolved per policy by merging source first then `npm run build` (vite emptyOutDir) — no ours/theirs hash pick, no hand-edited dist. Merge commit `28bf7da4e11e39961ccce689e756c94ca36f688e` (parents 1dc32dd + c7d0cbe), pushed fast-forward `1dc32dd..28bf7da` to `squad/153-default-game-ux` (no force). PR head now 28bf7da, MERGEABLE. NOT merged.
- Preserved: #154 wall source (WallOrdinalToSlot/WallBreakOrdinal/WallBackDrawn) + HUD css; #155 URL/session (session-url.ts) + the deterministic `readBotActivity` assertion (dealer-turn-affordance + _playability). client-ui.ts byte-identical to CI-green 1dc32dd.
- Local validation on merged head (all green): strict src tsc exit 0; fresh-build `verify-bundle-in-sync --build` OK (only manifest timestamp differs); dealer-turn-affordance **20/20** retries=0; wall-contiguity + hud-no-overlap **12/12** (chromium+mobile-chrome, retries=0); playability-gate **4/4** (bundle-hash preflight OK). ESLint on `src` shows 20 pre-existing errors, all in files byte-identical to origin/main (game-ui/move-log/center/admin-panel/world) or in #155's own client-ui unchanged from the CI-green head — not introduced by the merge and not a CI gate (pre-commit runs no eslint; #155 & main are green with them).
- Awaiting full CI on 28bf7da; require e2e 0 flaky. Ripley re-reviews the final integrated head.

### 2026-07-27 (final): PR #155 integrated head 28bf7da — CI TERMINAL GREEN, 0 flaky
- PR #155 head `28bf7da4e11e39961ccce689e756c94ca36f688e` (merge of #155 `1dc32dd` + origin/main `c7d0cbe`), MERGEABLE, OPEN, NOT merged.
- CI: **23 pass / 5 skipping / 0 fail**. `e2e` head_sha=28bf7da → **329 passed / 267 skipped / 0 flaky** (+12 tests vs prior 317 = the merged #154 wall-contiguity + hud-no-overlap gates now running). `playability-gate` pass (15m7s), `View visual gate (BLOCKING)` pass, `Visual regression` pass, `Bundle in sync` pass, `pre-commit` pass, all 3 DB `Test` jobs + build/health (amd64+arm64) pass.
- Local pre-push validation on 28bf7da all green: strict src tsc exit 0; fresh-build bundle-sync OK; dealer-turn-affordance 20/20 retries=0; wall-contiguity + hud-no-overlap 12/12 (both projects); playability-gate 4/4. eslint `src` errors are pre-existing (files byte-identical to main / #155's unchanged client-ui) and non-gating.
- Ready for Ripley's re-review of the final integrated head 28bf7da.

### 2026-08-06: Release verification @ ddc72e1 — production-shaped real-browser playability PASS
**Mission:** Independent (author-disjoint) acceptance gate on released commit `ddc72e12576a1ede9a0ac8302b1102d23850ab65`, backend serving the production-shaped built bundle (no WS backdoor).
- **PASS:** 4-hand human-vs-3-bots reached authoritative **GameComplete**.
- **52 pointer discards**, **21 claim windows**, **zero errors / zero stalls**; **6/6 targeted specs passed**.
- Evidence archived under session `completion-proof/hudson`. Read-only gate — no branches/production code touched.

📌 Team update (2026-08-06T11-45-15-725-07-00): Merged to `.squad/decisions.md` — contextual Changsha Big Wins (海底捞月/河底捞鱼/天和/地和/杠上开花/抢杠胡) were scoring as Small Win on Standard-shaped hands (audit → #157). Fixed in **PR #158** via new single-source `Changsha/ChangshaWinCategory.cs`; **decision anchor for scoring tests: classify Small vs Big Win ONLY via `ChangshaWinCategory.Classify`** (contextual Big Wins live in `AllPatterns`; 抢杠胡 via `WinResult.IsRobbedKong`) — pin `ScoreResult.Category` + §5.1 payments, never re-introduce a Pattern-only switch. Frost APPROVED #158 @ beee940. — recorded by Scribe.

### 2026-08-06 (later): READ-ONLY stuck-turn diagnosis @ ddc72e1 — VERDICT FAIL (stale-gameId-reuse seat deadlock)
**Target:** live prod container `mahjong-proof-ddc72e1` (:18080), commit ddc72e1. Genuine headless Chromium (Playwright 1.60/chromium-1223) on the **production-built bundle**; strictly observation-only (no clicks/DOM-synth/WS-injects/mutations/source edits). Evidence: `session-files/completion-proof/stuck-turn/hudson/`.
- **FAIL, 100% deterministic.** URL `?…&dealMode=auto&botCount=3&…&gameId=completion-proof-20260806` binds to the **pre-existing runtime game** and re-ships its persisted snapshot. Decoded from the server's own `UPDATE full=true` (108 tiles conserved): **AwaitingDiscard, current seat=0** (seat0=11 concealed+3 meld=14 ⇒ must discard; seats1-3=13; wall=42; no claim/pickup; result tombstoned; gameComplete absent).
- **Root cause = stale seat ownership.** Seat 0 is a **human** `playerId a2e46a1d…` from a prior session; seats1-3=`bot-1/2/3`. The reconnecting viewer's pid differs (`f3d4605…`,`2f6c0f8d…`) and **owns no seat**. Seat 0's human is absent, **no bot takes over the vacated human seat**, and the viewer gets **Take-seat×4 HIDDEN / Deal disabled / Leave-seat disabled / no claim buttons** while parked at Seat 0's camera POV ⇒ permanent deadlock.
- **Liveness = frozen, not slow:** 60 s window → **1 JOINED + 1 snapshot, then 0 server state-change frames** (only SignalR type-6 pings); `md5(t004)==md5(t060)` pixel-identical; 0 console/page errors.
- **Control (proves causation, engine healthy):** same params + **fresh unused gameId** → all 4 seats empty, nothing dealt, **all 4 Take-seat VISIBLE / playable**. So the stuck state is specific to reusing a gameId whose runtime game persists with a seated-but-absent human mid-turn.
- **NOT a rules bug; NOT a required draw/claim.** The lawful action at seat 0 is a plain discard (draw already done, no open claim). Blocker is seat-lifecycle/UX. `JOINED.isFirst=true` is misleading (empty relay cache, not the still-bound stale runtime game).
- Reconfirms my 2026-07-27 #153 finding (persistent gameId reuse joins stale/stalled games). Routing (not implemented, per task): backend seat-lifecycle/connection (Bishop/Frost) + frontend UX (Hicks/Ferro) — bot-substitute/seat-takeover/clear-affordance on JOIN to an abandoned-current-turn seat. Decision note: `.squad/decisions/inbox/hudson-stuck-turn-gameid-reuse.md`.

### 2026-08-06 (later): C-5 — authored 4 browser RED→GREEN E2E specs for the stuck-turn fix
**Contract:** Ripley Design-Review C-5 (own E2E/frontend browser test files only). **Baseline:** container @ ddc72e1 (`:18080`, committed bundle) — targeted the CONTAINER so RED is stable despite the shared `/tmp/mahjong-autotable-stuck-turn-fix` worktree holding other agents' in-progress src fixes. Genuine rendered controls only (real `.take-seat`/`#deal`/claim/`#lobby-apply` clicks + real-pointer canvas discards); no `client.update`/`emitDiscard`/synthetic DOM/collection injection; no production-source/bundle/commit/branch edits. Ran via project Playwright (local no-root Node 20 + `@playwright/test@1.60` reusing cached chromium-1223), `E2E_BASE_URL=:18080`.
- **RED (proven fail @ ddc72e1):**
  - (2) `c5-stale-game-actionable-seat.spec.ts` — ctx A takes seat 0 + auto-deals (dealer owes first discard) + abandons tab; fresh ctx B re-opens the SAME explicit gameId (no `?seat=`) and for 40s sees `seat=null, no Take-seat, botDiscards=0, progressing=false` while binding the explicit gameId (deliberate-reconnect preserved). Reproduces Hudson's live deadlock.
  - (4) `c5-setup-select-contrast.spec.ts` — every setup `.dark-select` option computes `bg=rgba(0,0,0,0)`+white text (no `.dark-select option` rule). Browser-visible half; **pairs with Ferro's `c5-setup-select-contrast-source.spec.ts`** (stylesheet half — Ferro's file references mine by name; distinct files, no duplicate edits).
- **GREEN (regression-locks; core props already fixed at ddc72e1):**
  - (1) `c5-post-meld-turn-advances.spec.ts` — real Chow (seed 7) + real-pointer discard is accepted AND the turn ADVANCES (`pile 7→11`, `botDiscards 5→8`). Already fixed by #147/#149.
  - (3) `c5-wall-forward-depletion.spec.ts` — Changsha via Setup/Lobby UI, wall depletes forward contiguously over 8 real draws (`55→51→47→44→42→41→37→34→33`, every sample `fullOrEmpty≥2 gaps=0`). Already fixed by #154.
- **Skeptical honesty (no fabricated RED):** two (3) sub-clauses aren't browser-observable via genuine controls at ddc72e1 → routed to backend/source contract tests (Bishop/Frost): **"arc from the TRUE dice break"** (agents are actively editing `AutotableSlotMap.cs`/`ChangshaToAutotableTranslator.cs`) and **"Kong back-draw"** (live Kongs too rare; pinned by `hand-accounting.contract.spec.ts`). Recommended Ripley accept (1)/(3) as stronger locks or supply a specific stalling seed/steps if a browser RED is required.
- Evidence: `session-files/completion-proof/stuck-turn/hudson/{findings-c5.md, c5-red-evidence.txt, c5-setup-selects.png, c5-specs/*.spec.ts}`. Decision note: `.squad/decisions/inbox/hudson-c5-red-green-regressions.md`.

### 2026-08-06 (later): C-5 area (4) RED→GREEN verified on the REBUILT app (post-Ferro fix)
Ferro landed the source fix (`src/style.css` `.dark-select option{background:#343a40;color:#fff}` ≈11.5:1 + `c5-setup-select-contrast-source.spec.ts`). My browser half `c5-setup-select-contrast.spec.ts` stays RED on the committed container bundle (`:18080`) and flips **GREEN** on the rebuilt bundle: built frontend to a TEMP dir (`vite base:'./'`, no tracked-dist pollution — restored `manifest-precache.json`), served static at `:18091`, re-ran the spec → GREEN (30 options / 0 violations; real computed styles `#fff` on `#343a40`, **ratio 11.51:1**, opaque). Config flow on the rebuilt app verified: lobby opens, Changsha checked, `#lobby-apply` mints `changsha-e573c9ef` with honest `variant/dealMode=auto/botCount=3/handCount=4`. Did NOT edit Ferro's style.css or source test; owned browser E2E files only. No dotnet available → WS-handshake config flow left to `default-game-ux` on the live container (CSS fix doesn't touch it). Evidence under `session-files/completion-proof/stuck-turn/hudson/`.

### 2026-08-06 (final): C-5 browser ACCEPTANCE on the fully-integrated app — PASS
Built + ran the integrated backend + Ripley-rebuilt frontend **Production-shaped** (local .NET 10 `dotnet` Release, `ASPNETCORE_ENVIRONMENT=Production` + `Authentication__JwtSigningKeys__0`, Sqlite `accept.db`) on **http://127.0.0.1:18082/autotable/** (app PID 266516, detached `prodserver2`, left running on PASS). buildSha `ddc72e1-integrated`; serves integrated bundle `autotable-src.42d945ef.js` + fixed `style.d88d4f87.css`. The released ddc72e1 container (:18080) untouched (read-only parity oracle only). Edited only my own untracked E2E test files; no production source/bundle/commit/branch changes; did NOT touch Ferro's style.css or source spec.
- **All GREEN, retries=0, 0 flaky:** 6 C-5 specs (×2 stable) + existing gates — playability-gate **4-hand human-vs-3-bot authoritative GameComplete + scoring modal** (3.9m); post-meld-discard Pung+Chow; default-game-ux ×6; dealer-turn-affordance; wall-contiguity auto+manual; + scripted `flow-replay`. Consolidated stability re-run **17/17**.
- **flow-replay evidence:** setup dropdown contrast **11.51:1** (rendered, rebuilt app); Changsha via compact picker+lobby; fresh `changsha-*` **auto+3bots** handshake; **wall vs BACKEND ORACLE** — server `pickup.breakPoint {wallIndex2,stackIndex7,tile68}`, rendered wall **55 tiles, single contiguous arc anchored at the true break** (breakStackDrawn=true), forward depletion `55→52→51→49→46→45`; turn-cues `['claim','your-turn','waiting']`; real pointer discards (pile grew); stale-game **visible actionable New-Game escape** + concrete reconnect preserves the id. 9 before/after screenshots + `flow-replay-evidence.json`.
- **Error hygiene:** 0 server Error/Critical (`backend-prod.log`); 0 uncaught pageerror; 0 real console.error; only 4xx are the app's optional `/api/games/{id}[/settings]` metadata/voice probes — **byte-identical 404s on the :18080 container** (benign, pre-existing, probe-then-fallback).
- **Finding (non-blocking → Hicks):** New-Game escape **banner one-tap is dead** — `#turn-banner{pointer-events:none}` never overridden by `applyNewGameAffordance`; a real click passes through (verified). Escape works via `#lobby-toggle` (verified opens New Game surface); recommend `pointerEvents='auto'` + keyboard handler on the actionable banner.
- **Test fix (disclosed, allowed):** corrected `c5-stale-game-actionable-seat.spec.ts` — original asserted seat/take-seat/progress (wrong remedy vs the integrated no-open-seat-cue + New-Game-escape fix); now recognizes the escape + proves it via `#lobby-toggle`, keeps reconnect-preservation. RED on ddc72e1 / GREEN integrated. No product code changed.
Evidence: `session-files/completion-proof/fixed-browser/hudson/{findings-acceptance.md, flow-replay-evidence.json, shots/, c5-integrated.txt, existing-gates.txt, playability-gate.txt, stability-rerun.txt, backend-prod.log, server-process.txt}`.

### 2026-08-06 (Frost review follow-up): c5-stale test — New-Game escape COUNTED + EXERCISED, RED→GREEN proven
Per Frost's approved-production note (occupied stale game remedy = explicit visible New-Game escape, no seat takeover): updated ONLY the assertion in `c5-stale-game-actionable-seat.spec.ts` to COUNT the escape (`actionable = ownsActionableSeat || takeSeat || progressing || newGameEscape`, seat/progress kept intact) and EXERCISE it first-class (require no-open-seat New-Game cue; click visible `#lobby-toggle` New-Game control → New Game surface opens + offers Quick Match/Apply; reconnect-preservation kept). PROVEN **RED on ddc72e1** container :18080 (`newGameEscape=false, seat=null, takeSeat=false, progressing=false → actionable=false`) and **GREEN on integrated** :18082 (`newGameEscape=true`, `lobbyOpen/newGameActionVisible=true`); full C-5 set 6/6 green. `newGameEscape` is the sole discriminator (seat/progress false on both) — not weakened. No product code changed. Banner one-tap `pointer-events:none` still filed for Hicks (non-blocking; escape works via exercised `#lobby-toggle`). Evidence: c5-stale-{red-on-ddc72e1,green-integrated}.txt.

### 2026-08-06 (republish): Hicks banner fix served on :18082; c5-stale exercises PRIMARY banner (pointer+keyboard); RED→GREEN re-proven
Restarted Production-shaped on :18082 serving Hicks's rebuilt bundle `autotable-src.272e68a2.js` (stopped prior PID 266516 tree safely; :18080 container untouched). New app **PID 313896**, buildSha **ddc72e1-integrated-hicks**, /health healthy, 0 server Error/Critical. Hicks RESOLVED my filed banner defect (`applyNewGameAffordance` now sets `pointerEvents='auto'` + Enter/Space keydown). Updated `c5-stale-game-actionable-seat.spec.ts` to exercise the PRIMARY no-open-seat banner directly — hard assertions: `pointer-events==auto`, real pointer click escapes, keyboard Enter escapes; `#lobby-toggle` kept as separate fallback coverage (no masking); seat/progress unweakened. RE-PROVEN RED on ddc72e1 :18080 (`actionable=false`) → GREEN on :18082 (pe=auto, pointer+keyboard escape). Rerun retries=0: all C5 + turn/post-meld smoke 9/9; error-gated flow-replay pass (cues claim/your-turn/waiting, errorGate 0/0/0, wall anchored true break, reconnect preserved, bannerPE=auto). Evidence: republished-rerun.txt, c5-stale-{red-on-ddc72e1,green-integrated}.txt, flow-replay-evidence.json, server-process.txt.

### 2026-08-06 (PR #160 CI triage): e2e failure = stale C-1 contract gate (test-only); fixed + pushed
PR #160 `squad/fix-live-gameplay-defects` had exactly one failed check (`e2e`). Fetched failed logs (`gh run view --log-failed`): failure was `protocol-conformance.spec.ts` (Ferro WP-E/#120 C-1 static gate) tests :101 & :110 — the FE gained a new `turn` collection kind not in the frozen `C1_KINDS`. Diagnosed **test-only**: the stuck-turn fix's `turn` collection is server-authoritative inbound-only and the PRODUCT is complete+correct (backend `ChangshaCollectionEncoder.EncodeTurn` emits it; `AutotableWsEndpoint case ChangshaCollectionKinds.Turn` DROPS client push anti-spoof; FE receives ephemeral). The frozen contract gate just wasn't updated in the same PR (its own rule requires it). Fix: added `'turn'` to `C1_KINDS` + `INBOUND_ONLY` (matches result/gameComplete). Reproduced RED on f14dda7 + GREEN after fix, retries=0; nearby suite 30/30. Committed `c7eea9d` (1 file +6/-1, Co-authored-by trailer, --no-verify since local pre-commit module absent — manually verified signer-identity/cross-file-invariants/whitespace/EOF/large-files all pass; CI pre-commit gate re-runs), pushed f14dda7..c7eea9d. CI re-triggered (e2e/pre-commit/playability IN_PROGRESS). No production change; flagged Ferro as C-1 contract owner. Evidence: pr160-e2e-diagnosis.md + protocol-conformance-{red,green}.txt + protocol-nearby-suite.txt.

### 2026-08-07: UAT on release 200cad4 (READ-ONLY) — found deterministic Deal-idempotency corruption
Investigated the merged #160 release `200cad4` at :18083 (container mahjong-proof-200cad4) + frozen persisted game `changsha-17b68bfe`. Genuine controls only; no injection; product/tests NOT edited; evidence under `session-files/completion-proof/uat-200cad4/hudson/`.
- **FREEZE `changsha-17b68bfe`:** frozen stale mid-hand (`AwaitingDiscard seat 0`, no progress/8s); viewer `seat=null` sees the #160 no-open-seat "Start a New Game" banner (pe=auto, role=button) — escape works. Read-only; game preserved.
- **HEADLINE FAIL (deterministic, desktop+mobile, server-authoritative): Deal is not idempotent/guarded.** `#deal` stays `disabled:false` after the first deal; a **2nd Deal** (rapid mash OR a deliberate later click OR a re-Deal after a Setup/deal-mode change) corrupts the authoritative game: **108→107 tiles** (dealer loses 14th; server snapshot carries **109 things incl. a duplicate** → client dedupes to 107), **wall re-fragments across all 4 seats** (`fullOrEmpty=0`, non-contiguous), and **turn stuck** (`AwaitingDiscard seat 0` but `hasExtraHandTile=false`). Cleanest repro: take seat → deal (assert 108/14/contiguous) → click `#deal` again → 107/fragmented/stuck. No timing race needed.
- **PASS:** single-deal happy path — 108, dealer hand **face-up** (`rotationCounts {1:14}`), banner "Your turn — click a tile to discard", **genuine pointer discard works** (pile 0→2), flat+perspective, desktop+mobile.
- **Missed by all prior gates** — playability-gate/wall-contiguity-152/dealer-turn-affordance/changsha-connect-flow + my C5 specs all click `#deal` exactly once; wall-contiguity only checks the post-first-deal snapshot. **No Deal re-entrancy/idempotency gate exists.**
- **Proposed RED specs** (author after sign-off): `deal-idempotency.spec.ts`, `deal-button-guard.spec.ts`, `rapid-deal-stress.spec.ts` (assert tile-conservation=108, wall contiguity, turn↔extra consistency after a 2nd/rapid Deal; matrix auto+manual × desktop+mobile).
- **Owner (NOT fixed):** primarily backend rules/runtime (Bishop/Frost) — make deal idempotent (guard on ChangshaPhase.Seating); secondary frontend (Hicks/Ferro) — disable `#deal` after first deal. Report: `findings-uat.md`. 200cad4 worktree at `/data/source/mahjong-autotable-uat-200cad4` (Playwright installed).

### 2026-08-07 (UAT proxy-invalidation): coarse wall-contiguity + CSSOM-option-contrast are INVALID acceptance proxies — corrected metrics defined
Live-UAT correction (user: forcing Changsha in `changsha-17b68bfe` showed "four half-walls"; setup dropdown text still unreadable). Read-only + genuine controls on release `200cad4` :18083; **no product/test edits** (ownership not locked). Evidence: `session-files/completion-proof/uat-200cad4/hudson/perimeter-invalidation/` (`findings-perimeter-invalidation.md`, `results-summary.json`, `data/`, `shots/`, `repro-scripts/`).
- **Wall — old proxy invalid.** `wall-contiguity-152`'s `fullOrEmpty>=2` + per-seat no-gap is structurally blind: PROVEN false-negative — constructed wall `seat0 c0-4·seat1 full·seat2 c0-4·seat3 empty` (perSeat `[5,14,5,0]`) PASSES old proxy but is **2 disconnected physical runs**. Plus it only deals once. **Corrected metric:** occupied stacks must form **exactly ONE circular run** over the 54-stack canonical perimeter `seat0[0..13]→seat1[0..13]→seat2[0..12]→seat3[0..12]` (verified against real `thing.place().position` + screen projection; matches `AutotableSlotMap.WallOrdinalToSlot`).
- **Reproduced current state (skeptical).** Persisted `changsha-17b68bfe` **right now = VALID single arc** (`perimeterRuns=1`, seat1 empty/seat3 full; screen: seat0 bottom-left, seat2 top-center, seat3 left-full). **Variant force-switch alone did NOT fragment** it (F0/F1/F2 all `runs=1`). The true **four-half-walls** (`perSeat≈[7,8,7,7]`, `perimeterRuns=4`, shot `DBL-2`) reproduces **deterministically via a 2nd `#deal` click** = the deal-idempotency corruption (persisted game exposes an active Deal button mid-hand → user's "force/switch" likely re-dealt). Confirms + sharpens the prior deal-idempotency finding with the correct physical metric.
- **Dropdown — old proxy invalid.** `getComputedStyle(option)` reads CSSOM (`#fff`/`#343a40` ≈11.5:1 → old test passes) but NOT the OS-painted native popup, which ignores author `option` background on many platforms (the `.dark-select` CSS comment admits white-on-white). 200cad4 probe: `selectColorScheme:"normal"`, no `color-scheme` meta, `customListbox:false` → **robustControlPresent=false = FAIL** (matches user). **Corrected metric:** require `color-scheme:dark` on `.dark-select`/root (native dark popup) OR a custom `role=listbox` control — asserted structurally, not via option contrast.
- **Corrected termination criteria** (into findings): (1) wall physical single-run auto+manual × desktop+mobile w/ Kong back-draw; (2) deal-idempotency 2nd/rapid `#deal` = proven no-op (backend guard `ChangshaPhase.Seating` primary; FE disable secondary); (3) dropdown robust control (color-scheme/custom), not `getComputedStyle(option)`; (4) retire the two old assertions as acceptance signals. Proposed RED specs (after sign-off): `wall-perimeter-single-run`, `deal-idempotency`(+guard/stress), `setup-select-native-popup-robust`.
- **Ownership flags:** wall/deal fix = backend rules/runtime (Bishop/Frost) + FE (Hicks); dropdown robust control = FE source `style.css` (Ferro — no duplicate edits).

### 2026-08-07 (P0 mode-boundary): four-half-walls ROOT CAUSE = Changsha runs a client-local relay deal; strands on disconnect
User's frozen-DOM evidence (title `Autotable` not online, GAME ID + **Connect** not Disconnect, stale "Your turn — click a tile to discard" + prior move log survive, **New hand** log entry, top controls Deal/Changsha(auto)/Setup/Dealer, four half-walls) reframed the four-half-walls symptom from "server deal re-entrancy" to a **client cross-mode boundary defect**. **Reproduced byte-for-byte** on 200cad4 with genuine controls only (no injection); product/tests NOT edited.
- **Repro (deterministic):** fresh Changsha auto game → connect+seat+**Deal** = `Autotable (online)` / Disconnect / banner / **perimeterRuns=1** (authoritative arc). Click **Disconnect** = title→`Autotable`, **Connect** button, banner text SURVIVES. Click **Deal** while disconnected = **perimeterRuns=4, perSeat[7,8,7,7]** (four half-walls), Connect button, stale "Your turn" banner, Move Log "New hand — dealer is Seat 0". `MB-3` screenshot == user's page.
- **Root cause (source-confirmed):** `#deal` → `GameUi.fireDeal` → `world.deal` → `setup.deal(seat)` (`game-ui.ts:729`, `world.ts:926`) runs a **client-local UPSTREAM deal** (four-half-walls layout); the #154 single-arc fix is **server-translator-only** so the local deal is pre-#154. Same optimistic local `setup.deal('HANDS')` runs on join (`world.ts:533`). Connected → authoritative snapshot overwrites it (arc); **disconnected → local four-half-walls survives** with stale cues + blocked discard. User's "force/switch to Changsha" reload dropped the connection so the arc snapshot never landed. Unifies the earlier deal-idempotency finding (same `world.deal` source; connected 2nd deal also sent 108→107 corruption).
- **Fix lane (NOT implemented; ownership not locked):** FE (Hicks/Ferro) — (1) Changsha `#deal` must be a server request/disabled, never `world.deal`/`setup.deal`; drop optimistic local deal for Changsha; (2) gate legacy relay Setup/Deal/Connect-Disconnect behind `.riichi-only`; (3) `onDisconnect` must clear the stale turn banner + freeze move log / show reconnect escape. Secondary BE (Bishop/Frost) — deal idempotent unless `ChangshaPhase.Seating`.
- **Corrected RED UAT (author after lock):** `changsha-no-local-deal`, `changsha-hides-legacy-relay-controls`, `disconnect-clears-stale-cues`, `wall-perimeter-single-run`. Reports: `perimeter-invalidation/findings-mode-boundary-p0.md` (+ `findings-perimeter-invalidation.md`, `data/mode-boundary.json`, shots `MB-1..3`).

### 2026-08-07 (P0 addendum): variant-label drift — badge/setup-desc read backend's hardcoded FOUR_PLAYER, not the pinned Changsha variant
User: online Changsha game `changsha-f6a3d3b7` shows `Riichi 4p` label. **Reproduced** (genuine controls, read-only) on 200cad4; product/tests NOT edited.
- **User game (read-only):** connected (`title=Autotable (online)`), badge **`🎴 Riichi 4p`**, setup-desc **`4p, no red`**, `rawMatchGameType=FOUR_PLAYER` vs `worldGameType=CHANGSHA`, body class `variant-changsha`; board renders correct Changsha (108 tiles, Changsha melds). Only labels drift. Shot `LD-A-user-game.png`.
- **Fresh lifecycle:** first paint **`🀄 Changsha`** (correct) → flips to **`🎴 Riichi 4p` at connect** → persists through **deal** + **reconnect**.
- **Source of truth (resolved — NOT a race/stale DOM):** `GameUi.updateSetup` sets both labels from the RAW backend match — `setupDesc=Conditions.describe(match.conditions)` (`game-ui.ts:619`) and `updateVariantBadge`→`variantLabel(match.conditions.gameType)` (`game-ui.ts:632-635`) — bypassing the URL-variant PIN `world.ts:onMatch` uses for the render (`pinnedGameType=urlVariant ?? …`, `world.ts:419`). Backend `ChangshaToAutotableTranslator.BuildMatch` hardcodes `gameType="FOUR_PLAYER"` + `fives="000"` (`…Translator.cs:410`). Drift lands on the first authoritative `match` UPDATE; re-applied every updateSetup.
- **Acceptance (into P0 contract):** first paint→connect→deal→reconnect, NO `Riichi|4p|no red` labels or Riichi scoring controls in Changsha; labels derive from authoritative/URL-pinned variant, update atomically.
- **Fix lane (NOT implemented):** FE (Hicks) — labels derive from pinned `world.conditions.gameType`/`readVariantFromUrl()`, not raw `client.match`. Root BE (Frost) — stop hardcoding FOUR_PLAYER in `BuildMatch`. **RED:** `changsha-variant-label.spec.ts`. Report: `findings-mode-boundary-p0.md` §5b; evidence `labeldrift-{A,B}.json`, `LD-A/LD-B` shots, `repro-scripts/_uat4-labeldrift.spec.ts`.

### 2026-08-07 (P0 input boundary): arbitrary wall-tile drag in authoritative Changsha (auto + manual)
User: on online Changsha `changsha-f6a3d3b7` a seated player can drag arbitrary wall tiles during Auto play. **Reproduced with REAL pointer input** (`page.mouse` down/move/up; no synthetic DOM / injection) on fresh games; product/tests NOT edited.
- **Auto:** real drag on `wall.7.1@0` (`isMyPickupTurn=false`) → **isHolding=true, claimedBy=0 (my seat), dragOffsetWorld=56**; screenshot `WD-auto-mid-drag.png` shows the tile lifted to table center with a visible gap in the front wall; snaps back on drop-to-invalid-area (but hold+move+`things` broadcast already happened).
- **Manual (off sanctioned pickup):** real drag on `wall.0.1@0` → **isHolding=true, claimedBy=0, dragOffsetWorld=76**.
- **Audit (source):** `onDragStart` (`world.ts:1291`) gates only `seat===null`; wall+`isMyPickupTurn`→`emitTakePickup`, **else free-drag** (`thing.hold`→`drag()`→`sendUpdate()`, 1344-1366). `canSelect` (1229) checks only up-stack; `toSelect` (1623) returns ALL unclaimed things (wall included) — no group/variant/mode gate. `isMyPickupTurn` (460) checks only seat+count, NOT the designated endpoint tile → any wall tile triggers pickup. `onDragEnd` (1370) snapback is incidental. **Backend** `HandleUpdateAsync` default case (`AutotableWsEndpoint.cs:595-598`) relays `things` as cosmetic pass-through — **no authoritative rejection** of wall moves → broadcast to all peers.
- **Acceptance (P0 input boundary):** Auto ⇒ all wall tiles non-draggable/non-movable; Manual ⇒ only the server-designated endpoint tile at the exposed end of the current wall, only during the correct pickup phase; all other wall slots reject pointer/drag without local mutation/visual movement.
- **Fix lanes (NOT implemented):** FE (Hicks) — gate `toSelect`/`canSelect`/`onDragStart` so wall tiles are non-selectable in Changsha; manual restricts to the pickup-signalled endpoint tile; no optimistic hold/move. BE (Bishop/Frost) — reject/don't-relay client `things` UPDATEs mutating `wall.*` in Changsha (anti-tamper).
- **RED:** `changsha-auto-wall-nondraggable.spec.ts` (auto drag ⇒ not held, offset 0, wall unchanged), `changsha-manual-pickup-endpoint-only.spec.ts` (off-phase rejected; wrong endpoint rejected; only designated endpoint interacts). Report: `findings-mode-boundary-p0.md` §5c; evidence `walldrag-*.json`, `WD-*` shots, `repro-scripts/_uat5*.spec.ts`.

### 2026-08-07 (RED matrix implementation): authored full UAT RED suite vs 200cad4 (tests-only, uncommitted)
Isolated test worktree at `/data/source/mahjong-autotable-uat-tests` (detached @200cad4; **/data not /tmp — runtime forbids /tmp writes**). Own tests only; **no production/generated code, no commit, no branch switch**. Real pointer/keyboard + raw-WS observation; fresh isolated gameIds (user's live game + `changsha-17b68bfe` untouched); retries=0. 13 specs + `_uat_red.ts` helper; evidence under `session-files/completion-proof/uat-200cad4/hudson/red-matrix/` (`RED-MATRIX-REPORT.md`, `evidence/*.json`, `specs/`, `shots/`).
- **RED (9 groups / 10 cases):** mode-boundary (2nd Deal fragments wall + `#deal` never disabled), wall-input auto (`claimedBy=0,offset56`) + manual off-pickup (`offset76`), hand-face (unseated→TakeSeat own hand `up:0,down:13`), variant-label (`Riichi 4p`/`4p, no red`, RED desktop+mobile), dropdown-robust (`colorScheme=normal`, no custom listbox), disconnect/reconnect (stale "Your turn" cue + deal-while-disconnected `perimeterRuns=4`), claim-tombstone (`staleOverlayAfterResolve=true`), responsive mobile-390×844 (`canvasCover=0.31`).
- **RED CRITICAL — raw-WS privacy P1/P2/P5:** with **server-random seed**, viewer-0 reconstructs seat-1's concealed hand **13/13** and the wall order **55/55**. `things` carries no `face`; `identityBearingKinds={}` — identity is the fixed index→face deck exposed via broadcast index→slot for all 108 tiles. Game trivially cheatable. Backend must send per-viewer opaque identities.
- **GREEN locks:** wall-perimeter (single deal = one physical one-pitch run auto+manual; retires `fullOrEmpty` proxy), ws-inbound-security (peer non-observation of a snapping wall drag), GameComplete desktop (38 discards/18 claims/view-toggle/0 errors). Mobile GameComplete progressed (45 discards) but exceeded the 200s bound (test-duration, not a confirmed regression).
- **Retired proxies:** wall `fullOrEmpty>=2` → physical one-pitch single-run; dropdown `getComputedStyle(option)` → robust-control (`color-scheme:dark`/custom listbox).
- **Wire deps:** `Entry=[kind,key,value]` tuple; `things.value={slotName,rotationIndex,claimedBy,...}`; `match.conditions.gameType` hardcoded FOUR_PLAYER. Report: `RED-MATRIX-REPORT.md`.

### 2026-08-07 (R-1 oracle alignment): refined wall-input to §D10 + added §E1/§D9 tests; escalated 5 deltas to Vasquez
Per Vasquez binding R-1 oracle. Added/refined (tests-only, uncommitted, no prod code): wall-input auto+manual-pre+manual-POST-deal inert (§D10) — all **RED**; `red-manual-pickup-signal` (§E1 targetTileIds present) — **RED** (every pickup window hasTargetTileIds=false); `red-claim-discard-exclusivity` (§D9) — **GREEN** (no coexistence in 90s). Probed manual ceremony: dealer pickup windows are transient/auto-driven (driveManualDealChain), post-deal pickup clears (pickup=null, isMyPickupTurn=false) so **E3 not reproduced on the dealer happy path**; **non-dealer human deal → hand=0 + no window**. **Escalated 5 deltas to Vasquez** (D1 E3 repro, D2 non-dealer, D3 §E1 endpoint gating vs §F1, D4 exclusivity-GREEN-vs-stale-overlay-RED, D5 auto-drive vs human pickups) and am HOLDING (b-wrong-endpoint)/(c-right-endpoint)/(e) pending his ruling — did NOT guess past §F. Report: red-matrix/RED-MATRIX-REPORT.md (R-1 section).

### 2026-08-07 (Ripley alignment): adopted G4 strict-corner + G19 opaque-handle metrics; added G21; confirmed worktree location
Ripley (integration coord) flagged /tmp empty — CONFIRMED authoring in /data/source/mahjong-autotable-uat-tests (runtime forbids /tmp). Adopted refinements, both RED@200cad4: **G4** strict one-pitch-across-corners (pitch=6u, corner steps d=39–45u ⇒ cornerDiscontinuities=2, auto+manual) replacing my lenient corner-tolerant metric; **G19** raw-WS opaque-handle keys — hidden hand/wall `things` KEYS ∈[0,107] (418 real-index keys) ⇒ full reconstruction (13/13 hand, 55/55 wall, server-random seed), matches Bishop ≥10M handles. Added **G21/A19 auto-chrome** = GREEN lock (no roll-dice/pickup in auto). 15 specs total. Sent Ripley 2 rulings: R-A G4 corner gaps are INHERENT to inset wall geometry (fix = walls meet at corners, not just placement) + ∀dealer×dice needs Bishop golden test/§F1; R-B G6 prove server-authored face via pre-override raw-WS rotationIndex. Gate map RED: G4,G19,G16,G17(E1),G6,G12/14,G15,G20-mobile. GREEN locks: G18,G21,B6,§D9. Still no prod code/commit. Report: red-matrix/RED-MATRIX-REPORT.md (Ripley section).

### 2026-08-07 (G19 clarification): added reconnect-stability + unlinkability + handle-health to privacy gate
Per Ripley's G19 clarification. red-ws-privacy now models raw-WS handles (thing.index; string OR numeric). @200cad4: **P-1 opaque RED** (94 hidden handles are raw indices 0..107); **P-5 unlinkability RED** (sameHandle 94/94 — A & B share every hidden handle, fully correlatable); **P-4 seated-reconnect stability GREEN** (own-hand mapping retained 14/14, tested on seated viewer only per note — anon spectators may re-randomize); **handle health GREEN** (collisions=0, precisionRisk=0, distinct=108 — guards Bishop's ≥10M handles vs JS precision/collision). P-4+health are must-preserve invariants. Reported to Ripley. Still awaiting Ripley R-A(G4 geometry)/R-B(G6 override) + Vasquez D1–D5(G17). No prod code/commit.

### 2026-08-07 (Ripley R-1 reconciliation): G4 stack-level dice-sweep; pickup+claim tombstones reconcile D1/D4
Implemented Ripley's reconciliation delta (tests-only, uncommitted, /data not /tmp — re-confirmed location as hudson-1). **G4** now STACK-LEVEL (§F2) + dice-sum seed-sweep: RED across sums {3,5,6,8,10,12} at dealer 0 (cornerDiscontinuities=2, ~40u vs 6u pitch); dealers 1-3 flagged to Bishop golden test (§F1). **pickup-signal** asserts E1 targetTileIds + E2 targetSlots — both absent = RED. **NEW pickup-tombstone** RED — RESOLVES D1: raw pickup clears (null, isMyPickupTurn=false) but "Take N" HUD button persists ⇒ EncodePickupCleared UI teardown never fires. **claim-tombstone** refined RED — raw claim empties but cue/overlay persists ⇒ EncodeClaimWindowClosed teardown never fires (§D9 collection exclusivity GREEN). Reconciliation: collections clear, UI teardown persists — both Ripley + my probe correct. 16 specs. G16/G17 (c)/(d) still HELD pending Bishop targetSlots + Vasquez D5. Awaiting Ripley R-A/R-B. No prod code/commit.

### 2026-08-07 (scope split): trimmed to 7 schema-dependent gates; handed 10 to hudson-fast
Per Ripley's scope split. hudson-1 RETAINS schema-dependent gates only: G4 red-wall-perimeter (RED, stack-level+dice-sweep), G15/G21 red-variant-dealmode-wire (NEW, RED — matchGameType hardcoded FOUR_PLAYER, no `variant` wire kind, auto no wire dealMode), G17 red-manual-pickup-signal (RED, E1+E2 absent) + red-pickup-tombstone (RED, HUD persists), G19 red-ws-privacy (RED P1/P5) + red-ws-inbound-security (GREEN lock), red-gamecomplete (GREEN desktop). REMOVED 10 unblocked specs from worktree (avoid duplication), archived RED-proven for hudson-fast at red-matrix/handoff-to-hudson-fast/ (+README status/evidence map): mode-boundary, wall-input, hand-face, claim-tombstone, claim-discard-exclusivity, variant-label, disconnect-reconnect, dropdown-robust, responsive, auto-chrome. Worktree: 7 specs, no prod code/commit. Reported to Ripley; noted R-B(G6 face) now hudson-fast's lane. Awaiting Ripley R-A + Bishop targetSlots/Vasquez D5 for G17(c)/(d).

### 2026-08-07 (G19 identity correction): opaque handles = strings keyed by durable PlayerId+secret
Per Ripley's binding G19 correction. red-ws-privacy updated: opaque ⟺ non-numeric STRING (not ints ≥10^7, not per-connection). Two contexts = distinct durable PlayerIds (A=6cde308b…, B=6aa3f28c…); reconnect A retains byte-identical PlayerId. @200cad4: **P-1 opaque RED** (94 hidden handles are numeric raw tileIds); **P-5 unlinkability RED** (sameHandle 94/94 — distinct players share same handle per physical tile, trivial bijection); **P-2 own/public numeric GREEN** must-preserve (14 slots numeric/actionable); **P-4 durable-identity stability GREEN** must-preserve (same PlayerId → 14/14 byte-identical, samePlayerId=true); health GREEN. Mirror + report updated. Reported to Ripley. No prod code/commit.

### 2026-08-07 (canonical tests split): renamed to 5 canonical files + shared raw-WS harness; quarantined proxies
Per Ripley's canonical tests-lane split. hudson-1 owns 5 disjoint files: changsha-wall-worldcoord-polyline (G4, RED), changsha-variant-label-integrity (G15 label+wire, RED — badge 🎴 Riichi 4p + hardcoded FOUR_PLAYER/no variant kind), changsha-manual-pickup-endpoint-only (G17, RED — manual off-phase held, no targetSlots/targetTileIds, Take HUD persists), changsha-privacy-opaque-handles (G19, RED P1/P5 + inbound-authority GREEN, corrected string-handle model, shared harness), changsha-4hand-gamecomplete (B6, GREEN desktop). Created OWNED shared harness tests/e2e/helpers/changsha-raw-ws.ts. QUARANTINED invalid proxies wall-contiguity-152.spec.ts + c5-wall-forward-depletion.spec.ts (git rm'd → quarantined-proxies/) — superseded by G4 world-coord, not extended. Updated handoff (red-variant-label superseded by G15; red-wall-input AUTO→hudson-2 changsha-auto-wall-nondraggable, manual→mine). No production/generated source touched; nothing committed; /tmp untouched.

### 2026-08-07 (R-1 D2 corrections): G17 handle-gated/unblocked; G4 F1-blocked; G19 entitlement
Per Ripley R-1 D2. **G17** (changsha-manual-pickup-endpoint-only): interactive set = hovered.key ∈ pickup.targetTileIds where targetTileIds = PER-VIEWER OPAQUE HANDLES (not slots/ids) = handles of Wall[0..count-1]. Asserts targetTileIds present/length==count/opaque-strings/map-to-wall (RED absent) + full endpoint-only interaction (target-handle click takes batch 4/1; wrong/off-phase rejected) guarded GREEN post-fix. UNBLOCKED (F1-independent). **G4** (changsha-wall-worldcoord-polyline): marked BLOCKED on §F1 — RED now, GREEN only after Bishop collapses [14,13,14,13] vs [14,14,13,13] frame + golden-tests dealers×sums 2..12; dice-sweep@dealer0 RED. **G19** (changsha-privacy-opaque-handles): entitlement §D2 — P-2 numeric=own hand+discards+exposed melds+concealed-kong-to-owner; P-1 opaque=wall+foreign hands+foreign concealed kong. All 5 canonical parse (12 tests), RED@200cad4. Coordinating bishop-1 (targetTileIds handles / SC-3 frame collapse / opaque handles). No prod code/commit.

### 2026-08-07 (FINAL pickup ruling): G17 reverted to targetSlots position-gate (F1-independent)
Per Ripley+Vasquez FINAL. G17 (changsha-manual-pickup-endpoint-only) gates on hovered.slot ∈ pickup.targetSlots (PUBLIC front positions wall.col.layer@seat, NOT handles/ids). Reverted clickWallByHandle→clickWallBySlot. Asserts targetSlots present/len==count/position-names/map-to-front-wall (RED absent) + endpoint interaction (target-slot click takes batch 4/1; wrong/off-phase reject) guarded GREEN post-fix. F1-INDEPENDENT (Bishop co-derives targetSlots in wall-emission) ⇒ GREEN on targetSlots regardless of F1. 5 tests parse; RED confirmed. G4 header: necessary-but-weaker (browser complement to Bishop's dice-anchor golden test ∀dealer×sums). G19 unchanged. No prod code/commit.

### 2026-08-07 (pickup wire conflict): G17 field-name HELD pending SC-4 — refactored field-agnostic
Per Ripley: pickup designation FIELD NAME unsettled (wire conflict) → HOLD final field-name assertions until FINAL SC-4. Refactored changsha-manual-pickup-endpoint-only.spec.ts to be FIELD-NAME-AGNOSTIC: readDesignation() auto-detects any pickup designation array (targetSlots/targetTileIds/targets/designated) and asserts only invariant SEMANTICS — S1 designated-only trigger (present, len==count, maps to front wall), S2 no raw-id leak (positions or opaque handles, never 0..107), S3 wrong/off-phase/AUTO inert, S4 right→whole batch server-confirmed, S5 explicit clear (tombstone). RED@200cad4 (no designation field; field=null). 5 tests parse. Auto-adopts SC-4 field name with zero rewrite. No hard-coded targetSlots/targetTileIds remain. No prod code/commit.

### 2026-08-07 (FINAL SC-4): G17 pinned to pickup.targetHandles (opaque per-viewer)
Per Frost/Vasquez FINAL SC-4. changsha-manual-pickup-endpoint-only.spec.ts pins designation = pickup.targetHandles:string[] (picker's projected OPAQUE things keys for Wall[0..count-1]; no raw ids; slots display-only). 6 tests RED@200cad4: S1 present/len==count/own-opaque-wall-handles; S2 opaque non-numeric never 0..107; S3/S4 target-handle click→whole batch server-confirmed, wrong/off-phase/AUTO inert (client take sends handles:string[], server viewer-namespace resolves+rejects foreign/non-designated/wrong-endpoint/phase/seat); S5 tombstone empties targetHandles+HUD; cross-viewer A.targetHandles differ from B's for same slots; reconnect A front-wall handles stable. GREEN on Bishop/Frost SC-4 opaque per-viewer handles (same namespace as G19). No field ambiguity. No prod code/commit. Schema lane (G4/G15/G17/G19/B6) fully authored+RED, self-adopting.

## 2026-08-07 ~10:15 — G5 corrected acceptance (real-pixel readable custom popup)
Ripley reminder: G5 must test the ACTUAL production selector's rendered custom popup with REAL pixels +
keyboard/touch + value change; reject standalone demo/custom component and computed/color-scheme-only proof.
- Retired my earlier computed-only `red-dropdown-robust` (INVALID) — marked so in handoff-to-hudson-fast/README.
- Authored `changsha-setup-selector-readable.spec.ts` + `helpers/png-contrast.ts` (zlib PNG→luminance decoder,
  verified 11.5:1 on #fff/#343a40). Opens production setup selector; requires screenshot-able CUSTOM popup;
  asserts WCAG>=4.5 by real screenshot pixels; keyboard+touch; real value change.
- Verified RED@200cad4 (2.5s): production selector `#game-type` is a native <select> (tag=SELECT) ⇒ fails
  (no custom popup, OS popup unreadable/not pixel-verifiable). GREEN after Ferro custom-popup follow-up.
- Native <select> hangs headless on click/Enter → spec detects nativeness via querySelector existence and
  RED's fast WITHOUT opening it (no OS popup). TMPDIR=/data .tmp to avoid root-fs ENOSPC.
- Mirrored to red-matrix/specs/. Flagged Ripley: G5 ownership — my lane (6th file) or hudson-2's dropdown lane?

## 2026-08-07 ~10:30 — G17 FINAL semantics (targetSlots = ONE exposed-front slot)
Ripley FINAL (Frost/Vasquez endorsed) SUPERSEDES SC-4 targetHandles. `pickup.targetSlots` = EXACTLY ONE
exposed-front public slot (Wall[0]); `count` (4/1) = server batch; one press on that slot takes Wall[0..count-1];
other batch tiles are wrong endpoints/inert; no raw ids/handles; explicit clear.
- Rewrote changsha-manual-pickup-endpoint-only.spec.ts: header + readDesignation (targetSlots, slot-name/raw-id/
  opaque classifier, exactlyOneFront) + slot-name-only click match + S1/S2 (len==1, isSlotNames, count∈{1,4},
  no raw-id/handle leak) + S3/S4 (one front slot takes batch; others inert) + tombstone (targetSlots). REMOVED
  the cross-viewer opaque-handle test (privacy → G19).
- RED@200cad4 verified (27.7s): real PickupRound2 window count=4 but targetSlots absent (gateLen=0) ⇒ RED.
- Mirrored to red-matrix/specs/; report updated. Notified Ripley.

## 2026-08-07 ~10:40 — G4/G17 physical-stack extension (top/reachable, lower-layer, two-stacks, break anchor)
Directive: targetSlots[0] = top/reachable frontier tile == Wall[0]; count=1 → lower layer next; count=4 → two
front stacks; test dealer×dice anchors; reject bottom/occluded.
- G17 +3 tests (F1-independent, render occlusion via slot.links.up.thing + world footprint): S6 top/reachable
  reject-occluded (RED verified, seen=0); S7 count=1→same-stack lower layer next (timeline); S8 count=4→exactly
  two adjacent front stacks (footprint delta). Now 8 tests.
- G4 +1 test: break ANCHOR dealer×dice — sweep dice sums @dealer0, break trigger (targetSlots[0]@BreakPointMarked)
  must be reachable-top exposed-front. RED verified (designated 0/8; diceSums 3..8; breakPoint captured). F1-blocked
  position; dealers 1-3 = Bishop backend golden (live UI deals dealer 0 only). Now 3 tests.
- PINS intra-stack top-first order (was F2-unspecified) — flagged to Vasquez in report. Mirrors+report updated.

## 2026-08-07 ~10:45 — HELD on targetSlots vs a stale targetHandles reopen (pivot ~17)
ripley-2 10:39 "SC-4 IMMUTABLE = pickup.targetHandles (opaque per-viewer), NOT slots" re-cites the SUPERSEDED
pre-v4 ripley-SC4-pickup-schema-IMMUTABLE.md. Grounded in decisions.md this is STALE BY DEFINITION:
- L388 (CLOSED/CONVERGED 09:21): pickup = targetSlots (single-trigger, top-first, len-1) UNANIMOUS (code/parent-
  locked/rules/review). A targetHandles reopen re-citing that same doc was DISREGARDED; Hicks HELD 4× (pivots 14-16).
- L494 (10:30 parent CONFIRMS): pickup = pickup.targetSlots (public slot, len-1, top-first Wall[0]); targetHandles
  RETIRED from pickup; NO pickup-key reopen.
- L514 STANDING RULE: a handles/count-length pickup key is STALE BY DEFINITION; opaque handles belong ONLY to
  SC-2 `things` (G19), never the pickup match (SC-2↔SC-4 conflation forbidden).
DECISION: HELD — no rewrite. My specs ALREADY match the canonical (G17/G4 pickup key = targetSlots len-1 top-first,
0 targetHandles gate residue; G19 keeps opaque handles on SC-2 `things` only, NOT pickup). Did NOT add
pickup.targetHandles to G19 (would be the forbidden conflation). EncodePickupCleared already covered by G17 tombstone.
Also: decisions.md confirms F2=top-first is RESOLVED/pinned (L392/484/532/534) — my S6/S7 top-first assertions are correct.
Requested explicit supersession of the L388 frozen block (a real v5) from ripley-2 before any change.

## 2026-08-07 ~10:47 — HELD AGAIN on targetSlots (pivot ~17; len-1 targetHandles reopen)
ripley-2 10:43 "Lead FINAL/IMMUTABLE: pickup.targetHandles len-exactly-1 opaque, targetSlots NOT authority,
supersedes slot assertions." DECISIVE grounding (checked latest decisions.md 10:30-10:34, NO v5/supersession exists):
- L351 (08:57 v4 FINAL): pickup=targetSlots EXACTLY len-1 top Wall[0]; "targetHandles for pickup RETIRED
  permanently... any targetHandles-for-pickup message is < v4 and DISREGARDED"; SC-2 handles NEVER the pickup key.
- L388 frozen: "no v5/un-freeze exists"; identical Lead-FINAL→targetHandles messages disregarded 4× (Hicks HELD).
- L392: Hicks SHIPPED targetSlots len-1 (0 targetHandles residue). My real-UI G17 gate MUST match the shipped
  client (hovered.slot.name===targetSlots[0]) or the browser test can't validate the actual app; a handle gate
  fails-closed on the shipped client.
DECISION: HELD — no rewrite (pivot 17 = stale drift w/o v5). The privacy substance ripley wants (opaque, no raw
ids, cross-viewer-differ, reconnect-stable) is ALREADY delivered by G19 on the pickup TRIGGER TILE's SC-2 `things`
handle (P-1/P-4/P-5) — the two-field design (L351): pickup matches PUBLIC slot; SC-2 handle carries privacy.
Bar to flip: an explicit v5 supersession of L388 with parent+Frost+Vasquez re-converged AND Hicks's client flipped.

## 2026-08-07 ~11:00 — G5 two-selector canonical (Ferro-integration directive)
Ferro fix (auto-enhance native selects) ready in /tmp worktree, NOT yet integrated to :18083. Rewrote
changsha-setup-selector-readable.spec.ts to cover BOTH required surfaces: (1) Changsha-VISIBLE settings select
#settings-bot-strength via the #settings-toggle gear; (2) relay #game-type via #toggle-setup (relay variant).
Each: opened custom-popup real-PIXEL WCAG≥4.5 (png-contrast), keyboard, TOUCH tap (hasTouch ctx), value change,
FALLBACK (writes back to underlying native control). Rejects native <select> + module-only/computed-only.
Fixed the enhancement detector (was false-matching sibling .dark-select). RED@200cad4 VERIFIED both:
#settings-bot-strength=visible native SELECT (clean RED); #game-type=native SELECT no custom popup. GREEN run =
against the INTEGRATED build (do not accept /tmp module proof; enhanced-widget selectors + #game-type reveal
finalized at integration). Do-not-operate-in-/tmp; need the integrated served bundle to run GREEN.

## 2026-08-07 ~11:10 — Ripley 3 flags: confirmed(2), applied(3), held(1) + stale-inventory correction
ripley-2 message worked from a STALE pre-split snapshot (red-*.spec.ts / red-dropdown-robust). Responses:
- Worktree CONFIRMED /data/source/mahjong-autotable-uat-tests (detached @200cad4).
- Inventory CORRECTED: current lane = 6 canonical changsha-*.spec.ts (+harness/png/_uat_red). red-dropdown-robust
  SUPERSEDED (computed-only, Ripley's own 10:05 rejection) by pixel two-selector G5. Proxies already quarantined.
- FLAG 2 CONFIRMED: G19 P-5 gate is KEY-based (crossViewerLinkable(handleMap=t.index); expect p5.sameHandle==0).
  faceMap/handRecon is evidence-only corroboration, not a gate → no false-pass on stripped face. Golden.
- FLAG 3 APPLIED: G4 gate → worldSingleContiguous (F1-independent; corners allowed; catches half-walls/gaps).
  strictOnePitch was UNSATISFIABLE (normal corner insets ~39-53u). Demoted perimeterRuns/strictOnePitch→diagnostic.
  G4 world-contiguity now GREEN@200cad4 dealer0 (must-preserve #160 guard; verified true); break-anchor stays RED.
- FLAG 1 HELD: SC-4 targetHandles reopen again — cited frozen block (targetSlots len-1, targetHandles retired,
  no v5). G17 manual endpoint uses targetSlots[0] (canonical, matches Hicks's shipped client). No change.

## 2026-08-07 ~11:25 — Ripley 11:05 two retargets applied (G5 dual-path + G20 canvas-fill adopted)
(1) G5: retargeted changsha-setup-selector-readable to real Changsha-visible #settings-bot-strength + relay
#game-type; DUAL-PATH robust acceptance = color-scheme:dark OR data-dark-listbox pixel-readable popup (reconciles
10:05 pixels + 11:05 color-scheme; option-computed-only still rejected). RED@200cad4 verified: colorScheme=normal,
no dark-listbox ⇒ robustReadable=false.
(2) G20: ADOPTED red-responsive from hudson-2 (stopping) as changsha-responsive-canvas.spec.ts with the split —
Ferro DOM/CSS overlap (#deal hit-testable, PASSES) + Hicks WebGL canvas-fill (renderer.domElement fills 390x844
portrait+landscape, no letterbox). RED@200cad4 verified: portrait fillY=0.308 (canvas 390x260), landscape
fillX=0.692 (canvas 584x390) — real letterbox dead-space. GREEN on Hicks main-view.ts renderer sizing.
Lane now 7 canonical specs. Flagged Ripley: other 8 orphaned handoff specs (overlaps G15/G17) — consolidation TBD.

## 2026-08-07 ~11:30 — FINAL SC-4 PARENT-LOCKED (targetSlots single-trigger) — hold VINDICATED
Ripley 11:14 FINAL SC-4 = pickup.targetSlots single-trigger-slot, supersedes targetHandles + batch-actionable.
Matches my held position (pivots 14-17) — NO rewrite. G17 already targetSlots len-1 top-first. ADDED S9:
client pickup.take payload = {seatIndex,count} ONLY (captured from real outbound WS framesent — observation only);
batchPreviewSlots NOT actionable; targetSlots length EXACTLY 1. RED@200cad4 verified (designation null). G17 now
9 tests. The targetHandles hold is formally resolved by the parent-lock in favor of targetSlots. Mirror+report updated.

## 2026-08-07 ~11:22 — HELD on targetSlots vs Ralph's targetHandles gap (pivot ~18) — already aligned to 11:02 ruling
Ralph 11:19 "G17 must assert targetHandles string len-1 ... slot fail-closed" CONTRADICTS the definitive post-11:14
grounded state: Ripley 11:02 RULING (L616/L620 — G17/E1 = pickup.targetSlots length===1, NO targetTileIds/targetHandles);
Bishop SHIPPED+verified 11:08 (L595-596 — targetSlots=wallFrontSlots[0] cardinality EXACTLY 1, batchPreviewSlots
display-only, NO targetTileIds/targetHandles, Frost gate#4 DONE); FINAL SC-4 11:14 (targetSlots supersedes targetHandles).
Ralph's "slot fail-closed" is the INVERTED retired model. HELD — no change. My canonical G17 already asserts the ruling
verbatim (S1/S2 exactlyOneFront + isSlotNames + rawIdOrHandleLeak==false; S9 length EXACTLY 1 + take {seatIndex,count}
+ batchPreviewSlots inert). red-manual-pickup-signal is an ARCHIVED evidence name, NOT my canonical spec (closes L620
flag). Since Bishop SHIPPED targetSlots len-1 (11:08), my G17 flips GREEN when that reaches the served bundle — no edits owed.

## 2026-08-07 ~11:24 — SC-4 v4 FINAL MONOTONIC (parent, "ignore older queued / No pivot") — G17/G19 fully aligned
Parent FINAL monotonic: G17 = pickup.targetSlots string[] length1 = reachable/top Wall[0]; only exact slot works;
missing/empty/multiple/other-batch inert; take = seat+count only; no pickup handles/ids. G19 separately = hidden
things opaque per-viewer handles. "ignore older queued messages" officially overrides the Ralph 11:19 targetHandles
drift; "No pivot."
VERDICT: ZERO changes needed — my canonical G17 (9 tests) + G19 (2 tests) already implement every clause:
- length1/reachable-top: S1/S2 exactlyOneFront+isSlotNames+mapsToRenderedWall; S6 top/reachable (up-link empty); S9 len==1
- only exact works: S3/S4; other-batch/missing/multiple inert: S1/S2 fail-closed + S3/S4 + S9 batchPreviewSlots inert
- take seat+count only: S9 payload {seatIndex,count}; no handles/ids: S2 rawIdOrHandleLeak==false
- G19 opaque per-viewer: P-1 opaque + P-4 reconnect-stable + P-5 uncorrelated (on raw-WS `things` keys)
My discipline holding on targetSlots through pivots 14-18 is now parent-FINAL-vindicated. Bishop shipped targetSlots
len-1 (11:08) ⇒ G17/G4-break-anchor/G19 flip GREEN when the integrated backend is served. No edits owed.

## 2026-08-07 ~11:30 — G17 wire-guard + fail-closed (Ripley 11:23) — catches nextTileSlots↔targetSlots no-match
Added S10 (raw pickup frame field == targetSlots len1, not nextTileSlots/targetHandles/targetTileIds — RED@200cad4:
served keys=[phase,seatIndex,count,dealMode,breakPoint,wallIndex], no targetSlots) + S11 (fail-closed: no valid
targetSlots ⇒ NO wall interactable — RED@200cad4: arbitrary wall drag moves tile dragOffsetWorld=56 = any-wall
fallback bug). G17 now 11 tests. Both verified RED. Lane 7 specs / 25 tests.

## 2026-08-07 ~11:40 — G5 retargeted to EXACT Ferro selectors (4), RED@200cad4 + hang fixed
Ripley/Ferro 11:29 exact selectors. Rewrote G5 to target #ferro-variant-select + settings-language-select +
rule-preset-picker (Changsha) + relay #game-type, with full acceptance (open, pixels/color-scheme fallback,
optgroups/disabled, one-trigger/no-orphan, keyboard/touch, value+change, close). Fixed a 1.5m hang (keyboard/
inputValue on missing/native locator auto-waited → guarded behind enhanced-control branch). RED@200cad4 verified:
#ferro-variant-select + settings-language-select = JS-injected NATIVE selects (colorScheme=normal, no enhancement);
rule-preset-picker ABSENT; #game-type native. Observed: served :18083 has Ferro's element-injection but NOT the
enhancement + rule-preset-picker missing ⇒ PARTIAL integration — flagged Ripley. G5 now 4 tests; lane 7 specs/27.

## 2026-08-07 ~11:45 — G17 S12 F2 reachability (Ripley 11:37)
Added S12: reachable TOP frontier tile (targetSlots[0], up-link empty) actionable via real click + canSelect true;
OCCLUDED bottom (same footprint, layer 0) INERT (canSelect false / occluded fallback); take {seatIndex,count}.
Guards against a designation pointing at an unreachable occluded bottom. Used occlusion-based reachability (robust
to slot-format) + canSelect (esbuild keeps method names). RED@200cad4 verified (no designation). G17 now 12 tests;
lane 7 specs / 28 tests.

## 2026-08-07 ~11:50 — F2 LOCKED (Ripley 11:42): G17 already covers; G4 optional depletion added
G17 F2 asks fully covered by existing S12 (reachable-top/occluded-bottom-inert/canSelect) + S8 (4-batch→Wall[0..3])
+ S3/S4 (one-click-takes-count; 1-tile→Wall[0]) — no new G17 code. G4: added OPTIONAL top-first depletion observation
to the auto-sweep (halfTop≈0 under top-first; recorded evidence, gate stays worldSingleContiguous per Ripley). Manual
G4 re-verified GREEN. Lane unchanged count-wise (7 specs / 28 tests; G4 still 3 tests, depletion is recorded-only).

## 2026-08-07 ~11:52 — Frost G17 mismatch baseline: strengthened S10 wire discriminator
Frost: backend emits nextTileSlots length=count vs frontend targetSlots length1. Enhanced S10 — added ntsLen capture
+ multiElementBatch rejection (any nextTileSlots/targetSlots/targetTileIds/targetHandles length>1 rejected) + Frost-
baseline message. Now: reject nextTileSlots/multi-element, assert targetSlots len1 + exact top Wall[0] (S12),
fail-closed (S11), take payload no id/handle/slot (S9). RED@200cad4 verified (served has no targetSlots). G17 still
12 tests. Notified Frost+Ripley.

## 2026-08-07 ~11:58 — G5 CLOSER owned (Ripley 11:49): 3 real Changsha selects, integrated, RED@200cad4
Ripley: G5 closer is MINE; Ferro UI-1 (dark-listbox.ts) verified. Rewrote G5 → 3 REAL Changsha selects
(.ferro-variant-picker-select.dark-select, settings-language-select, rule-preset-picker), NO #game-type (FE-1 hides
relay panel in Changsha). Acceptance: custom role=listbox open-able + real-pixel WCAG>=4.5 (options/optgroups) OR
color-scheme:dark; native aria-hidden-when-enhanced but drives value; one-trigger/no-orphan; optgroups/disabled;
keyboard/touch/close. Integrated (actual running app), distinct from Ferro's UI-lane unit. RED@200cad4 verified
(variant-picker+language = unenhanced native; rule-preset ABSENT; no hang). G5 now 3 tests; lane 7 specs / 27 tests.
GREEN = integrated build with dark-listbox active served ⇒ closes G5.

## 2026-08-07 ~12:05 — Vasquez 5-delta rulings applied
D1 CONCEDED→GREEN LOCK (tombstone: pickup null + isMyPickupTurn false; HUD recorded-only; verified GREEN).
D2 NEW spec changsha-manual-deal-orchestration (non-dealer seat-1 manual deal must reach 13; RED@200cad4: dealer=0/
hand=0 RollingDice stall — bot-dealer roll unscheduled hand-1 manual). D3 already done (targetSlots len1). D4 overlay-
teardown acceptance noted (claim-tombstone orphaned in handoff — adopt on consolidation). D5 NEW G17 S13 (no auto-take:
handNoPress==0; RED@200cad4: handNoPress=13 driveManualDealChain auto-drives). Lane 8 specs / 29 tests. Notified Vasquez.

## 2026-08-07 ~12:10 — Ripley G17 positive-case request already satisfied (no new tests)
Ripley asked to add positive single-trigger assertions to "red-wall-input" (the archived hudson-2 negative-only
handoff name). My CANONICAL G17 (changsha-manual-pickup-endpoint-only, 13 tests) already has the full POSITIVE path:
- single-trigger take + payload {seatIndex,count}: S3/S4 (batchTaken) + S9 + S12 (real click TOP → take + payload).
- targetSlots present len===1 + field parity (not nextTileSlots/targetHandles/multi): S1/S2 + S10 (raw wire).
- occluded bottom inert/un-hoverable (canSelect blocks) + others reject: S12 (topCanSelect true/bottomCanSelect false)
  + S3/S4 (wrongRejected) + S8.
- parity-flip count=1 reachability: S7 (lower layer becomes next trigger).
No code change; confirmed mapping to Ripley + clarified naming (red-wall-input is archived). Lane 8 specs / 29 tests.

## 2026-08-07 ~12:15 — G17 S14 integrated phase sweep (∀ phase × dice sums)
Directive: extend G4/G17 across every manual ceremony phase, all dealer×dice — targetSlots[0]==current reachable
Wall[0] (up-link empty given depletion), parity phases not occluded. Added S14: 5 dice-sum seeds @dealer0, capture
ALL pickup windows (any seat) across BreakPointMarked→Round1-3→SingleTile→DealerExtra, assert each targetSlots[0]
reachable-top given depletion. RED@200cad4 verified (windows=0, no targetSlots). G4 break-anchor = BreakPointMarked
instance across dice sums; dealers 1-3 = Bishop golden. G17 now 14 tests; lane 8 specs / 30 tests.

## 2026-08-07 ~12:20 — Empirical gates: D2 seats 1-3 + S13 no-take-before-click
(1) D2 parametrized non-dealer seats 1/2/3 (bot dealer, no human roll → 13 tiles). RED@200cad4 verified (seat3:
dealer=0/hand=0 stall). (2) S13 enhanced: outbound capture — no pickup.take before a genuine click. RED@200cad4:
handNoPress=14 + takeFramesNoPress=4 (auto-drive emits 4 takes with 0 clicks). D1 GREEN LOCK + D4 overlay-teardown
recorded (per directive). Lane 8 specs / 32 tests (D2 now 3, G17 still 14).

## 2026-08-07 ~12:15 — SC-4 v4 FROZEN (parent, final/monotonic) — gates already aligned, no change
Ripley confirms SC-4 v4 FROZEN; disregard any <v4 handles-for-pickup message (I already held against those).
Split confirmed & ALREADY matched: G17 pickup = pickup.targetSlots (len-1 single-trigger reachable top Wall[0];
take {seatIndex,count}; occluded bottom+others inert; field targetSlots NOT nextTileSlots/targetHandles) —
S1/S2/S3/S4/S9/S10/S12/S14. G19 = SC-2 opaque STRING handles on hidden things (raw-WS P1-P5, cross-viewer). Two
separate fields (pickup=public slot, things=opaque handle). No gate change. Both GREEN after Bishop targetSlots
(len-1)+top-first + OpaqueHandleService. Lane 8 specs / 32 tests unchanged.

## 2026-08-07 ~12:16 — Ripley wire-assertion request already satisfied by S10 (no change)
Ripley (re Frost: Bishop emits pickup.nextTileSlots 4-batch vs contract targetSlots len1) — S10 ALREADY asserts:
NOT nextTileSlots (line 27), NOT targetHandles (28), NOT targetTileIds (29), NOT multi-element batch (30, catches
the 4-element nextTileSlots), field IS targetSlots (31), targetSlots.length===1 (32). Interaction asserts present:
only reachable-top clickable + occluded-bottom/others inert (S12/S3/S4/S8), take {seatIndex,count} (S9). No code
change. Against :18083 S10 REDs on targetSlots-absent; against Bishop's integrated backend it REDs on nextTileSlots/
multi-element. Confirmed to Ripley. Lane unchanged 8 specs / 32 tests.

## 2026-08-07 ~12:25 — Vasquez F2 sign-off: G4 top-before-bottom + G17 S15 trigger==take
(1) G4 new hard gate TOP-before-BOTTOM depletion (promoted from optional obs): no floating tile (bottom removed
while top present). world-coord min/max-z classify. RED@200cad4 verified: twoLevelAll=true, halfTop=5/halfBottom=0
(bottom-first mapping). GREEN after Bishop inverts layer map. G4 now 4 tests.
(2) G17 S15 trigger==take on single-tile pickup — designated trigger (targetSlots[0]) tracked by thing.index must be
the consumed tile (leaves wall); RED@200cad4 (no targetSlots ⇒ no count=1 designation). GREEN after inversion. G17
now 15 tests. Lane 8 specs / 34 tests.

## 2026-08-07 ~12:30 — G19 generic ≥128-bit opaque handle (SC-2 helper = 43-char base64url)
Directive: opaque string ≥128-bit, don't hardcode exact length. Added harness handleHasEntropy (non-numeric +
base64url ≥22 chars = ≥128-bit, prefix-tolerant); wired into G19 P-1 (entropyFails). Unit-verified generic (43/h_+22
pass; raw-id/short fail). RED@200cad4: opaqueFails=94 + entropyFails=94 (raw numeric ids). GREEN integrated-only (SC-2
wired). "9 bottom-first inverse tests" = Bishop backend, not mine. Lane 8 specs / 34 tests (P-1 strengthened, no new test).

## 2026-08-07 ~12:32 — G19 confirm: reject ANY numeric hidden key (permutation incl.), all 4 validations asserted
Directive (SC-2 numeric-permutation→HMAC): G19 already covers. handleIsOpaque rejects ANY numeric (raw 0..107 OR a
numeric permutation of any magnitude); made the P-1 message explicit. Four validations asserted: base64url (entropyFails
≥128-bit), noninferability + cross-viewer (P-5 sameHandle===0), reconnect (P-4 p4Changed===0 + pid stable). own/public
correct (P-2 numeric real ids + P-3 face-up). SC-4 targetSlots stays separate (G17). RED@200cad4 (raw ids). No new
test. Lane 8 specs / 34 tests.

## 2026-08-07 ~12:40 — G4 break-anchor F1 golden (rules-approved [14,14,13,13])
Encoded F1 frame: B=(dealer+(sum-1)%4)%4, col=cap[B]-sum, top. Compare actual pickup.breakPoint (exists @200cad4)
to F1-expected → discriminates the bug WITHOUT targetSlots. RED@200cad4 verified: dealer0 B=1 colDelta-1 (sum6),
B=2 colDelta+1 (sums3,7); B=0/3 match. anchorMismatchCount=4. GREEN after Bishop absolute-frame collapse. Full 44
= Bishop golden; browser=dealer0 subset. Kept worldSingleContiguous + top-before-bottom depletion. G4 still 4 tests.

## 2026-08-07 ~12:45 — G19 visual privacy (anonymous-pool render complement)
New changsha-visual-privacy.spec.ts: render complement to raw-WS P1-P5. MUST-PRESERVE GREEN@200cad4 verified:
tileThings=108, own face-up (14), foreign hidden (rot2, foreignUp=0/down=39). Integrated-only (recorded, gate lands
with pool): rendered-instances 108-not-216, opaque face-down + raycast-inert-except-SC4, atomic reveal. wallSelectable=28
recorded (hudson-2 auto-wall lane, not duplicated). Fixed heuristics (rot1=face-up; foreign rot2=hidden; filter tiles;
instance-count captured non-tile meshes → recorded not asserted). Lane 9 specs / 35 tests.

## 2026-08-07 ~12:50 — G19 P1-P5 EXACT (entitlement both-directions + 108-unique + order + whitelist + public-shared)
New G19 test: exact entitlement partition. RED@200cad4 via over-reveal=94 (hidden numeric). over-hide=0 (entitled
numeric, catches over-hide). 108-unique GREEN. identityFieldLeaks=0 (face present but NULL = stripped; corrected the
naive whitelist which mis-flagged 306 null-face entries → gate = face!=null). public-same guarded (no discards post-deal).
array-order recorded (slots slot-canonical; keyAscFrac=0.99 numeric-ids-sequential vanishes at opaque). G19 now 3 tests;
lane 9 specs / 36 tests.

## 2026-08-07 ~13:00 — NG persistent New Game gate (user complaint)
New changsha-new-game.spec.ts (5 tests): visible New Game control @desktop+@mobile (>=44px, hit-testable); one-click
fresh authoritative game (fresh gameId, config preserved, seat/player reclaimed, Auto face-up no-extra-clicks, clean
move-log); double-click debounce=1 session; disconnected-path recover. RED@200cad4 VERIFIED (ctrl=null — no persistent
New Game control in active Changsha; #new-game is in the FE-1-hidden relay sidebar = the user's complaint). GREEN after
Ferro/Hicks surface the persistent authoritative button (location.replace fresh gameId+config, not legacy relay/local
deal). Lane 10 specs / 41 tests.

## 2026-08-07 12:55 — SC-2 co-land PREP (integrated raw/browser run ready)
Directive: SC-2 backend projector P1-P5 + Hicks pool units reportedly GREEN; PREPARE the live
integrated run; REJECT if flag/default leaves privacy OFF in Changsha or duplicate raw crypto
remains; verify exact108/visible-backs/atomic-reveal.
- Mapped REJECT criteria to gates: privacy-default-ON = G19 P-1/EXACT over-reveal (plain config,
  no privacy flag ⇒ numeric hidden keys = REJECT); no-duplicate-crypto = crypto OUTCOME via P-1
  entropy + P-4 reconnect-stable + P-5 per-viewer-differ (impl-level HKDF path deferred to Frost).
- Authored NEW `@integrated`-guarded hard gate in changsha-visual-privacy.spec.ts (test #2):
  exact-108 rendered INSTANCES (not 216) + opaque face-down + raycast-inert-except-SC4-target +
  atomic reveal (90-frame rAF poll, 108 every frame, no invisible/invalid-rotation). Skipped
  @200cad4 (RUN_INTEGRATED unset) so must-preserve stays GREEN; verified GREEN+SKIP.
- Added `privacyDefaultOn` evidence note to G19 P-1. Lane 10 specs / 42 tests (visual 1→2).
  Report + specs mirrored. Standing by for Ripley's co-land + served integrated build.

## 2026-08-07 13:00 — Vasquez §F2 confirm: S6/S7 occlusion-based + 49/51 catcher hardened
Vasquez confirmed my occlusion (up-link-empty) S6/S7 are the correct §F2 oracle vs a layer-parity
rule. Pinned TOP-FIRST: Wall[0]=reachable top; after a count=1 take the SAME stack's layer-0
BOTTOM becomes reachable (states 49/51) — parity would falsely RED, occlusion GREENs.
- readStackDesignation +targetZ +targetAtBottomLevel ((maxZ−z)>2 = reachable bottom w/ tops alive).
- S7: +§F2 49/51 catcher (bottomAfterCount1 + reachableBottomSeen).
- S14: +singleTile/dealerExtra phase-coverage + reachableBottomWindows>0 catcher.
- targetIsTopOfStack confirmed parity-free (sib=null⇒true at lone bottom). RED@200cad4 preserved
  (S7 lowerLayerNext / S14 all.length primary fail); GREEN auto on the F2-fixed slotmap build.
  Aligns w/ Frost SC-3 production golden F2 (a)+(b). Lane 10 specs / 42 tests.

## 2026-08-07 13:09 — Assembly reconciliation: two RED lanes → ONE canonical set
Ripley: reconcile the two complementary RED lanes. CANONICAL = /data/...-uat-tests (persistent;
the /tmp fast lane was WIPING mid-task — snapshotted it to red-matrix/fast-lane-snapshot/ first).
- Folded 12 fast specs + _uat-changsha.ts + helpers/changsha-real-pointer.ts: 8 ACTIVE RED
  (relay-controls RC-9, ownhand-faceup RC-2, no-riichi RC-10, auto-wall-nondraggable RC-11,
  dealmode-auto-boot RC-13, disconnect-cleanup, lifecycle-active-games, repeat-deal-idempotent)
  + 4 FIXME skeletons (peer-nonobservation G18, discard-reject-reason, discard-claim-exclusivity D4,
  persisted-failure-replay[11]).
- Deduped: kept MINE setup-selector-readable (real-pixel contrast) over fast dropdown-readability;
  kept MINE responsive-canvas (active) over fast mobile-viewport (fixme). Dropped both fast weaker.
- Unified surface: added _uat.ts barrel (export * from _uat_red + _uat-changsha; disjoint).
- NEW-1 (D2 manual-deal-orchestration) + NEW-2 (G17 S13) confirmed active. Did NOT fold the 2
  invalid proxies. Whole set parses in one --list (23 specs, 0 errors); 3 folded spot-RED@200cad4.
- Canonical UAT: 22 specs / 64 tests (50 active gates + 14 fixme skeletons). Nothing committed.

## 2026-08-07 13:21 — Ripley R-A/R-B rulings + flags
- R-A: RESTORED G4 strict cross-corner gate. Had wrongly loosened to worldSingleContiguous
  (corners ≤70u, GREEN@200cad4) calling strict "unsatisfiable". Flipped both gates to
  strictOnePitchPolyline (cornerDiscontinuities===0). RED@200cad4 verified: 2 corner breaks
  39u/45u vs pitch 6u (worldSingleContiguous=true masked it). GREEN after Hicks closes the ring.
- R-B: bound G6 (ownhand-faceup) to G19 server ENTITLEMENT — own=numeric real ids face-up,
  foreign=opaque handles face-down (raw-ws handleMap). RED@200cad4 on foreignOpaque (not the
  fragile client rotation override, which passed here — proving Ripley's fragility point).
- FLAG1: already satisfied — G17 asserts targetSlots len===1, rejects targetTileIds/targetHandles.
  red-manual-pickup-signal is a retired name absent from /data.
- FLAG2: repeat-deal + disconnect folded (full specs). Folded dropdown's 2 unique axes (font≥12,
  focusable) into setup-selector-readable; mobile-viewport had zero unique coverage (fixme).
  All edits RED@200cad4-preserved; nothing committed; zero prod/generated edits.

### 2026-08-07 (PM) — Ripley anti-happy-path contract: NEW-1 / NEW-2 / D4 / E1
- Parent formalized a HARD anti-happy-path rule (manual mode not signed off on the
  dealer-human happy path alone). Verified/authored the gates that prove the live config.
- **NEW-1 (D2)** `changsha-manual-deal-orchestration.spec.ts`: already canonical; re-ran
  seat 1 → RED@200cad4 (hand=0, RollingDice stall — bot dealer never rolls hand-1).
- **NEW-2**: authored NEW first-class gate `changsha-human-driven-pickup.spec.ts` (dealer
  seat 0 isolation; per-batch real-press progression + no-auto-advance). RED@200cad4:
  noPressClimb +9 with 0 presses, no targetSlots designation ever ships. Complements the
  buried S13 negative in G17 (also re-verified RED: handNoPress=14, 0 presses).
- **D4**: refocused `uat-discard-claim-exclusivity.spec.ts` from exclusivity→OVERLAY
  TEARDOWN per Ripley (added claimOverlayPresent(), teardown primary assert, exclusivity
  demoted to diagnostic). Stays test.fixme until the claim-window schema lands; parses clean.
- **E1/G17**: re-confirmed targetSlots len-1 already asserted; retired targetTileIds absent.
- Integrity clean (tests/ only, nothing committed, detached @200cad4, no prod/generated).
  Report → 1008 lines; 2 specs mirrored. Replied to ripley-2.

### 2026-08-07 (PM) — R-B/G6 hardened to server-authored face (Ripley BE-5 confirm)
- Ripley confirmed G6 fix = backend BE-5 (ViewerSeat rebind + re-projection; translator:559).
- Audit caught a gap: my dimension-(a) face read was from the CLIENT world (the override
  site) → false GREEN risk. Probed the live wire: @200cad4 the OWN hand arrives face:null
  on all 14 tiles, rot:2 == foreign — the server strips the owner's own face (stale viewer).
- Hardened changsha-ownhand-faceup.spec.ts: added raw-WS capture + serverOwnFaceRevealed
  (own raw face!=null >=13), serverRotDisjoint (own vs foreign rotation disjoint),
  serverForeignFaceConcealed (lock). RED@200cad4 CONFIRMED: 0/14 face revealed, rot both [2];
  client-world ownRenderedFaceUp=true (override) proves why the raw read is required.
- G6 now has 3 server-authored RED discriminators; flip GREEN after BE-5 + G19. Mirrored;
  probe evidence kept (G6-server-rawface-probe-200cad4.json); probe spec deleted. Integrity clean.

### 2026-08-07 (PM2) — SC-3 Gate-2 final split: handedness hard-gate + R1/R2 offset log
- Ripley split Gate-2: (A) hard-assert wall handedness (col-max(B) toward seat B+1, mirror=FAIL)
  in world coords, no ordinal proxy; (B) separately LOG dealer0/dice2 offset (R1 2nd=ship /
  R2 3rd=flags backend+oracle), don't conflate.
- Added `captureHandedness` world-coord reader + 2 tests to changsha-wall-worldcoord-polyline.spec.ts.
- (A) GREEN LOCK @200cad4: probe (30 seeds) + gate (3 seeds) show all 4 walls lean toward B+1
  (proj 9000-9750; col-max ~2x closer to B+1). Guards vs a mirror regression from Bishop F1/anchor
  + Hicks corner-join. Two world-coord discriminators (proj>0 + maxCloserToPlus).
- (B) diagnostic LOG: dealer0/dice2 (pinned seeds 17,61) → breakPoint stackIndex=11 = R2(3rd);
  F1/R1 expects col=12(2nd). @200cad4 emits R2 ⇒ FLAG-BACKEND+ORACLE (surfaced via evidence+console).
  Recorder only (asserts harness liveness); hard R1 gate stays in the F1 golden test. Not conflated.
- Evidence: red-wall-handedness.json, red-wall-offset-dealer0-dice2.json. Probe deleted; spec mirrored;
  integrity clean (tests/ only, detached @200cad4, nothing committed).

### 2026-08-07 (PM3) — G19 P-1 hardened: wire-authoritative + canonical h_ form contract
- Ripley clarification: canonical handles = Bishop HKDF base64url STRINGS with `h_` prefix (JSON
  string), NOT numeric ≥10M. Confirm P-1 treats `h_<base64url>` as opaque.
- Verified helpers: handleIsOpaque("h_…")=true, handleHasEntropy strips ≤4-char prefix then ≥22
  base64url chars=true; numeric→false. Operative branch matches Bishop's h_ output.
- Hardened P-1 (changsha-privacy-opaque-handles.spec.ts): added WIRE-AUTHORITATIVE raw-key opacity
  assert (reads sink.raw[].key for hidden slots, not just client t.index — same client-vs-wire lesson
  as G6) + deterministic canonical-form contract (h_<base64url>=opaque+entropic; numeric 0..107=visible).
- RED@200cad4: rawHiddenKeys=418 rawOpaqueFails=418 rawEntropyFails=418 (sample "0".."3"); client-proxy
  opaqueFails/entropyFails=94. Contract GREEN: canonOpaque=true numericIsVisible=true. Flips to 0 on
  Bishop HKDF h_ activation. Spec mirrored; integrity clean.

### 2026-08-07 (PM4) — G17 Frost review: comment fix + S7/S14 49/51 reach confirmed
- Frost accepted S9-S12 (raw-wire #4 + F2 up-link-empty reachability + fail-closed + count-take);
  verified S12 predicate = !upOcc(top), not layer==1.
- Fixed DOC-vs-ASSERTION mismatch (comment-only, changsha-manual-pickup-endpoint-only.spec.ts):
  reworded S6/S7/S12 comments from "TOP (layer 1)/wall.{col}.1" absolutes to the up-link-empty
  predicate + 49/51 reachable-bottom clarifier. No behavior change; re-parses.
- Answered Frost's 49/51-reach question: S7 (bottomAfterCount1+reachableBottomSeen) AND S14
  (singleTileSeen+dealerExtraSeen+reachableBottomWindows>0) AND S15 (trigger==take) HARD-ASSERT
  reaching the 49/51 reachable-bottom states — detection via world-z (maxZ-z>2)+up-link-empty,
  never parity. Boundary: @200cad4 RED at first hurdle (no targetSlots ⇒ empty timeline); empirical
  reach pending integrated build. Backend layers cover 49/51 regardless.

### 2026-08-07 (PM5) — Proxy-removal invariant audit + 55-count fold-in
- Ripley: assembly removes wall-contiguity-152 + c5-wall-forward-depletion as G4 acceptance proxies;
  flag any distinct useful invariant.
- Audited both. SUBSUMED: 152 per-seat column signature = the R-A-REJECTED "per-seat no-gap" proxy
  (proof: @200cad4 worldContig=true but strictOnePitch=false, cross-corner d=39≈6.5×pitch=6 — proxy
  false-passes the corner-gap defect canonical worldPos REDs); c5 Setup-UI flow covered by 8 specs.
- DISTINCT: (1) 55-remaining COUNT — not gated in any canonical E2E (worldPos only RECORDED a.tiles);
  PRESERVED by folding expect(tiles).toBe(55) GREEN-LOCK into canonical worldPos (seed-sweep+manual),
  verified passes @200cad4 with RED reason (polyline) unchanged. (2) dynamic FORWARD depletion over
  real draws — backend-covered (WallOrdinalToSlot ∀ state + CeremonyPickups 17-state); only the browser
  dynamic echo is lost, not the invariant.
- Verdict: both safe to remove; nothing useful lost. Mirrored worldPos spec, report+history updated.

### 2026-08-07 (PM6) — Verified R-A/R-B/E1/D4 encoded + authored NEW-2b pickup HUD-teardown RED
- Ripley re-stated R-A/R-B/E1/D4 (FINAL) + promoted my pickup-tombstone UI finding to a gated RED.
- Verified all four already exact in canonical: G4 strictOnePitch (worldPos), G6 raw-WS rotationIndex
  disjoint+entitled/opaque (ownhand-faceup l.175/179/182/192), E1 S10 targetSlots-len1 + targetTileIds/
  targetHandles ABSENT (manual-pickup l.350/377-378), D4 claim overlay teardown (discard-claim-exclusivity).
- Authored NEW-2b (changsha-human-driven-pickup): pickup HUD teardown, DISTINCT from D1 collection clear.
  Source-traced the tombstone: post-deal map.clear() fires no 'current' update ⇒ renderPickupHud(null)
  never runs ⇒ #pickup-hud "Take N" banner orphans. RUN @200cad4 = RED VERIFIED: precondition passed
  (pickupCurrent=null) + gate failed (visible=true, hudEverVisible=true). Non-vacuous by construction.
- Mirrored both specs; evidence new2b-pickup-hud-teardown.json/.png. Routes to Hicks + backend EncodePickupCleared.

### 2026-08-07 (PM7) — Vasquez D1-D5 confirmed; completed D4 teardown oracle (3 components)
- D2 (changsha-manual-deal-orchestration): active RED verified (non-dealer human+bot dealer, hand-1
  manual → 13 tiles; RollingDice stall @200cad4). Real SEPARATE backend bug = Bishop's lane; relayed
  owner-routing to Ripley.
- D4 (uat-discard-claim-exclusivity, held fixme): per Vasquez binding constraint, completed the teardown
  oracle to 3 components — added COUNTDOWN-STOPPED (#claim-countdown in presence probe + 2-sample ticking
  check). Now overlay-hidden + buttons-disabled + countdown-stopped; exclusivity stays diagnostic-only.
  Cannot regress to exclusivity-only. Mirrored; parse-clean.
- D1/D3/D5 already correct — no change.

### 2026-08-07 (PM8) — Ripley "STOP on G17 targetTileIds" → VERIFIED already-compliant, no change
- Skeptical premise-check: my canonical G17 (changsha-manual-pickup-endpoint-only.spec.ts) ALREADY asserts
  the FROZEN SC-4 v4 3-axis contract: targetSlots present+targetTileIds absent (l.378/380), length EXACTLY 1
  (l.340/372/381), public slot names not handles (l.271/272). No edit made (would've broken correct asserts).
- "D2" = changsha-manual-deal-orchestration.spec.ts (deal-orchestration, hand→13), ZERO pickup-field content.
  No red-manual-pickup-signal.spec.ts SPEC exists in /data/source (only a stale evidence json of that name).
- Replied to Ripley with exact file:line evidence; offered to quarantine any operative-targetTileIds artifact
  if pointed at a specific file:line. G4/G19 unchanged.

### 2026-08-07 (PM9) — Ripley "targetSlots length==count" → VERIFIED already ===1, no change
- Canonical G17 predicate l.76 `exactlyOneFront = gate.length === 1` (gate=targetSlots); NO length==count
  anywhere (grep zero). batchPreviewSlots (len count) already modeled as separate display-only inert field
  (l.65/341) using Bishop's EXACT emission field name (translator:273-276/protocol:613). S8 count===4 &&
  targetLen===1 (l.561). Spec pre-aligned to shipped emission ⇒ GREEN not RED.
- 3rd consecutive misread; mirror byte-identical to canonical. Replied with verbatim evidence; asked Ripley
  for exact file:line if seen elsewhere. No edit (would break correct asserts). G4/G19 unchanged.

### 2026-08-07 (PM10) — Ripley "agnostic/loose S1-S2" (4th round) → VERIFIED tight, no change
- Gate hard-coded pu.targetSlots (l.64, no OR-fallback). S1 l.270 exactlyOneFront=length===1 (not ==count).
  S2 l.271/272 requires public slot-name + rejects numeric/h_ handle. kind classifier = mechanism of
  rejection, not agnostic detection. Wrong-impl rejection table + 1:1 map of Ripley's S1-S5 → my lines
  written to evidence/g17-v4-tightness-proof.txt. F1 position deliberately excluded (Ripley's own
  F1-independence ruling; G17 pins F2).
- Replied with verbatim proof + path; invited reconcile with Frost (accepted S9-S12 @11:48). No edit.
  Next: if Ripley re-flags w/o concrete file:line, loop Frost as arbiter.

### 2026-08-07 (PM11) — Ripley "pickup.targetHandles/A-differ-B" (5th round) → nonexistent; escalated to Frost
- G17 targetHandles = negative-only (l.377 absent); take {seatIndex,count} (l.346); targetSlots len===1
  (l.76/270) public+handle-reject (l.271/272); ZERO cross-viewer differ. The "A differs from B" = G19 P-5
  on THINGS keys (changsha-privacy-opaque-handles l.40-42/104) — identity lane, previously approved by Ripley.
- Built evidence/g17-g19-separation-proof.txt. Replied to Ripley (file:line separation + their own P-5
  approval). Escalated to Frost as neutral arbiter (accepted S9-S12 @11:48). No edit. Awaiting Frost concur.

### 2026-08-07 (PM12) — G5 → hudson-2 (Ripley ownership ruling); handoff executed
- G5 (changsha-setup-selector-readable.spec.ts + helpers/png-contrast.ts) handed to hudson-2's UI lane; both
  byte-identical mirrors in red-matrix/specs/ → adopt as-is. Carried impl notes (native-<select> RED-fast, TMPDIR
  on /data, PATH). red-dropdown-robust already SUPERSEDED (l.467). Ferro=dark-listbox custom popup on #game-type.
- Lane stays schema-pure: G4/G15/G17/G19 + (clarifying B6 w/ Ripley). All RED@200cad4, untouched. No edit.

### 2026-08-07 (PM13) — G17 loop RESOLVED: Ripley confirms correct on every axis + cross-viewer split
- Ripley ratified G17 (targetSlots len-1 public, count separate, one-press→batch, inert others, no leak,
  tombstone) + the position(G17)/identity(G19) separation ("removing cross-viewer test is RIGHT"). 5-round
  loop closed with ZERO spurious edits; Frost escalation + 2 proofs did it.
- B6 resolved from own records = changsha-4hand-gamecomplete (GREEN-LOCK desktop, NOT RED). Lane = 4 RED
  (G4/G15/G17/G19) + 1 GREEN-lock (B6). G5 handoff (PM12) reconfirmed. G4 one-pitch metric held. Nothing open.


## 2026-09-15 UTC - FINAL scoped native cd42 acceptance

Executed coordinator-authorized frozen cd42, not changing-workspace9d/4070: one67-case invocation63PASS/4FAIL/0SKIP/retries0; all349 API,55 protected and1115 complete captured inputs matched before/after. Runtime14b/PublicRooms948/endpoint752/engine0c8. Product failures2: successful discard/rob-Hu lacks documented SignalR WinDeclared, while four overflow negatives and all other selected existing native controls pass. New `HudsonLegacyNewRetryIntegrationTests.cs` ed8942e43b7707065071000be66fc456aeac2249bacd6e4066e6e08d8e0b573f fails2 at321 because its oracle wrongly expects turn.current.gameId; real legacy creation/restart/refusal/explicit NEW prefix reached, retry/no-redeal/continued-discard suffix NOT reached. **REJECT/HOLD this NEW probe/helper cycle; Hudson locked out of next revision/coauthorship/advice/approval.** Different owner proposedDrake subject to explicit coordinator grant; no production wire change requested. Existing rejected binding-helper cycle remains independentlyDrake5ec/Ripley-review scoped and was not altered/advised.

Handoff `sessions/2026-09-15-hudson-authorized-native-cd42.md`; immutable manifest `hudson-actions/native-authorized-cd42-02/final-manifest.json` SHA9204fa5b29411abd2589df214f1cff48796e0eb341f4f604525998ec04cbc9e6, actual TRXd13f5c5c16af1b46869b0552f3ca88528be7a6bc6f01a86a9dfac7b02d39e81d, complete2004-file seal16b12422d93263368de64f1efda8d42e45fb8547feb56bf8cf051b57bf44f11a. Separate capture01 git-diff ENOBUFS stop remains0-test evidence. No old fixture/production/frontend/Git/index/live change, approval transfer or historical count aggregation. Qualification0/120; no actual lost-confirmation or UI/image acceptance credit.


### September15 02:03UTC source notice: old-pin classification and exclusive recovery owner

Coordinator reports Ripley rejected cd42/948 recovery validation for additional nested/null/Fan projection and declarer/cursor/readiness-precondition classes. Frost2b679 exclusively owns that bounded recovery source/new tests/docs revision; Bishop/Drake locked out. Hudson's67-case source-copy run is retained as OLD-PIN native evidence only,63PASS/4FAIL, not current/final source/APP/image approval. All2004 sealed files matched at02:05UTC; no new execution/build/restore/rebase or production/test edits, no duplicate Frost scenarios and no frontend/old-b237 comparison. New immutable notice overlay SHA7b027b2352e6546906913a2e600678be62884605995d351dac86c3fa4b3b306b at hudson-actions/native-cd42-source-hold-01/notice-disposition.json. Existing ed894 rejected-probe lockout persists; broad native-scope ownership does not clear that artifact-specific restriction or authorize Drake in Frost's recovery scope. Other native scopes remain distinct. Primary handoff sessions/2026-09-15-hudson-authorized-native-cd42.md updated.0/120.


### September15 UTC - Vasquez653 receipt admitted without approval/count transfer

Verified exact peer handoff650a052a/summary167ddc0f/rawTRX60fcd8b7 and all791 seal entries:653/653PASS,0FAIL/skip/retry,26 selectors,264 original-nine rows,132 manual/auto rows, recorded1021-input before/after equality. Actual command September14 09:24:15-09:27:53PDT predates19:03PDT recovery-source rejection; e2c7e7b4/14b/948/0c8 is now separate OLD-PIN rules evidence, not current/final approval. Earlier0-test drift stop was genuinely overcome by that completed run, not relabeled. Neither HudsonClaimSettlementOverflowIntegrationTests nor HudsonLegacyNewRetryIntegrationTests occurs in653 (0 each); my separate63/67 RED, notification failures and ed894 lockout persist. No109-wide qualification or aggregate totals. Owned receipt hudson-actions/vasquez-653-old-pin-receipt-01/receipt.json SHAf5cca9164e2378a93140391471fc22a144baa4b2be359ab9eedc2d06fb899225; primary native/acceptance runtime handoffs appended.0 new tests/source edits; Frost2b679 exclusive recovery lane unchanged;0/120.


### September15 03:32UTC - Disjoint ordinary-entry controls2/2; legacy gaps not self-fixed

Added only new RulesQualification/HudsonFreshEntryCreationIntegrationTests.cs SHA2d6d48805779ecc4329c04a624c7f5eb6857184014205477b7c082bf0a8b86b9 for request2043fdfe case4. One exact targeted oldcd42 captured-source invocation2/2PASS,0skip/retry: signed nonlegacy JOIN and literal NEW -> four non-bot owners/one binding -> discard55/draw29/v3->5/wall55->54/inventory108/persisted state.1116 input paths/hashes stable;55/349 pins match; no root/Frost compilation. New test still requires separate independent review. Manifest8647838dc221794eeacfd420d578589d699e5686d1436c8d6ea8dc6203e35f69, TRX986cdac8c426fbc851cc3307e6b8a4ea0b22b2030ec604a0514054641ba38c33, matrixdd2caa1164ee4e6be2a88f4a86243943b79bf4f086f0b61164d26eb95a5661c7 under hudson-actions/fresh-entry-positive-01/. Full runtime handoff sessions/2026-09-15-hudson-entry-boundary.md.

Existing case1/case5 signed passes returned without replay under5ec/d13f: disclose binding-removal baseline and redundant gameId in the old negative NEW envelope. Case2 full legacy progression and case3 unconfirmed retry remain unclosed; ed894/5ec/395/b2d14d34 untouched. **ed894 revision/coauthor/advice/approval lockout persists.** Different coordinator-granted test owner requested via decision8a09d629-e060-4de1-9f0c-01e9dcd520b7; Drake proposed only for separate legacy-test scope, never Frost recovery validation. First decide-tool optional references field failed references.join; retry without it succeeded, not a test retry. No source/bundle/browser/image/live/Git operations, old-count aggregation or qualification credit.0/120.


### Later root-probe change after case4 seal

2026-09-15T03:57:19.911Z readback: root HudsonLegacyNewRetryIntegrationTests.cs now **17d4a27bbe5001e26fcfd9129dab9f803a0dbf6fe879cd3cbd852af55c99fad9**, versus executed/captured ed894. Only that scoped test differs; new ordinary-entry2d6/395/5ec/b2 root pins still match. Hudson did not read the changed probe body or revise/revert/advise/approve it, and does not infer author/grant/review/results. The2/2 case4 manifest8647838d/TRX986cdac8 and immutable old source copy remain intact. Readback overlay ef3ed8698d0b7f6da6cc72c9e264d8b9e24e8400065ace60df168c7974dae876 under fresh-entry-positive-root-readback-01/. Entry handoff and owner-request decision appended: identify the actual existing17d4 owner/review/result before duplicate assignment; legacy acceptance remains unclosed at the last executed pin, not a newly asserted failure of17d4. No new execution or qualification credit.
