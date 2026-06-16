// vasquez-csp-violation-detail-probe.mjs — pinpoint the inline-style source.
import pw from 'playwright';
const { chromium } = pw;

const RAW_BASE = process.env.E2E_BASE_URL || 'http://127.0.0.1:8093';
const ORIGIN = RAW_BASE.replace(/\/autotable\/?$/, '').replace(/\/$/, '');
const PATHQ = process.env.PROBE_PATH || '/autotable/?variant=changsha&seat=-1&dealMode=auto&botCount=4&botDifficulty=Easy&handCount=4';
const URL = `${ORIGIN}${PATHQ}`;

const browser = await chromium.launch({ headless: true });
const ctx = await browser.newContext({ viewport: { width: 1280, height: 800 } });
const page = await ctx.newPage();

// Capture rich CSP violation detail BEFORE any app code runs. NO style injection.
await page.addInitScript(() => {
  window.__csp = [];
  document.addEventListener('securitypolicyviolation', (e) => {
    window.__csp.push({
      violatedDirective: e.violatedDirective,
      effectiveDirective: e.effectiveDirective,
      blockedURI: e.blockedURI,
      sourceFile: e.sourceFile,
      lineNumber: e.lineNumber,
      columnNumber: e.columnNumber,
      sample: e.sample,
    });
  });
});

const consoleCsp = [];
page.on('console', (m) => {
  if (m.type() === 'error' && /content security policy|style-src|refused to|applying inline style/i.test(m.text())) {
    consoleCsp.push(m.text());
  }
});

console.log(`NAV: ${URL}`);
await page.goto(URL, { waitUntil: 'domcontentloaded', timeout: 30000 });
await page.waitForLoadState('networkidle', { timeout: 30000 }).catch(() => {});
await page.waitForTimeout(8000); // let auto-deal run a few seconds

const hits = await page.evaluate(() => window.__csp || []);
console.log(`\nsecuritypolicyviolation events: ${hits.length}`);
for (const h of hits) console.log(JSON.stringify(h));
console.log(`\nconsole CSP errors: ${consoleCsp.length}`);
for (const c of consoleCsp) console.log('  ' + c);

await browser.close();
process.exit(0);
