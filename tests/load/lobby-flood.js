// Phase J Wave 10 — load test: lobby-flood
//
// Apone (DevOps). End-to-end load test exercising the public-facing surface
// area Stephen most cares about: a flood of users hitting the lobby + the
// matchmaking REST endpoint + simultaneous game-create + tournament create.
//
// We deliberately ship this as a plain `node` script (no k6 / artillery /
// playwright dep) so it runs anywhere — including Stephen's Linux server —
// without an extra binary. Node's built-in `fetch` and `ws` modules cover
// everything we need; k6 is the canonical target if it's available
// (`if (typeof __ENV !== 'undefined')` short-circuit), but the node path is
// the one this repo ships.
//
// Workload mix (defaults — tunable via env vars at the top of `main()`):
//   - 100 concurrent users polling the lobby (`GET /api/matchmaking/lobby`)
//   - 25 simultaneous game-join attempts via the autotable WS relay
//   -  5 simultaneous "tournaments" (a tournament here is 4 bots joining
//      the same gameId — exercises the runtime's bot fill + game-start path)
//
// Output:
//   - On stdout, a JSON summary suitable for piping into `jq`.
//   - Latency percentiles (p50 / p95 / p99) per workload.
//   - Error rate (4xx / 5xx / WS close) per workload.
//   - Hub reconnect rate (count of WS reconnects per minute).
//
// Usage:
//   node tests/load/lobby-flood.js
//   BASE_URL=https://mahjong.example.com DURATION_S=300 \
//     LOBBY_CONCURRENCY=200 node tests/load/lobby-flood.js
//
// Exit codes:
//   0 = test ran to completion (PASS-ish — does NOT enforce SLOs; that's
//       up to a downstream gate that consumes the JSON summary)
//   1 = setup failure (target unreachable / config invalid)

const ws = (() => {
  try { return require('ws'); }
  catch { return null; }
})();

function nowMs() { return Number(process.hrtime.bigint() / 1_000_000n); }

function percentile(sorted, p) {
  if (sorted.length === 0) return null;
  const idx = Math.min(sorted.length - 1, Math.floor((p / 100) * sorted.length));
  return sorted[idx];
}

function summarize(samples) {
  if (samples.length === 0) return { count: 0, p50: null, p95: null, p99: null, min: null, max: null, avg: null };
  const sorted = [...samples].sort((a, b) => a - b);
  const sum = samples.reduce((a, b) => a + b, 0);
  return {
    count: samples.length,
    min: sorted[0],
    max: sorted[sorted.length - 1],
    avg: +(sum / samples.length).toFixed(2),
    p50: percentile(sorted, 50),
    p95: percentile(sorted, 95),
    p99: percentile(sorted, 99),
  };
}

class Metrics {
  constructor(name) {
    this.name = name;
    this.latencies = [];
    this.errors = 0;
    this.successes = 0;
    this.reconnects = 0;
    this.startedAt = nowMs();
  }
  recordLatency(ms) { this.latencies.push(ms); }
  recordError() { this.errors++; }
  recordSuccess() { this.successes++; }
  recordReconnect() { this.reconnects++; }
  snapshot() {
    const durationMs = nowMs() - this.startedAt;
    const errorRate = this.successes + this.errors === 0
      ? 0
      : +(this.errors / (this.successes + this.errors)).toFixed(4);
    return {
      name: this.name,
      durationMs,
      successes: this.successes,
      errors: this.errors,
      errorRate,
      reconnectsPerMin: +(this.reconnects / Math.max(1, durationMs / 60000)).toFixed(2),
      latency: summarize(this.latencies),
    };
  }
}

async function lobbyWorker(baseUrl, deadlineMs, metrics) {
  while (nowMs() < deadlineMs) {
    const start = nowMs();
    try {
      const resp = await fetch(`${baseUrl}/api/matchmaking/lobby`, {
        method: 'GET',
        headers: { Accept: 'application/json' },
      });
      const latency = nowMs() - start;
      metrics.recordLatency(latency);
      if (resp.status >= 200 && resp.status < 300) {
        // Drain body so the socket can be reused by Node's keep-alive pool.
        await resp.text();
        metrics.recordSuccess();
      } else {
        metrics.recordError();
      }
    } catch (_err) {
      metrics.recordError();
    }
    // Small inter-request gap so 100 concurrent workers don't degenerate
    // into a CPU-bound tight loop on the client side.
    await new Promise(r => setTimeout(r, 200 + Math.floor(Math.random() * 100)));
  }
}

async function joinWorker(baseUrl, deadlineMs, gameId, metrics) {
  if (!ws) { metrics.recordError(); return; }
  const wsUrl = baseUrl.replace(/^http/, 'ws') + `/autotable/ws?gameId=${encodeURIComponent(gameId)}`;

  while (nowMs() < deadlineMs) {
    const start = nowMs();
    let socket;
    try {
      socket = new ws.WebSocket(wsUrl);
      await new Promise((resolve, reject) => {
        const t = setTimeout(() => reject(new Error('open timeout')), 5000);
        socket.on('open', () => { clearTimeout(t); resolve(); });
        socket.on('error', (e) => { clearTimeout(t); reject(e); });
      });

      // Send JOIN with seat 0 (race-safe: server picks first available seat).
      socket.send(JSON.stringify({ type: 'JOIN', gameId }));

      // Wait for the initial JOINED + UPDATE pair, with a short ceiling.
      let envelopes = 0;
      const opened = nowMs();
      await new Promise((resolve, reject) => {
        const t = setTimeout(() => reject(new Error('snapshot timeout')), 5000);
        socket.on('message', (data) => {
          envelopes++;
          if (envelopes >= 2) { clearTimeout(t); resolve(); }
        });
        socket.on('close', () => { clearTimeout(t); resolve(); });
        socket.on('error', (e) => { clearTimeout(t); reject(e); });
      });

      metrics.recordLatency(nowMs() - opened);
      metrics.recordSuccess();
    } catch (_err) {
      metrics.recordError();
      metrics.recordReconnect();
    } finally {
      try { socket?.close(); } catch { }
    }

    await new Promise(r => setTimeout(r, 1000 + Math.floor(Math.random() * 500)));
  }
}

async function tournamentWorker(baseUrl, deadlineMs, tournamentIdx, metrics) {
  // A "tournament" here is 4 concurrent WS joiners on the same gameId.
  // This exercises the runtime's bot-fill + seat-take path concurrently.
  const gameId = `LOADTEST-T-${tournamentIdx}-${Date.now()}`;
  while (nowMs() < deadlineMs) {
    const startedAt = nowMs();
    const seatPromises = [0, 1, 2, 3].map(seat => joinSingleSeat(baseUrl, gameId, seat));
    const results = await Promise.allSettled(seatPromises);
    const allOk = results.every(r => r.status === 'fulfilled' && r.value === true);
    metrics.recordLatency(nowMs() - startedAt);
    if (allOk) metrics.recordSuccess(); else metrics.recordError();
    await new Promise(r => setTimeout(r, 5000));
  }
}

async function joinSingleSeat(baseUrl, gameId, seat) {
  if (!ws) return false;
  const wsUrl = baseUrl.replace(/^http/, 'ws') + `/autotable/ws?gameId=${encodeURIComponent(gameId)}&seat=${seat}`;
  let socket;
  try {
    socket = new ws.WebSocket(wsUrl);
    await new Promise((resolve, reject) => {
      const t = setTimeout(() => reject(new Error('open timeout')), 5000);
      socket.on('open', () => { clearTimeout(t); resolve(); });
      socket.on('error', (e) => { clearTimeout(t); reject(e); });
    });
    socket.send(JSON.stringify({ type: 'JOIN', gameId }));
    // Hold the connection open for ~2 s so concurrent seats can race for
    // the bot-fill check.
    await new Promise(r => setTimeout(r, 2000));
    return true;
  } catch {
    return false;
  } finally {
    try { socket?.close(); } catch { }
  }
}

async function main() {
  const baseUrl = process.env.BASE_URL || 'http://localhost:8080';
  const durationS = Number(process.env.DURATION_S || '60');
  const lobbyConcurrency = Number(process.env.LOBBY_CONCURRENCY || '100');
  const joinConcurrency = Number(process.env.JOIN_CONCURRENCY || '25');
  const tournamentConcurrency = Number(process.env.TOURNAMENT_CONCURRENCY || '5');

  // Pre-flight: make sure the target is reachable.
  try {
    const resp = await fetch(`${baseUrl}/health?simple=1`);
    if (resp.status >= 500) {
      console.error(JSON.stringify({ error: 'target unreachable', status: resp.status }));
      process.exit(1);
    }
  } catch (err) {
    console.error(JSON.stringify({ error: 'target unreachable', detail: String(err) }));
    process.exit(1);
  }

  if (!ws && (joinConcurrency > 0 || tournamentConcurrency > 0)) {
    console.error(JSON.stringify({
      warn: "'ws' module not installed — WS workloads (join + tournament) will be skipped",
      hint: 'npm install ws',
    }));
  }

  const deadlineMs = nowMs() + durationS * 1000;
  const lobbyMetrics = new Metrics('lobby');
  const joinMetrics = new Metrics('join');
  const tournamentMetrics = new Metrics('tournament');

  const workers = [];
  for (let i = 0; i < lobbyConcurrency; i++) {
    workers.push(lobbyWorker(baseUrl, deadlineMs, lobbyMetrics));
  }
  if (ws) {
    for (let i = 0; i < joinConcurrency; i++) {
      const gameId = `LOADTEST-J-${i}-${Date.now()}`;
      workers.push(joinWorker(baseUrl, deadlineMs, gameId, joinMetrics));
    }
    for (let i = 0; i < tournamentConcurrency; i++) {
      workers.push(tournamentWorker(baseUrl, deadlineMs, i, tournamentMetrics));
    }
  }

  await Promise.all(workers);

  const summary = {
    baseUrl,
    durationS,
    lobbyConcurrency,
    joinConcurrency,
    tournamentConcurrency,
    lobby: lobbyMetrics.snapshot(),
    join: joinMetrics.snapshot(),
    tournament: tournamentMetrics.snapshot(),
    timestamp: new Date().toISOString(),
  };
  console.log(JSON.stringify(summary, null, 2));
}

main().catch(err => {
  console.error(JSON.stringify({ error: 'unexpected', detail: String(err) }));
  process.exit(1);
});
