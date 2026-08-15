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
        var unlinkedRed = SystemBreakdown(board, Direction.North, SystemColor.Red, null);

        Assert.False(board.IsSystemBlocked(SystemType.Torpedo));
        board.MarkBreakdown(unlinkedRed);
        Assert.True(board.IsSystemBlocked(SystemType.Torpedo));
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

        // Detecting the repair and applying it are split — the caller (GameEngine) does the clearing.
        board.ClearCircuitBreakdowns(2);
        Assert.All(board.BreakdownsInCircuit(2), s => Assert.False(s.IsMarked));
    }

    [Fact]
    public void MarkBreakdown_CompleteColumnCausesDamageAndWipesCircuitedBreakdownsOnly()
    {
        var board = new EngineerBoard();

        // West column: 3 circuit members (all group 2) + 1 unlinked green + 2 Reactor slots.
        board.MarkBreakdown(RadiationBreakdown(board, Direction.West, 0));
        board.MarkBreakdown(RadiationBreakdown(board, Direction.West, 1));
        board.MarkBreakdown(SystemBreakdown(board, Direction.West, SystemColor.Green, null));
        board.MarkBreakdown(SystemBreakdown(board, Direction.West, SystemColor.Green, 2));
        board.MarkBreakdown(SystemBreakdown(board, Direction.West, SystemColor.Yellow, 2));

        // E-red (the 4th group-2 member) is deliberately left unmarked so this final mark can't self-repair —
        // it should register purely as a Complete Area Breakdown.
        var last = SystemBreakdown(board, Direction.West, SystemColor.Red, 2);
        var result = board.MarkBreakdown(last);

        Assert.Equal(BreakdownResult.CompleteAreaBreakdown, result);

        board.ClearColumnBreakdowns(Direction.West);
        Assert.All(board.CircuitedBreakdownsIn(Direction.West), s => Assert.False(s.IsMarked));
        // A column wipe only clears that column's circuited symbols — the unlinked/reactor items are untouched.
        Assert.All(board.ReactorBreakdownsIn(Direction.West), s => Assert.True(s.IsMarked));
    }

    [Fact]
    public void MarkBreakdown_RepairWinsOverSimultaneousCompleteAreaBreakdown()
    {
        var board = new EngineerBoard();

        board.MarkBreakdown(RadiationBreakdown(board, Direction.West, 0));
        board.MarkBreakdown(RadiationBreakdown(board, Direction.West, 1));
        board.MarkBreakdown(SystemBreakdown(board, Direction.West, SystemColor.Green, null));
        board.MarkBreakdown(SystemBreakdown(board, Direction.West, SystemColor.Green, 2));
        board.MarkBreakdown(SystemBreakdown(board, Direction.West, SystemColor.Yellow, 2));
        board.MarkBreakdown(SystemBreakdown(board, Direction.East, SystemColor.Red, 2));

        // This mark simultaneously completes circuit group 2 AND the West column — repair wins, no damage.
        var last = SystemBreakdown(board, Direction.West, SystemColor.Red, 2);
        var result = board.MarkBreakdown(last);

        Assert.Equal(BreakdownResult.CanSelfRepairCircuit, result);
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
        board.MarkBreakdown(RadiationBreakdown(board, Direction.East, 0));
        var result = board.MarkBreakdown(RadiationBreakdown(board, Direction.East, 1));

        Assert.Equal(BreakdownResult.RadiationFull, result);

        board.ClearAllBreakdowns();
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
