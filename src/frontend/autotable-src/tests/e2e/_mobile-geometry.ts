import type { Page } from '@playwright/test';
import type { Camera, Group, InstancedMesh, Mesh } from 'three';
import type { Slot } from '../../src/slot';

interface ProbeGame {
  client: { seat: number | null };
  mainView: { camera: Camera; playArea(): Bounds };
  mainGroup: Group;
  objectView: { thingGroups: Map<string, { startIndex: number; meshes: Mesh[]; instancedMesh: InstancedMesh }> };
  world: { slots: Map<string, Slot>; things: Map<number, {
    index: number; type: string; typeIndex: number; hidden: boolean;
    slot: { name: string };
  }> };
}

interface Bounds { left: number; right: number; top: number; bottom: number }
interface TileBounds extends Bounds {
  id: number; face: number; width: number; height: number; x: number; y: number; hit: boolean;
}
export interface GeometryReport {
  width: number; height: number; dpr: number; camera: string; seat: number | null;
  canvas: { left: number; top: number; width: number; height: number };
  pageWidth: number; pageHeight: number; table: Bounds | null;
  meshes: Array<Bounds & { id: number; face: number; slot: string }>;
  ownMeshCount: number; tiles: TileBounds[]; tray: boolean;
  chrome: Record<string, Bounds & { hit: boolean }>;
  playArea: Bounds;
  areas: Array<Bounds & { slot: string }>;
  actions: Array<Bounds & { id: string; hit: boolean }>;
}

/** Observe the actually submitted GLB meshes/instances, never slot origins alone. */
export async function mobileGeometry(page: Page): Promise<GeometryReport> {
  return page.evaluate(() => {
    const game = (window as unknown as { game: ProbeGame }).game;
    const camera = game.mainView.camera;
    const canvas = document.querySelector('#main canvas')!.getBoundingClientRect();
    const group = game.objectView.thingGroups.get('TILE')!;
    const bounds = (points: Array<{ x: number; y: number }>): Bounds => ({
      left: Math.min(...points.map(p => p.x)), right: Math.max(...points.map(p => p.x)),
      top: Math.min(...points.map(p => p.y)), bottom: Math.max(...points.map(p => p.y)),
    });
    const projected = (mesh: Mesh, index?: number): Bounds | null => {
      const geometry = mesh.visible || index === undefined ? mesh.geometry : group.instancedMesh.geometry;
      if (!geometry.boundingBox) geometry.computeBoundingBox();
      const box = geometry.boundingBox!;
      const matrix = mesh.matrix.clone();
      if (mesh.visible || index === undefined) matrix.copy(mesh.matrixWorld);
      else {
        group.instancedMesh.getMatrixAt(index, matrix);
        if (matrix.elements[0] === 0 && matrix.elements[5] === 0 && matrix.elements[10] === 0) return null;
        matrix.premultiply(group.instancedMesh.matrixWorld);
      }
      const points = [];
      for (const x of [box.min.x, box.max.x]) for (const y of [box.min.y, box.max.y]) for (const z of [box.min.z, box.max.z]) {
        const p = box.min.clone().set(x, y, z).applyMatrix4(matrix).project(camera);
        points.push({ x: canvas.left + (p.x + 1) * canvas.width / 2, y: canvas.top + (1 - p.y) * canvas.height / 2 });
      }
      return bounds(points);
    };
    const meshes = [];
    for (const thing of game.world.things.values()) {
      if (thing.hidden || thing.type !== 'TILE') continue;
      const index = thing.index - group.startIndex;
      const mesh = group.meshes[index];
      if (!mesh) continue;
      const rect = projected(mesh, index);
      if (rect) meshes.push({ id: thing.index, face: thing.typeIndex, slot: thing.slot.name, ...rect });
    }
    const ownMeshes = meshes.filter(mesh => /^hand\.\d+@/.test(mesh.slot) && mesh.slot.endsWith('@' + game.client.seat));
    const tray = document.getElementById('own-hand-tray');
    const tiles = tray?.getClientRects().length ? [...tray.querySelectorAll<HTMLButtonElement>('[data-tile-id]')].map(tile => {
      const r = tile.getBoundingClientRect();
      const x = (r.left + r.right) / 2, y = (r.top + r.bottom) / 2;
      const hit = document.elementFromPoint(x, y);
      return {
        id: Number(tile.dataset.tileId), face: Number(tile.dataset.face),
        left: r.left, right: r.right, top: r.top, bottom: r.bottom, width: r.width, height: r.height,
        x, y, hit: hit === tile || tile.contains(hit),
      };
    }) : ownMeshes.map(mesh => {
      const x = (mesh.left + mesh.right) / 2, y = (mesh.top + mesh.bottom) / 2;
      return { ...mesh, width: mesh.right - mesh.left, height: mesh.bottom - mesh.top, x, y,
        hit: document.elementFromPoint(x, y)?.tagName === 'CANVAS' };
    });
    const chrome: Record<string, { left: number; top: number; right: number; bottom: number; hit: boolean }> = {};
    for (const id of ['new-game', 'lobby-toggle', 'settings-button', 'settings-toggle', 'move-log-toggle',
      'bot-banner', 'turn-banner', 'pickup-hud', 'chat-panel']) {
      const element = document.getElementById(id);
      if (!element || !element.getClientRects().length || getComputedStyle(element).visibility === 'hidden') continue;
      const rect = element.getBoundingClientRect();
      const top = document.elementFromPoint((rect.left + rect.right) / 2, (rect.top + rect.bottom) / 2);
      chrome[id] = { left: rect.left, right: rect.right, top: rect.top, bottom: rect.bottom,
        hit: !!top && (top === element || element.contains(top)) };
    }
    const table = game.mainGroup.getObjectByName('table') as Mesh | undefined;
    const areas = [];
    const geometry = group.instancedMesh.geometry;
    if (!geometry.boundingBox) geometry.computeBoundingBox();
    const box = geometry.boundingBox!;
    for (const slot of game.world.slots.values()) {
      if (!/^(wall|discard|meld)\.\d+\.\d+@\d$/.test(slot.name)) continue;
      const place = slot.placeWithOffset(0);
      const matrix = group.instancedMesh.matrix.clone().compose(place.position, place.rotation, place.size.clone().setScalar(1));
      matrix.premultiply(group.instancedMesh.matrixWorld);
      const points = [];
      for (const x of [box.min.x, box.max.x]) for (const y of [box.min.y, box.max.y]) for (const z of [box.min.z, box.max.z]) {
        const p = box.min.clone().set(x, y, z).applyMatrix4(matrix).project(camera);
        points.push({ x: canvas.left + (p.x + 1) * canvas.width / 2, y: canvas.top + (1 - p.y) * canvas.height / 2 });
      }
      areas.push({ slot: slot.name, ...bounds(points) });
    }
    const actions = [];
    for (const element of document.querySelectorAll<HTMLElement>(
      '#pickup-take-btn:not([disabled]), #roll-dice:not([disabled]), .ferro-claim-overlay-visible button:not([disabled]), #own-turn-action-panel button:not([disabled])',
    )) {
      if (!element.getClientRects().length || getComputedStyle(element).visibility === 'hidden') continue;
      const r = element.getBoundingClientRect();
      const hit = document.elementFromPoint((r.left + r.right) / 2, (r.top + r.bottom) / 2);
      actions.push({ id: element.id || element.className, left: r.left, right: r.right, top: r.top, bottom: r.bottom,
        hit: hit === element || element.contains(hit) });
    }
    const available = game.mainView.playArea();
    return {
      width: innerWidth, height: innerHeight, dpr: devicePixelRatio,
      camera: camera.type, seat: game.client.seat,
      canvas: { left: canvas.left, top: canvas.top, width: canvas.width, height: canvas.height },
      pageWidth: document.documentElement.scrollWidth, pageHeight: document.documentElement.scrollHeight,
      table: table ? projected(table) : null, meshes, ownMeshCount: ownMeshes.length,
      tiles, chrome, tray: !!tray?.getClientRects().length,
      areas, actions,
      playArea: { left: available.left + canvas.left, right: available.right + canvas.left,
        top: available.top + canvas.top, bottom: available.bottom + canvas.top },
    };
  });
}
