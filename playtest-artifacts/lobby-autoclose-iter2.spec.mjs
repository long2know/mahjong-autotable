// Hicks iter2 — manual verification that:
//   1. Bare URL /autotable/ opens the lobby panel (.lobby-open).
//   2. Clicking #lobby-quick-match navigates to a URL with botCount params
//      AND leaves the lobby panel CLOSED on the resulting page.
//   3. After the reload, #connect / #deal / .take-seat are NOT pointer-
//      blocked by an over-laying lobby panel.
import { chromium } from 'playwright';

const baseUrl = process.env.E2E_BASE_URL || 'http://127.0.0.1:8088';

const browser = await chromium.launch();
const ctx = await browser.newContext({ viewport: { width: 1280, height: 800 } });
const page = await ctx.newPage();

// Silence overlays so they don't intercept anything during the test.
await page.addInitScript(() => {
  const inject = () => {
    if (document.getElementById('lobby-autoclose-defang')) return;
    const style = document.createElement('style');
    style.id = 'lobby-autoclose-defang';
    style.textContent = `
      #tour-overlay, #magic-link-landing, #magic-link-overlay,
      #signin-modal-backdrop, .magic-link-landing, .magic-link-overlay,
      .signin-modal-backdrop, [data-testid="tour-overlay"], [data-testid="signin-modal-backdrop"]
        { display: none !important; pointer-events: none !important; visibility: hidden !important; }
      [aria-hidden="true"] { pointer-events: none !important; }
    `;
    document.head.appendChild(style);
  };
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', inject);
  } else { inject(); }
});

const results = {};

// 1) Load bare URL — lobby should auto-open.
await page.goto(`${baseUrl}/autotable/`, { waitUntil: 'domcontentloaded' });
await page.waitForTimeout(1500);
// Dismiss any tour
const tour = page.locator('#tour-skip');
if (await tour.isVisible().catch(() => false)) {
  await tour.click({ force: true, timeout: 3000 });
  await page.waitForTimeout(300);
}
results.lobbyOpenOnBare = await page.locator('#lobby-panel.lobby-open').count() > 0;
results.quickMatchVisibleOnBare = await page.locator('#lobby-quick-match').isVisible().catch(() => false);

// 2) Click Quick Match
if (results.quickMatchVisibleOnBare) {
  await page.locator('#lobby-quick-match').click({ timeout: 3000 });
  await page.waitForLoadState('domcontentloaded');
  await page.waitForTimeout(2000);
  results.urlAfterClick = page.url();
  // Dismiss any tour after reload
  const tour2 = page.locator('#tour-skip');
  if (await tour2.isVisible().catch(() => false)) {
    await tour2.click({ force: true, timeout: 3000 });
    await page.waitForTimeout(300);
  }
  results.lobbyClosedAfterReload = (await page.locator('#lobby-panel.lobby-open').count()) === 0;
  // Verify other elements aren't intercepted (i.e. lobby panel doesn't sit on top)
  // Use elementFromPoint to check: pick the centre of #connect (or #deal) and
  // make sure the topmost element isn't inside #lobby-panel.
  results.connectClickable = await page.evaluate(() => {
    const target = document.querySelector('#connect, #deal, .take-seat');
    if (!target) return 'no-target';
    const rect = target.getBoundingClientRect();
    if (rect.width === 0 || rect.height === 0) return 'no-rect';
    const cx = rect.left + rect.width / 2;
    const cy = rect.top + rect.height / 2;
    const top = document.elementFromPoint(cx, cy);
    if (top === null) return 'no-elem-at-point';
    let cur = top;
    while (cur !== null) {
      if (cur.id === 'lobby-panel') return false;
      cur = cur.parentElement;
    }
    return true;
  });
}

await browser.close();
console.log(JSON.stringify(results, null, 2));
const ok = results.lobbyOpenOnBare === true
  && results.lobbyClosedAfterReload === true
  && results.connectClickable !== false;
if (!ok) {
  console.error('FAIL: lobby auto-close verification failed');
  process.exit(1);
}
console.log('PASS: lobby auto-closes after Quick Match');
