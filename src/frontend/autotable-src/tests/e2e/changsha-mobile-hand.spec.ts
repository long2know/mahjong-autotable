import { test, expect, type Page } from '@playwright/test';
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname } from 'node:path';
import { newActor, applyRoom, closeLobby, dismissPrompts, probe, driveManualCeremony } from './_lobby-repair';
import { mobileGeometry } from './_mobile-geometry';

type Frame = { type?: string; entries?: Array<[string, string | number, { slotName?: string; tileId?: number } | null]> };
type Bounds = { left: number; top: number; right: number; bottom: number };
const overlap = (a: Bounds, b: Bounds): boolean =>
  Math.min(a.right, b.right) > Math.max(a.left, b.left)
  && Math.min(a.bottom, b.bottom) > Math.max(a.top, b.top);

function saveEvidence(filename: string, value: unknown): void {
  mkdirSync(dirname(filename), { recursive: true });
  writeFileSync(filename, JSON.stringify(value, null, 2));
}

async function oneHand(page: Page): Promise<void> {
  // Card radios are visually hidden; click their real visible label, not a forced input.
  await page.locator('#lobby-hand-count-fieldset label:has(input[value="1"])').click();
  await expect(page.locator('input[name="lobby-hand-count"][value="1"]')).toBeChecked();
}

interface TableSnapshot {
  wire: Array<[string | number, unknown]>;
  slots: Array<{ id: number; slot: string; occupant: number | undefined; sent: boolean }>;
}

async function snapshot(page: Page): Promise<TableSnapshot> {
  return page.evaluate(() => {
    const g = (window as unknown as { game: {
      client: { things: { entries(): Iterable<[string | number, unknown]> } };
      world: { things: Map<number, { slot: { name: string; thing?: { index: number } }; sent: boolean }> };
    } }).game;
    return {
      wire: [...g.client.things.entries()],
      slots: [...g.world.things].map(([id, tile]) => ({ id, slot: tile.slot.name, occupant: tile.slot.thing?.index, sent: tile.sent })),
    };
  });
}

async function viewMode(page: Page, perspective: boolean): Promise<void> {
  await page.getByTestId('settings-button').click();
  await page.getByTestId('settings-tab-display').click();
  await page.getByTestId('settings-perspective-toggle').setChecked(perspective);
  await page.getByTestId('settings-close').click();
  await expect.poll(async () => (await mobileGeometry(page)).camera).toBe(perspective ? 'PerspectiveCamera' : 'OrthographicCamera');
}

function assertLayout(layout: Awaited<ReturnType<typeof mobileGeometry>>, own = true): void {
  expect(layout.canvas.width / layout.width).toBeGreaterThan(0.98);
  expect(layout.canvas.height / layout.height).toBeGreaterThan(0.98);
  expect(layout.pageWidth).toBeLessThanOrEqual(layout.width);
  expect(layout.pageHeight).toBeLessThanOrEqual(layout.height);
  expect(layout.table, 'the actual table mesh must be included in the fit').not.toBeNull();
  for (const r of [...layout.meshes, layout.table!]) {
    expect(r.left, JSON.stringify(r)).toBeGreaterThanOrEqual(0);
    expect(r.right, JSON.stringify(r)).toBeLessThanOrEqual(layout.width);
    expect(r.top, JSON.stringify(r)).toBeGreaterThanOrEqual(0);
    expect(r.bottom, JSON.stringify(r)).toBeLessThanOrEqual(layout.height);
  }
  for (const id of ['new-game', 'lobby-toggle', 'settings-button', 'settings-toggle', 'move-log-toggle']) {
    const r = layout.chrome[id];
    if (!r && id === 'move-log-toggle' && layout.width > 900) continue;
    expect(r, id).toBeTruthy();
    expect(r.hit, `${id} must receive an ordinary press`).toBe(true);
    expect(r.left, id).toBeGreaterThanOrEqual(0);
    expect(r.top, id).toBeGreaterThanOrEqual(0);
    expect(r.right, id).toBeLessThanOrEqual(layout.width);
    expect(r.bottom, id).toBeLessThanOrEqual(layout.height);
  }
  if (!own) {
    expect(layout.tiles).toHaveLength(0);
    expect(layout.tray).toBe(false);
    return;
  }
  expect(layout.tiles.length).toBeGreaterThanOrEqual(11);
  expect(new Set(layout.tiles.map(tile => tile.id)).size).toBe(layout.tiles.length);
  if (layout.tray) expect(layout.ownMeshCount, 'the tray replaces, never duplicates, the own 3D hand').toBe(0);
  for (const tile of layout.tiles) {
    expect(tile.hit, `tile ${tile.id} body center must be hittable`).toBe(true);
    expect(tile.width).toBeGreaterThanOrEqual(layout.tray ? 44 : 30);
    expect(tile.height).toBeGreaterThanOrEqual(44);
    for (const id of ['bot-banner', 'turn-banner', 'chat-panel', 'pickup-hud']) {
      const r = layout.chrome[id];
      if (r) expect(overlap(tile, r), `${id} covers tile ${tile.id}`).toBe(false);
    }
  }
  expect(overlap(layout.chrome['turn-banner'], layout.chrome['lobby-toggle']), 'lobby does not cover turn text').toBe(false);
}

test('real private table fits projected geometry and touch hand across runtime rotations', async ({ browser, baseURL }, testInfo) => {
  test.setTimeout(90_000); // Twelve real camera/viewport combinations in one unchanged hand.
  const context = await browser.newContext({ viewport: { width: 390, height: 844 }, hasTouch: true, isMobile: true, deviceScaleFactor: 1 });
  try {
    const actor = await newActor(browser, baseURL, testInfo, 'mobile-fit', context);
    await oneHand(actor.page);
    await applyRoom(actor, 3, 0, 'auto');
    await closeLobby(actor);
    await expect.poll(async () => (await probe(actor)).handCount).toBe(14);
    await expect(actor.page.getByTestId('hand-tile')).toHaveCount(14);
    await expect(actor.page.locator('#bot-banner .bot-banner-summary')).toHaveText('1 human · 3 bots · 0 open');
    const reports = [];
    for (const [width, height] of [[390, 844], [360, 800], [844, 390], [780, 1180], [820, 1180], [1280, 900]]) {
      await actor.page.setViewportSize({ width, height });
      for (const perspective of [true, false]) {
        await viewMode(actor.page, perspective);
        const layout = await mobileGeometry(actor.page);
        assertLayout(layout);
        reports.push(layout);
        const name = `${width}x${height}-${perspective ? 'perspective' : 'flat'}`;
        const image = testInfo.outputPath(`${name}.png`);
        await actor.page.screenshot({ path: image });
        await testInfo.attach(name, { path: image, contentType: 'image/png' });
      }
    }
    await testInfo.attach('actual-mesh-and-hand-bounds', { body: JSON.stringify(reports, null, 2), contentType: 'application/json' });
    saveEvidence(testInfo.outputPath('mesh-measurements.json'), reports);
    expect(actor.errors).toEqual([]);
  } finally { await context.close(); }
});

test('sorting persists across reload without gameplay UPDATEs, and a real tap discards the displayed ID', async ({ browser, baseURL }, testInfo) => {
  test.setTimeout(60_000); // Two DPR-2 cold starts plus the authoritative discard round trip.
  const context = await browser.newContext({ viewport: { width: 360, height: 800 }, hasTouch: true, isMobile: true, deviceScaleFactor: 2 });
  const sent: Frame[] = [], received: Frame[] = [];
  try {
    const actor = await newActor(browser, baseURL, testInfo, 'mobile-sort-tap', context);
    await oneHand(actor.page);
    actor.page.on('websocket', socket => {
      socket.on('framesent', e => { try { sent.push(JSON.parse(String(e.payload)) as Frame); } catch { /* non-JSON */ } });
      socket.on('framereceived', e => { try { received.push(JSON.parse(String(e.payload)) as Frame); } catch { /* non-JSON */ } });
    });
    await applyRoom(actor, 3, 0, 'auto');
    await closeLobby(actor);
    await expect(actor.page.getByTestId('hand-tile')).toHaveCount(14);
    const initial = await snapshot(actor.page);
    const initialTiles = (await mobileGeometry(actor.page)).tiles;
    const start = sent.length;
    await actor.page.getByTestId('own-hand-tray').getByRole('button', { name: 'Hand order', exact: true }).click();
    await actor.page.getByRole('option', { name: 'Pairs / triples first', exact: true }).click();
    await expect.poll(async () => (await mobileGeometry(actor.page)).tiles.map(tile => tile.id)).toEqual(
      initialTiles.slice().sort((a, b) => {
        const count = (face: number): number => initialTiles.filter(tile => tile.face === face).length;
        return Number(count(b.face) > 1) - Number(count(a.face) > 1) || a.face - b.face || a.id - b.id;
      }).map(tile => tile.id));
    expect(await snapshot(actor.page)).toEqual(initial);
    expect(sent.slice(start).flatMap(frame => frame.entries ?? []).filter(([kind]) =>
      ['things', 'slots', 'discard', 'claim', 'match', 'seats'].includes(kind))).toEqual([]);
    await actor.page.getByTestId('settings-button').click();
    await actor.page.getByTestId('settings-tab-display').click();
    await expect(actor.page.getByTestId('settings-hand-sort')).toHaveValue('groups');
    await actor.page.getByTestId('settings-close').click();
    await actor.page.reload({ waitUntil: 'domcontentloaded' });
    await dismissPrompts(actor);
    await closeLobby(actor);
    await expect(actor.page.getByTestId('hand-sort')).toHaveValue('groups');
    await expect(actor.page.getByTestId('hand-tile')).toHaveCount(14);
    expect((await snapshot(actor.page)).wire).toEqual(initial.wire);
    // A sticky mobile hover must not rotate the whole 44px target off-screen.
    await actor.page.getByTestId('settings-button').hover();
    const layout = await mobileGeometry(actor.page);
    assertLayout(layout);
    expect(layout.dpr).toBe(2);
    const chosen = layout.tiles[4];
    const mark = sent.length;
    await actor.page.locator(`[data-testid="hand-tile"][data-tile-id="${chosen.id}"]`).tap();
    await expect.poll(() => sent.slice(mark).flatMap(frame => frame.entries ?? []).filter(([kind]) => kind === 'discard')).toHaveLength(1);
    expect(sent.slice(mark).flatMap(frame => frame.entries ?? []).find(([kind]) => kind === 'discard')?.[2]?.tileId).toBe(chosen.id);
    await expect.poll(() => received.flatMap(frame => frame.entries ?? []).some(([kind, id, info]) =>
      kind === 'things' && id === chosen.id && info?.slotName?.startsWith('discard.'))).toBe(true);
    await expect.poll(async () => (await probe(actor)).handIds).not.toContain(chosen.id);
    await testInfo.attach('tap-wire-server-confirmation', {
      body: JSON.stringify({ chosen, layout, sent: sent.slice(mark).filter(frame => frame.type === 'UPDATE'),
        confirmed: received.flatMap(frame => frame.entries ?? []).filter(([kind, id]) => kind === 'things' && id === chosen.id) }, null, 2),
      contentType: 'application/json',
    });
    saveEvidence(testInfo.outputPath('tap-confirmation.json'), {
      chosen, layout, sent: sent.slice(mark).filter(frame => frame.type === 'UPDATE'),
      confirmed: received.flatMap(frame => frame.entries ?? []).filter(([kind, id]) => kind === 'things' && id === chosen.id),
    });
    expect(actor.errors).toEqual([]);
  } finally { await context.close(); }
});

test('desktop sorted mesh raycasts the displayed ID and intentional zoom is not fitted away', async ({ browser, baseURL }, testInfo) => {
  const context = await browser.newContext({ viewport: { width: 1280, height: 900 }, deviceScaleFactor: 1 });
  try {
    const actor = await newActor(browser, baseURL, testInfo, 'desktop-sort', context);
    await oneHand(actor.page);
    await applyRoom(actor, 3, 0, 'auto');
    await closeLobby(actor);
    await expect.poll(async () => (await probe(actor)).handCount).toBe(14);
    await actor.page.getByTestId('settings-button').click();
    await actor.page.getByTestId('settings-tab-display').click();
    await actor.page.getByTestId('settings-panel-display').getByRole('button', { name: 'Hand order', exact: true }).click();
    await actor.page.getByRole('option', { name: 'Pairs / triples first', exact: true }).click();
    await actor.page.getByTestId('settings-close').click();
    const initial = await mobileGeometry(actor.page);
    assertLayout(initial);
    expect(initial.tray).toBe(false);
    const width = initial.table!.right - initial.table!.left;
    await actor.page.keyboard.down('z');
    await expect.poll(async () => {
      const table = (await mobileGeometry(actor.page)).table!;
      return table.right - table.left;
    }).toBeGreaterThan(width * 1.05);
    await actor.page.keyboard.up('z');
    await expect.poll(async () => {
      const table = (await mobileGeometry(actor.page)).table!;
      return Math.abs(table.right - table.left - width);
    }).toBeLessThan(1);
    const chosen = (await mobileGeometry(actor.page)).tiles[4];
    await actor.page.mouse.move(chosen.x, chosen.y);
    await expect.poll(() => actor.page.evaluate(() =>
      (window as unknown as { game: { world: { hovered?: { index: number } } } }).game.world.hovered?.index,
    )).toBe(chosen.id);
    const start = actor.sent.length;
    await actor.page.mouse.click(chosen.x, chosen.y);
    await expect.poll(() => actor.sent.slice(start).flatMap(frame => frame.entries ?? [])
      .filter(([kind]) => kind === 'discard').map(([, , value]) => value?.tileId)).toEqual([chosen.id]);
    await expect.poll(async () => (await probe(actor)).handIds).not.toContain(chosen.id);
    saveEvidence(testInfo.outputPath('desktop-click-confirmation.json'), { chosen, initial, sent: actor.sent.slice(start) });
    expect(actor.errors).toEqual([]);
  } finally { await context.close(); }
});

for (const seat of [1, 2, 3, -1]) {
  test(`real rotated seat ${seat} fits in both views without sorting anyone else's hand`, async ({ browser, baseURL }, testInfo) => {
    const context = await browser.newContext({ viewport: { width: 390, height: 844 }, hasTouch: true, isMobile: true, deviceScaleFactor: 1 });
    try {
      const actor = await newActor(browser, baseURL, testInfo, `seat-${seat}`, context);
      await oneHand(actor.page);
      await applyRoom(actor, seat === -1 ? 4 : 3, seat, 'auto');
      await closeLobby(actor);
      if (seat !== -1) await expect.poll(async () => (await probe(actor)).handCount).toBeGreaterThanOrEqual(13);
      for (const perspective of [true, false]) {
        await viewMode(actor.page, perspective);
        const layout = await mobileGeometry(actor.page);
        assertLayout(layout, seat !== -1);
        await testInfo.attach(`seat-${seat}-${perspective}`, { body: JSON.stringify(layout), contentType: 'application/json' });
        saveEvidence(testInfo.outputPath(`seat-${seat}-${perspective}.json`), layout);
      }
      expect(actor.errors).toEqual([]);
    } finally { await context.close(); }
  });
}

test('ordinary Quick Match manual pickup remains visible above the growing hand tray', async ({ browser, baseURL }, testInfo) => {
  const context = await browser.newContext({ viewport: { width: 390, height: 844 }, hasTouch: true, isMobile: true, deviceScaleFactor: 1 });
  try {
    const actor = await newActor(browser, baseURL, testInfo, 'manual-hand-tray', context);
    await oneHand(actor.page);
    await actor.page.locator('input[name="lobby-deal-mode"][value="manual"]').check();
    await actor.page.getByTestId('lobby-quick-match').click();
    await actor.page.waitForURL(url => url.searchParams.has('gameId'), { waitUntil: 'domcontentloaded' });
    await closeLobby(actor);
    await driveManualCeremony([actor]);
    await expect(actor.page.getByTestId('hand-tile')).toHaveCount(14);
    assertLayout(await mobileGeometry(actor.page));
    expect(actor.errors).toEqual([]);
  } finally { await context.close(); }
});
