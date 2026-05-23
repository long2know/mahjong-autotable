# Hicks — Phase K Wave 13 charter

> Wave-scoped snapshot of the persistent charter at
> `.squad/agents/hicks/charter.md`. The Phase_K_W13/Hicks/
> directory is the W13 hand-off artefact location; the
> persistent charter is the source of truth.

## Identity

- **Name:** Hicks
- **Role:** Frontend Dev
- **Wave:** Phase K Wave 13 — frontend bring-up
- **Branch:** `stlong/phase-k-wave-13-bringup`
- **Co-author trailer:** `Copilot <223556219+Copilot@users.noreply.github.com>`

## Lane (paths I'm allowed to stage)

- `src/frontend/**` (autotable-src + autotable build output)
- `Phase_K_W13/Hicks/**`
- `docs/frontend-*.md`
- `docs/contracts/**`
- `.squad/agents/hicks/**`
- `.squad/decisions/inbox/hicks-*.md`
- `src/frontend/autotable-src/tests/selectors.md` (per W8
  `shared_files` policy)
- `.github/workflows/pwa-*.yml` (carried fwd from W10/W11/W12
  precedent)
- `.github/workflows/bundle-health.yml` (new W13 workflow —
  declared in memo under shared CI policy)
- `src/frontend/autotable-src/tests/e2e/__screenshots__/**`
  (visual-regression baselines — image bytes only; spec file
  itself remains Vasquez's lane)

## NOT in my lane

- Backend C# (Bishop) — including the existing W12
  `/api/spectator/handoff` endpoint the W13 spectate-with-gameId
  routing targets. Hicks ships only the client; the W12 endpoint
  is unchanged this wave.
- Cross-cutting infra / Helm / k8s / Terraform (Apone)
- e2e Playwright spec source under `tests/e2e/*.spec.ts`
  (Vasquez) — including the `manifest-screenshots-visual.spec.ts`
  fix the W13 audit identified
- `tests/selectors.md` outside the `src/frontend/autotable-src/`
  copy (Vasquez authoritative)

## W13 deliverables (five)

1. **PMREMGenerator deeper strip** — extend the W12
   `stripUnusedShaderChunks` plugin from 11 to ~53 entries
   (adds `tonemapping_*`, `lights_phong/toon/physical_*`,
   `transmission_*`, `iridescence_*`, `clearcoat_*`,
   `dithering_*`, `premultiplied_alpha_fragment`, every
   map-feature `_fragment`/`_pars_fragment` chain
   (alphamap/alphahash/alphatest/aomap/lightmap/emissivemap/
   bumpmap/normalmap/specularmap-pars/metalnessmap/
   roughnessmap/displacementmap), `fog_*`); extend the
   W12 `stripUnusedUniformsLib` plugin from 5 to 14 entries.
   Target: `three-renderer-big < 440 KB stretch / < 445 KB
   acceptable`.
2. **LH13 workflow threshold hard-pin** — pull cron data
   from `pwa-audit.yml`, compute p95, hard-pin if ≥3
   successful runs available; otherwise defer to W14 with
   memo notification to Vasquez.
3. **Visual-regression baselines** — capture the three
   `manifest-screenshots-visual.spec.ts` baselines for
   `main-game`, `spectator-commentary`, `tournament-dashboard`
   at the Jest-style location
   `tests/e2e/__screenshots__/manifest-screenshots-visual.spec.ts/<slug>.png`.
4. **`?action=spectate&gameId=<id>` deep-link routing** —
   extend `src/action-router.ts` to detect the W13 co-param
   and POST `/api/spectator/handoff` (Bishop W12) before
   navigating; 200 → spectator livestream + URL rewrite;
   401 → sign-in redirect; 404/network → "Game not found"
   toast.
5. **bundle-health.yml CI workflow** — new
   `.github/workflows/bundle-health.yml` that builds the
   frontend on PR open/sync, parses `dist-size.json`, posts a
   sticky PR comment with the `three-renderer-big` size +
   delta vs W12 baseline (448,648 B), and hard-fails only on
   >500 KB.

## Targets

- `three-renderer-big < 440 KB stretch / < 445 KB acceptable`.
  W12 closed at 448.65 kB; deliverable #1 targets the
  stretch.
- LH13 hard-pin lands ONLY IF ≥3 cron data points exist;
  otherwise deferred to W14 with a documented rationale +
  memo to Vasquez (the threshold owner of record per W11
  §6.1).
- Visual-regression baselines committed as binary assets
  alongside the W12 spec.
- `?action=spectate&gameId=<id>` round-trips Bishop W12
  `/api/spectator/handoff` endpoint with refresh-safe URL
  rewriting on success, a sign-in redirect on 401, and a
  toast on any other failure.
- `.github/workflows/bundle-health.yml` posts a sticky PR
  comment on the wave's bring-up branch (and any subsequent
  PR) with the expected pass verdict for the W13 build.
- All five deliverables land on
  `stlong/phase-k-wave-13-bringup` with the Hicks identity
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
