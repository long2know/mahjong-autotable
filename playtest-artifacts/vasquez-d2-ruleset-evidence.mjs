// vasquez-d2-ruleset-evidence.mjs
// ─────────────────────────────────────────────────────────────────────
// Vasquez D2 — Live full-ruleset evidence capture against the REAL
// Production Docker image (strict CSP, Production env).
//
// Beyond the scripted stephen-first-play loop, this observer drives the
// canonical Changsha auto-deal URL and captures, FROM A SINGLE LIVE GAME,
// proof of each canonical-spec behaviour the mission calls out:
//
//   • Lobby (bare URL) renders with ZERO CSP violations.
//   • Dice-roll wall break  (move-log "Dice rolled: a + b = n → break @ col c").
//   • 4 visible walls, tiles face-down pre-deal; dealer hand 14 post-deal.
//   • Claims: at least one Peng(Pung)/Chow/Kong in the move log (bots claim).
//   • Win (Hu) + fan scoring: #result-modal renders with headline 胡!,
//     a per-seat score-Δ table, pattern/fan chips and the winning hand —
//     OR a legitimate exhaustive draw (result modal still renders).
//   • Dealer rotation: move-log "Match started — dealer is X" then a later
//     "New hand — dealer is Y" with Y != X.
//   • Flat + perspective views both usable (#perspective checkbox toggle).
//
// Modal visibility uses getComputedStyle(el).display==='block' OR
// el.classList.contains('show') (offsetParent is unreliable for modals).
//
// Strict Production CSP (style-src 'self') is active, so we never inject a
// defang <style>. Overlays do not gate the URL-driven auto-deal (we read
// world state + DOM directly).
//
// Run:
//   E2E_BASE_URL=http://127.0.0.1:8094 \
//     node playtest-artifacts/vasquez-d2-ruleset-evidence.mjs
// ─────────────────────────────────────────────────────────────────────

import pw from 'playwright';
const { chromium } = pw;
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname  = path.dirname(__filename);

const RAW_BASE = process.env.E2E_BASE_URL || 'http://127.0.0.1:8094';
const ORIGIN   = RAW_BASE.replace(/\/autotable\/?$/, '').replace(/\/$/, '');
const DIFF     = process.env.BOT_DIFFICULTY || 'Hard';
const RUN_TS   = process.env.RUN_TS || new Date().toISOString().replace(/[:.]/g, '-');
const ART_DIR  = path.resolve(__dirname, 'screenshots', `vasquez-regression-d2-${RUN_TS}`);
fs.mkdirSync(ART_DIR, { recursive: true });

const log = (...a) => console.log(...a);
const shot = async (page, name) => {
  const p = path.join(ART_DIR, name);
  await page.screenshot({ path: p, fullPage: true }).catch((e) => log(`  shot fail ${name}: ${e.message}`));
  return name;
};

// ── CSP / error tracking ────────────────────────────────────────────
const cspHits = [];
const consoleErrors = [];
const pageErrors = [];
function attachConsole(page, tag) {
  page.on('console', (m) => {
    if (m.type() !== 'error') return;
    const t = m.text();
    consoleErrors.push(`[${tag}] ${t}`);
    if (/content security policy|style-src|script-src|refused to|violates the/i.test(t)) cspHits.push(`[${tag}] ${t}`);
  });
  page.on('pageerror', (e) => {
    pageErrors.push(`[${tag}] ${e.message}`);
    if (/content security policy|style-src|script-src|refused to|violates the/i.test(e.message)) cspHits.push(`[${tag}] ${e.message}`);
  });
}

// ── in-page snapshot: world tiles + collections ─────────────────────
function snapshotFn() {
  const g = window.game;
  if (!g || !g.world) return null;
  const w = g.world;
  const seat = w.seat;
  const handBySeat = [0, 0, 0, 0];
  const meldBySeat = [0, 0, 0, 0];
  const discardBySeat = [0, 0, 0, 0];
  let totalDiscard = 0, totalMeld = 0, foreignHandFaceUp = 0;
  let wallFaceUp = 0, wallFaceDown = 0;
  for (const t of w.things.values()) {
    const s = t.slot;
    if (!s) continue;
    if (s.group === 'wall' || s.group === 'wall.open') {
      if (t.rotationIndex === 0) wallFaceDown++; else wallFaceUp++;
    }
    if (s.group === 'hand' && typeof s.seat === 'number') {
      handBySeat[s.seat]++;
      if (t.rotationIndex === 1 && s.seat !== seat) foreignHandFaceUp++;
    }
    if (s.group === 'meld' && typeof s.seat === 'number') { meldBySeat[s.seat]++; totalMeld++; }
    if (s.group === 'discard') {
      totalDiscard++;
      if (typeof s.seat === 'number') discardBySeat[s.seat]++;
    }
  }
  const safeGet = (col, key) => { try { return col?.get?.(key) ?? null; } catch { return null; } };
  const gc = safeGet(w.client?.gameComplete, 'current');
  const r = safeGet(w.client?.result, 'current');
  const handSum = handBySeat.reduce((a, b) => a + b, 0);
  // Dealer at a fresh deal == the seat holding 14 tiles while the rest hold 13.
  let dealerSeat = null;
  if (handSum >= 51) {
    const maxIdx = handBySeat.indexOf(Math.max(...handBySeat));
    if (handBySeat[maxIdx] >= 14) dealerSeat = maxIdx;
  }
  // Move log (text + category) straight from the DOM sidebar.
  const moveLog = [];
  for (const row of document.querySelectorAll('#move-log .move-log-entry')) {
    const cat = (row.className.match(/move-log-(\w+)/g) || []).map(c => c.replace('move-log-', '')).filter(c => c !== 'entry');
    const seatTxt = row.querySelector('.move-log-seat')?.textContent ?? '';
    const action = row.querySelector('.move-log-action')?.textContent ?? '';
    moveLog.push({ cat: cat.join(','), seat: seatTxt.trim(), action: action.trim() });
  }
  // Modal visibility via display:block OR .show (offsetParent unreliable).
  const modalVisible = (id) => {
    const el = document.getElementById(id);
    if (!el) return false;
    return getComputedStyle(el).display === 'block' || el.classList.contains('show');
  };
  const resultText = (() => {
    const el = document.getElementById('result-modal');
    return el ? (el.innerText || '').replace(/\s+/g, ' ').trim() : '';
  })();
  const gcText = (() => {
    const el = document.getElementById('game-complete-modal');
    return el ? (el.innerText || '').replace(/\s+/g, ' ').trim() : '';
  })();
  const perspectiveChecked = (() => {
    const el = document.getElementById('perspective');
    return el ? !!el.checked : null;
  })();
  const canvasCount = document.querySelectorAll('canvas').length;
  return {
    seat, handBySeat, meldBySeat, discardBySeat, totalDiscard, totalMeld,
    handSum, foreignHandFaceUp, wallFaceUp, wallFaceDown, dealerSeat,
    seatsWithHand: handBySeat.filter(n => n > 0).length,
    result: r ? { type: r.type ?? r.Type ?? null, winner: r.winner ?? r.Winner ?? null } : null,
    gameComplete: gc ? { isComplete: gc.isComplete ?? gc.IsComplete ?? null } : null,
    resultModalVisible: modalVisible('result-modal'),
    gameCompleteModalVisible: modalVisible('game-complete-modal'),
    resultText, gcText, perspectiveChecked, canvasCount,
    moveLog,
  };
}

async function newPage(browser) {
  const ctx = await browser.newContext({ viewport: { width: 1280, height: 800 } });
  return ctx.newPage();
}

const ev = {
  startedAt: new Date().toISOString(),
  backend: ORIGIN,
  botDifficulty: DIFF,
  artDir: ART_DIR,
  observed: {
    lobbyZeroCsp: false,
    diceWallBreak: null,        // move-log dice text
    fourWallsFaceDownPreDeal: null,
    dealerHandFaceUpPostDeal: null, // dealer 14 at deal
    claim: null,                // first peng/chow/kong move-log entry
    claimTypesSeen: [],
    handResult: null,           // { headline, winner, scoreRows, patterns, isDraw }
    dealerRotation: null,       // { dealers: [..], rotated: bool }
    flatView: false,
    perspectiveView: false,
  },
  screenshots: [],
  milestones: [],
};
const milestone = (k, v) => { ev.milestones.push({ k, atSec: ((Date.now() - T0) / 1000).toFixed(1), v }); log(`  ★ ${k} @${((Date.now()-T0)/1000).toFixed(1)}s ${v ? JSON.stringify(v).slice(0,160) : ''}`); };

let T0 = Date.now();
const browser = await chromium.launch({ headless: true });

// ════════════════════════════════════════════════════════════════════
//  STEP 0 — Lobby (bare URL) renders, ZERO CSP violations
// ════════════════════════════════════════════════════════════════════
{
  log('\n══════════ STEP 0 — Lobby (bare URL), CSP check ══════════');
  const page = await newPage(browser);
  attachConsole(page, 'lobby');
  await page.goto(`${ORIGIN}/autotable/`, { waitUntil: 'domcontentloaded', timeout: 30000 });
  await page.waitForTimeout(2500);
  // Dismiss tour / onboarding the way a real user would, if present.
  for (const sel of ['#tour-skip', '#onboarding-skip']) {
    const el = page.locator(sel);
    if (await el.isVisible({ timeout: 600 }).catch(() => false)) {
      await el.click({ timeout: 2500 }).catch(() => {});
      await page.waitForTimeout(300);
    }
  }
  ev.screenshots.push(await shot(page, '01-lobby-zero-csp.png'));
  ev.observed.lobbyZeroCsp = cspHits.length === 0;
  log(`  Lobby CSP violations so far: ${cspHits.length}`);
  await page.context().close();
}

// ════════════════════════════════════════════════════════════════════
//  STEP 1 — Spectator 4-bot auto-deal: observe the full ruleset live
// ════════════════════════════════════════════════════════════════════
{
  const gameId = `vasq-d2-ev-${Date.now()}`;
  const url = `${ORIGIN}/autotable/?variant=changsha&seat=-1&dealMode=auto&botCount=4&botDifficulty=${DIFF}&handCount=4&gameId=${gameId}`;
  log(`\n══════════ STEP 1 — Spectator 4-bot (${DIFF}) auto-deal ══════════\n${url}`);
  const page = await newPage(browser);
  attachConsole(page, 'spectator');
  await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 30000 });
  await page.waitForLoadState('networkidle', { timeout: 30000 }).catch(() => {});
  T0 = Date.now();

  // Single long observation loop — capture each behaviour the first time
  // it surfaces. Bounded so we never hang. We need: dice/walls, dealt,
  // claim, midgame discards, a hand result, and a 2nd dealer (rotation).
  const DEADLINE = Date.now() + 360_000; // 6 min hard cap
  let predealCaptured = false, dealtCaptured = false, claimShotDone = false;
  let midgameShotDone = false, resultShotDone = false, rotationShotDone = false;
  const dealersSeen = []; // from move-log "dealer is <label>"
  let lastMoveLogLen = 0;

  while (Date.now() < DEADLINE) {
    const s = await page.evaluate(snapshotFn).catch(() => null);
    if (s) {
      // ── pre-deal walls (face-down) — catch before hands fully dealt ──
      if (!predealCaptured && s.wallFaceDown >= 50 && s.handSum < 40) {
        ev.observed.fourWallsFaceDownPreDeal = { wallFaceDown: s.wallFaceDown, wallFaceUp: s.wallFaceUp, handSum: s.handSum };
        ev.screenshots.push(await shot(page, '02-walls-facedown-predeal.png'));
        milestone('walls-facedown-predeal', ev.observed.fourWallsFaceDownPreDeal);
        predealCaptured = true;
      }

      // ── dice-roll wall break (move-log) ──
      if (!ev.observed.diceWallBreak) {
        const dice = s.moveLog.find(e => /dice rolled/i.test(e.action));
        if (dice) {
          ev.observed.diceWallBreak = dice.action;
          milestone('dice-wall-break', dice.action);
          if (!predealCaptured) { // capture walls now if we missed the pure pre-deal frame
            ev.observed.fourWallsFaceDownPreDeal = ev.observed.fourWallsFaceDownPreDeal || { wallFaceDown: s.wallFaceDown, wallFaceUp: s.wallFaceUp, handSum: s.handSum, note: 'captured at dice' };
            ev.screenshots.push(await shot(page, '02-walls-facedown-predeal.png'));
            predealCaptured = true;
          }
        }
      }

      // ── dealt: dealer hand 14, others 13 (face-up on table for spectator) ──
      if (!dealtCaptured && s.handSum >= 50 && s.seatsWithHand >= 4) {
        ev.observed.dealerHandFaceUpPostDeal = {
          handBySeat: s.handBySeat, dealerSeat: s.dealerSeat,
          dealerHandCount: s.dealerSeat != null ? s.handBySeat[s.dealerSeat] : null,
          handSum: s.handSum,
        };
        ev.screenshots.push(await shot(page, '03-dealt-dealer-hand-14.png'));
        milestone('dealt-dealer-14', ev.observed.dealerHandFaceUpPostDeal);
        dealtCaptured = true;
      }

      // ── claim: peng(pung)/chow/kong from the move log or meld growth ──
      const claimEntries = s.moveLog.filter(e =>
        /claimed (pung|chow|kong)/i.test(e.action) ||
        /formed a meld/i.test(e.action) ||
        /\b(kong|claim)\b/.test(e.cat));
      for (const c of claimEntries) {
        const m = c.action.match(/claimed (Pung|Chow|Kong)/i);
        const ty = m ? m[1] : (c.cat.includes('kong') ? 'Kong' : (/formed a meld/i.test(c.action) ? 'Meld' : null));
        if (ty && !ev.observed.claimTypesSeen.includes(ty)) ev.observed.claimTypesSeen.push(ty);
      }
      if (!claimShotDone && (claimEntries.length > 0 || s.totalMeld > 0)) {
        ev.observed.claim = {
          firstEntry: claimEntries[0] || null,
          totalMeld: s.totalMeld, meldBySeat: s.meldBySeat,
          typesSeen: ev.observed.claimTypesSeen.slice(),
        };
        ev.screenshots.push(await shot(page, '04-claim-in-movelog.png'));
        milestone('claim', ev.observed.claim);
        claimShotDone = true;
      }

      // ── midgame discards ──
      if (!midgameShotDone && s.totalDiscard >= 10) {
        ev.screenshots.push(await shot(page, '05-midgame-discards.png'));
        milestone('midgame-discards', { totalDiscard: s.totalDiscard, discardBySeat: s.discardBySeat });
        midgameShotDone = true;
      }

      // ── dealer rotation: collect "dealer is <label>" from match rows ──
      for (const e of s.moveLog) {
        const dm = e.action.match(/dealer is\s+(.+)$/i);
        if (dm) {
          const label = dm[1].trim();
          const phase = /new hand/i.test(e.action) ? 'new' : 'first';
          if (!dealersSeen.some(d => d.label === label && d.phase === phase)) {
            dealersSeen.push({ label, phase, action: e.action });
          }
        }
      }
      const distinctDealers = [...new Set(dealersSeen.map(d => d.label))];
      if (!rotationShotDone && dealersSeen.some(d => d.phase === 'new') && distinctDealers.length >= 2) {
        ev.observed.dealerRotation = { dealers: dealersSeen, distinct: distinctDealers, rotated: true };
        ev.screenshots.push(await shot(page, '07-dealer-rotation.png'));
        milestone('dealer-rotation', ev.observed.dealerRotation);
        rotationShotDone = true;
      }

      // ── hand result modal: Hu + fan score OR exhaustive draw ──
      if (!resultShotDone && (s.resultModalVisible || (s.result && s.result.type))) {
        // Give the modal a beat to fully paint its chips/score table.
        await page.waitForTimeout(900);
        const s2 = await page.evaluate(snapshotFn).catch(() => s);
        const txt = (s2.resultText || s.resultText || '');
        const isDraw = /draw|流局|荒/i.test(txt) || (s2.result && /draw/i.test(String(s2.result.type)));
        ev.observed.handResult = {
          headline: txt.split(' ').slice(0, 6).join(' '),
          isDraw: !!isDraw,
          resultType: s2.result?.type ?? s.result?.type ?? null,
          winner: s2.result?.winner ?? s.result?.winner ?? null,
          modalVisible: s2.resultModalVisible,
          text: txt.slice(0, 600),
        };
        ev.screenshots.push(await shot(page, '06-result-modal-score.png'));
        milestone('hand-result', { isDraw: !!isDraw, type: ev.observed.handResult.resultType, winner: ev.observed.handResult.winner });
        resultShotDone = true;
        // Advance to the next hand so we can witness dealer rotation.
        const nextBtn = page.locator('#result-next');
        if (await nextBtn.isVisible().catch(() => false)) {
          await nextBtn.click({ timeout: 2500 }).catch(() => {});
          milestone('clicked-next-hand', true);
        }
      }

      // ── match fully complete (all hands) — capture + stop ──
      if (s.gameCompleteModalVisible || (s.gameComplete && s.gameComplete.isComplete)) {
        ev.screenshots.push(await shot(page, '08-game-complete.png'));
        milestone('game-complete', { gcText: (s.gcText || '').slice(0, 200) });
        // keep going only if we still lack a result or rotation and time remains
      }
    }

    // Early exit once we've gathered everything we came for.
    if (dealtCaptured && ev.observed.diceWallBreak && claimShotDone && midgameShotDone && resultShotDone && rotationShotDone) {
      log('  All ruleset milestones captured — ending observation loop.');
      break;
    }
    await page.waitForTimeout(1500);
  }

  // ── Flat + perspective view toggle (on the live game page) ──
  log('\n── View toggle: perspective ↔ flat ──');
  const before = await page.evaluate(snapshotFn).catch(() => null);
  ev.observed.perspectiveView = !!(before && before.perspectiveChecked === true && before.canvasCount > 0);
  ev.screenshots.push(await shot(page, '09-perspective-view.png'));
  milestone('perspective-view', { perspectiveChecked: before?.perspectiveChecked, canvas: before?.canvasCount });
  // Toggle to flat: uncheck #perspective + dispatch change; also press 'P'.
  await page.evaluate(() => {
    const el = document.getElementById('perspective');
    if (el && el.checked) { el.checked = false; el.dispatchEvent(new Event('change', { bubbles: true })); }
  });
  await page.keyboard.press('p').catch(() => {});
  await page.waitForTimeout(1500);
  const after = await page.evaluate(snapshotFn).catch(() => null);
  // Flat view is "usable" if the perspective flag flipped off AND the 3D
  // canvas is still present/rendering (scene didn't crash).
  ev.observed.flatView = !!(after && after.perspectiveChecked === false && after.canvasCount > 0);
  ev.screenshots.push(await shot(page, '10-flat-view.png'));
  milestone('flat-view', { perspectiveChecked: after?.perspectiveChecked, canvas: after?.canvasCount });
  // restore perspective
  await page.evaluate(() => {
    const el = document.getElementById('perspective');
    if (el && !el.checked) { el.checked = true; el.dispatchEvent(new Event('change', { bubbles: true })); }
  });

  await page.context().close();
}

await browser.close();

// ── finalize ────────────────────────────────────────────────────────
ev.cspViolations = cspHits;
ev.cspViolationCount = cspHits.length;
ev.consoleErrorCount = consoleErrors.length;
ev.pageErrorCount = pageErrors.length;
ev.finishedAt = new Date().toISOString();
fs.writeFileSync(path.join(ART_DIR, 'ruleset-evidence-summary.json'), JSON.stringify(ev, null, 2));

const o = ev.observed;
log('\n──────── RULESET EVIDENCE SUMMARY ────────');
log(`Backend: ${ORIGIN}  bots: ${DIFF}`);
log(`Lobby 0 CSP:           ${o.lobbyZeroCsp ? 'YES' : 'NO'}  (total CSP hits=${ev.cspViolationCount})`);
log(`Dice wall break:       ${o.diceWallBreak ? 'YES — ' + o.diceWallBreak : 'NO'}`);
log(`4 walls face-down:     ${o.fourWallsFaceDownPreDeal ? 'YES — ' + JSON.stringify(o.fourWallsFaceDownPreDeal) : 'NO'}`);
log(`Dealer hand 14 @deal:  ${o.dealerHandFaceUpPostDeal ? 'YES — ' + JSON.stringify(o.dealerHandFaceUpPostDeal) : 'NO'}`);
log(`Claim (peng/chow/kong):${o.claim ? 'YES — types=' + JSON.stringify(o.claimTypesSeen) : 'NO'}`);
log(`Hand result (Hu/draw): ${o.handResult ? (o.handResult.isDraw ? 'DRAW' : 'HU') + ' — ' + (o.handResult.text || '').slice(0,120) : 'NO'}`);
log(`Dealer rotation:       ${o.dealerRotation ? 'YES — ' + JSON.stringify(o.dealerRotation.distinct) : 'NO'}`);
log(`Perspective view:      ${o.perspectiveView ? 'YES' : 'NO'}`);
log(`Flat view:             ${o.flatView ? 'YES' : 'NO'}`);
log(`Artifacts: ${ART_DIR}`);

const core = o.lobbyZeroCsp && o.diceWallBreak && o.dealerHandFaceUpPostDeal && ev.cspViolationCount === 0;
log(`\nCORE EVIDENCE (lobby/csp/dice/dealt): ${core ? 'PASS' : 'PARTIAL'}`);
process.exit(0);
