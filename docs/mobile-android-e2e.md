# Mobile Android E2E smoke test (W19)

> Phase K Wave 19 — Apone (DevOps).
> Audience: SRE / on-call operator landing or debugging the
> Android E2E smoke job that runs against the SIGNED-RELEASE
> APK. Companion to
> [`docs/mobile-android-signing.md`](./mobile-android-signing.md)
> (the W17 SIGNED groundwork) and to
> [`docs/mobile-ios-signing.md`](./mobile-ios-signing.md) (the
> W18 iOS mirror).

W2 → W18 brought the Capacitor mobile shell from "scaffolded"
to "iOS + Android SIGNED branches with operator-provisioned
keystore + keychain". W19 adds an **E2E smoke test for the
Android SIGNED branch**: boot an emulator on CI, install the
just-built APK, launch the app, verify the process is alive,
and capture a screenshot. The test gates on the four `ANDROID_*`
secrets being present — when the SIGNED branch did not run
(UNSIGNED legacy path on a fork PR), the E2E job skips
gracefully without burning emulator-runner minutes.

## 1. The job shape

The W19 edit appends a third Android-flavoured job —
`android-e2e` — to `.github/workflows/mobile-build.yml`. The
job sits BETWEEN the W17 `android` build job and the W18 `ios`
job in declaration order:

```yaml
jobs:
  build-frontend-bundle: ...   # W2 — shared web bundle
  android:               ...   # W17 — SIGNED + UNSIGNED build
  android-e2e:           ...   # W19 — SIGNED-only emulator smoke (NEW)
  ios:                   ...   # W18 — SIGNED + UNSIGNED xcodebuild
  release:               ...   # W2 — prerelease on push-to-main
```

The `release` job's `needs:` list is extended to include
`android-e2e` (alongside `android` + `ios`), so a failed E2E
prevents prereleases on `main`. When the E2E job skips
(SIGNED branch did not run), the job's overall status is
`success` — the gate step exits 0 with `should-run=false`
and all subsequent steps short-circuit on the `if:` guard.

## 2. The SIGNED-only gate

The `android-e2e.steps[0]` ("Skip if SIGNED secrets absent")
inspects the `ANDROID_KEYSTORE_BASE64` secret. The four
`ANDROID_*` secrets land together (per
`docs/mobile-android-signing.md §3`), so checking the keystore
secret is a sufficient proxy for "did the SIGNED branch run":

```yaml
- name: Skip if SIGNED secrets absent
  id: gate
  run: |
    set -euo pipefail
    if [ -z "${ANDROID_KEYSTORE_BASE64:-}" ]; then
      echo "::notice::ANDROID_KEYSTORE_BASE64 absent — E2E smoke skipped (UNSIGNED legacy path)"
      echo "should-run=false" >> "$GITHUB_OUTPUT"
    else
      echo "::notice::SIGNED branch detected — Android E2E smoke will run"
      echo "should-run=true" >> "$GITHUB_OUTPUT"
    fi
```

Every subsequent step is gated on
`if: steps.gate.outputs.should-run == 'true'`. The job's
overall result remains `success` when the gate is
`should-run=false`; the `release` job's `needs:` proceeds.

### 2.1 Why check `ANDROID_KEYSTORE_BASE64` specifically

The W17 SIGNED job's "Decode Android keystore" step uses the
same env variable as the existence proxy. Checking the same
variable in the E2E gate keeps the SIGNED↔E2E coupling
explicit — if the W17 step's gate ever changes (e.g. a future
wave introduces multi-keystore support), the E2E gate is the
ONLY other reader.

The other three `ANDROID_*` secrets (KEYSTORE_PASSWORD,
KEY_ALIAS, KEY_PASSWORD) gate the actual Gradle signing call;
their absence with KEYSTORE_BASE64 present would cause the
W17 SIGNED build itself to fail (gradle would refuse to
sign with empty credentials). Therefore the gating shape:

* `KEYSTORE_BASE64` absent → SIGNED branch did not run → E2E
  skip is correct (no SIGNED APK to test).
* `KEYSTORE_BASE64` present but other secrets absent → W17
  SIGNED build fails → E2E `needs.android` resolves to
  `failure` → E2E itself skips via job-level gating.

## 3. The emulator stack

### 3.1 `reactivecircus/android-emulator-runner@v2.34.0`

The W19 job uses the upstream `reactivecircus/android-
emulator-runner` action (v2.34.0, pinned via SHA). The action:

* Provisions Android SDK + system images.
* Creates an AVD (Android Virtual Device) with the requested
  api-level / target / arch.
* Boots the emulator with hardware acceleration (KVM).
* Runs the operator-provided `script:` inside the emulator
  session.
* Tears down the emulator on completion.

### 3.2 KVM + `ubuntu-latest-8-cores`

The job runs on `ubuntu-latest-8-cores` (GitHub Actions large
runner). The standard `ubuntu-latest` runner does NOT expose
KVM (`/dev/kvm` missing); the emulator boots in software-
rendered SwiftShader fallback at ~10x the wall-clock cost (an
emulator boot that takes 90 s with KVM takes ~12-15 min
without). The 8-core large runner exposes `/dev/kvm` via
nested virtualisation.

The "Enable KVM acceleration" step writes a udev rule that
makes `/dev/kvm` world-writeable inside the runner. Required
because the GitHub Actions runner user is not in the `kvm`
group by default.

### 3.3 AVD cache

The job caches the AVD home (`~/.android/avd/*`) keyed by
the requested AVD parameters. A cache hit cuts emulator boot
time from ~90 s (cold boot) to ~30 s (warm-boot snapshot
restore). Cache key version (`avd-api-34-default-x86_64-v1`)
bumps when:

* The AVD parameters change (api-level / target / arch).
* Stephen suspects cache corruption (manual bump of the `-v1`
  suffix).

Cache miss path: the "Create AVD + warm-boot snapshot" step
runs the emulator-runner action with `script: echo "AVD
warm-boot snapshot created."` — boots the emulator, captures
a snapshot, exits. Cache writes happen at job-end via the
`actions/cache@v4` post-job hook.

## 4. The smoke test

The "Boot emulator + run E2E smoke" step is the actual test.
After the emulator boots, the script runs:

1. **Resolve package id** — `aapt dump badging` parses the
   APK manifest and extracts the application's package name.
   The W17 SIGNED + W18 UNSIGNED branches both produce
   identical package ids (the Capacitor template ships with
   `com.example.app` by default; the operator can override
   via `IOS_BUNDLE_ID_OVERRIDE` + the Android equivalent
   `cap.config.json`).

2. **Install APK** — `adb install -r` overwrites any
   pre-installed instance and runs the APK signature
   verifier. A malformed signature here would fail the
   install and surface the SIGNED branch is wired
   incorrectly.

3. **Launch app** — `adb shell monkey -p <pkg> -c
   android.intent.category.LAUNCHER 1` sends a single
   tap-like event to the launcher activity. The 15-second
   sleep accommodates the Capacitor WebView cold-start +
   the autotable PWA bundle mount; the W7 PWA cold-start
   budget is ~3 s on a desktop browser, but the emulator's
   slower clock + the bundle landing on the WebView
   networking stack adds overhead.

4. **Smoke 1: process alive** — `adb shell pidof <pkg>`
   confirms the app process is running after launch. A
   failed launch (e.g. WebView crash on a malformed
   bundle) would surface as an empty pidof; the step exits
   non-zero with the last 200 logcat lines for
   diagnostics.

5. **Smoke 2: navigate + screenshot** — `adb shell input
   keyevent KEYCODE_BACK` simulates a single back-button
   press. The autotable SPA's router has a navigation guard
   on the home route; a successful navigation lands the
   user back on the home screen. `adb exec-out screencap -p`
   captures a PNG of the current screen. The script verifies
   the PNG is ≥ 1024 bytes — a smaller PNG implies the
   emulator GPU failed (SwiftShader produced an empty frame
   buffer or the WebView never composed).

6. **Smoke 3: logcat tail** — `adb logcat -d -t 200`
   captures the last 200 lines of logcat to the artefact
   bundle. Useful for debugging when the screenshot smoke
   passes but the user sees something unexpected.

7. The screenshot + logcat are uploaded as `android-e2e-
   artefacts` (retention 14 days). Operator inspects the
   PNG on failure investigations via:

   ```bash
   gh run download <run-id> --name android-e2e-artefacts
   open android-e2e-artefacts/home-screen.png
   ```

### 4.1 What this E2E does NOT cover

* **No SignalR negotiate.** The smoke launches the app +
  takes a screenshot but does NOT log in / join a table /
  cycle SignalR. The W12-era SignalR sequence-SLO panel
  (Hudson) is the canonical observability surface for that.
* **No matchmaking flow.** The mobile shell wraps the same
  PWA bundle as the desktop browser, but the matchmaking
  W5 → W8 flow happens on the server; the E2E does not
  exercise it.
* **No multi-device.** Single emulator, single tap. A
  multi-device sync E2E (two emulators on the same backend
  table) is a Wave-20+ candidate.

## 5. Trigger surface

The `android-e2e` job runs on:

* `push` to `main`.
* `workflow_dispatch`.

It does NOT run on `pull_request`. Rationale:

* PRs from forks cannot access the `ANDROID_*` secrets
  (GitHub's secret-sharing model); the E2E would
  uniformly skip via the gate.
* PRs from the main repo trigger the E2E on the next
  push-to-main if the merge ships the SIGNED secrets.
* The large-runner credit burn is the limiting factor;
  PR runs would 4x the cost.

If a future wave wants per-PR E2E (e.g. for a major
ingress-side change), the `workflow_dispatch` is the
operator-side trigger; `gh workflow run mobile-build.yml`
from the PR branch.

## 6. Local reproduction

The job CAN be reproduced locally with the same emulator
parameters. From the operator workstation:

```bash
# Prerequisites: Android SDK + cmdline-tools, KVM available.
# Install via Android Studio (Tools → SDK Manager) or
# sdkmanager.

# 1. Create the AVD.
sdkmanager --install "system-images;android-34;default;x86_64"
echo "no" | avdmanager create avd \
    --name e2e-test \
    --package "system-images;android-34;default;x86_64" \
    --device "pixel_6"

# 2. Boot the emulator (background).
emulator -avd e2e-test -no-window -gpu swiftshader_indirect \
    -noaudio -no-boot-anim &

# 3. Wait for the device to come up.
adb wait-for-device
adb shell getprop sys.boot_completed
# (loop until "1" — typically 60-120 s)

# 4. Install the SIGNED APK from a local build.
# Build the APK first via:
#   cd mobile && npm install && npx cap sync android && \
#       cd android && ./gradlew assembleRelease ...
adb install -r mobile/android/app/build/outputs/apk/release/app-release.apk

# 5. Launch + smoke (mirrors the CI script).
PKG=$(aapt dump badging \
    mobile/android/app/build/outputs/apk/release/app-release.apk \
    | awk -F"'" '/^package: name=/ {print $2; exit}')
adb shell monkey -p "$PKG" -c android.intent.category.LAUNCHER 1
sleep 15
adb exec-out screencap -p > home-screen.png
ls -la home-screen.png
```

## 7. Cross-references

- [`.github/workflows/mobile-build.yml`](../.github/workflows/mobile-build.yml)
  — the `android-e2e` job source.
- [`docs/mobile-android-signing.md`](./mobile-android-signing.md)
  — W17 SIGNED-branch keystore + Gradle wiring.
- [`docs/mobile-ios-signing.md`](./mobile-ios-signing.md) — W18
  iOS SIGNED-branch keychain + xcodebuild wiring.
- [`docs/mobile-release.md`](./mobile-release.md) — W2
  release prerelease publication.
- `reactivecircus/android-emulator-runner` —
  <https://github.com/ReactiveCircus/android-emulator-runner>
  (upstream action source).
