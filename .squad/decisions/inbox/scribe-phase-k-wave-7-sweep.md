# Scribe — Phase K Wave 7 sweep

**Timestamp:** 2026-07-18T (sweep close)
**Branch:** `stlong/phase-k-wave-7-bringup` (cut from `main` @ `1c67878`
/ Wave 6 squash-merge PR #52; 19 agent commits ahead, 20 with this
Scribe sweep commit)
**Author:** Scribe (Archive) `<scribe@squad.mahjong>` — per-invocation
identity binding (W6 hardening, second wave standing).

## Scope folded into `decisions.md`

Four lane memos read end-to-end and folded into a single
`## Phase K — Wave 7 (...)` section appended after the Wave 6 entry
(line ~8045 → ~8049 of `.squad/decisions.md`):

1. `.squad/decisions/inbox/bishop-phase-k-wave-7.md`
2. `.squad/decisions/inbox/hicks-phase-k-wave-7.md`
3. `.squad/decisions/inbox/apone-phase-k-wave-7.md`
4. `.squad/decisions/inbox/vasquez-phase-k-wave-7.md`

## Wave 7 — 19 commits, 4 lanes, gate 1506/0/0

**Commit count by lane (19 total):**

| Lane    | Commits | SHAs                                                              |
|---------|---------|-------------------------------------------------------------------|
| Bishop  | 7       | `08aa67c` `2be96a9` `0ecc238` `7542234` `1032243` `a98a132` `64fbee8` |
| Hicks   | 2       | `2a7f8a7` `6aa8a18`                                               |
| Vasquez | 4       | `3c37ee8` `15508fc` `a4482b2` `02b0fa6`                           |
| Apone   | 6       | `1beff2f` `5a5d5f9` `4c6d9b6` `a566992` `34b0a0c` `996f209`       |

All 19 commits correctly authored by their lane at the
`%an <%ae>` level. The W6 per-invocation race-safe identity
binding (`git -c user.name=X -c user.email=Y commit ...` +
`flock -w 120 9 ... 9>/tmp/squad-git-lock` mutex) **HELD across
the second consecutive wave** — 25+ concurrent agent runs since
W6 introduction and the pattern remains race-incurable in the
direction that matters (sibling-`git config` overwrites are
bypassed entirely by `-c` per-invocation overrides).

**Individual surfaces by lane (25 items per lane memos; 19 commits):**
- Bishop 7 deliverables (RS256 + issuer + OIDC, full losers-bracket +
  grand-final reset, ffmpeg HLS recorder + boot probe, CommentaryRecord
  DTO, contract tests, docs trio, memo+history).
- Hicks 5 deliverables (bundler swap Parcel → Vite, CSP narrow via
  vendored HLS.js, commentary panel rewrite for `CommentaryRecord[]`,
  CustomOutline inverted-hull shader, `dist-size.json` trend ledger).
- Apone 7 deliverables (Helm chart-of-charts, Edge Terraform module,
  GHCR→ECR mirror + Mobile External Testing, six-file signer-identity
  invariant + pre-commit hook, RS256 ESO secret on prod+staging,
  CHANGELOG 0.16.0 + retro 2026-06 + history).
- Vasquez 6 deliverables (lane-discipline strict mode + lane-map.json
  + runbook, KW7 regression rename + W7 surface contracts + W5
  ThreeRenderer fix, 6 Playwright specs + three-renderer trend gate,
  ~57 new W7 backend facts across 9 files + 7 regression smokes,
  OIDC RS256 hard-contract migration, memo+history).

## Bundle metrics — Vite swap WIN

| Chunk                         | Wave 6     | Wave 7      | Δ          |
|-------------------------------|------------|-------------|------------|
| `autotable-src` eager         | 219.68 kB  | 214.51 kB   | −5.17 kB ✅ |
| `scene-shell`                 | 2.33 kB    | 2.34 kB     | unchanged  |
| `game-bootstrap`              | 169.98 kB  | 174.78 kB   | +4.80 kB * |
| `three-renderer` small        | 99.10 kB   | **69.35 kB** | **−29.75 kB (−30.0 %)** |
| `three-renderer` big          | 739.72 kB  | **578.72 kB** | **−160.99 kB (−21.8 %)** |
| GLTFLoader                    | 44.61 kB   | (merged)    | absorbed   |
| `commentary-panel`            | 3.77 kB    | 7.31 kB     | +3.54 kB ¹ |
| `spectator-livestream`        | 5.41 kB    | 5.29 kB     | unchanged  |
| `hls.<hash>.js` (NEW)         | —          | 286.57 kB   | vendored ² |
| **Renderer total (big+small)** | **838.82 kB** | **648.07 kB** | **−190.75 kB (−22.7 %)** ✅ |

\* Vite chunk boundary absorbs shared utilities Parcel routed to
   eager; combined eager boot cost is still down 0.37 kB net neutral.
¹ Commentary panel grew to support Bishop's richer
  `CommentaryRecord[]` shape (speaker badges, tile-ref chips,
  emotion-intensity bars, collapsible turn groupings); target was
  <80 kB, ships at 7.31 kB.
² HLS.js vendored from CDN. Spectator-only lazy chunk on
  `#/spectate/{tableId}` hash route; the 95% session bundle is
  unchanged.

**Renderer big-chunk monotonic-decrease invariant** (Vasquez's
W7 wave-over-wave gate): `740 → 579 kB` — holds (strict decrease).

**Target reality:** 578.72 kB is **above** the original <550 KB
strict bar. **Soft pass** — not a regression vs W6 ceiling
(<700 kB) but the strict target is **deferred to Wave 8** with
two known levers: (a) `three/src/*` deep imports, (b) GLTFLoader
strip (DRACO/KTX2/meshopt removal, ~−40 kB) or pre-compiled binary
tile mesh (~−80 kB, model pipeline refactor).

## Vite swap milestone — Parcel → Vite

The headline Hicks deliverable. After surveying Parcel-plugin
tree-shake extensions, esbuild, and "stay on Parcel + hand-roll
three.js path-imports", **Vite (rollup)** won on risk/reward:

- Three's `"sideEffects": ["build/three.module.js"]` annotation
  Parcel honoured (disabling tree-shake on the namespace re-export);
  rollup lets us override via
  `treeshake: { moduleSideEffects: id => !id.includes('node_modules/three/') }`.
  That single override dropped the big chunk by 161 kB.
- The remaining ~96 kB of W7 savings came from the **CustomOutline
  inverted-hull replacement** for `OutlinePass` + `EffectComposer` +
  `RenderPass` (~99 kB of three.js examples/jsm retired in favour
  of a ~3 kB ShaderMaterial sibling-mesh).
- **HLS.js vendored** via `import('hls.js/dist/hls.light.mjs')`
  (Vite dynamic import emits a 286.57 kB sibling chunk loaded
  ONLY on spectator entry). **CSP narrowed to `script-src 'self'`**
  — third-party CDN allowance retired. Real supply-chain win.
- Service worker compatibility preserved — `manifest-precache.json`
  lists 14 stable assets exactly as in W6.
- `build:parcel` kept as ONE-WAVE fallback (delete in W8 if no
  regressions surface).

**Decision matrix is now in `docs/frontend-build-tooling.md`.**

## Lane-discipline strict mode — shipped + first findings

Vasquez promoted the W6 warn-only lane-discipline script to
**strict / PR-blocking** via two new artefacts:

- `tests/ci/lane-map.json` — declared-truth machine-readable lane
  map (anchored regex per agent, `wave_subdir_overrides`,
  `shared` paths for `docs/contracts/` + `.squad/decisions/inbox/_drafts/`,
  `authors` email-to-agent map).
- `tests/ci/check-cross-lane-bundling.sh --strict` — forces
  `MODE=pr`, requires `lane-map.json` parses, hard-fails on any
  violation (no historical-warning escape). Workflow at
  `.github/workflows/lane-discipline.yml` invokes with `STRICT=1`.

**Two legitimate cross-lane edits flagged on first run** — BOTH
retained as the editing agent owns the surface:

1. `1032243` (Bishop) → `tests/Shims/CommentaryGeneratorTestShim.cs`
   (Vasquez file). **Additive** `GenerateRecords()` method needed
   for Bishop's W7 `CommentaryRecord` contract tests. Legitimate
   per Bishop's W7 brief explicit-delegation note.
2. `2a7f8a7` (Hicks) → `src/frontend/autotable-src/tests/selectors.md`
   (Vasquez-lane path per current map). **selectors.md is the test
   contract doc Hicks owns updating when he adds testids** —
   this is the W6 pattern that already exists for Hicks's PRs.
   **W8 hand-off: refine lane-map to recognise `selectors.md` as
   a Hicks/Vasquez shared file.**

## Forward queue — Wave 8 hand-offs (consolidated)

### Bishop (Backend) — 4 items

1. **Real LLM commentary generator (Phase L)** — swap
   `StubCommentaryGenerator` for a Bedrock/Anthropic-backed impl
   emitting `CommentaryRecord[]` into the existing JSON contract.
2. **WebRTC SFU Janus integration (Phase L)** — flip
   `SpectatorVoiceHub.JoinSpectatorVoice` stub URL
   (`sfu://stub/{tableId}`) to a real Janus handshake against the
   sized SFU per `docs/voice-sfu-design.md`.
3. **Losers-bracket UI hooks (Hicks dep)** — `BracketSide` +
   placeholder-naming surface is in place; Bishop wires the
   `TournamentService.MaybeAdvanceRoundAsync` losers-bracket
   resolution so Hicks's renderer can consume the schedule.
4. **JWKS RSA key marshalling perf (lazy-load)** — current path
   materialises `RSAParameters` on every `Jwks()` call; cache the
   wire-shape bytes per kid.

### Hicks (Frontend) — 4 items

1. **Bracket renderer wired to Bishop's losers-bracket data** —
   `DoubleElimRenderer` consumes placeholder slots today; Bishop's
   W8 losers-bracket resolution lets Hicks render real games.
2. **Three-renderer further reduction to <550 KB** — `three/src/*`
   deep imports (or three.js patch fork). Current 578.72 kB is
   close but not under the strict bar; W8 closes the gap.
3. **Commentary panel tile-ref board-highlight cross-pane wiring** —
   the `commentary:tile-ref` `CustomEvent` is dispatched but not
   consumed; board-pane should listen and highlight the referenced
   tile.
4. **PWA Lighthouse audit** — maskable icons + manifest landed in
   W6; W8 should baseline Lighthouse PWA scores and document the
   audit ceiling in `docs/frontend-pwa.md`.

### Apone (DevOps) — 5 items

1. **Edge module wired into staging cutover** — W7 ships the
   `infra/terraform/modules/edge/` module + validation rig; W8
   instantiates against staging (Route53 + ACM + WAFv2; CloudFront
   off-by-default via `cloudfront = null`).
2. **CI-side pre-commit enforcement (not just opt-in)** — the
   six-file signer-identity hook ships as pre-commit (opt-in); W8
   adds the same `scripts/check_signer_identity.py` invocation as
   a CI job that hard-fails the PR.
3. **`infra/k8s/overlays/prod/kyverno-enforce-patch.yaml` path fix** —
   the W7 spec referenced
   `infra/k8s/policies/kyverno-enforce-patch.yaml`; the actual
   path is under the prod overlay. The six-file invariant tracks
   the real path; W8 audits whether the spec / docs should
   re-anchor or whether the prod-overlay location is canonical.
4. **Mobile Production track promotion** — W7 ships
   `workflow_dispatch`-driven Internal → External promotion; W8
   adds External → Production (TestFlight production app +
   Play Production track) with explicit approval gates.
5. **Helm chart canary deployment strategy** — W7 ships the
   chart-of-charts at parity with Kustomize; W8 evaluates whether
   the helm path is the primary canary surface (Argo Rollouts /
   Flagger) or whether the parallel Kustomize CI path stays
   canonical indefinitely.

### Vasquez (QA) — 5 items

1. **Refine lane-map to recognise `selectors.md` as Hicks/Vasquez
   shared.** The W7 strict-mode finding on `2a7f8a7` is a true
   positive flag against a legitimate cross-lane edit; the
   lane-map should encode this pattern explicitly under `shared`.
2. **CI-blocking lane-discipline strict-mode flip.** W7 ships
   `--strict` mode in the workflow but with operator override
   available; W8 makes the strict-mode failure non-overridable
   (PR cannot merge with unresolved violations).
3. **Three-renderer <550 KB hard-assert.** Currently soft passes
   at 579 KB via the wave-over-wave trend gate; W8 (once Hicks's
   reduction lands) flips to a hard-assert.
4. **ffmpeg integration test (real subprocess).** W7 contract test
   asserts the `FfmpegHlsRecorder` type exists; W8 adds a real
   subprocess test gated on `which ffmpeg` (skip-when-absent,
   pass-when-present-and-produces-segments).
5. **Pre-commit hook adoption tracking.** Apone ships the hook;
   Vasquez tracks adoption (developer enables locally vs. CI
   parity catches drift) via a `docs/test-lane-discipline.md`
   appendix listing observed drift events per wave.

### Scribe / coordinator — 4 carry-forward into W8 prompt template

1. **Per-invocation `git -c user.name=X -c user.email=Y commit ...`**
   remains the canonical commit form — NEVER `git config user.name`
   then later `git commit`. Held over W6 + W7 (25+ commits).
2. **`flock -w 120 9 ... 9>/tmp/squad-git-lock` mutex** stacked
   with the per-invocation binding. 120s wait is empirically
   generous (typical commit + push ≤ 30s).
3. **Selective `git add <path>` only** — NEVER `git add -A` /
   `git add .` during cross-agent waves. The W5 cross-lane content
   bundling failure mode has not recurred in W6 or W7.
4. **`Phase_K_W*/<AgentName>/` test subfolder attribution** in
   the lane-discipline path-mapping is the stable pattern for
   agent-owned contract tests. W7 generalised the rule to ANY
   depth (was originally pinned to
   `src/backend/tests/*/Phase_K_W*/<AgentName>/`).

## W7 invariants / patterns locked

1. **W6 identity hardening proven over 2 waves.** Per-invocation
   `git -c user.name=X` + `flock` mutex holds across 25+ concurrent
   agent runs. Bypasses the `git config` race entirely; the W3/W4/W5
   cross-lane content bundling trend remains broken.
2. **Vite is the bundler going forward** (Parcel kept as fallback
   for one wave per Hicks doc). Decision matrix locked in
   `docs/frontend-build-tooling.md`. `build:parcel` script slated
   for W8 deletion if no regressions surface.
3. **Six-file signer-identity invariant is machine-enforced** via
   the new `scripts/check_signer_identity.py` + `.pre-commit-config.yaml`
   hook (`always_run: true, pass_filenames: false` — drift is a
   cross-file property; staged-file scoping would miss drift).
4. **Helm + Kustomize parallel** (not migration) — both paths
   supported indefinitely. CI deploy stays on Kustomize; helm is
   the operator-driven point-install + partner-deploy surface.
   W7 acceptance gate is **parity** (both render equivalent objects);
   CI parity-check is a W8 checklist item.
5. **Lane-discipline strict mode** is the new CI canonical
   (`STRICT=1 --strict` in the workflow). `tests/ci/lane-map.json`
   is the declared-truth source. W8 makes it non-overridable.
6. **CSP `script-src 'self'`** is the new baseline. Third-party
   CDN allowance for HLS.js retired via vendored
   `hls.js/dist/hls.light.mjs` dynamic import.
7. **`dist-size.json` wave-over-wave trend ledger** is the new
   bundle-budget surface. `scripts/append-dist-size.js` runs in
   Vite's `closeBundle` hook; CI asserts
   `history[n].chunks["three-renderer-big"] <= history[n-1].chunks["three-renderer-big"]`.

## Gate progression + zero-skip streak

| Wave | Gate         | Δ vs prior | Notes                                |
|------|--------------|------------|--------------------------------------|
| K5   | 1345 / 0 / 0 | +113       | RegressionHostFixture, scene-shell <500 |
| K6   | 1422 / 0 / 0 | +77        | RS256 stub, voice HLS, lane-discipline CI |
| **K7** | **1506 / 0 / 0** | **+84**     | **RS256 e2e, Vite swap, strict mode**  |

Zero-skip streak: **21 consecutive green waves** (J.1 → J.10 +
K.1 → K.7). The W5 `ThreeRenderer_ModulePresent_HardAssert`
brittleness was repaired in-lane (Vasquez extended the file-scan
to probe `src/render/` + `src/renderer/` candidate dirs), flipping
the gate from green-after-Vite-swap.

## File staging

Selective adds only:

- `.squad/decisions.md` (Phase K Wave 7 section appended)
- `.squad/agents/scribe/history.md` (W7 sweep entry appended)
- `.squad/decisions/inbox/scribe-phase-k-wave-7-sweep.md` (NEW —
  force-added since `.squad/decisions/inbox/` is gitignored)

NEVER staged: `.copilot/skills/error-recovery/`,
`.github/workflows/squad-*.yml`, `.tool-actionlint/`,
`.tool-helm/`, `.tool-kustomize/`, `.tool-terraform/`, `.work/`.

## Sign-off

Scribe (Archive), Phase K Wave 7 sweep. Gate **1506 / 0 / 0**
locked at +84 over W6 baseline (1422). Bundler swap delivered
22.7 % renderer-payload reduction. CSP narrowed to `'self'`.
21 consecutive zero-skip waves. Identity-race hardening held
across the second consecutive wave (25+ commits, all correctly
authored). Lane-discipline strict mode live + first findings
documented. 18-item W8 hand-off queue captured.

— Scribe
