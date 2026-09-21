# September 9, 2026 — authored inbox source archive (historical test-gate provenance)

Runtime-owned preservation by Scribe. Original authored texts are retained below, not adopted as fresh September 9 acceptance. No timeout, helper, product, or toolchain change is authorized by archiving these records. Exact current claim-key source approval is Frost's approval of Dietrich-authored SHA256 `6d3db9f335fe7b563ecff0b8081cf4fc9d050c03c9f483a45def50f3d3a68564`; the older Spunkmeyer hash below is a different artifact. Hicks's original rejected-cycle exclusion is not silently cleared. Current S11, browser and release status remains in `sessions/2026-09-09-reviewer-lockouts.md` and `sessions/2026-09-09-current-focus.md`.

## Source: decisions/inbox/dietrich-viewmode-changsha-and-f1-swiftshader.md

# Dietrich — view-mode Changsha centre + f1 (m2/m2-sweep) SwiftShader hardening

**Date:** 2026-08-11 · **Lane B owner:** Dietrich (Frontend Renderer Engineer)
**Scope (test-only, no product change):**
- `src/frontend/autotable-src/tests/e2e/view-mode-toggle.spec.ts`
- `src/frontend/autotable-src/tests/e2e/f1-gate2-visual-perimeter.spec.ts`

## First-invariant trace (view-mode Changsha red)

RED: `Changsha variant … centre hidden` asserted `center.mesh.visible === false`
but observed `true`.

The harness `mountAndDeal(page, 'CHANGSHA')` called
`world.deal('HANDS', { gameType: CHANGSHA })`. In Changsha `world.deal`
**early-returns at the FE-1 `blocksLocalDeal` gate** (`world.ts:1218`) BEFORE
`updateConditions()` (`world.ts:1247`) — and `updateConditions` is the sole caller
of `objectView.setVariant()` (`world.ts:1142`). So the harness never drove the
variant into ObjectView, and the Riichi centre HUD stayed visible.

**Verdict: HARNESS bug, not product.** Product `ObjectView.setVariant(CHANGSHA)`
correctly sets `center.mesh.visible = !isChangsha` (`object-view.ts`). A standalone
product repro at `?variant=changsha` hides the centre — so **no `object-view.ts`
change was needed** (reviewer reassignment condition NOT triggered).

## Fix — view-mode

For CHANGSHA the harness now enters the product's REAL Changsha table locally:
navigate `?variant=changsha&seat=0`. The `ObjectView` ctor
(`readVariantFromUrl → setVariant`) hides the centre HUD **and** skips the
NaN-prone stick-tray merge at first paint; the `World` ctor lays the canonical
108-tile Changsha wall (`Conditions.initial() === CHANGSHA`). Omitting `?gameId=`
keeps it fully local (`ClientUi.start()` only opens a WS when a gameId is present).
FOUR_PLAYER path unchanged. Assertions unchanged: `centerVisible===false`,
`distinctColors`, `litDelta` all preserved.

## Fix — f1 (m2 / m2-sweep) SwiftShader crash

RED: both crashed "Test timeout 90000ms / Target page, context or browser has been
closed" — the deal-INDEPENDENT camera-chirality tests ran a full server
connect + auto-deal + 3-bot LIVE game that churned the SwiftShader software
renderer for 60-90 s and leaked WebGL/serialisation resources until the page
target closed.

Replaced the live bring-up with a self-contained LOCAL Changsha mount
(`mountLocalChangsha`, `?variant=changsha&seat=0` — no WS, no bots, no live-deal
churn). The camera pipeline (`main-view.ts` `makeCamera`/`updateCamera`,
`viewGroup.rotation.z = seat·90°`) and the FIXED wall-slot geometry (`world.slots`)
the chirality gate reads are byte-identical either way, so the externally-anchored
chirality assertion (`sign(screenCross) === sign(worldCross)`) is **unchanged**.
Added bounded readiness + a WebGL context-loss tripwire (fail-fast, no 90 s hang).
No new `test.skip`, no retries, no timeout inflation, no force/backdoor.

## Chirality audit (green run, all 4 viewer seats, worldCross↔screenCross SAME sign)

- seat0 (0°): worldCross=2925  screenCross=0.1545  (+/+)
- seat1 (90°): worldCross=2700  screenCross=0.1449  (+/+)
- seat2 (180°): worldCross=2700 screenCross=0.1449  (+/+)
- seat3 (270°): worldCross=2925 screenCross=0.1545  (+/+)
All finite=true, onScreen=true, non-degenerate. Camera orientation-preserving
(no mirror) at every rotation.

## Old-red → new-green matrix (pinned toolchain: @playwright/test 1.60.0 +
chromium-1223 / browserVersion 148.0.7778.96, headless SwiftShader software WebGL)

| Spec / test | OLD | NEW |
|---|---|---|
| view-mode Changsha centre hidden | ✘ visible=true | ✓ (3/3 runs) |
| view-mode other 3 | ✓ | ✓ (3/3 runs) |
| f1 Part A ×2 (browser-free) | ✓ | ✓ |
| f1 Part B observe / codified | skip (S1) | skip (S1, unchanged) |
| f1 (m2) chirality | ✘ target-closed @90s | ✓ ~1.3s (3 runs + 5 stress) |
| f1 (m2-sweep) chirality | ✘ target-closed @90s | ✓ ~1.3s (3 runs + 5 stress) |

Regression: renderer-hidden-park 4/4 ✓; approved blocking visual baseline
(`view-mode-visual-baseline`, `visual` project, SwiftShader) 1/1 ✓ (host raster
matches container-generated committed baselines → host ≡ pinned container).
ESLint clean; strict TS (`tsc --noEmit --strict`) clean.

**Env note:** the pinned `playwright:v1.60.0-jammy` container cannot mount the 2 GB
`autotable-src/node_modules` in this sandbox (containers hang in "Created"); every
agent runs the identical pw1.60.0 + chromium-1223 SwiftShader toolchain on the
host `.hicks-node`. The visual-baseline pixel match proves host ≡ container.

**Reviewers:** Hicks + Frost.

## Source: decisions/inbox/drake-mobile-e2e-reds-are-spec-local.md

# Drake — the two :18089 mobile e2e reds are SPEC-LOCAL, not product defects

**Date:** 2026-08-11 · **Owner:** Drake (Lane C runtime/timing) · **Target:** `mahjong-hicks-18089`
(buildSha `hicks-repl-18089`, Dietrich hidden-park-slot renderer fix present ⇒ 0 pageerrors).
Write scope: `changsha-4hand-gamecomplete.spec.ts` + `post-meld-discard.spec.ts` ONLY.

## Verdict

**NO backend/runtime/scheduler/rules/layout product defect. Test-only corrections.**
Both mobile-chrome reds are harness cadence/selector issues; chromium is unaffected.

## Red 1 — mobile GameComplete "discards=28 nextHands=0" = SLOW PROGRESS, not a stall

Instrumented repro (fresh unique gameId, auto dealMode, Medium bots, 4 hands):
the game **progresses normally** — wall depletes 55→8, hands END (authoritative
`result` latch 0→1→2), and **the next hand auto-deals server-side** (wall resets to 55
with `nextHands=0` clicks). The spec's `#result-modal` never displays on the Pixel-5
layout (`rmVis` always false), so the old `clickNextHand` path never fires — but
progression never needed it. Root cause: the old loop calls `discardByPointer` EVERY
iteration (even during bot/claim phases); on the 390-wide viewport tile projection
misses more, so iterations cost 2.7→13s and only ~2.1 of 4 hands finish inside the old
200s deadline.

**Measured mobile cadence (efficient loop, gate `discardByPointer` on authoritative
`hasExtraHandTile`):** GameComplete reached at **339s and 341s** across two fresh games,
both **zero-sum** (`{0:-5,1:2,2:-3,3:6}` sum 0), **0 console/page errors**. Seed-stable
hand structure: hand-ends ≈120s / 153s / 281s / 339s (hand 3 ≈128s). Max within-hand
loop-poll gap 12.1s ⇒ a 90s (mobile)/60s (desktop) no-progress guard cleanly separates
slow-progress from a true hard-stall (the old renderer stall froze ~300s).

**Fix (spec-local):** real-pointer play loop gated on authoritative turn state; poll the
authoritative `gameComplete` terminal; per-project justified budget (mobile 480s play /
570s test timeout; desktop 210s/300s); monotonic hand-end + discard-high-water stall
guard that FAILS WITH DIAGNOSTICS on a genuine stall. Assertions strengthened, not
weakened: camera toggle + **GameComplete** + **totalScores zero-sum** + **0 errors**.

## Red 2 — mobile post-meld `#claim-pung` "not visible" = zero-area side-panel button

At the authoritative Pung window on mobile (viewport 393×727) the probe measured:

| control | present | enabled | display/vis | bbox | PW-visible |
|---|---|---|---|---|---|
| `#claim-pung` (side panel) | yes | yes | block/visible | **{0,0,0,0}** | **false** |
| `.ferro-claim-badge-pung` (overlay) | yes | yes | flex/visible | **{83,587,70,44}** | **true** |
| `.ferro-claim-pass` (overlay) | yes | yes | flex/visible | **{17,664,359,44}** | true |

The mobile CSS collapses `#sidebar` to a 160px pill; the legacy claim row renders with a
**zero-area** box (present + `isEnabled()` true, so the local `claimMeldByClick`'s
`isEnabled` guard passes, then `.click()` times out "element is not visible"). The
Ferro claim overlay is — by the shipped CSS's own comment — "the primary surface during
claim windows"; the side panel is a desktop-only fallback. Clicking the real visible
overlay badge committed the Pung (concealed 13→11, meld exposed, seat owes discard,
window closed) and the subsequent real-pointer discard grew the pile 10→13.

**Fix (spec-local):** local `claimMeldByClick`/`passClaimByClick` click the genuinely
visible overlay control (`.ferro-claim-badge-{type}` / `.ferro-claim-pass`) with a
bounded actionable wait, side panel as fallback. No force, no synthetic dispatch, no
backdoor. All `#147` post-claim + real-pointer-discard assertions preserved.

## Lane discipline

Zero product/helper/dist/backend edits. Only the two owned spec files change; frozen
`_playability.ts`/`_uat_red.ts` untouched. Diagnostics ran from a gitignored scratch
dir and are deleted. Frost reviews; Bishop co-sign NOT required (no product change).

## Source: decisions/inbox/spunkmeyer-claim-key-collision-fix.md

# Spunkmeyer — claim-key-collision.spec.ts revision (rejected → fixed)

**Owner:** Spunkmeyer (Endpoint/WS behavior). **Hicks LOCKED OUT of this artifact this cycle.**
**Exclusive edit:** `src/frontend/autotable-src/tests/e2e/claim-key-collision.spec.ts` ONLY. No stage/commit.
**Candidate pins (verified):** image `6ed906ae37c2` (`mahjong-autotable:hicks-18089`) · DLL `c039154d…` (`/app/Mahjong.Autotable.Api.dll`) · entry `autotable-src.09309beb.js`.
**Final spec sha256:** `0e44e59f530f4dc390392685957a8b962ae42219de4a752d3cebf3c802a8f396`.

## Root cause (instrumented, NOT assumed)
The rejected revision's reported failure — deterministic timeout at `ensureConnected(page)` (20 s) in the
full general run — is **host-CPU saturation of the cold-start pipeline**, NOT the in-page send monkeypatch.

Evidence (external probes on the exact immutable image, fresh DB):
- **The `WebSocket.prototype.send` spy is transparent.** A/B probe on an idle backend: with-spy vs
  without-spy connect identically (~1.3 s), JOIN sent, JOINED received, `spyThrew=0`. `connected()` =
  `open() && game!==null`; `game` is set only on the server's `JOINED`, which needs the JOIN frame to go
  out — the spy's `orig.apply(this, arguments)` forwards it faithfully.
- **The spy is the ONLY `WebSocket.prototype` patch in the whole e2e suite;** every other frame-observing
  spec uses Playwright's out-of-process `page.on('websocket')→framesent` (e.g. `installWallTakeRecorder`).
- **Isolated runs of the OLD spec always pass** (fresh :18090 34.8 s; reused :18089 w/342 games 30.5 s;
  8×8 self-stress all green). The red only appears under the full parallel run.
- **Reproduced the red under load:** with 14–16 concurrent WebGL contexts (load avg 128–195) a cold-start
  victim of the OLD spec fails at the tightest early gates (`ensureConnected`/seat/deal). The 20 s connect
  budget is the first to blow; the .NET backend stays light (~23 % CPU @ 24 bot games) — the pressure is
  client-side WebGL/SwiftShader on the host, exactly what the 16-worker run creates.

## Fix (surgical, spec-only)
1. **Moved observation out-of-process.** Replaced the in-page `addInitScript` `WebSocket.prototype.send`
   monkeypatch with a Playwright `page.on('websocket')→framesent` observer (`meldClaimFrames[]`), the
   house-standard transparent pattern. It reads the wire from the Node side and never touches the page's
   socket, so it cannot delay/drop/reorder the JOIN→JOINED handshake. Same meld filter
   (`includes('"claim"') && /"type":"(Pung|Chow|Kong)"/`); `readMeldClaims(page)` call sites now read the
   array length.
2. **Robust connection budget** for cold-start under load: `waitForGameObject(page, 60_000)` and
   `ensureConnected(page, 90_000)` (was default 30 s / 20 s).
3. **Non-vacuity guard:** assert `outboundFrames > 0` so a dead/misattached observer can't yield a vacuous
   zero-meld pass.
Untouched (preserved exactly): seed 4100, handCount 4, Hard bots, unique gameId, human-driven manual deal
ceremony + `dealerPickups>0`/`hasExtraHandTile` asserts, `meldWindowsExercised>=1`, Esc-pass, guarded
canvas discard, mobile-chrome skip. No WS injection/API/backdoor/force/synthetic dispatch.

## Observer transparency + non-vacuous capture — PROVEN
Throwaway capability probe (deleted) ran BOTH observers and clicked the real `#claim-pung`:
`emitted meld=Pung inPageSpy=1 framesentDelta=1 lastFrame={"type":"UPDATE","entries":[["claim","0",{"action":"claim","type":"Pung"}]],"full":false}`.
The out-of-process framesent observer captures the exact meld frame the old in-page spy did — identical,
non-vacuous, zero page-side interception.

## Validation matrix (chromium, retries 0)
- Old-red / new-green A/B under identical sustained load: OLD victim RED (seat/deal cold-start), NEW victim
  GREEN at load ~155 and ~95.
- 5 fresh unique runs on fresh-DB :18091: 5/5 GREEN (~30–35 s).
- Heavily-reused :18089 (activeGames 343): GREEN.
- Lane A pair (`playtest-changsha` + `claim-key`), applicable projects, workers 1, retries 0, ×2:
  each 3 passed / 1 skipped (claim-key mobile-chrome skip intact).
- Strict TS (tsc --noEmit --strict) and ESLint (legacy config): both clean. Zero new skips/`.only`.

## Handoff
**Requesting Hudson read-only re-review.** Spunkmeyer does NOT self-approve. Hicks remains locked out of
this artifact this cycle. Product otherwise fully approved; change is test-only.
