# Production secret-rotation runbook

> Phase K Wave 1 — Apone (DevOps).
>
> Operational handbook for **rotating** the credentials Mahjong
> Autotable uses in production. Companion to
> [`secret-management.md`](secret-management.md) (how secrets get
> *into* the runtime) and [`secrets.md`](secrets.md) (which strings
> are and aren't secrets in the first place). This document covers
> the **lifecycle** of each secret: cadence, procedure, validation,
> and known blast-radius.

## Rotation matrix

| Secret | Cadence | Blast radius on rotation | Rollback budget |
| --- | --- | --- | --- |
| `Auth:Google:ClientSecret` | **Quarterly** (90 days) | New sign-ins via Google fail for ≤ ESO sync interval (default 1 h); in-flight sessions unaffected | 7 days (Google honours both old + new during overlap) |
| `Auth:GitHub:ClientSecret` | **Quarterly** (90 days) | Same as Google | 7 days (GitHub honours both during overlap) |
| `ConnectionStrings:Postgres` (or `SqlServer`) | **Annual** (365 days) | All running pods need to re-establish DB connections after restart; readiness probe goes red for ≤ 30 s | DB role must keep the OLD credential valid until the rolling-update completes; drop the old role after |
| `Sentry:Dsn` | **Never** (compromise-only) | Sentry telemetry pauses for ≤ ESO sync; existing breadcrumbs/events already in flight finish via the old DSN | N/A — replace and move on |
| `Auth:ReconnectTokenSigningKey` | **Never** (compromise-only) | **All live WS reconnect tokens invalidate immediately.** Every connected player gets a `kick-with-reason: token-rotated`; new connections work normally | 0 — there is no overlap (single-key signer) |
| `Auth:MagicLinkSigningKey` | **Never** (compromise-only) | All outstanding magic-link emails fail validation. Users must request a new link | 0 — same single-key signer |
| `Auth:JwtSigningKey` | **180 days** (see `secret-management.md`) | JWT-backed cookies invalidate after ≤ 1 h (current cookie lifetime) | Wave-9 fallback-key list pattern (planned); today a hard rotation forces all users to re-sign-in |
| `Auth:CookieEncryptionKey` | **365 days** | DataProtection ring rotation; all auth cookies invalidate | Communicate the forced sign-out 7 days in advance |
| Cloudflare API token | **365 days** | CI / kubectl scripts using the token fail until updated | 0 — token is single-value, replace and update all consumers |
| `ghcr.io` push token (PAT) | per operator PAT expiry | Image-push workflow fails | 7 days (rotate before expiry, update repo secret) |

> `Auth:JwtSigningKey` and `Auth:CookieEncryptionKey` were documented
> at Wave 8 in [`secret-management.md`](secret-management.md). This
> runbook is the day-of-rotation companion.

## Where each secret lives

All secrets target a Kubernetes `Secret` named `mahjong-app-secrets`
(staging) or the prod analogue. The canonical write path is:

```
            ┌─ HashiCorp Vault ────┐
backend ←── ESO ←── AWS Secrets Manager  (canonical — Wave 5/6 pattern)
            └─ GCP / Azure / 1Password ─ (alternative backends)
```

See [`secret-management.md`](secret-management.md) §3 for the ESO
diagram and the `infra/k8s/overlays/{staging,prod}/secret-template.yaml`
template.

The key names use double-underscore separators (`__`) so the .NET
configuration binder maps `Auth__Google__ClientSecret` →
`Auth:Google:ClientSecret`.

## General procedure

Every rotation follows the same five-step shape; the per-secret
sections below specialise it.

1. **Mint the new secret value** in the upstream system (Google
   console, AWS Secrets Manager, `openssl rand`, etc).
2. **Write to the secret backend** (AWS Secrets Manager). Do NOT
   overwrite the old value yet — write the new value to a SEPARATE
   key first (e.g. `auth__google__clientsecret_next`) so you have an
   in-place rollback path.
3. **Promote.** Overwrite the canonical key
   (`auth__google__clientsecret`) with the new value.
4. **Sync.** Either wait for the ESO refresh interval (default 1 h)
   or force it: `kubectl annotate externalsecret mahjong-app-secrets
   force-sync=$(date +%s) --overwrite`. Then trigger a rolling restart:
   `kubectl rollout restart deployment/mahjong-autotable`.
5. **Validate.** Run the smoke checks listed in the per-secret
   section. ROLL BACK by writing the previous value back to the
   canonical key if validation fails.

## OAuth client secrets — Google + GitHub (quarterly)

The OAuth client secret is the password Google / GitHub uses to
authenticate the backend's token-exchange calls. Both providers
honour **two valid secrets per client** during a rotation window —
that's the rollback path.

### Procedure (Google)

```bash
# 1. Mint a new secret in the Google Cloud Console.
#    https://console.cloud.google.com/apis/credentials
#    → Open the OAuth 2.0 Client → "Add Secret" → copy the value.
#    Both the old and new secret will be listed; both are valid until
#    you delete the old one.

# 2. Write to AWS Secrets Manager.
aws secretsmanager put-secret-value \
    --secret-id mahjong/prod/app \
    --secret-string "$(aws secretsmanager get-secret-value \
        --secret-id mahjong/prod/app \
        --query SecretString --output text \
      | jq --arg v "$NEW_GOOGLE_SECRET" '.auth__google__clientsecret = $v')"

# 3. Force ESO sync + rolling restart.
kubectl -n mahjong annotate externalsecret mahjong-app-secrets \
    force-sync="$(date +%s)" --overwrite
kubectl -n mahjong rollout status externalsecret/mahjong-app-secrets
kubectl -n mahjong rollout restart deployment/mahjong-autotable

# 4. Validate — initiate a fresh Google sign-in from the prod URL.
#    The auth-flow smoke can do this for the dev surface:
PORT=18081 IMAGE=ghcr.io/long2know/mahjong-autotable:latest \
    tests/smoke/auth-flow-smoke.sh

# 5. Delete the OLD secret in the Google console ONLY after step 4
#    succeeds and you've watched the prod sign-in logs for a few hours.
```

### Procedure (GitHub)

```bash
# 1. Mint a new secret in the GitHub OAuth app settings.
#    https://github.com/settings/applications → "Generate a new client secret".
#    GitHub keeps the old secret valid until you explicitly delete it.

# 2-5. Same as Google, substituting `auth__github__clientsecret`.
```

### Rollback

Revert the AWS Secrets Manager value to the prior one, force ESO sync
+ rolling restart. The OLD provider secret is still valid for the
rollback window (don't delete it in the provider console until
validation completes).

## Database connection strings (annual)

Postgres / SqlServer connection strings carry a password (the DB
user's). Rotating means:

1. Create the NEW DB user/password (or assign a new password to the
   existing user — Postgres supports this without downtime via
   `ALTER USER mahjong WITH PASSWORD '<new>';`).
2. Update AWS Secrets Manager.
3. ESO sync + rolling restart (zero-downtime — `RollingUpdate`
   strategy keeps old pods serving until new pods pass readiness).
4. Drop the OLD password / OLD user.

### Procedure (Postgres)

```bash
# 1. ALTER role in Postgres. Or, if you want a fully separate user:
psql -h $PG_HOST -U postgres -c "
  CREATE ROLE mahjong_v2 WITH LOGIN PASSWORD '$NEW_PG_PASSWORD';
  GRANT mahjong_role TO mahjong_v2;
"

# 2. Write the new connection string to AWS Secrets Manager.
aws secretsmanager put-secret-value \
    --secret-id mahjong/prod/app \
    --secret-string "$(aws secretsmanager get-secret-value \
        --secret-id mahjong/prod/app \
        --query SecretString --output text \
      | jq --arg cs "Host=$PG_HOST;Port=5432;Database=mahjong_autotable;Username=mahjong_v2;Password=$NEW_PG_PASSWORD" \
            '.connectionstrings__postgres = $cs')"

# 3. ESO sync + rolling restart.
kubectl -n mahjong annotate externalsecret mahjong-app-secrets \
    force-sync="$(date +%s)" --overwrite
kubectl -n mahjong rollout restart deployment/mahjong-autotable
kubectl -n mahjong rollout status deployment/mahjong-autotable --timeout=10m

# 4. Validate — /health/detail surfaces DB connectivity.
curl -fsS https://mahjong.example.com/health/detail | jq .

# 5. Drop the OLD role.
psql -h $PG_HOST -U postgres -c "REVOKE mahjong_role FROM mahjong;
                                  DROP ROLE mahjong;"
```

### Procedure (SqlServer)

Same shape; substitute `ALTER LOGIN mahjong WITH PASSWORD = '<new>';`
and the SqlServer connection-string key
(`connectionstrings__sqlserver`).

### Rollback

Re-write the prior connection string to AWS Secrets Manager and roll
back. The OLD password/user must NOT be dropped (step 5) until the
window in which a rollback is acceptable has passed (typically 7
days).

## Sentry DSN (never, except on compromise)

The DSN is a public-ish key (it lives in the frontend `<meta>` tag —
see `src/sentry.ts`). Anyone who can read prod traffic can read it.
**Rotation cost is high** (existing events stop being attributed) and
**rotation benefit is low** (the worst an attacker can do with a
leaked DSN is consume your Sentry quota).

### When to rotate

- The DSN was published to a public source repo or pasted to a
  user-facing log.
- Sentry sends an explicit "this DSN was disclosed" notification.
- A regulatory mandate (rare).

### Procedure

```bash
# 1. Regenerate the DSN in Sentry → Settings → Client Keys.
#    Both old and new DSN are valid until you DISABLE the old key.

# 2. Update AWS Secrets Manager `sentry__dsn`.
# 3. ESO sync + rolling restart.
# 4. Update the frontend <meta name="sentry-dsn"> (which the bundle
#    bakes — requires a re-deploy of the frontend, not just the
#    backend pod restart).
# 5. Watch Sentry → Issues for traffic on the NEW DSN. Once stable,
#    DISABLE the old DSN (not delete — disable preserves history).
```

### Rollback

Re-enable the old DSN, revert AWS Secrets Manager, redeploy. Cost is
the few hours of events attributed to the new DSN (orphaned but
queryable).

## Reconnect-token signing key (NEVER except on compromise)

`Auth:ReconnectTokenSigningKey` signs the opaque tokens Bishop's
Wave-9 reconnect surface mints. Tokens are short-lived (currently 5
minutes) and **single-use rotated** (the rotation pattern is built
into the runtime — every reconnect mints a NEW token + invalidates
the previous). The SIGNING KEY itself, however, is shared across all
live tokens and the entire rotation chain.

### Why rotation is destructive

Rotating the signing key:

1. Invalidates every live token across all connected players.
2. Forces a full `kick-with-reason: token-rotated` cycle.
3. Players must re-sign-in (or re-mint via `/api/identity`) to get a
   fresh token.

There is **no overlap window** — the key signs all tokens with the
same secret. The runtime does NOT keep a list of historical signing
keys (this is by design — token forgery resistance trumps rotation
gracefulness for ephemeral reconnect tokens).

### When to rotate

- The key value was leaked.
- Forensic evidence of token forgery in the audit log
  (`ReconnectAuditEntry` rows showing `outcome = reuse-rejected`
  followed by `outcome = accepted-with-forged-signature` — implausible
  without key compromise).

### Procedure

```bash
# 1. Mint a new key.
NEW_KEY=$(openssl rand -base64 64)

# 2. Communicate in-advance to users (announcement banner +
#    Discord / status page). The forced sign-out hits everyone
#    simultaneously.

# 3. Write to AWS Secrets Manager `auth__reconnecttokensigningkey`.
# 4. ESO sync + rolling restart.
# 5. Within ≤ 5 s of pod restart every connected WS gets a
#    'token-rotated' kick. New connects work normally.

# 6. Validate — open a fresh browser, /api/identity, join a game,
#    open WS reconnect probe path, confirm 200.
PORT=18081 IMAGE=ghcr.io/long2know/mahjong-autotable:latest \
    tests/smoke/token-rotation-smoke.sh
```

### Rollback

Re-write the prior key to AWS Secrets Manager, restart. This
re-issues a second forced kick to all currently-connected users; the
rollback DOESN'T re-validate the tokens that were minted during the
brief window with the new key.

## Magic-link signing key (NEVER except on compromise)

`Auth:MagicLinkSigningKey` signs the email magic-link tokens Bishop's
Wave-8 auth surface mints. Tokens are 15-minute single-use. Same
destructive-rotation contract as reconnect tokens.

### Why rotation is destructive

Rotating the signing key invalidates every outstanding magic-link
email. Users who clicked a link AFTER the rotation will see
`magic-link expired or invalid`; they must request a new link.

### When to rotate

- Same triggers as reconnect-token signing key.
- Plus: if a phishing campaign minted magic links to compromised
  inboxes, rotating denies the attacker any open windows.

### Procedure

```bash
# 1. Mint a new key.
NEW_KEY=$(openssl rand -base64 64)

# 2. Communicate to users — magic-link email recipients in the
#    rotation window will be re-prompted to request a new link.

# 3. Write to AWS Secrets Manager `auth__magiclinksigningkey`.
# 4. ESO sync + rolling restart.

# 5. Validate — request a magic link, click it, confirm sign-in.
```

### Rollback

Re-write the prior key. The window during which the NEW key signed
links is lost (those links remain invalid even after rollback — they
were signed with a key the rolled-back runtime no longer recognises).

## Validation summary

| Smoke | Validates |
| --- | --- |
| `tests/smoke/auth-flow-smoke.sh` | `/api/identity` mint + `/api/auth/providers` + `/api/auth/me` |
| `tests/smoke/token-rotation-smoke.sh` | Reconnect-token mint → rotate → reuse-rejected invariant |
| `tests/smoke/chat-flow-smoke.sh` | Chat send/receive end-to-end |
| `tests/smoke/csp-report-smoke.sh` | CSP-report endpoint + DB persistence (Phase K Wave 1) |
| `curl /health/detail \| jq .` | DB pool stats — confirms new connection string works |
| Manual: sign in via Google / GitHub | OAuth client secrets honour the new value |

Run the smokes against a STAGING image first with the rotated
secrets, then against PROD after promotion. Production smoke must
NOT be skipped — a silent "did the credential roll" outage is the
worst-case incident.

## Audit + retention

- Every `kubectl rollout` + Secrets Manager `PutSecretValue` is
  logged to CloudTrail.
- ESO `ExternalSecret` status transitions are visible via
  `kubectl describe externalsecret mahjong-app-secrets`.
- Sentry breadcrumb scrubber + JSON-logger redaction ensure rotated
  values never appear in observability surfaces. If a rotated value
  DOES surface in a log line, that's a logging-discipline bug — file
  it as a security finding (NOT a rotation incident).

## Related docs

- [`secret-management.md`](secret-management.md) — secret injection
  patterns (dev / staging / prod / ESO).
- [`secrets.md`](secrets.md) — audit of what is / isn't a secret in
  the repo.
- [`production-deployment-runbook.md`](production-deployment-runbook.md) —
  end-to-end deploy + rollback procedures (the rotation steps reuse
  the same rolling-update primitives).
- [`image-signing.md`](image-signing.md) — image-signing cosign
  keyless OIDC. Image-signing keys are NOT in this matrix because
  they're ephemeral OIDC certs, not stored secrets.
- [`kubernetes.md`](kubernetes.md) — ESO + Argo CD ordering for
  pre-rollout migrations.

## Calendar

| Quarter | OAuth Google | OAuth GitHub | Postgres | JWT signing | Cookie ring | Cloudflare API |
| --- | --- | --- | --- | --- | --- | --- |
| Q1 (Jan–Mar) | rotate Jan 15 | rotate Jan 22 | — | — | — | rotate Jan 1 |
| Q2 (Apr–Jun) | rotate Apr 15 | rotate Apr 22 | rotate Jun 1 | rotate Jun 15 | — | — |
| Q3 (Jul–Sep) | rotate Jul 15 | rotate Jul 22 | — | — | — | — |
| Q4 (Oct–Dec) | rotate Oct 15 | rotate Oct 22 | — | rotate Dec 15 | rotate Dec 31 | — |

Adjust the calendar to whichever day-of-week is least disruptive to
the userbase. Postgres + cookie-ring rotations are the only ones with
a user-visible side effect (Postgres: ≤ 30 s readiness gap; cookie:
forced sign-out). Schedule those for low-traffic windows.
