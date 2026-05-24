# Mobile iOS E2E smoke test (W20)

> Phase K Wave 20 — Apone (DevOps).
> Audience: SRE / on-call operator landing or debugging the
> iOS E2E smoke job that runs against the SIGNED-RELEASE .app.
> Companion to:
> [`docs/mobile-android-e2e.md`](./mobile-android-e2e.md)
> (the W19 Android-side counterpart this iOS job mirrors),
> [`docs/mobile-ios-signing.md`](./mobile-ios-signing.md)
> (the W18 iOS signing groundwork), and
> [`.github/workflows/mobile-build.yml`](../.github/workflows/mobile-build.yml)
> (the workflow this runbook documents).

W2 → W18 brought the Capacitor mobile shell from "scaffolded"
to "iOS + Android SIGNED branches with operator-provisioned
keystore + keychain". W19 added an E2E smoke test for the
**Android SIGNED branch** (`android-e2e` job). W20 adds the
**iOS mirror**: boot an iOS Simulator on CI, install the
just-built SIGNED .app, launch the app, verify the process
is alive, and capture a screenshot. The test gates on the
four `IOS_*` secrets being present — when the SIGNED branch
did not run (UNSIGNED legacy path on a fork PR), the E2E job
skips gracefully without burning macos-latest runner minutes.

## 1. The job shape

The W20 edit appends a fourth Apple-flavoured job —
`ios-e2e` — to `.github/workflows/mobile-build.yml`. The job
sits BETWEEN the W18 `ios` build job and the W2 `release`
job in declaration order:

```yaml
jobs:
  build-frontend-bundle: ...   # W2  — shared web bundle
  android:               ...   # W17 — SIGNED + UNSIGNED build
  android-e2e:           ...   # W19 — SIGNED-only emulator smoke
  ios:                   ...   # W18 — SIGNED + UNSIGNED xcodebuild
  ios-e2e:               ...   # W20 — SIGNED-only Simulator smoke (NEW)
  release:               ...   # W2  — prerelease on push-to-main
```

The `release` job's `needs:` list is extended to include
`ios-e2e` (alongside `android`, `android-e2e`, `ios`), so a
failed iOS E2E prevents prereleases on `main`. When the E2E
job skips (SIGNED branch did not run), the job's overall
status is `success` — the gate step exits 0 with
`should-run=false` and all subsequent steps short-circuit on
the `if:` guard.

## 2. The SIGNED-only gate

The `ios-e2e.steps[0]` ("Skip if SIGNED secrets absent")
inspects the `IOS_DEV_CERT_BASE64` secret. The four `IOS_*`
secrets land together (per `docs/mobile-ios-signing.md §3`),
so checking the cert secret is a sufficient proxy for "did
the SIGNED branch run":

```yaml
- name: Skip if SIGNED secrets absent
  id: gate
  run: |
    set -euo pipefail
    if [ -z "${IOS_DEV_CERT_BASE64:-}" ]; then
      echo "::notice::IOS_DEV_CERT_BASE64 absent — E2E smoke skipped (UNSIGNED legacy path)"
      echo "should-run=false" >> "$GITHUB_OUTPUT"
    else
      echo "::notice::SIGNED branch detected — iOS E2E smoke will run"
      echo "should-run=true" >> "$GITHUB_OUTPUT"
    fi
```

Every subsequent step is gated on
`if: steps.gate.outputs.should-run == 'true'`. The job's
overall result remains `success` when the gate is
`should-run=false`; the `release` job's `needs:` proceeds.

### 2.1 Why check `IOS_DEV_CERT_BASE64` specifically

The W18 SIGNED job's "Decode iOS signing identity" step
inspects all four `IOS_*` secrets and skips when ANY is
missing. The E2E gate uses the cert secret as the canonical
proxy — same pattern as the W19 Android E2E
(`ANDROID_KEYSTORE_BASE64`). The other three `IOS_*` secrets
(DEV_CERT_PASSWORD, PROVISIONING_PROFILE_BASE64,
KEYCHAIN_PASSWORD) gate the keychain decode + xcodebuild
signing call; their absence with `DEV_CERT_BASE64` present
would cause the W18 SIGNED build itself to fail (xcodebuild
would refuse to sign with no profile or password).
Therefore the gating shape:

* `DEV_CERT_BASE64` absent → SIGNED branch did not run → E2E
  skip is correct (no SIGNED .app to test).
* `DEV_CERT_BASE64` present but other secrets absent → W18
  SIGNED build fails → E2E `needs.ios` resolves to `failure`
  → E2E itself skips via job-level dependency gating.

## 3. The Simulator stack

### 3.1 `xcrun simctl` (NOT Detox)

The W20 job uses Apple's native `xcrun simctl` CLI rather
than a higher-level E2E framework like Detox. Rationale:

* **No instrumentation required.** Detox needs the app to
  embed its runtime; injecting that into a SIGNED .app would
  invalidate the signature (or require re-signing in CI with
  a separate identity). `xcrun simctl` runs the SIGNED .app
  unmodified.
* **Simulator-class fidelity is sufficient.** The mobile
  E2E's job is "did the SIGNED shell boot + serve the web
  bundle?", not "does every UI interaction work?". The real
  E2E surface for the autotable web app lives at
  `.github/workflows/e2e-playwright.yml`.
* **`macos-latest` already has the Simulator runtimes
  pre-installed** — no SDK download step needed.

### 3.2 The Simulator + runtime selection

The W20 job hard-codes `iPhone 15` as the device name and
resolves the iOS runtime dynamically from
`xcrun simctl list runtimes | tail -1`. This picks up
whatever iOS the macos-latest runner image ships with at the
time of run (typically iOS 17.x at W20 land; will drift with
GitHub Actions image updates). Pinning a specific iOS
version is a Wave-21+ candidate.

The "Boot iOS Simulator" step:

1. Creates a new Simulator instance named
   `mahjong-e2e-${GITHUB_RUN_ID}` (per-run UDID so concurrent
   runs don't collide).
2. Boots the Simulator with `xcrun simctl boot`.
3. Polls every 1s for up to 60s waiting for `Booted` state.
4. Calls `xcrun simctl bootstatus -b` to block until the
   Simulator is fully ready for `install` (the device
   reports Booted before SpringBoard finishes warming).

### 3.3 No Simulator cache

Unlike the W19 Android job (which caches the AVD home for
~60s of cold-boot savings), the iOS job does NOT cache the
Simulator state. Two reasons:

* The macos-latest runner's Simulator boot is ~30s cold —
  comparable to the Android warm-boot snapshot restore. The
  caching ROI is marginal.
* The Simulator state is tied to a specific iOS runtime
  version; runtime drift between runner image releases
  would invalidate the cache anyway.

If a future wave finds the boot time problematic, the
`actions/cache@v4` shape would mirror the W19 AVD cache:
key on `simulator-iPhone15-iOS17-v1`, path
`~/Library/Developer/CoreSimulator/Devices/${UDID}`.

## 4. The smoke test

The "Install SIGNED .app + smoke-test" step is the actual
test. After the Simulator boots, the script runs:

1. **Resolve bundle id** — `/usr/libexec/PlistBuddy -c 'Print
   CFBundleIdentifier' "$APP_PATH/Info.plist"` extracts the
   bundle id from the .app's plist. The Capacitor template
   ships with `com.example.app` by default; the operator can
   override via `mobile/capacitor.config.json`.

2. **Install .app** — `xcrun simctl install "${UDID}"
   "${APP_PATH}"` copies the .app into the Simulator and
   runs the signature verifier. A malformed signature here
   would fail the install and surface the SIGNED branch is
   wired incorrectly.

3. **Launch app** — `xcrun simctl launch "${UDID}"
   "${BUNDLE_ID}"` launches the application's main bundle.
   The 15-second sleep accommodates the Capacitor WebView
   cold-start + the autotable PWA bundle mount; the W7 PWA
   cold-start budget is ~3 s on a desktop browser, but the
   Simulator's emulated clock + the bundle landing on the
   WebView networking stack adds overhead (matches the W19
   Android budget).

4. **Smoke 1: process alive** — `xcrun simctl spawn ...
   launchctl list | grep "${BUNDLE_ID}"` confirms the app
   process is running after launch. A failed launch (e.g.
   WebView crash on a malformed bundle) would surface as an
   empty pid; the step exits non-zero with the last 2 min
   of `os_log` output for diagnostics.

5. **Smoke 2: capture screenshot** — `xcrun simctl io
   "${UDID}" screenshot e2e-output/home-screen.png` captures
   a PNG of the current Simulator screen. The script verifies
   the PNG is ≥ 1024 bytes — a smaller PNG implies the
   Simulator render failed (Metal pipeline error or the
   WebView never composed).

6. **Smoke 3: os_log tail** — `xcrun simctl spawn ... log
   show --last 2m --predicate 'subsystem CONTAINS
   "${BUNDLE_ID}"'` captures the last 2 minutes of os_log
   filtered to the app's bundle id. Useful for debugging
   when the screenshot smoke passes but the user sees
   something unexpected.

7. The screenshot + os_log tail are uploaded as `ios-e2e-
   artefacts` (retention 14 days). Operator inspects the
   PNG on failure investigations via:

   ```bash
   gh run download <run-id> --name ios-e2e-artefacts
   open ios-e2e-artefacts/home-screen.png
   ```

### 4.1 What this E2E does NOT cover

* Real-device coverage. The Simulator's GPU + networking
  stack differ from a physical iPhone. The TestFlight
  internal-testing channel is the production cross-check
  (covered by `docs/mobile-release.md`).
* Full UI traversal. The W20 smoke is "did the shell boot?",
  not "does every screen render?". The Playwright E2E is
  the canonical full-surface test.
* Push-notification flow. APNS interactions don't work in
  the Simulator without additional setup; out of W20 scope.
* In-app-purchase flow. StoreKit Simulator behaviour
  differs from a physical device + sandbox account; out of
  W20 scope.

## 5. Simulator teardown

The "Tear down Simulator" step runs with `if: always() &&
steps.sim.outputs.udid != ''`. Even on a smoke-test failure,
the Simulator is shutdown + deleted at job teardown — the
macos-latest runner's `/Library/Developer/CoreSimulator/`
state is ephemeral per-job (GitHub recycles the runner),
but the explicit teardown is defensive (matches the W18 iOS
keychain teardown pattern).

## 6. Run triggers

Identical to the W19 Android E2E:

* `push` to `main` — full E2E runs after the SIGNED build.
* `workflow_dispatch` — operator can manually trigger E2E
  against an ad-hoc image.
* **NOT `pull_request`** — secrets are unreachable from fork
  PRs anyway, and the macos-latest runner is expensive (the
  runtime would burn ~10 min × ~$0.08/min ≈ $0.80 per PR).

## 7. Local-reproduction commands

For debugging an E2E failure locally on an operator's macOS
workstation:

```bash
# 1. Build a SIGNED .app locally (requires the operator's
#    signing identity + provisioning profile in the local
#    Keychain; see docs/mobile-ios-signing.md §3).
cd mobile
npm install
npx cap sync ios
cd ios/App
pod install
xcodebuild \
    -workspace App.xcworkspace \
    -scheme App \
    -configuration Release \
    -sdk iphoneos \
    -derivedDataPath build \
    CODE_SIGN_STYLE=Manual

# 2. Create + boot a Simulator (the CI shape).
UDID="$(xcrun simctl create 'mahjong-e2e-local' 'iPhone 15')"
xcrun simctl boot "$UDID"
xcrun simctl bootstatus "$UDID" -b

# 3. Install + launch.
APP_PATH="build/Build/Products/Release-iphonesimulator/App.app"
xcrun simctl install "$UDID" "$APP_PATH"
xcrun simctl launch "$UDID" "$(/usr/libexec/PlistBuddy -c 'Print CFBundleIdentifier' "$APP_PATH/Info.plist")"

# 4. Capture screenshot.
xcrun simctl io "$UDID" screenshot ~/Desktop/local-smoke.png

# 5. Teardown.
xcrun simctl shutdown "$UDID"
xcrun simctl delete "$UDID"
```

Note: for the local-reproduction path, build with
`-sdk iphonesimulator` (NOT `iphoneos` as CI uses) — the
Simulator runs x86_64/arm64 Mac binaries, not ARM iOS
binaries.

## 8. Failure-mode catalogue

| Failure                          | Likely cause                              | Triage |
| -------------------------------- | ----------------------------------------- | ------ |
| Gate step skips when secrets present | Workflow env block didn't propagate the secret | Verify `env.IOS_DEV_CERT_BASE64` set on the `ios-e2e` job |
| `simctl install` exits non-zero  | .app signature invalid (CI signed with wrong identity) | Inspect `xcodebuild` log on the upstream `ios` job |
| `simctl launch` returns success but pidof is empty | WebView crash / bundle load failure | Check os_log tail in `ios-e2e-artefacts/oslog-tail.txt` |
| Screenshot < 1024 bytes           | Simulator GPU failed (Metal error)        | Re-run; if persistent, Simulator runtime issue (try a different iOS version pin) |
| `simctl bootstatus` times out     | Runner image's Simulator runtime mis-installed | Surface to Stephen; may need to wait for GitHub Actions image refresh |

## 9. Cross-references

- [`.github/workflows/mobile-build.yml`](../.github/workflows/mobile-build.yml)
  — the workflow this runbook documents.
- [`docs/mobile-android-e2e.md`](./mobile-android-e2e.md) —
  W19 Android-side counterpart (the shape this iOS E2E
  mirrors).
- [`docs/mobile-ios-signing.md`](./mobile-ios-signing.md) —
  W18 iOS signing groundwork (the `IOS_*` secrets the gate
  inspects).
- [`docs/mobile-release.md`](./mobile-release.md) — downstream
  release / TestFlight / Play Internal flow.
- [`.github/workflows/e2e-playwright.yml`](../.github/workflows/e2e-playwright.yml)
  — the canonical full-surface web E2E (the iOS E2E is a
  mobile-shell-boot smoke, not a replacement).
