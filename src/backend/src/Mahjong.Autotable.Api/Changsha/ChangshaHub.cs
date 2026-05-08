using Microsoft.AspNetCore.SignalR;

namespace Mahjong.Autotable.Api.Changsha;

/// <summary>
/// SignalR hub for Changsha Mahjong real-time communication.
/// See docs/rules/changsha-signalr-contract.md for the authoritative contract.
/// </summary>
public sealed class ChangshaHub : Hub
{
    // ── Client → Server commands ──────────────────────────────────

    public async Task CreateGame(string ruleSet, int[]? botSeatIndexes = null, int? seed = null)
    {
        var resolvedSeed = seed ?? Random.Shared.Next(int.MinValue, int.MaxValue);
        var (state, events) = ChangshaGameStateMachine.CreateGame(resolvedSeed, botSeatIndexes);

        var gameId = state.GameId;
        await Groups.AddToGroupAsync(Context.ConnectionId, gameId);

        await Clients.Group(gameId).SendAsync("GameCreated", new
        {
            gameId,
            ruleSet = "changsha-v1",
            seats = state.Seats.Select(s => new
            {
                s.SeatIndex,
                wind = s.Wind.ToString().ToLowerInvariant(),
                s.PlayerId,
                s.IsBot,
                s.IsDealer,
                tileCount = 0,
                melds = Array.Empty<object>(),
                discards = Array.Empty<int>()
            })
        });
    }

    public async Task JoinTable(string gameId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, gameId);
        await Clients.Caller.SendAsync("JoinedTable", new { gameId, success = true });
    }

    public async Task TakeSeat(string gameId, int seatIndex)
    {
        await Clients.Group(gameId).SendAsync("PlayerSeated", new
        {
            gameId,
            seatIndex,
            playerId = Context.ConnectionId,
            isBot = false
        });
    }

    public async Task StartGame(string gameId)
    {
        await Clients.Group(gameId).SendAsync("GameStarted", new
        {
            gameId,
            dealerSeatIndex = 0,
            roundWind = "east",
            handNumber = 1
        });
    }

    public async Task Discard(string gameId, int seatIndex, int tileId)
    {
        await Clients.Group(gameId).SendAsync("TileDiscarded", new
        {
            gameId,
            seatIndex,
            tileId,
            turnNumber = 0
        });
    }

    public async Task Claim(string gameId, int seatIndex, string type, int[]? tileIds = null)
    {
        await Clients.Group(gameId).SendAsync("ClaimMade", new
        {
            gameId,
            claimingSeatIndex = seatIndex,
            claimType = type,
            tileId = 0,
            meld = new { type, tileIds = tileIds ?? [] }
        });
    }

    public async Task DeclareKong(string gameId, int seatIndex, int[] tileIds)
    {
        await Clients.Group(gameId).SendAsync("ClaimMade", new
        {
            gameId,
            claimingSeatIndex = seatIndex,
            claimType = "kong",
            tileId = tileIds.Length > 0 ? tileIds[0] : 0,
            meld = new { type = "kong", tileIds }
        });
    }

    public async Task DeclareWin(string gameId, int seatIndex)
    {
        await Clients.Group(gameId).SendAsync("WinDeclared", new
        {
            gameId,
            winResult = new
            {
                winningSeatIndex = seatIndex,
                winType = "selfDraw",
                winPattern = "standard",
                winningTileId = 0,
                sourceSeatIndex = seatIndex
            }
        });
    }

    public async Task Pass(string gameId, int seatIndex)
    {
        await Clients.Caller.SendAsync("ClaimPassed", new { gameId, seatIndex });
    }

    // ── Lifecycle ─────────────────────────────────────────────────

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}
