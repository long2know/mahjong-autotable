using Mahjong.Autotable.Api.Auth;
using Mahjong.Autotable.Api.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Mahjong.Autotable.Api.Commentary;

/// <summary>
/// Phase K Wave 14 — Bishop. REST surface for the commentary
/// LLM cost dashboard. W13 exposed the spend ledger through the
/// Prometheus <c>commentary_cost_dollars_total</c> counter + the
/// SignalR admin hub; W14 adds a plain JSON endpoint so non-
/// Prometheus dashboards (operator console, Slack /cost slash
/// command) can fetch the current month's spend without a
/// scraping pipeline.
///
/// <list type="bullet">
///   <item><c>GET /api/commentary/cost/summary</c> — admin-only.
///         Returns <c>{ currentMonthCost, budgetCapUsd,
///         percentUsed, monthlyTokens, tokensPerDollar, state,
///         model, month, byModel: [{ model, cost }] }</c>.</item>
/// </list>
///
/// <para>The endpoint is admin-gated to match the W13 SignalR
/// admin hub. Anonymous → 401; non-admin → 403. See
/// <c>docs/commentary-llm.md §6 "Cost dashboard endpoint"</c>.</para>
/// </summary>
[ApiController]
[Route("api/commentary")]
[EnableRateLimiting(RateLimitingExtensions.ApiPolicy)]
public sealed class CommentaryCostController : ControllerBase
{
    private readonly AuthCookieService _cookies;
    private readonly CommentaryCostBudget? _budget;
    private readonly IOptionsMonitor<CommentaryOptions>? _options;

    public CommentaryCostController(
        AuthCookieService cookies,
        CommentaryCostBudget? budget = null,
        IOptionsMonitor<CommentaryOptions>? options = null)
    {
        _cookies = cookies ?? throw new ArgumentNullException(nameof(cookies));
        _budget = budget;
        _options = options;
    }

    [HttpGet("cost/summary")]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        // Phase K Wave 14 — Bishop. Auth precedence:
        //   401 (no session) → 403 (non-admin) → 200.
        var session = await _cookies.ResolveAsync(HttpContext, ct);
        if (session is null)
        {
            return Unauthorized(new { error = "session-required" });
        }
        if (!string.Equals(session.Role, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "admin-required",
            });
        }

        var now = DateTime.UtcNow;
        var model = _options?.CurrentValue.Model ?? "unknown";
        var month = $"{now.Year:D4}-{now.Month:D2}";

        decimal currentCost = 0m;
        decimal capUsd = 0m;
        double percentUsed = 0d;
        long monthlyTokens = 0;
        long tokensPerDollar = 0;
        var state = BudgetState.Healthy.ToString();
        if (_budget is not null)
        {
            try
            {
                var eval = _budget.Evaluate(now);
                currentCost = decimal.Round(eval.MonthlyUsd, 4);
                capUsd = decimal.Round(eval.MonthlyCapUsd, 4);
                percentUsed = Math.Round(eval.Ratio * 100.0, 2);
                monthlyTokens = eval.MonthlyTokens;
                tokensPerDollar = eval.TokensPerDollar;
                state = eval.State.ToString();
            }
            catch
            {
                // Defensive: a transient store failure inside the
                // budget evaluation shouldn't 500 the endpoint. The
                // canonical zeroed envelope mirrors the metrics
                // endpoint's fail-safe shape.
            }
        }

        // Phase K Wave 14 — Bishop. The usage meter tracks a single
        // active model — the configured `Commentary:Model`. We
        // surface the same value verbatim as the lone `byModel`
        // entry so dashboards have a stable shape; future waves can
        // widen this once the meter is keyed by model.
        var byModel = new[]
        {
            new
            {
                model,
                cost = currentCost,
                monthlyTokens,
            },
        };

        return Ok(new
        {
            currentMonthCost = currentCost,
            budgetCapUsd = capUsd,
            percentUsed,
            monthlyTokens,
            tokensPerDollar,
            state,
            model,
            month,
            at = DateTimeOffset.UtcNow,
            byModel,
        });
    }
}
