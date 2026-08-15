namespace CommanderPeriscope.Domain.Game;

/// <summary>Ruleset flags configurable by the host in the lobby.</summary>
public sealed record GameOptions(
    GameMode Mode,
    PacingMode Pacing,
    bool RadioRelayEnabled = false,
    bool MandatoryDamageReportEnabled = false,
    bool AutomaticDamageVerification = false,
    bool TraditionalRepairGame = true,
    bool PauseOnDisconnect = true);
