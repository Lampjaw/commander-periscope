using System.Collections.Concurrent;

namespace CommanderPeriscope.Lobby;

public interface ILobbyRepository
{
    Lobby? TryGet(string code);
    bool Exists(string code);
    void Add(Lobby lobby);
}

/// <summary>Session state lives in-memory per instance — no persistence, matching the self-hosted single-instance deployment model.</summary>
public sealed class InMemoryLobbyRepository : ILobbyRepository
{
    private readonly ConcurrentDictionary<string, Lobby> _lobbies = new();

    public Lobby? TryGet(string code) => _lobbies.GetValueOrDefault(code);

    public bool Exists(string code) => _lobbies.ContainsKey(code);

    public void Add(Lobby lobby) => _lobbies[lobby.Code] = lobby;
}
