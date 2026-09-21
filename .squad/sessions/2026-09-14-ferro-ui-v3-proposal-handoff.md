# Ferro UIv3 shared proposal revision — source-only handoff

Observed 2026-09-14T06:32:32Z. Independent rereview is required; no self-approval or live/counting authority.

## Persistent artifacts

Namespace: `session-files/qualification/2026-09-12/ferro-ui-v3/shared-integration/`.

- `evidence/proposal-handoff-01/handoff.json` SHA256 `a8ca41621a0b9d9fb950f702521a45e6866d2c8a461d1ef15444186e4bcf08e2` contains exact source hashes, shared schema/commands, all four rejection outcomes, limits and blockers.
- `evidence/proposal-handoff-01/SHA256SUMS` SHA256 `a62e0e2cca908376e484190209291883bb090ffd1cfc06286ea858391d171a7f` seals the handoff and source-result receipts. `code.sha256` names every code/extra-input path and digest.
- Shared-observed harness digest: `d5ca445fee5af63da440837a32a01755e380ae6f818748c8563fda4f7f726397`; manifest file SHA256 `7ff8dfc01a1ccf62936e39ae44b5aee10525b92b8b8158730efe0ee4477961af`.
- Before-change snapshots: `evidence/proposal-revision-before-01/`; `before.sha256` seal `11f3d8a068dfe96c5d5f5a7ba76c1c0bf6c91aef0984c4062cf14d1b77e3e5f0`. Previous handoff `1d6bd05d55b8bed079d049812a8793cc043912ecc974afacf0fa700507fcb3b6` remains retained, not overwritten.

## Implementation

`prepare` now validates only the coordinator candidate, copies/checks immutable UI inputs, and emits a grant-free session plus shared `mahjong-run-proposal-v3`. Session stores proposal path only; proposal hashes session, each case model and exact copied inputs. Coordinator review/grant comes afterward. `run` and every child require separate immutable session AND grant Refs. No circular hashes, default pilot selection, partial counted row selection or self-minted grant.

Shared snake_case execution is UI workers1/retries0/max_uses1/progress_timeout_ms30000/max_hands4, model gpt-6-astra/max/long_context. Exact24 plan inputs/order remain unchanged. Original protected helpers are independently rehashed; grant/case/room attempts are exclusive. Final shared checkpoint and all local input/evidence checks precede atomic publication; mutations leave explicit FAILED_UNQUALIFIED instead of positive qualification.

The action-aware driver remains normal trusted UI input only, with actual owner runtime gameId/version and real self-Hu/Kong/Chow choices/effects; public actor/tile/hand-epoch discard acknowledgement remains mandatory. This UI consumer requires `capability_contract` in pin/grant/proposal to reference a coordinator-owned byte-identical copy of its supplied `runner/action-contract.json` (SHA `5887528ce5616bb3a8166ed939de2ce8c67c0062a5a2bb147c78275e52a4a567`). Both local action contract and exact shared helper-lock are reviewed harness extra inputs. Frost retains the single candidate/source/build/output/endpoint/grant schema; the old standalone UI signature/provenance schema is not imported.

## Actual source evidence / blocker

- Consumer Python:49 PASS =9 positive and40 negative methods; no failure/error/skip.
- TypeScript:33 browser-free witness/action/CDP contracts PASS, strict types PASS, new-code lint0errors/0warnings (legacy ESLint config deprecation notice separate).
- Actual shared source contract:4 PASS,1 FAIL,1 ERROR. The prepared proposal and altered model-ref rejection work with the actual shared reader. But current shared `authority.py:203` rejects counted UI workers1 and accepts UI workers2. The two unchanged discriminating tests are `test_shared_counted_ui_accepts_one_worker` and `test_shared_counted_ui_rejects_two_workers`; no bypass/skip was added.
- Actual observed shared digest is `660b8ffb605098d25761060485119475e36cc3687d2bf018eaaf01f148a5d1f9`, not the earlier96203/4517 releases. Captured schema SHA `a0da38f3882c8283b5369fb939b89cbdcb522a6737e629d1c3e650714b29370a`; helper-lock file SHA `58c869da076d022fcfa889a66c34b6baedff3b345afd533a6c199f1d53537ca6`. Shared code was rechecked unchanged through this seal.
- Reproduce only the shared source mismatch: `python3 -B -m unittest discover -s session-files/qualification/2026-09-12/ferro-ui-v3/shared-integration/tests -p 'test_shared_contract.py' -v`. These call source schema/proposal/case policy only, NOT candidate loading/Docker/HTTP/browser/game admission.

All own source fixtures are explicitly invalid external authority documents in temporary isolated evidence directories; mocked candidate/export controls are not live provenance proof. Full source inventory/build/daemon/output checks are delegated to Frost but were not executed against a new live candidate in this revision. Six old immutable transform-cache JS files remain identified in the shared harness inventory; no old outputs were overwritten and new generated files stay in evidence/runs.

## Closure / remaining authority

New browsers0, new UI pilots0, counted games0. ALL UI BROWSERS/CLIENTS CLOSED; the prior two September12 uncounted four-hand pilots remain historical (Manual44 + Auto35 public-proof own discards, same seat1/Medium/seed20261652), never reattributed to this shared harness or new production source. No app/dist/protected-helper/shared-verifier/Git/SQL/container changes.

Frost must correct and freeze the lane-specific counted policy (UI1/protocol2), then obtain a new traceable shared lock/harness observation. Independent rereview, reviewed production fixes, actual new root-source-built candidate/receipt/source freeze and explicitly reissued pilot grant are still required. The old pilot budget and old C03 image do not authorize any further live execution. Current Changsha rules only; no invented presets/caps/washouts.
