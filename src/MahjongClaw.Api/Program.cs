using MahjongClaw.Persistence;
using MahjongClaw.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<MahjongClawDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("MahjongClaw")));

var app = builder.Build();

app.MapGet("/", () => "Mahjong Claw API");

var gameGroup = app.MapGroup("/api/game");

gameGroup.MapPost("/", async (MahjongClawDbContext dbContext, CancellationToken cancellationToken) =>
{
    var game = new GameEntity
    {
        Status = "Waiting",
        Round = 1
    };

    dbContext.Games.Add(game);
    await dbContext.SaveChangesAsync(cancellationToken);

    var response = new CreateGameResponse(game.Id, game.Status, game.Round, game.CreatedAt);
    return Results.Created($"/api/game/{game.Id}", response);
});

var validSeats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "East",
    "South",
    "West",
    "North"
};

gameGroup.MapPost("/{id:guid}/join", async (
    Guid id,
    JoinGameRequest request,
    MahjongClawDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var game = await dbContext.Games
        .Include(g => g.Players)
        .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

    if (game is null)
    {
        return Results.NotFound();
    }

    if (!string.Equals(game.Status, "Waiting", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Conflict(new { message = "Game is not open for joining." });
    }

    if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Seat))
    {
        return Results.BadRequest(new { message = "Player name and seat are required." });
    }

    if (!validSeats.Contains(request.Seat.Trim()))
    {
        return Results.BadRequest(new { message = "Seat must be East, South, West, or North." });
    }

    if (game.Players.Any(player => string.Equals(player.Seat, request.Seat, StringComparison.OrdinalIgnoreCase)))
    {
        return Results.Conflict(new { message = "Seat is already taken." });
    }

    var player = new PlayerEntity
    {
        GameId = game.Id,
        Name = request.Name.Trim(),
        Seat = request.Seat.Trim(),
        Score = 0
    };

    dbContext.Players.Add(player);
    await dbContext.SaveChangesAsync(cancellationToken);

    var response = new JoinGameResponse(player.Id, game.Id, player.Name, player.Seat, player.Score);
    return Results.Ok(response);
});

gameGroup.MapPost("/{id:guid}/start", async (
    Guid id,
    MahjongClawDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var game = await dbContext.Games
        .Include(g => g.Players)
        .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

    if (game is null)
    {
        return Results.NotFound();
    }

    if (!string.Equals(game.Status, "Waiting", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Conflict(new { message = "Game has already started." });
    }

    if (game.Players.Count < 4)
    {
        return Results.Conflict(new { message = "Game requires 4 players to start." });
    }

    var dealer = game.Players.FirstOrDefault(player =>
        string.Equals(player.Seat, "East", StringComparison.OrdinalIgnoreCase)) ?? game.Players.First();

    game.DealerId = dealer.Id;
    game.CurrentPlayerId = dealer.Id;
    game.Status = "Active";

    await dbContext.SaveChangesAsync(cancellationToken);

    var response = new StartGameResponse(game.Id, game.Status, game.DealerId, game.CurrentPlayerId);
    return Results.Ok(response);
});

gameGroup.MapGet("/{id:guid}/state", async (
    Guid id,
    MahjongClawDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var game = await dbContext.Games
        .AsNoTracking()
        .Include(g => g.Players)
        .Include(g => g.Tiles)
        .Include(g => g.Melds)
        .Include(g => g.Discards)
        .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

    if (game is null)
    {
        return Results.NotFound();
    }

    var response = new GameStateResponse(
        game.Id,
        game.Status,
        game.CurrentPlayerId,
        game.DealerId,
        game.Round,
        game.CreatedAt,
        game.Players.Select(player => new PlayerSummary(player.Id, player.Name, player.Seat, player.Score)).ToList(),
        game.Tiles.Count,
        game.Melds.Count,
        game.Discards.Count);

    return Results.Ok(response);
});

gameGroup.MapPost("/{id:guid}/draw", (Guid id) =>
    Results.StatusCode(StatusCodes.Status501NotImplemented));

gameGroup.MapPost("/{id:guid}/discard", (Guid id) =>
    Results.StatusCode(StatusCodes.Status501NotImplemented));

gameGroup.MapPost("/{id:guid}/meld", (Guid id) =>
    Results.StatusCode(StatusCodes.Status501NotImplemented));

gameGroup.MapPost("/{id:guid}/win", (Guid id) =>
    Results.StatusCode(StatusCodes.Status501NotImplemented));

app.Run();

internal record CreateGameResponse(Guid Id, string Status, int Round, DateTimeOffset CreatedAt);
internal record JoinGameRequest(string Name, string Seat);
internal record JoinGameResponse(Guid PlayerId, Guid GameId, string Name, string Seat, int Score);
internal record StartGameResponse(Guid GameId, string Status, Guid? DealerId, Guid? CurrentPlayerId);
internal record PlayerSummary(Guid Id, string Name, string Seat, int Score);
internal record GameStateResponse(
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
