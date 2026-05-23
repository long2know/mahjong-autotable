# Changelog

All notable changes to `mahjong-autotable` are recorded here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
This project uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html);
each Phase J wave corresponds to a minor bump on the 0.x line. Phase K
opens at 0.10.0 (J shipped ten waves; the version number tracks the
wave count).

The list below was reconstructed retroactively at Wave 8 from the
merged-PR history (`gh pr list --base main --state merged --json
number,title,mergedAt`) plus the project's wave-decision memos in
`.squad/decisions/`. Phase J Waves 4–10 were back-filled at Phase K
Wave 1 (the Wave 8 backfill stopped at J3). Pre–Phase F entries are
summarised; the `mahjong-autotable` engine started life as a fork of
`pwmarcz/autotable` and only the deltas relevant to the Changsha
rebuild are tracked here.

## [Unreleased]

Working branch: `stlong/phase-k-wave-1-bringup`. Phase K Wave 1 in
progress — supply-chain signing, nightly load-test cron, multi-arch
runtime smoke, CSP-strict-styles production rollout coordination,
secret-rotation runbook, retroactive CHANGELOG backfill, and a 0.10.0
version bump that catches the project up to the J-wave count.

### Added (Phase K Wave 1 — not yet merged)
- **cosign keyless image signing.** `.github/workflows/sign-image.yml`
    fires on `docker-build` workflow success on `main` (and on
    `v*.*.*` tag pushes). Uses GitHub OIDC as the keyless signing
    identity (`id-token: write`), resolves the manifest-list digest,
    signs via Sigstore Fulcio, records the signature in Rekor, and
    immediately verifies with `cosign verify --certificate-identity-regexp …`.
    Documented in `docs/image-signing.md` (full verification runbook
    for operators + auditors). (Apone)
- **Nightly load-test cron.** `.github/workflows/load-test-nightly.yml`
    runs daily at 02:00 UTC: brings up the production-shaped
    docker-compose stack, waits for `/health`, runs
    `tests/load/lobby-flood.js` via the new
    `tests/load/run-and-compare.sh` wrapper, appends a row to
    `docs/load-test-results-history.md`, and ALERTS (email +
    Sentry event) if any workload's p99 latency regresses by >25 %
    vs the prior recorded run. Threshold + duration tunable via
    workflow_dispatch inputs. (Apone)
- **Multi-arch runtime smoke.** `.github/workflows/multi-arch-smoke.yml`
    runs after `docker-build` succeeds on `main`. Matrix: `linux/amd64`
    natively + `linux/arm64` via QEMU. Per-arch smoke checks: `/health`
    200 with the four-field shape, `POST /api/identity` mints a
    cookie, `GET /api/auth/providers` registers (forward-compat
    soft-pass on 404), `POST /api/csp-report` returns 204, and the
    runtime CSP header honours `Security:CspStrictStyles=true`
    (no `'unsafe-inline'` in `style-src`). (Apone)
- **CSP-report endpoint smoke.** `tests/smoke/csp-report-smoke.sh`
    posts a synthetic violation in BOTH the legacy
    `application/csp-report` and modern `application/reports+json`
    envelopes and confirms DB persistence by tailing the runtime's
    structured `CSP violation` warn log line. (Apone)
- **Secret-rotation runbook.** `docs/secret-rotation.md` covering OAuth
    client secrets (Google + GitHub — quarterly), DB connection
    strings (annual), Sentry DSN (compromise-only), reconnect-token
    signing key + magic-link signing key (never — rotation invalidates
    all live sessions). Cross-references ESO/Vault/AWS-Secrets-Manager
    flows from Wave 5/6 docs. (Apone)

## [0.10.0] — Phase J Wave 10 — 2026-05-24 (PR #46)

**Theme:** Final-pass polish — flake fixes, CSP Round 2 canary knob,
production runbook, end-to-end load test, multi-arch Docker image,
docs review. Phase J's tenth-and-final wave; J ships green at
**820/0/0** backend tests, zero-skip streak preserved.

### Added
- **Multi-arch Docker image (`linux/amd64` + `linux/arm64`).** Wave 4
    carry-over closed. `.github/workflows/docker-build.yml` adds
    `docker/setup-qemu-action@v3`, `PLATFORMS: linux/amd64,linux/arm64`
    env, and passes `platforms:` through to `docker/build-push-action@v6`.
    Manifest list digest surfaced in the workflow summary. Docs:
    `docs/docker.md` Wave-10 multi-arch section; `docs/sbom.md`
    cross-reference. (Apone)
- **End-to-end load test harness.** `tests/load/lobby-flood.js` (Node
    + `ws@^8` — no k6 dep). Three workloads: 100-concurrent lobby
    polling, 25-concurrent WS join, 5-concurrent 4-bot tournaments.
    Smoke run results in `docs/load-test-results.md` (Lobby p99
    525 ms / Join p99 555 ms / Tournament p99 2,520 ms, 0 % error
    rate on Debug build). (Apone)
- **Production deployment runbook.** `docs/production-deployment-runbook.md`
    (~26 KB): pre-flight checklist, image build/publish, DB init via
    the pre-rollout k8s Job, rolling update + readiness gates,
    rollback procedure, monitoring/alerting (Prometheus + Sentry +
    JSON logs), incident response playbooks (DB outage, rate-limit
    storm, OAuth provider down, magic-link queue stall, CSP
    regression). (Apone)
- **Docs index.** `docs/README.md` — landing page mapping each
    operator/dev/QA need to the right doc. (Apone)
- **CSP Round 2 — `style-src 'unsafe-inline'` canary knob.**
    `Observability/SecurityHeadersMiddleware.cs` gains
    `Security:CspStrictStyles` (default OFF). When set, `style-src`
    drops `'unsafe-inline'` while adjacent directives are byte-for-byte
    preserved. Constants intentionally remain permissive (pinned by
    Vasquez's `CspStyleSrcNoUnsafeInlineTests` contract suite). (Apone)
- **Tournament mode.** Multi-table tournaments + bracket UI + per-table
    auto-bot fill at start. (Bishop)
- **Replay v2 normaliser.** Forward-compat schema upgrade for the
    `Replay` table; old single-game replays auto-migrate to the
    multi-hand envelope. (Bishop)
- **Audit-log pruning service.** Background hosted service deletes
    `AuditEntry` rows older than `Audit:RetentionDays` (default 90).
    Configurable per provider. (Bishop)
- **Bot decision reasoning surface.** Each bot's pickup/discard
    decision is surfaced via `/api/games/{id}/bot-reasoning` for
    spectator transparency. (Bishop)
- **Database health detail.** `GET /health/detail` surfaces per-provider
    DB pool stats + last-migration timestamp. (Bishop)
- **e2e Playwright multi-arch sanity test.** Stand-alone test ensures
    the multi-arch image runs to completion under QEMU. (Vasquez)

### Fixed
- **`LateJoin_ReceivesAccumulatedSnapshot_OfPriorUpdates` flake** —
    `AutotableConnectionManager.GetStoredEntryCount(gameId)` aggregated
    across all `kind`s; translator `match` + `seat:N` entries inflated
    the count before Alice's `UPDATE things` ever landed. Fix: new
    `AutotableGameState.CountFor(string kind)` per-kind probe + new
    `GetStoredEntryCount(gameId, kind)` overload + `WaitForAsync` now
    THROWS on deadline expiry instead of silent-falsing. Pinned by a
    50× regression-gate test. (Apone)

### Build invariant
Backend gate: **820 / 0 / 0**. Zero-skip streak: **13 consecutive
green waves**.

## [0.9.0] — Phase J Wave 9 — 2026-05-23 (PR #45)

**Theme:** Reconnect-token rotation + table chat + i18n pattern
resources + CSP tightening + audit log + flake fix.

### Added
- **Reconnect-token rotation.** Bishop's `/api/reconnect/*` surface
    issues a new opaque token on every WS reconnect, invalidates the
    previous one server-side, and rejects reuse attacks. Persisted as
    `ReconnectToken` + `ReconnectAuditEntry`. Smoke test:
    `tests/smoke/token-rotation-smoke.sh`. (Bishop; smoke: Apone)
- **Table chat.** `POST /api/chat/send` + `GET /api/games/{id}/chat`
    + SignalR hub event. Per-route rate limit; profanity filter
    (`ChatProfanityFilter`); persisted as `ChatMessage`. Smoke test:
    `tests/smoke/chat-flow-smoke.sh`. (Bishop; smoke: Apone)
- **i18n pattern resources.** Yaku names + UI strings extracted to
    `Resources/Strings.{en,zh-CN,ja}.resx`. Frontend resource-key
    pattern in `src/frontend/autotable-src/i18n/`. (Bishop / Hicks)
- **CSP tightening.** `SecurityHeadersMiddleware` gains four operator
    knobs (`Security:CspStrict`, `Security:UseScriptNonces`,
    `Security:CspReportOnly`, `Security:CspReportUri`). Defaults
    backwards-compatible. `POST /api/csp-report` sink (`Observability/
    CspReportEndpoint.cs`) accepts legacy + Reporting-API envelopes;
    persists to `CspViolations` table (per-provider EF migration). (Apone)
- **Audit log + pre-rollout k8s migration Job.** `--migrate` CLI
    intercept in `Program.cs` runs EF migrations from a one-shot k8s
    `Job` with Argo CD `sync-wave: -1` + `hook: PreSync`. (Apone)
- **SBOM + Trivy CRITICAL/HIGH gate.** `.github/workflows/sbom.yml`:
    CycloneDX + SPDX SBOMs via `anchore/sbom-action@v0`, Trivy gate
    with `severity: CRITICAL,HIGH` + `exit-code: 1` + `ignore-unfixed:
    true`, SARIF upload to GitHub code-scanning, PR-summary comment.
    Docs: `docs/sbom.md`. (Apone)

### Fixed
- **`HotSeatSwap_PlayerToPlayer_PreservesGameState` flake** —
    tightened `bobSeated` `WaitForAsync` predicate to wait for both
    Bob's seat take AND Alice's seat release before asserting, so the
    post-take `FillEmptySeatsWithBotsAsync` doesn't race the assertion.
    Pure test-side fix. (Apone)

### Build invariant
Backend gate: **728 / 1 / 0** (one Bishop-owned profanity-filter
in-flight; resolved at Wave 10).

## [0.8.0] — Phase J Wave 8 — 2026-05-22

**Theme:** Production hardening.

### Added
- **Sentry SDK (backend + frontend).** `Sentry.AspNetCore` 6.5.0 wired
    through `Observability/SentryConfiguration.cs`; SignalR hub-method
    breadcrumbs via `SentryHubFilter`. Disabled by default — set
    `Sentry__Dsn` to enable. Frontend equivalent via `src/sentry.ts`
    + `@sentry/browser` 8.x, gated on `<meta name="sentry-dsn">`. See
    `docs/sentry.md`. (Apone)
- **Security headers middleware.** `SecurityHeadersMiddleware` stamps
    `X-Frame-Options`, `X-Content-Type-Options`, `Referrer-Policy`,
    and a Three.js-compatible `Content-Security-Policy` on every
    response. Parcel-hashed bundles get
    `Cache-Control: public, max-age=31536000, immutable`; index.html
    gets `no-cache`. (Apone)
- **Cloudflare-aware rate limiting.** `RateLimiting/RateLimitingExtensions.cs`
    now prefers `CF-Connecting-IP` over `X-Forwarded-For` when present
    so the rate limiter partitions per real client behind Cloudflare.
    (Apone)
- **Release workflow** (`.github/workflows/release.yml`) — on every
    `v*.*.*` tag push: waits for the ghcr.io image, runs the build +
    auth smoke, then creates a GitHub Release with the matching
    CHANGELOG section or auto-generated notes. (Apone)
- **Auth-flow smoke test** (`tests/smoke/auth-flow-smoke.sh`) — mints a
    `mahjong_pid` cookie via `POST /api/identity`, asserts idempotent
    refresh, probes `/api/auth/providers` and `/api/auth/me` (skips
    gracefully if the surface isn't yet registered). Wired into
    `docker-smoke.yml` nightly. (Apone)
- **Parcel + npm BuildKit cache mounts** in the Dockerfile — `npm ci`
    re-uses `/root/.npm`, parcel re-uses `/src/frontend/autotable-src/.parcel-cache`.
    CI rebuilds with no source changes drop from ~90s to ~20s. (Apone)
- **External Secrets templates** for staging/prod
    (`infra/k8s/overlays/{staging,prod}/secret-template.yaml`) — ESO
    `ExternalSecret` CRDs pointed at AWS Secrets Manager. (Apone)
- **Local dev secret generator** (`scripts/generate-dev-secrets.sh` +
    `appsettings.Development.example.json`). Idempotent; emits a
    `.env.dev` with strong random JWT/cookie keys. (Apone)
- **Docs:** `docs/sentry.md`, `docs/cloudflare.md`,
    `docs/secret-management.md`. (Apone)
- **Auth surface (preview).** OAuth (Google, GitHub), magic-link, and
    dev-login under `/api/auth/*`, plus persistence migrations for
    Sqlite / Postgres / SqlServer. (Bishop)
- **Rule presets surface (preview).** `POST /api/rule-presets` etc.
    with backend validation + frontend rule-presets pane. (Bishop)

### Build invariant
Backend gate: ≥554 tests passing. Wave 8 expanded the suite to **617
green** with the observability surface; the auth/rule-preset surface
adds further pending tests that gate Bishop's parallel work.

## [0.7.0] — Phase J Wave 7 — 2026-05-21 (PR #43)

**Theme:** Replay endpoint, accessibility, settings drawer, multi-DB,
Kubernetes overlays.

- Replay endpoint (`GET /api/replays/{gameId}`) + viewer (Bishop)
- Accessibility audit + WCAG 2.1 AA fixes (Hudson)
- Settings drawer + theme switching (Hicks)
- Multi-database support (Sqlite / Postgres / SqlServer) via
    `Persistence__Provider` (Bishop)
- k8s base manifests + staging/prod overlays (Apone)
- See `.squad/decisions/inbox/apone-phase-j-wave-7.md` for the deploy
    memo.

## [0.6.0] — Phase J Wave 6 — 2026-05-20 (PR #42)

**Theme:** Persistent player IDs + leaderboard + rate limiting + auth
UI + Playwright specs.

- `mahjong_pid` cookie minted by `POST /api/identity` (Bishop)
- Per-player leaderboard (`GET /api/leaderboard/top`) (Bishop)
- ASP.NET rate limiter: fixed-window anonymous + token-bucket api
    (Apone)
- Auth-aware UI shell (sign-in / sign-out chrome) (Hicks)
- Playwright e2e harness + first specs (Vasquez)

## [0.5.0] — Phase J Wave 5 — 2026-05-19 (PR #41)

**Theme:** Multiplayer matchmaking, profiles, stats, observability,
Playwright E2E.

- Public matchmaking lobby + Quick Match (Hicks)
- Player profile + display name + avatar color (Bishop)
- Personal stats panel (Bishop)
- Prometheus `/metrics` exposition + JSON structured logging (Apone;
    see `docs/observability.md`)
- Playwright config + first cross-browser specs (Vasquez)
- Secret audit (`docs/secrets.md`)

## [0.4.0] — Phase J Wave 4 — 2026-05-19 (PR #40)

**Theme:** Mobile responsiveness, reconnect tokens, CI hardening,
seed 40595, GameComplete reconciliation.

- Responsive layout + touch input (Hicks)
- Rejoin-token URL parameter (`?rejoin=…`) + server-side validation
    (Bishop)
- GitHub Actions: docker-build.yml, docker-smoke.yml, e2e-playwright.yml
    (Apone)
- Hand-50 seed 40595 fully passes with all rule presets (Hudson)
- GameComplete event reconciles the move-log against the server
    snapshot (Bishop)

## [0.3.0] — Phase J Wave 3 — 2026-05-18 (PR #39)

**Theme:** Docker deployment, sound, replay (foundation), WinResult
surfaces, /health.

- Multi-stage Dockerfile (parcel + dotnet publish + aspnet:10.0
    runtime; UID 1000 non-root; `/data` volume) (Apone)
- `GET /health` 4-field probe + Docker `HEALTHCHECK` (Bishop)
- Sound effects pipeline (Hicks)
- WinResult panel + move-log groundwork (Bishop)
- Replay-event recording (Bishop)

## [0.2.0] — Phase J Wave 2 — 2026-05-17 (PR #38)

**Theme:** Disconnect cleanup, N-hand game completion, UX polish.

- Disconnect cleanup: idle seats freed (Bishop)
- N-hand games (configurable hand count) with proper end-of-game
    flow (Hudson)
- "Concede" + "Resign" interactions (Hicks)

## [0.1.0] — Phase J Wave 1 — 2026-05-16 (PR #37)

**Theme:** Shanten claim gate, hot-seat swap, spectator camera lock.

- Shanten gating on Pong / Chow / Kong claims (Hudson)
- Hot-seat swap mid-game (Bishop)
- Spectator camera lock-on-table (Hicks)

## Earlier (Phases A–I) — not version-tagged

Phases A through I shipped on `main` without semver tags. Highlights:

- **Phase I** (PRs #33–#36): special-context wins (天和/地和/海底/河底/杠上开花),
    proper shanten counter, spectator/all-bots-watch mode, multi-game
    WebSocket routing, persistence hydration, result-modal pattern
    breakdown.
- **Phase H** (PRs #31–#32): V2 rules — NineTerminals, RobbingKong,
    stacked Big Wins, V2 design groundwork.
- **Phase G** (PR #30): bot pickup scheduler, sidebar lobby,
    privacy-mask cleanup.
- **Phase F** (PR #29): Changsha realism — manual pickup, variant
    switching, 3-tier bot engine.
- **Phases A–E**: initial Changsha rebuild on top of the
    `pwmarcz/autotable` engine, scoring & yaku catalogue, swap-call
    discipline, gang/chi/pong/ron implementations.

[Unreleased]: https://github.com/long2know/mahjong-autotable/compare/v0.10.0...HEAD
[0.10.0]: https://github.com/long2know/mahjong-autotable/compare/v0.9.0...v0.10.0
[0.9.0]: https://github.com/long2know/mahjong-autotable/compare/v0.8.0...v0.9.0
[0.8.0]: https://github.com/long2know/mahjong-autotable/compare/v0.7.0...v0.8.0
[0.7.0]: https://github.com/long2know/mahjong-autotable/compare/v0.6.0...v0.7.0
[0.6.0]: https://github.com/long2know/mahjong-autotable/compare/v0.5.0...v0.6.0
[0.5.0]: https://github.com/long2know/mahjong-autotable/compare/v0.4.0...v0.5.0
[0.4.0]: https://github.com/long2know/mahjong-autotable/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/long2know/mahjong-autotable/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/long2know/mahjong-autotable/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/long2know/mahjong-autotable/releases/tag/v0.1.0
