# Multiplayer lobby/chat repair — complete

Final status: sessions/2026-09-17-lobby-repair-status.json. The requested fixes, exact-image verification, latest-tag Docker export, Compose handoff, test-only consistency work and owned-resource cleanup are complete. Earlier in-progress/old-pin/HOLD records are historical, not the current task status.

Delivered /mnt/scratch/mahjong.tar.gz, 127109113 bytes, SHA25649230455af4ce0c18151aaec8b76a3e47c10ea9a287bd5a9c2b327b9732ddb9e; image mahjong-autotable:latest at sha256:f89b35c21a0df4382d0e198936c01faa73de53921a5b41843b7843b0830581c2. Compose keeps host8950, container8080, ${DOCKER_DATA}/mahjong/config/data:/data bind and external stable JWT key. No named volume, registry publication or remote server deployment.

Fresh whole focused backend confirmation128/128 passed; exact corrected-image normal-user walkthrough covered8flows. Independent source reviews closed R1/C1. The legacy fixture classes passed24/24. The two approved split browser cases passed2/2 within unchanged30s budgets,0skip/0retry. Initial transient failures and old-pin results were preserved, not retrospectively labeled green. No whole-repository or120-match claim.

All owned test containers/private binds and browser contexts/runners were cleaned up. Released application/image/archive did not change during the test-only follow-up. No further execution, restart, build/export or source edit is authorized by old queued messages. Future work requires a new explicit user/coordinator task.
