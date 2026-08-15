using System.Text.Json.Serialization;
using CommanderPeriscope;
using CommanderPeriscope.Components;
using CommanderPeriscope.Hubs;
using CommanderPeriscope.Lobby;
using CommanderPeriscope.Session;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .WriteTo.Console(new CompactJsonFormatter()));

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddLocalizationSupport();

builder.Services.AddSingleton<ILobbyRepository, InMemoryLobbyRepository>();
builder.Services.AddSingleton<LobbyService>();
builder.Services.AddScoped<PlayerIdentityService>();
builder.Services.AddScoped<CircuitPlayerContext>();
builder.Services.AddScoped<CircuitHandler, LobbyCircuitHandler>();

builder.Services.AddSignalR()
    .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton<GameConnectionRegistry>();
builder.Services.AddSingleton<InternalHubUrlResolver>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.UseLocalizationSupport();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapHub<GameHub>("/hubs/game");

app.Run();

/// <summary>Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> can host this app in-process for integration tests.</summary>
public partial class Program;
