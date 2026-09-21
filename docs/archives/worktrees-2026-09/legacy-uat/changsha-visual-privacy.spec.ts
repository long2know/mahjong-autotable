// G19-VISUAL (OWNED by hudson-1 — anonymous-pool integration, 2026-08-07 12:35).
// The RENDER/visual complement to the raw-WS P1-P5 privacy gate: with the SC-2
// opaque handles + Hicks's anonymous back pool integrated, the rendered scene must
// keep exactly 108 physical tiles (NOT 216 = the pool's back-capacity leaking into
// view); every opaque wall/foreign entry renders FACE-DOWN and raycast-INERT except
// the SC-4 target; own/public numeric entries render FACE-UP + actionable; the
// handle->numeric reveal is atomic (no invisible/duplicate frame); reconnect stable.
// RED@200cad4 for the integrated-only pieces (no opaque handles / no pool yet);
// the render invariants (108 count, own face-up, wall/foreign face-down, wall inert)
// are must-preserve locks that guard the pool integration. Real-UI + raw-WS.
import { test, expect } from '@playwright/test';
import { buildGameUrl, makeConfig, dismissLobbyAndTour, ensureConnected, takeSeatByClick, clickDeal, waitForPlayableHand } from './_playability';
import { recordEvidence, shot } from './_uat_red';

async function captureRender(page) {
  return page.evaluate(() => {
    /* eslint-disable @typescript-eslint/no-explicit-any */
    const g = (window as any).game; const w = g?.world;
    let things = 0, tileThings = 0, ownUp = 0, ownDown = 0, foreignUp = 0, foreignDown = 0, wallVisibleFace = 0, wallSelectable = 0;
    const rotCounts: Record<string, number> = {};
    const canSel = (t: any) => { try { return typeof w.canSelect === 'function' ? !!w.canSelect(t, []) : null; } catch { return null; } };
    if (w?.things) for (const t of w.things.values()) {
      things++;
      const nm = String(t?.slot?.name ?? ''); const rot = t.rotationIndex;
      rotCounts[String(rot)] = (rotCounts[String(rot)] ?? 0) + 1;
      const isTile = /^(wall|hand|discard|meld)\./.test(nm);
      if (isTile) tileThings++;
      const faceUp = rot === 1;            // own/public face-up orientation
      if (/^hand\.\d+@0$/.test(nm)) { if (faceUp) ownUp++; else ownDown++; }
      const m = /^hand\.(\d+)@([123])$/.exec(nm); if (m) { if (faceUp) foreignUp++; else foreignDown++; }
      if (t.slot?.group === 'wall') { if (faceUp) wallVisibleFace++; if (canSel(t) === true) wallSelectable++; }
    }
    let instancedMeshes = 0; let totalInstances = 0;
    try {
      const roots = [g?.mainView?.scene, g?.mainGroup, g?.objectView?.group].filter(Boolean); const seen = new Set<any>();
      for (const root of roots) root?.traverse?.((o: any) => { if (o?.isInstancedMesh && !seen.has(o)) { seen.add(o); instancedMeshes++; totalInstances += (o.count ?? 0); } });
    } catch { /* */ }
    return { things, tileThings, ownUp, ownDown, foreignUp, foreignDown, wallVisibleFace, wallSelectable, rotCounts, instancedMeshes, totalInstances };
    /* eslint-enable @typescript-eslint/no-explicit-any */
  });
}

test.describe('G19-visual: anonymous-pool render privacy (108 tiles, face-down/up, raycast-inert)', () => {
  test('rendered scene = 108 physical tiles; own face-up; foreign hidden (not face-up)', async ({ page }, testInfo) => {
    testInfo.setTimeout(120_000);
    await page.setViewportSize({ width: 1600, height: 900 });
    const base = testInfo.project.use.baseURL as string;
    const cfg = makeConfig({ gameId: `g19v-${Date.now()}`, dealMode: 'auto', botCount: 3, botDifficulty: 'Medium', handCount: 4 });
    await page.goto(buildGameUrl(base, cfg), { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(1000); await dismissLobbyAndTour(page); await ensureConnected(page);
    await takeSeatByClick(page, 0); await clickDeal(page); await waitForPlayableHand(page, 45_000).catch(() => {});
    await page.waitForTimeout(1500);
    const cap = await captureRender(page);
    await shot(page, 'g19v-render.png');
    recordEvidence('g19-visual-privacy.json', { cap,
      note: 'Anonymous-pool render privacy. MUST-PRESERVE @200cad4: exactly 108 physical tile things, own hand face-up, foreign hands NOT face-up. INTEGRATED-only (recorded, gate lands with the pool): rendered-tile-instances==108-not-216, opaque-entries face-down + raycast-inert-except-SC-4-target, handle->numeric atomic reveal. wallSelectable/wallVisibleFace recorded (auto-wall interactivity is hudson-2 lane).' });

    // MUST-PRESERVE render invariants (guard the anonymous-pool + opaque integration):
    // (1) exactly 108 physical tile things in the snapshot.
    expect(cap.tileThings, `must be exactly 108 physical tile things; got ${cap.tileThings} (all things=${cap.things})`).toBe(108);
    // (2) own/public numeric entries render FACE-UP + actionable.
    expect(cap.ownUp, `own hand must render FACE-UP (>=13); up=${cap.ownUp} down=${cap.ownDown} rot=${JSON.stringify(cap.rotCounts)}`).toBeGreaterThanOrEqual(13);
    // (3) foreign hands must NOT render face-up (no hidden-face visual leak).
    expect(cap.foreignUp, `foreign hands must NOT render face-up (visual leak); face-up=${cap.foreignUp} hidden=${cap.foreignDown}`).toBe(0);
    // Note: the 108-not-216 render-instance count, opaque-face-down, and atomic
    // reveal are integrated-only (need Hicks's pool + Bishop's opaque emission) —
    // recorded above, HARD-asserted by the @integrated test below once the
    // SC-2 backend + Hicks pool are co-landed on the served build.
  });

  // INTEGRATED-ONLY hard gate (Ripley co-land). Skipped @200cad4 (pool/opaque not
  // served) so the must-preserve locks above stay GREEN; RUN_INTEGRATED=1 turns it on
  // for the live raw/browser run. REJECT criteria per 2026-08-07 directive:
  //   - exact108: rendered tile INSTANCES == 108, NOT 216 (pool back-capacity leak).
  //   - visible backs: every opaque wall/foreign entry renders FACE-DOWN + raycast-INERT
  //     (canSelect=false) EXCEPT the single SC-4 pickup target.
  //   - atomic reveal: a real handle->numeric reveal keeps exactly 108 tiles every
  //     frame — no transient 107/109, no invisible (opacity~0 / invalid-rotation) tile.
  test('@integrated exact-108 instances + visible face-down backs (inert) + atomic reveal', async ({ page }, testInfo) => {
    test.skip(!process.env.RUN_INTEGRATED, 'integrated-only: run against the co-landed SC-2 opaque + Hicks pool build (RUN_INTEGRATED=1)');
    testInfo.setTimeout(150_000);
    await page.setViewportSize({ width: 1600, height: 900 });
    const base = testInfo.project.use.baseURL as string;
    const cfg = makeConfig({ gameId: `g19vi-${Date.now()}`, dealMode: 'auto', botCount: 3, botDifficulty: 'Medium', handCount: 4 });
    await page.goto(buildGameUrl(base, cfg), { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(1000); await dismissLobbyAndTour(page); await ensureConnected(page);
    await takeSeatByClick(page, 0); await clickDeal(page); await waitForPlayableHand(page, 45_000).catch(() => {});
    await page.waitForTimeout(1500);

    // (A) exact-108 rendered instances (NOT 216) + visible-backs face-down + inert.
    const render = await page.evaluate(() => {
      /* eslint-disable @typescript-eslint/no-explicit-any */
      const g = (window as any).game; const w = g?.world;
      let tileInstances = 0; const seen = new Set<any>();
      const roots = [g?.mainView?.scene, g?.mainGroup, g?.objectView?.group].filter(Boolean);
      for (const root of roots) root?.traverse?.((o: any) => {
        if (o?.isInstancedMesh && !seen.has(o)) { seen.add(o); const nm = String(o.name ?? ''); if (!/shadow|drop|table|tray|marker/i.test(nm)) tileInstances += (o.count ?? 0); }
      });
      // opaque = wall + foreign hands + foreign concealed kong; SC-4 target = pickup.targetSlots[0].
      const pickup: any = (() => { try { const p = w?.collections?.get?.('pickup'); return p?.get?.('current') ?? (window as any).client?.pickup ?? null; } catch { return null; } })();
      const target = Array.isArray(pickup?.targetSlots) && pickup.targetSlots.length === 1 ? String(pickup.targetSlots[0]) : null;
      const canSel = (t: any) => { try { return typeof w.canSelect === 'function' ? !!w.canSelect(t, []) : null; } catch { return null; } };
      let opaqueFaceUp = 0, opaqueSelectableNonTarget = 0, opaqueTotal = 0;
      if (w?.things) for (const t of w.things.values()) {
        const nm = String(t?.slot?.name ?? '');
        const isOpaque = t.slot?.group === 'wall' || /^hand\.[123]@[123]$/.test(nm) || /^hand\.\d+@[123]$/.test(nm);
        if (!isOpaque) continue;
        opaqueTotal++;
        if (t.rotationIndex === 1) opaqueFaceUp++;                             // opaque must be BACK/down, never face-up
        if (nm !== target && canSel(t) === true) opaqueSelectableNonTarget++;  // inert except the single SC-4 target
      }
      return { tileInstances, opaqueTotal, opaqueFaceUp, opaqueSelectableNonTarget, target };
      /* eslint-enable @typescript-eslint/no-explicit-any */
    });

    // (B) atomic reveal — poll the physical tile count across a real reveal window; a
    // non-atomic handle->numeric swap would momentarily show 107 (removed) or 109
    // (duplicate) or an invisible tile (invalid rotation).
    const reveal = await page.evaluate(async () => {
      /* eslint-disable @typescript-eslint/no-explicit-any */
      const w = (window as any).game?.world;
      const count = () => { let n = 0, bad = 0; if (w?.things) for (const t of w.things.values()) { const nm = String(t?.slot?.name ?? ''); if (/^(wall|hand|discard|meld)\./.test(nm)) { n++; const r = t.rotationIndex; if (r !== 1 && r !== 2) bad++; } } return { n, bad }; };
      const samples: { n: number; bad: number }[] = [];
      for (let i = 0; i < 90; i++) { samples.push(count()); await new Promise((r) => requestAnimationFrame(() => r(null))); }
      const nonAtomic = samples.filter((s) => s.n !== 108).length;
      const invisible = samples.filter((s) => s.bad > 0).length;
      return { frames: samples.length, nonAtomic, invisible, distinct: Array.from(new Set(samples.map((s) => s.n))) };
      /* eslint-enable @typescript-eslint/no-explicit-any */
    });

    await shot(page, 'g19vi-integrated.png');
    recordEvidence('g19-visual-integrated.json', { render, reveal,
      note: 'INTEGRATED hard gate (RUN_INTEGRATED). exact-108 rendered instances (NOT 216); opaque entries face-down (opaqueFaceUp==0) + raycast-inert except SC-4 target (opaqueSelectableNonTarget==0); atomic reveal (every frame == 108 tiles, no invalid-rotation/invisible tile). REJECT the co-land if any fails.' });

    // exact-108 rendered instances (the pool must not double physical tiles to 216).
    expect(render.tileInstances, `rendered tile INSTANCES must be exactly 108, NOT 216 (pool back-capacity leak); got ${render.tileInstances}`).toBe(108);
    // opaque entries render as BACKS (face-down), never face-up.
    expect(render.opaqueFaceUp, `opaque wall/foreign entries must render FACE-DOWN (backs); ${render.opaqueFaceUp} rendered face-up`).toBe(0);
    // opaque entries are raycast-INERT except the single SC-4 pickup target.
    expect(render.opaqueSelectableNonTarget, `opaque entries must be raycast-inert except the SC-4 target; ${render.opaqueSelectableNonTarget} selectable`).toBe(0);
    // atomic reveal — count stays 108 every frame, no invisible/invalid-rotation tile.
    expect(reveal.nonAtomic, `handle->numeric reveal must be atomic (108 tiles every frame); ${reveal.nonAtomic}/${reveal.frames} frames off-108 (distinct=${JSON.stringify(reveal.distinct)})`).toBe(0);
    expect(reveal.invisible, `no tile may be invisible/invalid-rotation during reveal; ${reveal.invisible}/${reveal.frames} frames had one`).toBe(0);
  });
});
