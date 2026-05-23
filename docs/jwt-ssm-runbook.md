# JWT SSM Parameter Store runbook (RS256)

> Phase K Wave 7 — Bishop (Backend).

This is the **operator-side companion** to [`jwt-rotation.md`
§8](jwt-rotation.md#8-rs256-key-provisioning-phase-k-wave-7). It
strips the runbook down to the literal AWS Systems Manager
Parameter Store commands an SRE will execute during an RS256 key
rotation. **The canonical procedure + ESO topology lives in
`jwt-rotation.md` §8** — this file is the cheat-sheet.

## 1. SSM topology

Three SecureString parameters per environment, under the
`/mahjong/{env}/auth/jwt/` prefix:

| Slot       | Path                                            | Mounts as                  |
| ---------- | ----------------------------------------------- | -------------------------- |
| active     | `/mahjong/{env}/auth/jwt/rsa-active`            | `Auth__JwtRsaKeys__0`      |
| previous   | `/mahjong/{env}/auth/jwt/rsa-previous`          | `Auth__JwtRsaKeys__1`      |
| archive    | `/mahjong/{env}/auth/jwt/rsa-archive`           | `Auth__JwtRsaKeys__2`      |

All three are `SecureString` parameters encrypted under the
environment-specific KMS CMK (`alias/mahjong-{env}-secrets`). The
value is the PEM body verbatim (PKCS#8 preferred; PKCS#1 accepted).

## 2. First-time provisioning

```bash
ENV=prod
KMS_KEY=alias/mahjong-${ENV}-secrets

openssl genrsa -out rsa-active.pem 2048
openssl pkcs8 -topk8 -nocrypt -in rsa-active.pem -out rsa-active.pk8.pem

aws ssm put-parameter \
  --name "/mahjong/${ENV}/auth/jwt/rsa-active" \
  --type SecureString \
  --key-id "${KMS_KEY}" \
  --value "$(cat rsa-active.pk8.pem)" \
  --description "RS256 active signing key for the Mahjong-Autotable API."

# Seed `previous` + `archive` with the same value at first
# provisioning so all three slots exist (ESO will fail-fast if any
# is missing). The next rotation will populate them properly.
for SLOT in previous archive; do
  aws ssm put-parameter \
    --name "/mahjong/${ENV}/auth/jwt/rsa-${SLOT}" \
    --type SecureString \
    --key-id "${KMS_KEY}" \
    --value "$(cat rsa-active.pk8.pem)" \
    --description "RS256 ${SLOT} signing key (seed copy of active)."
done

# Wipe the local PEMs — the SSM copies are now canonical.
shred -u rsa-active.pem rsa-active.pk8.pem
```

## 3. Rotation cadence

Rotate every **90 days** (quarterly) — tightened from the
original 180-day cadence in Wave 10 to align with the squad's
broader quarterly rotation cadence (`docs/secret-management.md`).
The rotation is **zero-downtime** because the fallback validator
accepts any of the three loaded keys.

### 3.1 Quarterly calendar

| Quarter      | Last rotation by | Owner             |
| ------------ | ---------------- | ----------------- |
| Q1 (Jan-Mar) | last day of Mar  | on-call SRE       |
| Q2 (Apr-Jun) | last day of Jun  | on-call SRE       |
| Q3 (Jul-Sep) | last day of Sep  | on-call SRE       |
| Q4 (Oct-Dec) | last day of Dec  | on-call SRE       |

The on-call SRE for the quarter is the rotation owner; the
hand-off between SREs at the quarter boundary picks up an
**already-rotated** active key, not an aging one. (Don't make
the hand-off pick up rotation as their first task.)

### 3.2 Quarterly rotation — full operator sequence

The full rotation procedure lives in §4. The quarterly cadence
adds two pre-flight validation steps + one post-rotation
validation step that the rare-emergency rotation procedure
(§5) skips:

```bash
ENV=prod
KMS_KEY=alias/mahjong-${ENV}-secrets

# ── Pre-flight: confirm all three SSM slots currently exist
# and that the JWKS endpoint already reflects them. If any
# slot is missing OR the JWKS shows < 3 kids, FAIL CLOSED —
# fix the prior rotation's drift before starting a new one.
for SLOT in active previous archive; do
  aws ssm get-parameter \
    --name "/mahjong/${ENV}/auth/jwt/rsa-${SLOT}" \
    --with-decryption --query Parameter.Value --output text >/dev/null \
  || { echo "::error::missing SSM slot: rsa-${SLOT}"; exit 1; }
done

JWKS_KIDS=$(curl -sfL "https://api.${ENV}.mahjong-autotable.com/.well-known/jwks.json" \
            | jq -r '.keys | length')
[ "${JWKS_KIDS}" -ge 3 ] \
  || { echo "::error::JWKS shows only ${JWKS_KIDS} keys; expected ≥ 3 before rotation"; exit 1; }

# ── Rotation proper: execute §4 step 0 through step 7.
#    (See §4 for the put-parameter sequence.)

# ── Post-rotation validation: confirm three DISTINCT kids on
# the JWKS (not the seed-copy case at first provisioning).
JWKS_DISTINCT=$(curl -sfL "https://api.${ENV}.mahjong-autotable.com/.well-known/jwks.json" \
                | jq -r '.keys[].kid' | sort -u | wc -l)
[ "${JWKS_DISTINCT}" -ge 3 ] \
  || { echo "::error::JWKS shows ${JWKS_DISTINCT} distinct kids after rotation; expected ≥ 3"; exit 1; }
echo "::notice::quarterly rotation complete; ${JWKS_DISTINCT} distinct kids published"
```

### 3.3 Quarterly rotation — checklist for the on-call hand-off

- [ ] Pre-flight validation in §3.2 passes (all three SSM slots
      present + JWKS shows ≥ 3 kids).
- [ ] Mint new keypair offline (air-gapped or in-cluster
      one-shot pod with `openssl genrsa`).
- [ ] Execute §4 steps 1-7 in order.
- [ ] Post-rotation validation in §3.2 passes (≥ 3 distinct
      `kid` values on the JWKS).
- [ ] Update `docs/secret-management.md` rotation log with the
      quarter + rotator + UTC timestamp.
- [ ] Notify Stephen in `.squad/decisions/inbox/` with the
      rotation memo (no action required from him; this is the
      audit-trail entry).

### 3.4 Quarterly rollback (if the new active key is rejected by clients)

If clients start seeing 401s after the rotation completes (very
rare — the W7 RS256 path validates against all three loaded
keys), the rollback is to **demote** the new active back to
previous without dropping the old:

```bash
ENV=prod
KMS_KEY=alias/mahjong-${ENV}-secrets

# Step 1 — restore active ← previous (the key clients trust).
PREV=$(aws ssm get-parameter \
  --name "/mahjong/${ENV}/auth/jwt/rsa-previous" \
  --with-decryption --query Parameter.Value --output text)
aws ssm put-parameter \
  --name "/mahjong/${ENV}/auth/jwt/rsa-active" \
  --type SecureString --key-id "${KMS_KEY}" \
  --value "${PREV}" --overwrite

# Step 2 — restore previous ← archive (so we keep BOTH a fresh
# active AND a fallback validator).
ARCH=$(aws ssm get-parameter \
  --name "/mahjong/${ENV}/auth/jwt/rsa-archive" \
  --with-decryption --query Parameter.Value --output text)
aws ssm put-parameter \
  --name "/mahjong/${ENV}/auth/jwt/rsa-previous" \
  --type SecureString --key-id "${KMS_KEY}" \
  --value "${ARCH}" --overwrite

# Step 3 — force-sync ESO + roll pods.
kubectl annotate externalsecret mahjong-jwt-keys \
  -n mahjong-autotable force-sync="$(date +%s)" --overwrite
kubectl rollout restart deployment/mahjong-autotable -n mahjong-autotable
```

The new key that was just minted is now LOST (we overwrote the
slot it briefly occupied). That's intentional — a rejected-by-
client key is a key we never want to revisit.

## 4. Rotation procedure

```bash
ENV=prod
KMS_KEY=alias/mahjong-${ENV}-secrets

# Step 0 — mint the new keypair (offline, air-gapped workstation
# preferred for production).
openssl genrsa -out new-active.pem 2048
openssl pkcs8 -topk8 -nocrypt -in new-active.pem -out new-active.pk8.pem

# Step 1 — archive ← previous.
PREV=$(aws ssm get-parameter \
  --name "/mahjong/${ENV}/auth/jwt/rsa-previous" \
  --with-decryption --query Parameter.Value --output text)
aws ssm put-parameter \
  --name "/mahjong/${ENV}/auth/jwt/rsa-archive" \
  --type SecureString \
  --key-id "${KMS_KEY}" \
  --value "${PREV}" \
  --overwrite

# Step 2 — previous ← active.
ACTIVE=$(aws ssm get-parameter \
  --name "/mahjong/${ENV}/auth/jwt/rsa-active" \
  --with-decryption --query Parameter.Value --output text)
aws ssm put-parameter \
  --name "/mahjong/${ENV}/auth/jwt/rsa-previous" \
  --type SecureString \
  --key-id "${KMS_KEY}" \
  --value "${ACTIVE}" \
  --overwrite

# Step 3 — active ← new.
aws ssm put-parameter \
  --name "/mahjong/${ENV}/auth/jwt/rsa-active" \
  --type SecureString \
  --key-id "${KMS_KEY}" \
  --value "$(cat new-active.pk8.pem)" \
  --overwrite

# Step 4 — force-sync ESO so the new values land in the cluster
# Secret without waiting for the 1h refresh interval.
kubectl annotate externalsecret mahjong-jwt-keys \
  -n mahjong-autotable \
  force-sync="$(date +%s)" --overwrite

# Step 5 — roll the pods so the in-memory key cache reloads.
kubectl rollout restart deployment/mahjong-autotable \
  -n mahjong-autotable
kubectl rollout status deployment/mahjong-autotable \
  -n mahjong-autotable --timeout=5m

# Step 6 — verify the new `kid` is published on the JWKS endpoint.
curl -sf https://api.${ENV}.mahjong-autotable.com/.well-known/jwks.json \
  | jq -r '.keys[].kid'
# Expected: the kid derived from new-active.pk8.pem MUST appear
# first in the list.

# Step 7 — shred the local PEMs.
shred -u new-active.pem new-active.pk8.pem
```

## 5. Emergency rotation (key compromise)

Skip the three-slot shuffle. Overwrite `/mahjong/{env}/auth/jwt/rsa-active`
with a fresh keypair AND clear `previous` + `archive` (so the
compromised key cannot validate legacy tokens):

```bash
ENV=prod
KMS_KEY=alias/mahjong-${ENV}-secrets

openssl genrsa -out emergency.pem 2048
openssl pkcs8 -topk8 -nocrypt -in emergency.pem -out emergency.pk8.pem
PEM=$(cat emergency.pk8.pem)

for SLOT in active previous archive; do
  aws ssm put-parameter \
    --name "/mahjong/${ENV}/auth/jwt/rsa-${SLOT}" \
    --type SecureString \
    --key-id "${KMS_KEY}" \
    --value "${PEM}" \
    --overwrite
done

kubectl annotate externalsecret mahjong-jwt-keys \
  -n mahjong-autotable force-sync="$(date +%s)" --overwrite
kubectl rollout restart deployment/mahjong-autotable \
  -n mahjong-autotable

shred -u emergency.pem emergency.pk8.pem
```

Note: every in-flight RS256 token signed by the compromised key is
invalidated by this procedure. Notify clients via the operational
channel — public clients fetching the JWKS will transparently
re-authenticate on the next 401.

## 6. Verification commands

```bash
# Confirm all three slots exist + decrypt successfully.
for SLOT in active previous archive; do
  aws ssm get-parameter \
    --name "/mahjong/prod/auth/jwt/rsa-${SLOT}" \
    --with-decryption --query Parameter.Value --output text \
    | head -1
done
# Expected: each prints "-----BEGIN PRIVATE KEY-----" (PKCS#8) or
# "-----BEGIN RSA PRIVATE KEY-----" (PKCS#1).

# Confirm the API is on RS256 + publishes the JWKS.
curl -sfL https://api.prod.mahjong-autotable.com/.well-known/jwks.json \
  | jq '.keys | length'
# Expected: a positive integer (typically 1; may be up to 3 if all
# slots are populated with distinct keys).

# Confirm the OIDC discovery document is published.
curl -sfL https://api.prod.mahjong-autotable.com/.well-known/openid-configuration \
  | jq '.issuer, .jwks_uri, .token_endpoint, .grant_types_supported'
```

## 7. IAM permissions

The Mahjong-Autotable pod IAM role (`mahjong-{env}-api-role`) needs:

```jsonc
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": ["ssm:GetParameter", "ssm:GetParameters"],
      "Resource": [
        "arn:aws:ssm:us-east-1:*:parameter/mahjong/{env}/auth/jwt/rsa-active",
        "arn:aws:ssm:us-east-1:*:parameter/mahjong/{env}/auth/jwt/rsa-previous",
        "arn:aws:ssm:us-east-1:*:parameter/mahjong/{env}/auth/jwt/rsa-archive"
      ]
    },
    {
      "Effect": "Allow",
      "Action": ["kms:Decrypt"],
      "Resource": "arn:aws:kms:us-east-1:*:alias/mahjong-{env}-secrets"
    }
  ]
}
```

The operator IAM role used to run the rotation procedure (typically
the SRE on-call) needs the `PutParameter` + `kms:Encrypt`
counterparts on the same resources.

## 8. Cross-references

* [`docs/jwt-rotation.md`](jwt-rotation.md) — canonical rotation policy, §8 is the matching code-side procedure.
* [`docs/secret-management.md`](secret-management.md) — broader secret-management policy + KMS conventions.
* [`infra/k8s/overlays/prod/jwt-keys-secret.yaml`](../infra/k8s/overlays/prod/jwt-keys-secret.yaml) — ESO ExternalSecret mounting the SSM values into the cluster.
* [`tests/Mahjong.Autotable.Api.Tests/Phase_K_W7/Bishop/JwtRotationE2ETests.cs`](../src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W7/Bishop/JwtRotationE2ETests.cs) — end-to-end pin: a pre-rotation token MUST validate against the post-rotation host.
