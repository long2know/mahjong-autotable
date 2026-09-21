// Ripley fast-tests lane — G10: no active-games leak. Opening a fresh Changsha
// game raises the server's active-game count; once the only client fully
// disconnects/closes, that game must be released so the count returns toward its
// prior level. A game that stays "active" forever after every client is gone is
// a resource leak.
//
// RED@200cad4: the game the client opened is NOT released after the client
// closes (the active-games count does not drop back), so repeated fresh games
// accumulate. GREEN-after: closing the last client releases the game.
//
// Observation via the production /health endpoint (activeGames). Robust against
// sibling games by measuring the DELTA around THIS client's own lifecycle rather
// than an absolute count. Real-UI only (open + connect + close a real browser
// context); no injection.

import { test, expect, request as pwRequest } from '@playwright/test';
import {
  defangOverlays, dismissLobbyAndTour, ensureConnected, takeSeatByClick,
  waitForGameObject, uatGameUrl, freshGameId, resolveBase, recordRedEvidence,
} from './helpers/changsha-real-pointer';

function healthUrl(base: string): string {
  // base looks like http://host:port/autotable/ — /health sits at the origin.
  const u = new URL(base);
  return `${u.protocol}//${u.host}/health`;
}

async function readActiveGames(base: string): Promise<number> {
  const ctx = await pwRequest.newContext();
  try {
    const res = await ctx.get(healthUrl(base));
    const body = await res.json();
    return typeof body.activeGames === 'number' ? body.activeGames : NaN;
  } finally {
    await ctx.dispose();
  }
}

test.describe('UAT G10 — no active-games leak after the last client leaves', () => {
  test('an opened game is released from the active-games count once its only client closes', async ({ browser, baseURL }) => {
    test.setTimeout(90_000);
    const base = resolveBase(baseURL);
    const gameId = freshGameId('g10-active-games');

    const before = await readActiveGames(base);
    expect(Number.isFinite(before), '/health did not report a numeric activeGames').toBe(true);

    // Open the fresh game in its own isolated context so closing it releases only
    // this game.
    const ctx = await browser.newContext();
    const page = await ctx.newPage();
    await defangOverlays(page);
    await page.goto(uatGameUrl(base, { gameId, dealMode: 'auto', seat: 0 }), { waitUntil: 'domcontentloaded' });
    expect(await waitForGameObject(page), 'window.game never booted').toBe(true);
    await dismissLobbyAndTour(page);
    await ensureConnected(page);
    await takeSeatByClick(page, 0);
    await page.waitForTimeout(1500);

    const during = await readActiveGames(base);
    expect(during, 'opening a fresh game must raise the active-games count').toBeGreaterThan(before - 1);

    // The only client for this game goes away entirely.
    await ctx.close();

    // Poll for release.
    let after = during;
    const deadline = Date.now() + 20_000;
    while (Date.now() < deadline) {
      after = await readActiveGames(base);
      if (after < during) break;
      await new Promise((r) => setTimeout(r, 1000));
    }

    const metrics = {
      activeBefore: before,
      activeDuring: during,
      activeAfterClose: after,
      released: after < during,
    };
    recordRedEvidence({
      id: 'G10-active-games-lifecycle',
      expected: 'Closing the only client of a freshly opened game releases it: the active-games count drops back below the in-session peak.',
      observed: `before=${before}, during=${during}, afterClose=${after}, released=${after < during}`,
      metrics,
    });

    expect(after, 'the game must be released from the active-games count once its only client closes').toBeLessThan(during);
  });
});
