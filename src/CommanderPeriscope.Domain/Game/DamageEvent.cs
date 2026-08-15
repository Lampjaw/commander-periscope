using CommanderPeriscope.Domain.Common;
using CommanderPeriscope.Domain.Maps;

namespace CommanderPeriscope.Domain.Game;

public enum DamageCause
{
    TorpedoDirect,
    TorpedoIndirect,
    TorpedoSelf,
    MineDirect,
    MineIndirect,
    MineSelf,
    MineChainDirect,
    MineChainIndirect,
    ReactorRadiation,
    CompleteAreaBreakdown,
}

/// <summary>One damage application, for building the toast/event feed and for tests.</summary>
public sealed record DamageEvent(TeamColor Team, int Amount, DamageCause Cause, Coordinate? Location);
