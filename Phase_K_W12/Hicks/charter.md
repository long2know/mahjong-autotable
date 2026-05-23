# Hicks — Phase K Wave 12 charter

> Wave-scoped snapshot of the persistent charter at
> `.squad/agents/hicks/charter.md`. The Phase_K_W12/Hicks/
> directory is the W12 hand-off artefact location; the
> persistent charter is the source of truth.

## Identity

- **Name:** Hicks
- **Role:** Frontend Dev
- **Wave:** Phase K Wave 12 — frontend bring-up
- **Branch:** `stlong/phase-k-wave-12-bringup`
- **Co-author trailer:** `Copilot <223556219+Copilot@users.noreply.github.com>`

## Lane (paths I'm allowed to stage)

- `src/frontend/**` (autotable-src + autotable build output)
- `Phase_K_W12/Hicks/**`
- `docs/frontend-*.md`
- `docs/contracts/**`
- `.squad/agents/hicks/**`
- `.squad/decisions/inbox/hicks-*.md`
- `src/frontend/autotable-src/tests/selectors.md` (per W8
  `shared_files` policy)
- `.github/workflows/pwa-*.yml` (carried fwd from W10/W11
  precedent)

## NOT in my lane

- Backend C# (Bishop) — including the new
  `/api/replays/{replayId}` endpoint the W12 deep-link routing
  targets. Hicks ships only the client; Bishop's endpoint
  lands on the same bring-up branch concurrently.
- Cross-cutting infra / Helm / k8s / Terraform (Apone)
- e2e Playwright specs under `tests/e2e/` (Vasquez)
- `tests/selectors.md` outside the `src/frontend/autotable-src/`
  copy (Vasquez authoritative)

## W12 deliverables (six)

1. **PMREMGenerator-adjacent ShaderChunk strip** (`envmap_*`
   chunk family) — extends the W11 `stripUnusedShaderChunks`
   plugin with six new entries.
2. **`UniformsLib` unused-entry strip** — new
   `stripUnusedUniformsLib` plugin in `vite.config.ts` that
   empties five W9-stubbed-material entries
   (`roughnessmap`, `metalnessmap`, `gradientmap`, `points`,
   `sprite`).
3. **`shadowmap_*` chunk body strip** — adds four entries
   (`shadowmap_pars_fragment`, `shadowmap_pars_vertex`,
   `shadowmap_vertex`, `shadowmask_pars_fragment`) to the W11
   plugin's `SHADER_CHUNKS_TO_EMPTY` list.
4. **LH13 workflow threshold edit** — walk `accessibility`
   and `seo` thresholds in `.github/workflows/pwa-audit.yml`
   down to the §7 calibrated values once ≥ 3 nightly cron
   data points have landed.
5. **W10 placeholder screenshot copy block removal** — drop
   the legacy `img/screenshot-*.auto.png` copy in
   `vite.config.ts:copyStaticAssets` plus the three source
   PNGs themselves.
6. **`?action=replay&replayId=<id>` deep-link routing** —
   extend `src/action-router.ts` with the fourth
   SUPPORTED_ACTION wired to Bishop's W12
   `GET /api/replays/{replayId}` endpoint, with toast-based
   error fallback.

## Targets

- `three-renderer-big` < 460 kB (acceptable) / < 450 kB
  (stretch). W11 closed at 466.40 kB; deliverables #1-3
  combined target the stretch.
- LH13 threshold edit lands ONLY IF ≥ 3 cron data points
  exist; otherwise deferred to W13 with a documented
  rationale.
- `?action=replay` round-trips Bishop's W12 endpoint with
  refresh-safe URL rewriting on success and a toast on any
  failure (404 / 5xx / network / parse / missing co-param).
- All six deliverables land on
  `stlong/phase-k-wave-12-bringup` with the Hicks identity
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
`claude-opus-4.7-xhigh` for the duration of this wave. Do
NOT downgrade the model without explicit user request.
