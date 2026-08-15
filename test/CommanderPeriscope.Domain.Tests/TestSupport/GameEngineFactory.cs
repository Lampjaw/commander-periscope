using CommanderPeriscope.Domain.Common;
using CommanderPeriscope.Domain.Game;
using CommanderPeriscope.Domain.Maps;
using CommanderPeriscope.Domain.Systems;

namespace CommanderPeriscope.Domain.Tests.TestSupport;

internal static class GameEngineFactory
{
    private static readonly Coordinate DefaultRedStart = new(0, 0);
    private static readonly Coordinate DefaultBlueStart = new(19, 19);

    /// <summary>A 20x20 all-water map. Subs default to opposite corners, far enough apart that tests must move deliberately to interact — pass explicit starts to set up a specific scenario instead.</summary>
    public static GameEngine CreateHunt(
        PacingMode pacing = PacingMode.RealTime,
        Coordinate? redStart = null,
        Coordinate? blueStart = null) =>
        Create(GameMode.Hunt, pacing, redStart, blueStart);

    public static GameEngine CreateSuddenDeath(
        PacingMode pacing = PacingMode.RealTime,
        Coordinate? redStart = null,
        Coordinate? blueStart = null) =>
        Create(GameMode.SuddenDeath, pacing, redStart, blueStart);

    private static GameEngine Create(GameMode mode, PacingMode pacing, Coordinate? redStart, Coordinate? blueStart)
    {
        var engine = new GameEngine(MapBuilder.Build(20), new GameOptions(mode, pacing));
        engine.SetStartingPosition(TeamColor.Red, redStart ?? DefaultRedStart);
        engine.SetStartingPosition(TeamColor.Blue, blueStart ?? DefaultBlueStart);
        return engine;
    }

    public static void FullyChargeGauge(GameEngine engine, TeamColor team, SystemType type)
    {
        var capacity = engine.SubFor(team).Gauges[type].Capacity;
        for (var i = 0; i < capacity; i++)
        {
            engine.MarkGauge(team, type);
        }
    }
}
