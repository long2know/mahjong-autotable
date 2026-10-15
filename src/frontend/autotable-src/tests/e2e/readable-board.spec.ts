import { test, expect, type Browser, type BrowserContext, type Page, type TestInfo } from '@playwright/test';
import { writeFileSync } from 'node:fs';
import { applyRoom, closeLobby, newActor, probe, type Actor, type WireFrame } from './_lobby-repair';

interface PublicReadback {
  expected: Array<{ slot: string; seat: number; zone: 'discard' | 'meld'; face: number | null }>;
  actual: Array<{ slot: string; kind: string; face: number | null }>;
  mismatches: string[];
  context: { kind: string; seat: number | null; face: number | null; slot: string | null };
  claim: { source: number; face: number | null; chowOptionCount: number } | null;
  claimMatches: boolean;
  wall: number;
  diagnostics: {
    seat: number | null;
    claimEntries: Array<Record<string, unknown>>;
    getterContext: Record<string, unknown>;
  };
}

async function startReadable(
  browser: Browser, baseURL: string | undefined, testInfo: TestInfo,
  context: BrowserContext, seed: number,
): Promise<Actor> {
  const url = new URL(baseURL ?? 'http://127.0.0.1:8080/autotable/');
  url.searchParams.set('mobileLayout', 'readable');
  const actor = await newActor(browser, baseURL, testInfo, 'readable-public-board', context, url.href);
  await actor.page.locator('#lobby-hand-count-fieldset label:has(input[value="1"])').click();
  await actor.page.locator('#lobby-advanced summary').click();
  await actor.page.locator('#lobby-seed').fill(String(seed));
  await actor.page.locator('#lobby-bot-difficulty-fieldset label:has(input[value="Easy"])').click();
  await applyRoom(actor, 3, 0, 'auto');
  await closeLobby(actor);
  expect(new URL(actor.page.url()).searchParams.getAll('mobileLayout')).toEqual(['readable']);
  await expect(actor.page.locator('body')).toHaveAttribute('data-scene-effects-ready', 'true');
  await expect(actor.page.locator('#mobile-hand-dock .mobile-hand-tile')).toHaveCount(14);
  await expect(actor.page.locator('#readable-board')).toBeVisible();
  await expect(actor.page.locator('#readable-board .readable-board-scroll > [data-seat]')).toHaveCount(4);
  return actor;
}

function discards(frames: WireFrame[]): Array<[string, string | number, Record<string, unknown> | null]> {
  return frames.flatMap(frame => frame.entries ?? []).filter(([kind]) => kind === 'discard');
}

async function publicReadback(page: Page): Promise<PublicReadback> {
  return page.evaluate(() => {
    interface ThingEntry {
      slotName: string; face?: number | null; rotationIndex?: number;
      claimedBy?: number | null; shiftSlotName?: string | null;
    }
    interface PublicThing {
      slot: { name: string; thing: unknown };
      type: string; typeIndex: number; rotationIndex: number; hidden: boolean;
      hiddenHandle: unknown; claimedBy: number | null; shiftSlot: unknown;
    }
    interface ClaimEntry { tile: number; source: number; deadline: number; chowOptions?: number[][] }
    const game = (window as unknown as { game: {
      client: {
        seat: number | null;
        things: { entries(): Iterable<[string | number, ThingEntry]>; get(id: string | number): ThingEntry | null };
        claim: {
          get(key: string): ClaimEntry | null;
          entries(): Iterable<[string | number, ClaimEntry]>;
        };
      };
      gameUi: { mobileHand: {
        readonly publicBoardEntries: Iterable<[string | number, ThingEntry]>;
        readonly publicTileContext: Record<string, unknown>;
      } };
      world: { things: Map<number, PublicThing>; slots: Map<string, unknown> };
    } }).game;
    const validFace = (value: unknown): value is number =>
      typeof value === 'number' && Number.isInteger(value) && value >= 0 && value < 27;
    const publicEntries = new Map(game.gameUi.mobileHand.publicBoardEntries);
    const publicFace = (id: string | number, entry: ThingEntry): number | null => {
      if (entry.face !== undefined) return validFace(entry.face) ? entry.face : null;
      const revealed = publicEntries.get(id);
      const value = revealed?.slotName === entry.slotName ? revealed.face : null;
      return validFace(value) ? value : null;
    };
    const expected: PublicReadback['expected'] = [];
    let wall = 0;
    for (const [id, entry] of game.client.things.entries()) {
      if (/^wall\.\d+\.\d+@[0-3]$/.test(entry.slotName)) wall++;
      const slot = /^(discard|meld)\.(\d+)\.(\d+)@([0-3])$/.exec(entry.slotName);
      if (!slot) continue;
      const canonical = slot[1] === 'meld' && slot[3] === '4'
        ? `meld.${slot[2]}.3@${slot[4]}` : entry.slotName;
      expected.push({
        slot: canonical, seat: Number(slot[4]), zone: slot[1] as 'discard' | 'meld',
        face: publicFace(id, entry),
      });
    }
    const root = document.getElementById('readable-board')!;
    const actual = [...root.querySelectorAll<HTMLElement>('.readable-board-scroll [data-slot]')].map(tile => ({
      slot: tile.dataset.slot!, kind: tile.dataset.kind!,
      face: tile.dataset.face === undefined ? null : Number(tile.dataset.face),
    }));
    const mismatches: string[] = [];
    for (const tile of actual) {
      const source = expected.find(entry => entry.slot === tile.slot);
      if (!source) mismatches.push(`Non-public or stale DOM slot: ${tile.slot}`);
      else if (source.face !== tile.face) mismatches.push(`Face mismatch at ${tile.slot}: ${source.face} / ${tile.face}`);
      else if ((source.face !== null) !== (tile.kind === 'face')) mismatches.push(`Wrong public kind at ${tile.slot}`);
    }
    for (const tile of expected) {
      if (!actual.some(entry => entry.slot === tile.slot)) mismatches.push(`Missing public slot: ${tile.slot}`);
    }
    const strip = root.querySelector<HTMLElement>('.readable-board-context')!;
    const current = {
      kind: strip.dataset.kind ?? '',
      seat: strip.dataset.seat === undefined ? null : Number(strip.dataset.seat),
      face: strip.dataset.face === undefined ? null : Number(strip.dataset.face),
      slot: strip.dataset.publicSlot ?? null,
    };
    const rawClaims = [...game.client.claim.entries()];
    const claim = game.client.seat === null ? null : game.client.claim.get(String(game.client.seat));
    const claimEntry = claim === null ? null : game.client.things.get(claim.tile);
    const claimFace = claim !== null && claimEntry !== null ? publicFace(claim.tile, claimEntry) : null;
    const highlighted = [...root.querySelectorAll<HTMLElement>('.readable-board-scroll .readable-board-context-match')]
      .map(tile => tile.dataset.slot);
    const matchingSource = current.slot === null ? undefined : expected.find(tile =>
      tile.slot === current.slot && tile.face === current.face && tile.seat === current.seat);
    const exactHighlight = current.slot === null ? highlighted.length === 0
      : matchingSource === undefined ? highlighted.length === 0
        : highlighted.length === 1 && highlighted[0] === current.slot;
    if (!exactHighlight) mismatches.push('Context highlights are not exact public slot/source/face matches');
    const claimEntries = rawClaims.map(([key, value]) => {
      const entry = game.client.things.get(value.tile), projected = publicEntries.get(value.tile);
      const thing = game.world.things.get(value.tile);
      return {
        key, ...value, wire: entry,
        projectedFace: projected?.face === undefined ? 'missing' : projected.face,
        pose: thing === undefined ? null : {
          slot: thing.slot.name, type: thing.type, face: thing.typeIndex, rotation: thing.rotationIndex,
          hidden: thing.hidden, opaque: thing.hiddenHandle !== null, claimedBy: thing.claimedBy,
          shifted: thing.shiftSlot !== null, symmetricSlot: thing.slot.thing === thing,
          registeredSlot: entry !== null && game.world.slots.get(entry.slotName) === thing.slot,
        },
      };
    });
    return {
      expected, actual, mismatches, context: current, wall,
      claim: claim === null ? null : {
        source: claim.source, face: claimFace, chowOptionCount: (claim.chowOptions ?? []).length,
      },
      claimMatches: claim !== null && claimFace !== null && current.kind === 'claim'
        && current.seat === claim.source && current.face === claimFace && exactHighlight,
      diagnostics: {
        seat: game.client.seat, claimEntries, getterContext: game.gameUi.mobileHand.publicTileContext,
      },
    };
  });
}

async function expectCurrentClaim(actor: Actor, testInfo: TestInfo): Promise<PublicReadback> {
  let observed: PublicReadback | null = null;
  try {
    await expect.poll(async () => {
      observed = await publicReadback(actor.page);
      return observed.claimMatches;
    }, { timeout: 2_000 }).toBe(true);
  } catch (error) {
    const evidence = testInfo.outputPath('readable-claim-failure-readback.json');
    writeFileSync(evidence, JSON.stringify({
      url: actor.page.url(), observed, current: await publicReadback(actor.page),
      received: actor.received.slice(-8), consoleErrors: actor.errors,
    }, null, 2) + '\n');
    await testInfo.attach('readable-claim-failure-readback', {
      path: evidence, contentType: 'application/json',
    });
    await actor.page.screenshot({ path: testInfo.outputPath('readable-claim-failure.png') });
    throw error;
  }
  return observed!;
}

async function tapConfirm(page: Page, index = 0): Promise<{ id: number; face: number }> {
  const tile = page.locator('#mobile-hand-dock .mobile-hand-tile:not([disabled])').nth(index);
  await expect(tile).toBeVisible();
  const id = Number(await tile.getAttribute('data-tile-id'));
  const face = Number(await tile.getAttribute('data-face'));
  await tile.tap();
  const confirm = page.locator('#mobile-hand-dock .mobile-hand-discard');
  await expect(confirm).toBeEnabled();
  await expect(confirm).toHaveAttribute('data-tile-id', String(id));
  await confirm.tap();
  return { id, face };
}

async function assertSceneRestored(page: Page, expectDock: boolean): Promise<void> {
  await expect(page.locator('body')).not.toHaveClass(/readable-board-active/);
  await expect(page.locator('#readable-board')).toBeHidden();
  await expect(page.locator('#main')).toHaveCSS('visibility', 'visible');
  expect(await page.locator('#main').evaluate(node => ({
    inert: (node as HTMLElement).inert, aria: node.getAttribute('aria-hidden'),
  }))).toEqual({ inert: false, aria: null });
  if (expectDock) await expect(page.locator('#mobile-hand-dock')).toBeVisible();
  else {
    await expect(page.locator('#mobile-hand-dock')).toHaveCount(0);
    expect(await page.locator('#pickup-hud').evaluate(node => node.closest('#mobile-hand-dock'))).toBeNull();
    expect(await page.locator('#own-turn-actions').evaluate(node => node.closest('#mobile-hand-dock'))).toBeNull();
  }
}

for (const viewport of [{ width: 360, height: 640 }, { width: 412, height: 915 }]) {
  test(`real readable ${viewport.width} portrait selects, explicitly discards once, and reversibly restores 3D/HUD`, async ({ browser, baseURL }, testInfo) => {
    test.setTimeout(90_000);
    const context = await browser.newContext({ viewport, hasTouch: true, isMobile: true, deviceScaleFactor: 1, locale: 'en' });
    try {
      const actor = await startReadable(browser, baseURL, testInfo, context, 4100);
      const page = actor.page, board = page.locator('#readable-board'), dock = page.locator('#mobile-hand-dock');
      const canvas = await page.locator('#main canvas').elementHandle();
      expect(canvas).not.toBeNull();
      await expect(page.locator('#main')).toHaveCSS('visibility', 'hidden');
      expect(await page.locator('#main').evaluate(node => (node as HTMLElement).inert)).toBe(true);
      const before = await publicReadback(page), start = actor.sent.length;
      const chosen = dock.locator('.mobile-hand-tile').nth(4);
      await expect(chosen).toBeEnabled();
      const id = Number(await chosen.getAttribute('data-tile-id'));
      const face = Number(await chosen.getAttribute('data-face'));
      await chosen.tap();
      await expect(chosen).toHaveAttribute('aria-pressed', 'true');
      expect(discards(actor.sent.slice(start)), 'selection must not discard').toEqual([]);
      expect((await publicReadback(page)).actual).toEqual(before.actual);
      const confirm = dock.locator('.mobile-hand-discard');
      await expect(confirm).toHaveAttribute('data-tile-id', String(id));
      await confirm.tap();
      await expect.poll(() => discards(actor.sent.slice(start))).toHaveLength(1);
      expect(discards(actor.sent.slice(start))[0][2]?.tileId).toBe(id);
      await expect.poll(async () => (await probe(actor)).handIds).not.toContain(id);
      await expect.poll(async () => (await publicReadback(page)).actual.filter(tile =>
        tile.kind === 'face' && tile.slot.startsWith('discard.')).length)
        .toBeGreaterThan(before.actual.filter(tile => tile.kind === 'face' && tile.slot.startsWith('discard.')).length);
      await expect.poll(async () => (await publicReadback(page)).mismatches).toEqual([]);
      const receipt = await page.evaluate(tileId => {
        const client = (window as unknown as { game: { client: {
          things: { get(id: number): { slotName: string } | null };
        } } }).game.client;
        return client.things.get(tileId)?.slotName ?? null;
      }, id);
      expect(receipt).toMatch(/^discard\./);
      const publicTile = board.locator('.readable-board-scroll [data-slot]');
      expect(await publicTile.count()).toBeGreaterThan(0);
      const ownDiscard = board.locator(`.readable-board-scroll [data-slot="${receipt}"]`);
      await expect(ownDiscard).toHaveAttribute('data-kind', 'face');
      await expect(ownDiscard).toHaveAttribute('data-face', String(face));
      await ownDiscard.scrollIntoViewIfNeeded();
      const tileBounds = await ownDiscard.boundingBox();
      expect(tileBounds).not.toBeNull();
      const minimum = viewport.width >= 400 ? 40 : 32;
      expect(tileBounds!.width).toBeGreaterThanOrEqual(minimum);
      expect(tileBounds!.height).toBeGreaterThanOrEqual(minimum * 1.25);
      expect(tileBounds!.height / tileBounds!.width).toBeCloseTo(1.25, 2);
      const stage = await board.boundingBox(), footer = await dock.boundingBox();
      expect(stage).not.toBeNull();
      expect(footer).not.toBeNull();
      expect(stage!.y + stage!.height).toBeLessThanOrEqual(footer!.y);
      await testInfo.attach(`readable-${viewport.width}-server-discard`, {
        body: JSON.stringify({ id, face, receipt, viewport, board: await publicReadback(page), sent: discards(actor.sent.slice(start)) }, null, 2),
        contentType: 'application/json',
      });
      await page.screenshot({ path: testInfo.outputPath(`readable-${viewport.width}-confirmed.png`) });
      await page.setViewportSize({ width: viewport.height, height: viewport.width });
      await assertSceneRestored(page, false);
      await expect(page.locator('#turn-banner')).toBeVisible();
      await page.setViewportSize(viewport);
      await expect(board).toBeVisible();
      const room = new URL(page.url()).searchParams.get('gameId'), origin = await page.evaluate(() => performance.timeOrigin);
      const switchStart = actor.sent.length;
      await board.getByRole('button', { name: 'Switch to the 3D table, keeping your hand controls' }).tap();
      await expect.poll(() => new URL(page.url()).searchParams.getAll('mobileLayout')).toEqual(['hybrid']);
      expect(new URL(page.url()).searchParams.get('gameId')).toBe(room);
      await assertSceneRestored(page, true);
      expect(await page.evaluate(() => performance.timeOrigin)).toBe(origin);
      expect(await canvas!.evaluate(node => node === document.querySelector('#main canvas'))).toBe(true);
      await page.evaluate(() => {
        const url = new URL(location.href);
        url.searchParams.delete('mobileLayout');
        history.replaceState(history.state, '', url);
      });
      await assertSceneRestored(page, false);
      await expect(page.locator('#turn-banner')).toBeVisible();
      expect(discards(actor.sent.slice(start))).toHaveLength(1);
      expect(actor.sent.slice(switchStart).filter(frame => frame.type === 'NEW' || frame.type === 'JOIN')).toEqual([]);
      expect(actor.errors).toEqual([]);
    } finally {
      await context.close();
    }
  });
}

test('a later real claim and exposed human meld match the authoritative public snapshot, not a synthetic UI fixture', async ({ browser, baseURL }, testInfo) => {
  test.setTimeout(150_000);
  const context = await browser.newContext({
    viewport: { width: 412, height: 915 }, hasTouch: true, isMobile: true, deviceScaleFactor: 1, locale: 'en',
  });
  try {
    const actor = await startReadable(browser, baseURL, testInfo, context, 12345);
    const page = actor.page, dock = page.locator('#mobile-hand-dock'), board = page.locator('#readable-board');
    const deadline = Date.now() + 60_000;
    let claimed: 'Pung' | 'Chow' | null = null;
    let observedClaim: PublicReadback | null = null;
    while (Date.now() < deadline && claimed === null) {
      const pung = dock.locator('.ferro-claim-badge-pung'), chow = dock.locator('.ferro-claim-badge-chow');
      if (await pung.isVisible() && await pung.isEnabled()) {
        observedClaim = await expectCurrentClaim(actor, testInfo);
        await pung.tap();
        claimed = 'Pung';
      } else if (await chow.isVisible() && await chow.isEnabled()) {
        observedClaim = await expectCurrentClaim(actor, testInfo);
        const options = observedClaim.claim!.chowOptionCount;
        expect(options, 'an enabled Chow must have at least one authoritative partner choice').toBeGreaterThan(0);
        await chow.tap();
        const choice = page.locator('.rule-action-dialog[open] .rule-action-choice').first();
        if (options > 1) {
          await expect(choice).toBeVisible();
          await choice.tap();
        } else {
          await expect(choice, 'a single legal Chow submits directly without a chooser').toHaveCount(0);
        }
        claimed = 'Chow';
      } else {
        const pass = dock.locator('.ferro-claim-pass');
        const tiles = dock.locator('.mobile-hand-tile:not([disabled])');
        if (await pass.isVisible() && await pass.isEnabled()) await pass.tap();
        else if (await tiles.count() > 0) await tapConfirm(page, Math.floor(await tiles.count() / 2));
        else await page.waitForTimeout(150);
      }
    }
    expect(claimed, 'seed 12345 / Easy bots must reach a legal human Pung or Chow').not.toBeNull();
    expect(observedClaim?.claimMatches).toBe(true);
    await expect.poll(async () => (await publicReadback(page)).expected.filter(tile =>
      tile.zone === 'meld' && tile.seat === 0).length).toBeGreaterThanOrEqual(3);
    await expect.poll(async () => (await publicReadback(page)).mismatches).toEqual([]);
    const ownMelds = board.locator('.readable-board-scroll > [data-seat="0"] .readable-board-meld');
    await expect(ownMelds).toHaveCount(1);
    await expect(ownMelds.locator('[data-slot]')).toHaveCount(3);
    const after = await publicReadback(page);
    const ownPublicMeld = after.expected.filter(tile => tile.zone === 'meld' && tile.seat === 0);
    expect(ownPublicMeld).toHaveLength(3);
    for (const source of ownPublicMeld) {
      expect(source.face).not.toBeNull();
      const tile = ownMelds.locator(`[data-slot="${source.slot}"]`);
      await expect(tile).toHaveAttribute('data-kind', 'face');
      await expect(tile).toHaveAttribute('data-face', String(source.face));
      await expect(tile).toHaveAttribute('role', 'img');
      await expect(tile).toHaveAttribute('aria-label', /\S/);
    }
    expect(after.expected.some(tile => tile.zone === 'discard')).toBe(true);
    const hiddenFaces = after.actual.filter(tile => tile.kind !== 'face');
    expect(hiddenFaces.every(tile => tile.face === null)).toBe(true);
    await expect(dock.locator('.mobile-hand-tile')).toHaveCount(11);
    await expect(dock.locator('.mobile-hand-tile').first()).toBeEnabled();
    const evidence = testInfo.outputPath('readable-real-current-claim-and-meld.json');
    writeFileSync(evidence, JSON.stringify({ claimed, observedClaim, after, consoleErrors: actor.errors }, null, 2) + '\n');
    await testInfo.attach('readable-real-current-claim-and-meld', { path: evidence, contentType: 'application/json' });
    await page.screenshot({ path: testInfo.outputPath('readable-real-exposed-meld.png') });
    expect(actor.errors).toEqual([]);
  } finally {
    await context.close();
  }
});
