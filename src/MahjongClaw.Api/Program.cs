using MahjongClaw.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<MahjongClawDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("MahjongClaw")));

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();
