using CommanderPeriscope.Lobby;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace CommanderPeriscope.Session;

/// <summary>
/// Bridges Blazor Server's own connection lifecycle to the lobby's disconnect/reconnect handling — no custom
/// transport needed, the circuit already tells us exactly when this player's tab drops or comes back.
/// </summary>
public sealed class LobbyCircuitHandler(CircuitPlayerContext context, LobbyService lobbyService) : CircuitHandler
{
    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        if (context is { LobbyCode: { } code, PlayerId: { } playerId })
        {
            lobbyService.HandleDisconnect(code, playerId);
        }

        return Task.CompletedTask;
    }

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        if (context is { LobbyCode: { } code, PlayerId: { } playerId })
        {
            lobbyService.HandleReconnect(code, playerId);
        }

        return Task.CompletedTask;
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        if (context is { LobbyCode: { } code, PlayerId: { } playerId })
        {
            lobbyService.HandleDisconnect(code, playerId);
        }

        return Task.CompletedTask;
    }
}
