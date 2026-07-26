// #119 (Hicks) — Changsha manual-deal ceremony regression.
//
// Blocker (found by Hudson's WP-F real-click gate, PR #128): the client's
// `World.driveManualDealChain()` drove only 4 pickups — PickupRound1..3 +
// SingleTilePickup — on the false assumption the runtime "collapses"
// SingleTilePickup and DealerExtra into one affordance.  It does NOT: they
// are distinct `ChangshaPhase` states (both gated by IsPickupPhase; see
// ChangshaStateMachine.cs / ChangshaDealingCeremony.cs).  So the runtime
// pushed the DealerExtra affordance AFTER the 4th take and the client never
// consumed it, stranding the dealer at 13 tiles with the real UI hung before
// the first discard.
//
// Fix: the chain now takes every ceremony pickup the runtime presents to our
// seat and stops the instant the terminal DealerExtra tile is claimed — the
// dealer reaches 14 tiles and the game arms AwaitingDiscard.
//
// This regression proves, through the REAL UI (connect → take-seat → click
// #deal → the production pickup chain drives the real `pickup.set('take')`
// protocol emit — NO WS `client.update([...])` backdoor):
//   1. all FIVE ceremony pickup phases fire (PickupRound1..3, SingleTilePickup,
//      DealerExtra) — captured event-driven off the pickup collection so the
//      transient DealerExtra affordance is never missed;
//   2. the dealer's hand reaches 14 tiles;
//   3. the game is discard-ready (extra tile in hand, no pending pickup);
//   4. a discard is PLAYABLE — the production discard entry point
//      `World.emitDiscard` (the exact call the click-to-discard UI makes at
//      world.ts:onDragStart) is accepted by the server, dropping the hand to
//      13 and advancing play.
//
// Runs against the backend serving the freshly-built bundle (C-8 harness).

import { test, expect, type Page } from '@playwright/test';

const CEREMONY_PHASES = [
  'PickupRound1',
  'PickupRound2',
  'PickupRound3',
  'SingleTilePickup',
  'DealerExtra',
];

// Defang the full-page overlays that intercept pointer events (tour /
// magic-link / signin backdrop) — same real accessibility+playability bug
// the playtest specs work around.  Injected before any document script runs.
async function defangOverlays(page: Page): Promise<void> {
  await page.addInitScript(() => {
    const inject = (): void => {
      if (document.getElementById('hicks-de-defang')) return;
      const style = document.createElement('style');
      style.id = 'hicks-de-defang';
      style.textContent = `
        #tour-overlay,#magic-link-landing,#magic-link-overlay,#signin-modal-backdrop,
        [data-testid="tour-overlay"],[data-testid="signin-modal-backdrop"]
          { display:none !important; pointer-events:none !important; visibility:hidden !important; }
        [aria-hidden="true"] { pointer-events:none !important; }
      `;
      document.head.appendChild(style);
    };
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', inject);
    else inject();
  });
}

// Count the local seat's concealed hand tiles from the server-authoritative
// `things` collection (keyed by tile id; slot name lives on the value).
function seatHandCountInPage(): number {
  const cli = (window as any).game?.client;
  if (!cli) return -1;
  const seat = cli.seat;
  if (seat === null || seat === undefined) return -1;
  const suffix = '@' + seat;
  let n = 0;
  for (const [, v] of cli.things.entries()) {
    const slot = v?.slotName ?? v?.SlotName;
    if (typeof slot === 'string' && slot.startsWith('hand.') && slot.endsWith(suffix)) n++;
  }
  return n;
}

test.describe('#119 Changsha manual-deal ceremony — DealerExtra regression', () => {
  test.beforeEach(async ({}, testInfo) => {
    test.skip(
      testInfo.project.name !== 'chromium',
      'WebGL manual-deal ceremony validated on chromium only.',
    );
  });

  test('dealer walks all 5 pickup phases to 14 tiles and can discard', async ({ page }) => {
    test.setTimeout(180_000);
    await defangOverlays(page);

    const consoleErrors: string[] = [];
    page.on('console', (m) => { if (m.type() === 'error') consoleErrors.push(m.text()); });
    page.on('pageerror', (e) => consoleErrors.push(`PAGEERROR: ${e.message}`));

    // 1) Real-UI boot: manual-deal Changsha, human at seat 0 + 3 bots, fresh id.
    const gameId = `hicks-de-${Date.now()}`;
    await page.goto(
      `./?variant=changsha&dealMode=manual&botCount=3&botDifficulty=Medium&handCount=4&gameId=${gameId}`,
      { waitUntil: 'domcontentloaded' },
    );
    await page.waitForTimeout(2000);
    for (const sel of ['#lobby-close', '#tour-skip', '#onboarding-skip']) {
      const el = page.locator(sel);
      if (await el.isVisible().catch(() => false)) {
        await el.click({ force: true, timeout: 3000 }).catch(() => undefined);
      }
    }

    // 2) Connect (real click).
    const connect = page.locator('#connect');
    if (await connect.first().isVisible().catch(() => false)) {
      await connect.first().click({ timeout: 8000 }).catch(() => undefined);
    }
    await page.waitForTimeout(2000);

    // 3) Take the first open seat (real click).
    const seats = page.locator('.take-seat');
    const seatCount = await seats.count();
    for (let i = 0; i < seatCount; i++) {
      if (await seats.nth(i).isVisible().catch(() => false)) {
        await seats.nth(i).click({ timeout: 8000 }).catch(() => undefined);
        break;
      }
    }

    // 4) Renderer up + connected + seated.
    await page
      .locator('[data-testid="three-renderer-ready"]')
      .waitFor({ state: 'attached', timeout: 90_000 });
    await page.waitForFunction(
      () => {
        const g: any = (window as any).game;
        return !!(g && g.world && g.client && g.client.connected?.() && g.world.seat !== null);
      },
      undefined,
      { timeout: 30_000 },
    );

    // 5) Capture EVERY pickup phase the runtime pushes — event-driven so the
    //    transient DealerExtra affordance (consumed within ~120ms by the fix)
    //    is never lost to a poll gap.
    await page.evaluate(() => {
      (window as any).__dealPhases = [];
      const cli = (window as any).game.client;
      cli.pickup.on('update', () => {
        const p = cli.pickup.get('current');
        const phases: string[] = (window as any).__dealPhases;
        if (p && p.phase && !phases.includes(p.phase)) phases.push(p.phase);
      });
    });

    // 6) Deal (real click).  The single-click #deal handler fires
    //    world.deal('HANDS') → driveManualDealChain (the fix under test).
    const deal = page.locator('#deal');
    expect(await deal.first().isVisible().catch(() => false), '#deal must be visible after seating').toBe(true);
    await deal.first().click({ timeout: 8000 });

    // 7) Wait for the ceremony to bring the dealer to 14 tiles.
    await page.waitForFunction(
      () => {
        const cli: any = (window as any).game?.client;
        if (!cli) return false;
        const seat = cli.seat;
        if (seat === null || seat === undefined) return false;
        const suffix = '@' + seat;
        let n = 0;
        for (const [, v] of cli.things.entries()) {
          const slot = v?.slotName ?? v?.SlotName;
          if (typeof slot === 'string' && slot.startsWith('hand.') && slot.endsWith(suffix)) n++;
        }
        return n >= 14;
      },
      undefined,
      { timeout: 90_000 },
    ).catch(() => undefined);

    const capturedPhases: string[] = await page.evaluate(() => (window as any).__dealPhases ?? []);
    const dealerHand: number = await page.evaluate(seatHandCountInPage);

    // ── Assertions: all five ceremony phases, dealer at 14 ──────────────
    for (const phase of CEREMONY_PHASES) {
      expect(
        capturedPhases,
        `ceremony phase "${phase}" must fire (saw: ${JSON.stringify(capturedPhases)})`,
      ).toContain(phase);
    }
    expect(
      dealerHand,
      `dealer must hold 14 tiles after DealerExtra (was ${dealerHand}) — the 13-tile stall is the #119 bug`,
    ).toBe(14);

    // 8) Discard-ready: extra tile in hand, no pending pickup.
    const ready = await page.evaluate(() => {
      const w: any = (window as any).game.world;
      return { hasExtra: w.hasExtraHandTile?.() ?? null, isPickupTurn: w.isMyPickupTurn?.() ?? null };
    });
    expect(ready.hasExtra, 'dealer must hold the extra (14th) tile → discard armed').toBe(true);
    expect(ready.isPickupTurn, 'ceremony pickups must be exhausted (no pending pickup)').toBe(false);

    // 9) Playable discard through the production discard entry point (the
    //    exact call world.ts:onDragStart's click-to-discard makes) — assert
    //    the server accepts it and the hand drops to 13 as play advances.
    const discard = await page.evaluate(() => {
      const g: any = (window as any).game;
      const cli = g.client;
      const seat = cli.seat;
      const suffix = '@' + seat;
      let tileId: number | null = null;
      for (const [k, v] of cli.things.entries()) {
        const slot = v?.slotName ?? v?.SlotName;
        if (typeof slot === 'string' && slot.startsWith('hand.') && slot.endsWith(suffix)) { tileId = k; break; }
      }
      if (tileId === null) return { ok: false, tileId: null };
      return { ok: g.world.emitDiscard(tileId) === true, tileId };
    });
    expect(discard.ok, 'production discard emit must be accepted for the dealer').toBe(true);

    await page.waitForFunction(
      () => {
        const cli: any = (window as any).game?.client;
        if (!cli) return false;
        const seat = cli.seat;
        if (seat === null || seat === undefined) return false;
        const suffix = '@' + seat;
        let n = 0;
        for (const [, v] of cli.things.entries()) {
          const slot = v?.slotName ?? v?.SlotName;
          if (typeof slot === 'string' && slot.startsWith('hand.') && slot.endsWith(suffix)) n++;
        }
        return n < 14;
      },
      undefined,
      { timeout: 15_000 },
    );
    const postHand: number = await page.evaluate(seatHandCountInPage);
    const totalDiscards: number = await page.evaluate(() => {
      const cli = (window as any).game.client;
      let d = 0;
      for (const [, v] of cli.things.entries()) {
        const slot = v?.slotName ?? v?.SlotName;
        if (typeof slot === 'string' && slot.startsWith('discard')) d++;
      }
      return d;
    });
    expect(postHand, 'a playable discard must drop the dealer hand below 14').toBeLessThan(14);
    expect(totalDiscards, 'the discard must register on the table').toBeGreaterThan(0);

    // eslint-disable-next-line no-console
    console.log(
      `[#119 ceremony] phases=${JSON.stringify(capturedPhases)} dealerHand=${dealerHand} ` +
        `postDiscardHand=${postHand} discards=${totalDiscards}`,
    );
  });
});
