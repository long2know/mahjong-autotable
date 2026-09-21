// UAT (Hudson) — Suite B (SKELETON): persisted-failure replay.
//
// Suite A proves acceptance behaviours against a FRESH isolated game. Suite B is
// the complementary lane: given a machine-readable RED discriminator captured by
// Suite A (tests/e2e/uat-artifacts/red-evidence.json), replay the SAME scenario
// deterministically so a fix can be verified and a regression can never silently
// return.
//
// Status: SKELETON (test.fixme). It reads the RED-evidence ledger written by
// Suite A and asserts each recorded discriminator has now been driven to GREEN.
// It stays fixme until the authoritative implementation lands, because on the
// 200cad4 baseline every recorded metric is (correctly) still RED. Activating it
// (remove `.fixme`) turns the ledger into a live regression gate.
//
// The replay is data-driven: each ledger entry names the behaviour, the desired
// outcome, and the metrics that must flip. A dispatch table maps behaviour ids
// to their fresh-state re-run so a single persisted failure can be re-executed
// in isolation without re-running the whole suite.

import { test, expect } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import { UAT_ARTIFACT_DIR } from './_uat-changsha';

interface LedgerEntry {
  id: string;
  expected: string;
  observed: string;
  metrics: Record<string, number | string | boolean | null>;
  at: string;
  commit: string;
}

function loadLedger(): LedgerEntry[] {
  const file = path.join(UAT_ARTIFACT_DIR, 'red-evidence.json');
  if (!fs.existsSync(file)) return [];
  try {
    const parsed = JSON.parse(fs.readFileSync(file, 'utf8'));
    return Array.isArray(parsed) ? (parsed as LedgerEntry[]) : [];
  } catch {
    return [];
  }
}

// GREEN predicate per behaviour id: given the metrics a fresh re-run recorded,
// is the behaviour now satisfied? (These mirror the Suite A assertions.)
const GREEN_PREDICATES: Record<string, (m: LedgerEntry['metrics']) => boolean> = {
  // Canonical Ripley-lane gates (fast, schema-independent).
  'G5-dropdown-readability': (m) => m.unnamedControls === 0 && m.illegibleControls === 0 && m.firstIsKeyboardFocusable === true,
  'G6-ownhand-faceup': (m) => m.reloadHappened === false && Number(m.ownHandCount) >= 13 && m.clientDealBurstThings === 0,
  'G7-relay-controls-hidden': (m) => m.controlsStillInA11yTree === 0 && m.controlsStillFocusable === 0 && m.outboundMutationsAfterInvoke === 0,
  'G10-active-games-lifecycle': (m) => m.released === true,
  'G12-disconnect-cleanup': (m) => m.staleMoveLog === false && m.pickupCue === false && m.turnCue === false && m.claimCue === false && m.tableTilesRemaining === 0,
  'G16-auto-wall-nondraggable': (m) => m.outboundThingsMutations === 0 && m.wallClaimedByDelta === 0 && m.localFingerprintChanged === false,
  'G20-mobile-viewport': (m) => Number(m.viewports) > 0,
  'G21-dealmode-auto-boot': (m) => m.reloadHappened === false && Number(m.ownTilesAfterAutoBoot) >= 13 && m.pickupHudVisible === false,
  // Retained original bullets not folded into a canonical G-id (disjoint, flagged
  // to Ripley for canonical naming).
  'B1-repeat-deal': (m) => m.outboundThingsMutationsDuringAbuse === 0 && m.stillConnected === true && m.moveLogGrewNewHand === false,
  'B5-no-riichi-labels': (m) => m.distinctForbiddenLabels === 0,
};

test.describe('UAT Suite B — persisted-failure replay (regression gate)', () => {
  test.fixme('every persisted RED discriminator has been driven to GREEN', async () => {
    const ledger = loadLedger();
    expect(ledger.length, 'no RED evidence captured yet — run Suite A first').toBeGreaterThan(0);

    const stillRed: string[] = [];
    for (const entry of ledger) {
      const pred = GREEN_PREDICATES[entry.id];
      if (!pred) continue; // unknown id — skip (forward-compatible)
      if (!pred(entry.metrics)) stillRed.push(`${entry.id} (${entry.observed})`);
    }
    expect(stillRed, `behaviours still RED:\n  ${stillRed.join('\n  ')}`).toEqual([]);
  });

  // Per-behaviour isolated replay stubs — activate individually as fixes land.
  for (const id of Object.keys(GREEN_PREDICATES)) {
    test.fixme(`replay ${id} in isolation reaches GREEN`, async () => {
      const entry = loadLedger().find((e) => e.id === id);
      expect(entry, `no ledger entry for ${id} — run the matching Suite A spec first`).toBeTruthy();
      const pred = GREEN_PREDICATES[id];
      expect(pred(entry!.metrics), `${id} still RED: ${entry!.observed}`).toBe(true);
    });
  }
});
