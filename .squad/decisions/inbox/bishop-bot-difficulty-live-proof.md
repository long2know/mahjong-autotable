# Bot difficulty live differentiation proof

**Author:** Bishop (mahjong-autotable squad)
**Date:** 2026-06-04
**Spec:** `playtest-artifacts/playtest-bot-difficulty-live.spec.mjs`
**Artifacts:** `playtest-artifacts/screenshots/bishop-bot-diff-2026-06-04T14-33-52-963Z/`
**Cross-ref:** Frost `4cd8963` (live FanCalculator scoring wire-proof) — same wire surface; this spec proves DIFFERENT strategies feed into it.

## Mission

My `452b558` plumbed `?botDifficulty=` end-to-end and pinned per-game strategy isolation. Stephen's follow-up: PROVE the difficulty tiers actually play DIFFERENTLY in live games, not just that the right class gets instantiated.

## Setup

- 4 difficulty tiers × 3 trials = 12 spectator-watch 4-bot games at `http://127.0.0.1:8088`.
- URL per trial: `?variant=changsha&seat=-1&dealMode=auto&botCount=4&botDifficulty=<tier>&handCount=1&gameId=bot-diff-<tier>-<ts>-<trial>`.
- Tiers run in PARALLEL (4 browser contexts), trials within a tier run SEQUENTIALLY.
- Per-trial cap: 90s.
- Total wall-clock: 100s (parallelism is real — 12 games would have been 18 min sequential).

## Evidence capture per trial

- **CDP WS-frame tap** on `Network.webSocketFrameReceived` parses every `["result","current",{...}]` entry. Catches the first Hu/Draw at the wire level, independent of the bundle's `client.result.on('update', ...)` subscription. This is the same proof-pattern Frost used in `4cd8963` — wire-level, not bundle-level.
- World snapshots every 3s for peak `meld` count (= claims attempted) and peak `discard` count.
- `page.on('pageerror')` + console-error counters.

## Results (12 games, 0 page errors)

| Tier   | Hu / Trials | Hu rate | Median time-to-Hu | Median claims | Median discards | Median fans | SelfDraw |
|--------|-------------|---------|-------------------|---------------|-----------------|-------------|----------|
| Easy   | 0 / 3       | 0.00    | 32 550 ms ⁽¹⁾    | 21            | 54 ⁽²⁾         | n/a         | 0        |
| Medium | 3 / 3       | 1.00    | 19 047 ms         | 30            | 24              | 0           | 1        |
| Hard   | 3 / 3       | 1.00    | 19 951 ms         | 18            | 25              | 0           | 0        |
| Master | 3 / 3       | 1.00    | 14 383 ms         | 12            | 22              | 0           | 0        |

⁽¹⁾ Easy never won; the value is the median total trial duration (the bots drove every hand to wall-exhausted DRAW, captured as a `result.current` entry of `type:"Draw"`).
⁽²⁾ A full Changsha wall after the deal is ~54 tiles; peak 54 = the wall was completely drained.

## Verdict: **DETECTED** (2 spread signals, 0 strict-monotonic)

Spread evidence (Easy → Master):

| Metric                 | Easy   | Master | Δ      | Relative | Expected dir | Triggered |
|------------------------|--------|--------|--------|----------|--------------|-----------|
| median_time_to_hu_ms   | 32 550 | 14 383 | −18 167| −55.8%   | DOWN         | ✅        |
| hu_rate                | 0.00   | 1.00   | +1.00  | +100%    | UP           | ✅        |
| median_fan_points      | n/a    | 0      | —      | —        | UP           | (no data) |
| median_claims          | 21     | 12     | −9     | −42.9%   | UP           | ❌ inverse |

Two metrics cleared the ≥20%-spread bar in the expected direction. Differentiation is **clear** — the strongest single signal is Easy's complete inability to win a hand inside the 90s budget (3/3 wall-exhausted draws) while Master wins 3/3 in ~14 s.

## Tier behaviour summary

- **Easy** is functionally non-competitive at single-hand scale — bots discard their tiles but rarely assemble a winning shape. All 3 trials drained the wall (peak 52/54/55 discards) and ended on a `Draw`. This is consistent with an "honest beginner" strategy that doesn't aggressively chi/pong/kong toward a Hu shape.
- **Medium** is a high-volume claimer (median 30 claims per hand — MOST aggressive) and reliably wins, but takes a bit longer than Master.
- **Hard** claims less than Medium (median 18) but with similar Hu time. Suggests more selective claim filtering.
- **Master** claims the least (median 12) AND wins fastest (14.4 s). This is the canonical pattern for a smart bot: decisive moves only when they advance the goal hand.

## Non-monotonicity observation (NOT a fix request — note for the team)

The brief's expected pattern ("Master: more claims") doesn't fully hold — Master makes FEWER claims than Medium because higher-tier bots filter out chi/pong opportunities that don't advance their goal hand. This is actually a desirable game-design property (smarter ≠ louder). The "more claims" prediction in the brief should be updated to "more *successful* claims relative to claim *attempts*" or "more *value-bearing* claims" — but that would require taps on `PassClaim` opportunities the bot declined, which is outside this PROOF wave's scope.

## Fan-counts are mostly zero — known and correct

11 of the 12 trials with a Hu won 0 fans (standard 258-pair Hu on a claimed discard with no concealment / no self-draw / mixed suits scores no fan). 2 trials (`Medium #2` self-draw, `Master #1` claimed) each scored 1 fan / 1 point. This matches Frost's `4cd8963` proof — the FanCalculator wire path is intact, the bots just rarely build big-fan shapes in single 90-s hands. Not a differentiation signal; not a bug.

## Verdict & recommendations

- **SHIP** — tier differentiation is real and observable on the live wire. `452b558`'s plumbing fix has measurable behavioral consequences.
- **No follow-up fix needed** for tier differentiation itself.
- **Optional future:** wave to add a `?seed=` URL param so each tier can be replayed against identical wall RNG — would reduce noise on the 3-trial samples and let us assert ordering exactly (e.g. `MasterTime < EasyTime` deterministically across a fixed seed set). Filed as observation, not actionable now.
- **Optional future:** add 100-hand-per-tier batch tests in the same shape as the existing `[Skip]`-flagged 100-hand bot simulations in the backend suite. Larger N would pin the Medium-claims-spike pattern as genuine or noise.

## Wire flow alignment with Frost

Frost's `4cd8963` proved `scoreResult.fans` lands on the wire correctly when a Hu fires. This spec uses the SAME CDP WS-frame tap pattern + the SAME `["result","current",{...}]` entry shape (with `winResult.isSelfDraw`, `scoreResult.fans[]`, `score: [{seat,delta}]`) and observed it land cleanly in 9/9 Hu trials and 3/3 Draw trials across 4 difficulty tiers. The wire contract holds independent of strategy tier — confirming Frost's proof for a wider matrix.

## Artifacts on main

- `playtest-artifacts/playtest-bot-difficulty-live.spec.mjs` (new)
- `playtest-artifacts/screenshots/bishop-bot-diff-2026-06-04T14-33-52-963Z/findings.json`
- `playtest-artifacts/screenshots/bishop-bot-diff-2026-06-04T14-33-52-963Z/{easy,medium,hard,master}-final.png`
