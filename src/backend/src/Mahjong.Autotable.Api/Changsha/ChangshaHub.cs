using Mahjong.Autotable.Api.Changsha.Runtime;
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

    public ChangshaHub(IChangshaGameRuntime runtime)
    {
        _runtime = runtime;
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

    // ── Lifecycle ─────────────────────────────────────────────────────

    public override Task OnConnectedAsync() => base.OnConnectedAsync();

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await _runtime.HandleDisconnectAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
