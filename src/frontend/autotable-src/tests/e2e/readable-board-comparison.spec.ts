import { test, expect, type Page, type TestInfo } from '@playwright/test';
import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import type { Game } from '../../src/game';
import type { Camera } from 'three';
import { newActor, applyRoom, closeLobby, probe, driveManualCeremony, type Actor } from './_lobby-repair';

const viewport = { width: 412, height: 924 };
const baselineOnly = process.env.READABLE_CROWDED_BASELINE === '1';

test.use({
  launchOptions: {
    args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'],
  },
});

function artifact(info: TestInfo, name: string): string {
  const directory = process.env.READABLE_BOARD_ARTIFACT_DIR;
  if (!directory) return info.outputPath(name);
  mkdirSync(directory, { recursive: true });
  return join(directory, name);
}

async function publicState(page: Page) {
  return page.evaluate(() => {
    const game = (window as Window & { game?: Game }).game;
    if (!game) throw new Error('Game shell is not ready');
    const { client, world } = game;
    const tiles = [...client.things.entries()].filter(([, entry]) =>
      /^(discard|meld)\.\d+\.\d+@[0-3]$/.test(entry.slotName)).map(([id, entry]) => {
      const thing = typeof id === 'number' ? world.things.get(id) : undefined;
      const face = thing && !thing.hidden && thing.hiddenHandle === null
        && thing.slot.name === entry.slotName && thing.slot.thing === thing
        ? thing.typeIndex : null;
      return { slot: entry.slotName, face };
    }).sort((a, b) => a.slot.localeCompare(b.slot));
    return {
      tiles,
      discards: tiles.filter(tile => tile.slot.startsWith('discard.')).length,
      melds: tiles.filter(tile => tile.slot.startsWith('meld.')).length,
      wall: [...client.things.entries()].filter(([, entry]) => entry.slotName.startsWith('wall.')).length,
      hand: world.ownHandPresentation().tiles.map(tile => tile.id).sort((a, b) => a - b),
      turn: client.turn.get('current'),
    };
  });
}

async function confirmOne(actor: Actor): Promise<void> {
  const choices = actor.page.locator('.mobile-hand-tile:not([disabled])');
  const count = await choices.count();
  expect(count).toBeGreaterThan(0);
  const tile = choices.nth(Math.floor(count / 2));
  const id = Number(await tile.getAttribute('data-tile-id'));
  const before = actor.sent.flatMap(frame => frame.entries ?? []).filter(([kind]) => kind === 'discard').length;
  await tile.tap();
  await expect(tile).toHaveAttribute('aria-pressed', 'true', { timeout: 2_000 });
  expect(actor.sent.flatMap(frame => frame.entries ?? []).filter(([kind]) => kind === 'discard')).toHaveLength(before);
  const confirm = actor.page.locator('.mobile-hand-discard');
  await expect(confirm).toHaveAttribute('data-tile-id', String(id));
  await expect(confirm).toBeEnabled({ timeout: 2_000 });
  await confirm.tap({ timeout: 5_000 });
  await expect.poll(() => actor.sent.flatMap(frame => frame.entries ?? []).filter(([kind]) => kind === 'discard').length)
    .toBe(before + 1);
  await actor.page.waitForFunction(tileId => {
    const game = (window as Window & { game?: Game }).game;
    return game !== undefined && !game.client.things.get(tileId)?.slotName.startsWith('hand.');
  }, id);
}

async function takeOrPassClaim(actor: Actor): Promise<boolean> {
  const chooser = actor.page.locator('.rule-action-dialog[open] .rule-action-choice').first();
  if (await chooser.isVisible()) {
    await chooser.tap();
    return true;
  }
  for (const type of ['pung', 'chow']) {
    const action = actor.page.locator(`#mobile-hand-dock .ferro-claim-badge-${type}`);
    if (await action.isVisible() && await action.isEnabled()) {
      await action.tap();
      const choice = actor.page.locator('.rule-action-dialog[open] .rule-action-choice').first();
      if (await choice.isVisible()) await choice.tap();
      return true;
    }
  }
  const pass = actor.page.locator('#mobile-hand-dock .ferro-claim-pass');
  if (await pass.isVisible() && await pass.isEnabled()) {
    await pass.tap();
    return true;
  }
  return false;
}

async function readableMeasurements(page: Page) {
  return page.evaluate(() => {
    const board = document.getElementById('readable-board')!;
    const scroll = board.querySelector<HTMLElement>('.readable-board-scroll')!;
    const bounds = (element: Element) => {
      const box = element.getBoundingClientRect();
      return { left: box.left, top: box.top, right: box.right, bottom: box.bottom, width: box.width, height: box.height };
    };
    const faces = [...scroll.querySelectorAll<HTMLElement>('.readable-board-face')].map(bounds);
    const hand = document.getElementById('mobile-hand-dock')!;
    return {
      viewport: [innerWidth, innerHeight],
      board: bounds(board),
      scroll: { ...bounds(scroll), contentHeight: scroll.scrollHeight, offset: scroll.scrollTop },
      faces,
      faceMinimum: [Math.min(...faces.map(face => face.width)), Math.min(...faces.map(face => face.height))],
      hand: bounds(hand),
      handTargets: [...hand.querySelectorAll('.mobile-hand-tile')].map(bounds),
      tiles: [...scroll.querySelectorAll<HTMLElement>('[data-slot]')].map(tile => ({
        slot: tile.dataset.slot, kind: tile.dataset.kind, face: tile.dataset.face,
      })),
      hiddenScene: getComputedStyle(document.getElementById('main')!).visibility,
      inertScene: document.getElementById('main')!.inert,
    };
  });
}

test('a crowded authoritative hand compares unchanged hybrid and readable public tiles in the same room', async ({ browser, baseURL }, info) => {
  test.setTimeout(180_000);
  const context = await browser.newContext({ viewport, deviceScaleFactor: 1, hasTouch: true, isMobile: true });
  const entry = new URL(baseURL ?? 'http://127.0.0.1:5114/autotable/');
  entry.search = new URLSearchParams({
    mobileLayout: 'hybrid', variant: 'changsha', dealMode: 'auto',
    botCount: '3', botDifficulty: 'Easy', handCount: '1', seed: '12345', seat: '0',
  }).toString();
  const actor = await newActor(browser, baseURL, info, 'readable-crowded', context, entry.href);
  try {
    await applyRoom(actor, 3, 0, 'auto');
    await closeLobby(actor);
    await expect.poll(async () => (await probe(actor)).handCount, { timeout: 30_000 }).toBe(14);
    await expect(actor.page.locator('#mobile-hand-dock')).toBeVisible();
    const deadline = Date.now() + 100_000;
    let state = await publicState(actor.page);
    const readyForComparison = (current: typeof state) => current.discards >= 32 && current.melds >= 3
      && current.turn?.phase === 'AwaitingDiscard' && current.turn.activeSeat === 0;
    while (Date.now() < deadline) {
      state = await publicState(actor.page);
      if (readyForComparison(state)) break;
      if (await actor.page.locator('#result-modal').isVisible()
        || await actor.page.locator('#game-complete-modal').isVisible()) break;
      if (!await takeOrPassClaim(actor)) {
        const ready = actor.page.locator('.mobile-hand-tile:not([disabled])');
        if (await ready.count() > 0) {
          state = await publicState(actor.page);
          if (readyForComparison(state)) break;
          await confirmOne(actor);
        }
        else await actor.page.waitForTimeout(100);
      }
    }
    expect(state.discards, 'comparison must contain a genuine crowded river, not a mocked opening hand').toBeGreaterThanOrEqual(32);
    expect(state.melds, 'comparison must include actual exposed meld tiles').toBeGreaterThanOrEqual(3);
    expect(state.turn?.phase).toBe('AwaitingDiscard');
    expect(state.turn?.activeSeat).toBe(0);
    const ownMelds = new Set(state.tiles.filter(tile => tile.slot.startsWith('meld.')
      && tile.slot.endsWith('@0')).map(tile => tile.slot.split('.')[1])).size;
    await expect.poll(async () => (await publicState(actor.page)).hand.length).toBe(14 - 3 * ownMelds);
    state = await publicState(actor.page);
    writeFileSync(artifact(info, 'crowded-authoritative-state.json'), JSON.stringify(state, null, 2) + '\n');
    await actor.page.screenshot({ path: artifact(info, 'crowded-hybrid-412x924.png') });
    if (baselineOnly) {
      expect(actor.errors).toEqual([]);
      return;
    }

    const beforeSwitch = actor.sent.length;
    await actor.page.evaluate(() => {
      const readable = new URL(location.href);
      readable.searchParams.set('mobileLayout', 'readable');
      history.replaceState(history.state, '', readable);
    });

    await expect(actor.page.locator('#readable-board')).toBeVisible({ timeout: 30_000 });
    expect((await publicState(actor.page)).tiles).toEqual(state.tiles);
    expect((await publicState(actor.page)).hand).toEqual(state.hand);
    expect(actor.sent.slice(beforeSwitch).filter(frame => frame.type === 'NEW' || frame.type === 'JOIN')).toEqual([]);
    const report = await readableMeasurements(actor.page);
    expect(report.hiddenScene).toBe('hidden');
    expect(report.inertScene).toBe(true);
    expect(report.faceMinimum[0]).toBeGreaterThanOrEqual(40);
    expect(report.faceMinimum[1]).toBeGreaterThanOrEqual(50);
    expect(report.tiles.map(tile => tile.slot).sort()).toEqual(state.tiles.map(tile => tile.slot).sort());
    expect(report.tiles.filter(tile => tile.kind === 'face').length).toBe(state.tiles.filter(tile => tile.face !== null).length);
    expect(report.board.bottom).toBeLessThanOrEqual(report.hand.top);
    await actor.page.screenshot({ path: artifact(info, 'crowded-readable-412x924.png') });
    writeFileSync(artifact(info, 'crowded-readable-412x924.json'), JSON.stringify(report, null, 2) + '\n');
    const scroll = actor.page.locator('.readable-board-scroll');
    await scroll.evaluate(element => { element.scrollTop = element.scrollHeight; });
    await actor.page.screenshot({ path: artifact(info, 'crowded-readable-scrolled-412x924.png') });
    const offset = await scroll.evaluate(element => element.scrollTop);
    await actor.page.waitForTimeout(200);
    expect(await scroll.evaluate(element => element.scrollTop)).toBe(offset);

    await actor.page.setViewportSize({ width: 360, height: 800 });
    await expect(actor.page.locator('#readable-board')).toBeVisible();
    const narrow = await readableMeasurements(actor.page);
    expect(narrow.faceMinimum[0]).toBeGreaterThanOrEqual(32);
    expect(narrow.faceMinimum[1]).toBeGreaterThanOrEqual(40);
    expect(narrow.board.bottom).toBeLessThanOrEqual(narrow.hand.top);
    await actor.page.screenshot({ path: artifact(info, 'crowded-readable-360x800.png') });
    writeFileSync(artifact(info, 'crowded-readable-360x800.json'), JSON.stringify(narrow, null, 2) + '\n');
    await confirmOne(actor);
    expect(actor.errors).toEqual([]);
  } finally {
    if (!actor.page.isClosed()) await context.close();
  }
});

test('readable manual dealing keeps the real 3D ceremony and restores ordinary desktop and invalid-flag views', async ({ browser, baseURL }, info) => {
  test.setTimeout(90_000);
  const context = await browser.newContext({ viewport, deviceScaleFactor: 1, hasTouch: true, isMobile: true });
  const entry = new URL(baseURL ?? 'http://127.0.0.1:5114/autotable/');
  entry.search = new URLSearchParams({
    mobileLayout: 'readable', variant: 'changsha', dealMode: 'manual',
    botCount: '3', botDifficulty: 'Easy', handCount: '1', seed: '4100', seat: '0',
  }).toString();
  const actor = await newActor(browser, baseURL, info, 'readable-manual', context, entry.href);
  try {
    await applyRoom(actor, 3, 0, 'manual');
    await closeLobby(actor);
    await expect.poll(async () => (await probe(actor)).turn?.phase).toBe('RollingDice');
    await expect(actor.page.locator('#mobile-hand-dock #roll-dice')).toBeVisible();
    await expect(actor.page.locator('#readable-board')).toBeHidden();
    await expect(actor.page.locator('#main')).toHaveCSS('visibility', 'visible');
    expect(await actor.page.locator('#main').evaluate(element => element instanceof HTMLElement && element.inert)).toBe(false);
    const start = actor.sent.length;
    await actor.page.locator('#mobile-hand-dock #roll-dice').tap();
    const take = actor.page.locator('#mobile-hand-dock #pickup-take-btn');
    await expect(take).toBeVisible();
    await expect(take).toBeEnabled();
    await take.tap();
    await expect.poll(async () => (await probe(actor)).handCount).toBe(4);
    await expect(actor.page.locator('#mobile-hand-dock .mobile-hand-tile')).toHaveCount(4);
    await expect(actor.page.locator('.mobile-hand-discard')).toBeDisabled();
    await expect(actor.page.locator('#readable-board')).toBeHidden();
    expect(actor.sent.slice(start).flatMap(frame => frame.entries ?? []).filter(([kind]) => kind === 'discard')).toEqual([]);
    await actor.page.screenshot({ path: artifact(info, 'readable-manual-partial-412x924.png') });
    await driveManualCeremony([actor]);
    await expect(actor.page.locator('#readable-board')).toBeVisible();
    await expect(actor.page.locator('#mobile-hand-dock .mobile-hand-tile')).toHaveCount(14);
    expect(new URL(actor.page.url()).searchParams.get('mobileLayout')).toBe('readable');

    await actor.page.setViewportSize({ width: 1280, height: 900 });
    await expect(actor.page.locator('#readable-board')).toBeHidden();
    await expect(actor.page.locator('#mobile-hand-dock')).toHaveCount(0);
    await expect(actor.page.locator('#main')).toHaveCSS('visibility', 'visible');
    expect(await actor.page.locator('#main').evaluate(element => element instanceof HTMLElement && element.inert)).toBe(false);
    await actor.page.setViewportSize(viewport);
    await expect(actor.page.locator('#readable-board')).toBeVisible();
    await actor.page.evaluate(() => {
      const url = new URL(location.href);
      url.searchParams.append('mobileLayout', 'readable');
      history.replaceState(history.state, '', url);
    });
    await expect(actor.page.locator('#readable-board')).toBeHidden();
    await expect(actor.page.locator('#mobile-hand-dock')).toHaveCount(0);
    await expect(actor.page.locator('#main')).toHaveCSS('visibility', 'visible');
    expect(actor.errors).toEqual([]);
  } finally {
    if (!actor.page.isClosed()) await context.close();
  }
});

test('a real relay room ignores readable without changing its camera or renderer flight', async ({ browser, baseURL }, info) => {
  test.setTimeout(60_000);
  const context = await browser.newContext({ viewport, deviceScaleFactor: 1, hasTouch: true, isMobile: true });
  const entry = new URL(baseURL ?? 'http://127.0.0.1:5114/autotable/');
  entry.search = new URLSearchParams({
    gameId: `readable-relay-${Date.now().toString(36)}`, variant: 'four_player', seat: '0', mobileLayout: 'readable',
  }).toString();
  const actor = await newActor(browser, baseURL, info, 'readable-relay', context, entry.href);
  try {
    await closeLobby(actor);
    await expect.poll(() => actor.page.evaluate(() => {
      const game = (window as Window & { game?: Game }).game;
      return game?.client.connected() && game.client.connectionMode === 'relay'
        && game.world.conditions.gameType === 'FOUR_PLAYER';
    })).toBe(true);
    await expect(actor.page.locator('body')).toHaveAttribute('data-scene-effects-ready', 'true');
    const camera = () => actor.page.evaluate(() => {
      const game = (window as Window & { game?: Game }).game;
      if (!game) throw new Error('Game shell is not ready');
      const view = Reflect.get(game, 'mainView') as { camera: Camera };
      return {
        position: view.camera.position.toArray(), quaternion: view.camera.quaternion.toArray(),
        projection: view.camera.projectionMatrix.toArray(),
      };
    });
    const before = await camera();
    await expect(actor.page.locator('#readable-board')).toBeHidden();
    await expect(actor.page.locator('#mobile-hand-dock')).toHaveCount(0);
    await expect(actor.page.locator('#main')).toHaveCSS('visibility', 'visible');
    expect(await actor.page.locator('#main').evaluate(element => element instanceof HTMLElement && element.inert)).toBe(false);
    await actor.page.evaluate(() => {
      const url = new URL(location.href);
      url.searchParams.delete('mobileLayout');
      history.replaceState(history.state, '', url);
    });
    expect(await camera()).toEqual(before);
    expect(await actor.page.evaluate(() => performance.getEntriesByType('resource')
      .some(resource => /renderer-webgl2[.]/.test(resource.name)))).toBe(false);
    expect(actor.errors).toEqual([]);
  } finally {
    if (!actor.page.isClosed()) await context.close();
  }
});
