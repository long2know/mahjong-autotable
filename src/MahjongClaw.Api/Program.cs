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
    game.RoundWind ??= "East";
    game.DealerRotationCount = 0;

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
    Guid? viewerPlayerId,
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
    var tilesById = game.Tiles.ToDictionary(tile => tile.Id);
    var discardSummaries = game.Discards
        .OrderBy(discard => discard.Sequence)
        .Select(discard =>
        {
            var tileLabel = tilesById.TryGetValue(discard.TileId, out var tileEntity)
                ? BuildTileLabel(tileEntity)
                : "🀫 Unknown";

            return new DiscardSummary(discard.Id, discard.PlayerId, discard.TileId, discard.Sequence, tileLabel);
        })
        .ToList();
    var meldSummaries = game.Melds
        .OrderBy(meld => meld.CreatedAt)
        .Select(meld => new MeldSummary(
            meld.Id,
            meld.PlayerId,
            meld.Type,
            game.Tiles
                .Where(tile => tile.MeldId == meld.Id)
                .OrderBy(tile => tile.Sequence)
                .Select(tile => new TileSummary(tile.Id, BuildTileLabel(tile)))
                .ToList()))
        .ToList();
    var viewerHand = viewerPlayerId is { } viewerId && game.Players.Any(player => player.Id == viewerId)
        ? game.Tiles
            .Where(tile =>
                tile.PlayerId == viewerId &&
                string.Equals(tile.Location, "Hand", StringComparison.OrdinalIgnoreCase))
            .OrderBy(tile => tile.Sequence)
            .Select(tile => new TileSummary(tile.Id, BuildTileLabel(tile)))
            .ToList()
        : new List<TileSummary>();
    var ruleset = RulesetResolver.Resolve(game.Ruleset);
    var stateRevision = tilesInPlay
        + game.Discards.Count
        + game.Melds.Count
        + (string.Equals(game.Status, "Completed", StringComparison.OrdinalIgnoreCase) ? 1 : 0);
    var wallTotal = WallBuilder.GetWallTotal(ruleset);
    var turnState = new TurnActionState(TurnPhase.NotStarted, Array.Empty<TurnAction>());
    if (game.CurrentPlayerId is { } currentPlayerId)
    {
        var handTileCount = game.Tiles.Count(tile =>
            tile.PlayerId == currentPlayerId &&
            string.Equals(tile.Location, "Hand", StringComparison.OrdinalIgnoreCase));
        var meldTileCount = game.Tiles.Count(tile =>
            tile.PlayerId == currentPlayerId &&
            string.Equals(tile.Location, "Meld", StringComparison.OrdinalIgnoreCase));
        var declaredKongCount = CountDeclaredKongs(game.Tiles, currentPlayerId);
        turnState = TurnActionStateResolver.Resolve(game.Status, handTileCount, meldTileCount, declaredKongCount);
    }
    var lastDiscard = game.Discards
        .OrderByDescending(discard => discard.Sequence)
        .FirstOrDefault();

    var response = new GameStateResponse(
        game.Id,
        game.Status,
        game.CurrentPlayerId,
        game.DealerId,
        game.Round,
        game.RoundWind,
        ruleset.ToString(),
        wallTotal,
        game.CreatedAt,
        game.Players.Select(player => new PlayerSummary(
            player.Id,
            player.Name,
            player.Seat,
            player.Score,
            game.Tiles.Count(tile =>
                tile.PlayerId == player.Id &&
                string.Equals(tile.Location, "Hand", StringComparison.OrdinalIgnoreCase)))).ToList(),
        tilesInPlay,
        stateRevision,
        meldSummaries,
        discardSummaries,
        viewerHand,
        turnState.Phase.ToString(),
        ToActionNames(turnState.LegalActions),
        lastDiscard?.TileId,
        lastDiscard?.PlayerId);

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
    var turnState = TurnActionStateResolver.Resolve(game.Status, handTileCount, meldTileCount, declaredKongCount);

    if (!turnState.HasAction(TurnAction.Draw))
    {
        if (turnState.HasAction(TurnAction.Discard))
        {
            var latestHandTile = game.Tiles
                .Where(tile =>
                    tile.PlayerId == request.PlayerId &&
                    string.Equals(tile.Location, "Hand", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(tile => tile.Sequence)
                .FirstOrDefault();

            if (latestHandTile is not null)
            {
                return Results.Ok(new DrawTileResponse(
                    latestHandTile.Id,
                    latestHandTile.Suit,
                    latestHandTile.Rank,
                    latestHandTile.Wind,
                    latestHandTile.Dragon));
            }
        }

        return Results.Conflict(new { message = $"Player cannot draw during {turnState.Phase} phase." });
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

    var existingDiscard = game.Discards.FirstOrDefault(discard =>
        discard.PlayerId == request.PlayerId &&
        discard.TileId == request.TileId);
    if (existingDiscard is not null)
    {
        return Results.Ok(new DiscardTileResponse(existingDiscard.Id, existingDiscard.TileId, existingDiscard.Sequence));
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
    var turnState = TurnActionStateResolver.Resolve(game.Status, handTileCount, meldTileCount, declaredKongCount);

    if (!turnState.HasAction(TurnAction.Discard))
    {
        return Results.Conflict(new { message = $"Player cannot discard during {turnState.Phase} phase." });
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

    if (!Enum.TryParse<MeldType>(request.Type, true, out var meldType))
    {
        return Results.BadRequest(new { message = "Invalid meld type." });
    }

    var normalizedMeldType = meldType.ToString();
    var requestedTileIds = request.TileIds.OrderBy(tileId => tileId).ToList();
    var existingMeld = game.Melds.FirstOrDefault(candidate =>
        candidate.PlayerId == request.PlayerId &&
        string.Equals(candidate.Type, normalizedMeldType, StringComparison.OrdinalIgnoreCase) &&
        game.Tiles
            .Where(tile => tile.MeldId == candidate.Id)
            .Select(tile => tile.Id)
            .OrderBy(tileId => tileId)
            .SequenceEqual(requestedTileIds));

    if (existingMeld is not null)
    {
        return Results.Ok(new MeldResponse(existingMeld.Id, existingMeld.Type, requestedTileIds));
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

    var meldTileRequirement = meldType == MeldType.Kong ? 4 : 3;
    var declaredKongCount = CountDeclaredKongs(game.Tiles, request.PlayerId);
    var turnState = TurnActionStateResolver.Resolve(game.Status, handTileCount, meldTileCount, declaredKongCount);

    if (!turnState.HasAction(TurnAction.Meld))
    {
        return Results.Conflict(new { message = $"Player cannot declare meld during {turnState.Phase} phase." });
    }

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

    // M3: Classify kong type
    string? kongKind = null;
    if (meldType == MeldType.Kong)
    {
        var lastDiscard = game.Discards
            .OrderByDescending(d => d.Sequence)
            .FirstOrDefault();
        var claimedFromDiscard = lastDiscard is not null && tiles.Any(t => t.Id == lastDiscard.TileId);
        
        KongKind kind;
        if (!claimedFromDiscard)
        {
            // Check if promoting an existing pung
            var sampleTile = tiles.First();
            var existingPung = game.Melds.FirstOrDefault(m =>
                m.PlayerId == request.PlayerId &&
                string.Equals(m.Type, "Pung", StringComparison.OrdinalIgnoreCase) &&
                game.Tiles.Any(t => t.MeldId == m.Id && 
                    t.Suit == sampleTile.Suit && 
                    t.Rank == sampleTile.Rank &&
                    t.Wind == sampleTile.Wind &&
                    t.Dragon == sampleTile.Dragon));
            
            kind = existingPung is not null ? KongKind.Promoted : KongKind.Concealed;
        }
        else
        {
            kind = KongKind.Exposed;
        }
        
        kongKind = KongKindParser.ToMeldTypeString(kind);
        meld.Kind = kongKind;
    }

    dbContext.Melds.Add(meld);

    foreach (var tile in tiles)
    {
        tile.Location = "Meld";
        tile.Meld = meld;
    }

    await dbContext.SaveChangesAsync(cancellationToken);

    // M4: Broadcast robbing-the-kong opportunity for promoted kongs
    if (meldType == MeldType.Kong && kongKind == "PromotedKong" && tiles.Any())
    {
        var kongTile = tiles.First();
        var eligibleOpponents = new List<Guid>();
        
        if (!TryBuildTileHand(new[] { kongTile }, out var kongTileHand) || kongTileHand.Count != 1)
        {
            // Invalid kong tile, skip robbing check
        }
        else
        {
            var promotedTile = kongTileHand[0];
            
            foreach (var opponent in game.Players.Where(p => p.Id != request.PlayerId))
            {
                var opponentHandTiles = game.Tiles
                    .Where(t => t.PlayerId == opponent.Id && 
                               string.Equals(t.Location, "Hand", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                
                if (!TryBuildTileHand(opponentHandTiles, out var opponentTileHand))
                {
                    continue;
                }
                
                var opponentKongCount = game.Melds
                    .Count(m => m.PlayerId == opponent.Id && 
                               (string.Equals(m.Type, "Kong", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(m.Kind, "ConcealedKong", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(m.Kind, "ExposedKong", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(m.Kind, "PromotedKong", StringComparison.OrdinalIgnoreCase)));
                
                if (ChangshaRobbingKongDetector.CanRobPromotedKong(opponentTileHand, promotedTile, opponentKongCount))
                {
                    eligibleOpponents.Add(opponent.Id);
                }
            }
        }
        
        if (eligibleOpponents.Any())
        {
            var robOpportunity = new
            {
                TileId = kongTile.Id,
                Suit = kongTile.Suit,
                Rank = kongTile.Rank,
                Wind = kongTile.Wind,
                Dragon = kongTile.Dragon,
                KongPlayerId = request.PlayerId,
                EligibleOpponents = eligibleOpponents
            };
            await SendToGameWithAliases(hubContext, game.Id, "KongRobOpportunity", robOpportunity, cancellationToken);
        }
    }

    // M3: Draw replacement tile after kong
    if (meldType == MeldType.Kong)
    {
        var replacementTile = game.Tiles
            .Where(t => t.Location == "Wall")
            .OrderBy(t => t.Sequence)
            .FirstOrDefault();

        if (replacementTile is not null)
        {
            replacementTile.Location = "Hand";
            replacementTile.PlayerId = request.PlayerId;
            await dbContext.SaveChangesAsync(cancellationToken);

            var drawResponse = new DrawTileResponse(
                replacementTile.Id, 
                replacementTile.Suit, 
                replacementTile.Rank, 
                replacementTile.Wind, 
                replacementTile.Dragon);
            await SendToGameWithAliases(hubContext, game.Id, "TileDrawn", drawResponse, cancellationToken);
        }
    }

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
        .Include(g => g.Melds)
        .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

    if (game is null)
    {
        return Results.NotFound();
    }

    if (string.Equals(game.Status, "Completed", StringComparison.OrdinalIgnoreCase) &&
        game.CurrentPlayerId == request.PlayerId)
    {
        return Results.Ok(new WinResponse(game.Id, game.Status, request.PlayerId));
    }

    if (!string.Equals(game.Status, "Active", StringComparison.OrdinalIgnoreCase))
    {
        return Results.Conflict(new { message = "Game is not active." });
    }

    if (!game.Players.Any(player => player.Id == request.PlayerId))
    {
        return Results.NotFound(new { message = "Player not found in game." });
    }

    var isDiscardWinClaim = !request.IsSelfDraw;

    if (game.CurrentPlayerId != request.PlayerId && !isDiscardWinClaim)
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
    var turnState = TurnActionStateResolver.Resolve(game.Status, handTileCount, meldTileCount, declaredKongCount);
    DiscardEntity? claimableDiscard = null;
    TileEntity? claimableDiscardTile = null;

    if (isDiscardWinClaim)
    {
        claimableDiscard = game.Discards
            .OrderByDescending(discard => discard.Sequence)
            .FirstOrDefault();

        if (claimableDiscard is null || claimableDiscard.PlayerId == request.PlayerId)
        {
            return Results.Conflict(new { message = "No claimable discard is available for a discard win." });
        }

        if (request.DiscarderPlayerId is not null && request.DiscarderPlayerId != claimableDiscard.PlayerId)
        {
            return Results.Conflict(new { message = "DiscarderPlayerId does not match the latest claimable discard." });
        }

        claimableDiscardTile = game.Tiles.FirstOrDefault(tile =>
            tile.Id == claimableDiscard.TileId &&
            string.Equals(tile.Location, "Discard", StringComparison.OrdinalIgnoreCase));

        if (claimableDiscardTile is null)
        {
            return Results.Conflict(new { message = "Latest claimable discard tile was not found." });
        }

        if (!turnState.HasAction(TurnAction.Draw))
        {
            return Results.Conflict(new { message = $"Player cannot claim discard win during {turnState.Phase} phase." });
        }

        if (!TurnFlowValidator.CanDraw(handTileCount, meldTileCount, declaredKongCount))
        {
            return Results.Conflict(new { message = "Player must hold 13 owned tiles (plus one per declared kong) before claiming discard win." });
        }
    }
    else
    {
        if (!turnState.HasAction(TurnAction.Win))
        {
            return Results.Conflict(new { message = $"Player cannot declare win during {turnState.Phase} phase." });
        }

        if (!TurnFlowValidator.CanDeclareWin(handTileCount, meldTileCount, declaredKongCount))
        {
            return Results.Conflict(new { message = "Player must hold 14 owned tiles (plus one per declared kong) before declaring win." });
        }
    }

    var playerTiles = game.Tiles
        .Where(tile => tile.PlayerId == request.PlayerId && (tile.Location == "Hand" || tile.Location == "Meld"))
        .ToList();

    if (playerTiles.Count == 0)
    {
        return Results.NotFound(new { message = "Player tiles were not found." });
    }

    var evaluationTiles = claimableDiscardTile is null
        ? playerTiles
        : playerTiles.Append(claimableDiscardTile).ToList();

    if (!TryBuildTileHand(evaluationTiles, out var tileHand))
    {
        return Results.Conflict(new { message = "Player tiles are invalid." });
    }

    var ruleset = RulesetResolver.Resolve(game.Ruleset);
    if (!RulesetHandAnalyzer.TryGetWinningHand(tileHand, ruleset, out var winningHand) || winningHand is null)
    {
        return Results.Conflict(new { message = "Hand does not meet ruleset requirements." });
    }

    ChangshaWinEvaluation? changshaEvaluation = null;
    IReadOnlyList<PlayerScoreDelta>? scoreDeltas = null;
    Guid? nextDealerId = null;
    int? nextRound = null;
    string? birdSeat = null;
    var resolvedDiscarderPlayerId = request.IsSelfDraw
        ? null
        : request.DiscarderPlayerId ?? claimableDiscard?.PlayerId;
    if (ruleset == MahjongRuleset.Changsha258)
    {
        if (!request.IsSelfDraw && resolvedDiscarderPlayerId is null)
        {
            return Results.BadRequest(new { message = "DiscarderPlayerId is required for discard wins." });
        }

        if (request.IsSelfDraw && request.DiscarderPlayerId is not null)
        {
            return Results.BadRequest(new { message = "DiscarderPlayerId must be omitted for self-draw wins." });
        }

        if (resolvedDiscarderPlayerId == request.PlayerId)
        {
            return Results.BadRequest(new { message = "Winner and discarder cannot be the same player." });
        }

        if (request.BirdRank is < 1 or > 9)
        {
            return Results.BadRequest(new { message = "BirdRank must be between 1 and 9." });
        }

        if (request.BirdRanks is not null && request.BirdRanks.Any(rank => rank is < 1 or > 9))
        {
            return Results.BadRequest(new { message = "All BirdRanks must be between 1 and 9." });
        }

        if (request.BirdRanks is not null && request.BirdRank is not null)
        {
            return Results.BadRequest(new { message = "Cannot specify both BirdRank and BirdRanks; use BirdRanks for multi-bird scenarios." });
        }

        // M5: Server-side last-tile detection
        var wallRemaining = game.Tiles.Count(t => string.Equals(t.Location, "Wall", StringComparison.OrdinalIgnoreCase));
        var isLastTileDraw = request.IsLastTileDraw;
        var isLastTileDiscard = request.IsLastTileDiscard;
        
        if (wallRemaining == 0)
        {
            if (request.IsSelfDraw)
            {
                isLastTileDraw = true;
            }
            else
            {
                isLastTileDiscard = true;
            }
        }

        // D6: Detect fully reliant via domain helper
        var isFullyReliant = false;
        var playerMelds = game.Melds.Where(m => m.PlayerId == request.PlayerId).ToList();
        
        if (playerMelds.Any())
        {
            var meldInfos = new List<MeldInfo>();
            
            foreach (var meld in playerMelds)
            {
                var meldType = meld.Type;
                var meldKind = meld.Kind;
                
                // Determine if meld came from claimed discard or self-drawn
                // Chow, Pung: assumed from claimed discard in standard play
                // ExposedKong: from claimed discard
                // ConcealedKong: self-drawn (all 4 tiles)
                // PromotedKong: original pung from discard, 4th tile self-drawn
                TileSource meldSource;
                if (string.Equals(meldType, "Kong", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(meldKind, "ConcealedKong", StringComparison.OrdinalIgnoreCase))
                    {
                        meldSource = TileSource.SelfDrawn;
                    }
                    else
                    {
                        // ExposedKong and PromotedKong: original tiles from discard
                        meldSource = TileSource.ClaimedDiscard;
                    }
                }
                else
                {
                    // Chow, Pung: assumed from claimed discard
                    meldSource = TileSource.ClaimedDiscard;
                }
                
                meldInfos.Add(new MeldInfo(meldSource));
            }
            
            var winningTileSource = request.IsSelfDraw ? TileSource.SelfDrawn : TileSource.ClaimedDiscard;
            isFullyReliant = ChangshaFullyReliantDetector.IsFullyReliant(meldInfos, winningTileSource);
        }
        else if (!request.IsSelfDraw)
        {
            // No melds, but won from discard = fully reliant
            isFullyReliant = true;
        }

        changshaEvaluation = ChangshaWinTypeEvaluator.Evaluate(
            tileHand,
            winningHand,
            BuildChangshaWinContext(request, isFullyReliant, isLastTileDraw, isLastTileDiscard));

        if (!TryBuildChangshaSettlementSeats(game.Players, out var settlementSeats, out var seatBuildError))
        {
            return Results.Conflict(new { message = seatBuildError });
        }

        var settlementRequest = new ChangshaSettlementRequest(
            settlementSeats,
            WinnerId: request.PlayerId,
            DealerId: game.DealerId ?? request.PlayerId,
            IsSelfDraw: request.IsSelfDraw,
            IsBigWin: changshaEvaluation.IsBigWin,
            DiscarderId: resolvedDiscarderPlayerId,
            BirdRank: request.BirdRanks is null ? request.BirdRank : null,
            BirdRanks: request.BirdRanks,
            IsMultipleWin: request.IsMultipleWin);

        var settlementResult = ChangshaSettlementCalculator.Calculate(settlementRequest);

        foreach (var delta in settlementResult.ScoreDeltas)
        {
            var player = game.Players.First(candidate => candidate.Id == delta.PlayerId);
            player.Score = delta.NewScore;
        }

        var previousDealerId = game.DealerId;
        game.DealerId = settlementResult.NextDealerId;
        game.Round = Math.Max(1, game.Round + settlementResult.NextRoundIncrement);

        if (previousDealerId != game.DealerId)
        {
            game.DealerRotationCount++;
            
            if (game.DealerRotationCount > 0 && game.DealerRotationCount % 4 == 0)
            {
                game.RoundWind = game.RoundWind switch
                {
                    "East" => "South",
                    "South" => "West",
                    "West" => "North",
                    "North" => "East",
                    _ => "East"
                };
            }
        }

        scoreDeltas = settlementResult.ScoreDeltas
            .Select(delta => new PlayerScoreDelta(delta.PlayerId, delta.Delta, delta.NewScore))
            .ToList();
        nextDealerId = settlementResult.NextDealerId;
        nextRound = game.Round;
        birdSeat = settlementResult.BirdSeat?.ToString();
    }

    game.Status = "Completed";
    game.CurrentPlayerId = request.PlayerId;

    await dbContext.SaveChangesAsync(cancellationToken);

    var response = new WinResponse(
        game.Id,
        game.Status,
        request.PlayerId,
        changshaEvaluation?.WinTypes.Select(static type => type.ToString()).ToList(),
        changshaEvaluation?.IsBigWin,
        changshaEvaluation?.IsSmallWin,
        scoreDeltas,
        nextDealerId,
        nextRound,
        birdSeat);
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

    if (!dbContext.Entry(game).Collection(g => g.Melds).IsLoaded)
    {
        await dbContext.Entry(game).Collection(g => g.Melds).LoadAsync(cancellationToken);
    }

    var handTiles = game.Tiles
        .Where(tile => tile.PlayerId == botPlayer.Id && tile.Location == "Hand")
        .ToList();
    var meldTiles = game.Tiles
        .Where(tile => tile.PlayerId == botPlayer.Id && tile.Location == "Meld")
        .ToList();
    var declaredKongCount = CountDeclaredKongs(game.Tiles, botPlayer.Id);
    var ruleset = RulesetResolver.Resolve(game.Ruleset);
    var turnState = TurnActionStateResolver.Resolve(game.Status, handTiles.Count, meldTiles.Count, declaredKongCount);

    DrawTileResponse? drawResponse = null;
    if (turnState.HasAction(TurnAction.Draw))
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
                ChangshaWinEvaluation? changshaEvaluation = null;
                IReadOnlyList<PlayerScoreDelta>? scoreDeltas = null;
                Guid? nextDealerId = null;
                int? nextRound = null;
                string? birdSeat = null;

                if (ruleset == MahjongRuleset.Changsha258)
                {
                    var winningTiles = botConcealedHand
                        .Concat(botMeldHand)
                        .Append(discardedTile)
                        .ToList();

                    if (!RulesetHandAnalyzer.TryGetWinningHand(winningTiles, ruleset, out var winningHand) || winningHand is null)
                    {
                        return BotTurnExecutionResult.Fail(409, "Bot discard win evaluation failed.");
                    }

                    var latestDiscard = game.Discards
                        .OrderByDescending(discard => discard.Sequence)
                        .FirstOrDefault();

                    if (latestDiscard is null)
                    {
                        return BotTurnExecutionResult.Fail(409, "Discard win requested but no discard was found.");
                    }

                    if (!TryApplyChangshaBotWin(
                        game,
                        botPlayer.Id,
                        isSelfDraw: false,
                        discarderId: latestDiscard.PlayerId,
                        winningTiles,
                        winningHand,
                        out changshaEvaluation,
                        out scoreDeltas,
                        out nextDealerId,
                        out nextRound,
                        out birdSeat,
                        out var changshaError))
                    {
                        return BotTurnExecutionResult.Fail(409, changshaError ?? "Changsha win settlement failed.");
                    }
                }

                game.Status = "Completed";
                game.CurrentPlayerId = botPlayer.Id;

                await dbContext.SaveChangesAsync(cancellationToken);

                var winResponse = new WinResponse(
                    game.Id,
                    game.Status,
                    botPlayer.Id,
                    changshaEvaluation?.WinTypes.Select(static type => type.ToString()).ToList(),
                    changshaEvaluation?.IsBigWin,
                    changshaEvaluation?.IsSmallWin,
                    scoreDeltas,
                    nextDealerId,
                    nextRound,
                    birdSeat);
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
    else if (!turnState.HasAction(TurnAction.Discard))
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
        ChangshaWinEvaluation? changshaEvaluation = null;
        IReadOnlyList<PlayerScoreDelta>? scoreDeltas = null;
        Guid? nextDealerId = null;
        int? nextRound = null;
        string? birdSeat = null;

        if (ruleset == MahjongRuleset.Changsha258)
        {
            if (!RulesetHandAnalyzer.TryGetWinningHand(botOwnedHand, ruleset, out var winningHand) || winningHand is null)
            {
                return BotTurnExecutionResult.Fail(409, "Bot win evaluation failed.");
            }

            if (!TryApplyChangshaBotWin(
                game,
                botPlayer.Id,
                isSelfDraw: true,
                discarderId: null,
                botOwnedHand,
                winningHand,
                out changshaEvaluation,
                out scoreDeltas,
                out nextDealerId,
                out nextRound,
                out birdSeat,
                out var changshaError))
            {
                return BotTurnExecutionResult.Fail(409, changshaError ?? "Changsha win settlement failed.");
            }
        }

        game.Status = "Completed";
        game.CurrentPlayerId = botPlayer.Id;

        await dbContext.SaveChangesAsync(cancellationToken);

        if (drawResponse is not null)
        {
            await SendToGameWithAliases(hubContext, game.Id, "TileDrawn", drawResponse, cancellationToken);
        }

        var winResponse = new WinResponse(
            game.Id,
            game.Status,
            botPlayer.Id,
            changshaEvaluation?.WinTypes.Select(static type => type.ToString()).ToList(),
            changshaEvaluation?.IsBigWin,
            changshaEvaluation?.IsSmallWin,
            scoreDeltas,
            nextDealerId,
            nextRound,
            birdSeat);
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

    if (turnState.HasAction(TurnAction.Meld))
    {
        if (BotMeldAdvisor.TrySelectPromotedKong(botHand, botMeldHandForDiscard, ruleset, out var promotedKongTile))
        {
            var kongApplied = await TryApplyBotPromotedKongAsync(
                game,
                botPlayer,
                promotedKongTile,
                drawResponse,
                dbContext,
                hubContext,
                cancellationToken);

            if (kongApplied)
            {
                return await ExecuteBotTurnAsync(game, dbContext, hubContext, cancellationToken);
            }
        }

        if (BotMeldAdvisor.TrySelectConcealedKong(botHand, ruleset, out var kongTile))
        {
            var kongTiles = handTiles
                .Where(tile => TileEntityMatches(tile, kongTile))
                .Take(4)
                .ToList();

            if (kongTiles.Count == 4)
            {
                var kongApplied = await TryApplyBotConcealedKongAsync(
                    game,
                    botPlayer,
                    kongTiles,
                    drawResponse,
                    dbContext,
                    hubContext,
                    cancellationToken);

                if (kongApplied)
                {
                    return await ExecuteBotTurnAsync(game, dbContext, hubContext, cancellationToken);
                }
            }
        }
    }

    var visibleMeldTileEntities = game.Tiles
        .Where(tile => string.Equals(tile.Location, "Meld", StringComparison.OrdinalIgnoreCase)
            && tile.PlayerId != botPlayer.Id)
        .ToList();

    var visibleTileEntities = game.Tiles
        .Where(tile => string.Equals(tile.Location, "Discard", StringComparison.OrdinalIgnoreCase))
        .Concat(visibleMeldTileEntities)
        .ToList();

    if (!TryBuildTileHand(visibleTileEntities, out var visibleTiles))
    {
        return BotTurnExecutionResult.Fail(409, "Bot visible tiles contain invalid tile data.");
    }

    if (!TryBuildTileHand(visibleMeldTileEntities, out var visibleMeldTiles))
    {
        return BotTurnExecutionResult.Fail(409, "Bot visible meld tiles contain invalid tile data.");
    }

    var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(botHand, botMeldHandForDiscard, visibleTiles, visibleMeldTiles, ruleset);
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

static async Task<bool> TryApplyBotConcealedKongAsync(
    GameEntity game,
    PlayerEntity botPlayer,
    IReadOnlyList<TileEntity> kongTiles,
    DrawTileResponse? drawResponse,
    MahjongClawDbContext dbContext,
    IHubContext<GameHub> hubContext,
    CancellationToken cancellationToken)
{
    if (kongTiles.Count != 4)
    {
        return false;
    }

    if (drawResponse is not null)
    {
        await SendToGameWithAliases(hubContext, game.Id, "TileDrawn", drawResponse, cancellationToken);
    }

    var meld = new MeldEntity
    {
        GameId = game.Id,
        PlayerId = botPlayer.Id,
        Type = MeldType.Kong.ToString(),
        Kind = KongKindParser.ToMeldTypeString(KongKind.Concealed)
    };

    dbContext.Melds.Add(meld);
    game.Melds.Add(meld);

    foreach (var tile in kongTiles)
    {
        tile.Location = "Meld";
        tile.Meld = meld;
    }

    await dbContext.SaveChangesAsync(cancellationToken);

    var replacementTile = game.Tiles
        .Where(t => t.Location == "Wall")
        .OrderBy(t => t.Sequence)
        .FirstOrDefault();

    if (replacementTile is not null)
    {
        replacementTile.Location = "Hand";
        replacementTile.PlayerId = botPlayer.Id;
        await dbContext.SaveChangesAsync(cancellationToken);

        var replacementDraw = new DrawTileResponse(
            replacementTile.Id,
            replacementTile.Suit,
            replacementTile.Rank,
            replacementTile.Wind,
            replacementTile.Dragon);
        await SendToGameWithAliases(hubContext, game.Id, "TileDrawn", replacementDraw, cancellationToken);
    }

    var meldResponse = new MeldResponse(meld.Id, meld.Type, kongTiles.Select(tile => tile.Id).ToList());
    await SendToGameWithAliases(hubContext, game.Id, "MeldDeclared", meldResponse, cancellationToken);
    return true;
}

static async Task<bool> TryApplyBotPromotedKongAsync(
    GameEntity game,
    PlayerEntity botPlayer,
    Tile kongTile,
    DrawTileResponse? drawResponse,
    MahjongClawDbContext dbContext,
    IHubContext<GameHub> hubContext,
    CancellationToken cancellationToken)
{
    var meldTiles = game.Tiles
        .Where(tile => tile.PlayerId == botPlayer.Id
            && string.Equals(tile.Location, "Meld", StringComparison.OrdinalIgnoreCase)
            && TileEntityMatches(tile, kongTile))
        .ToList();

    if (meldTiles.Count != 3)
    {
        return false;
    }

    var meldId = meldTiles[0].MeldId;
    if (meldId is null)
    {
        return false;
    }

    var meld = game.Melds.FirstOrDefault(candidate => candidate.Id == meldId);
    if (meld is null || !string.Equals(meld.Type, "Pung", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    var handTile = game.Tiles.FirstOrDefault(tile => tile.PlayerId == botPlayer.Id
        && string.Equals(tile.Location, "Hand", StringComparison.OrdinalIgnoreCase)
        && TileEntityMatches(tile, kongTile));

    if (handTile is null)
    {
        return false;
    }

    if (drawResponse is not null)
    {
        await SendToGameWithAliases(hubContext, game.Id, "TileDrawn", drawResponse, cancellationToken);
    }

    meld.Type = MeldType.Kong.ToString();
    meld.Kind = KongKindParser.ToMeldTypeString(KongKind.Promoted);

    handTile.Location = "Meld";
    handTile.Meld = meld;

    await dbContext.SaveChangesAsync(cancellationToken);

    if (TryBuildTileHand(new[] { handTile }, out var kongTileHand) && kongTileHand.Count == 1)
    {
        var promotedTile = kongTileHand[0];
        var eligibleOpponents = new List<Guid>();

        foreach (var opponent in game.Players.Where(p => p.Id != botPlayer.Id))
        {
            var opponentHandTiles = game.Tiles
                .Where(t => t.PlayerId == opponent.Id &&
                           string.Equals(t.Location, "Hand", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!TryBuildTileHand(opponentHandTiles, out var opponentTileHand))
            {
                continue;
            }

            var opponentKongCount = game.Melds
                .Count(m => m.PlayerId == opponent.Id &&
                           (string.Equals(m.Type, "Kong", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(m.Kind, "ConcealedKong", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(m.Kind, "ExposedKong", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(m.Kind, "PromotedKong", StringComparison.OrdinalIgnoreCase)));

            if (ChangshaRobbingKongDetector.CanRobPromotedKong(opponentTileHand, promotedTile, opponentKongCount))
            {
                eligibleOpponents.Add(opponent.Id);
            }
        }

        if (eligibleOpponents.Any())
        {
            var robOpportunity = new
            {
                TileId = handTile.Id,
                Suit = handTile.Suit,
                Rank = handTile.Rank,
                Wind = handTile.Wind,
                Dragon = handTile.Dragon,
                KongPlayerId = botPlayer.Id,
                EligibleOpponents = eligibleOpponents
            };
            await SendToGameWithAliases(hubContext, game.Id, "KongRobOpportunity", robOpportunity, cancellationToken);
        }
    }

    var replacementTile = game.Tiles
        .Where(t => t.Location == "Wall")
        .OrderBy(t => t.Sequence)
        .FirstOrDefault();

    if (replacementTile is not null)
    {
        replacementTile.Location = "Hand";
        replacementTile.PlayerId = botPlayer.Id;
        await dbContext.SaveChangesAsync(cancellationToken);

        var replacementDraw = new DrawTileResponse(
            replacementTile.Id,
            replacementTile.Suit,
            replacementTile.Rank,
            replacementTile.Wind,
            replacementTile.Dragon);
        await SendToGameWithAliases(hubContext, game.Id, "TileDrawn", replacementDraw, cancellationToken);
    }

    var meldTileIds = meldTiles.Select(tile => tile.Id).Append(handTile.Id).ToList();
    var meldResponse = new MeldResponse(meld.Id, meld.Type, meldTileIds);
    await SendToGameWithAliases(hubContext, game.Id, "MeldDeclared", meldResponse, cancellationToken);
    return true;
}

static bool TileEntityMatches(TileEntity tileEntity, Tile tile)
    => string.Equals(tileEntity.Suit, tile.Suit.ToString(), StringComparison.OrdinalIgnoreCase)
        && tileEntity.Rank == tile.Rank
        && string.Equals(tileEntity.Wind, tile.Wind?.ToString(), StringComparison.OrdinalIgnoreCase)
        && string.Equals(tileEntity.Dragon, tile.Dragon?.ToString(), StringComparison.OrdinalIgnoreCase);

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

static IReadOnlyList<string> ToActionNames(IReadOnlyList<TurnAction> actions)
    => actions
        .Select(static action => action.ToString())
        .ToList();

static string BuildTileLabel(TileEntity tile)
{
    var characterGlyphs = new[] { "🀇", "🀈", "🀉", "🀊", "🀋", "🀌", "🀍", "🀎", "🀏" };
    var bambooGlyphs = new[] { "🀐", "🀑", "🀒", "🀓", "🀔", "🀕", "🀖", "🀗", "🀘" };
    var dotGlyphs = new[] { "🀙", "🀚", "🀛", "🀜", "🀝", "🀞", "🀟", "🀠", "🀡" };

    if (Enum.TryParse<TileSuit>(tile.Suit, true, out var suit))
    {
        return suit switch
        {
            TileSuit.Characters when tile.Rank is >= 1 and <= 9
                => $"{characterGlyphs[tile.Rank - 1]} {tile.Rank} Character",
            TileSuit.Bamboos when tile.Rank is >= 1 and <= 9
                => $"{bambooGlyphs[tile.Rank - 1]} {tile.Rank} Bamboo",
            TileSuit.Dots when tile.Rank is >= 1 and <= 9
                => $"{dotGlyphs[tile.Rank - 1]} {tile.Rank} Dot",
            TileSuit.Winds when Enum.TryParse<Wind>(tile.Wind, true, out var wind)
                => wind switch
                {
                    Wind.East => "🀀 East Wind",
                    Wind.South => "🀁 South Wind",
                    Wind.West => "🀂 West Wind",
                    Wind.North => "🀃 North Wind",
                    _ => $"{tile.Suit} {tile.Wind}"
                },
            TileSuit.Dragons when !string.IsNullOrWhiteSpace(tile.Dragon)
                => tile.Dragon.Trim().ToLowerInvariant() switch
                {
                    "red" => "🀄 Red Dragon",
                    "green" => "🀅 Green Dragon",
                    "white" => "🀆 White Dragon",
                    _ => $"{tile.Dragon} Dragon"
                },
            _ => $"{tile.Suit} {tile.Rank}"
        };
    }

    return $"{tile.Suit} {tile.Rank}";
}

static ChangshaWinContext BuildChangshaWinContext(
    WinRequest request, 
    bool? overrideIsFullyReliant = null,
    bool? overrideIsLastTileDraw = null,
    bool? overrideIsLastTileDiscard = null)
    => new(
        IsInitialDeal: request.IsInitialDeal,
        IsHeavenly: request.IsHeavenly,
        IsEarthly: request.IsEarthly,
        IsLastTileDraw: overrideIsLastTileDraw ?? request.IsLastTileDraw,
        IsLastTileDiscard: overrideIsLastTileDiscard ?? request.IsLastTileDiscard,
        IsWinAfterKong: request.IsWinAfterKong,
        IsRobbingKong: request.IsRobbingKong,
        IsKongOnCannon: request.IsKongOnCannon,
        IsFullyReliant: overrideIsFullyReliant ?? request.IsFullyReliant);

static bool TryApplyChangshaBotWin(
    GameEntity game,
    Guid winnerId,
    bool isSelfDraw,
    Guid? discarderId,
    IReadOnlyList<Tile> tileHand,
    WinningHand winningHand,
    out ChangshaWinEvaluation? changshaEvaluation,
    out IReadOnlyList<PlayerScoreDelta>? scoreDeltas,
    out Guid? nextDealerId,
    out int? nextRound,
    out string? birdSeat,
    out string? errorMessage)
{
    changshaEvaluation = null;
    scoreDeltas = null;
    nextDealerId = null;
    nextRound = null;
    birdSeat = null;
    errorMessage = null;

    if (!isSelfDraw && discarderId is null)
    {
        errorMessage = "DiscarderPlayerId is required for discard wins.";
        return false;
    }

    if (discarderId == winnerId)
    {
        errorMessage = "Winner and discarder cannot be the same player.";
        return false;
    }

    var wallRemaining = game.Tiles.Count(t => string.Equals(t.Location, "Wall", StringComparison.OrdinalIgnoreCase));
    var isLastTileDraw = isSelfDraw && wallRemaining == 0;
    var isLastTileDiscard = !isSelfDraw && wallRemaining == 0;

    var isFullyReliant = false;
    var playerMelds = game.Melds.Where(m => m.PlayerId == winnerId).ToList();

    if (playerMelds.Any())
    {
        var meldInfos = new List<MeldInfo>();

        foreach (var meld in playerMelds)
        {
            var meldType = meld.Type;
            var meldKind = meld.Kind;

            TileSource meldSource;
            if (string.Equals(meldType, "Kong", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(meldKind, "ConcealedKong", StringComparison.OrdinalIgnoreCase))
                {
                    meldSource = TileSource.SelfDrawn;
                }
                else
                {
                    meldSource = TileSource.ClaimedDiscard;
                }
            }
            else
            {
                meldSource = TileSource.ClaimedDiscard;
            }

            meldInfos.Add(new MeldInfo(meldSource));
        }

        var winningTileSource = isSelfDraw ? TileSource.SelfDrawn : TileSource.ClaimedDiscard;
        isFullyReliant = ChangshaFullyReliantDetector.IsFullyReliant(meldInfos, winningTileSource);
    }
    else if (!isSelfDraw)
    {
        isFullyReliant = true;
    }

    changshaEvaluation = ChangshaWinTypeEvaluator.Evaluate(
        tileHand,
        winningHand,
        new ChangshaWinContext(
            IsInitialDeal: false,
            IsHeavenly: false,
            IsEarthly: false,
            IsLastTileDraw: isLastTileDraw,
            IsLastTileDiscard: isLastTileDiscard,
            IsWinAfterKong: false,
            IsRobbingKong: false,
            IsKongOnCannon: false,
            IsFullyReliant: isFullyReliant));

    if (!TryBuildChangshaSettlementSeats(game.Players, out var settlementSeats, out var seatBuildError))
    {
        errorMessage = seatBuildError;
        return false;
    }

    var settlementRequest = new ChangshaSettlementRequest(
        settlementSeats,
        WinnerId: winnerId,
        DealerId: game.DealerId ?? winnerId,
        IsSelfDraw: isSelfDraw,
        IsBigWin: changshaEvaluation.IsBigWin,
        DiscarderId: isSelfDraw ? null : discarderId);

    var settlementResult = ChangshaSettlementCalculator.Calculate(settlementRequest);

    foreach (var delta in settlementResult.ScoreDeltas)
    {
        var player = game.Players.First(candidate => candidate.Id == delta.PlayerId);
        player.Score = delta.NewScore;
    }

    var previousDealerId = game.DealerId;
    game.DealerId = settlementResult.NextDealerId;
    game.Round = Math.Max(1, game.Round + settlementResult.NextRoundIncrement);

    if (previousDealerId != game.DealerId)
    {
        game.DealerRotationCount++;

        if (game.DealerRotationCount > 0 && game.DealerRotationCount % 4 == 0)
        {
            game.RoundWind = game.RoundWind switch
            {
                "East" => "South",
                "South" => "West",
                "West" => "North",
                "North" => "East",
                _ => "East"
            };
        }
    }

    scoreDeltas = settlementResult.ScoreDeltas
        .Select(delta => new PlayerScoreDelta(delta.PlayerId, delta.Delta, delta.NewScore))
        .ToList();
    nextDealerId = settlementResult.NextDealerId;
    nextRound = game.Round;
    birdSeat = settlementResult.BirdSeat?.ToString();

    return true;
}

static bool TryBuildChangshaSettlementSeats(
    IEnumerable<PlayerEntity> players,
    out List<ChangshaSettlementSeat> seats,
    out string? error)
{
    seats = new List<ChangshaSettlementSeat>();
    error = null;

    foreach (var player in players)
    {
        if (!Enum.TryParse<Wind>(player.Seat, true, out var seatWind))
        {
            error = $"Player '{player.Name}' has invalid seat '{player.Seat}'.";
            seats.Clear();
            return false;
        }

        seats.Add(new ChangshaSettlementSeat(player.Id, seatWind, player.Score));
    }

    if (seats.Count != 4)
    {
        error = "Changsha settlement requires exactly four players.";
        seats.Clear();
        return false;
    }

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
internal record PlayerSummary(Guid Id, string Name, string Seat, int Score, int HandCount);
internal record TileSummary(Guid Id, string Label);
internal record DiscardSummary(Guid Id, Guid PlayerId, Guid TileId, int Sequence, string TileLabel);
internal record MeldSummary(Guid Id, Guid PlayerId, string Type, IReadOnlyList<TileSummary> Tiles);
internal record GameStateResponse(
    Guid Id,
    string Status,
    Guid? CurrentPlayerId,
    Guid? DealerId,
    int Round,
    string? RoundWind,
    string Ruleset,
    int WallTotal,
    DateTimeOffset CreatedAt,
    IReadOnlyList<PlayerSummary> Players,
    int TilesInPlay,
    int StateRevision,
    IReadOnlyList<MeldSummary> Melds,
    IReadOnlyList<DiscardSummary> Discards,
    IReadOnlyList<TileSummary> ViewerHand,
    string TurnPhase,
    IReadOnlyList<string> LegalActions,
    Guid? LastDiscardTileId,
    Guid? LastDiscardPlayerId);

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
internal record WinRequest(
    Guid PlayerId,
    bool IsSelfDraw = true,
    Guid? DiscarderPlayerId = null,
    int? BirdRank = null,
    int[]? BirdRanks = null,
    bool IsMultipleWin = false,
    bool IsInitialDeal = false,
    bool IsHeavenly = false,
    bool IsEarthly = false,
    bool IsLastTileDraw = false,
    bool IsLastTileDiscard = false,
    bool IsWinAfterKong = false,
    bool IsRobbingKong = false,
    bool IsKongOnCannon = false,
    bool IsFullyReliant = false);
internal record PlayerScoreDelta(Guid PlayerId, int Delta, int NewScore);
internal record WinResponse(
    Guid GameId,
    string Status,
    Guid PlayerId,
    IReadOnlyList<string>? ChangshaWinTypes = null,
    bool? IsBigWin = null,
    bool? IsSmallWin = null,
    IReadOnlyList<PlayerScoreDelta>? ScoreDeltas = null,
    Guid? NextDealerId = null,
    int? NextRound = null,
    string? BirdSeat = null);
