// Human-led Changsha playtest — companion to playtest-v3-fresh.spec.mjs.
//
// Drives a manual-mode game with a HUMAN at seat 0 (dealer) and 3 bots at
// seats 1/2/3. The flow mirrors real Changsha:
//   1. Dealer claims seat 0, clicks Deal → backend enters RollingDice.
//   2. Dealer rolls dice → BreakPointMarked, dealer's first 4-tile pickup.
//   3. Dealer + bots take wall tiles in round-robin (4/4/4 then +1 then dealer +1).
//   4. AwaitingDiscard reached → dealer attempts a discard (best effort:
//      tries window.game.client.sendDiscard?.(tileId) and world.emitDiscard?.()).
//   5. Observe bot claim window + subsequent turn flow for 60s.
//   6. Synthetic-Hu sanity check: inject a fake gameComplete entry locally
//      and confirm the result modal surfaces.
//
// Run with:
//   E2E_BASE_URL=http://127.0.0.1:8089 node playtest-artifacts/playtest-human-led.spec.mjs
//
// Stays observational — does NOT modify backend or frontend behaviour.

import { chromium } from 'playwright';
import fs from 'fs';
import path from 'path';

const ARTIFACT_DIR = path.resolve('./playtest-artifacts/human-led');
fs.mkdirSync(ARTIFACT_DIR, { recursive: true });

const baseUrl = process.env.E2E_BASE_URL || 'http://127.0.0.1:8089';
const findings = {
  url: '',
  variant: 'changsha',
  dealMode: 'manual',
  botCount: 3,
  steps: [],
  pageErrors: [],
  consoleErrors: [],
  consoleWarnings: [],
  networkFailures: [],
  collections: {},
  visibleButtonsAfterDeal: [],
  pickupProgression: [],
  moveLogProgression: [],
  discardAttempt: { tried: [], ok: false, via: null, tileId: null, reason: null },
  syntheticHu: { attempted: false, ok: null, modalVisible: null, reason: null },
  postDealHandSize: null,
};

const browser = await chromium.launch();
const ctx = await browser.newContext({ viewport: { width: 1280, height: 800 } });
const page = await ctx.newPage();

page.on('console', msg => {
  const t = msg.type();
  const text = msg.text();
  if (t === 'error') findings.consoleErrors.push(text);
  if (t === 'warning') findings.consoleWarnings.push(text);
  // Capture canonical collection size pings: "full update <kind> <N>" / "update <kind> <N>"
  const m = text.match(/(?:full update|update) (\w+) (\d+)/);
  if (m) findings.collections[m[1]] = parseInt(m[2], 10);
});
page.on('pageerror', err => findings.pageErrors.push(err.message));
page.on('response', resp => {
  if (resp.status() >= 400) {
    findings.networkFailures.push(`${resp.status()} ${resp.request().method()} ${resp.url()}`);
  }
});

// Defang full-page overlays so they never intercept clicks (parity with v3 spec).
await page.addInitScript(() => {
  const inject = () => {
    if (document.getElementById('human-led-overlay-defang')) return;
    const style = document.createElement('style');
    style.id = 'human-led-overlay-defang';
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
    console.log(`OK ${name}`, result ? JSON.stringify(result).slice(0, 240) : '');
  } catch (err) {
    const msg = err && err.message || String(err);
    findings.steps.push({ name, ok: false, error: msg });
    console.log(`FAIL ${name}: ${msg}`);
  }
}

// 1) Load human-led URL: manual deal + 3 bots + 4 hands, fresh game id.
await step('1-load', async () => {
  const uniqueGameId = `humanled-${Date.now()}-${Math.floor(Math.random() * 1000)}`;
  const url = `${baseUrl}/autotable/?variant=changsha&dealMode=manual&botCount=3&botDifficulty=Medium&handCount=4&gameId=${uniqueGameId}`;
  await page.goto(url, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(3000);
  findings.url = page.url();
  await snap('01-loaded.png');
  return { url: findings.url, gameId: uniqueGameId };
});

// 2) Dismiss tour overlay if present.
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

// 3) Close lobby panel (W23 playability workaround — lobby blocks Connect).
await step('3-close-lobby', async () => {
  const closeBtn = page.locator('#lobby-close');
  if (await closeBtn.isVisible().catch(() => false)) {
    await closeBtn.click({ force: true, timeout: 3000 });
    await page.waitForTimeout(800);
  }
  const lobbyOpen = await page.locator('#lobby-panel.lobby-open').count();
  return { lobbyStillOpen: lobbyOpen > 0 };
});

// 4) Click Connect to establish the WS session.
await step('4-connect', async () => {
  const connect = page.locator('#connect');
  const visible = await connect.first().isVisible().catch(() => false);
  if (!visible) {
    const alreadyConnected = await page.locator('#disconnect.server-connected').count() > 0;
    return { alreadyConnected };
  }
  await connect.first().click({ timeout: 5000 });
  await page.waitForTimeout(3500);
  const connected = await page.locator('#disconnect.server-connected').count() > 0;
  await snap('03-after-connect.png');
  return { connected };
});

// 5) Take the FIRST visible seat — this becomes the dealer seat (banker
// always starts at seat 0 on hand 1 per Changsha v1.2 §6.2).
await step('5-take-seat', async () => {
  const seats = page.locator('.take-seat');
  const total = await seats.count();
  let firstIdx = -1;
  let visibleCount = 0;
  for (let i = 0; i < total; i++) {
    if (await seats.nth(i).isVisible().catch(() => false)) {
      visibleCount++;
      if (firstIdx === -1) firstIdx = i;
    }
  }
  if (firstIdx === -1) {
    return { total, visibleCount, took: false, reason: 'no visible take-seat buttons' };
  }
  await seats.nth(firstIdx).click({ timeout: 5000 });
  await page.waitForTimeout(2500);
  const ourSeat = await page.evaluate(() => window.game?.client?.seat ?? null);
  await snap('04-after-take-seat.png');
  return { total, visibleCount, clickedIdx: firstIdx, ourSeat };
});

// 6) Click Deal — backend reads dealMode=manual and parks in RollingDice.
await step('6-deal', async () => {
  const deal = page.locator('#deal');
  const visible = await deal.first().isVisible().catch(() => false);
  const enabled = await deal.first().isEnabled().catch(() => false);
  if (!visible || !enabled) {
    return { visible, enabled, clicked: false };
  }
  await deal.first().click({ timeout: 5000 });
  await page.waitForTimeout(3500);
  await snap('05-after-deal.png');
  return { visible, enabled, clicked: true };
});

// 7) Roll dice on the dealer's behalf via World.emitRollDice() — equivalent
// to the dealer clicking the dice button in real play.
await step('7-roll-dice', async () => {
  await page.waitForTimeout(1500);
  return await page.evaluate(() => {
    const g = window.game;
    if (!g || !g.world) return { ok: false, reason: 'no window.game.world' };
    try {
      g.world.emitRollDice();
      return { ok: true };
    } catch (e) { return { ok: false, reason: String(e) }; }
  });
});

// 8) Drive the manual pickup chain: while pickup affordance is non-null and
// it's our turn, emit a take(). Otherwise wait for the bot to take its turn.
// Terminate when the pickup tombstones (phase reached AwaitingDiscard) OR
// after ~45s safety cap.
//
// Observed gap (W23): the connection-level `?dealMode=manual` is recorded
// on AutotableConnection but is NOT propagated to the Changsha runtime —
// `state.DealMode` defaults to Auto and StartGameAsync runs the one-shot
// auto-deal regardless. As a result the pickup collection is never
// populated for human-led games. The loop below will exit immediately on
// iteration 1 with pickup === null; that's the symptom of the gap, not a
// bug in this playtest. Findings.pickupProgression captures it.
await step('8-drive-deal', async () => {
  const startMs = Date.now();
  const iterations = [];
  for (let i = 0; i < 60; i++) {
    const snap = await page.evaluate(() => {
      const g = window.game;
      if (!g) return null;
      const w = g.world;
      const cli = g.client;
      const pickup = cli.pickup.get('current') ?? null;
      const myTurn = (typeof w.isMyPickupTurn === 'function') ? w.isMyPickupTurn() : null;
      // Count things-collection hand slots so we can correlate pickup
      // progress with hand-grow even when pickup is silently bypassed.
      // NB: things is keyed by tile-id (number); the slot name lives on
      // the value as v.slotName (AutotableProtocol.cs comment §1).
      let mySeatHandTiles = 0;
      const seat = w.seat;
      const seatSuffix = `@${seat}`;
      if (seat !== null && seat !== undefined) {
        for (const [, v] of cli.things.entries()) {
          const slot = v?.slotName ?? v?.SlotName;
          if (typeof slot === 'string' && slot.startsWith('hand.') && slot.endsWith(seatSuffix)) mySeatHandTiles++;
        }
      }
      return { seat, pickup, myTurn, mySeatHandTiles };
    });
    const elapsedSec = ((Date.now() - startMs) / 1000).toFixed(1);
    if (!snap) { iterations.push({ atSec: elapsedSec, error: 'no game' }); break; }
    iterations.push({ atSec: elapsedSec, seat: snap.seat, pickup: snap.pickup, myTurn: snap.myTurn, mySeatHandTiles: snap.mySeatHandTiles });

    // Pickup tombstoned → phase advanced past pickup; deal is done.
    // (Or — symptom of the dealMode gap — pickup was never populated.)
    if (snap.pickup === null) break;

    if (snap.myTurn) {
      const emitResult = await page.evaluate(() => {
        try {
          const ok = window.game.world.emitTakePickup();
          return { ok };
        } catch (e) { return { ok: false, error: String(e) }; }
      });
      iterations[iterations.length - 1].emitTake = emitResult;
    }

    await page.waitForTimeout(1200);

    // Safety cap on wall-clock time (45s).
    if (Date.now() - startMs > 45_000) break;
  }
  findings.pickupProgression = iterations;
  return { iterations: iterations.length, lastPickup: iterations[iterations.length - 1]?.pickup, finalHandSize: iterations[iterations.length - 1]?.mySeatHandTiles };
});

// 8b) If pickup-driven deal didn't run (auto-deal path or dealMode bypass),
// poll until the runtime has populated our seat's hand from the auto-deal
// path. Up to 20s. Captures the actual hand growth either way.
await step('8b-wait-hand-populated', async () => {
  const startMs = Date.now();
  let lastHandSize = -1;
  for (let i = 0; i < 20; i++) {
    const handSize = await page.evaluate(() => {
      const cli = window.game?.client;
      if (!cli) return null;
      const seat = cli.seat;
      if (seat === null || seat === undefined) return null;
      const seatSuffix = `@${seat}`;
      let n = 0;
      for (const [, v] of cli.things.entries()) {
        const slot = v?.slotName ?? v?.SlotName;
        if (typeof slot === 'string' && slot.startsWith('hand.') && slot.endsWith(seatSuffix)) n++;
      }
      return n;
    });
    lastHandSize = handSize;
    if (handSize !== null && handSize >= 13) break;
    await page.waitForTimeout(1000);
  }
  return { elapsedMs: Date.now() - startMs, finalHandSize: lastHandSize };
});

// 9) Post-deal screenshot + count of tiles in our own hand. After a complete
// manual deal the dealer should hold 14 tiles (12 + 1 + dealer-extra).
await step('9-post-deal', async () => {
  await page.waitForTimeout(2000);
  await snap('06-post-deal.png');
  const tileInfo = await page.evaluate(() => {
    const cli = window.game?.client;
    if (!cli || !cli.things) return null;
    const seat = cli.seat;
    if (seat === null || seat === undefined) return { seat: null };
    const seatSuffix = `@${seat}`;
    const handTiles = [];
    const byPrefix = {};
    for (const [k, v] of cli.things.entries()) {
      const slot = v?.slotName ?? v?.SlotName;
      if (typeof slot !== 'string') continue;
      const prefix = slot.split('@')[0].split('.')[0];
      byPrefix[prefix] = (byPrefix[prefix] ?? 0) + 1;
      if (slot.startsWith('hand.') && slot.endsWith(seatSuffix)) {
        handTiles.push({ tileId: k, slot });
      }
    }
    return { seat, handTileCount: handTiles.length, sampleSlots: handTiles.slice(0, 4), thingsByPrefix: byPrefix };
  });
  findings.postDealHandSize = tileInfo;
  return tileInfo;
});

// 10) Attempt the discard via several known + speculative backdoors.
//    a. client.sendDiscard?.(tileId)            — Hicks's expected API
//    b. world.emitDiscard?.(tileId)             — speculative parallel of emitRollDice
//    c. world.discardTile?.(tileId)             — speculative alt name
//    d. client.update([['discard', String(seat), { tileId }]])
//                                               — WS-direct path; backend
//                                                 already routes this kind
//                                                 (AutotableWsEndpoint.cs L711+)
// All paths are observed; the playtest does NOT fail if path (a/b/c) is
// missing — path (d) is the documented WS backdoor and should succeed
// whenever the runtime is in AwaitingDiscard with us as active seat.
await step('10-discard-attempt', async () => {
  await page.waitForTimeout(1500);
  const result = await page.evaluate(async () => {
    const tried = [];
    const game = window.game;
    if (!game) return { ok: false, tried, reason: 'no window.game' };
    const cli = game.client;
    if (!cli) return { ok: false, tried, reason: 'no client' };

    // Pick any tile in our own hand. `things` is keyed by tile-id (number);
    // the slot name lives on the value as `slotName`.
    const seat = cli.seat;
    const seatSuffix = `@${seat}`;
    let firstTileId = null;
    for (const [k, v] of cli.things.entries()) {
      const slot = v?.slotName ?? v?.SlotName;
      if (typeof slot === 'string' && slot.startsWith('hand.') && slot.endsWith(seatSuffix)) {
        if (typeof k === 'number') { firstTileId = k; break; }
        const tid = v?.thingIndex ?? v?.ThingIndex;
        if (typeof tid === 'number') { firstTileId = tid; break; }
      }
    }
    if (firstTileId === null) {
      return { ok: false, tried, reason: 'no own-seat hand tile found in things collection', seat };
    }

    // Path a: client.sendDiscard (Hicks's expected wire — optional chaining).
    tried.push({ path: 'client.sendDiscard', present: typeof cli.sendDiscard === 'function' });
    if (typeof cli.sendDiscard === 'function') {
      try {
        await cli.sendDiscard(firstTileId);
        return { ok: true, via: 'client.sendDiscard', tileId: firstTileId, seat, tried };
      } catch (e) { tried[tried.length - 1].error = String(e); }
    }

    // Path b: world.emitDiscard
    tried.push({ path: 'world.emitDiscard', present: typeof game.world.emitDiscard === 'function' });
    if (typeof game.world.emitDiscard === 'function') {
      try {
        await game.world.emitDiscard(firstTileId);
        return { ok: true, via: 'world.emitDiscard', tileId: firstTileId, seat, tried };
      } catch (e) { tried[tried.length - 1].error = String(e); }
    }

    // Path c: world.discardTile
    tried.push({ path: 'world.discardTile', present: typeof game.world.discardTile === 'function' });
    if (typeof game.world.discardTile === 'function') {
      try {
        await game.world.discardTile(firstTileId);
        return { ok: true, via: 'world.discardTile', tileId: firstTileId, seat, tried };
      } catch (e) { tried[tried.length - 1].error = String(e); }
    }

    // Path d: WS-direct `discard` collection push — the backend already
    // routes this in AutotableWsEndpoint.TryHandleDiscardActionAsync.
    tried.push({ path: 'client.update[discard]', present: typeof cli.update === 'function' });
    if (typeof cli.update === 'function') {
      try {
        cli.update([['discard', String(seat), { tileId: firstTileId, seatIndex: seat }]]);
        return { ok: true, via: 'client.update[discard]', tileId: firstTileId, seat, tried };
      } catch (e) { tried[tried.length - 1].error = String(e); }
    }

    return { ok: false, tileId: firstTileId, seat, tried, reason: 'no discard backdoor present in current build' };
  });
  findings.discardAttempt = result;
  await page.waitForTimeout(2000);
  await snap('07-after-discard-attempt.png');
  return result;
});

// 11) Wait 8s and capture the first move-log snapshot — should show bot
// Pung/Chow/Hu claim or pass plus subsequent turn(s), IF the discard fired.
await step('11-observe-8s', async () => {
  await page.waitForTimeout(8000);
  const entries = await page.locator('#move-log .move-log-entry, .move-log-entry').allTextContents().catch(() => []);
  return { moveLogCount: entries.length, recent: entries.slice(-10) };
});

// 12) Continuous observation: capture the move log every 5s for ~60s so the
// playtest report shows the multi-turn cadence (or lack thereof).
await step('12-continuous-observation', async () => {
  const captures = [];
  for (let i = 0; i < 12; i++) {
    const entries = await page.locator('#move-log .move-log-entry, .move-log-entry').allTextContents().catch(() => []);
    const phase = await page.evaluate(() => {
      // Best-effort phase peek via the result/pickup collections.
      const cli = window.game?.client;
      if (!cli) return null;
      const pickup = cli.pickup.get('current') ?? null;
      const result = cli.result.get('current') ?? null;
      return { pickup, result };
    });
    captures.push({ atSec: i * 5, count: entries.length, recent: entries.slice(-5), phase });
    if (i < 11) await page.waitForTimeout(5000);
  }
  findings.moveLogProgression = captures;
  await snap('08-after-60s-observation.png');
  return { captureCount: captures.length, finalLogCount: captures[captures.length - 1].count };
});

// 13) Synthetic-Hu sanity check — push a fake gameComplete entry via the
// BaseClient internal events emitter so subscribers (GameUi) receive an
// 'update' event identical to the one the server would dispatch. Confirms
// the win-detection UI surfaces correctly even without a real Hu.
await step('13-synthetic-hu', async () => {
  const result = await page.evaluate(() => {
    try {
      const cli = window.game?.client;
      if (!cli) return { ok: false, reason: 'no client' };
      const payload = {
        isComplete: true,
        totalScores: { '0': 12, '1': -4, '2': -4, '3': -4 },
        handHistory: [],
        maxHands: 4,
      };
      // BaseClient extends EventEmitter via an `events` private field.
      // At runtime TS private fields are accessible — emit a synthetic
      // 'update' that every Collection's onUpdate handler will fan out.
      const events = cli.events ?? cli['events'];
      if (!events || typeof events.emit !== 'function') {
        return { ok: false, reason: 'no events emitter on client' };
      }
      events.emit('update', [['gameComplete', 'current', payload]], false);
      return { ok: true };
    } catch (e) { return { ok: false, reason: String(e) }; }
  });
  findings.syntheticHu = { attempted: true, ...result };
  await page.waitForTimeout(2000);
  const modalVisible = await page.locator('#game-complete-modal').isVisible().catch(() => false);
  findings.syntheticHu.modalVisible = modalVisible;
  await snap('09-after-synthetic-hu.png');
  return { ok: result.ok, modalVisible, reason: result.reason };
});

// 14) Final state snapshot + visible-button inventory (post-game).
await step('14-final', async () => {
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
  await snap('10-final-state.png');
  return { visibleButtonCount: findings.visibleButtonsAfterDeal.length };
});

await browser.close();

fs.writeFileSync(path.join(ARTIFACT_DIR, 'findings.json'), JSON.stringify(findings, null, 2));

// Human-readable summary at the tail of the run.
console.log('\n=== FINAL findings ===');
console.log(JSON.stringify({
  url: findings.url,
  collections: findings.collections,
  postDealHandSize: findings.postDealHandSize,
  discardAttempt: findings.discardAttempt,
  syntheticHu: findings.syntheticHu,
  pickupIterations: findings.pickupProgression.length,
  moveLogProgressionCaptures: findings.moveLogProgression.length,
  finalMoveLogCount: findings.moveLogProgression[findings.moveLogProgression.length - 1]?.count ?? null,
  visibleButtonCount: findings.visibleButtonsAfterDeal.length,
  pageErrorsCount: findings.pageErrors.length,
  consoleErrorsCount: findings.consoleErrors.length,
  networkFailuresCount: findings.networkFailures.length,
  steps: findings.steps.map(s => ({ name: s.name, ok: s.ok, error: s.error })),
}, null, 2));

if (findings.pageErrors.length) {
  console.log('\nPAGE ERRORS:');
  for (const e of findings.pageErrors) console.log(' -', e);
}
if (findings.networkFailures.length) {
  console.log('\nNETWORK FAILURES (first 10):');
  for (const e of findings.networkFailures.slice(0, 10)) console.log(' -', e);
}
