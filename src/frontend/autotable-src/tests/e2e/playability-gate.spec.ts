// =============================================================================
//  P0 REAL-UI PLAYABILITY GATE — #122 (Hudson, Tester/Reviewer-gate)
// =============================================================================
//
//  THIS IS THE ACCEPTANCE GATE. It proves a human-vs-bots Changsha game is
//  playable to real completion THROUGH THE ACTUAL UI, and that the server
//  HONORS the requested match length (handCount / MaxHands):
//
//    real take-seat click → real #deal press → drive human turns by real
//    canvas pointer discards + real claim-button clicks → poll the
//    SERVER-AUTHORITATIVE gameComplete.isComplete → assert the real
//    #game-complete-modal opened from a REAL completion.
//
//  Lead decision (2026-07-26): handCount is REAL, not decorative — the server
//  MUST honor 1/4/8/16/32. So the bounded acceptance run uses a NON-DEFAULT
//  handCount (=1) and asserts the match completes in EXACTLY that many hands
//  with server MaxHands === requested, and a separate config check asserts the
//  server honors 8 and 16. This makes an implementation that ignores handCount
//  and always plays the default 4 UNABLE to evade the gate.
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
//        WP-A #116/#123 (per-hand manual ceremony + Manual+bots→GameComplete
//                        + handCount honored server-side),
//        WP-D #119/#125 (deterministic bundle-hash gate),
//        WP-E #120/#127 (P0-2 real-UI connect flow + handCount forwarded).
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
import type { Page, APIRequestContext } from '@playwright/test';
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
  readMaxHands,
  readPickup,
  waitForPlayableHand,
  isGameCompleteModalVisible,
  waitForGameObject,
  Recorder,
  snap,
  type GameConfig,
  type BundleHashResult,
} from './_playability';

// Bounded, deterministic budget. A real single-hand Changsha game with Hard
// bots completes well within this; the cap keeps a stalled build from hanging.
const GAME_BUDGET_MS = 4 * 60_000;
const POLL_INTERVAL_MS = 800;

function resolveBase(baseURL: string | undefined): string {
  return baseURL ?? process.env.E2E_BASE_URL ?? 'http://localhost:8080/autotable/';
}

interface ConnectDealResult {
  bundle: BundleHashResult;
  hasGame: boolean;
  connected: boolean;
  seat: number | null;
  dealt: boolean;
}

/**
 * Shared REAL-UI opening: bundle-hash preflight → boot → real connect → real
 * take-seat → real #deal. No backdoor. Screenshots/logs are tagged by
 * handCount so multi-config runs keep distinct evidence.
 */
async function connectAndDeal(
  page: Page,
  request: APIRequestContext,
  resolvedBase: string,
  cfg: GameConfig,
  rec: Recorder,
): Promise<ConnectDealResult> {
  const label = `hc${cfg.handCount}`;

  // Preflight recorded now; asserted by the caller so findings always write.
  const bundle = await checkServedBundleMatchesBuild(request, resolvedBase);
  rec.log('preflight.bundle-hash', bundle.ok, {
    ok: bundle.ok,
    entryShaMatches: bundle.entryShaMatches,
    reason: bundle.reason,
  });

  await defangOverlays(page);
  await page.goto(buildGameUrl(resolvedBase, cfg), { waitUntil: 'domcontentloaded' });
  const hasGame = await waitForGameObject(page);
  rec.log('boot.game-object', hasGame, { url: page.url(), handCount: cfg.handCount });

  await dismissLobbyAndTour(page);
  const connected = await ensureConnected(page);
  rec.log('connect.ws', connected, { connected });
  await snap(page, `${label}-01-after-connect.png`);

  const seat = await takeSeatByClick(page, cfg.seat);
  rec.log('seat.take', seat !== null, { requested: cfg.seat, assigned: seat });
  await snap(page, `${label}-02-after-take-seat.png`);

  const dealt = await clickDeal(page);
  rec.log('deal.press', dealt, { handCount: cfg.handCount });

  return { bundle, hasGame, connected, seat, dealt };
}

interface PlayResult {
  handsSeen: Set<string>;
  dealersSeen: Set<number>;
  discardsFired: number;
  realHuByHuman: boolean;
  gc: Awaited<ReturnType<typeof readGameComplete>>;
  modalVisible: boolean;
}

/**
 * Drive the game to completion with REAL interactions only: answer a claim
 * window (real Hu when offered, else Pass), else discard our 14th tile by real
 * pointer, else wait for the bots. Continuously observes the
 * server-authoritative result + match to prove hand/dealer progression.
 */
async function driveToCompletion(
  page: Page,
  seat: number | null,
  rec: Recorder,
  budgetMs: number,
): Promise<PlayResult> {
  const handsSeen = new Set<string>();
  const dealersSeen = new Set<number>();
  let discardsFired = 0;
  let realHuByHuman = false;
  let lastResultSig = '';
  let consecutiveDiscardMisses = 0;
  let gc = await readGameComplete(page);

  const deadline = Date.now() + budgetMs;
  while (Date.now() < deadline) {
    gc = await readGameComplete(page);
    if (gc.isComplete) break;

    const match = await readMatch(page);
    if (match.dealer !== null) dealersSeen.add(match.dealer);

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

    const claim = await readClaimWindow(page);
    if (claim.open) {
      const clicked = await claimByClick(page);
      rec.log('claim.click', clicked !== null, { available: claim.available, clicked });
      if (clicked === 'Hu') realHuByHuman = true;
      await page.waitForTimeout(POLL_INTERVAL_MS);
      continue;
    }

    if (await hasExtraHandTile(page)) {
      const out = await discardByPointer(page);
      rec.log('discard.pointer', out.ok, out);
      if (out.ok) {
        discardsFired++;
        consecutiveDiscardMisses = 0;
      } else {
        consecutiveDiscardMisses++;
        if (consecutiveDiscardMisses === 1) await snap(page, 'discard-miss.png');
        if (consecutiveDiscardMisses >= 6) {
          await snap(page, 'discard-blocked.png');
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

    await page.waitForTimeout(POLL_INTERVAL_MS);
  }

  gc = await readGameComplete(page);
  const modalVisible = await isGameCompleteModalVisible(page);
  return { handsSeen, dealersSeen, discardsFired, realHuByHuman, gc, modalVisible };
}

test.describe('@playability-gate P0 real-UI playability (human-vs-bots → real completion)', () => {
  // ── PRIMARY BOUNDED ACCEPTANCE — non-default handCount = 1 ───────────────
  // Using handCount=1 (a) bounds the run to a single hand for CI speed and
  // (b) DEFEATS THE "ignored MaxHands" EVASION: a server stuck at the default
  // 4 hands cannot pass a gate that requires completion in EXACTLY 1 hand with
  // server MaxHands === 1.
  test('human plays a bounded handCount=1 match via real DOM/canvas to a real game-complete modal', async ({
    page,
    request,
    baseURL,
  }) => {
    test.setTimeout(GAME_BUDGET_MS + 90_000);
    const rec = new Recorder();
    const cfg = makeConfig({ handCount: 1, gameId: `playability-gate-hc1-${process.env.PLAYABILITY_RUN_ID ?? 'local'}` });

    const pageErrors: string[] = [];
    page.on('pageerror', (e) => pageErrors.push(e.message));
    page.on('console', (m) => {
      if (m.type() === 'error') pageErrors.push(`console: ${m.text()}`);
    });

    const resolvedBase = resolveBase(baseURL);
    const cd = await connectAndDeal(page, request, resolvedBase, cfg, rec);
    await snap(page, 'hc1-03-after-deal.png');

    // The manual ceremony must deliver a playable hand (dealer's 14th tile).
    const playable = await waitForPlayableHand(page, 45_000);
    rec.log('deal.playable-hand', playable.playable, playable);

    let play: PlayResult = {
      handsSeen: new Set<string>(),
      dealersSeen: new Set<number>(),
      discardsFired: 0,
      realHuByHuman: false,
      gc: await readGameComplete(page),
      modalVisible: false,
    };
    if (playable.playable) {
      play = await driveToCompletion(page, cd.seat, rec, GAME_BUDGET_MS);
    }

    const observedMaxHands = await readMaxHands(page);
    await snap(page, 'hc1-05-final-state.png');

    const summary = {
      handCount: cfg.handCount,
      seat: cd.seat,
      connected: cd.connected,
      dealt: cd.dealt,
      playableHand: playable.playable,
      handsSeen: play.handsSeen.size,
      dealersSeen: [...play.dealersSeen],
      discardsFired: play.discardsFired,
      realHuByHuman: play.realHuByHuman,
      gameComplete: play.gc,
      modalVisible: play.modalVisible,
      observedMaxHands,
      lastPickup: playable.lastPickup,
      pageErrors: pageErrors.slice(0, 20),
    };
    const passed =
      cd.bundle.ok &&
      play.gc.isComplete &&
      play.modalVisible &&
      play.handsSeen.size === cfg.handCount &&
      observedMaxHands.value === cfg.handCount;
    rec.log('gate.summary', passed, summary);
    const evidencePath = rec.write('playability-gate-findings.json', {
      config: cfg,
      bundle: cd.bundle,
      summary,
    });
    // eslint-disable-next-line no-console
    console.log(`[gate] evidence written → ${evidencePath}`);

    // ── P0 ACCEPTANCE ASSERTIONS (full strength — do NOT weaken) ─────────
    expect(
      cd.bundle.ok,
      `BUNDLE PREFLIGHT FAILED — the backend is not serving the freshly-built ` +
        `source bundle.\n${cd.bundle.reason}`,
    ).toBe(true);

    expect(
      cd.connected && cd.seat !== null,
      `REAL CONNECT FLOW BLOCKED (WP-E/#120/#127): connected=${cd.connected}, seat=${cd.seat}. ` +
        `A human could not connect and take a seat through the UI.`,
    ).toBe(true);

    expect(
      cd.dealt,
      `REAL DEAL BLOCKED: the #deal press did not start a hand. ` +
        `Manual per-hand ceremony is WP-A/#116/#123.`,
    ).toBe(true);

    expect(
      playable.playable,
      `MANUAL DEAL CEREMONY STALLED — the dealer never received a playable ` +
        `14th tile, so a human cannot discard. Last pickup cursor: ` +
        `${JSON.stringify(playable.lastPickup.raw)} (myHandCount=${playable.myHandCount}). ` +
        `Root cause observed at HEAD: world.ts driveManualDealChain() drives only ` +
        `4 pickup 'take' rounds (PickupRound1-3 + SingleTilePickup), but the DEALER ` +
        `needs a 5th (DealerExtra). HANDOFF: per-hand manual ceremony is WP-A/#116/#123 ` +
        `(runtime) + world.ts driveManualDealChain (frontend, Hicks lane).`,
    ).toBe(true);

    expect(
      play.discardsFired,
      `NO REAL DISCARD FIRED through the canvas. A human could not discard a ` +
        `single tile via real pointer interaction. Blocker in world.onDragStart/` +
        `emitDiscard or the manual pickup ceremony (WP-A/#116/#123).`,
    ).toBeGreaterThan(0);

    // Server-authoritative completion + real modal.
    expect(
      play.gc.isComplete,
      `REAL gameComplete.isComplete NEVER SET. The autotable WS backend does ` +
        `not emit a 'gameComplete' collection entry (no ChangshaCollectionKinds ` +
        `.GameComplete; translator emits only result["current"]). The end-of-match ` +
        `signal that drives #game-complete-modal is unwired for the real UI — ` +
        `HANDOFF to WP-A/Bishop (#123 runtime + ChangshaToAutotableTranslator).`,
    ).toBe(true);

    expect(
      play.modalVisible,
      `#game-complete-modal NOT VISIBLE after real completion. The scoring modal ` +
        `must open from a real completion, not a backdoor.`,
    ).toBe(true);

    // ── ANTI-EVASION: server honored the non-default handCount ───────────
    // Exactly one hand (not the default 4) — a server that ignores handCount
    // would over-run this.
    expect(
      play.handsSeen.size,
      `HAND COUNT NOT HONORED: observed ${play.handsSeen.size} distinct hand ` +
        `result(s) for a handCount=${cfg.handCount} match. A server that ignores ` +
        `handCount and plays the default 4 hands fails here. HANDOFF: Ferro ` +
        `(#127 — forward handCount in buildWsUrl) + Bishop (#123 — honor MaxHands).`,
    ).toBe(cfg.handCount);

    // Server-observed MaxHands must equal the requested handCount.
    expect(
      observedMaxHands.value,
      `SERVER MaxHands != requested handCount (${cfg.handCount}). Observed: ` +
        `${JSON.stringify(observedMaxHands)}. handCount is decorative on the wire ` +
        `at HEAD — client-ui.ts buildWsUrl does not forward it and ` +
        `AutotableWsEndpoint does not read maxHands; MaxHands defaults to 4. ` +
        `HANDOFF: Ferro (#127 forward handCount) + Bishop (#123 read + honor + ` +
        `surface MaxHands in a real collection so it is observable without a backdoor).`,
    ).toBe(cfg.handCount);
  });

  // ── CONFIG-HONORED CHECKS — non-default handCount 8 and 16 ───────────────
  // These do NOT need a full multi-hand game: they connect + deal, then assert
  // the SERVER surfaces MaxHands === the requested non-default handCount. If
  // handCount were decorative (server stuck at 4), these fail. Bounded and
  // cheap, they close the "ignored MaxHands" evasion for larger match lengths.
  for (const hc of [8, 16] as const) {
    test(`server honors non-default handCount=${hc} (real-UI config assertion, no backdoor)`, async ({
      page,
      request,
      baseURL,
    }) => {
      test.setTimeout(120_000);
      const rec = new Recorder();
      const cfg = makeConfig({
        handCount: hc,
        gameId: `playability-gate-hc${hc}-${process.env.PLAYABILITY_RUN_ID ?? 'local'}`,
      });

      const resolvedBase = resolveBase(baseURL);
      const cd = await connectAndDeal(page, request, resolvedBase, cfg, rec);
      await snap(page, `hc${hc}-03-after-deal.png`);

      // Poll for the server-observed MaxHands (no full game needed).
      let observed = await readMaxHands(page);
      const deadline = Date.now() + 25_000;
      while (observed.value === null && Date.now() < deadline) {
        await page.waitForTimeout(1000);
        observed = await readMaxHands(page);
      }
      rec.log('handcount.observed', observed.value === hc, {
        requested: hc,
        observed,
        match: await readMatch(page),
      });
      rec.write(`playability-gate-hc${hc}-findings.json`, {
        config: cfg,
        bundle: cd.bundle,
        connected: cd.connected,
        seat: cd.seat,
        dealt: cd.dealt,
        observed,
      });

      // Fundamental preconditions (recorded above).
      expect(
        cd.bundle.ok,
        `BUNDLE PREFLIGHT FAILED — not serving the freshly-built bundle.\n${cd.bundle.reason}`,
      ).toBe(true);
      expect(
        cd.connected && cd.seat !== null,
        `REAL CONNECT FLOW BLOCKED (WP-E/#120/#127): connected=${cd.connected}, seat=${cd.seat}.`,
      ).toBe(true);

      // Anti-evasion keystone: the server must honor the requested handCount.
      expect(
        observed.value,
        `handCount=${hc} NOT honored server-side (observed MaxHands=${JSON.stringify(observed)}). ` +
          `handCount is decorative on the wire at HEAD: client-ui.ts buildWsUrl does not ` +
          `forward handCount and AutotableWsEndpoint does not read maxHands; MaxHands ` +
          `defaults to 4. HANDOFF: Ferro (#127 — forward handCount in buildWsUrl) + Bishop ` +
          `(#123 — read it at the WS handshake, set MaxHands, and surface it in a real ` +
          `collection e.g. match.conditions.maxHands so it is observable without a backdoor).`,
      ).toBe(hc);
    });
  }
});
