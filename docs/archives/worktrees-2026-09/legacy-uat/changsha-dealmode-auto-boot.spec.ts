// Ripley fast-tests lane — G21/A19: a Changsha game launched with dealMode=auto
// must boot straight into AUTO chrome — the authoritative server deals atomically
// at game start, so the seated player sees a full face-up hand WITHOUT pressing
// Deal, with NO manual pickup HUD and NO page reload.
//
// RED@200cad4: the shipped bundle is relay-driven — nothing is dealt until a
// manual #deal press, so an auto-mode game boots to an EMPTY table (own hand = 0)
// and the player is stuck. GREEN-after: the authoritative auto-deal populates the
// hand on boot.
//
// Real-UI only: connect + Take-seat via real clicks; we deliberately never click
// Deal. Fresh isolated gameId.

import { test, expect } from '@playwright/test';
import {
  defangOverlays, dismissLobbyAndTour, ensureConnected, takeSeatByClick,
  waitForGameObject, readMyHandTiles, uatGameUrl, freshGameId, resolveBase,
  recordRedEvidence,
} from './helpers/changsha-real-pointer';

test.describe('UAT G21 — dealMode=auto boots AUTO chrome (auto-deal, face-up, no HUD, no reload)', () => {
  test('an auto-mode game deals on boot: face-up hand with no manual Deal, no pickup HUD, no reload', async ({ page, baseURL }) => {
    test.setTimeout(90_000);
    const base = resolveBase(baseURL);
    const gameId = freshGameId('g21-auto-boot');

    await defangOverlays(page);
    await page.goto(uatGameUrl(base, { gameId, dealMode: 'auto', seat: 0 }), { waitUntil: 'domcontentloaded' });
    expect(await waitForGameObject(page), 'window.game never booted').toBe(true);
    await dismissLobbyAndTour(page);
    await ensureConnected(page);

    // Reload sentinel — a boot that silently reloads the page would fail GREEN.
    await page.evaluate(() => { (window as unknown as Record<string, unknown>).__g21_sentinel = true; });

    await takeSeatByClick(page, 0);

    // Deliberately DO NOT press Deal. In AUTO mode the authoritative server must
    // deal atomically; poll for a populated own hand.
    let ownTiles = 0;
    const deadline = Date.now() + 20_000;
    while (Date.now() < deadline) {
      ownTiles = (await readMyHandTiles(page).catch(() => [] as unknown[])).length;
      if (ownTiles >= 13) break;
      await page.waitForTimeout(500);
    }

    const sentinelAlive = await page.evaluate(() => (window as unknown as Record<string, unknown>).__g21_sentinel === true);
    // A manual pickup HUD must NOT be present in AUTO chrome.
    const pickupHudVisible = await page.locator('#pickup-hud, .pickup-hud, .manual-pickup').first().isVisible().catch(() => false);

    const metrics = {
      ownTilesAfterAutoBoot: ownTiles,
      reloadHappened: !sentinelAlive,
      pickupHudVisible,
    };
    recordRedEvidence({
      id: 'G21-dealmode-auto-boot',
      expected: 'dealMode=auto deals on boot: the seated player holds a full (>=13) face-up hand without pressing Deal, no manual pickup HUD, and no page reload.',
      observed: `ownTilesAfterAutoBoot=${ownTiles}, reload=${!sentinelAlive}, pickupHud=${pickupHudVisible}`,
      metrics,
    });

    expect(sentinelAlive, 'AUTO boot must not reload the page').toBe(true);
    expect(ownTiles, 'dealMode=auto must auto-deal a full hand on boot without a manual Deal press').toBeGreaterThanOrEqual(13);
    expect(pickupHudVisible, 'AUTO chrome must not show the manual pickup HUD').toBe(false);
  });
});
