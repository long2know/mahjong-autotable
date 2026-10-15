import { test, expect, type Browser, type BrowserContext, type Page, type TestInfo } from '@playwright/test';
import { applyRoom, closeLobby, dismissPrompts, driveManualCeremony, newActor, probe, type Actor } from './_lobby-repair';
import { mobileGeometry } from './_mobile-geometry';

type WireEntry = [string, string | number, { slotName?: string; tileId?: number } | null];
interface Frame { type?: string | number; entries?: WireEntry[] }

async function startTable(
  browser: Browser, baseURL: string | undefined, testInfo: TestInfo,
  context: BrowserContext, manual = false, seed = 4100,
): Promise<Actor> {
  const url = new URL(baseURL ?? 'http://127.0.0.1:8080/autotable/');
  url.searchParams.set('mobileLayout', 'hybrid');
  const actor = await newActor(browser, baseURL, testInfo, 'portrait-hybrid', context, url.href);
  await actor.page.locator('#lobby-hand-count-fieldset label:has(input[value="1"])').click();
  await actor.page.locator('#lobby-advanced summary').click();
  await actor.page.locator('#lobby-seed').fill(String(seed));
  await actor.page.locator('#lobby-bot-difficulty-fieldset label:has(input[value="Easy"])').click();
  await applyRoom(actor, 3, 0, manual ? 'manual' : 'auto');
  await closeLobby(actor);
  expect(new URL(actor.page.url()).searchParams.getAll('mobileLayout')).toEqual(['hybrid']);
  await expect(actor.page.locator('body')).toHaveAttribute('data-scene-effects-ready', 'true');
  await expect(actor.page.locator('#mobile-hand-dock')).toBeVisible();
  if (!manual) {
    await expect(actor.page.locator('#mobile-hand-dock .mobile-hand-tile')).toHaveCount(14);
    await expect(actor.page.locator('#mobile-hand-dock .mobile-hand-tile').first(),
      'ordinary AwaitingDiscard must be playable even without Hu/Kong metadata').toBeEnabled();
  }
  return actor;
}

function discards(frames: Frame[]): WireEntry[] {
  return frames.flatMap(frame => frame.entries ?? []).filter(([kind]) => kind === 'discard');
}

function recordGameFrames(page: Page): { sent: Frame[]; received: Frame[] } {
  const sent: Frame[] = [], received: Frame[] = [];
  const record = (payload: string | Buffer, into: Frame[]): void => {
    const text = String(payload);
    if (!text.startsWith('{')) return;
    into.push(JSON.parse(text) as Frame);
  };
  page.on('websocket', socket => {
    if (new URL(socket.url()).pathname !== '/autotable/ws') return;
    socket.on('framesent', event => record(event.payload, sent));
    socket.on('framereceived', event => record(event.payload, received));
  });
  return { sent, received };
}

async function hiddenOwnHand(page: Page): Promise<{ rendered: number; raycast: number; own: number[]; melds: number[] }> {
  const geometry = await mobileGeometry(page);
  const state = await page.evaluate(() => {
    const game = (window as unknown as { game: {
      client: { seat: number | null };
      world: {
        ownHandPresentation(): { tiles: ReadonlyArray<{ id: number }> };
        toSelect(): Array<{ id: number }>;
        things: Map<number, { slot: { group: string; seat: number | null } }>;
      };
    } }).game;
    return {
      own: game.world.ownHandPresentation().tiles.map(tile => tile.id),
      raycast: game.world.toSelect().filter(item => {
        const thing = game.world.things.get(item.id);
        return thing?.slot.group === 'hand' && thing.slot.seat === game.client.seat;
      }).length,
    };
  });
  return { rendered: geometry.ownMeshCount, ...state,
    melds: geometry.meshes.filter(mesh => mesh.slot.startsWith('meld.') && mesh.slot.endsWith('@' + geometry.seat)).map(mesh => mesh.id) };
}

async function tapConfirm(page: Page, index = 0): Promise<number> {
  const tile = page.locator('#mobile-hand-dock .mobile-hand-tile:not([disabled])').nth(index);
  await expect(tile).toBeVisible();
  const id = Number(await tile.getAttribute('data-tile-id'));
  await tile.tap();
  const confirm = page.locator('#mobile-hand-dock .mobile-hand-discard');
  await expect(confirm).toBeEnabled();
  await expect(confirm).toHaveAttribute('data-tile-id', String(id));
  await confirm.tap();
  return id;
}

test('real human tile taps select/cancel only, then one explicit confirmation discards one physical ID', async ({ browser, baseURL }, testInfo) => {
  test.setTimeout(90_000);
  const context = await browser.newContext({ viewport: { width: 412, height: 915 }, hasTouch: true, isMobile: true, deviceScaleFactor: 1 });
  try {
    const url = new URL(baseURL ?? 'http://127.0.0.1:8080/autotable/');
    url.searchParams.set('mobileLayout', 'hybrid');
    const actor = await newActor(browser, baseURL, testInfo, 'hybrid-confirm', context, url.href);
    // Attach before the real Apply navigation so both command and server receipt
    // are observed on the actual game socket, not a local synthetic subscriber.
    const frames = recordGameFrames(actor.page);
    await actor.page.locator('#lobby-hand-count-fieldset label:has(input[value="1"])').click();
    await actor.page.locator('#lobby-advanced summary').click();
    await actor.page.locator('#lobby-seed').fill('4100');
    await applyRoom(actor, 3, 0, 'auto');
    await closeLobby(actor);
    await expect(actor.page.locator('body')).toHaveAttribute('data-scene-effects-ready', 'true');
    const dock = actor.page.locator('#mobile-hand-dock');
    await expect(dock).toBeVisible();
    await expect(dock.locator('.mobile-hand-tile')).toHaveCount(14);
    expect(new URL(actor.page.url()).searchParams.getAll('mobileLayout')).toEqual(['hybrid']);
    const initial = await hiddenOwnHand(actor.page);
    expect(initial.rendered).toBe(0);
    expect(initial.raycast).toBe(0);
    expect(initial.own).toHaveLength(14);
    const tile = dock.locator('.mobile-hand-tile').nth(4);
    await expect(tile, 'the shared authoritative discard gate must enable ordinary turns').toBeEnabled();
    const id = Number(await tile.getAttribute('data-tile-id'));
    expect(initial.own).toContain(id);
    const start = frames.sent.length;
    await tile.tap();
    await expect(tile).toHaveAttribute('aria-pressed', 'true');
    expect(discards(frames.sent.slice(start))).toEqual([]);
    await tile.tap();
    await expect(tile).toHaveAttribute('aria-pressed', 'false');
    expect(discards(frames.sent.slice(start))).toEqual([]);
    await tile.tap();
    const confirm = dock.locator('.mobile-hand-discard');
    await expect(confirm).toHaveAttribute('aria-label', /^Confirm discard of /);
    const selection = testInfo.outputPath('hybrid-selected.png');
    await actor.page.screenshot({ path: selection });
    await testInfo.attach('hybrid-selected', { path: selection, contentType: 'image/png' });
    const button = await confirm.boundingBox();
    expect(button).not.toBeNull();
    await actor.page.touchscreen.tap(button!.x + button!.width / 2, button!.y + button!.height / 2);
    await actor.page.touchscreen.tap(button!.x + button!.width / 2, button!.y + button!.height / 2);
    await expect.poll(() => discards(frames.sent.slice(start))).toHaveLength(1);
    expect(discards(frames.sent.slice(start))[0][2]?.tileId).toBe(id);
    await expect.poll(() => frames.received.flatMap(frame => frame.entries ?? []).some(([kind, key, value]) =>
      kind === 'things' && key === id && value?.slotName?.startsWith('discard.'))).toBe(true);
    await expect.poll(async () => (await probe(actor)).handIds).not.toContain(id);
    await expect(dock).toHaveAttribute('aria-busy', 'false');
    await expect(dock.locator('.mobile-hand-tile[aria-pressed="true"]')).toHaveCount(0);
    const confirmed = testInfo.outputPath('hybrid-server-confirmed.png');
    await actor.page.screenshot({ path: confirmed });
    await testInfo.attach('hybrid-server-confirmed', { path: confirmed, contentType: 'image/png' });
    await testInfo.attach('authoritative-discard-receipt', {
      body: JSON.stringify({ id, initial, sent: discards(frames.sent.slice(start)),
        received: frames.received.flatMap(frame => frame.entries ?? []).filter(([kind, key]) => kind === 'things' && key === id) }, null, 2),
      contentType: 'application/json',
    });
    expect(actor.errors).toEqual([]);
  } finally { await context.close(); }
});

test('real portrait hand restores scene/raycast and original HUD on landscape, off, spectator and relay', async ({ browser, baseURL }, testInfo) => {
  test.setTimeout(90_000);
  const context = await browser.newContext({ viewport: { width: 412, height: 915 }, hasTouch: true, isMobile: true, deviceScaleFactor: 1 });
  try {
    const actor = await startTable(browser, baseURL, testInfo, context);
    await expect(actor.page.locator('#mobile-hand-dock .mobile-hand-tile')).toHaveCount(14);
    await actor.page.locator('#mobile-hand-dock .mobile-hand-tile').first().tap();
    await actor.page.setViewportSize({ width: 915, height: 412 });
    await expect(actor.page.locator('#mobile-hand-dock')).toHaveCount(0);
    await expect.poll(async () => (await hiddenOwnHand(actor.page)).rendered).toBe(14);
    await expect.poll(async () => (await hiddenOwnHand(actor.page)).raycast).toBe(14);
    expect(await actor.page.locator('#pickup-hud').evaluate(node => node.parentElement?.id)).not.toBe('mobile-hand-dock');
    await actor.page.setViewportSize({ width: 412, height: 915 });
    await expect(actor.page.locator('#mobile-hand-dock')).toBeVisible();
    await expect(actor.page.locator('#mobile-hand-dock .mobile-hand-tile[aria-pressed="true"]')).toHaveCount(0);
    // Only the opt-in presentation flag changes; this does not mutate game state.
    await actor.page.evaluate(() => {
      const url = new URL(location.href);
      url.searchParams.delete('mobileLayout');
      history.replaceState(null, '', url);
    });
    await expect(actor.page.locator('#mobile-hand-dock')).toHaveCount(0);
    await expect.poll(async () => (await hiddenOwnHand(actor.page)).rendered).toBe(14);
    const spectator = new URL(actor.page.url());
    spectator.searchParams.set('mobileLayout', 'hybrid');
    spectator.searchParams.set('seat', '-1');
    spectator.searchParams.delete('join');
    spectator.searchParams.delete('new');
    spectator.searchParams.set('gameId', `hybrid-spectator-${Date.now()}`);
    spectator.searchParams.set('botCount', '4');
    await actor.page.goto(spectator.href, { waitUntil: 'domcontentloaded' });
    await dismissPrompts(actor);
    await closeLobby(actor);
    await expect.poll(async () => {
      const state = await probe(actor);
      return { connected: state.connected, seat: state.seat };
    }).toEqual({ connected: true, seat: null });
    await expect(actor.page.locator('#mobile-hand-dock')).toHaveCount(0);
    const relay = new URL(baseURL ?? 'http://127.0.0.1:8080/autotable/');
    relay.search = new URLSearchParams({
      mobileLayout: 'hybrid', gameId: `hybrid-relay-${Date.now()}`, variant: 'four-player', seat: '0',
    }).toString();
    await actor.page.goto(relay.href, { waitUntil: 'domcontentloaded' });
    await dismissPrompts(actor);
    await closeLobby(actor);
    await expect(actor.page.locator('body')).toHaveAttribute('data-scene-effects-ready', 'true');
    await expect(actor.page.locator('#mobile-hand-dock')).toHaveCount(0);
    expect(actor.errors).toEqual([]);
  } finally { await context.close(); }
});

test('real manual dealing uses the original Roll/Take controls and never discards a partial hand', async ({ browser, baseURL }, testInfo) => {
  test.setTimeout(90_000);
  const context = await browser.newContext({ viewport: { width: 412, height: 915 }, hasTouch: true, isMobile: true, deviceScaleFactor: 1 });
  try {
    const actor = await startTable(browser, baseURL, testInfo, context, true);
    const dock = actor.page.locator('#mobile-hand-dock');
    const start = actor.sent.length;
    await expect(dock.locator('#roll-dice')).toBeVisible();
    await dock.locator('#roll-dice').tap();
    await expect(dock.locator('#pickup-take-btn')).toBeVisible();
    await expect(dock.locator('#pickup-take-btn')).toBeEnabled();
    await dock.locator('#pickup-take-btn').tap();
    await expect.poll(async () => (await probe(actor)).handCount).toBe(4);
    await expect(dock.locator('.mobile-hand-tile')).toHaveCount(4);
    for (const tile of await dock.locator('.mobile-hand-tile').all()) await expect(tile).toBeDisabled();
    await expect(dock.locator('.mobile-hand-discard')).toBeDisabled();
    expect(discards(actor.sent.slice(start))).toEqual([]);
    const partial = testInfo.outputPath('hybrid-manual-partial.png');
    await actor.page.screenshot({ path: partial });
    await testInfo.attach('hybrid-manual-partial', { path: partial, contentType: 'image/png' });
    await driveManualCeremony([actor]);
    await expect(dock.locator('.mobile-hand-tile')).toHaveCount(14);
    expect(actor.errors).toEqual([]);
  } finally { await context.close(); }
});

test('a real claimed meld stays rendered while only the local concealed hand moves into the dock', async ({ browser, baseURL }, testInfo) => {
  test.setTimeout(150_000);
  const context = await browser.newContext({ viewport: { width: 412, height: 915 }, hasTouch: true, isMobile: true, deviceScaleFactor: 1 });
  try {
    const actor = await startTable(browser, baseURL, testInfo, context, false, 12345);
    const deadline = Date.now() + 60_000;
    let claimed: 'Pung' | 'Chow' | null = null;
    while (Date.now() < deadline && !claimed) {
      const pung = actor.page.locator('#mobile-hand-dock .ferro-claim-badge-pung');
      const chow = actor.page.locator('#mobile-hand-dock .ferro-claim-badge-chow');
      if (await pung.isVisible() && await pung.isEnabled()) {
        await pung.tap();
        claimed = 'Pung';
      } else if (await chow.isVisible() && await chow.isEnabled()) {
        await chow.tap();
        const chooser = actor.page.locator('.rule-action-dialog[open] .rule-action-choice').first();
        if (await chooser.isVisible()) await chooser.tap();
        claimed = 'Chow';
      } else {
        const pass = actor.page.locator('#mobile-hand-dock .ferro-claim-pass');
        const tiles = actor.page.locator('#mobile-hand-dock .mobile-hand-tile:not([disabled])');
        if (await pass.isVisible() && await pass.isEnabled()) await pass.tap();
        else if (await tiles.count() > 0) await tapConfirm(actor.page, Math.floor(await tiles.count() / 2));
        else await actor.page.waitForTimeout(150);
      }
    }
    expect(claimed, 'seed 12345 with Easy bots must reach an ordinary legal human meld control').not.toBeNull();
    await expect.poll(async () => (await hiddenOwnHand(actor.page)).melds.length).toBeGreaterThanOrEqual(3);
    const state = await hiddenOwnHand(actor.page);
    expect(state.rendered).toBe(0);
    expect(state.raycast).toBe(0);
    expect(state.own).toHaveLength(11);
    await expect(actor.page.locator('#mobile-hand-dock .mobile-hand-tile')).toHaveCount(11);
    const image = testInfo.outputPath('hybrid-public-meld.png');
    await actor.page.screenshot({ path: image });
    await testInfo.attach('hybrid-public-meld', { path: image, contentType: 'image/png' });
    await tapConfirm(actor.page);
    expect(actor.errors).toEqual([]);
  } finally { await context.close(); }
});
