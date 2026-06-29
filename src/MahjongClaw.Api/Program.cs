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

var pendingClaimWindows = new Dictionary<Guid, PendingClaimWindow>();
var missedWinLocks = new Dictionary<Guid, Dictionary<Guid, MissedWinLock>>();
var gameRngs = new Dictionary<Guid, Random>(); // Game-specific RNG for deterministic dealer selection

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
    
    // B1: Validate bird ranks
    if (request?.BirdRank is < 1 or > 9)
    {
        return Results.BadRequest(new { message = "BirdRank must be between 1 and 9." });
    }
    
    if (request?.BirdRanks is not null && request.BirdRanks.Any(rank => rank is < 1 or > 9))
    {
        return Results.BadRequest(new { message = "All BirdRanks must be between 1 and 9." });
    }
    
    if (request?.BirdRanks is not null && request?.BirdRank is not null)
    {
        return Results.BadRequest(new { message = "Cannot specify both BirdRank and BirdRanks; use BirdRanks for multi-bird scenarios." });
    }
    
    var game = new GameEntity
    {
        Id = Guid.NewGuid(),
        Status = "Waiting",
        Round = 1,
        Ruleset = ruleset.ToString(),
        BirdRank = request?.BirdRank,
        BirdRanks = request?.BirdRanks is not null ? System.Text.Json.JsonSerializer.Serialize(request.BirdRanks) : null
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
        gameRngs[game.Id] = rng; // Store RNG for later use (dealer selection on multi-winner)
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
    await ExecuteBotTurnChainAsync(game, dbContext, hubContext, missedWinLocks, cancellationToken);
    return Results.Ok(response);
});

gameGroup.MapGet("/{id:guid}/history", async (
    Guid id,
    MahjongClawDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var game = await dbContext.Games
        .AsNoTracking()
        .Include(g => g.Players)
        .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
    
    if (game is null)
    {
        return Results.NotFound();
    }
    
    var handResults = await dbContext.HandResults
        .AsNoTracking()
        .Include(hr => hr.Winners)
        .Include(hr => hr.SeatDeltas)
        .Where(hr => hr.GameId == id)
        .OrderBy(hr => hr.HandNumber)
        .ToListAsync(cancellationToken);
    
    var playerNameMap = game.Players.ToDictionary(p => p.Id, p => p.Name);
    
    var history = handResults.Select(hr =>
    {
        var winners = hr.Winners.Select(w => new
        {
            PlayerName = playerNameMap.TryGetValue(w.PlayerId, out var name) ? name : "Unknown",
            WinTypes = System.Text.Json.JsonSerializer.Deserialize<List<string>>(w.WinTypesJson) ?? [],
            IsBigWin = w.IsBigWin,
            ScoreDelta = hr.SeatDeltas.FirstOrDefault(sd => sd.PlayerId == w.PlayerId)?.Delta ?? 0
        }).ToList();
        
        var totalDeltas = hr.SeatDeltas.ToDictionary(
            sd => playerNameMap.TryGetValue(sd.PlayerId, out var name) ? name : "Unknown",
            sd => sd.NewScore);
        
        return new
        {
            HandNumber = hr.HandNumber,
            CompletedAtUtc = hr.CompletedAtUtc,
            DealerName = playerNameMap.TryGetValue(hr.DealerId, out var dealerName) ? dealerName : "Unknown",
            RoundWind = hr.RoundWind,
            Winners = winners,
            WallExhausted = hr.WallExhausted,
            TotalDeltas = totalDeltas
        };
    }).ToList();
    
    return Results.Ok(history);
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
        await CompleteDrawnGameAsync(game, request.PlayerId, dbContext, hubContext, cancellationToken);
        return Results.Conflict(new { message = "No tiles left in wall. Hand ends in a draw." });
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

    await dbContext.SaveChangesAsync(cancellationToken);

    var response = new DiscardTileResponse(discard.Id, tile.Id, discard.Sequence);
    await SendToGameWithAliases(hubContext, game.Id, "TileDiscarded", response, cancellationToken);

    // Clear missed-win lock when player discards (their turn is over)
    ClearMissedWinLock(game.Id, request.PlayerId, missedWinLocks);

    await OpenClaimWindowAsync(game, tile, request.PlayerId, dbContext, hubContext, pendingClaimWindows, missedWinLocks, gameRngs, cancellationToken);
    return Results.Ok(response);
});

gameGroup.MapPost("/{id:guid}/claim", async (
    Guid id,
    ClaimSubmissionRequest request,
    MahjongClawDbContext dbContext,
    IHubContext<GameHub> hubContext,
    CancellationToken cancellationToken) =>
{
    var game = await dbContext.Games
        .Include(g => g.Players)
        .Include(g => g.Tiles)
        .Include(g => g.Melds)
        .Include(g => g.Discards)
        .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

    if (game is null)
    {
        return Results.NotFound();
    }

    if (!pendingClaimWindows.TryGetValue(game.Id, out var claimWindow))
    {
        return Results.Conflict(new { message = "No active claim window." });
    }

    if (!claimWindow.EligiblePlayerIds.Contains(request.PlayerId))
    {
        return Results.Conflict(new { message = "Player is not eligible to claim." });
    }

    if (claimWindow.SubmittedClaims.Any(c => c.PlayerId == request.PlayerId))
    {
        return Results.Conflict(new { message = "Player has already submitted a claim." });
    }

    List<Tile>? meldTiles = null;
    if (request.MeldTileIds is not null && request.MeldTileIds.Count > 0)
    {
        var tileEntities = game.Tiles
            .Where(t => request.MeldTileIds.Contains(t.Id) && 
                       t.PlayerId == request.PlayerId &&
                       string.Equals(t.Location, "Hand", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!TryBuildTileHand(tileEntities, out var tiles))
        {
            return Results.BadRequest(new { message = "Invalid meld tiles." });
        }
        meldTiles = tiles;
    }

    var claimRequest = new ClaimRequest(
        request.PlayerId,
        request.ClaimType,
        meldTiles,
        request.WinContext);

    claimWindow.SubmittedClaims.Add(claimRequest);

    await SendToGameWithAliases(hubContext, game.Id, "ClaimSubmitted", 
        new { PlayerId = request.PlayerId, ClaimType = request.ClaimType.ToString() }, 
        cancellationToken);

    if (claimWindow.HasAllResponses() || claimWindow.IsExpired(DateTime.UtcNow))
    {
        await ResolveClaimWindowAsync(game, claimWindow, dbContext, hubContext, pendingClaimWindows, missedWinLocks, gameRngs, cancellationToken);
    }

    return Results.Ok(new { success = true });
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

    var botTurnResult = await ExecuteBotTurnAsync(game, dbContext, hubContext, missedWinLocks, cancellationToken);
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

    if (isDiscardWinClaim && claimableDiscardTile is not null)
    {
        var discardedTile = tileHand[^1];
        if (IsBlockedByMissedWinLock(game.Id, request.PlayerId, discardedTile, missedWinLocks))
        {
            return Results.Conflict(new { message = "Win is blocked by missed-win lock." });
        }
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
                var meldSource = ChangshaMeldSourceResolver.Resolve(meld.Type, meld.Kind);
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

        // B1: Use bird ranks from request if provided, otherwise from game entity
        int? birdRank = request.BirdRank ?? game.BirdRank;
        int[]? birdRanks = request.BirdRanks;
        if (birdRanks is null && game.BirdRanks is not null)
        {
            try
            {
                birdRanks = System.Text.Json.JsonSerializer.Deserialize<int[]>(game.BirdRanks);
            }
            catch
            {
                // Ignore deserialization errors, keep null
            }
        }

        var settlementRequest = new ChangshaSettlementRequest(
            settlementSeats,
            WinnerId: request.PlayerId,
            DealerId: game.DealerId ?? request.PlayerId,
            IsSelfDraw: request.IsSelfDraw,
            IsBigWin: changshaEvaluation.IsBigWin,
            DiscarderId: resolvedDiscarderPlayerId,
            BirdRank: birdRanks is null ? birdRank : null,
            BirdRanks: birdRanks,
            IsMultipleWin: request.IsMultipleWin,
            WinTypes: changshaEvaluation.WinTypes);

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

    // Build Winners list for single-winner (for client consistency and M8 persistence)
    List<WinnerDetail>? winners = null;
    if (changshaEvaluation is not null && scoreDeltas is not null)
    {
        var winnerPlayer = game.Players.First(p => p.Id == request.PlayerId);
        var winnerDelta = scoreDeltas.First(sd => sd.PlayerId == request.PlayerId);
        var isDealer = game.DealerId == request.PlayerId;
        winners = [new WinnerDetail(
            PlayerId: request.PlayerId,
            PlayerName: winnerPlayer.Name,
            WinTypes: changshaEvaluation.WinTypes.Select(wt => wt.ToString()).ToList(),
            ScoreDelta: winnerDelta.Delta,
            IsBigWin: changshaEvaluation.IsBigWin,
            IsDealer: isDealer)];
    }

    game.Status = "Completed";
    game.CurrentPlayerId = request.PlayerId;

    await dbContext.SaveChangesAsync(cancellationToken);
    
    // M8: Persist hand result after scores updated
    if (scoreDeltas is not null)
    {
        await PersistHandResultAsync(
            game,
            dbContext,
            winners,
            scoreDeltas,
            request.IsSelfDraw,
            resolvedDiscarderPlayerId,
            wallExhausted: false,
            cancellationToken);
    }

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
        birdSeat,
        winners);
    await SendToGameWithAliases(hubContext, game.Id, "GameWon", response, cancellationToken);
    return Results.Ok(response);
});

app.Run();

static async Task CompleteDrawnGameAsync(
    GameEntity game,
    Guid? finalPlayerId,
    MahjongClawDbContext dbContext,
    IHubContext<GameHub> hubContext,
    CancellationToken cancellationToken)
{
    if (string.Equals(game.Status, "Completed", StringComparison.OrdinalIgnoreCase))
    {
        return;
    }

    game.Status = "Completed";

    if (finalPlayerId is not null)
    {
        game.CurrentPlayerId = finalPlayerId;
    }

    await dbContext.SaveChangesAsync(cancellationToken);
    
    // M8: Persist wall-exhausted hand result with zero deltas
    var scoreDeltas = game.Players
        .Select(p => new PlayerScoreDelta(p.Id, 0, p.Score))
        .ToList();
    
    await PersistHandResultAsync(
        game,
        dbContext,
        winners: null,
        scoreDeltas,
        isSelfDraw: false,
        discarderId: null,
        wallExhausted: true,
        cancellationToken);
    
    await SendToGameWithAliases(hubContext, game.Id, "GameStateUpdated", new { Reason = "WallExhausted" }, cancellationToken);
}

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
    Dictionary<Guid, Dictionary<Guid, MissedWinLock>> missedWinLocks,
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

        var result = await ExecuteBotTurnAsync(game, dbContext, hubContext, missedWinLocks, cancellationToken);
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
    Dictionary<Guid, Dictionary<Guid, MissedWinLock>> missedWinLocks,
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
            MissedWinLock? missedWinLock = null;
            missedWinLocks.TryGetValue(game.Id, out var gameLocks);
            gameLocks?.TryGetValue(botPlayer.Id, out missedWinLock);
            if (BotClaimAdvisor.ShouldClaimDiscardWin(botConcealedHand, botMeldHand, discardedTile, ruleset, missedWinLock))
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
            await CompleteDrawnGameAsync(game, botPlayer.Id, dbContext, hubContext, cancellationToken);
            return BotTurnExecutionResult.Ok(new BotTurnResponse(null, null, null, null, false));
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

    var opponentMeldTileViews = new List<OpponentMeldTileView>(visibleMeldTileEntities.Count);
    foreach (var tileEntity in visibleMeldTileEntities)
    {
        if (tileEntity.PlayerId is null
            || tileEntity.MeldId is null
            || !TryBuildTileHand([tileEntity], out var tileHand)
            || tileHand.Count != 1)
        {
            return BotTurnExecutionResult.Fail(409, "Bot visible meld tiles contain invalid tile/player/meld data.");
        }

        opponentMeldTileViews.Add(new OpponentMeldTileView(tileEntity.PlayerId.Value, tileEntity.MeldId.Value, tileHand[0]));
    }

    var opponentKongThreatTiles = OpponentMeldThreatAnalyzer.BuildPromotableKongThreatTiles(
        opponentMeldTileViews,
        new HashSet<Guid> { botPlayer.Id });

    if (turnState.HasAction(TurnAction.Meld))
    {
        if (BotMeldAdvisor.TrySelectPromotedKong(botHand, botMeldHandForDiscard, visibleTiles, ruleset, out var promotedKongTile))
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
                return await ExecuteBotTurnAsync(game, dbContext, hubContext, missedWinLocks, cancellationToken);
            }
        }

        if (BotMeldAdvisor.TrySelectConcealedKong(botHand, botMeldHandForDiscard, visibleTiles, ruleset, out var kongTile))
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
                    return await ExecuteBotTurnAsync(game, dbContext, hubContext, missedWinLocks, cancellationToken);
                }
            }
        }
    }

    var discardIndex = BotDiscardAdvisor.SelectDiscardIndex(botHand, botMeldHandForDiscard, visibleTiles, opponentKongThreatTiles, ruleset);
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
            var meldSource = ChangshaMeldSourceResolver.Resolve(meld.Type, meld.Kind);
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

    int? birdRank = game.BirdRank;
    int[]? birdRanks = null;
    if (game.BirdRanks is not null)
    {
        try
        {
            birdRanks = System.Text.Json.JsonSerializer.Deserialize<int[]>(game.BirdRanks);
        }
        catch
        {
            // Ignore deserialization errors, fallback to single BirdRank
        }
    }

    var settlementRequest = new ChangshaSettlementRequest(
        settlementSeats,
        WinnerId: winnerId,
        DealerId: game.DealerId ?? winnerId,
        IsSelfDraw: isSelfDraw,
        IsBigWin: changshaEvaluation.IsBigWin,
        DiscarderId: isSelfDraw ? null : discarderId,
        BirdRank: birdRanks is null ? birdRank : null,
        BirdRanks: birdRanks,
        WinTypes: changshaEvaluation.WinTypes);

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

static async Task OpenClaimWindowAsync(
    GameEntity game,
    TileEntity discardedTile,
    Guid discarderId,
    MahjongClawDbContext dbContext,
    IHubContext<GameHub> hubContext,
    Dictionary<Guid, PendingClaimWindow> pendingClaimWindows,
    Dictionary<Guid, Dictionary<Guid, MissedWinLock>> missedWinLocks,
    Dictionary<Guid, Random> gameRngs,
    CancellationToken cancellationToken)
{
    var allPlayerIds = game.Players.Select(p => p.Id).ToList();
    var eligiblePlayerIds = allPlayerIds.Where(id => id != discarderId).ToList();

    if (eligiblePlayerIds.Count == 0)
    {
        var currentPlayer = game.Players.FirstOrDefault(p => p.Id == discarderId);
        if (currentPlayer is not null)
        {
            var nextPlayerId = GetNextPlayerId(game, currentPlayer);
            if (nextPlayerId is not null)
            {
                game.CurrentPlayerId = nextPlayerId;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        await ExecuteBotTurnChainAsync(game, dbContext, hubContext, missedWinLocks, cancellationToken);
        return;
    }

    if (!TryBuildTileHand(new[] { discardedTile }, out var tiles) || tiles.Count == 0)
    {
        var currentPlayer = game.Players.FirstOrDefault(p => p.Id == discarderId);
        if (currentPlayer is not null)
        {
            var nextPlayerId = GetNextPlayerId(game, currentPlayer);
            if (nextPlayerId is not null)
            {
                game.CurrentPlayerId = nextPlayerId;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        await ExecuteBotTurnChainAsync(game, dbContext, hubContext, missedWinLocks, cancellationToken);
        return;
    }

    var botPlayerCount = game.Players.Count(p => IsBotPlayer(p));
    var timeoutMs = botPlayerCount >= 3 ? 250 : 3000;

    var claimWindow = new PendingClaimWindow
    {
        DiscardedTileId = discardedTile.Id,
        DiscardedTile = tiles[0],
        DiscarderId = discarderId,
        EligiblePlayerIds = eligiblePlayerIds,
        OpenedAtUtc = DateTime.UtcNow,
        TimeoutMs = timeoutMs
    };

    pendingClaimWindows[game.Id] = claimWindow;

    await SendToGameWithAliases(hubContext, game.Id, "ClaimWindowOpened", new
    {
        DiscardedTileId = discardedTile.Id,
        DiscarderId = discarderId,
        EligiblePlayerIds = eligiblePlayerIds,
        TimeoutMs = timeoutMs
    }, cancellationToken);

    _ = Task.Run(async () =>
    {
        await Task.Delay(timeoutMs, CancellationToken.None);
        
        if (pendingClaimWindows.TryGetValue(game.Id, out var window) && 
            window.DiscardedTileId == discardedTile.Id)
        {
            using var scope = dbContext.Database.BeginTransaction();
            try
            {
                var freshGame = await dbContext.Games
                    .Include(g => g.Players)
                    .Include(g => g.Tiles)
                    .Include(g => g.Melds)
                    .Include(g => g.Discards)
                    .FirstOrDefaultAsync(g => g.Id == game.Id, CancellationToken.None);

                if (freshGame is not null)
                {
                    await ResolveClaimWindowAsync(freshGame, window, dbContext, hubContext, pendingClaimWindows, missedWinLocks, gameRngs, CancellationToken.None);
                    await scope.CommitAsync(CancellationToken.None);
                }
            }
            catch
            {
                await scope.RollbackAsync(CancellationToken.None);
            }
        }
    }, CancellationToken.None);

    await ProcessBotClaimsAsync(game, claimWindow, dbContext, hubContext, pendingClaimWindows, missedWinLocks, gameRngs, cancellationToken);
}

static async Task ProcessBotClaimsAsync(
    GameEntity game,
    PendingClaimWindow claimWindow,
    MahjongClawDbContext dbContext,
    IHubContext<GameHub> hubContext,
    Dictionary<Guid, PendingClaimWindow> pendingClaimWindows,
    Dictionary<Guid, Dictionary<Guid, MissedWinLock>> missedWinLocks,
    Dictionary<Guid, Random> gameRngs,
    CancellationToken cancellationToken)
{
    foreach (var botPlayerId in claimWindow.EligiblePlayerIds)
    {
        var botPlayer = game.Players.FirstOrDefault(p => p.Id == botPlayerId);
        if (botPlayer is null || !IsBotPlayer(botPlayer))
        {
            continue;
        }

        var handTiles = game.Tiles
            .Where(t => t.PlayerId == botPlayerId && 
                       string.Equals(t.Location, "Hand", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var meldTiles = game.Tiles
            .Where(t => t.PlayerId == botPlayerId && 
                       string.Equals(t.Location, "Meld", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!TryBuildTileHand(handTiles, out var hand) || !TryBuildTileHand(meldTiles, out var melds))
        {
            continue;
        }

        var ruleset = RulesetResolver.Resolve(game.Ruleset);
        MissedWinLock? missedWinLock = null;
        missedWinLocks.TryGetValue(game.Id, out var gameLocks);
        gameLocks?.TryGetValue(botPlayerId, out missedWinLock);

        var shouldClaimWin = BotClaimAdvisor.ShouldClaimDiscardWin(
            hand,
            melds,
            claimWindow.DiscardedTile,
            ruleset,
            missedWinLock);

        if (shouldClaimWin)
        {
            var winClaim = new ClaimRequest(botPlayerId, ClaimType.Win, null, null);
            claimWindow.SubmittedClaims.Add(winClaim);
            
            await SendToGameWithAliases(hubContext, game.Id, "ClaimSubmitted", 
                new { PlayerId = botPlayerId, ClaimType = "Win" }, 
                cancellationToken);
        }
        else
        {
            var passClaim = new ClaimRequest(botPlayerId, ClaimType.Pass);
            claimWindow.SubmittedClaims.Add(passClaim);
            
            await SendToGameWithAliases(hubContext, game.Id, "ClaimSubmitted", 
                new { PlayerId = botPlayerId, ClaimType = "Pass" }, 
                cancellationToken);
        }
    }

    if (claimWindow.HasAllResponses())
    {
        await ResolveClaimWindowAsync(game, claimWindow, dbContext, hubContext, pendingClaimWindows, missedWinLocks, gameRngs, cancellationToken);
    }
}

static async Task ResolveClaimWindowAsync(
    GameEntity game,
    PendingClaimWindow claimWindow,
    MahjongClawDbContext dbContext,
    IHubContext<GameHub> hubContext,
    Dictionary<Guid, PendingClaimWindow> pendingClaimWindows,
    Dictionary<Guid, Dictionary<Guid, MissedWinLock>> missedWinLocks,
    Dictionary<Guid, Random> gameRngs,
    CancellationToken cancellationToken)
{
    pendingClaimWindows.Remove(game.Id);

    var playerSeats = game.Players.ToDictionary(
        p => p.Id,
        p => Enum.Parse<Wind>(p.Seat, true));

    // Process missed-win locks for players who passed on a winning opportunity
    await ProcessMissedWinLocksAsync(game, claimWindow, dbContext, missedWinLocks);

    var resolvedClaims = ClaimArbitrator.ResolveMultiWin(
        claimWindow.SubmittedClaims,
        claimWindow.DiscarderId,
        playerSeats);

    if (resolvedClaims.Count == 0)
    {
        await SendToGameWithAliases(hubContext, game.Id, "ClaimResolved", 
            new { Result = "NoClaim" }, 
            cancellationToken);

        var discarder = game.Players.FirstOrDefault(p => p.Id == claimWindow.DiscarderId);
        if (discarder is not null)
        {
            var nextPlayerId = GetNextPlayerId(game, discarder);
            if (nextPlayerId is not null)
            {
                game.CurrentPlayerId = nextPlayerId;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        await ExecuteBotTurnChainAsync(game, dbContext, hubContext, missedWinLocks, cancellationToken);
        return;
    }

    var winClaims = resolvedClaims.Where(c => c.ClaimType == ClaimType.Win).ToList();
    
    if (winClaims.Count > 0)
    {
        // Filter out locked winners (M7 interaction)
        var unlockedWinClaims = new List<ClaimRequest>();
        foreach (var winClaim in winClaims)
        {
            if (IsBlockedByMissedWinLock(game.Id, winClaim.PlayerId, claimWindow.DiscardedTile, missedWinLocks))
            {
                // Winner is locked, exclude from winners list
                continue;
            }
            unlockedWinClaims.Add(winClaim);
        }

        if (unlockedWinClaims.Count == 0)
        {
            // All winners were locked
            await SendToGameWithAliases(hubContext, game.Id, "ClaimResolved", 
                new { Result = "BlockedByMissedWinLock", Message = "All winning claims blocked by missed-win locks." }, 
                cancellationToken);

            var discarder = game.Players.FirstOrDefault(p => p.Id == claimWindow.DiscarderId);
            if (discarder is not null)
            {
                var nextPlayerId = GetNextPlayerId(game, discarder);
                if (nextPlayerId is not null)
                {
                    game.CurrentPlayerId = nextPlayerId;
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }

            await ExecuteBotTurnChainAsync(game, dbContext, hubContext, missedWinLocks, cancellationToken);
            return;
        }

        // Execute multi-winner settlement
        await ExecuteMultiWinClaimAsync(game, unlockedWinClaims, claimWindow, dbContext, hubContext, gameRngs, missedWinLocks, cancellationToken);
        return;
    }

    // Single non-win claim (Pung/Kong/Chow)
    var singleClaim = resolvedClaims[0];

    if (singleClaim.ClaimType == ClaimType.Chow)
    {
        if (!ChowEligibilityRule.IsEligibleForChow(singleClaim.PlayerId, claimWindow.DiscarderId, playerSeats))
        {
            await SendToGameWithAliases(hubContext, game.Id, "ClaimResolved", 
                new { Result = "InvalidChowSeat", Message = "Chow can only be claimed from immediate prior player." }, 
                cancellationToken);

            var discarder = game.Players.FirstOrDefault(p => p.Id == claimWindow.DiscarderId);
            if (discarder is not null)
            {
                var nextPlayerId = GetNextPlayerId(game, discarder);
                if (nextPlayerId is not null)
                {
                    game.CurrentPlayerId = nextPlayerId;
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }

            await ExecuteBotTurnChainAsync(game, dbContext, hubContext, missedWinLocks, cancellationToken);
            return;
        }
    }

    await SendToGameWithAliases(hubContext, game.Id, "ClaimResolved", new
    {
        Result = "ClaimExecuted",
        PlayerId = singleClaim.PlayerId,
        ClaimType = singleClaim.ClaimType.ToString()
    }, cancellationToken);

    await ExecuteMeldClaimAsync(game, singleClaim, claimWindow, dbContext, hubContext, missedWinLocks, cancellationToken);
}

static async Task ExecuteMultiWinClaimAsync(
    GameEntity game,
    IReadOnlyList<ClaimRequest> winClaims,
    PendingClaimWindow claimWindow,
    MahjongClawDbContext dbContext,
    IHubContext<GameHub> hubContext,
    Dictionary<Guid, Random> gameRngs,
    Dictionary<Guid, Dictionary<Guid, MissedWinLock>> missedWinLocks,
    CancellationToken cancellationToken)
{
    // Move discarded tile to first winner's hand (physical ownership)
    var discardedTileEntity = game.Tiles.FirstOrDefault(t => t.Id == claimWindow.DiscardedTileId);
    if (discardedTileEntity is null)
    {
        return;
    }

    discardedTileEntity.Location = "Hand";
    discardedTileEntity.PlayerId = winClaims[0].PlayerId;
    await dbContext.SaveChangesAsync(cancellationToken);

    // Get RNG for this game
    if (!gameRngs.TryGetValue(game.Id, out var rng))
    {
        rng = new Random();
        gameRngs[game.Id] = rng;
    }

    // Build settlement seats
    if (!TryBuildChangshaSettlementSeats(game.Players, out var settlementSeats, out var seatBuildError))
    {
        return;
    }

    // Evaluate each winner's hand
    var winners = new List<ChangshaWinnerInfo>();
    var winnerDetails = new List<WinnerDetail>(); // For response DTO
    var ruleset = RulesetResolver.Resolve(game.Ruleset);

    foreach (var winClaim in winClaims)
    {
        // Fetch winner's tiles
        var winnerTiles = game.Tiles
            .Where(tile => tile.PlayerId == winClaim.PlayerId && (tile.Location == "Hand" || tile.Location == "Meld"))
            .ToList();

        // Add discarded tile for evaluation
        var evaluationTiles = winnerTiles.Append(discardedTileEntity).ToList();

        if (!TryBuildTileHand(evaluationTiles, out var tileHand))
        {
            continue; // Skip invalid hand
        }

        if (!RulesetHandAnalyzer.TryGetWinningHand(tileHand, ruleset, out var winningHand) || winningHand is null)
        {
            continue; // Skip non-winning hand
        }

        // Detect fully reliant per winner
        var isFullyReliant = false;
        var playerMelds = game.Melds.Where(m => m.PlayerId == winClaim.PlayerId).ToList();
        
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
            
            var winningTileSource = TileSource.ClaimedDiscard; // Always discard for multi-winner
            isFullyReliant = ChangshaFullyReliantDetector.IsFullyReliant(meldInfos, winningTileSource);
        }
        else
        {
            // No melds, won from discard = fully reliant
            isFullyReliant = true;
        }

        // Server-side last-tile detection
        var wallRemaining = game.Tiles.Count(t => string.Equals(t.Location, "Wall", StringComparison.OrdinalIgnoreCase));
        var isLastTileDiscard = wallRemaining == 0;

        // Build win context (multi-winner is always discard-based)
        var winContext = new ChangshaWinContext(
            IsInitialDeal: false,
            IsHeavenly: false,
            IsEarthly: false,
            IsLastTileDraw: false,
            IsLastTileDiscard: isLastTileDiscard,
            IsWinAfterKong: false,
            IsRobbingKong: false,
            IsKongOnCannon: false,
            IsFullyReliant: isFullyReliant);

        // Evaluate win types
        var changshaEvaluation = ChangshaWinTypeEvaluator.Evaluate(tileHand, winningHand, winContext);

        // Add to winners list
        winners.Add(new ChangshaWinnerInfo(
            WinnerId: winClaim.PlayerId,
            WinTypes: changshaEvaluation.WinTypes,
            IsBigWin: changshaEvaluation.IsBigWin));

        // Store for response
        var winnerPlayer = game.Players.First(p => p.Id == winClaim.PlayerId);
        var isDealer = game.DealerId == winClaim.PlayerId;
        winnerDetails.Add(new WinnerDetail(
            PlayerId: winClaim.PlayerId,
            PlayerName: winnerPlayer.Name,
            WinTypes: changshaEvaluation.WinTypes.Select(wt => wt.ToString()).ToList(),
            ScoreDelta: 0, // Will be filled after settlement
            IsBigWin: changshaEvaluation.IsBigWin,
            IsDealer: isDealer));
    }

    if (winners.Count == 0)
    {
        return; // No valid winners
    }

    // Build multi-winner settlement request
    // B1: Read bird ranks from Game entity (configured at game start)
    int? birdRank = game.BirdRank;
    IReadOnlyList<int>? birdRanks = null;
    if (game.BirdRanks is not null)
    {
        try
        {
            birdRanks = System.Text.Json.JsonSerializer.Deserialize<int[]>(game.BirdRanks);
        }
        catch
        {
            // Ignore deserialization errors, fallback to single BirdRank
        }
    }
    
    var settlementRequest = new ChangshaSettlementRequestMulti(
        Seats: settlementSeats,
        Winners: winners,
        DealerId: game.DealerId ?? winClaims[0].PlayerId,
        IsSelfDraw: false, // Multi-winner is always discard-based
        DiscarderId: claimWindow.DiscarderId,
        BirdRank: birdRanks is null ? birdRank : null,
        BirdRanks: birdRanks);

    // Calculate multi-winner settlement
    var settlementResult = ChangshaSettlementCalculator.CalculateMultiWinner(settlementRequest, rng);

    // Apply score deltas
    foreach (var delta in settlementResult.ScoreDeltas)
    {
        var player = game.Players.First(candidate => candidate.Id == delta.PlayerId);
        player.Score = delta.NewScore;
    }

    // Update winner details with actual score deltas
    for (int i = 0; i < winnerDetails.Count; i++)
    {
        var detail = winnerDetails[i];
        var delta = settlementResult.ScoreDeltas.First(d => d.PlayerId == detail.PlayerId);
        winnerDetails[i] = detail with { ScoreDelta = delta.Delta };
    }

    // Update dealer and round wind
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

    // Clear M7 missed-win locks for all winners (their hands are fully resolved)
    foreach (var winClaim in winClaims)
    {
        ClearMissedWinLock(game.Id, winClaim.PlayerId, missedWinLocks);
    }

    game.Status = "Completed";
    game.CurrentPlayerId = winClaims[0].PlayerId; // First winner for backward compat

    await dbContext.SaveChangesAsync(cancellationToken);

    // Build response
    var scoreDeltas = settlementResult.ScoreDeltas
        .Select(delta => new PlayerScoreDelta(delta.PlayerId, delta.Delta, delta.NewScore))
        .ToList();
    
    // M8: Persist multi-winner hand result
    await PersistHandResultAsync(
        game,
        dbContext,
        winnerDetails,
        scoreDeltas,
        isSelfDraw: false,
        claimWindow.DiscarderId,
        wallExhausted: false,
        cancellationToken);

    var response = new WinResponse(
        GameId: game.Id,
        Status: game.Status,
        PlayerId: winClaims[0].PlayerId, // First winner for backward compat
        ChangshaWinTypes: winnerDetails[0].WinTypes,
        IsBigWin: winnerDetails[0].IsBigWin,
        IsSmallWin: !winnerDetails[0].IsBigWin,
        ScoreDeltas: scoreDeltas,
        NextDealerId: settlementResult.NextDealerId,
        NextRound: game.Round,
        BirdSeat: settlementResult.BirdSeat?.ToString(),
        Winners: winnerDetails);

    await SendToGameWithAliases(hubContext, game.Id, "GameWon", response, cancellationToken);
}

static async Task ExecuteMeldClaimAsync(
    GameEntity game,
    ClaimRequest claim,
    PendingClaimWindow claimWindow,
    MahjongClawDbContext dbContext,
    IHubContext<GameHub> hubContext,
    Dictionary<Guid, Dictionary<Guid, MissedWinLock>> missedWinLocks,
    CancellationToken cancellationToken)
{
    var discardedTileEntity = game.Tiles.FirstOrDefault(t => t.Id == claimWindow.DiscardedTileId);
    if (discardedTileEntity is null)
    {
        return;
    }

    discardedTileEntity.Location = "Meld";
    discardedTileEntity.PlayerId = claim.PlayerId;

    var meldTileEntities = new List<TileEntity> { discardedTileEntity };

    if (claim.MeldTiles is not null)
    {
        foreach (var claimTile in claim.MeldTiles)
        {
            var matchingTile = game.Tiles.FirstOrDefault(te =>
                te.PlayerId == claim.PlayerId &&
                string.Equals(te.Location, "Hand", StringComparison.OrdinalIgnoreCase) &&
                TileEntityMatches(te, claimTile));

            if (matchingTile is not null)
            {
                matchingTile.Location = "Meld";
                meldTileEntities.Add(matchingTile);
            }
        }
    }

    var meldType = claim.ClaimType switch
    {
        ClaimType.Chow => "Chow",
        ClaimType.Pung => "Pung",
        ClaimType.ExposedKong => "ExposedKong",
        _ => "Pung"
    };

    var meld = new MeldEntity
    {
        GameId = game.Id,
        PlayerId = claim.PlayerId,
        Type = meldType
    };

    game.Melds.Add(meld);
    dbContext.Melds.Add(meld);

    foreach (var tile in meldTileEntities)
    {
        tile.MeldId = meld.Id;
    }

    game.CurrentPlayerId = claim.PlayerId;
    await dbContext.SaveChangesAsync(cancellationToken);

    var response = new MeldResponse(
        meld.Id,
        meldType,
        meldTileEntities.Select(t => t.Id).ToList());

    await SendToGameWithAliases(hubContext, game.Id, "MeldDeclared", response, cancellationToken);

    // Clear missed-win lock when player makes a meld (hand changed)
    ClearMissedWinLock(game.Id, claim.PlayerId, missedWinLocks);
}

static async Task ProcessMissedWinLocksAsync(
    GameEntity game,
    PendingClaimWindow claimWindow,
    MahjongClawDbContext dbContext,
    Dictionary<Guid, Dictionary<Guid, MissedWinLock>> missedWinLocks)
{
    var ruleset = RulesetResolver.Resolve(game.Ruleset);

    foreach (var claim in claimWindow.SubmittedClaims)
    {
        if (claim.ClaimType != ClaimType.Pass)
        {
            continue;
        }

        var handTiles = game.Tiles
            .Where(t => t.PlayerId == claim.PlayerId && 
                       string.Equals(t.Location, "Hand", StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        var meldTiles = game.Tiles
            .Where(t => t.PlayerId == claim.PlayerId && 
                       string.Equals(t.Location, "Meld", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!TryBuildTileHand(handTiles, out var hand) || !TryBuildTileHand(meldTiles, out var melds))
        {
            continue;
        }

        var missedWinLock = MissedWinLockTracker.CreateLock(hand, melds, claimWindow.DiscardedTile, ruleset);
        
        if (missedWinLock is not null)
        {
            if (!missedWinLocks.ContainsKey(game.Id))
            {
                missedWinLocks[game.Id] = new Dictionary<Guid, MissedWinLock>();
            }
            
            missedWinLocks[game.Id][claim.PlayerId] = missedWinLock;
        }
    }
}

static bool IsBlockedByMissedWinLock(
    Guid gameId,
    Guid playerId,
    Tile discardedTile,
    Dictionary<Guid, Dictionary<Guid, MissedWinLock>> missedWinLocks)
{
    if (!missedWinLocks.TryGetValue(gameId, out var gameLocks))
    {
        return false;
    }

    if (!gameLocks.TryGetValue(playerId, out var playerLock))
    {
        return false;
    }

    return playerLock.IsLockedOnTile(discardedTile);
}

static void ClearMissedWinLock(
    Guid gameId,
    Guid playerId,
    Dictionary<Guid, Dictionary<Guid, MissedWinLock>> missedWinLocks)
{
    if (missedWinLocks.TryGetValue(gameId, out var gameLocks))
    {
        gameLocks.Remove(playerId);
        
        if (gameLocks.Count == 0)
        {
            missedWinLocks.Remove(gameId);
        }
    }
}

static async Task<int> GetNextHandNumberAsync(Guid gameId, MahjongClawDbContext dbContext, CancellationToken cancellationToken)
{
    var maxHandNumber = await dbContext.HandResults
        .Where(hr => hr.GameId == gameId)
        .Select(hr => (int?)hr.HandNumber)
        .MaxAsync(cancellationToken);
    
    return (maxHandNumber ?? 0) + 1;
}

static async Task PersistHandResultAsync(
    GameEntity game,
    MahjongClawDbContext dbContext,
    IReadOnlyList<WinnerDetail>? winners,
    IReadOnlyList<PlayerScoreDelta> scoreDeltas,
    bool isSelfDraw,
    Guid? discarderId,
    bool wallExhausted,
    CancellationToken cancellationToken)
{
    var handNumber = await GetNextHandNumberAsync(game.Id, dbContext, cancellationToken);
    var completedAtUtc = DateTimeOffset.UtcNow;
    
    var handResult = new HandResultEntity
    {
        Id = Guid.NewGuid(),
        GameId = game.Id,
        HandNumber = handNumber,
        CompletedAtUtc = completedAtUtc,
        DealerId = game.DealerId ?? Guid.Empty,
        RoundWind = game.RoundWind ?? "East",
        IsSelfDraw = isSelfDraw,
        DiscarderId = discarderId,
        WallExhausted = wallExhausted,
        CreatedAtUtc = completedAtUtc
    };
    
    dbContext.HandResults.Add(handResult);
    
    if (winners is not null)
    {
        foreach (var winner in winners)
        {
            var winnerEntity = new HandResultWinnerEntity
            {
                Id = Guid.NewGuid(),
                HandResultId = handResult.Id,
                PlayerId = winner.PlayerId,
                WinTypesJson = System.Text.Json.JsonSerializer.Serialize(winner.WinTypes),
                IsBigWin = winner.IsBigWin,
                IsDealer = winner.IsDealer,
                CreatedAtUtc = completedAtUtc
            };
            dbContext.HandResultWinners.Add(winnerEntity);
        }
    }
    
    foreach (var delta in scoreDeltas)
    {
        var category = "Winnings";
        if (wallExhausted && delta.Delta == 0)
        {
            category = "ZeroNoGame";
        }
        else if (delta.Delta < 0)
        {
            category = "Loss";
        }
        else if (delta.Delta > 0 && winners?.Any(w => w.PlayerId == delta.PlayerId) != true)
        {
            category = "BirdMatch";
        }
        
        var seatDelta = new HandResultSeatDeltaEntity
        {
            Id = Guid.NewGuid(),
            HandResultId = handResult.Id,
            PlayerId = delta.PlayerId,
            Delta = delta.Delta,
            NewScore = delta.NewScore,
            DeltaCategory = category,
            CreatedAtUtc = completedAtUtc
        };
        dbContext.HandResultSeatDeltas.Add(seatDelta);
    }
    
    await dbContext.SaveChangesAsync(cancellationToken);
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

internal record CreateGameRequest(
    string? HostName, 
    string? Mode, 
    int? BotSeatCount, 
    string? Ruleset = null,
    int? BirdRank = null,
    int[]? BirdRanks = null);
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
internal record ClaimSubmissionRequest(
    Guid PlayerId,
    ClaimType ClaimType,
    IReadOnlyList<Guid>? MeldTileIds = null,
    ChangshaWinContext? WinContext = null);
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
internal record WinnerDetail(
    Guid PlayerId,
    string PlayerName,
    IReadOnlyList<string> WinTypes,
    int ScoreDelta,
    bool IsBigWin,
    bool IsDealer);
internal record WinResponse(
    Guid GameId,
    string Status,
    Guid PlayerId, // First winner for backward compatibility
    IReadOnlyList<string>? ChangshaWinTypes = null,
    bool? IsBigWin = null,
    bool? IsSmallWin = null,
    IReadOnlyList<PlayerScoreDelta>? ScoreDeltas = null,
    Guid? NextDealerId = null,
    int? NextRound = null,
    string? BirdSeat = null,
    IReadOnlyList<WinnerDetail>? Winners = null); // New field for multi-winner support
