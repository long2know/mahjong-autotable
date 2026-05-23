namespace Mahjong.Autotable.Api.Commentary;

/// <summary>
/// Phase K Wave 8 — Bishop. Configuration knobs for the LLM-driven
/// commentary pipeline. Default <c>Provider = "Stub"</c> keeps the
/// Wave-6/7 surface running on the deterministic stub; operators
/// flip to <c>"OpenAI"</c> or <c>"Azure"</c> to wire the real LLM.
///
/// <para>The <see cref="ApiKey"/> field accepts either a literal
/// key or the <c>env:VAR_NAME</c> indirection (read at startup). The
/// indirection keeps secrets out of the appsettings.json blob;
/// production deployments configure the key via the
/// <c>COMMENTARY_API_KEY</c> environment variable.</para>
/// </summary>
public sealed class CommentaryOptions
{
    /// <summary>Active provider implementation:
    /// <c>"Stub"</c> (default; deterministic placeholder),
    /// <c>"OpenAI"</c> (Chat Completions HTTP), or <c>"Azure"</c>
    /// (Azure OpenAI Chat Completions). Case-insensitive.</summary>
    public string Provider { get; set; } = "Stub";

    /// <summary>HTTP base URI for the provider. Example values:
    /// <c>"https://api.openai.com/v1"</c> (OpenAI),
    /// <c>"https://my-resource.openai.azure.com/openai/deployments/gpt-4o-mini"</c>
    /// (Azure OpenAI).</summary>
    public string Endpoint { get; set; } = "https://api.openai.com/v1";

    /// <summary>Provider API key. Accepts either a literal key or
    /// the <c>"env:VAR_NAME"</c> indirection so secrets aren't
    /// checked-in. Empty/null on the stub provider.</summary>
    public string? ApiKey { get; set; }

    /// <summary>LLM model identifier. Default <c>gpt-4o-mini</c>
    /// keeps the token + latency budget moderate.</summary>
    public string Model { get; set; } = "gpt-4o-mini";

    /// <summary>Minimum interval (seconds) between successive
    /// generations for the same game. Default 5s matches the W8
    /// spec; below this the generator returns the most recent
    /// cached output rather than re-calling the LLM.</summary>
    public int RateLimitPerGameSeconds { get; set; } = 5;

    /// <summary>Monthly token cap (input + output tokens). When
    /// exceeded, generation switches to the fail-open path
    /// ("[commentary unavailable]") and an audit row is written.
    /// 0 = unlimited (default for local dev).</summary>
    public long MonthlyTokenCap { get; set; } = 0;

    /// <summary>HTTP request timeout (seconds) for outbound LLM
    /// calls. Default 15s — generous for batch generations, short
    /// enough that a stuck provider doesn't pin a worker
    /// thread.</summary>
    public int RequestTimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Phase K Wave 9 — Bishop. Toggle for the
    /// <see cref="ICommentaryUsageMeter"/> implementation. Two values
    /// are supported:
    /// <list type="bullet">
    ///   <item><c>"InMemory"</c> — singleton, lost on process
    ///         restart. Default for tests + single-replica
    ///         development.</item>
    ///   <item><c>"Ef"</c> — durable per-month ledger persisted to
    ///         the <c>CommentaryUsage</c> table via
    ///         <c>EfCommentaryUsageMeter</c>. Default for the
    ///         multi-replica production deployment.</item>
    /// </list>
    /// </summary>
    public string UsageMeterImpl { get; set; } = "InMemory";

    /// <summary>
    /// Phase K Wave 9 — Bishop. When <c>true</c>, exceeding the
    /// <see cref="MonthlyTokenCap"/> causes the commentary surface
    /// to throw <see cref="UsageCapExceededException"/> (mapped to
    /// HTTP 429) instead of returning the fail-open
    /// "[commentary unavailable]" envelope. Defaults to false so
    /// existing surfaces keep their soft-degradation posture.
    /// </summary>
    public bool ThrowOnMonthlyCap { get; set; } = false;

    /// <summary>
    /// Resolves the effective API key, expanding the
    /// <c>"env:VAR_NAME"</c> indirection when present. Returns null
    /// when the key is unset/empty so callers can short-circuit
    /// before constructing the HTTP request.
    /// </summary>
    public string? ResolveApiKey()
    {
        if (string.IsNullOrWhiteSpace(ApiKey)) return null;
        if (ApiKey.StartsWith("env:", StringComparison.Ordinal))
        {
            var varName = ApiKey.Substring("env:".Length).Trim();
            if (string.IsNullOrEmpty(varName)) return null;
            return Environment.GetEnvironmentVariable(varName);
        }
        return ApiKey;
    }
}
