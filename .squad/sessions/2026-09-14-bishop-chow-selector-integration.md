# Bishop — direct canonical Chow selector before queuing

September14,2026. **26 executed/26 PASS/0skip, no source/test input drift. Independent source review required; qualification0.**

Current ClaimAsync already rejected invalid choices before PendingClaims through a private-copy ResolveClaim preflight. After Burke exposed the existing pure SelectChowTiles helper, replaced that clone preflight with the exact canonical selector call under the same lock, AFTER offered-type validation and BEFORE PendingClaims. This avoids cloning/simulated resolution while preserving immediate CHOW_TILES_INVALID, held/distinct/two/suit/sequence checks and null/empty legacy choice compatibility. No validator duplication, timer-first cancellation, catch/rollback, or engine/test/frontend edit.

Changed only src/backend/src/Mahjong.Autotable.Api/Changsha/Runtime/ChangshaGameRuntime.cs:
- New SHAf44fb055199bd0b48f8b4de7c1f5657c14393511002a059fa7fd9e6101613e74
- Before SHA45d043e778a0145d687a2895b42e30420687cf21fdde26fa63cce040be3033a0.

Actual tested engine0c8b838b71746d4e772c651bd278a7d883b650871849f71c6cbfbe780286636f (later current source than3151be59 in the queued message); CanonicalEngineRepairTests remains cef9d332206b0883d890ec741c209837dd0af4fdfa887157edf341dbbb4398b3. No changes by Bishop to either.

GREEN scope: exact RuntimeInvalidChow_RejectsBeforeQueuing_AndValidClaimCanStillComplete discriminator1/1, legacy human claim-wire9/9, actual bot-Chow continuation6/6, unchanged bounded runtime partials10/10. The deferred higher-priority-Hu competitor case immediately rejects duplicate partners, retains state/version/window, then accepts a valid explicit Chow and normal passes without extra wall draw. All26 pass; earlier RED records remain historical and were not relabeled.

Evidence root session-files/qualification/2026-09-12/bishop-actions/chow-selector-integration/:
- review-manifest.json SHA005df1b0078a003bb1333dd3b6fd5d3a7b719367d951c55fa00c7a5e8e548c78
- source-delta.patch SHA4b8d496c94396e0e651d447e5eade8891761860bf96acbe42d994ea0038f1b79
- results/chow-selector-green.trx SHA5cea0aba23f47469848ea7a00273880114a03ef4b3cb806b2649861f631c629c
- inputs.json records all runtime/engine/auditor test inputs; comparison found no drift.

All build/obj/results/temp/CLI-home isolated; restore only after retained NETSDK1004. No live8950, browser, Docker/root scripts, keys/data, old fixture/harness, branch/staging/commit/push or new agent operation. Wire contracts/base-unit/Kong policy are unchanged by this delta; previously recorded independent-review and live/cohort gates remain in effect.
