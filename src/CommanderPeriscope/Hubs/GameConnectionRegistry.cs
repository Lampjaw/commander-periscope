using System.Collections.Concurrent;

namespace CommanderPeriscope.Hubs;

/// <summary>
/// Maps a live SignalR connection to the (lobby, player) it belongs to. A <see cref="Microsoft.AspNetCore.SignalR.Hub"/>
/// instance is transient — created fresh per invocation — so this is where that association actually lives
/// for the life of the connection (needed by <see cref="GameHub.OnDisconnectedAsync"/>, which only gets a
/// connection id, not the join arguments from whenever <c>JoinGame</c> was called).
/// </summary>
public sealed class GameConnectionRegistry
{
    private readonly ConcurrentDictionary<string, (string LobbyCode, string PlayerId)> _connections = new();

    public void Set(string connectionId, string lobbyCode, string playerId) =>
        _connections[connectionId] = (lobbyCode, playerId);

    public bool TryGet(string connectionId, out (string LobbyCode, string PlayerId) info) =>
        _connections.TryGetValue(connectionId, out info);

    public void Remove(string connectionId) => _connections.TryRemove(connectionId, out _);
}
