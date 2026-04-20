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
            StateVersion = 1,
            StateJson = serializer.Serialize(state),
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow,
            LastActionUtc = null
        };

        db.TableSessions.Add(session);
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
    ITableStateSerializer serializer,
    CancellationToken cancellationToken) =>
{
    var session = await db.TableSessions.FirstOrDefaultAsync(table => table.Id == id, cancellationToken);
    if (session is null)
    {
        return Results.NotFound();
    }

    var state = serializer.Deserialize(session.StateJson);
    return Results.Ok(session.ToDto(state));
});

app.MapPost("/api/tables/{id:guid}/bots/advance", async (
    Guid id,
    AdvanceBotsRequest request,
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
    var result = engine.AdvanceBots(state, request.MaxActions);

    session.StateJson = serializer.Serialize(state);
    session.StateVersion += 1;
    session.UpdatedUtc = DateTime.UtcNow;
    session.LastActionUtc = result.Actions.Count == 0 ? session.LastActionUtc : result.Actions[^1].OccurredUtc;

    await db.SaveChangesAsync(cancellationToken);

    var tableDto = session.ToDto(state);
    return Results.Ok(new AdvanceBotsResponse(tableDto, result.Actions, result.StopReason));
});

app.MapPost("/api/tables/{id:guid}/actions/discard", async (
    Guid id,
    DiscardActionRequest request,
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

    try
    {
        var result = engine.ApplyHumanDiscard(state, request.SeatIndex, request.TileId);

        session.StateJson = serializer.Serialize(state);
        session.StateVersion += 1;
        session.UpdatedUtc = DateTime.UtcNow;
        session.LastActionUtc = result.DrawAction?.OccurredUtc ?? result.DiscardAction.OccurredUtc;

        await db.SaveChangesAsync(cancellationToken);

        var tableDto = session.ToDto(state);
        return Results.Ok(new DiscardActionResponse(tableDto, result.DiscardAction, result.DrawAction));
    }
    catch (InvalidOperationException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});

app.Run();
