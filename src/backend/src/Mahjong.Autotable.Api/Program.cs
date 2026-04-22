using Mahjong.Autotable.Api.Data;
using Mahjong.Autotable.Api.Data.Entities;
using Mahjong.Autotable.Api.Persistence;
using Mahjong.Autotable.Api.Tables;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "data"));

builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddScoped<ITableStateEngine, TableStateEngine>();
builder.Services.AddScoped<ITableStateSerializer, TableStateSerializer>();
builder.Services.AddScoped<ITableSessionEventStore, TableSessionEventStore>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DatabaseBootstrapper.InitializeAsync(db);
}

app.UseDefaultFiles();
app.UseStaticFiles();

var autotablePath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "../../../frontend/autotable"));
if (Directory.Exists(autotablePath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(autotablePath),
        RequestPath = "/autotable"
    });
}

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", service = "mahjong-autotable-api" }));

app.MapGet("/api/system/persistence", (IConfiguration configuration) =>
{
    var provider = configuration.GetValue<string>("Persistence:Provider") ?? "Sqlite";
    return Results.Ok(new { provider });
});

app.MapPost("/api/tables", async (
    CreateTableRequest request,
    AppDbContext db,
    ITableStateEngine engine,
    ITableSessionEventStore eventStore,
    ITableStateSerializer serializer,
    CancellationToken cancellationToken) =>
{
    try
    {
        var ruleSet = string.IsNullOrWhiteSpace(request.RuleSet) ? "changsha" : request.RuleSet.Trim();
        var state = engine.CreateInitialState(request.BotSeatIndexes, request.Seed);
        var session = new TableSession
        {
            RuleSet = ruleSet,
            StateVersion = state.StateVersion,
            StateJson = serializer.Serialize(state),
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow,
            LastActionUtc = null
        };

        db.TableSessions.Add(session);
        await eventStore.PersistNewEventsAsync(session, state, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/tables/{session.Id}", session.ToDto(state));
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});

app.MapGet("/api/tables/{id:guid}", async (
    Guid id,
    AppDbContext db,
    ITableStateEngine engine,
    ITableStateSerializer serializer,
    CancellationToken cancellationToken) =>
{
    var session = await db.TableSessions.FirstOrDefaultAsync(table => table.Id == id, cancellationToken);
    if (session is null)
    {
        return Results.NotFound();
    }

    var state = serializer.Deserialize(session.StateJson);
    engine.NormalizePersistedState(state, session.StateVersion);
    return Results.Ok(session.ToDto(state));
});

app.MapGet("/api/tables/{id:guid}/view", async (
    Guid id,
    int? seatIndex,
    HttpContext httpContext,
    AppDbContext db,
    ITableStateEngine engine,
    ITableStateSerializer serializer,
    CancellationToken cancellationToken) =>
{
    var session = await db.TableSessions.FirstOrDefaultAsync(table => table.Id == id, cancellationToken);
    if (session is null)
    {
        return Results.NotFound();
    }

    var state = serializer.Deserialize(session.StateJson);
    engine.NormalizePersistedState(state, session.StateVersion);
    if (!seatIndex.HasValue)
    {
        var error = ToActionError(
            TableActionErrorCodes.InvalidSeat,
            "seatIndex query parameter is required.",
            state.StateVersion,
            state.ActionSequence,
            httpContext.TraceIdentifier);
        return Results.BadRequest(error);
    }

    try
    {
        return Results.Ok(session.ToSeatViewDto(state, seatIndex.Value));
    }
    catch (ArgumentOutOfRangeException)
    {
        var error = ToActionError(
            TableActionErrorCodes.SeatNotFound,
            $"Seat {seatIndex.Value} does not exist.",
            state.StateVersion,
            state.ActionSequence,
            httpContext.TraceIdentifier);
        return Results.BadRequest(error);
    }
});

app.MapGet("/api/tables/{id:guid}/events", async (
    Guid id,
    long? afterSequence,
    int? limit,
    AppDbContext db,
    ITableStateEngine engine,
    ITableStateSerializer serializer,
    ITableSessionEventStore eventStore,
    CancellationToken cancellationToken) =>
{
    var session = await db.TableSessions.FirstOrDefaultAsync(table => table.Id == id, cancellationToken);
    if (session is null)
    {
        return Results.NotFound();
    }

    var state = serializer.Deserialize(session.StateJson);
    engine.NormalizePersistedState(state, session.StateVersion);
    var events = await eventStore.GetEventsAsync(id, afterSequence, limit ?? 200, cancellationToken);

    return Results.Ok(new TableEventsResponse(
        id,
        state.StateVersion,
        state.ActionSequence,
        events.Select(evt => evt.ToDto()).ToList()));
});

app.MapPost("/api/tables/{id:guid}/bots/advance", async (
    Guid id,
    AdvanceBotsRequest request,
    HttpContext httpContext,
    AppDbContext db,
    ITableStateEngine engine,
    ITableSessionEventStore eventStore,
    ITableStateSerializer serializer,
    CancellationToken cancellationToken) =>
{
    var session = await db.TableSessions.FirstOrDefaultAsync(table => table.Id == id, cancellationToken);
    if (session is null)
    {
        return Results.NotFound();
    }

    var state = serializer.Deserialize(session.StateJson);
    engine.NormalizePersistedState(state, session.StateVersion);
    var integrity = engine.VerifyReplayIntegrity(state);
    if (!integrity.IntegrityMatch)
    {
        return Results.Conflict(ToIntegrityConflict(state, httpContext.TraceIdentifier, integrity));
    }

    BotAdvanceResult result;
    try
    {
        result = request.AdvanceUntilHumanTurnOrWallExhausted
            ? engine.AdvanceBotsUntilHumanTurnOrWallExhausted(state)
            : engine.AdvanceBots(state, request.MaxActions);
    }
    catch (TableRuleException exception)
    {
        var error = ToActionError(
            exception.Code,
            exception.Message,
            exception.StateVersion,
            exception.ActionSequence,
            httpContext.TraceIdentifier);
        return Results.BadRequest(error);
    }

    session.StateJson = serializer.Serialize(state);
    session.StateVersion = state.StateVersion;
    session.UpdatedUtc = DateTime.UtcNow;
    session.LastActionUtc = result.Actions.Count == 0 ? session.LastActionUtc : result.Actions[^1].OccurredUtc;
    await eventStore.PersistNewEventsAsync(session, state, cancellationToken);

    await db.SaveChangesAsync(cancellationToken);

    var tableDto = session.ToDto(state);
    return Results.Ok(new AdvanceBotsResponse(tableDto, result.Actions, result.StopReason));
});

app.MapPost("/api/tables/{id:guid}/actions/discard", async (
    Guid id,
    DiscardActionRequest request,
    HttpContext httpContext,
    AppDbContext db,
    ITableStateEngine engine,
    ITableSessionEventStore eventStore,
    ITableStateSerializer serializer,
    CancellationToken cancellationToken) =>
{
    var session = await db.TableSessions.FirstOrDefaultAsync(table => table.Id == id, cancellationToken);
    if (session is null)
    {
        return Results.NotFound();
    }

    var state = serializer.Deserialize(session.StateJson);
    engine.NormalizePersistedState(state, session.StateVersion);

    if (request.ExpectedStateVersion.HasValue && request.ExpectedStateVersion.Value != state.StateVersion)
    {
        var error = ToActionError(
            TableActionErrorCodes.ConcurrencyConflict,
            $"Expected state version {request.ExpectedStateVersion.Value}, current version is {state.StateVersion}.",
            state.StateVersion,
            state.ActionSequence,
            httpContext.TraceIdentifier);
        return Results.Conflict(error);
    }

    var integrity = engine.VerifyReplayIntegrity(state);
    if (!integrity.IntegrityMatch)
    {
        return Results.Conflict(ToIntegrityConflict(state, httpContext.TraceIdentifier, integrity));
    }

    try
    {
        var result = engine.ApplyHumanDiscard(state, request.SeatIndex, request.TileId);

        session.StateJson = serializer.Serialize(state);
        session.StateVersion = state.StateVersion;
        session.UpdatedUtc = DateTime.UtcNow;
        session.LastActionUtc = result.DrawAction?.OccurredUtc ?? result.DiscardAction.OccurredUtc;
        await eventStore.PersistNewEventsAsync(session, state, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        var tableDto = session.ToDto(state);
        return Results.Ok(new DiscardActionResponse(tableDto, result.DiscardAction, result.DrawAction));
    }
    catch (TableRuleException exception)
    {
        var error = ToActionError(
            exception.Code,
            exception.Message,
            exception.StateVersion,
            exception.ActionSequence,
            httpContext.TraceIdentifier);
        return Results.BadRequest(error);
    }
});

app.MapPost("/api/tables/{id:guid}/claims/resolve", async (
    Guid id,
    ResolveClaimRequest request,
    HttpContext httpContext,
    AppDbContext db,
    ITableStateEngine engine,
    ITableSessionEventStore eventStore,
    ITableStateSerializer serializer,
    CancellationToken cancellationToken) =>
{
    var session = await db.TableSessions.FirstOrDefaultAsync(table => table.Id == id, cancellationToken);
    if (session is null)
    {
        return Results.NotFound();
    }

    var state = serializer.Deserialize(session.StateJson);
    engine.NormalizePersistedState(state, session.StateVersion);

    if (request.ExpectedStateVersion.HasValue && request.ExpectedStateVersion.Value != state.StateVersion)
    {
        var error = ToActionError(
            TableActionErrorCodes.ConcurrencyConflict,
            $"Expected state version {request.ExpectedStateVersion.Value}, current version is {state.StateVersion}.",
            state.StateVersion,
            state.ActionSequence,
            httpContext.TraceIdentifier);
        return Results.Conflict(error);
    }

    var integrity = engine.VerifyReplayIntegrity(state);
    if (!integrity.IntegrityMatch)
    {
        return Results.Conflict(ToIntegrityConflict(state, httpContext.TraceIdentifier, integrity));
    }

    try
    {
        var result = engine.ResolveClaimWindow(state, request.Decision);

        session.StateJson = serializer.Serialize(state);
        session.StateVersion = state.StateVersion;
        session.UpdatedUtc = DateTime.UtcNow;
        session.LastActionUtc = result.DrawAction?.OccurredUtc ?? result.ResolutionAction.OccurredUtc;
        await eventStore.PersistNewEventsAsync(session, state, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        var tableDto = session.ToDto(state);
        return Results.Ok(new ResolveClaimResponse(tableDto, result.AppliedDecision, result.ResolutionAction, result.DrawAction));
    }
    catch (TableRuleException exception)
    {
        var error = ToActionError(
            exception.Code,
            exception.Message,
            exception.StateVersion,
            exception.ActionSequence,
            httpContext.TraceIdentifier);
        return Results.BadRequest(error);
    }
});

app.MapPost("/api/tables/{id:guid}/replay/verify", async (
    Guid id,
    bool strict,
    HttpContext httpContext,
    AppDbContext db,
    ITableStateEngine engine,
    ITableStateSerializer serializer,
    CancellationToken cancellationToken) =>
{
    var session = await db.TableSessions.FirstOrDefaultAsync(table => table.Id == id, cancellationToken);
    if (session is null)
    {
        return Results.NotFound();
    }

    var state = serializer.Deserialize(session.StateJson);
    engine.NormalizePersistedState(state, session.StateVersion);

    try
    {
        var verification = engine.VerifyReplayIntegrity(state);
        if (strict && !verification.IntegrityMatch)
        {
            return Results.Conflict(ToIntegrityConflict(state, httpContext.TraceIdentifier, verification));
        }

        var response = new ReplayVerificationResponse(
            session.ToDto(state),
            verification.IntegrityMatch,
            verification.ExpectedStateHash,
            verification.ReplayedStateHash,
            verification.ReplayedStateVersion,
            verification.ReplayedActionSequence);

        return Results.Ok(response);
    }
    catch (TableRuleException exception)
    {
        var error = ToActionError(
            exception.Code,
            exception.Message,
            exception.StateVersion,
            exception.ActionSequence,
            httpContext.TraceIdentifier);
        return Results.BadRequest(error);
    }
});

static TableActionError ToActionError(
    string code,
    string message,
    int stateVersion,
    long actionSequence,
    string correlationId) =>
    new(code, message, stateVersion, actionSequence, correlationId);

static TableActionError ToIntegrityConflict(
    TableGameState state,
    string correlationId,
    ReplayVerificationResult verification) =>
    ToActionError(
        TableActionErrorCodes.StateInvariantBroken,
        $"Replay integrity mismatch. expected={verification.ExpectedStateHash}, replayed={verification.ReplayedStateHash}.",
        state.StateVersion,
        state.ActionSequence,
        correlationId);

app.Run();

public partial class Program
{
}
