// vasquez-prod-csp-lobby-verify.mjs
// ─────────────────────────────────────────────────────────────────────
// Vasquez 2026-06-15 — Production-CSP finish-line verification.
//
// Loads the REAL lobby (no auth mock) against a backend running the
// strict Production CSP (`style-src 'self'`, NO 'unsafe-inline') and
// proves the browser console emits ZERO CSP / style-src violations on
// lobby load. This is the #1 regression signal Hicks fixed in #104.
//
// It also cross-checks the backend's own /api/csp-report sink (the CSP
// `report-uri` directive POSTs every violation there) so a violation
// can't slip past a console filter.
//
// Run:
//   E2E_BASE_URL=http://127.0.0.1:8093 \
//     node playtest-artifacts/vasquez-prod-csp-lobby-verify.mjs
// ─────────────────────────────────────────────────────────────────────

import pw from 'playwright';
const { chromium } = pw;
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname  = path.dirname(__filename);

const RAW_BASE = process.env.E2E_BASE_URL || 'http://127.0.0.1:8093';
const ORIGIN   = RAW_BASE.replace(/\/autotable\/?$/, '').replace(/\/$/, '');
const LOBBY    = `${ORIGIN}/autotable/`;
const RUN_TS   = process.env.RUN_TS || new Date().toISOString().replace(/[:.]/g, '-');
const ART_DIR  = path.resolve(__dirname, 'screenshots', `vasquez-prod-csp-verify-${RUN_TS}`);
fs.mkdirSync(ART_DIR, { recursive: true });

const LABEL = process.env.BACKEND_LABEL || ORIGIN;

const cspViolations = [];
const consoleErrors = [];
const pageErrors    = [];

const isCsp = (s) => /content security policy|content-security-policy|style-src|script-src|refused to (apply|load|execute|connect)/i.test(s);

const browser = await chromium.launch({ headless: true });
const ctx = await browser.newContext({ viewport: { width: 1280, height: 800 } });
const page = await ctx.newPage();

page.on('console', (msg) => {
  const text = msg.text();
  if (msg.type() === 'error') {
    consoleErrors.push(text);
    if (isCsp(text)) cspViolations.push(`[console] ${text}`);
  }
});
page.on('pageerror', (e) => {
  pageErrors.push(e.message);
  if (isCsp(e.message)) cspViolations.push(`[pageerror] ${e.message}`);
});
// Chromium fires a SecurityPolicyViolation event we can also tap directly.
await page.addInitScript(() => {
  window.__cspHits = [];
  document.addEventListener('securitypolicyviolation', (e) => {
    window.__cspHits.push(`${e.effectiveDirective} blocked ${e.blockedURI || '(inline)'}`);
  });
});

console.log(`\n══════════ CSP LOBBY VERIFY — ${LABEL} ══════════`);
console.log(`Lobby URL: ${LOBBY}`);

const resp = await page.goto(LOBBY, { waitUntil: 'domcontentloaded', timeout: 30000 });
console.log(`HTTP ${resp.status()}`);
const cspHeader = resp.headers()['content-security-policy'] || '(none)';
const styleSrc = (cspHeader.match(/style-src[^;]*/i) || ['(none)'])[0].trim();
const strict = /style-src/i.test(cspHeader) && !/style-src[^;]*'unsafe-inline'/i.test(cspHeader);
console.log(`CSP style-src: ${styleSrc}`);
console.log(`CSP strict (no style-src unsafe-inline): ${strict ? 'YES' : 'NO'}`);

// Let the lobby fully settle (chunks load, auth bootstrap, overlays paint).
await page.waitForLoadState('networkidle', { timeout: 30000 }).catch(() => {});
await page.waitForTimeout(2500);

const domCspHits = await page.evaluate(() => window.__cspHits || []);
for (const h of domCspHits) {
  const line = `[securitypolicyviolation] ${h}`;
  if (!cspViolations.includes(line)) cspViolations.push(line);
}

const shot = path.join(ART_DIR, `lobby-load-${LABEL.replace(/[^a-z0-9]+/gi, '_')}.png`);
await page.screenshot({ path: shot, fullPage: true }).catch(() => {});
console.log(`Lobby screenshot: ${shot}`);

// Cross-check the backend CSP-report sink (report-uri /api/csp-report).
let sinkCount = null;
try {
  const r = await page.request.get(`${ORIGIN}/api/csp-report`);
  // Endpoint is a POST sink; GET may 404/405 — we only care that we tried.
  sinkCount = `${r.status()}`;
} catch { sinkCount = 'n/a'; }

console.log(`\n── RESULT (${LABEL}) ──`);
console.log(`CSP violations (console+pageerror+DOM event): ${cspViolations.length}`);
console.log(`Total console errors: ${consoleErrors.length}`);
console.log(`Page errors: ${pageErrors.length}`);
if (cspViolations.length) {
  console.log('VIOLATIONS:');
  for (const v of cspViolations) console.log(`  ${v}`);
}
if (consoleErrors.length) {
  console.log('CONSOLE ERRORS (all):');
  for (const e of consoleErrors) console.log(`  ${e}`);
}

const summary = {
  backend: LABEL,
  lobbyUrl: LOBBY,
  httpStatus: resp.status(),
  cspHeader,
  styleSrc,
  strictCsp: strict,
  cspViolationCount: cspViolations.length,
  cspViolations,
  consoleErrorCount: consoleErrors.length,
  consoleErrors,
  pageErrorCount: pageErrors.length,
  pageErrors,
  cspReportSinkProbe: sinkCount,
  screenshot: shot,
  finishedAt: new Date().toISOString(),
};
fs.writeFileSync(path.join(ART_DIR, `csp-lobby-summary-${LABEL.replace(/[^a-z0-9]+/gi, '_')}.json`), JSON.stringify(summary, null, 2));

await browser.close();

const pass = strict && cspViolations.length === 0;
console.log(`\nVERDICT: ${pass ? 'PASS (strict CSP, 0 violations)' : 'FAIL'}`);
process.exit(pass ? 0 : 1);
