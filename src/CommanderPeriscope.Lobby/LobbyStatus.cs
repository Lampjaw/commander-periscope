namespace CommanderPeriscope.Lobby;

public enum LobbyStatus
{
    Open,
    InProgress,
    Paused,
    Finished,
}

/// <summary>Why an in-progress game is currently paused — so an auto-pause on disconnect never gets auto-resumed by a reconnect if the host had also manually paused.</summary>
public enum PauseReason
{
    None,
    Host,
    Disconnect,
}
