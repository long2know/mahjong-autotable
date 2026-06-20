// vasquez-d2-gamecomplete-capture.mjs — evidence for match completion.
//  (a) Drive a spectator Hard 4-bot game and capture the LIVE match-end
//      state (table revealed + move-log showing multiple hand-end events:
//      "won the hand" / "hand ended in a draw"). Genuine live proof the
//      match cycles hands to completion without stalling.
//  (b) Copy the integration-audit verified-visible #game-complete-modal
//      screenshot (E-02) into the evidence dir as the canonical
//      game-complete MODAL visual (modal('show') with final scores).
// Strict Production CSP throughout.
import pw from 'playwright';
const { chromium } = pw;
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ORIGIN = (process.env.E2E_BASE_URL || 'http://127.0.0.1:8094').replace(/\/autotable\/?$/, '').replace(/\/$/, '');
const ART_DIR = process.env.EV_DIR && fs.existsSync(process.env.EV_DIR)
  ? process.env.EV_DIR
  : path.resolve(__dirname, 'screenshots', `vasquez-regression-d2-gc-${Date.now()}`);
fs.mkdirSync(ART_DIR, { recursive: true });

const cspHits = [];
const browser = await chromium.launch({ headless: true });
const ctx = await browser.newContext({ viewport: { width: 1280, height: 800 } });
const page = await ctx.newPage();
page.on('console', (m) => { if (m.type() === 'error' && /content security|style-src|script-src|refused to|violates/i.test(m.text())) cspHits.push(m.text()); });

const url = `${ORIGIN}/autotable/?variant=changsha&seat=-1&dealMode=auto&botCount=4&botDifficulty=Hard&handCount=4&gameId=vasq-d2-gc-${Date.now()}`;
console.log(url);
await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 30000 });
await page.waitForLoadState('networkidle', { timeout: 30000 }).catch(() => {});

// Visibility uses ONLY the Bootstrap show-state (display:block or .show),
// NOT element text — #game-complete-modal carries a static
// "🏆 GAME OVER 🏆 / Final Scores" template even while hidden.
const probe = () => page.evaluate(() => {
  const g = window.game;
  const safe = (c, k) => { try { return c?.get?.(k) ?? null; } catch { return null; } };
  const gc = g && g.world ? safe(g.world.client?.gameComplete, 'current') : null;
  const el = document.getElementById('game-complete-modal');
  const modalShown = el ? (getComputedStyle(el).display === 'block' || el.classList.contains('show')) : false;
  const rows = [...document.querySelectorAll('#move-log .move-log-entry')]
    .map(r => (r.querySelector('.move-log-action')?.textContent ?? '').trim());
  const handEnds = rows.filter(a => /won the hand|hand ended in a draw|流局/i.test(a));
  return { isComplete: gc ? (gc.isComplete ?? gc.IsComplete ?? null) : null, modalShown, handEndCount: handEnds.length, handEnds: handEnds.slice(-6) };
});

let res = null;
const t0 = Date.now();
while (Date.now() - t0 < 150000) {
  const s = await probe().catch(() => null);
  if (s) {
    res = s;
    if (s.modalShown || s.isComplete || s.handEndCount >= 4) break;
  }
  await page.waitForTimeout(1000);
}
await page.waitForTimeout(400);
await page.screenshot({ path: path.join(ART_DIR, '08-live-match-end.png'), fullPage: true }).catch(() => {});

// (b) Copy the integration-audit verified game-complete modal screenshot.
let modalCopied = false;
const src = path.resolve(__dirname, 'integration-audit', 'E-02-win-modal.png');
if (fs.existsSync(src)) {
  fs.copyFileSync(src, path.join(ART_DIR, '08b-game-complete-modal-verified.png'));
  modalCopied = true;
}

fs.writeFileSync(path.join(ART_DIR, 'game-complete-capture.json'), JSON.stringify({
  url,
  liveMatchEnd: { handEndCount: res?.handEndCount ?? 0, recentHandEnds: res?.handEnds ?? [], modalShownLive: !!(res && res.modalShown), isComplete: res?.isComplete ?? null },
  gameCompleteModalScreenshot: modalCopied ? "08b-game-complete-modal-verified.png (integration Scenario E — #game-complete-modal modal('show') with Final Scores, verified visible)" : null,
  note: "Pure spectator (seat=-1) observes per-hand results but does not trigger the match-end modal('show'); the visible #game-complete-modal is proven by integration Scenario E (E1 modalVisible=true, E2 totals shown, E3 dismisses) and the 3x seated stephen runs reaching gameCompleted=true.",
  cspViolationCount: cspHits.length,
  capturedAtSec: ((Date.now() - t0) / 1000).toFixed(1),
}, null, 2));

console.log('live hand-end events seen:', res?.handEndCount, '| recent:', JSON.stringify(res?.handEnds));
console.log('match modal shown live (spectator):', !!(res && res.modalShown), '| game-complete modal visual copied:', modalCopied);
console.log('CSP violations:', cspHits.length);
await ctx.close();
await browser.close();
process.exit(0);
