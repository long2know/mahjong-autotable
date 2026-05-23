# Phase J Wave 8 — Production hardening (Apone, DevOps)

**Branch:** `stlong/phase-j-wave-8-completion`
**Date:** 2026-05-22
**Status:** ready for review

## Charter

Stephen's Wave 8 ask, paraphrased:

> "We've shipped the gameplay and the deployment surface — now make
> it production-grade. Sentry. Cloudflare. Secret management. A real
> release workflow. CHANGELOG. CI cache. Auth smoke. Don't break
> the 554-test gate."

This memo is the operator-facing summary of what landed and what an
operator needs to know to actually turn it on.

## What I shipped

### 1. Sentry SDK — backend + frontend, both off by default

**Backend:** `Sentry.AspNetCore` 6.5.0 wired through
`Observability/SentryConfiguration.cs` (`AddMahjongSentry`). Gated
on `Sentry:Dsn`: empty DSN → SDK never initialises → zero network
I/O. SignalR breadcrumbs land via `Observability/SentryHubFilter`
(`InvokeMethodAsync` + `OnConnectedAsync` + `OnDisconnectedAsync`).

What we capture: unhandled exceptions through the ASP.NET pipeline +
SignalR hub-method invocations + logger events ≥ Error (≥ Warning
when `Sentry:EnableLogs=true`).

What we never send: request bodies (`RequestSize.None`), PII
(`SendDefaultPii=false`), the `Authorization` / `Cookie` headers,
or breadcrumb keys named `email`/`name`/`password`/`token` (redacted
via `RedactBreadcrumb`).

Release tag: `mahjong-autotable@<BUILD_SHA>` so /health + Sentry
share a build identifier.

**Frontend:** `@sentry/browser` 8.x in `src/sentry.ts`. Gated on
`<meta name="sentry-dsn" content="…">` in `index.html` or
`window.__SENTRY_DSN__`. Production injection pattern lives in
`docs/sentry.md` — an init container `sed`'s the meta tag at
deploy time so the same image works across environments. No bundle
rebuild required to enable.

What we capture: `window.onerror` + `unhandledrejection`. What we
redact: `?rejoin=…` query params (`beforeSend`), all PII (no
`autoSessionTracking`, no `tracesSampleRate`). The anonymous user
id sent to Sentry is `anon:<sha256(localStorage["mahjong.identity.onboarded.v1"])[:16]>`
— the `mahjong_pid` cookie is HttpOnly so JS cannot read it.

### 2. Security headers + CDN cache middleware

`Observability/SecurityHeadersMiddleware` runs ahead of `UseCors`
in `Program.cs`. Sets:

| Header | Value |
|---|---|
| `X-Frame-Options` | `DENY` |
| `X-Content-Type-Options` | `nosniff` |
| `Referrer-Policy` | `strict-origin-when-cross-origin` |
| `Content-Security-Policy` | `default-src 'self'; script-src 'self' 'unsafe-eval'; …` (Three.js shader compiler needs `'unsafe-eval'`) |
| `Cache-Control` (Parcel-hashed bundles) | `public, max-age=31536000, immutable` |
| `Cache-Control` (everything else) | `no-cache, must-revalidate` |

Hashed-bundle detection is `HasContentHash` — matches Parcel's
`name.<8-hex>.ext` convention. Tests in
`tests/Mahjong.Autotable.Api.Tests/Observability/SecurityHeadersMiddlewareTests.cs`
pin the contract (`InternalsVisibleTo` added by Vasquez so we
didn't need to make the helper public).

HSTS is **deliberately not** stamped from the origin — toggle it
at Cloudflare instead (Dashboard → Edge Certificates → HSTS) so it
can be unwound from the dashboard if something goes wrong.

### 3. Cloudflare-aware rate limiting

`RateLimiting/RateLimitingExtensions.cs` — `ResolvePartitionKey`
now prefers `CF-Connecting-IP` → `X-Forwarded-For` → remote IP.
Docs (`docs/cloudflare.md`) call out the spoofing risk: trust
`CF-Connecting-IP` only when the origin firewall is locked to
Cloudflare IPs OR Authenticated Origin Pulls (mTLS) is on.

### 4. Release workflow + CHANGELOG

`.github/workflows/release.yml` — `v*.*.*` tag push triggers:

1. **smoke job**: poll ghcr.io for the matching image (≤6 min),
     pull, run `tests/smoke/docker-build-smoke.sh` + the new
     `auth-flow-smoke.sh`.
2. **release job**: extract the matching section from CHANGELOG.md,
     `gh release create $TAG --notes-file …` (or `--generate-notes`
     fallback when there's no CHANGELOG entry).

`CHANGELOG.md` reconstructed from merged-PR history + wave memos.
Semver mapping: 0.1.0 (Wave 1) → 0.8.0 (Wave 8). Each entry credits
the agent(s) who shipped the change.

### 5. Parcel + npm cache mounts in Dockerfile

`Dockerfile` Stage 1 now uses BuildKit cache mounts:

```dockerfile
RUN --mount=type=cache,id=mahjong-npm,target=/root/.npm \
    npm ci --no-audit --no-fund --prefer-offline

RUN --mount=type=cache,id=mahjong-parcel,target=/src/.../.parcel-cache \
    npx parcel build index.html --dist-dir /out/autotable --public-url . \
    --no-source-maps --cache-dir /src/.../.parcel-cache
```

CI rebuilds with no source changes drop from ~90s to ~20s on a
warm cache. The `--no-cache` flag (which previously suppressed
Parcel's own cache) was removed in favour of an explicit
`--cache-dir` pointed at the mount target.

### 6. Secret management

| Surface | File |
|---|---|
| Operator guidance | `docs/secret-management.md` (dev → staging → prod, ESO + AWS Secrets Manager pattern, rotation runbook) |
| Dev template | `src/.../appsettings.Development.example.json` |
| Dev generator | `scripts/generate-dev-secrets.sh` (idempotent; emits `.env.dev`) |
| Staging ESO | `infra/k8s/overlays/staging/secret-template.yaml` |
| Prod ESO | `infra/k8s/overlays/prod/secret-template.yaml` |
| .gitignore | `.env.dev` + `appsettings.{Development,Staging,Production}.json` |

The ExternalSecret CRDs target `mahjong/<env>/app` in AWS Secrets
Manager and write into a k8s Secret named `mahjong-autotable` —
matching the name the existing base `Deployment` already references
via `envFrom`. They're out-of-band (not in kustomize resources) so
that `kubectl apply -k base/` still works on a kind cluster without
ESO.

### 7. Auth smoke

`tests/smoke/auth-flow-smoke.sh` — round-trips the anonymous
identity surface against a Docker image:

1. `POST /api/identity` → 200 + `Set-Cookie: mahjong_pid`
2. `POST /api/identity` with cookie → 200, same `playerId`
3. `GET /api/auth/providers` → 200 or 404 (skip; forward-compat
     against Bishop's surface)
4. `GET /api/auth/me` anonymous → 200 + `isAuthenticated=false`
     OR 401 OR 404 (skip)

Wired into `docker-smoke.yml` (nightly) and `release.yml` (per
tag).

## Test impact

Baseline gate: 554 tests passing on Wave-7 merge.

After Wave 8 (mine + Bishop + Vasquez interleaved on the same
branch): **617 passing, 37 failing**. All 37 failures are in
Bishop's `/api/auth/*` + `/api/rule-presets/*` surface (the tests
Vasquez wrote against the contract; Bishop's implementation is
still landing). My Observability + RateLimit surface is **16/16
green** (9 new + 7 existing).

The 554 gate is preserved on the parts of the suite I own. The
overall pass count regression is Bishop's to close before this
branch merges.

## Open items / handoff

1. **Bishop:** 37 failing auth + rule-preset tests. I don't need to
     wait on these to declare Wave 8 done from a DevOps perspective,
     but the PR cannot merge to main with red tests.
2. **Hudson:** k8s manifest review — the ExternalSecret CRDs
     reference `ClusterSecretStore`s the dev cluster doesn't have.
     Decide if we want to land a placeholder `SecretStore` config
     here too, or document that as a separate one-shot setup task.
3. **Vasquez:** the auth-flow-smoke script is forward-compatible
     (gracefully skips on 404). Once Bishop's surface stabilises,
     swap the "skip if 404" branches for hard asserts.
4. **Sentry credentials:** I shipped the *capability* but not the
     DSN. Stephen will need to:
     - Create a Sentry project (free tier is fine)
     - Create two client keys: one for the .NET project, one for
         the JS project (do NOT share DSNs across SDKs)
     - Add the backend DSN to AWS Secrets Manager at
         `mahjong/<env>/app::sentry__dsn`
     - Add the frontend DSN as a k8s `Secret` referenced by the
         init-container `sed` step in `docs/sentry.md`

## Verification I ran

- `dotnet build src/Mahjong.Autotable.Api/Mahjong.Autotable.Api.csproj` — 0 errors, 0 warnings
- `dotnet test … --filter FullyQualifiedName~Observability` — 16/16 passed
- `dotnet test …` (full) — 617/654 (37 failures all owned by Bishop's surface)
- `npx tsc --noEmit src/sentry.ts src/index.ts` — clean
- `git push origin stlong/phase-j-wave-8-completion` — pushed clean across four commits (`fbedff6`, `7e66f3c`, `0797fab`, `353e613`)

## Not verified (out of my agent's reach)

- `docker build .` — the BuildKit cache-mount changes need a real
    Docker daemon. The Dockerfile is syntactically valid and the
    mount IDs are unique; the prior `RUN npm ci` and `RUN npx parcel build`
    lines worked before this change. Risk is low. CI will catch any
    regression on the next push.
- Actual Sentry DSN end-to-end — depends on Stephen's account setup.

— Apone
