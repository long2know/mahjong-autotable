# Spunkmeyer — FINAL targeted authority proof (September 9, 2026)

**Outcome:** existing backend acceptance **PASS 109/109, zero failures/skips**, 52 seconds execution. **Full completeness/exact-candidate approval is NOT claimed.** Apone's fresh target has not been handed off; a bounded same-socket room-switch privacy hypothesis remains unexecuted. No old live target/game was contacted. Ready for exact-image follow-up.

## Frozen artifact

Branch `squad/s11-stable-tombstone`; HEAD `b38e1a943e8804020c7887af3920fba257d1a427`. Inherited correction preserved byte-for-byte:
- Endpoint `Autotable/AutotableWsEndpoint.cs`: blob `50bd1fbbcfaaffc02309341c4bf7da76ac65a545`; SHA256 `df34a10bcb82853cdb0d7328cd581f0252b219d141054481d5a526033875bb99`.
- `AutotableWsSeatAuthorizationTests.cs`: blob `81d16d46b0e4ff9a5c18a3a90e80097bc7da9bee`; SHA256 `d5cc8f747ed20540c8df4dd8fb92be0d14d41cf6e1aea744131e2fabfb1f7d7a`.
- Protected dirty-diff SHA256 `34038dd1c176c387d87d8a53aed2b5c2d989d8d403d8c43630c74b4d870c9e83`; remains endpoint +12/-15, tests +155. All 30 recorded source/test/support hashes unchanged at end. No production/test/index/branch changes; unrelated dirty state untouched.

## Executed combined proof

Evidence root `session-files/completion-proof/2026-09-09/spunkmeyer/`. Exact command/environment is persisted in `run-backend-proof.sh` and `backend-01/logs/02-after-missing-assets.log`:

`bash session-files/completion-proof/2026-09-09/spunkmeyer/run-backend-proof.sh 02-after-missing-assets`

This invokes the existing test csproj in Release with SDK10 `--artifacts-path`, one combined filter, unique TRX/results/DBs and lane-local HOME/runtime/package-write directories. Cached packages are read-only fallback. No shared bin/obj writes. The selected classes/counts are: seat-authorization18; seat-spoof4; privacy-contract7; projector6; identity-WS4; token24; cookie13; identity-startup6; privacy-startup9; human claim-wire9; leave-tombstone2; shared-store privacy2; fail-closed viewer3; selected relay2.

**Proved rejection:** spectator/wrong-owner discard; Pung/Chow/Kong/Hu/Pass; dice and manual pickup. The endpoint authorization fixture compares both StateVersion and complete serialized state, plus sender-only rejection/corrective snapshot. Occupied-seat test observes a mouse processing barrier, no persisted attacker seat on reconnect, opaque victim hand, rejected attacker discard, then successful victim-owner discard.

**Positive controls:** seat acquisition; owner/reconnected-owner discard; owner roll/take; legitimate Pung/Chow/Kong/Hu/Pass through real WS payloads; signed owner hand/reconnect/multitab; public/owned real IDs versus opaque hidden keys; relay mutation pass-through and room isolation. Lifecycle start and own leave retain existing intentional semantics.

TRX `backend-01/results/02-after-missing-assets.trx`, SHA256 `9fbac2d8bc46db6ed63c8694fda54779e56a3ff9b75bc1cb7795065524381d0f`. Per-case counts in `backend-01/backend-summary.json`. Isolated API DLL SHA256 `bf7705b3d0d8cb41c4e1bfa7a75e9ee4b076b7b52bb9fba034f3ac1e680fe0aa`; test DLL `8f43bf1d405894cff37bcb45ec008295301b220c11db551750f222aa57fe63a9`. These are test-build identities, NOT final candidate image identities.

## Residual / next exact-candidate step

**Unconfirmed hypothesis, not a reproduced defect:** `HandleJoinAsync` changes GameId at endpoint :502 without clearing prior ViewerSeat; inference :527 returns early for an already-set seat. `SendFullSnapshotAsync` :1661 and translator :96/:559 consume that seat. A signed socket legitimately owning seat0 in fresh room A might re-JOIN occupied room B and expose B's hand / persist a forged neutral seat entry, while mutation authorization still rejects it. Existing109 tests do not cover this same-socket cross-room transition.

Prepared, syntax-checked, NOT EXECUTED: `reproduce-cross-room-viewer.py` SHA256 `9c6a7edd63a3833e17195037baccfae55a0d0ce6fad717b7261042ac4c749f2f`. It creates two new rooms, checks a fresh-socket opaque baseline, same-socket re-JOIN, occupied-seat processing barrier, rejection/board immutability, reconnect projection, and valid victim-owner discard. Records only counts/booleans, never identities/credentials/tile IDs. Translator SHA256 `7a288581901b0341d05b5b504f12a6db778af34f569d8cddf0484c15ea0dfe05`.

Also prepared `run-existing-ws-replays.py` SHA256 `4ac1d164c517620af310fddde807f1b7d6d280af167272379584ad6827ec9504`: executes unchanged historical matrix/identity/108-discard tools after pre-importing their shared helper at the fresh loopback target, defeating their otherwise-hard-coded OLD endpoint without changing source. Original hashes checked, private stdout suppressed. Both require explicit `--base-ws` and unique `--run-name`. Apone was notified. Any fresh defect still needs coordinator artifact-specific eligible-author grant; Bishop remains locked out, no revision attempted.

## Execution hygiene

Initial `dotnet test --no-restore` emitted no tests/zero TRX and is not counted as a pass. Explicit isolated no-restore build then failed NETSDK1004 missing assets; only afterward was restore allowed. Existing analyzer warnings retained; no timeouts/skips/parallelism/helpers weakened. `python` alias absent; existing `python3` and websockets10.4 used without installs. Credential scan found no real-length signed token/JWT/private-key/provider-token values in logs/TRX/summary; six short token-shaped matches are fixed malformed fixture names. Independent exact-version security review and the later signed-image release matrix remain outstanding.
