using System.Diagnostics;
using System.Globalization;
using System.Text;
using Mahjong.Autotable.Api.Changsha.Runtime;

namespace Mahjong.Autotable.Api.Observability;

/// <summary>
/// Phase J Wave 5 — minimal Prometheus exposition for the Docker /
/// self-hosted deployment (Apone, DevOps). Emitted in the canonical
/// <c>text/plain; version=0.0.4</c> format so a Prometheus scrape job
/// can ingest it without conversion.
///
/// The endpoint is deliberately built without a new NuGet dependency —
/// the three gauges below cover the operator-visibility baseline (uptime,
/// active game count, build SHA label). Anything heavier (counters,
/// histograms, observable instruments) should land via
/// <c>prometheus-net.AspNetCore</c> in a follow-up wave, not as an
/// ad-hoc bolt-on here.
/// </summary>
public static class MetricsEndpoint
{
    /// <summary>
    /// Process start anchor sourced from the OS — read once, lazily, at the
    /// time the type is first touched. <see cref="Process.StartTime"/> is the
    /// wall-clock instant the dotnet host was launched, not the time the type
    /// was first loaded, so a long lazy-init delay still produces a sensible
    /// uptime value (the equivalent <c>processStartTime</c> anchor in
    /// <see cref="Program"/> uses <see cref="DateTimeOffset.UtcNow"/> at module
    /// load, which agrees to within tens of milliseconds in practice).
    /// </summary>
    public static readonly DateTimeOffset ProcessStartTime = ResolveProcessStartTime();

    private static DateTimeOffset ResolveProcessStartTime()
    {
        try
        {
            return new DateTimeOffset(Process.GetCurrentProcess().StartTime.ToUniversalTime(), TimeSpan.Zero);
        }
        catch
        {
            // Some sandboxed runtimes (e.g. AOT-published single-file with no
            // process info) throw on Process.GetCurrentProcess; fall back to
            // "now" so the gauge degrades to "uptime since first scrape" rather
            // than failing the response.
            return DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Render the current snapshot as Prometheus exposition text.
    /// Resolved per request because <see cref="IChangshaGameRuntime.GameCount"/>
    /// is a live count, not a cached value.
    /// </summary>
    public static IResult Render(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var runtime = services.GetRequiredService<IChangshaGameRuntime>();
        var sha = Environment.GetEnvironmentVariable("BUILD_SHA");
        if (string.IsNullOrEmpty(sha))
        {
            sha = "dev";
        }

        var uptimeSeconds = Math.Max(0.0, (DateTimeOffset.UtcNow - ProcessStartTime).TotalSeconds);

        var sb = new StringBuilder(512);

        sb.AppendLine("# HELP mahjong_uptime_seconds Process uptime in seconds since the API container started.");
        sb.AppendLine("# TYPE mahjong_uptime_seconds gauge");
        sb.Append("mahjong_uptime_seconds ");
        sb.AppendLine(uptimeSeconds.ToString("F3", CultureInfo.InvariantCulture));

        sb.AppendLine("# HELP mahjong_active_games_total Currently active in-memory Changsha games (non-terminal).");
        sb.AppendLine("# TYPE mahjong_active_games_total gauge");
        sb.Append("mahjong_active_games_total ");
        sb.AppendLine(runtime.GameCount.ToString(CultureInfo.InvariantCulture));

        sb.AppendLine("# HELP mahjong_build_info Build identifier surfaced as a label. Always 1; the sha=\"...\" label carries the value.");
        sb.AppendLine("# TYPE mahjong_build_info gauge");
        sb.Append("mahjong_build_info{sha=\"");
        sb.Append(EscapeLabelValue(sha));
        sb.AppendLine("\"} 1");

        return Results.Text(sb.ToString(), "text/plain; version=0.0.4");
    }

    /// <summary>
    /// Prometheus label-value escaping per the exposition format spec:
    /// backslash, double-quote, and newline must be escaped. The BUILD_SHA
    /// environment variable is set by CI to a 40-char hex commit SHA so
    /// in practice nothing here gets escaped, but the rule is included so
    /// an operator can pass a free-form label without breaking the parse.
    /// </summary>
    private static string EscapeLabelValue(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        if (value.IndexOfAny(['\\', '"', '\n']) < 0) return value;

        var sb = new StringBuilder(value.Length + 8);
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"':  sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                default:   sb.Append(ch); break;
            }
        }
        return sb.ToString();
    }
}
