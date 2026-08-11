// NEW-2 (parent anti-happy-path, formalized by Ripley 2026-08-07). The manual
// pickup ceremony must be HUMAN-DRIVEN: the viewer must real-press targetSlots[0]
// to take EACH batch, and the ceremony must make NO progress absent a press. The
// SC-4 single-trigger interaction is currently DEAD CODE — world.ts
// driveManualDealChain auto-rolls + auto-takes, so the hand self-fills to 14 with
// ZERO human presses (proven live: S13 in changsha-manual-pickup-endpoint-only).
//
// This is the anti-happy-path partner of D2/NEW-1 (changsha-manual-deal-
// orchestration): NEW-1 proves a NON-DEALER human is not stalled by an unscheduled
// bot-dealer roll; NEW-2 proves the pickup INTERACTION itself is real (a human
// press moves each batch and nothing moves without one). We isolate NEW-2 at the
// DEALER seat (seat 0) ON PURPOSE: @200cad4 a non-dealer stalls in RollingDice
// (that is NEW-1's defect) and never reaches a pickup window, which would MASK the
// auto-drive. Seat 0's ceremony opens, so the auto-drive (or, post-fix, the human
// press) is directly observable. Dealer completes to 14 tiles.
//
// Acceptance (GREEN after Bishop ships pickup.targetSlots len-1 + Hicks/world.ts
// removes the client auto-drive):
//   (1) window opens with a single-trigger designation; the hand does NOT climb on
//       its own (no auto-advance) — RED@200cad4: hand auto-fills to 14 unpressed.
//   (2) each real press on targetSlots[0] takes EXACTLY that batch's `count` and
//       then STOPS — no auto-advance to the next batch until the next press.
//   (3) the dealer reaches 14 THROUGH discrete human presses (>=1) — RED@200cad4:
//       0 presses are possible (no targetSlots to click); the 14 is auto-driven,
//       so we assert completion-via-presses, never a bare hand>=13 (which the
//       auto-drive would falsely satisfy).
//
// Discipline: genuine rendered controls only — real-pointer clicks projected onto
// the reachable top wall tile (up-link empty). No emitDiscard, no synthetic
// pickup.take, no collection injection, no direct roll API (roll is a real control
// click if rendered; @200cad4 the auto-drive rolls, so it is best-effort).

import { test, expect, type Page } from '@playwright/test';
import { buildGameUrl, makeConfig, dismissLobbyAndTour, ensureConnected, takeSeatByClick, clickDeal } from './_playability';
import { recordEvidence, shot } from './_uat_red';

// Reads the viewer's current single-trigger pickup designation (SC-4 v4:
// pickup.targetSlots — a public exposed-front slot NAME, length EXACTLY 1). Returns
// null when there is no designation targeted at the local seat.
async function readDesignation(page: Page): Promise<{ phase: string; count: number; gate: string[]; gateLen: number } | null> {
  return page.evaluate(() => {
    /* eslint-disable @typescript-eslint/no-explicit-any */
    const g = (window as any).game;
    const pu = g?.client?.pickup?.get ? g.client.pickup.get('current') : null;
    if (!pu || pu.seatIndex !== g?.client?.seat) return null;
    const gate: string[] | null = Array.isArray((pu as any).targetSlots) ? (pu as any).targetSlots.map(String) : null;
    if (!gate || gate.length === 0) return null;
    return { phase: pu.phase, count: pu.count, gate, gateLen: gate.length };
    /* eslint-enable @typescript-eslint/no-explicit-any */
  });
}

// Own-hand tile count (hand.*@0) — the local seat is 0 in these runs.
async function handCount(page: Page): Promise<number> {
  return page.evaluate(() => {
    /* eslint-disable @typescript-eslint/no-explicit-any */
    const w = (window as any).game?.world; let hand = 0;
    if (w?.things) for (const t of w.things.values()) if (/^hand\.\d+@0$/.test(String(t?.slot?.name ?? ''))) hand++;
    return hand;
    /* eslint-enable @typescript-eslint/no-explicit-any */
  });
}

// Sample the hand count over `ms` WITHOUT any input; returns the net climb.
async function handClimbOver(page: Page, ms: number): Promise<{ start: number; end: number; climb: number }> {
  const start = await handCount(page);
  await page.waitForTimeout(ms);
  const end = await handCount(page);
  return { start, end, climb: end - start };
}

// Real-pointer press on the REACHABLE top wall tile whose public slot name is
// gate[0] (up-link empty ⇒ not occluded). Projects the tile's world position to
// screen and issues a genuine mouse down/up. Returns the hand delta.
async function pressDesignationTop(page: Page, gate: string[]): Promise<{ clicked: boolean; handBefore: number; handAfter: number }> {
  const target = await page.evaluate((values) => {
    /* eslint-disable @typescript-eslint/no-explicit-any */
    const g = (window as any).game; const w = g?.world; const camera = g?.mainView?.camera; const main = document.getElementById('main');
    const rect = main?.getBoundingClientRect();
    if (camera) { try { camera.parent?.updateMatrixWorld(true); camera.updateMatrixWorld(true); camera.matrixWorldInverse?.copy(camera.matrixWorld).invert(); } catch { /* */ } }
    const set = new Set(values.map(String));
    const proj = (p: any) => { const mw = camera.matrixWorldInverse.elements, pm = camera.projectionMatrix.elements; const vx = mw[0]*p.x+mw[4]*p.y+mw[8]*p.z+mw[12], vy = mw[1]*p.x+mw[5]*p.y+mw[9]*p.z+mw[13], vz = mw[2]*p.x+mw[6]*p.y+mw[10]*p.z+mw[14], vw = mw[3]*p.x+mw[7]*p.y+mw[11]*p.z+mw[15]; const cx = pm[0]*vx+pm[4]*vy+pm[8]*vz+pm[12]*vw, cy = pm[1]*vx+pm[5]*vy+pm[9]*vz+pm[13]*vw, cw = pm[3]*vx+pm[7]*vy+pm[11]*vz+pm[15]*vw; return { sx: (rect?.left??0)+(cx/cw+1)*0.5*(rect?.width??0), sy: (rect?.top??0)+(1-cy/cw)*0.5*(rect?.height??0) }; };
    let hand = 0; for (const t of w.things.values()) if (/^hand\.\d+@0$/.test(String(t?.slot?.name ?? ''))) hand++;
    for (const t of w.things.values()) {
      if (t?.slot?.group !== 'wall' || t.claimedBy != null) continue;
      const up = t.slot?.links?.up; if (up && up.thing) continue;   // occluded bottoms are inert
      if (!set.has(String(t.slot?.name))) continue;
      const s = proj(t.place().position);
      return { ok: true, cx: Math.round(s.sx), cy: Math.round(s.sy), handBefore: hand };
    }
    return { ok: false, handBefore: hand };
    /* eslint-enable @typescript-eslint/no-explicit-any */
  }, gate);
  if (!target.ok) return { clicked: false, handBefore: target.handBefore, handAfter: target.handBefore };
  await page.mouse.move(target.cx, target.cy); await page.waitForTimeout(120);
  await page.mouse.down(); await page.waitForTimeout(80); await page.mouse.up();
  await page.waitForTimeout(1200);
  const handAfter = await handCount(page);
  return { clicked: true, handBefore: target.handBefore, handAfter };
}

// Best-effort real-pointer roll for the dealer, if a roll control is rendered.
// @200cad4 the client auto-drive rolls, so this is not required for the RED; it
// exists so the same test drives cleanly once the auto-drive is removed (GREEN).
async function tryRealRoll(page: Page): Promise<void> {
  const roll = page.locator('#roll-dice, button:has-text("Roll")').first();
  if (await roll.isVisible().catch(() => false)) {
    await roll.click({ timeout: 2000 }).catch(() => {});
  }
}

test.describe('NEW-2 human-driven pickup — real press per batch; no progress absent a press', () => {
  test('dealer completes the manual ceremony ONLY through discrete real presses on targetSlots[0]; the hand never auto-advances', async ({ page }, testInfo) => {
    testInfo.setTimeout(150_000);
    await page.setViewportSize({ width: 1600, height: 900 });
    const base = testInfo.project.use.baseURL as string;
    const cfg = makeConfig({ gameId: `new2-humandrive-${Date.now()}`, seat: 0, dealMode: 'manual', botCount: 3, botDifficulty: 'Medium', handCount: 4 });
    await page.goto(buildGameUrl(base, cfg), { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(1200); await dismissLobbyAndTour(page); await ensureConnected(page);
    await takeSeatByClick(page, 0); await clickDeal(page).catch(() => {});
    await tryRealRoll(page);

    // PHASE 1 — no auto-advance: the window opens but the hand must NOT climb on its
    // own. @200cad4 the client auto-drive self-fills the hand (S13 observed 14),
    // so this climb is > 0 (RED). GREEN: climb === 0 (a designation is present and
    // waits for the human).
    const preDesignation = await readDesignation(page);
    const noPress = await handClimbOver(page, 6000);

    // PHASE 2 — per-batch human drive: press targetSlots[0] for each batch; each
    // press must take EXACTLY that batch's `count`, then STOP until the next press.
    let presses = 0;
    let allBatchesExact = true;             // every press moved exactly `count`
    let anyBetweenBatchAutoAdvance = false; // hand moved between presses w/o input
    let sawAnyDesignation = !!preDesignation;
    const batches: Array<{ phase: string; count: number; delta: number; betweenClimb: number }> = [];
    for (let i = 0; i < 8; i++) {
      const d = await readDesignation(page);
      if (!d) break;
      sawAnyDesignation = true;
      const before = await handCount(page);
      if (before >= 14) break;
      const res = await pressDesignationTop(page, d.gate);
      if (!res.clicked) break;
      const delta = res.handAfter - res.handBefore;
      presses++;
      if (delta !== d.count) allBatchesExact = false;
      // after the press, the hand must hold until the NEXT deliberate press
      const between = await handClimbOver(page, 1500);
      if (between.climb > 0) anyBetweenBatchAutoAdvance = true;
      batches.push({ phase: d.phase, count: d.count, delta, betweenClimb: between.climb });
      if (res.handAfter >= 14) break;
    }

    const handFinal = await handCount(page);
    await shot(page, 'new2-human-driven-pickup.png');
    recordEvidence('new2-human-driven-pickup.json', {
      preDesignationPresent: !!preDesignation,
      noPressClimb: noPress, presses, allBatchesExact, anyBetweenBatchAutoAdvance,
      sawAnyDesignation, batches, handFinal,
      note: 'NEW-2 anti-happy-path. RED@200cad4: no pickup.targetSlots is ever shipped (sawAnyDesignation=false ⇒ 0 human presses possible) AND the client auto-drive self-fills the hand to 14 without any press (noPressClimb>0). GREEN when Bishop ships targetSlots len-1 and world.ts drops driveManualDealChain: the window opens (designation present, noPressClimb=0), each real press on targetSlots[0] takes exactly count and stops, and the dealer reaches 14 via >=1 discrete presses.',
    });

    // (1) NO auto-advance without a press — the killer anti-happy-path assertion.
    expect(noPress.climb, `NEW-2(1): the hand must NOT auto-advance without a human press; it climbed ${noPress.climb} (from ${noPress.start} to ${noPress.end}) — RED@200cad4 = client auto-drive self-fills`).toBe(0);
    // (2) a real single-trigger designation must exist for the human to press.
    expect(sawAnyDesignation, 'NEW-2(2): a single-trigger pickup.targetSlots must be shipped for the human to press each batch — RED@200cad4 = no targetSlots designation ever appears').toBe(true);
    // (3) the ceremony was driven by discrete human presses, each taking exactly one batch.
    expect(presses, `NEW-2(3): the dealer ceremony must be driven by >=1 real presses on targetSlots[0]; performed ${presses} — RED@200cad4 = 0 (no clickable designation)`).toBeGreaterThanOrEqual(1);
    expect(allBatchesExact, 'NEW-2(3): every press must take EXACTLY the designated batch count (one interaction per batch)').toBe(true);
    expect(anyBetweenBatchAutoAdvance, 'NEW-2(2): the hand must NOT advance to the next batch between presses (no auto-drive)').toBe(false);
  });
});

// NEW-2b (Ripley 2026-08-07): the pickup HUD TEARDOWN, ruled a VALID RED that is
// DISTINCT from D1. D1 (Vasquez) is the COLLECTION clear — pickup["current"] goes
// null via the post-deal full-snapshot map.clear() — and is a GREEN LOCK (defensive,
// correct @200cad4). But that same map.clear() path fires NO per-key 'update' for
// 'current', so game-ui.ts onPickupUpdate never re-runs renderPickupHud(null), and
// the "Take N" banner (#pickup-hud) ORPHANS on screen while the collection is already
// empty. That lingering banner is a separate FRONTEND UI-teardown defect (routed to
// Hicks): the HUD must hide when rawPickup clears. This gate proves the RENDERED
// result, not the collection (which D1 owns): once pickup["current"] is null, the
// #pickup-hud banner must be gone. RED@200cad4 (collection null BUT banner visible);
// GREEN when the HUD teardown is bound to the clear (or the backend emits the
// EncodePickupCleared that fires an 'update'). A VISIBLE banner at the assertion point
// is intrinsic proof it rendered ⇒ the gate cannot pass vacuously; only a genuine
// teardown (banner hidden) greens it. Discipline: observation only — no emitTakePickup,
// no direct pickup mutation; we read the real DOM + the client's own pickup collection.
test.describe('NEW-2b pickup HUD teardown — the Take-N banner must not orphan after the collection clears', () => {
  test('#pickup-hud is torn down once pickup["current"] clears post-ceremony (no lingering Take-N banner)', async ({ page }, testInfo) => {
    testInfo.setTimeout(150_000);
    await page.setViewportSize({ width: 1600, height: 900 });
    const base = testInfo.project.use.baseURL as string;
    const cfg = makeConfig({ gameId: `new2b-hudteardown-${Date.now()}`, seat: 0, dealMode: 'manual', botCount: 3, botDifficulty: 'Medium', handCount: 4 });
    await page.goto(buildGameUrl(base, cfg), { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(1200); await dismissLobbyAndTour(page); await ensureConnected(page);
    await takeSeatByClick(page, 0); await clickDeal(page).catch(() => {});
    await tryRealRoll(page);

    // Robust visibility read for the position:absolute banner + the client's own
    // authoritative pickup["current"]. visible ⟺ not [hidden], computed display not
    // none, not visibility:hidden, and it actually has layout (rect height > 0).
    const readHud = () => page.evaluate(() => {
      /* eslint-disable @typescript-eslint/no-explicit-any */
      const el = document.getElementById('pickup-hud');
      let visible = false;
      if (el) {
        const cs = getComputedStyle(el); const rect = el.getBoundingClientRect();
        visible = !el.hidden && cs.display !== 'none' && cs.visibility !== 'hidden' && rect.height > 0;
      }
      const g = (window as any).game;
      const pu = g?.client?.pickup?.get ? g.client.pickup.get('current') : null;
      let hand = 0; const w = g?.world; if (w?.things) for (const t of w.things.values()) if (/^hand\.\d+@0$/.test(String(t?.slot?.name ?? ''))) hand++;
      return { present: !!el, visible, pickupCurrent: pu ? { count: pu.count, seatIndex: pu.seatIndex } : null, hand };
      /* eslint-enable @typescript-eslint/no-explicit-any */
    });

    // Drive the ceremony to completion (auto-drive fills the hand @200cad4), sampling
    // the banner so the teardown gate is provably non-vacuous (the banner is expected
    // to appear while the pickup affordance is live).
    let hudEverVisible = false;
    const deadline = Date.now() + 90_000;
    while (Date.now() < deadline) {
      const h = await readHud();
      if (h.visible) hudEverVisible = true;
      if (h.hand >= 13) break;
      await page.waitForTimeout(300);
    }
    // Settle the final post-deal snapshot (the map.clear() clear path).
    await page.waitForTimeout(2500);
    const finalState = await readHud();
    await shot(page, 'new2b-pickup-hud-teardown.png');
    recordEvidence('new2b-pickup-hud-teardown.json', {
      hudEverVisible, finalState,
      note: 'NEW-2b pickup HUD teardown (distinct from D1 collection clear). RED@200cad4: the post-deal full-snapshot map.clear() empties pickup["current"] WITHOUT firing a per-key update, so game-ui.ts renderPickupHud(null) never runs and the #pickup-hud "Take N" banner orphans on screen (finalState.pickupCurrent==null AND finalState.visible==true). GREEN when the HUD teardown is bound to the collection clear (Hicks) or the backend emits EncodePickupCleared that fires the update. A visible banner here is intrinsic proof it rendered — the gate cannot pass vacuously.',
    });

    // Precondition (D1 GREEN LOCK): the collection MUST have cleared post-ceremony, so
    // the ONLY thing under test is the rendered banner teardown.
    expect(finalState.pickupCurrent, `precondition (D1): pickup["current"] must be null post-ceremony so this gate isolates the HUD teardown; got ${JSON.stringify(finalState.pickupCurrent)}`).toBeNull();
    // NEW-2b RED gate: once the collection is empty, the banner must be torn down.
    expect(finalState.visible, `NEW-2b: the #pickup-hud "Take N" banner must be TORN DOWN once pickup["current"] clears — RED@200cad4 the banner orphans (collection null but banner still visible); hudEverVisible=${hudEverVisible}`).toBe(false);
  });
});
