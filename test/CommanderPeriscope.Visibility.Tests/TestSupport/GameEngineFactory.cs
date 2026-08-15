using CommanderPeriscope.Domain.Common;
using CommanderPeriscope.Domain.Game;
using CommanderPeriscope.Domain.Maps;

namespace CommanderPeriscope.Visibility.Tests.TestSupport;

internal static class GameEngineFactory
{
    public static GameEngine Create(GameOptions? options = null)
    {
        var cells = new CellType[20, 20];
        var map = new GameMap(20, cells);
        var engine = new GameEngine(map, options ?? new GameOptions(GameMode.Hunt, PacingMode.RealTime));
        engine.SetStartingPosition(TeamColor.Red, new Coordinate(0, 0));
        engine.SetStartingPosition(TeamColor.Blue, new Coordinate(19, 19));
        return engine;
    }
}
