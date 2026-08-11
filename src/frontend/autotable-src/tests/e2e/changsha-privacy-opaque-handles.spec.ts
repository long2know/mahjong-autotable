// G19 (OWNED by hudson-1) — raw-WS opaque-handle privacy + inbound authority.
// Corrected identity model (Ripley 2026-08-07): opaque handles are non-numeric
// STRINGS keyed by durable playerId + server secret (NOT ints, NOT per-connection).
// P-1 opaque + non-brute-forceable; P-2 own/public keep numeric real ids;
// P-3 own actionable; P-4 same durable-playerId reconnect ⇒ byte-identical;
// P-5 two distinct players ⇒ uncorrelated. Plus inbound authority: a client
// things push must not be observed by peers. REAL observation only.
import { test, expect, type Page } from '@playwright/test';
import { buildGameUrl, makeConfig, dismissLobbyAndTour, ensureConnected, takeSeatByClick, clickDeal, waitForPlayableHand } from './_playability';
import { realDragWallTile, recordEvidence } from './_uat_red';
import {
  newSink, attachRawWsCapture, handleMap, faceMap, readPlayerId, faceLeaks,
  handleIsOpaque, allNumeric, handleHealth, crossViewerLinkable, multisetOverlap,
} from './helpers/changsha-raw-ws';

function stripSeed(url: string): string { const u = new URL(url); u.searchParams.delete('seed'); return u.toString(); }

test.describe('G19 raw-WS opaque-handle privacy (P1–P5) + inbound authority', () => {
  test('P1 opaque + P2 numeric + P3 actionable + P4 durable-stable + P5 unlinkable', async ({ browser }, testInfo) => {
    testInfo.setTimeout(180_000);
    const gameId = `g19-priv-${Date.now()}`;
    const base = testInfo.project.use.baseURL as string;
    // Two DISTINCT durable identities via independent contexts; server-random seed.
    const ctxA = await browser.newContext({ viewport: { width: 1280, height: 800 } });
    const a = await ctxA.newPage(); const sinkA = newSink(); attachRawWsCapture(a, sinkA);
    await a.goto(stripSeed(buildGameUrl(base, makeConfig({ gameId, seat: 0, dealMode: 'auto', botCount: 3, botDifficulty: 'Medium', handCount: 4 }))), { waitUntil: 'domcontentloaded' });
    await a.waitForTimeout(1000); await dismissLobbyAndTour(a); await ensureConnected(a);
    await takeSeatByClick(a, 0); await clickDeal(a); await waitForPlayableHand(a, 60_000).catch(() => {}); await a.waitForTimeout(2500);

    const ctxB = await browser.newContext({ viewport: { width: 1280, height: 800 } });
    const b = await ctxB.newPage(); const sinkB = newSink(); attachRawWsCapture(b, sinkB);
    await b.goto(stripSeed(buildGameUrl(base, makeConfig({ gameId, seat: 1, dealMode: 'auto', botCount: 3, botDifficulty: 'Medium', handCount: 4 }))), { waitUntil: 'domcontentloaded' });
    await b.waitForTimeout(1000); await dismissLobbyAndTour(b); await ensureConnected(b); await takeSeatByClick(b, 1); await b.waitForTimeout(2500);

    const pidA = await readPlayerId(a); const pidB = await readPlayerId(b);
    const leaksA = faceLeaks(sinkA.raw, 0);

    const ownFaceUp = await a.evaluate(() => { const w = (window as any).game?.world; let up = 0, tot = 0; if (w?.things) for (const t of w.things.values()) { const nm = String(t?.slot?.name ?? ''); if (/^hand\.\d+@0$/.test(nm)) { tot++; if (t.rotationIndex === 1) up++; } } return { up, tot }; });

    const aHidden = await handleMap(a, '^(wall\\.|hand\\.\\d+@[123])');
    const bHidden = await handleMap(b, '^(wall\\.|hand\\.\\d+@[123])');
    const p5 = crossViewerLinkable(aHidden, bHidden);
    // §D2 entitlement: P-2 NUMERIC real ids = own concealed hand + ALL discards +
    // ALL exposed melds (+ concealed-kong-to-owner). P-1 OPAQUE = wall + foreign
    // concealed hands + foreign concealed kong. At DEAL time there are no
    // discards/melds/kongs, so P-2 reduces to the own hand and P-1 to wall +
    // foreign hands; the foreign-concealed-kong opacity is covered by the same
    // opaque-handle rule (any foreign concealed tile) once such a meld exists.
    const aPublic = await handleMap(a, '^(hand\\.\\d+@0$|discard\\.|meld\\.\\d+\\.\\d+@0)');
    const p2Numeric = allNumeric(Object.values(aPublic));
    const opaqueFails = Object.entries(aHidden).filter(([, h]) => !handleIsOpaque(h)).length;
    const health = handleHealth(Object.values(await handleMap(a, '^(wall\\.|hand\\.|discard\\.|meld\\.)')));
    // reconstruction corroboration
    const handRecon = multisetOverlap(await faceMap(a, '^hand\\.\\d+@1$'), await faceMap(b, '^hand\\.\\d+@1$'));

    const aOwnBefore = await handleMap(a, '^hand\\.\\d+@0$');
    await a.locator('#disconnect').click({ timeout: 5000 }).catch(() => {});
    await a.waitForTimeout(1000);
    await a.locator('#connect').click({ timeout: 5000 }).catch(() => {});
    await a.waitForTimeout(2500);
    const pidAafter = await readPlayerId(a);
    const aOwnAfter = await handleMap(a, '^hand\\.\\d+@0$');
    let p4Changed = 0; for (const k of Object.keys(aOwnBefore)) if (k in aOwnAfter && aOwnBefore[k] !== aOwnAfter[k]) p4Changed++;

    recordEvidence('g19-privacy-opaque-handles.json', {
      playerIds: { pidA, pidB, pidAafter, distinctAB: pidA !== pidB, aDurableStable: pidA === pidAafter },
      P1_opaqueFails: opaqueFails, P2_publicNumeric: p2Numeric, P3_ownFaceUp: ownFaceUp,
      P4_changed: p4Changed, P5_unlinkability: p5, handRecon, health, sampleHidden: Object.entries(aHidden).slice(0, 6), wireFaceLeaks: leaksA.slice(0, 6),
    });
    await ctxA.close(); await ctxB.close();

    expect(leaksA, `wire must carry no hidden-tile face; leaks=${JSON.stringify(leaksA.slice(0, 5))}`).toEqual([]);
    expect(pidA && pidB && pidA !== pidB, `two distinct durable PlayerIds required (A=${pidA}, B=${pidB})`).toBe(true);
    expect(opaqueFails, `P-1: hidden handles must be opaque non-numeric strings; ${opaqueFails} numeric e.g. ${JSON.stringify(Object.entries(aHidden).slice(0, 4))}`).toBe(0);
    expect(p5.sameHandle, `P-1/P-5: same physical tile must differ across players (unlinkable); shared ${p5.sameHandle}/${p5.compared}`).toBe(0);
    expect(p2Numeric, 'P-2: own/public tiles must keep numeric real ids (actionable)').toBe(true);
    expect(pidA === pidAafter, `P-4: reconnect must retain durable PlayerId (before=${pidA}, after=${pidAafter})`).toBe(true);
    expect(p4Changed, `P-4: same-PlayerId reconnect ⇒ byte-identical handles; ${p4Changed} changed`).toBe(0);
    expect(health.collisions, `handle collisions=${health.collisions}`).toBe(0);
    expect(health.precisionRisk, `numeric handles must be JS-safe; risky=${health.precisionRisk}`).toBe(0);
    expect(ownFaceUp.up, 'P-3: own hand actionable/face-up').toBeGreaterThanOrEqual(13);
  });

  test('inbound authority: a peer must not observe a client wall things push', async ({ browser }, testInfo) => {
    testInfo.setTimeout(150_000);
    const gameId = `g19-inbound-${Date.now()}`;
    const base = testInfo.project.use.baseURL as string;
    const ctxB = await browser.newContext({ viewport: { width: 1000, height: 700 } });
    const b = await ctxB.newPage(); const sinkB = newSink(); attachRawWsCapture(b, sinkB);
    await b.goto(buildGameUrl(base, makeConfig({ gameId, seat: 1, dealMode: 'auto', botCount: 2, botDifficulty: 'Medium', handCount: 4 })), { waitUntil: 'domcontentloaded' });
    await b.waitForTimeout(1000); await dismissLobbyAndTour(b); await ensureConnected(b); await takeSeatByClick(b, 1); await b.waitForTimeout(1500);

    const ctxA = await browser.newContext({ viewport: { width: 1000, height: 700 } });
    const a = await ctxA.newPage();
    await a.goto(buildGameUrl(base, makeConfig({ gameId, seat: 0, dealMode: 'auto', botCount: 2, botDifficulty: 'Medium', handCount: 4 })), { waitUntil: 'domcontentloaded' });
    await a.waitForTimeout(1000); await dismissLobbyAndTour(a); await ensureConnected(a); await takeSeatByClick(a, 0);
    await clickDeal(a); await waitForPlayableHand(a, 60_000).catch(() => {}); await a.waitForTimeout(1500);

    const wallB_before = await b.evaluate(() => { const w = (window as any).game?.world; let n = 0; if (w?.things) for (const t of w.things.values()) if (t?.slot?.group === 'wall') n++; return n; });
    // offense = a GENUINE real-pointer wall drag by seat 0 (the client things push)
    const off = await realDragWallTile(a); await a.waitForTimeout(1500);
    const peer = await b.evaluate(() => { const w = (window as any).game?.world; let claimed = 0, n = 0; if (w?.things) for (const t of w.things.values()) if (t?.slot?.group === 'wall') { n++; if (t.claimedBy != null) claimed++; } return { n, claimed }; });

    recordEvidence('g19-inbound-authority.json', { offenderHeld: off.held, wallB_before, peer,
      discriminators: { peerWallUnchanged: peer.n === wallB_before, peerNoForeignClaim: peer.claimed === 0 } });
    await ctxA.close(); await ctxB.close();

    expect(peer.claimed, 'a peer must NOT observe an inbound client wall claim/move').toBe(0);
    expect(peer.n, 'peer authoritative wall unchanged by an offender push').toBe(wallB_before);
  });
});
