# Decision: Ferro Iter 1 — Claim Window Countdown + Win Screen Polish

**Author:** Ferro (Frontend/UI Engineer)
**Date:** 2026-05-23
**Branch:** `feat/ferro-claim-window-and-win-screen`
**Status:** SHIPPED (PR opens with this memo)

## Summary

Two additive UX modules for Changsha Mahjong:

1. **Claim-window countdown overlay** — fixed bottom bar that appears when
   `client.claim` Collection populates, showing available claim actions
   (Pung / Chow / Kong / Hu) as large 44px-tall chips with keyboard
   shortcuts (P / C / K / H / Esc), an aria-live countdown timer, and a
   progress bar that fills as the window expires. Auto-passes at 0.
2. **Win-screen polish** — wraps the existing `#game-complete-modal`
   without modifying it. Replaces the static "Total Δ" numbers with
   1.2s rolling rAF counters (ease-out cubic, 80ms stagger between rows)
   and inserts a "番种 Fans Scored" card grid between the totals table
   and the hand-by-hand recap, aggregating fans from `handHistory`.

## Lane Discipline

Per `decisions/inbox/squad-frost-ferro-hire.md`, Ferro's lane is
**additive UI files only**.  Forbidden trunk: `world.ts`, `setup.ts`,
`setup-deal.ts`, `mouse-tracker.ts`, `game-ui.ts`, `lobby.ts`, `index.html`.

This iter touches the trunk in **exactly one place**: a 4-line dynamic
import inside the existing game-page gate in `src/index.ts` (NOT on the
forbidden list — only `index.html` is).  The bootstrap module then
listens for Hicks's public `mahjong:three-renderer-ready` event and
attaches both overlays idempotently.  Lobby cold path is unaffected
because the import is inside `if (!pwaActionHandled && window.location.search !== '')`.

## Files Shipped

```
src/frontend/autotable-src/src/ui/claim-window-overlay.ts      (new)
src/frontend/autotable-src/src/ui/claim-window-overlay.css     (new)
src/frontend/autotable-src/src/ui/win-screen-polish.ts         (new)
src/frontend/autotable-src/src/ui/win-screen-polish.css        (new)
src/frontend/autotable-src/src/ui/ferro-bootstrap.ts           (new)
src/frontend/autotable-src/src/index.ts                        (+4 lines)
playtest-artifacts/ferro-iter1/spec.mjs                        (visual proof)
playtest-artifacts/ferro-iter1/*.png                           (7 captures)
```

Bundle impact: `ferro-bootstrap` chunk = 14.34 kB raw / 4.89 kB gzipped.
No new runtime deps.

## Wire Shape Contracts (referenced, not modified)

- **`client.claim`** is `Collection<string, ClaimWindowEntry>` where
  entries are `{available: ['Pung'|'Chow'|'Kong'|'Hu', ...], deadline: epochMs, source: seat, tile: tileId}`.
  Defined in `types.ts:187` and `AutotableProtocol.cs:99-115`. The
  overlay subscribes to `client.claim.update` and uses an `isClaimEntry()`
  type guard to filter out junk shapes (e.g. echo of pass-acks).
- **`client.gameComplete`** carries `{isComplete, totalScores, handHistory, maxHands}`.
  The win-screen polish observes the modal via MutationObserver and
  re-applies its enhancements on each repaint without touching the
  underlying DOM that `game-ui.ts:renderGameCompleteModal` produces.
- **Public attach event:** `three-renderer.ts:82` dispatches
  `window.dispatchEvent(new CustomEvent('mahjong:three-renderer-ready'))`
  after `Object.assign(window, { game })`.  This is the canonical hook
  for any future additive UI module.

## Accessibility

- All claim chips are real `<button>` elements with `aria-label` and 44px
  minimum touch targets (WCAG 2.5.5).
- Countdown timer announces remaining time via `aria-live="polite"`.
- Fan list section uses `role="region"` + `aria-label="Fans scored this match"`.
- `prefers-reduced-motion: reduce` short-circuits the rolling counter
  animation to a snap-to-final and disables the progress-bar transitions.

## Pre-existing Trunk Bugs Discovered (for Hicks's queue — NOT fixed here)

While building the synthetic playtest fixture I hit two pre-existing
crashes in `game-ui.ts` that have NOTHING to do with Ferro's modules.
Logging them here so Hicks can pick them up:

1. **`refreshClaimButtons` TypeError** — `Cannot read properties of undefined (reading 'includes')`.
   Reproducer: when `client.claim.set({action:'pass', type:null})` echoes
   locally (disconnected-branch or synthetic spec), `game-ui` stores the
   bad shape and the next refresh crashes on `.available.includes()`.
   Mitigation in Ferro's overlay: `isClaimEntry()` guard before any read.
   Hicks should add the same guard in `refreshClaimButtons`.
2. **`result.score` not iterable** — `game-ui.ts:998` — `[...result.score].sort(...)`
   throws when `result.score` is undefined/null for a hand row.  Baseline
   v3 findings had 124 of these.  Easy fix: `[...(result.score ?? [])]`.

## Playtest Evidence

`playtest-artifacts/ferro-iter1/` contains 7 screenshots:

| File | What it shows |
|---|---|
| `01-claim-window-overlay.png` | Desktop overlay — full 5-button row + keyboard hints + progress bar at start |
| `02-claim-window-mid.png` | Desktop overlay mid-countdown — progress bar partially filled, "1.9s" timer |
| `03-win-screen-mid-roll.png` | Desktop modal — counters mid-animation showing intermediate values |
| `04-win-screen-final.png` | Desktop modal — counters settled at +12 / -4 / -4 / -4 + 平胡 Píng Hú Plain Win ×3 fan card |
| `05-win-screen-mobile-375.png` | Mobile 375px — modal reflows correctly: scores table + fan list + recap + action buttons all visible |
| `06-claim-overlay-mobile-375.png` | Mobile 375px — claim bar reflows to 2-row layout with full-width Pass |

**Spectator playtest regression** (`playtest-v3-fresh.spec.mjs`):
- `pageErrors`: 0
- `consoleErrors`: 3 (all pre-existing — THREE.NaN + 404s, unrelated to Ferro)
- `move-log entries`: 30 (gate ≥30) ✓

## Pattern for Future Additive UI

This iter establishes the additive-module pattern for Ferro's lane:

```ts
// src/ui/<feature>.ts — new file
export class FooWidget {
  attach(game: Game) { ... }
  detach() { ... }
}

// src/ui/<feature>-bootstrap.ts or shared ferro-bootstrap.ts
window.addEventListener('mahjong:three-renderer-ready', () => {
  new FooWidget().attach((window as any).game);
});

// src/index.ts — 1-line dynamic import inside the game-page gate
void import('./ui/foo-bootstrap');
```

This pattern lets Ferro ship UI without ever editing the forbidden trunk,
and lets Hicks evolve the trunk without breaking Ferro's modules (as
long as the public event + window.game contract holds).

## Next Up

- Wait for trunk-bug fixes from Hicks before extending the claim overlay
  to drive `sendClaim` directly (currently the overlay routes through
  `client.claim.set` to stay compatible with the existing wire).
- When Frost ships `Fan.cs`, drop the CHANGSHA_FANS fallback map in
  `win-screen-polish.ts` and rely on the per-hand `fans` array.
- Lobby polish (item 1 from charter) — overlay sizing parity with the
  original autotable.

---
**Co-authored-by:** Copilot
