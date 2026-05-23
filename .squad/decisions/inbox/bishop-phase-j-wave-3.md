# Bishop — Phase J Wave 3 completion memo

**Branch:** `stlong/phase-j-wave-3-completion`
**Baseline gate:** 418/0/0 (entering this wave).
**Final gate:** 424/0/0 (Vasquez's 6 net-new contract probes for the new
surfaces flipped GREEN — `HealthEndpointTests` × 2 and `WinResultSurfaceTests` × 4).

**Commit SHAs (in landing order):**

| # | Task | SHA       | Why first |
| - | ---- | --------- | --------- |
| 1 | Task 3 — `/health` endpoint                       | `9235859` | Apone's Docker HEALTHCHECK lane needed this before he could finalize the directive — pushed first per the brief. |
| 2 | Task 1 — `IsSelfDraw` + `IsKongReplacement` bools | `75baecc` | Vasquez's Wave 2 `SelfDrawWinContextTests` reflection-defensive fallback; Wave 3 `WinResultSurfaceTests` directly. |
| 3 | Task 2 — canonical pattern ordering API           | `2e84179` | Hicks's result-modal chip strip ordering. |

**Touch-points (strict scope only — Vasquez/Hicks/Apone lanes untouched):**

| File | Kind |
| --- | --- |
| `src/.../Mahjong.Autotable.Api/Program.cs`                                    | Task 2 + Task 3 (two new minimal-API endpoints + process-start anchor) |
| `src/.../Mahjong.Autotable.Api/Changsha/ChangshaDomain.cs`                    | Task 1 (`WinResult.IsSelfDraw` + `WinResult.IsKongReplacement` properties) |
| `src/.../Mahjong.Autotable.Api/Changsha/ChangshaStateMachine.cs`              | Task 1 (populate at both `DeclareSelfDrawWin` + `ResolveHuClaim` construction sites) |
| `src/.../Mahjong.Autotable.Api/Changsha/Runtime/ChangshaGameRuntime.cs`       | Task 1 (`WinDeclared` + `ScoringComplete` SignalR JSON wire surface) |
| `src/.../Mahjong.Autotable.Api/Autotable/AutotableProtocol.cs`                | Task 1 (`WinResultEntry` JSON DTO + `[JsonPropertyName]` for camelCase keys) |
| `src/.../Mahjong.Autotable.Api/Autotable/ChangshaToAutotableTranslator.cs`    | Task 1 (bundle WS collection-entry payload) |
| `src/.../Mahjong.Autotable.Api/Changsha/Patterns/ChangshaPatternOrdering.cs`  | Task 2 (new file — canonical ordering metadata) |

---

## Task 1 — Surface `IsSelfDraw` + `IsKongReplacement` bools on `WinResult`

### Field names (the bits Hicks needs verbatim for the UI)

| Surface | C# property | SignalR/JSON key | Source of truth |
| --- | --- | --- | --- |
| `WinResult`                                                  | `bool IsSelfDraw`        | n/a (server-internal) | Set true in `DeclareSelfDrawWin`, false in `ResolveHuClaim` (both discard + robbing-kong paths). |
| `WinResult`                                                  | `bool IsKongReplacement` | n/a (server-internal) | Set from `state.LastDrawWasKongReplacement` in `DeclareSelfDrawWin`; always false in `ResolveHuClaim`. |
| SignalR `WinDeclared.winResult` + `ScoringComplete.handSummary.winResult` | — | **`isSelfDraw`**, **`isKongReplacement`** | Anonymous-type field name → System.Text.Json camelCases by default. |
| Autotable bundle WS `collection-entry → handResult → winResult` | `WinResultEntry.IsSelfDraw` / `.IsKongReplacement` | **`isSelfDraw`**, **`isKongReplacement`** | Explicit `[JsonPropertyName("isSelfDraw")]` / `[JsonPropertyName("isKongReplacement")]`. |

Both bools are inside the existing `winResult` envelope — Hicks should already be
destructuring `winResult` from both transports, so this is an additive field
read (`winResult.isSelfDraw`, `winResult.isKongReplacement`), no new
subscription / new payload routing needed.

### Backward-compat (deliberately preserved)

* `WinResult.Method` (enum) — unchanged. `Method == WinMethod.SelfDraw` still
  works as the pre-Wave-3 derivation.
* `WinResult.AllPatterns` — unchanged. Still contains
  `WinPattern.KongReplacementWin` when applicable. Wave 2's
  `SelfDrawWinContextTests` reflection-defensive helpers will now prefer the
  explicit bool (via the `prop.PropertyType == typeof(bool)` probe) and
  silently bypass the fallback path; no test rewrite needed on Vasquez's side.

### Semantics (locking the contract for downstream)

* `IsSelfDraw` ⇔ "winning tile arrived via a wall draw" — both regular and
  kong-replacement draws qualify. Strictly equivalent to
  `Method == WinMethod.SelfDraw`; the new bool is a stylistic / wire-shape
  improvement, not a semantic change.
* `IsKongReplacement` ⇔ "the *last* draw on this hand was a kong replacement,
  AND we declared Hu on that draw". This is the same gate the detector
  consults for the `WinPattern.KongReplacementWin` flag. Robbing-the-added-kong
  wins (`WinMethod.RobbingKong`) are NOT kong-replacement wins — the winner
  intercepted the kong rather than drawing its replacement.

---

## Task 2 — Canonical pattern display order

### What shipped

* New static class `Changsha/Patterns/ChangshaPatternOrdering.cs`:
  * `IReadOnlyDictionary<WinPattern, int> Order` — the lookup table.
  * `int GetOrder(WinPattern p)` — defensive accessor; unknown patterns return
    `AlphabeticalFallbackOrder` (999) so future enum additions are
    tail-sorted without throwing.
  * `IReadOnlyList<WinPattern> Sort(IEnumerable<WinPattern>)` — convenience
    helper for any backend caller that wants to mirror the frontend order
    locally (replay export, move-log, etc.).

### Ordering table (lower = render first)

| Rank | `WinPattern` value | Chinese | Reason |
| ---- | ------------------ | ------- | ------ |
|   1  | `HeavenlyHand`       | 天和         | dealer self-draw on initial 14 — highest precedence Big Win |
|   2  | `EarthlyHand`        | 地和         | non-dealer Hu on dealer's first discard |
|   3  | `LastTileFromWall`   | 海底捞月     | last-tile self-draw |
|   4  | `LastDiscardCatch`   | 河底捞鱼     | last-tile discard claim |
|   5  | `KongReplacementWin` | 杠上开花     | kong-replacement self-draw |
|  *6* | *(reserved — `RobbedKong`)* | 抢杠胡 | Not a `WinPattern`; lives on `WinResult.IsRobbedKong`. Slot reserved so future promotion to enum doesn't shift everything else. |
|  *7* | *(reserved — `NineGates`)*   | 九莲宝灯 | not yet implemented |
|   8  | `NineTerminals`      | 九幺         | Big Win |
|   9  | `AllPungs`           | 碰碰胡       | Big Win |
| *10* | *(reserved — `AllConcealed`)* | 门前清 | not yet implemented |
|  11  | `SevenPairs`         | 七对子       | Big Win |
| *12* | *(reserved — `SelfDraw`)*     | 自摸    | Not a `WinPattern`; lives on `WinResult.IsSelfDraw`. |
| *13* | *(reserved — `SingleWait`)*   | 独张    | not yet implemented |
| 100  | `FullFlush`          | 清一色       | alphabetical fallback among unranked |
| 101  | `Standard`           | (none)       | alphabetical fallback among unranked |

Reserved integer slots are intentional — when future patterns ship, they slot
in at their reserved rank without re-numbering everything below them.

### Wire surface for Hicks

**New endpoint:** `GET /api/changsha/pattern-ordering`

Returns a flat JSON object keyed by the **same camelCase wire names** the
SignalR `winResult.allPatterns` array uses (mapping mirrors
`ChangshaGameRuntime.WinPatternToWire` + `ChangshaToAutotableTranslator.WinPatternToWire`),
mapped to the canonical integer order.

```bash
$ curl http://localhost:5114/api/changsha/pattern-ordering
{"heavenlyHand":1,"earthlyHand":2,"lastTileFromWall":3,"lastDiscardCatch":4,
 "kongReplacementWin":5,"nineTerminals":8,"allPungs":9,"sevenPairs":11,
 "fullFlush":100,"standard":101}
```

Recommended frontend usage (Hicks): fetch the map **once at boot**, cache it,
then sort `winResult.allPatterns` by `map[name] ?? 999` before rendering.
This keeps per-broadcast payload size unchanged (no sort-key injection into
the win payload).

---

## Task 3 — `/health` endpoint

### Wire format (for Apone)

**Endpoint:** `GET /health` (Minimal API in `Program.cs`; distinct from the
existing `/api/health` — that one remains untouched for the frontend's legacy
probe).

**Status code:** always `200 OK` when the process is responsive. (No DB
liveness check on this surface — it's a process-liveness probe, not a
readiness probe. If/when Apone needs a readiness probe with a deeper
dependency check, that's a separate endpoint.)

**Body shape:**

```json
{
  "status": "healthy",
  "buildSha": "abc123…",
  "uptime": "00:01:23.4567890",
  "version": "1.0.0.0"
}
```

| Key       | Source                                                                    | Notes |
| --------- | ------------------------------------------------------------------------- | ----- |
| `status`  | literal `"healthy"`                                                       | Stable; the smoke script grep-asserts presence, not the literal. |
| `buildSha`| `Environment.GetEnvironmentVariable("BUILD_SHA")` (or `"dev"` if unset)   | Apone passes the real git SHA in via the Dockerfile build-arg → ENV. Local `dotnet run` reports `"dev"`. |
| `uptime`  | `DateTimeOffset.UtcNow - processStartTime`                                | `processStartTime` is captured at module-load **before** `WebApplication.CreateBuilder` so it reflects host process start, not first-request time. Serialized as an ISO-8601 `TimeSpan` (e.g. `"00:01:23.4567890"`). |
| `version` | `typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown"`     | Assembly version — currently `1.0.0.0` until the csproj surfaces a real `<Version>` element. |

### Verification (local)

```bash
# without BUILD_SHA
curl http://localhost:5114/health
# {"status":"healthy","buildSha":"dev","uptime":"00:00:07.39","version":"1.0.0.0"}

# with BUILD_SHA
BUILD_SHA=test-sha-123 dotnet run
curl http://localhost:5114/health
# {"status":"healthy","buildSha":"test-sha-123","uptime":"00:00:09.18","version":"1.0.0.0"}
```

Both paths confirmed working before commit. Vasquez's
`HealthEndpointTests` (added concurrently on this branch) covers:

1. `HealthEndpoint_ReturnsOk_WithExpectedShape` — 200 + all 4 documented keys present + `status` is a non-empty string.
2. `HealthEndpoint_BuildSha_DefaultsToDev_WhenUnset` — explicit pin on the `"dev"` fallback when `BUILD_SHA` is null.

Both flipped GREEN as soon as my Task 3 commit landed.

---

## Cross-lane handoffs

* **Apone:** `/health` is live on `9235859`. Wire format above is locked.
  Recommended Dockerfile snippet:

  ```dockerfile
  ARG BUILD_SHA=dev
  ENV BUILD_SHA=${BUILD_SHA}
  HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -fsS http://localhost:8080/health || exit 1
  ```

  If Apone wants the HEALTHCHECK to *also* assert a known buildSha, he can
  pipe through `grep -q '"buildSha":"dev"'` — but I'd recommend NOT doing
  that since it would make the check fail any time someone forgets the
  `--build-arg BUILD_SHA=…`. Better to grep just `'"status":"healthy"'`.

* **Hicks:** Two contract additions to consume:
  1. `winResult.isSelfDraw` + `winResult.isKongReplacement` (camelCase) are
     now on both transports (SignalR `WinDeclared` + autotable bundle
     `collection-entry`). The legacy `winType` / `allPatterns` paths still
     work — these are additive.
  2. `GET /api/changsha/pattern-ordering` returns the canonical sort map.
     Fetch once at boot, then `allPatterns.sort((a, b) => (map[a] ?? 999) -
     (map[b] ?? 999))` before rendering the chip strip.

* **Vasquez:** Your reflection-defensive helpers in `SelfDrawWinContextTests`
  will now hit the bool surface directly (the `prop is not null` branch).
  The Wave 3 `WinResultSurfaceTests` lock the explicit-axis contract.
  Nothing needs to change on your side.

---

## Test gate summary

| Stage | Pass / Fail / Skip |
| ----- | ------------------ |
| Baseline (pre-changes)               | 418 / 0 / 0 |
| Post-Task 3 (/health)                | 418 / 0 / 0 |
| Post-Task 1 (bool surfaces)          | 418 / 0 / 0 (Vasquez's Wave-3 tests not yet present) |
| Post-Task 2 (ordering)               | 424 / 0 / 0 (Vasquez's 6 new tests now visible — 2 health + 4 surface) |
| Final                                | **424 / 0 / 0** ✅ |
