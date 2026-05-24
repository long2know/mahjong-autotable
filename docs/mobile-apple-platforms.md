# Mobile Apple-platforms parity — tvOS + watchOS (W22)

> Phase K Wave 22 — Apone (DevOps).
> Audience: Stephen (operator) onboarding tvOS + watchOS
> distribution; SRE / mobile on-call when the new CI
> matrix jobs surface failures. Companion to
> [`docs/mobile-ios-signing.md`](./mobile-ios-signing.md)
> (the W18 iOS signing runbook) and to
> [`docs/mobile-ios-e2e.md`](./mobile-ios-e2e.md) (the
> W19 iOS Simulator smoke).

## 1. Background

W2 shipped Capacitor 6 with iOS + Android jobs in
[`.github/workflows/mobile-build.yml`](../.github/workflows/mobile-build.yml).
W17 added Android keystore signing; W18 added iOS
distribution-cert signing; W19 added iOS Simulator
smoke E2E. W22 extends the Apple-platforms matrix with
**tvOS** + **watchOS** build jobs to achieve full
Apple-platform parity at the CI gate.

The two new jobs are deliberately SHAPED-LIKE the W2 iOS
job — unsigned soft-fail when the Apple Developer
distribution cert + provisioning profile are absent;
signed-path placeholder when secrets are present. Full
signed-release submission is a W23+ scope (App Store
Connect + per-platform Application Loader workflow).

## 2. The W22 matrix shape

| Platform | Job name | runs-on | SDK flag | Soft-fail shape |
| -------- | -------- | ------- | -------- | --------------- |
| iOS | `ios` | `macos-latest` | `-sdk iphoneos` (W2) | `CODE_SIGNING_ALLOWED=NO` (W2/W18) |
| Android | `android` | `ubuntu-latest` | `gradlew assembleRelease` (W2) | UNSIGNED soft-fail (W2/W17) |
| **tvOS** | **`tvos-build`** | **`macos-latest`** | **`-sdk appletvos`** | **`CODE_SIGNING_ALLOWED=NO` (W22)** |
| **watchOS** | **`watchos-build`** | **`macos-latest`** | **`-sdk watchos`** | **`CODE_SIGNING_ALLOWED=NO` (W22)** |

All four Apple-platform jobs share the same secret shape
(`IOS_DEV_CERT_BASE64` + `IOS_DEV_CERT_PASSWORD` +
`IOS_PROVISIONING_PROFILE_BASE64` +
`IOS_KEYCHAIN_PASSWORD`). One Apple Developer enrolment
provisions all four targets — the provisioning profile
SHOULD include `tvOS` and `watchOS` capabilities when
Stephen updates the profile in App Store Connect.

## 3. Skip-conditional shape

The W2 iOS job pioneered the "secrets present → SIGNED
build; secrets absent → UNSIGNED soft-fail" shape. The
W22 tvOS + watchOS jobs reuse it verbatim. Each job has
a `gate` step that:

* sets `secrets-present=true` when all four `IOS_*`
  secrets are non-empty;
* sets `secrets-present=false` otherwise.

Subsequent steps fork on `steps.gate.outputs.secrets-
present`:

* `false` → unsigned `xcodebuild` run with
  `CODE_SIGNING_ALLOWED=NO`. Emits an unsigned `.app`
  (or a placeholder text file when the Capacitor iOS
  shell hasn't been bootstrapped yet — same shape as
  the W2/W17 unsigned soft-fail).
* `true` → signed-path placeholder (W22-shaped). The
  actual signed-release flow is a W23+ deliverable; the
  placeholder confirms the job's `secrets-present`
  branch executes when secrets exist.

The skip-conditional means a PR from a fork (where
`secrets.*` is unavailable) still passes the matrix
without leaking a signing failure into the gate.

## 4. Operator runbook — onboarding Apple Developer for tvOS + watchOS

### 4.1 Apple Developer profile capabilities

The existing `IOS_PROVISIONING_PROFILE_BASE64` secret is
an iOS-only provisioning profile. tvOS + watchOS require
their own profiles. Two paths:

* **Single combined profile** — update the iOS
  provisioning profile in App Store Connect to include
  tvOS + watchOS capabilities. Re-export the profile,
  re-base64-encode it, and update the
  `IOS_PROVISIONING_PROFILE_BASE64` repo secret. This is
  the canonical path for a Capacitor-shaped app that
  reuses the same bundle ID across platforms.
* **Per-platform profiles** — provision separate
  `TVOS_PROVISIONING_PROFILE_BASE64` +
  `WATCHOS_PROVISIONING_PROFILE_BASE64` secrets. Requires
  a W23+ edit to the mobile-build.yml job env shape; not
  recommended for the initial bring-up.

Recommended: single combined profile. Stephen's W18 iOS
distribution enrolment becomes the single source of
truth for the four-platform CI matrix.

### 4.2 First green run

After updating the provisioning profile:

1. Push a no-op edit to `mobile/` (e.g. bump
   `mobile/package.json` version) to trigger the
   workflow.
2. Verify the `tvos-build` + `watchos-build` jobs
   complete green:

   ```bash
   gh run list --workflow mobile-build.yml --limit 1
   gh run view <run-id> --json jobs -q \
     '.jobs[] | select(.name | test("tvOS|watchOS")) | "\(.name) → \(.conclusion)"'
   tvOS (xcodebuild Release, unsigned soft-fail) → success
   watchOS (xcodebuild Release, unsigned soft-fail) → success
   ```

3. Download the artefacts:

   ```bash
   gh run download <run-id> --name tvos-artefacts
   gh run download <run-id> --name watchos-artefacts
   ```

4. Sanity-check the artefacts:

   ```bash
   ls tvos-artefacts/  watchos-artefacts/
   # Expected: tvOS-{signed,unsigned}.placeholder.txt OR
   # a `Release-appletvos/App.app` bundle when the
   # Capacitor iOS shell + tvOS target are wired up.
   ```

### 4.3 Wiring the release step

The `release:` job at the bottom of `mobile-build.yml`
currently lists `needs: [android, android-e2e, ios, ios-
e2e]`. The W22 launch INTENTIONALLY does NOT add
`tvos-build` / `watchos-build` to the `needs:` list —
the tvOS + watchOS jobs are still in pre-signing
soft-fail mode. Once Stephen's W23 Apple Developer
enrolment covers all four platforms, the `needs:` list
SHOULD be extended:

```yaml
release:
  needs: [android, android-e2e, ios, ios-e2e, tvos-build, watchos-build]
```

This wires tvOS + watchOS artefacts into the GitHub
prerelease alongside iOS + Android. The W23 doc edit is
a one-line change to the `needs:` list + a `gh release
create` extension to attach the new artefacts.

## 5. Placeholder behaviour (W22 launch baseline)

When neither the iOS Capacitor shell nor a full Apple
Developer profile is wired, both jobs emit a placeholder
text artefact:

```
tvos-artefacts/
  tvOS-unsigned.placeholder.txt  (12 bytes — timestamp only)

watchos-artefacts/
  watchOS-unsigned.placeholder.txt  (14 bytes — timestamp only)
```

The placeholder confirms the CI matrix shape lands clean
WITHOUT requiring an end-to-end signed build at W22 ship.
The placeholder shape is the same one used by the W2
iOS job before W18 added the signed flow.

## 6. tvOS + watchOS architecture notes

### 6.1 tvOS

* `-sdk appletvos` selects the tvOS device SDK (Apple TV
  4K + Apple TV HD). The Simulator SDK is `-sdk
  appletvsimulator`; the W22 job uses the device SDK so
  the unsigned `.app` can be sideloaded onto an Apple TV
  developer device for manual smoke before App Store
  submission.
* `ARCHS="arm64"` is the canonical tvOS device arch.
  Apple TV HD historically supported armv7 but the W22
  baseline targets arm64-only (Apple TV 4K +).
* No on-device camera / sensor APIs available — the
  autotable PWA renders fine since it's a pure-canvas /
  WebGL2 app. SignalR-over-WS works through the same
  Capacitor shell.

### 6.2 watchOS

* `-sdk watchos` selects the watchOS device SDK. Apple
  Watch is a constrained surface — the autotable PWA
  shell will NOT render the full game UI on the watch
  screen. The W22 job's purpose is to validate the
  Capacitor build artefact compiles for watchOS, NOT to
  ship the full game UI to the watch.
* The watch is a companion-device target — W23+ may add
  a glanceable summary view (current score + next-tile-
  draw timer) authored by Hicks in a separate
  `mobile/watchos-companion/` directory. The W22 job's
  ARCHS list (`arm64_32 arm64`) covers Apple Watch
  Series 4 through Series 10 hardware.

## 7. Failure semantics

| Job | Failure shape | Auto-recovery |
| --- | ------------- | ------------- |
| `tvos-build` (unsigned) | `xcodebuild` returns non-zero | Job emits a placeholder artefact + the `tail -200` xcodebuild output to logs; job exits 0 |
| `tvos-build` (signed) | N/A at W22 | Signed-path placeholder — W23+ wires the real signed flow |
| `watchos-build` (unsigned) | `xcodebuild` returns non-zero | Same placeholder-on-failure shape as tvOS |
| `watchos-build` (signed) | N/A at W22 | Same as tvOS-signed |

The "placeholder on failure" shape is intentional. The
two new jobs are GROUNDWORK — they validate the CI
matrix expansion lands without blocking the existing
iOS + Android pipeline. Once Stephen lands the full
provisioning profile + Capacitor iOS shell + W23 signed
flow, the placeholders disappear in favour of real
unsigned + signed `.app` bundles.

## 8. Cross-references

* [`.github/workflows/mobile-build.yml`](../.github/workflows/mobile-build.yml)
  — the mobile build workflow (W2 + W17/W18/W19 +
  **W22 tvOS + watchOS jobs**).
* [`docs/mobile-ios-signing.md`](./mobile-ios-signing.md)
  — W18 iOS signing runbook (the secret shape this
  matrix expansion reuses).
* [`docs/mobile-ios-e2e.md`](./mobile-ios-e2e.md)
  — W19 iOS Simulator smoke (parallel to a future tvOS
  Simulator smoke).
* [`docs/mobile-android-signing.md`](./mobile-android-signing.md)
  — W17 Android keystore reference shape.
* [`docs/mobile-ci-bootstrap.md`](./mobile-ci-bootstrap.md)
  — W2 mobile pipeline overview.
* [`mobile/package.json`](../mobile/package.json)
  — Capacitor 6 mobile shell manifest (W22 bumped to
  0.31.0).

## 9. W22 → W23 hand-off

* Stephen onboards tvOS + watchOS into the existing
  Apple Developer provisioning profile + re-uploads
  `IOS_PROVISIONING_PROFILE_BASE64`.
* W23 adds a real signed-build flow (replacing the W22
  placeholder branch) plus a Simulator smoke job
  parallel to the W19 iOS smoke.
* W23 extends the `release:` job's `needs:` list to
  include `tvos-build` + `watchos-build`, attaching the
  artefacts to the mobile prerelease.
* W24+ may add an Apple Watch glanceable-summary view
  authored by Hicks under `mobile/watchos-companion/`.
