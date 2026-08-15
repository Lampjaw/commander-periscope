using CommanderPeriscope.Domain.Maps;
using Xunit;

namespace CommanderPeriscope.Domain.Tests.Maps;

public class MapFixtureTests
{
    [Fact]
    public void Parse_BuildsMapFromRowStrings()
    {
        const string json = """
            {
              "size": 10,
              "redStartSector": 1,
              "blueStartSector": 4,
              "rows": [
                "..........",
                "..........",
                "..........",
                "..........",
                "..........",
                "..........",
                "..........",
                "..........",
                "..........",
                ".........."
              ]
            }
            """;

        var loaded = MapFixture.Parse(json);

        Assert.Equal(10, loaded.Map.Size);
        Assert.Equal(1, loaded.RedStartSector);
        Assert.Equal(4, loaded.BlueStartSector);
        Assert.True(loaded.Map.IsWater(new Coordinate(0, 0)));
    }

    [Fact]
    public void Parse_ReadsLandCells()
    {
        const string json = """
            {
              "size": 10,
              "redStartSector": 1,
              "blueStartSector": 4,
              "rows": [
                "#.........",
                "..........",
                "..........",
                "..........",
                "..........",
                "..........",
                "..........",
                "..........",
                "..........",
                ".........."
              ]
            }
            """;

        var loaded = MapFixture.Parse(json);

        Assert.True(loaded.Map.IsLand(new Coordinate(0, 0)));
        Assert.True(loaded.Map.IsWater(new Coordinate(1, 0)));
    }

    [Fact]
    public void Parse_RejectsRowCountMismatch()
    {
        const string json = """
            { "size": 10, "redStartSector": 1, "blueStartSector": 4, "rows": [".........."] }
            """;

        Assert.Throws<FormatException>(() => MapFixture.Parse(json));
    }

    [Fact]
    public void Parse_RejectsRowLengthMismatch()
    {
        const string json = """
            {
              "size": 10,
              "redStartSector": 1,
              "blueStartSector": 4,
              "rows": [
                ".....",
                "..........",
                "..........",
                "..........",
                "..........",
                "..........",
                "..........",
                "..........",
                "..........",
                ".........."
              ]
            }
            """;

        Assert.Throws<FormatException>(() => MapFixture.Parse(json));
    }

    [Theory]
    [InlineData("10x10", 10)]
    [InlineData("15x15", 15)]
    public void LoadReference_LoadsEmbeddedStartingMaps(string name, int expectedSize)
    {
        var loaded = MapFixture.LoadReference(name);

        Assert.Equal(expectedSize, loaded.Map.Size);
        Assert.True(loaded.RedStartSector >= 1 && loaded.RedStartSector <= loaded.Map.SectorCount);
        Assert.True(loaded.BlueStartSector >= 1 && loaded.BlueStartSector <= loaded.Map.SectorCount);

        var waterCount = 0;
        for (var row = 0; row < expectedSize; row++)
        {
            for (var col = 0; col < expectedSize; col++)
            {
                if (loaded.Map.IsWater(new Coordinate(col, row)))
                {
                    waterCount++;
                }
            }
        }

        // "Mostly sea" — water should heavily dominate the small scattered islands.
        Assert.True(waterCount > expectedSize * expectedSize * 0.8);
    }
}
