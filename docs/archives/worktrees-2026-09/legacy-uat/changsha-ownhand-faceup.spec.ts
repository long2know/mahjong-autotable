// UAT (Hudson) — Suite A / B3: an UNSEATED connect followed by a real TakeSeat
// must show the local player's OWN hand face-up WITHOUT a page reload, while the
// other seats' hands stay face-down — and the own hand must be delivered as a
// server-authoritative dealt hand (the client must NOT have to emit the wall/
// deal `things` burst itself to produce its own tiles).
//
// RED baseline (200cad4): the deal is client-driven (relay), so producing the
// local hand emits outbound `["things", <id>, {...}]` frames — the own hand is
// locally fabricated, not authoritatively delivered. This pins the desired
// authoritative-seat-view behaviour; expected to FAIL until the deal is
// server-owned.
//
// SCOPE (Ripley R-B, 2026-08-07; confirmed BE-5): G6 BINDS to G19 entitlement and
// reads the server's per-viewer projection straight off the RAW WS frames, NOT the
// client world (where the fragile isLocalSeatHand→rotationIndex override lives — a
// seat-GUESS that failed live and rendered own tiles face-down). The fix is backend
// BE-5: TakeSeat rebinds connection.ViewerSeat=seatIndex (endpoint:795) then
// re-projects a full snapshot (:817-820); translator:559 authors
// `viewerSeat==seat ? FaceUp : FaceDown` and reveals the owner's own face. This
// proves ownership as a PAIR a client seat-guess cannot fake:
//   • server TRUTH straight off the wire (raw `things` ThingInfo):
//       (a) FACE — the own hand (hand.*@myseat) arrives with face != null (REVEALED
//           to its owner) while FOREIGN hands arrive face==null (concealed);
//       (a) ROTATION — the own hand's server-authored rotationIndex set is DISJOINT
//           from the foreign set (own FaceUp vs foreign FaceDown);
//       (b) ENTITLEMENT — the own hand's identity handle is a real NUMERIC id, while
//           FOREIGN handles are OPAQUE strings (the server saying "yours vs not").
//   • rendered result: the own hand displays FACE-UP; foreign hands FACE-DOWN.
// RED@200cad4 (proven by probe): the own hand is projected to the STALE viewer — its
// raw face==null and its rotation (2) is IDENTICAL to foreign (2), and foreign ids
// leak numeric. All flip GREEN on the merged image once BE-5 (ViewerSeat rebind +
// re-projection) and the G19 opaque projection land. The full P1–P5 opaque-schema
// detail lives in changsha-privacy-opaque-handles (owned there).

import { test, expect } from '@playwright/test';
import {
  defangOverlays, dismissLobbyAndTour, ensureConnected, takeSeatByClick,
  clickDeal, waitForGameObject, readSeat,
} from './_playability';
import {
  resolveBase, uatGameUrl, freshGameId, recordOutboundWs, ownVsOppHand, recordRedEvidence,
} from './_uat-changsha';
import { handleMap, handleIsOpaque, allNumeric, newSink, attachRawWsCapture } from './helpers/changsha-raw-ws';

test.describe('UAT G6 — unseated → TakeSeat shows own hand face-up (server-authoritative), no reload', () => {
  test('own hand is delivered face-up without a reload or a client-side deal burst; others stay face-down', async ({ page, baseURL }) => {
    test.setTimeout(120_000);
    const base = resolveBase(baseURL);
    const gameId = freshGameId('b3-own-hand');
    // Unseated connect: NO seat param on the URL.
    const url = uatGameUrl(base, { gameId, dealMode: 'auto' });

    const ws = recordOutboundWs(page);
    const rawSink = newSink();
    attachRawWsCapture(page, rawSink);   // capture server→client `things` frames (BE-5 truth)
    await defangOverlays(page);
    await page.goto(url, { waitUntil: 'domcontentloaded' });
    expect(await waitForGameObject(page), 'window.game never booted').toBe(true);
    await dismissLobbyAndTour(page);
    expect(await ensureConnected(page), 'client never connected').toBe(true);
    expect(await readSeat(page), 'must start UNSEATED (no seat)').toBeNull();

    // Sentinel survives iff no full-page reload happens across TakeSeat + deal.
    await page.evaluate(() => { (window as unknown as { __uatNoReload?: string }).__uatNoReload = 'ALIVE'; });

    // Real TakeSeat click.
    await takeSeatByClick(page, 0);
    expect(await readSeat(page), 'TakeSeat must seat the human at 0').toBe(0);

    // Real Deal; capture the client's OWN outbound traffic during the deal.
    ws.clear();
    await clickDeal(page);
    await page.waitForTimeout(3800);

    const sentinelAlive = await page.evaluate(
      () => (window as unknown as { __uatNoReload?: string }).__uatNoReload === 'ALIVE',
    );
    const hand = await ownVsOppHand(page);
    const dealBurst = ws.countThingsMutations();

    // --- Server-AUTHORED face/rotation straight off the RAW WS frames (BE-5) ----
    // Dimension (a): read the server's per-viewer projection from the WIRE, not the
    // client world (where the fragile override could fake a face-up own hand). At
    // 200cad4 the own hand is projected to the STALE viewer → raw face==null and the
    // SAME rotation as foreign; BE-5 (ViewerSeat rebind + re-projection) reveals the
    // owner's face and authors own=FaceUp vs foreign=FaceDown.
    const rawFace = (() => {
      const lastBySlot: Record<string, { rot: number; face: unknown; faceProvided: boolean }> = {};
      for (const { v } of rawSink.raw) {
        const nm = String((v as { slotName?: string })?.slotName ?? '');
        if (!/^hand\.\d+@\d+$/.test(nm)) continue;
        const vi = v as { rotationIndex?: number; face?: unknown };
        lastBySlot[nm] = { rot: Number(vi.rotationIndex), face: vi.face ?? null, faceProvided: ('face' in (v as object)) };
      }
      const own = Object.entries(lastBySlot).filter(([n]) => /@0$/.test(n)).map(([, r]) => r);
      const foreign = Object.entries(lastBySlot).filter(([n]) => /@[123]$/.test(n)).map(([, r]) => r);
      const ownRot = new Set(own.map((r) => r.rot));
      const forRot = new Set(foreign.map((r) => r.rot));
      const disjoint = own.length > 0 && foreign.length > 0 && [...ownRot].every((r) => !forRot.has(r));
      return {
        ownCount: own.length, foreignCount: foreign.length,
        ownFaceRevealed: own.filter((r) => r.faceProvided && r.face !== null).length,
        foreignFaceConcealed: foreign.length > 0 && foreign.every((r) => !r.faceProvided || r.face === null),
        serverRotDisjoint: disjoint, ownRot: [...ownRot], foreignRot: [...forRot],
      };
    })();
    // The server must AUTHOR the owner's own hand as revealed (>=13 raw faces != null).
    const serverOwnFaceRevealed = rawFace.ownCount > 0 && rawFace.ownFaceRevealed >= 13;

    // --- Server-entitlement truth (raw-WS handle == thing.index) -------------
    // The own hand must be ENTITLED (real numeric ids); foreign hands must be
    // OPAQUE handles — the server's authoritative "yours vs not-yours" signal
    // (G19). Read straight off the projected handle so a client seat-guess or the
    // fragile rotation override cannot fabricate ownership.
    const ownEnt = await handleMap(page, '^hand\\.\\d+@0$');
    const foreignEnt = await handleMap(page, '^hand\\.\\d+@[123]$');
    const ownEntitledNumeric = allNumeric(Object.values(ownEnt));
    const foreignVals = Object.values(foreignEnt);
    const foreignOpaque = foreignVals.length > 0 && foreignVals.every((h) => handleIsOpaque(h));
    // Rendered face straight off rotationIndex (authoritative), PAIRED with the
    // entitlement above: entitled ⇒ own face-up, opaque ⇒ foreign face-down.
    const rendered = await page.evaluate(() => {
      /* eslint-disable @typescript-eslint/no-explicit-any */
      const w = (window as any).game?.world; let ownUp = 0, ownTot = 0, forUp = 0, forTot = 0;
      if (w?.things) for (const t of w.things.values()) {
        const nm = String(t?.slot?.name ?? ''); const m = /^hand\.\d+@(\d+)$/.exec(nm); if (!m) continue;
        if (Number(m[1]) === 0) { ownTot++; if (t.rotationIndex === 1) ownUp++; }
        else { forTot++; if (t.rotationIndex === 1) forUp++; }
      }
      return { ownUp, ownTot, forUp, forTot };
      /* eslint-enable @typescript-eslint/no-explicit-any */
    });
    const ownRenderedFaceUp = rendered.ownUp >= 13;
    const foreignRenderedFaceDown = rendered.forTot > 0 && rendered.forUp === 0;

    // Face-orientation: own hand rotations must be DISJOINT from the opponents'
    // face-down rotation set (own is presented face-up toward the local player).
    const oppSet = new Set(hand.oppRots);
    const ownFaceUp = hand.ownRots.length > 0 && hand.ownRots.every((r) => !oppSet.has(r));

    const metrics = {
      reloadHappened: !sentinelAlive,
      ownHandCount: hand.own,
      oppHandCount: hand.opp,
      ownFaceUpDisjointFromOpp: ownFaceUp,
      clientDealBurstThings: dealBurst,
      ownEntitledNumeric,
      foreignHandsOpaque: foreignOpaque,
      ownRenderedFaceUp,
      foreignRenderedFaceDown,
      foreignHandKeysSample: foreignVals.slice(0, 4),
      // server-authored (raw-WS) truth — the BE-5 discriminators
      serverOwnFaceRevealed,
      serverOwnFaceRevealedCount: rawFace.ownFaceRevealed,
      serverForeignFaceConcealed: rawFace.foreignFaceConcealed,
      serverRotDisjoint: rawFace.serverRotDisjoint,
      serverOwnRot: rawFace.ownRot,
      serverForeignRot: rawFace.foreignRot,
    };
    recordRedEvidence({
      id: 'G6-ownhand-faceup',
      expected: 'Unseated→TakeSeat delivers a face-up own hand (>=13) with no reload and ZERO client deal-burst; the SERVER authors it (raw own-hand face != null + rotation disjoint from foreign) and ENTITLEMENT proves ownership (own=real numeric ids face-up, FOREIGN=opaque handles face-down).',
      observed: `reload=${metrics.reloadHappened}, own=${hand.own}, opp=${hand.opp}, ownFaceUp=${ownFaceUp}, clientDealBurst=${dealBurst}, ownEntitledNumeric=${ownEntitledNumeric}, foreignOpaque=${foreignOpaque}, ownRenderedFaceUp=${ownRenderedFaceUp}, foreignRenderedFaceDown=${foreignRenderedFaceDown}, serverOwnFaceRevealed=${serverOwnFaceRevealed}(${rawFace.ownFaceRevealed}/${rawFace.ownCount}), serverRotDisjoint=${rawFace.serverRotDisjoint}(own=${JSON.stringify(rawFace.ownRot)} foreign=${JSON.stringify(rawFace.foreignRot)}), serverForeignFaceConcealed=${rawFace.foreignFaceConcealed}`,
      metrics,
    });

    // Rendering outcome (should hold): own hand present + face-up, no reload.
    expect(sentinelAlive, 'TakeSeat must not force a full-page reload').toBe(true);
    expect(hand.own, 'local seat must receive a full own hand (>=13)').toBeGreaterThanOrEqual(13);
    expect(ownFaceUp, 'own hand must be face-up (rotation disjoint from opponents’ face-down set)').toBe(true);
    expect(hand.opp, 'opponents must hold face-down hands').toBeGreaterThan(0);

    // --- G19-bound ownership pairing (server ENTITLEMENT + rendered result) ---
    // Defensive GREEN lock: the own hand IS the viewer's entitlement — real ids.
    expect(ownEntitledNumeric, 'own hand must arrive as ENTITLED real numeric ids (server ownership truth)').toBe(true);
    // RED@200cad4 → GREEN after the server-authoritative deal + G19 projection:
    // foreign hands must be the server's OPAQUE projection ("not yours"), never
    // numeric real ids. On the relay baseline they leak as numeric ⇒ RED.
    expect(foreignOpaque, 'foreign hands must arrive as OPAQUE handles (server entitlement: not the viewer’s tiles)').toBe(true);
    // Rendered result must MATCH the entitlement: own face-up BECAUSE entitled (not
    // a client seat-guess); foreign face-down BECAUSE opaque.
    expect(ownRenderedFaceUp, 'displayed own hand must be face-up (>=13 tiles at rotationIndex 1)').toBe(true);
    expect(foreignRenderedFaceDown, 'displayed foreign hands must be face-down (none at rotationIndex 1)').toBe(true);

    // --- Server-AUTHORED face/rotation (dimension a, read from the WIRE) ---------
    // These read the server's projection directly off the raw frames, so the client
    // isLocalSeatHand→rotation override CANNOT fake them. RED@200cad4 (own raw
    // face==null and own rotation == foreign rotation) → GREEN after BE-5 rebinds
    // ViewerSeat on TakeSeat and re-projects (translator:559 reveals the owner's face
    // and authors own=FaceUp vs foreign=FaceDown).
    expect(serverOwnFaceRevealed, `server must AUTHOR the own hand as REVEALED to its owner (raw things face != null for >=13 own tiles); saw ${rawFace.ownFaceRevealed}/${rawFace.ownCount} — RED@200cad4 where the own hand is projected to the stale viewer with face==null`).toBe(true);
    expect(rawFace.serverRotDisjoint, `server must AUTHOR own=FaceUp vs foreign=FaceDown (raw rotationIndex sets disjoint); own=${JSON.stringify(rawFace.ownRot)} foreign=${JSON.stringify(rawFace.foreignRot)} — RED@200cad4 where own and foreign share the same rotation`).toBe(true);
    // Defensive privacy lock (GREEN both): foreign faces are concealed on the wire.
    expect(rawFace.foreignFaceConcealed, 'foreign hands must be face-concealed on the wire (raw face==null)').toBe(true);

    // Authoritative-delivery discriminator — RED on the relay baseline.
    expect(
      dealBurst,
      'own hand must be delivered by the server — the client must NOT emit a wall/deal things burst',
    ).toBe(0);
  });
});
