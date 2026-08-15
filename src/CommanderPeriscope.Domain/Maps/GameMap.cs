using CommanderPeriscope.Domain.Common;

namespace CommanderPeriscope.Domain.Maps;

/// <summary>
/// A fixed grid of water/land cells, divisible into 5x5 sectors, numbered row-major starting at 1.
/// </summary>
public sealed class GameMap
{
    public const int SectorSize = 5;

    private readonly CellType[,] _cells;

    public int Size { get; }
    public int SectorsPerSide => Size / SectorSize;
    public int SectorCount => SectorsPerSide * SectorsPerSide;

    public GameMap(int size, CellType[,] cells)
    {
        if (size < 10 || size % SectorSize != 0)
        {
            throw new ArgumentException($"Map size must be at least 10 and divisible by {SectorSize}.", nameof(size));
        }

        if (cells.GetLength(0) != size || cells.GetLength(1) != size)
        {
            throw new ArgumentException("Cell grid dimensions must match the declared map size.", nameof(cells));
        }

        Size = size;
        _cells = cells;
    }

    public bool InBounds(Coordinate c) => c.Col >= 0 && c.Col < Size && c.Row >= 0 && c.Row < Size;

    public CellType CellAt(Coordinate c)
    {
        if (!InBounds(c))
        {
            throw new ArgumentOutOfRangeException(nameof(c), c, "Coordinate is outside the map.");
        }

        return _cells[c.Row, c.Col];
    }

    public bool IsWater(Coordinate c) => InBounds(c) && CellAt(c) == CellType.Water;

    public bool IsLand(Coordinate c) => InBounds(c) && CellAt(c) == CellType.Land;

    /// <summary>1-based sector number, numbered row-major (left-to-right, top-to-bottom).</summary>
    public int SectorOf(Coordinate c)
    {
        if (!InBounds(c))
        {
            throw new ArgumentOutOfRangeException(nameof(c), c, "Coordinate is outside the map.");
        }

        var sectorRow = c.Row / SectorSize;
        var sectorCol = c.Col / SectorSize;
        return sectorRow * SectorsPerSide + sectorCol + 1;
    }

    public static Coordinate Step(Coordinate origin, Direction direction) => direction switch
    {
        Direction.North => origin with { Row = origin.Row - 1 },
        Direction.South => origin with { Row = origin.Row + 1 },
        Direction.East => origin with { Col = origin.Col + 1 },
        Direction.West => origin with { Col = origin.Col - 1 },
        _ => throw new ArgumentOutOfRangeException(nameof(direction)),
    };

    /// <summary>A move is legal onto an in-bounds water dot that hasn't already been visited this leg.</summary>
    public bool CanMoveTo(Coordinate destination, IReadOnlyCollection<Coordinate> currentLegTrail) =>
        IsWater(destination) && !currentLegTrail.Contains(destination);

    /// <summary>The 8 surrounding dots, without bounds/terrain filtering (used for indirect-damage classification).</summary>
    public static IEnumerable<Coordinate> Surrounding(Coordinate c)
    {
        for (var dRow = -1; dRow <= 1; dRow++)
        {
            for (var dCol = -1; dCol <= 1; dCol++)
            {
                if (dRow == 0 && dCol == 0)
                {
                    continue;
                }

                yield return c with { Col = c.Col + dCol, Row = c.Row + dRow };
            }
        }
    }

    public static int ChebyshevDistance(Coordinate a, Coordinate b) =>
        Math.Max(Math.Abs(a.Col - b.Col), Math.Abs(a.Row - b.Row));

    public static int ManhattanDistance(Coordinate a, Coordinate b) =>
        Math.Abs(a.Col - b.Col) + Math.Abs(a.Row - b.Row);
}
