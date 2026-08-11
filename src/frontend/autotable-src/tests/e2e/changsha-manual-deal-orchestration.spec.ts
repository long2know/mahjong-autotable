// D2 (OWNED by hudson-1 — Vasquez ruling 2026-08-07 11:53). A REAL, SEPARATE
// backend deal-orchestration bug (NOT the R-1 pickup-interaction defect — tagged
// separately so it does not muddy G17). Root (Vasquez, grounded @200cad4):
// ChangshaGameRuntime.StartGameAsync manual branch returns WITHOUT scheduling the
// BOT dealer's dice roll; the bot-dealer roll only fires via ScheduleBotIfNeededAsync
// on hands 2+. So a manual HAND 1 with a BOT dealer parks in RollingDice forever
// (nobody rolls: the bot isn't scheduled and a non-dealer human's blind rollDice is
// server-rejected) ⇒ no pickup cursor ever targets the human ⇒ hand stays 0.
// Acceptance: a NON-DEALER human in a manual deal receives their pickup windows and
// reaches 13 tiles. RED@200cad4 (stalls). GREEN once the backend schedules the
// bot-dealer roll for hand-1 manual too.
import { test, expect } from '@playwright/test';
import { buildGameUrl, makeConfig, dismissLobbyAndTour, ensureConnected, takeSeatByClick, clickDeal } from './_playability';
import { recordEvidence, shot } from './_uat_red';

test.describe('D2 manual deal orchestration — non-dealer human must not stall (backend)', () => {
  test('non-dealer human (seat 1) in a manual deal reaches 13 tiles (no RollingDice stall)', async ({ page }, testInfo) => {
    testInfo.setTimeout(120_000);
    await page.setViewportSize({ width: 1600, height: 900 });
    const base = testInfo.project.use.baseURL as string;
    const cfg = makeConfig({ gameId: `d2-nondealer-${Date.now()}`, seat: 1, dealMode: 'manual', botCount: 3, botDifficulty: 'Medium', handCount: 4 });
    await page.goto(buildGameUrl(base, cfg), { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(1200); await dismissLobbyAndTour(page); await ensureConnected(page);
    // Take a NON-dealer seat so the DEALER is a bot (the stall condition).
    await takeSeatByClick(page, 1); await page.waitForTimeout(800);
    await clickDeal(page).catch(() => {});

    // poll up to 45s for the non-dealer human to reach a full 13-tile hand
    let hand = 0; let phase: string | null = null; let dealer: number | null = null; const t0 = Date.now();
    while (Date.now() - t0 < 45000) {
      const s = await page.evaluate(() => {
        /* eslint-disable @typescript-eslint/no-explicit-any */
        const g = (window as any).game; const w = g?.world;
        let h = 0; if (w?.things) for (const t of w.things.values()) if (/^hand\.\d+@0$/.test(String(t?.slot?.name ?? ''))) h++;
        const m = g?.client?.match?.get ? g.client.match.get(0) : null;
        const t = g?.client?.turn;
        return { hand: h, phase: t?.phase ?? null, dealer: m?.dealer ?? null };
        /* eslint-enable @typescript-eslint/no-explicit-any */
      });
      hand = s.hand; phase = s.phase; dealer = s.dealer;
      if (hand >= 13) break;
      await page.waitForTimeout(1000);
    }
    await shot(page, 'd2-nondealer-stall.png');
    recordEvidence('d2-nondealer-deal.json', { seat: 1, dealer, phase, handReached: hand,
      note: 'RED@200cad4: hand-1 manual with a BOT dealer parks in RollingDice (bot-dealer roll unscheduled) ⇒ the non-dealer human never gets pickup windows ⇒ hand stays 0. GREEN when the backend schedules the bot-dealer roll for hand-1 manual.' });
    // ACCEPTANCE: the non-dealer human must be dealt in (reach 13 tiles), not stall.
    expect(hand, `D2: a non-dealer human in a manual deal must reach 13 tiles; got ${hand} (dealer=${dealer}, phase=${phase}) — RED@200cad4 = RollingDice stall`).toBeGreaterThanOrEqual(13);
  });
});
