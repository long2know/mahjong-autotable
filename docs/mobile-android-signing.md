# Mobile — Android signing operator runbook

> Phase K Wave 17 — Apone (DevOps). Companion to
> [`docs/mobile-ci-bootstrap.md`](./mobile-ci-bootstrap.md)
> (W16 mobile-CI bootstrap) and
> [`.github/workflows/mobile-build.yml`](../.github/workflows/mobile-build.yml)
> (the W2 release pipeline; W17 added the Android signing
> branch).
>
> Audience: Stephen (operator) — provisions the four
> `ANDROID_*` GitHub Actions repository secrets that gate the
> SIGNED-RELEASE branch of the Android job. The W17
> deliverable is the **groundwork** (workflow steps + secrets
> contract + this runbook); the actual secret upload is a
> manual operator step Stephen takes once.
>
> iOS distribution signing is intentionally **deferred to
> W18** — it requires more cert-chain setup (Apple
> Distribution cert + Provisioning Profile + App Store
> Connect API key) than a single wave's bandwidth supports.
> See §7 for the W18 hand-off.

## 1. Scope at W17

| Concern                                          | W17 status                                                                                          | Owner             |
|--------------------------------------------------|-----------------------------------------------------------------------------------------------------|-------------------|
| Gradle signing-config wiring                      | ✅ landed (signed `assembleRelease` + `bundleRelease` branch in `mobile-build.yml`)                  | Apone — W17       |
| Keystore decode step (base64 → file)              | ✅ landed (`steps.keystore` step, `RUNNER_TEMP`-scoped)                                              | Apone — W17       |
| Secret-presence gate (signed vs unsigned)         | ✅ landed (`if: steps.keystore.outputs.keystore-present == 'true'`)                                  | Apone — W17       |
| Operator runbook (this doc)                       | ✅ landed                                                                                            | Apone — W17       |
| Four `ANDROID_*` secrets uploaded to repo         | ⏸ **deferred — Stephen action** per §3                                                              | Stephen — W17+    |
| Keystore generation (one-time)                    | ⏸ Stephen action per §2                                                                             | Stephen — one-time |
| Play Store Internal track auto-publish            | ⏸ deferred — Phase L scope per `mobile-build.yml` per-file header                                   | Phase L           |
| iOS distribution signing (cert + profile)         | ⏸ deferred — W18 per §7                                                                             | Apone — W18       |

The W17 wave is **secret-free** at PR-time: the workflow
runs the UNSIGNED-RELEASE branch (W16 behaviour, byte-
identical) until Stephen provisions the four
`secrets.ANDROID_*` entries. Once provisioned, the next
`mobile-build.yml` run on the main branch automatically
takes the SIGNED-RELEASE branch.

## 2. Generating the keystore (one-time)

The Play Store requires every release artefact to be signed
by **the same keystore** for the lifetime of the app — a
keystore swap is an app-republish event (different
`io.mahjong.autotable` package signature = Play Store treats
it as a brand-new app). The keystore generation is a
**one-time** operator action with **zero rotation cadence**
unless the operator deliberately wants to republish.

Stephen runs **once**, locally on his macOS workstation
(NOT in CI — CI never sees the unencrypted keystore):

```bash
# Use an LTS JDK matching the W2 + W17 mobile-build.yml `setup-java@v4`
# pin (`distribution: temurin`, `java-version: "17"`).
keytool -genkey -v \
  -keystore mahjong-autotable.keystore \
  -alias mahjong-autotable \
  -keyalg RSA \
  -keysize 4096 \
  -validity 36500 \
  -storetype JKS \
  -dname "CN=long2know,O=Mahjong Autotable,L=Atlanta,ST=GA,C=US"
```

The four prompts:

1. **Keystore password** — store this in 1Password vault
   `mahjong-autotable / Play Store / keystore-password`.
   16+ chars, alphanumeric + symbols.
2. **Key password** — can equal the keystore password (and
   often does for single-key keystores). Store at
   `mahjong-autotable / Play Store / key-password`.
3. **Key alias** — `mahjong-autotable` per the `-alias` flag
   above. Store at `mahjong-autotable / Play Store / key-alias`.
4. **Validity** — `36500` days (= 100 years). Play Store
   policy requires the key be valid through `2033-10-22` at
   the earliest for new app uploads; 100 years is the upper
   bound `keytool` accepts and matches Android Studio's
   wizard default.

**Backup the keystore IMMEDIATELY** to an offline-encrypted
volume (1Password "File" attachment under
`mahjong-autotable / Play Store / keystore-file`). Losing the
keystore is a **non-recoverable** event — see §6 for the
"lost keystore" disaster scenario.

## 3. Uploading the four `ANDROID_*` secrets

Once the keystore + passwords + alias exist on Stephen's
workstation, upload them as four GitHub Actions repository
secrets on `github.com/long2know/mahjong-autotable`:

| Secret name                  | Source                                                                                             | Notes                                                                 |
|------------------------------|----------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------|
| `ANDROID_KEYSTORE_BASE64`    | `base64 -i mahjong-autotable.keystore` (macOS) or `base64 -w 0 mahjong-autotable.keystore` (Linux). | Single-line base64 string; GitHub secrets max 64 KiB (the keystore is ~3 KiB binary → ~4 KiB base64). |
| `ANDROID_KEYSTORE_PASSWORD`  | The §2 keystore password.                                                                          | Plain text. Never log.                                                |
| `ANDROID_KEY_ALIAS`          | The §2 key alias (`mahjong-autotable`).                                                            | Plain text.                                                           |
| `ANDROID_KEY_PASSWORD`       | The §2 key password.                                                                               | Plain text. Often equal to `ANDROID_KEYSTORE_PASSWORD`.                |

Upload via the GitHub web UI (Settings → Secrets and
variables → Actions → New repository secret) or the `gh` CLI:

```bash
gh secret set ANDROID_KEYSTORE_BASE64 < /path/to/keystore.base64
gh secret set ANDROID_KEYSTORE_PASSWORD --body "<paste from 1Password>"
gh secret set ANDROID_KEY_ALIAS        --body "mahjong-autotable"
gh secret set ANDROID_KEY_PASSWORD     --body "<paste from 1Password>"
```

All four secrets are **repository-scoped** (not
environment-scoped) — the W17 workflow's signing job runs on
every main-branch push and we deliberately do **not** want a
GitHub Environment approval gate on prerelease artefacts
(prerelease ≠ Play Store upload; Play Store upload remains a
manual operator step per `mobile-build.yml` per-file header).

## 4. The W17 workflow shape

`mobile-build.yml` (W2, W17-amended) Android job:

```yaml
jobs:
  android:
    name: Android (gradlew assembleRelease)
    env:
      ANDROID_KEYSTORE_BASE64:   ${{ secrets.ANDROID_KEYSTORE_BASE64 }}
      ANDROID_KEYSTORE_PASSWORD: ${{ secrets.ANDROID_KEYSTORE_PASSWORD }}
      ANDROID_KEY_ALIAS:         ${{ secrets.ANDROID_KEY_ALIAS }}
      ANDROID_KEY_PASSWORD:      ${{ secrets.ANDROID_KEY_PASSWORD }}
    steps:
      # ... checkout + JDK + Node + cap sync ...

      - name: Decode Android keystore (when secret present)
        id: keystore
        run: |
          if [ -n "$ANDROID_KEYSTORE_BASE64" ]; then
            echo "$ANDROID_KEYSTORE_BASE64" | base64 -d > "$RUNNER_TEMP/mahjong-autotable.keystore"
            echo "keystore-present=true" >> "$GITHUB_OUTPUT"
            echo "keystore-path=$RUNNER_TEMP/mahjong-autotable.keystore" >> "$GITHUB_OUTPUT"
          else
            echo "keystore-present=false" >> "$GITHUB_OUTPUT"
          fi

      - name: Gradle assembleRelease + bundleRelease (SIGNED)
        if: steps.keystore.outputs.keystore-present == 'true'
        run: |
          ./gradlew assembleRelease bundleRelease \
            "-Pandroid.injected.signing.store.file=$RUNNER_TEMP/mahjong-autotable.keystore" \
            "-Pandroid.injected.signing.store.password=$ANDROID_KEYSTORE_PASSWORD" \
            "-Pandroid.injected.signing.key.alias=$ANDROID_KEY_ALIAS" \
            "-Pandroid.injected.signing.key.password=$ANDROID_KEY_PASSWORD" \
            -x lint

      - name: Gradle assembleRelease + bundleRelease (UNSIGNED)
        if: steps.keystore.outputs.keystore-present != 'true'
        run: |
          ./gradlew assembleRelease bundleRelease \
            -Pandroid.injected.signing.store.file= \
            -Pandroid.injected.signing.store.password= \
            -Pandroid.injected.signing.key.alias= \
            -Pandroid.injected.signing.key.password= \
            -x lint
```

The two `Gradle assembleRelease + bundleRelease` steps are
**mutually exclusive** via `if: steps.keystore.outputs.keystore-present == 'true'` /
`!=`. Exactly one runs per job; the other reports `Skipped`
in the GitHub Actions UI.

**Why the inline secret-presence gate vs a job-level `if:`:**
the secret-presence test must run AFTER Node + JDK + Capacitor
sync (those steps don't need the secret), but BEFORE the
Gradle build. A job-level `if:` would skip the entire job
(including the artefact upload step) which would break
downstream consumers expecting the artefact path to exist.
The inline gate preserves the artefact-upload contract and
keeps the workflow's job graph stable across signed /
unsigned modes.

## 5. Verifying a signed build

After Stephen lands the four secrets and the next main-branch
push triggers `mobile-build.yml`, verify the signed artefact:

```bash
# Download the latest mobile-<run_number> prerelease APK.
gh release download "mobile-<N>" --pattern "*.apk" --dir /tmp/mobile-verify

# Check the APK signing certificate matches the keystore.
$ANDROID_BUILD_TOOLS/apksigner verify --verbose /tmp/mobile-verify/app-release.apk

# Expected output:
#   Verifies
#   Verified using v1 scheme (JAR signing): true
#   Verified using v2 scheme (APK Signature Scheme v2): true
#   Verified using v3 scheme (APK Signature Scheme v3): true
#   Number of signers: 1
#   Signer #1 certificate DN: CN=long2know, O=Mahjong Autotable, ...
#   Signer #1 certificate SHA-256 digest: <40-char SHA matching the keystore>
```

The certificate SHA-256 should match `keytool -list -v
-keystore mahjong-autotable.keystore` on Stephen's
workstation. A mismatch = the wrong keystore landed in the
`ANDROID_KEYSTORE_BASE64` secret; rotate per §6.

## 6. Disaster scenarios

### 6.1 Keystore secret wrong / leaked

* **Symptom:** APK signature SHA doesn't match the §5
  expected fingerprint, OR signing step fails with
  `Failed to load key from key store: keystore password was incorrect`.
* **Action:** delete + re-upload the four `ANDROID_*`
  secrets per §3. The next workflow run picks up the new
  values. **No app re-publish needed** because the
  underlying keystore on Stephen's workstation is unchanged
  — only the CI-side copy is rotated.
* **Time-to-recover:** ~5 minutes.

### 6.2 Keystore file lost (workstation hard-drive failure)

* **Symptom:** Stephen cannot generate a new
  `ANDROID_KEYSTORE_BASE64` because the original keystore
  file is gone.
* **Action:** restore from the 1Password backup
  (`mahjong-autotable / Play Store / keystore-file`). The
  backup is the canonical disaster-recovery surface for the
  signing identity.
* **Time-to-recover:** ~10 minutes (1Password download +
  re-upload to GitHub secret).

### 6.3 Keystore file lost AND 1Password backup lost

* **Symptom:** the signing identity is irretrievable.
* **Impact:** the app on the Play Store cannot receive
  updates under the existing package signature. Republishing
  under a new keystore = new Play Store listing, lost
  install base, new reviews, new download counter from zero.
* **Mitigation:** do not let this happen. The 1Password
  backup MUST be created at §2 keystore-generation time,
  and 1Password's "Vault Recovery" feature MUST be enabled
  on Stephen's vault. The W17 deliverable assumes
  6.3 is structurally prevented by the §2 backup discipline.

## 7. iOS — deferred to W18

iOS Distribution signing requires:

1. **Apple Developer Program enrolment** (annual fee, USD
   $99 — Stephen action).
2. **Distribution certificate** — created in App Store
   Connect, exported as a `.p12` with a passphrase.
3. **Provisioning Profile** — created in App Store Connect
   bound to the Distribution cert + the
   `io.mahjong.autotable` App ID + the device list (or
   App Store distribution profile for production builds).
4. **App Store Connect API key** — JSON Web Token credential
   for `fastlane` / `xcodebuild`'s `-allowProvisioningUpdates`
   workflow (or the more modern `-altool` upload path).

Five GitHub Secrets required for iOS signing:

* `IOS_DISTRIBUTION_CERT_BASE64`
* `IOS_DISTRIBUTION_CERT_PASSPHRASE`
* `IOS_PROVISIONING_PROFILE_BASE64`
* `IOS_APP_STORE_CONNECT_API_KEY_ID`
* `IOS_APP_STORE_CONNECT_API_KEY_BASE64`

The W18 deliverable will:

* Land an iOS-side analogue of the W17
  `Decode Android keystore` step.
* Land a `Decode iOS distribution cert + profile` step.
* Wire `-allowProvisioningUpdates` + the API key into
  `xcodebuild` (replacing the `CODE_SIGNING_ALLOWED=NO`
  shortcut the W2 workflow uses).
* Author `docs/mobile-ios-signing.md` (the iOS analogue of
  this doc).

W17 deliberately scopes to Android-only because the iOS
cert chain has more steps + more failure modes (Provisioning
Profile expiry, API key rotation cadence, Apple Distribution
cert annual renewal) that benefit from a dedicated wave's
attention. The W17 workflow's iOS job continues to take the
W2 `CODE_SIGNING_ALLOWED=NO` unsigned path until W18.

## 8. Cross-references

* [`docs/mobile-ci-bootstrap.md`](./mobile-ci-bootstrap.md) — W16 mobile-CI bootstrap + the W2 / W16 two-workflow contract.
* [`docs/mobile-release.md`](./mobile-release.md) — pre-W16 release-runbook (covers the Play Store + TestFlight upload paths).
* [`.github/workflows/mobile-build.yml`](../.github/workflows/mobile-build.yml) — the W2 / W17-amended release pipeline.
* [`mobile/android/`](../mobile/android/) — Capacitor-managed Gradle scaffolding (created by `npx cap add android`; not committed to the repo; regenerated each CI run).
* [Android Developer docs — Sign your app from the command line](https://developer.android.com/build/building-cmdline#sign_cmdline) — upstream reference for `-Pandroid.injected.signing.*` Gradle properties.
* [GitHub Actions Secrets docs — Repository secrets](https://docs.github.com/en/actions/security-guides/using-secrets-in-github-actions) — secret-upload UI reference.
