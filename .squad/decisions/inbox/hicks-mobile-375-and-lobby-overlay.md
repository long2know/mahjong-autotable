# Mobile responsive (iPhone-SE 375 px) + lobby overlay sizing parity

**By:** Hicks (Frontend Dev)
**Date:** 2026-05-25
**Branch:** `feat/mobile-responsive-and-lobby-overlay`
**Trunk parent:** `b9b6482` (`feat(ui): lobby variant switcher dropdown — #91`)

## What I found

Stephen's 2026-05-19 directive flagged two distinct issues that hadn't
been verified end-to-end since the variant-switcher / Ferro overlay
ships:

1. **Mobile-375 audit gap.** The Changsha variant renders correctly at
   1280×800 (per `playtest-v3-fresh.spec.mjs`) but no spec exercised
   iPhone-SE width (375 px).  An exploratory 375 px run showed a 12 px
   horizontal scroll (`docW=387 vs innerW=375`) caused by `#lobby-panel`
   retaining its desktop `top:12px; left:12px` offset while the existing
   480-pixel media query had grown its width to `100vw`.  Quick-Match
   and the Ferro variant-picker were tappable but the lobby-close button
   could land under an iOS status bar on notched displays.

2. **Lobby overlay sizing regression.**  Stephen's image diff against
   upstream-autotable (`pwmarcz/autotable@8b81d92`) showed that our
   left-edge Deal/Setup overlay reads visually heavier than upstream's
   220 px sidebar.  The geometry comparison:
   - Upstream `#sidebar`: `width: 220px; padding: 1em` — short, compact.
   - Ours (pre-fix): same `220×Npx` box but **+131 px taller** because
     of the always-visible 4-button claim row (碰/吃/杠/胡) + Pass +
     countdown, plus Phase I/J Wave additions (Game ID input, move-seat
     row, settings shortcut).  The claim row alone is 188×115 px.
   - The newer `#lobby-panel` (320 px wide overlay) sat on top of the
     220 px sidebar and visually pushed the left-edge "Deal/Setup
     overlay" footprint out by another ~100 px when open.

## What I shipped

**New file:** `src/frontend/autotable-src/src/ui/hicks-mobile-sidebar.css`
(~200 LOC).  Layered after `style.css` so the local overrides win the
cascade; imported as a side-effect from `src/index.ts`.

### Fix 1 — Sidebar parity with upstream

Use the modern `:has()` selector to **hide the legacy claim button row
when no claim window is active**:

```css
#sidebar > .mt-3:has(#claim-pung[disabled])
                :has(#claim-chow[disabled])
                :has(#claim-kong[disabled])
                :has(#claim-hu[disabled])
                :has(#claim-pass[disabled]) {
    display: none;
}
```

The 5 buttons inside the row are toggled `disabled` by
`game-ui.ts:792-800` based on the live `claim` collection.  When
all five are disabled there is no active claim window — which is
the steady-state for every visible second of a game except the
5–7 s decision windows.  Ferro's `ClaimWindowOverlay` is the
primary claim surface; the legacy row remains as the keyboard-
accessible fallback whenever any single button enables.

**Measured impact:**
- Desktop 1280 sidebar height: **516 px → 385 px** (-25 %).
- The sidebar silhouette is now visually closer to the upstream
  220 px-tall pre-deal sidebar Stephen pointed at.

Also tightened `#lobby-panel` to 280 px wide (was 320 px) with
compacter padding at desktop so the two overlays read as the
same visual family — 220 px upstream sidebar + 60 px to host the
wider variant picker / bot selector.

### Fix 2 — Mobile reflow at ≤ 480 px

- `#lobby-panel` pinned to `top:0; left:0` (was inheriting
  `top:12px; left:12px` from the desktop rule, producing the 12 px
  horizontal scroll).  `padding-top/bottom: env(safe-area-inset-*)`
  so notched iPhones don't tuck the close button under the status
  bar.
- `#lobby-panel .lobby-header` is now `position: sticky; top: 0` so
  the close button stays visible as the user scrolls through the
  lobby body.
- `#lobby-quick-match` width 100% + `min-height: 44 px`.
- `#sidebar` collapses to a 160 px compact pill with
  `max-height: calc(100vh - 70px); overflow-y: auto;` so the
  Changsha discard pile + own-hand row stay visible on the right.
- `#pickup-hud` stacks its label + Take button vertically so the
  337-px-wide Take button doesn't compress the prompt text.
- `#variant-badge` is clamped right so it never overlaps the
  move-log toggle.
- `html, body { overflow-x: hidden; max-width: 100vw }` as
  defence-in-depth — keeps any future stray overflow off the
  document scrollbar (the canvas/sidebar/lobby are all
  `position: absolute` so this never affects gameplay scroll).

## Validation

### `playtest-mobile-375.spec.mjs` (new) — both scenarios PASS

- **Auto / spectator** (`?dealMode=auto&botCount=4`):
  `pageErrorsCount=0`, no horizontal overflow at lobby (375/375) or
  mid-game (375/375), QM h=44, picker h=44, canvas count=2.
- **Manual / human-led** (`?dealMode=manual&botCount=3&seat=0`):
  `pageErrorsCount=0`, no horizontal overflow, QM h=44, picker h=44,
  canvas count=2.

Both scenarios produce screenshots in `playtest-artifacts/mobile-375/`
(lobby, post-quick-match, midgame, claim-window, final).

### `playtest-v3-fresh.spec.mjs` (canonical spectator regression)

Re-ran at 1280×800 after the CSS landed.  Output identical to baseline:
`pageErrorsCount=0`, `consoleErrorsCount=3`, `networkFailuresCount=2`
(same pre-existing 404 GETs on `/api/games/changsha-default` that
existed pre-PR).

### Lobby-overlay screenshots

`playtest-artifacts/lobby-overlay/` carries before+after pairs for
desktop (1280) and mobile (375):

| Viewport | Metric | Before | After |
|---|---|---|---|
| Desktop 1280 | Sidebar height | 516 px | **385 px** (-25 %) |
| Desktop 1280 | Claim row | 188×115 visible | hidden (no active claim) |
| Mobile 375 | docW vs innerW | 387 vs 375 (overflow!) | **375 vs 375 (flush)** |
| Mobile 375 | Lobby panel offset | (12, 12) | **(0, 0)** |
| Mobile 375 | Sidebar | 180×667 | 160×542 |

### Backend / test counts

- Backend tests: **5125 / 1 pre-existing** (unchanged — no backend touched).

## Lane discipline (touched files)

- `src/frontend/autotable-src/src/index.ts` (8 LOC import block)
- `src/frontend/autotable-src/src/ui/hicks-mobile-sidebar.css` (new, ~200 LOC)
- `playtest-artifacts/playtest-mobile-375.spec.mjs` (new)
- `playtest-artifacts/mobile-375/*` (10 new screenshots + 2 findings.json)
- `playtest-artifacts/lobby-overlay/*` (8 new before/after screenshots)
- `.squad/agents/hicks/history.md` (append)
- `.squad/decisions/inbox/hicks-mobile-375-and-lobby-overlay.md` (this memo)

**Not touched** (per the lane map):

- `src/backend/**` (Bishop / Frost / Vasquez)
- `src/frontend/autotable-src/src/ui/claim-window-*` (Ferro)
- `src/frontend/autotable-src/src/ui/win-screen-*` (Ferro)
- `src/frontend/autotable-src/src/ui/ferro-bootstrap.ts` (Ferro)
- `.github/workflows/**` (Apone)

## Open follow-ups

1. **Mobile lobby Quick-Match position.** At 375 px the Quick-Match
   button sits at `y ≈ 2300` inside the lobby's internal scroll
   because the lobby body has ballooned with Phase J/K Wave content
   (stats panel, public-games tab, identity onboarding).  Reachable
   but requires a scroll.  A future iter could collapse the Stats /
   Public-Games tabs by default at ≤ 480 px and lift Quick-Match
   above the fold.  Not blocking — touch target is correct.

2. **Spectator claim row.** The `:has()` rule also hides the legacy
   claim row for spectators (they have no `seat` so `claim.available`
   never populates → all buttons stay disabled → row hides).  That's
   a feature not a bug today — spectators have nothing to claim — but
   if Frost surfaces a spectator-claim affordance later this rule
   may need a `body:not(.spectating)` guard.

3. **`#sidebar` extras stack.** The Phase J Wave 1 move-seat row and
   the Phase I Wave 3 Game-ID input still inflate the sidebar height
   when connected.  A subsequent pass could move those into the
   settings drawer to bring the sidebar even closer to upstream's
   compact 220×~300 box.
