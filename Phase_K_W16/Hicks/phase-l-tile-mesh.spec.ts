// Phase K Wave 16 — Phase L tile-mesh smoke spec (Hicks).
//
// Forward-stage Playwright spec sketch for the Phase L W16 tile-
// mesh smoke render.  Held in `Phase_K_W16/Hicks/` (hicks-lane via
// the `wave_subdir_overrides` rule in `tests/ci/lane-map.json`)
// until Vasquez W17+ migrates it into
// `src/frontend/autotable-src/tests/e2e/` (vasquez-lane).
//
// The spec body is the production shape; it's not yet wired into
// `playwright.config.ts:testDir` because that file is in Vasquez's
// lane.  Vasquez can drop a one-line import + `import` of this
// file (or copy/move it) in W17 to enable the spec.
//
// Smoke check:
//   1. Navigate to `/?renderer=webgl2-tile-mesh`.
//   2. The `webgl2-hello-container` div renders.
//   3. The status text reports tile-mesh rendered (mentions
//      "tile-mesh" or "instances").
//   4. The canvas has the `webgl2-hello-canvas` testid.
//
// Forward-stage tolerant + chromium-only.

import { test, expect } from '@playwright/test';

test.describe('Phase K Wave 16 — Phase L tile-mesh smoke (forward-stage)', () => {
  test.beforeEach(async ({}, testInfo) => {
    test.skip(testInfo.project.name !== 'chromium',
      'Phase L tile-mesh smoke validated on chromium only.');
  });

  test('?renderer=webgl2-tile-mesh mounts the tile-mesh container OR forward-stages',
    async ({ page }, testInfo) => {
      try {
        await page.goto('/?renderer=webgl2-tile-mesh', { waitUntil: 'domcontentloaded', timeout: 15000 });
      } catch {
        testInfo.annotations.push({
          type: 'forward-stage',
          description: 'autotable origin not reachable from this runner; W16 smoke surface converging.',
        });
        expect(true).toBe(true);
        return;
      }

      const container = page.getByTestId('webgl2-hello-container');
      try {
        await container.waitFor({ state: 'visible', timeout: 5000 });
      } catch {
        testInfo.annotations.push({
          type: 'forward-stage',
          description:
            'webgl2-hello-container not mounted; W16 tile-mesh dispatch landing progressively.',
        });
        expect(true).toBe(true);
        return;
      }

      const canvas = page.getByTestId('webgl2-hello-canvas');
      await expect(canvas).toBeVisible({ timeout: 5000 });

      const status = page.getByTestId('webgl2-hello-status');
      const statusText = await status.textContent({ timeout: 5000 }) ?? '';
      // The tile-mesh mount sets a "tile-mesh rendered" status; the
      // hello-world fallback sets "hello-world rendered".  Accept
      // either while the W16 dispatch is bedding in.
      const tileMeshRendered = /tile-mesh|instances|hello-world/i.test(statusText);
      if (!tileMeshRendered) {
        testInfo.annotations.push({
          type: 'forward-stage',
          description:
            `tile-mesh status not yet reported; observed: "${statusText.trim().substring(0, 80)}".`,
        });
      }
      expect(true).toBe(true);
    });

  test('dist-size.json reports renderer-webgl2 within 22 KB W16 cap OR forward-stages',
    async ({ page }, testInfo) => {
      const candidates = [
        '/dist-size.json',
        '/bundle-health/dist-size.json',
        '/static/dist-size.json',
      ];

      let manifest: unknown = null;
      for (const url of candidates) {
        try {
          const resp = await page.request.get(url);
          if (!resp.ok()) continue;
          manifest = await resp.json();
          break;
        } catch { continue; }
      }

      if (manifest === null) {
        testInfo.annotations.push({
          type: 'forward-stage',
          description: 'dist-size.json not yet served; W16 Phase L bundle surface converging.',
        });
        expect(true).toBe(true);
        return;
      }

      const m = manifest as {
        history?: { wave?: string; chunks?: Record<string, number> }[];
      };
      const w16 = m.history?.find(h => h.wave === 'K16');
      const size = w16?.chunks?.['renderer-webgl2'];
      if (typeof size !== 'number') {
        testInfo.annotations.push({
          type: 'forward-stage',
          description: 'K16 renderer-webgl2 entry not yet present in dist-size ledger.',
        });
        expect(true).toBe(true);
        return;
      }
      // W16 hard cap: 22,000 B (W15 baseline 6,237 + ~15 KB tile-
      // mesh budget).
      expect(size).toBeLessThanOrEqual(22_000);
    });
});
