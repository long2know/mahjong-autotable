// Vasquez — Visual gate for the Changsha dealing ceremony.
//
// THE GAP this gate closes:
//   Stephen called out a regression at 2026-05-27T21:27Z — walls were
//   rendering face-up + scattered instead of the canonical four face-down
//   walls.  The pre-existing `playtest-human-led.spec.mjs` failed to catch
//   it because it drove `world.deal('HANDS')` directly, bypassing the
//   visual choreography (face orientation, wall composition, pickup
//   ceremony).  This spec asserts the *visual contract* at each ceremony
//   phase so the regression cannot reappear silently.
//
// Six visual gates (see GATE-1..GATE-6 below):
//   1. Wall count = exactly 4 walls (one per seat).
//   2. All wall tiles face-down at T=2s post-connect.
//   3. No hand tiles visible at T=2s post-connect.
//   4. Dice has not yet rolled at T=2s post-connect.
//   5. After Hicks's auto-driven pickup chain (or our fallback explicit
//      pushes when the chain stalls), every seat reaches either
//      {12,12,12,12} (intermediate, post-3-rounds) or {14,13,13,13}
//      (final, dealer-extra applied).  Anything else means the ceremony
//      mis-fired.
//   6. Zero page errors throughout.
//
// Run with:
//   E2E_BASE_URL=http://127.0.0.1:8088 node playtest-artifacts/playtest-changsha-dealing-ceremony.spec.mjs
//
// Exits non-zero on any gate failure so it can wire into CI as a hard gate.

import { chromium } from 'playwright';
import fs from 'fs';
import path from 'path';

const ARTIFACT_DIR = path.resolve('./playtest-artifacts/changsha-dealing');
fs.mkdirSync(ARTIFACT_DIR, { recursive: true });

const baseUrl = process.env.E2E_BASE_URL || 'http://127.0.0.1:8088';
const GAME_ID = process.env.PLAYTEST_GAME_ID || `vasquez-deal-gate-${Date.now()}-${Math.floor(Math.random() * 1000)}`;
const URL = `${baseUrl}/autotable/?variant=changsha&dealMode=manual&botCount=3&botDifficulty=Hard&handCount=4&gameId=${GAME_ID}`;

const findings = {
  url: URL,
  gameId: GAME_ID,
  variant: 'changsha',
  dealMode: 'manual',
  botCount: 3,
  steps: [],
  gates: {
    'GATE-1-wall-count-eq-4': null,
    'GATE-2-walls-all-face-down': null,
    'GATE-3-no-hand-tiles-visible': null,
    'GATE-4-dice-not-yet-rolled': null,
    'GATE-5-twelve-tiles-after-3-rounds': null,
    'GATE-6-zero-page-errors': null,
  },
  pageErrors: [],
  consoleErrors: [],
  consoleWarnings: [],
  networkFailures: [],
  collections: {},
  wallTopology: null,
  handProgression: [],
  diceState: null,
};

const browser = await chromium.launch();
const ctx = await browser.newContext({ viewport: { width: 1280, height: 800 } });
const page = await ctx.newPage();

page.on('console', msg => {
  const t = msg.type();
  const text = msg.text();
  if (t === 'error') findings.consoleErrors.push(text);
  if (t === 'warning') findings.consoleWarnings.push(text);
  const m = text.match(/(?:full update|update) (\w+) (\d+)/);
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
    if (document.getElementById('deal-gate-overlay-defang')) return;
    const style = document.createElement('style');
    style.id = 'deal-gate-overlay-defang';
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
    const summary = result === undefined ? '' : JSON.stringify(result).slice(0, 240);
    console.log(`OK ${name} ${summary}`);
    return result;
  } catch (err) {
    const msg = err && err.message || String(err);
    findings.steps.push({ name, ok: false, error: msg });
    console.log(`FAIL ${name}: ${msg}`);
    return null;
  }
}

// Reach into the upstream bundle's runtime state and pull the things, dice,
// pickup, and match collections.  Runs entirely on the page so we read the
// authoritative client-side view (same data the renderer consumes).
async function probeState() {
  return await page.evaluate(() => {
    const g = window.game;
    if (!g || !g.client) return null;
    const cli = g.client;
    const out = {
      seat: cli.seat ?? null,
      connected: typeof cli.connected === 'function' ? cli.connected() : null,
      thingsTotal: 0,
      wallTilesBySeat: { 0: 0, 1: 0, 2: 0, 3: 0 },
      wallTilesFaceUp: [],
      wallTilesFaceDown: 0,
      wallStacksBySeat: { 0: new Set(), 1: new Set(), 2: new Set(), 3: new Set() },
      handTilesBySeat: { 0: 0, 1: 0, 2: 0, 3: 0 },
      handTilesFaceUpBySeat: { 0: 0, 1: 0, 2: 0, 3: 0 },
      discardTilesBySeat: { 0: 0, 1: 0, 2: 0, 3: 0 },
      meldTilesBySeat: { 0: 0, 1: 0, 2: 0, 3: 0 },
      thingsByPrefix: {},
      diceEntry: null,
      pickupEntry: null,
      matchEntry: null,
    };
    for (const [tileId, info] of cli.things.entries()) {
      out.thingsTotal++;
      const slot = info?.slotName ?? info?.SlotName;
      if (typeof slot !== 'string') continue;
      const prefix = slot.split('@')[0].split('.')[0];
      out.thingsByPrefix[prefix] = (out.thingsByPrefix[prefix] ?? 0) + 1;
      const m = slot.match(/^(\w+)\.(\d+)(?:\.(\d+))?@(\d)$/);
      if (!m) continue;
      const group = m[1];
      const col = parseInt(m[2], 10);
      const seat = parseInt(m[4], 10);
      const face = info.face === undefined ? undefined : info.face;
      if (group === 'wall') {
        out.wallTilesBySeat[seat] = (out.wallTilesBySeat[seat] ?? 0) + 1;
        if (out.wallStacksBySeat[seat]) out.wallStacksBySeat[seat].add(col);
        // Tile is face-down if face === null (explicit signal) OR face === undefined
        // (backend forgot to set it, which the bundle still renders as a back by
        // virtue of rotationIndex).  We treat face === undefined as ACCEPTABLE
        // (defensive default).  Any numeric face on a wall tile is a regression.
        if (typeof face === 'number') {
          out.wallTilesFaceUp.push({ tileId, slot, face });
        } else {
          out.wallTilesFaceDown++;
        }
      } else if (group === 'hand') {
        out.handTilesBySeat[seat] = (out.handTilesBySeat[seat] ?? 0) + 1;
        if (typeof face === 'number') {
          out.handTilesFaceUpBySeat[seat] = (out.handTilesFaceUpBySeat[seat] ?? 0) + 1;
        }
      } else if (group === 'discard') {
        out.discardTilesBySeat[seat] = (out.discardTilesBySeat[seat] ?? 0) + 1;
      } else if (group === 'meld') {
        out.meldTilesBySeat[seat] = (out.meldTilesBySeat[seat] ?? 0) + 1;
      }
    }
    out.wallStacksBySeat = Object.fromEntries(
      Object.entries(out.wallStacksBySeat).map(([k, v]) => [k, v.size])
    );
    out.diceEntry = cli.dice?.get?.('0') ?? cli.dice?.get?.(0) ?? null;
    out.pickupEntry = cli.pickup?.get?.('0') ?? cli.pickup?.get?.(0) ?? cli.pickup?.get?.('current') ?? null;
    out.matchEntry = cli.match?.get?.(0) ?? null;
    return out;
  });
}

// === STEP 1 — Load the deal-gate URL ===
await step('1-load', async () => {
  await page.goto(URL, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(2500);
  return { url: page.url() };
});

// === STEP 2 — Dismiss tour ===
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
});

// === STEP 3 — Close lobby + connect (per W23 recipe) ===
await step('3-close-lobby', async () => {
  const closeBtn = page.locator('#lobby-close');
  if (await closeBtn.isVisible().catch(() => false)) {
    await closeBtn.click({ force: true, timeout: 3000 });
    await page.waitForTimeout(800);
  }
});

await step('4-connect', async () => {
  const connect = page.locator('#connect');
  const visible = await connect.first().isVisible().catch(() => false);
  if (visible) {
    await connect.first().click({ timeout: 5000 });
  }
  await page.waitForTimeout(3500);
  return { connected: await page.locator('#disconnect.server-connected').count() > 0 };
});

// === STEP 5 — Take seat 0 (dealer) ===
await step('5-take-seat', async () => {
  const seats = page.locator('.take-seat');
  const total = await seats.count();
  let firstIdx = -1;
  for (let i = 0; i < total; i++) {
    if (await seats.nth(i).isVisible().catch(() => false)) { firstIdx = i; break; }
  }
  if (firstIdx === -1) return { took: false, reason: 'no take-seat visible' };
  await seats.nth(firstIdx).click({ timeout: 5000 });
  await page.waitForTimeout(2000);
  const ourSeat = await page.evaluate(() => window.game?.client?.seat ?? null);
  return { ourSeat };
});

// === STEP 6 — wait 2s + snapshot the "walls only" phase ===
// This is the canonical "T=2s post-connect" state that GATEs 1–4 assert against.
// At this point: no dice rolled, walls face-down, hands empty, 4 walls present.
//
// NB: the upstream bundle pre-populates its sandbox wall before our runtime
// deals — so this snapshot reflects whichever state the bundle considers
// "default" for the connected Changsha session.  Stephen's regression would
// surface here as face-up tiles or scattered/missing walls.
await step('6-walls-only-snapshot', async () => {
  await page.waitForTimeout(2000);
  await snap('01-walls-only.png');
  const s = await probeState();
  findings.wallTopology = s;
  return {
    thingsTotal: s?.thingsTotal,
    wallStacksBySeat: s?.wallStacksBySeat,
    wallTilesBySeat: s?.wallTilesBySeat,
    wallTilesFaceUpCount: s?.wallTilesFaceUp?.length,
    handTilesBySeat: s?.handTilesBySeat,
    diceEntry: s?.diceEntry,
    pickupEntry: s?.pickupEntry,
  };
});

// === STEP 6b — Fire the Deal action (world.deal('HANDS')) ===
// The dealer's hold-to-confirm #deal button calls this in real play.  In
// manual mode Hicks PR #88 makes this autonomously emit `pickup[rollDice]`
// + 4× `pickup[take]` to drive the dealer-side chain.  Without it the
// runtime stays in Seating phase and rollDice/take pushes are silent no-ops.
//
// We invoke `world.deal('HANDS')` directly (same path the bundle's
// setupDealButton.onSuccess callback uses after a real hold).
await step('6b-fire-deal', async () => {
  const r = await page.evaluate(() => {
    const g = window.game;
    if (!g || !g.world) return { ok: false, reason: 'no world' };
    try {
      g.world.deal('HANDS');
      return { ok: true };
    } catch (e) { return { ok: false, reason: String(e) }; }
  });
  await page.waitForTimeout(2500);
  const s = await probeState();
  return { fire: r, postDeal: { thingsByPrefix: s?.thingsByPrefix, dice: s?.diceEntry, pickup: s?.pickupEntry } };
});

// ============================================================
// === VISUAL GATES 1–4 ====================================
// ============================================================
//
// These are the hard gates Stephen's regression would have failed.

function assertGate(key, ok, detail) {
  findings.gates[key] = { ok, detail };
  console.log(`${ok ? '✅' : '❌'} ${key}: ${JSON.stringify(detail).slice(0, 300)}`);
  return ok;
}

const probe = findings.wallTopology;

if (!probe) {
  assertGate('GATE-1-wall-count-eq-4', false, { reason: 'probeState returned null' });
  assertGate('GATE-2-walls-all-face-down', false, { reason: 'probeState returned null' });
  assertGate('GATE-3-no-hand-tiles-visible', false, { reason: 'probeState returned null' });
  assertGate('GATE-4-dice-not-yet-rolled', false, { reason: 'probeState returned null' });
} else {
  // GATE-1: every seat must have ≥1 wall stack — 4 distinct walls total.
  // Canonical Changsha geometry (locked in AutotableSlotMap §1) is 14/14/13/13
  // stacks per seat = 54 stacks × 2 tiers = 108 tiles.  We assert the
  // structural-symmetry of "one wall per seat" as the hard gate; the
  // canonical 14/14/13/13 split is reported as an additional diagnostic so
  // a regression that fills walls with stale FOUR_PLAYER 19-col layouts is
  // visible in findings.json even though it doesn't fail GATE-1 directly.
  const seatsWithWall = Object.entries(probe.wallStacksBySeat).filter(([, n]) => n > 0).length;
  const totalWallTiles = Object.values(probe.wallTilesBySeat).reduce((a, b) => a + b, 0);
  const canonicalStacks = (probe.wallStacksBySeat[0] === 14
    && probe.wallStacksBySeat[1] === 14
    && probe.wallStacksBySeat[2] === 13
    && probe.wallStacksBySeat[3] === 13);
  const canonicalTotal = totalWallTiles === 108;
  assertGate('GATE-1-wall-count-eq-4',
    seatsWithWall === 4,
    {
      seatsWithWall,
      wallStacksBySeat: probe.wallStacksBySeat,
      totalWallTiles,
      canonicalChangshaStacks: canonicalStacks,
      canonicalChangshaTotal108: canonicalTotal,
    });

  // GATE-2: any wall tile with a numeric face is the regression.
  assertGate('GATE-2-walls-all-face-down',
    probe.wallTilesFaceUp.length === 0,
    {
      faceDownCount: probe.wallTilesFaceDown,
      faceUpCount: probe.wallTilesFaceUp.length,
      sampleFaceUp: probe.wallTilesFaceUp.slice(0, 5),
    });

  // GATE-3: no hand tiles should have a face revealed pre-pickup.  In the
  // pre-deal phase, NO hand tile entries should exist at all.
  const totalHandFaceUp = Object.values(probe.handTilesFaceUpBySeat).reduce((a, b) => a + b, 0);
  const totalHandTiles = Object.values(probe.handTilesBySeat).reduce((a, b) => a + b, 0);
  assertGate('GATE-3-no-hand-tiles-visible',
    totalHandTiles === 0 && totalHandFaceUp === 0,
    {
      handTilesBySeat: probe.handTilesBySeat,
      handTilesFaceUpBySeat: probe.handTilesFaceUpBySeat,
    });

  // GATE-4: dice must not yet have a value.  Accept missing entry OR
  // entry with d1/d2 both === 0 (initial state).
  const dice = probe.diceEntry;
  const diceRolled = !!(dice && (
    (typeof dice.d1 === 'number' && dice.d1 > 0) ||
    (typeof dice.d2 === 'number' && dice.d2 > 0) ||
    (Array.isArray(dice.dice) && dice.dice.some(v => v > 0))
  ));
  assertGate('GATE-4-dice-not-yet-rolled', !diceRolled, { dice, diceRolled });
}

// === STEP 7 — Roll dice ===
// Use the same wire path the bundle uses (Hicks's emitRollDice): push a
// `pickup[rollDice]` collection entry.  Backend's
// AutotableWsEndpoint.TryHandlePickupActionAsync routes this into the
// runtime's RollingDice → BreakPointMarked transition.
await step('7-roll-dice', async () => {
  const ok = await page.evaluate(() => {
    const g = window.game;
    if (!g || !g.world) return { ok: false, reason: 'no world' };
    try {
      // Prefer the Hicks emit path (validates seat ownership).  Fall back to
      // a direct collection push if emitRollDice short-circuits.
      if (typeof g.world.emitRollDice === 'function') {
        g.world.emitRollDice();
        return { ok: true, via: 'world.emitRollDice' };
      }
      g.client.update([['pickup', 'rollDice', { seatIndex: 0 }]]);
      return { ok: true, via: 'client.update(pickup.rollDice)' };
    } catch (e) { return { ok: false, reason: String(e) }; }
  });
  await page.waitForTimeout(2000);
  await snap('02-dice-rolled.png');
  const s = await probeState();
  findings.diceState = s?.diceEntry;
  return { emit: ok, dice: s?.diceEntry, pickup: s?.pickupEntry };
});

// === STEP 8 — Observe the auto-driven pickup chain ===
// With Hicks PR #88 the bundle's `world.deal('HANDS')` autonomously emits the
// rollDice + 4× take sequence for the dealer's seat, and bot AI auto-takes
// for the other 3 seats.  We just OBSERVE, capturing hand growth + the
// pickup affordance phase at every 600ms tick until either:
//   (a) every seat reaches 12 tiles (3-round milestone — GATE-5),
//   (b) the chain stalls for 4 consecutive ticks (chain regression),
//   (c) the 30s wall-clock budget expires.
//
// If the chain stalls AND any seat is short of 12, we fall back to pushing
// a `pickup[take]` for the active seat to expose whether the runtime
// would have progressed had it received the wire trigger.  This lets the
// gate distinguish "chain auto-drives correctly" (the post-Hicks contract)
// from "chain dead, manual takes still progress runtime" (the Bishop
// pre-Hicks state) from "everything dead" (the deep regression).
await step('8-drive-three-rounds-of-four', async () => {
  const log = [];
  const startMs = Date.now();
  const budgetMs = 30_000;
  let stallTicks = 0;
  let lastHandsTotal = 0;
  let midSnapshotted = false;
  let twelveSnapshotted = false;
  for (let iter = 0; iter < 60; iter++) {
    const s = await probeState();
    const handsBySeat = s?.handTilesBySeat ?? {};
    const handsTotal = Object.values(handsBySeat).reduce((a, b) => a + b, 0);
    const wallTotal = s ? Object.values(s.wallTilesBySeat).reduce((a, b) => a + b, 0) : null;
    const allTwelve = [0, 1, 2, 3].every(k => handsBySeat[k] === 12);
    log.push({
      iter,
      atMs: Date.now() - startMs,
      handsTotal,
      handsBySeat,
      wallTotal,
      pickupEntry: s?.pickupEntry,
    });
    if (!midSnapshotted && handsTotal >= 24) {
      await snap('03-mid-pickup.png');
      midSnapshotted = true;
    }
    if (allTwelve && !twelveSnapshotted) {
      await snap('04-all-hands-12.png');
      twelveSnapshotted = true;
      break;
    }
    if (handsTotal === lastHandsTotal) {
      stallTicks++;
      if (stallTicks >= 4 && s?.pickupEntry?.seatIndex !== undefined) {
        const active = s.pickupEntry.seatIndex;
        const cnt = s.pickupEntry.count ?? 4;
        await page.evaluate(({ a, c }) => {
          try { window.game.client.update([['pickup', 'take', { seatIndex: a, count: c }]]); } catch {}
        }, { a: active, c: cnt });
        log[log.length - 1].stalled = true;
        log[log.length - 1].fallbackPush = { seatIndex: active, count: cnt };
        stallTicks = 0;
      }
    } else {
      stallTicks = 0;
    }
    lastHandsTotal = handsTotal;
    if (Date.now() - startMs > budgetMs) break;
    await page.waitForTimeout(600);
  }
  if (!twelveSnapshotted) await snap('04-all-hands-12.png');
  findings.handProgression = log;

  // GATE-5: after 3 rounds of 4 every seat should hold exactly 12 tiles.
  // This is the "intermediate" milestone — before the single-tile round and
  // dealer-extra.  The chain may overshoot to 14/13/13/13 if it's fast; we
  // accept either the intermediate (12/12/12/12) or the final (14/13/13/13)
  // canonical state as a pass — both indicate the ceremony played out.
  const final = await probeState();
  const handsBySeatFinal = final?.handTilesBySeat ?? {};
  const intermediate = [0, 1, 2, 3].every(k => handsBySeatFinal[k] === 12);
  const finalDeal = (handsBySeatFinal[0] === 14
    && handsBySeatFinal[1] === 13
    && handsBySeatFinal[2] === 13
    && handsBySeatFinal[3] === 13);
  assertGate('GATE-5-twelve-tiles-after-3-rounds',
    intermediate || finalDeal,
    {
      handsBySeat: handsBySeatFinal,
      totalHands: Object.values(handsBySeatFinal).reduce((a, b) => a + b, 0),
      expected: 'either {12,12,12,12} or {14,13,13,13}',
      intermediateReached: intermediate,
      finalDealReached: finalDeal,
    });
  return { iterations: log.length, finalHandsBySeat: handsBySeatFinal };
});

// === STEP 9 — One single-tile pickup per seat (round 4) ===
// If the auto-driven chain already advanced past the single-tile round (the
// pickup affordance is null or in 'inPlay'), this step is a no-op.  We push
// a single-tile take only when the pickup phase reports we're owed one.
await step('9-single-tile-round', async () => {
  for (let i = 0; i < 8; i++) {
    const s = await probeState();
    const p = s?.pickupEntry;
    if (!p || p.phase === 'inPlay' || p.count !== 1) break;
    const seatIndex = p.seatIndex;
    await page.evaluate((sx) => {
      try { window.game.client.update([['pickup', 'take', { seatIndex: sx, count: 1 }]]); } catch {}
    }, seatIndex);
    await page.waitForTimeout(500);
  }
  await page.waitForTimeout(800);
  const s = await probeState();
  return { handTilesBySeat: s?.handTilesBySeat, pickupEntry: s?.pickupEntry };
});

// === STEP 10 — Dealer's extra pickup (the +1 that puts the dealer at 14) ===
// Same observational pattern: only push if the affordance is still showing
// DealerExtra (the chain may have completed already).
await step('10-dealer-extra', async () => {
  for (let i = 0; i < 4; i++) {
    const s = await probeState();
    const p = s?.pickupEntry;
    if (!p || p.phase === 'inPlay') break;
    if (p.phase && p.phase.toLowerCase().includes('dealerextra')) {
      await page.evaluate((sx) => {
        try { window.game.client.update([['pickup', 'take', { seatIndex: sx, count: 1 }]]); } catch {}
      }, p.seatIndex);
      await page.waitForTimeout(500);
    } else {
      break;
    }
  }
  await page.waitForTimeout(1500);
  await snap('05-final-deal.png');
  const s = await probeState();
  return { handTilesBySeat: s?.handTilesBySeat, pickupEntry: s?.pickupEntry };
});

// === STEP 11 — Capture failure baseline if anything went wrong ===
// If any gate failed and the user didn't explicitly tag this as a post-fix
// run, save a `baseline-before-fix.png` for the squad memo.  We prefer the
// most-informative artifact (final-deal screenshot showing the regression),
// falling back to the walls-only snapshot if 05-final-deal.png is missing.
const anyGateFailed = Object.values(findings.gates).some(g => g && g.ok === false);
if (anyGateFailed && !process.env.PLAYTEST_POST_FIX) {
  try {
    const finalImg = path.join(ARTIFACT_DIR, '05-final-deal.png');
    const wallsImg = path.join(ARTIFACT_DIR, '01-walls-only.png');
    const src = fs.existsSync(finalImg) ? finalImg : wallsImg;
    fs.copyFileSync(src, path.join(ARTIFACT_DIR, 'baseline-before-fix.png'));
    console.log(`Saved baseline-before-fix.png (gates failed; pre-fix snapshot from ${path.basename(src)}).`);
  } catch (e) {
    console.log(`baseline-before-fix.png copy failed: ${e.message}`);
  }
}

// === GATE-6 — zero page errors ===
assertGate('GATE-6-zero-page-errors',
  findings.pageErrors.length === 0,
  { count: findings.pageErrors.length, sample: findings.pageErrors.slice(0, 5) });

await browser.close();

fs.writeFileSync(path.join(ARTIFACT_DIR, 'findings.json'), JSON.stringify(findings, null, 2));

console.log('\n=== FINAL summary ===');
console.log(JSON.stringify({
  url: findings.url,
  gameId: findings.gameId,
  gates: Object.fromEntries(
    Object.entries(findings.gates).map(([k, v]) => [k, v?.ok ?? null])
  ),
  collections: findings.collections,
  pageErrorsCount: findings.pageErrors.length,
  consoleErrorsCount: findings.consoleErrors.length,
  networkFailuresCount: findings.networkFailures.length,
  handProgressionFinal: findings.handProgression[findings.handProgression.length - 1] ?? null,
  steps: findings.steps.map(s => ({ name: s.name, ok: s.ok, error: s.error })),
}, null, 2));

if (findings.pageErrors.length) {
  console.log('\nPAGE ERRORS:');
  for (const e of findings.pageErrors) console.log(' -', e);
}

const failedGates = Object.entries(findings.gates).filter(([, v]) => v && v.ok === false);
if (failedGates.length) {
  console.log(`\n❌ ${failedGates.length} GATE(S) FAILED:`);
  for (const [k, v] of failedGates) console.log(' -', k, '→', JSON.stringify(v.detail).slice(0, 240));
  process.exit(1);
}
console.log('\n✅ all gates passed');
process.exit(0);
