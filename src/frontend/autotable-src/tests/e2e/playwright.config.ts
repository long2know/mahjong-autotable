// Phase J Wave 5 — Playwright E2E scaffold (Apone).
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

import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: '.',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: process.env.CI ? 'github' : 'list',
  use: {
    baseURL: process.env.E2E_BASE_URL || 'http://localhost:8080/autotable/',
    trace: 'on-first-retry',
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
    { name: 'mobile-chrome', use: { ...devices['Pixel 5'] } },
  ],
});
