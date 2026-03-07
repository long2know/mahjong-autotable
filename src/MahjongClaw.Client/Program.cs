using MahjongClaw.Client;
using MahjongClaw.Client.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"];
var baseAddress = string.IsNullOrWhiteSpace(apiBaseUrl)
    ? builder.HostEnvironment.BaseAddress
    : apiBaseUrl;

builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(baseAddress, UriKind.Absolute)
});
builder.Services.AddScoped<GameApiClient>();

await builder.Build().RunAsync();
