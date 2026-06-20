// vasquez-d2-views-and-claims.mjs
// ─────────────────────────────────────────────────────────────────────
// Vasquez D2 — supplementary evidence: (1) flat ↔ perspective view toggle
// verified CLEANLY (the prior pass double-toggled via the 'p' key), and
// (2) structured claim-type capture (Pung/Chow/Kong) + a full move-log
// dump from a fresh spectator Hard 4-bot game. Strict Production CSP.
//
//   E2E_BASE_URL=http://127.0.0.1:8094 node playtest-artifacts/vasquez-d2-views-and-claims.mjs
// ─────────────────────────────────────────────────────────────────────

import pw from 'playwright';
const { chromium } = pw;
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const RAW_BASE = process.env.E2E_BASE_URL || 'http://127.0.0.1:8094';
const ORIGIN   = RAW_BASE.replace(/\/autotable\/?$/, '').replace(/\/$/, '');
const EV_DIR   = process.env.EV_DIR; // reuse the main evidence dir if provided
const ART_DIR  = EV_DIR && fs.existsSync(EV_DIR)
  ? EV_DIR
  : path.resolve(__dirname, 'screenshots', `vasquez-regression-d2-views-${new Date().toISOString().replace(/[:.]/g, '-')}`);
fs.mkdirSync(ART_DIR, { recursive: true });

const cspHits = [];
const out = { backend: ORIGIN, startedAt: new Date().toISOString(), views: {}, claims: {}, cspViolations: [] };
const log = (...a) => console.log(...a);

const browser = await chromium.launch({ headless: true });
const ctx = await browser.newContext({ viewport: { width: 1280, height: 800 } });
const page = await ctx.newPage();
page.on('console', (m) => { if (m.type() === 'error' && /content security|style-src|script-src|refused to|violates/i.test(m.text())) cspHits.push(m.text()); });
page.on('pageerror', (e) => { if (/content security|style-src|script-src|refused to|violates/i.test(e.message)) cspHits.push(e.message); });

const gameId = `vasq-d2-vc-${Date.now()}`;
const url = `${ORIGIN}/autotable/?variant=changsha&seat=-1&dealMode=auto&botCount=4&botDifficulty=Hard&handCount=4&gameId=${gameId}`;
log(`\n══════════ Spectator 4-bot game for views + claims ══════════\n${url}`);
await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 30000 });
await page.waitForLoadState('networkidle', { timeout: 30000 }).catch(() => {});

// Wait until the table + tiles are rendered (hands dealt, canvas present).
async function tableReady() {
  return await page.evaluate(() => {
    const g = window.game;
    if (!g || !g.world) return null;
    let hand = 0; for (const t of g.world.things.values()) if (t.slot && t.slot.group === 'hand') hand++;
    return { hand, canvas: document.querySelectorAll('canvas').length, hasMainView: !!g.mainView };
  });
}
{
  const t0 = Date.now();
  while (Date.now() - t0 < 60000) {
    const r = await tableReady();
    if (r && r.hand >= 40 && r.canvas > 0) { log(`  table ready: hand=${r.hand} canvas=${r.canvas} mainView=${r.hasMainView}`); break; }
    await page.waitForTimeout(1000);
  }
}

// ── (1) View toggle: perspective ↔ flat (checkbox 'change' ONLY) ──────
// game.ts binds each settings checkbox 'change' → updateSettings() →
// mainView.setPerspective(checkbox.checked). We read the internal
// mainView.perspective flag (TS-private but present at runtime) to PROVE
// the camera actually switched, not just the checkbox value.
async function viewState() {
  return await page.evaluate(() => {
    const g = window.game;
    const cb = document.getElementById('perspective');
    const mv = g && g.mainView;
    return {
      checkbox: cb ? !!cb.checked : null,
      mainViewPerspective: mv ? !!mv.perspective : null,
      canvas: document.querySelectorAll('canvas').length,
    };
  });
}
async function setPerspective(on) {
  await page.evaluate((on) => {
    const cb = document.getElementById('perspective');
    if (cb && cb.checked !== on) { cb.checked = on; cb.dispatchEvent(new Event('change', { bubbles: true })); }
  }, on);
  await page.waitForTimeout(1200);
}

log('\n── View: PERSPECTIVE ──');
await setPerspective(true);
const persp = await viewState();
await page.screenshot({ path: path.join(ART_DIR, '09-perspective-view.png'), fullPage: true }).catch(() => {});
log(`  ${JSON.stringify(persp)}`);

log('\n── View: FLAT (orthographic) ──');
await setPerspective(false);
const flat = await viewState();
await page.screenshot({ path: path.join(ART_DIR, '10-flat-view.png'), fullPage: true }).catch(() => {});
log(`  ${JSON.stringify(flat)}`);

// restore perspective for any later viewing
await setPerspective(true);

out.views = {
  perspective: { ...persp, usable: persp.checkbox === true && persp.mainViewPerspective === true && persp.canvas > 0 },
  flat:        { ...flat,  usable: flat.checkbox === false && flat.mainViewPerspective === false && flat.canvas > 0 },
  bothUsable: (persp.mainViewPerspective === true && flat.mainViewPerspective === false),
};

// ── (2) Structured claim capture from the move log (Pung/Chow/Kong) ───
// Observe up to 75s; collect every claim-window + executed-meld entry.
const claimWindows = []; // { type, seat, tile, from }
const claimExec = [];    // executed melds (formed a meld / claimed X)
let fullLog = [];
{
  const t0 = Date.now();
  while (Date.now() - t0 < 75000) {
    const snap = await page.evaluate(() => {
      const rows = [];
      for (const r of document.querySelectorAll('#move-log .move-log-entry')) {
        const cls = r.className;
        rows.push({
          cls,
          seat: (r.querySelector('.move-log-seat')?.textContent ?? '').trim(),
          action: (r.querySelector('.move-log-action')?.textContent ?? '').trim(),
        });
      }
      return rows;
    });
    fullLog = snap;
    for (const e of snap) {
      const win = e.action.match(/claim window\s*[—-]\s*(Pung|Chow|Kong|Hu)\s+on\s+(\S+)\s*\(from\s+(.+?)\)/i);
      if (win) {
        const key = `${e.seat}|${win[1]}|${win[2]}|${win[3]}`;
        if (!claimWindows.some(c => c.key === key)) claimWindows.push({ key, type: win[1], by: e.seat, tile: win[2], from: win[3] });
      }
      const exec = e.action.match(/(?:claimed|formed a meld)\b.*?(Pung|Chow|Kong)?/i);
      if (/formed a meld|claimed (pung|chow|kong)/i.test(e.action)) {
        const ty = (e.action.match(/claimed (Pung|Chow|Kong)/i) || [])[1] || (/kong/i.test(e.cls) ? 'Kong' : 'Meld');
        const key = `${e.seat}|${ty}|${e.action}`;
        if (!claimExec.some(c => c.key === key)) claimExec.push({ key, type: ty, by: e.seat, action: e.action });
      }
    }
    // Stop early once we've seen a good spread of claim types.
    const types = new Set(claimWindows.map(c => c.type));
    if (types.has('Pung') && types.has('Chow')) {
      // keep going a bit longer to try for a Kong, but cap it
      if (Date.now() - t0 > 35000) break;
    }
    await page.waitForTimeout(1500);
  }
}
fs.writeFileSync(path.join(ART_DIR, 'move-log-full.json'), JSON.stringify(fullLog, null, 2));
out.claims = {
  windowTypesSeen: [...new Set(claimWindows.map(c => c.type))],
  windows: claimWindows.map(({ key, ...r }) => r),
  executed: claimExec.map(({ key, ...r }) => r),
  diceBreakEntries: fullLog.filter(e => /dice rolled/i.test(e.action)).map(e => e.action),
};

await ctx.close();
await browser.close();

out.cspViolations = cspHits;
out.cspViolationCount = cspHits.length;
out.finishedAt = new Date().toISOString();
fs.writeFileSync(path.join(ART_DIR, 'views-and-claims-summary.json'), JSON.stringify(out, null, 2));

log('\n──────── VIEWS + CLAIMS SUMMARY ────────');
log(`Perspective usable: ${out.views.perspective.usable}  (mainView.perspective=${persp.mainViewPerspective})`);
log(`Flat usable:        ${out.views.flat.usable}  (mainView.perspective=${flat.mainViewPerspective})`);
log(`Both views usable:  ${out.views.bothUsable}`);
log(`Claim window types: ${JSON.stringify(out.claims.windowTypesSeen)}`);
log(`Executed melds:     ${out.claims.executed.length}`);
log(`Dice entries:       ${JSON.stringify(out.claims.diceBreakEntries.slice(0, 4))}`);
log(`CSP violations:     ${out.cspViolationCount}`);
log(`Artifacts: ${ART_DIR}`);
process.exit(0);
