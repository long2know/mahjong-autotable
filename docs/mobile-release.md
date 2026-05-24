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

## 4a. External testing flow (Phase K Wave 7 — Apone)

[`mobile-external-testing.yml`](../.github/workflows/mobile-external-testing.yml)
is the operator-driven promotion of an existing Internal build
to External Testing on **both** Apple + Google distribution
surfaces. It is NEVER triggered by a tag push — only by manual
`workflow_dispatch`.

### 4a.1 When to run

After Internal Testing soak has surfaced no P0/P1 bugs (typically
24–72 h of in-team use against a tagged build), promote to
External Testing for the broader beta cohort. Auto-promotion on
every tag is intentionally NOT shipped because:

* The first External-Testing distribution of a new iOS version
  triggers a 24-h Apple Beta App Review, and re-triggering
  CANNOT cancel a review in flight.
* External testers receive an email on promotion; pushing
  half-baked builds erodes their willingness to upgrade.
* Release notes for External testers are user-facing copy that
  warrants careful authoring — the workflow takes them as a
  required input.

### 4a.2 Inputs

| Input | Required | Default | Notes |
|---|---|---|---|
| `tag` | yes | — | `mobile-vMAJOR.MINOR.PATCH` — MUST be a tag that mobile-internal-testing.yml has already processed. |
| `release_notes` | yes | — | ≤4000 chars; pushed to both TestFlight and Play. |
| `ios_external_groups` | no | `External-Beta` | Comma-separated TestFlight External Group names (must already exist in App Store Connect). |
| `android_track` | no | `beta` | Play Console Closed Testing track ID. |
| `release_status` | no | `draft` | `draft` / `completed` / `inProgress` — gates Play roll-out. |

### 4a.3 Trigger

```bash
gh workflow run mobile-external-testing.yml \
    -f tag=mobile-v0.16.0 \
    -f release_notes="$(cat release-notes.md)" \
    -f ios_external_groups="External-Beta,Power-Users" \
    -f android_track="beta" \
    -f release_status="draft"
```

### 4a.4 What it does

| Platform | Promotion mechanism | Notes |
|---|---|---|
| iOS — TestFlight | `fastlane pilot distribute --build_number latest --distribute_external true --notify_external_testers true --groups <list>` | Builds previously uploaded by mobile-internal-testing.yml. First External distribution of a new build triggers Beta App Review (~24 h, Apple-managed). |
| Android — Play | `fastlane supply --track internal --track_promote_to <DEST>` | Promotes the most recent build on the Internal track to a Closed Testing track WITHOUT re-uploading the AAB (`(packageName, versionCode)` uniqueness prevents re-upload). Changelog written to `fastlane/metadata/android/en-US/changelogs/default.txt`. |

Both jobs **soft-fail** on missing secrets — if `PLAY_SERVICE_ACCOUNT_JSON`
or any of `APPLE_API_KEY_*` are absent (e.g. fork PR), the
matching job logs a warning and exits 0. Operator-driven
dispatches from `main` always have the secrets.

### 4a.5 Apple Beta App Review

The FIRST External-Testing distribution of a new iOS version
triggers Apple's Beta App Review. Typical turnaround:

* **First version of a calendar quarter:** 24–72 h.
* **Subsequent builds of the same version:** auto-approved within
  ~30 min (Apple skips re-review until the marketing version
  changes).
* **Rejected build:** Apple posts feedback in App Store Connect.
  Operator fixes the issue + pushes a new `mobile-vX.Y.Z+1` tag
  (NOT a workflow re-run) → mobile-internal-testing.yml uploads
  the new build → re-trigger this workflow to re-promote.

### 4a.6 Rolling back an External promotion

There is no API to retract an External-Testing build once
testers have been notified. Mitigation:

* **TestFlight:** App Store Connect → TestFlight → expire the
  specific build (testers see the "this build has expired"
  banner; install link becomes inactive).
* **Play Closed Testing:** Play Console → Testing → Closed
  testing → halt rollout (manual step; the workflow's
  `release_status: draft` default lets you do this BEFORE
  testers see anything).

### 4a.7 Tester management

External testers are added in the platform consoles (NOT in this
repo):

* **TestFlight External Groups** — App Store Connect → TestFlight
  → External Groups → create group → add testers by email →
  enable "automatic notifications for new builds". The
  group name passed in `ios_external_groups` must match exactly.
* **Play Closed Testing tracks** — Play Console → Testing →
  Closed testing → manage testers (Google Group or email list).

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

### 5.2 Promote to external (Wave 7 — automated)

Once internal testing surfaces no P0/P1 bugs, trigger the W7
external promotion workflow (see §4a for the full operator
flow):

```bash
gh workflow run mobile-external-testing.yml \
    -f tag=mobile-v0.16.0 \
    -f release_notes="$(cat release-notes.md)"
```

The workflow handles:

1. Distributing the build to the named TestFlight External Group(s).
2. Submitting for Beta App Review (Apple, ~24 h on first
   External distribution of a new version).
3. Promoting the corresponding Play build to a Closed Testing
   track.

External testers receive a TestFlight email once Beta App Review
passes; Play Closed Testing testers receive an email
immediately (or when the operator flips `release_status` from
`draft` to `completed`).

## 6. Play Internal Testing tester management

| Scope | Audience | Setup |
|-------|----------|-------|
| **Internal Testing** | Up to 100 testers (members of testing groups linked to the track) | `fastlane supply --track internal` |
| **Closed Testing** | Tester groups of arbitrary size | Operator dispatches `mobile-external-testing.yml` — Wave 7 (see §4a) |
| **Open Testing** | Public + auto-rolled out | Phase L+ |

### 6.1 Add an internal tester

Play Console → Testing → Internal testing → Testers → email list.
The W6 workflow uploads with `release_status: draft` so the
operator can review before the testers see anything; flip to
`release_status: completed` in the workflow YAML to auto-roll-out.

## 7. Production track promotion (Phase K Wave 8 — Apone)

Wave 8 closes the promotion ladder. The full path from PR to
public release is:

```
            ┌──────────────────────────────┐
            │ mobile/** PR → main          │
            │ mobile-build.yml (every PR)  │
            │ (unsigned smoke build)       │
            └─────────────┬────────────────┘
                          │
                          ▼  operator cuts tag `mobile-v0.17.0`
            ┌──────────────────────────────┐
            │ mobile-internal-testing.yml  │
            │ (W6 — tag-driven)            │
            │ → TestFlight Internal        │
            │ → Play Internal Testing      │
            └─────────────┬────────────────┘
                          │
                          ▼  operator dispatches workflow
            ┌──────────────────────────────┐
            │ mobile-external-testing.yml  │
            │ (W7 — workflow_dispatch)     │
            │ → TestFlight External Groups │
            │ → Play Closed Testing track  │
            └─────────────┬────────────────┘
                          │
                          ▼  internal beta cohort signs off
                          │  operator cuts tag `mobile-prod-v0.17.0`
            ┌──────────────────────────────┐
            │ mobile-production-release.yml│
            │ (W8 — tag + env-gated)       │
            │ env: release-channel-prod    │
            │ → App Store Production       │
            │ → Play Production track      │
            │   (10% staged → 100%)        │
            └──────────────────────────────┘
```

Three workflows; three tag spaces; one shared secret set. The
W8 workflow lives at
[`.github/workflows/mobile-production-release.yml`](../.github/workflows/mobile-production-release.yml).

### 7.1 Tag space

| Tag prefix | Workflow | Audience |
|---|---|---|
| `mobile-v*.*.*` | `mobile-internal-testing.yml` | TestFlight Internal + Play Internal Testing (dev team) |
| (no tag — workflow_dispatch on `main`) | `mobile-external-testing.yml` | TestFlight External Groups + Play Closed Testing |
| `mobile-prod-v*.*.*` | `mobile-production-release.yml` | App Store Production + Play Production |

The prefix split is intentional — a `mobile-prod-v*.*.*` push does
NOT trigger Internal Testing. The two tag namespaces are disjoint
by prefix; `git log --tags` shows the production ladder in
distinct labels.

### 7.2 Hotfix path (Phase K Wave 9 — Apone)

The W8 production-release workflow assumes the **happy path**:
Internal Testing tag → External-Testing soak (≥ 7 days) →
production tag. That window is correct for routine releases but
**unworkable for security or revenue-impacting hotfixes** where
24-h external soak adds 24 h of customer exposure.

W9 adds a **separate** workflow,
[`.github/workflows/mobile-production-hotfix.yml`](../.github/workflows/mobile-production-hotfix.yml),
that **bypasses External-Testing** with explicit operator
acknowledgement.

**Triggers:**

- Tag push matching `mobile-hotfix-v*.*.*` (separate tag
  namespace from `mobile-prod-v*.*.*` so a hotfix doesn't get
  confused with a routine cut in `git log --tags`).
- `workflow_dispatch` on `main` with operator-supplied `version`,
  `internal_tag`, and `hotfix_reason`.

**Env-gate — two-reviewer rule:**

Where the routine production-release workflow uses environment
`release-channel-production` (one reviewer), the hotfix workflow
uses **`release-channel-production-hotfix`** with **two**
required reviewers. The intent: skipping External-Testing
demands a second pair of eyes on the decision, not just on the
output.

Provision the environment in the GitHub UI (operator action,
one-time):

```
Settings → Environments → New environment "release-channel-production-hotfix"
  Required reviewers: 2  (select 3+ named approvers to avoid
                          single-point bottlenecks)
  Wait timer: 0          (every minute matters during a hotfix)
  Deployment branches: main only
```

**Audit-trail guarantees:**

The workflow emits THREE durable audit-trail markers per run:

1. A `::warning::` line in the job log (visible in the Actions
   UI summary) calling out that External-Testing was skipped:
   `::warning::HOTFIX PATH — External-Testing skipped. Reason: <reason>. Reviewers: <list>.`
2. A `step-summary` banner (markdown rendered at the top of the
   run page) with the hotfix reason verbatim + a link back to
   this section of the docs.
3. A Slack notification on the `#mobile-releases` channel
   (`SLACK_WEBHOOK_URL` secret) with the hotfix reason embedded
   — so audit reviewers don't need to dig through the Actions
   log to reconstruct WHY the cut bypassed soak.

The hotfix reason is REQUIRED — the `workflow_dispatch` input is
non-empty-validated, and the tag-push path reads the reason from
the tag annotation (`git tag -a mobile-hotfix-v0.17.1 -m "<reason>"`).

**Internal-tag validation:**

Even on the hotfix path, the cut MUST originate from a build
that landed in **Internal Testing**. The `prepare` job validates
that the supplied `internal_tag` exists as a ref and that its
commit matches the workflow's checkout SHA. This catches the
class of mistake where an operator cuts a hotfix from an
uncommitted local branch — that build would never have run
through the W6 mobile-internal-testing acceptance gates.

**Default rollout posture — full, not staged:**

The W8 routine cut starts Android at 10% staged rollout (see
§7.6). The W9 hotfix defaults to **100% rollout + `status:
completed`** because a hotfix that's good enough to skip soak is
good enough to fully replace the broken build immediately. The
operator can override via the `android_rollout_fraction` input
on workflow_dispatch.

On iOS, the hotfix submits with `automatic_release=true` (auto-
release once approved). The operator should request **Expedited
App Review** out-of-band via App Store Connect — see Apple's
[Expedited Review documentation](https://developer.apple.com/contact/app-store/?topic=expedite).
The workflow does NOT request expedited review programmatically
(no public API).

**When to use the hotfix path:**

| Scenario | Hotfix? | Why |
|---|---|---|
| RCE / auth-bypass / data-loss bug | YES | Customer harm exceeds soak benefit |
| Crash on launch affecting > 1% of installs | YES | App is effectively down |
| Crash on launch affecting < 1%, no data loss | NO | Soak 24 h via accelerated External-Testing instead |
| Revenue-blocking checkout flow | YES | Direct $ impact |
| UX paper-cut (typo, layout glitch) | NO | Routine release ladder is correct |
| Compliance-mandated takedown (GDPR / DMCA) | YES | Legal obligation |
| Subscription pricing bug | judgement call | Discuss with finance + legal first |

When in doubt, **route through External-Testing**. The hotfix
path's cost is permanent — the audit trail records every
bypass, and a high bypass rate erodes trust in the soak
process.

### 7.3 Pre-flight (operator)

Before cutting a production tag:

1. **Confirm the internal beta cohort has signed off** — the
   External-Testing build (W7) has been in the wild long enough
   to surface P0/P1 bugs. The W8 retro committed to a minimum
   internal soak window of 7 days for routine releases, 24 h
   for security hotfixes.
2. **Author user-facing release notes** — max 4000 chars
   (TestFlight + Play both reject longer blobs). The W8
   workflow input `release_notes` carries the user-facing copy
   verbatim into both stores.
3. **Confirm `release-channel-production` GitHub Environment
   approvers are reachable** — the W8 workflow's first job is
   env-gated. A run will sit pending approval indefinitely;
   weekend cuts need someone on PagerDuty.

### 7.4 Cut + promote

```bash
# 1. Tag the production release. The version MUST match the
#    `mobile-v*.*.*` tag whose build is being promoted (the
#    workflow validates this).
git tag mobile-prod-v0.17.0
git push origin mobile-prod-v0.17.0

# 2. The push triggers mobile-production-release.yml. The first
#    job (prepare) is env-gated — a reviewer approves via
#    https://github.com/long2know/mahjong-autotable/actions →
#    pending environment approval.

# 3. Once approved, downstream jobs (android-production +
#    ios-production) run in parallel. iOS submits for App
#    Review (~24 h on first submission of a version); Android
#    promotes to a staged Production rollout (10% by default).

# 4. Slack notification fires once both store jobs complete.
```

### 7.5 workflow_dispatch (manual promotion from existing tag)

If the tag already exists (e.g. re-running after a transient
fastlane failure):

```bash
gh workflow run mobile-production-release.yml \
    -f tag=mobile-prod-v0.17.0 \
    -f internal_tag=mobile-v0.17.0 \
    -f release_notes="$(cat release-notes.md)" \
    -f android_rollout_fraction=0.10 \
    -f android_release_status=inProgress \
    -f ios_automatic_release=true
```

The `internal_tag` input is required so the workflow can
validate the originating Internal-Testing tag exists. The
default `android_rollout_fraction=0.1` means 10% of users get
the new build initially; subsequent workflow_dispatch runs with
a higher fraction bump the rollout.

### 7.6 Staged rollout — Android

Play Production supports staged rollout. The W8 baseline:

| Day | Rollout fraction | How |
|---|---|---|
| T+0 | 10% | First push: `android_rollout_fraction=0.10`, `release_status=inProgress` |
| T+2 (no P0 metrics) | 25% | Re-run workflow with `android_rollout_fraction=0.25` |
| T+4 | 50% | Re-run with 0.50 |
| T+7 | 100% | Re-run with `android_release_status=completed` |

If a P0 surfaces mid-staged rollout, halt with:

```bash
gh workflow run mobile-production-release.yml \
    -f tag=mobile-prod-v0.17.0 \
    -f internal_tag=mobile-v0.17.0 \
    -f release_notes="(halt rollout)" \
    -f android_rollout_fraction=0.0 \
    -f android_release_status=inProgress
```

`--rollout 0.0` stops further roll-out without unpublishing
the build from users who already received it.

### 7.7 Staged rollout — iOS

App Store has NO direct staged rollout for fresh releases
(only phased release for >7-day distributions). The W8 workflow
flips `--automatic_release=true` by default so the release goes
live the moment App Review passes.

To gate manual release (Apple's "Pending Developer Release"):

```bash
# Run with automatic_release=false; review still happens but
# the operator clicks "Release this version" in App Store
# Connect to publish.
gh workflow run mobile-production-release.yml \
    -f ios_automatic_release=false \
    ... (other inputs)
```

### 7.8 Env approval

`mobile-production-release.yml` declares
`environment: { name: release-channel-production }` on the
`prepare` job. The GitHub Environment object (configured in
repo Settings → Environments) MUST have:

* Required reviewers — at least one of `@long2know`, the on-call
  operator role.
* Deployment branches — restricted to `main` (production
  releases must come off the main branch).
* No wait-timer — Apple's App Review latency dominates; no need
  for a workflow-level delay.

Once an approver clicks "Approve and deploy", the workflow
proceeds without further per-job approval.

### 7.9 Rollback

| Store | Possible? | How |
|---|---|---|
| Play Production | Yes (halt) | Set `android_rollout_fraction=0.0` via workflow_dispatch — stops further rollout, doesn't recall builds already installed. |
| Play Production | Yes (revert) | Re-promote the PRIOR `mobile-prod-v` tag's build (Play retains prior builds). |
| App Store | No (no revert) | Submit a hotfix build with a higher version number — Apple has no direct revert. |

The W8 retro action item: build a "mobile-production-hotfix"
workflow that takes a version + a known-good prior tag, cuts a
patch tag (`mobile-prod-v0.17.1`), and submits within the env
gate. Scheduled for W9.

## 8. Verifying a release

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

## 9. Troubleshooting

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| `Error: missing IOS_DIST_CERT_BASE64` workflow warning | Secret not set on the repo | §3.1 — re-encode + gh secret set |
| `gym` fails with `Code signing is required for product type 'Application'` | Provisioning profile UUID mismatch | Decode the `.mobileprovision` and confirm its `UUID` key matches the profile in `mobile/ios/App/App.xcodeproj/project.pbxproj` |
| `fastlane supply` fails with `Cannot upload AAB — versionCode already exists` | Same version uploaded twice | Bump `versionCode` in `mobile/capacitor.config.json` + push new tag |
| Slack notification missing | `SLACK_WEBHOOK_URL` secret absent | §3.3 |
| Workflow run shows `have-secrets=false` | Fork PR (cannot read secrets) | Expected — fork PRs cannot push to TestFlight / Play |

## 10. Cross-references

* `.github/workflows/mobile-build.yml` — W2 unsigned-build CI (every PR).
* `.github/workflows/mobile-internal-testing.yml` — Internal Testing tag-driven workflow.
* `.github/workflows/mobile-external-testing.yml` — **Wave 7** External Testing promotion (operator-driven; §4a).
* `.github/workflows/mobile-production-release.yml` — **Wave 8** Production track promotion (tag + env-gated; §7).
* `mobile/capacitor.config.json` — Capacitor app metadata (bundle ID, version).
* `docs/secret-rotation.md` — rotation cadence for signing identities.
