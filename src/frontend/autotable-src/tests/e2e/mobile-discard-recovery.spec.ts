import { test, expect, type WebSocketRoute } from '@playwright/test';
import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { newActor, applyRoom, closeLobby, dismissPrompts, probe, type WireFrame } from './_lobby-repair';
import { mobileGeometry, waitForBoardHand } from './_mobile-geometry';

type Bounds = { left: number; right: number; top: number; bottom: number };
const overlaps = (a: Bounds, b: Bounds): boolean =>
  Math.min(a.right, b.right) > Math.max(a.left, b.left)
  && Math.min(a.bottom, b.bottom) > Math.max(a.top, b.top);

for (const exit of ['landscape', 'off'] as const) {
  test(`real pending recovery survives timeout, ${exit} and reconnect without resending`, async ({ browser, baseURL }, testInfo) => {
    test.setTimeout(90_000);
    const context = await browser.newContext({
      viewport: { width: 412, height: 924 }, hasTouch: true, isMobile: true, deviceScaleFactor: 1,
    });
    const sent: WireFrame[] = [], forwarded: WireFrame[] = [], received: WireFrame[] = [];
    let dropNextDiscard = false;
    let connections = 0;
    let current: WebSocketRoute | null = null;
    const entries = (frames: WireFrame[]) => frames.flatMap(frame => frame.entries ?? [])
      .filter(([kind]) => kind === 'discard');
    await context.routeWebSocket(url => url.pathname === '/autotable/ws', route => {
      const server = route.connectToServer();
      current = server;
      connections++;
      route.onMessage(message => {
        const frame = JSON.parse(String(message)) as WireFrame;
        sent.push(frame);
        // Lose one real outbound packet, not game state or a synthetic receipt.
        if (dropNextDiscard && entries([frame]).length > 0) {
          dropNextDiscard = false;
          return;
        }
        forwarded.push(frame);
        server.send(message);
      });
      server.onMessage(message => {
        received.push(JSON.parse(String(message)) as WireFrame);
        route.send(message);
      });
    });
    try {
      const url = new URL(baseURL ?? 'http://127.0.0.1:5114/autotable/');
      url.searchParams.set('mobileLayout', 'hybrid');
      const actor = await newActor(browser, baseURL, testInfo, `pending-${exit}`, context, url.href);
      await actor.page.locator('#lobby-hand-count-fieldset label:has(input[value="1"])').click();
      await actor.page.locator('#lobby-advanced summary').click();
      await actor.page.locator('#lobby-seed').fill('4100');
      await applyRoom(actor, 3, 0, 'auto');
      await closeLobby(actor);
      const dock = actor.page.locator('#mobile-hand-dock');
      await expect(dock.locator('.mobile-hand-tile')).toHaveCount(14);
      const initial = await probe(actor);
      const tile = dock.locator('.mobile-hand-tile').first();
      await expect(tile).toBeEnabled();
      const selected = Number(await tile.getAttribute('data-tile-id'));
      dropNextDiscard = true;
      await tile.tap();
      await dock.locator('.mobile-hand-discard').tap();
      await expect.poll(() => entries(sent).map(([, , value]) => value?.tileId)).toEqual([selected]);
      expect(entries(forwarded)).toEqual([]);
      await expect(dock.locator('.mobile-hand-recovery')).toBeVisible({ timeout: 20_000 });
      await expect(dock.locator('.mobile-hand-status')).toContainText('will not be resent');
      if (exit === 'landscape') {
        await actor.page.setViewportSize({ width: 924, height: 412 });
      } else {
        await actor.page.evaluate(() => {
          const url = new URL(location.href);
          url.searchParams.delete('mobileLayout');
          history.replaceState(null, '', url);
        });
      }
      await expect(dock).toHaveCount(0);
      await waitForBoardHand(actor.page);
      const root = actor.page.locator('#mobile-discard-recovery');
      const recovery = root.getByRole('button', { name: 'Refresh table without resending the discard' });
      await expect(root).toBeVisible();
      await expect(recovery).toBeInViewport({ ratio: 1 });
      await expect(actor.page.locator('#rule-action-recovery')).toBeHidden();
      const geometry = await mobileGeometry(actor.page);
      const box = await root.boundingBox();
      expect(box).not.toBeNull();
      const bounds = { left: box!.x, right: box!.x + box!.width, top: box!.y, bottom: box!.y + box!.height };
      for (const handTile of geometry.tiles) expect(overlaps(bounds, handTile), `recovery covers tile ${handTile.id}`).toBe(false);
      for (const action of geometry.actions) expect(overlaps(bounds, action), `recovery covers ${action.id}`).toBe(false);
      const guarded = geometry.tiles.find(item => item.id !== selected)!;
      expect(guarded.hit, 'ordinary pointer reaches the restored canvas and physical tile').toBe(true);
      await actor.page.touchscreen.tap(guarded.x, guarded.y);
      expect(entries(sent)).toHaveLength(1);
      expect((await probe(actor)).handIds).toEqual(initial.handIds);
      const beforeConnections = connections;
      expect(current).not.toBeNull();
      await current!.close({ code: 1000, reason: 'local recovery regression' });
      await expect.poll(() => connections).toBeGreaterThan(beforeConnections);
      await expect.poll(async () => {
        const state = await probe(actor);
        writeFileSync(testInfo.outputPath('reconnect-state.json'), JSON.stringify({
          state, url: actor.page.url(), errors: actor.errors,
          recentFrames: received.slice(-12).map(frame => ({
            type: frame.type, gameId: frame.gameId,
            entries: frame.entries?.filter(([kind]) => ['seats', 'turn', 'ownTurn', 'conditions', 'actionRejected'].includes(kind)),
          })),
        }, null, 2));
        return { connected: state.connected, seat: state.seat, hand: state.handIds.slice().sort((a, b) => a - b) };
      }, { timeout: 15_000 }).toEqual({
        connected: true, seat: initial.seat, hand: initial.handIds.slice().sort((a, b) => a - b),
      });
      await expect(root).toBeVisible();
      await expect(root.locator('.mobile-hand-status')).toContainText('will not be resent');
      await waitForBoardHand(actor.page);
      const reconnected = await mobileGeometry(actor.page);
      const sameTile = reconnected.tiles.find(item => item.id === guarded.id)!;
      await actor.page.touchscreen.tap(sameTile.x, sameTile.y);
      expect(entries(sent)).toHaveLength(1);
      expect(entries(forwarded)).toEqual([]);
      await expect.poll(() => recovery.evaluate(button => {
        const rect = button.getBoundingClientRect();
        const target = document.elementFromPoint((rect.left + rect.right) / 2, (rect.top + rect.bottom) / 2);
        return target !== null && (target === button || button.contains(target));
      }), { timeout: 10_000 }).toBe(true);
      const directory = process.env.MOBILE_LAYOUT_ARTIFACT_DIR;
      const evidence = (name: string): string => {
        if (!directory) return testInfo.outputPath(name);
        mkdirSync(directory, { recursive: true });
        return join(directory, name);
      };
      await actor.page.screenshot({ path: evidence(`pending-recovery-${exit}.png`) });
      const loaded = actor.page.waitForEvent('domcontentloaded');
      if (exit === 'landscape') await recovery.press('Enter');
      else await recovery.tap();
      await loaded;
      await dismissPrompts(actor);
      await closeLobby(actor);
      await waitForBoardHand(actor.page);
      await expect(root).toHaveCount(0);
      await expect(dock).toHaveCount(0);
      expect(entries(sent), 'Refresh and reconnect do not resend the uncertain packet').toHaveLength(1);
      expect(entries(forwarded)).toEqual([]);
      const refreshed = await mobileGeometry(actor.page);
      const deliberate = refreshed.tiles.find(item => item.id === guarded.id)!;
      await actor.page.touchscreen.tap(deliberate.x, deliberate.y);
      await expect.poll(() => entries(forwarded).map(([, , value]) => value?.tileId)).toEqual([guarded.id]);
      await expect.poll(() => received.flatMap(frame => frame.entries ?? []).some(([kind, id, value]) =>
        kind === 'things' && id === guarded.id && String(value?.slotName).startsWith('discard.'))).toBe(true);
      await expect.poll(async () => (await probe(actor)).handIds).not.toContain(guarded.id);
      expect(entries(sent).map(([, , value]) => value?.tileId)).toEqual([selected, guarded.id]);
      writeFileSync(evidence(`pending-recovery-${exit}.json`), JSON.stringify({
        exit, selected, guarded: guarded.id, initial, geometry, reconnected, connections,
        sentDiscards: entries(sent), forwardedDiscards: entries(forwarded),
        serverDiscard: received.flatMap(frame => frame.entries ?? []).filter(([kind, id, value]) =>
          kind === 'things' && id === guarded.id && String(value?.slotName).startsWith('discard.')),
        recoveryBounds: bounds, keyboardRefresh: exit === 'landscape', consoleErrors: actor.errors,
      }, null, 2) + '\n');
      expect(actor.errors).toEqual([]);
    } finally {
      await context.close();
    }
  });
}
