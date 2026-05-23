using Mahjong.Autotable.Api.Changsha.Runtime;
using Mahjong.Autotable.Api.Matchmaking;
using Mahjong.Autotable.Api.Players;
using Microsoft.AspNetCore.SignalR;

namespace Mahjong.Autotable.Api.Changsha;

/// <summary>
/// SignalR hub for Changsha Mahjong. Thin command/dispatch layer over IChangshaGameRuntime.
/// All state mutation lives in the runtime; the hub validates connection identity and
/// adapts inbound RPCs to runtime calls. See docs/rules/changsha-signalr-contract.md.
/// </summary>
public sealed class ChangshaHub : Hub
{
    private readonly IChangshaGameRuntime _runtime;
    private readonly MatchmakingService _matchmaking;
    private readonly PlayerProfileService _profiles;

    public ChangshaHub(
        IChangshaGameRuntime runtime,
        MatchmakingService matchmaking,
        PlayerProfileService profiles)
    {
        _runtime = runtime;
        _matchmaking = matchmaking;
        _profiles = profiles;
    }

    // ── Client → Server commands ──────────────────────────────────────

    public async Task<object> CreateGame(string ruleSet, int[]? botSeatIndexes = null, int? seed = null)
    {
        var gameId = await _runtime.CreateGameAsync(seed, botSeatIndexes, Context.ConnectionId, Context.ConnectionAborted);
        return new { gameId };
    }

    public async Task<object> JoinTable(string gameId)
    {
        await _runtime.JoinTableAsync(gameId, Context.ConnectionId, Context.ConnectionAborted);
        return new { success = true };
    }

    public async Task<object> TakeSeat(string gameId, int? seatIndex = null)
    {
        var seat = await _runtime.TakeSeatAsync(gameId, Context.ConnectionId, seatIndex, Context.ConnectionAborted);
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
        var ok = await _runtime.ReconnectAsync(gameId, seatIndex, Context.ConnectionId, Context.ConnectionAborted);
        return new { success = ok };
    }

    // ── Phase J Wave 5 — Matchmaking + Profile RPCs ──────────────────

    /// <summary>
    /// Phase J Wave 5 — toggles a game's public-listing flag. Only the
    /// original host (matched by <c>state.CreatorPlayerId ==
    /// Context.ConnectionId</c>) may call this. <paramref name="publicName"/>
    /// is the friendly lobby label (trimmed, capped at 64 chars by the
    /// service). Throws <see cref="HubException"/> if the caller isn't the
    /// host or the game has already left the Seating phase.
    /// </summary>
    public async Task<object> SetGamePublic(string gameId, bool isPublic, string? publicName = null)
    {
        await _matchmaking.SetGamePublicAsync(gameId, Context.ConnectionId, isPublic, publicName, Context.ConnectionAborted);
        return new { success = true, isPublic, publicName };
    }

    /// <summary>
    /// Phase J Wave 5 — seats the caller into a randomly-picked public,
    /// lobby-phase game. Returns <c>{ matched: false }</c> when no candidate
    /// exists (Hicks's UI should fall back to "create a game"). On success
    /// returns the chosen gameId + seatIndex so the client knows where it
    /// landed; the runtime has already added the connection to the SignalR
    /// group (mirrors <c>TakeSeat</c>).
    /// </summary>
    public async Task<object> JoinRandom(string? variant = null)
    {
        var result = await _matchmaking.JoinRandomAsync(Context.ConnectionId, variant, Context.ConnectionAborted);
        if (result is null) return new { matched = false };
        return new { matched = true, gameId = result.Value.GameId, seatIndex = result.Value.SeatIndex };
    }

    /// <summary>
    /// Phase J Wave 5 — updates the caller's <see cref="PlayerProfile"/>
    /// (display name + optional avatar colour). Returns the resulting
    /// profile + stats DTO using the same shape as the
    /// <c>ProfileLoaded</c> on-connect event.
    /// </summary>
    public async Task<object> UpdateProfile(string displayName, string? avatarColor = null)
    {
        var profile = await _profiles.UpdateDisplayNameAsync(Context.ConnectionId, displayName, Context.ConnectionAborted);
        if (!string.IsNullOrEmpty(avatarColor))
        {
            profile = await _profiles.UpdateAvatarColorAsync(Context.ConnectionId, avatarColor, Context.ConnectionAborted);
        }
        var stats = await _profiles.GetStatsAsync(Context.ConnectionId, Context.ConnectionAborted);
        return BuildProfileDto(profile, stats);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();

        // Phase J Wave 5 — broadcast the caller's profile + career stats on
        // connect so the frontend renders the avatar/display-name chip
        // without an extra round-trip. Auto-creates the profile on first
        // connect via PlayerProfileService.GetOrCreateAsync.
        try
        {
            var profile = await _profiles.GetOrCreateAsync(Context.ConnectionId, Context.ConnectionAborted);
            var stats = await _profiles.GetStatsAsync(Context.ConnectionId, Context.ConnectionAborted);
            await Clients.Caller.SendAsync("ProfileLoaded", BuildProfileDto(profile, stats), Context.ConnectionAborted);
        }
        catch
        {
            // Non-fatal — a profile DB hiccup must not break the connection.
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await _runtime.HandleDisconnectAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
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
