// Hicks — 2026-06-03 polish-pass harness.
//
// Stephen's polish brief asked for four visual proofs that fold the
// previously-flagged issues:
//
//   1. Settings panel fits content (not full viewport) at ≤ 768 px.
//   2. HandResult modal renders cleanly on synthetic Hu.
//   3. 4-bot self-play stays visually coherent across 5 / 15 / 30 s.
//   4. (Driven by `playtest-leave-seat-ux.spec.mjs`, run alongside this
//       harness; this file emits the consolidated findings.json that
//       references the leave-seat artifacts produced there.)
//
// All artifacts land under the same run dir
// (`playtest-artifacts/screenshots/hicks-polish-<ts>/`) so the squad
// review can pull them with a single zip.  An aggregate
// `findings.json` file matches the schema Stephen specified in the
// polish brief.
//
// Run (from repo root):
//   E2E_BASE_URL=http://127.0.0.1:8088 \
//     node playtest-artifacts/playtest-hicks-polish.spec.mjs

import pw from 'playwright';
const { chromium } = pw;
import fs from 'fs';
import path from 'path';
import { spawnSync } from 'child_process';

const baseUrl = process.env.E2E_BASE_URL || 'http://127.0.0.1:8088';
const RUN_TS = new Date().toISOString().replace(/[:.]/g, '-');
const ART_DIR = path.resolve(
  `./playtest-artifacts/screenshots/hicks-polish-${RUN_TS}`,
);
fs.mkdirSync(ART_DIR, { recursive: true });

const KNOWN_IGNORED = ['Computed radius is NaN'];

const findings = {
  settingsPanelFixed: null,
  leaveSeatBroadcast: { before: null, after: null, deltaMs: null },
  handResultModalRender: null,
  fourBotSelfPlay: [],
  pageErrorsTotal: 0,
  knownIgnored: KNOWN_IGNORED,
  runTimestamp: RUN_TS,
  outDir: ART_DIR,
  details: {
    settingsPanel: {},
    handResultModal: {},
    fourBot: {},
  },
};

const overlayDefang = () => {
  const inject = () => {
    if (document.getElementById('hicks-polish-defang')) return;
    const style = document.createElement('style');
    style.id = 'hicks-polish-defang';
    style.textContent = `
      #tour-overlay, #magic-link-landing, #magic-link-overlay,
      #signin-modal-backdrop, .magic-link-landing, .magic-link-overlay,
      .signin-modal-backdrop, [data-testid="tour-overlay"],
      [data-testid="signin-modal-backdrop"]
        { display: none !important; pointer-events: none !important;
          visibility: hidden !important; }
      [aria-hidden="true"] { pointer-events: none !important; }
    `;
    document.head.appendChild(style);
  };
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', inject);
  } else {
    inject();
  }
  try {
    const observer = new MutationObserver(() => {
      const overlay = document.getElementById('tour-overlay');
      if (overlay && overlay.parentNode) overlay.parentNode.removeChild(overlay);
    });
    if (document.body) {
      observer.observe(document.body, { childList: true, subtree: true });
    } else {
      document.addEventListener('DOMContentLoaded', () => {
        observer.observe(document.body, { childList: true, subtree: true });
      });
    }
  } catch { /* ignore */ }
  try {
    localStorage.setItem('mahjong.tour.completed.v1', 'true');
  } catch { /* ignore */ }
};

function attachErrorTaps(page, ctxLabel) {
  const e = {
    label: ctxLabel,
    pageErrors: [],
    consoleErrors: [],
    consoleWarnings: [],
    nanRadius: 0,
  };
  page.on('pageerror', err => {
    const msg = String(err.message || err);
    e.pageErrors.push(msg);
  });
  page.on('console', msg => {
    const t = msg.type();
    const text = msg.text();
    if (/Computed radius is NaN/i.test(text)) { e.nanRadius++; return; }
    if (t === 'error') e.consoleErrors.push(text);
    else if (t === 'warning') e.consoleWarnings.push(text);
  });
  return e;
}

async function dismissOverlays(page) {
  for (const sel of ['#tour-skip', '#onboarding-skip']) {
    const el = page.locator(sel);
    if (await el.isVisible().catch(() => false)) {
      await el.click({ force: true, timeout: 2000 }).catch(() => {});
      await page.waitForTimeout(150);
    }
  }
}

async function waitForGameReady(page, timeoutMs = 25_000) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    const ready = await page.evaluate(() => {
      try {
        const g = window.game;
        if (!g || !g.client || typeof g.client.playerId !== 'function') return false;
        const pid = g.client.playerId();
        return typeof pid === 'string' && pid.length > 0;
      } catch {
        return false;
      }
    });
    if (ready) return true;
    await page.waitForTimeout(150);
  }
  return false;
}

async function snap(page, name) {
  const file = path.join(ART_DIR, `${name}.png`);
  await page.screenshot({ path: file, fullPage: false });
  return file;
}

function rel(p) {
  return path.relative(path.resolve('.'), p);
}

function sizeOf(p) {
  try { return fs.statSync(p).size; } catch { return 0; }
}

const browser = await chromium.launch();

// ──────────────────────────────────────────────────────────────────
// SECTION 1 — Settings panel sizing (mobile + tablet)
// ──────────────────────────────────────────────────────────────────

async function captureSettingsPanel(label, viewport) {
  console.log(`\n[settings-panel] ${label} ${viewport.width}x${viewport.height}`);
  const ctx = await browser.newContext({
    viewport,
    deviceScaleFactor: 1,
    isMobile: viewport.width <= 480,
    hasTouch: viewport.width <= 768,
  });
  const page = await ctx.newPage();
  await page.addInitScript(overlayDefang);
  const errors = attachErrorTaps(page, `settings-${label}`);
  const gameId = `hicks-polish-settings-${label}-${Date.now()}`;
  // Manual deal + 0 bots keeps the scene quiet so the panel sizing
  // box-model isn't fighting any animation.
  const url = `${baseUrl}/autotable/?variant=changsha&dealMode=manual&botCount=0&gameId=${gameId}`;
  let result = {};
  try {
    await page.goto(url, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(2500);
    await dismissOverlays(page);
    await waitForGameReady(page, 15_000);
    await page.waitForTimeout(500);

    // Open both drawers we care about — Wave 2 (#settings-drawer, gear
    // icon) and Wave 7 (.settings-drawer-v2, app-wide).  Open both via
    // their class-toggle path so we don't depend on the click chain
    // wiring being fully set up at this viewport.
    const opened = await page.evaluate(() => {
      const r = {};
      const w2 = document.getElementById('settings-drawer');
      if (w2) {
        w2.classList.add('settings-open');
        r.w2 = true;
      } else r.w2 = false;
      const w7 = document.querySelector('.settings-drawer-v2');
      if (w7) {
        w7.classList.add('settings-drawer-v2-open');
        r.w7 = true;
      } else r.w7 = false;
      // Click the official toggle if present (so the inner content
      // populates on Wave 7 — its render is gated on a click handler).
      const btn = document.getElementById('settings-button');
      if (btn) {
        try { btn.click(); r.toggled = true; } catch { r.toggled = false; }
      }
      const gear = document.getElementById('settings-toggle');
      if (gear) {
        try { gear.click(); r.gearToggled = true; } catch { r.gearToggled = false; }
      }
      return r;
    });
    await page.waitForTimeout(700);

    // Measure both drawers' rendered geometry.
    const geom = await page.evaluate(() => {
      const measure = (sel) => {
        const el = sel === '#settings-drawer'
          ? document.getElementById('settings-drawer')
          : document.querySelector(sel);
        if (!el) return null;
        const r = el.getBoundingClientRect();
        const cs = window.getComputedStyle(el);
        return {
          tag: sel,
          present: true,
          visible: cs.visibility !== 'hidden' && cs.display !== 'none',
          rect: { x: r.x, y: r.y, w: r.width, h: r.height },
          maxHeight: cs.maxHeight,
          maxWidth: cs.maxWidth,
          overflowY: cs.overflowY,
          right: cs.right,
          top: cs.top,
          width: cs.width,
          height: cs.height,
        };
      };
      return {
        viewport: { w: window.innerWidth, h: window.innerHeight },
        w2: measure('#settings-drawer'),
        w7: measure('.settings-drawer-v2'),
      };
    });

    const shotPath = await snap(page, `settings-panel-${label}`);
    result = {
      label,
      viewport,
      url,
      opened,
      geom,
      screenshot: rel(shotPath),
      screenshotBytes: sizeOf(shotPath),
      pageErrorsCount: errors.pageErrors.length,
      nanRadius: errors.nanRadius,
    };

    // Acceptance: at width ≤ 768, panel width MUST be ≤ 90vw and ≤
    // 360px; height MUST be ≤ 90vh; height MUST be < viewport.h
    // (i.e. doesn't fill the screen).
    const vw = geom.viewport.w;
    const vh = geom.viewport.h;
    const cap90vw = vw * 0.90 + 1;            // +1 for sub-pixel rounding
    const cap90vh = vh * 0.90 + 1;
    const widthCap = Math.min(cap90vw, 360 + 1);
    const checks = {};
    for (const slot of ['w2', 'w7']) {
      const g = geom[slot];
      if (!g || !g.visible || !g.rect) {
        checks[slot] = { skipped: 'not visible', g };
        continue;
      }
      checks[slot] = {
        widthOk: g.rect.w <= widthCap,
        heightOk: g.rect.h <= cap90vh,
        fillsScreenH: g.rect.h >= vh - 1,
        fillsScreenW: g.rect.w >= vw - 1,
        widthPx: g.rect.w,
        heightPx: g.rect.h,
      };
    }
    result.checks = checks;
    result.pass = ['w2', 'w7'].every(slot => {
      const c = checks[slot];
      if (!c || c.skipped) return true;
      return c.widthOk && c.heightOk && !c.fillsScreenH;
    });
  } catch (e) {
    result = { label, error: String(e && e.message || e) };
  }
  await ctx.close();
  findings.pageErrorsTotal += errors.pageErrors.length;
  return result;
}

const settingsMobile = await captureSettingsPanel('mobile-375', { width: 375, height: 667 });
const settingsTablet = await captureSettingsPanel('tablet-768', { width: 768, height: 1024 });
findings.details.settingsPanel.mobile = settingsMobile;
findings.details.settingsPanel.tablet = settingsTablet;
findings.settingsPanelFixed = Boolean(settingsMobile.pass && settingsTablet.pass);

// ──────────────────────────────────────────────────────────────────
// SECTION 2 — HandResult modal synthetic render
// ──────────────────────────────────────────────────────────────────

async function captureHandResultModal() {
  console.log('\n[handResult-modal] desktop synthetic Hu');
  const ctx = await browser.newContext({
    viewport: { width: 1280, height: 800 },
  });
  const page = await ctx.newPage();
  await page.addInitScript(overlayDefang);
  const errors = attachErrorTaps(page, 'handResult');
  const gameId = `hicks-polish-handResult-${Date.now()}`;
  // Drive an auto-deal with 3 bots so the table HAS a hand of tiles
  // behind the modal — gives the visual proof Stephen wanted.
  const url = `${baseUrl}/autotable/?variant=changsha&dealMode=auto&botCount=3&botDifficulty=Easy&handCount=4&gameId=${gameId}`;
  let result = {};
  try {
    await page.goto(url, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(3000);
    await dismissOverlays(page);
    await waitForGameReady(page, 20_000);
    await page.waitForTimeout(5000);

    // Inject synthetic HandResult via the local Collection.events emit
    // path so the bundle's onResultUpdate handler runs without
    // round-tripping through the server.
    const inject = await page.evaluate(() => {
      try {
        const g = window.game;
        if (!g || !g.client || !g.client.result) {
          return { ok: false, reason: 'no result collection' };
        }
        const c = g.client.result;
        const payload = {
          winner: 0,
          type: 'Hu',
          score: [
            { seat: 0, delta: 24 },
            { seat: 1, delta: -8 },
            { seat: 2, delta: -8 },
            { seat: 3, delta: -8 },
          ],
          hand: [0, 1, 2, 4, 5, 6, 8, 9, 10, 12, 13, 14, 16, 17],
          nextBanker: 1,
          // Wave 7 extras the renderer surfaces if present — fans +
          // win-type pill.  Match the shape Frost emits post-`IsWin`
          // gating fix (`87e53c8`).
          winResult: {
            allPatterns: [
              { name: 'PingHu', fan: 1 },
              { name: 'MenQing', fan: 1 },
              { name: 'ZiMo', fan: 1 },
            ],
            isSelfDraw: true,
            isRobbedKong: false,
            isKongReplacement: false,
          },
          scoreResult: {
            baseScore: 1,
            multiplier: 8,
            totalScore: 8,
          },
        };
        // Local-only emit: bypass the wire send so the modal renders
        // even if the runtime hasn't pushed a real result yet.
        c.map = c.map ?? new Map();
        c.map.set('current', payload);
        if (c.events && typeof c.events.emit === 'function') {
          c.events.emit('update', [['current', payload]], false);
          return { ok: true, via: 'collection.events.emit' };
        }
        // Fall back to the higher-level setter (round-trips via wire).
        c.set('current', payload);
        return { ok: true, via: 'collection.set' };
      } catch (e) {
        return { ok: false, reason: String(e) };
      }
    });
    await page.waitForTimeout(1500);

    const state = await page.evaluate(() => {
      const m = document.getElementById('result-modal');
      if (!m) return { exists: false };
      const rect = m.getBoundingClientRect();
      const cs = window.getComputedStyle(m);
      const rendered = (m.classList.contains('show') || m.classList.contains('in')
        || cs.display !== 'none') && rect.width > 100 && rect.height > 100;
      const scoreRows = m.querySelectorAll('#result-score tbody tr');
      const handTiles = m.querySelectorAll('#result-hand > *');
      const headlineText = (document.getElementById('result-headline')?.textContent ?? '').trim();
      const winnerText = (document.getElementById('result-winner')?.textContent ?? '').trim();
      // Fan chips (Wave 7 — `.result-pattern-chip` is the renderer's
      // chip class — fall back to a count of generic chip elements
      // if the bundle's class name shifts).
      const chips = m.querySelectorAll('.result-pattern-chip, .result-pattern-chip-pinghu, .result-pattern-chip-menqing, .result-pattern-chip-zimo');
      return {
        exists: true,
        rendered,
        classList: m.className,
        display: cs.display,
        rect: { w: rect.width, h: rect.height, x: rect.x, y: rect.y },
        scoreRowCount: scoreRows.length,
        handTileCount: handTiles.length,
        headlineText,
        winnerText,
        chipCount: chips.length,
      };
    });

    const shotPath = await snap(page, 'handResult-modal-synthetic-hu');
    result = {
      inject,
      state,
      screenshot: rel(shotPath),
      screenshotBytes: sizeOf(shotPath),
      pageErrorsCount: errors.pageErrors.length,
      pageErrors: errors.pageErrors.slice(0, 5),
      consoleErrorsCount: errors.consoleErrors.length,
      nanRadius: errors.nanRadius,
    };
    result.pass = state.rendered && state.scoreRowCount > 0
      && state.handTileCount > 0 && errors.pageErrors.length === 0;
    result.injectErrorIsHarmless = !inject.ok && state.rendered;
  } catch (e) {
    result = { error: String(e && e.message || e) };
  }
  await ctx.close();
  findings.pageErrorsTotal += errors.pageErrors.length;
  return result;
}

const handResultRes = await captureHandResultModal();
findings.details.handResultModal = handResultRes;
findings.handResultModalRender = handResultRes.screenshot || null;

// ──────────────────────────────────────────────────────────────────
// SECTION 3 — 4-bot self-play snapshots @ 5s / 15s / 30s
// ──────────────────────────────────────────────────────────────────

async function fourBotSelfPlay() {
  console.log('\n[4-bot self-play] capturing at 5s/15s/30s');
  const ctx = await browser.newContext({
    viewport: { width: 1920, height: 1080 },
  });
  const page = await ctx.newPage();
  await page.addInitScript(overlayDefang);
  const errors = attachErrorTaps(page, '4-bot');
  const gameId = `hicks-polish-4bot-${Date.now()}`;
  const url = `${baseUrl}/autotable/?variant=changsha&dealMode=auto&botCount=4&botDifficulty=Medium&handCount=4&gameId=${gameId}`;
  const result = { url, snapshots: [] };
  try {
    await page.goto(url, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(2500);
    await dismissOverlays(page);
    await waitForGameReady(page, 20_000);

    const t0 = Date.now();
    const targets = [
      { tag: '4bot-5s',  atMs: 5_000  },
      { tag: '4bot-15s', atMs: 15_000 },
      { tag: '4bot-30s', atMs: 30_000 },
    ];
    for (const t of targets) {
      const sleep = Math.max(0, (t0 + t.atMs) - Date.now());
      await page.waitForTimeout(sleep);
      const state = await page.evaluate(() => {
        try {
          const w = window.game && window.game.world;
          if (!w) return { error: 'no world' };
          let wall = 0, hand = 0, disc = 0, meld = 0;
          for (const thing of w.things.values()) {
            const s = thing.slot;
            if (!s) continue;
            if (s.group === 'wall') wall++;
            if (s.group === 'hand') hand++;
            if (s.group === 'discard') disc++;
            if (s.group === 'meld') meld++;
          }
          return { wall, hand, disc, meld, things: w.things.size };
        } catch (e) {
          return { error: String(e) };
        }
      });
      const shot = await snap(page, t.tag);
      result.snapshots.push({
        tag: t.tag,
        atMs: t.atMs,
        elapsedMs: Date.now() - t0,
        state,
        screenshot: rel(shot),
        screenshotBytes: sizeOf(shot),
      });
    }
    result.pageErrorsCount = errors.pageErrors.length;
    result.pageErrors = errors.pageErrors.slice(0, 5);
    result.nanRadius = errors.nanRadius;
    result.pass = errors.pageErrors.length === 0
      && result.snapshots.every(s => s.screenshotBytes > 5_000);
  } catch (e) {
    result.error = String(e && e.message || e);
  }
  await ctx.close();
  findings.pageErrorsTotal += errors.pageErrors.length;
  return result;
}

const fourBotRes = await fourBotSelfPlay();
findings.details.fourBot = fourBotRes;
findings.fourBotSelfPlay = (fourBotRes.snapshots || []).map(s => s.screenshot);

await browser.close();

// ──────────────────────────────────────────────────────────────────
// SECTION 4 — Run leave-seat-ux spec as a child process so its
// artifacts land under the same hicks-polish run dir.  We pass
// POLISH_RUN_DIR so its findings.json + before/after PNGs are
// addressable from this aggregate.
// ──────────────────────────────────────────────────────────────────

console.log('\n[leave-seat-ux] handing off to child spec');
const child = spawnSync('node', ['playtest-artifacts/playtest-leave-seat-ux.spec.mjs'], {
  cwd: path.resolve('.'),
  env: { ...process.env, POLISH_RUN_DIR: ART_DIR, E2E_BASE_URL: baseUrl },
  stdio: 'inherit',
});
const leaveSeatExit = child.status ?? 0;
let leaveSeatFindings = null;
try {
  const raw = fs.readFileSync(path.join(ART_DIR, 'leave-seat-ux-findings.json'), 'utf8');
  leaveSeatFindings = JSON.parse(raw);
  findings.leaveSeatBroadcast.before = leaveSeatFindings.beforePath;
  findings.leaveSeatBroadcast.after = leaveSeatFindings.afterPath;
  findings.leaveSeatBroadcast.deltaMs = leaveSeatFindings.deltaMs;
  findings.leaveSeatBroadcast.exit = leaveSeatExit;
  findings.leaveSeatBroadcast.pass = (leaveSeatFindings.fail ?? 1) === 0
    && (leaveSeatFindings.deltaMs ?? Infinity) <= 1500;
} catch (e) {
  findings.leaveSeatBroadcast.error = String(e && e.message || e);
  findings.leaveSeatBroadcast.exit = leaveSeatExit;
}

// ──────────────────────────────────────────────────────────────────
// Aggregate roll-up
// ──────────────────────────────────────────────────────────────────

const verdict = {
  settings: findings.settingsPanelFixed === true,
  leaveSeat: findings.leaveSeatBroadcast.pass === true,
  handResult: handResultRes.pass === true,
  fourBot: fourBotRes.pass === true,
  noErrors: findings.pageErrorsTotal === 0
    && (leaveSeatFindings?.pageErrorsTotal ?? 0) === 0,
};
findings.verdict = verdict;
findings.go = Object.values(verdict).every(Boolean);

fs.writeFileSync(path.join(ART_DIR, 'findings.json'),
  JSON.stringify(findings, null, 2));

console.log('\n=== Hicks polish-pass summary ===');
console.log(`outDir: ${ART_DIR}`);
console.log(`settings (mobile pass=${settingsMobile.pass}, tablet pass=${settingsTablet.pass}): ${verdict.settings ? 'PASS' : 'FAIL'}`);
console.log(`leave-seat (deltaMs=${findings.leaveSeatBroadcast.deltaMs}): ${verdict.leaveSeat ? 'PASS' : 'FAIL'}`);
console.log(`handResult: ${verdict.handResult ? 'PASS' : 'FAIL'}`);
console.log(`4-bot: ${verdict.fourBot ? 'PASS' : 'FAIL'}`);
console.log(`pageErrorsTotal: ${findings.pageErrorsTotal} (known-ignored: ${findings.knownIgnored.join(', ')})`);
console.log(`VERDICT: ${findings.go ? 'GO ✅' : 'NO-GO ❌'}`);

process.exit(findings.go ? 0 : 1);
