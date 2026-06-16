// Phase J Wave 5 — Playwright E2E scaffold (Apone).
// Phase K Wave 15 — snapshotPathTemplate convention (Hicks).
//
// The frontend is served by the .NET backend at /autotable/ (single-image
// Docker deploy from Phase J Wave 3). The smoke spec targets the rendered
// bundle, NOT the Parcel dev-server, so contributors run a real container
// before invoking this config (see ./README.md for the exact commands).
//
// CI override (.github/workflows/e2e-playwright.yml) sets
//   E2E_BASE_URL=http://localhost:8080/autotable/
// and runs `npm run e2e` from src/frontend/autotable-src/ after the
// Docker container is healthy.
//
// W15 — `snapshotPathTemplate` is standardised so every visual-regression
// spec (today: manifest-screenshots-visual.spec.ts; tomorrow: any spec
// that uses `expect(page).toHaveScreenshot(...)`) reads / writes to the
// same canonical baseline tree.  Layout:
//
//   tests/e2e/__screenshots__/<spec-filename>/<arg>
//
// where `<arg>` is the first positional argument passed to
// `toHaveScreenshot()` (e.g. `"main-game.png"`).  The capture script
// `scripts/capture-real-surfaces.js` writes PNGs to this exact path, so
// once a baseline is captured the spec compares against it with no extra
// configuration.  Documented in `docs/frontend-pwa-audit.md §7.2`.

import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: '.',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: process.env.CI ? 'github' : 'list',
  // W15 — canonical baseline tree.  Tokens documented at
  // https://playwright.dev/docs/api/class-testconfig#test-config-snapshot-path-template
  //   {testDir}            → <abs>/tests/e2e  (absolute testDir root)
  //   {testFileName}       → manifest-screenshots-visual.spec.ts
  //   {arg}                → first arg of toHaveScreenshot (sans extension)
  //   {ext}                → .png (or whatever the arg's extension was)
  // NB: the leading segment MUST be an absolute-base token (`{testDir}`),
  // NOT `{testFileDir}` — the latter is the test file's dir *relative to
  // testDir*, which is empty for specs that live directly in `tests/e2e`,
  // collapsing the template to the filesystem-root path `/__screenshots__/…`
  // (mkdir EACCES for non-root runners).  `{testDir}` yields the intended
  // `tests/e2e/__screenshots__/<spec>/<arg>.png` tree.
  // Pinned WITHOUT `{projectName}` / `{platform}` to keep baselines stable
  // across local-vs-CI runs; visual-regression specs already skip non-
  // chromium projects via testInfo.project.name guards.
  snapshotPathTemplate: '{testDir}/__screenshots__/{testFileName}/{arg}{ext}',
  use: {
    baseURL: process.env.E2E_BASE_URL || 'http://localhost:8080/autotable/',
    trace: 'on-first-retry',
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
    { name: 'mobile-chrome', use: { ...devices['Pixel 5'] } },
  ],
});
