// NG (OWNED by hudson-1 — user "New Game" complaint, 2026-08-07 12:51). A persistent
// one-click New Game must work on ACTIVE Changsha (desktop + mobile): a visible text
// `New Game` control (>=44px, no overlap, keyboard/touch); one real click ⇒ exactly
// one fresh gameId with config preserved, the old socket/cues cleared, the same
// player/seat auto-reclaimed, 3 bots seated, and Auto reaching a playable FACE-UP
// hand WITHOUT any Deal/Setup/Take-Seat/lobby click (Manual ⇒ stable ceremony); no
// legacy relay UPDATE/local deal, no stale state/move-log, old activeGames released;
// rapid double-click ⇒ exactly ONE new session. RED@200cad4 (no persistent button in
// active Changsha — #new-game is the hidden relay-sidebar control). Real pointer only.
import { test, expect, type Page } from '@playwright/test';
import { buildGameUrl, makeConfig, dismissLobbyAndTour, ensureConnected, takeSeatByClick, clickDeal, waitForPlayableHand } from './_playability';
import { recordEvidence, shot } from './_uat_red';

async function findNewGameControl(page: Page) {
  return page.evaluate(() => {
    const els = Array.from(document.querySelectorAll('button,[role="button"],a,input[type="button"],input[type="submit"]')) as HTMLElement[];
    for (const el of els) {
      const txt = ((el.textContent || '') + ' ' + (el.getAttribute('aria-label') || '') + ' ' + ((el as HTMLInputElement).value || '')).trim();
      if (!/new\s*game/i.test(txt)) continue;
      const r = el.getBoundingClientRect(); const cs = getComputedStyle(el);
      const visible = r.width > 0 && r.height > 0 && cs.display !== 'none' && cs.visibility !== 'hidden' && el.offsetParent !== null && r.top < window.innerHeight && r.bottom > 0 && r.left < window.innerWidth && r.right > 0;
      if (!visible) continue;
      const cx = Math.min(window.innerWidth - 1, Math.max(0, r.left + r.width / 2)); const cy = Math.min(window.innerHeight - 1, Math.max(0, r.top + r.height / 2));
      const top = document.elementFromPoint(cx, cy); const hitOk = !!top && (top === el || el.contains(top) || (top as any).contains?.(el));
      return { id: el.id || null, tag: el.tagName, text: txt.slice(0, 40), w: Math.round(r.width), h: Math.round(r.height), x: Math.round(r.left), y: Math.round(r.top), cx: Math.round(cx), cy: Math.round(cy), hitOk };
    }
    return null;
  });
}

async function readGameState(page: Page) {
  return page.evaluate(() => {
    /* eslint-disable @typescript-eslint/no-explicit-any */
    const g = (window as any).game; const w = g?.world; const u = new URL(location.href);
    let hand = 0, ownUp = 0, wall = 0; if (w?.things) for (const t of w.things.values()) { const nm = String(t?.slot?.name ?? ''); if (/^hand\.\d+@0$/.test(nm)) { hand++; if (t.rotationIndex === 1) ownUp++; } if (t?.slot?.group === 'wall') wall++; }
    const moveLog = (document.getElementById('move-log')?.textContent || '').trim().length;
    const q = (k: string) => u.searchParams.get(k);
    return { gameId: q('gameId'), variant: q('variant'), dealMode: q('dealMode'), botCount: q('botCount'), botDifficulty: q('botDifficulty'), seat: g?.client?.seat ?? null, playerId: g?.client?.playerId ? g.client.playerId() : null, hand, ownUp, wall, moveLogLen: moveLog, connected: !!g?.client?.connected };
    /* eslint-enable @typescript-eslint/no-explicit-any */
  });
}

async function bootChangsha(page: Page, dealMode: 'auto' | 'manual', gameId: string) {
  const base = (test.info().project.use as any).baseURL as string;
  const cfg = makeConfig({ gameId, dealMode, botCount: 3, botDifficulty: 'Medium', handCount: 4 });
  await page.goto(buildGameUrl(base, cfg), { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(1000); await dismissLobbyAndTour(page); await ensureConnected(page);
  await takeSeatByClick(page, 0); await clickDeal(page).catch(() => {});
  if (dealMode === 'auto') await waitForPlayableHand(page, 45_000).catch(() => {});
  await page.waitForTimeout(1500);
}

test.describe('NG persistent New Game on active Changsha (real pointer)', () => {
  for (const vp of [{ name: 'desktop', w: 1440, h: 900 }, { name: 'mobile-390x844', w: 390, h: 844 }]) {
    test(`@${vp.name}: a visible New Game control is present (>=44px, hit-testable)`, async ({ page }, testInfo) => {
      testInfo.setTimeout(90_000);
      await page.setViewportSize({ width: vp.w, height: vp.h });
      await bootChangsha(page, 'auto', `ng-btn-${vp.name}-${Date.now()}`);
      const ctrl = await findNewGameControl(page);
      await shot(page, `ng-btn-${vp.name}.png`);
      recordEvidence(`ng-button-${vp.name}.json`, { ctrl, note: 'RED@200cad4: no persistent New Game control in active Changsha (the #new-game button lives in the FE-1-hidden relay sidebar).' });
      expect(ctrl, `@${vp.name}: a visible "New Game" control must be present during active Changsha play`).not.toBeNull();
      expect(ctrl && ctrl.w >= 44 && ctrl.h >= 44, `@${vp.name}: New Game control must be a >=44px hit target; got ${ctrl?.w}x${ctrl?.h}`).toBe(true);
      expect(ctrl && ctrl.hitOk, `@${vp.name}: New Game control must be hit-testable (not occluded)`).toBe(true);
    });
  }

  test('one real click → exactly one fresh authoritative game (config+seat preserved, Auto playable with NO extra clicks)', async ({ page }, testInfo) => {
    testInfo.setTimeout(120_000);
    await page.setViewportSize({ width: 1440, height: 900 });
    await bootChangsha(page, 'auto', `ng-click-${Date.now()}`);
    const before = await readGameState(page);
    const ctrl = await findNewGameControl(page);
    recordEvidence('ng-oneclick.json', { before, ctrl, note: 'RED@200cad4: no persistent New Game control ⇒ cannot one-click a fresh authoritative game.' });
    expect(ctrl, 'a visible New Game control is required to test the one-click flow').not.toBeNull();

    // ONE real pointer click.
    await page.mouse.click(ctrl!.cx, ctrl!.cy);
    await page.waitForTimeout(3500);
    await waitForPlayableHand(page, 30_000).catch(() => {});
    await page.waitForTimeout(1000);
    const after = await readGameState(page);
    await shot(page, 'ng-after-click.png');
    recordEvidence('ng-oneclick-after.json', { before, after });

    // fresh gameId, config preserved, same seat/player auto-reclaimed.
    expect(after.gameId && after.gameId !== before.gameId, `must mint a FRESH gameId; before=${before.gameId} after=${after.gameId}`).toBe(true);
    expect(after.variant === before.variant && after.dealMode === before.dealMode && after.botCount === before.botCount && after.botDifficulty === before.botDifficulty, `config must be preserved; before=${JSON.stringify(before)} after=${JSON.stringify(after)}`).toBe(true);
    expect(after.seat === before.seat && after.playerId === before.playerId, `same player/seat auto-reclaimed; seat ${before.seat}->${after.seat}, pid stable=${before.playerId === after.playerId}`).toBe(true);
    // Auto reaches a playable FACE-UP hand WITHOUT any Deal/Setup/Take-Seat click.
    expect(after.ownUp, `Auto must reach a playable FACE-UP hand with NO extra clicks; ownUp=${after.ownUp}`).toBeGreaterThanOrEqual(13);
    // no stale move-log carried over from the old game.
    expect(after.moveLogLen, `move log must be clean on the fresh game; len=${after.moveLogLen}`).toBe(0);
  });

  test('rapid double-click creates exactly ONE new session (debounce)', async ({ page }, testInfo) => {
    testInfo.setTimeout(120_000);
    await page.setViewportSize({ width: 1440, height: 900 });
    await bootChangsha(page, 'auto', `ng-dbl-${Date.now()}`);
    const before = await readGameState(page);
    const ctrl = await findNewGameControl(page);
    expect(ctrl, 'a visible New Game control is required to test debounce').not.toBeNull();
    const seen = new Set<string>();
    page.on('framenavigated', () => { const gid = new URL(page.url()).searchParams.get('gameId'); if (gid) seen.add(gid); });
    // two rapid real clicks
    await page.mouse.click(ctrl!.cx, ctrl!.cy); await page.waitForTimeout(120); await page.mouse.click(ctrl!.cx, ctrl!.cy);
    await page.waitForTimeout(4000);
    const after = await readGameState(page);
    const freshIds = [...seen].filter((g) => g !== before.gameId);
    recordEvidence('ng-debounce.json', { beforeGameId: before.gameId, navigatedGameIds: [...seen], freshIds, afterGameId: after.gameId,
      note: 'RED@200cad4 (no button). GREEN: a rapid double-click must mint exactly ONE fresh gameId, not two.' });
    expect(freshIds.length, `a rapid double-click must create exactly ONE new session; minted ${freshIds.length} fresh gameIds ${JSON.stringify(freshIds)}`).toBe(1);
  });

  test('disconnected path: a New Game control stays reachable + mints a fresh authoritative game after a WS drop', async ({ page }, testInfo) => {
    testInfo.setTimeout(120_000);
    await page.setViewportSize({ width: 1440, height: 900 });
    await bootChangsha(page, 'auto', `ng-disc-${Date.now()}`);
    const before = await readGameState(page);
    await page.locator('#disconnect').click({ timeout: 5000 }).catch(() => {});
    await page.waitForTimeout(1800);
    const ctrl = await findNewGameControl(page);
    recordEvidence('ng-disconnected.json', { before, ctrl, note: 'RED@200cad4: after a WS drop there is no persistent New Game control to recover via a fresh authoritative game.' });
    expect(ctrl, 'a New Game control must stay reachable after a disconnect (recover path)').not.toBeNull();
    await page.mouse.click(ctrl!.cx, ctrl!.cy); await page.waitForTimeout(3500); await waitForPlayableHand(page, 30_000).catch(() => {});
    const after = await readGameState(page);
    recordEvidence('ng-disconnected-after.json', { before, after });
    expect(after.gameId && after.gameId !== before.gameId, `disconnected New Game must mint a FRESH gameId; before=${before.gameId} after=${after.gameId}`).toBe(true);
    expect(after.connected, 'the fresh game must be connected (authoritative), not left disconnected').toBe(true);
    expect(after.seat === before.seat && after.playerId === before.playerId, 'same player/seat auto-reclaimed on the recover path').toBe(true);
  });
});
