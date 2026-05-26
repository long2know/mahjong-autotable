# Decision: Ferro Iter 2 — Lobby Variant Switcher

**Author:** Ferro (Frontend/UI Engineer)
**Date:** 2026-05-25
**Branch:** `feat/variant-switcher`
**Status:** SHIPPED (PR opens with this memo)

## Summary

Surface the existing backend `?variant=` query-param as a prominent
lobby dropdown so players can switch between Changsha and the four
upstream autotable variants without crafting URLs by hand.

Backend already understands the parameter at WS handshake
(`AutotableWsEndpoint.cs:228`):

- `changsha` (case-insensitive) → `ChangshaRuntime`.
- Any other non-empty string → Relay mode → forwards the upstream
  Setup for `four-player` (Riichi), `three-player` (Riichi-3),
  `bamboo` (American), or `minefield`.
- Five variants are natively live.  Hong Kong (`hong-kong`) is shipped
  as a disabled "Coming soon" `<option>` per the charter spec.

## Lane Discipline

Per `decisions/inbox/squad-frost-ferro-hire.md`, Ferro's lane is
**additive UI files only**.  Forbidden trunk: `world.ts`, `setup.ts`,
`setup-deal.ts`, `mouse-tracker.ts`, `game-ui.ts`, `lobby.ts`,
`index.html`.

This iter touches the trunk in **exactly two places**:

1. `src/ui/ferro-bootstrap.ts` — 1 line: `import './variant-picker';`
   (Ferro-owned bootstrap, already in the additive overlay zone).
2. `src/index.ts` — 1 line: `void import('./ui/variant-picker');`
   placed outside the existing game-page gate.  `index.ts` is NOT on
   Hicks's forbidden list (only `index.html` is), and this is exactly
   the precedent set by Ferro PR #87 (claim-window/win-screen).

Why two import sites?  `ferro-bootstrap` is only imported when
`window.location.search !== ''` — i.e. game pages.  The variant picker
must mount on lobby cold paths too (no query params), so it needs its
own dynamic import outside the gate.  The variant-picker module is
idempotent — both imports resolve to the same singleton, so double-load
is a no-op.

`lobby.ts` was NOT modified.  The picker coexists with Hicks's existing
`#lobby-variant-fieldset` radio group rather than replacing it: a CSS
`:has(.ferro-variant-picker) #lobby-variant-fieldset { display: none !important }` rule hides the radios when Ferro's picker is present.  If the picker
fails to mount for any reason, the radios remain visible as fallback,
so `readPickers()` at `lobby.ts:612` (`variantInputs.find(i => i.checked)?.value`) still functions for Apply & Start.

## Files Shipped

```
src/frontend/autotable-src/src/ui/variant-picker.ts            (new)
src/frontend/autotable-src/src/ui/variant-picker.css           (new)
src/frontend/autotable-src/src/ui/ferro-bootstrap.ts           (+1 line)
src/frontend/autotable-src/src/index.ts                        (+5 lines)
playtest-artifacts/ferro-iter2/variant-picker.spec.mjs         (visual proof)
playtest-artifacts/ferro-iter2/variant-picker-desktop.png      (1280x800)
playtest-artifacts/ferro-iter2/variant-picker-mobile.png       (375x667)
playtest-artifacts/ferro-iter2/findings.json                   (9 step results)
```

Bundle impact: `variant-picker.ee62af4e.js` = 3.26 kB raw / 1.38 kB
gzipped.  No new runtime deps.

## Design Decisions

### Resolution priority for the picker's initial value

`URL ?variant=` > `localStorage['mahjong.preferredVariant']` > default
`changsha`.

Rationale: a shared URL must always win so players landing on
`/?variant=bamboo` see the picker reflect that and the WS connects to
Relay-mode bamboo.  Otherwise we fall back to the most recently chosen
variant (sticky across reloads) and finally to the project default.

### Change handler: eager reload (`window.location.replace`)

Selecting a new variant writes LS + URL and immediately calls
`window.location.replace`.  Tradeoff: this drops any unsaved lobby state
(seat selection, rule overrides) but guarantees the backend WS handshake
re-runs with the new variant and the frontend bundle rehydrates against
the matching runtime.  Reloading is much simpler and less error-prone
than trying to live-swap the WS connection mid-flight, which would
require touching Hicks's trunk Client / Setup / World code.

`replace` (not `assign`) keeps the back button useful — pressing back
returns to whatever page the player was on before the lobby, not to the
prior variant.

### Hong Kong as disabled `<option>`

Stephen's charter spec explicitly listed Hong Kong as a "Coming soon"
placeholder.  Encoded as `disabled` in its own `<optgroup label="Coming soon">` — visually distinct, screen-reader friendly, and a single line
to flip to enabled once the backend gains a `HongKongRuntime` (or
chooses to route it through Relay with an HK-specific setup bundle).

### Hide-radios via `:has()` rather than removing them

Modern selector support is now broad enough (Safari 15.4+, Chrome 105+,
Firefox 121+) that `:has()` is safe for an enhancement layer.  Keeping
the radios in the DOM means Hicks's `readPickers()` / `writePickers()`
flow still functions, and if the picker fails to mount, players see the
original radios as fallback rather than a broken-looking lobby.

## Playtest Evidence

`playtest-artifacts/ferro-iter2/variant-picker.spec.mjs` — 9 behavior
steps, all pass.  pageErrors=0.  Pre-existing console noise (THREE NaN,
404 on `/api/games/changsha-default*`) is unchanged from baseline.

Spectator regression (`playtest-v3-fresh.spec.mjs`): pageErrors=0,
23 move-log entries — bot game plays autonomously.

## Charter Follow-ups Still Open

- Task 1: Lobby overlay sizing parity (desktop vs mobile)
- Task 4: Mobile canvas/HUD polish beyond what iter-1 verified

## Hand-off Notes

For Hicks: the radio fieldset at `index.html:1147-1154` can eventually
be removed once Ferro's picker is the canonical UI, but it's NOT urgent
— the CSS hides it cleanly today and leaving it in place keeps the
existing `lobby.ts:readPickers/writePickers` path intact as a defensive
fallback.

For backend (Bishop / Apone): the only piece needed to enable the Hong
Kong option is a runtime/setup mapping for `?variant=hong-kong`.  Once
that ships, Ferro just flips one `disabled: true` to `disabled: false`
in `variant-picker.ts:VARIANT_OPTIONS`.
