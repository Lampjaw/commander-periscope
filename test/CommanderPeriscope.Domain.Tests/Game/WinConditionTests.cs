using CommanderPeriscope.Domain.Common;
using CommanderPeriscope.Domain.Game;
using CommanderPeriscope.Domain.Maps;
using CommanderPeriscope.Domain.Systems;
using CommanderPeriscope.Domain.Tests.TestSupport;
using Xunit;

namespace CommanderPeriscope.Domain.Tests.Game;

public class WinConditionTests
{
    [Fact]
    public void SuddenDeath_AnyDamageEndsTheGameImmediately()
    {
        var engine = GameEngineFactory.CreateSuddenDeath(blueStart: new Coordinate(2, 0));
        GameEngineFactory.FullyChargeGauge(engine, TeamColor.Red, SystemType.Torpedo);

        engine.FireTorpedo(TeamColor.Red, new Coordinate(2, 0));

        Assert.Equal(WinResult.RedWins, engine.Result);
        Assert.True(engine.IsOver);
    }

    [Fact]
    public void SimultaneousDestruction_ResultsInTie()
    {
        // Red at (0,0), Blue at (1,1) — both are exactly 1 Chebyshev dot from an impact at (1,0), so a single
        // torpedo there causes Red's close-range self-damage AND an indirect hit on Blue in the same resolution.
        var engine = GameEngineFactory.CreateSuddenDeath(blueStart: new Coordinate(1, 1));
        GameEngineFactory.FullyChargeGauge(engine, TeamColor.Red, SystemType.Torpedo);

        engine.FireTorpedo(TeamColor.Red, new Coordinate(1, 0));

        Assert.Equal(WinResult.Tie, engine.Result);
    }

    [Fact]
    public void Hunt_FourthDamageBoxNeutralizesTheSub()
    {
        var engine = GameEngineFactory.CreateHunt(blueStart: new Coordinate(2, 0));
        GameEngineFactory.FullyChargeGauge(engine, TeamColor.Red, SystemType.Torpedo);
        engine.FireTorpedo(TeamColor.Red, new Coordinate(2, 0)); // direct hit: 2 damage
        Assert.False(engine.IsOver);

        GameEngineFactory.FullyChargeGauge(engine, TeamColor.Red, SystemType.Torpedo);
        engine.Move(TeamColor.Red, Direction.East);
        engine.FireTorpedo(TeamColor.Red, new Coordinate(2, 0)); // second direct hit: 2 more damage -> 4 total

        Assert.Equal(WinResult.RedWins, engine.Result);
    }

    [Fact]
    public void IsOver_BlocksFurtherIntents()
    {
        var engine = GameEngineFactory.CreateSuddenDeath(blueStart: new Coordinate(2, 0));
        GameEngineFactory.FullyChargeGauge(engine, TeamColor.Red, SystemType.Torpedo);
        engine.FireTorpedo(TeamColor.Red, new Coordinate(2, 0));

        Assert.Throws<InvalidOperationException>(() => engine.Move(TeamColor.Blue, Direction.West));
    }
}
