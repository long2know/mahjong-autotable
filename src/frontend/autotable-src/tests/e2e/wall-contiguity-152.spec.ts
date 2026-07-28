// =============================================================================
//  #152 — authoritative wall-order / contiguity structural gate
// =============================================================================
//
//  Proves the remaining live wall is CONSUMED CONTIGUOUSLY from the dice break
//  point around the perimeter, using real deal controls + real WS state
//  (world.things slotNames) — no client.update / injection / synthetic
//  dispatch / direct pickup.
//
//  CANONICAL MATH (docs/rules/changsha-spec.md §2.3–2.5): 108 tiles = 54 stacks;
//  deal = 3×13 + dealer 14 = 53; exactly 55 remain. With contiguous depletion
//  from a single break point, the 53 drawn tiles form ONE arc, so AT MOST TWO
//  of the four seat-walls can be partially consumed — the other two must be
//  fully FULL or fully EMPTY. Before the fix the backend translator
//  (AutotableSlotMap.EnumerateWallSlotsInOrder, column-major-across-seats)
//  spread the remainder evenly, leaving ALL FOUR walls half-consumed. The fix
//  (AutotableSlotMap.WallOrdinalToSlot + WallBreakOrdinal, driven by the
//  derived front-draw anchor `108 - Wall.Count - WallBackDrawn`) places each
//  remaining tile at a STABLE physical slot so the wall depletes as one arc.

import { test, expect, type Page } from '@playwright/test';
import {
  buildGameUrl, makeConfig, dismissLobbyAndTour, ensureConnected,
  takeSeatByClick, clickDeal, waitForPlayableHand,
} from './_playability';

// Physical wall capacity per seat (tiles): seats 0,1 → 14 stacks × 2 = 28;
// seats 2,3 → 13 stacks × 2 = 26. Mirrors AutotableSlotMap.WallTileCapacity.
const CAPACITY: Record<number, number> = { 0: 28, 1: 28, 2: 26, 3: 26 };

interface WallShape {
  gameType: string | null;
  thingCount: number;
  total: number;
  perSeat: Record<number, { count: number; cols: number[] }>;
}

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
    return {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      gameType: (window as any).game?.world?.gameType ?? null,
      thingCount: w?.things?.size ?? 0,
      total, perSeat,
    };
  });
}

test.describe('#152 wall depletes contiguously from the break point', () => {
  for (const dealMode of ['auto', 'manual'] as const) {
    test(`contiguous remaining wall after ${dealMode} deal`, async ({ page }, testInfo) => {
      testInfo.setTimeout(120_000);
      await page.setViewportSize({ width: 1600, height: 900 });
      const cfg = makeConfig({ dealMode, gameId: `wall152-${dealMode}-${Date.now()}`, botCount: 3, handCount: 4 });
      await page.goto(buildGameUrl(testInfo.project.use.baseURL as string, cfg), { waitUntil: 'domcontentloaded' });
      await page.waitForTimeout(1200);
      await dismissLobbyAndTour(page);
      await ensureConnected(page);
      await takeSeatByClick(page, 0);
      await clickDeal(page);
      await waitForPlayableHand(page, 60_000);
      await page.waitForTimeout(1500);

      const shape = await readWallShape(page);

      // 1) Canonical count — exactly 55 tiles remain after the deal.
      expect(shape.total, `remaining wall tiles (${dealMode})`).toBe(55);

      // 2) Contiguity signature — with a single contiguous drawn arc, at most
      //    two walls can be partial; the other two must be full or empty.
      const fullOrEmpty = [0, 1, 2, 3].filter((seat) => {
        const c = shape.perSeat[seat]?.count ?? 0;
        return c === 0 || c === CAPACITY[seat];
      }).length;
      expect(
        fullOrEmpty,
        `seats fully-full or fully-empty (${dealMode}) — got ${JSON.stringify(shape.perSeat)}; ` +
        `contiguous depletion requires >=2, column-major spread yields 0`,
      ).toBeGreaterThanOrEqual(2);

      // 3) No internal gaps within any seat's occupied columns.
      for (const seat of [0, 1, 2, 3]) {
        const cols = shape.perSeat[seat]?.cols ?? [];
        for (let i = 1; i < cols.length; i++) {
          expect(cols[i] - cols[i - 1], `seat ${seat} wall column gap (${dealMode})`).toBe(1);
        }
      }
    });
  }
});
