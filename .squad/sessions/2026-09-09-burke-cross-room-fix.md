# Burke — SEALED cross-room viewer correction, September 9, 2026

**Outcome: IMPLEMENTED, source GREEN, ready for independent Frost review. C01 remains immutable RED. No image acceptance or completion/release claim.**

Branch `squad/s11-stable-tombstone`; HEAD `b38e1a943e8804020c7887af3920fba257d1a427`, unchanged. Burke read the required blocker/design/source-proof/skills and own runtime history: recorded prior authorship was cookie identity, not the rejected endpoint artifact. This is Burke's independent revision under the explicit two-file grant; no delegation, factory, or excluded-author consultation.

## Implemented root correction

Only `AutotableWsEndpoint.cs` and the existing `AutotableWsSeatAuthorizationTests.cs` were changed as application/test source. No additional shared source was needed.
- Endpoint :461/:502/:517 refreshes destination-room ownership **before** NEW/JOIN's first snapshot. Old/query seats are not grants. Explicit spectators do not inherit ownership; a legitimate same-room explicit TakeSeat grant is revalidated.
- Endpoint :1649 validates cached entitlement against the projected room's frozen non-bot player ownership and uses one local seat consistently for private things, seat/nick projection, shared neutral storage and filtering.
- Endpoint :2046/:2148 rejects departed-room snapshots at locked send time and resolves each queued snapshot's own room, rather than reusing the original drainer's room. Existing per-room FIFO behavior is preserved.
- Mutation authorization, cookie/token identity, translator, gameplay/runtime, explicit leave/disconnect paths and relay passthrough were not rewritten. All eight inspected authorization/action methods and all 18 inherited test methods remain byte-identical to the before snapshot.

## Sealed source identity and exact inherited-baseline diff

| File | Protected baseline SHA256 | Final SHA256 | Baseline-relative delta |
|---|---|---|---|
| Endpoint | `df34a10bcb82853cdb0d7328cd581f0252b219d141054481d5a526033875bb99` | `7543e6a4d6d50102af3bb737f4d7a21cd01c5e38fd2e3838ce282cb5a6d45768` | +47/-36 |
| Authorization tests | `d5cc8f747ed20540c8df4dd8fb92be0d14d41cf6e1aea744131e2fabfb1f7d7a` | `a3ad5f00cff2f9bd913011170193457baf881f7b472debaf6da98e3f268381c3` | +291/-7 |

Final git blobs: endpoint `6cb9e999d3fbbff6e1bad0408dc3a1f87e9b41b7`; tests `1349b1cfd1ed190b8d3c51ba891375445ca43659`.

Evidence root: `session-files/completion-proof/2026-09-09/burke/`.
- `before/` contains exact protected before bytes, original inherited diff/status and source manifest.
- `candidate-source/` matches final working source byte-for-byte.
- `baseline-relative.diff`: exact additional correction, SHA256 `694f21a3ccab33595e52238d300c84a5cc2d13d1681e715bdfeab6b79b1fee3c`.
- `final-manifest.json`: hashes, protected-method checks, per-case/class counts and artifact identities; SHA256 `69a855d9ce46bbcd89d56d91af79fb0f0a6f5842ec68fad4a5b2d1d4b5b45b61`.

## Executed validation

All builds/tests used supported lane-local `--artifacts-path`, isolated HOME/runtime/package-write directories and existing read-only package fallback. `01-missing-assets` explicitly failed NETSDK1004 before restoration. No zero-test result was accepted. Exact commands/filters are retained in `run-backend-proof.sh` and each log header; no shared bin/obj writes, timeout/skip changes or new test/build tools.

**Exact final regression bytes, inherited endpoint:** `bash .../burke/run-backend-proof.sh 05-red-final-regressions test-no-restore` → **8 executed: 7 expected assertion failures, 1 relay control pass; zero skips**. Endpoint was verified byte-identical to the inherited baseline for that run, then the exact sealed fix was restored with apply_patch. Redacted TRX SHA256 `a1e4419018caa7558d84f673508157d08b80e275e3f8028bfbc8e7b78d9f96ef`.

**Restored final source:** `06-green-final-controls test-no-restore` with the recorded combined filter → **137/137 passed, zero failed/skipped**. This is the existing 109-case authority/identity/privacy selection, 8 new cases and 20 JOIN/spectator/disconnect/leave controls. Redacted TRX SHA256 `e784f3b547c592cc1786cd94a91921961f52398699e0512823cbcf45b5dad66c`.

New regressions inspect every JOIN full snapshot through an explicit rejected-discard/resync processing fence, plus occupied-seat mouse barriers and StateVersion/full-state equality. They verify direct/unbound-hop source-owner→occupied-destination opacity (14 hidden tiles), no persisted forged seat on reconnect, sender rejection with unchanged board, successful victim-owner discard and opaque 13-tile broadcast, same-room/return/repeated JOIN, actual destination ownership at equal/different seats, owner reconnect despite wrong query hint, explicit spectator opacity, release/replacement and relay isolation.

`git diff --check` passed. Evidence scan: 18 log/TRX/JSON files, zero credential, generated-player-ID or projection-payload matches after documented redaction; counters, individual outcomes and assertion stack traces retained. Final API test-build DLL `148afd41fe779875080d65a6a483876588b52faf532e8361e87f1fca4249ad45` is **not an image identity**.

## Remaining gate / ownership

No source blocker remains in this lane. Frost must review these exact source/test hashes. Only after independent approval may Apone build C02; coordinator owns immediate raw-WS replay and Hudson owns final browser proof. C01 was not rebuilt/restarted, no browser touched, no frontend/dist changes, and no staging/commit/branch change/publish/deploy occurred. Exact-candidate acceptance and the later signed-image release matrix remain open.
