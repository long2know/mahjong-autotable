# Tournament query duration — operator runbook

**Owner:** Bishop (Backend). Phase K Wave 17.

This runbook walks the operator through the
`TournamentQueryDurationP99HighPage` (PAGE) and
`TournamentQueryDurationP95HighTicket` (TICKET) alerts raised by
the Prometheus rules in
`src/backend/src/Mahjong.Autotable.Api/Observability/Alerts/tournament-query-duration.yaml`.

The alerts wrap the
`tournament_query_duration_seconds` histogram landed by the W15
surface (`TournamentQueryLatencyMetrics`) — bucketed by
`endpoint` + `page_size_bucket`. The two-rail design lets the
on-call engineer reach for a non-paging investigation thread
(TICKET) before the latency regression escalates to a wake-up
PAGE.

## Alerts

### p99-page

* **Threshold:** `histogram_quantile(0.99, …) > 0.5s`
* **Window:** 5-minute rate, 5-minute `for`
* **Severity:** `page` — wakes the on-call.

Trips when 1-in-100 tournament-scale queries cross 500 ms for
five consecutive minutes. Most likely causes (rank-ordered by
how often they've fired in canary):

1. **Bracket store saturation** — the EF binding is back-pressured
   by `BracketStorageOptions.MaxConcurrent` (default `8`). Check
   `mahjong_bracket_store_inflight` (W14 collector); if pinned at
   max, scale the bracket replica count or lift the
   semaphore.
2. **Page-size envelope drift** — a caller has started requesting
   a `pageSize` above the W14 tuning bucket. Verify via the
   `page_size_bucket` label on the histogram; the `xl` bucket
   should be < 5% of total volume. If it's higher, validate the
   caller is honouring the W14
   `BracketQueryOptions.MaxPageSize` cap.
3. **Downstream tournament service** — `TournamentService` has a
   downstream dependency (rating ladder) that occasionally bursts
   into a 500-ms tail. Confirm via
   `tournament_service_downstream_seconds`; if that's spiking
   too, route to the ratings team.

**Mitigation:**
* Short-term: enable the per-tenant rate-limiter shed (the
  `BracketQueryRateLimiter` accepts a runtime override via the
  W17 admin endpoint — see `docs/replay-by-id.md §4.1`).
* Medium-term: lift the page-size cap if the caller pattern is
  legitimate; otherwise reach out to the caller.
* Long-term: profile the slow endpoint via the W14
  `TournamentQueryLatencyMetrics` traces.

### p95-ticket

* **Threshold:** `histogram_quantile(0.95, …) > 0.25s`
* **Window:** 15-minute rate, 15-minute `for`
* **Severity:** `ticket` — files a non-paging followup.

Trips when 1-in-20 tournament-scale queries cross 250 ms for
fifteen consecutive minutes. The TICKET rail is intentionally
slower-paced than the PAGE rail — by the time it fires, the
operator usually has a clear pattern of degradation on the
dashboard. Cross-reference with the W15 bracket store
dashboard (Grafana ID `bishop-bracket-store-v1`).

## Dashboards

* **Primary:** the W15 bracket store dashboard renders the
  `tournament_query_duration_seconds` histogram heatmap, the p95
  + p99 quantile lines, and the per-endpoint volume bars.
* **Secondary:** `mahjong_active_games_total` from the W3
  collector — a sudden burst in active games often precedes the
  TICKET rail by a few minutes (callers query brackets more
  aggressively during peak game-count windows).

## Escalation

Tag `@long2know/backend` (Bishop's lane) for the PAGE; cc
`@long2know/qa` (Vasquez) when the regression is reproducible in
the contract-test harness. Apone owns the Prometheus runtime
itself — escalate to `@long2know/devops` if the alert is
silenced or stuck firing.

## Related

* `docs/realtime-resilience.md §8` — SignalR sequence metrics.
* `docs/bracket-shape.md §6` — page-size tuning rationale (W14).
* `docs/replay-by-id.md §4.1` — per-tenant retention admin
  (W17 surface that landed the runtime rate-limit shed).
