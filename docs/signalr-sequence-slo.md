# SignalR Sequence SLO

> Phase K Wave 16 — Bishop (Backend). Operator-facing SLO + error-budget
> contract for the SignalR sequence-replay surface that landed across
> waves 6 through 15. This document defines the customer-promise, the
> measurement methodology, and the runbook for the on-call engineer.

## 1. Promise

> The SignalR sequence-replay surface is available **99.95% of the time
> over a rolling 30-day window**.

That translates to:

| Window      | Allowed error budget |
| ----------- | -------------------- |
| 30 days     | **21.6 minutes**     |
| 7 days      | **5.04 minutes**     |
| 24 hours    | **43.2 seconds**     |
| 1 hour      | **1.8 seconds**      |

The 30-day budget (**21.6 minutes / month**) is the headline number; the
shorter windows are the burn-rate guards.

## 2. What counts as "available"?

A request to one of the SignalR sequence-replay surfaces is **good** when
all of the following hold:

1. The client's reconnect attempt was accepted (the
   `JoinTable` / `JoinGame` invocation returned without an exception).
2. The server replayed any missing frames in the
   `signalr_seq_replay_from_ack_total{result="ok"}` bucket.
3. The replay completed within the **2 second p99** latency target.

A request is **bad** when any of:

- The `signalr_seq_replay_from_ack_total{result="error"}` counter
  incremented (server-side store fault or sequence-id corruption).
- The `signalr_seq_replay_from_ack_total{result="cap_exceeded"}` counter
  incremented (the client fell behind further than the configured
  retention window — see §5 below).
- The reconnect attempt itself returned an HTTP 5xx.

## 3. Measurement (PromQL)

The canonical good-event ratio over a window `$W`:

```promql
sum(rate(signalr_seq_replay_from_ack_total{result="ok"}[$W]))
/
sum(rate(signalr_seq_replay_from_ack_total[$W]))
```

The corresponding **bad-event ratio**:

```promql
1 - (
  sum(rate(signalr_seq_replay_from_ack_total{result="ok"}[$W]))
  /
  sum(rate(signalr_seq_replay_from_ack_total[$W]))
)
```

For the **burn rate** (how fast we are consuming the error budget) over
window `$W`:

```promql
(
  1 - sum(rate(signalr_seq_replay_from_ack_total{result="ok"}[$W]))
      / sum(rate(signalr_seq_replay_from_ack_total[$W]))
) / 0.0005
```

`0.0005` is `1 - 0.9995` — the allowed bad-event ratio. A burn rate of
`1.0` means we are spending the budget at the long-term sustainable rate;
anything above `1.0` is unsustainable.

## 4. Alerts

Two paired alerts (the standard Google SRE Workbook 2-window structure):

| Alert            | Burn rate | Long window | Short window | Page?  |
| ---------------- | --------- | ----------- | ------------ | ------ |
| `fast-burn`      | > 14.4    | 1h          | 5m           | Yes    |
| `slow-burn`      | > 6.0     | 6h          | 30m          | Yes    |
| `monthly-budget` | > 1.0     | 30d         | 6h           | Ticket |

Concretely, the `fast-burn` Prometheus alert expression:

```promql
(
  (1 - sum(rate(signalr_seq_replay_from_ack_total{result="ok"}[1h]))
       / sum(rate(signalr_seq_replay_from_ack_total[1h]))) > (14.4 * 0.0005)
)
AND
(
  (1 - sum(rate(signalr_seq_replay_from_ack_total{result="ok"}[5m]))
       / sum(rate(signalr_seq_replay_from_ack_total[5m]))) > (14.4 * 0.0005)
)
```

The supporting metrics that the alert annotations should reference:

- `signalr_seq_store_rows_active` — gauge. Spiking when retention sweep
  is falling behind. See `SignalRSequenceRetentionSweep`.
- `signalr_seq_retention_sweep_deleted_total` — counter. Flat-line for
  >2h suggests the sweep is stuck.

## 5. Runbook (on-call)

When a burn-rate alert fires:

1. **Confirm the error**. Open the dashboard
   `signalr-sequence-replay` (Grafana). The `result` breakdown panel
   surfaces whether the burn is driven by `error` (server fault) or
   `cap_exceeded` (client behind retention window).

2. **If `error` is dominant**:
   - Check `signalr_seq_store_rows_active`. If gauge is climbing the
     retention sweep is stuck — restart the
     `SignalRSequenceRetentionSweep` hosted service.
   - Check the storage backend. The store is selected by
     `SignalR:Sequence:StorageImpl` (`InMemory` for tests, `Ef` for
     prod). For `Ef`, inspect the database connection pool — a wedged
     pool surfaces as `result="error"`.

3. **If `cap_exceeded` is dominant**:
   - The retention window is too short for current client behaviour
     (slow networks, mobile sleeps). Raise the configured retention
     via `SignalR:Sequence:RetentionMinutes`. The default 30 minutes
     is the W14 floor; production may run up to 120 minutes.

4. **If the burn is short-window only (5m / 30m)**:
   - Often a single noisy client. Inspect
     `signalr_seq_replay_from_ack_total` by `hub` label — if one hub
     dominates, capture the client id and reach out to the affected
     tenant.

## 6. Budget-burn examples

- **At 14.4× burn**, the entire 30-day budget is consumed in
  `30d / 14.4 ≈ 50 hours`. Page immediately.
- **At 6× burn**, the budget is consumed in `30d / 6 = 5 days`. Page
  during business hours.
- **At 1× burn**, the budget is consumed exactly on schedule —
  acceptable.

## 7. Wave history

- **W6** landed the in-memory sequence store + the broadcaster.
- **W7** added the join-reconnect handshake.
- **W11** added the EF-backed store + retention sweep.
- **W12** added the rate-limit policy + back-pressure broadcaster.
- **W13** added the cost-budget broadcaster (paired surface).
- **W14** landed the `signalr_seq_*` metrics this SLO measures.
- **W15** added the staged-rotation cadence-check pairing.
- **W16** (this wave) — formalised the 99.95% SLO + 21.6-minute
  monthly error budget in this document; paired with the per-tenant
  JWKS rotation hard-gate so signing failures don't bleed into the
  SLO.

## 8. Open work for Wave 17

- Per-hub SLO (currently aggregated across all hubs).
- Per-tenant SLO once the W16 per-tenant JWKS rotation surface lands
  the per-tenant client-id correlation.
- Migrate to multi-window multi-burn-rate alerts once the W17
  observability fleet supports `for_state: pending` properly.
