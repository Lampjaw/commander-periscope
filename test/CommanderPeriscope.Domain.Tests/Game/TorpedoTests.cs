using CommanderPeriscope.Domain.Common;
using CommanderPeriscope.Domain.Maps;
using CommanderPeriscope.Domain.Systems;
using CommanderPeriscope.Domain.Tests.TestSupport;
using CommanderPeriscope.Domain.Weapons;
using Xunit;

namespace CommanderPeriscope.Domain.Tests.Game;

public class TorpedoTests
{
    [Fact]
    public void FireTorpedo_ThrowsWhenGaugeNotReady()
    {
        var engine = GameEngineFactory.CreateHunt();
        Assert.Throws<InvalidOperationException>(() => engine.FireTorpedo(TeamColor.Red, new Coordinate(1, 0)));
    }

    [Fact]
    public void FireTorpedo_ThrowsWhenOutOfRange()
    {
        var engine = GameEngineFactory.CreateHunt();
        GameEngineFactory.FullyChargeGauge(engine, TeamColor.Red, SystemType.Torpedo);

        Assert.Throws<InvalidOperationException>(() => engine.FireTorpedo(TeamColor.Red, new Coordinate(5, 0)));
    }

    [Fact]
    public void FireTorpedo_DirectHitDealsTwoDamage()
    {
        var engine = GameEngineFactory.CreateHunt(blueStart: new Coordinate(2, 0));
        GameEngineFactory.FullyChargeGauge(engine, TeamColor.Red, SystemType.Torpedo);

        var result = engine.FireTorpedo(TeamColor.Red, new Coordinate(2, 0));

        Assert.Equal(ImpactResult.Direct, result.Impact);
        Assert.Equal(2, engine.Blue.Damage.Checked);
    }

    [Fact]
    public void FireTorpedo_IndirectHitDealsOneDamage()
    {
        var engine = GameEngineFactory.CreateHunt(blueStart: new Coordinate(3, 0));
        GameEngineFactory.FullyChargeGauge(engine, TeamColor.Red, SystemType.Torpedo);

        var result = engine.FireTorpedo(TeamColor.Red, new Coordinate(2, 0));

        Assert.Equal(ImpactResult.Indirect, result.Impact);
        Assert.Equal(1, engine.Blue.Damage.Checked);
    }

    [Fact]
    public void FireTorpedo_ClearWhenFarFromTarget()
    {
        var engine = GameEngineFactory.CreateHunt();
        GameEngineFactory.FullyChargeGauge(engine, TeamColor.Red, SystemType.Torpedo);

        var result = engine.FireTorpedo(TeamColor.Red, new Coordinate(4, 0));

        Assert.Equal(ImpactResult.Clear, result.Impact);
        Assert.Equal(0, engine.Blue.Damage.Checked);
    }

    [Fact]
    public void FireTorpedo_CloseRangeCausesSelfDamage()
    {
        var engine = GameEngineFactory.CreateHunt();
        GameEngineFactory.FullyChargeGauge(engine, TeamColor.Red, SystemType.Torpedo);

        engine.FireTorpedo(TeamColor.Red, new Coordinate(1, 0));

        Assert.Equal(1, engine.Red.Damage.Checked);
    }

    [Fact]
    public void FireTorpedo_DestroysMineOnExactImpactWithoutDetonatingIt()
    {
        var engine = GameEngineFactory.CreateHunt();
        GameEngineFactory.FullyChargeGauge(engine, TeamColor.Red, SystemType.Mine);
        engine.PlaceMine(TeamColor.Red, new Coordinate(1, 0)); // activation — a move must happen before the next one
        engine.Move(TeamColor.Red, Direction.South); // Red now at (0,1)

        GameEngineFactory.FullyChargeGauge(engine, TeamColor.Red, SystemType.Torpedo);
        engine.FireTorpedo(TeamColor.Red, new Coordinate(1, 0));

        Assert.Empty(engine.Red.Mines);
        // Destroyed outright, not detonated — only the close-range torpedo penalty applies, not a mine hit too.
        Assert.Equal(1, engine.Red.Damage.Checked);
    }

    [Fact]
    public void FireTorpedo_ChainDetonatesAdjacentMine()
    {
        var engine = GameEngineFactory.CreateHunt();
        engine.Move(TeamColor.Red, Direction.East); // Red now at (1,0), so (2,0) is cardinally adjacent.
        GameEngineFactory.FullyChargeGauge(engine, TeamColor.Red, SystemType.Mine);
        engine.PlaceMine(TeamColor.Red, new Coordinate(2, 0)); // activation — a move must happen before the next one
        engine.Move(TeamColor.Red, Direction.South); // Red now at (1,1)

        GameEngineFactory.FullyChargeGauge(engine, TeamColor.Red, SystemType.Torpedo);
        // Impact at (3,0) is adjacent (Chebyshev 1) to the mine at (2,0), chain-detonating it.
        engine.FireTorpedo(TeamColor.Red, new Coordinate(3, 0));

        Assert.Empty(engine.Red.Mines);
    }
}
