using CommanderPeriscope.Domain.Common;
using CommanderPeriscope.Domain.Systems;

namespace CommanderPeriscope.Domain.Engineering;

public class BreakdownItem(Direction column)
{
    public Direction Column { get; } = column;
    public bool IsMarked { get; set; }
}

public sealed class SystemBreakdown(Direction column, SystemColor color, int? circuitGroupId = null) : BreakdownItem(column)
{
    public SystemColor Color { get; } = color;

    public int? CircuitGroupId { get; } = circuitGroupId;
}

public sealed class RadiationBreakdown(Direction column) : BreakdownItem(column)
{
}
