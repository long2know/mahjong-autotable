# Apone — Phase K Wave 2 memo

**Branch:** `stlong/phase-k-wave-2-bringup`
**Date:** 2026-05-25
**Author:** Apone (DevOps / Platform Engineer)
**Scope:** PR-time multi-arch runtime gate, TURN/STUN k8s scaffolding,
Capacitor mobile shell, PWA service-worker smoke, OAuth production
docs (Google + GitHub + Microsoft for Wave-3), cosign verify reusable
workflow wired into `release.yml` as a pre-publish gate, CHANGELOG
bump to **0.11.0**.

**Test gate:** `dotnet test src/backend/Mahjong.Autotable.slnx --nologo`
→ **832 / 0 / 0** (baseline preserved — Wave-2 scope is pure DevOps +
docs + infra, no `src/backend/**` touched).

---

## What shipped

### Task 1 — Multi-arch live `arm64 curl /health` PR gate

**New workflow** `.github/workflows/multi-arch-runtime.yml`.

Wave 1's `multi-arch-smoke.yml` runs on `workflow_run` after
`docker-build` succeeds on `main` — i.e., POST-merge. That left a PR-
time blind spot: a backend change that breaks arm64 only surfaced AFTER
merge. Wave 2 closes the gap with a PR-triggered runtime gate.

- **Triggers:** PR (paths-filtered to `Dockerfile`, `src/backend/**`,
  `src/frontend/autotable-src/**`, the workflow itself), `push` on
  `main`, `workflow_dispatch`.
- **Matrix:** `linux/amd64` (boot ≤60 s, native) +
  `linux/arm64` (boot ≤300 s, QEMU via `docker/setup-qemu-action@v3`).
- **Build path:** `docker buildx build --output type=docker` for each
  per-arch image into the local Docker daemon, then `docker run` +
  `curl http://localhost:<host_port>/health`. Per-matrix host ports
  (18091/18092) avoid collision across the matrix.
- **Assertions:** HTTP 200 + JSON body must contain
  `"status":"healthy"` (the Wave-1 detail-shape's `status` field —
  Program.cs Phase J Wave 7 contract).
- **Sticky PR comment:** `marocchino/sticky-pull-request-comment@v2`
  with header `multi-arch-runtime` posts a markdown matrix table.
  Reviewers see verdicts without clicking into the Actions tab. The
  `report` job downloads per-arch artefacts so the comment carries
  per-arch boot time + verdict.
- **Concurrency:** group `multi-arch-runtime-<ref>`,
  `cancel-in-progress: true` so PR pushes preempt the prior run.

**Why a NEW workflow rather than extending `multi-arch-smoke.yml`?**

`multi-arch-smoke.yml` is `workflow_run`-triggered (post-merge) and
runs against the PUBLISHED image (`ghcr.io/.../latest`). PR runs need
to build LOCALLY against the PR's commit — different image source,
different trigger shape. Combining them in one file would muddy both.
Two workflows + clear scopes is the right factoring.

### Task 2 — TURN server overlay (stubbed for Phase L bringup)

**New files:**

- `infra/k8s/base/turn-server.yaml` — coturn 4.6 Deployment + ConfigMap
  (`turnserver.conf`) + LoadBalancer Service (UDP/TCP 3478 + TLS 5349)
  + `turn-server-secrets` ExternalSecret stub.
- `infra/k8s/overlays/prod/turn-server-patch.yaml` — repoints the
  ExternalSecret at `aws-secrets-manager-prod` ClusterSecretStore +
  `/mahjong/prod/turn/*` SSM key family + ups resource limits.
- `infra/k8s/overlays/prod/turnserver-prod.conf` — production
  turnserver.conf with `realm=turn.mahjong.example.com` +
  `external-ip=REPLACE_WITH_LB_PUBLIC_IP` (operator action).
- `infra/k8s/overlays/staging/turn-server-patch.yaml` — same shape,
  staging targets.
- `infra/k8s/overlays/staging/turnserver-staging.conf` — staging conf.
- `docs/turn-server-setup.md` — operator runbook covering SSM key
  provisioning, IAM scope, DNS record, TLS cert (Phase L follow-up),
  HMAC time-limited credential migration path (Wave 3 — Bishop flips
  `lt-cred-mech` → `use-auth-secret` once `/api/turn` mints tokens),
  default ICE-server URLs `GET /api/turn` should return, rotation
  cadence (quarterly with two-value overlap).

**DevOps lane discipline:** I did NOT provision any production secrets.
The `turn-server-secrets` ExternalSecret references `/mahjong/prod/turn/{realm,username,password}`
which are operator-pre-provisioned per `docs/turn-server-setup.md`
§"Provisioning checklist (operator)". The base manifest ships with a
STUB ClusterSecretStore reference (`aws-secrets-manager-local`) so an
accidental `kubectl apply -k base/` fails fast instead of provisioning
a broken-credential TURN server.

**Network shape locked-in for Wave 3 + Phase L:**

- 3478/udp + 3478/tcp — TURN control channel
- 5349/tcp — TURN over TLS (cert provisioning Phase L)
- 49160-49200/udp — TURN relay range, matches `min-port`/`max-port`
- `externalTrafficPolicy: Local` on the Service preserves client
  source IP (coturn needs it for XOR-RELAYED-ADDRESS minting)
- LoadBalancer type; operator pins `external-ip` to the LB public IP
  AFTER first apply

### Task 3 — Capacitor mobile shell scaffolding

**New files:**

- `mobile/package.json` — Capacitor 6.1.x deps (`@capacitor/core`,
  `@capacitor/cli`, `@capacitor/ios`, `@capacitor/android`). Scripts
  for `sync`, `open:ios`, `open:android`, `build:ios`, `build:android`.
- `mobile/capacitor.config.json` — `appId: io.mahjong.autotable`,
  `webDir: ../src/frontend/autotable` (Parcel bundle output dir), iOS
  + Android platform options.
- `mobile/README.md` — full operator runbook: prereqs (macOS +
  Xcode 15+ for iOS, JDK 17 + Android SDK for Android), first-time
  setup (`npx cap add ios/android`), day-to-day workflow, production
  build commands, **signing** (iOS distribution cert + provisioning
  profile, Android keystore generation + 1Password storage),
  TestFlight + Play Internal upload procedure.
- `.gitignore` — excludes `mobile/ios/`, `mobile/android/`,
  `mobile/node_modules/`, `mobile/build/`, `mobile/.gradle/`,
  `mobile/*.tgz` (all generated by `npx cap add` / Gradle / xcodebuild).

**New workflow:** `.github/workflows/mobile-build.yml`

- **Triggers:** push on `main` (paths-filtered to `mobile/**` +
  frontend bundle source + workflow file), `workflow_dispatch`.
- **Jobs:**
  - `build-frontend-bundle` (ubuntu-latest) — Parcel-builds the web
    bundle once, uploads as `autotable-bundle` artefact. Both
    platform jobs consume it (no duplicate npm install in iOS job).
  - `android` (ubuntu-latest) — Java 17 + `npm ci` in `mobile/` +
    `npx cap add android` + `gradlew assembleRelease bundleRelease`.
    Signs IFF `ANDROID_KEYSTORE_BASE64` secret is present (base64
    decode → JKS file → gradle picks up via env). Uploads APK + AAB
    as `mahjong-android-<run_number>` artefact.
  - `ios` (macos-latest) — CocoaPods + `npx cap add ios` +
    `xcodebuild -workspace App.xcworkspace -scheme App
    -configuration Release -sdk iphoneos CODE_SIGNING_ALLOWED=NO`.
    Packages `.app` into a tarball, uploads as
    `mahjong-ios-<run_number>` artefact.
  - `release` (ubuntu-latest, needs both) — downloads all
    `mahjong-*` artefacts, creates a `prerelease` GitHub Release
    tagged `mobile-<run_number>` with the artefacts attached. App
    Store / Play Store submission is **manual operator action** in
    Phase K; auto-promotion is Phase L.

**Why scaffolding only?** The platform dirs (`mobile/ios/`,
`mobile/android/`) are GENERATED by `npx cap add` and gitignored
because their content is mechanically reproducible from
`package.json` + `capacitor.config.json`. CI runs the `add` step
fresh on every build, which keeps the repo lean and avoids Xcode-
project-file merge conflicts.

### Task 4 — PWA service-worker CI verification

**New files:**

- `tests/smoke/pwa-smoke.js` — Playwright (chromium-only)
  Node script. Resolves the driver from
  `src/frontend/autotable-src/node_modules/playwright` (which Hicks
  already installs for E2E — no new dep tree). Probe:
  1. `GET /` → expect 200.
  2. `GET /sw.js` → if 404 **soft-pass** (forward-compat: Hicks's
     SW artefact is in-flight on a separate lane). If 200, assert
     `content-type: */javascript`.
  3. Wait for `navigator.serviceWorker.getRegistration()` to yield
     an active worker.
  4. `page.reload()` and assert `navigator.serviceWorker.controller`
     is non-null (the canonical "the SW took control" assertion —
     requires the second nav because controller hand-off is async).
- `tests/smoke/pwa-smoke.sh` — bash wrapper. Boots the production
  image on port 18093 (extends the unique-port series: docker-build
  =18080, auth=18081, chat=18082, token-rotation=18083, csp-report
  =18084, **pwa=18093**), waits for `/health`, installs the
  Playwright Chromium binary if missing (`npx playwright install
  --with-deps chromium`), invokes the JS probe.

**New workflow:** `.github/workflows/pwa-smoke.yml`

- Triggers: push on `main`, PR, `workflow_dispatch` — paths-filtered
  to `src/frontend/autotable-src/src/{pwa,sw}.{ts,js}`,
  `tests/smoke/pwa-smoke.{sh,js}`, the workflow itself, `Dockerfile`.
- Builds the production image, runs `pwa-smoke.sh` against it. Dumps
  container logs on failure.

**Forward-compat note:** the smoke is **soft-pass on `/sw.js` 404**
specifically because Hicks's `sw.js` artefact has not yet shipped
through the bundle pipeline. Once that lands, the smoke auto-tightens
to a hard assertion (controller MUST be non-null). Same pattern as
the auth-flow-smoke.sh forward-compat probes.

### Task 5 — OAuth production secret docs (Google + GitHub + Microsoft)

**New file:** `docs/oauth-production-setup.md`

Operator-facing runbook (NOT executable code — DevOps does NOT touch
production secrets). Covers:

- **Contract summary table** mapping each provider to its SSM family
  + env-var names the API binds to.
- **Google** — Google Cloud Console → APIs & Services → Credentials
  → OAuth 2.0 Client ID. Redirect URI
  `https://<domain>/api/auth/callback/google`, scopes
  `openid email profile`. SSM keys `/mahjong/prod/oauth/google/{client_id,client_secret}`.
- **GitHub** — github.com/settings/applications/new. Redirect URI
  `https://<domain>/api/auth/callback/github`, scopes
  `read:user user:email`. SSM keys `/mahjong/prod/oauth/github/{client_id,client_secret}`.
- **Microsoft (NEW this wave)** — portal.azure.com → AAD → App
  registrations → New registration. Multi-tenant, redirect URI
  `https://<domain>/api/auth/callback/microsoft`, scopes
  `openid email profile`. SSM keys
  `/mahjong/prod/oauth/microsoft/{client_id,client_secret,tenant_id}`.
  `tenant_id=common` for the public-facing app (lets work + school
  + personal MSA accounts all sign in).
- **Rotation cadence** for all three — quarterly, two-value overlap
  procedure per `docs/secret-rotation.md`. Google + Microsoft require
  provisioning a SECOND client/secret for the overlap; GitHub
  supports multiple active secrets on a single OAuth App with a
  30-day grace window.
- **Validation checklist** — 4-step post-rotation gate that catches
  the three most common failure modes (wrong SSM key, ESO refresh
  not yet triggered, backend not restarted).
- **Microsoft known quirks** — `oid` claim is the stable primary key
  (not `email`), `tid=9188040d-...` distinguishes personal accounts,
  `email` scope is required for the `mail` claim on consumer
  accounts.

Bishop will add Microsoft provider middleware in Wave 3 — my docs
unblock that.

### Task 6 — Cosign verify reusable workflow + release.yml gate

**New workflow:** `.github/workflows/verify-signature.yml`

- `workflow_call` interface — inputs `image-digest` (required),
  `expected-issuer` (default `https://token.actions.githubusercontent.com`),
  `expected-identity-pattern` (default this repo's `sign-image.yml`
  at `refs/heads/main` or `refs/tags/v.*`), `cosign-version` (default
  v2.4.1 — kept in lock-step with `sign-image.yml`).
- Validates the digest shape (`@sha256:<64-hex>` regex), installs
  cosign, logs into GHCR (`packages: read`), runs
  `cosign verify --certificate-identity-regexp …
  --certificate-oidc-issuer …`, exposes `verified: true|false` as a
  workflow output.
- Fails red on any of: missing signature, mismatched identity,
  mismatched issuer, Rekor entry mismatch — same gate shape that
  `sign-image.yml`'s post-sign verify uses.

**Wired into `release.yml` as a pre-publish gate:**

- `smoke` job now resolves the manifest-list digest via
  `docker buildx imagetools inspect --format '{{.Manifest.Digest}}'`
  and exposes it as the `image-digest` output.
- New `verify-signature` job invokes
  `./.github/workflows/verify-signature.yml` with
  `image-digest: ${{ needs.smoke.outputs.image-digest }}`.
- `release` job's `needs:` list now requires `[smoke,
  verify-signature]` — the GitHub Release is NOT created when the
  signature gate fails.

**Why a separate reusable workflow rather than copy-pasting the verify
step?** Two reasons:

1. **Single source of truth for the expected-identity regex.** Rename
   `sign-image.yml` once, change ONE consumer. Today only `release.yml`
   consumes the reusable; tomorrow Argo CD pre-sync gates + Kyverno
   policy controllers will.
2. **Centralised cosign version pinning.** When cosign 3.x lands, we
   bump the default in `verify-signature.yml` once and every caller
   picks up the new version on the next run.

### Task 7 — CHANGELOG bump to 0.11.0

`CHANGELOG.md` Wave 1's [Unreleased] section (PR #47 just merged)
was rolled into the new **[0.11.0] — Phase K Waves 1 + 2 — 2026-05-25
(PRs #47 + #48)** section. Both waves share the release tag because
Wave 1 was a bringup wave that did not advance the version cursor
(per the preamble's "Phase K opens at 0.10.0" convention).

- [Unreleased] header reset → points at "Phase K Wave 3 not yet
  started"; explains the K Wave 1 + Wave 2 → 0.11.0 consolidation.
- Compare-link footnotes updated:
  - `[Unreleased]: …v0.11.0...HEAD`
  - `[0.11.0]: …v0.10.0...v0.11.0`

---

## Patterns locked for future DevOps work on this codebase

- **PR-time multi-arch runtime gate.** Even when post-merge smokes
  exist, a PR-time runtime gate catches arch-specific breakage BEFORE
  merge. The `multi-arch-runtime.yml` / `multi-arch-smoke.yml` split
  (PR-time local-build vs. post-merge published-image) is the right
  factoring; combining them in one workflow muddies both.
- **Sticky PR comment for matrix verdicts.** Reviewers don't
  habitually click into the Actions tab. `marocchino/sticky-pull-request-comment@v2`
  with a stable `header:` makes the matrix result visible under the
  PR conversation. Use this pattern for any multi-arch / multi-target
  CI gate that needs reviewer attention.
- **TURN/STUN stub layout.** Base manifest ships with deliberately-
  broken stub credentials (`mahjong/local/turn/*` SSM family that
  does not exist) so `kubectl apply -k base/` against a real cluster
  fails FAST instead of provisioning a working-but-leaky TURN server.
  Overlays must always be applied with the stub. Same pattern as the
  Wave-8 `secret-template.yaml` stub.
- **Capacitor scaffolding without committed platform dirs.** `mobile/ios/`
  + `mobile/android/` are gitignored; CI runs `npx cap add` fresh
  every build. Keeps the repo lean + avoids Xcode-project-file merge
  conflicts. The only "stable" mobile state in git is
  `package.json` + `capacitor.config.json` + the wrapper README.
- **Reusable cosign verify workflow.** ONE source of truth for the
  expected-identity regex + cosign version. Callers (release.yml,
  Argo CD pre-sync, Kyverno admission) all dial in via
  `workflow_call`. Renaming `sign-image.yml` later changes one
  consumer.
- **Pre-publish signature gate.** `release.yml` now refuses to cut a
  GitHub Release for an unsigned image. The cluster-layer enforcement
  (Kyverno / Cosign policy-controller) is the next step — once that
  ships, end-to-end "no unsigned images in production" is enforced
  at the admission layer too.
- **PWA forward-compat soft-pass on `/sw.js` 404.** Same shape as
  the auth-flow-smoke / provider forward-compat probes. Soft-pass
  prevents the gate from blocking PRs while in-flight work catches
  up; hard-pass auto-engages when the surface ships.
- **Unique smoke port allocation.** docker-build=18080, auth=18081,
  chat=18082, token-rotation=18083, csp-report=18084, multi-arch-
  runtime(amd64)=18091 / (arm64)=18092, **pwa=18093**. Allocate the
  NEXT free port for any new smoke; document in the wrapper header.
- **Multi-platform Docker into local daemon.** `docker buildx build
  --output type=docker -t …` produces a single-platform image in
  the daemon (cf. `--load` which fails for multi-platform builds).
  Pair with `docker/setup-qemu-action@v3` for arm64 emulation on an
  amd64 GitHub runner.

---

## Cross-lane handoffs

**For Bishop (Wave 3):**
1. `/api/turn` endpoint — return the default ICE server list
   documented in `docs/turn-server-setup.md` §"Default ICE-server
   list". HMAC time-limited credentials preferred; flip coturn from
   `lt-cred-mech` → `use-auth-secret` in the overlay's
   turnserver-prod.conf once the surface mints tokens.
2. Microsoft OAuth provider middleware — bind
   `Authentication__Microsoft__{ClientId,ClientSecret,TenantId}` env
   vars; the operator's SSM key family is documented in
   `docs/oauth-production-setup.md` §3.
3. `MicrosoftAuthenticationOptions.cs` (Wave 3 file) needs to support
   `tenant_id=common` (multi-tenant + MSA). The `oid` claim is the
   stable primary key, NOT `email`.
4. Wave-3 Microsoft provider middleware should extend the
   `oauth-secrets` ExternalSecret in
   `infra/k8s/overlays/prod/secret-template.yaml` with three new
   `data:` entries (client_id, client_secret, tenant_id). Same shape
   as Google + GitHub.

**For Hicks (any wave):**
1. `sw.js` artefact — when it ships through the Parcel pipeline, the
   PWA smoke auto-tightens. No CI change needed.
2. Capacitor `isNativePlatform()` adaptation hooks — `mobile/README.md`
   notes the bundle should detect mobile via Capacitor's `Capacitor.isNativePlatform()`
   if any UI changes are needed for the wrapped shell.

**For Vasquez (any wave):**
1. The new PWA smoke is a separate workflow from the E2E Playwright
   suite. They share the chromium driver install path but should not
   share the same browser context (the smoke explicitly uses
   `serviceWorkers: 'allow'` to exercise the SW lifecycle).
2. Mobile build workflow is separate from the main CI — failures do
   NOT gate `main`. Operator-only deliverable.

**For Operator (Stephen):**
1. **Phase L pre-bringup:** provision SSM keys before applying the
   TURN overlays — see `docs/turn-server-setup.md` §"Provisioning
   checklist". Same shape for Microsoft OAuth before Bishop's Wave-3
   middleware goes live — see `docs/oauth-production-setup.md` §3.
2. **TLS cert for `turns:` (port 5349)** — Phase L follow-up. Today
   the TURN overlay ships plaintext-only (UDP + TCP 3478). Mount via
   cert-manager Certificate + Secret when ready.
3. **Mobile signing identities** — operator-only credentials.
   - iOS: Apple Developer Program enrolment + Distribution Cert +
     provisioning profile pushed into the `ios-signing` GitHub Actions
     secret family (placeholder — Phase L will define the full secret
     contract).
   - Android: keystore generation + base64 + push to
     `ANDROID_KEYSTORE_BASE64` + `ANDROID_KEYSTORE_PASSWORD` +
     `ANDROID_KEY_ALIAS` + `ANDROID_KEY_PASSWORD` secrets. **Losing
     the keystore forfeits the bundle ID** on Play Store — store in
     1Password.
4. **Pre-publish signature gate is now live.** `release.yml` will REFUSE
   to cut a GitHub Release for an unsigned image. If `sign-image.yml`
   ever times out or fails, the release tag will not auto-publish —
   investigate the sign-image run before re-triggering release.

---

## Open items / handoff to Wave 3

1. **Kyverno / Cosign policy-controller** k8s admission policy that
   REJECTS unsigned image pulls at the cluster layer. Today the
   verify is `release.yml`-gated; cluster-layer enforcement is the
   next step. Documented as a future Phase K item in Wave 1's memo,
   still open.
2. **`Auth:JwtSigningKey` fallback-key list** so 180-day JWT rotation
   doesn't force everyone to re-sign-in. Still deferred from Wave 1.
3. **TLS cert for `turns:` (port 5349)** on the TURN deployment.
   Phase L bringup.
4. **Mobile auto-promotion to TestFlight / Play Internal** — Phase L
   operator-mode-flip. CI produces artefacts today; auto-upload via
   `fastlane` / `bundletool` is the Phase L scope.

---

**Memo:** `.squad/decisions/inbox/apone-phase-k-wave-2.md` (this file).

**Test gate:** `dotnet test src/backend/Mahjong.Autotable.slnx --nologo`
→ **832 / 0 / 0** (baseline preserved). **Actionlint clean** on all
five new/modified workflows
(`multi-arch-runtime.yml`, `mobile-build.yml`, `pwa-smoke.yml`,
`verify-signature.yml`, `release.yml`). `bash -n` clean on
`tests/smoke/pwa-smoke.sh`. `node --check` clean on
`tests/smoke/pwa-smoke.js`. K8s YAML manifests parse-clean under
`yaml.safe_load_all`.
