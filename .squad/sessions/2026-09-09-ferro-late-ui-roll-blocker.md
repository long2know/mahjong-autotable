# Confirmed UI-only manual-start race — Ferro, September 9, 2026

## Actual narrow C01 browser result
Window **17:24:18–17:24:30 PDT** (2026-09-10T00:24:18.151Z–00:24:30.338Z). All browsers CLOSED; slot released. C01 used only for new-blocker diagnosis, NOT final acceptance.

Two fresh manual/seat0/3Medium/seed94209 rooms with real WS authority:
- **Normal delivery PASS:** confirmed connected seat0, dealer0, RollingDice/pickup null, installed GameUi, Roll visible+enabled; an ordinary real pointer click is accepted → BreakPointMarked/own pickup4/exact1 target.
- **Delayed unchanged `scene-effects.*.js` delivery RED:** real server reaches RollingDice/dealer0/seat0 while UI module is still in transit. After releasing the ORIGINAL module without changing bytes, GameUi installs, but Roll remains `hidden=true`, `display:none` for the complete5s visibility window despite unchanged authoritative prerequisites. No game state, DOM, server state or control injection; only a legitimate late network-delivery order is exercised. All source/canonical-test/helper/dist files untouched. No page errors.

This proves a real UI-offered-action mismatch and a slow/lazy-load manual-start deadlock. It demonstrates the same visible failure as Dietrich r3; r3 did not record historical timing, so exact attribution of that older room remains unproved.

## Mechanism and bounded proposed correction
Read-only source chain: `game.ts:24–29,47–79,124–127` starts ClientUi before lazy GameUi; `game-ui.ts:2256–2274` already explicitly handles late GameComplete snapshots because Collection.on does not replay existing entries. But `setupPickupHud` (`game-ui.ts:1624–1632`) only subscribes to future pickup updates, and `setupTurnBanner` never hydrates the Roll/Pickup HUD at construction. Existing `onPickupUpdate()` already reads authoritative current pickup + turn/dealer/confirmed seat, handles null tombstones, and renders Roll/HUD/break marker. A bounded initial hydration there is the proposed correction, not local deal/auto-roll or new defaults.

**Proposed production file:** `src/frontend/autotable-src/src/game-ui.ts` only (named UI lane).
**Proposed regression grant:** NEW `src/frontend/autotable-src/tests/e2e/changsha-late-ui-controls.spec.ts` or an independent eligible test author; existing specs/helpers/config remain frozen. Gate should retain both normal and late-module order, use actual server truth, and require a real Roll click to advance after hydration. No retries/timeout-only fix/skips/game injection. Need coordinator confirmation of eligible revision/test author before crossing frozen test boundary or any implicated rejected artifact.

Current GameUi SHA256 **`50d25b18b7edbb60067e1fcbff6bce27b7fa5ba7f56aadba5219766b07d9cb68`** (unchanged baseline). No known exact rejection of this new late-subscriber artifact was found; historical per-file authorship is not assumed to grant all cycles. No corrective edit yet.

## Repro / immutable evidence
`E2E_BASE_URL=http://127.0.0.1:18190/autotable/ TMPDIR="$PWD/session-files/completion-proof/2026-09-09/ferro/<new-run>/runtime" node session-files/completion-proof/2026-09-09/ferro/probe-late-ui-roll.cjs session-files/completion-proof/2026-09-09/ferro/<new-run>` (create unique runtime directory first).

Actual evidence: `session-files/completion-proof/2026-09-09/ferro/c01-late-ui-roll-01/` — results, full command output, normal/delayed screenshots, START/END HTTP+container pins, fingerprints.
- Script SHA256 `557ba0ba633bde4e4d559f9ec4272122a6d9d2f926b63a22e0ee5fb123f7d71d`.
- Results SHA256 `95d5c3eff3a0dc8f460c40dda7115f88bf4b386120cd4ae863723a5f561643bd`.
- Delayed original lazy module SHA256 `7cc99a07334a16bb44fdc55e326d25cf2a52ca343c3bb84ec40d57674c8907cd`.
- Normal screenshot `f98bbd408675b93e036c1ed3362cdf399018bf80afce3f9cb803ea73006b76c0`; RED screenshot `5798525eec038e877157b65d1377775b7707396c2af60c3144d0f20ebf8090f7`.

START/END immutable C01 match: image `sha256:113c10b315f41483bdd0e1b0879ffdee08399e4f8b2dd91fc37671284ba10461`; DLL `836821f3819d881fc1ee7bc1a480f98b16adf32e7b37e21449b70d04f1b4cec5`; index `37796c25f581a1e49b26179718bccdc70148e0618997cb4ce5c6cec67cbed6eb`; entry09309beb SHA256 `790a5bef6f5a2a7cf764fa69914601b549815155660b44d26996f58d444e191a`. C01 remains separately confidentiality/hidden-mesh RED. C02 final UI proof must include reviewed corrections; do not treat this diagnostic as acceptance.

## Authorized correction IMPLEMENTED / exact review freeze — September9 18:38 PDT

Consumed explicit narrow author/test grant in latest `sessions/2026-09-09-c02-build-go.md`. Changed **only** production `game-ui.ts` (+2 lines: comment + initial `this.onPickupUpdate()` after subscribing) and NEW `changsha-late-ui-controls.spec.ts`. Existing renderer/backend/S11/helpers/config/package/dist remain untouched. The existing authoritative renderer handles null tombstones, cached Roll phase/dealer/confirmed seat, cached pickup HUD and marker; no local deal, auto-roll/take or default changes.

Exact frozen production SHA256 **`4de6d7682e7083132cd03f025e5425fa3d10becdac7efa9aa8e8583459f37583`**.
Exact new test SHA256 **`8673cbee03e13f37fb474b57fe29523659aa02b18847849599040b2f4d3dc140`**.
Review copies + tiny production diff + full manifest: `session-files/completion-proof/2026-09-09/ferro/ui-hydration-review-freeze-r1/`; manifest SHA256 **`cdd85bb637c680462f01938e86a22a46ac12677350d67cf7d874e3cff1025f57`**.

Fresh validation:
- Targeted strict app/new-test TypeScript: PASS.
- New regression ESLint: PASS, 0 errors/0 warnings (existing legacy-config deprecation only). Two initial return-type warnings were corrected before freeze; initial/final logs both retained. Unrelated source lint debt is not altered/waived.
- Existing turn/tombstone + mode-chrome source contracts rechecked: **39 PASS/0fail/0skip/0flaky**, workers1/retries0. These overlap earlier134, not additional unique coverage.
- NEW canonical regression on immutable PRE-FIX C01: **1 PASS (normal real Roll+human take), 2 RED (late RollingDice → hidden Roll; same-owner reload with cached pickup4 → hidden HUD), 0skip/0flaky/retries0**. This proves both defects without relying on the first ad-hoc diagnostic. Actual run18:35:33–18:36:17 PDT, source/testhashes retained, no browser remains active. C01 HTTP index/entry/CSS START/END unchanged; actual END image/DLL still match C01.
- New canonical RED results `c01-late-ui-regression-red-01/results.json`, SHA256 **`78ea9436c409d9c15be83bdf4ebee09ec6ba208f1fb4b7e9d220e10e4b78abb6`**.

The new3-case regression uses genuine mouse/touch activation, authoritative-before-UI preconditions, ordinary same-room owner reload, exact1 `{seatIndex:0,count:4}` outbound take and exact4 owned-hand result. HTTP delivery is controlled with service workers blocked solely for observable original-module routing. No native/control/game event injection, synthetic success, new skip or timeout masking.

**Independent exact-version review and post-fix C02 browser GREEN remain PENDING**, not self-approved. Apone must include the reviewed UI bytes in final C02; source-only greens and C01 RED/control are not fixed-image acceptance.
