# C01 BLOCKER: same-socket cross-room viewer privacy

Coordinator reclaimed stalled live replay execution and ran the already-prepared frozen scripts against the fresh isolated C01 candidate at loopback port18190. No application/test source was edited and no existing shared game was contacted.

C01 image `sha256:113c10b315f41483bdd0e1b0879ffdee08399e4f8b2dd91fc37671284ba10461`; DLL `836821f3819d881fc1ee7bc1a480f98b16adf32e7b37e21449b70d04f1b4cec5`; endpoint SHA256 `df34a10bcb82853cdb0d7328cd581f0252b219d141054481d5a526033875bb99` (protected PR163 correction included).

## Executed results

- Existing matrix:12/12, signed identity:4/4, spectator brute force:108/108 explicit rejections, all PASS. Valid owner/reconnect and relay pass-through controls preserved.
- NEW cross-room reproduction: **FAIL, confirmed confidentiality defect**. A legitimately signed owner of seat0 in fresh room A sends JOIN on that SAME socket to fresh room B already owned by a different player. A fresh-socket same-identity baseline sees B's hand opaque, but the switched socket sees all14 real tile IDs exactly matching B's victim hand and claims destination seat0 in projection. A subsequent rejected occupied-seat update leaves a forged neutral seat entry visible on fresh reconnect. The attacker's discard is explicitly rejected and the victim board stays unchanged; the legitimate victim owner can still discard. This is a viewer projection/room-context bug, not a mutation-authorization bypass.

Exact commands:
`python3 session-files/completion-proof/2026-09-09/spunkmeyer/run-existing-ws-replays.py --base-ws ws://127.0.0.1:18190/autotable/ws --run-name c01-coordinator-matrix-01`
`python3 session-files/completion-proof/2026-09-09/spunkmeyer/reproduce-cross-room-viewer.py --base-ws ws://127.0.0.1:18190/autotable/ws --run-name c01-coordinator-crossroom-01`

Redacted results in corresponding run directories; only counts/booleans recorded, no credentials/player IDs/tile IDs. Fresh rooms generated uniquely by the existing harness. The prepared source analysis points to retained ViewerSeat across HandleJoinAsync room changes and an early return in viewer inference; the revision owner must confirm and correct the root cause.

## Gate and ownership

C01 cannot be called final or privacy-complete. Preserve it immutable for red/green evidence; no in-place restart/rebuild during active browser work. A narrowly scoped independent endpoint/room-viewer correction plus focused regression is required, followed by independent review and a NEW C02 candidate. Do not weaken opacity or valid-owner/spectator/relay semantics. Bishop remains excluded from the historical rejected endpoint revision; an independently eligible owner must implement. Spunkmeyer's source109 proof remains valid for its covered cases but does not close this new gap.
