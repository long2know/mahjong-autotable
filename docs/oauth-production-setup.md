# OAuth production setup — Google + GitHub + Microsoft

Phase K Wave 2 (Apone, DevOps). NEW for Wave 2: the Microsoft section
(below, §3) unblocks Bishop's Wave 3 OAuth middleware. Google + GitHub
sections were shipped in earlier phases and are consolidated here as
the authoritative production runbook.

> **Scope:** This document is OPERATOR-facing. DevOps does NOT touch
> production OAuth client secrets — they are provisioned out-of-band
> by the operator into AWS SSM and ESO (`docs/secret-rotation.md`)
> binds them to the API at runtime.

## 1. Contract summary

| Provider  | SSM family                            | API env vars                                                            | Redirect URI                              |
| --------- | ------------------------------------- | ----------------------------------------------------------------------- | ----------------------------------------- |
| Google    | `/mahjong/prod/oauth/google/*`        | `Authentication__Google__ClientId`<br>`Authentication__Google__ClientSecret` | `https://mahjong.example.com/api/auth/callback/google`    |
| GitHub    | `/mahjong/prod/oauth/github/*`        | `Authentication__GitHub__ClientId`<br>`Authentication__GitHub__ClientSecret` | `https://mahjong.example.com/api/auth/callback/github`    |
| Microsoft | `/mahjong/prod/oauth/microsoft/*`     | `Authentication__Microsoft__ClientId`<br>`Authentication__Microsoft__ClientSecret`<br>`Authentication__Microsoft__TenantId` | `https://mahjong.example.com/api/auth/callback/microsoft` |

All three providers wire into the existing `oauth-secrets`
ExternalSecret in `infra/k8s/overlays/prod/secret-template.yaml`.

## 2. Google OAuth 2.0

### 2.1 Console steps

1. Visit `https://console.cloud.google.com/apis/credentials`.
2. Project: `mahjong-autotable-prod` (create if absent).
3. Click **Create Credentials → OAuth client ID**.
4. Application type: **Web application**.
5. Name: `Mahjong Autotable — Production`.
6. **Authorized redirect URIs:** `https://mahjong.example.com/api/auth/callback/google`.
7. Authorized JavaScript origins: `https://mahjong.example.com`.
8. Click **Create** → copy `Client ID` + `Client secret`.

### 2.2 Scopes

The API requests `openid email profile`. Configure the consent screen:

* APIs & Services → OAuth consent screen → **Production**.
* User type: **External** (consumer Google accounts).
* Scopes: `.../auth/userinfo.email`, `.../auth/userinfo.profile`,
  `openid`. NOTHING ELSE — least-privilege.
* Authorized domains: `mahjong.example.com`.

### 2.3 SSM provisioning

```bash
aws ssm put-parameter \
    --name /mahjong/prod/oauth/google/client_id \
    --type SecureString \
    --value '<copied Client ID>'

aws ssm put-parameter \
    --name /mahjong/prod/oauth/google/client_secret \
    --type SecureString \
    --value '<copied Client secret>'
```

## 3. GitHub OAuth

### 3.1 Console steps

1. Visit `https://github.com/settings/applications/new`
   (or the org-level equivalent at
   `https://github.com/organizations/<org>/settings/applications/new`).
2. Application name: `Mahjong Autotable — Production`.
3. Homepage URL: `https://mahjong.example.com`.
4. **Authorization callback URL:** `https://mahjong.example.com/api/auth/callback/github`.
5. Click **Register application**.
6. Click **Generate a new client secret** → copy the secret (shown
   once — store it immediately).

### 3.2 Scopes

The API requests `read:user user:email`. GitHub does NOT issue an
OIDC `id_token` for the OAuth (non-Apps) flow — the API's nonce
check is a no-op on the GitHub provider and the email is fetched
via a separate `GET /user/emails` call.

### 3.3 SSM provisioning

```bash
aws ssm put-parameter \
    --name /mahjong/prod/oauth/github/client_id \
    --type SecureString \
    --value '<copied Client ID>'

aws ssm put-parameter \
    --name /mahjong/prod/oauth/github/client_secret \
    --type SecureString \
    --value '<copied Client secret>'
```

## 4. Microsoft Identity Platform (NEW — Wave 2)

> **Unblocks Wave 3.** Bishop adds the Microsoft provider middleware
> in Wave 3 (`Authentication.AddMicrosoftAccount(...)`). These SSM
> keys + console steps MUST be in place before Wave 3 deploys, or
> the Microsoft sign-in button will return a config error.

### 4.1 Console steps (Azure portal)

1. Visit `https://portal.azure.com` → **Azure Active Directory** (now
   "Microsoft Entra ID").
2. **App registrations → New registration**.
3. Name: `Mahjong Autotable — Production`.
4. **Supported account types:** **Accounts in any organizational
   directory (Any Microsoft Entra ID tenant — Multitenant) and
   personal Microsoft accounts (e.g. Skype, Xbox)**.
   * This sets `tenant_id=common`. We want consumer MSA accounts +
     all org tenants. If you ONLY want corporate accounts, choose
     "single tenant" and use that tenant's UUID instead.
5. **Redirect URI:** type = **Web**, value =
   `https://mahjong.example.com/api/auth/callback/microsoft`.
6. Click **Register** → land on the app overview page.
7. Copy the **Application (client) ID** — this is your `client_id`.
8. **Certificates & secrets → New client secret**:
   * Description: `mahjong-autotable-prod`.
   * Expires: `24 months` (rotation cadence is quarterly per §6, but
     the max lifetime cap is operator-set here).
   * Click **Add** → copy the **Value** (NOT the Secret ID) — shown
     once.
9. **API permissions → Add a permission → Microsoft Graph →
   Delegated permissions**: add `openid`, `email`, `profile`,
   `User.Read`.
10. (Optional, recommended) **Grant admin consent for <tenant>** —
    skips the per-user consent prompt for org users.

### 4.2 Tenant ID conventions

* `common`: multi-tenant + personal MSA. **Use this for the public
  SaaS shape.** Token's `iss` claim varies per signing tenant — the
  API MUST validate the `iss` against a wildcard (`*sts.windows.net/*`)
  OR pin to `https://login.microsoftonline.com/common/v2.0` per the
  v2.0 endpoint spec.
* `organizations`: multi-tenant, NO personal MSA. Use if you want
  enterprise-only.
* `consumers`: personal MSA only. Use for consumer-only.
* `<tenant-uuid>`: single tenant. Use for org-internal apps only.

### 4.3 SSM provisioning

```bash
aws ssm put-parameter \
    --name /mahjong/prod/oauth/microsoft/client_id \
    --type SecureString \
    --value '<copied Application (client) ID>'

aws ssm put-parameter \
    --name /mahjong/prod/oauth/microsoft/client_secret \
    --type SecureString \
    --value '<copied Client secret VALUE>'

aws ssm put-parameter \
    --name /mahjong/prod/oauth/microsoft/tenant_id \
    --type SecureString \
    --value 'common'
```

### 4.4 Wave-3 ExternalSecret extension

When Bishop's Wave-3 middleware lands, extend `infra/k8s/overlays/prod/secret-template.yaml`'s
`oauth-secrets` ExternalSecret with:

```yaml
- secretKey: Authentication__Microsoft__ClientId
  remoteRef:
    key: /mahjong/prod/oauth/microsoft/client_id
- secretKey: Authentication__Microsoft__ClientSecret
  remoteRef:
    key: /mahjong/prod/oauth/microsoft/client_secret
- secretKey: Authentication__Microsoft__TenantId
  remoteRef:
    key: /mahjong/prod/oauth/microsoft/tenant_id
```

### 4.5 Microsoft-specific quirks

1. **`oid` claim is the stable primary key**, NOT `email`. Consumer
   MSA accounts can change their primary email; the `oid` GUID does
   not. Bishop's middleware MUST persist the `oid` as the canonical
   user ID and treat `email` as a contact attribute only.
2. **`tid=9188040d-6c67-4c5b-b112-36a304b66dad`** is the well-known
   tenant ID for personal Microsoft accounts (Xbox, Hotmail, Outlook
   personal, etc.). Useful for telemetry / UX (e.g. "signed in with
   your personal account").
3. **`email` scope** is required to receive the `email` claim for
   consumer MSA accounts. Org users get `email` from the directory
   regardless; consumers require the explicit scope.
4. **`prompt=select_account`** on the authorize URL forces the
   account picker; without it, returning users skip account choice
   even when they have multiple accounts. Bishop's middleware MAY
   want this behind a "switch account" UI button.

## 5. Validation (post-rotation or post-deploy)

For each provider, run the auth-flow smoke against production:

```bash
# Spin up a fresh browser context, click the provider button, sign in,
# verify the API returns a session cookie. Operator-driven manual
# verification.
gh workflow run auth-flow-smoke.yml \
    -F provider=microsoft \
    -F environment=production
```

Failures to look for:

* **`401 invalid_client`** — `client_secret` SSM key wasn't picked
  up by ESO. Force a refresh: `kubectl -n mahjong-prod annotate
  externalsecret oauth-secrets force-sync="$(date +%s)" --overwrite`.
* **`redirect_uri_mismatch`** — console redirect URI doesn't match
  the API's configured callback. Check the console-side URI matches
  EXACTLY (trailing slash matters).
* **`invalid_scope`** — operator skipped the API Permissions step
  (Microsoft). Re-do §4.1 step 9 + admin consent.

## 6. Rotation cadence

Per `docs/secret-rotation.md`, OAuth client secrets rotate quarterly.
Procedure:

1. Generate the new secret in the provider console (do NOT delete the
   old one yet — providers allow two active client secrets for
   overlap).
2. `aws ssm put-parameter --name <path>/client_secret --type
   SecureString --value '<new>' --overwrite`.
3. Wait for ESO refresh (1h refresh interval) → `kubectl rollout
   restart deployment mahjong-autotable` to pick up the new value.
4. Verify with §5.
5. Delete the OLD secret in the provider console.

## 7. Phase K Wave 6 — production verification + zero-downtime
##    dev → prod migration runbook

Phase K Wave 6 (Bishop) hard-pinned the per-provider verification +
scope-justification matrix and the dev → prod credential migration
playbook. The earlier §§1-6 cover the static console setup; this
section is the runtime checklist operators step through during the
production cutover.

### 7.1 Google — production-app verification

Google requires an OAuth-consent-screen verification pass before
external (non-`@gmail.com`-tested) users can complete the flow:

1. Visit `console.cloud.google.com/apis/credentials/consent`.
2. **Publishing status → In production** is the target. Click
   **Publish app**. Google's verification workflow opens.
3. Submit:
   - **App homepage**: `https://mahjong.example.com`.
   - **Privacy policy**: `https://mahjong.example.com/privacy`
     (must be reachable BEFORE you submit — Google's automated
     check fails an unreachable URL).
   - **Terms of service**: `https://mahjong.example.com/tos`.
   - **Application domain ownership**: verify via
     Google Search Console (TXT record on the apex domain).
4. **Scope justification (per scope)** — the API requests three
   scopes; this is exactly what to paste into the justification
   text box per scope:
   - `openid` — "Issue the user with a verified Google identity
     token (sub claim); required to associate a Mahjong-Autotable
     player profile with the Google account."
   - `.../auth/userinfo.email` — "Display the user's email in the
     account settings + use it as the unique account key. We do not
     send marketing or contact email from this address."
   - `.../auth/userinfo.profile` — "Display the user's first name +
     avatar in the lobby. The profile picture is not stored
     server-side; we resolve it from Google's `picture` claim on
     each session."
5. Verification turnaround: **4-6 weeks** for the first pass.
   Plan accordingly — staging traffic can run under the
   "testing" mode (capped at 100 test users) while the production
   verification is pending.

### 7.2 Microsoft (Entra ID) — admin consent flow

The Microsoft surface uses the **common** tenant by default (multi-
tenant + personal Microsoft accounts), but enterprise users hit the
admin-consent gate the first time they sign in. To make this
seamless:

1. In the App registration, **API permissions → Add a permission →
   Microsoft Graph → Delegated → `User.Read`** (sufficient for the
   profile claims we consume). Click **Grant admin consent for
   &lt;tenant&gt;** in the same screen — that pre-consents the app for
   the home tenant.
2. For external tenants, the admin-consent URL lives at
   `https://login.microsoftonline.com/{tenant}/v2.0/adminconsent?client_id=<your-client-id>&redirect_uri=https://mahjong.example.com/api/auth/callback/microsoft`.
   Operators can DM this link to a tenant admin to pre-consent
   their entire org.
3. **Publisher verification** is OPTIONAL for `common`-tenant apps
   but recommended for production. Submit at
   `partner.microsoft.com/dashboard/account/v3/enrollment`. The
   verification badge removes the "unverified publisher" warning
   on the consent screen.

### 7.3 GitHub — rate-limit considerations

GitHub OAuth tokens issued for sign-in flow inherit the 5,000-
requests-per-hour authenticated rate limit. The Mahjong-Autotable
backend's USERINFO fetch consumes 1 request per sign-in, so the
limit only matters under load:

1. **Capacity planning:** 5,000 sign-ins/hour ≈ 83/min, ≈ 1.4/sec.
   The API's per-user rate limiter (`AuthValidatePolicy`) caps
   inbound traffic at 100/min already; GitHub's headroom is fine.
2. **Per-IP throttle** (separate from the per-token budget):
   GitHub returns a `Retry-After` header on burst. The
   `OAuthService` does NOT currently honour this header — if you
   see burst-related sign-in failures, the mitigation is to
   migrate to a **GitHub App** (per-installation tokens with
   higher quotas) rather than the OAuth App used today.
3. **Webhook quota does not apply** — we do not consume GitHub
   webhooks for the sign-in path.

### 7.4 Zero-downtime dev → prod credential migration

When promoting a new OAuth Client ID from the dev / staging
environment to production WITHOUT interrupting in-flight sign-ins:

1. **Pre-flight**: confirm the OLD dev credential is still
   provisioned in `Authentication:Providers:{Provider}` AND the
   NEW production credential is provisioned in a *second*
   configuration path. The
   `Authentication:Providers:{Provider}:Migrating` boolean (forward-
   compat hook, Phase L) signals the migration window.
2. **Issue overlap**: in the provider console, mint the new
   client secret WITHOUT deleting the old one. Providers (all
   three) permit two active secrets per client ID for at least 24
   hours.
3. **Push the new SSM value**: `aws ssm put-parameter --name
   /mahjong/prod/oauth/{provider}/client_secret --type SecureString
   --value '<new>' --overwrite`. ESO refresh window: 1 hour.
4. **Restart with both values** (transitional): the new ESO sync
   places the new secret into the API pods. The validation
   path on `POST /api/auth/callback/{provider}` already tries both
   the active + previous OAuth state-signing keys; the OAuth
   client secret rotates atomically (only the new value lives in
   SSM at a time) so any in-flight callback that started with the
   old secret will fail server-side and the user will see a
   one-time "session expired, sign in again" message.
5. **Drain**: leave the OLD secret active in the provider console
   for 24 hours after the SSM swap. After 24h, delete the old
   secret.
6. **Verify**:
   - `curl -s https://mahjong.example.com/api/auth/providers | jq .`
     — every configured provider returns
     `{ "id": "...", "configured": true }`.
   - `kubectl logs deployment/mahjong-autotable | grep "OAuth"` —
     no `provider.callback.failed` audit kinds during the cut-
     over window.

### 7.5 Phase L forward-compat hooks

The Wave 6 surface includes two forward-compat hooks for Phase L:

- **JWT RS256 migration** — when `Authentication:JwtAlgorithm` flips
  from `HS256` (default) to `RS256`, the `/api/auth/.well-known/jwks.json`
  endpoint starts publishing the RSA public key set. The OIDC
  discovery document at `/.well-known/openid-configuration`
  follows the same gate. See `docs/jwt-rotation.md` §RS256.
- **Voice livestream HLS** — admin-only HLS recording at
  `/api/voice/livestream/{gameId}/playlist.m3u8`. The Wave-6
  surface returns a stub playlist; Phase L wires the real ffmpeg
  encoder behind the same controller URL.

## 8. Cross-references

* `docs/secret-rotation.md` — broader rotation cadence policy.
* `docs/oauth-setup.md` — DEV / staging OAuth setup (this doc covers
  production specifically).
* `docs/jwt-rotation.md` — JWT key rotation (HMAC today, RSA in
  Phase L).
* `docs/voice-sfu-design.md` — voice livestream + SFU sizing.
* `infra/k8s/overlays/prod/secret-template.yaml` — `oauth-secrets`
  ExternalSecret that binds these SSM keys to the API pods.
* Wave 3 PR (Bishop) — Microsoft provider middleware.
* Wave 6 PR (Bishop) — production-verification matrix +
  zero-downtime migration runbook (§7).
