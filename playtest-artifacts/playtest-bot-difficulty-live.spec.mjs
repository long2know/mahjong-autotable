// Bishop W26 — Bot difficulty live differentiation proof.
//
// Stephen's directive (post-452b558): "your `?botDifficulty=` plumbing
// makes the right strategy class get *instantiated*. Now PROVE the
// tiers actually *play* differently on the wire."
//
// Strategy:
//   - 4 difficulty tiers (Easy/Medium/Hard/Master).
//   - 3 trials per tier (12 games total).
//   - Each trial: open a spectator-watch 4-bot game, run to natural
//     first-hand completion (Hu or Draw) or hit the 90s cap.
//   - Per trial capture (via CDP WS frame tap + world snapshots):
//       outcome              ("Hu" | "Draw" | "Timeout")
//       timeToHuMs           (page-load → first Hu observed; null on Draw)
//       claimsAttempted      (peak meld count across hand)
//       discardsCount        (peak discard pile size across hand)
//       fans                 (winning hand fan count)
//       fanPoints / basePoints
//       isSelfDraw / concealedHand
//       scoreDeltas
//   - Tiers run in PARALLEL (4 browser contexts), trials within a tier
//     run SEQUENTIALLY to keep server load bounded.
//   - Compute medianTimeToHuMs (Hu trials only), huRate, medianFans,
//     medianClaims per tier.
//   - Verdict: tier_differentiation = "DETECTED" iff the per-tier
//     metric spread is non-trivial (see thresholds in writeFindings).
//
// Cross-reference: Frost's `4cd8963` (live FanCalculator scoring path)
// proved the wire DELIVERS scoreResult.fans correctly. This spec proves
// that, given the same wire format, different difficulty tiers actually
// produce DIFFERENT scoreResult / discard / meld profiles.
//
// Run:
//   E2E_BASE_URL=http://127.0.0.1:8088 \
//     node playtest-artifacts/playtest-bot-difficulty-live.spec.mjs

import { chromium } from 'playwright';
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const baseUrl = process.env.E2E_BASE_URL || 'http://127.0.0.1:8088';
const ts = new Date().toISOString().replace(/[:.]/g, '-');
// Pin the artifact root to <spec-dir>/screenshots so the spec is
// CWD-independent — running it from the repo root or from
// playtest-artifacts/ both land artifacts under the same canonical path.
const SPEC_DIR = dirname(fileURLToPath(import.meta.url));
const ARTIFACT_ROOT = resolve(SPEC_DIR, 'screenshots', `bishop-bot-diff-${ts}`);
mkdirSync(ARTIFACT_ROOT, { recursive: true });

const log = (...m) => console.log('[bishop-bot-diff]', ...m);
const dump = (name, data) =>
  writeFileSync(join(ARTIFACT_ROOT, name), JSON.stringify(data, null, 2));

const TIERS = ['Easy', 'Medium', 'Hard', 'Master'];
const TRIALS_PER_TIER = parseInt(process.env.BISHOP_TRIALS_PER_TIER || '3', 10);
const PER_GAME_BUDGET_MS = parseInt(process.env.BISHOP_PER_GAME_MS || '90000', 10);

// Discard-pile cross-game guard. Tile ids 0..107 repeat across games so we
// can't naively dedupe by id — keep tuples scoped to a single trial.

// ════════════════════════════════════════════════════════════════════
// Helpers
// ════════════════════════════════════════════════════════════════════
function installDefang(page) {
  return page.addInitScript(() => {
    const inject = () => {
      if (document.getElementById('bishop-bot-diff-defang')) return;
      const style = document.createElement('style');
      style.id = 'bishop-bot-diff-defang';
      style.textContent = `
        #tour-overlay, #magic-link-landing, #magic-link-overlay,
        #signin-modal-backdrop, .magic-link-landing, .magic-link-overlay,
        .signin-modal-backdrop, [data-testid="tour-overlay"], [data-testid="signin-modal-backdrop"]
          { display: none !important; pointer-events: none !important; visibility: hidden !important; }
      `;
      document.head.appendChild(style);
    };
    if (document.readyState === 'loading') {
      document.addEventListener('DOMContentLoaded', inject);
    } else { inject(); }
  });
}

async function snapshotWorld(page) {
  return await page.evaluate(() => {
    const w = window.game?.world;
    if (!w) return null;
    const things = Array.from(
      (w.things && typeof w.things.values === 'function') ? w.things.values() : [],
    );
    const byGroup = { hand: 0, wall: 0, discard: 0, meld: 0, other: 0 };
    for (const t of things) {
      const g = t.slot?.group;
      if (g === 'hand') byGroup.hand++;
      else if (g === 'wall') byGroup.wall++;
      else if (g === 'discard') byGroup.discard++;
      else if (g === 'meld') byGroup.meld++;
      else byGroup.other++;
    }
    return {
      byGroup,
      activeSeat: w.match?.activeSeat ?? w.match?.activeSeatIndex ?? null,
      phase: w.match?.phase ?? null,
      handCount: w.match?.handCount ?? null,
    };
  });
}

function median(nums) {
  const xs = nums.filter((n) => typeof n === 'number' && Number.isFinite(n)).slice().sort((a, b) => a - b);
  if (xs.length === 0) return null;
  const mid = Math.floor(xs.length / 2);
  return xs.length % 2 ? xs[mid] : Math.round((xs[mid - 1] + xs[mid]) / 2);
}

function mean(nums) {
  const xs = nums.filter((n) => typeof n === 'number' && Number.isFinite(n));
  if (xs.length === 0) return null;
  return Math.round(xs.reduce((s, n) => s + n, 0) / xs.length);
}

// ════════════════════════════════════════════════════════════════════
// Per-trial runner
// ════════════════════════════════════════════════════════════════════
async function runTrial(browser, tier, trialIdx, totalAcrossAllTiers) {
  const gameId = `bot-diff-${tier.toLowerCase()}-${Date.now()}-${trialIdx}`;
  const url = `${baseUrl}/autotable/?variant=changsha&seat=-1&dealMode=auto`
    + `&botCount=4&botDifficulty=${tier}&handCount=1`
    + `&gameId=${encodeURIComponent(gameId)}`;

  const ctx = await browser.newContext({ viewport: { width: 1280, height: 800 }, ignoreHTTPSErrors: true });
  const page = await ctx.newPage();
  await installDefang(page);

  const trial = {
    tier,
    trialIdx,
    gameId,
    url,
    startedAt: Date.now(),
    outcome: null,            // "Hu" | "Draw" | "Timeout"
    timeToHuMs: null,
    claimsAttempted: 0,       // peak melds in any snapshot
    discardsCount: 0,         // peak discards in any snapshot
    fans: null,               // count of FanEntry items in winning scoreResult
    fanPoints: null,
    basePoints: null,
    fanNames: [],
    isSelfDraw: null,
    isRobbedKong: null,
    winPattern: null,
    winner: null,
    scoreDeltas: null,
    handTileCount: null,
    pageErrors: 0,
    pageErrorMessages: [],
    consoleErrors: 0,
    wsFramesSeen: 0,
    resultFramesSeen: 0,
    huFramesSeen: 0,
    drawFramesSeen: 0,
    samples: [],
  };

  page.on('pageerror', (e) => {
    trial.pageErrors++;
    if (trial.pageErrorMessages.length < 10) trial.pageErrorMessages.push(e.message);
  });
  page.on('console', (msg) => {
    if (msg.type() === 'error') trial.consoleErrors++;
  });

  // CDP WS tap — capture every `["result","current",{ ... }]` entry so we
  // have authoritative wire evidence of Hu/Draw and the scoreResult shape.
  // We don't depend on the bundle's client.result subscription because the
  // result modal can race the bundle's mount and silently drop the first
  // entry on tier=Master where the Hu fires ~5s after page-load.
  let firstHuEntry = null;
  let firstDrawEntry = null;
  const cdp = await ctx.newCDPSession(page);
  await cdp.send('Network.enable');
  cdp.on('Network.webSocketFrameReceived', ({ response }) => {
    const payload = response?.payloadData;
    if (!payload || typeof payload !== 'string') return;
    trial.wsFramesSeen++;
    if (!payload.includes('"result"')) return;
    try {
      const parsed = JSON.parse(payload);
      const entries = parsed?.entries ?? [];
      for (const entry of entries) {
        if (!Array.isArray(entry) || entry.length !== 3) continue;
        const [kind, key, value] = entry;
        if (kind !== 'result' || key !== 'current' || !value) continue;
        trial.resultFramesSeen++;
        if (value.type === 'Hu' && !firstHuEntry) {
          trial.huFramesSeen++;
          firstHuEntry = { capturedAt: Date.now(), value };
        } else if (value.type === 'Draw' && !firstDrawEntry) {
          trial.drawFramesSeen++;
          firstDrawEntry = { capturedAt: Date.now(), value };
        }
      }
    } catch { /* non-JSON frame */ }
  });

  const loadStart = Date.now();
  try {
    await page.goto(url, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(2000);

    // Poll loop — snapshot the world every 3s to track discard/meld peaks
    // and watch for the first result entry to fire (captured on CDP).
    const deadline = loadStart + PER_GAME_BUDGET_MS;
    let postHuFrames = 0;
    while (Date.now() < deadline) {
      await page.waitForTimeout(3000);
      const snap = await snapshotWorld(page).catch(() => null);
      if (snap) {
        snap.elapsedMs = Date.now() - loadStart;
        trial.samples.push(snap);
        trial.claimsAttempted = Math.max(trial.claimsAttempted, snap.byGroup.meld);
        trial.discardsCount = Math.max(trial.discardsCount, snap.byGroup.discard);
      }
      if (firstHuEntry || firstDrawEntry) {
        // Once a terminal entry arrives we want one more world snapshot so
        // the screenshot captures the post-game tableau (melds revealed,
        // result modal up). Two extra 3s waits = ~6s of settle.
        postHuFrames++;
        if (postHuFrames >= 2) break;
      }
    }

    if (firstHuEntry) {
      trial.outcome = 'Hu';
      trial.timeToHuMs = firstHuEntry.capturedAt - loadStart;
      const v = firstHuEntry.value;
      const sr = v.scoreResult ?? {};
      const wr = v.winResult ?? {};
      trial.fans = Array.isArray(sr.fans) ? sr.fans.length : 0;
      trial.fanPoints = typeof sr.fanPoints === 'number' ? sr.fanPoints : null;
      trial.basePoints = typeof sr.basePoints === 'number' ? sr.basePoints : null;
      trial.fanNames = Array.isArray(sr.fans) ? sr.fans.map((f) => f.fan) : [];
      trial.isSelfDraw = wr.isSelfDraw ?? null;
      trial.isRobbedKong = wr.isRobbedKong ?? null;
      trial.winPattern = wr.winPattern ?? null;
      trial.winner = v.winner ?? null;
      trial.scoreDeltas = Array.isArray(v.score)
        ? v.score.map((s) => ({ seat: s.seat, delta: s.delta }))
        : null;
      trial.handTileCount = Array.isArray(v.hand) ? v.hand.length : null;
    } else if (firstDrawEntry) {
      trial.outcome = 'Draw';
      trial.winner = firstDrawEntry.value?.winner ?? null;
      trial.scoreDeltas = Array.isArray(firstDrawEntry.value?.score)
        ? firstDrawEntry.value.score.map((s) => ({ seat: s.seat, delta: s.delta }))
        : null;
    } else {
      trial.outcome = 'Timeout';
    }
  } catch (e) {
    trial.outcome = trial.outcome || 'Timeout';
    trial.pageErrorMessages.push(`trial-runner threw: ${e?.message || String(e)}`);
  } finally {
    trial.durationMs = Date.now() - loadStart;
    // Screenshot only the LAST trial of each tier — gives us a 4-shot
    // gallery without blowing up artifact count.
    if (trialIdx === TRIALS_PER_TIER - 1) {
      try {
        await page.screenshot({
          path: join(ARTIFACT_ROOT, `${tier.toLowerCase()}-final.png`),
          fullPage: false,
        });
      } catch { /* page may have closed */ }
    }
    await ctx.close().catch(() => {});
  }

  log(`  [${tier}] trial ${trialIdx + 1}/${TRIALS_PER_TIER}: outcome=${trial.outcome} `
    + `timeToHu=${trial.timeToHuMs ?? '–'}ms claims=${trial.claimsAttempted} discards=${trial.discardsCount} `
    + `fans=${trial.fans ?? '–'} fanPoints=${trial.fanPoints ?? '–'} pageErrors=${trial.pageErrors}`);

  return trial;
}

// ════════════════════════════════════════════════════════════════════
// Per-tier runner (sequential trials)
// ════════════════════════════════════════════════════════════════════
async function runTier(browser, tier) {
  log(`\n════ Tier ${tier} starting (${TRIALS_PER_TIER} trials × ${PER_GAME_BUDGET_MS / 1000}s cap) ════`);
  const trials = [];
  for (let i = 0; i < TRIALS_PER_TIER; i++) {
    const trial = await runTrial(browser, tier, i);
    trials.push(trial);
  }
  return trials;
}

// ════════════════════════════════════════════════════════════════════
// Tier aggregation + verdict
// ════════════════════════════════════════════════════════════════════
function aggregateTier(tier, trials) {
  const huTrials = trials.filter((t) => t.outcome === 'Hu');
  const huRate = trials.length === 0 ? null : huTrials.length / trials.length;
  return {
    tier,
    trials: trials.length,
    hu_count: huTrials.length,
    draw_count: trials.filter((t) => t.outcome === 'Draw').length,
    timeout_count: trials.filter((t) => t.outcome === 'Timeout').length,
    hu_rate: huRate == null ? null : Number(huRate.toFixed(3)),
    // Median time-to-Hu uses only Hu trials. If none, fall back to median
    // duration across all trials (which floors at the 90s cap → strongest
    // possible "slow" signal).
    median_time_to_hu_ms: huTrials.length > 0
      ? median(huTrials.map((t) => t.timeToHuMs))
      : median(trials.map((t) => t.durationMs)),
    mean_time_to_hu_ms: huTrials.length > 0
      ? mean(huTrials.map((t) => t.timeToHuMs))
      : null,
    median_fans: huTrials.length > 0 ? median(huTrials.map((t) => t.fans)) : null,
    median_fan_points: huTrials.length > 0 ? median(huTrials.map((t) => t.fanPoints)) : null,
    median_base_points: huTrials.length > 0 ? median(huTrials.map((t) => t.basePoints)) : null,
    median_claims: median(trials.map((t) => t.claimsAttempted)),
    median_discards: median(trials.map((t) => t.discardsCount)),
    self_draw_count: huTrials.filter((t) => t.isSelfDraw === true).length,
    page_errors: trials.reduce((s, t) => s + (t.pageErrors || 0), 0),
    console_errors: trials.reduce((s, t) => s + (t.consoleErrors || 0), 0),
  };
}

function decideDifferentiation(perTier) {
  // We expect MONOTONIC trends across Easy → Medium → Hard → Master on at
  // least one of: time-to-Hu (decreasing) OR hu-rate (increasing) OR
  // fan-points (increasing) OR claims (increasing). The bot strategies
  // are sampled — a single trial swing won't shift the median enough.
  //
  // Threshold: "meaningful" = the spread between Easy and Master on the
  // metric is ≥ 20 % of the larger value (≥ 1 for integer counts).
  const evidence = [];
  const labels = ['Easy', 'Medium', 'Hard', 'Master'];
  const lookup = Object.fromEntries(perTier.map((t) => [t.tier, t]));

  function spread(key, expectedDirection /* 'down' | 'up' */) {
    const easy = lookup.Easy?.[key];
    const master = lookup.Master?.[key];
    if (easy == null || master == null) return null;
    if (Math.max(Math.abs(easy), Math.abs(master)) === 0) return null;
    const delta = master - easy;
    const relative = delta / Math.max(Math.abs(easy), Math.abs(master));
    return { easy, master, delta, relative: Number(relative.toFixed(3)), expectedDirection };
  }

  function ordering(key) {
    const xs = labels.map((l) => lookup[l]?.[key]).filter((n) => typeof n === 'number');
    if (xs.length < 2) return null;
    const ups = []; const downs = [];
    for (let i = 1; i < xs.length; i++) {
      if (xs[i] > xs[i - 1]) ups.push(i);
      else if (xs[i] < xs[i - 1]) downs.push(i);
    }
    return { values: xs, monotonicUp: ups.length === xs.length - 1, monotonicDown: downs.length === xs.length - 1 };
  }

  evidence.push({ metric: 'median_time_to_hu_ms', spread: spread('median_time_to_hu_ms', 'down'), ordering: ordering('median_time_to_hu_ms') });
  evidence.push({ metric: 'hu_rate',               spread: spread('hu_rate', 'up'),               ordering: ordering('hu_rate') });
  evidence.push({ metric: 'median_fan_points',     spread: spread('median_fan_points', 'up'),     ordering: ordering('median_fan_points') });
  evidence.push({ metric: 'median_claims',         spread: spread('median_claims', 'up'),         ordering: ordering('median_claims') });

  let signalCount = 0;
  for (const e of evidence) {
    if (!e.spread) continue;
    if (e.spread.expectedDirection === 'down' && e.spread.delta < 0 && Math.abs(e.spread.relative) >= 0.2) signalCount++;
    if (e.spread.expectedDirection === 'up'   && e.spread.delta > 0 && Math.abs(e.spread.relative) >= 0.2) signalCount++;
  }
  // Also count any clean monotonic ordering (up OR down) as one signal —
  // even a small but consistent step across all four tiers is meaningful.
  for (const e of evidence) {
    if (!e.ordering) continue;
    if (e.ordering.monotonicUp || e.ordering.monotonicDown) signalCount++;
  }

  return {
    tier_differentiation: signalCount >= 1 ? 'DETECTED' : 'NOT_DETECTED',
    signal_count: signalCount,
    evidence,
  };
}

// ════════════════════════════════════════════════════════════════════
// Driver
// ════════════════════════════════════════════════════════════════════
const browser = await chromium.launch();
const startedAt = Date.now();

let tierResults;
try {
  tierResults = await Promise.all(TIERS.map((tier) => runTier(browser, tier)));
} finally {
  await browser.close().catch(() => {});
}

const perTier = tierResults.map((trials, i) => aggregateTier(TIERS[i], trials));
const verdict = decideDifferentiation(perTier);

const allTrials = tierResults.flat();
const pageErrorsTotal = allTrials.reduce((s, t) => s + (t.pageErrors || 0), 0);

const findings = {
  spec: 'playtest-bot-difficulty-live.spec.mjs',
  baseUrl,
  ts,
  startedAt: new Date(startedAt).toISOString(),
  completedAt: new Date().toISOString(),
  totalDurationMs: Date.now() - startedAt,
  trials_per_tier: TRIALS_PER_TIER,
  per_game_budget_ms: PER_GAME_BUDGET_MS,
  tiers: TIERS,
  // Shape the brief requested verbatim.
  results: Object.fromEntries(perTier.map((t) => [t.tier, {
    median_time_to_hu_ms: t.median_time_to_hu_ms,
    hu_rate: t.hu_rate,
    median_fans: t.median_fans,
    median_fan_points: t.median_fan_points,
    median_base_points: t.median_base_points,
    median_claims: t.median_claims,
    median_discards: t.median_discards,
    hu_count: t.hu_count,
    draw_count: t.draw_count,
    timeout_count: t.timeout_count,
    self_draw_count: t.self_draw_count,
    page_errors: t.page_errors,
    console_errors: t.console_errors,
  }])),
  tier_differentiation: verdict.tier_differentiation,
  verdict_signal_count: verdict.signal_count,
  verdict_evidence: verdict.evidence,
  verdict: verdict.tier_differentiation === 'DETECTED'
    ? `Tier-to-tier variation detected across ${verdict.signal_count} metric signal(s). Bot difficulty strategies produce measurably different play across ${TIERS.length} tiers × ${TRIALS_PER_TIER} trials.`
    : `NO meaningful tier-to-tier variation across ${TIERS.length} tiers × ${TRIALS_PER_TIER} trials. ?botDifficulty= plumbing is wired (per 452b558) but the strategies appear to produce equivalent on-wire play. Likely a real bug — see decision memo.`,
  page_errors_total: pageErrorsTotal,
  raw_trials: allTrials,
};

dump('findings.json', findings);

log('\n════════════════════════════════════════');
log('Bot difficulty live differentiation summary');
for (const t of perTier) {
  log(`  ${t.tier.padEnd(7)} hu=${t.hu_count}/${t.trials} rate=${t.hu_rate ?? '–'} `
    + `medTimeToHu=${t.median_time_to_hu_ms ?? '–'}ms medFans=${t.median_fans ?? '–'} `
    + `medFanPts=${t.median_fan_points ?? '–'} medClaims=${t.median_claims ?? '–'} `
    + `medDiscards=${t.median_discards ?? '–'} pageErr=${t.page_errors}`);
}
log(`Verdict: ${verdict.tier_differentiation} (signals=${verdict.signal_count})`);
log(`Page errors total: ${pageErrorsTotal}`);
log(`Findings: ${join(ARTIFACT_ROOT, 'findings.json')}`);
log('════════════════════════════════════════');

// Exit code: 0 if findings written and zero page errors, 2 otherwise so a
// future CI hook can pick up the signal. NOT_DETECTED is still a valid
// (and useful!) finding — the spec's job is to MEASURE, not enforce.
process.exit(pageErrorsTotal === 0 ? 0 : 2);
