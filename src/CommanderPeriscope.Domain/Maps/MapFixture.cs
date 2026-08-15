using System.Text.Json;
using System.Text.Json.Serialization;

namespace CommanderPeriscope.Domain.Maps;

/// <summary>JSON shape for a map fixture: '.' is water, '#' is land, one string per row.</summary>
public sealed class MapFixtureData
{
    [JsonPropertyName("size")]
    public int Size { get; set; }

    [JsonPropertyName("redStartSector")]
    public int RedStartSector { get; set; }

    [JsonPropertyName("blueStartSector")]
    public int BlueStartSector { get; set; }

    [JsonPropertyName("rows")]
    public List<string> Rows { get; set; } = [];
}

/// <summary>A loaded map plus the metadata (starting sectors) that isn't part of <see cref="GameMap"/> itself.</summary>
public sealed record LoadedMap(GameMap Map, int RedStartSector, int BlueStartSector);

public static class MapFixture
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static LoadedMap Parse(string json)
    {
        var data = JsonSerializer.Deserialize<MapFixtureData>(json, JsonOptions)
            ?? throw new FormatException("Map fixture JSON did not deserialize to a map.");

        if (data.Rows.Count != data.Size)
        {
            throw new FormatException($"Map declared size {data.Size} but had {data.Rows.Count} rows.");
        }

        var cells = new CellType[data.Size, data.Size];
        for (var row = 0; row < data.Size; row++)
        {
            var line = data.Rows[row];
            if (line.Length != data.Size)
            {
                throw new FormatException($"Row {row} has length {line.Length}, expected {data.Size}.");
            }

            for (var col = 0; col < data.Size; col++)
            {
                cells[row, col] = line[col] switch
                {
                    '.' => CellType.Water,
                    '#' => CellType.Land,
                    var c => throw new FormatException($"Unrecognized map cell character '{c}' at row {row}, col {col}."),
                };
            }
        }

        return new LoadedMap(new GameMap(data.Size, cells), data.RedStartSector, data.BlueStartSector);
    }

    /// <summary>Loads one of the two starting reference maps embedded in this assembly ("10x10" or "15x15").</summary>
    public static LoadedMap LoadReference(string name)
    {
        var resourceName = $"CommanderPeriscope.Domain.Maps.Fixtures.map-{name}.json";
        using var stream = typeof(MapFixture).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Embedded map fixture '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }
}
