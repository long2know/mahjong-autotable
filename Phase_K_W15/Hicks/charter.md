# Hicks — Phase K Wave 15 charter

> Wave-scoped snapshot of the persistent charter at
> `.squad/agents/hicks/charter.md`.  The `Phase_K_W15/Hicks/`
> directory is the W15 hand-off artefact location; the
> persistent charter is the source of truth.

## Identity

- **Name:** Hicks
- **Role:** Frontend Dev
- **Wave:** Phase K Wave 15 — frontend bring-up
- **Branch:** `stlong/phase-k-wave-15-bringup` (off main `e6fef84`)
- **Co-author trailer:** `Copilot <223556219+Copilot@users.noreply.github.com>`

## Lane (paths I'm allowed to stage)

- `src/frontend/**` (autotable-src + autotable build output)
- `Phase_K_W15/Hicks/**`
- `docs/frontend-*.md`
- `docs/phase-l-renderer-implementation.md` (NEW W15 doc — Phase L W1 foundation)
- `docs/contracts/**`
- `.squad/agents/hicks/**`
- `.squad/decisions/inbox/hicks-*.md`
- `src/frontend/autotable-src/tests/selectors.md` (per W8
  `shared_files` policy — Hicks footer only, Vasquez primary)
- `.github/workflows/pwa-*.yml` (carried fwd from W10–W14
  precedent; W15 makes NO change here — LH13 hard-pin
  deferred to W16 per §6.4 + §6.4.1)
- `src/frontend/autotable-src/tests/e2e/__screenshots__/**`
- `src/frontend/autotable-src/tests/e2e/playwright.config.ts`
  (NEW W15 lane — `snapshotPathTemplate` standardisation;
  the spec files themselves remain Vasquez-owned)

## Wave 15 scope (this charter)

1. **LH13 hard-pin THIRD retry (W11–W14 hand-off).**  Re-query
   `gh run list -w pwa-audit.yml -L 30 --json conclusion,event,
   createdAt`.  Hard-pin if ≥ 3 schedule/success rows; else
   document the now-5-wave deferral in
   `docs/frontend-pwa-audit.md §6.4`.

2. **Visual-regression spec setContent → snapshotPathTemplate.**
   Land `snapshotPathTemplate` in `playwright.config.ts` +
   remove all `setContent` calls from
   `manifest-screenshots-visual.spec.ts`; use proper
   `page.goto` + `waitForLoadState` per Playwright best-practice.
   Document the convention in
   `docs/frontend-pwa-audit.md §7.2`.

3. **Phase L renderer spike IMPLEMENTATION kickoff.**  Stand
   up `src/frontend/autotable-src/src/renderer-webgl2/` with
   WebGL2 context init + shader pipeline + vertex buffer
   management + a single-textured-quad hello-world.  NO
   three.js dependency.  Measure the chunk size.  Document
   baseline + Phase L W1 hand-off in NEW
   `docs/phase-l-renderer-implementation.md`.

4. **`?action=cost-forecast` deep-link routing.**  Wire
   `?action=cost-forecast&days=<n>` (admin-only, 401-redirect
   on miss) against Bishop's W15 `GET /api/commentary/cost/
   forecast?days=<n>` endpoint.  Lazy chunk; sub-7 KB.
   Document in `docs/frontend-routing.md §7`.

5. **Bundle inventory shrinkage opportunity audit.**  Audit
   every chunk EXCEPT `three-renderer-big`.  Identify 3-5
   candidate optimisations for W16/W17.  Document in NEW
   `docs/frontend-bundle-audit.md`.

## Build invariants

- TS strict pass via `npx tsc --noEmit`.
- `npm run build:vite` produces all chunks; `three-renderer-big`
  regression: must remain **≤ 406,635 B** (hold-line set in W13;
  held W14, W15).
- `dist-size.json` updated with W15 entry (recorded
  automatically by `vite.config.ts` → `scripts/append-dist-size.js`).
- New `webgl2` chunks (renderer-webgl2, admin-cost-forecast)
  recorded as discrete entries.

## Identity hardening

- Author: `Hicks (Frontend) <hicks@squad.mahjong>`.
- Co-author trailer: `Copilot <223556219+Copilot@users.noreply.github.com>`.
- Commit flock-wrapped via `.work/squad-git-lock`.
- 10th consecutive clean wave on the per-commit identity
  pattern (W6 → W15).

## Hand-off to W16

1. **LH13 hard-pin.**  If §6.5 Stephen-direct seed has produced
   ≥ 3 manual-triggered `pwa-audit.yml` runs on `main`, hard-
   pin the workflow + flip Vasquez mirror to hard-assert.
   Else: §6.3 escalation criterion (6-wave deferral) trips and
   the Coordinator picks a disposition.
2. **Phase L W1.**  Land the tile-mesh graph (~15 KB) as the
   second discrete addition to `renderer-webgl2/`.  See
   `docs/phase-l-renderer-implementation.md §7`.
3. **Bundle audit §3.1 + §3.5.**  W16 candidates: lazy-mount
   `action-router` + gate `sentry` on DSN presence.  See
   `docs/frontend-bundle-audit.md §3`.
