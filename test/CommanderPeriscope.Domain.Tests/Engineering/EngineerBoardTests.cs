using CommanderPeriscope.Domain.Common;
using CommanderPeriscope.Domain.Engineering;
using CommanderPeriscope.Domain.Systems;
using Xunit;

namespace CommanderPeriscope.Domain.Tests.Engineering;

public class EngineerBoardTests
{
    private static SystemBreakdown SystemBreakdown(EngineerBoard board, Direction column, SystemColor color, int? circuitId) =>
        board.Breakdowns.OfType<SystemBreakdown>().Single(s => s.Column == column && s.Color == color && s.CircuitGroupId == circuitId);

    private static RadiationBreakdown RadiationBreakdown(EngineerBoard board, Direction column, int index = 0) =>
        board.Breakdowns.OfType<RadiationBreakdown>().Where(s => s.Column == column).ElementAt(index);

    [Fact]
    public void MarkBreakdown_BlocksSystemOfMatchingColor()
    {
        var board = new EngineerBoard();
        var unlinkedYellow = SystemBreakdown(board, Direction.North, SystemColor.Yellow, null);

        Assert.False(board.IsSystemBlocked(SystemType.Silence));
        board.MarkBreakdown(unlinkedYellow);
        Assert.True(board.IsSystemBlocked(SystemType.Silence));
    }

    [Fact]
    public void MarkBreakdown_SelfRepairsWhenCircuitGroupCompletes()
    {
        var board = new EngineerBoard();

        // Group 2: (W: green), (W: yellow), (W: red), (E: red).
        board.MarkBreakdown(SystemBreakdown(board, Direction.West, SystemColor.Green, 2));
        board.MarkBreakdown(SystemBreakdown(board, Direction.West, SystemColor.Yellow, 2));
        board.MarkBreakdown(SystemBreakdown(board, Direction.East, SystemColor.Red, 2));

        var last = SystemBreakdown(board, Direction.West, SystemColor.Red, 2);
        var result = board.MarkBreakdown(last);

        Assert.Equal(BreakdownResult.CanSelfRepairCircuit, result);
        Assert.All(
            board.BreakdownsInCircuit(2),
            s => Assert.False(s.IsMarked));
    }

    [Fact]
    public void MarkBreakdown_CompleteColumnCausesDamageAndWipesBreakdownsOnly()
    {
        var board = new EngineerBoard();

        // West column has no unlinked symbol: 3 circuit members (all group 2) + 2 Reactor slots.
        board.MarkBreakdown(RadiationBreakdown(board, Direction.West, 0));
        board.MarkBreakdown(RadiationBreakdown(board, Direction.West, 1));
        board.MarkBreakdown(SystemBreakdown(board, Direction.West, SystemColor.Green, 2));
        board.MarkBreakdown(SystemBreakdown(board, Direction.West, SystemColor.Yellow, 2));

        // E-red (the 4th group-2 member) is deliberately left unmarked so this final mark can't self-repair —
        // it should register purely as a Complete Area Breakdown.
        var last = SystemBreakdown(board, Direction.West, SystemColor.Red, 2);
        var result = board.MarkBreakdown(last);

        Assert.Equal(BreakdownResult.CompleteAreaBreakdown, result);
        Assert.All(board.Breakdowns, s => Assert.False(s.IsMarked));
        // Repairing the Reactor is only ever done via radiation-full or surfacing — a column wipe leaves it alone.
        Assert.All(board.ReactorBreakdownsIn(Direction.West), s => Assert.True(s.IsMarked));
    }

    [Fact]
    public void MarkBreakdown_RepairWinsOverSimultaneousCompleteAreaBreakdown()
    {
        var board = new EngineerBoard();

        board.MarkBreakdown(RadiationBreakdown(board, Direction.West, 0));
        board.MarkBreakdown(RadiationBreakdown(board, Direction.West, 1));
        board.MarkBreakdown(SystemBreakdown(board, Direction.West, SystemColor.Green, 2));
        board.MarkBreakdown(SystemBreakdown(board, Direction.West, SystemColor.Yellow, 2));
        board.MarkBreakdown(SystemBreakdown(board, Direction.East, SystemColor.Red, 2));

        // This mark simultaneously completes circuit group 2 AND the West column — repair wins, no damage.
        var last = SystemBreakdown(board, Direction.West, SystemColor.Red, 2);
        var result = board.MarkBreakdown(last);

        Assert.Equal(BreakdownResult.CanSelfRepairCircuit, result);
        Assert.False(board.IsColumnComplete(Direction.West));
    }

    [Fact]
    public void MarkRadiation_FullTrackCausesDamageAndWipesEverything()
    {
        var board = new EngineerBoard();
        board.MarkBreakdown(SystemBreakdown(board, Direction.North, SystemColor.Green, 0));

        board.MarkBreakdown(RadiationBreakdown(board, Direction.West, 0));
        board.MarkBreakdown(RadiationBreakdown(board, Direction.West, 1));
        board.MarkBreakdown(RadiationBreakdown(board, Direction.North));
        board.MarkBreakdown(RadiationBreakdown(board, Direction.South));
        var result = board.MarkBreakdown(RadiationBreakdown(board, Direction.East));

        Assert.Equal(BreakdownResult.RadiationFull, result);
        Assert.All(board.Breakdowns, s => Assert.False(s.IsMarked));
    }

    [Fact]
    public void MarkBreakdown_ThrowsWhenAlreadyMarked()
    {
        var board = new EngineerBoard();
        var symbol = SystemBreakdown(board, Direction.North, SystemColor.Green, 0);
        board.MarkBreakdown(symbol);

        Assert.Throws<InvalidOperationException>(() => board.MarkBreakdown(symbol));
    }
}
