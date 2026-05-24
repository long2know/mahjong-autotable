# Mobile native CI — W16 bootstrap

> Phase K Wave 16 — Apone (DevOps).

This document is the operator runbook for the W16 mobile-CI
bootstrap. The W16 deliverable is the **CI groundwork** — a
fast-feedback workflow (`.github/workflows/mobile-bundle-ci.yml`)
that validates the Capacitor config + dep install + per-platform
config slices on every PR that touches mobile surfaces, without
paying the heavy native-build cost on every PR.

The W16 bootstrap is **not** an end-to-end shipping pipeline.
Native artefact emission stays in `mobile-build.yml` (W2);
operator-provisioned signing identities (Android keystore + iOS
dev cert) are deferred to W17+ per §4 below.

## 1. Scope at W16

| Concern                                  | W16 status                                                                                              | Owner / next wave      |
|------------------------------------------|---------------------------------------------------------------------------------------------------------|-----------------------|
| Capacitor JSON parses cleanly             | ✅ landed (`validate-config` job in `mobile-bundle-ci.yml`)                                              | Apone — W16            |
| `mobile/package.json` scripts surface     | ✅ landed (`lint` job validates `sync`, `build:android`, `build:ios`)                                    | Apone — W16            |
| Per-platform config block validation      | ✅ landed (`matrix-platform-prep` job matrix on `android` + `ios`)                                       | Apone — W16            |
| `infra/mobile/capacitor.config.json` stub | ✅ landed — env-bound config sits in infra/ lane                                                         | Apone — W16            |
| Native artefact build (`.aab`, `.app`)    | ✅ already in `mobile-build.yml` (W2)                                                                    | Apone — pre-W16        |
| Android keystore signing                  | ⏸ deferred — operator-provisioned secrets per §4                                                        | Stephen — W17+         |
| iOS distribution cert + provisioning      | ⏸ deferred — operator-provisioned secrets per §4                                                        | Stephen — W17+         |
| Play Internal track auto-publish          | ⏸ deferred — Phase L scope per `mobile-build.yml` per-file header                                       | Apone or Hicks — Phase L |
| TestFlight beta-app auto-distribution     | ⏸ deferred — Phase L scope                                                                              | Apone or Hicks — Phase L |
| Mobile-bundle in-bundle telemetry         | ⏸ deferred — coupled to the Phase L renderer split (Hicks-lane)                                         | Hicks — Phase L        |

## 2. The two-workflow contract

Mobile CI has TWO complementary workflows:

| Workflow                                 | Wave introduced | Purpose                                                                                                                          | Runs on                                                                  |
|------------------------------------------|-----------------|----------------------------------------------------------------------------------------------------------------------------------|--------------------------------------------------------------------------|
| `.github/workflows/mobile-bundle-ci.yml` | W16             | Fast-feedback **CI gate** — JSON config validate, dep install, per-platform config slice validate. ~3-min wall time. Secret-free. | PR + main push on `mobile/**`, `infra/mobile/**`                          |
| `.github/workflows/mobile-build.yml`     | W2              | **Release pipeline** — full Android + iOS native build, unsigned artefacts attached to a `mobile-<run_number>` GH prerelease.    | Main push + manual `workflow_dispatch` on `mobile/**`, frontend bundle    |

Hand-off shape:

```
PR opens (touches mobile/**)
  └─→ mobile-bundle-ci.yml runs (~3 min)
        ├─ validate-config ✅
        ├─ lint            ✅
        └─ matrix x [android, ios] ✅
  └─→ PR merges to main
        └─→ mobile-build.yml runs (~20 min)
              ├─ build-frontend-bundle ✅
              ├─ android (assembleRelease)
              ├─ ios     (xcodebuild)
              └─ release (attach .aab + .app to GH prerelease)
```

**Why two workflows.** Reviewers want fast feedback. The W2
`mobile-build.yml` workflow takes ~20 minutes (the macOS
runner for iOS build dominates). For PRs that touch only
config JSON or the Capacitor plugin set, the W2 workflow is
overkill. The W16 `mobile-bundle-ci.yml` validates the surface
in ~3 minutes; the heavy build happens once on the main-branch
push.

## 3. Per-environment config override matrix

The W16 stub at `infra/mobile/capacitor.config.json` is the
canonical home for **environment-bound** Capacitor knobs. The
app-level `mobile/capacitor.config.json` carries the
**environment-invariant** runtime config.

| Key                          | Lives in                                | Env-bound? | W16 value                                          |
|------------------------------|------------------------------------------|------------|-----------------------------------------------------|
| `appId`                      | both files (MUST agree)                 | No         | `io.mahjong.autotable`                              |
| `appName`                    | both files                              | No         | `Mahjong Autotable`                                 |
| `webDir`                     | both files                              | No         | `../src/frontend/autotable` (app) / `../../src/frontend/autotable` (infra) |
| `server.url`                 | infra only (W16 stub)                   | Yes        | `https://mobile.mahjong.example.com` (prod placeholder) |
| `server.allowNavigation`     | infra only (W16 stub)                   | Yes        | prod + staging host list                            |
| `server.androidScheme`       | both files                              | No         | `https`                                             |
| `ios.contentInset`           | both files                              | No         | `always`                                            |
| `android.allowMixedContent`  | both files                              | No         | `false`                                             |
| `plugins.CapacitorHttp`      | infra only (W16 stub)                   | Yes        | `{ enabled: true }`                                 |
| `plugins.Preferences.group`  | infra only (W16 stub)                   | No         | `io.mahjong.autotable.preferences`                  |

The two configs are MERGED at bundle time by the
`mobile-bundle-ci.yml` Capacitor sync step (W17+ — the merge
is not exercised at W16 since no native build runs in this
workflow). The infra-side overrides the app-side; per
Capacitor 6 `configFile` semantics, later-loaded files win.

**Why the split.** The app-level config is committed once,
rarely touched, owned by the mobile-shell author (Hicks-lane
in Phase L when the renderer split lands). The infra-level
config tracks environment changes (staging URL flips, plugin
toggle for a beta-only feature, etc.) — Apone-lane churn.
Splitting the files lets Apone update infra/ without touching
the mobile/ scaffolding's git churn signature.

## 4. Expected secrets — operator-provisioned at W17+

The W16 bootstrap is **deliberately secret-free**. Operator
provisioning of signing identities is the W17+ deliverable.
Document the expected secrets here so the W17 owner has a
single source of truth.

### 4.1 Android keystore (Play Store signing)

| Secret name                  | Type                                     | Source                                          | Used by                                     |
|------------------------------|------------------------------------------|--------------------------------------------------|---------------------------------------------|
| `ANDROID_KEYSTORE_BASE64`    | Base64-encoded `.jks` keystore (10–50KB) | Operator generates via `keytool -genkey`         | `mobile-build.yml` Android job             |
| `ANDROID_KEYSTORE_PASSWORD`  | String (32+ char random)                 | Operator generates; stored in 1Password vault    | `mobile-build.yml` Android job             |
| `ANDROID_KEY_ALIAS`          | String                                   | Operator chooses (e.g. `mahjong-autotable-prod`) | `mobile-build.yml` Android job             |
| `ANDROID_KEY_PASSWORD`       | String (32+ char random)                 | Operator generates; stored alongside keystore    | `mobile-build.yml` Android job             |

Operator runbook (W17+): generate via `keytool`, base64-encode
the keystore (`base64 -i keystore.jks | tr -d '\n'`), paste
into `gh secret set` against the org or repo scope.

### 4.2 iOS distribution cert + provisioning profile

| Secret name                       | Type                                     | Source                                                   | Used by                                 |
|-----------------------------------|------------------------------------------|----------------------------------------------------------|-----------------------------------------|
| `IOS_DISTRIBUTION_CERT_BASE64`    | Base64-encoded `.p12` cert (5–10KB)     | Operator exports from Apple Developer portal              | `mobile-build.yml` iOS job             |
| `IOS_PROVISIONING_PROFILE_BASE64` | Base64-encoded `.mobileprovision`       | Operator downloads from Apple Developer portal            | `mobile-build.yml` iOS job             |
| `IOS_CERT_PASSWORD`               | String (32+ char random)                | Operator chooses at `.p12` export time                    | `mobile-build.yml` iOS job             |
| `IOS_TEAM_ID`                     | 10-char string (e.g. `ABCDE12345`)      | From Apple Developer portal account                       | `mobile-build.yml` iOS job             |

Operator runbook (W17+): export the cert + provisioning
profile from Apple Developer, base64-encode each, paste into
`gh secret set`.

### 4.3 Soft-fail at W16

`mobile-build.yml` has soft-fail logic (the per-file header
notes "CI soft-fails to unsigned artefacts when secrets are
absent") so the W16 + pre-W17 CI surface continues to emit
unsigned artefacts. Signed artefacts start landing at W17
when the secrets are provisioned.

## 5. Node version cadence

Both `mobile-bundle-ci.yml` (W16) and `mobile-build.yml` (W2)
pin `NODE_VERSION: "20"` (the W11+ frontend baseline). When the
frontend bumps to Node 22 (per `src/frontend/autotable-src/.
nvmrc`), bump both workflows in lock-step. The drift detection
(W18 candidate per the `docs/mobile-ci-bootstrap.md §7` future
candidate) would auto-detect Node-version drift between the
two workflows + the frontend `.nvmrc`.

## 6. Operator runbook — running locally

### 6.1 Validate Capacitor config without CI

```bash
# Validates BOTH config files parse + agree on appId.
cd /data/source/mahjong-autotable
node -e "JSON.parse(require('fs').readFileSync('mobile/capacitor.config.json','utf8'))"
node -e "JSON.parse(require('fs').readFileSync('infra/mobile/capacitor.config.json','utf8'))"
app_id_app=$(node -p "require('./mobile/capacitor.config.json').appId")
app_id_infra=$(node -p "require('./infra/mobile/capacitor.config.json').appId")
[ "$app_id_app" = "$app_id_infra" ] && echo "appId aligned: $app_id_app" || echo "DRIFT"
```

### 6.2 Local bundle build (without native compile)

```bash
cd mobile/
npm install --no-audit --no-fund
npm run sync   # cap sync — copies the webDir into native projects
```

### 6.3 Native build (operator with Android Studio + Xcode)

```bash
cd mobile/
npm run build:android   # ./gradlew assembleRelease
npm run build:ios       # xcodebuild -configuration Release ...
```

CI runs the equivalents in `mobile-build.yml`; this local
flow is for operator-side smoke testing.

## 7. W17+ follow-on candidates

| Candidate                                                       | Severity | Wave target | Effort |
|-----------------------------------------------------------------|----------|-------------|--------|
| Android keystore secret provisioning + signed `.aab` emission    | MED      | W17         | 1 wave; operator-driven secret cut |
| iOS distribution cert provisioning + signed `.app` emission      | MED      | W17         | 1 wave; operator-driven secret cut |
| Play Internal track auto-publish                                  | MED-HIGH | Phase L     | Coupled to renderer split |
| TestFlight beta-app auto-distribution                              | MED-HIGH | Phase L     | Coupled to renderer split |
| Node version drift detector (mobile vs frontend `.nvmrc`)          | LOW      | W18         | 1 wave; CI lint job |
| Mobile-bundle in-bundle telemetry (crash reporter, perf snapshot) | HIGH     | Phase L     | Hicks-lane; couples to Sentry mobile SDK |
| Capacitor 7 upgrade (when Capacitor 7 ships GA)                    | MED      | TBD         | Track upstream release cadence |

## 8. Cross-references

* [`.github/workflows/mobile-bundle-ci.yml`](../.github/workflows/mobile-bundle-ci.yml)
  — the W16 fast-feedback CI workflow.
* [`.github/workflows/mobile-build.yml`](../.github/workflows/mobile-build.yml)
  — the W2 release pipeline (companion).
* [`mobile/capacitor.config.json`](../mobile/capacitor.config.json)
  — the app-level Capacitor runtime config.
* [`infra/mobile/capacitor.config.json`](../infra/mobile/capacitor.config.json)
  — the W16 NEW infra-managed Capacitor override stub.
* [`mobile/package.json`](../mobile/package.json) — the
  Capacitor shell's `npm` surface; W16 bumped to `0.25.0` to
  align with the W16 CHANGELOG entry.
* [`mobile/README.md`](../mobile/README.md) — the W2 operator
  README (pre-existing).
* Capacitor 6 docs — <https://capacitorjs.com/docs>.

## 9. Why this lives at `docs/mobile-ci-bootstrap.md` and NOT under `docs/mobile.md`

`docs/mobile.md` (the Phase J Wave 2 doc) covers the
**Capacitor shell architecture** — what Capacitor IS, why we
chose it over React Native, the per-OS scaffolding shape.
This document covers the **CI bring-up** — what workflows
run, what secrets are needed, what the per-environment
override matrix looks like. Two different audiences (mobile
architects vs DevOps operators) — split files keep both
audiences from over-scrolling.
