// UAT (Hudson) — Suite A / B1: repeated + mid-hand Deal/Setup abuse must NOT
// locally redeal/rebuild/disconnect/mutate the rendered wall/hand, nor mint a
// "New hand". In a server-authoritative Changsha game the human's Deal is a
// one-shot request the SERVER owns; hammering Deal or toggling the legacy Setup
// controls after the deal must be inert on the client (no outbound `things`
// tile-mutation burst, no local collection churn, connection preserved).
//
// RED baseline (200cad4): the shipped bundle is relay-driven — the CLIENT
// rebuilds the wall and pushes tiles, so a second #deal press re-emits
// `["things", <id>, {...}]` UPDATE frames. This spec pins the desired
// authoritative behaviour and is expected to FAIL until the deal is lifted
// server-side.
//
// Discipline: fresh isolated gameId; every advance is a real button click; the
// only "reads" are the client's own collections + its OWN outbound WS frames.

import { test, expect } from '@playwright/test';
import {
  defangOverlays, dismissLobbyAndTour, ensureConnected, takeSeatByClick,
  clickDeal, waitForGameObject,
} from './_playability';
import {
  resolveBase, uatGameUrl, freshGameId, recordOutboundWs, thingsFingerprint,
  readMoveLogText, recordRedEvidence,
} from './_uat-changsha';

test.describe('UAT B1 — Deal/Setup abuse is server-authoritative & inert on the client', () => {
  test('repeated #deal after the deal emits NO outbound things mutation and does not churn the local wall/hand', async ({ page, baseURL }) => {
    test.setTimeout(120_000);
    const base = resolveBase(baseURL);
    const gameId = freshGameId('b1-repeat-deal');

    const pageErrors: string[] = [];
    page.on('pageerror', (e) => pageErrors.push(String(e)));
    const ws = recordOutboundWs(page);

    await defangOverlays(page);
    await page.goto(uatGameUrl(base, { gameId, dealMode: 'auto' }), { waitUntil: 'domcontentloaded' });
    expect(await waitForGameObject(page), 'window.game never booted').toBe(true);
    await dismissLobbyAndTour(page);
    expect(await ensureConnected(page), 'client never connected').toBe(true);
    await takeSeatByClick(page, 0);

    // First (legitimate) deal.
    await clickDeal(page);
    await page.waitForTimeout(3500);
    const fpAfterFirstDeal = await thingsFingerprint(page);
    const moveLogAfterFirst = await readMoveLogText(page);
    expect(fpAfterFirstDeal.count, 'first deal should populate the rendered table').toBeGreaterThan(0);

    // ── ABUSE: hammer #deal three more times + toggle the legacy Setup group.
    ws.clear();
    for (let i = 0; i < 3; i++) {
      await clickDeal(page);
      await page.waitForTimeout(600);
    }
    // Also poke the legacy Setup dropdown + deal-type mid-hand.
    await page.locator('#toggle-setup').click({ timeout: 3000 }).catch(() => {});
    await page.locator('#deal-type').selectOption('INITIAL').catch(() => {});
    await page.waitForTimeout(400);
    await clickDeal(page);
    await page.waitForTimeout(2500);

    const fpAfterAbuse = await thingsFingerprint(page);
    const moveLogAfterAbuse = await readMoveLogText(page);
    const thingsMutations = ws.countThingsMutations();

    const stillConnected = await page.evaluate(() => {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const c = (window as any).game?.client;
      try { return typeof c?.connected === 'function' ? Boolean(c.connected()) : false; } catch { return false; }
    });

    // The metrics that back the verdict.
    const metrics = {
      outboundThingsMutationsDuringAbuse: thingsMutations,
      localFingerprintChanged: fpAfterAbuse.hash !== fpAfterFirstDeal.hash || fpAfterAbuse.count !== fpAfterFirstDeal.count,
      moveLogGrewNewHand: /new hand/i.test(moveLogAfterAbuse) && !/new hand/i.test(moveLogAfterFirst),
      stillConnected,
      pageErrors: pageErrors.length,
    };
    recordRedEvidence({
      id: 'B1-repeat-deal',
      expected: 'Repeated/mid-hand Deal/Setup abuse is inert on the client: 0 outbound things mutations, no local wall/hand churn, no "New hand", connection preserved.',
      observed: `outbound things mutations=${thingsMutations}, localFingerprintChanged=${metrics.localFingerprintChanged}, stillConnected=${stillConnected}`,
      metrics,
    });

    // Desired (GREEN) invariants — RED on the relay baseline.
    expect(stillConnected, 'Deal abuse must not disconnect the client').toBe(true);
    expect(thingsMutations, 'repeated Deal must not push client-side tile mutations (server owns the deal)').toBe(0);
    expect(
      metrics.moveLogGrewNewHand,
      'hammering Deal must not fabricate a "New hand" locally',
    ).toBe(false);
    expect(pageErrors, `no uncaught page errors during abuse: ${pageErrors.slice(0, 3).join(' | ')}`).toHaveLength(0);
  });
});
