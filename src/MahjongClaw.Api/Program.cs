using MahjongClaw.Api.Hubs;
using MahjongClaw.Domain.Gameplay;
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
    var ruleset = RulesetResolver.Resolve(request?.Ruleset);
    var game = new GameEntity
    {
        Id = Guid.NewGuid(),
        Status = "Waiting",
        Round = 1,
        Ruleset = ruleset.ToString()
    };

    dbContext.Games.Add(game);

    Guid? hostPlayerId = null;
    var hostName = request?.HostName?.Trim();
    var createMode = request?.Mode?.Trim();
    var requestedBotSeatCount = request?.BotSeatCount;
    var botSeatCount = string.Equals(createMode, "HumanVsBot", StringComparison.OrdinalIgnoreCase)
        ? Math.Clamp(requestedBotSeatCount ?? 3, 0, 3)
        : 0;
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

    var response = new CreateGameResponse(game.Id, game.Status, game.Round, game.CreatedAt, hostPlayerId, ruleset.ToString());
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
        var ruleset = RulesetResolver.Resolve(game.Ruleset);
        var wall = WallBuilder.BuildShuffledWall(rng, ruleset);
        var tileEntities = wall.Tiles.Select((tile, index) => new TileEntity
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
    await ExecuteBotTurnChainAsync(game, dbContext, hubContext, cancellationToken);
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

    var tilesInPlay = game.Tiles.Count(tile => !string.Equals(tile.Location, "Wall", StringComparison.OrdinalIgnoreCase));
    var ruleset = RulesetResolver.Resolve(game.Ruleset);
    var wallTotal = WallBuilder.GetWallTotal(ruleset);

    var response = new GameStateResponse(
        game.Id,
        game.Status,
        game.CurrentPlayerId,
        game.DealerId,
        game.Round,
        ruleset.ToString(),
        wallTotal,
        game.CreatedAt,
        game.Players.Select(player => new PlayerSummary(player.Id, player.Name, player.Seat, player.Score)).ToList(),
        tilesInPlay,
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

    var handTileCount = game.Tiles.Count(tile =>
        tile.PlayerId == request.PlayerId &&
        string.Equals(tile.Location, "Hand", StringComparison.OrdinalIgnoreCase));
    var meldTileCount = game.Tiles.Count(tile =>
        tile.PlayerId == request.PlayerId &&
        string.Equals(tile.Location, "Meld", StringComparison.OrdinalIgnoreCase));
    var declaredKongCount = CountDeclaredKongs(game.Tiles, request.PlayerId);

    if (!TurnFlowValidator.CanDraw(handTileCount, meldTileCount, declaredKongCount))
    {
        return Results.Conflict(new { message = "Player must hold 13 owned tiles (plus one per declared kong) before drawing." });
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

    if (!string.Equals(game.Status, "Active", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Conflict(new { message = "Game is not active." });
    }

    if (game.CurrentPlayerId != request.PlayerId)
    {
        return Results.Conflict(new { message = "It is not this player's turn." });
    }

    var handTileCount = game.Tiles.Count(tile =>
        tile.PlayerId == request.PlayerId &&
        string.Equals(tile.Location, "Hand", StringComparison.OrdinalIgnoreCase));
    var meldTileCount = game.Tiles.Count(tile =>
        tile.PlayerId == request.PlayerId &&
        string.Equals(tile.Location, "Meld", StringComparison.OrdinalIgnoreCase));
    var declaredKongCount = CountDeclaredKongs(game.Tiles, request.PlayerId);

    if (!TurnFlowValidator.CanDiscard(handTileCount, meldTileCount, declaredKongCount))
    {
        return Results.Conflict(new { message = "Player must hold 14 owned tiles (plus one per declared kong) before discarding." });
    }

    var tile = game.Tiles.FirstOrDefault(t =>
        t.Id == request.TileId &&
        t.PlayerId == request.PlayerId &&
        string.Equals(t.Location, "Hand", StringComparison.OrdinalIgnoreCase));

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
    game.Discards.Add(discard);

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
    await ExecuteBotTurnChainAsync(game, dbContext, hubContext, cancellationToken);
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

    var botTurnResult = await ExecuteBotTurnAsync(game, dbContext, hubContext, cancellationToken);
    if (!botTurnResult.Success)
    {
        return botTurnResult.StatusCode switch
        {
            404 => Results.NotFound(new { message = botTurnResult.ErrorMessage }),
            409 => Results.Conflict(new { message = botTurnResult.ErrorMessage }),
            _ => Results.BadRequest(new { message = botTurnResult.ErrorMessage })
        };
    }

    return Results.Ok(botTurnResult.Response);
});

gameGroup.MapPost("/{id:guid}/meld", async (
    Guid id,
    MeldRequest request,
    MahjongClawDbContext dbContext,
    IHubContext<GameHub> hubContext,
    CancellationToken cancellationToken) =>
{
    var game = await dbContext.Games
        .Include(g => g.Players)
        .Include(g => g.Tiles)
        .Include(g => g.Melds)
        .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

    if (game is null)
    {
        return Results.NotFound();
    }

    if (!string.Equals(game.Status, "Active", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Conflict(new { message = "Game is not active." });
    }

    if (!game.Players.Any(player => player.Id == request.PlayerId))
    {
        return Results.NotFound(new { message = "Player not found in game." });
    }

    if (game.CurrentPlayerId != request.PlayerId)
    {
        return Results.Conflict(new { message = "It is not this player's turn." });
    }

    if (!Enum.TryParse<MeldType>(request.Type, true, out var meldType))
    {
        return Results.BadRequest(new { message = "Invalid meld type." });
    }

    var handTileCount = game.Tiles.Count(tile =>
        tile.PlayerId == request.PlayerId &&
        string.Equals(tile.Location, "Hand", StringComparison.OrdinalIgnoreCase));
    var meldTileCount = game.Tiles.Count(tile =>
        tile.PlayerId == request.PlayerId &&
        string.Equals(tile.Location, "Meld", StringComparison.OrdinalIgnoreCase));

    var meldTileRequirement = meldType == MeldType.Kong ? 4 : 3;
    var declaredKongCount = CountDeclaredKongs(game.Tiles, request.PlayerId);

    if (!TurnFlowValidator.CanDeclareMeld(handTileCount, meldTileCount, meldTileRequirement, declaredKongCount))
    {
        return Results.Conflict(new { message = "Player must hold 14 owned tiles (plus one per declared kong) with enough hand tiles before declaring a meld." });
    }

    var tiles = game.Tiles
        .Where(t =>
            request.TileIds.Contains(t.Id) &&
            t.PlayerId == request.PlayerId &&
            string.Equals(t.Location, "Hand", StringComparison.OrdinalIgnoreCase))
        .ToList();

    if (tiles.Count != request.TileIds.Count)
    {
        return Results.NotFound(new { message = "One or more tiles are missing from the player's hand." });
    }

    if (!TryBuildTileHand(tiles, out var tileHand))
    {
        return Results.Conflict(new { message = "Meld tiles are invalid." });
    }

    var ruleset = RulesetResolver.Resolve(game.Ruleset);
    if (!RulesetMeldValidator.IsValidMeld(tileHand, meldType, ruleset))
    {
        return Results.Conflict(new { message = "Meld does not meet ruleset requirements." });
    }

    var meld = new MeldEntity
    {
        GameId = game.Id,
        PlayerId = request.PlayerId,
        Type = meldType.ToString()
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
    var game = await dbContext.Games
        .Include(g => g.Players)
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

    if (!game.Players.Any(player => player.Id == request.PlayerId))
    {
        return Results.NotFound(new { message = "Player not found in game." });
    }

    if (game.CurrentPlayerId != request.PlayerId)
    {
        return Results.Conflict(new { message = "It is not this player's turn." });
    }

    var handTileCount = game.Tiles.Count(tile =>
        tile.PlayerId == request.PlayerId &&
        string.Equals(tile.Location, "Hand", StringComparison.OrdinalIgnoreCase));
    var meldTileCount = game.Tiles.Count(tile =>
        tile.PlayerId == request.PlayerId &&
        string.Equals(tile.Location, "Meld", StringComparison.OrdinalIgnoreCase));
    var declaredKongCount = CountDeclaredKongs(game.Tiles, request.PlayerId);

    if (!TurnFlowValidator.CanDeclareWin(handTileCount, meldTileCount, declaredKongCount))
    {
        return Results.Conflict(new { message = "Player must hold 14 owned tiles (plus one per declared kong) before declaring win." });
    }

    var playerTiles = game.Tiles
        .Where(tile => tile.PlayerId == request.PlayerId && (tile.Location == "Hand" || tile.Location == "Meld"))
        .ToList();

    if (playerTiles.Count == 0)
    {
        return Results.NotFound(new { message = "Player tiles were not found." });
    }

    if (!TryBuildTileHand(playerTiles, out var tileHand))
    {
        return Results.Conflict(new { message = "Player tiles are invalid." });
    }

    var ruleset = RulesetResolver.Resolve(game.Ruleset);
    if (!RulesetHandAnalyzer.IsWinningHand(tileHand, ruleset))
    {
        return Results.Conflict(new { message = "Hand does not meet ruleset requirements." });
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

static async Task ExecuteBotTurnChainAsync(
    GameEntity game,
    MahjongClawDbContext dbContext,
    IHubContext<GameHub> hubContext,
    CancellationToken cancellationToken)
{
    const int maxTurns = 4;
    var turns = 0;

    while (turns < maxTurns &&
           string.Equals(game.Status, "Active", StringComparison.OrdinalIgnoreCase) &&
           game.CurrentPlayerId is not null)
    {
        var currentPlayer = game.Players.FirstOrDefault(player => player.Id == game.CurrentPlayerId);
        if (currentPlayer is null || !IsBotPlayer(currentPlayer))
        {
            break;
        }

        var result = await ExecuteBotTurnAsync(game, dbContext, hubContext, cancellationToken);
        if (!result.Success || result.Response is null || result.Response.Won)
        {
            break;
        }

        turns++;
    }
}

static async Task<BotTurnExecutionResult> ExecuteBotTurnAsync(
    GameEntity game,
    MahjongClawDbContext dbContext,
    IHubContext<GameHub> hubContext,
    CancellationToken cancellationToken)
{
    if (!string.Equals(game.Status, "Active", StringComparison.OrdinalIgnoreCase))
    {
        return BotTurnExecutionResult.Fail(409, "Game is not active.");
    }

    if (game.CurrentPlayerId is null)
    {
        return BotTurnExecutionResult.Fail(409, "No current player set.");
    }

    var botPlayer = game.Players.FirstOrDefault(player => player.Id == game.CurrentPlayerId);
    if (botPlayer is null)
    {
        return BotTurnExecutionResult.Fail(404, "Current player not found.");
    }

    if (!IsBotPlayer(botPlayer))
    {
        return BotTurnExecutionResult.Fail(409, "Current player is not a bot.");
    }

    var handTiles = game.Tiles
        .Where(tile => tile.PlayerId == botPlayer.Id && tile.Location == "Hand")
        .ToList();
    var meldTiles = game.Tiles
        .Where(tile => tile.PlayerId == botPlayer.Id && tile.Location == "Meld")
        .ToList();
    var declaredKongCount = CountDeclaredKongs(game.Tiles, botPlayer.Id);
    var ruleset = RulesetResolver.Resolve(game.Ruleset);
    var canDraw = TurnFlowValidator.CanDraw(handTiles.Count, meldTiles.Count, declaredKongCount);

    DrawTileResponse? drawResponse = null;
    if (canDraw)
    {
        if (!TryBuildTileHand(handTiles, out var botConcealedHand) || !TryBuildTileHand(meldTiles, out var botMeldHand))
        {
            return BotTurnExecutionResult.Fail(409, "Bot hand contains invalid tile data.");
        }

        if (TryGetLatestDiscardTileForBotClaim(game, botPlayer.Id, out var latestDiscardTileEntity))
        {
            if (!TryBuildTileHand([latestDiscardTileEntity], out var discardedTileHand))
            {
                return BotTurnExecutionResult.Fail(409, "Bot hand contains invalid tile data.");
            }

            var discardedTile = discardedTileHand[0];
            if (BotClaimAdvisor.ShouldClaimDiscardWin(botConcealedHand, botMeldHand, discardedTile, ruleset))
            {
                game.Status = "Completed";
                game.CurrentPlayerId = botPlayer.Id;

                await dbContext.SaveChangesAsync(cancellationToken);

                var winResponse = new WinResponse(game.Id, game.Status, botPlayer.Id);
                await SendToGameWithAliases(hubContext, game.Id, "GameWon", winResponse, cancellationToken);

                return BotTurnExecutionResult.Ok(new BotTurnResponse(null, null, latestDiscardTileEntity.Id, game.CurrentPlayerId, true));
            }
        }

        var tile = game.Tiles
            .Where(t => t.Location == "Wall")
            .OrderBy(t => t.Sequence)
            .FirstOrDefault();

        if (tile is null)
        {
            return BotTurnExecutionResult.Fail(409, "No tiles left in wall.");
        }

        tile.Location = "Hand";
        tile.PlayerId = botPlayer.Id;
        drawResponse = new DrawTileResponse(tile.Id, tile.Suit, tile.Rank, tile.Wind, tile.Dragon);
        handTiles.Add(tile);
    }
    else if (!TurnFlowValidator.CanDiscard(handTiles.Count, meldTiles.Count, declaredKongCount))
    {
        return BotTurnExecutionResult.Fail(409, "Bot hand is in an invalid state.");
    }

    var ownedTiles = handTiles.Concat(meldTiles).ToList();
    if (!TryBuildTileHand(ownedTiles, out var botOwnedHand))
    {
        return BotTurnExecutionResult.Fail(409, "Bot hand contains invalid tile data.");
    }

    if (RulesetHandAnalyzer.IsWinningHand(botOwnedHand, ruleset))
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

        return BotTurnExecutionResult.Ok(new BotTurnResponse(drawResponse?.TileId, null, null, game.CurrentPlayerId, true));
    }

    if (!TryBuildTileHand(handTiles, out var botHand))
    {
        return BotTurnExecutionResult.Fail(409, "Bot hand contains invalid tile data.");
    }

    if (!TryBuildTileHand(meldTiles, out var botMeldHandForDiscard))
    {
        return BotTurnExecutionResult.Fail(409, "Bot hand contains invalid tile data.");
    }

    var visibleTileEntities = game.Tiles
        .Where(tile => string.Equals(tile.Location, "Discard", StringComparison.OrdinalIgnoreCase)
            || (string.Equals(tile.Location, "Meld", StringComparison.OrdinalIgnoreCase) && tile.PlayerId != botPlayer.Id))
        .ToList();

    if (!TryBuildTileHand(visibleTileEntities, out var visibleTiles))
    {
        return BotTurnExecutionResult.Fail(409, "Bot visible tiles contain invalid tile data.");
    }

    var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(botHand, botMeldHandForDiscard, visibleTiles, ruleset);
    var discardTile = handTiles[discardIndex];
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
    game.Discards.Add(discard);

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

    return BotTurnExecutionResult.Ok(new BotTurnResponse(drawResponse?.TileId, discard.Id, discardTile.Id, game.CurrentPlayerId));
}

static bool IsBotPlayer(PlayerEntity player)
    => player.Name.StartsWith("Bot", StringComparison.OrdinalIgnoreCase);

static int CountDeclaredKongs(IEnumerable<TileEntity> tiles, Guid playerId)
    => tiles
        .Where(tile => tile.PlayerId == playerId && string.Equals(tile.Location, "Meld", StringComparison.OrdinalIgnoreCase))
        .GroupBy(tile => new { tile.Suit, tile.Rank, tile.Wind, tile.Dragon })
        .Count(group => group.Count() == 4);

static bool TryGetLatestDiscardTileForBotClaim(GameEntity game, Guid botPlayerId, out TileEntity latestDiscardTile)
{
    latestDiscardTile = null!;

    var latestDiscard = game.Discards
        .OrderByDescending(discard => discard.Sequence)
        .FirstOrDefault();

    if (latestDiscard is null || latestDiscard.PlayerId == botPlayerId)
    {
        return false;
    }

    var tile = game.Tiles.FirstOrDefault(candidate =>
        candidate.Id == latestDiscard.TileId &&
        string.Equals(candidate.Location, "Discard", StringComparison.OrdinalIgnoreCase));

    if (tile is null)
    {
        return false;
    }

    latestDiscardTile = tile;
    return true;
}

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

internal record CreateGameRequest(string? HostName, string? Mode, int? BotSeatCount, string? Ruleset = null);
internal record CreateGameResponse(Guid Id, string Status, int Round, DateTimeOffset CreatedAt, Guid? HostPlayerId, string Ruleset);
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
    string Ruleset,
    int WallTotal,
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
internal record BotTurnExecutionResult(bool Success, BotTurnResponse? Response, string? ErrorMessage, int StatusCode)
{
    public static BotTurnExecutionResult Ok(BotTurnResponse response) => new(true, response, null, 200);
    public static BotTurnExecutionResult Fail(int statusCode, string message) => new(false, null, message, statusCode);
}
internal record MeldRequest(Guid PlayerId, string Type, IReadOnlyList<Guid> TileIds);
internal record MeldResponse(Guid MeldId, string Type, IReadOnlyList<Guid> TileIds);
internal record WinRequest(Guid PlayerId);
internal record WinResponse(Guid GameId, string Status, Guid PlayerId);
