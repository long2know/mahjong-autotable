# Project Context

- **Owner:** Stephen Long
- **Project:** Changsha-first Mahjong game built from pwmarcz/autotable, with expanded Chinese rules planned
- **Stack:** .NET 10 backend, EF Core + SQLite initially, optional React + Fluent UI 9 + TypeScript + Vite frontend modernization, single-image Docker deployment
- **Created:** 2026-04-20

## Learnings (summarized 2026-07-27T01-56-23-811-07-00)

> Full history (278617 B, 255 entries) preserved verbatim in `history-archive.md`. Most-recent entries retained below.

  - **Floating "Seat 0" HUD dead-centre** = the upstream `Center` plane mesh with its CanvasTexture (nicks, dealer bar, honba, dice). Hidden for Changsha by the same `setVariant` toggle. Defence-in-depth: `ObjectView.updateScores(...)` now skips `center.draw()` when the mesh is hidden, so the canvas is never repainted with the Riichi-shape readout.
  - **Top-wall "gap" / phantom slots** = my own round-1 memo had flagged `setup-slots.ts` using `row(19)` for every variant. Round-2 fix: CHANGSHA wall split into `[start('wall'), row(14), stack(), seats([0, 1])]` and `[start('wall'), row(13), stack(), seats([2, 3])]` — matches backend `AutotableSlotMap.WallStackCount` (28+28+26+26 = 108) exactly, eliminating all trailing phantom slots. `fixupSlots(slots, gameType)` updated to use the per-seat last-col index (13/12 for Changsha, 18 elsewhere) for the wall-end drop-shadow guard.
  - Validation: re-ran `playtest-broken-deal-repro.spec.mjs` on the rebuilt bundle: `gameType:"CHANGSHA"`, `thingCount:109`, `wallSlots:108` (was 152 with the `row(19)` phantom set), `tilesInWall:55`, dealer hand 14 face-up, **zero stick trays**, **zero centre HUD**. Inline `page.evaluate` over `world.things` confirmed each seat populates `wall.0..6@N` contiguously (14+14+14+13 = 55).
  - Honest caveat in the round-2 memo: the *visual* "gap" in the top-of-image wall region is the geometric corner between seats 2 and 3 (each seat owns its own edge length, no seat owns the corner); not a slot-allocation bug. With phantom `row(19)` removed it's a hair more visible because the walls no longer overshoot with empty drop-shadow positions. Flagged as Phase-G geometry decision for Stephen rather than a deal bug.
  - Files: `setup-slots.ts`, `object-view.ts`, `world.ts` (one new line wiring `setVariant`), rebuilt `src/frontend/autotable/`, memo `hicks-cleanup-round2.md`, this entry. Proof: `playtest-artifacts/screenshots/hicks-deal-fixed-round2-20260601T202305Z.png`. Pre-existing `Computed radius is NaN` console warning (Vasquez `dd2608d`) still present — refactor to skip the tray-merge entirely on Changsha confirmed trays were NOT the source; another GLB primitive (`meshes.center` or a tile/marker mesh) is the residual offender. Visual artifacts ARE gone; warning is decoupled console noise, flagged as Phase-G ticket.

📌 **2026-06-01T13:41Z** — Cross-team milestone: Frost's diagnostic (commit `165166d`) identified frontend `setup-deal.ts` as the actual fence-post culprit — not backend. Backend is healthy and per-seat capped as shipped in prior `99c1af0`. Frost added 5 regression tests (`AutotableTranslatorTests`) to pin the per-seat wall contract forever.

📌 **2026-06-01T13:54Z** — Round 3 quick scoped patch (commissioned by Stephen via Copilot). Frost's diagnostic memo `frost-wall-fence-post-fix.md` (`165166d`) showed my round-2 (`b4c82ec`) per-seat wall sizing in `setup-slots.ts` (14/14/13/13) had a sibling miss: `setup-deal.ts` `DEALS.CHANGSHA` still walked from `wall.1.0` (slotNames index 2) for 26 entries on seats 2/3 → ran off the new shrunken row end at `wall.13.0@2`, throwing `slot not found: wall.13.0@2` from `setup.ts:249` in the pre-WS first-paint render. Applied Frost's 6-line patch verbatim (commit `ff096ff`): three blocks (`INITIAL`, `HANDS[1]`, `UNSHUFFLED`) each had `['wall.1.0', 2, …]` / `['wall.1.0', 3, …]` → `['wall.0.0', 2, …]` / `['wall.0.0', 3, …]`. Seats 0/1 ranges (`['wall.1.0', 0/1, 28]` and `[14]`/`[15]`) untouched — those rows still have 14 stacks where the `wall.1.0` start is safe. Vestigial `wall.1.0` start was inherited from upstream's uniform `row(19)` layout (38 slots, index 2 + 26 = 28 < 38 was harmless). Rebuild + bundle inspection confirms `"wall.0.0",2,26` / `"wall.0.0",3,26` and `"wall.0.0",2,13` / `"wall.0.0",3,13` present in `three-renderer.e788248e.js`. Validation on freshly-restarted backend (`/tmp/mat-hicks-r3.db`): `walls-facedown.spec.mjs pageErrorsCount: 0`, `human-led.spec.mjs pageErrorsCount: 0`, `broken-deal-repro.spec.mjs pageErrorsCount: 0`. Pre-existing `wallCountAtLeast100` measurement-timing failure in walls-facedown is unrelated and noted as obsolete Riichi-era threshold. Final visual proof `playtest-artifacts/screenshots/hicks-final-clean-2026-06-01T20-52-57Z.png` (walls-facedown post-deal frame). Lane discipline kept. Frost's `AutotableTranslatorTests` regression tests (`165166d`) continue to guard the backend side of the per-seat cap contract — together with this patch the fence-post bug is fully closed. **End result: ZERO page errors end-to-end. Game is visually + functionally playable.**

📌 **2026-06-03T16:10Z** — Visual regression sweep (10 scenarios, commissioned by Stephen via Copilot directive "thorough testing of the UI"). Built `playtest-artifacts/playtest-hicks-vreg.spec.mjs` (one spec, ten scenarios run sequentially against the shared backend at `:8088` with `hicks-vreg-*` gameId prefix to avoid squad collision). Scenarios: `desktop-1920`, `mobile-375`, `tablet-768`, `human-4p-nobots`, `bots-2`, `bots-4-auto`, `camera-flat`, `setup-menu-open`, `movelog-open`, `settled-30s`. Each scenario captures full-page screenshot, console errors, page errors, network failures, and a `world.things` state dump (seat / wallCount / dealerHand / allDiscard / gameType / thingCount).
  - **Result: ZERO page errors across all 10 scenarios.** Residual console noise (`Computed radius is NaN` ×1 + benign 404 ×2) matches the round-3 baseline exactly — same noise floor, not introduced by this sweep. `gameType=CHANGSHA` everywhere; `thingCount=109` (108 tiles + 1 marker) confirms no Riichi 197-thing flip survived the round-3 fix.
  - **bots-4-auto highlight:** auto-played to a Bot 1 WIN at 12s of settle — strongest end-to-end proof the game loop (draw → discard → claim → meld → score → modal) works in CI.
  - **Two cosmetic UX observations (NOT regressions):**
    1. Settings panel takes full viewport at mobile (375) and tablet (768) widths — pre-existing UX documented in `hicks-mobile-375-and-lobby-overlay.md`. On mobile this blocks Quick-Match clicks (`seat=null`, `dealerHand=0`); on tablet the scene rendered correctly behind the panel (`dealerHand=14`). Needs a visible ✕ close affordance at narrow widths — Phase-F/G polish.
    2. `camera-flat` toggle selector not present in current bundle. Scene rendered in standard perspective and matches `desktop-1920`. Feature either was never shipped or lives behind a different control surface. Filed as Stephen/Ripley decision.
  - **Hand-off to Frost:** `settled-30s` (4-bot, 32s) ended in a Draw rather than a Win. Possible Medium-difficulty bot strategy being over-conservative; visual scene clean.
  - **Hand-off to Bishop (low priority):** `/api/games/{id}` returns 404 during WS-first session creation. Frontend handles gracefully; would be nice if REST returned empty-200 for newly-allocated IDs.
  - **Self follow-up (Phase-G ticket):** residual `THREE.BufferGeometry.computeBoundingSphere(): Computed radius is NaN.` source still unidentified. Confirmed not the point-stick tray (round-2 toggle removed tray rendering on Changsha; warning persists). Likely another GLB primitive (`meshes.center` or a tile/marker mesh) with NaN vertex positions. Investigation needs `Number.isFinite` guards on `position.array` walks.
  - Decision memo: `.squad/decisions/inbox/hicks-vreg-sweep.md` (full per-scenario summary table + screenshot paths). Spec is the sole code artifact. Lane discipline kept (touched only spec + memo + this history entry — screenshots gitignored). **Verdict: no regressions vs `hicks-final-clean-2026-06-01T20-52-57Z.png` baseline. The Changsha bring-up is visually + functionally clean end-to-end.**

📌 Visual regression sweep (2026-06-03): 10 scenarios, 0 page errors, no regressions vs round-3 baseline — committed `ce948fe`.

## Pre-07-27 retained detail (05-26 bare-URL flow, 05-26 turn-indicator, 06-04 polish) — summarized 2026-08-07T09-25

> Long-form preserved verbatim in `history-archive.md` (folded 2026-08-07T09-25). Compact:

- **06-04 Polish pass:** (1) mobile/tablet settings panel filled viewport (`@media` 768/480 pushed both `#settings-drawer` + `.settings-drawer-v2` to 100vw/100vh) → re-anchored via new `ui/hicks-mobile-sidebar.css` (`min(90vw,360px)` × `max-height:90vh`, safe-area, recomputed closed-state offset); (2) PROVED Bishop leave-seat broadcast (`35b7f76`) clears the seat label in another tab <1s (no refresh); (3) PROVED Frost `IsWin` gating (`87e53c8`) doesn't break the HandResult modal; (4) 4-bot ~32s Draw visual-coherence re-sweep.
- **05-26 bare-URL fresh-user flow (6 hard blockers):** shipped Fix-1 `gameId` minting in `buildUrl`, Fix-2 `dealMode=auto` default, Fix-3 tour opt-in only, Fix-4 single-click deal w/ disabled-tooltip, Fix-5 discard-rejection 2s toasts. Learning: the z=2000 tour overlay was the highest-friction blocker → made strictly opt-in.
- **05-26 Turn-indicator wave (SHA `7a50257`):** turn-banner 3-state priority (claim window → pickup → discard) + canvas pointer-cursor; appears <500ms of seat reaching 14 tiles; `requestAnimationFrame` debounce. Learning: state-driven (`cli.claim`/`cli.pickup`/`hasExtraHandTile()`) beats DOM-animation inference — banner is a view over state, not source of truth.
- 📌 07-27 team update: your #125 (WP-D) rejection was cleared by Vasquez's revision (flat+perspective `toHaveScreenshot` baselines + BLOCKING `view-visual-gate.yml`); Hudson re-review → APPROVE #125.

## 2026-07-27 — #152 wall non-contiguity + HUD collisions (PR #154, SHA dba8415)

Owned #152 end-to-end from an isolated worktree (`mahjong-autotable-152-fix`,
branch `squad/152-contiguous-wall-layout`) based on exact main `507268f`.

**Canonical wall math (spec §2.3–2.5, verified live):** 108 tiles = 54 stacks;
deal 3×13 + dealer 14 = 53; **exactly 55 remain**. User "1.5 walls" ≈ 55/108.

**Root cause of the four-fragment wall = BACKEND seam.** Reproduced on clean
main (real deal controls + WS state, Auto AND Manual): after deal every seat
holds ~13–14 wall tiles in cols 0..6 only (14/14/14/13=55) → four half-walls.
`AutotableSlotMap.EnumerateWallSlotsInOrder()` packs remaining `state.Wall`
column-major-across-seats, deliberately spreading the remainder (its own
doc-comment cites avoiding "empty walls"; Stephen 2026-05-29). Client renders
tiles only at backend-assigned `slotName` (client.ts `things` keyed on
`slotName`) — no client wall logic; a JS fix would invent authoritative state
(forbidden). **Handed off** the exact backend contract (perimeter-anchored
mapping; reconcile dealer-relative [14,13,14,13] vs absolute [14,14,13,13];
persist front/back-draw counts) via issue comment + decision
`Hicks-152-wall-non-contiguity`. No backend files touched.

**Frontend fix (my lane):** top-bar relayout — moved `#lobby-toggle` out of
the top-left (overlapped `#sidebar`/`#deal`) into the deterministic top-right
toolbar; non-overlapping fixed offsets for install/gears/variant-badge;
coherent mobile top-bar (icon-only lobby pill, right cluster, dice-HUD row 2,
install pill bottom-right). Files: `style.css`, `main.css`,
`ui/hicks-mobile-sidebar.css`, rebuilt `src/frontend/autotable/`.

**Tests:** `hud-no-overlap-152.spec.ts` (RED→GREEN bbox gate, 1920×1080/1160,
1366×768, mobile, flat + perspective) — GREEN. `wall-contiguity-152.spec.ts`
(55-count + contiguous-perimeter signature) — RED on main, gated behind
`WALL_CONTIGUITY_GATE=1`; flips GREEN when the backend mapping lands.

**Validation:** tsc --strict, eslint (new specs), verify:bundle-sync,
pre-commit --all-files, and CI view-visual-gate + visual-regression +
smoke/settings/a11y/lobby/view-mode — all green, no regressions.

PR **#154** opened "Addresses #152" (NOT auto-close — wall part still open for
the backend lane). Cross-lane hand-off flagged as needs-review.

📌 #152: HUD collisions fixed (frontend, PR #154); wall non-contiguity is a
backend translator seam — contract handed off, #152 stays open for it.

## 2026-07-27 (cont.) — #152 wall fix IMPLEMENTED in backend translator (PR #154, SHA a8b516d)

Hudson posted independent confirming evidence (#152 comment 5096546277) and the
parent directed: "Fix stable physical slot identity/depletion, not wall
count/rules. Continue through PR/CI; do not stop at diagnosis." So I crossed
into the backend lane (coordinated via decisions) and implemented the fix.

**Backend change (a8b516d):**
- `AutotableSlotMap.WallOrdinalToSlot(ordinal)` — seat-major perimeter map
  (seat0→3, cols asc, layer 0 then 1); consecutive ordinals physically adjacent.
- `AutotableSlotMap.WallBreakOrdinal(seat, stack)` — anchors arc at the marked
  break seat; clamps stack to render capacity (reconciles engine dealer-relative
  14/13/14/13 vs render absolute 14/14/13/13).
- `ChangshaGameState.WallBackDrawn` — rendering-only counter (no rule impact),
  ++ in DrawFromBack, reset each deal.
- Translator places Wall[i] at WallOrdinalToSlot(break + frontDrawn + i),
  frontDrawn = 108 - Wall.Count - WallBackDrawn. Offset cancels the list shift →
  a tile keeps its slot until drawn (no re-pack). Superseded the 2 Phase-5a
  tests that pinned the old column-major spread.

**Validation:** backend 4633 pass excl. the documented Phase_K_W16/17/18
worktree-only cases (they PASS in the CI clone — CI Test(Sqlite/Postgres/
SqlServer) all green). Real WS repro Auto+Manual: 55 tiles form one contiguous
arc. Stability: 4-bot spectator drew 25 tiles, kept=26/moved=0.

**CI on a8b516d: ALL GREEN** — Test×3 DBs, playability-gate, e2e (incl. new
wall+HUD specs), View visual gate (BLOCKING), visual regression, lighthouse,
bundle-sync, lane-discipline, pre-commit. PR #154 updated to `Closes #152`,
MERGEABLE/CLEAN, NOT merged.

📌 #152 FULLY FIXED in PR #154: wall depletes contiguously (backend translator,
stable slot identity) + HUD collisions resolved. Both defects closed. CI green.

📌 HOLD of record (2026-08-07T09-25, recorded by Scribe): You DECLINED a 09:23 `targetHandles` pickup reopen (re-citing `ripley-SC4-pickup-schema-IMMUTABLE.md`, the < v4 SUPERSEDED form) and HELD pickup on `pickup.targetSlots` (exact-1, top-first, fail-closed; tsc clean, 102 contract tests, 0 new lint). Correct per decisions.md §2026-08-07T09-21 closure ("reopening requires explicit supersession of THIS block") and SC-4 v4-FROZEN — the reopen was DISREGARDED. Extra ground: `hovered.thing.key` is numeric TODAY (SC-2 wall-handle plumbing unshipped), so a handle gate would fail-closed on every tile. Do NOT conflate the TWO string[] fields: (a) SC-2 `things` = opaque HANDLES (rendering/privacy, G19); (b) SC-4 pickup match = public `targetSlots`. Awaiting Bishop `nextTileSlots`→`targetSlots` (len-1, top-layer Wall[0]).

## 2026-09-14 — Independent UI qualification successor only

Fresh independent context delivered `session-files/qualification/2026-09-12/hicks-ui-qualification-revision/approved-context-r5` for Ripley re-review, NOT self-approved. No consultation/delegation to rejected authors. Application b237 source / 7a4ee030 bundle remains APPROVED/HOLD; no application, backend, rules, packaging, protected-helper, shared-verifier or Git edits.

Exact frozen r4 RED reproduced dropped actual baseUnit and stale FULL discard credit. Successor captures actual query without defaults and command-time own-hand/turn/epoch witnesses; complete-batch pre/post comparison plus independent journal-linked exporter validation rejects replay/handless/late/duplicate credit while preserving terminal and rapid-claim discard paths. Three generic helpers byte-identical; original 928 historical files, r4 59 files / 45 manifest inputs, and 106 held production/plan/helper inputs unchanged.

Final frozen validation: 85 TypeScript SOURCE-ONLY contracts (64 inherited + 21 observer controls), 65 Python unittest methods (58 inherited + 7 observer/export controls), strict types pass; no failures/skips/retries. 152 inherited Python assertion ASTs and 180 TypeScript assertions unchanged. Intermediate fixture-prerequisite failures retained, repaired without weakening assertions. Full executed-dependency/fixture manifests and exact patch retained.

Handoff `session-files/qualification/2026-09-12/hicks-ui-qualification-revision/evidence/final/handoff.json` SHA256 `08236f015be6ac07cd9d573d70a3689a61bd34a7711bdcaebf6d6c1854c0f126`. Code manifest SHA256 `698764219120e3f756a3f62a6e4df719c160c17e2e71b348556397d6bd6bfd8c`; patch SHA256 `ebbeadbb374bb1d258b664f00687da5be15db97d936cc4091c5db975e6a27984`.

Shared3.5 lock/API/schema preserved; active unapproved3.6 not imported/reviewed/repinned. Approved-helper compatibility and canonical reseal, independent source reviews, fresh source-built image/full proofs, explicit grants and every native/live browser gate remain deferred. Old C03 forbidden; 9a25 drift blocked; no AppArmor changes. Counts0/120, zero browsers/WS clients/rooms/images/pilots/games. Old runners are history, never a runtime fallback.

### Closing integrity addendum — later external application drift, no harness revision

After the final passing source-only run and initial seal, the closing hash check found current application changes in `src/frontend/autotable-src/src/client-ui.ts` (`ff8ce31e...` → `88e03e6c...`), `lobby.ts` (`42b3c1ac...` → `15e56749...`), and `session-url.ts` (`67e84bc2...` → `ad98fe5d...`). Only hashes/size/modes were compared; this context did not edit, inspect/review, restore, or adopt those changed sources. The earlier equality record is retained as its timestamped observation, not a current-tree approval. Current application admission stays BLOCKED pending the proper owner/coordinator review. All928 frozen historical files, all59 r4 files, the held dist, all52 successor+control inputs, and executed evidence seals still match.

Use FINAL additive review packet `session-files/qualification/2026-09-12/hicks-ui-qualification-revision/evidence/final/review-packet.json` SHA256 `91c7b2806f89eece483be455cfe8604cb9258e89d848e5bcfeb59a574f3b5325`. Drift addendum SHA256 `4ca5afa2958027c8a914d30913cbad5107705e70392c9f0fb8e795981ebcde56`; complete `SHA256SUMS.review` SHA256 `88c9ed8a68c81f57f011318cc3c6bc30f68717597e0c0bef1fbed8bae61d07ef`. Primary handoff, exact patch and all prior results/manifests are unchanged. Source re-review can proceed independently; no waiting on a future image. Counts remain0/120, zero browsers/rooms, no self-approval.

## 2026-09-14 — Coordinator confirms parallel application boundary after harness freeze

Coordinator confirmed that originalHicksd2f4f3f6 has a separate narrow application entry/creation alignment grant with Bishop. The previously observed moving-root source drift belongs to that parallel boundary, not this independent UI-harness revision. No further moving-root UI reads/rebinding, application edits, or source-input reconciliation are authorized here. Copied b237 contracts remain the immutable regression baseline. Application reconciliation requires the NEW application approval after independent review and an explicit coordinator grant.

Shared3.6 was separately rejected for actual HTTP-schema fields; fresh Aponeb309 owns shared3.7 repair. This supersedes the future-helper-version wording in the timestamped frozen handoff, not its source/evidence hashes: helper integration and the existing3.5 lock remain CLOSED and unchanged pending an approved handoff and explicit authority. No locked-author advice or other-owner investigation.

The two observer/export repairs are already complete and frozen in approved-context-r5. Final review packet SHA256 remains91c7b2806f89eece483be455cfe8604cb9258e89d848e5bcfeb59a574f3b5325; code manifest remains698764219120e3f756a3f62a6e4df719c160c17e2e71b348556397d6bd6bfd8c. Passing source-only evidence remains85 TypeScript contracts,65 Python tests and strict types. No new code edits, tests, browsers, rooms or games on this notice. Counts0/120; source re-review ready, not self-approved.

## 2026-09-17 — Multiplayer/lobby/chat frontend handoff

Implemented the approved ripley-before-lobby-repair-20260917 frontend contract in the existing Vite/vanilla chat application. Verified identity now gates both transports; presence/invitations are independent of renderer/chat visibility; public/voice controls consume one room+identity metadata cache; safe join-only links use FindJoinableGame and no numeric-seat handoff; bot choices include 0/1/2/3 and spectator-only 4; REST chat uses canonical DTOs and scoped history. Necessary helper changes are confined to metadata/profile/URL/voice and bot-display seams; no claim/S11/rules/backend/test edits.

Source and dist are sealed. Handoff: /data/Copilot/GitHub-Copilot-linux-x64.AppImage.home/.copilot/session-state/2647f044-06b9-4b9a-bf4b-711b6615e5da/files/lobby-fix-20260917/hicks/frontend-handoff.json. Early selectors are in frontend-selectors.json; complete source/generated byte manifests are in frontend-build-identity.json. Final entry autotable-src.655a3513.js; chat chat.32fe8b62.js; served index SHA-256 9e69692c4c661453fd021d87643653c3a50bd0278bd5bd61682a893791723bed. Frontend input aggregate 16cf90b9880cbabbbb8a543475455a8040a2b02633d3b833f7aa3d19b0a0c1d9; served assets aggregate 7c7105559197f3959ee04ac533780f462dfe4f08bd773f8174ed7b9a896896dd.

CI-parity strict typecheck and normal npm run build succeeded. Focused 17-file ESLint remains exit 1 with exactly the prior 14 errors/1 warning; zero added errors or warnings, no waiver. Seventeen pure URL/config checks passed, plus generated-anchor/reference/hash checks. No live/browser/backend acceptance or image claim; Hudson owns integrated QA and Apone packaging follows acceptance.

One contract seam remains raised to coordinator/Ripley: duplicate same-identity observer presentation cannot be distinguished by the inspected JOINED.playerId + public SeatInfo mapping alone. No entitlement workaround or frozen-wire change was introduced. See runtime decision 94e1326d-8843-4fd4-a3ac-0f5c84f5d366 and the handoff's incompleteSurfaces. No commits/reset/stash/checkout/push, dependency changes, agents, deployment, registry publication or Docker export. Frontend editing/builds have stopped.

## 2026-09-17 — Approved C1 frontend amendment implemented and refrozen

Read ripley/observer-contract-c1.md in full and applied the coordinator grant (not an author rejection). Changed only frontend server/protocol.ts, src/base-client.ts, src/client.ts, and necessary src/client-ui.ts/src/game.ts mode/lifecycle wiring. Changsha requires explicit viewer {roomId, revision, seat}; validated primary-socket authority is applied before connect/update events. Client.seat is now an exact grant getter, while public seatPlayers retain stable identities. Explicit modes preserve relay/offline inference without falling back on connected()==false. Stale/wrong-room frames are discarded before entries; old socket callbacks are epoch-guarded; loss/rejoin/revocation clear authority. Pre-JOINED terminal errors carry no room/seat authority, and protocol-unavailable is explicit. No per-tab identities, backend/test/claim/S11/crypto/R1 edits.

Final C1 handoff: /data/Copilot/GitHub-Copilot-linux-x64.AppImage.home/.copilot/session-state/2647f044-06b9-4b9a-bf4b-711b6615e5da/files/lobby-fix-20260917/hicks/c1-handoff.json. Complete input/asset manifests: hicks/c1/build-identity.json. Source aggregate 8a7c3784f27f815931c5d26d8e908ddab5e92cb5bb6293b5851ecabb6bab147d; served aggregate fd88d97c1848e5d00ffba29f592e0793970697b9577d22e3b02e8a423453893d; index SHA-256 fe5777e7209867bd2214536ecfbf8bbf050ed7637eb00152f724e5292c66b767. Entry autotable-src.f26bdd7b.js; chat chat.6945f5a8.js; client UI client-ui.3e340d42.js. All 142 input and 83 served hashes verified after publication. Other frozen frontend inputs are unchanged; normal Vite build regenerated dist/dist-size only.

Strict CI typecheck and normal npm run build passed. Focused five-file lint delta is zero additions; the prior 1 error/1 warning remain, unwaived. Thirty isolated real-consumer/fake-WS lifecycle checks passed; these are not backend/browser/privacy-matrix/image acceptance. Temporary consumer bundle removed. Production source/builds stopped again. Drake C1 producer pairing, Hudson live/independent QA, Frost's separately rejected R1 repair, and coordinator/Ripley acceptance remain release gates; Apone's old-pin private candidate is not final.
