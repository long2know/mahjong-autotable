import { test, expect, type Browser, type Page, type TestInfo } from '@playwright/test';
import { mkdirSync, writeFileSync, readFileSync, existsSync } from 'node:fs';
import { join } from 'node:path';
import type { Camera, Group, InstancedMesh, Mesh } from 'three';
import { newActor, applyRoom, closeLobby, dismissPrompts, probe, driveManualCeremony, type Actor } from './_lobby-repair';
import { mobileGeometry, waitForBoardHand } from './_mobile-geometry';

const captureStage = process.env.MOBILE_LAYOUT_CAPTURE_STAGE ?? 'final';
const exactViewport = { width: 412, height: 924 };
const portraitViewports = [exactViewport, { width: 360, height: 800 }, { width: 390, height: 844 }];
const guardViewports = [{ width: 844, height: 390 }, { width: 1280, height: 900 }];

type Rect = { left: number; right: number; top: number; bottom: number; width: number; height: number };
type LayoutMode = 'off' | 'camera' | 'hybrid';

interface PresentationReport {
  camera: { type: string; position: number[]; quaternion: number[]; projection: number[] };
  worldVariant: string | null;
  resources: string[];
  handWire: Array<{ id: number; face: number | null | undefined; slot: string }>;
  projectedFaces: Array<Rect & { id: number; samples: number }>;
  pickableHandIds: number[];
  dock: {
    visible: boolean; rect: Rect | null; context: string; contextRect: Rect | null; busy: string | null;
    tiles: Array<Rect & {
      id: number; face: string | null; pressed: string | null; disabled: boolean;
      hit: boolean; faceRect: Rect | null; row: number; drawn: string | null;
    }>;
    actions: Array<Rect & { id: string; disabled: boolean; hit: boolean }>;
  };
}

test.use({
  launchOptions: {
    args: [
      '--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader',
      '--force-color-profile=srgb', '--disable-lcd-text', '--hide-scrollbars', '--mute-audio',
    ],
  },
});

function evidencePath(testInfo: TestInfo, filename: string): string {
  const root = process.env.MOBILE_LAYOUT_ARTIFACT_DIR;
  if (!root) return testInfo.outputPath(filename);
  mkdirSync(root, { recursive: true });
  return join(root, filename);
}

function saveJson(testInfo: TestInfo, filename: string, value: unknown): void {
  writeFileSync(evidencePath(testInfo, filename), JSON.stringify(value, null, 2) + '\n');
}

async function seededActor(
  browser: Browser, baseURL: string | undefined, testInfo: TestInfo, label: string,
  viewport = exactViewport, destination?: string,
  options: { dealMode?: 'manual' | 'auto'; seed?: number; botDifficulty?: 'Easy' | 'Medium' | 'Hard' } = {},
): Promise<Actor> {
  const context = await browser.newContext({
    viewport, deviceScaleFactor: 1, hasTouch: true, isMobile: true, reducedMotion: 'reduce',
  });
  try {
    const actor = await newActor(browser, baseURL, testInfo, label, context, destination);
    await actor.page.locator('#lobby-hand-count-fieldset label:has(input[value="1"])').click();
    const difficulty = options.botDifficulty ?? 'Hard';
    await actor.page.locator(`#lobby-bot-difficulty-fieldset label:has(input[value="${difficulty}"])`).click();
    if (!await actor.page.locator('#lobby-seed').isVisible())
      await actor.page.locator('#lobby-advanced summary').click();
    await actor.page.locator('#lobby-seed').fill(String(options.seed ?? 4100));
    const dealMode = options.dealMode ?? 'auto';
    await applyRoom(actor, 3, 0, dealMode);
    await closeLobby(actor);
    await expect.poll(async () => {
      const state = await probe(actor);
      return { hand: state.handCount, seat: state.seat, phase: state.turn?.phase };
    }, { timeout: 30_000 }).toEqual({
      hand: dealMode === 'auto' ? 14 : 0, seat: 0, phase: dealMode === 'auto' ? 'AwaitingDiscard' : 'RollingDice',
    });
    if (dealMode === 'auto') expect((await probe(actor)).turn?.activeSeat).toBe(0);
    await expect(actor.page.locator('body')).toHaveAttribute('data-scene-effects-ready', 'true');
    return actor;
  } catch (error) {
    await context.close();
    throw error;
  }
}

async function presentation(page: Page): Promise<PresentationReport> {
  return page.evaluate(() => {
    interface ProbeGame {
      client: {
        seat: number | null;
        things: { entries(): Iterable<[string | number, { slotName?: string; face?: number | null } | null]> };
      };
      mainView: { camera: Camera };
      mainGroup: Group;
      mouseUi: { currentObjects: Mesh[] };
      objectView: { thingGroups: Map<string, { startIndex: number; meshes: Mesh[]; instancedMesh: InstancedMesh }> };
      world: { conditions: { gameType: string }; things: Map<number, { index: number; hidden: boolean; slot: { name: string } }> };
    }
    const game = (window as unknown as { game: ProbeGame }).game;
    const camera = game.mainView.camera;
    const handWire = [...game.client.things.entries()]
      .filter(([id, thing]) => typeof id === 'number'
        && thing?.slotName?.startsWith('hand.') && thing.slotName.endsWith('@' + game.client.seat))
      .map(([id, thing]) => ({ id: id as number, face: thing!.face, slot: thing!.slotName! }))
      .sort((a, b) => a.id - b.id);
    const rect = (element: Element): Rect => {
      const r = element.getBoundingClientRect();
      return { left: r.left, right: r.right, top: r.top, bottom: r.bottom, width: r.width, height: r.height };
    };
    const visible = (element: HTMLElement): boolean =>
      element.getClientRects().length > 0 && getComputedStyle(element).visibility !== 'hidden';
    const hit = (element: HTMLElement): boolean => {
      const r = rect(element);
      const target = document.elementFromPoint((r.left + r.right) / 2, (r.top + r.bottom) / 2);
      return !!target && (target === element || element.contains(target));
    };
    const dock = document.getElementById('mobile-hand-dock');
    const buttons = dock ? [...dock.querySelectorAll<HTMLButtonElement>('.mobile-hand-tile')].filter(visible) : [];
    const rows = [...new Set(buttons.map(button => Math.round(rect(button).top)))].sort((a, b) => a - b);
    const group = game.objectView.thingGroups.get('TILE')!;
    const canvas = document.querySelector('#main canvas')!.getBoundingClientRect();
    const geometry = group.instancedMesh.geometry;
    const position = geometry.getAttribute('position');
    const uv = geometry.getAttribute('uv');
    const projectedFaces = [];
    for (const owned of handWire) {
      const thing = game.world.things.get(owned.id);
      const index = owned.id - group.startIndex;
      const mesh = group.meshes[index];
      if (!thing || thing.hidden || !mesh) continue;
      const matrix = mesh.matrix.clone();
      if (mesh.visible) matrix.copy(mesh.matrixWorld);
      else {
        group.instancedMesh.getMatrixAt(index, matrix);
        if (matrix.elements[0] === 0 && matrix.elements[5] === 0 && matrix.elements[10] === 0) continue;
        matrix.premultiply(group.instancedMesh.matrixWorld);
      }
      const points = [];
      // The submitted GLB's label surface is the first 32x40 cell of its 256px atlas.
      for (let vertex = 0; vertex < position.count; vertex++) {
        if (uv.getX(vertex) > 32 / 256 + .00001 || uv.getY(vertex) > 40 / 256 + .00001) continue;
        const p = mesh.position.clone().fromBufferAttribute(position, vertex).applyMatrix4(matrix).project(camera);
        points.push({ x: canvas.left + (p.x + 1) * canvas.width / 2, y: canvas.top + (1 - p.y) * canvas.height / 2 });
      }
      if (!points.length) continue;
      const left = Math.min(...points.map(point => point.x)), right = Math.max(...points.map(point => point.x));
      const top = Math.min(...points.map(point => point.y)), bottom = Math.max(...points.map(point => point.y));
      projectedFaces.push({ id: owned.id, samples: points.length, left, right, top, bottom, width: right - left, height: bottom - top });
    }
    return {
      camera: { type: camera.type, position: camera.position.toArray(), quaternion: camera.quaternion.toArray(),
        projection: camera.projectionMatrix.toArray() },
      worldVariant: game.world.conditions.gameType,
      resources: performance.getEntriesByType('resource').map(resource => resource.name)
        .filter(url => /\.(?:js|css)(?:\?|$)/.test(url)).sort(),
      handWire, projectedFaces,
      pickableHandIds: game.mouseUi.currentObjects.map(mesh => mesh.userData.id as number)
        .filter(id => handWire.some(tile => tile.id === id)).sort((a, b) => a - b),
      dock: {
        visible: !!dock && visible(dock), rect: dock && visible(dock) ? rect(dock) : null,
        context: dock?.querySelector('.mobile-hand-context')?.textContent ?? '', busy: dock?.getAttribute('aria-busy') ?? null,
        contextRect: dock?.querySelector('.mobile-hand-context')
          ? rect(dock.querySelector('.mobile-hand-context')!) : null,
        tiles: buttons.map(button => {
          const face = button.querySelector('.mobile-hand-tile-face');
          return { ...rect(button), id: Number(button.dataset.tileId), face: button.dataset.face ?? null,
            pressed: button.getAttribute('aria-pressed'), disabled: button.disabled, hit: hit(button),
            faceRect: face ? rect(face) : null, row: rows.indexOf(Math.round(rect(button).top)),
            drawn: button.getAttribute('data-drawn') };
        }),
        actions: dock ? [...dock.querySelectorAll<HTMLButtonElement>('.mobile-hand-actions button')]
          .filter(visible).map(button => ({ ...rect(button), id: button.id || button.className, disabled: button.disabled, hit: hit(button) })) : [],
      },
    };
  });
}

async function captureBoard(actor: Actor, testInfo: TestInfo, name: string) {
  await actor.page.evaluate(() => new Promise<void>(resolve =>
    requestAnimationFrame(() => requestAnimationFrame(() => resolve()))));
  const geometry = await mobileGeometry(actor.page);
  const observed = await presentation(actor.page);
  const state = await probe(actor);
  const report = {
    name, url: actor.page.url(), seed: Number(new URL(actor.page.url()).searchParams.get('seed')),
    state, geometry, presentation: observed,
    tableHeightCssPx: geometry.table ? geometry.table.bottom - geometry.table.top : null,
    tableWidthCssPx: geometry.table ? geometry.table.right - geometry.table.left : null,
    canvasHeightCssPx: geometry.canvas.height,
    onBoardTileBodies: geometry.tiles.map(tile => ({ id: tile.id, face: tile.face, width: tile.width, height: tile.height })),
    consoleErrors: actor.errors, httpFailures: actor.httpFailures,
  };
  await actor.page.screenshot({ path: evidencePath(testInfo, name + '.png'), animations: 'disabled' });
  saveJson(testInfo, name + '.json', report);
  return report;
}

type ComparisonReport = Awaited<ReturnType<typeof captureBoard>>;

function overlaps(a: Pick<Rect, 'left' | 'right' | 'top' | 'bottom'>, b: Pick<Rect, 'left' | 'right' | 'top' | 'bottom'>): boolean {
  return Math.min(a.right, b.right) > Math.max(a.left, b.left) + .1
    && Math.min(a.bottom, b.bottom) > Math.max(a.top, b.top) + .1;
}

function expectReachable(report: ComparisonReport, hybrid: boolean, handCount = 14): void {
  const { geometry, presentation: observed } = report;
  expect(geometry.pageWidth).toBeLessThanOrEqual(geometry.width);
  expect(geometry.pageHeight).toBeLessThanOrEqual(geometry.height);
  expect(geometry.canvas.width).toBe(geometry.width);
  expect(geometry.canvas.height).toBe(geometry.height);
  expect(observed.worldVariant).toBe('CHANGSHA');
  expect(observed.dock.visible).toBe(hybrid);
  expect(geometry.table).not.toBeNull();
  for (const r of [geometry.table!, ...geometry.meshes]) {
    expect(r.left, JSON.stringify(r)).toBeGreaterThanOrEqual(-.02);
    expect(r.right, JSON.stringify(r)).toBeLessThanOrEqual(geometry.width + .02);
    expect(r.top, JSON.stringify(r)).toBeGreaterThanOrEqual(-.02);
    expect(r.bottom, JSON.stringify(r)).toBeLessThanOrEqual(geometry.height + .02);
  }
  for (const id of ['new-game', 'lobby-toggle', 'settings-button', 'settings-toggle', 'move-log-toggle']) {
    const chrome = geometry.chrome[id];
    if (!chrome && id === 'move-log-toggle' && geometry.width > 900) continue;
    expect(chrome, id).toBeTruthy();
    expect(chrome.hit, id + ' must receive an ordinary press').toBe(true);
  }
  if (!hybrid) {
    expect(geometry.ownMeshCount).toBe(handCount);
    expect(observed.projectedFaces).toHaveLength(handCount);
    expect(geometry.tiles.every(tile => tile.hit), 'all physical board tile IDs remain reachable').toBe(true);
    return;
  }
  expect(geometry.ownMeshCount, 'concealed tiles are presented once, not duplicated on the board').toBe(0);
  expect(observed.pickableHandIds, 'hidden 3D hand meshes must not remain pickable').toEqual([]);
  expect(observed.dock.tiles).toHaveLength(handCount);
  expect(new Set(observed.dock.tiles.map(tile => tile.id)).size).toBe(handCount);
  expect(observed.dock.tiles.filter(tile => tile.row === 0)).toHaveLength(Math.min(handCount, 7));
  expect(observed.dock.tiles.filter(tile => tile.row === 1)).toHaveLength(Math.max(0, handCount - 7));
  expect(observed.dock.tiles.map(tile => tile.id).sort((a, b) => a - b)).toEqual(report.state.handIds.slice().sort((a, b) => a - b));
  for (const [id, chrome] of Object.entries(geometry.chrome))
    expect(overlaps(observed.dock.contextRect!, chrome), id + ' covers the claim/last-discard context').toBe(false);
  for (const tile of observed.dock.tiles) {
    expect(tile.width).toBeGreaterThanOrEqual(44);
    expect(tile.height).toBeGreaterThanOrEqual(44);
    expect(tile.hit, 'DOM tile ' + tile.id).toBe(true);
    if (report.state.turn?.awaitingDiscard && report.state.turn.activeSeat === report.state.seat)
      expect(tile.disabled, 'human AwaitingDiscard must permit selection of physical tile ' + tile.id).toBe(false);
    expect(tile.faceRect, 'real atlas face must be measurable').not.toBeNull();
    expect(tile.faceRect!.width).toBeGreaterThanOrEqual(32);
    expect(tile.faceRect!.height).toBeGreaterThanOrEqual(40);
    for (const [id, chrome] of Object.entries(geometry.chrome))
      expect(overlaps(tile, chrome), id + ' covers hand tile ' + tile.id).toBe(false);
    for (const action of observed.dock.actions)
      expect(overlaps(tile, action), action.id + ' covers hand tile ' + tile.id).toBe(false);
  }
  for (const action of observed.dock.actions) {
    expect(action.width).toBeGreaterThanOrEqual(44);
    expect(action.height).toBeGreaterThanOrEqual(44);
    expect(action.hit, action.id + ' must receive an ordinary press').toBe(true);
  }
  for (const publicMesh of geometry.meshes.filter(mesh => /^(wall|discard|meld)\./.test(mesh.slot)))
    expect(overlaps(publicMesh, observed.dock.rect!), 'dock covers public tile ' + publicMesh.id).toBe(false);
}

async function navigateFlags(actor: Actor, flags: string[], handCount = 14, phase = 'AwaitingDiscard'): Promise<void> {
  const url = new URL(actor.page.url());
  url.searchParams.delete('mobileLayout');
  for (const flag of flags) url.searchParams.append('mobileLayout', flag);
  await actor.page.goto(url.href, { waitUntil: 'domcontentloaded' });
  await dismissPrompts(actor);
  await closeLobby(actor);
  await expect.poll(async () => {
    const state = await probe(actor);
    return state.connected && state.seat === 0 && state.handCount === handCount && state.turn?.phase === phase;
  }, { timeout: 30_000 }).toBe(true);
  await expect(actor.page.locator('body')).toHaveAttribute('data-scene-effects-ready', 'true');
}

interface HandObservation {
  ids: number[]; rows: number[]; selected: number[]; pending: number[]; busy: string | null;
}

async function observeHandTransitions(page: Page): Promise<void> {
  await page.evaluate(() => {
    const observations: HandObservation[] = [];
    let previous = '';
    const record = (): void => {
      const dock = document.getElementById('mobile-hand-dock');
      if (!dock) return;
      const buttons = [...dock.querySelectorAll<HTMLButtonElement>('.mobile-hand-tile')];
      const tops = [...new Set(buttons.map(button => Math.round(button.getBoundingClientRect().top)))].sort((a, b) => a - b);
      const value = {
        ids: buttons.map(button => Number(button.dataset.tileId)),
        rows: tops.map(top => buttons.filter(button => Math.round(button.getBoundingClientRect().top) === top).length),
        selected: buttons.filter(button => button.getAttribute('aria-pressed') === 'true').map(button => Number(button.dataset.tileId)),
        pending: buttons.filter(button => button.dataset.pending === 'true').map(button => Number(button.dataset.tileId)),
        busy: dock.getAttribute('aria-busy'),
      };
      const signature = JSON.stringify(value);
      if (signature === previous) return;
      previous = signature;
      observations.push(value);
    };
    const dock = document.getElementById('mobile-hand-dock')!;
    new MutationObserver(record).observe(dock, {
      childList: true, subtree: true, attributes: true, attributeFilter: ['aria-busy', 'aria-pressed', 'data-pending'],
    });
    dock.addEventListener('click', record);
    Object.assign(window, { __mobileLayoutHandObservations: observations });
    record();
  });
}

async function handObservations(page: Page): Promise<HandObservation[]> {
  return page.evaluate(() =>
    (window as unknown as { __mobileLayoutHandObservations: HandObservation[] }).__mobileLayoutHandObservations);
}

function discardCommands(actor: Actor, since: number) {
  return actor.sent.slice(since).flatMap(frame => frame.entries ?? []).filter(([kind]) => kind === 'discard');
}

test('capture unchanged served-bundle baseline at exactly 412x924 CSS pixels', async ({ browser, baseURL }, testInfo) => {
  test.skip(captureStage !== 'baseline', 'Baseline is captured explicitly before rebuilding, never overwritten by final runs.');
  test.setTimeout(60_000);
  const actor = await seededActor(browser, baseURL, testInfo, 'mobile-layout-baseline');
  try {
    await waitForBoardHand(actor.page);
    await expect(actor.page.locator('#mobile-hand-dock')).toHaveCount(0);
    const report = await captureBoard(actor, testInfo, 'baseline-412x924');
    await testInfo.attach('baseline-measurements', { body: JSON.stringify(report, null, 2), contentType: 'application/json' });
    expect(actor.errors).toEqual([]);
  } finally { await actor.context.close(); }
});

test('capture unchanged supplementary portrait and desktop/landscape baselines', async ({ browser, baseURL }, testInfo) => {
  test.skip(captureStage !== 'baseline', 'Only the explicit pre-build capture stage may write baseline images.');
  test.setTimeout(60_000);
  const actor = await seededActor(browser, baseURL, testInfo, 'mobile-layout-baseline-guards');
  try {
    for (const viewport of [...portraitViewports.slice(1), ...guardViewports]) {
      await actor.page.setViewportSize(viewport);
      await waitForBoardHand(actor.page);
      const report = await captureBoard(actor, testInfo, `baseline-${viewport.width}x${viewport.height}`);
      expectReachable(report, false);
    }
    expect(actor.errors).toEqual([]);
  } finally { await actor.context.close(); }
});

test('same authoritative seeded hand compares off/camera/hybrid at exact CSS viewports', async ({ browser, baseURL }, testInfo) => {
  test.skip(captureStage === 'baseline', 'Candidate comparisons require the integrated rebuilt bundle.');
  test.setTimeout(120_000);
  const actor = await seededActor(browser, baseURL, testInfo, 'mobile-layout-comparison');
  try {
    const initialIds = (await probe(actor)).handIds.slice().sort((a, b) => a - b);
    const baselinePath = evidencePath(testInfo, 'baseline-412x924.json');
    if (existsSync(baselinePath)) {
      const baseline = JSON.parse(readFileSync(baselinePath, 'utf8')) as ComparisonReport;
      expect(initialIds, 'seed4100 must reproduce the pre-build authoritative human hand').toEqual(baseline.state.handIds.slice().sort((a, b) => a - b));
    }
    const reports: ComparisonReport[] = [];
    for (const viewport of [...portraitViewports, ...guardViewports]) {
      await actor.page.setViewportSize(viewport);
      const variants: Partial<Record<LayoutMode, ComparisonReport>> = {};
      for (const mode of ['off', 'camera', 'hybrid'] as const) {
        await navigateFlags(actor, mode === 'off' ? [] : [mode]);
        const report = await captureBoard(actor, testInfo, `${mode}-${viewport.width}x${viewport.height}`);
        reports.push(report);
        variants[mode] = report;
        const active = viewport.width <= 600 && viewport.height > viewport.width;
        expectReachable(report, active && mode === 'hybrid');
        expect(report.state.handIds.slice().sort((a, b) => a - b)).toEqual(initialIds);
        expect(report.consoleErrors).toEqual([]);
        expect(report.presentation.resources.some(url => /renderer-webgl2[.]/.test(url)),
          'mobile layout must not opt into the experimental renderer').toBe(false);
      }
      const { off, camera, hybrid } = variants as Record<LayoutMode, ComparisonReport>;
      expect(camera.presentation.handWire).toEqual(off.presentation.handWire);
      expect(hybrid.presentation.handWire).toEqual(off.presentation.handWire);
      if (viewport.width <= 600 && viewport.height > viewport.width) {
        expect(camera.tableHeightCssPx!).toBeGreaterThan(off.tableHeightCssPx! * 1.2);
        expect(hybrid.tableHeightCssPx!, 'hybrid must not buy readable tiles by shrinking the public board')
          .toBeGreaterThanOrEqual(camera.tableHeightCssPx! * .9);
      } else {
        expect(camera.presentation.camera, 'camera flag does not affect desktop/landscape').toEqual(off.presentation.camera);
        expect(hybrid.presentation.camera, 'hybrid flag does not affect desktop/landscape').toEqual(off.presentation.camera);
        expect(camera.geometry.table).toEqual(off.geometry.table);
        expect(hybrid.geometry.table).toEqual(off.geometry.table);
      }
    }
    saveJson(testInfo, 'comparison-measurements.json', reports);
    await testInfo.attach('comparison-measurements', { body: JSON.stringify(reports, null, 2), contentType: 'application/json' });
  } finally { await actor.context.close(); }
});

test('hybrid real taps select physical IDs, explicit Discard round-trips once, and 13 tiles use 7+6', async ({ browser, baseURL }, testInfo) => {
  test.skip(captureStage === 'baseline', 'Interaction checks require the integrated rebuilt bundle.');
  test.setTimeout(60_000);
  const actor = await seededActor(browser, baseURL, testInfo, 'hybrid-discard');
  const receivedThings: Array<[string, string | number, { slotName?: string } | null]> = [];
  actor.page.on('websocket', socket => socket.on('framereceived', event => {
    for (const fragment of String(event.payload).split('\u001e').filter(Boolean)) {
      try {
        const frame = JSON.parse(fragment) as { entries?: typeof receivedThings };
        receivedThings.push(...(frame.entries ?? []).filter(([kind]) => kind === 'things'));
      } catch { /* Non-game frames do not contain collection updates. */ }
    }
  }));
  try {
    await navigateFlags(actor, ['hybrid']);
    const initial = await captureBoard(actor, testInfo, 'hybrid-interaction-before-412x924');
    expectReachable(initial, true);
    const first = initial.presentation.dock.tiles.find(tile =>
      initial.presentation.dock.tiles.some(other => other.id !== tile.id && Math.floor(other.id / 4) === Math.floor(tile.id / 4)))!;
    const second = initial.presentation.dock.tiles.find(tile =>
      tile.id !== first.id && Math.floor(tile.id / 4) === Math.floor(first.id / 4))!;
    expect(first).toBeTruthy();
    expect(second).toBeTruthy();
    await observeHandTransitions(actor.page);
    const before = actor.sent.length;
    await expect(actor.page.locator(`.mobile-hand-tile[data-tile-id="${first.id}"]`)).toBeEnabled();
    await actor.page.locator(`.mobile-hand-tile[data-tile-id="${first.id}"]`).tap();
    await expect(actor.page.locator(`.mobile-hand-tile[data-tile-id="${first.id}"]`)).toHaveAttribute('aria-pressed', 'true');
    expect(discardCommands(actor, before)).toEqual([]);
    expect((await probe(actor)).handIds).toEqual(initial.state.handIds);
    await actor.page.locator(`.mobile-hand-tile[data-tile-id="${second.id}"]`).tap();
    await expect(actor.page.locator(`.mobile-hand-tile[data-tile-id="${first.id}"]`)).toHaveAttribute('aria-pressed', 'false');
    await expect(actor.page.locator(`.mobile-hand-tile[data-tile-id="${second.id}"]`)).toHaveAttribute('aria-pressed', 'true');
    expect(discardCommands(actor, before)).toEqual([]);
    expect((await probe(actor)).handIds).toEqual(initial.state.handIds);
    const selected = await captureBoard(actor, testInfo, 'hybrid-selected-412x924');
    expectReachable(selected, true);
    const discard = actor.page.locator('.mobile-hand-discard');
    await expect(discard).toBeEnabled();
    await expect(discard).toHaveAttribute('data-tile-id', String(second.id));
    await discard.tap();
    await expect.poll(() => discardCommands(actor, before).map(([, , value]) => value?.tileId)).toEqual([second.id]);
    await expect.poll(() => receivedThings.some(([, id, value]) =>
      Number(id) === second.id && value?.slotName?.startsWith('discard.'))).toBe(true);
    await expect.poll(async () => (await probe(actor)).handIds).not.toContain(second.id);
    await expect.poll(async () => (await handObservations(actor.page)).some(observation =>
      observation.ids.length === 13 && observation.rows.join(',') === '7,6')).toBe(true);
    const transitions = await handObservations(actor.page);
    expect(transitions.some(observation =>
      observation.busy === 'true' && observation.pending.includes(second.id) && observation.ids.length === 14),
    'the actual pending command retains the hand until server confirmation').toBe(true);
    await expect.poll(async () => {
      const state = await probe(actor);
      return state.turn?.awaitingDiscard && state.turn.activeSeat === 0;
    }, { timeout: 30_000 }).toBe(true);
    await actor.page.getByTestId('settings-button').tap();
    await expect(actor.page.getByTestId('settings-drawer')).toBeVisible();
    await actor.page.getByTestId('settings-close').click();
    await actor.page.getByTestId('chat-toggle').tap();
    const after = await captureBoard(actor, testInfo, 'hybrid-after-discard-open-chat-412x924');
    expectReachable(after, true, after.state.handCount);
    saveJson(testInfo, 'hybrid-discard-confirmation.json', {
      selectedSameFaceIds: [first.id, second.id], command: discardCommands(actor, before),
      authoritativeDiscard: receivedThings.filter(([, id, value]) =>
        Number(id) === second.id && value?.slotName?.startsWith('discard.')),
      transitions, after,
    });
    expect(discardCommands(actor, before)).toHaveLength(1);
    expect(actor.errors).toEqual([]);
  } finally { await actor.context.close(); }
});

test('hybrid reuses live dice/pickup controls and reversibly restores them on rotation', async ({ browser, baseURL }, testInfo) => {
  test.skip(captureStage === 'baseline', 'Manual control checks require the integrated rebuilt bundle.');
  test.setTimeout(90_000);
  const actor = await seededActor(browser, baseURL, testInfo, 'hybrid-manual', exactViewport, undefined, { dealMode: 'manual' });
  try {
    const originalParents = await actor.page.evaluate(() => Object.fromEntries(
      ['roll-dice', 'pickup-hud', 'own-turn-actions'].map(id => {
        const parent = document.getElementById(id)!.parentElement!;
        return [id, parent.id || parent.tagName];
      })));
    await navigateFlags(actor, ['hybrid'], 0, 'RollingDice');
    await expect(actor.page.locator('#mobile-hand-dock #roll-dice')).toBeVisible();
    expectReachable(await captureBoard(actor, testInfo, 'hybrid-manual-roll-412x924'), true, 0);
    await actor.page.locator('#roll-dice').tap();
    await expect.poll(async () => {
      const state = await probe(actor);
      return state.pickup?.seatIndex === 0 && state.pickup.count === 4;
    }).toBe(true);
    await expect(actor.page.locator('#mobile-hand-dock #pickup-take-btn')).toBeVisible();
    await expect(actor.page.locator('#pickup-take-btn')).toHaveCount(1);
    await actor.page.locator('#pickup-take-btn').tap();
    await expect.poll(async () => (await probe(actor)).handCount).toBe(4);
    const partial = await captureBoard(actor, testInfo, 'hybrid-manual-pickup-412x924');
    expectReachable(partial, true, 4);
    await driveManualCeremony([actor]);
    await expect.poll(async () => (await probe(actor)).handCount).toBe(14);
    expectReachable(await captureBoard(actor, testInfo, 'hybrid-manual-complete-412x924'), true);
    await actor.page.setViewportSize(guardViewports[0]);
    await expect(actor.page.locator('#mobile-hand-dock')).toHaveCount(0);
    await waitForBoardHand(actor.page);
    const restoredParents = await actor.page.evaluate(() => Object.fromEntries(
      ['roll-dice', 'pickup-hud', 'own-turn-actions'].map(id => {
        const parent = document.getElementById(id)!.parentElement!;
        return [id, parent.id || parent.tagName];
      })));
    expect(restoredParents).toEqual(originalParents);
    await actor.page.setViewportSize(exactViewport);
    await expect(actor.page.locator('.mobile-hand-tile')).toHaveCount(14);
    expectReachable(await captureBoard(actor, testInfo, 'hybrid-restored-portrait-412x924'), true);
    saveJson(testInfo, 'hybrid-manual-controls.json', { originalParents, restoredParents, partial });
    expect(actor.errors).toEqual([]);
  } finally { await actor.context.close(); }
});

test('the opt-in survives ordinary lobby Apply without changing renderer flighting', async ({ browser, baseURL }, testInfo) => {
  test.skip(captureStage === 'baseline', 'Startup checks require the integrated rebuilt bundle.');
  test.setTimeout(60_000);
  const destination = new URL(baseURL ?? 'http://127.0.0.1:5114/autotable/');
  destination.searchParams.set('mobileLayout', 'hybrid');
  const actor = await seededActor(browser, baseURL, testInfo, 'hybrid-lobby-apply', exactViewport, destination.href);
  try {
    expect(new URL(actor.page.url()).searchParams.getAll('mobileLayout')).toEqual(['hybrid']);
    await expect(actor.page.locator('.mobile-hand-tile')).toHaveCount(14);
    expect((await presentation(actor.page)).resources.some(url => /renderer-webgl2[.]/.test(url))).toBe(false);
    const url = new URL(actor.page.url());
    url.searchParams.set('renderer', 'webgl2-hello');
    await actor.page.goto(url.href, { waitUntil: 'domcontentloaded' });
    await dismissPrompts(actor);
    await closeLobby(actor);
    await expect(actor.page.getByTestId('webgl2-hello-container')).toBeVisible();
    await expect(actor.page.getByTestId('webgl2-hello-canvas')).toBeVisible();
    const renderer = await actor.page.evaluate(() => ({
      mode: new URLSearchParams(location.search).get('renderer'),
      experimentalRequests: performance.getEntriesByType('resource').map(resource => resource.name)
        .filter(url => /renderer-webgl2[.]/.test(url)),
    }));
    expect(renderer.mode).toBe('webgl2-hello');
    expect(renderer.experimentalRequests).toHaveLength(1);
    saveJson(testInfo, 'renderer-flight-guard.json', renderer);
    expect(actor.errors).toEqual([]);
  } finally { await actor.context.close(); }
});

test('actual relay world and connection ignore both portrait flags', async ({ browser, baseURL }, testInfo) => {
  test.skip(captureStage === 'baseline', 'Relay guards require the integrated rebuilt bundle.');
  test.setTimeout(60_000);
  const context = await browser.newContext({ viewport: exactViewport, deviceScaleFactor: 1, hasTouch: true, isMobile: true });
  const url = new URL(baseURL ?? 'http://127.0.0.1:5114/autotable/');
  url.search = new URLSearchParams({
    gameId: `ferro-relay-${Date.now().toString(36)}`, variant: 'four_player', seat: '0', mobileLayout: 'hybrid',
  }).toString();
  try {
    const actor = await newActor(browser, baseURL, testInfo, 'relay-mobile-layout', context, url.href);
    const states = [];
    for (const mode of ['hybrid', 'camera', 'off'] as const) {
      if (mode !== 'hybrid') {
        url.searchParams.delete('mobileLayout');
        if (mode !== 'off') url.searchParams.set('mobileLayout', mode);
        await actor.page.goto(url.href, { waitUntil: 'domcontentloaded' });
        await dismissPrompts(actor);
      }
      await closeLobby(actor);
      await expect(actor.page.locator('body')).toHaveAttribute('data-scene-effects-ready', 'true');
      await expect.poll(() => actor.page.evaluate(() => {
        const game = (window as unknown as { game?: { client: { connectionMode: string; connected(): boolean }; world: { conditions: { gameType: string } } } }).game;
        return game?.client.connected() && game.client.connectionMode === 'relay' && game.world.conditions.gameType === 'FOUR_PLAYER';
      })).toBe(true);
      await expect(actor.page.locator('#mobile-hand-dock')).toHaveCount(0);
      const observed = await presentation(actor.page);
      expect(observed.worldVariant).toBe('FOUR_PLAYER');
      states.push({ mode, observed });
    }
    expect(states[0].observed.camera).toEqual(states[2].observed.camera);
    expect(states[1].observed.camera).toEqual(states[2].observed.camera);
    saveJson(testInfo, 'relay-flag-guard.json', states);
  } finally { await context.close(); }
});

test('a genuine meld uses the original hybrid claim actuator and keeps exposed meld meshes on the board', async ({ browser, baseURL }, testInfo) => {
  test.skip(captureStage === 'baseline', 'Public-meld interaction checks require the integrated rebuilt bundle.');
  test.setTimeout(150_000);
  const actor = await seededActor(browser, baseURL, testInfo, 'hybrid-live-meld', exactViewport, undefined, {
    seed: 12345, botDifficulty: 'Easy',
  });
  const turns: unknown[] = [];
  try {
    await navigateFlags(actor, ['hybrid']);
    let claimed = false;
    for (let step = 0; step < 40 && !claimed; step++) {
      saveJson(testInfo, 'hybrid-live-claim-progress.json', {
        step, turns, state: await probe(actor),
        claims: await actor.page.evaluate(() => [...(window as unknown as {
          game: { client: { claim: { entries(): Iterable<[string | number, unknown]> } } };
        }).game.client.claim.entries()]),
      });
      await expect.poll(() => actor.page.evaluate(() => {
        const client = (window as unknown as { game: { client: {
          seat: number; turn: { get(key: string): { activeSeat: number; awaitingDiscard: boolean } | null };
          gameComplete: { get(key: string): { isComplete?: boolean } | null };
        } } }).game.client;
        const claim = document.querySelector<HTMLElement>('.ferro-claim-overlay-visible:not([hidden])');
        return !!document.querySelector('.mobile-hand-tile:not([disabled])')
          && client.turn.get('current')?.awaitingDiscard && client.turn.get('current')?.activeSeat === client.seat
          || !!claim?.querySelector('.ferro-claim-badge-available:not([disabled])')
          || client.gameComplete.get('current')?.isComplete === true;
      }), { timeout: 20_000, intervals: [50, 100, 250, 500] }).toBe(true);
      const available = await actor.page.locator('.ferro-claim-overlay-visible:not([hidden]) .ferro-claim-badge-available:not([disabled])')
        .evaluateAll(buttons => buttons.map(button => (button as HTMLElement).dataset.claimType!));
      const type = available.includes('Pung') ? 'Pung' : available.includes('Chow') ? 'Chow' : null;
      if (type !== null) {
        const button = actor.page.locator(`#mobile-hand-dock .ferro-claim-badge-${type.toLowerCase()}`);
        await expect(button).toBeEnabled();
        await expect(actor.page.locator('.ferro-claim-overlay')).toHaveCount(1);
        const claimView = await captureBoard(actor, testInfo, 'hybrid-live-claim-412x924');
        expectReachable(claimView, true, claimView.state.handCount);
        await button.tap();
        if (await actor.page.locator('.rule-action-dialog[open]').isVisible())
          await actor.page.locator('.rule-action-dialog[open] .rule-action-choice').first().tap();
        await expect.poll(async () => {
          const layout = await mobileGeometry(actor.page);
          return layout.meshes.filter(mesh => /^meld\./.test(mesh.slot) && mesh.slot.endsWith('@0')).length;
        }).toBeGreaterThanOrEqual(3);
        claimed = true;
        turns.push({ available, type, claimView });
      } else if (available.length > 0) {
        await actor.page.locator('#mobile-hand-dock .ferro-claim-pass').tap();
        turns.push({ available, action: 'pass' });
        await expect.poll(() => actor.page.locator('.ferro-claim-overlay-visible').count()).toBe(0);
      } else {
        const state = await probe(actor);
        expect(state.turn?.awaitingDiscard && state.turn.activeSeat === 0,
          'the genuine fixture must reach a human turn or claim before the hand ends').toBe(true);
        const ids = state.handIds.slice().sort((a, b) => a - b);
        const id = ids.slice().sort((a, b) => {
          const count = (tile: number) => ids.filter(other => Math.floor(other / 4) === Math.floor(tile / 4)).length;
          return count(a) - count(b) || a - b;
        })[0];
        await actor.page.locator(`.mobile-hand-tile[data-tile-id="${id}"]`).tap();
        await actor.page.locator('.mobile-hand-discard').tap();
        await expect.poll(async () => (await probe(actor)).handIds).not.toContain(id);
        turns.push({ handIds: ids, discarded: id });
      }
    }
    expect(claimed, 'the exposed meld must come from a genuine claim, never a synthetic collection update').toBe(true);
    await expect.poll(async () => {
      const state = await probe(actor);
      return state.turn?.awaitingDiscard && state.turn.activeSeat === 0;
    }).toBe(true);
    const exposed = await captureBoard(actor, testInfo, 'hybrid-public-meld-412x924');
    expectReachable(exposed, true, exposed.state.handCount);
    const melds = exposed.geometry.meshes.filter(mesh => /^meld\./.test(mesh.slot) && mesh.slot.endsWith('@0'));
    expect(melds.length).toBeGreaterThanOrEqual(3);
    const id = exposed.state.handIds[0];
    const commandStart = actor.sent.length;
    await actor.page.locator(`.mobile-hand-tile[data-tile-id="${id}"]`).tap();
    await actor.page.locator('.mobile-hand-discard').tap();
    await expect.poll(() => discardCommands(actor, commandStart).map(([, , value]) => value?.tileId)).toEqual([id]);
    await expect.poll(async () => (await probe(actor)).handIds).not.toContain(id);
    saveJson(testInfo, 'hybrid-public-meld-proof.json', {
      fixture: { seed: 12345, difficulty: 'Easy' }, turns, exposed, melds,
      postMeldDiscard: discardCommands(actor, commandStart),
    });
    expect(actor.errors).toEqual([]);
  } catch (error) {
    if (!actor.page.isClosed()) {
      saveJson(testInfo, 'hybrid-live-claim-failure.json', {
        turns, state: await probe(actor), presentation: await presentation(actor.page),
        sent: actor.sent, received: actor.received, errors: actor.errors,
      });
      await actor.page.screenshot({ path: evidencePath(testInfo, 'hybrid-live-claim-failure-412x924.png') });
    }
    throw error;
  } finally {
    if (!actor.page.isClosed()) await actor.context.close();
  }
});

test('full CSS width and strict portrait height enforce the 600px opt-in boundary', async ({ browser, baseURL }, testInfo) => {
  test.skip(captureStage === 'baseline', 'Boundary checks require the integrated rebuilt bundle.');
  test.setTimeout(60_000);
  const actor = await seededActor(browser, baseURL, testInfo, 'hybrid-threshold');
  try {
    await navigateFlags(actor, ['hybrid']);
    const results = [];
    for (const viewport of [
      { width: 600, height: 924, active: true },
      { width: 601, height: 924, active: false },
      { width: 412, height: 412, active: false },
      { width: 412, height: 924, active: true },
    ]) {
      await actor.page.setViewportSize({ width: viewport.width, height: viewport.height });
      await expect(actor.page.locator('#mobile-hand-dock')).toHaveCount(viewport.active ? 1 : 0);
      await expect.poll(async () => (await mobileGeometry(actor.page)).ownMeshCount).toBe(viewport.active ? 0 : 14);
      const observed = await presentation(actor.page);
      expect(observed.dock.visible).toBe(viewport.active);
      results.push({ viewport, observed });
    }
    saveJson(testInfo, 'viewport-threshold-guard.json', results);
    expect(actor.errors).toEqual([]);
  } finally { await actor.context.close(); }
});

test('invalid, duplicate and absent flags fail closed on the real portrait table', async ({ browser, baseURL }, testInfo) => {
  test.skip(captureStage === 'baseline', 'Flag guards require the integrated rebuilt bundle.');
  test.setTimeout(60_000);
  const actor = await seededActor(browser, baseURL, testInfo, 'mobile-layout-flag-guards');
  try {
    const before = await captureBoard(actor, testInfo, 'guard-default-412x924');
    const results = [];
    for (const flags of [['invalid'], ['hybrid', 'hybrid'], ['camera', 'hybrid'], [''], ['CAMERA']]) {
      await navigateFlags(actor, flags);
      const observed = await presentation(actor.page);
      expect(observed.dock.visible).toBe(false);
      expect(observed.camera).toEqual(before.presentation.camera);
      expect((await mobileGeometry(actor.page)).ownMeshCount).toBe(14);
      results.push({ flags, observed });
    }
    saveJson(testInfo, 'flag-guard-measurements.json', results);
    expect(actor.errors).toEqual([]);
  } finally { await actor.context.close(); }
});
