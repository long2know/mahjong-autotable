// One-shot playtest — CORRECTLY sequenced: Connect → Take Seat → Deal → observe.
// Run with: node playtest-artifacts/playtest-v3-fresh.spec.mjs
import { chromium } from 'playwright';
import fs from 'fs';
import path from 'path';

const ARTIFACT_DIR = path.resolve('./playtest-artifacts/v3');
fs.mkdirSync(ARTIFACT_DIR, { recursive: true });

const baseUrl = process.env.E2E_BASE_URL || 'http://127.0.0.1:8088';
const findings = {
  url: '',
  steps: [],
  pageErrors: [],
  consoleErrors: [],
  consoleWarnings: [],
  networkFailures: [],
  collections: {},
  visibleButtonsAfterDeal: [],
};

const browser = await chromium.launch();
const ctx = await browser.newContext({ viewport: { width: 1280, height: 800 } });
const page = await ctx.newPage();

page.on('console', msg => {
  const t = msg.type();
  const text = msg.text();
  if (t === 'error') findings.consoleErrors.push(text);
  if (t === 'warning') findings.consoleWarnings.push(text);
  // Tap collection-update logs to know state
  const m = text.match(/full update (\w+) (\d+)/);
  if (m) findings.collections[m[1]] = parseInt(m[2], 10);
});
page.on('pageerror', err => findings.pageErrors.push(err.message));
page.on('response', resp => {
  if (resp.status() >= 400) {
    findings.networkFailures.push(`${resp.status()} ${resp.request().method()} ${resp.url()}`);
  }
});

// Defang overlays as in the canonical spec
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
  await page.screenshot({ path: path.join(ARTIFACT_DIR, name), fullPage: true });
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

// 1) Load — go DIRECTLY to spectator mode (4 bots, auto-deal) to verify bot AI
await step('1-load', async () => {
  await page.goto(`${baseUrl}/autotable/?variant=changsha&dealMode=auto&botCount=4&botDifficulty=Medium&handCount=4`, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(2500);
  findings.url = page.url();
  await snap('01-loaded.png');
  return { url: findings.url };
});

// 2) Dismiss tour
await step('2-dismiss-tour', async () => {
  const tour = page.locator('#tour-skip');
  if (await tour.isVisible().catch(() => false)) {
    await tour.click({ force: true, timeout: 3000 });
    await page.waitForTimeout(500);
  }
  const onb = page.locator('#onboarding-skip');
  if (await onb.isVisible().catch(() => false)) {
    await onb.click({ force: true, timeout: 3000 });
    await page.waitForTimeout(500);
  }
  await snap('02-no-tour.png');
});

// 3) Click Quick Match — it's INSIDE the lobby panel, triggers location.replace
//    with auto-connect URL (?botCount=3&...) that boots straight to a seated game.
//    Override the gameId to a unique value so each playtest starts fresh.
await step('3-quick-match', async () => {
  const uniqueGameId = `playtest-${Date.now()}-${Math.floor(Math.random() * 1000)}`;
  // Set the game ID in the input field BEFORE clicking Quick Match
  const gameIdInput = page.locator('#game-id, [data-testid="game-id"]').first();
  if (await gameIdInput.isVisible().catch(() => false)) {
    await gameIdInput.fill(uniqueGameId);
    await page.waitForTimeout(300);
  }
  const qm = page.locator('#lobby-quick-match');
  const count = await qm.count();
  const visible = count > 0 && await qm.first().isVisible().catch(() => false);
  if (!visible) throw new Error(`#lobby-quick-match not visible (count=${count})`);
  await qm.first().click({ timeout: 5000 });
  await page.waitForLoadState('domcontentloaded');
  await page.waitForTimeout(3500);
  const tour = page.locator('#tour-skip');
  if (await tour.isVisible().catch(() => false)) {
    await tour.click({ force: true });
    await page.waitForTimeout(500);
  }
  await snap('03-after-quick-match.png');
  return { url: page.url(), uniqueGameId };
});

// 3b) Close lobby panel if it's still open (it stays open after Quick Match in some flows)
await step('3b-close-lobby', async () => {
  const closeBtn = page.locator('#lobby-close');
  if (await closeBtn.isVisible().catch(() => false)) {
    await closeBtn.click({ force: true, timeout: 3000 });
    await page.waitForTimeout(800);
  }
  const lobbyOpen = await page.locator('#lobby-panel.lobby-open').count();
  await snap('03b-after-close.png');
  return { lobbyStillOpen: lobbyOpen > 0 };
});

// 3c) Click Connect now that lobby is closed
await step('3c-connect', async () => {
  const connect = page.locator('#connect');
  const visible = await connect.first().isVisible().catch(() => false);
  if (!visible) {
    const disconnect = await page.locator('#disconnect').first().isVisible().catch(() => false);
    return { alreadyConnected: disconnect };
  }
  await connect.first().click({ timeout: 5000 });
  await page.waitForTimeout(3500);
  const connected = await page.locator('#disconnect.server-connected').count() > 0;
  await snap('03c-after-connect.png');
  return { connected };
});

// 4) Take Seat — skipped in spectator mode (botCount=4 promotes the
//    connection to spectator on the backend, so no seats are available
//    to take). When 0..3 visible take-seat buttons exist we click the
//    first one; otherwise we just record that the table is full / we're
//    a spectator.
await step('4-take-seat', async () => {
  const seats = page.locator('.take-seat');
  const total = await seats.count();
  let visibleCount = 0;
  let firstIdx = -1;
  for (let i = 0; i < total; i++) {
    if (await seats.nth(i).isVisible().catch(() => false)) {
      visibleCount++;
      if (firstIdx === -1) firstIdx = i;
    }
  }
  if (firstIdx === -1) {
    // No take-seat — spectator path; the backend auto-deals when 4 bots
    // fill the table.  Don't fail the step; just record the situation.
    await snap('04-spectator-no-takeseat.png');
    return { total, visibleCount, spectator: true };
  }
  await seats.nth(firstIdx).click({ timeout: 5000 });
  await page.waitForTimeout(2500);
  await snap('04-after-take-seat.png');
  return { total, visibleCount, clickedIdx: firstIdx };
});

// 5) Deal — skipped in spectator mode (backend already auto-dealt)
await step('5-deal', async () => {
  const deal = page.locator('#deal');
  const visible = await deal.first().isVisible().catch(() => false);
  const enabled = await deal.first().isEnabled().catch(() => false);
  if (!visible || !enabled) {
    // Spectator path: the backend auto-dealt as soon as the WS connected
    // with botCount=4. Nothing to click; the table is already running.
    return { visible, enabled, spectator: true };
  }
  await deal.first().click({ timeout: 5000 });
  await page.waitForTimeout(5000);
  await snap('05-after-deal.png');
  return { visible, enabled };
});

// 6) Observe game state
await step('6-observe', async () => {
  // Wait a bit more for any WS-driven state to settle
  await page.waitForTimeout(2000);
  const tilesOnTable = await page.locator('[data-testid*="tile"]').count();
  const handTestids = await page.locator('[data-testid*="hand"]').count();
  const seatTestids = await page.locator('[data-testid*="seat"]').count();
  const canvasCount = await page.locator('canvas').count();
  await snap('06-observed.png');
  // Visible buttons after deal
  const all = await page.getByRole('button').all();
  for (const b of all) {
    try {
      if (!(await b.isVisible())) continue;
      const t = (await b.textContent())?.trim();
      if (!t) continue;
      const id = (await b.getAttribute('id')) || '';
      findings.visibleButtonsAfterDeal.push(`${t} (#${id})`);
    } catch {}
  }
  return { tilesOnTable, handTestids, seatTestids, canvasCount };
});

// 7) Capture move log + per-bot activity (extended observation for bot AI)
await step('7-move-log-progression', async () => {
  // Capture move log at intervals to see bot activity
  const captures = [];
  for (let i = 0; i < 6; i++) {
    const entries = await page.locator('#move-log .move-log-entry, .move-log-entry').allTextContents().catch(() => []);
    const recentMoves = entries.slice(-10);
    captures.push({ atSec: i * 5, count: entries.length, recent: recentMoves });
    if (i < 5) await page.waitForTimeout(5000);
  }
  await snap('07-after-bot-activity.png');
  return { progression: captures };
});

// 8) Final canvas screenshot
await step('8-final', async () => {
  await snap('08-final-state.png');
});

await browser.close();

// Write findings
fs.writeFileSync(path.join(ARTIFACT_DIR, 'findings.json'), JSON.stringify(findings, null, 2));
console.log('\n=== FINAL findings ===');
console.log(JSON.stringify({
  url: findings.url,
  collections: findings.collections,
  visibleButtonsAfterDeal: findings.visibleButtonsAfterDeal,
  steps: findings.steps,
  pageErrorsCount: findings.pageErrors.length,
  consoleErrorsCount: findings.consoleErrors.length,
  networkFailuresCount: findings.networkFailures.length,
}, null, 2));
if (findings.pageErrors.length) {
  console.log('\nPAGE ERRORS:');
  for (const e of findings.pageErrors) console.log(' -', e);
}
if (findings.networkFailures.length) {
  console.log('\nNETWORK FAILURES:');
  for (const e of findings.networkFailures) console.log(' -', e);
}
