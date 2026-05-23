# Hicks — Phase K Wave 5 memo

**Branch:** `stlong/phase-k-wave-5-bringup`
**Date:** 2026-06-21
**Author:** Hicks (Frontend Engineer) `<hicks@squad.mahjong>`
**Scope:** Lazy three.js renderer chunk (scene-shell <500 KB target
hit), retire `game-scene-ready` back-compat marker, keyboard-
accessible sparse-seed reorder + edit prompt, exhaustive `VoiceReason`
discriminated union with `never`-narrowing.
**Build gate:** `parcel build` clean (~8 s wall); `tsc --noEmit
--strict --module esnext --moduleResolution bundler` zero errors.

---

## Headline — three.js peeled into a third chunk, scene-shell hits 2.3 kB

Wave 4 left a single 886 kB `scene-shell.<hash>.js` chunk that
statically imported three.js (~575 kB) + AssetLoader + Game + World
+ MainView + ClientUi.  three.js is the entire renderer-critical
weight floor on a game-URL navigation, and `scene-shell` was the
chunk the user had to download before they saw their first tile.
Wave 4's memo logged the Wave-2 <500 kB target as deferred to
Wave 5.

Wave 5 peels everything that statically imports `from 'three'` out
of the `scene-shell` graph and into a new sibling `three-renderer.ts`
module, dynamic-imported by `scene-shell` once `mountScene()` is
called.  Result:

| Chunk                          | Wave 4   | Wave 5      | Δ                     |
|--------------------------------|----------|-------------|-----------------------|
| `scene-shell.<hash>.js`        | 886.4 kB | **2.33 kB** | **−884 kB (−99.7 %)** |
| `three-renderer.<hash>.js` (NEW, x2 sub-chunks) | —        | 144.9 kB + 724.7 kB ≈ 870 kB | parcel split three.js + asset/world graph naturally |
| `scene-effects.<hash>.js`      | 59.7 kB  | 59.7 kB     | unchanged             |
| `game-bootstrap.<hash>.js`     | 169.9 kB | **170.0 kB** | +0.1 kB (preload helper now also warms three-renderer) |
| `autotable-src.<hash>.js` (eager) | 218.7 kB | 218.7 kB | unchanged             |

**scene-shell <500 KB target met** — and then some.  At 2.33 kB the
new shell is a microscopic coordinator: it dynamic-imports
`three-renderer`, awaits `mountThreeRenderer()`, wires
`attachLobbyClient`, mints `data-testid="scene-shell-ready"`, and
fires the parallel `scene-effects` import.  Net renderer transfer
on cold game-URL load: `2.33 kB + 870 kB three-renderer ≈ 872 kB`,
roughly the same as the Wave-4 monolithic shell (the small
reduction comes from parcel deduplicating the import-helper runtime
shims across the dynamic boundary).

### Why two `three-renderer` sub-chunks?

Parcel naturally splits the heavy graph at the
`asset-loader` / `game` import boundary because `AssetLoader` pulls
in `GLTFLoader` + the GLB URL helpers (a different parcel "shared
module" cohort than the bulk three.js core).  Both sub-chunks live
in `manifest-precache.json` and load in parallel from the same
service-worker cache on warm navigations, so the user experience is
identical to a single chunk; we just don't pay an artificial
"force into one chunk" code-size penalty.

### SW pre-cache

Wave 4 deliberately excluded the renderer from `manifest-precache.json`
because pre-caching ~900 kB on install was hostile.  With Wave 5's
2.3 kB shell, the calculus flips: the user is going to fetch the
renderer on first game-URL navigation anyway, and getting the SW to
commit it on install means warm returning users see WebGL in
~50 ms instead of ~3 s on a flaky connection.  Both `scene-shell`
and `three-renderer` sub-chunks added to the pre-cache list
(updated regex set in `scripts/generate-sw-manifest.js`).

---

## Wave 4 → Wave 5 retirements

### `data-testid="game-scene-ready"` is gone

Wave 3 introduced `game-scene-ready` as a body marker for the
post-renderer ready signal.  Wave 4 renamed it to `scene-shell-ready`
but kept `game-scene-ready` alongside as a back-compat alias so
Vasquez didn't need a same-wave spec sweep.  Vasquez's Wave-4 specs
already gate on `scene-shell-ready`; carrying the alias through
Wave 5 just kept dead branches in the renderer chunk.

Wave 5 deletes the alias emit from `scene-shell.ts:markShellReady`
(no `data-game-scene-ready` body attribute, no second marker
`<div>`, no `mahjong:game-scene-ready` CustomEvent).  selectors.md
strikethrough'd the row in the Wave-5 footer table.

---

## Keyboard-accessible sparse-seed reorder (Wave 5)

Wave 4 shipped drag-drop bracket seeding — mouse-only.  Wave 5
adds a keyboard alternative without disturbing the drag-drop path:

- Each row's seed handle (`tournament-seeding-handle`) is now
  `tabindex="0"` + `role="button"` with a verbose `aria-label`
  describing the current seed state and the available keystrokes.
  The Wave-4 `aria-hidden="true"` on the handle is removed.
- **Arrow Up** / **Arrow Down** on a focused handle reorder the
  row by ±1 and persist via the existing
  `POST /api/tournaments/{id}/seed` endpoint.  Boundary cases
  (already at top / already at bottom) announce a no-op message
  rather than wrapping or failing silently.  Focus is restored to
  the handle's new position on the next rAF (looked up by stable
  `data-player-id`, not the index-based testid which churns under
  reorders).
- **Enter** / **Space** on a focused handle opens an inline modal
  dialog (`role="dialog"` + `aria-modal="true"`) carrying
  `data-testid="seed-keyboard-prompt"`.  The dialog has a numeric
  input (1..N to seed at that position, 0 to demote to unseeded),
  Apply + Cancel buttons, a `role="alert"` validation pill, and
  Enter/Escape keyboard handling.
- Every reorder / edit announces via a visually-hidden
  `aria-live="polite"` region (`data-testid="seed-live-region"`).
  Drag-drop deliberately does NOT announce — mouse users get
  visual feedback already and screen-readers shouldn't hear noise
  from another user's drag.

### Selector contract for Vasquez

| testid                          | element                              |
|---------------------------------|--------------------------------------|
| `seed-row-{playerId}`           | The focusable handle (stable across reorders) |
| `seed-keyboard-prompt`          | The inline dialog root               |
| `seed-keyboard-prompt-input`    | Numeric input inside the dialog      |
| `seed-keyboard-prompt-ok`       | Apply button                         |
| `seed-keyboard-prompt-cancel`   | Cancel button                        |
| `seed-keyboard-prompt-error`    | `role="alert"` validation pill       |
| `seed-live-region`              | `aria-live="polite"` announcer       |

The Wave-4 `tournament-seed-row-{i}` testids on the row `<li>` are
preserved verbatim so existing specs continue to work.

### Why not the browser `prompt()` builtin?

It blocks the main thread, is unstyleable, isn't traversable by
the SR, and Playwright treats it as a dialog the spec must
`accept()` — all hostile to the toolchain.  The inline dialog is
8 lines longer in the source but radically friendlier for both
keyboard users and the spec author.

---

## Exhaustive `VoiceReason` discriminated union (Wave 5)

Wave 4's `voiceReasonToText` accepted `reason: string` and fell
through to a defensive default-case toast.  That worked, but the
union of valid Bishop reasons was implicit — a new wire code added
to `VoiceHubResult` silently routed to the generic fallback instead
of the targeted copy.

Wave 5 promotes the wire vocabulary to a TypeScript discriminated
union:

```ts
export type VoiceReason =
  | 'voice-not-enabled'
  | 'not-seated'
  | 'spectator'
  | 'rate-limited'
  | 'target-not-found'
  | 'unauthorized';
```

`voiceReasonToText(reason: VoiceReason): string` is an exhaustive
switch with a `const _exhaustive: never = reason` guard — adding a
new `VoiceReason` member without updating the switch becomes a
compile-time `Type 'X' is not assignable to type 'never'` error.

A second wrapper `voiceReasonStringToText(reason: string)`
normalises kebab/snake/camel/legacy aliases (`not_seated`,
`notseated`, `spectators`, `unauthenticated`, …) and falls back to
a generic "Voice chat error: …" copy for unknown tokens —
preserving the Wave-4 default-case behaviour at the boundary
without sacrificing exhaustiveness on the typed entry point.
Callers in `voice.ts` that receive raw wire strings (the
`signaller.invoke('JoinVoice')` rejection branch + the `catch (err)`
branch) now go through the string wrapper; the hardcoded
`voiceReasonToText('voice-not-enabled')` call at the top of
`toggleMic` continues to use the typed entry point directly.

`ALL_VOICE_REASONS` is exported as a `ReadonlyArray<VoiceReason>`
for Vasquez's Wave-5 contract test that asserts all 6 reason codes
resolve to non-empty text mappings.

### Bishop's Wave-5 spectator disambiguation

Bishop's Wave-5 backend disambiguates `spectator` from `not-seated`
on the wire (Wave 4 returned `not-seated` for both spectators and
observers who had never claimed a seat).  The mapper carried a
distinct `spectator` branch since Wave 4 (Wave-4 copy: "Spectators
cannot join voice"), so no copy change was required — Bishop's
Wave-5 backend just starts populating the value Bishop already had
in the typed contract.

---

## Files modified

### Source (`src/frontend/autotable-src/src/`)

- `three-renderer.ts` (NEW) — three.js + AssetLoader + Game + World
  + MainView + ClientUi boot.  Dynamic-imported by `scene-shell`.
  Mints `data-testid="three-renderer-ready"`.
- `scene-shell.ts` — rewritten as a thin three.js-free coordinator
  (~80 lines down from ~110).  No static three.js import; awaits
  `three-renderer` chunk; emits `scene-shell-ready` only (no Wave-3
  back-compat alias).
- `game-bootstrap.ts` — header comment refreshed; `preloadGameBootstrap`
  now also warms `three-renderer` so the renderer chunk loads in
  parallel with `scene-shell` rather than serially after it.
- `voice.ts` — `VoiceReason` discriminated union + `isVoiceReason`
  type guard + `voiceReasonToText(VoiceReason)` exhaustive switch
  with `never` guard + `voiceReasonStringToText(string)` wrapper +
  `ALL_VOICE_REASONS` export.  In-file callers routed through the
  appropriate variant.
- `tournaments.ts` — `buildSeedingPanel` adds focusable handles
  with `Arrow{Up,Down}` + `Enter/Space` keyboard handling,
  `aria-live="polite"` announcement region, `openSeedKeyboardPrompt`
  inline modal dialog, `focusHandleByPlayerId` post-rerender focus
  restore helper, `seed-row-{playerId}` stable testid on the handle.

### Build tooling (`src/frontend/autotable-src/scripts/`)

- `generate-sw-manifest.js` — `SCENE_SHELL_RE` + `THREE_RENDERER_RE`
  added to the pre-cache allow-list so the SW install pre-warms
  both renderer chunks.

### Tests / docs

- `tests/selectors.md` — Wave 5 footer (renderer split, keyboard
  seeding, typed voice reasons + Wave-5 Vasquez spec map).

### Built artefacts (`src/frontend/autotable/`)

- `scene-shell.6e7f6886.js` (NEW hash, 2.33 kB; was 886 kB)
- `three-renderer.9b0cd931.js` (NEW, 144.9 kB; GLTFLoader + asset graph)
- `three-renderer.c3e34903.js` (NEW, 724.7 kB; three.js core + Game/World)
- `game-bootstrap.68251e93.js` (re-hashed)
- `tournaments.50127ca1.js` (re-hashed)
- `voice.ef5d6345.js` (re-hashed)
- `manifest-precache.json` (regenerated; now 14 assets — added
  scene-shell + 2 three-renderer sub-chunks)
- 4 stale Wave-4 chunks pruned by `generate-sw-manifest.js`
  (`game-bootstrap.82b641aa`, `scene-shell.fb7fa473`,
  `tournaments.a4482948`, `voice.62b99d7e`).

---

## Open Wave 6 questions

- The two `three-renderer` sub-chunks (145 kB + 725 kB) load in
  parallel from cache, so warm load is fine — but on a cold first
  game-URL navigation they serialise behind the dynamic-import
  resolver.  A tiny `<link rel="modulepreload">` in `index.html`
  for both sub-chunks would parallelise the cold path; deferred so
  we can measure first.
- `three.js` itself is ~575 kB of which we use ~30 % — the unused
  add-ons (post-processing passes, examples loaders the asset
  pipeline doesn't touch) are dead weight.  A tree-shaken parcel
  config could plausibly halve the renderer transfer.
- `scene-effects.<hash>.js` (60 kB GameUi + MoveLog) could itself
  split GameUi modals (result modal, settings drawer, replay
  viewer, claim window) into separate sub-chunks if any one of
  them becomes a hot spot.

---

## Cross-lane

- **Bishop (Backend)** — Wave 5 `VoiceReason` union mirrors Bishop's
  Wave-5 typed `VoiceHubResult.reason` enum; Bishop's spectator
  disambiguation lands without a frontend copy change.
- **Apone (Platform)** — no changes to CI/CD workflows or
  deployment surface this wave; renderer is bundled into the same
  static asset tree.
- **Vasquez (QA)** — Wave-5 selector additions: `three-renderer-ready`,
  `seed-row-{playerId}`, `seed-keyboard-prompt` (+ child
  `-input`/`-ok`/`-cancel`/`-error`), `seed-live-region`.  See
  selectors.md Wave-5 footer table for the canonical contract.

Memo: this file (force-add: `.squad/decisions/inbox/hicks-phase-k-wave-5.md`).
