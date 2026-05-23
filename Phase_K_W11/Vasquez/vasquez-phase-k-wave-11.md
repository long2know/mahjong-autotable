# Vasquez — Phase K Wave 11 (QA bring-up)

**Date:** 2026-10-16
**Branch:** `stlong/phase-k-wave-11-bringup`
**Author:** `Vasquez (QA) <vasquez@squad.mahjong>`

## Summary

Ship the Phase K Wave 11 QA bring-up: broaden the lane-map
shared-file registry to cover `Shims/*` (four-author) and the
`pwa-audit.yml` / `pwa-builder.yml` workflow pair (Hicks +
Apone), document the §4.1 screenshot walkthrough + 422
troubleshooting one-liner PATCH for Stephen's branch-protection
re-prompt, flip the W10 soft-pins on Janus readiness + Redis
idempotency + the 480 KB three-renderer cap to hard-asserts,
ship 7 forward-stage W11 contract tests (~95 facts), close the
W10 test-architecture coverage gaps with 3 gap-fill integration
tests (RedisIdempotencyStore, JanusReadinessSupervisor,
SignalRBackpressure), rename the `Wave1ThroughKW10` regression
class to `Wave1ThroughKW11` + append 13 new W11 smokes,
add 6 Playwright specs pinning the W11 frontend surfaces, and
push the backend gate from 2108/0/0 to **2282/0/0** while
preserving the 25-wave zero-skip streak.

## Deliverables (8)

1. **Lane-map broadening** (`tests/ci/lane-map.json`):
   - Added `shims_shared` (bishop|hicks|apone|vasquez,
     primary vasquez) — Phase J `Shims/*` cross-pane glue.
   - Added `pwa_audit_workflow_shared` (hicks|apone,
     primary apone) — `pwa-audit.yml` + `pwa-builder.yml`.
   - `tests/ci/check-cross-lane-bundling.sh` extended:
     `is_shared_file()` + `shared_file_authors()` recognise
     both new path patterns. Lane-discipline repo-mode
     baseline unchanged (51 historical pre-W6 violations).

2. **Stephen branch-protection re-prompt — §4.1** in
   `docs/agent-handoff-protocol.md`:
   - Five-step screenshot walkthrough A→E
     (Settings → Branches → Protect → Required checks →
     contexts list).
   - 422 troubleshooting clause (context spelling, branch
     pattern, fine-grained PAT scope).
   - One-liner `gh api -X PATCH` recipe so Stephen can flip
     the gate without the UI when convenient.

3. **DbSerial migration audit** — documented in §4.4 of
   `docs/test-architecture.md` as a W12 open-gap entry. The
   3-parallel `dotnet test` flake-detection harness will be
   added once Bishop tags the candidate test classes with
   `[Collection("DbSerial")]` in W12.

4. **W10 soft-pin → W11 hard-flip** — 4 facts flipped:
   - `BishopW10JanusGradualDegradationTests.JanusReadinessLevel_HasThreeCanonicalLevels`
     — hard-asserts the three canonical enum values.
   - `BishopW10JanusGradualDegradationTests.JanusSupervisor_HasLevelProperty`
     — hard-asserts `CurrentLevel` is enum-typed.
   - `BishopW10RedisIdempotencyClientTests.RedisIdempotencyStore_Ctor_Accepts_ConnectionMultiplexer`
     — hard-asserts the W10 ctor lands.
   - `BishopW10RedisIdempotencyClientTests.RedisIdempotencyStore_HasWriteMethod`
     — hard-asserts the W10 `Record(IdempotencyRecord)` API.
   - `HicksW10FrontendContractTests.ThreeRendererBig_W10_HardCap_480KB`
     — hard-asserts ≤ 480 KB (K10 entry is at 466,395 bytes).

5. **7 forward-stage W11 contract test files** under
   `src/backend/tests/Mahjong.Autotable.Api.Tests/Phase_K_W11/Vasquez/`:
   - `BishopW11FideSwissPairingTests.cs` (8 facts —
     FIDE C.04.1 Swiss + Buchholz + Berger).
   - `BishopW11TileReferenceBinaryCodecTests.cs` (8 facts —
     binary codec round-trip + nibble layout).
   - `BishopW11JanusMountpointMetricsTests.cs` (8 facts —
     eviction counter + age-at-publish histogram).
   - `BishopW11EfCommentaryStorePersistenceTests.cs` (8 facts
     — EF storage + retention sweep + pagination).
   - `BishopW11OAuthIntrospectionTests.cs` (8 facts — RFC
     7662 introspection endpoint).
   - `HicksW11FrontendContractTests.cs` (~17 facts — 475 KB
     shader cap, PWA Builder Edge/Chrome/Safari, LH13
     baseline, Vite cache hit-rate, real screenshots,
     `?action=` routing, W10 regression pins).
   - `AponeW11InfraContractTests.cs` (~20 facts — prod
     Redis, Argo auth ingress, Terraform prod CLI, JWT
     rotation rehearsal, multi-region probes, CHANGELOG
     0.20.0, W9/W10 pins).
   - `VasquezW11SelfLaneTests.cs` (~25 facts — lane-map
     broadenings, handoff §4.1+§5.9, test-architecture
     §4.3+§4.4, gap-fill class existence, Playwright
     spec existence, regression rename pin).

6. **3 gap-fill integration tests** closing the W10
   test-architecture §4.2 coverage gaps:
   - `RedisIdempotencyStoreIntegrationTests.cs` — full
     `TryGet → Record → TryGet → Remove → TryGet` round
     trip via in-memory `IIdempotencyRedis` fake; 5 facts.
   - `JanusReadinessSupervisorIntegrationTests.cs` — enum
     3-value invariant, supervisor namespace, public
     `CurrentLevel`, probe/update seam (BackgroundService);
     5 facts.
   - `SignalRBackpressureIntegrationTests.cs` —
     broadcaster type, namespace, Broadcast/Enqueue/Send
     method, queue-depth telemetry, DI ctor with `ILogger`;
     5 facts.

7. **6 new Playwright specs** under
   `src/frontend/autotable-src/tests/e2e/`:
   - `shader-chunk-475-hard.spec.ts`
   - `pwa-builder-platforms.spec.ts`
   - `lh13-baseline-calibration.spec.ts`
   - `cache-hit-rate.spec.ts`
   - `manifest-screenshots-real.spec.ts`
   - `deep-link-action-routing.spec.ts`

8. **KW10 → KW11 regression rename + 13 W11 smokes**:
   - `Wave1ThroughKW10RegressionTests.cs` →
     `Wave1ThroughKW11RegressionTests.cs` (`git mv`;
     class + ctor + doc-comment + dbserial assembly
     anchor all updated).
   - 13 new W11 smokes appended targeting:
     FideC04 Swiss, TileReference binary, EfCommentaryStore,
     OAuthIntrospection, pwa-builder.yml,
     jwt-rotation-rehearsal.yml, argo-rollouts-ingress-auth
     manifest, docs/swiss-pairing.md,
     docs/jwt-rotation-rehearsal.md,
     docs/edge-region-probes.md, docs/frontend-routing.md,
     CHANGELOG 0.20.0, and the Vasquez-lane lane-map
     hard-assert.

## Side-deliverables

- `Phase_K_W11/Vasquez/W11SurfaceSmokeFactsTests.cs` —
  paired W11 surface-smoke harness (~24 facts) covering
  Bishop / Hicks / Apone surfaces with one-fact-per-axis
  breadth. Includes 5 Vasquez self-lane hard-asserts.
- `docs/test-architecture.md` §4.3 (W11 closed gaps) +
  §4.4 (W11+ open-gap inventory) + §4.5 (anti-patterns,
  renamed from §4.3). Gate reference bumped to ≥ 2200/0/0
  / 25-wave zero-skip streak.
- `docs/agent-handoff-protocol.md` §4.1 (screenshot
  walkthrough) + §5.9 (shared-files registry policy with
  4-row table).
- `src/frontend/autotable-src/tests/selectors.md` — W11
  Vasquez QA inventory appended (canonical spec names →
  pinned surfaces).

## Lane-map summary table

| shared_files key                  | paths                                                                | authors                          | primary  |
|-----------------------------------|----------------------------------------------------------------------|----------------------------------|----------|
| `selectors_md_shared` (W8)        | `src/frontend/autotable-src/tests/selectors.md`                      | hicks, vasquez                   | hicks    |
| `agent_handoff_protocol_md_shared` (W10) | `docs/agent-handoff-protocol.md`                              | bishop, hicks, apone, vasquez    | vasquez  |
| **`shims_shared` (W11)**          | `src/backend/src/Mahjong.Autotable.Api/Shims/*`                      | bishop, hicks, apone, vasquez    | vasquez  |
| **`pwa_audit_workflow_shared` (W11)** | `.github/workflows/pwa-audit.yml`, `.github/workflows/pwa-builder.yml` | hicks, apone                     | apone    |

## Backend gate

Target: ≥ **2200 / 0 / 0**.  Hit **2282 / 0 / 0** (+174
vs W10). Zero-skip streak preserved through wave 25.

## W12 forward queue (Vasquez sees from here)

1. **DbSerial migration follow-up** — Bishop tags
   SQLite-heavy classes with `[Collection("DbSerial")]`
   (W12). Vasquez wires the 3-parallel flake-detection
   harness and bumps the gate dependency.
2. **Hard-flip the W11 soft-pins** (Bishop's FIDE C.04,
   EfCommentaryStore, OAuth introspection; Hicks's PWA
   Builder workflow + LH13 baseline; Apone's prod Redis
   secret + Terraform prod + JWT rotation rehearsal).
3. **§4.4 open gaps** — pick up Bishop's EF migration
   parallel-run + multi-region probe negative-path +
   PWA Builder install-test cross-platform validation.
4. **Branch-protection follow-through** — confirm Stephen
   has applied the §4.1 walkthrough and the
   `lane-discipline-pr` context is required-for-merge on
   `main` / `release/*`.
5. **Lane-map** — when the W12 wave introduces new
   cross-pane glue, add the next `*_shared` entry following
   the §5.9 registry policy template.
