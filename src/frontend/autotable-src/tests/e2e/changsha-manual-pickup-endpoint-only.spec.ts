// G17 (OWNED by hudson-1) — Vasquez R-1 §D10/§B + FINAL SC-4 pickup semantics
// (Ripley PARENT-LOCKED 2026-08-07 11:14, ripley-SC4-FINAL-single-trigger-slot.md;
// supersedes targetHandles + batch-actionable): `pickup.targetSlots: string[]` of
// EXACTLY length 1 = the single exposed-end trigger tile = state.Wall[0] (public
// render-slot name — NO raw tile ids, NO opaque handles). `pickup.count` (4 for
// BreakPointMarked/PickupRound1-3, 1 for SingleTile/DealerExtra) = the SERVER batch.
// ONLY targetSlots[0] is interactable; the other (count-1) batch tiles, any
// `batchPreviewSlots` (display-only), and every other wall tile REJECT (no hover-
// select/hold/drag/mutation/take). A real-pointer click on targetSlots[0] ⇒ the
// server takes the whole batch, and the client's `pickup.take` payload = {seatIndex,
// count} ONLY (zero client tile authority — no id/handle/slot). Assertions:
//   S1 exactly-one-front: targetSlots present, length==1, a real rendered wall
//      slot the viewer sees (the exposed front), count ∈ {1,4};
//   S2 no raw id / no handle leak: the entry is a slot-NAME (public position),
//      never a numeric id and never an opaque handle string;
//   S3 wrong endpoint (a non-front wall tile incl. the other batch tiles) /
//      off-phase / AUTO ⇒ inert (no move/take); S4 the one front slot ⇒ whole
//      batch of `count`, server-confirmed (no optimistic local move);
//   S5 explicit clear tombstones the designation post-batch.
// PHYSICAL-STACK extension (Ripley 2026-08-07 10:28, F1-independent — render
// occlusion + world footprint, not the break frame):
//   S6 targetSlots[0] is the TOP/reachable tile of the frontier stack (its `up`
//      link empty), NEVER the bottom/occluded layer; any lower sibling is occluded;
//   S7 after a count=1 pickup the SAME stack's lower layer becomes the next
//      reachable trigger (single-tile phases go top→bottom within a stack);
//   S8 a count=4 press on the single front trigger consumes EXACTLY two adjacent
//      front stacks (2 footprints × 2 layers = 4 tiles).
// (The dealer×dice break ANCHOR position is G4's F1-blocked concern.)
// WIRE GUARD + FAIL-CLOSED (Ripley 11:23 — catches the live nextTileSlots-vs-
// targetSlots mismatch): S10 inspects the RAW `pickup` frame and asserts the field
// is `targetSlots` (NOT nextTileSlots/targetHandles/targetTileIds) with length
// EXACTLY 1 — a silent backend/client field no-match fails at the wire, not just
// the happy path. S11 asserts fail-closed: when targetSlots is missing/empty/
// length>1, NO wall tile is interactable (the client must not fall back to any-wall).
// S12 (Vasquez F2 reachability): the single actionable tile is the REACHABLE TOP of
// the frontier stack (targetSlots[0], form wall.{col}.1@{seat}) — a real click TAKES;
// the OCCLUDED bottom (layer 0) of that same stack is INERT (canSelect blocks it);
// take payload = {seatIndex,count}. Guards against a designation pointing at an
// unreachable/occluded bottom tile.
// AUTO reject is hudson-2's. F1-independent (Bishop co-derives targetSlots).
import { test, expect, type Page } from '@playwright/test';
import { buildGameUrl, makeConfig, dismissLobbyAndTour, ensureConnected, takeSeatByClick, clickDeal, waitForPlayableHand } from './_playability';
import { realDragWallTile, recordEvidence, shot } from './_uat_red';

// FINAL designation reader. GATE = pickup.targetSlots = EXACTLY ONE public
// exposed-front render-slot name (state.Wall[0]); count is the server batch size.
async function readDesignation(page: Page): Promise<any> {
  return page.evaluate(() => {
    /* eslint-disable @typescript-eslint/no-explicit-any */
    const g = (window as any).game; const w = g?.world;
    const pu = g?.client?.pickup?.get ? g.client.pickup.get('current') : null;
    if (!pu || pu.seatIndex !== g?.client?.seat) return null;
    const wallSlots = new Set<string>();
    if (w?.things) for (const t of w.things.values()) if (t?.slot?.group === 'wall') wallSlots.add(String(t.slot?.name));
    const gate: string[] | null = Array.isArray((pu as any).targetSlots) ? (pu as any).targetSlots.map(String) : null;
    const preview: string[] | null = Array.isArray((pu as any).batchPreviewSlots) ? (pu as any).batchPreviewSlots.map(String) : null;
    const slotNameRe = /^wall\.\d+\.\d+@\d+$/;
    let kind: string | null = null;
    if (gate && gate.length) {
      if (gate.every((v) => slotNameRe.test(v))) kind = 'slot-names';
      else if (gate.every((v) => /^-?\d+$/.test(v))) kind = 'raw-ids';
      else kind = 'opaque-or-other';   // opaque handle strings do NOT belong in targetSlots
    }
    return {
      phase: pu.phase, count: pu.count, gate, gateLen: gate ? gate.length : 0, kind,
      preview, previewLen: preview ? preview.length : 0,
      exactlyOneFront: !!gate && gate.length === 1,
      isSlotNames: kind === 'slot-names',
      mapsToRenderedWall: !!gate && gate.length > 0 && gate.every((s) => wallSlots.has(s)),
      rawIdOrHandleLeak: kind === 'raw-ids' || kind === 'opaque-or-other',
    };
    /* eslint-enable @typescript-eslint/no-explicit-any */
  });
}

// Real-click the single designated exposed-front slot vs a non-designated wall
// tile (matches by PUBLIC slot name). Right (the one front slot) ⇒ whole batch of
// `count` to hand; wrong (a non-front wall tile, incl. the other batch tiles) ⇒ inert.
async function clickWallByDesignationAndCount(page: Page, values: string[], count: number): Promise<{ batchTaken: boolean; wrongRejected: boolean }> {
  const pick = async (wantTarget: boolean) => page.evaluate(({ values, wantTarget }) => {
    /* eslint-disable @typescript-eslint/no-explicit-any */
    const g = (window as any).game; const w = g?.world; const camera = g?.mainView?.camera; const main = document.getElementById('main');
    const rect = main?.getBoundingClientRect();
    if (camera) { try { camera.parent?.updateMatrixWorld(true); camera.updateMatrixWorld(true); camera.matrixWorldInverse?.copy(camera.matrixWorld).invert(); } catch { /* */ } }
    const set = new Set(values.map(String));
    const proj = (p: any) => { const mw = camera.matrixWorldInverse.elements, pm = camera.projectionMatrix.elements; const vx = mw[0]*p.x+mw[4]*p.y+mw[8]*p.z+mw[12], vy = mw[1]*p.x+mw[5]*p.y+mw[9]*p.z+mw[13], vz = mw[2]*p.x+mw[6]*p.y+mw[10]*p.z+mw[14], vw = mw[3]*p.x+mw[7]*p.y+mw[11]*p.z+mw[15]; const cx = pm[0]*vx+pm[4]*vy+pm[8]*vz+pm[12]*vw, cy = pm[1]*vx+pm[5]*vy+pm[9]*vz+pm[13]*vw, cw = pm[3]*vx+pm[7]*vy+pm[11]*vz+pm[15]*vw; return { sx: (rect?.left??0)+(cx/cw+1)*0.5*(rect?.width??0), sy: (rect?.top??0)+(1-cy/cw)*0.5*(rect?.height??0) }; };
    let hand = 0; for (const t of w.things.values()) if (/^hand\.\d+@0$/.test(String(t?.slot?.name ?? ''))) hand++;
    let wall = 0; for (const t of w.things.values()) if (t?.slot?.group === 'wall') wall++;
    for (const t of w.things.values()) {
      if (t?.slot?.group !== 'wall' || t.claimedBy != null) continue;
      const up = t.slot?.links?.up; if (up && up.thing) continue;
      const isTarget = set.has(String(t.slot?.name));
      if (isTarget !== wantTarget) continue;
      const s = proj(t.place().position);
      return { ok: true, cx: Math.round(s.sx), cy: Math.round(s.sy), handBefore: hand, wallBefore: wall };
    }
    return { ok: false, handBefore: hand, wallBefore: wall };
    /* eslint-enable @typescript-eslint/no-explicit-any */
  }, { values, wantTarget });
  const counts = async () => page.evaluate(() => { const w = (window as any).game?.world; let hand = 0, wall = 0; if (w?.things) for (const t of w.things.values()) { const nm = String(t?.slot?.name ?? ''); if (/^hand\.\d+@0$/.test(nm)) hand++; if (t?.slot?.group === 'wall') wall++; } return { hand, wall }; });

  const tp = await pick(true); let batchTaken = false;
  if (tp.ok) { await page.mouse.move(tp.cx, tp.cy); await page.waitForTimeout(120); await page.mouse.down(); await page.waitForTimeout(80); await page.mouse.up(); await page.waitForTimeout(1200); const after = await counts(); batchTaken = (after.hand - tp.handBefore) === count && (tp.wallBefore - after.wall) === count; }
  const wp = await pick(false); let wrongRejected = false;
  if (wp.ok) { await page.mouse.move(wp.cx, wp.cy); await page.waitForTimeout(120); await page.mouse.down(); await page.waitForTimeout(80); await page.mouse.up(); await page.waitForTimeout(1000); const after = await counts(); wrongRejected = after.hand === wp.handBefore && after.wall === wp.wallBefore; }
  return { batchTaken, wrongRejected };
}

// PHYSICAL-STACK extension (F1-independent — uses render occlusion + world
// footprint, not the break frame). Reads the SINGLE designated front trigger and
// classifies it: reachable ⟺ no tile occupies its slot's `up` link; top-of-stack
// ⟺ its footprint sibling (same world x,y) sits below it. `filterMine` restricts
// to my own pickup window; the timeline reader (readAnyPickupStack) does not.
async function readStackDesignation(page: Page, filterMine = true): Promise<any> {
  return page.evaluate((filterMine) => {
    /* eslint-disable @typescript-eslint/no-explicit-any */
    const g = (window as any).game; const w = g?.world;
    const pu = g?.client?.pickup?.get ? g.client.pickup.get('current') : null;
    if (!pu) return null;
    if (filterMine && pu.seatIndex !== g?.client?.seat) return null;
    const target: string[] | null = Array.isArray((pu as any).targetSlots) ? (pu as any).targetSlots.map(String) : null;
    const wall: any[] = [];
    if (w?.things) for (const t of w.things.values()) if (t?.slot?.group === 'wall') {
      let p = { x: 0, y: 0, z: 0 }; try { const pl = t.place(); p = { x: pl.position.x, y: pl.position.y, z: pl.position.z }; } catch { /* */ }
      const up = t.slot?.links?.up;
      wall.push({ name: String(t.slot?.name), reachable: !(up && up.thing), x: p.x, y: p.y, z: p.z });
    }
    const byName: Record<string, any> = {}; for (const t of wall) byName[t.name] = t;
    const t0 = target && target.length ? byName[target[0]] : null;
    const sib = t0 ? wall.find((t) => t.name !== t0.name && Math.abs(t.x - t0.x) < 3 && Math.abs(t.y - t0.y) < 3) : null;
    return {
      phase: pu.phase, count: pu.count, seatIndex: pu.seatIndex,
      targetLen: target ? target.length : 0,
      targetName: target && target.length ? target[0] : null,
      targetFound: !!t0,
      targetReachable: t0 ? t0.reachable : null,
      targetIsTopOfStack: (t0 && sib) ? (t0.z >= sib.z) : (t0 ? true : null),
      hasOccludedSibling: !!sib, siblingReachable: sib ? sib.reachable : null,
      footprint: t0 ? { x: Math.round(t0.x), y: Math.round(t0.y) } : null,
    };
    /* eslint-enable @typescript-eslint/no-explicit-any */
  }, filterMine);
}

// Group wall tiles into physical stacks by world footprint (x,y); a full stack
// has 2 layers. Used to prove a count=4 batch removes EXACTLY two adjacent stacks.
async function wallStacks(page: Page): Promise<Array<{ x: number; y: number; layers: number }>> {
  return page.evaluate(() => {
    /* eslint-disable @typescript-eslint/no-explicit-any */
    const w = (window as any).game?.world; const pts: Array<{ x: number; y: number }> = [];
    if (w?.things) for (const t of w.things.values()) if (t?.slot?.group === 'wall') { let p = { x: 0, y: 0 }; try { const pl = t.place(); p = { x: pl.position.x, y: pl.position.y }; } catch { /* */ } pts.push({ x: p.x, y: p.y }); }
    const stacks: Array<{ x: number; y: number; layers: number }> = [];
    for (const p of pts) { const s = stacks.find((q) => Math.abs(q.x - p.x) < 3 && Math.abs(q.y - p.y) < 3); if (s) s.layers++; else stacks.push({ x: Math.round(p.x), y: Math.round(p.y), layers: 1 }); }
    return stacks;
    /* eslint-enable @typescript-eslint/no-explicit-any */
  });
}

async function clickSlotByName(page: Page, name: string): Promise<{ ok: boolean }> {
  const s = await page.evaluate((name) => {
    /* eslint-disable @typescript-eslint/no-explicit-any */
    const g = (window as any).game; const w = g?.world; const camera = g?.mainView?.camera; const main = document.getElementById('main');
    const rect = main?.getBoundingClientRect();
    if (camera) { try { camera.parent?.updateMatrixWorld(true); camera.updateMatrixWorld(true); camera.matrixWorldInverse?.copy(camera.matrixWorld).invert(); } catch { /* */ } }
    for (const t of w.things.values()) {
      if (t?.slot?.group !== 'wall' || String(t.slot?.name) !== name) continue;
      const p = t.place().position; const mw = camera.matrixWorldInverse.elements, pm = camera.projectionMatrix.elements;
      const vx = mw[0]*p.x+mw[4]*p.y+mw[8]*p.z+mw[12], vy = mw[1]*p.x+mw[5]*p.y+mw[9]*p.z+mw[13], vz = mw[2]*p.x+mw[6]*p.y+mw[10]*p.z+mw[14], vw = mw[3]*p.x+mw[7]*p.y+mw[11]*p.z+mw[15];
      const cx = pm[0]*vx+pm[4]*vy+pm[8]*vz+pm[12]*vw, cy = pm[1]*vx+pm[5]*vy+pm[9]*vz+pm[13]*vw, cw = pm[3]*vx+pm[7]*vy+pm[11]*vz+pm[15]*vw;
      return { ok: true, cx: Math.round((rect?.left??0)+(cx/cw+1)*0.5*(rect?.width??0)), cy: Math.round((rect?.top??0)+(1-cy/cw)*0.5*(rect?.height??0)) };
    }
    return { ok: false, cx: 0, cy: 0 };
    /* eslint-enable @typescript-eslint/no-explicit-any */
  }, name);
  if (!s.ok) return { ok: false };
  await page.mouse.move(s.cx, s.cy); await page.waitForTimeout(120); await page.mouse.down(); await page.waitForTimeout(80); await page.mouse.up(); await page.waitForTimeout(1200);
  return { ok: true };
}

// Scan RAW received WS frames for the most-recent pickup entry and return its
// value object — protocol Entry = [kind, key, value] tuple. Used to guard the WIRE
// field name (`targetSlots`, NOT nextTileSlots/targetHandles/targetTileIds) so a
// backend/client field mismatch (silent no-match) is caught at the wire, not just
// via the client's parsed state.
function scanForPickup(node: any): any {
  if (Array.isArray(node)) {
    if (node.length >= 3 && node[0] === 'pickup' && node[2] && typeof node[2] === 'object' && !Array.isArray(node[2])) return node[2];
    for (const el of node) { const r = scanForPickup(el); if (r) return r; }
  } else if (node && typeof node === 'object') {
    for (const v of Object.values(node)) { const r = scanForPickup(v); if (r) return r; }
  }
  return null;
}
function extractRawPickup(recv: string[]): any {
  for (let i = recv.length - 1; i >= 0; i--) { let m: any; try { m = JSON.parse(recv[i]); } catch { continue; } const p = scanForPickup(m); if (p) return p; }
  return null;
}

// Count own hand + wall tiles (for the fail-closed no-any-wall-fallback probe).
async function handWallCounts(page: Page): Promise<{ hand: number; wall: number }> {
  return page.evaluate(() => { const w = (window as any).game?.world; let hand = 0, wall = 0; if (w?.things) for (const t of w.things.values()) { const nm = String(t?.slot?.name ?? ''); if (/^hand\.\d+@0$/.test(nm)) hand++; if (t?.slot?.group === 'wall') wall++; } return { hand, wall }; });
}

test.describe('G17 manual pickup endpoint-only (§D10/§B/§E2)', () => {
  test('manual PRE-ceremony: wall press is inert', async ({ page }, testInfo) => {
    testInfo.setTimeout(120_000);
    await page.setViewportSize({ width: 1600, height: 900 });
    const base = testInfo.project.use.baseURL as string;
    const cfg = makeConfig({ gameId: `g17-pre-${Date.now()}`, dealMode: 'manual', botCount: 3, botDifficulty: 'Medium', handCount: 4 });
    await page.goto(buildGameUrl(base, cfg), { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(1200); await dismissLobbyAndTour(page); await ensureConnected(page);
    await takeSeatByClick(page, 0); await page.waitForTimeout(1500);
    const drag = await realDragWallTile(page);
    recordEvidence('g17-manual-pre-ceremony.json', { isMyPickupTurn: drag.isMyPickupTurn, held: drag.held });
    expect(drag.held?.isHolding ?? false, 'pre-ceremony wall tile must NOT be held').toBe(false);
    expect(drag.held?.claimedBy ?? null, 'pre-ceremony wall tile must NOT be claimed').toBeNull();
  });

  test('manual POST-deal (AwaitingDiscard): wall press is inert', async ({ page }, testInfo) => {
    testInfo.setTimeout(150_000);
    await page.setViewportSize({ width: 1600, height: 900 });
    const base = testInfo.project.use.baseURL as string;
    const cfg = makeConfig({ gameId: `g17-post-${Date.now()}`, dealMode: 'manual', botCount: 3, botDifficulty: 'Medium', handCount: 4 });
    await page.goto(buildGameUrl(base, cfg), { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(1200); await dismissLobbyAndTour(page); await ensureConnected(page);
    await takeSeatByClick(page, 0); await clickDeal(page); await waitForPlayableHand(page, 60_000).catch(() => {});
    await page.waitForTimeout(1500);
    const drag = await realDragWallTile(page);
    await shot(page, 'g17-post-deal-drag.png');
    recordEvidence('g17-manual-post-deal.json', { isMyPickupTurn: drag.isMyPickupTurn, held: drag.held });
    expect(drag.isMyPickupTurn, 'post-deal is not a pickup turn').toBe(false);
    expect(drag.held?.isHolding ?? false, 'post-deal wall tile must NOT be held').toBe(false);
  });

  test('S1/S2: pickup ships targetSlots == EXACTLY ONE exposed-front slot (public name, no raw id/handle)', async ({ page }, testInfo) => {
    testInfo.setTimeout(120_000);
    await page.setViewportSize({ width: 1600, height: 900 });
    const base = testInfo.project.use.baseURL as string;
    const cfg = makeConfig({ gameId: `g17-sig-${Date.now()}`, seat: 0, dealMode: 'manual', botCount: 3, botDifficulty: 'Medium', handCount: 4 });
    await page.goto(buildGameUrl(base, cfg), { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(1200); await dismissLobbyAndTour(page); await ensureConnected(page);
    await takeSeatByClick(page, 0); await clickDeal(page).catch(() => {});
    const seen: any[] = []; const t0 = Date.now();
    while (Date.now() - t0 < 22000) {
      const d = await readDesignation(page);
      if (d) { const key = d.phase + ':' + d.count; if (!seen.some((s) => s.phase + ':' + s.count === key)) seen.push({ phase: d.phase, count: d.count, gateLen: d.gateLen, kind: d.kind, exactlyOneFront: d.exactlyOneFront, isSlotNames: d.isSlotNames, mapsToRenderedWall: d.mapsToRenderedWall, rawIdOrHandleLeak: d.rawIdOrHandleLeak }); }
      if (await page.evaluate(() => { const t = (window as any).game?.client?.turn; return !!(t && t.awaitingDiscard); })) break;
      await page.waitForTimeout(300);
    }
    recordEvidence('g17-pickup-signal.json', { windowsSeen: seen.length, windows: seen });
    expect(seen.length, 'must observe a manual pickup window targeting my seat').toBeGreaterThan(0);
    for (const s of seen) {
      expect([1, 4], `pickup ${s.phase} server batch count must be 4 (BreakPointMarked/Round1-3) or 1 (SingleTile/DealerExtra); got ${s.count}`).toContain(s.count);
      expect(s.exactlyOneFront, `S1: pickup ${s.phase} targetSlots must contain EXACTLY ONE exposed-front slot (Wall[0]); got len=${s.gateLen}`).toBe(true);
      expect(s.isSlotNames, `S2: pickup ${s.phase} targetSlots must be a PUBLIC slot NAME, never a raw id or opaque handle; kind=${s.kind}`).toBe(true);
      expect(s.rawIdOrHandleLeak, `S2: pickup ${s.phase} targetSlots must NOT leak a raw tile id or opaque handle`).toBe(false);
      expect(s.mapsToRenderedWall, `S1: pickup ${s.phase} targetSlots must name a real rendered wall slot the viewer sees (the exposed front)`).toBe(true);
    }
  });

  test('S3/S4: the ONE front slot takes the whole batch (server-confirmed); every other wall tile inert', async ({ page }, testInfo) => {
    testInfo.setTimeout(120_000);
    await page.setViewportSize({ width: 1600, height: 900 });
    const base = testInfo.project.use.baseURL as string;
    const cfg = makeConfig({ gameId: `g17-int-${Date.now()}`, seat: 0, dealMode: 'manual', botCount: 3, botDifficulty: 'Medium', handCount: 4 });
    await page.goto(buildGameUrl(base, cfg), { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(1200); await dismissLobbyAndTour(page); await ensureConnected(page);
    await takeSeatByClick(page, 0); await clickDeal(page).catch(() => {});
    let d: any = null; const t0 = Date.now();
    while (Date.now() - t0 < 22000) {
      d = await readDesignation(page);
      if (d && d.gate && d.gateLen) break;
      if (await page.evaluate(() => { const t = (window as any).game?.client?.turn; return !!(t && t.awaitingDiscard); })) break;
      await page.waitForTimeout(250);
    }
    recordEvidence('g17-endpoint-interaction.json', { designation: d ? { phase: d.phase, count: d.count, gateLen: d.gateLen, kind: d.kind } : null,
      note: 'RED@200cad4: pickup ships no targetSlots, so endpoint-only gating cannot hold; any wall press currently takes.' });

    expect(d && d.gate && d.exactlyOneFront,
      `endpoint-only interaction requires pickup.targetSlots == exactly one exposed-front slot; got ${JSON.stringify(d)}`).toBe(true);
    if (d && d.gate && d.gateLen) {
      const outcome = await clickWallByDesignationAndCount(page, d.gate, d.count);
      expect(outcome.batchTaken, `S4: pressing the ONE exposed-front slot must take the whole batch of ${d.count} (server-confirmed)`).toBe(true);
      expect(outcome.wrongRejected, 'S3: a non-front wall tile (incl. the other batch tiles) must be inert (no move/take)').toBe(true);
    }
  });

  test('S9 (FINAL SC-4 parent-locked): pickup.take payload = {seatIndex,count} ONLY + batchPreviewSlots inert', async ({ page }, testInfo) => {
    testInfo.setTimeout(120_000);
    await page.setViewportSize({ width: 1600, height: 900 });
    // OUTBOUND ws capture (observation only — no injection/emit).
    const sent: string[] = [];
    page.on('websocket', (ws) => ws.on('framesent', (ev: any) => { try { sent.push(typeof ev.payload === 'string' ? ev.payload : Buffer.from(ev.payload).toString('utf8')); } catch { /* */ } }));
    const base = testInfo.project.use.baseURL as string;
    const cfg = makeConfig({ gameId: `g17-take-${Date.now()}`, seat: 0, dealMode: 'manual', botCount: 3, botDifficulty: 'Medium', handCount: 4 });
    await page.goto(buildGameUrl(base, cfg), { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(1200); await dismissLobbyAndTour(page); await ensureConnected(page);
    await takeSeatByClick(page, 0); await clickDeal(page).catch(() => {});
    let d: any = null; const t0 = Date.now();
    while (Date.now() - t0 < 22000) {
      d = await readDesignation(page);
      if (d && d.gate && d.gateLen) break;
      if (await page.evaluate(() => { const t = (window as any).game?.client?.turn; return !!(t && t.awaitingDiscard); })) break;
      await page.waitForTimeout(250);
    }
    let previewInert = true; let batchTaken = false; let payloadKeys: string[] | null = null;
    if (d && d.gate && d.gateLen) {
      // batchPreviewSlots (display-only) must NOT be actionable
      if (d.preview && d.preview.length) { const out = await clickWallByDesignationAndCount(page, d.preview, d.count); previewInert = out.wrongRejected && !out.batchTaken; }
      // the single trigger press → server takes the whole batch; capture the take frame
      sent.length = 0;
      const outcome = await clickWallByDesignationAndCount(page, d.gate, d.count); batchTaken = outcome.batchTaken;
      await page.waitForTimeout(400);
      const frames = sent.map((s) => { try { return JSON.parse(s); } catch { return null; } }).filter((m) => m && /take|pickup/i.test(JSON.stringify(m)));
      for (const m of frames) {
        const scan = (o: any): string[] | null => { if (o && typeof o === 'object') { if ('count' in o || 'seatIndex' in o) return Object.keys(o); for (const v of Object.values(o)) { const r = scan(v); if (r) return r; } } return null; };
        const k = scan(m); if (k) { payloadKeys = k; break; }
      }
    }
    recordEvidence('g17-take-payload.json', { designation: d ? { gateLen: d.gateLen, previewLen: d.previewLen, count: d.count } : null, previewInert, batchTaken, takePayloadKeys: payloadKeys, sentFrames: sent.length,
      note: 'RED@200cad4: no targetSlots ⇒ no single-trigger designation ⇒ take path unreachable. GREEN needs targetSlots len-1 + a {seatIndex,count}-only take.' });

    // FINAL SC-4: single-trigger-slot; take carries ZERO tile authority.
    expect(d && d.exactlyOneFront, `SC-4: pickup.targetSlots must be EXACTLY length 1 (single trigger); got ${JSON.stringify(d && { gateLen: d.gateLen })}`).toBe(true);
    expect(previewInert, 'SC-4: batchPreviewSlots (display-only) must NOT be actionable (no take)').toBe(true);
    expect(batchTaken, `SC-4: the single trigger press must take the whole batch of ${d?.count} (server count-based)`).toBe(true);
    expect(payloadKeys, 'SC-4: an outbound pickup.take frame must be observed').not.toBeNull();
    if (payloadKeys) {
      const extra = payloadKeys.filter((k) => k !== 'seatIndex' && k !== 'count');
      expect(extra, `SC-4: pickup.take payload must be {seatIndex,count} ONLY — no tile id/handle/slot; extra=${JSON.stringify(extra)}`).toEqual([]);
    }
  });

  test('S10 (wire guard): RAW pickup frame field == targetSlots len1 (not nextTileSlots/targetHandles/targetTileIds)', async ({ page }, testInfo) => {
    testInfo.setTimeout(120_000);
    await page.setViewportSize({ width: 1600, height: 900 });
    // capture RAW incoming frames (observation only)
    const recv: string[] = [];
    page.on('websocket', (ws) => ws.on('framereceived', (ev: any) => { try { recv.push(typeof ev.payload === 'string' ? ev.payload : Buffer.from(ev.payload).toString('utf8')); } catch { /* */ } }));
    const base = testInfo.project.use.baseURL as string;
    const cfg = makeConfig({ gameId: `g17-wire-${Date.now()}`, seat: 0, dealMode: 'manual', botCount: 3, botDifficulty: 'Medium', handCount: 4 });
    await page.goto(buildGameUrl(base, cfg), { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(1200); await dismissLobbyAndTour(page); await ensureConnected(page);
    await takeSeatByClick(page, 0); await clickDeal(page).catch(() => {});
    // let the ceremony emit a pickup frame
    const t0 = Date.now();
    while (Date.now() - t0 < 22000) { if (extractRawPickup(recv)) break; if (await page.evaluate(() => { const t = (window as any).game?.client?.turn; return !!(t && t.awaitingDiscard); })) break; await page.waitForTimeout(250); }
    const pu = extractRawPickup(recv);
    const keys = pu ? Object.keys(pu) : [];
    const arrLen = (k: string) => pu && Array.isArray(pu[k]) ? pu[k].length : -1;
    const tsLen = arrLen('targetSlots');
    const ntsLen = arrLen('nextTileSlots');
    // Frost baseline: backend emits nextTileSlots length=count; frontend needs
    // targetSlots length 1. Reject ANY multi-element batch designation.
    const multiElementBatch = ['nextTileSlots', 'targetSlots', 'targetTileIds', 'targetHandles'].some((k) => arrLen(k) > 1);
    recordEvidence('g17-wire-fieldname.json', { observedPickup: !!pu, keys, targetSlotsLen: tsLen, nextTileSlotsLen: ntsLen, multiElementBatch, hasNextTileSlots: keys.includes('nextTileSlots'), hasTargetHandles: keys.includes('targetHandles'), hasTargetTileIds: keys.includes('targetTileIds'),
      note: 'WIRE guard for the Frost baseline (backend nextTileSlots length=count vs frontend targetSlots length1). RED@200cad4: served build emits no targetSlots (absent/renamed).' });
    // WIRE FIELD-NAME + CARDINALITY DISCRIMINATOR — catches a silent backend/client no-match.
    expect(pu, 'must observe a RAW pickup frame during the manual ceremony').not.toBeNull();
    expect(keys.includes('nextTileSlots'), `raw pickup must NOT use the retired field 'nextTileSlots' (Frost baseline: backend emits nextTileSlots length=${ntsLen}); keys=${JSON.stringify(keys)}`).toBe(false);
    expect(keys.includes('targetHandles'), 'raw pickup must NOT use the retired field targetHandles').toBe(false);
    expect(keys.includes('targetTileIds'), 'raw pickup must NOT use the retired field targetTileIds').toBe(false);
    expect(multiElementBatch, `raw pickup must NOT carry a MULTI-ELEMENT batch designation (single-trigger only); nextTileSlots=${ntsLen} targetSlots=${tsLen}`).toBe(false);
    expect(keys.includes('targetSlots'), `raw pickup must carry the field 'targetSlots'; keys=${JSON.stringify(keys)}`).toBe(true);
    expect(tsLen, `raw pickup.targetSlots must be length EXACTLY 1 (exact top Wall[0]); got ${tsLen}`).toBe(1);
  });

  test('S11 (fail-closed): with NO valid targetSlots, NO wall tile is interactable (no any-wall fallback)', async ({ page }, testInfo) => {
    testInfo.setTimeout(120_000);
    await page.setViewportSize({ width: 1600, height: 900 });
    const base = testInfo.project.use.baseURL as string;
    const cfg = makeConfig({ gameId: `g17-failclosed-${Date.now()}`, seat: 0, dealMode: 'manual', botCount: 3, botDifficulty: 'Medium', handCount: 4 });
    await page.goto(buildGameUrl(base, cfg), { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(1200); await dismissLobbyAndTour(page); await ensureConnected(page);
    await takeSeatByClick(page, 0); await clickDeal(page).catch(() => {});
    await page.waitForTimeout(1500);
    // the client-parsed designation: missing/empty/multiple ⇒ NO exact-1 trigger
    const d = await readDesignation(page);
    const noValidDesignation = !d || !d.gate || d.gateLen !== 1;
    // press an ARBITRARY wall tile (real pointer) and assert nothing moves/takes
    const before = await handWallCounts(page);
    const drag = await realDragWallTile(page);
    await page.waitForTimeout(600);
    const after = await handWallCounts(page);
    const anyWallActed = (after.hand > before.hand) || (after.wall < before.wall) || !!(drag.held && drag.held.isHolding) || ((drag.held?.dragOffsetWorld ?? 0) > 5);
    recordEvidence('g17-fail-closed.json', { noValidDesignation, designationLen: d ? d.gateLen : null, before, after, held: drag.held, anyWallActed,
      note: 'Fail-closed: with missing/empty/multiple targetSlots the client must NOT fall back to any-wall interaction. RED@200cad4 if a wall press still hovers/holds/takes.' });
    // @200cad4 the served build ships no valid single-trigger targetSlots ⇒ this IS
    // the missing fail-closed case; the client must NOT let any wall tile act.
    expect(noValidDesignation, 'precondition: no exact-1 targetSlots designation (missing/empty/multiple)').toBe(true);
    expect(anyWallActed, 'FAIL-CLOSED: with no valid targetSlots, pressing ANY wall tile must be inert (no hold/move/take — no any-wall fallback)').toBe(false);
  });

  test('S12 (F2 reachability): reachable TOP frontier tile actionable; OCCLUDED bottom inert (canSelect blocks)', async ({ page }, testInfo) => {
    testInfo.setTimeout(120_000);
    await page.setViewportSize({ width: 1600, height: 900 });
    const sent: string[] = [];
    page.on('websocket', (ws) => ws.on('framesent', (ev: any) => { try { sent.push(typeof ev.payload === 'string' ? ev.payload : Buffer.from(ev.payload).toString('utf8')); } catch { /* */ } }));
    const base = testInfo.project.use.baseURL as string;
    const cfg = makeConfig({ gameId: `g17-f2-${Date.now()}`, seat: 0, dealMode: 'manual', botCount: 3, botDifficulty: 'Medium', handCount: 4 });
    await page.goto(buildGameUrl(base, cfg), { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(1200); await dismissLobbyAndTour(page); await ensureConnected(page);
    await takeSeatByClick(page, 0); await clickDeal(page).catch(() => {});
    let d: any = null; const t0 = Date.now();
    while (Date.now() - t0 < 22000) {
      d = await readDesignation(page);
      if (d && d.gate && d.gateLen) break;
      if (await page.evaluate(() => { const t = (window as any).game?.client?.turn; return !!(t && t.awaitingDiscard); })) break;
      await page.waitForTimeout(250);
    }
    // classify the frontier stack: the designated TOP tile (targetSlots[0]) + its
    // same-footprint OCCLUDED sibling; canSelect (esbuild keeps method names) with an
    // occlusion fallback. Ripley F2: top=reachable (layer 1) selectable; bottom inert.
    const react = d && d.gate && d.gateLen ? await page.evaluate((topSlot) => {
      /* eslint-disable @typescript-eslint/no-explicit-any */
      const g = (window as any).game; const w = g?.world;
      const pos = (t: any) => { let p = { x: 0, y: 0, z: 0 }; try { const pl = t.place(); p = { x: pl.position.x, y: pl.position.y, z: pl.position.z }; } catch { /* */ } return p; };
      const cs = (t: any) => { try { return typeof w.canSelect === 'function' ? !!w.canSelect(t, []) : null; } catch { return null; } };
      let top: any = null;
      if (w?.things) for (const t of w.things.values()) if (t?.slot?.group === 'wall' && String(t.slot?.name) === topSlot) top = t;
      let bottom: any = null;
      if (top) { const tp = pos(top); if (w?.things) for (const t of w.things.values()) if (t?.slot?.group === 'wall' && String(t.slot?.name) !== topSlot) { const p = pos(t); if (Math.abs(p.x - tp.x) < 3 && Math.abs(p.y - tp.y) < 3) bottom = t; } }
      const upOcc = (t: any) => { const up = t?.slot?.links?.up; return !!(up && up.thing); };
      return {
        topName: top ? String(top.slot?.name) : null, topReachable: top ? !upOcc(top) : null, topCanSelect: top ? cs(top) : null,
        hasBottom: !!bottom, bottomName: bottom ? String(bottom.slot?.name) : null, bottomOccluded: bottom ? upOcc(bottom) : null, bottomCanSelect: bottom ? cs(bottom) : null,
        canSelectAvailable: typeof w?.canSelect === 'function',
      };
      /* eslint-enable @typescript-eslint/no-explicit-any */
    }, d.gate[0]) : null;

    let batchTaken = false; let payloadKeys: string[] | null = null;
    if (d && d.gate && d.gateLen) {
      sent.length = 0;
      const outcome = await clickWallByDesignationAndCount(page, d.gate, d.count); batchTaken = outcome.batchTaken;
      await page.waitForTimeout(400);
      const frames = sent.map((s) => { try { return JSON.parse(s); } catch { return null; } }).filter((m) => m && /take|pickup/i.test(JSON.stringify(m)));
      for (const m of frames) { const scan = (o: any): string[] | null => { if (o && typeof o === 'object') { if ('count' in o || 'seatIndex' in o) return Object.keys(o); for (const v of Object.values(o)) { const r = scan(v); if (r) return r; } } return null; }; const k = scan(m); if (k) { payloadKeys = k; break; } }
    }
    recordEvidence('g17-f2-reachability.json', { designation: d ? { gateLen: d.gateLen, count: d.count, top: d.gate?.[0] } : null, react, batchTaken, payloadKeys,
      note: 'F2: the actionable tile is the reachable TOP of the frontier stack; the occluded bottom must be inert. RED@200cad4: no targetSlots designation.' });

    expect(d && d.exactlyOneFront, `F2: requires a single-trigger pickup.targetSlots designation; got ${JSON.stringify(d && { gateLen: d.gateLen })}`).toBe(true);
    // (top) reachable + selectable
    expect(react && react.topReachable, `F2(a): targetSlots[0]=${react?.topName} must be the REACHABLE top of the frontier stack`).toBe(true);
    if (react && react.canSelectAvailable) expect(react.topCanSelect, 'F2(a): the reachable top tile must be selectable (canSelect true)').toBe(true);
    // (b) occluded bottom inert
    if (react && react.hasBottom) {
      expect(react.bottomOccluded, `F2(b): the same-stack bottom tile ${react.bottomName} must be OCCLUDED (a tile on top)`).toBe(true);
      const bottomInert = react.bottomCanSelect === false || (react.bottomCanSelect === null && react.bottomOccluded === true);
      expect(bottomInert, `F2(b): the OCCLUDED bottom tile must be INERT (canSelect blocks it); bottomCanSelect=${react.bottomCanSelect}`).toBe(true);
    }
    // (a) real click on the top → take; (d) payload seat+count only
    expect(batchTaken, 'F2(a): a real-pointer click on the reachable TOP tile must trigger the take').toBe(true);
    expect(payloadKeys, 'F2(d): an outbound pickup.take frame must be observed').not.toBeNull();
    if (payloadKeys) expect(payloadKeys.filter((k) => k !== 'seatIndex' && k !== 'count'), `F2(d): take payload = {seatIndex,count} only; extra=${JSON.stringify(payloadKeys)}`).toEqual([]);
  });

  test('S6: targetSlots[0] is the TOP/reachable frontier tile — never bottom/occluded', async ({ page }, testInfo) => {
    testInfo.setTimeout(120_000);
    await page.setViewportSize({ width: 1600, height: 900 });
    const base = testInfo.project.use.baseURL as string;
    const cfg = makeConfig({ gameId: `g17-top-${Date.now()}`, seat: 0, dealMode: 'manual', botCount: 3, botDifficulty: 'Medium', handCount: 4 });
    await page.goto(buildGameUrl(base, cfg), { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(1200); await dismissLobbyAndTour(page); await ensureConnected(page);
    await takeSeatByClick(page, 0); await clickDeal(page).catch(() => {});
    const seen: any[] = []; const t0 = Date.now();
    while (Date.now() - t0 < 22000) {
      const d = await readStackDesignation(page);
      if (d && d.targetName) { const key = d.phase + ':' + d.targetName; if (!seen.some((s) => s.key === key)) seen.push({ key, phase: d.phase, count: d.count, targetName: d.targetName, targetFound: d.targetFound, targetReachable: d.targetReachable, targetIsTopOfStack: d.targetIsTopOfStack, hasOccludedSibling: d.hasOccludedSibling, siblingReachable: d.siblingReachable }); }
      if (await page.evaluate(() => { const t = (window as any).game?.client?.turn; return !!(t && t.awaitingDiscard); })) break;
      await page.waitForTimeout(250);
    }
    recordEvidence('g17-top-reachable.json', { seen: seen.length, samples: seen.slice(0, 8),
      note: 'RED@200cad4: pickup ships no targetSlots ⇒ no named front trigger to classify top/reachable.' });
    expect(seen.length, 'S6 requires an observed pickup with a named targetSlots[0]').toBeGreaterThan(0);
    for (const s of seen) {
      expect(s.targetFound, `S6: ${s.phase} targetSlots[0]=${s.targetName} must name a rendered wall tile`).toBe(true);
      expect(s.targetReachable, `S6: ${s.phase} targetSlots[0]=${s.targetName} must be REACHABLE (no tile on top) — the exposed front`).toBe(true);
      expect(s.targetIsTopOfStack, `S6: ${s.phase} targetSlots[0]=${s.targetName} must be the TOP of its stack, not the bottom/occluded layer`).toBe(true);
      if (s.hasOccludedSibling) expect(s.siblingReachable, `S6: ${s.phase} the lower layer under the trigger must be OCCLUDED (rejected as a target)`).toBe(false);
    }
  });

  test('S7: after a one-tile pickup the LOWER layer of the same stack becomes the next trigger', async ({ page }, testInfo) => {
    testInfo.setTimeout(150_000);
    await page.setViewportSize({ width: 1600, height: 900 });
    const base = testInfo.project.use.baseURL as string;
    const cfg = makeConfig({ gameId: `g17-lower-${Date.now()}`, seat: 0, dealMode: 'manual', botCount: 3, botDifficulty: 'Medium', handCount: 4 });
    await page.goto(buildGameUrl(base, cfg), { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(1200); await dismissLobbyAndTour(page); await ensureConnected(page);
    await takeSeatByClick(page, 0); await clickDeal(page).catch(() => {});
    // Timeline of the GLOBAL pickup trigger across the whole ceremony (any seat) —
    // the single-tile phases pick top then bottom of the same stack, so a count=1
    // trigger must be immediately followed by the SAME-footprint lower layer.
    const timeline: any[] = []; const t0 = Date.now();
    while (Date.now() - t0 < 30000) {
      const d = await readStackDesignation(page, false);
      if (d && d.targetName) {
        const last = timeline[timeline.length - 1];
        if (!last || last.targetName !== d.targetName) timeline.push({ phase: d.phase, count: d.count, seatIndex: d.seatIndex, targetName: d.targetName, reachable: d.targetReachable, fx: d.footprint?.x ?? null, fy: d.footprint?.y ?? null });
      }
      if (await page.evaluate(() => { const t = (window as any).game?.client?.turn; return !!(t && t.awaitingDiscard); })) break;
      await page.waitForTimeout(180);
    }
    // find a count=1 → same-footprint, different-slot, reachable transition
    let lowerLayerNext = false; let evidencePair: any = null;
    for (let i = 0; i + 1 < timeline.length; i++) {
      const a = timeline[i], b = timeline[i + 1];
      if (a.count === 1 && a.fx != null && b.fx != null && Math.abs(a.fx - b.fx) < 3 && Math.abs(a.fy - b.fy) < 3 && a.targetName !== b.targetName && a.reachable === true && b.reachable === true) { lowerLayerNext = true; evidencePair = { a, b }; break; }
    }
    recordEvidence('g17-lower-layer-next.json', { timelineLen: timeline.length, timeline: timeline.slice(0, 12), lowerLayerNext, evidencePair,
      note: 'RED@200cad4: no targetSlots ⇒ empty timeline ⇒ cannot observe the top→bottom same-stack single-pickup advance.' });
    expect(lowerLayerNext, 'S7: a count=1 pickup must expose the SAME stack\u2019s lower layer as the next reachable trigger').toBe(true);
  });

  test('S8: a count=4 batch press on the ONE front trigger consumes EXACTLY two adjacent front stacks', async ({ page }, testInfo) => {
    testInfo.setTimeout(150_000);
    await page.setViewportSize({ width: 1600, height: 900 });
    const base = testInfo.project.use.baseURL as string;
    const cfg = makeConfig({ gameId: `g17-2stacks-${Date.now()}`, seat: 0, dealMode: 'manual', botCount: 3, botDifficulty: 'Medium', handCount: 4 });
    await page.goto(buildGameUrl(base, cfg), { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(1200); await dismissLobbyAndTour(page); await ensureConnected(page);
    await takeSeatByClick(page, 0); await clickDeal(page).catch(() => {});
    let d: any = null; const t0 = Date.now();
    while (Date.now() - t0 < 22000) {
      d = await readStackDesignation(page);
      if (d && d.count === 4 && d.targetLen === 1 && d.targetFound) break;
      if (await page.evaluate(() => { const t = (window as any).game?.client?.turn; return !!(t && t.awaitingDiscard); })) break;
      await page.waitForTimeout(220);
    }
    expect(d && d.count === 4 && d.targetLen === 1 && d.targetFound,
      `S8 needs a count=4 pickup with a single found front trigger; got ${JSON.stringify(d && { phase: d.phase, count: d.count, targetLen: d.targetLen, targetFound: d.targetFound })}`).toBe(true);
    const before = await wallStacks(page);
    await clickSlotByName(page, d.targetName);
    const after = await wallStacks(page);
    // stacks fully removed = footprints present-before, absent-after
    const removed = before.filter((s) => !after.some((q) => Math.abs(q.x - s.x) < 3 && Math.abs(q.y - s.y) < 3));
    const tilesRemoved = before.reduce((n, s) => n + s.layers, 0) - after.reduce((n, s) => n + s.layers, 0);
    const adjacent = removed.length === 2 && Math.hypot(removed[0].x - removed[1].x, removed[0].y - removed[1].y) < 20;
    recordEvidence('g17-two-stacks.json', { count: d.count, tilesRemoved, fullStacksRemoved: removed.length, removed, adjacent });
    expect(tilesRemoved, `S8: a count=4 batch must remove exactly 4 wall tiles; removed=${tilesRemoved}`).toBe(4);
    expect(removed.length, `S8: those 4 tiles must be EXACTLY two full stacks (both layers each); fullStacksRemoved=${removed.length}`).toBe(2);
    expect(adjacent, 'S8: the two consumed stacks must be adjacent at the front (one-pitch apart)').toBe(true);
  });

  test('S13 (D5 auto-drive removal): manual deal must NOT auto-take the human pickups without a real press', async ({ page }, testInfo) => {
    testInfo.setTimeout(120_000);
    await page.setViewportSize({ width: 1600, height: 900 });
    const base = testInfo.project.use.baseURL as string;
    const cfg = makeConfig({ gameId: `g17-noauto-${Date.now()}`, seat: 0, dealMode: 'manual', botCount: 3, botDifficulty: 'Medium', handCount: 4 });
    await page.goto(buildGameUrl(base, cfg), { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(1200); await dismissLobbyAndTour(page); await ensureConnected(page);
    await takeSeatByClick(page, 0); await clickDeal(page).catch(() => {});
    // WITHOUT pressing any wall tile: the human's pickups must NOT self-advance.
    // (§B one-interaction-per-batch + the single-trigger lock; auto-drive removed.)
    await page.waitForTimeout(7000);
    const obs = await page.evaluate(() => {
      /* eslint-disable @typescript-eslint/no-explicit-any */
      const g = (window as any).game; const w = g?.world;
      let hand = 0; if (w?.things) for (const t of w.things.values()) if (/^hand\.\d+@0$/.test(String(t?.slot?.name ?? ''))) hand++;
      const t = g?.client?.turn; const awaitingDiscard = !!(t && t.awaitingDiscard);
      return { handNoPress: hand, awaitingDiscard };
      /* eslint-enable @typescript-eslint/no-explicit-any */
    });
    recordEvidence('g17-no-auto-take.json', { obs,
      note: 'D5: WITHOUT a human press the manual pickup must NOT advance. RED@200cad4: world.ts driveManualDealChain auto-rolls + auto-takes the local seat ⇒ hand self-fills without any press.' });
    // D5: no client-side auto-take of the human pickups — hand stays empty until a
    // real endpoint press. RED@200cad4 (auto-drive fills it).
    expect(obs.handNoPress, `D5: without any human wall press the hand must NOT auto-advance (auto-drive removed); got ${obs.handNoPress}`).toBe(0);
  });

  test('pickup tombstone GREEN LOCK (Vasquez D1): pickup["current"]==null + isMyPickupTurn()==false post-ceremony', async ({ page }, testInfo) => {
    testInfo.setTimeout(150_000);
    await page.setViewportSize({ width: 1600, height: 900 });
    const base = testInfo.project.use.baseURL as string;
    const cfg = makeConfig({ gameId: `g17-tomb-${Date.now()}`, dealMode: 'manual', botCount: 3, botDifficulty: 'Medium', handCount: 4 });
    await page.goto(buildGameUrl(base, cfg), { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(1200); await dismissLobbyAndTour(page); await ensureConnected(page);
    await takeSeatByClick(page, 0); await clickDeal(page); await waitForPlayableHand(page, 60_000).catch(() => {});
    await page.waitForTimeout(2000);
    const post = await page.evaluate(() => {
      /* eslint-disable @typescript-eslint/no-explicit-any */
      const g = (window as any).game; const w = g?.world;
      const pu = g?.client?.pickup?.get ? g.client.pickup.get('current') : null;
      const takeBtn = Array.from(document.querySelectorAll('button')).some((b) => /take\s*\d|pick your tiles/i.test((b.textContent || '')) && (b as HTMLElement).offsetParent !== null);
      const rollVisible = (() => { const e = document.getElementById('roll-dice'); return !!e && e.offsetParent !== null; })();
      return { rawPickup: pu ? { phase: pu.phase, count: pu.count } : null, designationLen: Array.isArray((pu as any)?.targetSlots) ? (pu as any).targetSlots.length : 0, isMyPickupTurn: !!(w?.isMyPickupTurn && w.isMyPickupTurn()), takeBtnVisible: takeBtn, rollVisible };
      /* eslint-enable @typescript-eslint/no-explicit-any */
    });
    await shot(page, 'g17-pickup-tombstone.png');
    recordEvidence('g17-pickup-tombstone.json', { post, note: 'Vasquez D1 CONCEDED: full-snapshot map.clear() wipes pickup["current"] when the post-deal snapshot omits pickup ⇒ pickup null / isMyPickupTurn false is a GREEN LOCK (defensive). HUD button state is a recorded observation, not gated (D1 rules pickup-clear GREEN, not the HUD).' });
    // GREEN LOCK (Vasquez D1) — pickup state clears on the full-update path; assert
    // the defensive must-preserve, NOT a RED. (The takeBtn HUD is recorded only.)
    expect(post.rawPickup, `D1: pickup["current"] must be null post-ceremony; got ${JSON.stringify(post.rawPickup)}`).toBeNull();
    expect(post.designationLen, 'D1: targetSlots designation must be empty post-ceremony').toBe(0);
    expect(post.isMyPickupTurn, 'D1: isMyPickupTurn() must be false post-ceremony').toBe(false);
  });
});
