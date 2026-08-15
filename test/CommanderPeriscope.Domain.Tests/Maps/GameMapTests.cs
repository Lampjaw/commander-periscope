using CommanderPeriscope.Domain.Common;
using CommanderPeriscope.Domain.Maps;
using CommanderPeriscope.Domain.Tests.TestSupport;
using Xunit;

namespace CommanderPeriscope.Domain.Tests.Maps;

public class GameMapTests
{
    [Theory]
    [InlineData(9)]
    [InlineData(11)]
    public void Constructor_RejectsSizesNotDivisibleByFive(int size)
    {
        var cells = new CellType[size, size];
        Assert.Throws<ArgumentException>(() => new GameMap(size, cells));
    }

    [Fact]
    public void Constructor_RejectsSizesBelowTen()
    {
        var cells = new CellType[5, 5];
        Assert.Throws<ArgumentException>(() => new GameMap(5, cells));
    }

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(4, 4, 1)]
    [InlineData(5, 0, 2)]
    [InlineData(9, 9, 4)]
    public void SectorOf_NumbersRowMajorFromOne(int col, int row, int expectedSector)
    {
        var map = MapBuilder.Build(10);
        Assert.Equal(expectedSector, map.SectorOf(new Coordinate(col, row)));
    }

    [Fact]
    public void SectorOf_15x15HasNineSectors()
    {
        var map = MapBuilder.Build(15);
        Assert.Equal(9, map.SectorCount);
        Assert.Equal(9, map.SectorOf(new Coordinate(14, 14)));
    }

    [Fact]
    public void CanMoveTo_RejectsLand()
    {
        var land = new Coordinate(1, 0);
        var map = MapBuilder.Build(10, land);
        Assert.False(map.CanMoveTo(land, []));
    }

    [Fact]
    public void CanMoveTo_RejectsRevisitingCurrentLegTrail()
    {
        var map = MapBuilder.Build(10);
        var visited = new Coordinate(2, 2);
        Assert.False(map.CanMoveTo(visited, [visited]));
    }

    [Fact]
    public void CanMoveTo_RejectsOutOfBounds()
    {
        var map = MapBuilder.Build(10);
        Assert.False(map.CanMoveTo(new Coordinate(10, 0), []));
    }

    [Fact]
    public void CanMoveTo_AllowsFreshWaterDot()
    {
        var map = MapBuilder.Build(10);
        Assert.True(map.CanMoveTo(new Coordinate(3, 3), []));
    }

    [Theory]
    [InlineData(Direction.North, 0, -1)]
    [InlineData(Direction.South, 0, 1)]
    [InlineData(Direction.East, 1, 0)]
    [InlineData(Direction.West, -1, 0)]
    public void Step_MovesOneDotInDirection(Direction direction, int expectedDCol, int expectedDRow)
    {
        var origin = new Coordinate(5, 5);
        var next = GameMap.Step(origin, direction);
        Assert.Equal(origin.Col + expectedDCol, next.Col);
        Assert.Equal(origin.Row + expectedDRow, next.Row);
    }

    [Fact]
    public void Coordinate_FormatsAsLetterThenOneBasedRow()
    {
        Assert.Equal("A1", new Coordinate(0, 0).ToString());
        Assert.Equal("C6", new Coordinate(2, 5).ToString());
    }

    [Fact]
    public void Surrounding_ReturnsEightNeighbors()
    {
        var neighbors = GameMap.Surrounding(new Coordinate(5, 5)).ToList();
        Assert.Equal(8, neighbors.Count);
        Assert.DoesNotContain(new Coordinate(5, 5), neighbors);
    }
}
