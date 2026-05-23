# Hicks — Phase K Wave 9 decisions memo

Branch: `stlong/phase-k-wave-9-bringup`
Author: Hicks (Frontend)
Co-author trailer: `Copilot <223556219+Copilot@users.noreply.github.com>`

## Scope shipped

5 deliverables per the W9 directive:

1. **3D mesh pulse for commentary tile-ref highlight** — 2D CSS
   overlay (W8) now joined by the actual WebGL outline-hull pulse
   on the canvas. Independent `mahjong:highlight-tile` listener
   in `game.ts` calls `world.findThingByFace(tileId)` →
   `world.setHighlightedThing(thing)`. World sin-wave envelope
   (0.5 + 0.5·sin(t·π·4)) · (1 − t) over `HIGHLIGHT_DURATION_MS =
   2000 ms`. New API: `World.findThingByFace`,
   `World.setHighlightedThing`, `ObjectView.highlightedObjects`,
   `ObjectView.highlightIntensity`, `MainView.updateHighlight`,
   `CustomOutline.setHighlight` / `.setHighlightIntensity` /
   `.setHighlightColor`. Default highlight color `0xff8c1a` (warm
   orange), thickness 0.036 (vs selection 0.022). See
   `src/frontend/autotable-src/tests/selectors.md` W9 footer for
   canonical citations.

2. **WebGLRenderer feature strip via Vite transform plugins** —
   `three-renderer-big` 531.86 → **507.47 kB** (−24.39 kB, under
   W9 510 kB ceiling). Two `enforce: 'pre'` plugins:
   - `stripUnusedThreeMaterials` gutted 13 unused material
     classes in `three.core.js`. Stubs preserve `isXxxMaterial`
     flags + the depthPacking slot on MeshDepthMaterial.
   - `stripModuleFeatures` gutted `WebGLShadowMap`,
     `WebXRManager`, `WebXRDepthSensing` in `three.module.js`.
     WebXRManager stub extends EventDispatcher to satisfy the
     `xr.addEventListener('sessionstart', …)` call inside the
     renderer constructor.
   - Smoke-tested via headless Playwright (0 JS errors, canvas
     renders). Full autopsy in `docs/frontend-three-budget.md
     §5`.
   - **Do not** retry deep imports (W8 §4 autopsy — grew bundle
     by ~150 kB on the same scene).

3. **Lighthouse 13 + PWA-Builder migration** — `lighthouse@^13`
   is now a permanent devDep (was `--no-save` in W8). LH13
   confirmed the PWA category + every PWA-specific audit are
   gone; only `viewport` survives (now under `best-practices`).
   PWA installability migrates to PWA Builder per Lighthouse
   RFC. Recipe documented in `docs/frontend-pwa-audit.md §3`
   (build → serve → LH13 categories → PWA Builder manual report
   card → manifest-lint substitute). CI/CLI wiring of PWA
   Builder deferred to W10 (needs public preview URL).

4. **Bishop bracket wire-shape canonicalization** —
   `normalizeDoubleElimLayout` (`tournaments.ts:353-380`)
   accepts ONLY the canonical W9 keys (`layout`,
   `winnersBracket`, `losersBracket`, `grandFinal.match`,
   `grandFinal.resetMatch`). When absent in
   `DoubleElimRenderer.render`, the renderer emits
   `<div data-testid="bracket-shape-error" role="alert">` plus
   `console.error('[bracket] Unknown double-elim wire shape — '
   + 'expected { layout: { winnersBracket, losersBracket, '
   + 'grandFinal: { match, resetMatch } } } per '
   + 'docs/contracts/bracket-api.md')`. The W6
   `partitionDoubleElim` heuristic still compiles for its unit
   tests but production code no longer reaches it.
   - NEW file: `docs/contracts/bracket-api.md` pins canonical
     shape + migration discipline (Bishop flag-gates dual
     fields for one wave → Hicks normalises → Vasquez updates
     mocks → Bishop drops flag).

5. **Vasquez W8 spec gate** — **7/7 PASS** (4.1 s, 7 workers,
   chromium): `bracket-live-update`, `commentary-streaming`,
   `commentary-tile-ref-latency`, `losers-bracket-render`,
   `pwa-lighthouse-score`, `three-renderer-540-hard`,
   `vite-signalr-proxy`.

## Trend ledger

`three-renderer-big`: 740 → 579 → 531.86 → **507.47** kB —
Vasquez's monotonic-decrease invariant holds for a 4th
consecutive wave.

## Files modified

Frontend source: 7 files. Build config: 1. Tests/docs: 4
(selectors.md, frontend-three-budget.md, frontend-pwa-audit.md,
contracts/bracket-api.md new). Generated: `dist-size.json` + the
`src/frontend/autotable/` Vite output dir. `package.json` and
`package-lock.json` updated for `lighthouse@^13` devDep bump
(609 added, 50 removed, 126 changed pkgs).

See `.squad/agents/hicks/history.md` for the full W9 entry +
hand-off notes for W10.

## Identity discipline

- All commits use per-command git env
  (`git -c user.name="Hicks (Frontend)" -c user.email=
  "hicks@squad.mahjong"`).
- NEVER `git config user.name`.
- Flock-wrapped commit at `.work/squad-git-lock` (-w 120)
  (relocated from `/tmp/squad-git-lock` by Apone W9).
- Stash-before / restore-after (no half-baked work left).
- Only lane-allowed paths staged: `src/frontend/`,
  `docs/frontend-*`, `docs/contracts/`, `.squad/agents/hicks/`,
  `.squad/decisions/inbox/hicks-*`,
  `src/frontend/autotable-src/tests/selectors.md`.
- `Co-authored-by: Copilot <223556219+Copilot@users.noreply.github
  .com>` trailer included.

## Open hand-offs to W10

1. Bishop commentary panel: dispatch
   `mahjong:highlight-tile` from the tile-ref chip click handler
   (currently only fires the CSS-overlay event).
2. PWA Builder CLI in CI behind a public preview URL.
3. `partitionDoubleElim` removal once W6 unit tests are
   migrated.
4. `build:parcel` script removal (3 waves unused).
5. Manifest gap-fills (`screenshots[]`, `id`, `lang`, `dir`,
   `iarc_rating_id`).
6. PMREMGenerator strip (lazy-instantiated; if proven unreached,
   add to `stripModuleFeatures`).
