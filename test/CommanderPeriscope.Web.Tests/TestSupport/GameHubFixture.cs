using CommanderPeriscope.Lobby;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace CommanderPeriscope.Web.Tests.TestSupport;

/// <summary>Hosts the real app in-process (via <see cref="WebApplicationFactory{TEntryPoint}"/>) and hands out real <see cref="HubConnection"/>s against it — this is "driving the hub directly," not mocking it.</summary>
public sealed class GameHubFixture : IAsyncDisposable
{
    public WebApplicationFactory<Program> Factory { get; } = new();

    public LobbyService LobbyService => Factory.Services.GetRequiredService<LobbyService>();

    public async Task<HubConnection> ConnectAsync()
    {
        var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/game", options =>
            {
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
            })
            .Build();

        await connection.StartAsync();
        return connection;
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
    }
}
