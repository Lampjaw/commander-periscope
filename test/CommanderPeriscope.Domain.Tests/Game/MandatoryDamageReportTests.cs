using CommanderPeriscope.Domain.Common;
using CommanderPeriscope.Domain.Game;
using CommanderPeriscope.Domain.Maps;
using CommanderPeriscope.Domain.Tests.TestSupport;
using Xunit;

namespace CommanderPeriscope.Domain.Tests.Game;

public class MandatoryDamageReportTests
{
    private static GameEngine CreateWithOption(bool enabled)
    {
        var engine = new GameEngine(MapBuilder.Build(20), new GameOptions(GameMode.Hunt, PacingMode.RealTime, MandatoryDamageReportEnabled: enabled));
        engine.SetStartingPosition(TeamColor.Red, new Coordinate(0, 0));
        engine.SetStartingPosition(TeamColor.Blue, new Coordinate(19, 19));
        return engine;
    }

    [Fact]
    public void Move_WhenOptionDisabled_NeverSetsPendingDamageReport()
    {
        var engine = CreateWithOption(enabled: false);

        engine.Move(TeamColor.Red, Direction.East);

        Assert.False(engine.Red.PendingDamageReport);
    }

    [Fact]
    public void Move_WhenOptionEnabled_SetsPendingDamageReport()
    {
        var engine = CreateWithOption(enabled: true);

        engine.Move(TeamColor.Red, Direction.East);

        Assert.True(engine.Red.PendingDamageReport);
    }

    [Fact]
    public void Move_WhenPendingDamageReportOutstanding_BlocksTheNextMove()
    {
        var engine = CreateWithOption(enabled: true);

        engine.Move(TeamColor.Red, Direction.East);

        Assert.Throws<InvalidOperationException>(() => engine.Move(TeamColor.Red, Direction.East));
    }

    [Fact]
    public void MarkBreakdown_ClearsPendingDamageReportAndUnblocksTheNextMove()
    {
        var engine = CreateWithOption(enabled: true);
        engine.Move(TeamColor.Red, Direction.East);

        var symbol = engine.Red.Engineer.Breakdowns[0];
        engine.MarkBreakdown(TeamColor.Red, symbol);

        Assert.False(engine.Red.PendingDamageReport);
        engine.Move(TeamColor.Red, Direction.East);
        Assert.True(engine.Red.PendingDamageReport);
    }

    [Fact]
    public void MarkBreakdown_OnTheOtherTeam_DoesNotClearThisTeamsPendingDamageReport()
    {
        var engine = CreateWithOption(enabled: true);
        engine.Move(TeamColor.Red, Direction.East);

        var blueSymbol = engine.Blue.Engineer.Breakdowns[0];
        engine.MarkBreakdown(TeamColor.Blue, blueSymbol);

        Assert.True(engine.Red.PendingDamageReport);
    }
}
