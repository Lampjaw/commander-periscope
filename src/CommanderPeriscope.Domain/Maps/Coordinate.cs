namespace CommanderPeriscope.Domain.Maps;

/// <summary>A single navigable dot, addressed zero-based (col 0 = "A", row 0 = "1").</summary>
public readonly record struct Coordinate(int Col, int Row)
{
    public override string ToString() => $"{(char)('A' + Col)}{Row + 1}";
}
