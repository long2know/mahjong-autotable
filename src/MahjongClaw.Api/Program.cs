using MahjongClaw.Api.Hubs;
using MahjongClaw.Domain.Rules;
using MahjongClaw.Domain.Tiles;
using MahjongClaw.Persistence;
using MahjongClaw.Persistence.Entities;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<MahjongClawDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("MahjongClaw")));

builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();
app.UseCors();

app.MapGet("/", () => "Mahjong Claw API");
app.MapHub<GameHub>("/hubs/game");

var gameGroup = app.MapGroup("/api/game");

gameGroup.MapPost("/", async (
    MahjongClawDbContext dbContext,
    IHubContext<GameHub> hubContext,
    CreateGameRequest? request,
    CancellationToken cancellationToken) =>
{
    var game = new GameEntity
    {
        Id = Guid.NewGuid(),
        Status = "Waiting",
        Round = 1
    };

    dbContext.Games.Add(game);

    Guid? hostPlayerId = null;
    var hostName = request?.HostName?.Trim();
    var createMode = request?.Mode?.Trim();
    var botSeatCount = Math.Clamp(request?.BotSeatCount ?? 0, 0, 3);
    var createdPlayers = new List<PlayerEntity>();

    if (!string.IsNullOrWhiteSpace(hostName))
    {
        var seatPlan = new[] { "East", "South", "West", "North" };
        var hostPlayer = new PlayerEntity
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            Name = hostName,
            Seat = seatPlan[0],
            Score = 0
        };

        dbContext.Players.Add(hostPlayer);
        createdPlayers.Add(hostPlayer);
        hostPlayerId = hostPlayer.Id;

        if (string.Equals(createMode, "HumanVsBot", StringComparison.OrdinalIgnoreCase))
        {
            for (var index = 1; index <= botSeatCount && index < seatPlan.Length; index++)
            {
                var botPlayer = new PlayerEntity
                {
                    Id = Guid.NewGuid(),
                    GameId = game.Id,
                    Name = $"Bot {index}",
                    Seat = seatPlan[index],
                    Score = 0
                };

                dbContext.Players.Add(botPlayer);
                createdPlayers.Add(botPlayer);
            }
        }
    }

    await dbContext.SaveChangesAsync(cancellationToken);

    foreach (var createdPlayer in createdPlayers)
    {
        var joinResponse = new JoinGameResponse(createdPlayer.Id, game.Id, createdPlayer.Name, createdPlayer.Seat, createdPlayer.Score);
        await SendToGameWithAliases(hubContext, game.Id, "PlayerJoined", joinResponse, cancellationToken);
    }

    var response = new CreateGameResponse(game.Id, game.Status, game.Round, game.CreatedAt, hostPlayerId);
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
    IHubContext<GameHub> hubContext,
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
    await SendToGameWithAliases(hubContext, game.Id, "PlayerJoined", response, cancellationToken);
    return Results.Ok(response);
});

gameGroup.MapPost("/{id:guid}/start", async (
    Guid id,
    MahjongClawDbContext dbContext,
    IHubContext<GameHub> hubContext,
    CancellationToken cancellationToken) =>
{
    var game = await dbContext.Games
        .Include(g => g.Players)
        .Include(g => g.Tiles)
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

    if (game.Tiles.Count == 0)
    {
        var rng = new Random();
        var tiles = TileFactory.CreateStandardSet().OrderBy(_ => rng.Next()).ToList();
        var tileEntities = tiles.Select((tile, index) => new TileEntity
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            Suit = tile.Suit.ToString(),
            Rank = tile.Rank,
            Wind = tile.Wind?.ToString(),
            Dragon = tile.Dragon?.ToString(),
            Location = "Wall",
            Sequence = index
        }).ToList();

        dbContext.Tiles.AddRange(tileEntities);

        DealStartingHands(game, tileEntities);
    }

    await dbContext.SaveChangesAsync(cancellationToken);

    var response = new StartGameResponse(game.Id, game.Status, game.DealerId, game.CurrentPlayerId);
    await SendToGameWithAliases(hubContext, game.Id, "GameStarted", response, cancellationToken);
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

gameGroup.MapPost("/{id:guid}/draw", async (
    Guid id,
    DrawTileRequest request,
    MahjongClawDbContext dbContext,
    IHubContext<GameHub> hubContext,
    CancellationToken cancellationToken) =>
{
    var game = await dbContext.Games
        .Include(g => g.Tiles)
        .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

    if (game is null)
    {
        return Results.NotFound();
    }

    if (!string.Equals(game.Status, "Active", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Conflict(new { message = "Game is not active." });
    }

    if (game.CurrentPlayerId != request.PlayerId)
    {
        return Results.Conflict(new { message = "It is not this player's turn." });
    }

    var tile = game.Tiles
        .Where(t => t.Location == "Wall")
        .OrderBy(t => t.Sequence)
        .FirstOrDefault();

    if (tile is null)
    {
        return Results.Conflict(new { message = "No tiles left in wall." });
    }

    tile.Location = "Hand";
    tile.PlayerId = request.PlayerId;

    await dbContext.SaveChangesAsync(cancellationToken);

    var response = new DrawTileResponse(tile.Id, tile.Suit, tile.Rank, tile.Wind, tile.Dragon);
    await SendToGameWithAliases(hubContext, game.Id, "TileDrawn", response, cancellationToken);
    return Results.Ok(response);
});

gameGroup.MapPost("/{id:guid}/discard", async (
    Guid id,
    DiscardTileRequest request,
    MahjongClawDbContext dbContext,
    IHubContext<GameHub> hubContext,
    CancellationToken cancellationToken) =>
{
    var game = await dbContext.Games
        .Include(g => g.Players)
        .Include(g => g.Tiles)
        .Include(g => g.Discards)
        .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

    if (game is null)
    {
        return Results.NotFound();
    }

    if (game.CurrentPlayerId != request.PlayerId)
    {
        return Results.Conflict(new { message = "It is not this player's turn." });
    }

    var tile = game.Tiles.FirstOrDefault(t => t.Id == request.TileId && t.PlayerId == request.PlayerId);

    if (tile is null)
    {
        return Results.NotFound(new { message = "Tile not found in player's hand." });
    }

    tile.Location = "Discard";

    var sequence = game.Discards.Count == 0
        ? 1
        : game.Discards.Max(d => d.Sequence) + 1;

    var discard = new DiscardEntity
    {
        GameId = game.Id,
        PlayerId = request.PlayerId,
        TileId = tile.Id,
        Sequence = sequence
    };

    dbContext.Discards.Add(discard);

    var currentPlayer = game.Players.FirstOrDefault(player => player.Id == request.PlayerId);
    if (currentPlayer is not null)
    {
        var nextPlayerId = GetNextPlayerId(game, currentPlayer);
        if (nextPlayerId is not null)
        {
            game.CurrentPlayerId = nextPlayerId;
        }
    }

    await dbContext.SaveChangesAsync(cancellationToken);

    var response = new DiscardTileResponse(discard.Id, tile.Id, discard.Sequence);
    await SendToGameWithAliases(hubContext, game.Id, "TileDiscarded", response, cancellationToken);
    return Results.Ok(response);
});

gameGroup.MapPost("/{id:guid}/bot/turn", async (
    Guid id,
    MahjongClawDbContext dbContext,
    IHubContext<GameHub> hubContext,
    CancellationToken cancellationToken) =>
{
    var game = await dbContext.Games
        .Include(g => g.Players)
        .Include(g => g.Tiles)
        .Include(g => g.Discards)
        .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

    if (game is null)
    {
        return Results.NotFound();
    }

    if (!string.Equals(game.Status, "Active", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Conflict(new { message = "Game is not active." });
    }

    if (game.CurrentPlayerId is null)
    {
        return Results.Conflict(new { message = "No current player set." });
    }

    var botPlayer = game.Players.FirstOrDefault(player => player.Id == game.CurrentPlayerId);
    if (botPlayer is null)
    {
        return Results.NotFound(new { message = "Current player not found." });
    }

    if (!IsBotPlayer(botPlayer))
    {
        return Results.Conflict(new { message = "Current player is not a bot." });
    }

    var handTiles = game.Tiles
        .Where(tile => tile.PlayerId == botPlayer.Id && tile.Location == "Hand")
        .ToList();

    DrawTileResponse? drawResponse = null;
    if (handTiles.Count == 13)
    {
        var tile = game.Tiles
            .Where(t => t.Location == "Wall")
            .OrderBy(t => t.Sequence)
            .FirstOrDefault();

        if (tile is null)
        {
            return Results.Conflict(new { message = "No tiles left in wall." });
        }

        tile.Location = "Hand";
        tile.PlayerId = botPlayer.Id;
        drawResponse = new DrawTileResponse(tile.Id, tile.Suit, tile.Rank, tile.Wind, tile.Dragon);
        handTiles.Add(tile);
    }
    else if (handTiles.Count != 14)
    {
        return Results.Conflict(new { message = "Bot hand is in an invalid state." });
    }

    if (!TryBuildTileHand(handTiles, out var botHand))
    {
        return Results.Conflict(new { message = "Bot hand contains invalid tile data." });
    }

    if (HandAnalyzer.TryGetWinningHand(botHand, out _))
    {
        game.Status = "Completed";
        game.CurrentPlayerId = botPlayer.Id;

        await dbContext.SaveChangesAsync(cancellationToken);

        if (drawResponse is not null)
        {
            await SendToGameWithAliases(hubContext, game.Id, "TileDrawn", drawResponse, cancellationToken);
        }

        var winResponse = new WinResponse(game.Id, game.Status, botPlayer.Id);
        await SendToGameWithAliases(hubContext, game.Id, "GameWon", winResponse, cancellationToken);

        return Results.Ok(new BotTurnResponse(drawResponse?.TileId, null, null, game.CurrentPlayerId, true));
    }

    var discardTile = SelectBotDiscard(handTiles);
    discardTile.Location = "Discard";

    var sequence = game.Discards.Count == 0
        ? 1
        : game.Discards.Max(d => d.Sequence) + 1;

    var discard = new DiscardEntity
    {
        GameId = game.Id,
        PlayerId = botPlayer.Id,
        TileId = discardTile.Id,
        Sequence = sequence
    };

    dbContext.Discards.Add(discard);

    var nextPlayerId = GetNextPlayerId(game, botPlayer);
    if (nextPlayerId is not null)
    {
        game.CurrentPlayerId = nextPlayerId;
    }

    await dbContext.SaveChangesAsync(cancellationToken);

    if (drawResponse is not null)
    {
        await SendToGameWithAliases(hubContext, game.Id, "TileDrawn", drawResponse, cancellationToken);
    }

    var discardResponse = new DiscardTileResponse(discard.Id, discardTile.Id, discard.Sequence);
    await SendToGameWithAliases(hubContext, game.Id, "TileDiscarded", discardResponse, cancellationToken);

    return Results.Ok(new BotTurnResponse(drawResponse?.TileId, discard.Id, discardTile.Id, game.CurrentPlayerId));
});

gameGroup.MapPost("/{id:guid}/meld", async (
    Guid id,
    MeldRequest request,
    MahjongClawDbContext dbContext,
    IHubContext<GameHub> hubContext,
    CancellationToken cancellationToken) =>
{
    var game = await dbContext.Games
        .Include(g => g.Tiles)
        .Include(g => g.Melds)
        .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

    if (game is null)
    {
        return Results.NotFound();
    }

    var tiles = game.Tiles
        .Where(t => request.TileIds.Contains(t.Id) && t.PlayerId == request.PlayerId)
        .ToList();

    if (tiles.Count != request.TileIds.Count)
    {
        return Results.NotFound(new { message = "One or more tiles are missing from the player's hand." });
    }

    var meld = new MeldEntity
    {
        GameId = game.Id,
        PlayerId = request.PlayerId,
        Type = request.Type
    };

    dbContext.Melds.Add(meld);

    foreach (var tile in tiles)
    {
        tile.Location = "Meld";
        tile.Meld = meld;
    }

    await dbContext.SaveChangesAsync(cancellationToken);

    var response = new MeldResponse(meld.Id, request.Type, tiles.Select(t => t.Id).ToList());
    await SendToGameWithAliases(hubContext, game.Id, "MeldDeclared", response, cancellationToken);
    return Results.Ok(response);
});

gameGroup.MapPost("/{id:guid}/win", async (
    Guid id,
    WinRequest request,
    MahjongClawDbContext dbContext,
    IHubContext<GameHub> hubContext,
    CancellationToken cancellationToken) =>
{
    var game = await dbContext.Games.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

    if (game is null)
    {
        return Results.NotFound();
    }

    game.Status = "Completed";
    game.CurrentPlayerId = request.PlayerId;

    await dbContext.SaveChangesAsync(cancellationToken);

    var response = new WinResponse(game.Id, game.Status, request.PlayerId);
    await SendToGameWithAliases(hubContext, game.Id, "GameWon", response, cancellationToken);
    return Results.Ok(response);
});

app.Run();

static async Task SendToGameWithAliases(IHubContext<GameHub> hubContext, Guid gameId, string eventName, object payload, CancellationToken cancellationToken)
{
    await SendToGame(hubContext, gameId, eventName, payload, cancellationToken);

    foreach (var alias in GetEventAliases(eventName))
    {
        await SendToGame(hubContext, gameId, alias, payload, cancellationToken);
    }
}

static Task SendToGame(IHubContext<GameHub> hubContext, Guid gameId, string eventName, object payload, CancellationToken cancellationToken)
    => hubContext.Clients.Group(gameId.ToString()).SendAsync(eventName, payload, cancellationToken);

static IEnumerable<string> GetEventAliases(string eventName)
    => eventName switch
    {
        "PlayerJoined" => ["PlayerJoin"],
        "TileDrawn" => ["DrawnTile"],
        "TileDiscarded" => ["DiscardedTile"],
        "MeldDeclared" => ["Meld"],
        "GameWon" => ["WinDeclared"],
        _ => []
    };

static bool TryBuildTileHand(IEnumerable<TileEntity> entities, out List<Tile> tiles)
{
    tiles = new List<Tile>();

    foreach (var entity in entities)
    {
        if (!Enum.TryParse<TileSuit>(entity.Suit, true, out var suit))
        {
            tiles.Clear();
            return false;
        }

        Wind? wind = null;
        Dragon? dragon = null;

        if (!string.IsNullOrWhiteSpace(entity.Wind))
        {
            if (!Enum.TryParse<Wind>(entity.Wind, true, out var parsedWind))
            {
                tiles.Clear();
                return false;
            }

            wind = parsedWind;
        }

        if (!string.IsNullOrWhiteSpace(entity.Dragon))
        {
            if (!Enum.TryParse<Dragon>(entity.Dragon, true, out var parsedDragon))
            {
                tiles.Clear();
                return false;
            }

            dragon = parsedDragon;
        }

        try
        {
            tiles.Add(new Tile(suit, entity.Rank, wind, dragon));
        }
        catch (Exception)
        {
            tiles.Clear();
            return false;
        }
    }

    return true;
}

static TileEntity SelectBotDiscard(IReadOnlyList<TileEntity> handTiles)
    => handTiles
        .OrderBy(tile => GetBotTileKeepScore(tile, handTiles))
        .ThenBy(tile => IsHonorSuit(tile.Suit) ? 0 : 1)
        .ThenBy(tile => IsTerminalSuitedTile(tile) ? 0 : 1)
        .ThenBy(tile => tile.Suit)
        .ThenBy(tile => tile.Rank)
        .First();

static int GetBotTileKeepScore(TileEntity tile, IReadOnlyList<TileEntity> handTiles)
{
    var score = 0;
    var suitTiles = handTiles
        .Where(other => string.Equals(other.Suit, tile.Suit, StringComparison.OrdinalIgnoreCase))
        .ToList();
    var suitCount = suitTiles.Count;
    var suitClusterBonus = !IsHonorSuit(tile.Suit)
        ? Math.Max(0, (suitCount - 1) / 2)
        : 0;
    var suitSingletonPenalty = !IsHonorSuit(tile.Suit)
        ? suitCount switch
        {
            1 => -2,
            2 => -1,
            _ => 0
        }
        : 0;
    var suitedCounts = handTiles
        .Where(other => !IsHonorSuit(other.Suit))
        .GroupBy(other => other.Suit, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
    var suitFocusPenalty = 0;
    if (!IsHonorSuit(tile.Suit) && suitedCounts.Count > 1)
    {
        var minSuitCount = suitedCounts.Values.Min();
        if (suitCount == minSuitCount)
        {
            suitFocusPenalty = minSuitCount switch
            {
                1 => -2,
                2 => -1,
                _ => 0
            };
        }
    }
    var hasHonorMatch = false;
    var honorMatchCount = 0;

    foreach (var other in suitTiles)
    {
        if (ReferenceEquals(tile, other))
        {
            continue;
        }

        if (IsHonorSuit(tile.Suit))
        {
            if (string.Equals(tile.Wind, other.Wind, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(tile.Dragon, other.Dragon, StringComparison.OrdinalIgnoreCase))
            {
                score += 3;
                hasHonorMatch = true;
                honorMatchCount++;
            }

            continue;
        }

        if (other.Rank == tile.Rank)
        {
            score += 2;
            continue;
        }

        var diff = Math.Abs(other.Rank - tile.Rank);
        if (diff == 1)
        {
            score += 2;
        }
        else if (diff == 2)
        {
            score += 1;
        }
    }

    if (IsHonorSuit(tile.Suit))
    {
        if (!hasHonorMatch)
        {
            score -= suitCount == 1 ? 3 : 2;
        }
        else
        {
            var matchSetSize = honorMatchCount + 1;
            score += matchSetSize switch
            {
                >= 4 => 3,
                3 => 2,
                2 => 1,
                _ => 0
            };
        }

        return score + suitClusterBonus + suitFocusPenalty;
    }

    var rankCounts = suitTiles
        .GroupBy(other => other.Rank)
        .ToDictionary(group => group.Key, group => group.Count());

    rankCounts.TryGetValue(tile.Rank, out var sameRankCount);
    if (sameRankCount >= 2)
    {
        score += 2;
    }

    if (sameRankCount >= 3)
    {
        score += 2;
    }

    var hasMinus1 = rankCounts.ContainsKey(tile.Rank - 1);
    var hasPlus1 = rankCounts.ContainsKey(tile.Rank + 1);
    var hasMinus2 = rankCounts.ContainsKey(tile.Rank - 2);
    var hasPlus2 = rankCounts.ContainsKey(tile.Rank + 2);
    var hasNeighbor = hasMinus1 || hasPlus1 || hasMinus2 || hasPlus2;

    if (hasMinus1 && hasPlus1)
    {
        score += 2;
    }

    if (hasMinus2 && hasMinus1)
    {
        score += 1;
    }

    if (hasPlus1 && hasPlus2)
    {
        score += 1;
    }

    if (sameRankCount <= 1 && !hasNeighbor)
    {
        score -= 1;
    }

    if ((tile.Rank == 1 || tile.Rank == 9) && sameRankCount <= 1 && !hasMinus1 && !hasPlus1)
    {
        score -= 1;
    }

    return score + suitClusterBonus + suitSingletonPenalty + suitFocusPenalty;
}

static bool IsHonorSuit(string suit)
    => string.Equals(suit, "Winds", StringComparison.OrdinalIgnoreCase)
        || string.Equals(suit, "Dragons", StringComparison.OrdinalIgnoreCase);

static bool IsTerminalSuitedTile(TileEntity tile)
    => !IsHonorSuit(tile.Suit) && tile.Rank is 1 or 9;

static bool IsBotPlayer(PlayerEntity player)
    => player.Name.StartsWith("Bot", StringComparison.OrdinalIgnoreCase);

static Guid? GetNextPlayerId(GameEntity game, PlayerEntity currentPlayer)
{
    var seatOrder = new[] { "East", "South", "West", "North" };
    var currentIndex = Array.FindIndex(seatOrder, seat =>
        string.Equals(seat, currentPlayer.Seat, StringComparison.OrdinalIgnoreCase));

    if (currentIndex < 0)
    {
        return null;
    }

    for (var offset = 1; offset <= seatOrder.Length; offset++)
    {
        var nextSeat = seatOrder[(currentIndex + offset) % seatOrder.Length];
        var nextPlayer = game.Players.FirstOrDefault(player =>
            string.Equals(player.Seat, nextSeat, StringComparison.OrdinalIgnoreCase));

        if (nextPlayer is not null)
        {
            return nextPlayer.Id;
        }
    }

    return null;
}

void DealStartingHands(GameEntity game, IReadOnlyList<TileEntity> tiles)
{
    var seatOrder = new[] { "East", "South", "West", "North" };
    var players = seatOrder
        .Select(seat => game.Players.FirstOrDefault(p =>
            string.Equals(p.Seat, seat, StringComparison.OrdinalIgnoreCase)))
        .Where(p => p is not null)
        .ToList();

    if (players.Count != 4)
    {
        return;
    }

    var wallTiles = tiles.Where(t => t.Location == "Wall").OrderBy(t => t.Sequence).ToList();
    var drawIndex = 0;

    for (var round = 0; round < 3; round++)
    {
        foreach (var player in players)
        {
            DealTiles(player!, wallTiles, ref drawIndex, 4);
        }
    }

    foreach (var player in players)
    {
        DealTiles(player!, wallTiles, ref drawIndex, 1);
    }

    var east = players.First(p => string.Equals(p!.Seat, "East", StringComparison.OrdinalIgnoreCase));
    DealTiles(east!, wallTiles, ref drawIndex, 1);
}

void DealTiles(PlayerEntity player, IReadOnlyList<TileEntity> wallTiles, ref int drawIndex, int count)
{
    for (var i = 0; i < count && drawIndex < wallTiles.Count; i++)
    {
        var tile = wallTiles[drawIndex++];
        tile.Location = "Hand";
        tile.PlayerId = player.Id;
    }
}

internal record CreateGameRequest(string? HostName, string? Mode, int? BotSeatCount);
internal record CreateGameResponse(Guid Id, string Status, int Round, DateTimeOffset CreatedAt, Guid? HostPlayerId);
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

internal record DrawTileRequest(Guid PlayerId);
internal record DrawTileResponse(Guid TileId, string Suit, int Rank, string? Wind, string? Dragon);
internal record DiscardTileRequest(Guid PlayerId, Guid TileId);
internal record DiscardTileResponse(Guid DiscardId, Guid TileId, int Sequence);
internal record BotTurnResponse(Guid? DrawnTileId, Guid? DiscardId, Guid? DiscardTileId, Guid? NextPlayerId, bool Won = false);
internal record MeldRequest(Guid PlayerId, string Type, IReadOnlyList<Guid> TileIds);
internal record MeldResponse(Guid MeldId, string Type, IReadOnlyList<Guid> TileIds);
internal record WinRequest(Guid PlayerId);
internal record WinResponse(Guid GameId, string Status, Guid PlayerId);
