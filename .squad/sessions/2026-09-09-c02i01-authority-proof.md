# C02-I01 corrected-backend live proof — coordinator executed

**Backend red-to-green demonstrated on the actual running interim image. This is NOT final whole-game acceptance: GameUi still has the pending late-hydration correction.**

Verified target `http://127.0.0.1:18191/autotable/`, WS `/autotable/ws`; container `mahjong-20260909-e2fb89d1-c02-i01`, running/healthy. Actual image `sha256:97b455352e21ffb92775c56e8753ea2c3ece46b6c512e90aa39df925208f9ecc`; actual served DLL SHA256 `8e3488aa9eac0203d1980919f74b12d174840002f1259e1ce18265c28ef62437`; entry `autotable-src.bff046d6.js` HTTP SHA256 `4029dce9936c4887b6df7343cdfdabfe04b38c06cd24af80f82bf1afb285ff18`; health HTTP200 with SQLite connected. Source endpoint remains approved SHA256 `7543e6a4d6d50102af3bb737f4d7a21cd01c5e38fd2e3838ce282cb5a6d45768`.

Executed unchanged prepared drivers, new isolated rooms, no source/game injection or restart:
- `python3 session-files/completion-proof/2026-09-09/spunkmeyer/run-existing-ws-replays.py --base-ws ws://127.0.0.1:18191/autotable/ws --run-name c02i01-coordinator-matrix-01`: matrix12/12, signed identity4/4, brute-force control1/1 with108/108 explicit rejections; legitimate owner/reconnect/relay controls PASS.
- `python3 session-files/completion-proof/2026-09-09/spunkmeyer/reproduce-cross-room-viewer.py --base-ws ws://127.0.0.1:18191/autotable/ws --run-name c02i01-coordinator-crossroom-01`: PASS. Same-socket destination victim hand remains opaque (exposed real IDs14 on C01 ->0 on C02-I01); no forged destination seat or persisted forged reconnect entry; wrong-owner discard explicitly rejected and board unchanged; legitimate victim-owner discard accepted.

Results are in those two immutable run directories as existing-ws-replays.json and cross-room-viewer.json. Only redacted counts/booleans, no credentials/player IDs/tile IDs. Existing helper/source hashes unchanged. Parent shell runs259/260/261 captured identity and actual executions.

C01 stays the original RED baseline. C02-I01 closes the served-backend defect for its exact image/DLL, but final UI patch, final image pin and renderer/real-game/browser acceptance still remain. Do not transfer this to an unverified final image without recording matching identities and completing final changed-path checks.
