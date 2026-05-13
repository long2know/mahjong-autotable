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
    options.AddPolicy(ChangshaCorsPolicy, policy => policy
        .WithOrigins(
            "http://localhost:5173",
            "https://localhost:5173",
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
