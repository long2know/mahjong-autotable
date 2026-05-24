using Microsoft.Extensions.Hosting;

namespace Mahjong.Autotable.Api.Observability;

/// <summary>
/// Phase K Wave 13 — Bishop. Always-on retention sweeper for the
/// <see cref="ISignalRSequenceStore"/>. W12 shipped
/// <see cref="SignalRSequenceSweepService"/> as an EF-only background
/// service; W13 lifts the sweep to run for any store implementation
/// (InMemory + EF) so an over-long single-replica development session
/// also collects expired rows.
///
/// <para>The sweep cadence is bound from the new dedicated config key
/// <c>SignalR:Sequences:SweepIntervalMinutes</c> (default 5). Falls
/// back to the legacy <c>SignalRSequenceStoreOptions.SweepIntervalMinutes</c>
/// when the new key is absent so an operator upgrading from W12 keeps
/// the existing cadence without an appsettings edit.</para>
///
/// <para>The hosted service deletes rows where
/// <c>ExpiresAt &lt; now</c> (the schema column maps the spec's
/// "<c>LastSeenAt &lt; now - retention</c>" semantic — both the EF
/// store + the in-memory store stamp <c>ExpiresAt = CreatedAt +
/// RetentionMinutes</c> at append time, so the sweep predicate is
/// equivalent).</para>
///
/// <para>See <c>docs/realtime-resilience.md §7</c>.</para>
/// </summary>
public sealed class SignalRSequenceRetentionSweep : BackgroundService
{
    /// <summary>Floor on the sweep cadence — protects against a
    /// mis-configured 0 / negative value that would spin the loop.</summary>
    public const int MinSweepIntervalMinutes = 1;

    /// <summary>Default sweep cadence in minutes. Matches the canonical
    /// guidance in <c>docs/realtime-resilience.md §7</c>.</summary>
    public const int DefaultSweepIntervalMinutes = 5;

    private readonly ISignalRSequenceStore _store;
    private readonly int _intervalMinutes;
    private readonly ILogger<SignalRSequenceRetentionSweep> _logger;

    public SignalRSequenceRetentionSweep(
        ISignalRSequenceStore store,
        SignalRSequenceRetentionSweepOptions options,
        ILogger<SignalRSequenceRetentionSweep> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _intervalMinutes = options.SweepIntervalMinutes > 0
            ? options.SweepIntervalMinutes
            : DefaultSweepIntervalMinutes;
        if (_intervalMinutes < MinSweepIntervalMinutes)
        {
            _intervalMinutes = MinSweepIntervalMinutes;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(_intervalMinutes);
        _logger.LogInformation(
            "SignalRSequenceRetentionSweep started (interval={Minutes}m).",
            interval.TotalMinutes);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "SignalRSequenceRetentionSweep failed (non-fatal); next tick in {Minutes}m.",
                    interval.TotalMinutes);
            }
        }
    }

    /// <summary>Single-sweep entry-point — exposed so tests can drive
    /// deletions deterministically without waiting for the
    /// background timer to fire.</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        var removed = await _store.SweepExpiredAsync(DateTime.UtcNow, ct);
        if (removed > 0)
        {
            _logger.LogInformation(
                "SignalRSequenceRetentionSweep removed {Count} expired entries.", removed);
        }
        return removed;
    }
}

/// <summary>
/// Phase K Wave 13 — Bishop. Configuration block for
/// <see cref="SignalRSequenceRetentionSweep"/>. Bound from the
/// <c>SignalR:Sequences</c> section so the new
/// <c>SweepIntervalMinutes</c> key sits alongside the W12
/// <see cref="SignalRSequenceStoreOptions"/> retention knobs.
/// </summary>
public sealed class SignalRSequenceRetentionSweepOptions
{
    /// <summary>Cadence between sweeps, minutes. 0 / negative
    /// → default (<see cref="SignalRSequenceRetentionSweep.DefaultSweepIntervalMinutes"/>).</summary>
    public int SweepIntervalMinutes { get; set; } =
        SignalRSequenceRetentionSweep.DefaultSweepIntervalMinutes;
}
