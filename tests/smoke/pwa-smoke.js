// Phase K Wave 2 — Apone (DevOps).
//
// Playwright (chromium-only) PWA smoke probe. Invoked by
// `tests/smoke/pwa-smoke.sh` after the production Docker image is
// already running on `PWA_SMOKE_BASE_URL` (default
// http://localhost:18093).
//
// Forward-compat: `/sw.js` may not yet be shipped through Hicks's
// Parcel pipeline. The probe soft-passes on 404 and hard-asserts
// when the resource is present. Same shape as the auth-flow / csp
// soft-pass patterns from Wave 1.
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

(async () => {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({
    serviceWorkers: 'allow',
    ignoreHTTPSErrors: true,
  });
  const page = await context.newPage();

  let exitCode = 0;
  try {
    // (a) Index.
    const resp = await page.goto(BASE_URL + '/', { waitUntil: 'load', timeout: 20000 });
    if (!resp || resp.status() !== 200) {
      throw new Error(`GET / expected 200, got ${resp && resp.status()}`);
    }
    console.log('[pwa] GET / → 200');

    // (b) /sw.js — soft-pass on 404 (Hicks's SW may not yet have shipped).
    const swResp = await context.request.get(BASE_URL + '/sw.js', { failOnStatusCode: false });
    if (swResp.status() === 404) {
      console.log('[pwa] GET /sw.js → 404 (soft-pass; SW artefact not yet shipped)');
    } else if (swResp.status() === 200) {
      const ct = swResp.headers()['content-type'] || '';
      if (!/javascript/i.test(ct)) {
        throw new Error(`/sw.js content-type expected to contain "javascript", got "${ct}"`);
      }
      console.log(`[pwa] GET /sw.js → 200 (content-type=${ct})`);
    } else {
      throw new Error(`/sw.js expected 200 or 404, got ${swResp.status()}`);
    }

    // (c) Wait for the page-side SW to register.
    const registered = await page.evaluate(async () => {
      if (!('serviceWorker' in navigator)) return false;
      try {
        const regs = await navigator.serviceWorker.getRegistrations();
        return regs && regs.length > 0;
      } catch (e) {
        return false;
      }
    });

    if (!registered) {
      if (swResp.status() === 404) {
        console.log('[pwa] navigator.serviceWorker has no registration yet (soft-pass; SW not shipped)');
        console.log('[pwa] ✅ soft-pass on forward-compat path');
        return;
      }
      throw new Error('navigator.serviceWorker.getRegistrations() returned empty');
    }

    // (d) Reload + assert controller. Controller hand-off is async — it
    // requires the SECOND navigation to take effect.
    await page.reload({ waitUntil: 'load', timeout: 20000 });
    const hasController = await page.evaluate(() => {
      return !!(navigator.serviceWorker && navigator.serviceWorker.controller);
    });
    if (!hasController) {
      throw new Error('navigator.serviceWorker.controller was null after reload — SW failed to take control');
    }
    console.log('[pwa] navigator.serviceWorker.controller !== null after reload');
    console.log('[pwa] ✅ service-worker registration + controller hand-off OK');
  } catch (e) {
    console.error('[pwa] ❌ ' + e.message);
    exitCode = 1;
  } finally {
    await context.close().catch(() => {});
    await browser.close().catch(() => {});
    process.exit(exitCode);
  }
})();
