import { test, expect, type Browser, type Page, type TestInfo } from '@playwright/test';
import { mkdirSync, writeFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { join } from 'node:path';
import type { Camera, InstancedMesh, Mesh } from 'three';
import type { Game } from '../../src/game';
import { newActor, applyRoom, closeLobby, openLobby, probe, driveManualCeremony, type Actor } from './_lobby-repair';

const artifactDirectory = process.env.COMPACT_TABLE_ARTIFACT_DIR;
const baselineOnly = process.env.COMPACT_TABLE_CAPTURE_STAGE === 'baseline';
const phone = { width: 412, height: 924 };
const viewports = [phone, { width: 500, height: 954 }, { width: 1280, height: 900 }];

interface ProjectedFace {
  id: number;
  slot: string;
  zone: string;
  seat: number | null;
  left: number;
  top: number;
  right: number;
  bottom: number;
  width: number;
  height: number;
  pickingError: number;
}

test.use({
  launchOptions: {
    args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'],
  },
});

function output(info: TestInfo, name: string): string {
  if (!artifactDirectory) return info.outputPath(name);
  mkdirSync(artifactDirectory, { recursive: true });
  return join(artifactDirectory, name);
}

async function state(page: Page) {
  return page.evaluate(() => {
    const game = (window as Window & { game?: Game }).game;
    if (!game) throw new Error('Game shell is not ready');
    const entries = [...game.client.things.entries()];
    const publicTiles = entries.filter(([, entry]) => /^(discard|meld)\.\d+\.\d+@[0-3]$/.test(entry.slotName));
    return {
      discards: publicTiles.filter(([, entry]) => entry.slotName.startsWith('discard.')).length,
      melds: publicTiles.filter(([, entry]) => entry.slotName.startsWith('meld.')).length,
      ownMelds: new Set(publicTiles.filter(([, entry]) => entry.slotName.startsWith('meld.')
        && entry.slotName.endsWith('@0')).map(([, entry]) => entry.slotName.split('.')[1])).size,
      hand: game.world.ownHandPresentation().tiles.map(tile => tile.id).sort((a, b) => a - b),
      turn: game.client.turn.get('current'),
    };
  });
}

async function confirmDiscard(actor: Actor): Promise<void> {
  const tiles = actor.page.locator('#mobile-hand-dock .mobile-hand-tile:not([disabled])');
  const count = await tiles.count();
  expect(count).toBeGreaterThan(0);
  const chosen = tiles.nth(Math.floor(count / 2));
  const id = Number(await chosen.getAttribute('data-tile-id'));
  await chosen.tap();
  await expect(chosen).toHaveAttribute('aria-pressed', 'true');
  const confirm = actor.page.locator('.mobile-hand-discard');
  await expect(confirm).toHaveAttribute('data-tile-id', String(id));
  await confirm.tap({ timeout: 5_000 });
  await actor.page.waitForFunction(tile => {
    const game = (window as Window & { game?: Game }).game;
    return game !== undefined && !game.client.things.get(tile)?.slotName.startsWith('hand.');
  }, id);
}

async function respondToClaim(actor: Actor): Promise<boolean> {
  const option = actor.page.locator('.rule-action-dialog[open] .rule-action-choice').first();
  if (await option.isVisible()) {
    await option.tap();
    return true;
  }
  for (const type of ['pung', 'chow']) {
    const button = actor.page.locator(`#mobile-hand-dock .ferro-claim-badge-${type}`);
    if (await button.isVisible() && await button.isEnabled()) {
      await button.tap();
      if (await option.isVisible()) await option.tap();
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

async function table(
  browser: Browser, baseURL: string | undefined, info: TestInfo, compact: boolean, seed = 12345,
): Promise<Actor> {
  const context = await browser.newContext({ viewport: phone, deviceScaleFactor: 1, hasTouch: true, isMobile: true });
  const url = new URL(baseURL ?? 'http://127.0.0.1:5114/autotable/');
  url.search = new URLSearchParams({
    ...(compact ? { tableLayout: 'compact' } : {}),
    mobileLayout: 'hybrid', variant: 'changsha', dealMode: 'auto', botCount: '3',
    botDifficulty: 'Easy', handCount: '1', seed: String(seed), seat: '0',
  }).toString();
  const actor = await newActor(browser, baseURL, info, 'compact-table', context, url.href);
  await applyRoom(actor, 3, 0, 'auto');
  await closeLobby(actor);
  await expect.poll(async () => (await probe(actor)).handCount, { timeout: 30_000 }).toBe(14);
  await expect(actor.page.locator('#mobile-hand-dock')).toBeVisible();
  if (compact) {
    await expect(actor.page.locator('body')).toHaveAttribute('data-table-layout', 'compact');
    writeFileSync(output(info, 'fresh-link-proof.json'), JSON.stringify({
      entryURL: url.href,
      connected: (await probe(actor)).connected,
      handCount: await actor.page.locator('.mobile-hand-tile').count(),
      tableLayout: await actor.page.locator('body').getAttribute('data-table-layout'),
      mobileLayout: new URL(actor.page.url()).searchParams.get('mobileLayout'),
    }, null, 2) + '\n');
  }
  return actor;
}

async function crowded(actor: Actor): Promise<Awaited<ReturnType<typeof state>>> {
  const ready = (value: Awaited<ReturnType<typeof state>>) => value.discards >= 32 && value.melds >= 3
    && value.turn?.phase === 'AwaitingDiscard' && value.turn.activeSeat === 0;
  const deadline = Date.now() + 100_000;
  let current = await state(actor.page);
  while (Date.now() < deadline) {
    current = await state(actor.page);
    if (ready(current)) break;
    if (await actor.page.locator('#game-complete-modal').isVisible()
      || await actor.page.locator('#result-modal').isVisible()) break;
    if (!await respondToClaim(actor)) {
      if (await actor.page.locator('.mobile-hand-tile:not([disabled])').count() > 0) {
        current = await state(actor.page);
        if (ready(current)) break;
        await confirmDiscard(actor);
      } else await actor.page.waitForTimeout(100);
    }
  }
  if (!ready(current)) {
    expect(['Scoring', 'EndHand', 'WallExhausted', 'GameComplete']).toContain(current.turn?.phase);
    return current;
  }
  await expect.poll(async () => (await state(actor.page)).hand.length).toBe(14 - 3 * current.ownMelds);
  return state(actor.page);
}

async function settle(page: Page): Promise<void> {
  await page.mouse.move(0, 0);
  await page.evaluate(() => new Promise<void>(resolve =>
    requestAnimationFrame(() => requestAnimationFrame(() => resolve()))));
}

async function authoritativeFingerprint(page: Page): Promise<string> {
  const source = await page.evaluate(() => {
    const game = (window as Window & { game?: Game }).game;
    if (!game) throw new Error('Game shell is not ready');
    return JSON.stringify({
      entries: [...game.client.things.entries()].sort((a, b) => String(a[0]).localeCompare(String(b[0]))),
      slots: [...game.world.slots.values()].map(slot => ({
        name: slot.name, origin: slot.origin.toArray(), direction: slot.direction.toArray(),
        places: slot.places.map(place => ({
          position: place.position.toArray(), rotation: place.rotation.toArray(), size: place.size.toArray(),
        })),
      })).sort((a, b) => a.name.localeCompare(b.name)),
    });
  });
  return createHash('sha256').update(source).digest('hex');
}

async function faceMetrics(page: Page) {
  return page.evaluate(() => {
    const game = (window as Window & { game?: Game }).game;
    if (!game) throw new Error('Game shell is not ready');
    const { camera } = Reflect.get(game, 'mainView') as { camera: Camera };
    const objectView = Reflect.get(game, 'objectView') as {
      thingGroups: Map<string, { startIndex: number; meshes: Mesh[]; instancedMesh: InstancedMesh }>;
      shadowObject: InstancedMesh;
    };
    const group = objectView.thingGroups.get('TILE');
    if (!group) throw new Error('Tile render group is unavailable');
    const canvas = document.querySelector('#main canvas')!.getBoundingClientRect();
    const geometry = group.instancedMesh.geometry;
    const positions = geometry.getAttribute('position'), uv = geometry.getAttribute('uv');
    const picks = new Map(game.world.toSelect().map(place => [place.id, place]));
    const readMatrix = (id: number) => {
      const mesh = group.meshes[id - group.startIndex];
      if (!mesh) throw new Error(`No submitted tile body for ${id}`);
      const matrix = mesh.matrix.clone();
      if (mesh.visible) matrix.copy(mesh.matrixWorld);
      else {
        group.instancedMesh.getMatrixAt(id - group.startIndex, matrix);
        matrix.premultiply(group.instancedMesh.matrixWorld);
      }
      return matrix;
    };
    const faces: ProjectedFace[] = [];
    for (const [id, entry] of game.client.things.entries()) {
      const publicSlot = /^(discard|meld)\.\d+\.\d+@[0-3]$/.test(entry.slotName);
      const ownHand = typeof id === 'number' && game.world.ownHandThing(id) !== null;
      if (typeof id !== 'number' || (!publicSlot && !ownHand)) continue;
      const thing = game.world.things.get(id), mesh = group.meshes[id - group.startIndex];
      if (!thing || thing.hidden || thing.hiddenHandle !== null || !mesh
        || thing.rotationIndex >= 2 || thing.slot.name !== entry.slotName || thing.slot.thing !== thing) continue;
      const matrix = readMatrix(id);
      if (Math.abs(matrix.determinant()) < 1e-9) continue;
      const min = [Infinity, Infinity, Infinity], max = [-Infinity, -Infinity, -Infinity];
      const label = [];
      for (let vertex = 0; vertex < positions.count; vertex++) {
        const point = mesh.position.clone().fromBufferAttribute(positions, vertex).applyMatrix4(matrix);
        for (const [axis, value] of point.toArray().entries()) {
          min[axis] = Math.min(min[axis], value);
          max[axis] = Math.max(max[axis], value);
        }
        if (uv.getX(vertex) > 32 / 256 + .00001 || uv.getY(vertex) > 40 / 256 + .00001) continue;
        point.project(camera);
        label.push({ x: canvas.left + (point.x + 1) * canvas.width / 2, y: canvas.top + (1 - point.y) * canvas.height / 2 });
      }
      if (label.length === 0) throw new Error(`No actual label surface for ${entry.slotName}`);
      const left = Math.min(...label.map(point => point.x)), right = Math.max(...label.map(point => point.x));
      const top = Math.min(...label.map(point => point.y)), bottom = Math.max(...label.map(point => point.y));
      const pick = picks.get(id);
      const pickingError = pick ? Math.max(...pick.position.toArray().flatMap((center, axis) => [
        Math.abs(min[axis] - (center - pick.size.getComponent(axis) / 2)),
        Math.abs(max[axis] - (center + pick.size.getComponent(axis) / 2)),
      ])) : Infinity;
      faces.push({ id, slot: entry.slotName, zone: thing.slot.group, seat: thing.slot.seat,
        left, top, right, bottom, width: right - left, height: bottom - top, pickingError });
    }
    const zones = Object.fromEntries(['discard', 'meld', 'hand'].map(zone => {
      const tiles = faces.filter(face => face.zone === zone);
      return [zone, {
        count: tiles.length,
        minWidth: Math.min(...tiles.map(face => face.width)),
        minHeight: Math.min(...tiles.map(face => face.height)),
        minShortSide: Math.min(...tiles.map(face => Math.min(face.width, face.height))),
      }];
    }));
    const gutters = [0, 1, 2, 3].flatMap(seat => {
      const river = faces.filter(face => face.seat === seat && face.zone === 'discard');
      const melds = faces.filter(face => face.seat === seat && face.zone === 'meld');
      if (river.length === 0 || melds.length === 0) return [];
      const bounds = (tiles: typeof faces) => ({
        left: Math.min(...tiles.map(tile => tile.left)), right: Math.max(...tiles.map(tile => tile.right)),
        top: Math.min(...tiles.map(tile => tile.top)), bottom: Math.max(...tiles.map(tile => tile.bottom)),
      });
      const a = bounds(river), b = bounds(melds);
      const gap = Math.hypot(Math.max(0, a.left - b.right, b.left - a.right),
        Math.max(0, a.top - b.bottom, b.top - a.bottom));
      const tileSize = Math.min(...[...river, ...melds].map(tile => Math.min(tile.width, tile.height)));
      return [{ seat, gap, normalizedGap: gap / tileSize }];
    });
    const dock = document.getElementById('mobile-hand-dock');
    const tableCounts = {
      wall: { authored: 0, rendered: 0 },
      opponentHand: { authored: 0, rendered: 0 },
      discard: { authored: 0, rendered: 0 },
      meld: { authored: 0, rendered: 0 },
    };
    const clippedBodies: string[] = [];
    const area = (Reflect.get(game, 'mainView') as {
      tablePlayArea: { left: number; right: number; top: number; bottom: number };
    }).tablePlayArea;
    for (const [, entry] of game.client.things.entries()) {
      const slot = game.world.slots.get(entry.slotName);
      if (!slot) continue;
      let zone: keyof typeof tableCounts | null = null;
      if (slot.group === 'wall' || slot.group === 'discard' || slot.group === 'meld') zone = slot.group;
      else if (slot.group === 'hand' && slot.seat !== game.client.seat) zone = 'opponentHand';
      if (zone === null) continue;
      tableCounts[zone].authored++;
      const thing = slot.thing;
      if (!thing || thing.slot !== slot || thing.hidden) throw new Error(`Missing authoritative body for ${slot.name}`);
      const matrix = readMatrix(thing.index);
      if (Math.abs(matrix.determinant()) < 1e-9) continue;
      tableCounts[zone].rendered++;
      let clipped = false;
      for (let vertex = 0; vertex < positions.count; vertex++) {
        const point = thing.place().position.clone().fromBufferAttribute(positions, vertex).applyMatrix4(matrix).project(camera);
        const x = (point.x + 1) * canvas.width / 2, y = (1 - point.y) * canvas.height / 2;
        if (x < area.left - .5 || x > area.right + .5 || y < area.top - .5 || y > area.bottom + .5) clipped = true;
      }
      if (clipped) clippedBodies.push(slot.name);
    }
    const shadowMatrices = Array.from({ length: objectView.shadowObject.count }, (_, index) => {
      const matrix = group.instancedMesh.matrix.clone();
      objectView.shadowObject.getMatrixAt(index, matrix);
      return matrix.elements;
    });
    let riverGridCells = 0;
    for (const slot of game.world.slots.values()) {
      if (slot.group !== 'discard' || !slot.drawShadow) continue;
      const canonical = slot.places[slot.shadowRotation];
      const place = game.world.tableLayoutProfile?.present(slot, canonical) ?? canonical;
      if (shadowMatrices.some(matrix => Math.abs(matrix[12] - place.position.x) < .001
        && Math.abs(matrix[13] - place.position.y) < .001
        && Math.abs(matrix[0] - place.size.x) < .001
        && Math.abs(matrix[5] - place.size.y) < .001)) riverGridCells++;
    }
    return {
      viewport: [innerWidth, innerHeight], zones, faces, gutters,
      meanNormalizedGutter: gutters.reduce((sum, gutter) => sum + gutter.normalizedGap, 0) / gutters.length,
      dockTop: dock?.getBoundingClientRect().top ?? null,
      compact: document.body.dataset.tableLayout ?? null,
      tableCounts, clippedBodies, riverGridCells,
    };
  });
}

test('compact 3D enlarges real public faces on phone and desktop without changing game geometry or IDs', async ({ browser, baseURL }, info) => {
  test.setTimeout(180_000);
  let actor: Actor | null = null;
  let position: Awaited<ReturnType<typeof state>> | null = null;
  const acquisition = [];
  for (const seed of [12345, 4100, 777, 24680, 5388, 98765]) {
    const candidate = await table(browser, baseURL, info, false, seed);
    const current = await crowded(candidate);
    acquisition.push({ seed, discards: current.discards, melds: current.melds, phase: current.turn?.phase });
    if (current.discards >= 32 && current.melds >= 3
      && current.turn?.phase === 'AwaitingDiscard' && current.turn.activeSeat === 0) {
      actor = candidate;
      position = current;
      break;
    }
    await candidate.context.close();
  }
  writeFileSync(output(info, baselineOnly ? 'baseline-acquisition.json' : 'comparison-acquisition.json'),
    JSON.stringify(acquisition, null, 2) + '\n');
  if (actor === null || position === null) throw new Error('No live crowded hand reached before the bots legitimately ended their games');
  try {
    const fingerprint = await authoritativeFingerprint(actor.page);
    const reports = [];
    for (const viewport of viewports) {
      await actor.page.setViewportSize(viewport);
      await settle(actor.page);
      const before = await faceMetrics(actor.page);
      expect(before.zones.discard.count).toBe(position.discards);
      expect(before.zones.meld.count).toBeGreaterThanOrEqual(3);
      await actor.page.screenshot({ path: output(info, `before-${viewport.width}x${viewport.height}.png`) });
      if (baselineOnly) {
        reports.push({ viewport, before });
        continue;
      }
      await actor.page.evaluate(() => {
        const url = new URL(location.href);
        url.searchParams.set('tableLayout', 'compact');
        history.replaceState(history.state, '', url);
      });
      await expect(actor.page.locator('body')).toHaveAttribute('data-table-layout', 'compact');
      await settle(actor.page);
      const after = await faceMetrics(actor.page);
      await actor.page.screenshot({ path: output(info, `compact-${viewport.width}x${viewport.height}.png`) });
      for (const zone of ['wall', 'opponentHand', 'discard', 'meld'] as const) {
        expect(after.tableCounts[zone].authored).toBe(before.tableCounts[zone].authored);
        expect(after.tableCounts[zone].rendered, `${zone} must remain on the real table`)
          .toBe(after.tableCounts[zone].authored);
      }
      expect(after.clippedBodies).toEqual([]);
      expect(after.riverGridCells).toBe(72);
      expect(after.faces.map(face => face.id).sort((a, b) => a - b))
        .toEqual(before.faces.map(face => face.id).sort((a, b) => a - b));
      for (const zone of ['discard', 'meld']) {
        expect(after.zones[zone].minWidth, `${zone} width ${viewport.width}`).toBeGreaterThanOrEqual(before.zones[zone].minWidth * 1.2);
        expect(after.zones[zone].minHeight, `${zone} height ${viewport.width}`).toBeGreaterThanOrEqual(before.zones[zone].minHeight * 1.5);
      }
      expect(after.meanNormalizedGutter, `public gutter ${viewport.width}`)
        .toBeLessThan(before.meanNormalizedGutter * .7);
      if (before.zones.hand.count > 0) {
        expect(after.zones.hand.count).toBe(before.zones.hand.count);
        expect(after.zones.hand.minWidth).toBeGreaterThanOrEqual(before.zones.hand.minWidth * .95);
        expect(after.zones.hand.minHeight).toBeGreaterThanOrEqual(before.zones.hand.minHeight * .95);
      }
      for (const face of after.faces) {
        expect(face.pickingError, face.slot).toBeLessThan(.01);
        expect(face.left, face.slot).toBeGreaterThanOrEqual(-.5);
        expect(face.right, face.slot).toBeLessThanOrEqual(viewport.width + .5);
        expect(face.top, face.slot).toBeGreaterThanOrEqual(0);
        expect(face.bottom, face.slot).toBeLessThanOrEqual(after.dockTop ?? viewport.height);
      }
      expect(await authoritativeFingerprint(actor.page)).toBe(fingerprint);
      await actor.page.screenshot({ path: output(info, `compact-${viewport.width}x${viewport.height}.png`) });
      reports.push({ viewport, before, after });
      await actor.page.evaluate(() => {
        const url = new URL(location.href);
        url.searchParams.delete('tableLayout');
        history.replaceState(history.state, '', url);
      });
      await expect(actor.page.locator('body')).not.toHaveAttribute('data-table-layout', 'compact');
      await settle(actor.page);
      const restored = await faceMetrics(actor.page);
      expect(restored.faces.map(face => face.id)).toEqual(before.faces.map(face => face.id));
      for (let i = 0; i < before.faces.length; i++) {
        expect(restored.faces[i].width).toBeCloseTo(before.faces[i].width, 5);
        expect(restored.faces[i].height).toBeCloseTo(before.faces[i].height, 5);
      }
    }
    if (!baselineOnly) {
      const currentActor = actor;
      const room = new URL(actor.page.url()).searchParams.get('gameId');
      const sentBeforeReload = actor.sent.length;
      await actor.page.evaluate(() => {
        const url = new URL(location.href);
        url.searchParams.set('tableLayout', 'compact');
        history.replaceState(history.state, '', url);
      });
      await actor.page.reload({ waitUntil: 'domcontentloaded' });
      await expect.poll(async () => (await probe(currentActor)).handCount).toBe(position.hand.length);
      await expect(actor.page.locator('body')).toHaveAttribute('data-table-layout', 'compact');
      await closeLobby(actor);
      await expect.poll(() => state(currentActor.page)).toEqual(position);
      expect(new URL(actor.page.url()).searchParams.get('gameId')).toBe(room);
      expect(actor.sent.slice(sentBeforeReload).filter(frame => frame.type === 'NEW')).toEqual([]);
      await settle(actor.page);
      const reloaded = await faceMetrics(actor.page);
      for (const zone of ['wall', 'opponentHand', 'discard', 'meld'] as const) {
        expect(reloaded.tableCounts[zone].rendered).toBe(reloaded.tableCounts[zone].authored);
      }
      expect(reloaded.riverGridCells).toBe(72);
      expect(reloaded.clippedBodies).toEqual([]);
      await actor.page.screenshot({ path: output(info, 'reloaded-existing-room-1280x900.png') });
      writeFileSync(output(info, 'existing-room-reload.json'), JSON.stringify({
        sameRoom: true, noNewGameCommand: true, position, reloaded,
      }, null, 2) + '\n');
    }
    writeFileSync(output(info, baselineOnly ? 'baseline-measurements.json' : 'comparison-measurements.json'),
      JSON.stringify({ position, fingerprint, reports }, null, 2) + '\n');
    expect(actor.errors).toEqual([]);
  } finally {
    if (!actor.page.isClosed()) await actor.context.close();
  }
});

test('the install affordance cannot cover a live hand action and remains usable in the lobby', async ({ browser, baseURL }, info) => {
  test.skip(baselineOnly, 'The install-overlap fix is validated only after the integrated build.');
  const actor = await table(browser, baseURL, info, true);
  try {
    await expect(actor.page.getByTestId('pwa-offline-banner')).toBeAttached();
    await actor.page.evaluate(() => {
      document.body.dataset.installPromptCalls = '0';
      const event = Object.assign(new Event('beforeinstallprompt'), {
        prompt: async () => {
          document.body.dataset.installPromptCalls = String(Number(document.body.dataset.installPromptCalls) + 1);
        },
        userChoice: Promise.resolve({ outcome: 'dismissed', platform: 'web' }),
      });
      window.dispatchEvent(event);
    });
    const install = actor.page.getByTestId('pwa-install-button');
    await expect(install).toHaveCount(1);
    await expect(install).toBeHidden();
    const chosen = actor.page.locator('.mobile-hand-tile').first();
    await chosen.tap();
    await expect(actor.page.locator('.mobile-hand-discard')).toBeEnabled();
    expect(await actor.page.locator('.mobile-hand-discard').evaluate(element => {
      const box = element.getBoundingClientRect();
      const hit = document.elementFromPoint(box.right - 12, box.top + box.height / 2);
      return hit === element || (hit !== null && element.contains(hit));
    })).toBe(true);
    await openLobby(actor);
    await expect(install).toBeVisible();
    await install.click();
    await expect(actor.page.locator('body')).toHaveAttribute('data-install-prompt-calls', '1');
    await expect(install).toHaveCount(0);
    expect(actor.sent.flatMap(frame => frame.entries ?? []).filter(([kind]) => kind === 'discard')).toEqual([]);
    expect(actor.errors).toEqual([]);
  } finally {
    if (!actor.page.isClosed()) await actor.context.close();
  }
});

test('compact waits for the real manual ceremony and survives ordinary lobby configuration', async ({ browser, baseURL }, info) => {
  test.skip(baselineOnly, 'Activation requires the new served implementation.');
  test.setTimeout(90_000);
  const context = await browser.newContext({ viewport: phone, deviceScaleFactor: 1, hasTouch: true, isMobile: true });
  const entry = new URL(baseURL ?? 'http://127.0.0.1:5114/autotable/');
  entry.search = new URLSearchParams({
    variant: 'changsha', mobileLayout: 'hybrid', tableLayout: 'compact',
    dealMode: 'manual', botCount: '3', botDifficulty: 'Easy', seed: '4100', seat: '0', handCount: '1',
  }).toString();
  const actor = await newActor(browser, baseURL, info, 'compact-manual', context, entry.href);
  try {
    await applyRoom(actor, 3, 0, 'manual');
    await closeLobby(actor);
    expect(new URL(actor.page.url()).searchParams.get('tableLayout')).toBe('compact');
    await expect.poll(async () => (await probe(actor)).turn?.phase).toBe('RollingDice');
    await expect(actor.page.locator('body')).not.toHaveAttribute('data-table-layout', 'compact');
    await expect(actor.page.locator('#mobile-hand-dock #roll-dice')).toBeVisible();
    await driveManualCeremony([actor]);
    await expect(actor.page.locator('body')).toHaveAttribute('data-table-layout', 'compact');
    await expect(actor.page.locator('.mobile-hand-tile')).toHaveCount(14);
    const before = actor.sent.length;
    await confirmDiscard(actor);
    expect(actor.sent.slice(before).flatMap(frame => frame.entries ?? []).filter(([kind]) => kind === 'discard')).toHaveLength(1);
    expect(actor.errors).toEqual([]);
  } finally {
    if (!actor.page.isClosed()) await context.close();
  }
});

test('compact is inert on a real relay connection and duplicate flags cannot activate it', async ({ browser, baseURL }, info) => {
  test.skip(baselineOnly, 'Activation guards require the new served implementation.');
  const context = await browser.newContext({ viewport: phone, deviceScaleFactor: 1, hasTouch: true, isMobile: true });
  const entry = new URL(baseURL ?? 'http://127.0.0.1:5114/autotable/');
  entry.search = new URLSearchParams({
    gameId: `compact-relay-${Date.now().toString(36)}`, variant: 'four_player', tableLayout: 'compact', seat: '0',
  }).toString();
  const actor = await newActor(browser, baseURL, info, 'compact-relay', context, entry.href);
  try {
    await closeLobby(actor);
    await actor.page.waitForFunction(() => {
      const game = (window as Window & { game?: Game }).game;
      return game?.client.connected() && game.client.connectionMode === 'relay'
        && game.world.conditions.gameType === 'FOUR_PLAYER';
    });
    await expect(actor.page.locator('body')).not.toHaveAttribute('data-table-layout', 'compact');
    expect(actor.errors).toEqual([]);
  } finally {
    if (!actor.page.isClosed()) await context.close();
  }
  const owned = await table(browser, baseURL, info, true);
  try {
    await expect(owned.page.locator('body')).toHaveAttribute('data-table-layout', 'compact');
    const fresh = [];
    for (const viewport of viewports) {
      await owned.page.setViewportSize(viewport);
      await settle(owned.page);
      const current = await faceMetrics(owned.page);
      expect(current.tableCounts.wall).toEqual({ authored: 55, rendered: 55 });
      expect(current.tableCounts.opponentHand).toEqual({ authored: 39, rendered: 39 });
      expect(current.tableCounts.discard).toEqual({ authored: 0, rendered: 0 });
      expect(current.riverGridCells).toBe(72);
      expect(current.clippedBodies).toEqual([]);
      fresh.push({ viewport, current });
      await owned.page.screenshot({ path: output(info, `fresh-link-${viewport.width}x${viewport.height}.png`) });
    }
    writeFileSync(output(info, 'fresh-table-measurements.json'), JSON.stringify(fresh, null, 2) + '\n');
    await owned.page.evaluate(() => {
      const url = new URL(location.href);
      url.searchParams.append('tableLayout', 'compact');
      history.replaceState(history.state, '', url);
    });
    await expect(owned.page.locator('body')).not.toHaveAttribute('data-table-layout', 'compact');
    expect(owned.errors).toEqual([]);
  } finally {
    if (!owned.page.isClosed()) await owned.context.close();
  }
});
