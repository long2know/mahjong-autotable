using Microsoft.AspNetCore.SignalR;

namespace MahjongClaw.Api.Hubs;

public sealed class GameHub : Hub
{
    public Task JoinGame(string gameId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, gameId);

    public Task LeaveGame(string gameId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, gameId);
}
