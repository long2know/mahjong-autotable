# Ferro — Final combined-head review of PR #149

- **Verdict:** APPROVE
- **Pinned head:** 117176725cc8cc529756df9cca24fbda22bba7d6
- **Base (main):** 51b718c7ba07ec360300f0f359acce4e6e1d9f1b
- **Prior approval:** fcd096a55b3301729c989e78f01610755aaa3dc4 (semantic change) — re-confirmed intact
- **Date:** 2026-07-27T08:03-07:00
- **Mode:** strict read-only (git/gh inspection only; no worktree exec needed)

## Findings (all high-confidence)

1. **fcd096a..1171767 = merge integration + regenerated dist only.** Head merges
   0527bf2 (branch) + 51b718c (main). Deterministic recompute
   `git merge-tree 0527bf2 51b718c` conflicts ONLY in generated dist
   (`autotable/*.js`, `index.html`, `manifest-precache.json`) and `dist-size.json`.
   ALL source merged clean. Head's `world.ts`, `hand-accounting.ts`, and every
   non-dist source file are **byte-identical** to the auto-merge tree
   (ff3c1a9) — no hand-merge, no unreviewed #149 semantic edit.

2. **world.ts carries both changes, no collision.** #150's distinct-phase deal
   loop (`lastTakenPhase` + `World.normalizePickupPhase`, drops the fixed 120ms
   sleep) is the entire fcd096a..head world.ts delta. #149's meld-aware
   `hasExtraHandTile()` delegation to `hasExtraDiscardTile()` is the entire
   base..head delta and is untouched by the merge. Single definition of each
   symbol (no duplicate loop / stale helper). Relay keeps concealed-only
   (`meldAware=false`); spectator (`seat===null -> false`) and server-side bots
   unaffected.

3. **Dist is a clean deterministic build.** "Bundle in sync (committed dist ==
   fresh build)" gate = success at 1171767. index.html entry
   `autotable-src.2e943e9b.js` and every manifest-precache chunk resolve to
   objects present in the tree. Not a hand merge.

4. **Meld accounting correct.** Contract test covers Pung/Chow/exposed/concealed/
   added Kong (each counts 3; Kong 4th offset by replacement draw), multi-meld,
   rest states, `hand.extra@` preview exclusion, orphan skip, foreign-seat
   isolation, malformed-name fail-safe, and relay gating. Authoritative validator
   `TryHandleDiscardActionAsync` unchanged — intercept only gates the UI click.
   #150's `claimByClick` change (real `.ferro-claim-*` overlay clicks, side-panel
   fallback) is E2E-harness only and does not weaken #149's real-pointer regression.

5. **No backdoors.** No `client.update`, direct `emitDiscard`, synthetic
   Pointer/Mouse/Touch events, `{force:true}`, or state injection in #149 files.
   post-meld-discard.spec.ts drives real `.click()` claims + real `page.mouse`
   discard and asserts the authoritative discard pile grows.

6. **Terminal green / clean merge.** Head 1171767 check-runs: 23 success,
   4 skipped (SIGNED/main-only), 1 neutral (Trivy), 0 failing/pending.
   mergeStateStatus=CLEAN, mergeable=MERGEABLE. (Legacy combined-status "pending"
   is a GitHub artifact of 0 legacy statuses — all gating is via check-runs.)

## Blockers
None.
