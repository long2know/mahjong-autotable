# JWT signing-key rotation — fallback-list pattern

> Phase K Wave 3 — Apone (DevOps).

This runbook covers the **rotate-without-downtime** strategy for
the HMAC key that signs the API's JWT-backed authentication cookie
(`Auth:JwtSigningKey` in pre-Wave-3 config; now
`Auth:JwtSigningKeys` — an ordered LIST — from Wave 3 onward).

The motivation:
[`docs/secret-management.md`](secret-management.md) §rotation calls
out the historical pain point:

> `Auth:JwtSigningKey`: 180 days — tokens are JWTs with 1h
> lifetime — rotate, restart pods, accept ≤ 1 h of 401s.
> To avoid downtime, ship a fallback-key list (Wave 9 work).

[`docs/secret-rotation.md`](secret-rotation.md) carries the same
caveat. Wave-9 (originally) and Wave-3 (actually delivered) close
that gap by introducing the LIST shape.

## 1. Schema

`appsettings.json` Wave 3 shape:

```jsonc
{
  "Auth": {
    // JwtSigningKeys[0]   = ACTIVE signer (new tokens are minted with this).
    // JwtSigningKeys[1..N] = PREVIOUS keys, accepted for validation only.
    "JwtSigningKeys": []
  }
}
```

Environment-variable binding (.NET configuration system maps `:` →
`__` and array indices to integers):

```bash
Auth__JwtSigningKeys__0=<active>
Auth__JwtSigningKeys__1=<previous-1>
Auth__JwtSigningKeys__2=<previous-2>
```

ESO / Kubernetes Secret mounting (production —
[`infra/k8s/overlays/prod/jwt-keys-secret.yaml`](../infra/k8s/overlays/prod/jwt-keys-secret.yaml)
SHIPPED in Wave 4 as a SEPARATE `ExternalSecret` (`mahjong-jwt-keys`)
distinct from the omnibus `mahjong-autotable` secret. Splitting them
keeps JWT rotation on its own data plane — operators rotate the
signing keys WITHOUT having to re-shape the omnibus secret JSON):

```yaml
data:
  - secretKey: auth__jwtsigningkeys__0
    remoteRef:
      key: /mahjong/prod/auth/jwt/key-active
  - secretKey: auth__jwtsigningkeys__1
    remoteRef:
      key: /mahjong/prod/auth/jwt/key-previous
  - secretKey: auth__jwtsigningkeys__2
    remoteRef:
      key: /mahjong/prod/auth/jwt/key-archive
```

**SSM key-naming convention (Wave 4):** the SSM parameter names are
**rotation-state names** (`key-active`, `key-previous`, `key-archive`)
rather than array indices (`__0`, `__1`, `__2`). Operators rotate by
moving values BETWEEN named SSM parameters; ESO's template
re-binds the values to indexed env vars at materialise time. The
operator NEVER has to compute "which SSM key holds index 1 today?":

| SSM parameter                              | env var binding             | Role |
|--------------------------------------------|-----------------------------|------|
| `/mahjong/prod/auth/jwt/key-active`        | `Auth__JwtSigningKeys__0`   | Signer (new tokens minted with this) |
| `/mahjong/prod/auth/jwt/key-previous`      | `Auth__JwtSigningKeys__1`   | Validator-only fallback (most recent rotated-out key) |
| `/mahjong/prod/auth/jwt/key-archive`       | `Auth__JwtSigningKeys__2`   | Validator-only fallback (second-most recent rotated-out key, optional) |

The deployment patch in
[`infra/k8s/overlays/prod/kustomization.yaml`](../infra/k8s/overlays/prod/kustomization.yaml)
mounts the resulting Secret via `envFrom: secretRef: { name: mahjong-jwt-keys, optional: true }`.
`optional: true` means a fresh cluster without ESO bootstrapped can
still start (the app falls back to the singular `Auth:JwtSigningKey`
from the omnibus secret).

**ESO refresh cadence:** 15 minutes (vs the omnibus secret's 1 h).
JWT key rotation is the most security-sensitive rotation we run;
the tighter cadence means an emergency rotation propagates within
minutes rather than within the hour. The `force-sync` annotation
flow below still applies for immediate refresh.

## 2. Code-side contract (Bishop's Wave 4 / Wave 5 deliverable)

Bishop owns the code-side binding. The expected shape, documented
here so Bishop has a sealed-in spec to implement against:

* **Signer (token minting):** read `JwtSigningKeys[0]` once at
  startup. Cache the `SigningCredentials` for the lifetime of the
  process; any rotation requires a pod restart (the existing
  ESO-refresh + rollout-restart loop). The active signer is what
  goes into the `alg` + `kid` fields of the JWT header.

* **Validator (token validation):** build a
  `TokenValidationParameters.IssuerSigningKeys` collection from
  EVERY entry in `JwtSigningKeys[0..N]`. A token validates if its
  signature matches ANY of the keys in the list — that's the
  fallback semantics. The `kid` header field (optional) selects
  the validating key directly; absent that, .NET's
  `JwtSecurityTokenHandler` iterates the collection on lookup
  miss.

* **`kid` header:** the active key's `kid` is its index in the
  list at mint time (`"0"`). Validation does NOT rely on `kid` —
  if a token was minted under key-index `0` and the operator
  rotates so the old key moves to index `1`, the validator still
  accepts the token (it iterates the collection on `kid` lookup
  miss). The `kid` is informational only.

* **Configuration validation at startup:** if `JwtSigningKeys` is
  empty OR if `JwtSigningKeys[0]` is shorter than 32 bytes (the
  HMAC-SHA256 minimum), Program.cs throws `InvalidOperationException`
  before the host starts listening — this is the same fail-fast
  shape as today's `Auth:JwtSigningKey` validator.

* **Backwards compatibility:** for one wave after Bishop's binding
  lands, accept BOTH the new `JwtSigningKeys` (preferred) and the
  legacy `JwtSigningKey` (singular). If both are set, the array
  wins. After Wave 5 (or Wave 6) the legacy singular path is
  removed.

The smoke test that proves this contract end-to-end:
[`tests/smoke/jwt-rotation-smoke.sh`](../tests/smoke/jwt-rotation-smoke.sh).
It soft-passes today (Bishop's surface returns 404) and auto-
tightens to a hard assertion as soon as `/api/auth/token` +
`/api/auth/validate` register.

## 3. Rotation cadence

| Cadence    | Action                                                                                                              | Grace window |
|------------|---------------------------------------------------------------------------------------------------------------------|--------------|
| **Annually** (was 180 d in pre-Wave-3 docs; relaxed to 365 d because the fallback list now eliminates the user-pain) | Mint a new HMAC key, prepend to `JwtSigningKeys`, drop the eldest entry if `length > 3`. | **30 days** — keep the prior 2 keys in the list for 30 days post-rotation so every live JWT (1 h lifetime) gracefully transitions. |
| **Emergency** (compromise) | Same procedure as annual BUT shorter grace — set `JwtSigningKeys` to ONLY the new key, accept ≤ 1 h of 401s as the user-visible cost of evicting the leaked key. | 0 — the leak forces a hard rotation. |

The 30-day grace is the conservative ceiling. JWTs in this codebase
have a 1-hour lifetime, so technically 1 h of grace is sufficient.
The 30-day window is a hedge against operator surprise — long-
lived refresh-token flows or downstream services that cache tokens
in unusual ways might exceed the 1 h ceiling, and 30 days is the
canonical SaaS grace window.

## 4. Annual rotation procedure (zero-downtime)

1. **Mint a new HMAC key** (cryptographically random, ≥ 48 bytes):

    ```bash
    openssl rand -base64 48 > new-key.txt
    ```

2. **Cycle SSM** — the operator does NOT touch array indices;
   instead, they cycle VALUES BETWEEN the three rotation-state-
   named SSM parameters (see §1 for the naming convention):

    ```bash
    NEW_KEY=$(cat new-key.txt)
    OLD_ACTIVE=$(aws ssm get-parameter --name /mahjong/prod/auth/jwt/key-active   --with-decryption --query Parameter.Value --output text)
    OLD_PREV=$(aws ssm get-parameter   --name /mahjong/prod/auth/jwt/key-previous --with-decryption --query Parameter.Value --output text 2>/dev/null || echo "")

    # active     → previous
    # previous   → archive
    # new key    → active
    aws ssm put-parameter --name /mahjong/prod/auth/jwt/key-active \
        --type SecureString --value "$NEW_KEY"   --overwrite
    aws ssm put-parameter --name /mahjong/prod/auth/jwt/key-previous \
        --type SecureString --value "$OLD_ACTIVE" --overwrite
    if [ -n "$OLD_PREV" ]; then
      aws ssm put-parameter --name /mahjong/prod/auth/jwt/key-archive \
          --type SecureString --value "$OLD_PREV" --overwrite
    fi
    ```

   ESO (Wave-4 `mahjong-jwt-keys` ExternalSecret) re-binds the
   rotation-state names to the indexed env vars
   (`Auth__JwtSigningKeys__{0,1,2}`) at materialise time — the
   operator never has to compute which numeric index a value
   ends up at.

3. **Wait for ESO refresh** (≤ 15 min per the Wave-4 ESO refresh
   interval — tighter than the 1 h omnibus refresh). Or
   force-refresh immediately:

    ```bash
    kubectl -n mahjong-prod annotate externalsecret mahjong-jwt-keys \
        force-sync="$(date +%s)" --overwrite
    ```

4. **Rolling restart** so the new active signer is read at
   startup:

    ```bash
    kubectl -n mahjong-prod rollout restart deployment mahjong-autotable
    kubectl -n mahjong-prod rollout status  deployment mahjong-autotable
    ```

   Existing JWTs minted under the old key REMAIN VALID for their
   normal 1-hour TTL (they validate via the fallback entries at
   `JwtSigningKeys[1..2]`). New JWTs are minted under the new
   active signer.

5. **Verify** — mint a test token from a synthetic user and decode
   the `kid` header (should be `"0"`); validate a previously-issued
   token from a long-lived monitoring user (should succeed under
   the fallback path). The smoke test
   [`tests/smoke/jwt-rotation-smoke.sh`](../tests/smoke/jwt-rotation-smoke.sh)
   automates this assertion end-to-end against a Docker image.

6. **Audit-log** the rotation in `docs/secret-rotation.md`'s
   change log (operator action — git commit on `main` with the
   rotation date + new-key-id hash).

7. **After 30 days** — drop the eldest fallback entry:

    ```bash
    aws ssm delete-parameter --name /mahjong/prod/app/auth__jwtsigningkeys__2
    ```

   Wait for the next ESO refresh + rolling restart. The list now
   shrinks back to two entries (active + one fallback).

## 5. Emergency rotation (key compromise)

If the active signing key is suspected leaked:

1. **Generate new key** (step 1 above).
2. **Replace the active entry ONLY** — do NOT keep the compromised
   key in the fallback list:

    ```bash
    aws ssm put-parameter --name /mahjong/prod/auth/jwt/key-active \
        --type SecureString --value "$NEW_KEY" --overwrite
    aws ssm delete-parameter --name /mahjong/prod/auth/jwt/key-previous 2>/dev/null || true
    aws ssm delete-parameter --name /mahjong/prod/auth/jwt/key-archive  2>/dev/null || true
    ```

3. **Force-refresh ESO + immediate rolling restart** (step 3–4 above).
4. **All outstanding JWTs are invalidated.** Users see ≤ 1 h of 401s
   as they re-authenticate. This is the deliberate trade-off — the
   leak is contained.
5. **Audit-log** the rotation in `docs/secret-rotation.md` AND
   open a security advisory issue in the repo (no secrets in the
   issue body — reference the rotation date + SSM key id only).

## 6. Smoke validation

[`tests/smoke/jwt-rotation-smoke.sh`](../tests/smoke/jwt-rotation-smoke.sh)
exercises the full rotation cycle end-to-end against a live Docker
image (Wave 3 — Apone). The script:

1. Boots the image with `Auth__JwtSigningKeys__0=key0`.
2. Mints a token under key0 via Bishop's `POST /api/auth/token`
   surface.
3. Stops the container, re-boots with
   `Auth__JwtSigningKeys__0=key1` + `Auth__JwtSigningKeys__1=key0`
   (key0 demoted to fallback).
4. Validates the old key0-signed token against the rotated
   container — MUST succeed (the fallback contract).
5. Mints a new token — MUST be signed under key1.

The script is wired into [`docker-smoke.yml`](../.github/workflows/docker-smoke.yml)
as a nightly check. Until Bishop's code-side binding lands (Wave 4
or Wave 5), the script soft-passes when the `/api/auth/token` /
`/api/auth/validate` endpoints return 404 — the established
forward-compat shape used by `pwa-smoke`, `csp-report-smoke`,
`chat-flow-smoke`, etc.

## 7. Migration path (one-time)

The wave-by-wave migration path:

| Wave | Owner | Action |
|------|-------|--------|
| **W3** | Apone | `appsettings.json` ships `Auth.JwtSigningKeys: []` schema. `tests/smoke/jwt-rotation-smoke.sh` ships forward-compat. Docs land. |
| **W4** | Bishop | Code-side binding: read `IConfiguration` array → `TokenValidationParameters.IssuerSigningKeys` collection. Honor `JwtSigningKey` (singular) as a fallback for one wave. |
| **W4** | Apone | ESO `mahjong-jwt-keys` ExternalSecret shipped at `infra/k8s/overlays/prod/jwt-keys-secret.yaml`; prod kustomization mounts it via `envFrom: { optional: true }`. Operator seeds three SSM SecureString parameters (`/mahjong/prod/auth/jwt/key-{active,previous,archive}`) before applying the overlay. |
| **W5** (this wave) | Bishop | `kid` header on minted tokens (already present from W4) confirmed end-to-end via `JwtKidRolloverContractTests`. `POST /api/auth/token` returns the pinned `AuthTokenResponse` envelope (`token`, `expiresAtUtc`, `kid`, `tokenType="Bearer"`, `expiresInSeconds`). `GET /api/auth/.well-known/jwks.json` reserved at 404 + `Cache-Control: no-store` so Phase L RS256 flip works without cache contamination. Legacy `JwtSigningKey` (singular) **kept** for one-more-wave back-compat — drop slated for Wave 6 once Apone's SSM rotation drill exercises the array path in production. |

Until W4 code-side binding lands, the `envFrom: { optional: true }`
on the new secret is the gate — the deployment starts fine without
the secret (current behaviour), and AS SOON AS Bishop binds
`Auth.JwtSigningKeys`, the ESO-materialised values feed the array
with zero further DevOps work.

## 8. RS256 key provisioning (Phase K Wave 7)

> Phase K Wave 7 — Bishop (Backend).

The HS256 fallback-list pattern documented above carries the
identity-issuance surface through every wave up to and including
Wave 6. **Wave 7 introduces the RS256 path** so the API can publish
a public JWKS (`/.well-known/jwks.json`) + OIDC discovery document
(`/.well-known/openid-configuration`) and downstream verifiers
(Auth0, Cognito, jose-jwt, pyJWT, `verify_oauth2_token`) can validate
tokens without sharing a symmetric secret.

The Wave 7 rotation flow is **strictly additive** — operators who
have not flipped `Auth:JwtAlgorithm` from `HS256` to `RS256` continue
on the §4 procedure. Once flipped, the same three-slot fallback
shape applies, but the secret material is a **PEM-encoded RSA
private key** rather than a base64 HMAC secret.

### 8.1 Keypair generation

Generate a 2048-bit RSA keypair with OpenSSL on an air-gapped
workstation (or via AWS KMS asymmetric keypair — see §8.5 for the
KMS variant):

```bash
# Private key (PKCS#1, PEM) — used by the API for signing.
openssl genrsa -out jwt-rs256-active.pem 2048

# Convert to PKCS#8 (preferred — RFC 5208; .NET's
# RSA.ImportFromPem() handles both shapes transparently but PKCS#8
# is the canonical SSM Parameter Store value).
openssl pkcs8 -topk8 -nocrypt \
  -in jwt-rs256-active.pem \
  -out jwt-rs256-active.pk8.pem

# Public key (X.509 SubjectPublicKeyInfo) — for downstream verifiers
# that prefer a static PEM over the dynamic JWKS endpoint.
openssl rsa -in jwt-rs256-active.pem -pubout -out jwt-rs256-active.pub.pem
```

The `kid` is derived deterministically from the public-key bytes
(SHA-256 truncated, base64url) — see
[`JwtRsaSigningKey.ComputeKid`](../src/backend/src/Mahjong.Autotable.Api/Auth/JwtSigningKeyProvider.cs).
The same private key always produces the same `kid`, so an operator
can verify the rotation landed cleanly by curling
`/.well-known/jwks.json` after the pod restart and matching the
`kid` against the expected value computed locally.

### 8.2 SSM Parameter Store topology

Wave 7 reuses the three-slot active/previous/archive convention from
§4 but under a separate path prefix so HS256 and RS256 secrets are
managed independently (an operator can run both algorithms in
parallel during a long migration window):

| Slot | Parameter path | Purpose |
| ---- | -------------- | ------- |
| **active** | `/mahjong/{env}/auth/jwt/rsa-active` | Mints new tokens. Mounted as `Auth__JwtRsaKeys__0`. |
| **previous** | `/mahjong/{env}/auth/jwt/rsa-previous` | Validation-only fallback for the previous active key (still in-flight tokens). Mounted as `Auth__JwtRsaKeys__1`. |
| **archive** | `/mahjong/{env}/auth/jwt/rsa-archive` | Long-tail validation for tokens minted before the LAST rotation. Mounted as `Auth__JwtRsaKeys__2`. |

The values MUST be **SecureString** parameters with KMS encryption
under the `mahjong-{env}-secrets` CMK (matches the HS256 convention
from §4). The PEM body is stored verbatim with embedded newlines —
SSM accepts up to 4 KB for SecureString which comfortably fits a
2048-bit PKCS#8 PEM (~1.7 KB).

### 8.3 ESO ExternalSecret mount

The W7 ESO mount lives in **dedicated** ExternalSecret manifests
(separate from the W4 `mahjong-jwt-keys` Secret) so HS256 and
RS256 rotation surfaces stay independent — operators can rotate
one without ESO-resyncing the other, and the cryptographic
shapes (opaque HMAC bytes vs PEM-encoded RSA key) never alias
inside a single Secret object:

* [`infra/k8s/overlays/prod/jwt-rsa-keys-secret.yaml`](../infra/k8s/overlays/prod/jwt-rsa-keys-secret.yaml)
  → materialises `Secret/mahjong-jwt-rsa-keys` in `mahjong-prod`
  (out-of-band — apply manually, mirroring the W4 prod convention).
* [`infra/k8s/overlays/staging/jwt-rsa-keys-secret.yaml`](../infra/k8s/overlays/staging/jwt-rsa-keys-secret.yaml)
  → materialises `Secret/mahjong-jwt-rsa-keys-staging` in
  `mahjong-staging` (listed in staging `kustomization.yaml`
  resources so `kubectl apply -k overlays/staging/` picks it up).

```yaml
data:
  - secretKey: auth__jwtrsakeys__0
    remoteRef:
      key: /mahjong/prod/auth/jwt/rsa-active
  - secretKey: auth__jwtrsakeys__1
    remoteRef:
      key: /mahjong/prod/auth/jwt/rsa-previous
  - secretKey: auth__jwtrsakeys__2
    remoteRef:
      key: /mahjong/prod/auth/jwt/rsa-archive
```

The deployment patch adds a **new** `envFrom` entry for the RSA
Secret (alongside the existing HS256 `mahjong-jwt-keys` mount —
both bindings co-exist during the cutover window):

```yaml
- op: add
  path: /spec/template/spec/containers/0/envFrom/-
  value:
    secretRef:
      name: mahjong-jwt-rsa-keys           # staging suffix in the staging overlay
      optional: true
```

`optional: true` lets the deployment start before the operator
has bootstrapped the RSA SSM parameters — the existing HS256
path stays canonical until `Auth:DefaultAlgorithm=RS256` is
flipped (see §8.4). Both overlay kustomization files carry the
matching patch.

### 8.4 Algorithm flip + rotation procedure

The **algorithm flip** (HS256 → RS256) is a one-time event:

1. **Pre-flight**: confirm `/mahjong/{env}/auth/jwt/rsa-active` is
   populated. Curl `/.well-known/jwks.json` before the flip — MUST
   return 404 (HS256 hosts publish no JWKS).
2. **Flip**: set `Auth__JwtAlgorithm=RS256` in the deployment
   `ConfigMap` (NOT a Secret — this is non-sensitive policy).
   Optionally set `Auth__Issuer=https://api.{env}.mahjong-autotable.com`
   so the `iss` claim + OIDC `issuer` field is self-describing.
3. **Roll the pods**: `kubectl rollout restart deployment/mahjong-autotable`.
   New tokens mint under RS256; the HMAC fallback list stays loaded
   so any in-flight HS256 tokens validate until expiry (1h TTL).
4. **Verify**: curl `/.well-known/jwks.json` — MUST return 200 with
   `{"keys": [{"kty":"RSA","kid":"…","n":"…","e":"AQAB",…}]}`.
   Curl `/.well-known/openid-configuration` — MUST return 200 with
   `issuer`, `jwks_uri`, `token_endpoint`, `grant_types_supported`.

Once the host is on RS256, **subsequent rotations** follow the same
three-slot shuffle as the HS256 procedure (§4):

```bash
ENV=prod  # or staging
NEW=jwt-rs256-active.pk8.pem  # freshly minted PKCS#8 PEM

# Step 1: archive ← previous (long-tail tokens age out).
aws ssm get-parameter --name "/mahjong/${ENV}/auth/jwt/rsa-previous" \
  --with-decryption --query Parameter.Value --output text \
  | aws ssm put-parameter --name "/mahjong/${ENV}/auth/jwt/rsa-archive" \
      --type SecureString --overwrite --value file:///dev/stdin

# Step 2: previous ← active.
aws ssm get-parameter --name "/mahjong/${ENV}/auth/jwt/rsa-active" \
  --with-decryption --query Parameter.Value --output text \
  | aws ssm put-parameter --name "/mahjong/${ENV}/auth/jwt/rsa-previous" \
      --type SecureString --overwrite --value file:///dev/stdin

# Step 3: active ← new.
aws ssm put-parameter --name "/mahjong/${ENV}/auth/jwt/rsa-active" \
  --type SecureString --overwrite --value "$(cat ${NEW})"

# Step 4: kick ESO to re-sync (or wait for the 1h refresh).
kubectl annotate externalsecret mahjong-jwt-keys \
  force-sync=$(date +%s) --overwrite -n mahjong-autotable

# Step 5: roll the pods — new tokens mint under the new kid.
kubectl rollout restart deployment/mahjong-autotable
```

The `JwtRotationE2ETests` fact pins the cryptographic surface end
to end — a token minted under the pre-rotation key MUST still
validate against the post-rotation host (the fallback list catches
the legacy `kid`). See
[`tests/Mahjong.Autotable.Api.Tests/Phase_K_W7/Bishop/JwtRotationE2ETests.cs`](../src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W7/Bishop/JwtRotationE2ETests.cs).

### 8.5 AWS KMS asymmetric keypair (alternative)

For deployments with stricter HSM-backed key-custody requirements,
the active key MAY live in AWS KMS as an asymmetric `RSA_2048`
signing key with usage `SIGN_VERIFY`. The API uses the KMS public
half (downloaded once at boot via `kms:GetPublicKey`) and signs via
`kms:Sign` per-request. This path is **NOT shipped in Wave 7** —
documented here for the Wave 8 / Wave 9 migration plan. Until then,
all RSA private keys live in SSM as SecureString PEMs.

### 8.6 Recovery: lost RSA private key

If the active RSA key material is lost (operator error / KMS
permission revocation / SSM parameter deletion), follow the §5
emergency rotation pattern but on the RSA path:

1. Mint a fresh keypair (§8.1).
2. SSM-put the new private key into `/mahjong/{env}/auth/jwt/rsa-active`
   (skip the archive/previous shuffle — every in-flight RS256 token
   is unrecoverable anyway).
3. Force-sync ESO + roll the pods.
4. Notify clients to re-authenticate. Public clients fetching the
   JWKS will see the new `kid` immediately + transparently retry
   any 401-on-stale-kid responses.

## 9. Cross-references

* [`docs/jwt-ssm-runbook.md`](jwt-ssm-runbook.md) — SSM-Parameter-Store-focused operator runbook (RS256 keypair custody, §8 above is the canonical procedure).
* [`docs/secret-management.md`](secret-management.md) — broader secret-management policy.
* [`docs/secret-rotation.md`](secret-rotation.md) — day-of-rotation cadence + runbooks.
* [`tests/smoke/jwt-rotation-smoke.sh`](../tests/smoke/jwt-rotation-smoke.sh) — end-to-end rotation smoke.
* [`src/backend/src/Mahjong.Autotable.Api/appsettings.json`](../src/backend/src/Mahjong.Autotable.Api/appsettings.json) — `Auth.JwtSigningKeys` schema (forward-compat shipped in Wave 3).
* [`infra/k8s/overlays/prod/jwt-keys-secret.yaml`](../infra/k8s/overlays/prod/jwt-keys-secret.yaml) — Wave-4 ESO `mahjong-jwt-keys` ExternalSecret (HS256 active/previous/archive SSM mounts).
* [`infra/k8s/overlays/prod/jwt-rsa-keys-secret.yaml`](../infra/k8s/overlays/prod/jwt-rsa-keys-secret.yaml) — **Wave-7** ESO `mahjong-jwt-rsa-keys` ExternalSecret (RS256 active/previous/archive PEM mounts).
* [`infra/k8s/overlays/staging/jwt-rsa-keys-secret.yaml`](../infra/k8s/overlays/staging/jwt-rsa-keys-secret.yaml) — Wave-7 staging equivalent (`mahjong-jwt-rsa-keys-staging`).
* [`infra/k8s/overlays/prod/kustomization.yaml`](../infra/k8s/overlays/prod/kustomization.yaml) — both `envFrom` mounts (HS256 + RS256), each `optional: true`.
* [`infra/k8s/overlays/prod/secret-template.yaml`](../infra/k8s/overlays/prod/secret-template.yaml) — omnibus ESO (kept distinct from the JWT keys ESO so rotation surfaces don't entangle).
