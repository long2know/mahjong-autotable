import { test, expect, type Browser, type Page, type TestInfo } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { relative } from 'node:path';
import type { OrthographicCamera, PerspectiveCamera, Vector3 } from 'three';
import { newActor, closeLobby, probe, type Actor } from './_lobby-repair';
import { buildGameUrl, makeConfig, ensureConnected } from './_playability';
import { mobileGeometry } from './_mobile-geometry';

interface Bounds { left: number; top: number; right: number; bottom: number }
interface CameraGame {
  mainView: {
    camera: PerspectiveCamera | OrthographicCamera;
    fitCorners: Vector3[];
    mobileDomHandActive: boolean;
    mobileFooterInset: number;
    playArea(): Bounds;
  };
  world: { conditions: { gameType: string }; viewSeat: number | null; hovered: { index: number } | null };
  client: { connectionMode: string };
  zoom: { pos: number };
  lookDown: { pos: number };
}
interface CameraFrame {
  type: string;
  elevation: number;
  gameType: string;
  connectionMode: string;
  seat: number | null;
  domHand: boolean;
  footerInset: number;
  position: number[];
  rotation: number[];
  projection: number[];
  zoom: number;
  lookDown: number;
  canvas: { width: number; height: number };
  area: Bounds;
  anchors: Array<{ x: number; y: number }>;
  center: { x: number; y: number };
}
interface CameraWindow extends Window {
  game: CameraGame;
  __mobileCameraRead?: () => CameraFrame;
  __mobileCameraCapture?: { running: boolean; truncated: boolean; frames: CameraFrame[] };
}

const PORTRAIT = { width: 390, height: 844 };
const ANCHOR_TOLERANCE_PX = .25;

async function frames(page: Page, count = 3): Promise<void> {
  await page.evaluate(n => new Promise<void>(resolve => {
    const next = (): void => { if (--n <= 0) resolve(); else requestAnimationFrame(next); };
    requestAnimationFrame(next);
  }), count);
}

async function cameraFrame(page: Page): Promise<CameraFrame> {
  return page.evaluate(() => {
    const win = window as unknown as CameraWindow;
    const read = (): CameraFrame => {
      const game = win.game;
      const view = game.mainView, camera = view.camera;
      const canvas = document.querySelector('#main canvas')!.getBoundingClientRect();
      const project = (point: Vector3): { x: number; y: number } => {
        const p = point.clone().project(camera);
        return { x: canvas.left + (p.x + 1) * canvas.width / 2, y: canvas.top + (1 - p.y) * canvas.height / 2 };
      };
      const direction = camera.getWorldDirection(view.fitCorners[0].clone());
      const center = view.fitCorners[0].clone().add(view.fitCorners[7]).multiplyScalar(.5);
      center.z = 0;
      const area = view.playArea();
      return {
        type: camera.type,
        elevation: Math.asin(Math.max(-1, Math.min(1, -direction.z))) * 180 / Math.PI,
        gameType: game.world.conditions.gameType, connectionMode: game.client.connectionMode,
        seat: game.world.viewSeat, domHand: view.mobileDomHandActive, footerInset: view.mobileFooterInset,
        position: camera.position.toArray(), rotation: [camera.rotation.x, camera.rotation.y, camera.rotation.z],
        projection: camera.projectionMatrix.elements.slice(), zoom: game.zoom.pos, lookDown: game.lookDown.pos,
        canvas: { width: canvas.width, height: canvas.height },
        area: { left: canvas.left + area.left, right: canvas.left + area.right,
          top: canvas.top + area.top, bottom: canvas.top + area.bottom },
        anchors: view.fitCorners.map(project), center: project(center),
      };
    };
    win.__mobileCameraRead = read;
    return read();
  });
}

async function startCapture(page: Page): Promise<void> {
  await page.evaluate(() => {
    const win = window as unknown as CameraWindow;
    const capture = { running: true, truncated: false, frames: [] as CameraFrame[] };
    win.__mobileCameraCapture = capture;
    const sample = (): void => {
      if (!capture.running) return;
      if (capture.frames.length >= 4000) {
        capture.running = false;
        capture.truncated = true;
        return;
      }
      capture.frames.push(win.__mobileCameraRead!());
      requestAnimationFrame(sample);
    };
    requestAnimationFrame(sample);
  });
}

async function stopCapture(page: Page): Promise<{ truncated: boolean; frames: CameraFrame[] }> {
  return page.evaluate(() => {
    const capture = (window as unknown as CameraWindow).__mobileCameraCapture!;
    capture.running = false;
    return { truncated: capture.truncated, frames: capture.frames };
  });
}

function anchorDeviation(before: CameraFrame, after: CameraFrame): number {
  return Math.max(...after.anchors.map((point, i) =>
    Math.hypot(point.x - before.anchors[i].x, point.y - before.anchors[i].y)));
}

function stationary(before: CameraFrame, after: CameraFrame, stage: string): void {
  expect(after.type, `${stage}: camera mode`).toBe(before.type);
  expect(after.position, `${stage}: pose`).toEqual(before.position);
  expect(after.rotation, `${stage}: orientation`).toEqual(before.rotation);
  expect(after.area, `${stage}: stable play area`).toEqual(before.area);
  expect(after.domHand, `${stage}: DOM hand ownership`).toBe(before.domHand);
  expect(anchorDeviation(before, after), `${stage}: authored table anchors`).toBeLessThan(ANCHOR_TOLERANCE_PX);
  expect(Math.max(...after.projection.map((value, i) => Math.abs(value - before.projection[i]))),
    `${stage}: projection fit survives input rebuild`).toBeLessThan(1e-10);
}

function fitted(frame: CameraFrame): void {
  for (const point of frame.anchors) {
    expect(point.x).toBeGreaterThanOrEqual(frame.area.left - .01);
    expect(point.x).toBeLessThanOrEqual(frame.area.right + .01);
    expect(point.y).toBeGreaterThanOrEqual(frame.area.top - .01);
    expect(point.y).toBeLessThanOrEqual(frame.area.bottom + .01);
  }
}

async function perspective(page: Page, enabled = true): Promise<void> {
  await page.getByTestId('settings-button').click();
  await page.getByTestId('settings-tab-display').click();
  await page.getByTestId('settings-perspective-toggle').setChecked(enabled);
  await page.getByTestId('settings-close').click();
  await expect.poll(async () => (await cameraFrame(page)).type)
    .toBe(enabled ? 'PerspectiveCamera' : 'OrthographicCamera');
  await frames(page);
}

async function openLayout(
  browser: Browser, baseURL: string | undefined, info: TestInfo, flags: string[],
  viewport = PORTRAIT, variant = 'changsha',
): Promise<Actor> {
  const mobile = viewport.width <= 900;
  const context = await browser.newContext({ viewport, hasTouch: mobile, isMobile: mobile, deviceScaleFactor: 1 });
  try {
    const url = new URL(buildGameUrl(baseURL ?? 'http://localhost:8080/autotable/', makeConfig({
      gameId: `mobile-camera-${randomUUID().slice(0, 8)}`, variant,
      dealMode: 'auto', botCount: variant === 'changsha' ? 3 : 0, botDifficulty: 'Medium',
      handCount: 4, seed: 4100,
    })));
    for (const flag of flags) url.searchParams.append('mobileLayout', flag);
    const actor = await newActor(browser, baseURL, info, 'mobile-camera', context, url.href);
    await closeLobby(actor);
    await expect(actor.page.locator('body')).toHaveAttribute('data-scene-effects-ready', 'true');
    expect(await ensureConnected(actor.page), 'the test must use the real variant connection').toBe(true);
    await perspective(actor.page);
    await actor.page.mouse.move(0, 0);
    await frames(actor.page);
    return actor;
  } catch (error) {
    await context.close();
    throw error;
  }
}

async function evidence(page: Page, info: TestInfo, label: string, value: unknown): Promise<void> {
  const filename = relative(process.cwd(), info.outputPath(`${label}.png`));
  await page.screenshot({ path: filename });
  await info.attach(label, { path: filename, contentType: 'image/png' });
  await info.attach(`${label}-camera`, { body: JSON.stringify(value, null, 2), contentType: 'application/json' });
}

test.describe('bounded mobile camera experiment', () => {
  test.beforeEach(async ({}, info) => {
    test.skip(info.project.name !== 'chromium', 'Explicit mobile and desktop contexts are covered once in Chromium.');
    test.setTimeout(75_000);
  });

  for (const [name, flags] of [
    ['absent', []], ['invalid', ['steeper']], ['duplicate', ['camera', 'camera']],
  ] as const) {
    test(`${name} flag leaves portrait framing at the existing seated elevation`, async ({ browser, baseURL }, info) => {
      const actor = await openLayout(browser, baseURL, info, [...flags]);
      try {
        const frame = await cameraFrame(actor.page);
        expect(frame.gameType).toBe('CHANGSHA');
        expect(frame.elevation).toBeCloseTo(36, 8);
        expect(frame.domHand).toBe(false);
        fitted(frame);
        await evidence(actor.page, info, 'baseline-off', frame);
        expect(actor.errors).toEqual([]);
      } finally { await actor.context.close(); }
    });
  }

  for (const mode of ['camera', 'hybrid'] as const) {
    for (const scenario of [
      { name: 'desktop', viewport: { width: 1280, height: 900 }, variant: 'changsha', world: 'CHANGSHA' },
      { name: 'landscape', viewport: { width: 844, height: 390 }, variant: 'changsha', world: 'CHANGSHA' },
      { name: 'relay portrait', viewport: PORTRAIT, variant: 'four_player', world: 'FOUR_PLAYER' },
    ]) {
      test(`${mode} flag leaves ${scenario.name} untouched`, async ({ browser, baseURL }, info) => {
        const actor = await openLayout(browser, baseURL, info, [mode], scenario.viewport, scenario.variant);
        try {
          const frame = await cameraFrame(actor.page);
          expect(frame.gameType).toBe(scenario.world);
          expect(frame.connectionMode).toBe(scenario.variant === 'changsha' ? 'changsha' : 'relay');
          expect(frame.elevation).toBeCloseTo(36, 8);
          expect(frame.position).toEqual([0, -174 * 1.44, 174 * 1.05]);
          expect(frame.domHand).toBe(false);
          await evidence(actor.page, info, 'flag-off-scope', frame);
          expect(actor.errors).toEqual([]);
        } finally { await actor.context.close(); }
      });
    }

    test(`${mode} portrait has a stable fitted 60-degree camera, independent controls and hand changes`, async ({ browser, baseURL }, info) => {
      const actor = await openLayout(browser, baseURL, info, [mode]);
      const page = actor.page;
      try {
        await expect.poll(async () => (await probe(actor)).handCount).toBe(14);
        await expect.poll(async () => (await cameraFrame(page)).domHand).toBe(mode === 'hybrid');
        await frames(page);
        const before = await cameraFrame(page);
        expect(before.elevation).toBeCloseTo(60, 8);
        expect(before.zoom).toBe(0);
        expect(before.lookDown).toBe(0);
        expect(before.canvas).toEqual(PORTRAIT);
        if (mode === 'hybrid') expect(before.footerInset).toBeGreaterThan(0);
        fitted(before);
        expect((await mobileGeometry(page)).ownMeshCount).toBe(mode === 'hybrid' ? 0 : 14);
        await evidence(page, info, 'portrait-before', before);

        const refreshed = await page.evaluate(() => {
          const win = window as unknown as CameraWindow;
          win.game.mainView.camera.updateProjectionMatrix();
          return win.__mobileCameraRead!();
        });
        stationary(before, refreshed, 'input projection refresh');
        await page.mouse.move(before.center.x, before.center.y);
        await page.mouse.down();
        await frames(page);
        stationary(before, await cameraFrame(page), 'ordinary canvas press');
        await page.mouse.move(before.center.x + 30, before.center.y + 20, { steps: 3 });
        await frames(page);
        stationary(before, await cameraFrame(page), 'selection drag');
        await page.mouse.move(1, PORTRAIT.height / 2);
        await page.mouse.up();
        await frames(page);
        stationary(before, await cameraFrame(page), 'selection released');

        for (const key of ['Space', 'z']) {
          await page.keyboard.down(key);
          try {
            await expect.poll(async () => anchorDeviation(before, await cameraFrame(page))).toBeGreaterThan(10);
          } finally { await page.keyboard.up(key); }
          await expect.poll(async () => anchorDeviation(before, await cameraFrame(page))).toBeLessThan(ANCHOR_TOLERANCE_PX);
        }
        await perspective(page, false);
        expect((await cameraFrame(page)).elevation).toBeCloseTo(45, 8);
        await perspective(page);
        stationary(before, await cameraFrame(page), 'perspective toggle restored');

        await page.setViewportSize({ width: 844, height: 390 });
        await expect.poll(async () => (await cameraFrame(page)).elevation).toBeCloseTo(36, 8);
        await expect.poll(async () => (await cameraFrame(page)).domHand).toBe(false);
        await page.setViewportSize(PORTRAIT);
        await expect.poll(async () => (await cameraFrame(page)).elevation).toBeCloseTo(60, 8);
        await expect.poll(async () => (await cameraFrame(page)).domHand).toBe(mode === 'hybrid');
        await frames(page);
        const restored = await cameraFrame(page);
        stationary(before, restored, 'portrait orientation restored');

        const sent = actor.sent.length;
        await startCapture(page);
        let tileId: number;
        if (mode === 'hybrid') {
          const dock = page.getByRole('region', { name: 'Your hand — select a tile, then confirm discard' });
          const tile = dock.getByRole('button', { name: /; tap to select, tap again to cancel$/ }).nth(4);
          tileId = Number(await tile.getAttribute('data-tile-id'));
          await tile.click();
          await frames(page);
          stationary(restored, await cameraFrame(page), 'DOM hand selection');
          expect(actor.sent.slice(sent).flatMap(frame => frame.entries ?? []).filter(([kind]) => kind === 'discard')).toEqual([]);
          await dock.getByRole('button', { name: /^Confirm discard of / }).click();
        } else {
          const tile = (await mobileGeometry(page)).tiles[4];
          tileId = tile.id;
          await page.mouse.move(tile.x, tile.y);
          await expect.poll(() => page.evaluate(() =>
            (window as unknown as { game: CameraGame }).game.world.hovered?.index)).toBe(tileId);
          await page.mouse.click(tile.x, tile.y);
        }
        await expect.poll(() => actor.sent.slice(sent).flatMap(frame => frame.entries ?? [])
          .filter(([kind]) => kind === 'discard').map(([, , value]) => value?.tileId)).toEqual([tileId]);
        await expect.poll(async () => (await probe(actor)).handIds).not.toContain(tileId);
        for (let i = 0; i < 12; i++) {
          await frames(page, 3);
        }
        const observed = await stopCapture(page);
        expect(observed.truncated, 'all intermediate camera frames must be observed').toBe(false);
        expect(observed.frames.length).toBeGreaterThan(12);
        for (const frame of observed.frames) stationary(restored, frame, 'authoritative hand/turn/claim update');
        await evidence(page, info, 'portrait-after-discard', { tileId, before: restored, observed });
        expect(actor.errors).toEqual([]);
      } finally { await actor.context.close(); }
    });
  }
});
