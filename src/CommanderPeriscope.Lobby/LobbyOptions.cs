using CommanderPeriscope.Domain.Game;

namespace CommanderPeriscope.Lobby;

/// <summary>The host's configurable session options, set in the lobby before the game starts.</summary>
public sealed record LobbyOptions
{
    public required string MapId { get; init; }
    public GameMode Mode { get; init; } = GameMode.Hunt;
    public PacingMode Pacing { get; init; } = PacingMode.RealTime;
    public int RedTeamSize { get; init; } = 4;
    public int BlueTeamSize { get; init; } = 4;
    public bool RadioRelayEnabled { get; init; }
    public bool MandatoryDamageReportEnabled { get; init; }
    public bool AutomaticDamageVerification { get; init; }
    public bool TraditionalRepairGame { get; init; } = true;
    public bool PauseOnDisconnect { get; init; } = true;

    public static readonly IReadOnlyList<int> ValidTeamSizes = [2, 3, 4];

    public GameOptions ToGameOptions() => new(Mode, Pacing, RadioRelayEnabled, MandatoryDamageReportEnabled, AutomaticDamageVerification, TraditionalRepairGame, PauseOnDisconnect);
}
