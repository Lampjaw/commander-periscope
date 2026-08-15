using CommanderPeriscope.Domain.Systems;
using Xunit;

namespace CommanderPeriscope.Domain.Tests.Systems;

public class GaugeBoardTests
{
    [Fact]
    public void Mark_BecomesReadyAtCapacity()
    {
        var board = new GaugeBoard();

        Assert.False(board.Mark(SystemType.Torpedo)); // 1/3
        Assert.False(board.Mark(SystemType.Torpedo)); // 2/3
        Assert.True(board.Mark(SystemType.Torpedo)); // 3/3 — just became ready

        Assert.True(board.IsReady(SystemType.Torpedo));
    }

    [Fact]
    public void Mark_ThrowsOnceGaugeIsFull()
    {
        var board = new GaugeBoard();
        for (var i = 0; i < 3; i++)
        {
            board.Mark(SystemType.Mine);
        }

        Assert.Throws<InvalidOperationException>(() => board.Mark(SystemType.Mine));
    }

    [Fact]
    public void Reset_EmptiesGaugeAfterUse()
    {
        var board = new GaugeBoard();
        for (var i = 0; i < 3; i++)
        {
            board.Mark(SystemType.Sonar);
        }

        board.Reset(SystemType.Sonar);

        Assert.False(board.IsReady(SystemType.Sonar));
        Assert.Equal(0, board[SystemType.Sonar].Charge);
    }

    [Theory]
    [InlineData(SystemType.Mine, 3)]
    [InlineData(SystemType.Torpedo, 3)]
    [InlineData(SystemType.Drone, 4)]
    [InlineData(SystemType.Sonar, 3)]
    [InlineData(SystemType.Silence, 6)]
    public void Capacities_MatchTabletopGauges(SystemType type, int expectedCapacity)
    {
        var board = new GaugeBoard();
        Assert.Equal(expectedCapacity, board[type].Capacity);
    }
}
