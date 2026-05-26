// playtest-mobile-375.spec.mjs — Hicks (`feat/mobile-responsive-and-lobby-overlay`).
//
// Mobile-responsive regression at iPhone-SE viewport (375×667).  Modeled
// on `playtest-v3-fresh.spec.mjs` but drives BOTH the spectator path
// (`?dealMode=auto&botCount=4`) and the human-led path
// (`?dealMode=manual&botCount=3`) so we catch Changsha-specific reflow
// breakage in either flow.
//
// Gates:
//   • `pageErrorsCount === 0` for each run
//   • `docW <= innerW + 1` (no horizontal overflow at 375 px)
//   • Lobby Quick-Match button visible AND `min-height >= 44 px`
//   • Ferro variant-picker dropdown visible AND `min-height >= 44 px`
//   • Mid-game canvas mounted (`canvasCount > 0`)
//
// Screenshots land in `playtest-artifacts/mobile-375/` so reviewers can
// eyeball reflow without running the spec.
//
// Run:
//   node playtest-artifacts/playtest-mobile-375.spec.mjs
import { chromium } from 'playwright';
import fs from 'fs';
import path from 'path';

const ARTIFACT_DIR = path.resolve('./playtest-artifacts/mobile-375');
fs.mkdirSync(ARTIFACT_DIR, { recursive: true });

const baseUrl = process.env.E2E_BASE_URL || 'http://127.0.0.1:8088';
const VIEWPORT = { width: 375, height: 667 };

function makeFindings(label) {
  return {
    label,
    url: '',
    viewport: VIEWPORT,
    steps: [],
    pageErrors: [],
    consoleErrors: [],
    consoleWarnings: [],
    networkFailures: [],
    collections: {},
    overflow: null,
    measurements: {},
  };
}

const browser = await chromium.launch();

async function runScenario(label, urlSuffix) {
  console.log(`\n############## SCENARIO: ${label} ##############`);
  const findings = makeFindings(label);
  const ctx = await browser.newContext({
    viewport: VIEWPORT,
    deviceScaleFactor: 2,
    isMobile: true,
    hasTouch: true,
  });
  const page = await ctx.newPage();

  page.on('console', msg => {
    const t = msg.type();
    const text = msg.text();
    if (t === 'error') findings.consoleErrors.push(text);
    if (t === 'warning') findings.consoleWarnings.push(text);
    const m = text.match(/full update (\w+) (\d+)/);
    if (m) findings.collections[m[1]] = parseInt(m[2], 10);
  });
  page.on('pageerror', err => findings.pageErrors.push(err.message));
  page.on('response', resp => {
    if (resp.status() >= 400) {
      findings.networkFailures.push(`${resp.status()} ${resp.request().method()} ${resp.url()}`);
    }
  });

  await page.addInitScript(() => {
    const inject = () => {
      if (document.getElementById('v3-overlay-defang')) return;
      const style = document.createElement('style');
      style.id = 'v3-overlay-defang';
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

  async function snap(name) {
    await page.screenshot({ path: path.join(ARTIFACT_DIR, `${label}-${name}`), fullPage: false });
  }

  async function step(name, fn) {
    console.log(`\n=== [${label}] ${name} ===`);
    try {
      const result = await fn();
      findings.steps.push({ name, ok: true, result });
      console.log(`OK ${name}`, result || '');
    } catch (err) {
      const msg = err && err.message || String(err);
      findings.steps.push({ name, ok: false, error: msg });
      console.log(`FAIL ${name}: ${msg}`);
    }
  }

  await step('1-load', async () => {
    await page.goto(`${baseUrl}/autotable/?${urlSuffix}`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(2500);
    findings.url = page.url();
    return { url: findings.url };
  });

  await step('2-lobby-view', async () => {
    await page.evaluate(() => {
      const p = document.getElementById('lobby-panel');
      if (p) p.classList.add('lobby-open');
      document.body.classList.add('lobby-active');
    });
    await page.waitForTimeout(400);
    await snap('01-lobby.png');
    const m = await page.evaluate(() => {
      const docW = document.documentElement.scrollWidth;
      const innerW = window.innerWidth;
      const lobby = document.getElementById('lobby-panel');
      const qm = document.getElementById('lobby-quick-match');
      const picker = document.querySelector('.ferro-variant-picker-select');
      function rect(el) {
        if (!el) return null;
        const r = el.getBoundingClientRect();
        return { x: r.x, y: r.y, w: r.width, h: r.height };
      }
      return { docW, innerW, lobby: rect(lobby), qm: rect(qm), picker: rect(picker) };
    });
    findings.measurements.lobby = m;
    if (m.docW > m.innerW + 1) throw new Error(`Horizontal overflow at lobby: docW=${m.docW} innerW=${m.innerW}`);
    if (!m.qm || m.qm.h < 44) throw new Error(`Quick-Match button not 44px tall: h=${m.qm && m.qm.h}`);
    if (!m.picker || m.picker.h < 44) throw new Error(`Variant picker not 44px tall: h=${m.picker && m.picker.h}`);
    return m;
  });

  await step('3-quick-match', async () => {
    const uniqueGameId = `mobile375-${label}-${Date.now()}-${Math.floor(Math.random() * 1000)}`;
    const gameIdInput = page.locator('#game-id, [data-testid="game-id"]').first();
    if (await gameIdInput.isVisible().catch(() => false)) {
      await gameIdInput.fill(uniqueGameId);
      await page.waitForTimeout(200);
    }
    const qm = page.locator('#lobby-quick-match');
    if (!(await qm.first().isVisible().catch(() => false))) {
      // Scroll inside the lobby panel until the QM button enters view.
      await qm.first().scrollIntoViewIfNeeded({ timeout: 3000 }).catch(() => {});
    }
    await qm.first().click({ timeout: 5000 });
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(3500);
    return { url: page.url(), uniqueGameId };
  });

  await step('3b-close-lobby', async () => {
    const closeBtn = page.locator('#lobby-close');
    if (await closeBtn.isVisible().catch(() => false)) {
      await closeBtn.click({ force: true, timeout: 3000 });
      await page.waitForTimeout(700);
    }
    await snap('02-after-quick-match.png');
  });

  await step('3c-connect', async () => {
    const connect = page.locator('#connect');
    const visible = await connect.first().isVisible().catch(() => false);
    if (visible) {
      await connect.first().click({ timeout: 5000 });
      await page.waitForTimeout(3500);
    }
    return { wasVisible: visible };
  });

  await step('4-take-seat-if-available', async () => {
    const seats = page.locator('.take-seat');
    const total = await seats.count();
    let firstIdx = -1;
    for (let i = 0; i < total; i++) {
      if (await seats.nth(i).isVisible().catch(() => false)) { firstIdx = i; break; }
    }
    if (firstIdx === -1) {
      return { spectator: true, total };
    }
    await seats.nth(firstIdx).click({ timeout: 5000 });
    await page.waitForTimeout(2500);
    return { total, clickedIdx: firstIdx };
  });

  await step('5-deal-if-button-shown', async () => {
    const deal = page.locator('#deal');
    const visible = await deal.first().isVisible().catch(() => false);
    const enabled = await deal.first().isEnabled().catch(() => false);
    if (visible && enabled) {
      await deal.first().click({ timeout: 5000 });
      await page.waitForTimeout(4500);
    }
    return { visible, enabled };
  });

  await step('6-observe-midgame', async () => {
    await page.waitForTimeout(2500);
    await snap('03-midgame.png');
    const m = await page.evaluate(() => {
      const docW = document.documentElement.scrollWidth;
      const innerW = window.innerWidth;
      const canvasCount = document.querySelectorAll('canvas').length;
      const sidebar = document.getElementById('sidebar');
      const handThings = document.querySelectorAll('[data-testid*="hand"]').length;
      function rect(el) { if (!el) return null; const r = el.getBoundingClientRect(); return { x: r.x, y: r.y, w: r.width, h: r.height }; }
      return { docW, innerW, canvasCount, sidebar: rect(sidebar), handThings };
    });
    findings.measurements.midgame = m;
    findings.overflow = { docW: m.docW, innerW: m.innerW };
    if (m.docW > m.innerW + 1) throw new Error(`Horizontal overflow mid-game: docW=${m.docW} innerW=${m.innerW}`);
    if (m.canvasCount < 1) throw new Error(`No canvas mounted mid-game (canvasCount=${m.canvasCount})`);
    return m;
  });

  await step('7-claim-window-snapshot', async () => {
    // Try to force-show the claim overlay by injecting a fake collection
    // state. This is a visual reflow test — we don't dispatch a real
    // claim, just verify the overlay's CSS holds up at 375px.
    const created = await page.evaluate(() => {
      const overlay = document.querySelector('.ferro-claim-overlay');
      if (overlay) {
        overlay.classList.add('ferro-claim-overlay-visible');
        return true;
      }
      return false;
    });
    if (created) {
      await page.waitForTimeout(300);
      await snap('04-claim-window.png');
    }
    const m = await page.evaluate(() => ({
      docW: document.documentElement.scrollWidth,
      innerW: window.innerWidth,
    }));
    if (m.docW > m.innerW + 1) throw new Error(`Horizontal overflow with claim overlay: docW=${m.docW} innerW=${m.innerW}`);
    return { created, ...m };
  });

  await step('8-final', async () => {
    await snap('05-final.png');
  });

  await ctx.close();
  fs.writeFileSync(path.join(ARTIFACT_DIR, `${label}-findings.json`), JSON.stringify(findings, null, 2));

  return {
    label,
    pageErrorsCount: findings.pageErrors.length,
    consoleErrorsCount: findings.consoleErrors.length,
    networkFailuresCount: findings.networkFailures.length,
    failedSteps: findings.steps.filter(s => !s.ok).map(s => s.name + ': ' + s.error),
    overflow: findings.overflow,
    measurements: findings.measurements,
  };
}

const auto = await runScenario('auto', 'variant=changsha&dealMode=auto&botCount=4&botDifficulty=Medium&handCount=4');
const manual = await runScenario('manual', 'variant=changsha&dealMode=manual&botCount=3&botDifficulty=Medium&handCount=4&seat=0');

await browser.close();

console.log('\n=== SUMMARY ===');
console.log(JSON.stringify({ auto, manual }, null, 2));

const fail = (r) => r.pageErrorsCount > 0 || r.failedSteps.length > 0;
if (fail(auto)) { console.log('\nAUTO SCENARIO FAILED'); process.exit(1); }
if (fail(manual)) { console.log('\nMANUAL SCENARIO FAILED'); process.exit(1); }
console.log('\nALL SCENARIOS PASS at 375x667');
