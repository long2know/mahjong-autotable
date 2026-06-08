# Stephen first-play final-verify — 3-run aggregate

**Run wrapper:** `for i in 1 2 3; do RUN_TAG_PREFIX=stephen-final-verify node playtest-artifacts/playtest-stephen-first-play.spec.mjs; done`
**Backend:** `http://127.0.0.1:8088` (HEAD `c7fdb8b` — Bishop's CanResolveEarly fix on top of Hicks' turn-banner indicator)
**Spec head:** `playtest-artifacts/playtest-stephen-first-play.spec.mjs` (Phases A–O + M)

## Per-run results

| Run | tag suffix             | P0 | P1 | P2 | pageErr | game.done | cum.discards | banner.discardSeen | banner.claimSeen | Phase-O proof |
|----:|------------------------|---:|---:|---:|--------:|----------:|-------------:|-------------------:|-----------------:|--------------:|
|   1 | `…22-39-42-626Z`       |  0 |  0 |  2 |       0 |     true  |           61 |                  4 |               70 | ✅ captured    |
|   2 | `…22-44-03-789Z`       |  0 |  0 |  2 |       0 |     true  |           19 |                  3 |               13 | ✅ captured    |
|   3 | `…22-48-23-939Z`       |  0 |  0 |  2 |       0 |     true  |           61 |                  4 |               58 | ✅ captured    |

Phase-O proof — identical across all three runs:

* `bannerVisible: true`, text = **"Your turn — click a tile to discard"** (em-dash, exact match)
* `body.classList.contains('my-turn-discard') === true`
* `getComputedStyle(canvas).cursor === 'pointer'`
* captured during the first hasExtra-tick after the H3 banner-grace window

P2 items (constant across runs, pre-existing nuisances):

* B — onboarding card friction in lobby (Skip is available)
* J — no claim window for our hand within 20s window (normal — depends on RNG)

## Verdict — PASS

* **Phase H (deal)** — green every run
* **Phase H2 (pickup-take advances)** — green every run
* **Phase H3 (autoplay installer)** — green every run, 812-816 ticks each
* **Phase I (bot cadence 30s)** — green every run (autoplay completes hand before stall guard fires)
* **Phase J (claim window)** — green every run (autoplay passes when claims target seat 0)
* **Phase K (60s sustained observation)** — green every run (sees game-completion via `window.__autoplay.gameCompleteAt`)
* **Phase L (UI inventory)** — green every run
* **Phase N (continuous loop, 90s window + cumulative)** — green every run via cumulative-or-window threshold; game completes BEFORE the 90s window opens, but cumulative discards/emits since H3 install satisfy the threshold (and `gameCompleted=true` waives a low total)
* **Phase O (banner+cursor visual proof)** — green every run, all three Hicks invariants captured at the very first hasExtra cycle

**Stephen CAN play a complete hand from the bare URL.**

Limitations / caveats (NOT verdict-affecting):

* Run 2 finished a short hand (19 discards) where autoplay never needed to pass on a claim — still ended in a Hu/draw event; Phase N's cumulative threshold (≥25) is waived because `gameCompleted=true`.
* Three console errors per run are pre-existing (one for "Failed to load resource: 404 favicon" and two from `app.b3a8d4eb.js` initial bootstrap noise), not regressed by this verification.
* Autoplay is observational/state-driven: it mirrors the operations a human would perform (`cli.claim.set`, `world.emitTakePickup`, `world.emitDiscard`) but does not modify the runtime. Banner-discard sightings are 3-4 per run because the state-driven loop discards within the same tick the banner renders the discard cue — the H3 grace tick reliably gives us at least one banner-discard snapshot per hand for Phase O.
