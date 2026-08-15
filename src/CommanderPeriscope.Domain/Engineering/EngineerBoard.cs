using CommanderPeriscope.Domain.Common;
using CommanderPeriscope.Domain.Systems;

namespace CommanderPeriscope.Domain.Engineering;

public sealed record BreakdownMarkResult(BreakdownResult BreakdownResult);

public sealed class EngineerBoard
{
    public IReadOnlyList<BreakdownItem> Breakdowns { get; }

    public EngineerBoard()
    {
        Breakdowns =
        [
            // System breakdown circuits
            new SystemBreakdown(Direction.North, SystemColor.Green, 0),
            new SystemBreakdown(Direction.North, SystemColor.Red, 0),
            new SystemBreakdown(Direction.North, SystemColor.Yellow, 0),
            new SystemBreakdown(Direction.East, SystemColor.Green, 0),

            new SystemBreakdown(Direction.South, SystemColor.Green, 1),
            new SystemBreakdown(Direction.South, SystemColor.Yellow, 1),
            new SystemBreakdown(Direction.South, SystemColor.Red, 1),
            new SystemBreakdown(Direction.East, SystemColor.Yellow, 1),

            new SystemBreakdown(Direction.West, SystemColor.Green, 2),
            new SystemBreakdown(Direction.West, SystemColor.Yellow, 2),
            new SystemBreakdown(Direction.West, SystemColor.Red, 2),
            new SystemBreakdown(Direction.East, SystemColor.Red, 2),

            // Reactor row
            new SystemBreakdown(Direction.West, SystemColor.Green),
            new RadiationBreakdown(Direction.West),
            new RadiationBreakdown(Direction.West),

            new SystemBreakdown(Direction.North, SystemColor.Green),
            new SystemBreakdown(Direction.North, SystemColor.Red),
            new RadiationBreakdown(Direction.North),
            
            new SystemBreakdown(Direction.South, SystemColor.Red),
            new RadiationBreakdown(Direction.South),
            new SystemBreakdown(Direction.South, SystemColor.Yellow),

            new RadiationBreakdown(Direction.East),
            new SystemBreakdown(Direction.East, SystemColor.Green),
            new RadiationBreakdown(Direction.East)
        ];
    }

    public IEnumerable<BreakdownItem> BreakdownsIn(Direction column) => Breakdowns.Where(s => s.Column == column);

    public IEnumerable<BreakdownItem> ReactorBreakdownsIn(Direction column) => BreakdownsIn(column).Where(s => s is SystemBreakdown sb && sb.CircuitGroupId == null || s is RadiationBreakdown);

    public IEnumerable<BreakdownItem> CircuitedBreakdownsIn(Direction column) => BreakdownsIn(column).Where(s => s is SystemBreakdown sb && sb.CircuitGroupId != null);

    public IEnumerable<BreakdownItem> BreakdownsInCircuit(int circuitId) => Breakdowns.OfType<SystemBreakdown>().Where(s => s.CircuitGroupId == circuitId);

    public bool IsSystemBlocked(SystemType type) => Breakdowns.OfType<SystemBreakdown>().Any(s => s.Color == type.Color() && s.IsMarked);

    public bool IsColumnComplete(Direction column) => BreakdownsIn(column).All(s => s.IsMarked);

    public bool IsRadiationFull => Breakdowns.All(s => s is RadiationBreakdown && s.IsMarked);

    public BreakdownResult MarkBreakdown(BreakdownItem item)
    {
        if (!Breakdowns.Contains(item))
        {
            throw new ArgumentException("Item does not belong to this board.", nameof(item));
        }

        if (item.IsMarked)
        {
            throw new InvalidOperationException("That breakdown item is already marked.");
        }

        item.IsMarked = true;

        if (item is SystemBreakdown sb && sb.CircuitGroupId != null)
        {
            var canSelfRepair = CanCircuitSelfRepair(sb.CircuitGroupId.Value);
            if (canSelfRepair)
            {
                return BreakdownResult.CanSelfRepairCircuit;
            }
        }

        if (item is RadiationBreakdown)
        {
            var radiationFull = IsRadiationFull;
            if (radiationFull)
            {
                return BreakdownResult.RadiationFull;
            }
        }

        var completeArea = IsColumnComplete(item.Column);
        if (completeArea)
        {
            return BreakdownResult.CompleteAreaBreakdown;
        }

        return BreakdownResult.NoEffect;
    }

    public void ClearAllBreakdowns()
    {
        foreach (var item in Breakdowns)
        {
            item.IsMarked = false;
        }
    }

    public void ClearColumnBreakdowns(Direction column)
    {
        foreach (var item in CircuitedBreakdownsIn(column))
        {
            item.IsMarked = false;
        }
    }

    public void ClearCircuitBreakdowns(int circuitId)
    {
        foreach (var item in BreakdownsInCircuit(circuitId))
        {
            item.IsMarked = false;
        }
    }

    private bool CanCircuitSelfRepair(int circuitId)
    {
        return BreakdownsInCircuit(circuitId).All(s => s.IsMarked);
    }
}
