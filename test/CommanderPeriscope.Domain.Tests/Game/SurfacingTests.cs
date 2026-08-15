using CommanderPeriscope.Domain.Common;
using CommanderPeriscope.Domain.Game;
using CommanderPeriscope.Domain.Maps;
using CommanderPeriscope.Domain.Systems;
using CommanderPeriscope.Domain.Tests.TestSupport;
using Xunit;

namespace CommanderPeriscope.Domain.Tests.Game;

public class SurfacingTests
{
    [Fact]
    public void Surface_RealTime_OpensRepairSessionAndStopsTheSub()
    {
        var engine = GameEngineFactory.CreateHunt(PacingMode.RealTime);

        engine.Surface(TeamColor.Red);

        Assert.True(engine.Red.Surfaced);
        Assert.NotNull(engine.ActiveRealTimeRepair);
    }

    [Fact]
    public void Surface_RealTime_CompletesOnceAllFourSectionsAreSecured()
    {
        var engine = GameEngineFactory.CreateHunt(PacingMode.RealTime);
        engine.Move(TeamColor.Red, Direction.East);
        var trailBeforeSurface = engine.Red.Trail.Count;
        Assert.True(trailBeforeSurface > 1);

        engine.Surface(TeamColor.Red);
        engine.SecureRealTimeRepairSection(TeamColor.Red, CrewRole.Captain);
        engine.SecureRealTimeRepairSection(TeamColor.Red, CrewRole.FirstMate);
        engine.SecureRealTimeRepairSection(TeamColor.Red, CrewRole.RadioOperator);
        Assert.NotNull(engine.ActiveRealTimeRepair); // still waiting on the Engineer's own section

        engine.SecureRealTimeRepairSection(TeamColor.Red, CrewRole.Engineer);

        Assert.Null(engine.ActiveRealTimeRepair);
        Assert.False(engine.Red.Surfaced);
        Assert.Single(engine.Red.Trail); // erased down to just the current position
    }

    [Fact]
    public void Surface_TurnByTurn_EnemyGetsThreeBonusActionsThenRepairCompletes()
    {
        var engine = GameEngineFactory.CreateHunt(PacingMode.TurnByTurn);

        engine.Surface(TeamColor.Red);
        Assert.NotNull(engine.ActiveTurnByTurnSurface);
        Assert.Equal(3, engine.ActiveTurnByTurnSurface!.BonusActionsRemaining);

        engine.ConsumeTurnByTurnBonusAction();
        engine.ConsumeTurnByTurnBonusAction();
        Assert.True(engine.Red.Surfaced); // still surfaced/repairing — sanity guard against a premature complete
        Assert.NotNull(engine.ActiveTurnByTurnSurface);

        engine.ConsumeTurnByTurnBonusAction();

        Assert.Null(engine.ActiveTurnByTurnSurface);
        Assert.False(engine.Red.Surfaced);
    }

    [Fact]
    public void Surface_TurnByTurn_EnemySurfacingMidBonusForfeitsAndSwaps()
    {
        var engine = GameEngineFactory.CreateHunt(PacingMode.TurnByTurn);

        engine.Surface(TeamColor.Red); // Blue now owes Red 3 bonus actions
        engine.ConsumeTurnByTurnBonusAction();

        engine.Surface(TeamColor.Blue); // Blue surfaces instead of using its remaining bonus actions

        Assert.Equal(TeamColor.Blue, engine.ActiveTurnByTurnSurface!.SurfacingTeam);
        Assert.Equal(3, engine.ActiveTurnByTurnSurface.BonusActionsRemaining); // Red gets its own fresh 3 bonus actions
    }

    [Fact]
    public void CompleteRepair_ClearsBreakdownsButKeepsPlacedMines()
    {
        var engine = GameEngineFactory.CreateHunt(PacingMode.RealTime);
        GameEngineFactory.FullyChargeGauge(engine, TeamColor.Red, SystemType.Mine);
        engine.PlaceMine(TeamColor.Red, new Coordinate(1, 0));

        var symbol = engine.Red.Engineer.Breakdowns[0];
        engine.MarkBreakdown(TeamColor.Red, symbol);

        engine.Surface(TeamColor.Red);
        foreach (var role in new[] { CrewRole.Captain, CrewRole.FirstMate, CrewRole.RadioOperator, CrewRole.Engineer })
        {
            engine.SecureRealTimeRepairSection(TeamColor.Red, role);
        }

        Assert.False(symbol.IsMarked);
        Assert.Contains(new Coordinate(1, 0), engine.Red.Mines);
    }
}
