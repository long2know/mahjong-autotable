import { test, expect, type JSHandle, type Page } from '@playwright/test';
import { writeFileSync } from 'node:fs';
import {
  newActor, applyRoom, openLobby, closeLobby, dismissPrompts, probe, type Actor, type WireFrame,
} from './_lobby-repair';
import { mobileGeometry, waitForBoardHand } from './_mobile-geometry';

function discards(frames: WireFrame[]): Array<[string, string | number, Record<string, unknown> | null]> {
  return frames.flatMap(frame => frame.entries ?? []).filter(([kind]) => kind === 'discard');
}

async function waitForDiscardTurn(actor: Actor): Promise<void> {
  const dock = actor.page.locator('#mobile-hand-dock');
  await expect.poll(async () => {
    const pass = dock.locator('.ferro-claim-pass');
    if (await pass.isVisible() && await pass.isEnabled()) await pass.tap();
    return dock.locator('.mobile-hand-tile:not([disabled])').count();
  }, { timeout: 15_000 }).toBe(14);
}

async function confirmMiddleTile(page: Page): Promise<number> {
  const tiles = page.locator('#mobile-hand-dock .mobile-hand-tile:not([disabled])');
  const tile = tiles.nth(Math.floor(await tiles.count() / 2));
  await expect(tile).toBeVisible();
  const id = Number(await tile.getAttribute('data-tile-id'));
  await tile.tap();
  const confirm = page.locator('#mobile-hand-dock .mobile-hand-discard');
  await expect(confirm).toBeEnabled();
  await expect(confirm).toHaveAttribute('data-tile-id', String(id));
  await confirm.tap();
  return id;
}

async function bottomGeometry(page: Page): Promise<{
  board: { top: number; bottom: number };
  dock: { top: number; height: number };
  pane: { top: number; bottom: number; scrollTop: number; scrollHeight: number; clientHeight: number };
  tile: { slot: string; top: number; bottom: number; hit: boolean } | null;
}> {
  return page.evaluate(() => {
    const board = document.getElementById('readable-board')!;
    const dock = document.getElementById('mobile-hand-dock')!;
    const pane = board.querySelector<HTMLElement>('.readable-board-scroll')!;
    pane.scrollTop = pane.scrollHeight;
    const boardBounds = board.getBoundingClientRect(), dockBounds = dock.getBoundingClientRect();
    const paneBounds = pane.getBoundingClientRect();
    const tiles = [...pane.querySelectorAll<HTMLElement>('[data-slot][data-kind]')];
    const bottom = tiles.reduce<HTMLElement | null>((previous, tile) =>
      previous === null || tile.getBoundingClientRect().bottom > previous.getBoundingClientRect().bottom
        ? tile : previous, null);
    const bounds = bottom?.getBoundingClientRect();
    const target = bounds === undefined ? null
      : document.elementFromPoint((bounds.left + bounds.right) / 2, (bounds.top + bounds.bottom) / 2);
    return {
      board: { top: boardBounds.top, bottom: boardBounds.bottom },
      dock: { top: dockBounds.top, height: dockBounds.height },
      pane: {
        top: paneBounds.top, bottom: paneBounds.bottom, scrollTop: pane.scrollTop,
        scrollHeight: pane.scrollHeight, clientHeight: pane.clientHeight,
      },
      tile: bottom === null || bounds === undefined ? null : {
        slot: bottom.dataset.slot!, top: bounds.top, bottom: bounds.bottom,
        hit: target !== null && (target === bottom || bottom.contains(target)),
      },
    };
  });
}

async function expectSceneRestored(page: Page): Promise<void> {
  await expect(page.locator('body')).not.toHaveClass(/readable-board-active/);
  await expect(page.locator('#readable-board')).toBeHidden();
  await expect(page.locator('#main')).toHaveCSS('visibility', 'visible');
  expect(await page.locator('#main').evaluate(node => ({
    inert: (node as HTMLElement).inert, aria: node.getAttribute('aria-hidden'),
  }))).toEqual({ inert: false, aria: null });
}

interface FocusProbe {
  capture(stage: string): Record<string, unknown>;
  history: Array<Record<string, unknown>>;
  stop(): void;
}

async function traceFocus(page: Page): Promise<JSHandle<FocusProbe>> {
  return page.evaluateHandle(() => {
    const root = document.getElementById('readable-board')!;
    const pane = root.querySelector<HTMLElement>('.readable-board-scroll')!;
    const dock = document.getElementById('mobile-hand-dock')!;
    const chat = document.getElementById('chat-panel');
    const tile = pane.querySelector('[data-slot]');
    const main = document.getElementById('main')!;
    const history: Array<Record<string, unknown>> = [];
    const describe = (node: Element | null): unknown => node === null ? null : {
      tag: node.tagName, id: node.id, className: node.getAttribute('class'),
      hidden: node instanceof HTMLElement && node.hidden, inert: node instanceof HTMLElement && node.inert,
    };
    const geometry = (node: HTMLElement): unknown => {
      const bounds = node.getBoundingClientRect(), style = getComputedStyle(node);
      return {
        top: bounds.top, bottom: bounds.bottom, width: bounds.width, height: bounds.height,
        display: style.display, visibility: style.visibility, connected: node.isConnected,
      };
    };
    const capture = (stage: string, detail: Record<string, unknown> = {}): Record<string, unknown> => {
      const state = {
        stage, time: performance.now(), ...detail, active: describe(document.activeElement),
        documentFocused: document.hasFocus(), paneFocused: document.activeElement === pane,
        viewport: { width: innerWidth, height: innerHeight, visualHeight: visualViewport?.height },
        pane: {
          geometry: geometry(pane), same: root.querySelector('.readable-board-scroll') === pane,
          parent: describe(pane.parentElement), scrollTop: pane.scrollTop, tabIndex: pane.tabIndex,
        },
        root: {
          geometry: geometry(root), same: document.getElementById('readable-board') === root,
          hidden: root.hidden, bodyClass: document.body.className,
          footer: root.style.getPropertyValue('--readable-board-footer-inset'),
        },
        dock: { geometry: geometry(dock), same: document.getElementById('mobile-hand-dock') === dock },
        chat: chat === null ? null : { geometry: geometry(chat), bottom: getComputedStyle(chat).bottom },
        tileConnected: tile?.isConnected,
        main: { inert: main.inert, ariaHidden: main.getAttribute('aria-hidden') },
      };
      if (history.length < 80) history.push(state);
      return state;
    };
    const onFocus = (event: FocusEvent): void => {
      capture(event.type, { target: describe(event.target as Element | null), related: describe(event.relatedTarget as Element | null) });
    };
    const observer = new MutationObserver(records => {
      for (const record of records) {
        const target = record.target as Element;
        if (record.type === 'attributes' && record.oldValue === target.getAttribute(record.attributeName!)) continue;
        capture(`mutation:${record.type}`, { target: describe(target), attribute: record.attributeName });
      }
    });
    for (const node of [root, document.body, main]) {
      observer.observe(node, {
        attributes: true, attributeOldValue: true, attributeFilter: ['hidden', 'class', 'style', 'inert', 'aria-hidden'],
        childList: node === root,
      });
    }
    observer.observe(pane, { childList: true });
    document.addEventListener('focusin', onFocus);
    document.addEventListener('focusout', onFocus);
    capture('before-focus');
    return {
      capture, history,
      stop() {
        observer.disconnect();
        document.removeEventListener('focusin', onFocus);
        document.removeEventListener('focusout', onFocus);
      },
    };
  });
}

for (const viewport of [{ width: 360, height: 640 }, { width: 412, height: 915 }]) {
  test(`readable ${viewport.width} keeps the last public tile reachable through a real dropped-discard recovery`, async ({ browser, baseURL }, testInfo) => {
    test.setTimeout(90_000);
    const context = await browser.newContext({
      viewport, hasTouch: true, isMobile: true, deviceScaleFactor: 1, locale: 'en',
    });
    const sent: WireFrame[] = [], forwarded: WireFrame[] = [], received: WireFrame[] = [];
    let dropNextDiscard = false, dropped = 0;
    let focusProbe: JSHandle<FocusProbe> | null = null;
    await context.routeWebSocket(url => url.pathname === '/autotable/ws', route => {
      const server = route.connectToServer();
      route.onMessage(message => {
        const frame = JSON.parse(String(message)) as WireFrame;
        sent.push(frame);
        if (dropNextDiscard && discards([frame]).length > 0) {
          dropNextDiscard = false;
          dropped++;
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
      url.searchParams.set('mobileLayout', 'readable');
      const actor = await newActor(browser, baseURL, testInfo, `readable-recovery-${viewport.width}`, context, url.href);
      const page = actor.page;
      await page.locator('#lobby-hand-count-fieldset label:has(input[value="1"])').click();
      await page.locator('#lobby-advanced summary').click();
      await page.locator('#lobby-seed').fill('12345');
      await page.locator('#lobby-bot-difficulty-fieldset label:has(input[value="Easy"])').click();
      const room = await applyRoom(actor, 3, 0, 'auto');
      await closeLobby(actor);
      const board = page.locator('#readable-board'), dock = page.locator('#mobile-hand-dock');
      await expect(board).toBeVisible();
      await waitForDiscardTurn(actor);
      const first = await confirmMiddleTile(page);
      await expect.poll(() => received.flatMap(frame => frame.entries ?? []).some(([kind, id, value]) =>
        kind === 'things' && id === first && String(value?.slotName).startsWith('discard.'))).toBe(true);
      await expect.poll(async () => (await probe(actor)).handIds).not.toContain(first);
      await waitForDiscardTurn(actor);
      await expect(board.locator('.readable-board-scroll [data-slot]').first()).toBeVisible();
      const initial = await probe(actor), attemptStart = sent.length, forwardedStart = forwarded.length;
      const before = await bottomGeometry(page);
      dropNextDiscard = true;
      const selected = await confirmMiddleTile(page);
      await expect.poll(() => discards(sent.slice(attemptStart)).map(([, , value]) => value?.tileId))
        .toEqual([selected]);
      expect(discards(forwarded.slice(forwardedStart))).toEqual([]);
      await expect(dock).toHaveAttribute('aria-busy', 'true');
      await expect(dock.locator('.mobile-hand-discard')).toBeDisabled();
      const pane = board.locator('.readable-board-scroll');
      const stableTile = await pane.locator('[data-slot]').last().elementHandle();
      const stablePane = await pane.elementHandle();
      focusProbe = await traceFocus(page);
      await pane.focus();
      const focused = await focusProbe.evaluate(probe => probe.capture('after-focus'));
      expect(focused.paneFocused, 'the public scroller must hold focus before the recovery wait').toBe(true);
      const scrollTop = await pane.evaluate(node => {
        node.scrollTop = Math.min(40, node.scrollHeight - node.clientHeight);
        return node.scrollTop;
      });
      await focusProbe.evaluate(probe => probe.capture('before-recovery-wait'));
      const recovery = dock.getByRole('button', { name: 'Refresh table without resending the discard' });
      await expect(recovery).toBeVisible({ timeout: 20_000 });
      await expect(recovery).toBeInViewport({ ratio: 1 });
      await expect(dock.locator('.mobile-hand-status')).toContainText('will not be resent');
      await expect(dock.locator('.mobile-hand-tile:not([disabled])')).toHaveCount(0);
      const recoveredFocus = await focusProbe.evaluate(probe => probe.capture('after-recovery-visible'));
      expect((await probe(actor)).handIds.slice().sort((a, b) => a - b))
        .toEqual(initial.handIds.slice().sort((a, b) => a - b));
      expect((await probe(actor)).handIds).toContain(selected);
      expect(dropped).toBe(1);
      expect(discards(sent.slice(attemptStart))).toHaveLength(1);
      expect(discards(forwarded.slice(forwardedStart))).toEqual([]);
      expect(await stableTile!.evaluate(node => node.isConnected)).toBe(true);
      expect(await stablePane!.evaluate(node => node === document.activeElement)).toBe(true);
      expect(await pane.evaluate(node => node.scrollTop)).toBe(scrollTop);
      const focusHistory = await focusProbe.evaluate(probe => {
        probe.stop();
        return probe.history;
      });
      await focusProbe.dispose();
      focusProbe = null;
      const after = await bottomGeometry(page);
      const evidence = testInfo.outputPath(`readable-${viewport.width}-real-recovery.json`);
      writeFileSync(evidence, JSON.stringify({
        viewport, room, selected, initial, before, after, dropped, focused, recoveredFocus, focusHistory,
        sentDiscards: discards(sent), forwardedDiscards: discards(forwarded), consoleErrors: actor.errors,
      }, null, 2) + '\n');
      await testInfo.attach(`readable-${viewport.width}-real-recovery`, { path: evidence, contentType: 'application/json' });
      await page.screenshot({ path: testInfo.outputPath(`readable-${viewport.width}-real-recovery.png`) });
      expect(after.dock.height).toBeGreaterThan(before.dock.height);
      // Retain geometry failures while also exercising guard, refresh and new-room recovery.
      expect.soft(after.board.bottom, 'readable geometry must follow actual recovery dock growth').toBeLessThanOrEqual(after.dock.top);
      expect(after.tile).not.toBeNull();
      expect.soft(after.tile!.top).toBeGreaterThanOrEqual(after.pane.top);
      expect.soft(after.tile!.bottom).toBeLessThanOrEqual(after.pane.bottom);
      expect.soft(after.tile!.bottom).toBeLessThanOrEqual(after.dock.top);
      expect.soft(after.tile!.hit, 'the bottommost public tile is pointer-reachable at maximum scroll').toBe(true);

      await board.getByRole('button', { name: 'Switch to the 3D table, keeping your hand controls' }).tap();
      await expectSceneRestored(page);
      await expect(dock).toHaveAttribute('aria-busy', 'true');
      await expect(recovery).toBeVisible();
      await page.evaluate(() => {
        const url = new URL(location.href);
        url.searchParams.delete('mobileLayout');
        history.replaceState(history.state, '', url);
      });
      await expect(dock).toHaveCount(0);
      await expectSceneRestored(page);
      await waitForBoardHand(page);
      const standalone = page.locator('#mobile-discard-recovery');
      await expect(standalone).toBeVisible();
      const geometry = await mobileGeometry(page);
      const guarded = geometry.tiles.find(tile => tile.id !== selected && tile.hit);
      expect(guarded).toBeDefined();
      await page.touchscreen.tap(guarded!.x, guarded!.y);
      expect(discards(sent.slice(attemptStart))).toHaveLength(1);
      expect(discards(forwarded.slice(forwardedStart))).toEqual([]);
      expect((await probe(actor)).handIds.slice().sort((a, b) => a - b))
        .toEqual(initial.handIds.slice().sort((a, b) => a - b));

      const loaded = page.waitForEvent('domcontentloaded');
      await standalone.getByRole('button', { name: 'Refresh table without resending the discard' }).press('Enter');
      await loaded;
      await dismissPrompts(actor);
      await closeLobby(actor);
      await waitForBoardHand(page);
      await expect(standalone).toHaveCount(0);
      const refreshed = await probe(actor);
      expect(refreshed.handIds.slice().sort((a, b) => a - b))
        .toEqual(initial.handIds.slice().sort((a, b) => a - b));
      expect(discards(sent.slice(attemptStart))).toHaveLength(1);
      expect(discards(forwarded.slice(forwardedStart))).toEqual([]);
      await page.evaluate(() => {
        const url = new URL(location.href);
        url.searchParams.set('mobileLayout', 'readable');
        history.replaceState(history.state, '', url);
      });
      await openLobby(actor);
      if (!await page.locator('#lobby-seed').isVisible()) {
        await page.locator('#lobby-advanced summary').click();
      }
      await page.locator('#lobby-seed').fill('12346');
      const freshRoom = await applyRoom(actor, 3, 0, 'auto');
      expect(freshRoom).not.toBe(room);
      await closeLobby(actor);
      await expect(board).toBeVisible();
      await waitForDiscardTurn(actor);
      await expect(dock).toHaveAttribute('aria-busy', 'false');
      await expect(dock.locator('.mobile-hand-recovery')).toBeHidden();
      await expect(board.locator('.readable-board-scroll [data-slot]')).toHaveCount(0);
      const fresh = await probe(actor);
      expect(discards(sent.slice(attemptStart))).toHaveLength(1);
      const deliberateStart = forwarded.length, sentStart = sent.length, receiptStart = received.length;
      const deliberate = await confirmMiddleTile(page);
      await expect.poll(() => discards(forwarded.slice(deliberateStart)).map(([, , value]) => value?.tileId))
        .toEqual([deliberate]);
      await expect.poll(() => received.slice(receiptStart).flatMap(frame => frame.entries ?? []).some(([kind, id, value]) =>
        kind === 'things' && id === deliberate && String(value?.slotName).startsWith('discard.'))).toBe(true);
      await expect.poll(async () => (await probe(actor)).handIds).not.toContain(deliberate);
      expect(discards(sent.slice(sentStart))).toHaveLength(1);
      expect(dropped).toBe(1);
      expect(actor.errors).toEqual([]);
      const restoredEvidence = testInfo.outputPath(`readable-${viewport.width}-recovery-restored.json`);
      writeFileSync(restoredEvidence, JSON.stringify({
        viewport, room, freshRoom, selected, deliberate, dropped, refreshed, fresh,
        afterDeliberate: await probe(actor), sentDiscards: discards(sent),
        forwardedDiscards: discards(forwarded), consoleErrors: actor.errors,
      }, null, 2) + '\n');
      await testInfo.attach(`readable-${viewport.width}-recovery-restored`, {
        path: restoredEvidence, contentType: 'application/json',
      });
    } catch (error) {
      if (focusProbe !== null) {
        const evidence = await focusProbe.evaluate(probe => {
          const current = probe.capture('failure');
          probe.stop();
          return { current, history: probe.history };
        }).catch(probeError => ({ probeError: String(probeError) }));
        const filename = testInfo.outputPath(`readable-${viewport.width}-focus-failure.json`);
        writeFileSync(filename, JSON.stringify({ error: String(error), evidence }, null, 2) + '\n');
        await testInfo.attach(`readable-${viewport.width}-focus-failure`, {
          path: filename, contentType: 'application/json',
        });
      }
      throw error;
    } finally {
      await context.close();
    }
  });
}
