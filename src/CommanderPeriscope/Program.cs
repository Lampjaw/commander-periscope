using CommanderPeriscope;
using CommanderPeriscope.Components;
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

app.Run();
