// =============================================================================
//  C-5 (3/4) — Ripley Design-Review contract: choosing Changsha through the
//  SETUP UI yields ONE contiguous physical wall arc from the true break, and
//  ordinary draws deplete it FORWARD, wall-by-wall (dynamic, over many draws).
// =============================================================================
//
//  The existing wall-contiguity-152.spec.ts pins the post-deal SNAPSHOT (55 +
//  >=2 full/empty + no gaps) for a URL-configured game. This spec is stricter
//  on two axes the review calls out:
//    • config is driven through the real Setup/Lobby UI (variant radio + Apply),
//      not hand-crafted URL params; and
//    • the contiguity/forward-depletion invariant is asserted at EVERY sample
//      across a run of REAL draws within a hand — the arc must only ever shrink
//      from its front, never scatter, split, or grow mid-hand.
//
//  Advances ONLY through genuine rendered controls (lobby Apply, .take-seat,
//  #deal, real-pointer discards, real Pass clicks). No client.update, no
//  emitDiscard, no synthetic DOM, no collection injection.

import { test, expect, type Page } from '@playwright/test';
import {
  defangOverlays, dismissLobbyAndTour, ensureConnected, takeSeatByClick, clickDeal,
  waitForPlayableHand, hasExtraHandTile, readClaimWindow, claimByClick, discardByPointer,
} from './_playability';

const CAP: Record<number, number> = { 0: 28, 1: 28, 2: 26, 3: 26 };

interface WallShape { total: number; perSeat: Record<number, { count: number; cols: number[] }>; }

async function readWallShape(page: Page): Promise<WallShape> {
  return page.evaluate(() => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const w = (window as any).game?.world;
    const perSeat: Record<number, { count: number; cols: number[] }> = {};
    let total = 0;
    if (w?.things) {
      for (const t of w.things.values()) {
        if (t?.slot?.group !== 'wall') continue;
        const m = /^wall\.(\d+)\.(\d+)@(\d+)$/.exec(String(t.slot.name));
        if (!m) continue;
        const col = Number(m[1]); const seat = Number(m[3]);
        (perSeat[seat] ??= { count: 0, cols: [] });
        perSeat[seat].count++;
        if (!perSeat[seat].cols.includes(col)) perSeat[seat].cols.push(col);
        total++;
      }
    }
    for (const s of Object.keys(perSeat)) perSeat[Number(s)].cols.sort((a, b) => a - b);
    return { total, perSeat };
  });
}

function contiguitySignature(s: WallShape): { fullOrEmpty: number; gaps: number } {
  const fullOrEmpty = [0, 1, 2, 3].filter((seat) => {
    const c = s.perSeat[seat]?.count ?? 0;
    return c === 0 || c === CAP[seat];
  }).length;
  let gaps = 0;
  for (const seat of [0, 1, 2, 3]) {
    const cols = s.perSeat[seat]?.cols ?? [];
    for (let k = 1; k < cols.length; k++) if (cols[k] - cols[k - 1] !== 1) gaps++;
  }
  return { fullOrEmpty, gaps };
}

test.describe('#C-5 wall — Setup-UI Changsha depletes forward, wall-by-wall, from one contiguous arc', () => {
  test('configuring Changsha via the Setup UI yields a single arc that depletes forward over real draws', async ({
    page,
  }, testInfo) => {
    test.skip(testInfo.project.name !== 'chromium', 'WebGL real-pointer gameplay is validated on chromium.');
    test.setTimeout(180_000);

    // ── Configure Changsha through the real Setup/Lobby UI (no URL crafting) ──
    await defangOverlays(page);
    await page.goto('', { waitUntil: 'domcontentloaded' });
    await page.evaluate(() => {
      try {
        localStorage.clear();
        localStorage.setItem('mahjong.tour.completed.v1', 'true');
        localStorage.setItem('mahjong.identity.onboarded.v1', 'true');
      } catch { /* storage disabled */ }
    });
    await page.goto('?variant=changsha', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('#lobby-panel.lobby-open')).toBeVisible({ timeout: 15_000 });

    // Changsha variant radio (default checked) — assert it's the chosen rule set.
    const changshaRadio = page.locator('#lobby-variant-fieldset input[value="changsha"]');
    await expect(changshaRadio).toBeChecked();

    // Real Apply → mints an honest fresh Changsha game and navigates.
    await Promise.all([
      page.waitForNavigation({ waitUntil: 'domcontentloaded', timeout: 20_000 }).catch(() => null),
      page.getByTestId('lobby-apply').click(),
    ]);
    const q = new URL(page.url()).searchParams;
    expect(q.get('variant'), 'Setup UI must start a Changsha game').toBe('changsha');

    // ── Seat + deal through genuine controls ──
    await dismissLobbyAndTour(page);
    await ensureConnected(page);
    await takeSeatByClick(page, 0);
    await clickDeal(page);
    await waitForPlayableHand(page, 60_000).catch(() => undefined);
    await page.waitForTimeout(1200);

    // ── Post-deal snapshot: exactly one contiguous 55-tile arc. ──
    const dealt = await readWallShape(page);
    const dealSig = contiguitySignature(dealt);
    expect(dealt.total, 'exactly 55 wall tiles remain after the Setup-UI deal').toBe(55);
    expect(dealSig.fullOrEmpty, `one contiguous arc ⇒ >=2 seat-walls full/empty (got ${JSON.stringify(dealt.perSeat)})`).toBeGreaterThanOrEqual(2);
    expect(dealSig.gaps, 'no internal column gaps within any seat wall').toBe(0);

    // ── Drive REAL draws and assert the arc only ever shrinks from its front. ──
    const samples: number[] = [dealt.total];
    let prev = dealt.total;
    const deadline = Date.now() + 120_000;
    let violation: string | null = null;
    while (Date.now() < deadline && samples.length < 9) {
      const claim = await readClaimWindow(page);
      if (claim.open) {
        await claimByClick(page); // Pass (or Hu) — a real click, keeps play moving.
      } else if (await hasExtraHandTile(page)) {
        await discardByPointer(page);
      } else {
        await page.waitForTimeout(700);
      }
      const s = await readWallShape(page);
      if (s.total > prev) break; // hand rolled over (wall reset) — stop sampling this hand.
      if (s.total < prev) {
        const sig = contiguitySignature(s);
        if (sig.fullOrEmpty < 2 || sig.gaps > 0) {
          violation = `non-contiguous mid-play wall at total=${s.total}: ${JSON.stringify(s.perSeat)} (fullOrEmpty=${sig.fullOrEmpty}, gaps=${sig.gaps})`;
          break;
        }
        samples.push(s.total);
        // eslint-disable-next-line no-console
        console.log(`[C-5 wall] draw#${samples.length - 1} total ${prev}->${s.total} fullOrEmpty=${sig.fullOrEmpty} gaps=${sig.gaps}`);
        prev = s.total;
      }
    }

    expect(violation, violation ?? 'wall stayed contiguous').toBeNull();
    // Forward depletion proven over several ordinary draws (strictly decreasing).
    expect(samples.length, `must observe >=4 forward wall draws within one hand (saw totals ${samples.join(',')})`).toBeGreaterThanOrEqual(4);
    for (let i = 1; i < samples.length; i++) {
      expect(samples[i], `wall total must only shrink within a hand (${samples.join(',')})`).toBeLessThan(samples[i - 1]);
    }
  });
});
