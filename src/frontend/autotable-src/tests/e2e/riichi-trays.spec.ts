import { test, expect, type Page, type TestInfo } from '@playwright/test';
import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import type { Camera, Mesh } from 'three';
import type { AssetLoader } from '../../src/asset-loader';
import type { Game } from '../../src/game';
import { newActor, closeLobby, probe } from './_lobby-repair';

const artifactDirectory = process.env.RIICHI_TRAY_ARTIFACT_DIR;
const stage = process.env.RIICHI_TRAY_CAPTURE_STAGE ?? 'after';
const viewports = [{ width: 1900, height: 1063 }, { width: 1280, height: 900 }];

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

async function trayGeometry(page: Page) {
  return page.evaluate(() => {
    const game = (window as Window & { game?: Game }).game;
    if (!game) throw new Error('The actual relay game is unavailable');
    const assets = Reflect.get(game, 'assetLoader') as AssetLoader;
    const view = Reflect.get(game, 'objectView') as { trayMesh: Mesh | null; center: { mesh: Mesh } };
    const mainView = Reflect.get(game, 'mainView') as {
      camera: Camera; tablePlayArea: { left: number; right: number; top: number; bottom: number };
    };
    const camera = mainView.camera;
    const tray = view.trayMesh;
    if (!tray) throw new Error('Riichi scoring trays were removed instead of repaired');
    const prototype = assets.meshes.tray.geometry;
    const original = prototype.getAttribute('position');
    const indices = prototype.index;
    const actual = tray.geometry.getAttribute('position');
    const vertices = indices?.count ?? original.count;
    const up = tray.position.clone().set(0, 0, 1);
    let maximumPositionError = 0;
    if (actual.count === vertices * 24) {
      for (let seat = 0; seat < 4; seat++) for (let column = 0; column < 6; column++) {
        const origin = tray.position.clone().set(25 + 24 * column - 87, -33 - 87, 0)
          .applyAxisAngle(up, seat * Math.PI / 2).add(tray.position.clone().set(87, 87, 0));
        for (let vertex = 0; vertex < vertices; vertex++) {
          const source = indices?.getX(vertex) ?? vertex;
          const expected = tray.position.clone().fromBufferAttribute(original, source)
            .applyAxisAngle(up, seat * Math.PI / 2).add(origin);
          const rendered = tray.position.clone().fromBufferAttribute(actual, (seat * 6 + column) * vertices + vertex);
          maximumPositionError = Math.max(maximumPositionError, expected.distanceTo(rendered));
        }
      }
    } else maximumPositionError = Infinity;
    tray.geometry.computeBoundingSphere();
    const projected = [];
    for (let vertex = 0; vertex < actual.count; vertex++) {
      const point = tray.position.clone().fromBufferAttribute(actual, vertex).applyMatrix4(tray.matrixWorld).project(camera);
      projected.push({ x: (point.x + 1) * innerWidth / 2, y: (1 - point.y) * innerHeight / 2 });
    }
    return {
      gameType: game.world.conditions.gameType,
      connectionMode: game.client.connectionMode,
      sourceVertices: original.count,
      sourceTriangleVertices: vertices,
      renderedTriangleVertices: actual.count,
      triangles: actual.count / 3,
      maximumPositionError,
      indexed: tray.geometry.index !== null,
      visible: tray.visible && tray.parent !== null,
      centerVisible: view.center.mesh.visible,
      radius: tray.geometry.boundingSphere?.radius,
      area: mainView.tablePlayArea,
      bounds: {
        left: Math.min(...projected.map(point => point.x)), right: Math.max(...projected.map(point => point.x)),
        top: Math.min(...projected.map(point => point.y)), bottom: Math.max(...projected.map(point => point.y)),
      },
    };
  });
}

for (const [variant, gameType] of [['four_player', 'FOUR_PLAYER'], ['three_player', 'THREE_PLAYER']]) {
  test(`actual ${variant} relay renders all original scoring-tray triangles`, async ({ browser, baseURL }, info) => {
    const context = await browser.newContext({ viewport: viewports[0], deviceScaleFactor: 1 });
    const graphicsErrors: string[] = [];
    context.on('page', page => page.on('console', message => {
      if (/THREE.*NaN|Computed radius is NaN|WebGLProgram.*ERROR/.test(message.text())) graphicsErrors.push(message.text());
    }));
    const entry = new URL(baseURL ?? 'http://127.0.0.1:5114/autotable/');
    entry.search = new URLSearchParams({
      gameId: `tray-${variant}-${Date.now().toString(36)}`, variant, seat: '0', tableLayout: 'compact',
    }).toString();
    const actor = await newActor(browser, baseURL, info, `tray-${variant}`, context, entry.href);
    try {
      await closeLobby(actor);
      await actor.page.waitForFunction(type => {
        const game = (window as Window & { game?: Game }).game;
        return game?.client.connected() && game.client.connectionMode === 'relay'
          && game.world.conditions.gameType === type && Reflect.get(game, 'gameUi') !== null;
      }, gameType);
      await actor.page.locator('.seat-button-0 .take-seat').click();
      await expect.poll(async () => (await probe(actor)).seat).toBe(0);
      await actor.page.locator('#deal').click();
      await expect.poll(async () => (await probe(actor)).handCount).toBe(13);
      const reports = [];
      for (const viewport of viewports) {
        await actor.page.setViewportSize(viewport);
        await actor.page.mouse.move(viewport.width / 2, 60);
        await actor.page.evaluate(() => new Promise<void>(resolve =>
          requestAnimationFrame(() => requestAnimationFrame(() => resolve()))));
        const geometry = await trayGeometry(actor.page);
        reports.push({ viewport, geometry });
        await actor.page.screenshot({ path: output(info, `${stage}-${variant}-${viewport.width}x${viewport.height}.png`) });
        writeFileSync(output(info, `${stage}-${variant}.json`), JSON.stringify(reports, null, 2) + '\n');
        expect(geometry.sourceVertices).toBe(108);
        expect(geometry.sourceTriangleVertices).toBe(180);
        expect(geometry.renderedTriangleVertices).toBe(4320);
        expect(geometry.triangles).toBe(1440);
        expect(geometry.maximumPositionError).toBeLessThan(.0001);
        expect(geometry.indexed).toBe(false);
        expect(geometry.visible).toBe(true);
        expect(geometry.centerVisible).toBe(true);
        expect(Number.isFinite(geometry.radius)).toBe(true);
        expect(geometry.bounds.left).toBeGreaterThanOrEqual(geometry.area.left - .5);
        expect(geometry.bounds.right).toBeLessThanOrEqual(geometry.area.right + .5);
        expect(geometry.bounds.top).toBeGreaterThanOrEqual(geometry.area.top - .5);
        expect(geometry.bounds.bottom).toBeLessThanOrEqual(geometry.area.bottom + .5);
        expect(Math.max(
          (geometry.bounds.right - geometry.bounds.left) / (geometry.area.right - geometry.area.left),
          (geometry.bounds.bottom - geometry.bounds.top) / (geometry.area.bottom - geometry.area.top),
        )).toBeGreaterThanOrEqual(.85);
        await expect(actor.page.locator('body')).not.toHaveAttribute('data-table-layout', 'compact');
        await actor.page.keyboard.press('p');
        await expect(actor.page.locator('#perspective')).not.toBeChecked();
        await actor.page.evaluate(() => new Promise<void>(resolve =>
          requestAnimationFrame(() => requestAnimationFrame(() => resolve()))));
        const flat = await trayGeometry(actor.page);
        expect(flat.renderedTriangleVertices).toBe(4320);
        expect(flat.bounds.left).toBeGreaterThanOrEqual(flat.area.left - .5);
        expect(flat.bounds.right).toBeLessThanOrEqual(flat.area.right + .5);
        expect(flat.bounds.top).toBeGreaterThanOrEqual(flat.area.top - .5);
        expect(flat.bounds.bottom).toBeLessThanOrEqual(flat.area.bottom + .5);
        await actor.page.screenshot({ path: output(info, `${stage}-${variant}-${viewport.width}x${viewport.height}-flat.png`) });
        await actor.page.keyboard.press('p');
        await expect(actor.page.locator('#perspective')).toBeChecked();
      }
      expect(actor.errors).toEqual([]);
      expect(graphicsErrors).toEqual([]);
    } finally {
      if (!actor.page.isClosed()) await context.close();
    }
  });
}
