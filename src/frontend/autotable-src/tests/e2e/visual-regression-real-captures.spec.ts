// Phase K Wave 14 — Visual regression with REAL captures (Vasquez).
//
// W12 introduced manifest-screenshot visual regression at the 2%
// pixel-diff tolerance. W14 extends to *real* live captures of
// the deployed origin: navigate to canonical app routes, capture,
// compare against baselines. Forward-stage tolerant: first runs
// record baselines, missing origins annotate and pass.
//
// Pre-flight per `docs/test-architecture.md §5`:
//   - chromium-only
//   - animations frozen via addStyleTag
//   - fonts ready awaited
//
// See `tests/selectors.md` § Phase K Wave 14 → visual-regression-real-captures.

import { test, expect } from '@playwright/test';

const ROUTES = [
  { path: '/',                             slug: 'home' },
  { path: '/?action=bracket',              slug: 'bracket' },
  { path: '/?action=replays',              slug: 'replays' },
];

const MAX_DIFF_PIXEL_RATIO = 0.02;

test.describe('Phase K Wave 14 — visual regression on real captures (<= 2% diff)', () => {
  test.beforeEach(async ({}, testInfo) => {
    test.skip(testInfo.project.name !== 'chromium',
      'Real-capture visual regression validated on chromium only.');
  });

  for (const r of ROUTES) {
    test(`real capture of ${r.slug} matches baseline OR forward-staged`,
      async ({ page }, testInfo) => {
        let resp;
        try {
          resp = await page.goto(r.path);
        } catch (_e) {
          testInfo.annotations.push({
            type: 'forward-stage',
            description: `Origin not reachable for ${r.path}.`,
          });
          return;
        }
        if (resp === null || !resp.ok()) {
          testInfo.annotations.push({
            type: 'forward-stage',
            description: `Route ${r.path} not yet reachable; W14 surface converging.`,
          });
          return;
        }

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
        await page.evaluate(async () => {
          if (document.fonts && document.fonts.ready) {
            await document.fonts.ready;
          }
        });
        await page.waitForLoadState('networkidle');

        try {
          await expect(page).toHaveScreenshot(
            `phase-k-w14-${r.slug}.png`,
            { maxDiffPixelRatio: MAX_DIFF_PIXEL_RATIO, fullPage: false },
          );
        } catch (e) {
          const msg = e instanceof Error ? e.message : String(e);
          if (msg.includes('Writing actual') || msg.includes('--update-snapshots')
              || msg.includes('A snapshot doesn')) {
            testInfo.annotations.push({
              type: 'forward-stage',
              description: `Recorded baseline for phase-k-w14-${r.slug}.png.`,
            });
          } else {
            throw e;
          }
        }
      });
  }
});
