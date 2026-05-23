# Apone — Phase K Wave 3 memo

**Branch:** `stlong/phase-k-wave-3-bringup`
**Date:** 2026-05-26
**Author:** Apone (DevOps / Platform Engineer)
**Scope:** Kyverno cosign admission policy, `Auth:JwtSigningKeys`
fallback-list schema + smoke + docs, TLS for `turns:` 5349,
container-scan PR gate + nightly cron, SBOM signed-by-cosign +
verified pre-publish gate, PWA-asset presence smoke, CHANGELOG
bump to **0.12.0**.

**Test gate:** `dotnet test src/backend/Mahjong.Autotable.slnx
--nologo` → baseline preserved (Wave-3 scope is pure DevOps + docs
+ infra; `src/backend/**` untouched except the schema-only
`appsettings.json` Auth array which Bishop will bind code-side in
W4/W5).

---

## What shipped

### Task 1 — Kyverno cosign admission policy

**New file:** `infra/k8s/policies/kyverno-cosign-verify.yaml`.

`ClusterPolicy` named `verify-mahjong-images` that REFUSES to admit
any Pod / Deployment / StatefulSet / DaemonSet / Job / CronJob
whose `image:` matches `ghcr.io/long2know/mahjong-autotable:*` (or
the `@sha256:…` digest-qualified form) unless the image carries
a valid cosign keyless signature whose Fulcio cert was issued to
this repo's `sign-image.yml` workflow on `refs/heads/main` or
`refs/tags/v*`, with Rekor entry verifying.

**Action-mode shape:**
- Global default: **Audit** (PolicyReport only).
- `validationFailureActionOverrides`:
    - `Enforce` in `mahjong-prod` (reject the admission).
    - `Audit` in `mahjong-staging` (log only; admit).
- Any new namespace gets the global default — fail-safe. Operator
  must explicitly add a namespace to the Enforce list once
  workloads in it are verified to consume signed images cleanly.

**Hardening details:**
- `background: false` — verifyImages must run synchronously on
  admission (cannot run as a background scan per Kyverno docs).
- `failurePolicy: Fail` — Sigstore-stack outage blocks NEW
  rollouts in Enforce namespaces; existing pods keep running. The
  alternative (`Ignore`) would let unsigned images through during
  an outage — unacceptable for a supply-chain policy.
- `mutateDigest: true` — rewrites `:tag` to `@sha256:...` post-
  verify so the pod is pinned to the exact attested bits.
- Excluded namespaces: `kube-system`, `kube-public`,
  `kube-node-lease`, `kyverno` (bootstrap chicken-and-egg —
  Kyverno's own pods cannot self-admit while still coming up).
- `webhookTimeoutSeconds: 30` — leaves headroom for the Fulcio
  + Rekor round-trip without crossing the cluster-wide webhook
  timeout ceiling.

**Identity contract:**
- `issuer: https://token.actions.githubusercontent.com`
- `subjectRegExp: ^https://github\.com/long2know/mahjong-autotable/\.github/workflows/sign-image\.yml@refs/(heads/main|tags/v.*)$`

Single source of truth for this regex now spans THREE files
(documented in the policy header):
1. `.github/workflows/sign-image.yml` (the signer)
2. `.github/workflows/verify-signature.yml` (default
   `expected-identity-pattern` input)
3. `infra/k8s/policies/kyverno-cosign-verify.yaml` (this file)

Renaming the workflow forces a coordinated change to all three.

**Operator runbook:** new `docs/admission-policy.md` (~10 KB):
Helm install snippet for Kyverno v3.2.7, apply procedure,
action-mode matrix, positive + negative test cases with expected
output, PolicyReport observability via `kubectl get policyreport`,
Prometheus alert rule, signing-workflow-rename procedure,
cross-references to Wave-1/-2 deliverables.

**Why now:** closes the explicit handoff in Wave-1 + Wave-2 memos
("Future Phase K wave: wire a Kyverno / Cosign policy-controller
k8s admission policy that REJECTS unsigned image pulls").
CI-layer enforcement was operator-checklist-gated in
`production-deployment-runbook.md`; cluster-layer enforcement is
the next step.

### Task 2 — `Auth:JwtSigningKeys` fallback list (schema + docs + smoke)

**Schema change** (`src/backend/src/Mahjong.Autotable.Api/appsettings.json`):
new top-level `Auth.JwtSigningKeys: []` array, documented inline
via the codebase's `//`-key convention. Forward-compat — empty
array; Bishop binds code-side in Wave 4 or Wave 5 (whichever has
plate space).

**Docs runbook:** new `docs/jwt-rotation.md` (~12 KB) — the full
lifecycle:

1. **Schema** — JSON shape + env-var binding (`Auth__JwtSigningKeys__0`)
   + ESO mounting from `mahjong/prod/app:auth__jwtsigningkeys__{0,1,2}`.
2. **Code-side contract** (Bishop's sealed-in spec):
   - Signer reads `[0]` at startup, caches `SigningCredentials`.
   - Validator builds `TokenValidationParameters.IssuerSigningKeys`
     from `[0..N]` — token validates if signature matches ANY
     entry (the fallback semantics).
   - `kid` header is informational (token validates regardless of
     `kid` lookup; validator iterates collection on lookup miss).
   - Startup validation: throw on empty array OR `[0]` shorter
     than 32 bytes.
   - Backwards-compat: accept legacy singular `Auth:JwtSigningKey`
     for one wave; array wins if both set.
3. **Rotation cadence:** annual (was 180 d pre-Wave-3 because the
   pain of a hard rotation gated it; relaxed to 365 d now that
   fallback eliminates the user-visible 401 window). 30-day grace
   window — keep prior 2 keys.
4. **Emergency procedure:** key compromise → set
   `JwtSigningKeys` to ONLY the new key, force-refresh ESO,
   rolling restart, accept ≤ 1 h of 401s.
5. **Annual rotation procedure** (zero-downtime, 7 steps): mint
   new key, SSM-shift (`[0]` → `[1]`, new → `[0]`), force-refresh
   ESO, rolling restart, verify via smoke, audit-log in
   `docs/secret-rotation.md`, drop eldest entry after 30 days.
6. **Smoke validation** (see Task 5 below).
7. **Migration path** — wave-by-wave: W3 (schema + smoke + docs)
   → W4 (Bishop binds, accept singular fallback) → W5 (drop
   singular fallback, add `kid` header) → W6 (Apone extends ESO).

**Why the relaxed cadence:** the secret-management.md note "180
days — Tokens are JWTs with 1h lifetime — rotate, restart pods,
accept ≤ 1h of 401s. To avoid downtime, ship a fallback-key list
(Wave 9 work)" is the original justification for the 180-day
cycle. Once the fallback list eliminates the 401 window, the
cadence can match the SaaS-canonical 365-day cycle.

**Smoke:** new `tests/smoke/jwt-rotation-smoke.sh`. End-to-end
against a live Docker image:

1. Boot image with `Auth__JwtSigningKeys__0=key0` → POST
   `/api/auth/token` → capture key0-signed JWT.
2. Stop, restart with `Auth__JwtSigningKeys__0=key1` +
   `Auth__JwtSigningKeys__1=key0`.
3. POST `/api/auth/validate` with the OLD key0-signed JWT (as
   `Authorization: Bearer …`) → MUST validate (fallback works).
4. Mint a new token → MUST be byte-different from the old one
   (proves the signer rotated).

**Forward-compat:** soft-passes (⏭ + exit 0) when `/api/auth/token`
or `/api/auth/validate` return 404. Matches the established
shape used by `pwa-smoke.sh`, `chat-flow-smoke.sh`,
`csp-report-smoke.sh`. Wired into `docker-smoke.yml` for nightly
execution. Auto-tightens to hard assertion as soon as Bishop's
surface lands.

**Port allocation:** 18094 — next free in the series (docker-build
18080, auth 18081, chat 18082, token-rotation 18083, csp-report
18084, multi-arch-runtime amd64 18091 / arm64 18092, pwa 18093,
**jwt-rotation 18094**).

### Task 3 — TLS for `turns:` on port 5349

**Base-manifest change** (`infra/k8s/base/turn-server.yaml`): coturn
args extended with `--cert=/etc/tls/tls.crt --pkey=/etc/tls/tls.key`;
new `tls` volume mounted from a `tls-cert-turn` Secret at
`/etc/tls/`. The volume is NOT marked `optional: true` — dev
clusters without the Secret fail loud (same fail-fast shape as the
existing `users` secret + `turn-server-secrets` ExternalSecret
stub).

**Production overlay** (NEW `infra/k8s/overlays/prod/turn-tls-secret.yaml`):
`ExternalSecret` bound to the `aws-secrets-manager-prod`
ClusterSecretStore. SSM key family is `/mahjong/prod/turn/tls/*`
(operator-provisioned out-of-band). Materialised k8s Secret is
typed `kubernetes.io/tls` (standard `tls.crt` + `tls.key` keys)
so coturn's `--cert/--pkey` reads the canonical key names directly.

**ACM-vs-export decision:** we do NOT bind ESO directly to AWS
Certificate Manager (ACM). Reason: ACM private certs are
cryptographically locked inside the ACM HSM by design — they
cannot be materialised outside the service. Operators export a
PUBLIC cert (cert-manager + Let's Encrypt HTTP-01, or ACM Public
CA with cert-export enabled) into SSM SecureString parameters at
`/mahjong/prod/turn/tls/{crt,key}`. Documented in the file
header + `docs/turn-server-setup.md` §1.4.

**Docs update:** `docs/turn-server-setup.md` §1.4 rewritten from
"Phase L follow-up" placeholder to operator-actionable runbook:
cert provisioning paths (cert-manager+LE preferred; ACM Public CA
acceptable), SSM upload steps, IAM scope (extra
`/mahjong/prod/turn/tls/*` grant alongside Wave-2's
`/mahjong/prod/turn/*`), apply procedure, force-refresh ESO,
rotation cadence (60 days before expiry for LE's 90-day certs).
Phase L additions still deferred: mTLS for API ↔ TURN signalling,
DTLS over UDP browser-testing.

**Why now:** Wave-2 deferred TLS to Phase L. Wave-3 brings it
forward because corporate firewalls that block plain UDP/TCP
:3478 are the canonical user-impact scenario; the `turns:`
listener was already exposed on port 5349 (Wave-2
`turn-server.yaml` ConfigMap had `tls-listening-port=5349`) —
just missing the cert mount.

### Task 4 — Container-scan workflow

**New workflow** `.github/workflows/container-scan.yml`.

**Why a NEW workflow vs extending `sbom.yml`** (which already does
Trivy CRITICAL+HIGH on the same image):

| Dimension | `sbom.yml` (Wave 9) | `container-scan.yml` (Wave 3) |
|-----------|---------------------|--------------------------------|
| Purpose   | SBOM generation primary; vuln gate attached | Vuln scan primary |
| Trigger   | Paths filter (Dockerfile / csproj / package.json) | EVERY PR (no path filter) |
| Threshold | CRITICAL + HIGH (fixed) | CRITICAL default, configurable to HIGH / MEDIUM |
| Cron      | Weekly (Mon 09:00 UTC) | Nightly (04:00 UTC, offset) |
| PR comment| One-time create | STICKY (`marocchino/sticky-pull-request-comment@v2`) |
| SARIF cat | `trivy-image` | `trivy-container-scan` (distinct so findings don't overlay) |

Two workflows + distinct purposes is the right factoring. A
CRITICAL CVE published against an indirect dep MUST surface on
any PR (not only the ones that touch the image surface) — that's
the gap container-scan closes.

**Threshold knob:** `workflow_dispatch.inputs.threshold` (choice:
CRITICAL / HIGH / MEDIUM, default CRITICAL). Lets triage
temporarily relax the gate for a one-off rerun without a code
change.

**Sticky PR comment** with header `container-scan` includes
CRITICAL + HIGH + MEDIUM counts, gate verdict, links to Security
tab + workflow run. Updates in place across subsequent runs.

**Concurrency:** group `container-scan-<workflow>-<ref>` with
`cancel-in-progress: true` — PR pushes preempt prior runs.

### Task 5 — SBOM signed-by-cosign pre-publish gate in `release.yml`

**Change:** new `verify-sbom` job between `verify-signature` and
`release`. Three steps:

1. Generate SPDX SBOM from the digest-qualified image reference
   (`needs.smoke.outputs.image-digest` — the exact bits we just
   smoke-tested AND signature-verified).
2. `cosign sign-blob --yes --output-signature
   sbom.spdx.json.sig --output-certificate sbom.spdx.json.pem
   sbom.spdx.json` — keyless OIDC signing (separate `id-token:
   write` permission on this job only; rest of release.yml stays
   at `contents: read, packages: read`).
3. `cosign verify-blob --signature sbom.spdx.json.sig
   --certificate sbom.spdx.json.pem
   --certificate-identity-regexp "…/release.yml@refs/tags/v.*"
   --certificate-oidc-issuer "https://token.actions.githubusercontent.com"
   sbom.spdx.json` — gates release on a positive verify.

**Why generate-sign-verify here vs reading `sbom.yml`'s artefact:**
cross-workflow artefact passing is brittle in GitHub Actions
(artefacts are per-run; resolving the right run id at tag-push
time requires extra plumbing). Generating from the tagged image,
signing keyless, and verifying in the same job binds the SBOM
cryptographically to the release tag — exactly what supply-chain
auditors want to see in the Rekor entry.

**Release attachment:** signed SBOM bundle (`sbom.spdx.json` +
`sbom.spdx.json.sig` + `sbom.spdx.json.pem`) is attached as
workflow artefacts (90-day retention) AND as assets on the GitHub
Release page. Downstream auditors can pull all three directly
without re-running CI.

**Identity regex distinction:** image signing identity is
`sign-image.yml@refs/(heads/main|tags/v.*)` (because that workflow
fires on `main` pushes too); SBOM signing identity is
`release.yml@refs/tags/v.*` (because release.yml ONLY runs on tag
pushes). The verify-blob regex pins the more restrictive identity.

### Task 6 — PWA-asset presence gate in `docker-smoke.yml`

**Change:** new step in `docker-smoke.yml` that builds the
production image once (with a per-run image tag
`mahjong-pwa-asset-gate-<run_id>`), then runs
`docker run --rm <image> sh -c 'ls -la
/frontend/autotable/{sw.js,manifest.webmanifest,manifest-precache.json}'`
— HARD-FAILS if ANY of the three Wave-3 PWA artefacts are
missing.

**Path correction:** the spec mentioned `/app/wwwroot/...` but the
Dockerfile copies the frontend output to `/frontend/autotable/`
(Program.cs L65 hardcodes that path). Used the correct runtime
path.

**Placement (`docker-smoke.yml` extension vs Dockerfile RUN
step):** chose docker-smoke.yml because a Dockerfile `RUN ls ...
|| exit 1` would block EVERY image build (local dev included)
until Hicks's Wave-3 PWA artefacts merge. docker-smoke.yml runs
nightly + on dispatch, surfacing the gap without blocking
inner-loop development. Same artefact-presence floor, gentler
failure surface.

**Coexists with `pwa-smoke.yml`** (Wave-2) — that workflow
exercises the SW LIFECYCLE in chromium (Playwright probe of
`navigator.serviceWorker.controller`); this gate is the
per-FILE-PRESENCE floor that catches the case where SW JS
shipped but the precache manifest didn't (browser would silently
install an empty SW that controls no routes — caught here, not
in pwa-smoke).

### Task 7 — CHANGELOG 0.12.0 entry

Rolled the previous `[Unreleased]` section into the new
**[0.12.0] — Phase K Wave 3 — 2026-05-26 (PR #49)** section.
Comprehensive Added/Changed lists per task. `[Unreleased]`
header reset → "Phase K Wave 4 not yet started". Compare-link
footnote added: `[0.12.0]: …v0.11.0...v0.12.0`,
`[Unreleased]: …v0.12.0...HEAD`.

---

## Patterns locked for future DevOps work

- **Three-layer supply-chain enforcement.** Workflow-level
  signing (`sign-image.yml`) → release-gate verification
  (`release.yml` → `verify-signature.yml`) → admission-layer
  enforcement (`kyverno-cosign-verify.yaml`). Each layer has a
  distinct bypass scenario; together they form defense-in-depth.
  The signer-identity regex is the cross-layer invariant — change
  one, change all three.

- **Per-namespace Audit/Enforce action via
  `validationFailureActionOverrides`.** Single ClusterPolicy with
  global Audit default + Enforce override for prod is cleaner
  than two separate policies, AND fail-safe for new namespaces
  (they get Audit until explicitly opted in). Standard Kyverno
  1.10+ shape; works with the operator's existing Helm-installed
  controller.

- **`failurePolicy: Fail` is the right default for supply-chain
  policies.** Sigstore outage during admission should block NEW
  rollouts, not let unsigned images through. The cost is
  temporarily-degraded deploy velocity during a Fulcio/Rekor
  outage; the alternative is bypassing the policy at exactly the
  moments when it most matters.

- **`mutateDigest: true` pins the pod to the attested bits.**
  Tag re-pushes between admit-and-pull would otherwise defeat
  the signature check; rewriting `:tag` → `@sha256:...` post-
  verify closes the gap.

- **Forward-compat smoke pattern, generalised.** Six smokes now
  follow the soft-pass-on-404 + auto-tighten-on-200 shape:
  `docker-build`, `auth-flow`, `chat-flow`, `token-rotation`,
  `csp-report`, `pwa`, **`jwt-rotation`** (Wave-3). New
  surface-probing smokes should ALWAYS adopt this shape so
  Bishop / Hicks can land code-side surfaces without coordinating
  with my smoke flips.

- **Unique smoke port allocation continues.** docker-build=18080,
  auth=18081, chat=18082, token-rotation=18083, csp-report=18084,
  multi-arch-runtime(amd64)=18091 / (arm64)=18092, pwa=18093,
  **jwt-rotation=18094**. Next free: 18095.

- **JWT fallback-list semantics (codified for Bishop's W4/W5
  binding).** Active signer = `[0]`; validator iterates
  `[0..N]`; `kid` is informational only; startup throws on empty
  array or `[0]` < 32 bytes. Documented in `docs/jwt-rotation.md`
  §2 as a sealed-in spec — Bishop has zero design ambiguity.

- **30-day fallback-grace window.** Long enough to swallow any
  cached-token weirdness in downstream services; short enough
  that key-leak risk doesn't compound. SaaS-canonical.

- **TLS-cert ExternalSecret pattern for stateful services.**
  Operator pre-provisions cert+key in SSM SecureString (NOT in
  ACM directly — ACM private certs can't be materialised outside
  the HSM). ESO materialises a `kubernetes.io/tls` Secret with
  standard `tls.crt`/`tls.key` keys so downstream consumers
  (Ingress / coturn / nginx / haproxy) work with zero
  per-consumer adapter code. Reusable pattern for the next TLS
  endpoint we ship.

- **Container-scan vs SBOM workflow factoring.** SBOM-focused
  workflow gates on path filters + weekly cron (SBOM-refresh
  cadence). Scan-focused workflow gates on EVERY PR + nightly
  cron (vulnerability-watch cadence). Different SARIF categories
  so findings don't overlay in the Security tab. Two workflows
  + distinct purposes; do NOT collapse into one.

- **SBOM signing identity is `release.yml@refs/tags/v.*` (not
  `sign-image.yml@…`).** release.yml only fires on tag pushes;
  sign-image.yml also fires on main pushes. The verify-blob
  regex MUST match the SIGNER workflow, not the image-signer
  workflow.

- **Cross-workflow artefact passing is brittle; generate-sign-
  verify in-process is robust.** Resolving "the SBOM artefact
  for this commit from a different workflow run" requires
  resolving the right run id — extra plumbing, extra failure
  modes. Generating from the tagged image in the same job pins
  the SBOM cryptographically to the tag.

- **PWA-asset gate placement: `docker-smoke.yml` extension over
  Dockerfile RUN step.** A Dockerfile `RUN ls … || exit 1`
  would block EVERY image build (local dev too) until the
  artefacts land. docker-smoke.yml runs nightly — gentler
  failure surface, same artefact-presence floor.

---

## Open items / handoff

1. **Bishop (W4 or W5):** code-side `Auth.JwtSigningKeys` binding
   per the sealed-in contract at `docs/jwt-rotation.md` §2. Once
   bound, the `tests/smoke/jwt-rotation-smoke.sh` auto-tightens
   to a hard assertion. Surfaces to expose:
   `POST /api/auth/token` (mint), `POST /api/auth/validate`
   (validate). Preserve singular `Auth:JwtSigningKey` fallback
   for one wave then remove. Add `kid` header to minted tokens
   in W5.

2. **Hicks (W3 or W4):** Parcel post-build must emit
   `sw.js`, `manifest.webmanifest`, `manifest-precache.json`
   into `src/frontend/autotable/` so the new PWA-asset gate
   passes. The gate is HARD now — once the artefacts land, the
   docker-smoke nightly goes green and stays green.

3. **Operator (Stephen):**
   - **Kyverno install** — Helm install per
     `docs/admission-policy.md` §2. Apply the policy. Smoke-test
     positive + negative deployments per §5. Production cutover
     stays in Audit until the first week's PolicyReports are
     clean, then add `mahjong-prod` to the Enforce override list
     (already there in the shipped policy file — verify on apply).
   - **TURN TLS cert** — provision cert via cert-manager+LE or
     ACM-export, push to SSM per `docs/turn-server-setup.md`
     §1.4. Apply `turn-tls-secret.yaml`. Restart turn-server
     Deployment.
   - **JWT rotation SSM seed** — when Bishop's W4 binding ships,
     seed `/mahjong/prod/app/auth__jwtsigningkeys__0` (and
     optional `__1`, `__2`) before deploy. Extend
     `infra/k8s/overlays/prod/secret-template.yaml` ESO
     `data:` block (planned: W6, Apone).
   - **Tag push** — `v0.12.0` push triggers the full chain:
     docker-build → smoke → verify-signature → verify-sbom →
     release. The SBOM bundle attaches to the Release page.

4. **Vasquez (audit):** new container-scan SARIF lands under
   `category: trivy-container-scan` in the Security tab. Distinct
   from `sbom.yml`'s `trivy-image` so findings don't overlay.
   `jwt-rotation` smoke is now part of the docker-smoke nightly —
   if Bishop's W4 binding regresses the rotation surface, the
   nightly artefact upload will surface the failure.

5. **Future Phase K wave (W5+):**
   - **SLSA provenance predicates.** cosign supports attaching
     in-toto attestations (build provenance, vulnerability scan
     results, SBOMs) to images. Wave-3 ships SBOM signing as a
     detached blob; a future wave can attach the SBOM as an
     in-toto predicate to the image, and Kyverno can verify the
     attestation alongside the signature (another `attestors`
     block in `kyverno-cosign-verify.yaml`).
   - **HMAC time-limited TURN credentials** — still on the
     deferred list from Wave-2 (`docs/turn-server-setup.md` §5).
     Bishop's `/api/turn` endpoint needs to mint per-session
     HMAC creds; coturn flips from `lt-cred-mech` to
     `use-auth-secret`.
   - **Mobile app-store auto-promotion.** fastlane / bundletool
     wiring (still on the Phase L deferred list).

---

## Build invariants verified

- **`actionlint`** clean on the three modified workflows
  (`release.yml`, `container-scan.yml`, `docker-smoke.yml`) and
  the existing workflows (no regressions).
- **`bash -n`** clean on `tests/smoke/jwt-rotation-smoke.sh`.
- **`shellcheck`** clean on `tests/smoke/jwt-rotation-smoke.sh`.
- **`yaml.safe_load_all`** clean on all new + modified YAMLs:
  - `infra/k8s/policies/kyverno-cosign-verify.yaml`
  - `infra/k8s/overlays/prod/turn-tls-secret.yaml`
  - `infra/k8s/base/turn-server.yaml`
  - `.github/workflows/container-scan.yml`
  - `.github/workflows/release.yml`
  - `.github/workflows/docker-smoke.yml`
- **`python3 -c "json.load(open('appsettings.json'))"`** clean on
  the schema addition.
- **`dotnet test`** baseline preserved (Wave-3 scope did not
  touch `src/backend/**` source code; the schema-only
  `appsettings.json` change is bound to nothing yet — Bishop's
  W4/W5 code-side binding is the next gate to flip).
