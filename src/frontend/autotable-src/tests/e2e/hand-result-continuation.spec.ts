import { test, expect, type Page, type TestInfo } from '@playwright/test';
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname } from 'node:path';
import {
  newActor, applyRoom, closeLobby, copyJoinLink, followJoinLink, roomId,
  dismissPrompts, probe, driveManualCeremony, type Actor,
} from './_lobby-repair';
import type { HandResultEntry } from '../../src/types';

type Frame = { type?: string; full?: boolean; entries?: Array<[string, string | number, Record<string, unknown> | null]> };

function wire(page: Page): { sent: Frame[]; received: Frame[] } {
  const result = { sent: [] as Frame[], received: [] as Frame[] };
  page.on('websocket', socket => {
    if (new URL(socket.url()).pathname !== '/autotable/ws') return;
    socket.on('framesent', e => { try { result.sent.push(JSON.parse(String(e.payload)) as Frame); } catch { /* non-JSON */ } });
    socket.on('framereceived', e => { try { result.received.push(JSON.parse(String(e.payload)) as Frame); } catch { /* non-JSON */ } });
  });
  return result;
}

async function currentResult(page: Page): Promise<HandResultEntry | null> {
  return page.evaluate(() => (window as unknown as { game?: {
    client: { result: { get(key: string): HandResultEntry | null } };
  } }).game?.client.result.get('current') ?? null);
}

async function signature(actor: Actor): Promise<string> {
  return actor.page.evaluate(() => {
    const client = (window as unknown as { game: { client: {
      seat: number | null;
      things: { entries(): Iterable<[string | number, { slotName?: string }]> };
      turn: { get(key: string): unknown };
      claim: { get(key: string): unknown };
      result: { get(key: string): unknown };
    } } }).game.client;
    return JSON.stringify({
      hand: [...client.things.entries()].filter(([, tile]) => tile.slotName?.startsWith('hand.') && tile.slotName.endsWith('@' + client.seat)),
      turn: client.turn.get('current'), claim: client.claim.get(String(client.seat)), result: client.result.get('current'),
    });
  });
}

async function board(page: Page): Promise<unknown> {
  return page.evaluate(() => {
    const client = (window as unknown as { game: { client: {
      things: { entries(): Iterable<[string | number, unknown]> }; match: { get(key: number): unknown };
    } } }).game.client;
    return {
      things: [...client.things.entries()].sort(([a], [b]) => String(a).localeCompare(String(b))),
      match: client.match.get(0),
    };
  });
}

async function configure(actor: Actor, hands: 1 | 4, seed: number): Promise<void> {
  await actor.page.locator(`#lobby-hand-count-fieldset label:has(input[value="${hands}"])`).click();
  await actor.page.locator('#lobby-advanced summary').click();
  await actor.page.locator('#lobby-seed').fill(String(seed));
}

/** Bounded one-hand play, using only visible legal Pass/discard controls. */
async function playOneHand(actors: Actor[]): Promise<HandResultEntry> {
  const deadline = Date.now() + 140_000;
  let discards = 0;
  const passedWindows = new Map<Actor, string>();
  while (Date.now() < deadline) {
    const result = await currentResult(actors[0].page);
    if (result !== null) return result;
    let acted = false;
    for (const actor of actors) {
      const settled = await currentResult(actor.page);
      if (settled !== null) return settled;
      const state = await probe(actor);
      const claimKey = await actor.page.evaluate(() => {
        const client = (window as unknown as { game: { client: {
          seat: number | null; claim: { get(key: string): unknown };
        } } }).game.client;
        const claim = client.claim.get(String(client.seat));
        return claim === null ? null : JSON.stringify(claim);
      });
      const pass = actor.page.locator('.ferro-claim-overlay-visible .ferro-claim-pass');
      if (claimKey !== null && await pass.isVisible()) {
        if (passedWindows.get(actor) === claimKey) continue;
        try { await pass.click({ timeout: 1200 }); }
        catch {
          if (await currentResult(actors[0].page)) return (await currentResult(actors[0].page))!;
          continue; // A genuine server claim deadline may close before the press.
        }
        // Pass does not promise an immediate phase change: other players may
        // still respond until this server-advertised window expires.
        passedWindows.set(actor, claimKey);
        acted = true;
      } else if (state.seat !== null && state.turn?.activeSeat === state.seat && state.turn.awaitingDiscard) {
        const tile = actor.page.getByTestId('hand-tile').last();
        await expect(tile).toBeVisible();
        const before = await signature(actor);
        try { await tile.click({ timeout: 1200 }); }
        catch (error) {
          const completed = await currentResult(actor.page);
          if (completed !== null) return completed;
          if (await signature(actor) === before) throw error;
          continue; // The server advanced while the ordinary press was queued.
        }
        await expect.poll(() => signature(actor)).not.toBe(before);
        expect(++discards, 'one wall cannot require an unbounded human-play campaign').toBeLessThanOrEqual(48);
        acted = true;
      }
    }
    if (!acted) await actors[0].page.waitForTimeout(80);
  }
  throw new Error(`A normal one-hand play did not settle; last state=${await signature(actors[0])}`);
}

function acknowledgements(frames: Frame[]): Frame[] {
  return frames.filter(frame => frame.entries?.some(([kind]) => kind === 'handResultAck'));
}

async function save(testInfo: TestInfo, name: string, value: unknown, page: Page): Promise<void> {
  const json = testInfo.outputPath(name + '.json');
  mkdirSync(dirname(json), { recursive: true });
  writeFileSync(json, JSON.stringify(value, null, 2));
  await page.screenshot({ path: testInfo.outputPath(name + '.png') });
}

for (const dealMode of ['auto', 'manual'] as const) {
test(`real bot win/draw (${dealMode}) is held through backdrop, Escape, metadata and reconnect until one owner Continue`, async ({ browser, baseURL }, testInfo) => {
  test.setTimeout(180_000);
  const context = await browser.newContext({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true, deviceScaleFactor: 1 });
  try {
    const actor = await newActor(browser, baseURL, testInfo, 'result-owner', context);
    const transport = wire(actor.page);
    await configure(actor, 4, 4100);
    await applyRoom(actor, 3, 0, dealMode);
    await closeLobby(actor);
    if (dealMode === 'manual') await driveManualCeremony([actor]);
    const result = await playOneHand([actor]);
    expect(result.continuation?.waitingSeats).toEqual([0]);
    await expect(actor.page.getByTestId('hand-result-dialog')).toBeVisible();
    await expect(actor.page.locator('#result-score tbody tr')).toHaveCount(4);
    await expect(actor.page.locator('#result-winner')).not.toBeEmpty();
    await expect(actor.page.getByTestId('hand-result-continue')).toHaveText('Continue');
    const held = await board(actor.page);
    await actor.page.keyboard.press('Escape');
    await actor.page.getByTestId('hand-result-dialog').click({ position: { x: 2, y: 2 } });
    await actor.page.waitForTimeout(2500); // Longer than the former immediate bot-driven next deal.
    await expect(actor.page.getByTestId('hand-result-dialog')).toBeVisible();
    expect(await currentResult(actor.page)).toEqual(result);
    expect(await board(actor.page)).toEqual(held);
    expect(acknowledgements(transport.sent)).toHaveLength(0);
    await actor.page.setViewportSize({ width: 844, height: 390 });
    await actor.page.locator('#result-score').scrollIntoViewIfNeeded();
    const landscapeButton = await actor.page.getByTestId('hand-result-continue').boundingBox();
    expect(landscapeButton!.y).toBeGreaterThanOrEqual(0);
    expect(landscapeButton!.y + landscapeButton!.height).toBeLessThanOrEqual(390);
    await save(testInfo, 'held-result-landscape', { result, transport }, actor.page);
    await actor.page.setViewportSize({ width: 390, height: 844 });
    await actor.page.request.get(new URL('/api/games/' + roomId(actor), actor.page.url()).href);
    expect(acknowledgements(transport.sent)).toHaveLength(0);

    await actor.page.reload({ waitUntil: 'domcontentloaded' });
    await dismissPrompts(actor);
    await closeLobby(actor);
    await expect.poll(async () => (await probe(actor)).seat).toBe(0);
    await expect(actor.page.getByTestId('hand-result-dialog')).toBeVisible();
    expect((await currentResult(actor.page))?.continuation).toEqual(result.continuation);
    expect(acknowledgements(transport.sent)).toHaveLength(0);
    const button = actor.page.getByTestId('hand-result-continue');
    const rect = await button.boundingBox();
    expect(rect).not.toBeNull();
    expect(rect!.y).toBeGreaterThanOrEqual(0);
    expect(rect!.y + rect!.height).toBeLessThanOrEqual(844);
    await save(testInfo, 'held-result-before-continue', { result, held, transport }, actor.page);
    await button.tap();
    await expect.poll(() => acknowledgements(transport.sent).length).toBe(1);
    const { gameId, handNumber, resultToken } = result.continuation!;
    expect(acknowledgements(transport.sent)[0]).toEqual({
      type: 'UPDATE', full: false, entries: [['handResultAck', 'current', { gameId, handNumber, resultToken }]],
    });
    expect(gameId).not.toBe(roomId(actor));
    await expect.poll(() => currentResult(actor.page)).toBeNull();
    await expect(actor.page.getByTestId('hand-result-dialog')).toBeHidden();
    expect(await board(actor.page)).not.toEqual(held);
    await save(testInfo, 'server-advanced-after-continue', { result, transport, after: await board(actor.page) }, actor.page);
    expect(actor.errors).toEqual([]);
  } finally { await context.close(); }
});
}

test('two actual humans must both Continue; an acknowledged reconnect waits and an observer cannot acknowledge', async ({ browser, baseURL }, testInfo) => {
  test.setTimeout(210_000);
  const first = await browser.newContext({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true, deviceScaleFactor: 1 });
  const second = await browser.newContext({ viewport: { width: 360, height: 800 }, isMobile: true, hasTouch: true, deviceScaleFactor: 1 });
  try {
    const owner = await newActor(browser, baseURL, testInfo, 'first-human', first);
    const ownerWire = wire(owner.page);
    await configure(owner, 4, 4101);
    await applyRoom(owner, 2, 0, 'auto');
    const href = await copyJoinLink(owner);
    const peer = await newActor(browser, baseURL, testInfo, 'second-human', second);
    const peerWire = wire(peer.page);
    await followJoinLink(peer, href, roomId(owner));
    await closeLobby(peer);
    const peerSeat = (await probe(peer)).seat;
    expect(peerSeat).not.toBeNull();
    expect(peerSeat).not.toBe(0);
    const result = await playOneHand([owner, peer]);
    expect(result.continuation?.waitingSeats).toEqual([0, peerSeat!].sort((a, b) => a - b));
    await expect(peer.page.getByTestId('hand-result-dialog')).toBeVisible();
    const held = await board(owner.page);

    const observer = await newActor(browser, baseURL, testInfo, 'duplicate-observer', first);
    expect(observer.id).toBe(owner.id);
    const observerWire = wire(observer.page);
    await observer.page.goto(href, { waitUntil: 'domcontentloaded' });
    await dismissPrompts(observer);
    await closeLobby(observer);
    await expect.poll(async () => (await probe(observer)).connected).toBe(true);
    expect((await probe(observer)).seat).toBeNull();
    await expect(observer.page.getByTestId('hand-result-continue')).toHaveText('Dismiss');
    await observer.page.getByTestId('hand-result-continue').tap();
    await expect(observer.page.getByTestId('hand-result-dialog')).toBeHidden();
    expect(acknowledgements(observerWire.sent)).toHaveLength(0);
    await observer.page.close();

    await owner.page.getByTestId('hand-result-continue').tap();
    await expect.poll(() => acknowledgements(ownerWire.sent).length).toBe(1);
    await expect(owner.page.getByTestId('hand-result-status')).toContainText('Waiting for the other players');
    await expect(owner.page.getByTestId('hand-result-continue')).toBeDisabled();
    await owner.page.waitForTimeout(1800);
    expect((await currentResult(owner.page))?.continuation?.waitingSeats).toEqual([peerSeat]);
    expect(await board(owner.page)).toEqual(held);
    await owner.page.reload({ waitUntil: 'domcontentloaded' });
    await dismissPrompts(owner);
    await closeLobby(owner);
    await expect(owner.page.getByTestId('hand-result-status')).toContainText('Waiting for the other players');
    await expect(owner.page.getByTestId('hand-result-continue')).toBeDisabled();
    expect(acknowledgements(ownerWire.sent)).toHaveLength(1);
    await save(testInfo, 'waiting-for-second-owner', { result, ownerWire, peerWire, held }, owner.page);
    await peer.page.getByTestId('hand-result-continue').tap();
    await expect.poll(() => acknowledgements(peerWire.sent).length).toBe(1);
    await expect.poll(() => currentResult(owner.page)).toBeNull();
    await expect.poll(() => currentResult(peer.page)).toBeNull();
    expect(await board(owner.page)).not.toEqual(held);
    await save(testInfo, 'both-owners-acknowledged', { result, ownerWire, peerWire }, owner.page);
    expect([...owner.errors, ...peer.errors, ...observer.errors]).toEqual([]);
  } finally { await first.close(); await second.close(); }
});

test('terminal hand keeps authoritative GameComplete and the existing final New Game without an ack', async ({ browser, baseURL }, testInfo) => {
  test.setTimeout(180_000);
  const context = await browser.newContext({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true, deviceScaleFactor: 1 });
  try {
    const actor = await newActor(browser, baseURL, testInfo, 'final-hand', context);
    const transport = wire(actor.page);
    await configure(actor, 1, 4100);
    const originalRoom = await applyRoom(actor, 3, 0, 'auto');
    await closeLobby(actor);
    const result = await playOneHand([actor]);
    expect(result.continuation).toBeNull();
    await expect(actor.page.locator('#game-complete-modal')).toBeVisible();
    await expect(actor.page.getByTestId('hand-result-dialog')).toBeHidden();
    expect(acknowledgements(transport.sent)).toHaveLength(0);
    await save(testInfo, 'terminal-result-no-ack', { result, transport }, actor.page);
    await actor.page.locator('#game-complete-new-game').tap();
    await actor.page.waitForURL(url => url.searchParams.get('gameId') !== originalRoom, { waitUntil: 'domcontentloaded' });
    expect(acknowledgements(transport.sent)).toHaveLength(0);
    expect(actor.errors).toEqual([]);
  } finally { await context.close(); }
});
