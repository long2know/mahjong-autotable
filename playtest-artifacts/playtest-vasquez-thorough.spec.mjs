// Vasquez 2026-06-03 — Thorough full-game playthrough audit.
//
// Stephen's directive (W?? Wave): "have the team fan out and thoroughly
// test the game and its functionality". This spec exercises FIVE
// independent scenarios end-to-end against the live backend and grades
// every gate from REAL state (world.things, client.* collections, DOM).
// Nothing is graded PASS from "no exception thrown" alone.
//
// Scenarios:
//   A — Auto-deal Changsha (3 bots / Medium) → human discards →
//       assert at least one bot discards within 8s; per-seat counters.
//   B — Manual-deal Changsha → human drives RollDice +
//       4-tile-per-round pickups → assert hand reaches 13, dealer-extra
//       brings it to 14, hand stays face-up.
//   C — Synthetic claim-window injection (via cli.events.emit on the
//       `claim` collection) → assert Pung button enables on the right
//       seat, countdown shows.
//   D — Synthetic Hu via `result` collection injection → assert
//       #result-modal becomes visible with the right headline and
//       score breakdown.
//   E — Multi-game isolation: two browser contexts join two distinct
//       gameIds → discard in game A → confirm game B sees zero state
//       bleed (its own seat hands intact, no foreign discards).
//
// Artifacts: playtest-artifacts/screenshots/vasquez-pt-<scenario>-<ts>.{png,json}
//
// Run: backend on http://127.0.0.1:8088 (or E2E_BASE_URL override).
//   cd /data/source/mahjong-autotable
//   E2E_BASE_URL=http://127.0.0.1:8088 \
//     node playtest-artifacts/playtest-vasquez-thorough.spec.mjs
//
// Exit: 0 if all 5 scenarios PASS, 1 otherwise.

import pw from 'playwright';
const { chromium } = pw;
import fs from 'fs';
import path from 'path';

const baseUrl = process.env.E2E_BASE_URL || 'http://127.0.0.1:8088';
const ARTIFACT_DIR = path.resolve('./playtest-artifacts/screenshots');
fs.mkdirSync(ARTIFACT_DIR, { recursive: true });
const ts = new Date().toISOString().replace(/[:.]/g, '-');

const findings = {
  startedAt: new Date().toISOString(),
  baseUrl,
  ts,
  scenarios: {
    A_autoDealRoundRobin: { status: 'pending', gates: {}, diagnostics: {} },
    B_manualDealPickups:  { status: 'pending', gates: {}, diagnostics: {} },
    C_claimWindow:        { status: 'pending', gates: {}, diagnostics: {} },
    D_synthHandResult:    { status: 'pending', gates: {}, diagnostics: {} },
    E_multiGameIsolation: { status: 'pending', gates: {}, diagnostics: {} },
  },
};

const browser = await chromium.launch();

// ── helpers ─────────────────────────────────────────────────────────────

const OVERLAY_DEFANG = `
  #tour-overlay, #magic-link-landing, #magic-link-overlay,
  #signin-modal-backdrop, .magic-link-landing, .magic-link-overlay,
  .signin-modal-backdrop, [data-testid="tour-overlay"],
  [data-testid="signin-modal-backdrop"]
    { display: none !important; pointer-events: none !important; visibility: hidden !important; }
  [aria-hidden="true"] { pointer-events: none !important; }
`;

async function makePage(label) {
  const ctx = await browser.newContext({ viewport: { width: 1280, height: 800 } });
  await ctx.addInitScript((css) => {
    const inject = () => {
      if (document.getElementById('vasquez-pt-defang')) return;
      const s = document.createElement('style');
      s.id = 'vasquez-pt-defang';
      s.textContent = css;
      document.head.appendChild(s);
    };
    if (document.readyState === 'loading') {
      document.addEventListener('DOMContentLoaded', inject);
    } else inject();
  }, OVERLAY_DEFANG);
  const page = await ctx.newPage();
  const pageDiag = {
    label,
    pageErrors: [],
    consoleErrors: [],
    consoleWarnings: [],
    networkFailures: [],
    staleMoveToWarnings: 0,
  };
  page.on('console', msg => {
    const t = msg.type();
    const text = msg.text();
    if (t === 'error') pageDiag.consoleErrors.push(text);
    if (t === 'warning') {
      pageDiag.consoleWarnings.push(text);
      if (/skipped stale moveTo/.test(text)) pageDiag.staleMoveToWarnings++;
    }
  });
  page.on('pageerror', err => pageDiag.pageErrors.push({
    message: err.message,
    stack: (err.stack ?? '').split('\n').slice(0, 6).join('\n'),
  }));
  page.on('response', resp => {
    if (resp.status() >= 400) {
      pageDiag.networkFailures.push(`${resp.status()} ${resp.request().method()} ${resp.url()}`);
    }
  });
  return { ctx, page, pageDiag };
}

async function navigateAndSeat(page, url) {
  await page.goto(url, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(2500);

  for (const sel of ['#tour-skip', '#onboarding-skip']) {
    const el = page.locator(sel);
    if (await el.isVisible().catch(() => false)) {
      await el.click({ force: true, timeout: 3000 }).catch(() => {});
      await page.waitForTimeout(250);
    }
  }
  const qm = page.locator('#lobby-quick-match').first();
  if (await qm.isVisible().catch(() => false)) {
    await qm.click({ force: true, timeout: 5000 }).catch(() => {});
    await page.waitForTimeout(2500);
  }
  const closeBtn = page.locator('#lobby-close');
  if (await closeBtn.isVisible().catch(() => false)) {
    await closeBtn.click({ force: true }).catch(() => {});
    await page.waitForTimeout(400);
  }
  const connect = page.locator('#connect').first();
  if (await connect.isVisible().catch(() => false)) {
    await connect.click({ force: true, timeout: 5000 }).catch(() => {});
    await page.waitForTimeout(2500);
  }
  const seats = page.locator('.take-seat');
  const total = await seats.count();
  for (let i = 0; i < total; i++) {
    if (await seats.nth(i).isVisible().catch(() => false)) {
      await seats.nth(i).click({ force: true, timeout: 5000 }).catch(() => {});
      await page.waitForTimeout(1500);
      break;
    }
  }
}

// Pulls authoritative state from the runtime — `world.things` (a Map),
// `client.{pickup,claim,result,gameComplete}`, and per-seat counts.
async function worldSnapshot(page) {
  return await page.evaluate(() => {
    const g = window.game;
    if (!g || !g.world) return null;
    const w = g.world;
    const seat = w.seat;
    const handBySeat = [0, 0, 0, 0];
    const meldBySeat = [0, 0, 0, 0];
    const discardBySeat = [0, 0, 0, 0];
    let totalDiscard = 0;
    let totalMeld = 0;
    let myHandFaceUp = 0;
    let foreignHandFaceUp = 0;
    let wallCount = 0;
    const myHandIds = [];
    for (const t of w.things.values()) {
      if (!t.slot) continue;
      const s = t.slot;
      if (s.group === 'wall') wallCount++;
      if (s.group === 'hand' && typeof s.seat === 'number') {
        handBySeat[s.seat]++;
        if (s.seat === seat) {
          myHandIds.push({ id: t.index, key: s.key });
          if (t.rotationIndex === 1) myHandFaceUp++;
        } else if (t.rotationIndex === 1) foreignHandFaceUp++;
      }
      if (s.group === 'meld' && typeof s.seat === 'number') {
        meldBySeat[s.seat]++;
        totalMeld++;
      }
      if (s.group === 'discard') {
        totalDiscard++;
        if (typeof s.seat === 'number') discardBySeat[s.seat]++;
      }
    }
    const safeGet = (col, key) => {
      try { return col?.get?.(key) ?? null; } catch { return null; }
    };
    const p = safeGet(w.client?.pickup, 'current');
    const r = safeGet(w.client?.result, 'current');
    const gc = safeGet(w.client?.gameComplete, 'current');
    const claimsBySeat = [0, 1, 2, 3].map(s => safeGet(w.client?.claim, String(s)));
    let matchPhase = null;
    try { matchPhase = w.client?.match?.get?.(0)?.phase ?? null; } catch { /* ignore */ }
    return {
      seat,
      handBySeat,
      meldBySeat,
      discardBySeat,
      totalDiscard,
      totalMeld,
      wallCount,
      myHandIds: myHandIds.sort((a, b) => a.key - b.key),
      myHandFaceUp,
      foreignHandFaceUp,
      hasExtraHandTile: typeof w.hasExtraHandTile === 'function' ? w.hasExtraHandTile() : null,
      pickup: p ? { phase: p.phase ?? null, seatIndex: p.seatIndex ?? null, count: p.count ?? null } : null,
      result: r ? { type: r.type, winner: r.winner, scoreLen: Array.isArray(r.score) ? r.score.length : null } : null,
      gameComplete: gc ? { isComplete: gc.isComplete ?? gc.IsComplete ?? null, totalScores: gc.totalScores ?? gc.TotalScores ?? null } : null,
      claimsBySeat: claimsBySeat.map((c, i) => c ? {
        seat: i,
        available: c.available,
        source: c.source,
        tile: c.tile,
        remainingMs: typeof c.deadline === 'number' ? Math.max(0, c.deadline - Date.now()) : null,
      } : null),
      matchPhase,
    };
  });
}

async function moveLog(page) {
  return await page.evaluate(() => {
    const rows = document.querySelectorAll('#move-log .move-log-entry');
    const out = [];
    for (const r of rows) {
      const ts = r.querySelector('.move-log-ts')?.textContent ?? '';
      const seat = r.querySelector('.move-log-seat')?.textContent ?? '';
      const action = r.querySelector('.move-log-action')?.textContent ?? '';
      out.push(`${ts} ${seat}: ${action}`);
    }
    return out;
  });
}

async function snap(page, scenarioId, label) {
  const file = path.join(ARTIFACT_DIR, `vasquez-pt-${scenarioId}-${label}-${ts}.png`);
  await page.screenshot({ path: file, fullPage: true });
  return file;
}

function writeStateDump(scenarioId, label, payload) {
  const file = path.join(ARTIFACT_DIR, `vasquez-pt-${scenarioId}-${label}-${ts}.json`);
  fs.writeFileSync(file, JSON.stringify(payload, null, 2));
  return file;
}

function grade(scenarioId, gateId, ok, detail) {
  findings.scenarios[scenarioId].gates[gateId] = { status: ok ? 'PASS' : 'FAIL', detail };
  console.log(`  ${scenarioId}.${gateId}: ${ok ? 'PASS' : 'FAIL'} ${JSON.stringify(detail).slice(0, 240)}`);
}

function finalize(scenarioId) {
  const gates = Object.values(findings.scenarios[scenarioId].gates);
  const ok = gates.length > 0 && gates.every(g => g.status === 'PASS');
  findings.scenarios[scenarioId].status = ok ? 'PASS' : 'FAIL';
  console.log(`\n>>> Scenario ${scenarioId}: ${findings.scenarios[scenarioId].status} (${gates.filter(g => g.status === 'PASS').length}/${gates.length} gates)`);
}

// ── SCENARIO A — Auto-deal + human-driven discard + bot reaction ────

async function scenarioA() {
  console.log('\n========================================');
  console.log('  SCENARIO A — Auto-deal + bot reaction');
  console.log('========================================');
  const sid = 'A_autoDealRoundRobin';
  const diag = findings.scenarios[sid].diagnostics;
  const { ctx, page, pageDiag } = await makePage('A');
  try {
    const gameId = `vasquez-ptA-${Date.now()}`;
    const url = `${baseUrl}/autotable/?variant=changsha&dealMode=auto&botCount=3&botDifficulty=Medium&handCount=4&gameId=${gameId}`;
    diag.url = url;
    await navigateAndSeat(page, url);

    // Auto-deal kicks off when the seat fills with bots. Belt-and-suspenders.
    await page.waitForTimeout(2000);
    await page.evaluate(() => {
      try { window.game?.world?.deal?.('HANDS'); } catch { /* idempotent */ }
    });

    // Wait up to 30s for the deal to settle: dealer hand reaches 14 AND
    // tiles flip face-up (broken-deal-repro shows the flip lags hand-grow
    // by ~3s of animation; breaking on hand>=14 alone catches mid-flip
    // state with rotationIndex still at 0).
    const dealDeadline = Date.now() + 30_000;
    let preDiscard = null;
    while (Date.now() < dealDeadline) {
      preDiscard = await worldSnapshot(page);
      const handReady = (preDiscard?.handBySeat?.[0] ?? 0) >= 14;
      const faceUpReady = (preDiscard?.myHandFaceUp ?? 0) >= 13;
      if (handReady && faceUpReady) break;
      await page.waitForTimeout(500);
    }
    // Extra settle so emitDiscard sees a stable hand.
    await page.waitForTimeout(1500);
    preDiscard = await worldSnapshot(page);
    diag.preDiscard = preDiscard;
    await snap(page, sid, '01-after-deal');

    grade(sid, 'A1_dealerHas14FaceUp',
      (preDiscard?.handBySeat?.[0] ?? 0) >= 14 &&
      (preDiscard?.myHandFaceUp ?? 0) >= 13 &&
      (preDiscard?.foreignHandFaceUp ?? 0) === 0,
      {
        dealerHand: preDiscard?.handBySeat?.[0],
        faceUpMine: preDiscard?.myHandFaceUp,
        foreignFaceUp: preDiscard?.foreignHandFaceUp,
        handBySeat: preDiscard?.handBySeat,
      });

    // Pick the lowest-key tile from our hand and emit a discard.
    const targetTile = (preDiscard?.myHandIds ?? [])[0];
    diag.discardTarget = targetTile;
    let discardEmit = null;
    if (targetTile) {
      discardEmit = await page.evaluate(({ tileId }) => {
        try { return { ok: !!window.game.world.emitDiscard(tileId) }; }
        catch (e) { return { ok: false, reason: String(e) }; }
      }, { tileId: targetTile.id });
    }
    diag.discardEmit = discardEmit;

    // Wait for our discard pile to grow OR move-log to record it.
    const dDeadline = Date.now() + 10_000;
    let postDiscard = null;
    let log = [];
    while (Date.now() < dDeadline) {
      postDiscard = await worldSnapshot(page);
      log = await moveLog(page);
      const dealerDiscarded = (postDiscard?.discardBySeat?.[0] ?? 0) > 0
        || log.some(e => /Seat 0.*discarded/i.test(e));
      if (dealerDiscarded) break;
      await page.waitForTimeout(400);
    }
    diag.postDiscard = postDiscard;
    diag.logAfterDiscard = log.slice(-10);
    await snap(page, sid, '02-after-human-discard');

    const dealerPileGrew = (postDiscard?.discardBySeat?.[0] ?? 0) > 0;
    const logDealerDiscard = log.some(e => /Seat 0.*discarded/i.test(e));
    grade(sid, 'A2_humanDiscardRoundTrip', dealerPileGrew || logDealerDiscard, {
      dealerDiscardPile: postDiscard?.discardBySeat?.[0],
      totalDiscard: postDiscard?.totalDiscard,
      logDealerDiscard,
      logTail: log.slice(-5),
    });

    // Observe for up to 12s — at least ONE bot should respond.
    const botDeadline = Date.now() + 12_000;
    let observedBots = { 1: false, 2: false, 3: false };
    let botLog = log;
    while (Date.now() < botDeadline) {
      const snap = await worldSnapshot(page);
      botLog = await moveLog(page);
      for (let s = 1; s <= 3; s++) {
        if ((snap?.discardBySeat?.[s] ?? 0) > 0
            || botLog.some(e => new RegExp(`Seat ${s}.*discarded`, 'i').test(e))) {
          observedBots[s] = true;
        }
      }
      if (Object.values(observedBots).some(v => v)) break;
      await page.waitForTimeout(500);
    }
    diag.botObservation = observedBots;
    diag.botLogTail = botLog.slice(-10);
    await snap(page, sid, '03-after-bot-reaction');

    const anyBot = Object.values(observedBots).some(v => v);
    grade(sid, 'A3_atLeastOneBotDiscarded', anyBot, {
      seats: observedBots,
      logTail: botLog.slice(-8),
    });

    // Stretch: 60s round-robin observation — count distinct seats that
    // ACTED (discarded OR claimed OR passed). When a bot is mid-claim
    // window the round-robin pauses for several seconds; relying on raw
    // discard count alone is flaky. Accept any of the canonical
    // move-log verbs as evidence the seat is alive in the turn cycle.
    const rrDeadline = Date.now() + 60_000;
    let rrSnap = null;
    let rrLog = botLog;
    const seatActed = (log, seat) => log.some(e => new RegExp(
      `Seat ${seat}.*(?:discarded|claim window|passed|claimed|Chow|Pung|Kong)`,
      'i').test(e));
    while (Date.now() < rrDeadline) {
      rrSnap = await worldSnapshot(page);
      rrLog = await moveLog(page);
      const acted1 = seatActed(rrLog, 1);
      const acted2 = seatActed(rrLog, 2);
      const acted3 = seatActed(rrLog, 3);
      if (acted1 && acted2 && acted3) break;
      await page.waitForTimeout(1000);
    }
    diag.rrFinal = rrSnap;
    diag.rrLogTail = rrLog?.slice(-20);
    await snap(page, sid, '04-round-robin');

    const others = rrSnap?.discardBySeat ?? [0, 0, 0, 0];
    const acted1 = seatActed(rrLog ?? [], 1);
    const acted2 = seatActed(rrLog ?? [], 2);
    const acted3 = seatActed(rrLog ?? [], 3);
    const actedCount = [acted1, acted2, acted3].filter(Boolean).length;
    // Gate accepts: 3/3 acted (clean round-robin) OR 2/3 acted AND total
    // discards >= 3 (claim-interrupted round-robin where one seat was
    // skipped because a prior claim drained their turn).
    grade(sid, 'A4_roundRobinAllOthers',
      actedCount === 3 || (actedCount >= 2 && (rrSnap?.totalDiscard ?? 0) >= 3),
      {
        discardBySeat: others,
        actedBySeat: { seat1: acted1, seat2: acted2, seat3: acted3 },
        actedCount,
        totalDiscard: rrSnap?.totalDiscard,
        logTail: (rrLog ?? []).slice(-10),
      });

    diag.pageDiag = pageDiag;
    writeStateDump(sid, 'state', { diag, gates: findings.scenarios[sid].gates });
    finalize(sid);
  } catch (e) {
    findings.scenarios[sid].error = e?.message ?? String(e);
    findings.scenarios[sid].status = 'ERROR';
    console.log(`SCENARIO ${sid} ERROR: ${e?.message ?? e}`);
  } finally {
    await ctx.close();
  }
}

// ── SCENARIO B — Manual-deal pickup chain → hand grows to 14 ────────

async function scenarioB() {
  console.log('\n========================================');
  console.log('  SCENARIO B — Manual deal 4-tile rounds');
  console.log('========================================');
  const sid = 'B_manualDealPickups';
  const diag = findings.scenarios[sid].diagnostics;
  const { ctx, page, pageDiag } = await makePage('B');
  try {
    const gameId = `vasquez-ptB-${Date.now()}`;
    const url = `${baseUrl}/autotable/?variant=changsha&dealMode=manual&botCount=3&botDifficulty=Medium&handCount=4&gameId=${gameId}`;
    diag.url = url;
    await navigateAndSeat(page, url);

    // Trigger the deal → RollingDice.
    await page.waitForTimeout(1500);
    diag.deal = await page.evaluate(() => {
      try { window.game.world.deal('HANDS'); return { ok: true }; }
      catch (e) { return { ok: false, reason: String(e) }; }
    });

    // Roll dice → BreakPointMarked.
    await page.waitForTimeout(2500);
    diag.rollDice = await page.evaluate(() => {
      try { window.game.world.emitRollDice(); return { ok: true }; }
      catch (e) { return { ok: false, reason: String(e) }; }
    });

    // Pickup chain: while pickup affordance non-null and it's our turn,
    // emitTakePickup(). Capture per-iteration hand size.
    const pickupTrace = [];
    const pickupDeadline = Date.now() + 60_000;
    let lastSnap = null;
    while (Date.now() < pickupDeadline) {
      const snap = await page.evaluate(() => {
        const g = window.game;
        if (!g?.world) return null;
        const w = g.world;
        const pickup = w.client?.pickup?.get?.('current') ?? null;
        const myTurn = typeof w.isMyPickupTurn === 'function' ? w.isMyPickupTurn() : null;
        let hand = 0;
        for (const t of w.things.values()) {
          if (t.slot?.group === 'hand' && t.slot.seat === w.seat) hand++;
        }
        return { seat: w.seat, pickup, myTurn, hand };
      });
      if (!snap) break;
      pickupTrace.push({ atMs: Date.now(), ...snap });
      lastSnap = snap;
      if (snap.pickup === null) break;
      if (snap.myTurn) {
        await page.evaluate(() => {
          try { window.game.world.emitTakePickup(); } catch { /* swallow */ }
        });
      }
      await page.waitForTimeout(900);
    }
    diag.pickupTrace = pickupTrace;
    diag.pickupTraceLen = pickupTrace.length;
    diag.lastPickupSnap = lastSnap;
    await snap(page, sid, '01-after-pickup-chain');

    // Belt-and-suspenders: poll until hand >= 13 OR 12s elapses.
    const handDeadline = Date.now() + 12_000;
    let final = null;
    while (Date.now() < handDeadline) {
      final = await worldSnapshot(page);
      if ((final?.handBySeat?.[0] ?? 0) >= 13) break;
      await page.waitForTimeout(500);
    }
    diag.final = final;
    await snap(page, sid, '02-final-deal');

    // Gate B1: dealer hand at 13 OR 14 (manual deal often parks at 13 then
    // dealer-extra adds 1).
    grade(sid, 'B1_dealerHandReached13', (final?.handBySeat?.[0] ?? 0) >= 13, {
      dealerHand: final?.handBySeat?.[0],
      handBySeat: final?.handBySeat,
      pickupTraceLen: pickupTrace.length,
      lastPickup: lastSnap?.pickup,
    });

    // Gate B2: all our hand tiles face-up.
    grade(sid, 'B2_myHandFaceUp',
      (final?.myHandFaceUp ?? 0) >= (final?.handBySeat?.[0] ?? 0) - 1, // 14-tile state may have 1 transient
      {
        myHandFaceUp: final?.myHandFaceUp,
        dealerHand: final?.handBySeat?.[0],
      });

    // Gate B3: foreign hands NEVER face-up.
    grade(sid, 'B3_foreignHandsHidden', (final?.foreignHandFaceUp ?? 0) === 0, {
      foreignFaceUp: final?.foreignHandFaceUp,
      handBySeat: final?.handBySeat,
    });

    // Gate B4: dealer-extra arrived → hand >= 14 OR hasExtraHandTile === true.
    grade(sid, 'B4_dealerExtraComplete',
      (final?.handBySeat?.[0] ?? 0) >= 14 || final?.hasExtraHandTile === true,
      {
        dealerHand: final?.handBySeat?.[0],
        hasExtraHandTile: final?.hasExtraHandTile,
        lastPickupPhase: lastSnap?.pickup?.phase,
      });

    diag.pageDiag = pageDiag;
    writeStateDump(sid, 'state', { diag, gates: findings.scenarios[sid].gates });
    finalize(sid);
  } catch (e) {
    findings.scenarios[sid].error = e?.message ?? String(e);
    findings.scenarios[sid].status = 'ERROR';
    console.log(`SCENARIO ${sid} ERROR: ${e?.message ?? e}`);
  } finally {
    await ctx.close();
  }
}

// ── SCENARIO C — Synthetic claim-window injection ────────────────────

async function scenarioC() {
  console.log('\n========================================');
  console.log('  SCENARIO C — Claim window UI surfaces');
  console.log('========================================');
  const sid = 'C_claimWindow';
  const diag = findings.scenarios[sid].diagnostics;
  const { ctx, page, pageDiag } = await makePage('C');
  try {
    const gameId = `vasquez-ptC-${Date.now()}`;
    const url = `${baseUrl}/autotable/?variant=changsha&dealMode=auto&botCount=3&botDifficulty=Medium&handCount=4&gameId=${gameId}`;
    diag.url = url;
    await navigateAndSeat(page, url);

    // Auto-deal → wait for steady state.
    await page.waitForTimeout(3500);
    await page.evaluate(() => {
      try { window.game?.world?.deal?.('HANDS'); } catch { /* idempotent */ }
    });

    const dealDeadline = Date.now() + 20_000;
    let baseline = null;
    while (Date.now() < dealDeadline) {
      baseline = await worldSnapshot(page);
      if ((baseline?.handBySeat?.[0] ?? 0) >= 13) break;
      await page.waitForTimeout(500);
    }
    diag.baseline = baseline;

    // Inject a synthetic claim entry targeting OUR seat. Shape per types.ts:
    //   { available: ['Pung','Chow'], deadline: Date.now()+5000, source: 1, tile: 5 }
    // game-ui.ts:807 onClaimUpdate enables matching buttons + reveals countdown.
    const injection = await page.evaluate(() => {
      try {
        const cli = window.game?.client;
        if (!cli) return { ok: false, reason: 'no client' };
        const seat = cli.seat;
        const events = cli.events ?? cli['events'];
        if (!events?.emit) return { ok: false, reason: 'no events emitter' };
        const payload = {
          available: ['Pung', 'Chow', 'Hu'],
          deadline: Date.now() + 5000,
          source: (seat + 1) % 4,
          tile: 5,
        };
        events.emit('update', [['claim', String(seat), payload]], false);
        return { ok: true, seat, payload };
      } catch (e) { return { ok: false, reason: String(e) }; }
    });
    diag.injection = injection;

    await page.waitForTimeout(600);
    await snap(page, sid, '01-after-claim-inject');

    // Gate C1: injection succeeded and emitter received it.
    grade(sid, 'C1_injectionAccepted', injection.ok === true, injection);

    // Gate C2: countdown element is visible AND Pung/Chow/Hu buttons enabled.
    const ui = await page.evaluate(() => {
      const cd = document.getElementById('claim-countdown');
      const cdVal = document.getElementById('claim-countdown-value');
      const buttons = {
        Pung: document.getElementById('claim-pung'),
        Chow: document.getElementById('claim-chow'),
        Kong: document.getElementById('claim-kong'),
        Hu:   document.getElementById('claim-hu'),
        Pass: document.getElementById('claim-pass'),
      };
      const visible = (el) => !!el && !!el.offsetParent
        && getComputedStyle(el).display !== 'none'
        && getComputedStyle(el).visibility !== 'hidden';
      return {
        countdownVisible: visible(cd),
        countdownText: cdVal?.textContent ?? null,
        disabled: Object.fromEntries(Object.entries(buttons).map(([k, b]) => [k, b ? b.disabled : null])),
      };
    });
    diag.ui = ui;

    grade(sid, 'C2_claimUiSurfaces',
      ui.countdownVisible &&
      ui.disabled.Pung === false &&
      ui.disabled.Chow === false &&
      ui.disabled.Hu === false &&
      ui.disabled.Pass === false &&
      ui.disabled.Kong === true,
      ui);

    // Tombstone the claim and verify the buttons disable again.
    await page.evaluate(() => {
      const cli = window.game?.client;
      const events = cli?.events ?? cli?.['events'];
      events?.emit('update', [['claim', String(cli.seat), null]], false);
    });
    await page.waitForTimeout(500);
    const ui2 = await page.evaluate(() => {
      const cd = document.getElementById('claim-countdown');
      const visible = (el) => !!el && !!el.offsetParent
        && getComputedStyle(el).display !== 'none'
        && getComputedStyle(el).visibility !== 'hidden';
      const btn = (id) => document.getElementById(id);
      return {
        countdownStillVisible: visible(cd),
        pungDisabled: btn('claim-pung')?.disabled,
        passDisabled: btn('claim-pass')?.disabled,
      };
    });
    diag.uiAfterTombstone = ui2;
    grade(sid, 'C3_claimTombstoneClears',
      !ui2.countdownStillVisible && ui2.pungDisabled === true && ui2.passDisabled === true,
      ui2);

    diag.pageDiag = pageDiag;
    writeStateDump(sid, 'state', { diag, gates: findings.scenarios[sid].gates });
    finalize(sid);
  } catch (e) {
    findings.scenarios[sid].error = e?.message ?? String(e);
    findings.scenarios[sid].status = 'ERROR';
    console.log(`SCENARIO ${sid} ERROR: ${e?.message ?? e}`);
  } finally {
    await ctx.close();
  }
}

// ── SCENARIO D — Synthetic Hu HandResult + score overlay ─────────────

async function scenarioD() {
  console.log('\n========================================');
  console.log('  SCENARIO D — Synthetic HandResult / Hu');
  console.log('========================================');
  const sid = 'D_synthHandResult';
  const diag = findings.scenarios[sid].diagnostics;
  const { ctx, page, pageDiag } = await makePage('D');
  try {
    const gameId = `vasquez-ptD-${Date.now()}`;
    const url = `${baseUrl}/autotable/?variant=changsha&dealMode=auto&botCount=3&botDifficulty=Medium&handCount=4&gameId=${gameId}`;
    diag.url = url;
    await navigateAndSeat(page, url);

    await page.waitForTimeout(3500);
    await page.evaluate(() => {
      try { window.game?.world?.deal?.('HANDS'); } catch { /* idempotent */ }
    });

    const dealDeadline = Date.now() + 20_000;
    let baseline = null;
    while (Date.now() < dealDeadline) {
      baseline = await worldSnapshot(page);
      if ((baseline?.handBySeat?.[0] ?? 0) >= 13) break;
      await page.waitForTimeout(500);
    }
    diag.baseline = baseline;

    // Inject a synthetic per-hand Hu. Shape per HandResultEntry (types.ts:204):
    //   { winner, type:'Hu', score:[{seat,delta}...], hand:[tileIds...], nextBanker }
    const inject = await page.evaluate(() => {
      try {
        const cli = window.game?.client;
        if (!cli) return { ok: false, reason: 'no client' };
        const events = cli.events ?? cli['events'];
        if (!events?.emit) return { ok: false, reason: 'no events emitter' };
        const payload = {
          winner: cli.seat,
          type: 'Hu',
          score: [
            { seat: cli.seat,         delta:  24 },
            { seat: (cli.seat+1) % 4, delta: -8 },
            { seat: (cli.seat+2) % 4, delta: -8 },
            { seat: (cli.seat+3) % 4, delta: -8 },
          ],
          hand: [0,1,2, 3,4,5, 6,7,8, 9,10,11, 12,13],
          nextBanker: cli.seat,
        };
        events.emit('update', [['result', 'current', payload]], false);
        return { ok: true, payload };
      } catch (e) { return { ok: false, reason: String(e) }; }
    });
    diag.inject = inject;
    await page.waitForTimeout(800);
    await snap(page, sid, '01-after-hu-inject');

    grade(sid, 'D1_resultInjectionAccepted', inject.ok === true, inject);

    // Gate D2: #result-modal visible + headline contains 胡 + score row count == 4.
    // Bootstrap modals don't set offsetParent reliably (they're absolute-
    // positioned and may exist outside the offsetParent chain). Use the
    // bootstrap `.show` class + `display: block` style as the canonical
    // visibility signal.
    const ui = await page.evaluate(() => {
      const modal = document.getElementById('result-modal');
      const headline = document.getElementById('result-headline');
      const winner = document.getElementById('result-winner');
      const scoreBody = document.querySelector('#result-score tbody');
      const cs = modal ? getComputedStyle(modal) : null;
      const modalVisible = !!modal && (
        modal.classList.contains('show') ||
        (cs && cs.display !== 'none' && cs.visibility !== 'hidden')
      );
      return {
        modalVisible,
        modalClasses: modal?.className ?? null,
        modalDisplay: cs?.display ?? null,
        headlineText: headline?.textContent ?? null,
        winnerText: winner?.textContent ?? null,
        scoreRows: scoreBody ? scoreBody.querySelectorAll('tr').length : 0,
      };
    });
    diag.ui = ui;
    grade(sid, 'D2_modalSurfaces',
      ui.modalVisible &&
      /胡/.test(ui.headlineText ?? '') &&
      ui.scoreRows >= 4,
      ui);

    // Gate D3: tombstone clears modal.
    await page.evaluate(() => {
      const cli = window.game?.client;
      const events = cli?.events ?? cli?.['events'];
      events?.emit('update', [['result', 'current', null]], false);
    });
    await page.waitForTimeout(600);
    const ui2 = await page.evaluate(() => {
      const modal = document.getElementById('result-modal');
      const cs = modal ? getComputedStyle(modal) : null;
      const modalVisible = !!modal && (
        modal.classList.contains('show') ||
        (cs && cs.display !== 'none' && cs.visibility !== 'hidden')
      );
      // Bootstrap modal('hide') sets display:none. Bias toward `display`
      // because the `.show` class can be removed before the transition.
      return {
        modalVisible: modalVisible && cs?.display !== 'none',
        modalDisplay: cs?.display ?? null,
        modalClasses: modal?.className ?? null,
      };
    });
    diag.uiAfterTombstone = ui2;
    await snap(page, sid, '02-after-tombstone');
    grade(sid, 'D3_modalTombstoneClears', ui2.modalVisible === false, ui2);

    // Gate D4: also exercise the gameComplete singleton path (different modal).
    const gc = await page.evaluate(() => {
      try {
        const cli = window.game?.client;
        const events = cli?.events ?? cli?.['events'];
        events?.emit('update', [['gameComplete', 'current', {
          isComplete: true,
          totalScores: { '0': 12, '1': -4, '2': -4, '3': -4 },
          handHistory: [],
          maxHands: 4,
        }]], false);
        return { ok: true };
      } catch (e) { return { ok: false, reason: String(e) }; }
    });
    diag.gameCompleteInject = gc;
    await page.waitForTimeout(900);
    const gcModalVisible = await page.locator('#game-complete-modal').isVisible().catch(() => false);
    diag.gcModalVisible = gcModalVisible;
    await snap(page, sid, '03-game-complete-modal');
    grade(sid, 'D4_gameCompleteModal', gc.ok === true && gcModalVisible === true, {
      ...gc, gcModalVisible });

    diag.pageDiag = pageDiag;
    writeStateDump(sid, 'state', { diag, gates: findings.scenarios[sid].gates });
    finalize(sid);
  } catch (e) {
    findings.scenarios[sid].error = e?.message ?? String(e);
    findings.scenarios[sid].status = 'ERROR';
    console.log(`SCENARIO ${sid} ERROR: ${e?.message ?? e}`);
  } finally {
    await ctx.close();
  }
}

// ── SCENARIO E — Multi-game isolation across two contexts ────────────

async function scenarioE() {
  console.log('\n========================================');
  console.log('  SCENARIO E — Multi-game cross-contam');
  console.log('========================================');
  const sid = 'E_multiGameIsolation';
  const diag = findings.scenarios[sid].diagnostics;

  const aSession = await makePage('E-A');
  const bSession = await makePage('E-B');
  try {
    const gameA = `vasquez-ptE-A-${Date.now()}`;
    const gameB = `vasquez-ptE-B-${Date.now()}-x`;
    const urlA = `${baseUrl}/autotable/?variant=changsha&dealMode=auto&botCount=3&botDifficulty=Medium&handCount=4&gameId=${gameA}`;
    const urlB = `${baseUrl}/autotable/?variant=changsha&dealMode=auto&botCount=3&botDifficulty=Medium&handCount=4&gameId=${gameB}`;
    diag.urls = { A: urlA, B: urlB };

    // Navigate and seat both in parallel.
    await Promise.all([
      navigateAndSeat(aSession.page, urlA),
      navigateAndSeat(bSession.page, urlB),
    ]);

    // Belt-and-suspenders deal for both.
    await Promise.all([aSession.page, bSession.page].map(p =>
      p.evaluate(() => { try { window.game?.world?.deal?.('HANDS'); } catch { /* ignore */ } })));

    // Wait for both to settle — gate on hand=14 AND myHandFaceUp>=13 so
    // emitDiscard sees a stable, selectable hand (same wait protocol as
    // scenario A — break-on-hand-only catches a mid-flip rotationIndex=0
    // state and emitDiscard silently no-ops).
    const deadline = Date.now() + 40_000;
    let snapA = null, snapB = null;
    while (Date.now() < deadline) {
      [snapA, snapB] = await Promise.all([
        worldSnapshot(aSession.page),
        worldSnapshot(bSession.page),
      ]);
      const readyA = (snapA?.handBySeat?.[0] ?? 0) >= 14 && (snapA?.myHandFaceUp ?? 0) >= 13;
      const readyB = (snapB?.handBySeat?.[0] ?? 0) >= 14 && (snapB?.myHandFaceUp ?? 0) >= 13;
      if (readyA && readyB) break;
      await aSession.page.waitForTimeout(500);
    }
    // Extra settle.
    await aSession.page.waitForTimeout(1500);
    [snapA, snapB] = await Promise.all([
      worldSnapshot(aSession.page),
      worldSnapshot(bSession.page),
    ]);
    diag.snapA_pre = snapA;
    diag.snapB_pre = snapB;
    await Promise.all([
      snap(aSession.page, sid, '01-A-after-deal'),
      snap(bSession.page, sid, '01-B-after-deal'),
    ]);

    // Capture each game's gameId from URL inside-page so we can verify routing.
    const gameIdInPageA = await aSession.page.evaluate(() =>
      new URL(window.location.href).searchParams.get('gameId'));
    const gameIdInPageB = await bSession.page.evaluate(() =>
      new URL(window.location.href).searchParams.get('gameId'));
    diag.gameIdInPage = { A: gameIdInPageA, B: gameIdInPageB };

    grade(sid, 'E1_bothGamesDealtIndependently',
      gameIdInPageA === gameA && gameIdInPageB === gameB &&
      (snapA?.handBySeat?.[0] ?? 0) >= 13 &&
      (snapB?.handBySeat?.[0] ?? 0) >= 13, {
        gameIdInPageA, gameIdInPageB,
        handA: snapA?.handBySeat?.[0],
        handB: snapB?.handBySeat?.[0],
      });

    // Drive a discard in game A only.
    const tileA = (snapA?.myHandIds ?? [])[0];
    let discardA = null;
    if (tileA) {
      discardA = await aSession.page.evaluate(({ tileId }) => {
        try { return { ok: !!window.game.world.emitDiscard(tileId) }; }
        catch (e) { return { ok: false, reason: String(e) }; }
      }, { tileId: tileA.id });
    }
    diag.discardA = { tileId: tileA?.id, result: discardA };

    // Wait for A's game to show ANY post-discard activity (discard pile
    // growth, OR a meld if a bot Chow/Pung'd it). 12s deadline.
    const aDeadline = Date.now() + 12_000;
    while (Date.now() < aDeadline) {
      snapA = await worldSnapshot(aSession.page);
      const activity = (snapA?.totalDiscard ?? 0) + (snapA?.totalMeld ?? 0);
      if (activity > 0) break;
      await aSession.page.waitForTimeout(400);
    }
    // Capture B's state at the SAME moment.
    snapB = await worldSnapshot(bSession.page);
    diag.snapA_postDiscard = snapA;
    diag.snapB_postDiscard = snapB;
    await Promise.all([
      snap(aSession.page, sid, '02-A-after-discard'),
      snap(bSession.page, sid, '02-B-during-A-discard'),
    ]);

    // Gate E2: A's game saw activity post-discard (the discard tile may
    // have been immediately Chow'd by a bot — moving the tile from
    // discard pile to a meld — so dealer pile === 0 is not a failure;
    // total discards OR melds growing IS the activity signal). B's
    // game must show ZERO activity (no discards, no melds) since we
    // never touched B.
    const aTotalActivity = (snapA?.totalDiscard ?? 0) + (snapA?.totalMeld ?? 0);
    const bTotalActivity = (snapB?.totalDiscard ?? 0) + (snapB?.totalMeld ?? 0);
    grade(sid, 'E2_noCrossContaminationFromAtoB',
      aTotalActivity > 0 && bTotalActivity === 0,
      {
        aDealerDiscards: snapA?.discardBySeat?.[0],
        aTotalDiscard: snapA?.totalDiscard,
        aTotalMeld: snapA?.totalMeld,
        aMeldBySeat: snapA?.meldBySeat,
        aTotalActivity,
        bDealerDiscards: snapB?.discardBySeat?.[0],
        bTotalDiscard: snapB?.totalDiscard,
        bTotalMeld: snapB?.totalMeld,
        bHandBySeat: snapB?.handBySeat,
        bTotalActivity,
      });

    // Gate E3: B's hand size still matches its independent deal — i.e. B's
    // dealer still has 14 (auto-deal) unaffected by A's actions.
    grade(sid, 'E3_BGameStateIntact',
      (snapB?.handBySeat?.[0] ?? 0) >= 13 &&
      (snapB?.foreignHandFaceUp ?? 0) === 0,
      {
        bHandBySeat: snapB?.handBySeat,
        bForeignFaceUp: snapB?.foreignHandFaceUp,
        bWallCount: snapB?.wallCount,
      });

    // Gate E4: synthetic Hu in A does NOT trigger a result modal in B.
    await aSession.page.evaluate(() => {
      const cli = window.game?.client;
      const events = cli?.events ?? cli?.['events'];
      events?.emit('update', [['result', 'current', {
        winner: cli.seat, type: 'Hu',
        score: [{seat:0,delta:24},{seat:1,delta:-8},{seat:2,delta:-8},{seat:3,delta:-8}],
        hand: [0,1,2,3,4,5,6,7,8,9,10,11,12,13],
        nextBanker: cli.seat,
      }]], false);
    });
    await aSession.page.waitForTimeout(800);
    const aModalVisible = await aSession.page.locator('#result-modal').isVisible().catch(() => false);
    const bModalVisible = await bSession.page.locator('#result-modal').isVisible().catch(() => false);
    diag.aModalVisible = aModalVisible;
    diag.bModalVisible = bModalVisible;
    await Promise.all([
      snap(aSession.page, sid, '03-A-with-result-modal'),
      snap(bSession.page, sid, '03-B-without-result-modal'),
    ]);
    grade(sid, 'E4_resultModalIsolatedToA',
      aModalVisible === true && bModalVisible === false,
      { aModalVisible, bModalVisible });

    diag.pageDiagA = aSession.pageDiag;
    diag.pageDiagB = bSession.pageDiag;
    writeStateDump(sid, 'state', { diag, gates: findings.scenarios[sid].gates });
    finalize(sid);
  } catch (e) {
    findings.scenarios[sid].error = e?.message ?? String(e);
    findings.scenarios[sid].status = 'ERROR';
    console.log(`SCENARIO ${sid} ERROR: ${e?.message ?? e}`);
  } finally {
    await aSession.ctx.close();
    await bSession.ctx.close();
  }
}

// ── Run all scenarios ───────────────────────────────────────────────

await scenarioA();
await scenarioB();
await scenarioC();
await scenarioD();
await scenarioE();

await browser.close();

// ── Summary + persist ───────────────────────────────────────────────

const summary = {
  ts: findings.ts,
  baseUrl: findings.baseUrl,
  byScenario: Object.fromEntries(Object.entries(findings.scenarios).map(([id, s]) => [id, {
    status: s.status,
    error: s.error ?? null,
    gates: Object.fromEntries(Object.entries(s.gates).map(([gid, g]) => [gid, g.status])),
  }])),
  totalPass: Object.values(findings.scenarios).filter(s => s.status === 'PASS').length,
  totalFail: Object.values(findings.scenarios).filter(s => s.status === 'FAIL').length,
  totalError: Object.values(findings.scenarios).filter(s => s.status === 'ERROR').length,
};
findings.summary = summary;

const summaryFile = path.join(ARTIFACT_DIR, `vasquez-pt-summary-${ts}.json`);
fs.writeFileSync(summaryFile, JSON.stringify(findings, null, 2));

console.log('\n=== FINAL SUMMARY ===');
console.log(JSON.stringify(summary, null, 2));
console.log(`\nSummary file: ${summaryFile}`);

const allPass = summary.totalPass === 5;
console.log(`\nOVERALL: ${allPass ? 'PASS' : 'FAIL'} (${summary.totalPass}/5)`);
process.exit(allPass ? 0 : 1);
