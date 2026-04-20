using Mahjong.Autotable.Api.Data;
using Mahjong.Autotable.Api.Persistence;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);
Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "data"));

builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
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

app.Run();
