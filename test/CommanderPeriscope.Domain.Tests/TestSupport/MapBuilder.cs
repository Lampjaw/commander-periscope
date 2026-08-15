using CommanderPeriscope.Domain.Maps;

namespace CommanderPeriscope.Domain.Tests.TestSupport;

internal static class MapBuilder
{
    /// <summary>An all-water map of the given size, with land placed at the given coordinates.</summary>
    public static GameMap Build(int size, params Coordinate[] land)
    {
        var cells = new CellType[size, size];
        foreach (var coordinate in land)
        {
            cells[coordinate.Row, coordinate.Col] = CellType.Land;
        }

        return new GameMap(size, cells);
    }
}
