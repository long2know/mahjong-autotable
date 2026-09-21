# Hicks — entry/seat source implementation complete; independent review and live proof pending

## Root causes and surgical changes
- **F-COLD-NEW-GAME:** enabled header delegation lived only in deferred ClientUi construction. `index.ts` now binds a single eager lobby-owned listener before asynchronous entry/game bootstrap. It reuses `buildFreshGameUrl`; ClientUi replaces its fallback action with the unchanged ownership/teardown method without adding another listener. Shared in-flight protection, disabled/aria-disabled/aria-busy checks, and retiring pending action/rejoin directives preserve one activation/one navigation.
- **F-LATE-SEAT-ROW:** seats/nicks subscriptions do not replay cached state. GameUi now invokes its existing seat renderer after initialization and initializes its cached connection flag. Offline Changsha cannot mistake Client's local default seat for confirmed ownership; relay offline behavior is preserved. No blanket collection/event replay or transport/state mutation was added.

## Exact final SHA256 (paths beneath src/frontend/autotable-src/)
```text
src/client-ui.ts  4aad08d638cad6e6fae5831c6dbfa91cfe546d398dbe02f8983e6784ab23bb1b
src/game-ui.ts    a7a1883ecaad1dda67231378e027159dd971ce5fe6716689a78a09679ad5ee93
src/lobby.ts      14e287e225417ff907a7cb085da2b3399f89a9b3adae6aebf1862a7d250d7bd3
src/index.ts      d6486e5eb80096c0ffd7c41772e10a371c158e7f3080962e214786bbf9547ddd
tests/e2e/entry-seat-initialization.spec.ts  9dd8c9785ca777fa5aec2b57169e371b8396053d8cce46198213633f60334feb
```

## Validation and new regressions
- Existing browser-free New Game URL/handoff, Apply/reconfiguration and activation-predicate contracts: **34 PASS, 0 FAIL/SKIP/flaky**. The existing browser-dependent DOM case was not selected; no browser launched.
- Exact existing strict production typecheck: **exit0**. New spec strict Node-aware typecheck: **exit0**. Discovery: **9 cases × chromium/mobile-chrome =18 registered**, NOT executed.
- Scoped existing ESLint: **exit1, 14 errors/1 warning**, byte-baseline comparison establishes **zero introduced diagnostics**. New spec: **0 errors/0 warnings**. Not an all-clean lint claim. Initial test-typing failure and corrected final runs are retained.
- New tests hold ORIGINAL client-ui/action-router/scene-effects delivery, use genuine click/tap or normal PWA navigation, and capture chrome synchronously at the existing effects-ready event so later updates cannot hide missed hydration. Cover cold defaults/config, pending and automatic PWA entry, genuinely acquired seat handoff/one navigation, cached empty relay, occupied Changsha, spectator privacy, and offline Changsha.

## Review artifacts / preserved boundaries
All evidence: `session-files/completion-proof/2026-09-09/hicks-entry-fix/`.
- `review-ready.patch` SHA256 **8b83c8c9ac7a2ab0f654298f2ccae2d17bab07ebb844375fcfba0ad1f48b73cd** — diff against verified supplied starting bytes, NOT against HEAD's pre-approved-hydration state.
- `implementation-results.json` SHA256 **c61c93d782de45c5a42f55cfef8f20e1822c082b80fd49cd0bc59a9f464b2056** — commands, final hashes, actual results/limits; `artifact-hashes.json` seals retained evidence.
- **262 protected files unchanged**, including existing specs/helpers, rejected claim-key/S11 artifacts, renderer files, dist and dist-size. Approved hydration regression remains **8673cbee03e13f37fb474b57fe29523659aa02b18847849599040b2f4d3dc140**. AST/text comparison verifies13 existing ownership, teardown, connection and Roll/Pickup methods unchanged. Ferro final-results master retains its supplied SHA.
- No browser, dist/image build, server/candidate lifecycle action, backend/client transport edit, dependency change, branch/staging/commit/push, or peer broadcast. Only this task's scratch caches were removed.

**Next:** independent source review, then coordinator-authorized negative controls on immutable18209 and positive runs on the newly reviewed immutable pin, workers1/retries0, plus affected frozen acceptance. This is NOT live acceptance or a whole-game completion claim.
