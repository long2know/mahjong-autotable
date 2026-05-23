# Changelog

All notable changes to `mahjong-autotable` are recorded here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
This project uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html);
each Phase J wave corresponds to a minor bump on the 0.x line.

The list below was reconstructed retroactively at Wave 8 from the
merged-PR history (`gh pr list --base main --state merged --json
number,title,mergedAt`) plus the project's wave-decision memos in
`.squad/decisions/`. Pre–Phase F entries are summarised; the
`mahjong-autotable` engine started life as a fork of `pwmarcz/autotable`
and only the deltas relevant to the Changsha rebuild are tracked here.

## [Unreleased]

Working branch: `stlong/phase-j-wave-8-completion`. Nothing here has
shipped yet.

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

[Unreleased]: https://github.com/long2know/mahjong-autotable/compare/v0.8.0...HEAD
[0.8.0]: https://github.com/long2know/mahjong-autotable/compare/v0.7.0...v0.8.0
[0.7.0]: https://github.com/long2know/mahjong-autotable/compare/v0.6.0...v0.7.0
[0.6.0]: https://github.com/long2know/mahjong-autotable/compare/v0.5.0...v0.6.0
[0.5.0]: https://github.com/long2know/mahjong-autotable/compare/v0.4.0...v0.5.0
[0.4.0]: https://github.com/long2know/mahjong-autotable/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/long2know/mahjong-autotable/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/long2know/mahjong-autotable/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/long2know/mahjong-autotable/releases/tag/v0.1.0
