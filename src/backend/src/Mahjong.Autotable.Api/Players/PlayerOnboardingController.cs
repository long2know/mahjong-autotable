using Mahjong.Autotable.Api.Auth;
using Mahjong.Autotable.Api.Data;
using Mahjong.Autotable.Api.Data.Entities;
using Mahjong.Autotable.Api.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Mahjong.Autotable.Api.Players;

/// <summary>
/// Phase K Wave 3 — Bishop (Backend). Server-authoritative onboarding
/// tour progress, surfaced via:
/// <list type="bullet">
///   <item><c>GET  /api/players/me/onboarding-status</c> — returns the
///         persisted envelope, defaulting to
///         <c>{ completed: false, stepsCompleted: 0 }</c> when no row
///         exists yet.</item>
///   <item><c>POST /api/players/me/onboarding-status</c> — upserts the
///         row. Body: <c>{ "completed": bool?, "stepsCompleted": int? }</c>.
///         <see cref="PlayerOnboardingStatus.StepsCompleted"/> climbs
///         monotonically; lower values are ignored. Once
///         <see cref="PlayerOnboardingStatus.Completed"/> is true, it
///         stays true.</item>
/// </list>
///
/// <para>Identity: anonymous-only — the <c>mahjong_pid</c> cookie is
/// the canonical key. Logged-in callers' sessions are read for the
/// same player id so anon → signed-in progression doesn't reset.</para>
/// </summary>
[ApiController]
[Route("api/players/me/onboarding-status")]
[EnableRateLimiting(RateLimitingExtensions.ApiPolicy)]
public sealed class PlayerOnboardingController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PlayerIdentityService _identity;
    private readonly AuthCookieService _cookies;

    public PlayerOnboardingController(
        AppDbContext db,
        PlayerIdentityService identity,
        AuthCookieService cookies)
    {
        _db = db;
        _identity = identity;
        _cookies = cookies;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var playerId = await ResolvePlayerIdAsync(ct);
        if (playerId is null)
        {
            // No identity at all — return the default envelope without
            // minting a cookie (GET shouldn't have side effects).
            return Ok(new
            {
                completed = false,
                stepsCompleted = 0,
                lastStepCompletedUtc = (DateTime?)null,
            });
        }

        var row = await _db.PlayerOnboardingStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.PlayerId == playerId, ct);
        return Ok(new
        {
            completed = row?.Completed ?? false,
            stepsCompleted = row?.StepsCompleted ?? 0,
            lastStepCompletedUtc = row?.LastStepCompletedUtc,
        });
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] OnboardingUpdateBody? body, CancellationToken ct)
    {
        if (body is null)
        {
            return BadRequest(new { error = "Body must include at least one of `completed` or `stepsCompleted`." });
        }

        // POST mints a cookie if none exists so the client can persist
        // progress across the first session.
        var playerId = _identity.ResolveOrMint(HttpContext);
        var now = DateTime.UtcNow;
        var row = await _db.PlayerOnboardingStatuses
            .FirstOrDefaultAsync(s => s.PlayerId == playerId, ct);
        if (row is null)
        {
            row = new PlayerOnboardingStatus
            {
                PlayerId = playerId,
                Completed = body.Completed ?? false,
                StepsCompleted = Math.Max(0, body.StepsCompleted ?? 0),
                LastStepCompletedUtc = (body.Completed == true || (body.StepsCompleted ?? 0) > 0) ? now : null,
                CreatedAt = now,
                UpdatedAt = now,
            };
            _db.PlayerOnboardingStatuses.Add(row);
        }
        else
        {
            // Monotonic step count — a lower value never regresses
            // the persisted progress.
            if (body.StepsCompleted is int steps && steps > row.StepsCompleted)
            {
                row.StepsCompleted = steps;
                row.LastStepCompletedUtc = now;
            }
            // Completed flips one-way to true.
            if (body.Completed == true && !row.Completed)
            {
                row.Completed = true;
                row.LastStepCompletedUtc = now;
            }
            row.UpdatedAt = now;
        }
        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            completed = row.Completed,
            stepsCompleted = row.StepsCompleted,
            lastStepCompletedUtc = row.LastStepCompletedUtc,
        });
    }

    private async Task<string?> ResolvePlayerIdAsync(CancellationToken ct)
    {
        // Prefer the persistent anon cookie — that's the canonical
        // onboarding scope. Fall back to the auth session player id
        // if the cookie is missing but a logged-in session exists.
        var cookieId = _identity.ResolveFromCookie(HttpContext);
        if (!string.IsNullOrEmpty(cookieId)) return cookieId;
        var session = await _cookies.ResolveAsync(HttpContext, ct);
        return session?.PlayerId;
    }

    /// <summary>
    /// Phase K Wave 3 — body shape for the onboarding-status POST.
    /// Both fields are optional; clients typically POST
    /// <c>{ stepsCompleted: N }</c> after each tour step and a final
    /// <c>{ completed: true, stepsCompleted: total }</c> when done.
    /// </summary>
    public sealed class OnboardingUpdateBody
    {
        public bool? Completed { get; set; }
        public int? StepsCompleted { get; set; }
    }
}
