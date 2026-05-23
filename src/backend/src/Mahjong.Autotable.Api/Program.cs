using Mahjong.Autotable.Api.Autotable;
using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Changsha.Patterns;
using Mahjong.Autotable.Api.Changsha.Runtime;
using Mahjong.Autotable.Api.Data;
using Mahjong.Autotable.Api.Leaderboard;
using Mahjong.Autotable.Api.Matchmaking;
using Mahjong.Autotable.Api.Observability;
using Mahjong.Autotable.Api.Persistence;
using Mahjong.Autotable.Api.Players;
using Mahjong.Autotable.Api.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using System.Text.Json;

// Phase J Wave 3 — Apone's Docker HEALTHCHECK / Linux deploy needs a stable
// process-uptime anchor. Captured at module load (before WebApplication build)
// so the value reflects the actual host process start, not the time of the
// first /health request.
var processStartTime = DateTimeOffset.UtcNow;

// Phase J Wave 9 — Apone (DevOps). Stand-alone migrate mode for the k8s
// pre-rollout Job (infra/k8s/base/job-migrate.yaml). When invoked with
// `--migrate`, the process boots the DI container far enough to resolve
// AppDbContext, runs Database.MigrateAsync (or EnsureCreatedAsync for
// SQLite, which still uses the EnsureCreated bootstrap pattern), and
// exits 0 — the listener port is never bound, so the Job completes
// cleanly without fighting the Deployment's readiness probe.
if (args.Contains("--migrate"))
{
    var migrateBuilder = WebApplication.CreateBuilder(args);
    migrateBuilder.Services.AddPersistence(migrateBuilder.Configuration);
    using var migrateApp = migrateBuilder.Build();
    using var scope = migrateApp.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    Console.WriteLine($"[migrate] provider={(db.Database.IsSqlite() ? "Sqlite" : db.Database.IsNpgsql() ? "Postgres" : db.Database.IsSqlServer() ? "SqlServer" : "Unknown")} starting…");
    if (db.Database.IsSqlite())
    {
        // SQLite uses EnsureCreated + defensive bootstraps; reuse the
        // canonical InitializeAsync path so the Job is equivalent to
        // the in-process boot.
        await DatabaseBootstrapper.InitializeAsync(db);
    }
    else
    {
        await db.Database.MigrateAsync();
    }
    Console.WriteLine($"[migrate] complete.");
    return;
}

var builder = WebApplication.CreateBuilder(args);
Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "data"));

// Phase J Wave 5 — structured logging contract (Apone, DevOps). Production
// emits one JSON document per log line so log shippers (Loki promtail, Vector,
// CloudWatch agent, etc.) ingest categories/levels/scopes without parsing
// human-formatted text. Non-production environments keep the readable
// AddSimpleConsole output so `dotnet run` / `docker compose up` stays
// developer-friendly. ClearProviders() is required — without it the default
// Console provider double-emits each entry alongside the JSON one. Scopes are
// surfaced in both modes so SignalR's ConnectionId / HubMethodName scopes
// appear in the structured payload (see docs/observability.md).
builder.Logging.ClearProviders();
if (builder.Environment.IsProduction())
{
    builder.Logging.AddJsonConsole(o =>
    {
        o.IncludeScopes = true;
        o.UseUtcTimestamp = true;
        o.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
        o.JsonWriterOptions = new JsonWriterOptions { Indented = false };
    });
}
else
{
    builder.Logging.AddSimpleConsole(o =>
    {
        o.IncludeScopes = true;
        o.SingleLine = true;
        o.UseUtcTimestamp = true;
        o.TimestampFormat = "HH:mm:ss.fff ";
    });
}

builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
// Phase J Wave 8 — SignalR hub-method breadcrumbs into Sentry (Apone, DevOps).
// The hub filter is a no-op when Sentry isn't initialised (SDK gated on
// `Sentry:Dsn`), so adding it unconditionally costs nothing in dev / test.
builder.Services.AddSignalR(o => o.AddFilter<Mahjong.Autotable.Api.Observability.SentryHubFilter>());

// Phase J Wave 8 — Sentry crash reporting (Apone, DevOps). The call is a
// no-op when `Sentry:Dsn` is unset / empty (which is the default in
// `appsettings.json`), so test / dev runs never hit the network.
var sentryEnabled = builder.WebHost.AddMahjongSentry(builder.Configuration);

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

// Phase J Wave 6 — identity + leaderboard services. PlayerIdentityService
// owns the mahjong_pid cookie (mint/read/refresh) consumed by both the
// REST identity endpoint and the autotable WS upgrade handshake;
// LeaderboardService backs GET /api/leaderboard. Both are thin stateless
// wrappers around HttpContext + EF Core scopes, so singleton lifetime is
// fine.
builder.Services.AddSingleton<PlayerIdentityService>();
builder.Services.AddSingleton<LeaderboardService>();

// Phase J Wave 8 — Bishop: OAuth / magic-link auth + rule-preset CRUD
// (see .squad/decisions/inbox/bishop-phase-j-wave-8.md). Auth services are
// scope-shaped wrappers around AppDbContext: they take an
// IServiceScopeFactory and open a fresh scope per call, mirroring the
// PlayerProfileService / MatchmakingService pattern. Singleton lifetime
// is therefore safe.
var authSection = builder.Configuration.GetSection("Authentication");
var authOptions = authSection.Get<Mahjong.Autotable.Api.Auth.AuthOptions>()
    ?? new Mahjong.Autotable.Api.Auth.AuthOptions();
builder.Services.AddSingleton(authOptions);
var smtpOptions = builder.Configuration.GetSection("Smtp").Get<Mahjong.Autotable.Api.Auth.SmtpOptions>()
    ?? new Mahjong.Autotable.Api.Auth.SmtpOptions();
builder.Services.AddSingleton(smtpOptions);
builder.Services.AddHttpClient("oauth");
if (!string.IsNullOrWhiteSpace(smtpOptions.Host))
{
    builder.Services.AddSingleton<Mahjong.Autotable.Api.Auth.IEmailSender>(sp =>
        new Mahjong.Autotable.Api.Auth.SmtpEmailSender(
            smtpOptions,
            sp.GetRequiredService<ILogger<Mahjong.Autotable.Api.Auth.SmtpEmailSender>>()));
}
else
{
    builder.Services.AddSingleton<Mahjong.Autotable.Api.Auth.IEmailSender,
        Mahjong.Autotable.Api.Auth.LogEmailSender>();
}
builder.Services.AddSingleton<Mahjong.Autotable.Api.Auth.AuthCookieService>();
builder.Services.AddSingleton<Mahjong.Autotable.Api.Auth.AuthIdentityService>();
builder.Services.AddSingleton<Mahjong.Autotable.Api.Auth.OAuthService>();
builder.Services.AddSingleton<Mahjong.Autotable.Api.Auth.MagicLinkService>();

const string ChangshaCorsPolicy = "ChangshaCors";
var configuredOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    // Phase J Wave 6 — origins now come from configuration (Apone, DevOps).
    // appsettings.json carries the two localhost dev origins (the Kestrel HTTP
    // listener and the Parcel dev server); appsettings.Production.json
    // ships with an empty list so production deploys MUST set
    // `Cors__AllowedOrigins__0=https://<public-host>` (see docs/secrets.md
    // and docs/deployment.md § "CORS"). AllowCredentials() is required for
    // the autotable WS `mahjong_pid` cookie + SignalR cookie auth — which is
    // why we deliberately do NOT call AllowAnyOrigin() (the framework
    // rejects that combination at policy-build time).
    options.AddPolicy(ChangshaCorsPolicy, policy =>
    {
        var builderPolicy = policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
        if (configuredOrigins.Length > 0)
        {
            builderPolicy.WithOrigins(configuredOrigins);
        }
    });
});

// Phase J Wave 6 — production rate limiting (Apone, DevOps). Gated by
// `RateLimiting:Enabled` so the xUnit `WebApplicationFactory` harness
// (which boots `Development`) and `dotnet run` keep their unrestricted
// throughput. See RateLimitingExtensions for the policy contract and
// docs/deployment.md § "Rate limiting" for the runbook.
var rateLimitingEnabled = builder.Services.AddMahjongRateLimiting(builder.Configuration);

var app = builder.Build();

// Phase J Wave 8 — security + CDN cache headers (Apone, DevOps). Installed
// first so security headers stamp on every response (including errors and
// short-circuit returns from rate limiting / CORS). The middleware uses
// `Response.OnStarting` to mutate headers AFTER UseStaticFiles has set
// its own Cache-Control.
app.UseMiddleware<Mahjong.Autotable.Api.Observability.SecurityHeadersMiddleware>();

app.UseCors(ChangshaCorsPolicy);

if (rateLimitingEnabled)
{
    // Phase J Wave 6 — installed only when the gate is on so dev / test
    // pipelines bypass the middleware entirely.
    app.UseRateLimiter();
}

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

// Phase J Wave 6 — /api/health is a probe surface. Excluded from rate
// limiting via DisableRateLimiting() (works whether the middleware is
// registered or not — the attribute is metadata-only when no limiter is
// wired). Same treatment for /health and /metrics below.
app.MapGet("/api/health", () => Results.Ok(new { status = "ok", service = "mahjong-autotable-api" }))
    .DisableRateLimiting();

// Phase J Wave 3 — Docker HEALTHCHECK + Linux deploy probe (Apone). Returns a
// stable JSON shape: status="healthy", buildSha from BUILD_SHA env var (or
// "dev" when unset OR empty — Apone's Dockerfile defaults BUILD_SHA="" which
// would bypass `?? "dev"` since `??` only catches null), uptime since process
// start, and the assembly version string. Distinct from /api/health (legacy
// short-form probe used by the frontend) so deployment infrastructure has its
// own stable wire contract.
//
// Phase J Wave 7 — extended with optional db connectivity + activeGames
// counters. `?simple=1` falls back to the 4-field Wave-3 contract so any
// load balancer / k8s liveness probe that grep-asserts the old shape (the
// Docker HEALTHCHECK and tests/smoke/docker-build-smoke.sh do exactly that)
// keeps working unchanged. The detailed shape adds a nested `db` object
// (`{ connected: bool, latencyMs: number }`) and an `activeGames` integer
// pulled from the runtime's in-memory game count. Probing the DB issues a
// single `SELECT 1` round-trip via the EF Core relational connection.
app.MapGet("/health", async (HttpContext ctx, IServiceProvider services) =>
{
    var sha = Environment.GetEnvironmentVariable("BUILD_SHA");
    var resolvedSha = string.IsNullOrEmpty(sha) ? "dev" : sha;
    var uptime = DateTimeOffset.UtcNow - processStartTime;
    var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown";

    // Phase J Wave 7 — `?simple=1` returns the Wave-3 4-field contract so
    // load-balancer probes continue to grep-assert {status, buildSha,
    // uptime, version} without picking up the new fields. The default
    // (no query parameter) returns the extended detail shape.
    if (ctx.Request.Query.TryGetValue("simple", out var simpleVal) && simpleVal == "1")
    {
        return Results.Ok(new
        {
            status = "healthy",
            buildSha = resolvedSha,
            uptime,
            version,
        });
    }

    // DB probe: lightweight SELECT 1 round-trip. Catch broadly so any
    // provider-specific exception surfaces as `connected:false`; the
    // endpoint itself stays 200 so the health probe semantics don't flip
    // just because a single round-trip blipped (load balancers conflate
    // "endpoint down" with "service down").
    var dbConnected = false;
    long dbLatencyMs = 0;
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    try
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var conn = db.Database.GetDbConnection();
        var closeWhenDone = conn.State != System.Data.ConnectionState.Open;
        if (closeWhenDone) await conn.OpenAsync();
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            _ = await cmd.ExecuteScalarAsync();
            dbConnected = true;
        }
        finally
        {
            if (closeWhenDone) await conn.CloseAsync();
        }
    }
    catch
    {
        dbConnected = false;
    }
    stopwatch.Stop();
    dbLatencyMs = stopwatch.ElapsedMilliseconds;

    var runtime = services.GetService<IChangshaGameRuntime>();
    var activeGames = runtime?.GameCount ?? 0;

    return Results.Ok(new
    {
        status = dbConnected ? "healthy" : "degraded",
        buildSha = resolvedSha,
        uptime,
        version,
        db = new { connected = dbConnected, latencyMs = dbLatencyMs },
        activeGames,
    });
}).DisableRateLimiting();

app.MapGet("/api/system/persistence", (IConfiguration configuration) =>
{
    var provider = configuration.GetValue<string>("Persistence:Provider") ?? "Sqlite";
    return Results.Ok(new { provider });
}).RequireRateLimiting(RateLimitingExtensions.ApiPolicy);

// Phase J Wave 5 — Prometheus scrape endpoint (Apone, DevOps). The body
// is rendered by Observability.MetricsEndpoint.Render in the canonical
// text/plain v0.0.4 exposition format. See docs/observability.md for
// the metric catalog. Phase J Wave 6 — explicitly off-limits to rate
// limiting so the scrape loop doesn't trip on high-cardinality polls.
app.MapGet("/metrics", (IServiceProvider services) => MetricsEndpoint.Render(services))
    .DisableRateLimiting();

// Phase J Wave 9 — CSP violation report sink (Apone, DevOps). Off the
// rate-limiter path because browsers and Cloudflare fan-out a burst of
// reports when a policy first lands. See Observability/CspReportEndpoint.cs
// for the endpoint contract and docs/cloudflare.md § "CSP reporting" for
// the operator runbook.
app.MapCspReport().DisableRateLimiting();

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
}).RequireRateLimiting(RateLimitingExtensions.ApiPolicy);

// Phase J Wave 6 — SignalR hubs are long-lived connections; the
// rate-limiter middleware would only see the initial handshake and then
// be bypassed for the persistent transport anyway, so leaving the hub
// route un-limited keeps semantics honest and avoids surprising 429s on
// the upgrade request when a client reconnects rapidly.
app.MapHub<ChangshaHub>("/hubs/changsha");

// Phase J Wave 5 — map MVC controllers (MatchmakingController owns
// GET /api/matchmaking/lobby). Phase J Wave 6 — controllers under
// /api/** opt into the token-bucket policy so any future REST endpoint
// Bishop adds inherits the limit automatically.
app.MapControllers()
    .RequireRateLimiting(RateLimitingExtensions.ApiPolicy);

// Autotable WS endpoint — speaks upstream NEW/JOIN/JOINED/UPDATE protocol
// so the byte-identical autotable.9519e86d.js bundle connects unchanged.
// Force singleton manager construction so it subscribes to runtime events
// before any games are created. Phase J Wave 6 — raw WS is a long-lived
// transport, intentionally unlimited (same reasoning as /hubs/changsha).
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
