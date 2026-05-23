# Mobile release flow — TestFlight + Play Internal Testing

> Phase K Wave 6 — Apone (DevOps).

This runbook covers the tag-driven internal-testing promotion of
the Mahjong Autotable Capacitor mobile app (`mobile/`) to:

* **iOS** — TestFlight (App Store Connect → Internal Testing track).
* **Android** — Play Console → Internal Testing track.

## 1. Release flow

```
                     ┌───────────────────────────┐
                     │  mobile/** PR → main      │
                     │  (mobile-build.yml runs)  │
                     └────────────┬──────────────┘
                                  │
                                  ▼
                     ┌───────────────────────────┐
                     │  Operator cuts mobile tag │
                     │  git tag mobile-v0.15.0   │
                     │  git push origin <tag>    │
                     └────────────┬──────────────┘
                                  │
                                  ▼
       ┌──────────────────────────────────────────────────┐
       │  mobile-internal-testing.yml runs on the tag     │
       │                                                  │
       │  ┌────────────┐   ┌────────────┐                 │
       │  │ Android    │   │ iOS        │                 │
       │  │ bundleRel  │   │ gym + sign │                 │
       │  │ signed AAB │   │ signed IPA │                 │
       │  └─────┬──────┘   └─────┬──────┘                 │
       │        │                │                        │
       │        ▼                ▼                        │
       │  fastlane supply   fastlane pilot                │
       │  → Play Internal   → TestFlight                  │
       └──────────────────────────┬───────────────────────┘
                                  │
                                  ▼
                     ┌───────────────────────────┐
                     │  Slack notification       │
                     │  (#mobile-releases)       │
                     └───────────────────────────┘
```

## 2. Tag conventions

* **Tag pattern.** `mobile-v<MAJOR>.<MINOR>.<PATCH>`.
* **Cadence.** Independent of the backend's `vX.Y.Z` semver — the
  mobile shell ships at a different rhythm.
* **Branch.** Always tag the merge commit on `main`; the
  `mobile-internal-testing.yml` workflow checks out the tag SHA
  and rebuilds from a clean state.
* **Atomicity.** ONE tag = ONE upload pair (Android + iOS).
  Failed uploads can be re-run via `workflow_dispatch` with the
  same tag.

## 3. Code-signing setup (one-time, operator)

### 3.1 iOS — TestFlight

1. **Enrol in the Apple Developer Program** ($99/year).
2. **Create a distribution certificate** + **provisioning profile**
   in App Store Connect. Profile type: `App Store`. Bundle ID:
   `com.long2know.mahjong` (or your overridden value — see §4).
3. **Create an App Store Connect API key** with the `App Manager`
   role. Download the `.p8` file (one-time).
4. Encode each artefact as base64 + add to repo secrets:

    ```bash
    base64 -i AuthKey_XXXXXXXX.p8 | gh secret set APPLE_API_KEY_BASE64
    base64 -i dist.p12             | gh secret set IOS_DIST_CERT_BASE64
    base64 -i profile.mobileprovision | gh secret set IOS_PROVISION_PROFILE_BASE64
    gh secret set APPLE_API_KEY_ID         # the 10-char ID
    gh secret set APPLE_API_ISSUER_ID      # the UUID from App Store Connect
    gh secret set IOS_DIST_CERT_PASSWORD   # .p12 password
    gh secret set IOS_KEYCHAIN_PASSWORD    # any random string — ephemeral
    ```

### 3.2 Android — Play Console

1. **Generate a release keystore** (one-time; rotate after key
   compromise but NOT routinely — Play Console pins by the
   signing-key's cert hash).

    ```bash
    keytool -genkeypair \
        -alias release \
        -keyalg RSA \
        -keysize 2048 \
        -validity 10000 \
        -keystore release.jks
    # Note the keystore password + key password.
    ```

2. **Upload the cert** to the new Play Console app (if first
   release). Play Console will sign the deliverable on upload
   from W6+ via Play App Signing — keep the local keystore as
   the *upload key* (not the final signing key).

3. **Create a Play Console service account** with the
   `androidpublisher` edit role:
   - GCP console → IAM → Service accounts → create one for this
     project; grant it the `androidpublisher` role.
   - Generate a JSON key, download.
   - Add to repo secrets:

    ```bash
    base64 -i release.jks | gh secret set ANDROID_KEYSTORE_BASE64
    base64 -i play-sa.json | gh secret set PLAY_SERVICE_ACCOUNT_JSON
    gh secret set ANDROID_KEYSTORE_PASSWORD
    gh secret set ANDROID_KEY_ALIAS      # 'release' if you followed §3.2.1
    gh secret set ANDROID_KEY_PASSWORD
    ```

### 3.3 Slack

```bash
gh secret set SLACK_WEBHOOK_URL   # https://hooks.slack.com/services/...
```

The `notify` job soft-fails when this secret is absent (forks +
first-time setups don't block on Slack).

## 4. Workflow dispatch (manual re-run)

If the tag-push workflow run fails mid-way (e.g. TestFlight
returned 503), re-run via dispatch:

```bash
gh workflow run mobile-internal-testing.yml \
    -f tag=mobile-v0.15.0
```

The workflow checks out the tag SHA, rebuilds, and re-uploads.
Play Console + TestFlight de-dupe by `(packageName, versionCode)`;
to push a NEW build to the same tag, bump the `versionCode` /
`CFBundleVersion` in `mobile/capacitor.config.json` + push a
follow-up tag (`mobile-v0.15.0+1` is reserved syntax — use
`mobile-v0.15.1` instead).

## 5. TestFlight beta-tester management

TestFlight has two tester scopes:

| Scope | Audience | Setup |
|-------|----------|-------|
| **Internal Testing** | Up to 100 Apple Developer Program team members | Auto-promoted by `fastlane pilot` (skip_submission:true) |
| **External Testing** | Up to 10 000 users | Requires App Review (24-72h); flip `distribute_external:true` in pilot args when ready |

The W6 workflow targets **Internal Testing only**. External
Testing promotion is operator-driven (manual click in App Store
Connect) — see §5.2.

### 5.1 Add an internal tester

App Store Connect → Users and Access → Users → invite via email →
assign the `Developer` role → opt them into TestFlight. They
receive a TestFlight invitation within 1 h of an upload.

### 5.2 Promote to external (Phase L)

Once internal testing surfaces no P0/P1 bugs:

1. App Store Connect → TestFlight → external groups → add the
   release build.
2. Submit for Beta App Review (Apple, ~24 h).
3. Public link auto-generated → distribute to ~10 000 testers.

Phase L scope; not automated in W6.

## 6. Play Internal Testing tester management

| Scope | Audience | Setup |
|-------|----------|-------|
| **Internal Testing** | Up to 100 testers (members of testing groups linked to the track) | `fastlane supply --track internal` |
| **Closed Testing** | Tester groups of arbitrary size | Operator click in Play Console |
| **Open Testing** | Public + auto-rolled out | Phase L+ |

### 6.1 Add an internal tester

Play Console → Testing → Internal testing → Testers → email list.
The W6 workflow uploads with `release_status: draft` so the
operator can review before the testers see anything; flip to
`release_status: completed` in the workflow YAML to auto-roll-out.

## 7. Verifying a release

After the workflow run completes:

```bash
# Latest TestFlight build:
xcrun altool --list-builds \
    --apiKey "$APPLE_API_KEY_ID" \
    --apiIssuer "$APPLE_API_ISSUER_ID"
# Or via the API:
fastlane pilot list

# Latest Play Internal build:
gcloud --project "$GCP_PROJECT" \
    auth activate-service-account --key-file play-sa.json
# (or fastlane supply --query) — easier: check the Play Console UI.
```

## 8. Troubleshooting

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| `Error: missing IOS_DIST_CERT_BASE64` workflow warning | Secret not set on the repo | §3.1 — re-encode + gh secret set |
| `gym` fails with `Code signing is required for product type 'Application'` | Provisioning profile UUID mismatch | Decode the `.mobileprovision` and confirm its `UUID` key matches the profile in `mobile/ios/App/App.xcodeproj/project.pbxproj` |
| `fastlane supply` fails with `Cannot upload AAB — versionCode already exists` | Same version uploaded twice | Bump `versionCode` in `mobile/capacitor.config.json` + push new tag |
| Slack notification missing | `SLACK_WEBHOOK_URL` secret absent | §3.3 |
| Workflow run shows `have-secrets=false` | Fork PR (cannot read secrets) | Expected — fork PRs cannot push to TestFlight / Play |

## 9. Cross-references

* `.github/workflows/mobile-build.yml` — W2 unsigned-build CI (every PR).
* `.github/workflows/mobile-internal-testing.yml` — this workflow.
* `mobile/capacitor.config.json` — Capacitor app metadata (bundle ID, version).
* `docs/secret-rotation.md` — rotation cadence for signing identities.
