using System.Net.Http.Json;

namespace MahjongClaw.Client.Services;

public sealed class GameApiClient
{
    private readonly HttpClient _http;

    public GameApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<CreateGameResponse> CreateGameAsync(CreateGameRequest? request = null, CancellationToken cancellationToken = default)
    {
        using var response = request is null
            ? await _http.PostAsync("/api/game", content: null, cancellationToken)
            : await _http.PostAsJsonAsync("/api/game", request, cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CreateGameResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Game create response was empty.");
    }

    public async Task<JoinGameResponse> JoinGameAsync(Guid gameId, JoinGameRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync($"/api/game/{gameId}/join", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JoinGameResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Join game response was empty.");
    }

    public async Task<StartGameResponse> StartGameAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsync($"/api/game/{gameId}/start", content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StartGameResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Start game response was empty.");
    }

    public async Task<GameStateResponse> GetGameStateAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        var response = await _http.GetFromJsonAsync<GameStateResponse>($"/api/game/{gameId}/state", cancellationToken);
        return response ?? throw new InvalidOperationException("Game state response was empty.");
    }

    public async Task<DrawTileResponse> DrawTileAsync(Guid gameId, DrawTileRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync($"/api/game/{gameId}/draw", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DrawTileResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Draw tile response was empty.");
    }

    public async Task<DiscardTileResponse> DiscardTileAsync(Guid gameId, DiscardTileRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync($"/api/game/{gameId}/discard", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DiscardTileResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Discard response was empty.");
    }

    public async Task<WinResponse> DeclareWinAsync(Guid gameId, WinRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync($"/api/game/{gameId}/win", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WinResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Win response was empty.");
    }

    public async Task<BotTurnResponse> TriggerBotTurnAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsync($"/api/game/{gameId}/bot/turn", content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BotTurnResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Bot turn response was empty.");
    }

    public record CreateGameRequest(string? HostName, string? Mode, int? BotSeatCount);
    public record CreateGameResponse(Guid Id, string Status, int Round, DateTimeOffset CreatedAt, Guid? HostPlayerId);
    public record JoinGameRequest(string Name, string Seat);
    public record JoinGameResponse(Guid PlayerId, Guid GameId, string Name, string Seat, int Score);
    public record StartGameResponse(Guid GameId, string Status, Guid? DealerId, Guid? CurrentPlayerId);
    public record PlayerSummary(Guid Id, string Name, string Seat, int Score);
    public record GameStateResponse(
        Guid Id,
        string Status,
        Guid? CurrentPlayerId,
        Guid? DealerId,
        int Round,
        DateTimeOffset CreatedAt,
        IReadOnlyList<PlayerSummary> Players,
        int TilesInPlay,
        int MeldCount,
        int DiscardCount);

    public record DrawTileRequest(Guid PlayerId);
    public record DrawTileResponse(Guid TileId, string Suit, int Rank, string? Wind, string? Dragon);
    public record DiscardTileRequest(Guid PlayerId, Guid TileId);
    public record DiscardTileResponse(Guid DiscardId, Guid TileId, int Sequence);
    public record MeldResponse(Guid MeldId, string Type, IReadOnlyList<Guid> TileIds);
    public record WinRequest(Guid PlayerId);
    public record WinResponse(Guid GameId, string Status, Guid PlayerId);
    public record BotTurnResponse(Guid? DrawnTileId, Guid? DiscardId, Guid? DiscardTileId, Guid? NextPlayerId, bool Won);
}
