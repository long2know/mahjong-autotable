using Mahjong.Autotable.Api.Autotable;
using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Changsha.Patterns;
using Mahjong.Autotable.Api.Changsha.Runtime;
using Mahjong.Autotable.Api.Data;
using Mahjong.Autotable.Api.Matchmaking;
using Mahjong.Autotable.Api.Persistence;
using Mahjong.Autotable.Api.Players;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

// Phase J Wave 3 — Apone's Docker HEALTHCHECK / Linux deploy needs a stable
// process-uptime anchor. Captured at module load (before WebApplication build)
// so the value reflects the actual host process start, not the time of the
// first /health request.
var processStartTime = DateTimeOffset.UtcNow;

var builder = WebApplication.CreateBuilder(args);
Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "data"));

builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSignalR();

// Phase J Wave 5 — MVC controllers for the matchmaking REST endpoint
// (MatchmakingController owns GET /api/matchmaking/lobby).
builder.Services.AddControllers();

builder.Services.Configure<ChangshaRuntimeOptions>(builder.Configuration.GetSection("ChangshaRuntime"));
builder.Services.AddSingleton<IChangshaGameRuntime, ChangshaGameRuntime>();
builder.Services.AddSingleton<AutotableConnectionManager>();

// Phase J Wave 5 — player profile + matchmaking services. Singleton-scoped so
// they share the runtime's lifetime and use IServiceScopeFactory for DB scopes.
builder.Services.AddSingleton<PlayerProfileService>();
builder.Services.AddSingleton<MatchmakingService>();

const string ChangshaCorsPolicy = "ChangshaCors";
builder.Services.AddCors(options =>
{
    // Phase H Wave 1 — the `modern/` Vite frontend (localhost:5173) was deleted in
    // Phase A; the remaining origins are the Kestrel HTTP/HTTPS endpoints used by
    // the in-tree `frontend/autotable/` bundle and the SignalR ChangshaHub clients.
    options.AddPolicy(ChangshaCorsPolicy, policy => policy
        .WithOrigins(
            "http://localhost:5114",
            "https://localhost:7135")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

var app = builder.Build();

app.UseCors(ChangshaCorsPolicy);

// Raw WebSockets (separate transport from SignalR) — required for the
// upstream pwmarcz/autotable bundle's WS protocol at /autotable/ws.
app.UseWebSockets();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DatabaseBootstrapper.InitializeAsync(db);
}

// Phase I Wave 2 — re-hydrate any non-terminal Changsha games from persistence so
// a process restart no longer wipes active hands. Must run after the schema
// bootstrap (which guarantees the ChangshaGames table exists) and before any
// hub/WS subscriber is wired so no inbound command can race the load.
var changshaRuntime = app.Services.GetRequiredService<IChangshaGameRuntime>();
await changshaRuntime.HydrateAsync(app.Services, app.Lifetime.ApplicationStopping);

app.UseDefaultFiles();
app.UseStaticFiles();

var autotablePath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "../../../frontend/autotable"));
if (Directory.Exists(autotablePath))
{
    var autotableFileProvider = new PhysicalFileProvider(autotablePath);
    var contentTypeProvider = new FileExtensionContentTypeProvider();
    contentTypeProvider.Mappings[".glb"] = "model/gltf-binary";
    contentTypeProvider.Mappings[".gltf"] = "model/gltf+json";

    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = autotableFileProvider,
        RequestPath = "/autotable"
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = autotableFileProvider,
        RequestPath = "/autotable",
        ContentTypeProvider = contentTypeProvider
    });
}

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", service = "mahjong-autotable-api" }));

// Phase J Wave 3 — Docker HEALTHCHECK + Linux deploy probe (Apone). Returns a
// stable JSON shape: status="healthy", buildSha from BUILD_SHA env var (or
// "dev" when unset OR empty — Apone's Dockerfile defaults BUILD_SHA="" which
// would bypass `?? "dev"` since `??` only catches null), uptime since process
// start, and the assembly version string. Distinct from /api/health (legacy
// short-form probe used by the frontend) so deployment infrastructure has its
// own stable wire contract.
app.MapGet("/health", () =>
{
    var sha = Environment.GetEnvironmentVariable("BUILD_SHA");
    return Results.Ok(new
    {
        status = "healthy",
        buildSha = string.IsNullOrEmpty(sha) ? "dev" : sha,
        uptime = DateTimeOffset.UtcNow - processStartTime,
        version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown"
    });
});

app.MapGet("/api/system/persistence", (IConfiguration configuration) =>
{
    var provider = configuration.GetValue<string>("Persistence:Provider") ?? "Sqlite";
    return Results.Ok(new { provider });
});

// Phase J Wave 3 — canonical display ordering for WinPattern values (Hicks's UI).
// Returns a flat JSON object keyed by the camelCase pattern wire-name (same strings
// the SignalR winResult.allPatterns array uses) mapped to the integer canonical
// order. Lower = display first. Sourced from ChangshaPatternOrdering.Order so the
// frontend doesn't have to embed a parallel table.
app.MapGet("/api/changsha/pattern-ordering", () =>
{
    var map = new Dictionary<string, int>();
    foreach (var kvp in ChangshaPatternOrdering.Order)
    {
        map[WinPatternWireName(kvp.Key)] = kvp.Value;
    }
    return Results.Ok(map);
});

app.MapHub<ChangshaHub>("/hubs/changsha");

// Phase J Wave 5 — map MVC controllers (MatchmakingController owns
// GET /api/matchmaking/lobby).
app.MapControllers();

// Autotable WS endpoint — speaks upstream NEW/JOIN/JOINED/UPDATE protocol
// so the byte-identical autotable.9519e86d.js bundle connects unchanged.
// Force singleton manager construction so it subscribes to runtime events
// before any games are created.
_ = app.Services.GetRequiredService<AutotableConnectionManager>();
app.MapAutotableWs();

app.Run();

// Phase J Wave 3 — wire-name mapping mirrors
// ChangshaToAutotableTranslator.WinPatternToWire and
// ChangshaGameRuntime.WinPatternToWire so the /api/changsha/pattern-ordering
// keys match the strings used in winResult.allPatterns across both transports.
static string WinPatternWireName(WinPattern p) => p switch
{
    WinPattern.Standard => "standard",
    WinPattern.SevenPairs => "sevenPairs",
    WinPattern.AllPungs => "allPungs",
    WinPattern.FullFlush => "fullFlush",
    WinPattern.NineTerminals => "nineTerminals",
    WinPattern.HeavenlyHand => "heavenlyHand",
    WinPattern.EarthlyHand => "earthlyHand",
    WinPattern.LastTileFromWall => "lastTileFromWall",
    WinPattern.LastDiscardCatch => "lastDiscardCatch",
    WinPattern.KongReplacementWin => "kongReplacementWin",
    _ => p.ToString().ToLowerInvariant()
};

public partial class Program
{
}
