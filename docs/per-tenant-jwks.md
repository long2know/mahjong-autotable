# Per-tenant JWKS rotation

> **Phase K Wave 15 — Bishop.** Per-tenant JWKS rotation policy
> table. The W14 staged rotation policy
> (`JwtStagedRotationPolicy`) carries one **global** rotation
> window — sufficient for single-tenant deployments but coarse
> for multi-tenant clusters where one customer can be mid-rotation
> while a sibling tenant is steady state. W15 lands the durable
> per-tenant row keyed by a stable tenant identifier, gated behind
> an opt-in toggle. See `docs/jwt-rotation.md` for the global
> rotation contract.

## §1 — Why per-tenant?

The W14 surface assumes the operator coordinates rotation
windows across every tenant. In a multi-tenant deployment that
constraint forces a chain of coordinated maintenance windows —
in practice operators want each tenant to schedule its own
rotation against its own ops calendar.

The W15 surface adds:

* A durable `PerTenantJwksRotationPolicies` table keyed by
  `TenantId`.
* `RotationStartUtc` + `RotationCompleteUtc` typed as
  `DateTimeOffset` (the W14 path used `DateTime` — W15 widens
  the type so an operator scheduling rotations in their local
  timezone keeps the offset across persistence).
* `IPerTenantJwksRotationStore` seam with `InMemory` + `Ef`
  implementations.
* An opt-in toggle — when disabled the table exists but no
  lookup path consults it; the global rotation policy remains
  authoritative.

## §2 — Entity

```csharp
public sealed class PerTenantJwksRotationPolicy
{
    public string TenantId { get; set; }            // PK, max 128
    public DateTimeOffset RotationStartUtc { get; set; }
    public DateTimeOffset RotationCompleteUtc { get; set; }
    public string ActiveKid { get; set; }           // max 128
    public string PreviousKid { get; set; }         // max 128
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public bool IsWithinOverlapWindow(DateTimeOffset utcNow) =>
        utcNow >= RotationStartUtc && utcNow <= RotationCompleteUtc;
}
```

The `DateTimeOffset` widening is the headline change. The W14
`DateTime` columns stripped the offset on serialisation, so an
operator scheduling a rotation for `2026-01-01T09:00:00-08:00`
saw the row come back as `2026-01-01T17:00:00Z` — correct
semantically but misleading at the dashboard render. The W15
columns persist the offset verbatim.

## §3 — Store seam

```csharp
public interface IPerTenantJwksRotationStore
{
    Task<PerTenantJwksRotationPolicy> UpsertAsync(
        PerTenantJwksRotationPolicy policy, CancellationToken ct = default);
    Task<PerTenantJwksRotationPolicy?> GetAsync(
        string tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<PerTenantJwksRotationPolicy>> ListAsync(
        CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
}
```

Implementations:

| Type                                   | Use case                               |
| -------------------------------------- | -------------------------------------- |
| `InMemoryPerTenantJwksRotationStore`   | Dev / smoke / contract tests           |
| `EfPerTenantJwksRotationStore`         | Production (Sqlite / Postgres / SqlServer) |

Migrations land in all three EF providers as
`Phase_K_W15_PerTenantJwksRotation`.

## §4 — Configuration

| Key                                       | Default      | Notes                                                              |
| ----------------------------------------- | ------------ | ------------------------------------------------------------------ |
| `JwksRotation:PerTenant:Enabled`          | `false`      | Master toggle — false → global policy authoritative                |
| `JwksRotation:PerTenant:StorageImpl`      | `"InMemory"` | `"InMemory"` or `"Ef"` (case-insensitive)                          |

When `Enabled = false` (the default) the store is **not** wired
into the validator surface — the global
`JwtStagedRotationPolicy` continues to drive overlap-window
enforcement. Multi-tenant operators flip the toggle and populate
the table, then a follow-up wave wires the validator to consult
the per-tenant row first and fall back to the global window.

## §5 — Forward roadmap

W15 lands the **table + opt-in toggle + store seam** only. The
validator integration (resolving the per-tenant row at JWT
validation time + falling back to the global window) is deferred
to a future wave so the surface boundary is reviewable in
isolation. See `Phase_K_W15/Bishop/charter.md` for the rationale.

## §6 — Contract pins

Hard-asserted in
`tests/Mahjong.Autotable.Api.Tests/Phase_K_W15/Bishop/PerTenantJwksRotationStoreTests.cs`:

* Upsert inserts a new row and updates an existing row in place.
* Get returns null for unknown tenant.
* List returns rows ordered by tenant id.
* Count reflects stored row count.
* `RotationStartUtc` + `RotationCompleteUtc` types are
  `DateTimeOffset` (W14 → W15 widening).
* Offset is preserved on round-trip.
* `IsWithinOverlapWindow` true for inside-window timestamps,
  false for before / after.
* Options default to `Enabled = false`, `StorageImpl = "InMemory"`.
* Empty `TenantId` rejected with `ArgumentException`.
