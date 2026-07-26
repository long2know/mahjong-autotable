// =============================================================================
//  P0 REAL-UI PLAYABILITY GATE — #122 (Hudson, Tester/Reviewer-gate)
// =============================================================================
//
//  THIS IS THE ACCEPTANCE GATE. It proves a human-vs-bots Changsha game is
//  playable to real completion THROUGH THE ACTUAL UI:
//
//    real take-seat click → real #deal press → drive human turns by real
//    canvas pointer discards + real claim-button clicks → poll the
//    SERVER-AUTHORITATIVE gameComplete.isComplete across all 4 hands →
//    assert the real #game-complete-modal opened from a REAL Hu.
//
//  HARD RULES (issue #122 · Lead C-8 · playtest-ws-backdoor SKILL:88-93):
//    • NO WS backdoor may advance or satisfy this test. Not client.update,
//      not events.emit, not world.emitDiscard(id), not collection injection,
//      not server-state mutation. Every forward move here is a real
//      Playwright pointer/click event (see tests/e2e/_playability.ts header).
//    • The bundle under test MUST be the freshly-built source bundle — a
//      content-hash preflight enforces served === built before we interact.
//    • Assertions are NOT weakened to make current HEAD pass. The gate is
//      SCAFFOLDED NOW and goes green only after its dependencies land:
//        WP-A #116 (per-hand manual ceremony → full game completes via UI),
//        WP-D #119 (deterministic bundle-hash gate),
//        WP-E #120 (P0-2 real-UI connect flow).
//      Until then this gate FAILS HONESTLY at the first real blocker and
//      writes evidence to playtest-artifacts/playability-gate/.
//
//  Reviewer independence: Hudson AUTHORED this gate and may NOT self-approve
//  it; Ripley (Lead) independently verifies WP-F.
//
//  Selectors used here are catalogued in tests/selectors.md → "Playability
//  gate (#122)".
// =============================================================================

import { test, expect } from '@playwright/test';
import {
  makeConfig,
  buildGameUrl,
  checkServedBundleMatchesBuild,
  defangOverlays,
  dismissLobbyAndTour,
  ensureConnected,
  takeSeatByClick,
  clickDeal,
  discardByPointer,
  claimByClick,
  readClaimWindow,
  hasExtraHandTile,
  readResult,
  readGameComplete,
  readMatch,
  readPickup,
  waitForPlayableHand,
  isGameCompleteModalVisible,
  waitForGameObject,
  Recorder,
  snap,
} from './_playability';

// Bounded, deterministic budget. A real 4-hand Changsha game with Hard bots
// completes well within this; the cap keeps a stalled build from hanging CI.
const GAME_BUDGET_MS = 4 * 60_000;
const POLL_INTERVAL_MS = 800;

test.describe('@playability-gate P0 real-UI playability (4-hand human-vs-bots → real Hu)', () => {
  test('human plays 4 hands via real DOM/canvas to a real game-complete modal', async ({
    page,
    request,
    baseURL,
  }) => {
    test.setTimeout(GAME_BUDGET_MS + 90_000);
    const rec = new Recorder();
    const cfg = makeConfig();

    // Surface browser diagnostics into the run log for blocker triage.
    const pageErrors: string[] = [];
    page.on('pageerror', (e) => pageErrors.push(e.message));
    page.on('console', (m) => {
      if (m.type() === 'error') pageErrors.push(`console: ${m.text()}`);
    });

    const resolvedBase = baseURL ?? process.env.E2E_BASE_URL ?? 'http://localhost:8080/autotable/';

    // ── PREFLIGHT ────────────────────────────────────────────────────────
    // The served bundle MUST be the freshly-built source bundle (C-8). A stale
    // bundle invalidates every UI assertion below. Recorded now; asserted at the
    // end so findings are always written regardless of where the gate fails.
    const bundle = await checkServedBundleMatchesBuild(request, resolvedBase);
    rec.log('preflight.bundle-hash', bundle.ok, bundle);

    // ── REAL CONNECT + SEAT ──────────────────────────────────────────────
    await defangOverlays(page);
    await page.goto(buildGameUrl(resolvedBase, cfg), { waitUntil: 'domcontentloaded' });
    const hasGame = await waitForGameObject(page);
    rec.log('boot.game-object', hasGame, { url: page.url() });

    await dismissLobbyAndTour(page);
    const connected = await ensureConnected(page);
    rec.log('connect.ws', connected, { connected });
    await snap(page, '01-after-connect.png');

    const seat = await takeSeatByClick(page, cfg.seat);
    rec.log('seat.take', seat !== null, { requested: cfg.seat, assigned: seat });
    await snap(page, '02-after-take-seat.png');

    // ── REAL DEAL ────────────────────────────────────────────────────────
    const dealt = await clickDeal(page);
    rec.log('deal.press', dealt, null);
    // Give the manual pickup ceremony (client-driven) time to place the hand,
    // then wait until it's our turn to discard (dealer holds the 14th tile).
    const playable = await waitForPlayableHand(page, 45_000);
    rec.log('deal.playable-hand', playable.playable, playable);
    await snap(page, '03-after-deal.png');

    // ── DRIVE THE GAME BY REAL INTERACTIONS ──────────────────────────────
    // We NEVER call a backdoor. Each loop: answer a claim window (real Hu when
    // offered, else Pass), else discard our 14th tile by real pointer, else
    // wait for the bots. We continuously observe the server-authoritative
    // result + match to prove hand/dealer progression, and poll gameComplete.
    // The loop is skipped when the ceremony never produced a playable hand, so a
    // stalled deal reports its blocker fast instead of spinning for the budget.
    const handsSeen = new Set<string>();
    const dealersSeen = new Set<number>();
    let discardsFired = 0;
    let realHuByHuman = false;
    let lastResultSig = '';
    let consecutiveDiscardMisses = 0;
    let gc = await readGameComplete(page);

    const deadline = Date.now() + GAME_BUDGET_MS;
    while (playable.playable && Date.now() < deadline) {
      gc = await readGameComplete(page);
      if (gc.isComplete) break;

      const match = await readMatch(page);
      if (match.dealer !== null) dealersSeen.add(match.dealer);

      // Record each distinct hand-end result (server-authoritative).
      const r = await readResult(page);
      if (r.present) {
        const sig = JSON.stringify([r.winner, r.type, r.nextBanker]);
        if (sig !== lastResultSig) {
          lastResultSig = sig;
          handsSeen.add(sig);
          rec.log('hand.result', true, r);
          if (r.type === 'Hu' && r.winner === seat) realHuByHuman = true;
        }
      }

      // Real claim response if a window is open for us.
      const claim = await readClaimWindow(page);
      if (claim.open) {
        const clicked = await claimByClick(page);
        rec.log('claim.click', clicked !== null, { available: claim.available, clicked });
        if (clicked === 'Hu') realHuByHuman = true;
        await page.waitForTimeout(POLL_INTERVAL_MS);
        continue;
      }

      // Real discard when it's our turn (we hold the 14th tile).
      if (await hasExtraHandTile(page)) {
        const out = await discardByPointer(page);
        rec.log('discard.pointer', out.ok, out);
        if (out.ok) {
          discardsFired++;
          consecutiveDiscardMisses = 0;
        } else {
          // Don't give up on the first miss — the projection can miss on a
          // transient camera/animation frame. Retry a few times before
          // treating it as a hard, reproducible blocker.
          consecutiveDiscardMisses++;
          if (consecutiveDiscardMisses === 1) await snap(page, '04-discard-miss.png');
          if (consecutiveDiscardMisses >= 6) {
            await snap(page, '04-discard-blocked.png');
            rec.log('discard.blocked', false, {
              misses: consecutiveDiscardMisses,
              pickup: await readPickup(page),
            });
            break;
          }
        }
        await page.waitForTimeout(POLL_INTERVAL_MS);
        continue;
      }

      // Otherwise the bots are acting; wait and re-observe.
      await page.waitForTimeout(POLL_INTERVAL_MS);
    }

    gc = await readGameComplete(page);
    const modalVisible = await isGameCompleteModalVisible(page);
    await snap(page, '05-final-state.png');

    const summary = {
      seat,
      connected,
      dealt,
      playableHand: playable.playable,
      handsSeen: handsSeen.size,
      dealersSeen: [...dealersSeen],
      discardsFired,
      realHuByHuman,
      gameComplete: gc,
      modalVisible,
      lastPickup: playable.lastPickup,
      pageErrors: pageErrors.slice(0, 20),
      timedOut: playable.playable && Date.now() >= deadline,
    };
    const passed = bundle.ok && gc.isComplete && modalVisible && handsSeen.size >= cfg.handCount;
    rec.log('gate.summary', passed, summary);
    const evidencePath = rec.write('playability-gate-findings.json', {
      config: cfg,
      bundle,
      summary,
    });
    // eslint-disable-next-line no-console
    console.log(`[gate] evidence written → ${evidencePath}`);

    // ── P0 ACCEPTANCE ASSERTIONS (full strength — do NOT weaken) ─────────
    // Ordered from most-fundamental to keystone; findings are already on disk.

    expect(
      bundle.ok,
      `BUNDLE PREFLIGHT FAILED — the backend is not serving the freshly-built ` +
        `source bundle.\n${bundle.reason}`,
    ).toBe(true);

    // Connect + seat are P0-2 (#120) territory.
    expect(
      connected && seat !== null,
      `REAL CONNECT FLOW BLOCKED (WP-E/#120): connected=${connected}, seat=${seat}. ` +
        `A human could not connect and take a seat through the UI.`,
    ).toBe(true);

    expect(
      dealt,
      `REAL DEAL BLOCKED: the #deal press did not start a hand. ` +
        `Manual per-hand ceremony is WP-A/#116.`,
    ).toBe(true);

    // The manual ceremony must deliver a *playable* hand: the dealer's 14th
    // tile (DealerExtra) so hasExtraHandTile() is true and a real discard is
    // possible. If it stalls, report the exact pickup phase it stuck on.
    expect(
      playable.playable,
      `MANUAL DEAL CEREMONY STALLED — the dealer never received a playable ` +
        `14th tile, so a human cannot discard. Last pickup cursor: ` +
        `${JSON.stringify(playable.lastPickup.raw)} (myHandCount=${playable.myHandCount}). ` +
        `Root cause observed at HEAD: world.ts driveManualDealChain() drives only ` +
        `4 pickup 'take' rounds (PickupRound1-3 + SingleTilePickup), but the DEALER ` +
        `needs a 5th (DealerExtra). HANDOFF: per-hand manual ceremony is WP-A/#116 ` +
        `(runtime) + world.ts driveManualDealChain (frontend, Hicks lane).`,
    ).toBe(true);

    // Bots-progress gate (P1-4 E2E side): at least one real discard fired.
    expect(
      discardsFired,
      `NO REAL DISCARD FIRED through the canvas. A human could not discard a ` +
        `single tile via real pointer interaction. Blocker in world.onDragStart/` +
        `emitDiscard or the manual pickup ceremony (WP-A/#116).`,
    ).toBeGreaterThan(0);

    // Hand/dealer progression (P1-4): the match must move through 4 hands.
    expect(
      handsSeen.size,
      `HAND PROGRESSION INCOMPLETE: only ${handsSeen.size} distinct hand ` +
        `result(s) observed; a full match is ${cfg.handCount} hands. ` +
        `Per-hand manual ceremony is WP-A/#116.`,
    ).toBeGreaterThanOrEqual(cfg.handCount);

    // The keystone: server-authoritative completion + real modal from a real Hu.
    expect(
      gc.isComplete,
      `REAL gameComplete.isComplete NEVER SET. The autotable WS backend does ` +
        `not emit a 'gameComplete' collection entry (no ChangshaCollectionKinds ` +
        `.GameComplete; translator emits only result["current"]). The end-of-match ` +
        `signal that drives #game-complete-modal is unwired for the real UI — ` +
        `HANDOFF to WP-A/Bishop (runtime + ChangshaToAutotableTranslator).`,
    ).toBe(true);

    expect(
      modalVisible,
      `#game-complete-modal NOT VISIBLE after real completion. The scoring modal ` +
        `must open from a real Hu, not a backdoor.`,
    ).toBe(true);
  });
});
