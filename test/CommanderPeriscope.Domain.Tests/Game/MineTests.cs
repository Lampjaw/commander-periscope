using CommanderPeriscope.Domain.Common;
using CommanderPeriscope.Domain.Engineering;
using CommanderPeriscope.Domain.Maps;
using CommanderPeriscope.Domain.Systems;
using CommanderPeriscope.Domain.Tests.TestSupport;
using CommanderPeriscope.Domain.Weapons;
using Xunit;

namespace CommanderPeriscope.Domain.Tests.Game;

public class MineTests
{
    [Fact]
    public void PlaceMine_ThrowsWhenNotCardinallyAdjacent()
    {
        var engine = GameEngineFactory.CreateHunt();
        GameEngineFactory.FullyChargeGauge(engine, TeamColor.Red, SystemType.Mine);

        Assert.Throws<InvalidOperationException>(() => engine.PlaceMine(TeamColor.Red, new Coordinate(1, 1)));
    }

    [Fact]
    public void PlaceMine_ThrowsWhenOnOwnTrail()
    {
        var engine = GameEngineFactory.CreateHunt();
        engine.Move(TeamColor.Red, Direction.East); // trail now includes (0,0) and (1,0)
        GameEngineFactory.FullyChargeGauge(engine, TeamColor.Red, SystemType.Mine);

        Assert.Throws<InvalidOperationException>(() => engine.PlaceMine(TeamColor.Red, new Coordinate(0, 0)));
    }

    [Fact]
    public void PlaceMine_ClearsGaugeButHasNoImmediateEffect()
    {
        var engine = GameEngineFactory.CreateHunt();
        GameEngineFactory.FullyChargeGauge(engine, TeamColor.Red, SystemType.Mine);

        engine.PlaceMine(TeamColor.Red, new Coordinate(1, 0));

        Assert.Contains(new Coordinate(1, 0), engine.Red.Mines);
        Assert.False(engine.Red.Gauges.IsReady(SystemType.Mine));
        Assert.Equal(0, engine.Blue.Damage.Checked);
    }

    [Fact]
    public void PlaceMine_IsNotItselfBlockedByBackToBackGate()
    {
        // Placing a mine IS a system activation and gauge-gated, just like Torpedo — verify it obeys the same gate.
        var engine = GameEngineFactory.CreateHunt();
        GameEngineFactory.FullyChargeGauge(engine, TeamColor.Red, SystemType.Mine);
        GameEngineFactory.FullyChargeGauge(engine, TeamColor.Red, SystemType.Torpedo);

        engine.PlaceMine(TeamColor.Red, new Coordinate(1, 0));

        Assert.Throws<InvalidOperationException>(() => engine.FireTorpedo(TeamColor.Red, new Coordinate(0, 1)));
    }

    [Fact]
    public void DetonateMine_DoesNotTouchTheGaugeAndIsNotBackToBackGated()
    {
        var engine = GameEngineFactory.CreateHunt();
        GameEngineFactory.FullyChargeGauge(engine, TeamColor.Red, SystemType.Mine);
        engine.PlaceMine(TeamColor.Red, new Coordinate(1, 0)); // activation — a move must happen before the next one
        engine.Move(TeamColor.Red, Direction.South); // Red now at (0,1), re-opening the activation gate
        GameEngineFactory.FullyChargeGauge(engine, TeamColor.Red, SystemType.Torpedo);

        engine.DetonateMine(TeamColor.Red, new Coordinate(1, 0));

        // Torpedo is still fireable right after — detonating a mine doesn't consume the back-to-back gate.
        engine.FireTorpedo(TeamColor.Red, new Coordinate(1, 1));
        Assert.True(true);
    }

    [Fact]
    public void DetonateMine_DirectHitDealsTwoDamage()
    {
        var engine = GameEngineFactory.CreateHunt(blueStart: new Coordinate(1, 0));
        GameEngineFactory.FullyChargeGauge(engine, TeamColor.Red, SystemType.Mine);
        engine.PlaceMine(TeamColor.Red, new Coordinate(1, 0));

        var result = engine.DetonateMine(TeamColor.Red, new Coordinate(1, 0));

        Assert.Equal(ImpactResult.Direct, result.Impact);
        Assert.Equal(2, engine.Blue.Damage.Checked);
    }

    [Fact]
    public void DetonateMine_CloseToOwnSubCausesSelfDamage()
    {
        var engine = GameEngineFactory.CreateHunt();
        GameEngineFactory.FullyChargeGauge(engine, TeamColor.Red, SystemType.Mine);
        engine.PlaceMine(TeamColor.Red, new Coordinate(1, 0));

        engine.DetonateMine(TeamColor.Red, new Coordinate(1, 0));

        Assert.Equal(1, engine.Red.Damage.Checked);
    }

    [Fact]
    public void DetonateMine_ThrowsWhenBlockedByRedBreakdown()
    {
        var engine = GameEngineFactory.CreateHunt();
        GameEngineFactory.FullyChargeGauge(engine, TeamColor.Red, SystemType.Mine);
        engine.PlaceMine(TeamColor.Red, new Coordinate(1, 0));

        var redSymbol = engine.Red.Engineer.Breakdowns.OfType<SystemBreakdown>().First(s => s.Color == SystemColor.Red);
        engine.Red.Engineer.MarkBreakdown(redSymbol);

        Assert.Throws<InvalidOperationException>(() => engine.DetonateMine(TeamColor.Red, new Coordinate(1, 0)));
    }
}
