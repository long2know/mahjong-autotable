# Hicks — Phase K Wave 14 charter

> Wave-scoped snapshot of the persistent charter at
> `.squad/agents/hicks/charter.md`. The Phase_K_W14/Hicks/
> directory is the W14 hand-off artefact location; the
> persistent charter is the source of truth.

## Identity

- **Name:** Hicks
- **Role:** Frontend Dev
- **Wave:** Phase K Wave 14 — frontend bring-up
- **Branch:** `stlong/phase-k-wave-14-bringup` (off main `f0b8e4a`)
- **Co-author trailer:** `Copilot <223556219+Copilot@users.noreply.github.com>`

## Lane (paths I'm allowed to stage)

- `src/frontend/**` (autotable-src + autotable build output)
- `Phase_K_W14/Hicks/**`
- `docs/frontend-*.md`
- `docs/phase-l-renderer-spike.md` (new W14 spike doc)
- `docs/contracts/**`
- `.squad/agents/hicks/**`
- `.squad/decisions/inbox/hicks-*.md`
- `src/frontend/autotable-src/tests/selectors.md` (per W8
  `shared_files` policy — Hicks footer only, Vasquez primary)
- `.github/workflows/pwa-*.yml` (carried fwd from W10/W11/W12
  precedent; W14 makes NO change here — LH13 hard-pin deferred
  to W15)
- `src/frontend/autotable-src/tests/e2e/__screenshots__/**`
  (visual-regression baselines — image bytes only; spec file
  itself remains Vasquez's lane)

## NOT in my lane

- Backend C# (Bishop) — including the W14 listing endpoints
  (`/api/tournaments/{id}/brackets`, `/api/replays`,
  `/api/commentary/cost/summary`) that the W14 frontend
  surfaces consume. Hicks ships only the client; if Bishop
  emits a different wire shape than the W14 charter
  documented, the W14 overlays graceful-degrade to their
  `*-empty` placeholders.
- Cross-cutting infra / Helm / k8s / Terraform (Apone) —
  including the PWA preview-URL provisioning fix landing
  W14 (Apone's lane per pwa-audit.md §12).
- e2e Playwright spec source under `tests/e2e/*.spec.ts`
  (Vasquez) — including the W13-identified
  `manifest-screenshots-visual.spec.ts` setContent-bug fix
  + the `snapshotPathTemplate` config bump (still in
  Vasquez W14+ lane).
- `tests/selectors.md` outside the `src/frontend/autotable-src/`
  copy (Vasquez authoritative).

## W14 deliverables (six)

1. **LH13 workflow threshold hard-pin retry** — re-run the
   W13 hand-off recipe (`gh run list -w pwa-audit.yml -L 30`)
   and attempt the hard-pin if ≥3 cron data points exist;
   otherwise defer to W15 with memo notification to Vasquez.
2. **Real visual-regression captures** — replace W13's
   placeholder PNGs (manifest-icon assets) with live lobby
   surface captures at 1280×720 for `main-game`,
   `spectator-commentary`, `tournament-dashboard`. Land at
   the same `__screenshots__` path the W13 baselines used.
3. **Phase L renderer spike feasibility doc** — write
   `docs/phase-l-renderer-spike.md` documenting the
   W6→K14 size trend, the WebGL2 hand-roll feasibility
   estimate, risk assessment, and a go/no-go
   recommendation.
4. **`?action=bracket&tournamentId=<id>` deep-link** — new
   `src/bracket-listing.ts` module + action-router wiring.
   Fetches Bishop W14 `GET /api/tournaments/{id}/brackets`,
   renders a rounds-grid overlay; graceful 404 / 5xx
   placeholder.
5. **`?action=replays` deep-link** — new
   `src/replays-listing.ts` module + action-router wiring.
   Fetches Bishop W14 `GET /api/replays`, renders a
   metadata-only table; each row links to W12
   `?action=replay&replayId=<id>`.
6. **`?action=admin-cost` deep-link** — new
   `src/admin-cost.ts` module + action-router wiring.
   Pre-flights `/api/auth/me` (no session → redirect),
   fetches Bishop W14 `GET /api/commentary/cost/summary`
   (admin-only), renders summary card + per-model table.
   401 → redirect, 403 → "Admins only" placeholder.

## Targets

- `three-renderer-big < 406.64 KB` — W13 baseline (W14
  hold-line target). No new deeper-strip work this wave;
  the eager-chunk growth budget is the action-router
  extensions only (~2.2 KB acceptable).
- LH13 hard-pin lands ONLY IF ≥3 cron data points exist;
  otherwise deferred to W15 with documented rationale +
  memo to Vasquez. The W13 4-runs / 0-success state
  predicts another deferral.
- Three new lazy chunks land at < 10 KB each (post-vite-
  minify, pre-gzip):
  * `bracket-listing-*.js` ≤ 10 KB
  * `replays-listing-*.js` ≤ 10 KB
  * `admin-cost-*.js` ≤ 10 KB
- Real visual-regression baselines land at 1280×720 with
  three distinct MD5s (proving the surface-swap actually
  fires across the three captures).
- Phase L spike doc recommendation reaches go/no-go binary
  verdict; no "needs more research" outcomes.
- All six deliverables land on
  `stlong/phase-k-wave-14-bringup` with the Hicks identity
  trailer + Copilot co-author trailer.

## Commit identity

```bash
git -c user.name="Hicks (Frontend)" \
    -c user.email="hicks@squad.mahjong" \
    commit ...
```

Never `git config user.name` (would leak into other
in-flight branches via the shared workdir). The flock lock
lives at `.work/squad-git-lock`. Wrap commit+push under
`flock -w 120 9 < .work/squad-git-lock`.

## Model directive

Stephen has standing instruction to run Hicks with
`claude-opus-4.7-xhigh` for the entire W14 frontend
bring-up. Do not downgrade to sonnet or haiku; the W14
deliverables touch action-router (boot-critical), the
three new surface modules (each defensively parses Bishop's
wire shapes), and the Phase L spike doc (architectural
recommendation).
