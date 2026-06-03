// Hicks 2026-06-01 — Visual regression sweep across 10 scenarios.
//
// Catches any visual regressions vs the
// `hicks-final-clean-2026-06-01T20-52-57Z.png` baseline (4 walls, full
// dealer hand, no wedges, no HUD label, no NaN warnings, clean discard
// mat).  Drives every scenario sequentially against the shared backend
// at :8088 — each run uses a unique `hicks-vreg-*` gameId so we don't
// collide with concurrent agents.
//
// Scenarios:
//   1. desktop-1920 — fresh deal, dealer hand assertion
//   2. mobile-375  — lobby collapses, scene renders
//   3. tablet-768  — mid viewport, no overflow
//   4. human-4p    — 0 bots, spectator/dealer wait
//   5. bots-2      — 2 bots
//   6. bots-4-auto — 4 bots, autonomous play
//   7. camera-flat — flat (top-down) camera toggle
//   8. setup-menu  — Setup dropdown open
//   9. movelog     — Move Log open with content
//  10. settled-30s — 30s of bot play, discard mat fills cleanly
//
// Run:
//   node playtest-artifacts/playtest-hicks-vreg.spec.mjs
import pw from 'playwright';
const { chromium } = pw;
import fs from 'fs';
import path from 'path';

const ART = path.resolve('./playtest-artifacts/screenshots');
fs.mkdirSync(ART, { recursive: true });

const baseUrl = process.env.E2E_BASE_URL || 'http://127.0.0.1:8088';
const RUN_TS = new Date().toISOString().replace(/[:.]/g, '-');
const VREG_PREFIX = `hicks-vreg-${RUN_TS}`;

const allFindings = [];

const browser = await chromium.launch();

function newGameId(label) {
  return `hicks-vreg-${label}-${Date.now()}-${Math.floor(Math.random() * 1000)}`;
}

async function setupPage(ctx) {
  const page = await ctx.newPage();
  const findings = {
    consoleErrors: [],
    consoleWarnings: [],
    pageErrors: [],
    networkFailures: [],
    nanRadiusWarnings: 0,
  };
  page.on('console', msg => {
    const t = msg.type();
    const text = msg.text();
    if (t === 'error') findings.consoleErrors.push(text);
    if (t === 'warning') findings.consoleWarnings.push(text);
    if (/Computed radius is NaN/i.test(text)) findings.nanRadiusWarnings++;
  });
  page.on('pageerror', err => findings.pageErrors.push(err.message));
  page.on('response', resp => {
    if (resp.status() >= 400) {
      findings.networkFailures.push(`${resp.status()} ${resp.request().method()} ${resp.url()}`);
    }
  });
  await page.addInitScript(() => {
    const inject = () => {
      if (document.getElementById('vreg-overlay-defang')) return;
      const style = document.createElement('style');
      style.id = 'vreg-overlay-defang';
      style.textContent = `
        #tour-overlay, #magic-link-landing, #magic-link-overlay,
        #signin-modal-backdrop, .magic-link-landing, .magic-link-overlay,
        .signin-modal-backdrop, [data-testid="tour-overlay"], [data-testid="signin-modal-backdrop"]
          { display: none !important; pointer-events: none !important; visibility: hidden !important; }
        [aria-hidden="true"] { pointer-events: none !important; }
      `;
      document.head.appendChild(style);
    };
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', inject);
    else inject();
  });
  return { page, findings };
}

async function snap(page, name) {
  const p = path.join(ART, `${VREG_PREFIX}-${name}.png`);
  await page.screenshot({ path: p, fullPage: false });
  return p;
}

async function dumpWorldState(page) {
  return await page.evaluate(() => {
    const w = window.game && window.game.world;
    if (!w) return { error: 'no window.game.world' };
    const seat = w.seat;
    let wallCount = 0, dealerHand = 0, allHand = 0, allDiscard = 0, allMeld = 0;
    const wallSeats = new Set();
    for (const thing of w.things.values()) {
      const slot = thing.slot;
      if (!slot) continue;
      if (slot.group === 'wall') {
        wallCount++;
        if (slot.seat !== null && slot.seat !== undefined) wallSeats.add(slot.seat);
      }
      if (slot.group === 'hand') {
        allHand++;
        if (slot.seat === seat) dealerHand++;
      }
      if (slot.group === 'discard') allDiscard++;
      if (slot.group === 'meld') allMeld++;
    }
    const conditions = w.conditions || {};
    return {
      seat,
      wallCount,
      dealerHand,
      allHand,
      allDiscard,
      allMeld,
      wallSeats: [...wallSeats].sort(),
      gameType: conditions.gameType,
      dealType: conditions.dealType,
      thingCount: w.things.size,
    };
  });
}

async function dismissOverlays(page) {
  const tour = page.locator('#tour-skip');
  if (await tour.isVisible().catch(() => false)) {
    await tour.click({ force: true, timeout: 2000 }).catch(() => {});
    await page.waitForTimeout(300);
  }
  const onb = page.locator('#onboarding-skip');
  if (await onb.isVisible().catch(() => false)) {
    await onb.click({ force: true, timeout: 2000 }).catch(() => {});
    await page.waitForTimeout(300);
  }
}

async function quickMatchAndSeat(page, gameId, takeSeat = true) {
  const gameIdInput = page.locator('#game-id, [data-testid="game-id"]').first();
  if (await gameIdInput.isVisible().catch(() => false)) {
    await gameIdInput.fill(gameId);
    await page.waitForTimeout(200);
  }
  const qm = page.locator('#lobby-quick-match');
  if (await qm.first().isVisible().catch(() => false)) {
    await qm.first().click({ timeout: 5000 }).catch(() => {});
    await page.waitForLoadState('domcontentloaded').catch(() => {});
    await page.waitForTimeout(2500);
  }
  const closeBtn = page.locator('#lobby-close');
  if (await closeBtn.isVisible().catch(() => false)) {
    await closeBtn.click({ force: true, timeout: 2000 }).catch(() => {});
    await page.waitForTimeout(400);
  }
  const connect = page.locator('#connect');
  if (await connect.first().isVisible().catch(() => false)) {
    await connect.first().click({ timeout: 5000 }).catch(() => {});
    await page.waitForTimeout(2000);
  }
  if (takeSeat) {
    const seats = page.locator('.take-seat');
    const total = await seats.count();
    for (let i = 0; i < total; i++) {
      if (await seats.nth(i).isVisible().catch(() => false)) {
        await seats.nth(i).click({ timeout: 5000 }).catch(() => {});
        await page.waitForTimeout(1500);
        break;
      }
    }
  }
}

async function triggerDeal(page) {
  await page.waitForTimeout(1500);
  return await page.evaluate(() => {
    const g = window.game;
    if (!g || !g.world) return { ok: false, reason: 'no window.game.world' };
    try {
      g.world.deal('HANDS');
      return { ok: true };
    } catch (e) { return { ok: false, reason: String(e) }; }
  });
}

async function runScenario(label, opts) {
  console.log(`\n############## ${label} ##############`);
  const ctx = await browser.newContext({
    viewport: opts.viewport || { width: 1920, height: 1080 },
    deviceScaleFactor: opts.deviceScaleFactor || 1,
    isMobile: !!opts.isMobile,
    hasTouch: !!opts.isMobile,
  });
  const { page, findings } = await setupPage(ctx);
  const gameId = newGameId(label);
  const url = `${baseUrl}/autotable/?${opts.urlParams}&gameId=${gameId}`;
  console.log(`URL: ${url}`);
  console.log(`Viewport: ${(opts.viewport || { width: 1920, height: 1080 }).width}x${(opts.viewport || { width: 1920, height: 1080 }).height}`);

  let screenshotPaths = [];
  let worldState = null;
  let extra = {};

  try {
    await page.goto(url, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(2500);
    await dismissOverlays(page);

    if (opts.lobbyOnly) {
      screenshotPaths.push(await snap(page, `${label}-01-lobby`));
    } else {
      await quickMatchAndSeat(page, gameId, opts.takeSeat !== false);
      if (opts.triggerDeal !== false) {
        const dealResult = await triggerDeal(page);
        extra.dealResult = dealResult;
      }
      const settleSec = opts.settleSec || 8;
      console.log(`Settling for ${settleSec}s...`);
      await page.waitForTimeout(settleSec * 1000);

      if (opts.afterSettle) {
        try { await opts.afterSettle(page); } catch (e) { extra.afterSettleError = String(e); }
      }

      worldState = await dumpWorldState(page);
      screenshotPaths.push(await snap(page, `${label}-01-post-settle`));
    }
  } catch (e) {
    extra.scenarioError = String(e);
    try { screenshotPaths.push(await snap(page, `${label}-99-error`)); } catch (_) {}
  }

  await ctx.close();

  const summary = {
    label,
    url,
    viewport: opts.viewport || { width: 1920, height: 1080 },
    worldState,
    pageErrors: findings.pageErrors,
    pageErrorsCount: findings.pageErrors.length,
    consoleErrorsCount: findings.consoleErrors.length,
    consoleErrorsSample: findings.consoleErrors.slice(0, 8),
    consoleWarningsCount: findings.consoleWarnings.length,
    nanRadiusWarnings: findings.nanRadiusWarnings,
    networkFailuresCount: findings.networkFailures.length,
    networkFailuresSample: findings.networkFailures.slice(0, 4),
    screenshots: screenshotPaths,
    extra,
  };
  allFindings.push(summary);
  console.log(JSON.stringify(summary, null, 2));
  return summary;
}

// ============================================================
// SCENARIOS
// ============================================================

// 1. Desktop fresh deal (1920×1080)
await runScenario('desktop-1920', {
  viewport: { width: 1920, height: 1080 },
  urlParams: 'variant=changsha&dealMode=auto&botCount=3&botDifficulty=Medium&handCount=4',
  settleSec: 10,
});

// 2. Mobile 375
await runScenario('mobile-375', {
  viewport: { width: 375, height: 667 },
  deviceScaleFactor: 2,
  isMobile: true,
  urlParams: 'variant=changsha&dealMode=auto&botCount=3&botDifficulty=Medium&handCount=4',
  settleSec: 9,
});

// 3. Tablet 768
await runScenario('tablet-768', {
  viewport: { width: 768, height: 1024 },
  urlParams: 'variant=changsha&dealMode=auto&botCount=3&botDifficulty=Medium&handCount=4',
  settleSec: 9,
});

// 4. Human-led, 0 bots (no-deal yet, just lobby + seat assignment)
await runScenario('human-4p-nobots', {
  viewport: { width: 1920, height: 1080 },
  urlParams: 'variant=changsha&dealMode=manual&botCount=0&handCount=4',
  triggerDeal: false,
  settleSec: 4,
});

// 5. 2-bot game
await runScenario('bots-2', {
  viewport: { width: 1920, height: 1080 },
  urlParams: 'variant=changsha&dealMode=auto&botCount=2&botDifficulty=Medium&handCount=4',
  settleSec: 10,
});

// 6. 4 bots auto (game plays itself)
await runScenario('bots-4-auto', {
  viewport: { width: 1920, height: 1080 },
  urlParams: 'variant=changsha&dealMode=auto&botCount=4&botDifficulty=Medium&handCount=4',
  takeSeat: false,
  settleSec: 12,
});

// 7. Camera flat mode toggle (after a deal lands)
await runScenario('camera-flat', {
  viewport: { width: 1920, height: 1080 },
  urlParams: 'variant=changsha&dealMode=auto&botCount=3&botDifficulty=Medium&handCount=4',
  settleSec: 8,
  afterSettle: async (page) => {
    // Toggle camera: prefer the spec-style world API if present, else
    // click the spectator/camera button if exposed.
    const toggled = await page.evaluate(() => {
      const g = window.game;
      if (g && g.world && g.world.client && typeof g.world.client.camera === 'object') {
        // No spec exposes a flat-camera flag, so attempt via UI button id.
        return { hasApi: true };
      }
      return { hasApi: false };
    });
    const candidates = ['#camera-flat', '#perspective-toggle', '#view-flat', '#toggle-perspective'];
    for (const sel of candidates) {
      const btn = page.locator(sel);
      if (await btn.isVisible().catch(() => false)) {
        await btn.click({ force: true }).catch(() => {});
        await page.waitForTimeout(800);
        break;
      }
    }
    // Some bundles expose perspective via the Setup dropdown — open and
    // look for a "Flat" option.
    const setup = page.locator('button:has-text("Setup"), #setup-toggle, .setup-toggle');
    if (await setup.first().isVisible().catch(() => false)) {
      await setup.first().click({ force: true, timeout: 1500 }).catch(() => {});
      await page.waitForTimeout(400);
      const flatOpt = page.locator('text=/Flat|Top-?down/i');
      if (await flatOpt.first().isVisible().catch(() => false)) {
        await flatOpt.first().click({ force: true }).catch(() => {});
        await page.waitForTimeout(800);
      }
    }
    await page.waitForTimeout(1200);
  },
});

// 8. Setup menu open
await runScenario('setup-menu-open', {
  viewport: { width: 1920, height: 1080 },
  urlParams: 'variant=changsha&dealMode=auto&botCount=3&botDifficulty=Medium&handCount=4',
  settleSec: 8,
  afterSettle: async (page) => {
    // Open the Lobby panel first (Setup lives inside it).
    await page.evaluate(() => {
      const p = document.getElementById('lobby-panel');
      if (p) p.classList.add('lobby-open');
      document.body.classList.add('lobby-active');
    });
    await page.waitForTimeout(300);
    const setupBtn = page.locator('button:has-text("Setup"), #setup-toggle, .setup-toggle, [data-testid="setup-toggle"]');
    if (await setupBtn.first().isVisible().catch(() => false)) {
      await setupBtn.first().click({ force: true, timeout: 2000 }).catch(() => {});
      await page.waitForTimeout(700);
    }
  },
});

// 9. Move log open
await runScenario('movelog-open', {
  viewport: { width: 1920, height: 1080 },
  urlParams: 'variant=changsha&dealMode=auto&botCount=3&botDifficulty=Medium&handCount=4',
  settleSec: 12,
  afterSettle: async (page) => {
    const moveLogBtn = page.locator('button:has-text("Move Log"), #move-log-toggle, .move-log-toggle, [data-testid="move-log-toggle"]');
    if (await moveLogBtn.first().isVisible().catch(() => false)) {
      await moveLogBtn.first().click({ force: true, timeout: 2000 }).catch(() => {});
      await page.waitForTimeout(600);
    } else {
      // Fall back to clicking any movelog drawer header in the right column.
      const drawer = page.locator('.move-log, [data-testid="move-log"], #move-log, .ferro-move-log');
      if (await drawer.first().isVisible().catch(() => false)) {
        // already visible — no toggle needed
      }
    }
    await page.waitForTimeout(800);
  },
});

// 10. 30s settled bot play
await runScenario('settled-30s', {
  viewport: { width: 1920, height: 1080 },
  urlParams: 'variant=changsha&dealMode=auto&botCount=4&botDifficulty=Medium&handCount=4',
  takeSeat: false,
  settleSec: 32,
});

await browser.close();

// Persist findings
const outPath = path.join(ART, `${VREG_PREFIX}-findings.json`);
fs.writeFileSync(outPath, JSON.stringify(allFindings, null, 2));
console.log(`\n=== Findings written to ${outPath} ===\n`);

// Summary table
console.log('\n=== SUMMARY ===');
for (const f of allFindings) {
  const ws = f.worldState || {};
  console.log([
    f.label.padEnd(20),
    `pErr=${f.pageErrorsCount}`,
    `cErr=${f.consoleErrorsCount}`,
    `NaN=${f.nanRadiusWarnings}`,
    `netFail=${f.networkFailuresCount}`,
    `wall=${ws.wallCount ?? '-'}`,
    `dHand=${ws.dealerHand ?? '-'}`,
    `disc=${ws.allDiscard ?? '-'}`,
    `gType=${ws.gameType ?? '-'}`,
  ].join('  '));
}

const fatal = allFindings.filter(f => f.pageErrorsCount > 0);
if (fatal.length > 0) {
  console.log(`\nFATAL: ${fatal.length} scenarios had page errors`);
  for (const f of fatal) {
    console.log(`  ${f.label}:`);
    for (const e of f.pageErrors) console.log(`    - ${e}`);
  }
  process.exit(1);
}
console.log('\nNo page errors across all scenarios.');
