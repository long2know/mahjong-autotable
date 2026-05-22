using Mahjong.Autotable.Api.Autotable;
using Mahjong.Autotable.Api.Changsha;
using Mahjong.Autotable.Api.Changsha.Runtime;
using Mahjong.Autotable.Api.Data;
using Mahjong.Autotable.Api.Persistence;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "data"));

builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSignalR();

builder.Services.Configure<ChangshaRuntimeOptions>(builder.Configuration.GetSection("ChangshaRuntime"));
builder.Services.AddSingleton<IChangshaGameRuntime, ChangshaGameRuntime>();
builder.Services.AddSingleton<AutotableConnectionManager>();

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

app.MapGet("/api/system/persistence", (IConfiguration configuration) =>
{
    var provider = configuration.GetValue<string>("Persistence:Provider") ?? "Sqlite";
    return Results.Ok(new { provider });
});

app.MapHub<ChangshaHub>("/hubs/changsha");

// Autotable WS endpoint — speaks upstream NEW/JOIN/JOINED/UPDATE protocol
// so the byte-identical autotable.9519e86d.js bundle connects unchanged.
// Force singleton manager construction so it subscribes to runtime events
// before any games are created.
_ = app.Services.GetRequiredService<AutotableConnectionManager>();
app.MapAutotableWs();

app.Run();

public partial class Program
{
}
