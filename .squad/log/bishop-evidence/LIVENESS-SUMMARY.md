# Runtime liveness investigation — mobile e2e "RollingDice stall" (Bishop)

Head 9ffb6c56ec6325abc9d77d1dff30d9248c10663c, run 31625770258, job 94211633429
(`e2e (mobile-chrome)`) — 12 failed / 200 passed / 301 skipped; Playwright smoke step
ran 76m47s on a fresh 2-vCPU runner (chromium job green ~27m; P0 green).

## Failure signature (all 12)
Manual-deal/pickup ceremony tests (HDP, D2 non-dealer, endpoint-only S1–S12, break-anchor):
`pickup.targetSlots == null`, "RollingDice stall: the window never reaches the human",
no break trigger at BreakPointMarked. Each failed all 3 CI attempts.

## Runtime audit (ChangshaGameRuntime) — no lost-wakeup pattern
- Bot scheduling = fire-and-forget `Task` + `Task.Delay(BotPickupDelayMs=500 / BotTurnDelayMs=350)`
  + re-validate under the per-instance `SemaphoreSlim` lock + `EndBotSchedule` in `finally`.
  The chain re-fires via `ScheduleBotIfNeededAsync` after every mutation. Delay-under-load,
  not a lost wakeup; the dedup slot is always released.
- `ChangshaBotEngine` sync-over-async is SAFE: `.Result`/`.GetAwaiter().GetResult()` only on
  already-`IsCompleted` tasks, guarded by a wall-clock `Stopwatch` timeout + safe-default.
- `PersistSnapshotAsync` (on the locked critical path) = full-state JSON serialize+deserialize
  deep-clone + `StateChanged` (enqueue only) + EF read/write. The heavy WS send
  (translate→SendAsync) is a fire-and-forget FIFO drainer OFF the lock. All awaited forward progress.

## Empirical proof — backend-only (no browser), non-dealer seat 1, bot dealer
Metric = wall-clock from seat-take to the human's own `pickup.targetSlots` window opening
(WS driver, out-of-repo). Backend pinned `taskset -c 0,1`, `DOTNET_PROCESSOR_COUNT=2`.

| Condition | reachedHuman | time to human window |
|---|---|---|
| Unconstrained baseline (×3) | 3/3 | 1.18–1.38 s |
| Backend 2-core, no burners (×3) | 3/3 | 1.19–1.38 s |
| Backend 2-core + 4 burners (×3) | 3/3 | 1.22–1.24 s |
| Backend 2-core + 6 burners + **12 CONCURRENT games** | **12/12** | 2.05–4.32 s (avg 3.22 s) |
| Same constrained backend, control after browser test | 1/1 | 1.20 s |

⇒ The rules runtime ALWAYS reaches the human's pickup window, ≤ ~4.3 s worst-case even under
2-core + 6 burners + 12× concurrency. No permanent stall, no unscheduled op, no deadlock.

## Empirical proof — browser vs same backend (the smoking gun)
Backend serving the fresh HEAD bundle, pinned `taskset -c 0,1`; the mobile-chrome Playwright
run ALSO pinned `taskset -c 0,1` (backend+browser share 2 cores = CI-like). Ran D2 (the
purest backend-dependent cell) ×3:

- **3/3 browser runs FAILED** — "0 presses / RollingDice stall: the window never reaches the human".
- **Every time the BACKEND had advanced correctly**: persisted state = phase `PickupRound1`(4),
  `pickupSeat=1` (the human's window is OPEN), `dealer=0` (bot). i.e. the server rolled the bot
  dealer, marked the break, ran round-1, and handed the pickup cursor to the human — then correctly
  WAITS for the human press (a human pickup seat stalls the chain by design).
- A lightweight WS control on the SAME constrained backend received the seat-1 `targetSlots` in
  **1.20 s** while the browser saw nothing for its 90 s loop.

## Exact phase chain
Server (correct, ~1.2 s): `Seating → RollingDice → [bot-dealer roll] → BreakPointMarked →
PickupRound1 (pickupSeat=1, human targetSlots shipped) → waits for human press`.
Client (2-core WebGL-starved browser): never observes/renders the already-open window →
`readIsMyPickupTurn=false` for 90 s → 0 presses → asserts "RollingDice stall".

## Classification
**NO backend/runtime liveness defect.** The runtime is correct and fast (bounded ≤ ~5 s to open
the human window even under severe CPU starvation + 12× concurrency). The CI "RollingDice stall"
is a CLIENT/BROWSER observation failure: mobile-chrome WebGL on a 2-vCPU runner cannot process
WS frames / render / drive the pointer within the test deadline while the server has already
advanced. Worst observed RUNTIME delay 4.3 s; the CLIENT-side stall is unbounded under 2-core
WebGL starvation (never catches up in 90 s).

## Recommended lever (test/CI lane — Hudson/Apone; NOT backend)
Give the mobile-chrome e2e job more CPU headroom: a larger runner (≥4 vCPU) or fewer concurrent
consumers on the 2 cores; optionally shard the mobile ceremony suite. This is a browser-resource
problem, NOT a rules-runtime problem. Do NOT weaken assertions, add sleeps, enable retries, or
"fix" the backend. (Same root class as the prior mobile-cell environment-sensitivity finding,
now proven at the runtime level.)

## Reproducibility
Backend-only: 22/22 runs reached the human window (≤4.3 s). Browser-constrained: 3/3 reproduced
the stall with the backend proven advanced each time. Head byte-clean (no product/test edits;
diagnostics were an out-of-repo WS driver + read-only DB/state inspection).
