// =============================================================================
//  P0 REAL-UI PLAYABILITY GATE — #122 (Hudson, Tester/Reviewer-gate)
// =============================================================================
//
//  THE acceptance gate for "the autotable integration is complete and
//  functional". Two complementary proofs, BOTH via real DOM/canvas/pointer
//  interactions only (NO WS backdoor — no client.update, no synthetic events,
//  no direct emitDiscard, no collection injection, no server-state mutation;
//  read-only observation of window.game.* state is allowed):
//
//   1. FULL 4-HAND human-vs-bots Changsha playthrough → authoritative
//      GameComplete + scoring modal. Exercises the whole integration: manual
//      wall/dice/5-step dealer pickup, real tile discards, claim buttons, bot
//      turns, dealer/washout progression, zero-sum scoring, the game-complete
//      modal, and BOTH perspective + flat views toggled mid-game. Asserts no
//      unhandled console/page/CSP errors and served-bundle hash == fresh build.
//
//   2. BOUNDED non-default handCount cases proving the server is authoritative
//      over the match length (1 human-vs-bots; 8 + 16 all-bot completion) —
//      a server that ignored handCount and always played the default 4 cannot
//      pass these.
//
//  Reviewer independence: Hudson AUTHORED this gate and may NOT self-approve
//  it; Ripley (Lead) independently verifies WP-F. Selectors are catalogued in
//  tests/selectors.md → "Playability gate (#122)".
// =============================================================================

import { test, expect } from '@playwright/test';
import type { Page, APIRequestContext, ConsoleMessage } from '@playwright/test';
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
  takePickup,
  readIsMyPickupTurn,
  claimByClick,
  readClaimWindow,
  hasExtraHandTile,
  installHandEndObserver,
  readHandEndObserver,
  readGameComplete,
  readMatch,
  readMaxHands,
  readPickup,
  readCameraType,
  pressViewToggle,
  readTotalScores,
  clickNextHand,
  isResultModalVisible,
  waitForPlayableHand,
  isGameCompleteModalVisible,
  waitForGameObject,
  Recorder,
  snap,
  type GameConfig,
} from './_playability';

const POLL_INTERVAL_MS = 700;
const VIEW_TOGGLE_EVERY_MS = 7000;
// A human game that makes zero real-play progress (no discard/claim/hand-end/
// dealer change) for this long is wedged — break fast and record why instead of
// burning the whole budget on a dead game.
const STALL_MS = 45_000;

function resolveBase(baseURL: string | undefined): string {
  return baseURL ?? process.env.E2E_BASE_URL ?? 'http://localhost:8080/autotable/';
}

// ── Error surveillance ──────────────────────────────────────────────────────
// The benign, expected REST probes for a not-yet-persisted game return 404 and
// are handled gracefully by the app; everything else is a real defect.
const BENIGN_404_RE = /\/api\/games\/[^/]+(\/settings)?$/;
const CSP_RE = /content security policy|refused to (load|execute|connect|apply)|violates the following/i;

interface ErrorReport {
  pageErrors: string[];
  cspViolations: string[];
  jsConsoleErrors: string[];
  badResponses: string[];
}

class ErrorWatch {
  private readonly pageErrors: string[] = [];
  private readonly cspViolations: string[] = [];
  private readonly jsConsoleErrors: string[] = [];
  private readonly badResponses: string[] = [];

  constructor(page: Page) {
    page.on('pageerror', (e) => this.pageErrors.push(e.message));
    page.on('console', (m: ConsoleMessage) => {
      if (m.type() !== 'error') return;
      const t = m.text();
      if (CSP_RE.test(t)) this.cspViolations.push(t);
      else if (/failed to load resource/i.test(t)) {
        /* correlated with badResponses below; ignore the generic text */
      } else this.jsConsoleErrors.push(t);
    });
    page.on('response', (r) => {
      if (r.status() < 400) return;
      const u = r.url();
      if (BENIGN_404_RE.test(u) && r.status() === 404) return; // expected fresh-game probe
      if (/\/api\/csp-report/.test(u)) return;
      this.badResponses.push(`${r.status()} ${r.request().resourceType()} ${u}`);
    });
  }

  report(): ErrorReport {
    return {
      pageErrors: this.pageErrors.slice(),
      cspViolations: this.cspViolations.slice(),
      jsConsoleErrors: this.jsConsoleErrors.slice(),
      badResponses: this.badResponses.slice(),
    };
  }

  clean(): boolean {
    return (
      this.pageErrors.length === 0 &&
      this.cspViolations.length === 0 &&
      this.jsConsoleErrors.length === 0 &&
      this.badResponses.length === 0
    );
  }
}

// ── Rich game-driver result ─────────────────────────────────────────────────
interface GameRun {
  bundleOk: boolean;
  bundleReason: string;
  connected: boolean;
  seat: number | null;
  dealt: boolean;
  playable: boolean;
  handEnds: number;
  handResults: Array<{ winner: number | null; type: string | null }>;
  dealersSeen: number[];
  discardsFired: number;
  claimsHandled: number;
  huByHuman: boolean;
  gcComplete: boolean;
  gcMaxHands: number | null;
  modalVisible: boolean;
  scores: Record<string, number> | null;
  zeroSum: number | null;
  maxHandsObserved: number | null;
  viewPerspective: boolean;
  viewFlat: boolean;
  errors: ErrorReport;
  timedOut: boolean;
  modalStorm: boolean;
  redismissCount: number;
  stalled: boolean;
  stallReason: string | null;
  realClaimsAttempted: number;
}

interface PlayOpts {
  humanPlays: boolean;
  toggleViews: boolean;
  budgetMs: number;
  label: string;
}

/**
 * Drives a real Changsha game to completion using ONLY real interactions.
 * humanPlays=true → seat 0 human takes seat, deals, and drives discards/claims
 * by real pointer/click. humanPlays=false → spectator; the all-bot table plays
 * itself (config proof). Read-only observation only; no backdoor.
 */
async function playRealGame(
  page: Page,
  request: APIRequestContext,
  resolvedBase: string,
  cfg: GameConfig,
  rec: Recorder,
  opts: PlayOpts,
): Promise<GameRun> {
  const errors = new ErrorWatch(page);

  const bundle = await checkServedBundleMatchesBuild(request, resolvedBase);
  rec.log('preflight.bundle-hash', bundle.ok, { ok: bundle.ok, entryShaMatches: bundle.entryShaMatches });

  await defangOverlays(page);
  await page.goto(buildGameUrl(resolvedBase, cfg), { waitUntil: 'domcontentloaded' });
  const hasGame = await waitForGameObject(page);
  rec.log('boot.game-object', hasGame, { url: page.url(), handCount: cfg.handCount });
  await dismissLobbyAndTour(page);
  const connected = await ensureConnected(page);
  rec.log('connect.ws', connected, { connected, humanPlays: opts.humanPlays });
  // Install the in-page hand-end observer BEFORE the first hand can end, so the
  // brief (post-#132-tombstone) EndHand window is latched from the real result
  // `update` event instead of sampled by a coarse poll that can slip past it.
  const obsInstalled = await installHandEndObserver(page);
  rec.log('observer.hand-end.install', obsInstalled, { installed: obsInstalled });
  await snap(page, `${opts.label}-01-connected.png`);

  let seat: number | null = null;
  let dealt = false;
  let playable = !opts.humanPlays; // spectator needs no playable hand
  if (opts.humanPlays) {
    seat = await takeSeatByClick(page, cfg.seat);
    rec.log('seat.take', seat !== null, { assigned: seat });
    dealt = await clickDeal(page);
    rec.log('deal.press', dealt, null);
    const p = await waitForPlayableHand(page, 45_000);
    playable = p.playable;
    rec.log('deal.playable-hand', p.playable, { myHandCount: p.myHandCount, lastPickup: p.lastPickup.raw });
    await snap(page, `${opts.label}-02-dealt.png`);
  }

  // ── Real-interaction game loop ──
  let handEnds = 0;
  let discardsFired = 0;
  let claimsHandled = 0;
  let huByHuman = false;
  let consecutiveMisses = 0;
  let redismissCount = 0;
  let modalStorm = false;
  let lastToggle = Date.now();
  const dealersSeen = new Set<number>();
  const handResults: Array<{ winner: number | null; type: string | null }> = [];
  let viewPerspective = false;
  let viewFlat = false;
  let lastProgressAt = Date.now();
  let prevProgress = -1;
  let realClaimsAttempted = 0;
  let lastRealClaimType: string | null = null;
  let stalled = false;
  let stallReason: string | null = null;

  const noteCamera = (c: 'perspective' | 'orthographic' | null): void => {
    if (c === 'perspective') viewPerspective = true;
    if (c === 'orthographic') viewFlat = true;
  };

  let gc = await readGameComplete(page);
  const deadline = Date.now() + opts.budgetMs;
  while (Date.now() < deadline) {
    // Hand ends are latched in-page by the observer on the real result `update`
    // event (installed at connect); read its running count here. The #132 fix
    // tombstones result['current'] promptly, so the EndHand window is too brief
    // for a coarse poll — the in-page latch catches every null->present flip.
    handEnds = (await readHandEndObserver(page)).ends.length;

    const match = await readMatch(page);
    if (match.dealer !== null) dealersSeen.add(match.dealer);

    // Progress watchdog (human games): discards, claims, hand ends and dealer
    // rotations all count as progress. If none advance for STALL_MS the human is
    // wedged — most often because a real Pung/Chow/Kong/Hu claim was silently
    // rejected by the server — so stop fast and surface WHY.
    const progress = discardsFired + claimsHandled + handEnds + dealersSeen.size;
    if (progress > prevProgress) {
      prevProgress = progress;
      lastProgressAt = Date.now();
    } else if (opts.humanPlays && Date.now() - lastProgressAt > STALL_MS) {
      stalled = true;
      stallReason =
        `real play made no progress for ${Math.round((Date.now() - lastProgressAt) / 1000)}s ` +
        `(discards=${discardsFired} claims=${claimsHandled} handEnds=${handEnds} ` +
        `dealers=${JSON.stringify([...dealersSeen])})` +
        (realClaimsAttempted > 0
          ? ` after a real "${lastRealClaimType}" claim — human Pung/Chow/Kong/Hu claims are ` +
            `rejected by the server: the bundle sends {action:'claim',type:T} but the backend ` +
            `reads 'action' as the claim type ("Unknown claim type claim"). PRODUCT DEFECT (handoff).`
          : '');
      await snap(page, `${opts.label}-stall.png`);
      rec.log('play.stalled', false, { stallReason, progress });
      break;
    }

    gc = await readGameComplete(page);
    if (gc.isComplete) break;

    // The per-hand #result-modal (static backdrop) can re-cover the table over
    // the next hand's pickup HUD; dismiss it via the real "Next Hand" button
    // whenever it is showing during a non-final hand so the human can pick up.
    if (opts.humanPlays && handEnds > 0 && handEnds < cfg.handCount && (await isResultModalVisible(page))) {
      const dismissed = await clickNextHand(page);
      redismissCount++;
      rec.log('result-modal.redismiss', dismissed, { handEnds, redismissCount });
      // A single Next Hand click should dismiss the modal for good. If it keeps
      // re-covering the table, result.current is never being tombstoned — the
      // multi-hand human flow is blocked by a product defect (see the test's
      // handoff message). Fail fast instead of fighting it for the full budget.
      if (redismissCount >= 15) {
        modalStorm = true;
        await snap(page, `${opts.label}-result-modal-storm.png`);
        rec.log('result-modal.storm', false, { redismissCount, handEnds });
        break;
      }
      await page.waitForTimeout(POLL_INTERVAL_MS);
      continue;
    }

    if (opts.toggleViews && Date.now() - lastToggle > VIEW_TOGGLE_EVERY_MS) {
      noteCamera(await readCameraType(page));
      noteCamera(await pressViewToggle(page));
      lastToggle = Date.now();
    }

    if (opts.humanPlays) {
      const claim = await readClaimWindow(page);
      if (claim.open) {
        const clicked = await claimByClick(page);
        if (clicked !== null) claimsHandled++;
        if (clicked !== null && clicked !== 'Pass') {
          realClaimsAttempted++;
          lastRealClaimType = clicked;
        }
        if (clicked === 'Hu') huByHuman = true;
        rec.log('claim.click', clicked !== null, { available: claim.available, clicked });
        await page.waitForTimeout(POLL_INTERVAL_MS);
        continue;
      }
      // Manual wall pickup for hands 2..N (hand 1 is auto-driven client-side).
      if (await readIsMyPickupTurn(page)) {
        const pu = await takePickup(page);
        rec.log('pickup.pointer', pu.ok, pu);
        if (pu.ok) {
          consecutiveMisses = 0;
        } else {
          consecutiveMisses++;
          if (consecutiveMisses >= 10) {
            await snap(page, `${opts.label}-pickup-blocked.png`);
            rec.log('pickup.blocked', false, { misses: consecutiveMisses, pickup: await readPickup(page) });
            break;
          }
        }
        await page.waitForTimeout(POLL_INTERVAL_MS);
        continue;
      }
      if (await hasExtraHandTile(page)) {
        const out = await discardByPointer(page);
        if (out.ok) {
          discardsFired++;
          consecutiveMisses = 0;
          rec.log('discard.pointer', true, { tileId: out.tileId, discardAfter: out.discardAfter });
        } else {
          consecutiveMisses++;
          if (consecutiveMisses >= 10) {
            await snap(page, `${opts.label}-discard-blocked.png`);
            rec.log('discard.blocked', false, { misses: consecutiveMisses, pickup: await readPickup(page) });
            break;
          }
        }
        await page.waitForTimeout(POLL_INTERVAL_MS);
        continue;
      }
    }
    await page.waitForTimeout(POLL_INTERVAL_MS);
  }

  // Final read of the in-page hand-end observer: it captured every EndHand
  // transition (including the last hand, whose result arrives just before
  // gameComplete tombstones it) directly from the result `update` event.
  const obsFinal = await readHandEndObserver(page);
  handEnds = obsFinal.ends.length;
  for (const e of obsFinal.ends) {
    handResults.push({ winner: e.winner, type: e.type });
    if (e.type === 'Hu' && seat !== null && e.winner === seat) huByHuman = true;
  }
  rec.log('hand.end.observed', handEnds >= cfg.handCount, {
    handEnds,
    ends: obsFinal.ends,
    clears: obsFinal.clears,
  });
  noteCamera(await readCameraType(page));

  gc = await readGameComplete(page);
  const modalVisible = await isGameCompleteModalVisible(page);
  const scores = await readTotalScores(page);
  const zeroSum = scores ? Object.values(scores).reduce((a, b) => a + b, 0) : null;
  const maxHandsObs = await readMaxHands(page);
  await snap(page, `${opts.label}-03-final.png`);

  return {
    bundleOk: bundle.ok,
    bundleReason: bundle.reason,
    connected,
    seat,
    dealt,
    playable: playable || discardsFired > 0,
    handEnds,
    handResults,
    dealersSeen: [...dealersSeen].sort((a, b) => a - b),
    discardsFired,
    claimsHandled,
    huByHuman,
    gcComplete: gc.isComplete,
    gcMaxHands: maxHandsObs.value,
    modalVisible,
    scores,
    zeroSum,
    maxHandsObserved: maxHandsObs.value,
    viewPerspective,
    viewFlat,
    errors: errors.report(),
    timedOut: !gc.isComplete && Date.now() >= deadline,
    modalStorm,
    redismissCount,
    stalled,
    stallReason,
    realClaimsAttempted,
  };
}

function assertCleanErrors(run: GameRun): void {
  expect(run.errors.pageErrors, `Uncaught page errors: ${JSON.stringify(run.errors.pageErrors)}`).toEqual([]);
  expect(run.errors.cspViolations, `CSP violations: ${JSON.stringify(run.errors.cspViolations)}`).toEqual([]);
  expect(run.errors.badResponses, `Unexpected HTTP failures: ${JSON.stringify(run.errors.badResponses)}`).toEqual([]);
  expect(run.errors.jsConsoleErrors, `JS console errors: ${JSON.stringify(run.errors.jsConsoleErrors)}`).toEqual([]);
}

test.describe('@playability-gate P0 real-UI playability (autotable integration acceptance)', () => {
  // ── HEADLINE: full 4-hand human-vs-bots playthrough → real GameComplete ──
  // Run repeatedly for reliability (CI: `--repeat-each=3`); the seed varies per
  // repeat so each pass is an independent real game.
  test('full 4-hand human-vs-bots playthrough → authoritative GameComplete + scoring modal', async ({
    page,
    request,
    baseURL,
  }, testInfo) => {
    test.setTimeout(8 * 60_000);
    const rec = new Recorder();
    const seed = 4100 + testInfo.repeatEachIndex;
    const cfg = makeConfig({
      handCount: 4,
      seed,
      gameId: `playability-4hand-${process.env.PLAYABILITY_RUN_ID ?? 'local'}-r${testInfo.repeatEachIndex}`,
    });

    const run = await playRealGame(page, request, resolveBase(baseURL), cfg, rec, {
      humanPlays: true,
      toggleViews: true,
      budgetMs: 6 * 60_000,
      label: `4hand-r${testInfo.repeatEachIndex}`,
    });
    rec.log('gate.summary', run.gcComplete && run.modalVisible, run);
    rec.write(`playability-4hand-r${testInfo.repeatEachIndex}-findings.json`, { config: cfg, run });

    // Preconditions.
    expect(run.bundleOk, `served bundle != fresh build: ${run.bundleReason}`).toBe(true);
    expect(run.connected && run.seat !== null, `connect/seat failed (connected=${run.connected} seat=${run.seat})`).toBe(true);
    expect(run.dealt, 'real #deal press failed').toBe(true);
    expect(run.playable, 'manual deal ceremony never produced a playable hand (dealer 14th tile)').toBe(true);

    // Real gameplay actually happened through the canvas.
    expect(run.discardsFired, 'no real pointer discard fired through the canvas').toBeGreaterThan(0);

    // Both views exercised live.
    expect(run.viewPerspective, 'perspective view never observed during the live game').toBe(true);
    expect(run.viewFlat, 'flat (orthographic) view never observed during the live game').toBe(true);

    // Regression guard: the per-hand #result-modal must NOT re-storm over the
    // table between hands (the #132 tombstone fix, now merged, keeps this false).
    expect(
      run.modalStorm,
      `per-hand #result-modal re-stormed ${run.redismissCount}+ times between hands ` +
        `(result.current tombstone regressed — #132). handEnds=${run.handEnds}/${cfg.handCount}.`,
    ).toBe(false);

    // ── CURRENT PRODUCT-DEFECT BLOCKER (handoff, not test-owned) → #134 ───
    // A real human Pung/Chow/Kong/Hu claim wedges the game: the shipped bundle
    // sends the claim as `claim[seat] = { action:'claim', type:'Hu' }`
    // (game-ui.ts sendClaim), but the backend's TryHandleClaimActionAsync reads
    // the `action` field AS the claim type and never reads `type`, so
    // ParseClaimType("claim") throws "Unknown claim type claim" and the claim is
    // dropped — leaving the human's turn stuck. Only Pass works (its 'pass'
    // matches 'Pass' case-insensitively). HANDOFF → Bishop / WP-A (runtime), see
    // issue #134: the backend claim handler must read `type` when action=='claim'.
    // Pass-only human hands (handCount=1) and all-bot games (8/16) are unaffected.
    expect(run.stalled, run.stallReason ?? 'real play stalled before completion').toBe(false);

    // Four hands actually played, to authoritative completion.
    expect(run.gcMaxHands, `server MaxHands != 4 (observed ${run.gcMaxHands})`).toBe(4);
    expect(run.handEnds, `expected 4 hand results, saw ${run.handEnds} (${JSON.stringify(run.handResults)})`).toBeGreaterThanOrEqual(4);
    expect(run.dealersSeen.length, `dealer never progressed across hands (dealers=${JSON.stringify(run.dealersSeen)})`).toBeGreaterThanOrEqual(2);

    // Authoritative GameComplete + real scoring modal + zero-sum totals.
    expect(run.gcComplete, 'server-authoritative gameComplete.isComplete never set').toBe(true);
    expect(run.modalVisible, '#game-complete-modal not visible after real completion').toBe(true);
    expect(run.scores, 'gameComplete carried no per-seat totals').not.toBeNull();
    expect(run.zeroSum, `scoring totals are not zero-sum (Σ=${run.zeroSum}, scores=${JSON.stringify(run.scores)})`).toBe(0);

    // No unhandled console / page / CSP errors during the whole game.
    assertCleanErrors(run);
  });

  // ── BOUNDED non-default handCount = 1 (human-vs-bots, anti-evasion) ──
  test('bounded handCount=1 human-vs-bots completes in exactly one hand (server honors non-default cap)', async ({
    page,
    request,
    baseURL,
  }, testInfo) => {
    test.setTimeout(4 * 60_000);
    const rec = new Recorder();
    const cfg = makeConfig({
      handCount: 1,
      seed: 12345 + testInfo.repeatEachIndex,
      gameId: `playability-hc1-${process.env.PLAYABILITY_RUN_ID ?? 'local'}-r${testInfo.repeatEachIndex}`,
    });
    const run = await playRealGame(page, request, resolveBase(baseURL), cfg, rec, {
      humanPlays: true,
      toggleViews: false,
      budgetMs: 3 * 60_000,
      label: 'hc1',
    });
    rec.log('gate.summary', run.gcComplete && run.gcMaxHands === 1, run);
    rec.write('playability-hc1-findings.json', { config: cfg, run });

    expect(run.bundleOk, run.bundleReason).toBe(true);
    expect(run.connected && run.seat !== null, `connect/seat failed`).toBe(true);
    expect(run.playable, 'no playable hand').toBe(true);
    expect(run.discardsFired, 'no real discard fired').toBeGreaterThan(0);
    expect(run.gcComplete, 'game did not complete').toBe(true);
    expect(run.modalVisible, 'modal not visible').toBe(true);
    // Anti-evasion: exactly ONE hand, server MaxHands === 1. A server that
    // ignored handCount and played the default 4 fails both of these.
    expect(run.gcMaxHands, `server MaxHands != 1 (observed ${run.gcMaxHands}) — handCount not honored`).toBe(1);
    expect(run.handEnds, `expected exactly 1 hand, saw ${run.handEnds}`).toBe(1);
    assertCleanErrors(run);
  });

  // ── BOUNDED non-default handCount = 8 / 16 (all-bot completion) ──
  // Proving the server honors larger non-default caps. All-bot spectator games
  // complete autonomously; we read the authoritative gameComplete.maxHands.
  for (const hc of [8, 16] as const) {
    test(`server honors non-default handCount=${hc} to authoritative completion (all-bot)`, async ({
      page,
      request,
      baseURL,
    }) => {
      test.setTimeout((hc * 45 + 120) * 1000);
      const rec = new Recorder();
      const cfg = makeConfig({
        handCount: hc,
        seat: -1, // spectator
        botCount: 4, // all four seats are bots
        dealMode: 'auto',
        botDifficulty: 'Medium',
        seed: 900 + hc,
        gameId: `playability-hc${hc}-${process.env.PLAYABILITY_RUN_ID ?? 'local'}`,
      });
      const run = await playRealGame(page, request, resolveBase(baseURL), cfg, rec, {
        humanPlays: false,
        toggleViews: false,
        budgetMs: (hc * 40 + 60) * 1000,
        label: `hc${hc}`,
      });
      rec.log('gate.summary', run.gcComplete && run.gcMaxHands === hc, run);
      rec.write(`playability-hc${hc}-findings.json`, { config: cfg, run });

      expect(run.bundleOk, run.bundleReason).toBe(true);
      expect(run.connected, 'spectator did not connect').toBe(true);
      expect(run.gcComplete, `handCount=${hc} game did not complete within budget (timedOut=${run.timedOut})`).toBe(true);
      expect(run.gcMaxHands, `server MaxHands != ${hc} (observed ${run.gcMaxHands}) — handCount not honored`).toBe(hc);
      expect(run.dealersSeen.length, `dealer never rotated across ${hc} hands`).toBeGreaterThanOrEqual(2);
      assertCleanErrors(run);
    });
  }
});
