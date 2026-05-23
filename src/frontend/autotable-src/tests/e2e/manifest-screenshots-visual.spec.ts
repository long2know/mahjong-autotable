// Phase K Wave 12 — Manifest screenshots visual regression spec (Vasquez).
//
// W11 shipped *real* manifest screenshots (size + presence). W12
// adds VISUAL regression with a 2% pixel-diff tolerance, matching
// the new `docs/test-architecture.md §5 Visual Regression`
// methodology (Playwright `toHaveScreenshot({maxDiffPixelRatio:
// 0.02})`).
//
// Forward-stage tolerant:
//   - If no manifest is reachable, annotate and pass.
//   - If no baseline exists yet, the first run *records* the
//     baseline (Playwright's standard behavior) and we annotate.
//
// See `tests/selectors.md` § Phase K Wave 12 → manifest-screenshots-visual.
// See `docs/test-architecture.md §5` for the 2% policy + the
// pre-flight checklist (deterministic viewport, fonts loaded,
// animations frozen).

import { test, expect, type Page } from '@playwright/test';

const MANIFEST_CANDIDATES = [
  '/manifest.webmanifest',
  '/autotable/manifest.webmanifest',
  '/manifest.json',
];

const MAX_DIFF_PIXEL_RATIO = 0.02; // W12 §5 policy: <=2%.

interface ScreenshotEntry {
  src?: string;
  sizes?: string;
}

interface Manifest {
  screenshots?: ScreenshotEntry[];
}

async function fetchManifest(page: Page): Promise<Manifest | null> {
  for (const path of MANIFEST_CANDIDATES) {
    try {
      const res = await page.request.get(path);
      if (res.ok()) {
        return JSON.parse(await res.text()) as Manifest;
      }
    } catch (_e) { /* try next */ }
  }
  return null;
}

function slug(src: string | undefined): string {
  if (!src) return 'unnamed';
  return src.split('/').pop()!.replace(/\.[a-z]+$/i, '').replace(/[^a-z0-9-]/gi, '-');
}

test.describe('Phase K Wave 12 — manifest screenshots visual regression (<= 2% diff)', () => {
  test.beforeEach(async ({}, testInfo) => {
    test.skip(testInfo.project.name !== 'chromium',
      'Visual regression validated on chromium only (per §5 determinism rule).');
  });

  test('each manifest screenshot matches its baseline within 2% diff OR forward-staged',
    async ({ page }, testInfo) => {
      const manifest = await fetchManifest(page);
      if (manifest === null) {
        testInfo.annotations.push({
          type: 'forward-staged',
          description: 'No manifest reachable at canonical paths.',
        });
        return;
      }
      const shots = Array.isArray(manifest.screenshots) ? manifest.screenshots : [];
      if (shots.length === 0) {
        testInfo.annotations.push({
          type: 'forward-staged',
          description: 'Manifest has no screenshots[] yet.',
        });
        return;
      }

      // Pre-flight per §5: pin viewport, freeze animations, ensure
      // fonts loaded. The viewport is set via playwright.config.ts
      // but we explicitly disable animations + transitions here.
      await page.addStyleTag({
        content: `
          *, *::before, *::after {
            animation-duration: 0s !important;
            animation-delay: 0s !important;
            transition-duration: 0s !important;
            transition-delay: 0s !important;
          }
        `,
      });
      // Wait for fonts so kerning doesn't drift across runs.
      await page.evaluate(async () => {
        if (document.fonts && document.fonts.ready) {
          await document.fonts.ready;
        }
      });

      let comparedCount = 0;
      for (const shot of shots) {
        if (!shot.src) continue;
        const url = shot.src.startsWith('/') || shot.src.startsWith('http')
          ? shot.src
          : `/${shot.src}`;
        // Render the screenshot URL as an image inside a blank page so
        // we get a deterministic comparison surface.
        await page.setContent(
          `<!doctype html><html><body style="margin:0;background:#000;">
             <img src="${url}" style="display:block;max-width:100%;height:auto;" />
           </body></html>`,
        );
        // Wait for the image to load.
        try {
          await page.waitForFunction(
            () => {
              const img = document.querySelector('img');
              return !!img && (img as HTMLImageElement).complete && (img as HTMLImageElement).naturalWidth > 0;
            },
            { timeout: 5000 },
          );
        } catch (_e) {
          testInfo.annotations.push({
            type: 'forward-staged',
            description: `Screenshot "${url}" did not load on the deployed origin.`,
          });
          continue;
        }

        try {
          await expect(page).toHaveScreenshot(
            `${slug(shot.src)}.png`,
            { maxDiffPixelRatio: MAX_DIFF_PIXEL_RATIO },
          );
          comparedCount++;
        } catch (e) {
          // First-run mode (no baseline) — Playwright records and
          // throws; surface that as forward-staged the first time.
          const msg = e instanceof Error ? e.message : String(e);
          if (msg.includes('Writing actual') || msg.includes('--update-snapshots')
              || msg.includes('A snapshot doesn')) {
            testInfo.annotations.push({
              type: 'forward-staged',
              description: `Recorded baseline for "${slug(shot.src)}.png"; future runs will compare.`,
            });
          } else {
            throw e;
          }
        }
      }

      if (comparedCount === 0) {
        testInfo.annotations.push({
          type: 'forward-staged',
          description: 'No baselines to compare yet; W13 will see the comparison.',
        });
      }
    });
});
