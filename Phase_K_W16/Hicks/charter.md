# Hicks — Phase K Wave 16 charter

> Wave-scoped snapshot of the persistent charter at
> `.squad/agents/hicks/charter.md`.  The `Phase_K_W16/Hicks/`
> directory is the W16 hand-off artefact location; the
> persistent charter is the source of truth.

## Identity

- **Name:** Hicks
- **Role:** Frontend Dev
- **Wave:** Phase K Wave 16 — frontend bring-up
- **Branch:** `stlong/phase-k-wave-16-bringup` (off main `c1f336a`)
- **Co-author trailer:** `Copilot <223556219+Copilot@users.noreply.github.com>`

## Lane (paths I'm allowed to stage)

- `src/frontend/**` (autotable-src + autotable build output)
- `Phase_K_W16/Hicks/**`
- `docs/frontend-*.md`
- `docs/phase-l-*.md`
- `docs/lh13-*.md`
- `.squad/decisions/inbox/hicks-phase-k-wave-16.md`

## Deliverables (four)

1. **LH13 6th-wave decision (Option A soft-flip).**
   The Stephen-direct seed from §6.5 has not landed; continuing
   to defer would trip the §6.3 6-wave escalation.  Pick
   Option A: documentation-only soft-flip with provisional
   thresholds (W11 calibration values, tagged
   `provisional-until-calibrated`).  New
   `docs/lh13-soft-pin-rationale.md`.

2. **Phase L tile-mesh graph (~15 KB).**
   Expand `renderer-webgl2/` (W15: 6.2 KB hello-world) with
   instanced tile mesh + atlas loader stub + orbital camera.
   Target: total chunk ≤ 22 KB.

3. **Bundle audit §3.1 + §3.5 surgery.**
   * §3.1 — `autotable-src-eager` lazy-mount action-router +
     avatar-migration modal.
   * §3.5 — sentry conditional load (gate on PROD || debug LS
     flag).
   * Update `docs/frontend-bundle-audit.md` with delivered
     savings + W17 §3.6+ candidates.

4. **three-renderer hold-line (6th wave).**
   Hold `three-renderer-big` at ≤ 406,635 B.  Investigate any
   regression; take any quick wins that drop it further without
   disrupting Phase L work.

## Build invariants

- TS strict pass via `npx tsc --noEmit`.
- `npm run build:vite` produces all chunks; `three-renderer-big`
  regression: must remain **≤ 406,635 B** (hold-line set in W13;
  held W14 / W15 / W16).
- `renderer-webgl2` chunk: must remain **≤ 22,000 B** through W16.
- `dist-size.json` updated with W16 entry (recorded automatically
  by `vite.config.ts` → `scripts/append-dist-size.js`).
- New tracked chunks (W16): `action-router`, `sentry-shim`,
  `sentry` (SDK).

## Identity hardening

- Author: `Hicks (Frontend) <hicks@squad.mahjong>`.
- Co-author trailer: `Copilot <223556219+Copilot@users.noreply.github.com>`.
- Commit flock-wrapped via `.work/squad-git-lock`.
- 11th consecutive clean wave on the per-commit identity
  pattern (W6 → W16).

## Hand-off to W17

1. **LH13 hard-pin.**  If the Coordinator-direct seed from
   `docs/lh13-soft-pin-rationale.md §4.1` has produced ≥ 3
   manual-triggered `pwa-audit.yml` runs on `main`, re-run
   §4.2 evidence collection.  If convergence: hard-pin the
   provisional values from §3 of that doc + close the soft-pin
   doc with a `supersededAt: W17` tag.  Else: re-coordinate
   with Coordinator on §4.3 diagnostics.

2. **Phase L W3.**  Land the **animation graph** on the W16
   tile-mesh — tween-on-deal, lift-on-discard, dice-roll
   spin.  Should drop into `renderer-webgl2/animation.ts`
   as the third discrete addition (~8-12 KB target keeping
   total chunk under ~30 KB).  See
   `docs/phase-l-renderer-implementation.md §8`.

3. **Bundle audit §3.6 / §3.7 / §3.8.**  W17 candidates
   surfaced during W16 surgery:
   * §3.6 — `i18n` lazy locale-table split.
   * §3.7 — `game-bootstrap` autotable-src-eager re-fold
     (move scheduler shells to bootstrap chunk).
   * §3.8 — `SENTRY_DEFER_INIT` meta-tag flag to drop the
     sentry-shim entirely for performance-sensitive deploys.
