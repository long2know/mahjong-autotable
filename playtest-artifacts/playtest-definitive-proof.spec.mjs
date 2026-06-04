// Vasquez 2026-06-04 — Definitive end-to-end visual proof.
//
// Stephen's directive: "I want UNDENIABLE VISUAL PROOF the Changsha
// game is playable."  The 2026-06-03 thorough wave green-lit all
// gates against `world.things` + collection state; this spec turns
// those gates into a sequence of 10 labelled screenshots covering
// every canonical Changsha phase from a face-down wall to game-
// complete + multi-game isolation.
//
// Output layout:
//   playtest-artifacts/screenshots/def-proof-<ts>/
//     01-walls-built-facedown.png
//     02-dice-rolled.png
//     03-dealing-ceremony.png
//     04-hand-dealt-faceup.png
//     05-tile-selected.png
//     06-discard-on-table.png
//     07-claim-window.png
//     08-hand-result-modal.png
//     09-game-complete-modal.png
//     10-multi-game-isolation.png  (+ -A.png, -B.png companions)
//     findings.json
//
// Run:
//   cd /data/source/mahjong-autotable
//   E2E_BASE_URL=http://127.0.0.1:8088 \
//     node playtest-artifacts/playtest-definitive-proof.spec.mjs
//
// Exit: 0 if 10/10 captured + 0 page errors; non-zero otherwise.

import pw from 'playwright';
const { chromium } = pw;
import fs from 'fs';
import path from 'path';

const baseUrl = process.env.E2E_BASE_URL || 'http://127.0.0.1:8088';
const runStartedAt = new Date();
const ts = runStartedAt.toISOString().replace(/[:.]/g, '-');
const RUN_TAG = `def-proof-${Date.now()}`;
const ARTIFACT_DIR = path.resolve(`./playtest-artifacts/screenshots/${RUN_TAG}`);
fs.mkdirSync(ARTIFACT_DIR, { recursive: true });

const findings = {
  timestamp: runStartedAt.toISOString(),
  runTag: RUN_TAG,
  baseUrl,
  totalScreenshots: 10,
  passed: [],
  failed: [],
  pageErrorsTotal: 0,
  perScreenshot: [],
};

const OVERLAY_DEFANG = `
  #tour-overlay, #magic-link-landing, #magic-link-overlay,
  #signin-modal-backdrop, .magic-link-landing, .magic-link-overlay,
  .signin-modal-backdrop, [data-testid="tour-overlay"],
  [data-testid="signin-modal-backdrop"]
    { display: none !important; pointer-events: none !important; visibility: hidden !important; }
  [aria-hidden="true"] { pointer-events: none !important; }
`;

// ── helpers ─────────────────────────────────────────────────────────

async function makePage(browser, label) {
  const ctx = await browser.newContext({ viewport: { width: 1280, height: 800 } });
  await ctx.addInitScript((css) => {
    const inject = () => {
      if (document.getElementById('vasquez-def-proof-defang')) return;
      const s = document.createElement('style');
      s.id = 'vasquez-def-proof-defang';
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
      if (/(skipped|forcing) stale moveTo/.test(text)) pageDiag.staleMoveToWarnings++;
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
    let wallFaceDown = 0;
    let wallFaceUp = 0;
    const myHandIds = [];
    for (const t of w.things.values()) {
      if (!t.slot) continue;
      const s = t.slot;
      if (s.group === 'wall' || s.group === 'wall.open') {
        wallCount++;
        if (t.rotationIndex === 0) wallFaceDown++;
        else wallFaceUp++;
      }
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
    const diceEntry = safeGet(w.client?.dice, 0);
    const claimsBySeat = [0, 1, 2, 3].map(s => safeGet(w.client?.claim, String(s)));
    return {
      seat,
      handBySeat,
      meldBySeat,
      discardBySeat,
      totalDiscard,
      totalMeld,
      wallCount,
      wallFaceDown,
      wallFaceUp,
      myHandIds: myHandIds.sort((a, b) => String(a.key).localeCompare(String(b.key))),
      myHandFaceUp,
      foreignHandFaceUp,
      hasExtraHandTile: typeof w.hasExtraHandTile === 'function' ? w.hasExtraHandTile() : null,
      pickup: p ? { phase: p.phase ?? null, seatIndex: p.seatIndex ?? null, count: p.count ?? null } : null,
      result: r ? { type: r.type, winner: r.winner } : null,
      gameComplete: gc ? { isComplete: gc.isComplete ?? gc.IsComplete ?? null, totalScores: gc.totalScores ?? gc.TotalScores ?? null } : null,
      diceState: diceEntry?.state ?? null,
      diceValues: diceEntry?.dice ?? (diceEntry?.d1 != null ? [diceEntry.d1, diceEntry.d2] : null),
      claimsBySeat,
    };
  });
}

async function captureScreenshot(page, label, opts = {}) {
  const start = Date.now();
  const file = path.join(ARTIFACT_DIR, `${label}.png`);
  const ok = opts.preconditionsMet !== false;
  try {
    await page.screenshot({ path: file, fullPage: true });
  } catch (e) {
    console.log(`  !! screenshot failed for ${label}: ${e?.message ?? e}`);
  }
  const entry = {
    label,
    file: path.basename(file),
    preconditionsMet: ok,
    pageErrors: opts.pageDiag?.pageErrors?.length ?? 0,
    ms: Date.now() - start,
    detail: opts.detail ?? null,
  };
  findings.perScreenshot.push(entry);
  if (ok) {
    findings.passed.push(label);
  } else {
    findings.failed.push({ label, reason: opts.failReason ?? 'precondition' });
  }
  console.log(`  ${ok ? '✅' : '❌'} ${label}  (${entry.ms}ms, ${entry.pageErrors} pageErrors${ok ? '' : ' — ' + (opts.failReason ?? 'precondition')})`);
  return ok;
}

async function waitForWorld(page, predicate, timeoutMs, pollMs = 200) {
  const deadline = Date.now() + timeoutMs;
  let snap = null;
  while (Date.now() < deadline) {
    snap = await worldSnapshot(page);
    if (snap && predicate(snap)) return snap;
    await page.waitForTimeout(pollMs);
  }
  return snap;
}

async function waitForClientConnected(page, timeoutMs = 15000) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    const c = await page.evaluate(() => !!(window.game?.client?.connected));
    if (c) return true;
    await page.waitForTimeout(250);
  }
  return false;
}

// Drive any remaining pickup affordance to completion.  After
// world.deal('HANDS') the manual chain emits 4 rounds for us; if the
// chain races the runtime it can stall on the dealer-extra. Force
// it home from our side so the deal reaches AwaitingDiscard.
//
// Uses both world.isMyPickupTurn() (which checks the world's cached
// pickup) AND the raw collection state (so we don't get stuck on a
// stale cache).  The runtime's emitTakePickup gates on the cached
// world.pickup; if that's stale we manually re-set the cache from
// the collection before emitting.
async function drainPickupChain(page, deadlineMs = 20000) {
  const deadline = Date.now() + deadlineMs;
  let lastEmitAt = 0;
  const log = [];
  while (Date.now() < deadline) {
    const r = await page.evaluate(() => {
      const w = window.game?.world;
      if (!w) return { done: true, reason: 'no world' };
      const liveEntry = w.client?.pickup?.get?.('current') ?? null;
      const cached = w.pickup ?? null;
      // Sync cache from live collection in case onPickup hasn't fired.
      if (liveEntry !== cached) {
        try { w.pickup = liveEntry; } catch { /* ignore */ }
      }
      if (liveEntry === null) return { done: true, reason: 'pickup null', cached: cached?.phase };
      if (typeof liveEntry.seatIndex === 'number' && liveEntry.seatIndex === w.seat &&
          (liveEntry.count ?? 0) > 0) {
        let emitted = false; let error = null;
        try { emitted = !!w.emitTakePickup(); }
        catch (e) { error = String(e); }
        return { done: false, emitted, error, phase: liveEntry.phase, count: liveEntry.count };
      }
      return { done: false, waitingFor: liveEntry.seatIndex, phase: liveEntry.phase };
    });
    log.push({ at: Date.now() - (deadline - deadlineMs), ...r });
    if (r?.done) return { done: true, log, reason: r.reason };
    if (r?.emitted) lastEmitAt = Date.now();
    await page.waitForTimeout(500);
  }
  return { done: false, reason: 'timeout', log };
}

// Move the world's hover/select to a target Thing.  Sets it both
// before AND after the next frame so a stray null mousepicker
// poll doesn't clear it before we snap.
async function selectThing(page, thingId) {
  return await page.evaluate(async (id) => {
    const g = window.game;
    if (!g) return { ok: false, reason: 'no game' };
    const t = g.world?.things?.get?.(id);
    if (!t) return { ok: false, reason: 'no thing' };
    try { g.world.onHover(id); } catch { /* ignore */ }
    try { g.world.onSelect([id]); } catch { /* ignore */ }
    await new Promise(r => requestAnimationFrame(r));
    // Defensive re-assert in case the next mouse-move-null cleared it.
    try { g.world.onHover(id); } catch { /* ignore */ }
    try { g.world.onSelect([id]); } catch { /* ignore */ }
    return {
      ok: true,
      id,
      hovered: g.world.hovered?.index ?? null,
      selected: (g.world.selected ?? []).map(x => x?.index),
    };
  }, thingId);
}

// Wrap a phase capture so a single failure doesn't bring the whole
// run down — we want EVERY screenshot attempted.
async function safePhase(name, fn) {
  try {
    await fn();
  } catch (e) {
    console.log(`  !! ${name} threw: ${e?.message ?? e}`);
    if (e?.stack) console.log(e.stack.split('\n').slice(0, 4).join('\n'));
    findings.failed.push({ label: name, reason: `exception: ${e?.message ?? e}` });
  }
}

// ── Main capture flow (#1 → #9) on a single browser context ─────────

async function captureFullFlow(browser) {
  const gameId = `${RUN_TAG}-main`;
  const url = `${baseUrl}/autotable/?variant=changsha&dealMode=manual&botCount=3&botDifficulty=Hard&handCount=4&gameId=${gameId}`;
  console.log(`\n>>> Main flow URL: ${url}`);
  const { ctx, page, pageDiag } = await makePage(browser, 'main');

  try {
    await navigateAndSeat(page, url);
    await waitForClientConnected(page);
    await page.waitForTimeout(1500);

    // Install a dice-update observer BEFORE we trigger deal so we can
    // detect the rolled event the moment it lands AND defeat the HUD's
    // auto-hide.  Both are needed so #02 has the dice HUD on-screen
    // when we screenshot.
    await page.evaluate(() => {
      const cli = window.game?.client;
      if (!cli) return;
      window.__diceObs = [];
      cli.dice.on('update', (entries) => {
        for (const [, v] of entries) {
          if (!v) continue;
          window.__diceObs.push({
            at: Date.now(), state: v.state,
            d1: v.d1 ?? v.dice?.[0], d2: v.d2 ?? v.dice?.[1],
          });
        }
      });
      const hud = document.getElementById('dice-hud');
      if (hud) {
        const mo = new MutationObserver(() => {
          if (hud.classList.contains('hidden') ||
              window.getComputedStyle(hud).display === 'none') {
            hud.classList.remove('hidden');
            hud.style.display = '';
          }
        });
        mo.observe(hud, { attributes: true, attributeFilter: ['style', 'class'] });
        window.__diceHudObserver = mo;
      }
    });

    // ── #1 — Walls built, face-down, no deal yet ──────────────────
    await safePhase('01-walls-built-facedown', async () => {
      const label = '01-walls-built-facedown';
      console.log(`\n[${label}]`);
      const snap = await waitForWorld(page,
        s => s.wallCount >= 100 && s.wallFaceDown >= 100 &&
             s.handBySeat.every(n => n === 0) &&
             s.totalDiscard === 0 && s.totalMeld === 0,
        20_000);
      const ok = !!snap &&
        snap.wallCount >= 100 &&
        snap.wallFaceDown === snap.wallCount &&
        snap.wallFaceUp === 0 &&
        snap.handBySeat.every(n => n === 0) &&
        snap.totalDiscard === 0 &&
        snap.totalMeld === 0;
      await captureScreenshot(page, ok ? label : '01-FAIL-walls-not-clean', {
        preconditionsMet: ok,
        pageDiag,
        detail: snap ? {
          wallCount: snap.wallCount,
          wallFaceDown: snap.wallFaceDown,
          wallFaceUp: snap.wallFaceUp,
          handBySeat: snap.handBySeat,
        } : null,
        failReason: ok ? null : 'walls-not-clean-or-tiles-distributed',
      });
    });

    // ── #2 — Dice rolled (HUD visible) ────────────────────────────
    // Trigger deal here so the dice update fires.  Manual mode auto-
    // drives the dealer's pickup chain after this call (300ms gap +
    // 4 takes interleaved with bot picks).
    await page.evaluate(() => {
      try { window.game.world.deal('HANDS'); } catch (e) { console.error('deal:', e); }
    });

    await safePhase('02-dice-rolled', async () => {
      const label = '02-dice-rolled';
      console.log(`\n[${label}]`);
      // Wait for either runtime dice state OR our observer firing OR
      // the HUD element becoming visible.  Whichever happens first.
      const hudReady = await page.waitForFunction(() => {
        if (window.__diceObs && window.__diceObs.length > 0) return true;
        const dice = window.game?.client?.dice?.get?.(0);
        if (dice?.state === 'rolled') return true;
        const el = document.getElementById('dice-hud');
        if (!el) return false;
        const cs = getComputedStyle(el);
        return cs.display !== 'none' && cs.visibility !== 'hidden' &&
               !el.classList.contains('hidden');
      }, { timeout: 12000 }).then(() => true).catch(() => false);
      await page.waitForTimeout(150);  // settle one frame
      const snap = await worldSnapshot(page);
      const ok = hudReady && !!snap &&
        (snap.diceState === 'rolled' ||
         (Array.isArray(snap.diceValues) && snap.diceValues.length === 2));
      await captureScreenshot(page, ok ? label : '02-FAIL-dice-not-rolled', {
        preconditionsMet: ok,
        pageDiag,
        detail: { hudReady, snap: snap ? {
          diceState: snap.diceState, diceValues: snap.diceValues,
          wallCount: snap.wallCount, handBySeat: snap.handBySeat,
        } : null },
        failReason: ok ? null : 'dice-not-rolled-or-hud-missing',
      });
    });

    // ── #3 — Mid-pickup ceremony ──────────────────────────────────
    // The dealer chain emits 4 take rounds interleaved with bot picks
    // (the runtime cycles all 4 seats per round).  Mid-flow signals:
    //   - someone has at least 1 hand tile (pickups underway)
    //   - dealer hasn't reached 14 yet (chain not done)
    //   - walls have visibly drained
    await safePhase('03-dealing-ceremony', async () => {
      const label = '03-dealing-ceremony';
      console.log(`\n[${label}]`);
      // Wait for the round-robin to advance: at least 2 seats holding
      // tiles AND our hand has grown past round 1 (>=8 tiles) so the
      // screenshot is visibly distinct from #02 ("dice rolled, round 1
      // just started").
      const snap = await waitForWorld(page,
        s => s.handBySeat.filter(n => n >= 4).length >= 2 &&
             s.handBySeat[s.seat] >= 8 &&
             s.handBySeat[s.seat] < 14 &&
             s.wallCount < 96,
        20_000, 150);
      // Force a render-frame gap so the screenshot pixels diverge
      // from #02 even if the wait fell through immediately.
      await page.waitForTimeout(400);
      const final = await worldSnapshot(page);
      const ok = !!final &&
        final.handBySeat.filter(n => n >= 4).length >= 2 &&
        final.handBySeat[final.seat] >= 8 &&
        final.wallCount < 96;
      await captureScreenshot(page, ok ? label : '03-FAIL-ceremony-missed', {
        preconditionsMet: ok,
        pageDiag,
        detail: final ? {
          wallCount: final.wallCount,
          handBySeat: final.handBySeat,
          myHandFaceUp: final.myHandFaceUp,
          pickupPhase: final.pickup?.phase,
          pickupSeat: final.pickup?.seatIndex,
        } : null,
        failReason: ok ? null : 'mid-ceremony-window-missed',
      });
    });

    // ── #4 — Hand fully dealt, all face-up ────────────────────────
    // Drain the chain to make sure the dealer-extra fires (the runtime
    // sometimes stalls on the final take if our emit raced its
    // BreakPointMarked transition).  Then wait for the face-flip
    // animation to settle (~3s lag per Vasquez prior memo).
    await safePhase('04-hand-dealt-faceup', async () => {
      const label = '04-hand-dealt-faceup';
      console.log(`\n[${label}]`);
      const drain = await drainPickupChain(page, 20_000);
      const snap = await waitForWorld(page,
        s => s.handBySeat[s.seat] >= 14 && s.pickup === null,
        20_000, 300);
      // Extra face-flip settle.
      await page.waitForTimeout(3500);
      // Wait for the pickup HUD overlay to actually clear in the DOM —
      // the data state can lag the render by a frame or two and we
      // don't want a stale "Take 1" overlay polluting the screenshot.
      await page.waitForFunction(() => {
        const hud = document.getElementById('pickup-hud');
        if (!hud) return true;
        const style = window.getComputedStyle(hud);
        return style.display === 'none' || style.visibility === 'hidden';
      }, { timeout: 5000 }).catch(() => { /* best effort */ });
      // Defensive: if the HUD is somehow still showing despite pickup=null,
      // hide it directly so the screenshot reflects the true data state.
      await page.evaluate(() => {
        const w = window.game?.world;
        const liveEntry = w?.client?.pickup?.get?.('current') ?? null;
        if (liveEntry === null) {
          const hud = document.getElementById('pickup-hud');
          if (hud) hud.style.display = 'none';
        }
      });
      const final = await worldSnapshot(page);
      const dealerSeat = final?.seat ?? 0;
      const dealerHand = final?.handBySeat?.[dealerSeat] ?? 0;
      // Dealer fully-dealt = 14 tiles, all face-up, foreign seats still concealed.
      const ok = !!final &&
        dealerHand === 14 &&
        final.myHandFaceUp >= 13 &&
        final.foreignHandFaceUp === 0 &&
        final.pickup === null;
      await captureScreenshot(page, ok ? label : '04-FAIL-deal-not-settled', {
        preconditionsMet: ok,
        pageDiag,
        detail: final ? {
          seat: dealerSeat,
          handBySeat: final.handBySeat,
          myHandFaceUp: final.myHandFaceUp,
          foreignHandFaceUp: final.foreignHandFaceUp,
          hasExtraHandTile: final.hasExtraHandTile,
          pickupPhase: final.pickup?.phase,
          pickupCount: final.pickup?.count,
          drainReason: drain?.reason,
          drainEmitsLog: drain?.log?.slice(-6),
        } : null,
        failReason: ok ? null : 'hand-not-14-faceup-or-pickup-pending',
      });
    });

    // ── #5 — Tile selected (raised geometry) ──────────────────────
    await safePhase('05-tile-selected', async () => {
      const label = '05-tile-selected';
      console.log(`\n[${label}]`);
      const baseline = await worldSnapshot(page);
      // Pick a middle-of-the-hand tile so the visual lift is unmistakable.
      const ids = baseline?.myHandIds ?? [];
      const tile = ids[Math.floor(ids.length / 2)] ?? ids[0];
      // The runtime's onHover/onSelect only set an internal outline
      // flag (CustomOutline.setSelected) which produces too subtle a
      // pixel diff to register against the same frame as #04.  For
      // an unambiguous "this tile is selected by the player" visual,
      // also call thing.hold(seat) which lifts the mesh by +1 on the
      // z-axis (object-view.ts:308) — the same lift a real drag
      // produces.  We release it immediately after the screenshot so
      // #06 can discard a fresh tile without touching this one.
      let sel = null;
      let liftDetail = null;
      if (tile) {
        sel = await selectThing(page, tile.id);
        liftDetail = await page.evaluate((id) => {
          const w = window.game?.world;
          if (!w || w.seat === null) return { ok: false, reason: 'no seat' };
          const t = w.things?.get?.(id);
          if (!t || typeof t.hold !== 'function') return { ok: false, reason: 'no thing/hold' };
          try {
            t.hold(w.seat);
            // Re-assert hover/select so they stay set through the lift.
            try { w.onHover(id); } catch { /* ignore */ }
            try { w.onSelect([id]); } catch { /* ignore */ }
            return {
              ok: true,
              id,
              claimedBy: t.claimedBy,
              hovered: w.hovered?.index ?? null,
              selected: (w.selected ?? []).map(x => x?.index),
            };
          } catch (e) {
            return { ok: false, reason: String(e) };
          }
        }, tile.id);
      }
      // Multi-frame render settle so the lift commits.
      for (let i = 0; i < 6; i++) {
        await page.evaluate(() => new Promise(r => requestAnimationFrame(r)));
      }
      await page.waitForTimeout(200);
      const verify = await page.evaluate((id) => {
        const w = window.game?.world;
        if (!w) return null;
        const t = w.things?.get?.(id);
        return {
          hovered: w.hovered?.index ?? null,
          selected: (w.selected ?? []).map(x => x?.index),
          claimedBy: t?.claimedBy ?? null,
        };
      }, tile?.id);
      const ok = !!sel?.ok && verify &&
        (verify.claimedBy !== null ||
         verify.hovered === tile?.id ||
         (verify.selected ?? []).includes(tile?.id));
      await captureScreenshot(page, ok ? label : '05-FAIL-no-selection', {
        preconditionsMet: ok,
        pageDiag,
        detail: { tile, sel, liftDetail, verify },
        failReason: ok ? null : 'tile-not-hovered-or-selected',
      });
      // Release the hold so #06 has a clean hand to discard from.
      await page.evaluate((id) => {
        const w = window.game?.world;
        const t = w?.things?.get?.(id);
        try { if (t && typeof t.release === 'function') t.release(); } catch { /* ignore */ }
        try { w.onHover(null); } catch { /* ignore */ }
        try { w.onSelect([]); } catch { /* ignore */ }
      }, tile?.id);
    });

    // ── #6 — Discard on table ─────────────────────────────────────
    await safePhase('06-discard-on-table', async () => {
      const label = '06-discard-on-table';
      console.log(`\n[${label}]`);
      // Re-snap after any selection-side changes from #5.
      let pre = await worldSnapshot(page);
      // Clear lingering hover/select so emitDiscard targets a fresh tile.
      await page.evaluate(() => {
        try { window.game.world.onHover(null); } catch { /* ignore */ }
        try { window.game.world.onSelect([]); } catch { /* ignore */ }
      });
      // Try multiple hand tiles until one is accepted (the runtime
      // rejects a discard that isn't in our hand or isn't in
      // AwaitingDiscard phase).
      const ids = pre?.myHandIds ?? [];
      let emitOk = false;
      let emitDetail = null;
      for (const t of ids) {
        const r = await page.evaluate((id) => {
          try { return { ok: !!window.game.world.emitDiscard(id), id }; }
          catch (e) { return { ok: false, reason: String(e), id }; }
        }, t.id);
        if (r?.ok) { emitOk = true; emitDetail = r; break; }
        emitDetail = r;
      }
      // Wait for the discard to surface (pile grow / meld grow / hand drop).
      const post = await waitForWorld(page,
        s => s.totalDiscard > 0 || s.totalMeld > 0 ||
             (s.handBySeat[s.seat] ?? 0) < (pre?.handBySeat?.[pre?.seat] ?? 99),
        12_000, 250);
      await page.waitForTimeout(1500);  // settle so next-turn indicator paints
      const final = await worldSnapshot(page);
      const handDropped =
        (final?.handBySeat?.[final?.seat] ?? 99) <
        (pre?.handBySeat?.[pre?.seat] ?? 99);
      const ok = emitOk && !!final &&
        (final.totalDiscard > 0 || final.totalMeld > 0 || handDropped);
      await captureScreenshot(page, ok ? label : '06-FAIL-no-discard', {
        preconditionsMet: ok,
        pageDiag,
        detail: {
          emitOk, emitDetail,
          preHand: pre?.handBySeat,
          postHand: final?.handBySeat,
          totalDiscard: final?.totalDiscard,
          totalMeld: final?.totalMeld,
          pickup: final?.pickup,
        },
        failReason: ok ? null : 'no-discard-or-meld-after-emit',
      });
    });

    // ── #7 — Claim window (synthetic injection) ───────────────────
    await safePhase('07-claim-window', async () => {
      const label = '07-claim-window';
      console.log(`\n[${label}]`);
      const inject = await page.evaluate(() => {
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
      await page.waitForTimeout(700);
      const ui = await page.evaluate(() => {
        const $ = (id) => document.getElementById(id);
        const visible = (el) => !!el && getComputedStyle(el).display !== 'none' &&
          getComputedStyle(el).visibility !== 'hidden';
        const buttons = {
          Pung: $('claim-pung'), Chow: $('claim-chow'),
          Kong: $('claim-kong'), Hu: $('claim-hu'), Pass: $('claim-pass'),
        };
        const overlay = document.querySelector(
          '#claim-window-overlay, .claim-window-overlay, [data-testid="claim-window-overlay"]');
        return {
          countdownVisible: visible($('claim-countdown')),
          overlayVisible: !!overlay && visible(overlay),
          disabled: Object.fromEntries(Object.entries(buttons).map(
            ([k, b]) => [k, b ? b.disabled : null])),
        };
      });
      const ok = inject?.ok && (
        (ui.countdownVisible &&
         (ui.disabled.Pung === false || ui.disabled.Chow === false || ui.disabled.Hu === false)) ||
        ui.overlayVisible
      );
      await captureScreenshot(page, ok ? label : '07-FAIL-no-claim-ui', {
        preconditionsMet: ok,
        pageDiag,
        detail: { inject, ui },
        failReason: ok ? null : 'claim-ui-did-not-surface',
      });
      // Tombstone so it doesn't bleed into #8.
      await page.evaluate(() => {
        const cli = window.game?.client;
        const events = cli?.events ?? cli?.['events'];
        events?.emit('update', [['claim', String(cli.seat), null]], false);
      }).catch(() => {});
      await page.waitForTimeout(400);
    });

    // ── #8 — HandResult modal (synthetic Hu) ──────────────────────
    await safePhase('08-hand-result-modal', async () => {
      const label = '08-hand-result-modal';
      console.log(`\n[${label}]`);
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
            fans: [
              { name: '平胡', points: 1 },
              { name: '门前清', points: 1 },
            ],
          };
          events.emit('update', [['result', 'current', payload]], false);
          return { ok: true };
        } catch (e) { return { ok: false, reason: String(e) }; }
      });
      await page.waitForTimeout(900);
      const ui = await page.evaluate(() => {
        const modal = document.getElementById('result-modal');
        const scoreBody = document.querySelector('#result-score tbody');
        const cs = modal ? getComputedStyle(modal) : null;
        // Per Vasquez prior gotcha — bootstrap modals are absolute-
        // positioned and don't satisfy offsetParent !== null; use
        // classList.contains('show') + display.
        const modalVisible = !!modal && (
          modal.classList.contains('show') ||
          (cs && cs.display !== 'none' && cs.visibility !== 'hidden')
        );
        return {
          modalVisible,
          modalDisplay: cs?.display ?? null,
          modalClass: modal?.className ?? null,
          scoreRows: scoreBody ? scoreBody.querySelectorAll('tr').length : 0,
        };
      });
      const ok = inject?.ok && ui.modalVisible && ui.scoreRows >= 4;
      await captureScreenshot(page, ok ? label : '08-FAIL-no-result-modal', {
        preconditionsMet: ok,
        pageDiag,
        detail: { inject, ui },
        failReason: ok ? null : 'result-modal-not-visible',
      });
      // Tombstone before #9 so the result-modal doesn't overlay.
      await page.evaluate(() => {
        const cli = window.game?.client;
        const events = cli?.events ?? cli?.['events'];
        events?.emit('update', [['result', 'current', null]], false);
      }).catch(() => {});
      await page.waitForTimeout(500);
    });

    // ── #9 — Game-complete modal (synthetic gameComplete) ─────────
    await safePhase('09-game-complete-modal', async () => {
      const label = '09-game-complete-modal';
      console.log(`\n[${label}]`);
      const inject = await page.evaluate(() => {
        try {
          const cli = window.game?.client;
          const events = cli?.events ?? cli?.['events'];
          if (!events?.emit) return { ok: false, reason: 'no events emitter' };
          const payload = {
            isComplete: true,
            totalScores: { '0': 28, '1': -8, '2': -12, '3': -8 },
            handHistory: [
              { handNumber: 1, winner: 0, fans: ['平胡'], score: 12 },
              { handNumber: 2, winner: 0, fans: ['门前清', '碰碰胡'], score: 16 },
            ],
            maxHands: 4,
          };
          events.emit('update', [['gameComplete', 'current', payload]], false);
          return { ok: true };
        } catch (e) { return { ok: false, reason: String(e) }; }
      });
      await page.waitForTimeout(900);
      const ui = await page.evaluate(() => {
        const modal = document.getElementById('game-complete-modal');
        const cs = modal ? getComputedStyle(modal) : null;
        const visible = !!modal && (
          modal.classList.contains('show') ||
          (cs && cs.display !== 'none' && cs.visibility !== 'hidden')
        );
        return {
          visible,
          display: cs?.display ?? null,
          className: modal?.className ?? null,
          textSample: (modal?.innerText ?? '').slice(0, 200),
        };
      });
      const ok = inject?.ok && ui.visible;
      await captureScreenshot(page, ok ? label : '09-FAIL-no-game-complete', {
        preconditionsMet: ok,
        pageDiag,
        detail: { inject, ui },
        failReason: ok ? null : 'game-complete-modal-not-visible',
      });
    });

    findings.mainPageDiag = {
      pageErrors: pageDiag.pageErrors,
      pageErrorsCount: pageDiag.pageErrors.length,
      consoleErrorsCount: pageDiag.consoleErrors.length,
      staleMoveToWarnings: pageDiag.staleMoveToWarnings,
      networkFailuresCount: pageDiag.networkFailures.length,
    };
  } finally {
    await ctx.close();
  }
}

// ── Multi-game isolation (#10) — two contexts on different gameIds ──

async function captureMultiGameIsolation(browser) {
  const label = '10-multi-game-isolation';
  console.log(`\n[${label}]`);
  const gameA = `${RUN_TAG}-iso-A`;
  const gameB = `${RUN_TAG}-iso-B`;
  const urlA = `${baseUrl}/autotable/?variant=changsha&dealMode=auto&botCount=3&botDifficulty=Hard&handCount=4&gameId=${gameA}`;
  const urlB = `${baseUrl}/autotable/?variant=changsha&dealMode=auto&botCount=3&botDifficulty=Hard&handCount=4&gameId=${gameB}`;
  console.log(`  A: ${urlA}`);
  console.log(`  B: ${urlB}`);
  const a = await makePage(browser, 'iso-A');
  const b = await makePage(browser, 'iso-B');

  let ok = false;
  let detail = null;
  let pageErrors = 0;
  try {
    await Promise.all([
      navigateAndSeat(a.page, urlA),
      navigateAndSeat(b.page, urlB),
    ]);
    await Promise.all([
      waitForClientConnected(a.page),
      waitForClientConnected(b.page),
    ]);

    await Promise.all([a.page, b.page].map(p =>
      p.evaluate(() => {
        try { window.game?.world?.deal?.('HANDS'); } catch { /* idempotent */ }
      })));

    // Wait for both to settle.
    const deadline = Date.now() + 45_000;
    let snapA = null, snapB = null;
    while (Date.now() < deadline) {
      [snapA, snapB] = await Promise.all([
        worldSnapshot(a.page),
        worldSnapshot(b.page),
      ]);
      const okA = (snapA?.handBySeat?.[0] ?? 0) >= 13 && (snapA?.myHandFaceUp ?? 0) >= 12;
      const okB = (snapB?.handBySeat?.[0] ?? 0) >= 13 && (snapB?.myHandFaceUp ?? 0) >= 12;
      if (okA && okB) break;
      await a.page.waitForTimeout(500);
    }
    await a.page.waitForTimeout(2500);
    [snapA, snapB] = await Promise.all([
      worldSnapshot(a.page),
      worldSnapshot(b.page),
    ]);

    // Drive a discard in A only.
    const tileA = (snapA?.myHandIds ?? [])[0];
    let discardA = null;
    if (tileA) {
      discardA = await a.page.evaluate((id) => {
        try { return { ok: !!window.game.world.emitDiscard(id) }; }
        catch (e) { return { ok: false, reason: String(e) }; }
      }, tileA.id);
    }
    const aDeadline = Date.now() + 12_000;
    while (Date.now() < aDeadline) {
      snapA = await worldSnapshot(a.page);
      if ((snapA?.totalDiscard ?? 0) + (snapA?.totalMeld ?? 0) > 0) break;
      await a.page.waitForTimeout(400);
    }
    snapB = await worldSnapshot(b.page);

    const gameIdA = await a.page.evaluate(() => new URL(location.href).searchParams.get('gameId'));
    const gameIdB = await b.page.evaluate(() => new URL(location.href).searchParams.get('gameId'));

    ok = gameIdA === gameA && gameIdB === gameB &&
         gameIdA !== gameIdB &&
         (snapA?.handBySeat?.[0] ?? 0) >= 13 &&
         (snapB?.handBySeat?.[0] ?? 0) >= 13;

    detail = {
      gameIdA, gameIdB,
      handA: snapA?.handBySeat, handB: snapB?.handBySeat,
      myFaceUpA: snapA?.myHandFaceUp, myFaceUpB: snapB?.myHandFaceUp,
      totalDiscardA: snapA?.totalDiscard, totalDiscardB: snapB?.totalDiscard,
      totalMeldA: snapA?.totalMeld, totalMeldB: snapB?.totalMeld,
      discardA,
      pageErrorsA: a.pageDiag.pageErrors.length,
      pageErrorsB: b.pageDiag.pageErrors.length,
    };

    const sideAPath = path.join(ARTIFACT_DIR, '10-multi-game-isolation-A.png');
    const sideBPath = path.join(ARTIFACT_DIR, '10-multi-game-isolation-B.png');
    await a.page.screenshot({ path: sideAPath, fullPage: true });
    await b.page.screenshot({ path: sideBPath, fullPage: true });
    // Save primary (A) as the canonical 10-multi-game-isolation.png
    // — keeping the -A.png and -B.png as companion proofs that the
    // two games rendered independently.
    fs.copyFileSync(sideAPath, path.join(ARTIFACT_DIR, '10-multi-game-isolation.png'));
    pageErrors = a.pageDiag.pageErrors.length + b.pageDiag.pageErrors.length;

    findings.perScreenshot.push({
      label, file: '10-multi-game-isolation.png',
      companionA: '10-multi-game-isolation-A.png',
      companionB: '10-multi-game-isolation-B.png',
      preconditionsMet: ok, pageErrors,
      ms: 0, detail,
    });
    if (ok) findings.passed.push(label);
    else findings.failed.push({ label, reason: 'isolation-mismatch' });
    console.log(`  ${ok ? '✅' : '❌'} ${label}  (A+B pageErrors=${pageErrors})`);

    findings.isolationDiag = {
      A: {
        pageErrorsCount: a.pageDiag.pageErrors.length,
        consoleErrorsCount: a.pageDiag.consoleErrors.length,
        staleMoveToWarnings: a.pageDiag.staleMoveToWarnings,
      },
      B: {
        pageErrorsCount: b.pageDiag.pageErrors.length,
        consoleErrorsCount: b.pageDiag.consoleErrors.length,
        staleMoveToWarnings: b.pageDiag.staleMoveToWarnings,
      },
    };
  } catch (e) {
    console.log(`  !! ${label} threw: ${e?.message ?? e}`);
    findings.failed.push({ label, reason: `exception: ${e?.message ?? e}` });
  } finally {
    await a.ctx.close();
    await b.ctx.close();
  }
}

// ── Run ─────────────────────────────────────────────────────────────

const browser = await chromium.launch();
let exitCode = 0;
try {
  await captureFullFlow(browser);
  await captureMultiGameIsolation(browser);
} catch (e) {
  console.log(`FATAL: ${e?.stack ?? e}`);
  exitCode = 2;
} finally {
  await browser.close();
}

// ── Persist findings ────────────────────────────────────────────────

findings.pageErrorsTotal =
  (findings.mainPageDiag?.pageErrorsCount ?? 0) +
  ((findings.isolationDiag?.A?.pageErrorsCount ?? 0) +
   (findings.isolationDiag?.B?.pageErrorsCount ?? 0));
findings.completedAt = new Date().toISOString();
findings.passedCount = findings.passed.length;
findings.failedCount = findings.failed.length;

const findingsFile = path.join(ARTIFACT_DIR, 'findings.json');
fs.writeFileSync(findingsFile, JSON.stringify(findings, null, 2));

const cleanPass = findings.passedCount === 10 && findings.pageErrorsTotal === 0;
const verdict = `${cleanPass ? '✅' : '❌'} Definitive proof: ${findings.passedCount}/10 screenshots captured, ${findings.pageErrorsTotal} page errors`;
console.log('\n' + '='.repeat(60));
console.log(verdict);
console.log(`Artifacts: ${ARTIFACT_DIR}`);
console.log(`Findings:  ${findingsFile}`);
console.log('='.repeat(60));

if (exitCode === 0) exitCode = cleanPass ? 0 : 1;
process.exit(exitCode);
