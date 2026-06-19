// D3 frontend regression — flat vs perspective view-mode toggle (Hicks).
//
// Stephen's standing criterion: "flat and perspective view options from
// autotable."  The in-game view toggle is the `#perspective` checkbox
// (index.html) — wired in `game.ts:updateSettings()` to
// `MainView.setPerspective()`, which swaps the THREE camera between a
// `PerspectiveCamera` (perspective) and an `OrthographicCamera` (flat
// top-down).  The `P` key and the settings-drawer
// `settings-perspective-toggle` mirror the same `#perspective` input.
//
// This spec loads a game (mounts the heavy `three-renderer` chunk that
// publishes `window.game`), deals a dice-bearing FOUR_PLAYER hand so the
// tile walls AND the dice-bearing centre both render, then:
//   1. asserts the default camera is a PerspectiveCamera,
//   2. toggles to flat       → asserts the camera became Orthographic,
//   3. toggles back           → asserts it returned to Perspective,
//   4. confirms tiles + dice render in BOTH modes, and
//   5. captures a screenshot of each view under
//      playtest-artifacts/screenshots/hicks-regression-d3-<stamp>/.
//
// The camera-type read goes through the same debug `window.game` handle
// the Wave-9 three-mesh-pulse spec uses; see src/main-view.ts:makeCamera
// and src/center.ts:drawDice.

import { test, expect, type Page } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

// tests/e2e/ -> repo-root playtest-artifacts is five levels up (matches
// playtest-changsha.spec.ts).
const STAMP =
  process.env.HICKS_SHOT_STAMP ||
  new Date().toISOString().replace(/[:.]/g, '-');
const SHOT_DIR = path.resolve(
  __dirname,
  '../../../../../playtest-artifacts/screenshots',
  `hicks-regression-d3-${STAMP}`,
);

interface ViewProbe {
  cameraType: string;
  isPerspective: boolean;
  isOrthographic: boolean;
  tileCount: number;
  centerVisible: boolean;
  diceState: string | null;
  diceValues: [number, number] | null;
}

// Drive the centre canvas to paint the two dice pips so they are visible
// in the screenshot.  `Center.updateDice()` only flips `shouldDrawDice`
// on for ~1 s after a roll, so for a deterministic capture we set the
// flag directly (no setTimeout reset) and force a redraw.  This exercises
// the exact `Center.drawDice()` path a live roll uses.
async function paintDice(page: Page): Promise<void> {
  await page.evaluate(() => {
    const g: any = (window as any).game;
    const center = g?.objectView?.center ?? g?.world?.objectView?.center;
    if (!center) return;
    center.diceInfo = { dice: [3, 4], state: 'rolled' };
    center.shouldDrawDice = true;
    center.dirty = true;
    center.draw();
  });
  await page.waitForTimeout(150);
}

async function probe(page: Page): Promise<ViewProbe> {
  return page.evaluate(() => {
    const g: any = (window as any).game;
    const cam = g.mainView.camera;
    const center = g.objectView?.center ?? g.world?.objectView?.center ?? null;
    const dice = g.client.dice.get(0) ?? null;
    return {
      cameraType: cam.type,
      isPerspective: cam.isPerspectiveCamera === true,
      isOrthographic: cam.isOrthographicCamera === true,
      tileCount: g.world.things.size,
      centerVisible: center ? center.mesh.visible === true : false,
      diceState: dice ? dice.state : null,
      diceValues: dice ? dice.dice : null,
    };
  });
}

// Flip the canonical `#perspective` input and dispatch the `change` the
// game listens for (game.ts:setupEvents).  Returns once MainView has
// swapped the camera to the expected projection.
async function setPerspective(page: Page, on: boolean): Promise<void> {
  await page.evaluate((want) => {
    const cb = document.getElementById('perspective') as HTMLInputElement | null;
    if (cb === null) throw new Error('#perspective toggle not found');
    cb.checked = want;
    cb.dispatchEvent(new Event('change', { bubbles: true }));
  }, on);
  await page.waitForFunction((want) => {
    const g: any = (window as any).game;
    const cam = g?.mainView?.camera;
    if (!cam) return false;
    return want ? cam.isPerspectiveCamera === true : cam.isOrthographicCamera === true;
  }, on, { timeout: 10_000 });
}

async function mountGameWithDice(page: Page): Promise<void> {
  // A non-empty query string is what flips index.ts into bootstrapGame()
  // (the lobby-only path never fetches the renderer chunk).  We omit
  // ?gameId= on purpose: ClientUi.start() only joins a backend match when
  // the URL carries a gameId (client-ui.ts:getUrlState), so this mounts
  // the renderer in the standalone local-deal mode where World.deal is
  // authoritative and no server snapshot reconciles the dice away.  The
  // bundle + assets are still served by the Production-CSP backend.
  await page.goto('./?seat=0', {
    waitUntil: 'domcontentloaded',
  });

  // Clear any onboarding / tour overlay so it cannot intercept later.
  for (const sel of ['#tour-skip', '#onboarding-skip']) {
    const el = page.locator(sel);
    if (await el.isVisible().catch(() => false)) {
      await el.click({ force: true, timeout: 3000 }).catch(() => undefined);
    }
  }

  // The heavy three-renderer chunk mints this sentinel right after it
  // publishes window.game + game.start().
  await page
    .locator('[data-testid="three-renderer-ready"]')
    .waitFor({ state: 'attached', timeout: 90_000 });
  await page.waitForFunction(
    () => {
      const g: any = (window as any).game;
      return !!(g && g.world && g.client && g.mainView && g.mainView.camera);
    },
    undefined,
    { timeout: 30_000 },
  );

  // Deal a FOUR_PLAYER (Riichi) hand: it rolls dice (so the dice-bearing
  // centre becomes visible — Changsha hides it) and lays the full
  // 136-tile wall.  Drives the same World.deal path #deal uses.
  await page.evaluate(() => {
    const g: any = (window as any).game;
    g.world.seat = 0;
    g.world.deal('HANDS', { gameType: 'FOUR_PLAYER' });
  });
  // Let the deal's own 1 s dice-reset timer fire before we take control.
  await page.waitForTimeout(1300);
}

test.describe('D3 regression — flat + perspective view modes', () => {
  test.beforeEach(async ({}, testInfo) => {
    test.skip(
      testInfo.project.name !== 'chromium',
      'WebGL view-mode toggle validated on chromium only.',
    );
  });

  test('toggles perspective <-> flat camera with tiles + dice in both', async ({
    page,
  }) => {
    test.setTimeout(150_000);
    fs.mkdirSync(SHOT_DIR, { recursive: true });

    await mountGameWithDice(page);

    // ── Default: perspective ────────────────────────────────────────
    const initial = await probe(page);
    expect(
      initial.isPerspective,
      `expected default PerspectiveCamera, got ${initial.cameraType}`,
    ).toBe(true);
    expect(
      initial.tileCount,
      'expected a full tile wall to be dealt',
    ).toBeGreaterThan(100);
    expect(
      initial.centerVisible,
      'dice-bearing centre must be visible for a Riichi (FOUR_PLAYER) deal',
    ).toBe(true);
    expect(initial.diceState, 'dice must be rolled after the deal').toBe('rolled');
    expect(Array.isArray(initial.diceValues) && initial.diceValues.length === 2).toBe(
      true,
    );

    await paintDice(page);
    const persp = await probe(page);
    expect(persp.isPerspective).toBe(true);
    await page.screenshot({
      path: path.join(SHOT_DIR, 'perspective-view.jpg'),
      type: 'jpeg',
      quality: 88,
      fullPage: false,
    });

    // ── Toggle to flat (orthographic top-down) ──────────────────────
    await setPerspective(page, false);
    const flat = await probe(page);
    expect(
      flat.isOrthographic,
      `expected OrthographicCamera after flat toggle, got ${flat.cameraType}`,
    ).toBe(true);
    expect(flat.isPerspective).toBe(false);
    // Render surface is unchanged: tiles + dice still present.
    expect(flat.tileCount).toBeGreaterThan(100);
    expect(flat.centerVisible).toBe(true);
    expect(flat.diceState).toBe('rolled');

    await paintDice(page);
    await page.screenshot({
      path: path.join(SHOT_DIR, 'flat-view.jpg'),
      type: 'jpeg',
      quality: 88,
      fullPage: false,
    });

    // ── Toggle back to perspective ──────────────────────────────────
    await setPerspective(page, true);
    const restored = await probe(page);
    expect(
      restored.isPerspective,
      `expected PerspectiveCamera after toggling back, got ${restored.cameraType}`,
    ).toBe(true);
    expect(restored.isOrthographic).toBe(false);
    expect(restored.tileCount).toBeGreaterThan(100);

    // The camera projection genuinely changed between the two modes.
    expect(persp.cameraType).not.toEqual(flat.cameraType);

    // eslint-disable-next-line no-console
    console.log(
      `[view-mode-toggle] perspective=${persp.cameraType} flat=${flat.cameraType} ` +
        `tiles=${flat.tileCount} dice=${JSON.stringify(flat.diceValues)} ` +
        `screenshots=${SHOT_DIR}`,
    );
  });
});
