namespace CommanderPeriscope.Lobby;

public enum PlayerConnectionStatus
{
    Connected,
    Disconnected,
}

/// <summary>A joined session identity — keyed by a client-generated token, not an account.</summary>
public sealed class Player(string id, string displayName)
{
    public string Id { get; } = id;
    public string DisplayName { get; set; } = displayName;
    public PlayerConnectionStatus ConnectionStatus { get; set; } = PlayerConnectionStatus.Connected;
}
