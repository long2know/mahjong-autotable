# OAuth setup — Google + GitHub

Phase K Wave 1 (Bishop). Walks an operator through registering OAuth
apps with Google + GitHub, wiring the resulting credentials into
`appsettings.json` (or the matching env vars / Kubernetes secrets), and
verifying the surface with the in-process `verify-oauth` CLI.

## 1. Provider config schema

The API binds the following section out of `appsettings.json`:

```jsonc
"Authentication": {
  "Google": {
    "Enabled": true,
    "ClientId": "<from Google Cloud Console>",
    "ClientSecret": "<from Google Cloud Console>"
  },
  "GitHub": {
    "Enabled": true,
    "ClientId": "<from GitHub OAuth Apps>",
    "ClientSecret": "<from GitHub OAuth Apps>"
  },
  "EmailMagicLink": { "Enabled": true, "BaseUrl": "https://your-host" },
  "SessionLifetimeDays": 30,
  "MagicLinkTtlMinutes": 15,
  "StateSigningKey": "<32-byte random string>"
}
```

A provider is treated as **configured** when `Enabled` is `true` **and**
both `ClientId` and `ClientSecret` are non-empty. Disabled / unconfigured
providers are hidden from `GET /api/auth/providers` (so the login UI
never offers a button you can't click).

> **Production tip** — set `StateSigningKey` to a stable 32-byte random
> secret (`openssl rand -hex 32`). When it's blank the application mints
> a per-process random key + logs a warning; that means a rolling
> restart invalidates every in-flight OAuth authorize redirect.

## 2. Register the Google OAuth client

1. Open <https://console.cloud.google.com/apis/credentials>.
2. Click **Create credentials → OAuth client ID**.
3. Application type: **Web application**.
4. Name: anything that identifies your environment (e.g. `mahjong-prod`).
5. Authorised redirect URIs: add **all of**
   - `https://your-host/api/auth/callback/google`
   - (optional) `http://localhost:5114/api/auth/callback/google` for
     local dev
6. Click **Create**. Copy the generated **Client ID** and
   **Client secret** into `Authentication:Google:ClientId` /
   `Authentication:Google:ClientSecret` (or the equivalent k8s secret).
7. Under **OAuth consent screen** ensure the required scopes
   (`openid`, `profile`, `email`) are declared and that the consent
   screen is published (or the test users are listed) for the
   environment you're rolling out to.

## 3. Register the GitHub OAuth app

1. Open <https://github.com/settings/developers> → **OAuth Apps** →
   **New OAuth App**.
2. Application name: any descriptive label.
3. Homepage URL: `https://your-host`.
4. Authorization callback URL: `https://your-host/api/auth/callback/github`.
5. Click **Register application**.
6. On the next screen, click **Generate a new client secret**. Copy
   both the **Client ID** and the **Client secret** into
   `Authentication:GitHub:ClientId` / `Authentication:GitHub:ClientSecret`.

> GitHub does **not** issue an OIDC `id_token`. The Phase-K-1 nonce
> check is therefore a no-op for the GitHub provider — only the PKCE
> code-challenge + HMAC-signed `state` checks apply.

## 4. Verify the wiring

```bash
# Boots the DI container far enough to run the OIDC discovery probe
# (no listener port, no migrations). Exit code 0 iff every configured
# provider is healthy.
dotnet run --project src/backend/src/Mahjong.Autotable.Api -- verify-oauth
```

Each enabled provider emits one JSON line, e.g.

```
[verify-oauth] {"provider":"google","healthy":true,"statusCode":200,"error":null}
[verify-oauth] {"provider":"github","healthy":true,"statusCode":200,"error":null}
```

The same status is also surfaced live via `GET /health` under the
`oauth.providers.{name}` block — a load balancer can therefore detect
a provider outage without log-tail surveillance.

## 5. End-to-end smoke

1. With both providers configured, hit `GET /api/auth/providers` —
   both should appear.
2. `GET /api/auth/login/google` should 302 to
   `https://accounts.google.com/o/oauth2/v2/auth?…` with
   `code_challenge=…&code_challenge_method=S256&nonce=…&state=…` query
   params; matching `mahjong_oauth_state`, `mahjong_oauth_pkce`, and
   `mahjong_oauth_nonce` cookies should be set on the response.
3. Complete the Google flow in a browser; the callback should
   `302 → /` and a `mahjong_session` cookie should be present.
4. `GET /api/auth/me` should now return `isAuthenticated: true`.

## 6. Hardening notes

- **PKCE** — every authorize redirect carries a fresh SHA-256-hashed
  code challenge; the verifier is held in an HttpOnly cookie and
  forwarded on token exchange. RFC 7636 §4.4.
- **HMAC-signed state** — Wave 8's plain 32-byte nonce is replaced
  with `base64url(nonce(16) | expiry(8) | hmac-sha256(32))`. Tampered
  states fail signature check; expired states fail the embedded
  unix-seconds gate.
- **ID-token nonce** — when Google returns an `id_token` we parse the
  JWT payload (signature validation is out of scope for Wave K-1) and
  refuse the response if the `nonce` claim doesn't match the cookie.
- **Constant-time comparisons** — every cookie / nonce equality check
  goes through `CryptographicOperations.FixedTimeEquals` to avoid
  timing oracles.

## 7. Rotation

To rotate a provider's `ClientSecret`:

1. Generate the new secret in the provider console.
2. Update the relevant env var / k8s secret. The application reads
   `Authentication:*:ClientSecret` on every request (it's bound from
   `IConfiguration`, not cached on hot path); a rolling restart is
   sufficient.
3. Revoke the old secret in the provider console once the new one is
   confirmed working (`dotnet run -- verify-oauth` returns 0).
