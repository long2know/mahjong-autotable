// Ferro iter1 — visual verification spec.
//
// Verifies the two new UX modules render:
//   1. Win-screen polish (animated score counters + fan list).
//      Triggered via the synthetic Hu backdoor — emits a
//      gameComplete singleton update so the modal opens without
//      having to play a full match.
//   2. Claim-window overlay.  Triggered via a synthetic claim
//      collection update so we can screenshot the bar without
//      waiting for a natural bot claim to fire.
//
// Run with: node playtest-artifacts/ferro-iter1/spec.mjs
import { chromium } from 'playwright';
import fs from 'fs';
import path from 'path';

const ARTIFACT_DIR = path.resolve('./playtest-artifacts/ferro-iter1');
fs.mkdirSync(ARTIFACT_DIR, { recursive: true });

const baseUrl = process.env.E2E_BASE_URL || 'http://127.0.0.1:8088';
const findings = {
  url: '',
  steps: [],
  pageErrors: [],
  consoleErrors: [],
  consoleWarnings: [],
};

const browser = await chromium.launch();
const ctx = await browser.newContext({ viewport: { width: 1280, height: 800 } });
const page = await ctx.newPage();
page.on('console', msg => {
  const t = msg.type();
  const text = msg.text();
  if (t === 'error') findings.consoleErrors.push(text);
  if (t === 'warning') findings.consoleWarnings.push(text);
});
page.on('pageerror', err => findings.pageErrors.push(err.message + '\n' + (err.stack || '').split('\n').slice(0, 4).join('\n')));

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
  await page.screenshot({ path: path.join(ARTIFACT_DIR, name), fullPage: false });
  console.log(`  📸  ${name}`);
}

async function step(name, fn) {
  console.log(`\n=== ${name} ===`);
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

// 1) Load a spectator session with 4 bots so the backend auto-deals.
await step('1-load', async () => {
  await page.goto(`${baseUrl}/autotable/?variant=changsha&dealMode=auto&botCount=4&botDifficulty=Medium&handCount=4`, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(8000);
  findings.url = page.url();
  await snap('00-loaded.png');
  return { url: findings.url };
});

// 1b) Confirm ferro-bootstrap actually attached (window.game published).
await step('1b-bootstrap-ready', async () => {
  const gotGame = await page.waitForFunction(() => {
    const g = window.game;
    return g !== undefined && g.client !== undefined;
  }, { timeout: 30000 }).then(() => true).catch(() => false);
  return { gotGame };
});

// 2) Trigger synthetic claim window so the overlay renders.
await step('2-synthetic-claim', async () => {
  const ok = await page.evaluate(() => {
    const game = window.game;
    if (!game || !game.client) return { ok: false, why: 'no game.client' };
    const cli = game.client;
    if (cli.seat === null || cli.seat === undefined) {
      try { cli.seat = 0; } catch (e) {}
    }
    const events = cli.events ?? cli['events'];
    if (!events) return { ok: false, why: 'no events' };
    const selfKey = String(cli.seat ?? 0);
    events.emit('update', [['claim', selfKey, {
      available: ['Pung', 'Chow', 'Kong'],
      deadline: Date.now() + 5000,
      source: (cli.seat ?? 0) === 0 ? 3 : 0,
      tile: 12,
    }]], false);
    return { ok: true, seat: cli.seat };
  });
  await page.waitForTimeout(400);
  await snap('01-claim-window-overlay.png');
  await page.waitForTimeout(2000);
  await snap('02-claim-window-mid.png');
  // Close synthetic claim with a tombstone via the same events bus so
  // we don't trigger the existing trunk (game-ui.ts) pass-echo bug.
  await page.evaluate(() => {
    const game = window.game;
    const cli = game.client;
    const events = cli.events ?? cli['events'];
    events.emit('update', [['claim', String(cli.seat ?? 0), null]], false);
  });
  await page.waitForTimeout(300);
  return ok;
});

// 3) Trigger synthetic Hu / gameComplete so the win-screen modal opens.
await step('3-synthetic-hu', async () => {
  const ok = await page.evaluate(() => {
    const game = window.game;
    if (!game || !game.client) return { ok: false, why: 'no game.client' };
    const cli = game.client;
    const events = cli.events ?? cli['events'];
    if (!events) return { ok: false, why: 'no events' };
    events.emit('update', [['gameComplete', 'current', {
      isComplete: true,
      totalScores: { '0': 12, '1': -4, '2': -4, '3': -4 },
      handHistory: [
        { winner: 0, type: 'Hu',   score: [{ seat: 0, delta: +8 }, { seat: 1, delta: -2 }, { seat: 2, delta: -2 }, { seat: 3, delta: -4 }], hand: [], nextBanker: 1 },
        { winner: 0, type: 'Hu',   score: [{ seat: 0, delta: +4 }, { seat: 1, delta: -2 }, { seat: 2, delta: -1 }, { seat: 3, delta: -1 }], hand: [], nextBanker: 2 },
        { winner: 3, type: 'Draw', score: [],                                                                                              hand: [], nextBanker: 3 },
        { winner: 0, type: 'Hu',   score: [{ seat: 0, delta: +0 }, { seat: 1, delta: +0 }, { seat: 2, delta: +1 }, { seat: 3, delta: -1 }], hand: [], nextBanker: 0 },
      ],
      maxHands: 4,
    }]], false);
    return { ok: true };
  });
  await page.waitForTimeout(150);
  await snap('03-win-screen-mid-roll.png');
  await page.waitForTimeout(1400);
  await snap('04-win-screen-final.png');
  const fansSection = await page.locator('#ferro-win-fans').count();
  const rollCounters = await page.locator('.ferro-roll-counter').count();
  return { ok, fansSection, rollCounters };
});

// 4) Mobile viewport screenshot of the win-screen.
await step('4-mobile-win', async () => {
  await page.setViewportSize({ width: 375, height: 667 });
  await page.waitForTimeout(300);
  // Bootstrap modal may have lost its show state on viewport change —
  // re-emit gameComplete + force-show via the jquery handle.  Also
  // explicitly close the settings drawer (it's a fixed 320px overlay
  // that occludes the modal on 375px viewports).
  await page.evaluate(() => {
    const game = window.game;
    const cli = game.client;
    const events = cli.events ?? cli['events'];
    events.emit('update', [['gameComplete', 'current', {
      isComplete: true,
      totalScores: { '0': 12, '1': -4, '2': -4, '3': -4 },
      handHistory: [
        { winner: 0, type: 'Hu',   score: [{ seat: 0, delta: +8 }, { seat: 1, delta: -2 }, { seat: 2, delta: -2 }, { seat: 3, delta: -4 }], hand: [], nextBanker: 1 },
        { winner: 0, type: 'Hu',   score: [{ seat: 0, delta: +4 }, { seat: 1, delta: -2 }, { seat: 2, delta: -1 }, { seat: 3, delta: -1 }], hand: [], nextBanker: 2 },
      ],
      maxHands: 2,
    }]], false);
    // Hide both settings drawer variants.
    for (const id of ['settings-drawer', 'settings-drawer-v2']) {
      const d = document.getElementById(id);
      if (d) {
        d.classList.remove('settings-open');
        d.style.display = 'none';
      }
    }
    if (window['$']) { try { window['$']('#game-complete-modal').modal('show'); } catch (e) {} }
  });
  await page.waitForTimeout(1500);
  await snap('05-win-screen-mobile-375.png');
});

// 5) Mobile viewport screenshot of the claim overlay.
await step('5-mobile-claim', async () => {
  // Dismiss the modal first.
  await page.evaluate(() => {
    const game = window.game;
    const cli = game.client;
    const events = cli.events ?? cli['events'];
    events.emit('update', [['gameComplete', 'current', null]], false);
    if (window['$']) { try { window['$']('#game-complete-modal').modal('hide'); } catch (e) {} }
  });
  await page.waitForTimeout(400);
  // Re-trigger synthetic claim window.
  await page.evaluate(() => {
    const game = window.game;
    const cli = game.client;
    const events = cli.events ?? cli['events'];
    if (cli.seat === null || cli.seat === undefined) {
      try { cli.seat = 0; } catch (e) {}
    }
    events.emit('update', [['claim', String(cli.seat ?? 0), {
      available: ['Pung', 'Chow', 'Kong', 'Hu'],
      deadline: Date.now() + 5000,
      source: 3,
      tile: 4,
    }]], false);
  });
  await page.waitForTimeout(500);
  await snap('06-claim-overlay-mobile-375.png');
});

await browser.close();

fs.writeFileSync(path.join(ARTIFACT_DIR, 'findings.json'), JSON.stringify(findings, null, 2));
console.log('\n=== Findings ===');
console.log(JSON.stringify({
  url: findings.url,
  steps: findings.steps.map(s => ({ name: s.name, ok: s.ok, result: s.result, error: s.error })),
  pageErrors: findings.pageErrors,
  consoleErrors: findings.consoleErrors.slice(0, 5),
}, null, 2));
