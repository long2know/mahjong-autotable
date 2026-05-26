// Ferro iter-2 — Variant picker visual + behavior check.
// Captures desktop + mobile screenshots of the new dropdown and verifies:
//   • Picker is present, has data-testid="ferro-variant-picker"
//   • Default is 'changsha' on first load (no LS, no URL param)
//   • Reading the URL `?variant=...` pre-populates the picker
//   • Selecting a new value triggers a reload with the new URL
//   • localStorage['mahjong.preferredVariant'] is written
//
// Run with:
//   E2E_BASE_URL=http://127.0.0.1:8088 \
//     node playtest-artifacts/ferro-iter2/variant-picker.spec.mjs
import { chromium } from 'playwright';
import fs from 'fs';
import path from 'path';

const ARTIFACT_DIR = path.resolve('./playtest-artifacts/ferro-iter2');
fs.mkdirSync(ARTIFACT_DIR, { recursive: true });

const baseUrl = process.env.E2E_BASE_URL || 'http://127.0.0.1:8088';
const findings = {
  baseUrl,
  steps: [],
  pageErrors: [],
  consoleErrors: [],
  consoleWarnings: [],
};

const browser = await chromium.launch();

async function recordErrors(page, label) {
  page.on('pageerror', err => findings.pageErrors.push(`[${label}] ${err.message}`));
  page.on('console', msg => {
    const t = msg.type();
    if (t === 'error') findings.consoleErrors.push(`[${label}] ${msg.text()}`);
    if (t === 'warning') findings.consoleWarnings.push(`[${label}] ${msg.text()}`);
  });
}

async function openLobby(page) {
  const url = page.url();
  // On a bare URL the lobby auto-opens (lobby.ts:shouldShowOnLoad).  On
  // a URL with query params the panel stays closed and we click the
  // toggle to open it.
  const hasQuery = new URL(url).search !== '';
  if (hasQuery) {
    await page.waitForSelector('#lobby-toggle:not([hidden])', { timeout: 10000 });
    const isOpenAlready = await page.locator('#lobby-panel.lobby-open').count();
    if (isOpenAlready === 0) {
      await page.click('#lobby-toggle');
    }
  }
  await page.waitForSelector('#lobby-panel.lobby-open', { timeout: 10000 });
  // The picker self-installs via MutationObserver — wait for the
  // `data-testid` to land.
  await page.waitForSelector('[data-testid="ferro-variant-picker"]', { timeout: 5000 });
}

// ---------------------------------------------------------------------------
// Desktop — 1280x800
// ---------------------------------------------------------------------------
{
  const ctx = await browser.newContext({ viewport: { width: 1280, height: 800 } });
  const page = await ctx.newPage();
  await recordErrors(page, 'desktop');

  // Clear LS to verify the default path.
  await page.goto(new URL('/autotable/', baseUrl).toString(), { waitUntil: 'domcontentloaded' });
  await page.evaluate(() => window.localStorage.removeItem('mahjong.preferredVariant'));
  // Reload after clearing LS so the picker boots fresh.
  await page.reload({ waitUntil: 'domcontentloaded' });

  await openLobby(page);

  // Dismiss onboarding card so the picker is in the screenshot frame.
  await page.evaluate(() => {
    const skip = document.getElementById('onboarding-skip');
    if (skip) skip.click();
    const card = document.getElementById('onboarding-card');
    if (card) card.hidden = true;
  });
  await page.waitForTimeout(200);

  // Confirm default = changsha.
  const defaultValue = await page.locator('[data-testid="ferro-variant-picker"]').inputValue();
  findings.steps.push({ step: 'default-value', value: defaultValue, expected: 'changsha' });

  const screenshotPath = path.join(ARTIFACT_DIR, 'variant-picker-desktop.png');
  await page.screenshot({ path: screenshotPath, fullPage: false });
  findings.steps.push({ step: 'screenshot-desktop', path: screenshotPath });

  // Verify the option list (including the disabled "Hong Kong" item).
  const options = await page.$$eval(
    '[data-testid="ferro-variant-picker"] option',
    els => els.map(e => ({
      value: e.value,
      label: e.textContent,
      disabled: e.disabled,
      group: (e.parentElement.tagName === 'OPTGROUP') ? e.parentElement.label : null,
    })),
  );
  findings.steps.push({ step: 'option-list', options });

  // Behaviour — selecting a new value triggers a reload with ?variant=...
  // We simulate by setting the value programmatically + dispatching 'change'.
  await page.evaluate(() => {
    const sel = document.getElementById('ferro-variant-select');
    sel.value = 'four-player';
    sel.dispatchEvent(new Event('change', { bubbles: true }));
  });
  await page.waitForURL(/variant=four-player/, { timeout: 5000 });
  await page.waitForLoadState('domcontentloaded');
  const afterUrl = page.url();
  const afterLs = await page.evaluate(() => window.localStorage.getItem('mahjong.preferredVariant'));
  findings.steps.push({
    step: 'after-change',
    url: afterUrl,
    localStorage: afterLs,
    expectedLs: 'four-player',
    expectedUrlContains: 'variant=four-player',
  });

  // The picker reloaded with ?variant=four-player → open lobby and verify
  // it now pre-populates from URL.
  await openLobby(page);
  const reloadedValue = await page.locator('[data-testid="ferro-variant-picker"]').inputValue();
  findings.steps.push({
    step: 'pre-populate-from-url',
    value: reloadedValue,
    expected: 'four-player',
  });

  // localStorage fallback — clear the URL, reload, verify LS is used.
  await page.goto(new URL('/autotable/', baseUrl).toString(), { waitUntil: 'domcontentloaded' });
  await openLobby(page);
  const lsValue = await page.locator('[data-testid="ferro-variant-picker"]').inputValue();
  findings.steps.push({
    step: 'pre-populate-from-localstorage',
    value: lsValue,
    expected: 'four-player',
  });

  // Reset to Changsha for the next viewport.
  await page.evaluate(() => {
    window.localStorage.setItem('mahjong.preferredVariant', 'changsha');
  });

  await ctx.close();
}

// ---------------------------------------------------------------------------
// Mobile — 375x667 (iPhone SE)
// ---------------------------------------------------------------------------
{
  const ctx = await browser.newContext({
    viewport: { width: 375, height: 667 },
    deviceScaleFactor: 2,
    isMobile: true,
    hasTouch: true,
  });
  const page = await ctx.newPage();
  await recordErrors(page, 'mobile');

  await page.goto(new URL('/autotable/', baseUrl).toString(), { waitUntil: 'domcontentloaded' });
  await page.evaluate(() => {
    window.localStorage.removeItem('mahjong.preferredVariant');
    // Pre-set tour-completed flag so the mobile onboarding overlay
    // doesn't paint over the lobby picker for the screenshot.  The
    // tour module reads `mahjong.tour.completed.v1` per
    // `src/tour.ts:TOUR_LS_KEY`.
    window.localStorage.setItem('mahjong.tour.completed.v1', 'true');
  });
  await page.reload({ waitUntil: 'domcontentloaded' });

  await openLobby(page);
  // Hide the settings drawers — both variants auto-overlay the lobby on
  // mobile (Ferro iter-1 lesson: `#settings-drawer-v2` covers the
  // panel at 375px viewports).
  await page.evaluate(() => {
    for (const id of ['settings-drawer', 'settings-drawer-v2']) {
      const d = document.getElementById(id);
      if (d) {
        d.classList.remove('settings-open');
        d.style.display = 'none';
      }
    }
    // Dismiss onboarding card + any tour overlay that may have mounted.
    const skip = document.getElementById('onboarding-skip');
    if (skip) skip.click();
    const card = document.getElementById('onboarding-card');
    if (card) card.hidden = true;
    const tourSkip = document.getElementById('tour-skip');
    if (tourSkip) tourSkip.click();
    document.querySelectorAll('.tour-card, .tour-card-overlay, #tour-overlay, .onboarding-tour-overlay').forEach(el => {
      el.remove();
    });
    // Scroll the lobby panel so the variant picker is in view.
    const picker = document.getElementById('ferro-variant-select');
    picker?.scrollIntoView({ block: 'center' });
  });
  await page.waitForTimeout(300);

  // 44px touch target check.
  const box = await page.locator('[data-testid="ferro-variant-picker"]').boundingBox();
  findings.steps.push({
    step: 'touch-target',
    height: box?.height,
    width: box?.width,
    expectedMinHeight: 44,
  });

  const screenshotPath = path.join(ARTIFACT_DIR, 'variant-picker-mobile.png');
  await page.screenshot({ path: screenshotPath, fullPage: false });
  findings.steps.push({ step: 'screenshot-mobile', path: screenshotPath });

  // Default = changsha
  const defaultMobile = await page.locator('[data-testid="ferro-variant-picker"]').inputValue();
  findings.steps.push({ step: 'default-mobile', value: defaultMobile, expected: 'changsha' });

  await ctx.close();
}

await browser.close();

findings.pageErrorsCount = findings.pageErrors.length;
findings.consoleErrorsCount = findings.consoleErrors.length;

fs.writeFileSync(
  path.join(ARTIFACT_DIR, 'findings.json'),
  JSON.stringify(findings, null, 2),
);

console.log(JSON.stringify({
  pageErrors: findings.pageErrorsCount,
  consoleErrors: findings.consoleErrorsCount,
  steps: findings.steps.length,
}, null, 2));

if (findings.pageErrorsCount > 0) {
  console.error('PAGE ERRORS:', findings.pageErrors);
  process.exit(1);
}
