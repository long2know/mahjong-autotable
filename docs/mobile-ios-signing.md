# Mobile iOS signing — Apple Developer enrolment + CI secret runbook

> Phase K Wave 18 — Apone (DevOps). Companion to:
> [`docs/mobile-android-signing.md`](./mobile-android-signing.md)
> (the W17 Android-side counterpart),
> [`.github/workflows/mobile-build.yml`](../.github/workflows/mobile-build.yml)
> (the workflow this runbook provisions secrets for), and
> [`docs/mobile-release.md`](./mobile-release.md) (the
> downstream release / TestFlight / Play Internal flow).
>
> Audience: Operator (Stephen) enrolling the project in the
> Apple Developer Program for the first time. After enrolment,
> the secrets land in GitHub Actions and the mobile-build
> workflow's iOS job promotes from the W2 → W17 UNSIGNED .app
> output to the W18+ SIGNED .app output.

## 1. Why this doc exists

Wave 17 landed Android signing groundwork in `mobile-build.yml`
(per `docs/mobile-android-signing.md`): four `ANDROID_*`
secrets gate a SIGNED-RELEASE Gradle branch; absent secrets
fall back to the W2 → W16 UNSIGNED-RELEASE branch.

Wave 18 mirrors the same pattern for iOS:

* Four `IOS_*` secrets gate a SIGNED-RELEASE `xcodebuild`
  branch.
* Absent secrets fall back to the W2 → W17 UNSIGNED-RELEASE
  branch (`CODE_SIGNING_ALLOWED=NO`).
* CI soft-fails to UNSIGNED .app for PRs from forks (where
  secrets are unavailable by GitHub Actions design) and for
  any main-push before Stephen enrols in the Apple Developer
  Program.

This doc covers the Apple Developer enrolment + secret-
provisioning procedure the operator runs ONCE per signing
identity rotation.

## 2. Apple Developer Program enrolment

### 2.1 Account type

The Apple Developer Program is the prerequisite for
distributing iOS apps outside of Xcode's free local-signing
flow. Two membership tiers exist:

* **Individual** — $99 USD / year. Apps are attributed to the
  enrolling Apple ID's legal name.
* **Organization** — $99 USD / year + a D-U-N-S number for the
  legal entity. Apps are attributed to the organization name
  (the displayed "seller" on the App Store).

For `mahjong-autotable`, the Organization tier is preferred
once the project graduates from personal-account to squad-
hosted (Phase L+ scope). Phase K W18 lands the Individual
enrolment as the minimum-viable signing path; the
Organization migration is a documented downstream procedure
in `docs/mobile-release.md` §6.

### 2.2 Enrolment URL + waiting period

1. Sign in at <https://developer.apple.com/programs/enroll/>
   with the Apple ID that will own the signing identity.
2. Submit enrolment + payment.
3. Wait 24–48 hours for Apple to review the application.
   Organization enrolments routinely take 5–10 business days
   due to the D-U-N-S verification step.
4. Once approved, the Apple ID can access
   <https://developer.apple.com/account/> with the Developer
   tab visible.

### 2.3 Distribution identity creation

Once enroled:

1. <https://developer.apple.com/account/resources/certificates/list>
   → Create a new "Apple Distribution" certificate. Follow
   Apple's instructions to generate a CSR (Certificate
   Signing Request) from Keychain Access on a Mac.
2. Download the issued `.cer` file from Apple's portal.
3. Open the `.cer` file in Keychain Access to import the
   public certificate alongside the local private key
   generated from the CSR.
4. Right-click the cert + key pair in Keychain Access →
   "Export 2 items..." → save as a `.p12` file with a STRONG
   password. **This password is the value of
   `IOS_DEV_CERT_PASSWORD`** (§4).

### 2.4 App identifier + provisioning profile

1. <https://developer.apple.com/account/resources/identifiers/list>
   → Create a new App ID with the bundle identifier matching
   `mobile/capacitor.config.json` `appId` (currently
   `com.long2know.mahjong.autotable` per the Capacitor config).
2. <https://developer.apple.com/account/resources/profiles/list>
   → Create a new App Store distribution provisioning profile
   tied to the App ID + the distribution certificate from
   §2.3.
3. Download the issued `.mobileprovision` file.

The `.mobileprovision` is **the value of
`IOS_PROVISIONING_PROFILE_BASE64`** (§4) after base64
encoding.

## 3. Mac-side base64 encoding

The CI workflow consumes the `.p12` + `.mobileprovision` as
base64-encoded GitHub Action secrets. On the operator's Mac
(where the cert + profile were downloaded):

```bash
# Encode the .p12.
base64 -i mahjong-autotable-dist.p12 -o mahjong-autotable-dist.p12.base64

# Encode the .mobileprovision.
base64 -i mahjong-autotable-dist.mobileprovision -o mahjong-autotable-dist.mobileprovision.base64
```

The `-i` flag is the macOS-specific input-file form; Linux
operators using `base64 < file > file.base64` produce the
same output (no `\n` insertion needed — both forms emit a
single-line stream the CI decoder consumes).

## 4. GitHub Actions secret provisioning

Navigate to <https://github.com/long2know/mahjong-autotable/settings/secrets/actions>
and add the following four repository secrets:

| Secret name                       | Value                                                              | Source                          |
|-----------------------------------|--------------------------------------------------------------------|---------------------------------|
| `IOS_DEV_CERT_BASE64`             | Contents of `mahjong-autotable-dist.p12.base64` (§3)               | §2.3 distribution `.p12`        |
| `IOS_DEV_CERT_PASSWORD`           | The password set when exporting the .p12 in Keychain Access (§2.3) | Operator-generated, store in 1Password or equivalent password manager |
| `IOS_PROVISIONING_PROFILE_BASE64` | Contents of `mahjong-autotable-dist.mobileprovision.base64` (§3)   | §2.4 distribution profile       |
| `IOS_KEYCHAIN_PASSWORD`           | A FRESH random password used to lock the temporary CI keychain     | `openssl rand -base64 32` or 1Password-generated |

**`IOS_KEYCHAIN_PASSWORD` is NOT the same as `IOS_DEV_CERT_PASSWORD`** —
keep them distinct. The keychain password is single-job
ephemeral (the keychain is deleted at job teardown per the
workflow's `Tear down iOS keychain` step); the cert password
is long-lived (rotates only when the cert itself rotates).

**Do NOT commit the four secret values anywhere outside of
the GitHub Actions UI.** The `.p12.base64` and
`.mobileprovision.base64` files on the operator's Mac should
be deleted (or moved to a secure password manager attachment)
after the GitHub secret values are confirmed working.

## 5. CI workflow integration

`.github/workflows/mobile-build.yml` (W18 edit) consumes the
four secrets via the iOS job's `env:` block and the
`Decode iOS signing identity (when secrets present)` step:

```yaml
ios:
  name: iOS (xcodebuild Release)
  runs-on: macos-latest
  env:
    IOS_DEV_CERT_BASE64: ${{ secrets.IOS_DEV_CERT_BASE64 }}
    IOS_DEV_CERT_PASSWORD: ${{ secrets.IOS_DEV_CERT_PASSWORD }}
    IOS_PROVISIONING_PROFILE_BASE64: ${{ secrets.IOS_PROVISIONING_PROFILE_BASE64 }}
    IOS_KEYCHAIN_PASSWORD: ${{ secrets.IOS_KEYCHAIN_PASSWORD }}
  steps:
    # ... (checkout, web-bundle download, Capacitor sync)
    - name: Decode iOS signing identity (when secrets present)
      id: ios-keychain
      run: |
        if [ -n "${IOS_DEV_CERT_BASE64:-}" ] && [ -n ... ]; then
          # Decode .p12 + .mobileprovision into ${RUNNER_TEMP}
          # Create a temporary keychain locked with IOS_KEYCHAIN_PASSWORD
          # Import the cert; install the profile under
          #   $HOME/Library/MobileDevice/Provisioning Profiles
          echo "keychain-present=true" >> "$GITHUB_OUTPUT"
        else
          echo "keychain-present=false" >> "$GITHUB_OUTPUT"
        fi
    - name: xcodebuild Release (SIGNED)
      if: steps.ios-keychain.outputs.keychain-present == 'true'
      run: xcodebuild ... CODE_SIGN_STYLE=Manual
    - name: xcodebuild Release (UNSIGNED)
      if: steps.ios-keychain.outputs.keychain-present != 'true'
      run: xcodebuild ... CODE_SIGNING_ALLOWED=NO
    - name: Tear down iOS keychain
      if: always() && steps.ios-keychain.outputs.keychain-present == 'true'
      run: security delete-keychain "${{ steps.ios-keychain.outputs.keychain-path }}"
```

The `security delete-keychain` step is run with `if: always()`
so a build failure does NOT leak the temporary keychain into
the next reused job on the same runner (macOS runner state
lifetime is per-job by default, but the explicit delete is
defensive).

## 6. Failure modes + safety

### 6.1 Secret absent — UNSIGNED-RELEASE fallback

If ANY of the four `IOS_*` secrets is missing, the workflow's
gating step (`steps.ios-keychain.outputs.keychain-present`)
emits `false` and the `xcodebuild Release (UNSIGNED)` branch
runs. This is the W2 → W17 behaviour; the UNSIGNED .app
artefact still uploads to the `ios-artefacts` artifact set,
but cannot be installed on a non-jailbroken device or
submitted to TestFlight.

### 6.2 Wrong cert / wrong profile — xcodebuild error

If the cert + profile don't match the App ID, `xcodebuild`
fails with `Code Signing Error: No matching profile for
"...App ID..." found`. Operator action: verify
`mobile/capacitor.config.json` `appId` matches the App ID
that the provisioning profile is tied to; re-generate the
provisioning profile if needed.

### 6.3 Cert expiry

Apple Distribution certs expire after 1 year. The Hudson
`mobile-cert-expiry` panel (see `docs/mobile-release.md §4`)
SHOULD be added in a downstream wave to surface the expiry
date; W18 does NOT land that panel (it requires a Hudson-
side metric ingestion change out of W18 scope). Until then,
the operator MUST track expiry manually via the
<https://developer.apple.com/account/resources/certificates/list>
expiry column.

When the cert rotates, all four `IOS_*` secrets MUST be
re-provisioned (the keychain password can be regenerated; the
cert / profile / cert-password must come from the new
distribution identity).

### 6.4 Rotation cadence

Mirror the W17 Android-side recommendation (per
`docs/mobile-android-signing.md §6`): rotate the iOS
distribution identity on a 12-month cadence aligned to the
Apple-side expiry, or IMMEDIATELY on any suspected leak.

## 7. Verify

### 7.1 Static — actionlint

```bash
.tool-actionlint/actionlint .github/workflows/mobile-build.yml
```

Exit 0.

### 7.2 Dynamic — mobile-build CI run

Trigger the workflow against a feature branch (where the
four `IOS_*` secrets are accessible — branch protection
allows secret access for non-fork PRs):

```bash
gh workflow run mobile-build.yml --ref <feature-branch>
```

Then inspect the iOS job log:

* "iOS keychain decoded — SIGNED-RELEASE path will run"
  confirms the secret-detection step's positive branch.
* The `xcodebuild Release (SIGNED)` step runs (and the
  `xcodebuild Release (UNSIGNED)` step is `skipped`).
* The "Tear down iOS keychain" step's exit code is `0`.

### 7.3 Artefact inspection (post-run)

Download the `ios-artefacts` artifact. Inspect the .app's
embedded provisioning profile:

```bash
unzip -p ios-artefacts.zip App.app/embedded.mobileprovision \
  | security cms -D \
  | grep -E "<key>UUID</key>" -A 1
```

The UUID should match the operator's distribution profile.

## 8. App Store / TestFlight submission

W18 does NOT automate App Store submission. The SIGNED .app
artefact is the input to a manual `xcrun altool` (or Xcode-UI)
TestFlight upload — see `docs/mobile-release.md §3` for the
manual upload procedure. Auto-promotion to TestFlight is a
Phase L+ scope.

## 9. What W18 does NOT change

* The W2 → W17 UNSIGNED-RELEASE fallback path is unchanged
  and remains the default for PRs from forks.
* The mobile-build workflow's `release` job (the
  GitHub-prerelease publish) is unchanged — both the SIGNED
  and UNSIGNED .app artefacts upload under the same
  `ios-artefacts` artifact name.
* The Android signing flow (W17) is unchanged.
* The Capacitor 6 + CocoaPods build invocation shape is
  unchanged — the W18 delta is the signing identity
  injection, NOT the build command.
* The Apple Developer Program enrolment is a one-time
  operator action; no recurring CI cost is added.

## 10. Cross-references

* `.github/workflows/mobile-build.yml` (lines 202+) — the
  iOS job with the W18 signing groundwork.
* `docs/mobile-android-signing.md` — W17 Android counterpart.
* `docs/mobile-release.md` — manual TestFlight / Play Internal
  submission runbook.
* `docs/mobile-ci-bootstrap.md` — the W2 CI bootstrap that
  this W18 work extends.
* `CHANGELOG.md` 0.27.0 — W18 entry recording the iOS signing
  groundwork.
