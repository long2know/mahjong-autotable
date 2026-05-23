using Mahjong.Autotable.Api.Changsha.Runtime;
using Mahjong.Autotable.Api.Matchmaking;
using Mahjong.Autotable.Api.Players;
using Microsoft.AspNetCore.SignalR;

namespace Mahjong.Autotable.Api.Changsha;

/// <summary>
/// SignalR hub for Changsha Mahjong. Thin command/dispatch layer over IChangshaGameRuntime.
/// All state mutation lives in the runtime; the hub validates connection identity and
/// adapts inbound RPCs to runtime calls. See docs/rules/changsha-signalr-contract.md.
///
/// <para><b>Phase J Wave 6 identity model:</b> the hub no longer treats the
/// SignalR <c>ConnectionId</c> as the player identity. On
/// <see cref="OnConnectedAsync"/> it resolves the persistent player id from
/// the <c>mahjong_pid</c> cookie via <see cref="PlayerIdentityService"/> and
/// stashes it in <see cref="HubCallerContext.Items"/> under
/// <see cref="PlayerIdentityExtensions.PlayerIdItemKey"/>. Every RPC reads
/// the persistent id through <see cref="PlayerIdentityExtensions.GetPlayerId"/>;
/// SignalR connection ids are still passed alongside for transport-level
/// routing (Group membership, per-connection <c>Clients.Client(connId)</c>
/// sends), but they no longer drive identity. Frontend should call
/// <c>POST /api/identity</c> once before opening the hub connection so a
/// long-lived cookie is pinned to the browser; without that call the hub
/// mints a session-scoped id on connect which still survives the lifetime
/// of the single connection.</para>
/// </summary>
public sealed class ChangshaHub : Hub
{
    private readonly IChangshaGameRuntime _runtime;
    private readonly MatchmakingService _matchmaking;
    private readonly PlayerProfileService _profiles;
    private readonly PlayerIdentityService _identity;

    public ChangshaHub(
        IChangshaGameRuntime runtime,
        MatchmakingService matchmaking,
        PlayerProfileService profiles,
        PlayerIdentityService identity)
    {
        _runtime = runtime;
        _matchmaking = matchmaking;
        _profiles = profiles;
        _identity = identity;
    }

    // ── Client → Server commands ──────────────────────────────────────

    public async Task<object> CreateGame(string ruleSet, int[]? botSeatIndexes = null, int? seed = null)
    {
        var gameId = await _runtime.CreateGameAsync(
            seed,
            botSeatIndexes,
            hostPlayerId: Context.GetPlayerId(),
            hostConnectionId: Context.ConnectionId,
            Context.ConnectionAborted);
        return new { gameId };
    }

    public async Task<object> JoinTable(string gameId)
    {
        await _runtime.JoinTableAsync(gameId, Context.ConnectionId, Context.ConnectionAborted);
        return new { success = true };
    }

    public async Task<object> TakeSeat(string gameId, int? seatIndex = null)
    {
        var seat = await _runtime.TakeSeatAsync(
            gameId,
            Context.GetPlayerId(),
            Context.ConnectionId,
            seatIndex,
            Context.ConnectionAborted);
        return new { success = true, seatIndex = seat };
    }

    public async Task<object> FillWithBots(string gameId)
    {
        await _runtime.FillEmptySeatsWithBotsAsync(gameId, Context.ConnectionAborted);
        return new { success = true };
    }

    public async Task<object> StartGame(string gameId)
    {
        await _runtime.StartGameAsync(gameId, Context.ConnectionAborted);
        return new { success = true };
    }

    public Task AcknowledgeDeal(string gameId, int seatIndex) =>
        _runtime.AcknowledgeDealAsync(gameId, seatIndex, Context.ConnectionAborted);

    public Task Discard(string gameId, int seatIndex, int tileId) =>
        _runtime.DiscardAsync(gameId, seatIndex, tileId, Context.ConnectionAborted);

    public Task Claim(string gameId, int seatIndex, string type, int[]? tileIds = null) =>
        _runtime.ClaimAsync(gameId, seatIndex, type, tileIds, Context.ConnectionAborted);

    public Task Pass(string gameId, int seatIndex) =>
        _runtime.PassAsync(gameId, seatIndex, Context.ConnectionAborted);

    public Task DeclareKong(string gameId, int seatIndex, int[] tileIds) =>
        _runtime.DeclareKongAsync(gameId, seatIndex, tileIds, Context.ConnectionAborted);

    public Task DeclareWin(string gameId, int seatIndex) =>
        _runtime.DeclareWinAsync(gameId, seatIndex, Context.ConnectionAborted);

    public async Task<object> ReconnectGame(string gameId, int seatIndex)
    {
        var ok = await _runtime.ReconnectAsync(
            gameId,
            seatIndex,
            Context.GetPlayerId(),
            Context.ConnectionId,
            Context.ConnectionAborted);
        return new { success = ok };
    }

    // ── Phase J Wave 5 — Matchmaking + Profile RPCs ──────────────────

    /// <summary>
    /// Phase J Wave 6 — toggles a game's public-listing flag. Only the
    /// original host (matched by <c>state.CreatorPlayerId ==
    /// callerPlayerId</c>) may call this. <paramref name="publicName"/>
    /// is the friendly lobby label (trimmed, capped at 64 chars by the
    /// service). Throws <see cref="HubException"/> if the caller isn't the
    /// host or the game has already left the Seating phase. Wave-6 passes
    /// the cookie-derived persistent player id so the check survives
    /// reconnects (Wave-5 used the SignalR connection id which broke
    /// every refresh).
    /// </summary>
    public async Task<object> SetGamePublic(string gameId, bool isPublic, string? publicName = null)
    {
        await _matchmaking.SetGamePublicAsync(
            gameId,
            Context.GetPlayerId(),
            isPublic,
            publicName,
            Context.ConnectionAborted);
        return new { success = true, isPublic, publicName };
    }

    /// <summary>
    /// Phase J Wave 6 — seats the caller into a randomly-picked public,
    /// lobby-phase game. Returns <c>{ matched: false }</c> when no candidate
    /// exists (Hicks's UI should fall back to "create a game"). On success
    /// returns the chosen gameId + seatIndex so the client knows where it
    /// landed; the runtime has already added the connection to the SignalR
    /// group (mirrors <c>TakeSeat</c>). Wave-6 passes the persistent player
    /// id alongside the transport connection id so the seat's
    /// <c>PlayerId</c> reflects the cookie-derived identity.
    /// </summary>
    public async Task<object> JoinRandom(string? variant = null)
    {
        var result = await _matchmaking.JoinRandomAsync(
            Context.GetPlayerId(),
            Context.ConnectionId,
            variant,
            Context.ConnectionAborted);
        if (result is null) return new { matched = false };
        return new { matched = true, gameId = result.Value.GameId, seatIndex = result.Value.SeatIndex };
    }

    /// <summary>
    /// Phase J Wave 6 — updates the caller's <see cref="PlayerProfile"/>
    /// (display name + optional avatar colour). Returns the resulting
    /// profile + stats DTO using the same shape as the
    /// <c>ProfileLoaded</c> on-connect event. Keyed by the persistent
    /// cookie-derived player id so edits survive reconnects.
    /// </summary>
    public async Task<object> UpdateProfile(string displayName, string? avatarColor = null)
    {
        var playerId = Context.GetPlayerId();
        var profile = await _profiles.UpdateDisplayNameAsync(playerId, displayName, Context.ConnectionAborted);
        if (!string.IsNullOrEmpty(avatarColor))
        {
            profile = await _profiles.UpdateAvatarColorAsync(playerId, avatarColor, Context.ConnectionAborted);
        }
        var stats = await _profiles.GetStatsAsync(playerId, Context.ConnectionAborted);
        return BuildProfileDto(profile, stats);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();

        // Phase J Wave 6 — resolve the persistent player id from the
        // mahjong_pid cookie carried with the SignalR negotiate / WS
        // upgrade handshake. If absent we mint a session-scoped id on the
        // spot; we do NOT write a Set-Cookie header from here because the
        // response headers have already been flushed by the SignalR
        // transport machinery. The frontend should call
        // POST /api/identity FIRST so a long-lived cookie is pinned to the
        // browser before this hub negotiates.
        var pid = ResolvePlayerId();
        Context.Items[PlayerIdentityExtensions.PlayerIdItemKey] = pid;

        // Phase J Wave 5 — broadcast the caller's profile + career stats on
        // connect so the frontend renders the avatar/display-name chip
        // without an extra round-trip. Auto-creates the profile on first
        // connect via PlayerProfileService.GetOrCreateAsync. Phase J Wave 6
        // keyed by the persistent player id (Wave-5 used ConnectionId, which
        // shed identity on every reconnect).
        try
        {
            var profile = await _profiles.GetOrCreateAsync(pid, Context.ConnectionAborted);
            var stats = await _profiles.GetStatsAsync(pid, Context.ConnectionAborted);
            await Clients.Caller.SendAsync("ProfileLoaded", BuildProfileDto(profile, stats), Context.ConnectionAborted);
        }
        catch
        {
            // Non-fatal — a profile DB hiccup must not break the connection.
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Phase J Wave 6 — pass both the persistent player id (for host-
        // transfer / public-game cleanup) and the transport connection id
        // (for SeatConnections release). Fallback to ConnectionId for the
        // playerId arg if the connect handshake never stored an id (e.g.
        // very-early disconnect path).
        var pid = (Context.Items.TryGetValue(PlayerIdentityExtensions.PlayerIdItemKey, out var v) && v is string s)
            ? s
            : Context.ConnectionId;
        await _runtime.HandleDisconnectAsync(pid, Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Reads the persistent player id from the <c>mahjong_pid</c> cookie
    /// attached to the SignalR negotiate / WS-upgrade handshake; mints a
    /// fresh server-side id if the cookie is absent. Mint path does NOT
    /// write the cookie back (response headers are already sent by
    /// OnConnectedAsync); frontend should call POST /api/identity first.
    /// </summary>
    private string ResolvePlayerId()
    {
        var http = Context.GetHttpContext();
        if (http is not null)
        {
            var fromCookie = _identity.ResolveFromCookie(http);
            if (!string.IsNullOrEmpty(fromCookie)) return fromCookie;
        }
        return _identity.Mint();
    }

    private static object BuildProfileDto(PlayerProfile profile, PlayerStats stats) => new
    {
        playerId = profile.PlayerId,
        displayName = profile.DisplayName,
        avatarColor = profile.AvatarColor,
        createdAt = profile.CreatedAt,
        lastSeenAt = profile.LastSeenAt,
        stats = new
        {
            gamesPlayed = stats.GamesPlayed,
            gamesWon = stats.GamesWon,
            totalScore = stats.TotalScore,
            highestSingleGameScore = stats.HighestSingleGameScore,
            longestWinStreak = stats.LongestWinStreak,
            currentWinStreak = stats.CurrentWinStreak,
            lastGameAt = stats.LastGameAt
        }
    };
}
