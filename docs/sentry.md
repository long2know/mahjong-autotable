# Sentry (error reporting)

Phase J Wave 8 (Apone, DevOps) wires Sentry into both the .NET backend
(`Sentry.AspNetCore` 6.5.0) and the parcel-bundled frontend
(`@sentry/browser` 8.x). This document is the operator-facing contract:
**what we send, what we never send, and how to turn it on**.

## TL;DR

- Sentry is **off by default** in every environment, including
  production images.
- Set the `Sentry__Dsn` env var (or `sentry-dsn` `<meta>` tag in
  index.html) to a non-empty value to enable it.
- With no DSN, `SentrySdk.Init` is never called and `@sentry/browser`
  is never started; **no network I/O is performed**.
- The xUnit harness boots Sentry-disabled (Development + empty DSN),
  so unit tests cannot accidentally send events to a real Sentry
  project.

## Where Sentry runs

| Process | SDK | Init site | Toggle |
|---|---|---|---|
| `Mahjong.Autotable.Api` (ASP.NET Core) | `Sentry.AspNetCore` 6.5.0 | `Observability/SentryConfiguration.cs` (`AddMahjongSentry`) | `Sentry:Dsn` config key |
| Parcel SPA (browser) | `@sentry/browser` 8.x | `src/sentry.ts` (`initSentry`) | `<meta name="sentry-dsn">` in `index.html` |

## Backend configuration

`appsettings.json` block (canonical, with safe defaults):

```jsonc
"Sentry": {
    "Dsn": "",                  // empty = SDK disabled
    "Environment": null,         // null = inherits ASPNETCORE_ENVIRONMENT
    "SampleRate": 1.0,           // fraction of error events sent (0.0..1.0)
    "TracesSampleRate": 0.0,     // performance tracing — OFF by default
    "EnableLogs": false          // when true, ILogger Warning+ becomes Sentry events
}
```

Environment variable overrides follow ASP.NET's standard double-underscore
syntax:

```bash
Sentry__Dsn=https://abc123@o12345.ingest.sentry.io/67890
Sentry__Environment=staging
Sentry__SampleRate=0.5
```

### What the backend captures

- **Unhandled exceptions** in the ASP.NET pipeline (middleware,
  controllers, endpoints) via the auto-installed
  `SentryTracingMiddleware`.
- **SignalR hub method invocations** (`Mahjong.Autotable.Api.Observability.SentryHubFilter`):
  one breadcrumb per `InvokeMethodAsync`, plus
  `OnConnectedAsync` / `OnDisconnectedAsync` lifecycle events. Hub
  exceptions are re-thrown to the framework after being captured with
  `signalr.hub` + `signalr.method` tags.
- **Logger events at Error+** (always) and at **Warning+** when
  `Sentry:EnableLogs` is true. Breadcrumbs run at Information+.
- **`BUILD_SHA`** is sent as the `release` tag in the form
  `mahjong-autotable@<sha>` so deploys can be correlated against the
  ghcr.io image set.

### What the backend redacts / never captures

- **Request bodies.** `options.MaxRequestBodySize = RequestSize.None`.
  Even on a 500 we do not send the request payload — the SignalR
  hub messages can carry hand state and would balloon the event size
  without operational value.
- **PII.** `options.SendDefaultPii = false`. We do NOT send remote IP,
  cookies, or the `Authorization` header.
- **Breadcrumb data.** `RedactBreadcrumb` strips the keys `email`,
  `name`, `password`, `token`, and any cookie/header value before
  the breadcrumb is queued.

## Frontend configuration

The browser SDK looks for the DSN in three places, in this order:

1. `window.__SENTRY_DSN__` (set by an inline `<script>` before the
   bundle loads — useful for e2e harnesses).
2. `<meta name="sentry-dsn" content="…">` (the production
   injection point — see "Deploying" below).
3. *(none)* → SDK never initialises.

`<meta name="sentry-environment">` and `<meta name="sentry-release">`
can be set the same way.

### What the frontend captures

- Uncaught exceptions (`window.onerror`)
- Unhandled promise rejections (`unhandledrejection`)
- Manual breadcrumbs and events via the `recordSentryBreadcrumb` /
  `captureSentryError` helpers exported from `src/sentry.ts`.

### What the frontend redacts / never captures

- `sendDefaultPii: false` — Sentry never auto-attaches the user
  agent or the remote IP.
- `tracesSampleRate: 0` — no performance spans, no transaction
  payloads.
- `autoSessionTracking: false` — no `session.health` beacons.
- `beforeSend` strips `?rejoin=<token>` query params off any URL in
  the event payload.
- The `mahjong_pid` cookie is HttpOnly so JavaScript cannot read it.
  Sentry events instead carry
  `user.id = "anon:" + sha256(localStorage["mahjong.identity.onboarded.v1"])[:16]`
  when the onboarding flag is set, or no user at all.

## Deploying with Sentry enabled

### Backend

```bash
docker run -d \
    -e Sentry__Dsn="https://…@o0.ingest.sentry.io/0" \
    -e Sentry__Environment=production \
    -e Sentry__SampleRate=0.25 \
    -e BUILD_SHA="$(git rev-parse HEAD)" \
    ghcr.io/long2know/mahjong-autotable:vX.Y.Z
```

### Frontend

Inject the DSN at deploy time without rebuilding the bundle. The
simplest pattern is a `sed` step on the index.html in the running
container:

```bash
docker exec mahjong sed -i 's|name="sentry-dsn" content=""|name="sentry-dsn" content="https://abc@…"|' \
    /app/wwwroot/index.html
```

A Kubernetes-friendly pattern uses an init container with `envsubst`:

```yaml
initContainers:
- name: inject-sentry
    image: ghcr.io/long2know/mahjong-autotable:vX.Y.Z
    command: ["/bin/sh","-c"]
    args:
    - |
        sed -i "s|name=\"sentry-dsn\" content=\"\"|name=\"sentry-dsn\" content=\"$SENTRY_DSN_FRONTEND\"|" \
            /app/wwwroot/index.html
    env:
    - name: SENTRY_DSN_FRONTEND
        valueFrom:
            secretKeyRef:
                name: mahjong-sentry
                key: dsn-frontend
    volumeMounts:
    - name: wwwroot
        mountPath: /app/wwwroot
```

> **Use a separate DSN for the frontend project.** Public DSNs are
> visible in the HTML payload by design — Sentry's per-project rate
> limits and key scoping are how you bound exposure. **Do not reuse
> the backend DSN on the frontend.**

## Sampling guidance

| Environment | `SampleRate` (errors) | `TracesSampleRate` |
|---|---|---|
| Local / dev | DSN off (no events at all) | n/a |
| Staging | 1.0 | 0.0 |
| Production | 0.25–1.0 (start at 1.0, tighten on quota) | 0.0 (until we need it) |

Performance tracing is intentionally off — the SignalR hub's latency
budget is tight enough that wrapping every hub method in a span
adds noticeable jitter on low-end Android devices. Re-evaluate when
Sentry adds streaming traces.

## Disabling Sentry in tests

The xUnit harness (`WebApplicationFactory<Program>`) inherits the
checked-in `appsettings.json`, which ships with `Sentry:Dsn = ""`. This
keeps every integration test Sentry-free without any test-specific
override. **Do not** set `Sentry__Dsn` in `appsettings.Test.json` or
in any test fixture — the empty-DSN contract is what
`SentryConfigurationApiTests.AddMahjongSentry_DsnUnset_ReturnsFalse`
pins.

## Operational runbook

- **Quota exhausted?** Drop `Sentry__SampleRate` to a smaller value
  (e.g. 0.1) and roll the deployment. No code change needed.
- **A noisy exception is flooding Sentry?** Add it to the
  `BeforeSendFilter` chain in `SentryConfiguration.cs` and ship a
  fresh build. Inline filters in the Sentry dashboard are also fine
  for short-term suppression.
- **No events arriving?** Check `GET /health?simple=0` — the
  `sentry_enabled` field is `true` iff `SentrySdk.IsEnabled` returned
  true at startup. (Future Wave; not yet on the health payload.)
- **Need to test a DSN locally?** `dotnet run --launch-profile https`
  with `ASPNETCORE_ENVIRONMENT=Staging` and your DSN as an env var.
  Throw an uncaught exception from `/api/health?fail=1` (debug-only
  endpoint).

## See also

- `docs/observability.md` — `/health` + `/metrics`, JSON logging
- `docs/cloudflare.md` — CDN cache + security headers
- `docs/secret-management.md` — Sentry DSN handling in k8s / External Secrets
