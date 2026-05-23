using Mahjong.Autotable.Api.Auth;
using Mahjong.Autotable.Api.Data;
using Mahjong.Autotable.Api.Data.Entities;
using Mahjong.Autotable.Api.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Mahjong.Autotable.Api.Tournament;

/// <summary>
/// Phase J Wave 10 — REST surface for tournament mode:
/// <list type="bullet">
///   <item><c>GET    /api/tournaments[?status=]</c> — list (anonymous).</item>
///   <item><c>GET    /api/tournaments/{id}</c>      — details + matches + standings (anonymous).</item>
///   <item><c>POST   /api/tournaments</c>           — create (auth required → 401).</item>
///   <item><c>POST   /api/tournaments/{id}/register</c>   — register (auth required).</item>
///   <item><c>DELETE /api/tournaments/{id}/register</c>   — unregister (auth required).</item>
///   <item><c>POST   /api/tournaments/{id}/start</c>      — start (creator only → 403).</item>
///   <item><c>GET    /api/tournaments/{id}/leaderboard</c>— standings (anonymous).</item>
/// </list>
///
/// <para>Per Wave-10 spec the service is the canonical owner of state
/// transitions; the controller is a thin adapter. Auth is resolved via
/// <see cref="AuthCookieService"/> — anonymous reads remain accessible
/// (the lobby is public) but every mutation requires a session.</para>
/// </summary>
[ApiController]
[Route("api/tournaments")]
[EnableRateLimiting(RateLimitingExtensions.ApiPolicy)]
public sealed class TournamentController : ControllerBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AuthCookieService _cookies;

    public TournamentController(IServiceScopeFactory scopeFactory, AuthCookieService cookies)
    {
        _scopeFactory = scopeFactory;
        _cookies = cookies;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<TournamentService>();
        var tournaments = await svc.ListAsync(status, ct);
        return Ok(new { tournaments = tournaments.Select(ToDto).ToArray() });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<TournamentService>();
        var t = await svc.GetAsync(id, ct);
        if (t is null) return NotFound(new { error = "Tournament not found.", id });
        var regs = await svc.ListRegistrationsAsync(id, ct);
        var matches = await svc.ListMatchesAsync(id, ct);
        return Ok(new
        {
            tournament = ToDto(t),
            registrations = regs.Select(r => new { r.PlayerId, r.Seed, r.RegisteredAt }).ToArray(),
            matches = matches.Select(MatchToDto).ToArray(),
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBody? body, CancellationToken ct)
    {
        var session = await _cookies.ResolveAsync(HttpContext, ct);
        if (session is null) return Unauthorized(new { error = "Authentication required to create tournaments." });
        if (body is null || string.IsNullOrWhiteSpace(body.Name))
            return BadRequest(new { error = "name is required." });
        if (string.IsNullOrWhiteSpace(body.Format))
            return BadRequest(new { error = "format is required." });

        using var scope = _scopeFactory.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<TournamentService>();
        try
        {
            var t = await svc.CreateAsync(
                body.Name,
                body.Format,
                session.PlayerId,
                body.MaxPlayers ?? 16,
                body.GamesPerMatch ?? 1,
                ct);
            return CreatedAtAction(nameof(Get), new { id = t.Id }, ToDto(t));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/register")]
    public async Task<IActionResult> Register([FromRoute] Guid id, CancellationToken ct)
    {
        var session = await _cookies.ResolveAsync(HttpContext, ct);
        if (session is null) return Unauthorized(new { error = "Authentication required to register." });

        using var scope = _scopeFactory.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<TournamentService>();
        try
        {
            var reg = await svc.RegisterAsync(id, session.PlayerId, ct);
            return Ok(new { registration = new { reg.Id, reg.PlayerId, reg.Seed, reg.RegisteredAt } });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}/register")]
    public async Task<IActionResult> Unregister([FromRoute] Guid id, CancellationToken ct)
    {
        var session = await _cookies.ResolveAsync(HttpContext, ct);
        if (session is null) return Unauthorized(new { error = "Authentication required to unregister." });

        using var scope = _scopeFactory.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<TournamentService>();
        try
        {
            var removed = await svc.UnregisterAsync(id, session.PlayerId, ct);
            return removed ? NoContent() : NotFound(new { error = "Registration not found.", id });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> Start([FromRoute] Guid id, CancellationToken ct)
    {
        var session = await _cookies.ResolveAsync(HttpContext, ct);
        if (session is null) return Unauthorized(new { error = "Authentication required to start a tournament." });

        using var scope = _scopeFactory.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<TournamentService>();
        try
        {
            var matches = await svc.StartAsync(id, session.PlayerId, ct);
            return Ok(new { matches = matches.Select(MatchToDto).ToArray() });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}/leaderboard")]
    public async Task<IActionResult> Leaderboard([FromRoute] Guid id, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<TournamentService>();
        var rows = await svc.LeaderboardAsync(id, ct);
        return Ok(new { leaderboard = rows.Select(r => new { r.PlayerId, r.Wins, r.Buchholz }).ToArray() });
    }

    private static object ToDto(Data.Entities.Tournament t) => new
    {
        t.Id,
        t.Name,
        t.Format,
        t.Status,
        t.CreatedByPlayerId,
        t.MaxPlayers,
        t.GamesPerMatch,
        t.CreatedAt,
        t.StartedAt,
        t.CompletedAt,
    };

    private static object MatchToDto(TournamentMatch m) => new
    {
        m.Id,
        m.TournamentId,
        m.Round,
        m.Player1Id,
        m.Player2Id,
        m.Player3Id,
        m.Player4Id,
        m.WinnerPlayerId,
        m.Status,
        m.CreatedAt,
        m.CompletedAt,
    };

    public sealed class CreateBody
    {
        public string? Name { get; set; }
        public string? Format { get; set; }
        public int? MaxPlayers { get; set; }
        public int? GamesPerMatch { get; set; }
    }
}
