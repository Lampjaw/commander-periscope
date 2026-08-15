namespace CommanderPeriscope.Session;

/// <summary>
/// Which lobby seat this circuit (browser tab) belongs to, if any — set once a page knows its player/lobby.
/// Scoped so it's shared between the page component and <see cref="LobbyCircuitHandler"/> for the same circuit.
/// </summary>
public sealed class CircuitPlayerContext
{
    public string? LobbyCode { get; private set; }
    public string? PlayerId { get; private set; }

    public void SetPlayer(string lobbyCode, string playerId)
    {
        LobbyCode = lobbyCode;
        PlayerId = playerId;
    }
}
