# OAuth — token endpoints

Phase K Wave 11 (Bishop). Companion document to
`oauth-setup.md` (provider registration) +
`oauth-production-setup.md` (deployment runbook). This file
captures the **token-shaped** OAuth surfaces — issuance,
validation, JWKS, OIDC discovery, **and** RFC 7662 token
introspection (new in W11).

## 1. Token endpoint — `POST /api/auth/token`

* **Auth**: cookie session (admin role required).
* **Body**: `{ subject, claims? }`.
* **Response**: `AuthTokenResponse` envelope —
  `{ token, expiresAtUtc, kid, tokenType, expiresInSeconds }`.
* **Errors**: 401 (no session), 403 (non-admin), 400 (empty
  subject).
* **Rate-limit policy**: `ApiPolicy`.

## 2. Validate endpoint — `POST /api/auth/validate`

* **Auth**: none — rate-limited per-IP at 100/min via
  `AuthValidatePolicy`.
* **Body**: `{ token }`.
* **Response**: `{ valid, subject?, claims?, kid?, error? }`.
* **Errors**: never; always `200 OK` with `valid: false` for
  invalid tokens.

## 3. JWKS document — `GET /api/auth/.well-known/jwks.json`

* When `Authentication:JwtAlgorithm = "RS256"`: serves the
  RFC 7517 JWKS array of every loaded RSA public key,
  cached at `max-age=3600` with a strong ETag for 304 short-
  circuit.
* When HS256: returns 404 with `{ "reason": "jwt-algorithm-is-hs256", "migrate-to": "RS256" }`
  and `max-age=60` so caches don't pin a positive 404
  indefinitely.

## 4. OIDC discovery — `GET /api/auth/.well-known/openid-configuration`

* When RS256 AND `Authentication:Issuer` is set: returns the
  RFC 8414 discovery document with the issuer, JWKS URI,
  token endpoint, **introspection endpoint** (W11 addition),
  and supported grant types.
* When HS256: returns 404 with `{ "reason": "oidc-discovery-disabled" }`.

## 5. JWT rotation

See `docs/jwt-rotation.md` for the day-1 operator runbook.
Highlights:

* `Authentication:JwtSigningKeys` (HS256) or
  `Authentication:JwtRsaKeys` (RS256) — ordered array.
* Position 0 is the active signer; positions 1..N are
  fallback verifiers retained for the grace window.
* `Authentication:RotationGracePeriodSeconds` defaults to
  600 (10 minutes); production runs the canonical 30-day
  grace window — set to 2592000.

## 6. SSM-backed rotation rehearsal

See `docs/jwt-ssm-runbook.md`. Highlights:

* AWS Parameter Store SecureString entries hold the JWT
  signing keys; the `ExternalSecrets` operator syncs them
  into a Kubernetes `Secret`; Argo Rollouts hot-swaps the
  pod env without a full restart.

## 7. Introspection endpoint — `POST /api/auth/introspect`

**Phase K Wave 11 (Bishop) — new in W11.**

RFC 7662 token-introspection surface for programmatic
verifiers (Janus mountpoint health-probe, bot frameworks,
etc.) that need to confirm a token's `active` status
without re-implementing the JWT validation flow.

### 7.1 Wire shape

| Property | Value |
|----------|-------|
| **URL** | `POST /api/auth/introspect` |
| **Auth** | HTTP Basic (client allowlisted in `Authentication:IntrospectionClients`) |
| **Body** | `application/x-www-form-urlencoded`, fields `token` (required) + `token_type_hint` (ignored — we only mint bearer tokens) |
| **Rate-limit** | `AuthValidatePolicy` (100/min per-IP) |

### 7.2 Response

```json
{
  "active": true,
  "scope": "voice:read voice:write",
  "client_id": "janus-health-probe",
  "username": "bot-12345",
  "sub": "bot-12345",
  "iat": 1779567890,
  "exp": 1779571490,
  "kid": "k-2026-01",
  "token_type": "Bearer"
}
```

When the token is **inactive** (malformed, bad signature,
expired, unsupported algorithm), the response collapses to:

```json
{ "active": false }
```

per RFC 7662 §2.2. Per-token errors map to `active: false`,
**not** HTTP 4xx. The 4xx response codes are reserved for
transport-layer errors:

| Status | Condition |
|--------|-----------|
| `401` | Missing or invalid `Authorization: Basic` header |
| `400` | Missing `token` form field |
| `415` | Wrong content-type (not `application/x-www-form-urlencoded`) |

The 401 response carries `WWW-Authenticate: Basic realm="introspect"`
per RFC 7662 §2.3.

### 7.3 Client allowlist config

```jsonc
"Authentication": {
  "IntrospectionClients": [
    {
      "ClientId": "janus-health-probe",
      "ClientSecret": "env:JANUS_INTROSPECT_SECRET",
      "Scope": "voice:read"
    },
    {
      "ClientId": "discord-bot",
      "ClientSecret": "env:DISCORD_BOT_INTROSPECT_SECRET",
      "Scope": "tournament:read tournament:write"
    }
  ]
}
```

* `ClientSecret` accepts either a literal value or the
  `env:VAR_NAME` indirection (read at validation time, not
  startup) — keeps secrets out of the JSON blob.
* Empty `IntrospectionClients` list = endpoint returns 401
  for every request (introspection effectively disabled).
* `Scope` is a free-form label echoed back in the
  introspection response; the JWT itself does not carry
  scopes today (a future wave will wire scope-aware
  authorization).

### 7.4 Constant-time secret comparison

The W11 implementation uses
`CryptographicOperations.FixedTimeEquals` for the client-
secret comparison so timing-side-channel discovery of the
secret is bounded. The hash-then-compare alternative is
overkill at our scale (a handful of allowlisted clients);
constant-time string compare is the canonical defence
recommended by RFC 7662.

### 7.5 Hard-asserted contract

The W11 contract suite (`Phase_K_W11/Bishop/`
`OAuthIntrospectionFacts.cs`) pins:

* **Valid token → `active: true`** with `client_id` matching
  the Basic-auth caller.
* **Expired token → `active: false`** (no optional fields).
* **Malformed token → 400** with `error: "invalid_request"`.
* **Missing Basic auth → 401** with the canonical
  `WWW-Authenticate` header.
* **Wrong secret → 401** (constant-time compare path).
* **Empty allowlist → 401** for every request (introspection
  disabled).

### 7.6 Operator runbook

1. Mint a per-client shared secret with
   `openssl rand -hex 32`.
2. Store the secret under an env-var (AWS SSM
   SecureString or k8s Secret) — never check it in.
3. Add the client to `Authentication:IntrospectionClients`
   using the `env:VAR_NAME` indirection.
4. Roll the API replicas (`kubectl rollout restart`) so the
   PostConfigure hook picks up the new client list.
5. Verify the surface with `curl -u <id>:<secret> -d 'token=<jwt>'
   https://your-host/api/auth/introspect | jq`.

### 7.7 References

* RFC 7662 — OAuth 2.0 Token Introspection.
* RFC 6750 §2.1 — Bearer-token authorization header.
* `src/backend/src/Mahjong.Autotable.Api/Auth/AuthTokenController.cs`
  — implementation.
* `src/backend/src/Mahjong.Autotable.Api/Auth/AuthOptions.cs`
  — `IntrospectionClient` config shape.
