# Per-Tenant JWKS Rotation

> Phase K Wave 16 — Bishop (Backend). Operator runbook for the
> per-tenant JWKS rotation surface, layered on top of the W11–W15
> JWT rotation pipeline.

## 1. What is this?

Multi-tenant deployments need to rotate signing keys independently per
tenant — one tenant's compliance event should not require a global key
rotation across the entire fleet. The per-tenant JWKS rotation surface
adds:

- A **policy row** per tenant (`PerTenantJwksRotationPolicy`):
  `activeKid`, `previousKid`, `rotationStartUtc`, `rotationCompleteUtc`,
  and an optional per-tenant `overlapWindowDays` override.
- A **validator** (`PerTenantJwksRotationValidator`) that decides whether
  signing for a given tenant is permitted at the current instant.
- An **admin controller** (`PerTenantRotationAdminController`) that lets
  operators provision, rotate, and decommission tenant policies via
  HTTP.

The surface is **off by default**. Single-tenant deployments keep the
W11+ global rotation pipeline. Set
`JwksRotation:PerTenant:Enabled=true` to opt in.

## 2. Configuration

```jsonc
{
  "JwksRotation": {
    "PerTenant": {
      "Enabled": false,        // master toggle. Off by default.
      "StorageImpl": "InMemory", // "InMemory" (dev) or "Ef" (prod).
      "DefaultOverlapDays": 7  // fallback when the row leaves it 0.
    }
  }
}
```

Per-row overlap-day precedence (the validator's
`OverlapWindowDays(policy)` helper applies these in order):

1. `PerTenantJwksRotationPolicy.OverlapWindowDays > 0` → use the row.
2. `PerTenantJwksRotationOptions.DefaultOverlapDays > 0` → use the
   option.
3. Else → `PerTenantJwksRotationValidator.DefaultOverlapDays` = **7**.

## 3. Validator verdicts

`PerTenantJwksRotationValidator.EvaluateAsync(tenantId, utcNow)` returns
a `PerTenantRotationVerdict` with one of five
`PerTenantRotationVerdictKind` values:

| Kind                  | Allowed | Meaning                                                                 |
| --------------------- | ------- | ----------------------------------------------------------------------- |
| `ToggleDisabled`      | ✅      | The master toggle is off — single-tenant path, no per-tenant gating.     |
| `NoPolicy`            | ✅      | No row for this tenant — fall back to the global rotation policy.        |
| `PolicyFresh`         | ✅      | Row present; `utcNow ≤ completeUtc + overlap`.                          |
| `WithinOverlapWindow` | ✅      | Row present; `utcNow ∈ [startUtc, completeUtc]` (active rotation).      |
| `Stale`               | ❌      | Row present; `utcNow > completeUtc + overlap`. **Signing blocked.**     |
| `StoreMissing`        | ❌      | Defensive — toggle on but the store seam was not registered.            |

The "hard" gate is `EnforceSigningAsync(tenantId, utcNow)`, which throws
`PerTenantRotationStaleException` for the two `Allowed = false` cases.
The future multi-tenant `JwtIssuingService` will call this immediately
before signing.

## 4. Admin surface

All endpoints are gated on `Role == "admin"` (cookie-session). Wave-16
endpoints:

| Method   | Path                                            | Returns                                     |
| -------- | ----------------------------------------------- | ------------------------------------------- |
| `GET`    | `/api/admin/jwks-rotation/per-tenant`           | List of all policies + count                |
| `GET`    | `/api/admin/jwks-rotation/per-tenant/{tenantId}` | Single policy or 404                        |
| `POST`   | `/api/admin/jwks-rotation/per-tenant`           | 201 (new) or 200 (upsert)                   |
| `PUT`    | `/api/admin/jwks-rotation/per-tenant/{tenantId}` | 200 — full-row replace by route id          |
| `DELETE` | `/api/admin/jwks-rotation/per-tenant/{tenantId}` | 204 — soft-delete via overlap=0 + audit row |

Auth precedence: 401 (no session) → 403 (non-admin) → 503
(`per-tenant-disabled` when the toggle is off) → 200/201/204.

Every successful write emits a `ReconnectAuditEntry` with
`Kind = "auth.jwks.per-tenant.{created|updated|deleted}"` and
`Detail = tenantId` so the audit dashboard can replay the rotation
history.

## 5. Rotation procedure

The canonical "rotate tenant X" workflow:

1. **T-7 days**: operator updates `previousKid` to the currently-active
   kid, sets `activeKid` to the new kid, and stamps
   `rotationStartUtc = now()` + `rotationCompleteUtc = now() + 7d`.
   ```bash
   curl -X PUT \
     -H "Cookie: session=…" \
     -H "Content-Type: application/json" \
     -d '{
       "tenantId": "acme",
       "activeKid": "acme-2026-06",
       "previousKid": "acme-2026-05",
       "rotationStartUtc": "2026-06-01T00:00:00Z",
       "rotationCompleteUtc": "2026-06-08T00:00:00Z",
       "overlapWindowDays": 7
     }' \
     https://api.example.com/api/admin/jwks-rotation/per-tenant/acme
   ```
   The validator's verdict during T..T+7 is `WithinOverlapWindow` —
   both kids accepted.

2. **T+7 to T+14**: verdict transitions to `PolicyFresh`. New tokens
   minted with the new kid; old kid still accepted for unexpired
   tokens.

3. **T+14**: verdict transitions to `Stale`. Signing for this tenant is
   hard-blocked until the operator stamps a new row. Alerts fire from
   the paired SLO dashboard.

## 6. Failure modes

- **Toggle on but no store registered** → `StoreMissing` verdict on
  every call. The validator surfaces `Allowed = false` so signing is
  blocked rather than silently downgraded.
- **Toggle on but no row for tenant** → `NoPolicy` verdict, signing
  **allowed**. Operators are expected to provision a row before
  enabling the toggle for a given tenant; the fall-through preserves
  the global path for un-onboarded tenants.
- **DateTimeOffset rounding** → All timestamps stored as
  `DateTimeOffset` (W16 widening). The W12+ `JwtStagedRotationPolicy`
  also gained `DateTimeOffset` overloads in W16 so the two surfaces
  agree on instant arithmetic.

## 7. Wire shape — error envelopes

`PerTenantRotationStaleException` maps to:

```json
{
  "error": "per-tenant-rotation-stale",
  "tenantId": "acme",
  "staleAfter": "2026-06-15T00:00:00+00:00"
}
```

`StoreMissing` maps to:

```json
{
  "error": "per-tenant-rotation-store-missing"
}
```

Both reason strings are wire-stable across waves.

## 8. Wave 17 open work

- Hook the validator into the actual `JwtIssuingService` for the
  multi-tenant signing path (the W16 surface is side-channel by
  design — wired only at admin endpoints + the future multi-tenant
  caller).
- Add a sweeper that auto-emits warnings 24h before `staleAfter`.
- Per-tenant alert routing (paired with the
  `docs/signalr-sequence-slo.md` per-tenant work item).
