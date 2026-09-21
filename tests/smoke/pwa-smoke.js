// Phase K Wave 2 — Apone (DevOps).
//
// Playwright (chromium-only) PWA smoke probe. Invoked by
// `tests/smoke/pwa-smoke.sh` after the production Docker image is
// already running on `PWA_SMOKE_BASE_URL` (default
// http://localhost:18093).
//
// Probe the actual application scope, not the root landing-page redirect.
'use strict';

const path = require('path');
const root = path.resolve(__dirname, '..', '..');

// Resolve chromium from autotable-src/node_modules so we don't grow a
// second Playwright dep tree.
const driverDir = path.join(root, 'src', 'frontend', 'autotable-src', 'node_modules', 'playwright');
let chromium;
try {
  chromium = require(driverDir).chromium;
} catch (e) {
  console.error(`::error::could not load Playwright from ${driverDir}: ${e.message}`);
  process.exit(2);
}

const BASE_URL = process.env.PWA_SMOKE_BASE_URL || 'http://localhost:18093';
const APP_URL = new URL('/autotable/', BASE_URL).href;
const SW_URL = new URL('sw.js', APP_URL).href;

(async () => {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({
    serviceWorkers: 'allow',
    ignoreHTTPSErrors: true,
  });
  const page = await context.newPage();
  page.on('console', message => {
    if (message.type() === 'error') console.error('[pwa] browser: ' + message.text());
  });
  page.on('pageerror', error => console.error('[pwa] page: ' + error.message));

  let exitCode = 0;
  try {
    // (a) Index.
    const resp = await page.goto(APP_URL, { waitUntil: 'load', timeout: 20000 });
    if (!resp || resp.status() !== 200) {
      throw new Error(`GET /autotable/ expected 200, got ${resp && resp.status()}`);
    }
    console.log('[pwa] GET /autotable/ → 200');

    const swResp = await context.request.get(SW_URL, { failOnStatusCode: false });
    if (swResp.status() !== 200) {
      throw new Error(`/autotable/sw.js expected 200, got ${swResp.status()}`);
    }
    const ct = swResp.headers()['content-type'] || '';
    if (!/javascript/i.test(ct)) {
      throw new Error(`/autotable/sw.js content-type expected JavaScript, got "${ct}"`);
    }
    console.log(`[pwa] GET /autotable/sw.js → 200 (content-type=${ct})`);

    await page.waitForFunction(async ({ scope, script }) => {
      if (!('serviceWorker' in navigator)) throw new Error('Service workers are unavailable');
      const registration = await navigator.serviceWorker.getRegistration(scope);
      return registration?.scope === scope && registration.active?.state === 'activated'
        && registration.active.scriptURL === script;
    }, { scope: APP_URL, script: SW_URL }, { timeout: 20000 });
    await page.waitForFunction(script =>
      navigator.serviceWorker.controller?.scriptURL === script, SW_URL, { timeout: 10000 });
    console.log('[pwa] activated worker controls the initial application page');

    // (d) Reload + assert controller. Controller hand-off is async — it
    // requires the SECOND navigation to take effect.
    await page.reload({ waitUntil: 'load', timeout: 20000 });
    await page.waitForFunction(script =>
      navigator.serviceWorker.controller?.scriptURL === script, SW_URL, { timeout: 10000 });
    console.log('[pwa] navigator.serviceWorker.controller !== null after reload');
    console.log('[pwa] ✅ service-worker registration + controller hand-off OK');
  } catch (e) {
    console.error('[pwa] ❌ ' + e.message);
    try {
      const state = await page.evaluate(async () => ({
        url: location.href,
        secureContext: isSecureContext,
        controller: navigator.serviceWorker.controller?.scriptURL ?? null,
        registrations: (await navigator.serviceWorker.getRegistrations()).map(registration => ({
          scope: registration.scope,
          active: registration.active?.scriptURL ?? null,
          state: registration.active?.state ?? null,
        })),
      }));
      console.error('[pwa] state: ' + JSON.stringify(state));
    } catch (diagnosticError) {
      console.error('[pwa] state unavailable: ' + diagnosticError.message);
    }
    exitCode = 1;
  } finally {
    await context.close().catch(() => {});
    await browser.close().catch(() => {});
    process.exit(exitCode);
  }
})();
