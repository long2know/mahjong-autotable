// UAT (Hudson) — D4 claim OVERLAY TEARDOWN (SKELETON).
//
// GATE (Ripley ruling 2026-08-07): the D4 gate is deterministic OVERLAY TEARDOWN —
// after a claim window CLOSES, the claim overlay must be fully torn down
// (removed/hidden), never left lingering/orphaned. This pairs with backend
// EncodeClaimWindowClosed + frontend onClaimUpdate. Collection-level claim/discard
// EXCLUSIVITY (coexistence) is already GREEN, so it is NOT the gate here — it is
// recorded as a diagnostic only, not asserted, to keep the teardown signal clean.
//
// Status: SKELETON (test.fixme). The body is fully implemented against the real UI,
// but deterministically OPENING a claim window (so we can then prove it tears down)
// at the relay baseline requires the final Bishop/Hicks claim schema (claim-window
// lifecycle + turn ownership). Activate by removing `.fixme` once the authoritative
// claim/turn wire lands (EncodeClaimWindowClosed + onClaimUpdate); the assertions
// below already encode the teardown acceptance.
//
// Discipline: real pointer discard (no direct emitDiscard); real claim-overlay
// clicks; observation only. Fresh isolated gameId.

import { test, expect } from '@playwright/test';
import {
  defangOverlays, dismissLobbyAndTour, ensureConnected, takeSeatByClick, clickDeal,
  waitForGameObject, waitForPlayableHand, discardByPointer, readClaimWindow, hasExtraHandTile,
} from './_playability';
import { resolveBase, uatGameUrl, freshGameId, recordRedEvidence } from './_uat-changsha';

async function claimActionable(page: import('@playwright/test').Page): Promise<boolean> {
  // A claim is "actionable" iff the overlay/side-panel exposes an ENABLED claim
  // button for the local seat.
  const overlay = page.locator('.ferro-claim-pass, .ferro-claim-badge-hu');
  if (await overlay.first().isVisible().catch(() => false)) {
    if (await overlay.first().isEnabled().catch(() => false)) return true;
  }
  for (const id of ['#claim-pung', '#claim-chow', '#claim-kong', '#claim-hu', '#claim-pass']) {
    const b = page.locator(id);
    if (await b.first().isEnabled().catch(() => false)) return true;
  }
  return false;
}

// Overlay PRESENCE (teardown probe) — the claim overlay is "present" iff its
// container OR any claim control is rendered and visible, REGARDLESS of enabled
// state. Teardown means the overlay is fully removed/hidden after the window
// closes; a present-but-disabled overlay is still a lingering overlay (RED).
async function claimOverlayPresent(page: import('@playwright/test').Page): Promise<boolean> {
  const sels = [
    '.ferro-claim-pass', '.ferro-claim-badge-hu', '.ferro-claim-overlay', '.ferro-claim-panel',
    '#claim-pung', '#claim-chow', '#claim-kong', '#claim-hu', '#claim-pass',
    '#claim-countdown', // a lingering countdown container is itself a lingering overlay
  ];
  for (const sel of sels) {
    if (await page.locator(sel).first().isVisible().catch(() => false)) return true;
  }
  return false;
}

test.describe('UAT D4 — claim overlay tears down deterministically after the window closes', () => {
  test.fixme('a closed claim window leaves NO lingering claim overlay (deterministic teardown); discard works', async ({ page, baseURL }) => {
    test.setTimeout(150_000);
    const base = resolveBase(baseURL);
    const gameId = freshGameId('b4-discard-claim');

    await defangOverlays(page);
    await page.goto(uatGameUrl(base, { gameId, dealMode: 'auto', seat: 0 }), { waitUntil: 'domcontentloaded' });
    expect(await waitForGameObject(page)).toBe(true);
    await dismissLobbyAndTour(page);
    await ensureConnected(page);
    await takeSeatByClick(page, 0);
    await clickDeal(page);

    // Wait until the local seat has a playable (14th) tile → discard affordance.
    const playable = await waitForPlayableHand(page, 45_000);
    expect(playable.playable, 'local seat never reached a playable discard state').toBe(true);

    // Exclusivity is recorded as a DIAGNOSTIC only (already GREEN — not the gate).
    // We also track whether a claim overlay ever APPEARED, so the teardown gate is
    // non-vacuous (something must open before we can prove it tears down).
    const coexistSamples: Array<{ discard: boolean; claim: boolean }> = [];
    let overlayEverAppeared = false;
    for (let i = 0; i < 8; i++) {
      const discard = await hasExtraHandTile(page);
      const claim = await claimActionable(page);
      if (await claimOverlayPresent(page)) overlayEverAppeared = true;
      coexistSamples.push({ discard, claim });
      await page.waitForTimeout(250);
    }
    const coexist = coexistSamples.filter((s) => s.discard && s.claim).length;

    // A valid own-hand discard must succeed via a real pointer gesture.
    const outcome = await discardByPointer(page);

    // TEARDOWN gate: after the claim window closes, the overlay must be fully torn
    // down — not merely disabled. Read presence (visible DOM), not just actionable.
    const claimBefore = await readClaimWindow(page);
    if (claimBefore.open) overlayEverAppeared = true;
    await page.waitForTimeout(6000); // allow the claim window to close/time out
    const overlayAfterClose = await claimOverlayPresent(page);
    const claimActionableAfter = await claimActionable(page);

    // COUNTDOWN-STOPPED component of the teardown oracle (Vasquez binding constraint
    // 2026-08-07): the acceptance is overlay hidden + claim buttons disabled + COUNTDOWN
    // STOPPED within one snapshot of resolve — not exclusivity-only. Sample the
    // #claim-countdown twice: a countdown that is gone/frozen is stopped; one that keeps
    // decrementing after resolve means the window is still live ⇒ RED. deadline=0
    // ("no client-side countdown") also reads as stopped, so this stays schema-agnostic.
    const readCountdown = () => page.evaluate(() => {
      const el = document.getElementById('claim-countdown');
      const val = document.getElementById('claim-countdown-value');
      const visible = !!el && !el.hidden && getComputedStyle(el).display !== 'none' && el.getBoundingClientRect().height > 0;
      const n = val ? parseFloat((val.textContent || '').replace(/[^\d.]/g, '')) : NaN;
      return { visible, n: Number.isFinite(n) ? n : null };
    });
    const cd1 = await readCountdown();
    await page.waitForTimeout(900);
    const cd2 = await readCountdown();
    const countdownStopped = (!cd1.visible && !cd2.visible) || cd1.n === null || cd2.n === null || cd2.n >= cd1.n;
    const countdownStillTicking = !countdownStopped;

    recordRedEvidence({
      id: 'D4-claim-overlay-teardown',
      expected: 'A real-pointer discard succeeds; after the claim window closes the claim overlay is fully torn down (overlay hidden + claim buttons disabled + countdown stopped) within one snapshot of resolve, leaving no lingering overlay or actionable control. (Exclusivity/coexistence is necessary-but-not-sufficient, diagnostic-only — already GREEN.)',
      observed: `discardOk=${outcome.ok}, overlayEverAppeared=${overlayEverAppeared}, overlayAfterClose=${overlayAfterClose}, claimActionableAfterClose=${claimActionableAfter}, countdownStillTicking=${countdownStillTicking} (cd ${JSON.stringify(cd1)}→${JSON.stringify(cd2)}), coexistSamples=${coexist} (diagnostic)`,
      metrics: {
        discardOk: outcome.ok,
        overlayEverAppeared,
        overlayLingersAfterClose: overlayAfterClose,
        staleClaimActionable: claimActionableAfter,
        countdownStillTicking,
        coexistCount: coexist,
      },
    });

    expect(outcome.ok, `valid own-hand discard must work (reason: ${outcome.reason})`).toBe(true);
    // Non-vacuous precondition: a claim overlay must have actually opened.
    expect(overlayEverAppeared, 'D4 precondition: a claim window/overlay must have opened so its teardown can be proven (requires the claim-window schema)').toBe(true);
    // PRIMARY GATE (Ripley 2026-08-07): deterministic overlay teardown. The oracle is
    // the THREE teardown components (Vasquez 2026-08-07), NOT discard/claim exclusivity
    // (which is necessary-but-not-sufficient and already GREEN — recorded diagnostically).
    expect(overlayAfterClose, 'D4 (teardown 1/3 — overlay hidden): after the claim window closes the claim overlay must be fully torn down (removed/hidden); a lingering overlay is the RED').toBe(false);
    expect(claimActionableAfter, 'D4 (teardown 2/3 — buttons disabled): a torn-down claim window must leave no actionable claim control').toBe(false);
    expect(countdownStillTicking, 'D4 (teardown 3/3 — countdown stopped): after resolve the claim countdown must be STOPPED (gone/frozen), not still ticking down — a live countdown means the window is still open').toBe(false);
  });
});
