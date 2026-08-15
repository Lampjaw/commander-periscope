using CommanderPeriscope.Domain.Common;
using CommanderPeriscope.Domain.Maps;
using CommanderPeriscope.Domain.Systems;
using CommanderPeriscope.Domain.Tests.TestSupport;
using Xunit;

namespace CommanderPeriscope.Domain.Tests.Game;

public class MovementTests
{
    [Fact]
    public void Move_UpdatesPositionAndTrail()
    {
        var engine = GameEngineFactory.CreateHunt();

        engine.Move(TeamColor.Red, Direction.East);

        Assert.Equal(new Coordinate(1, 0), engine.Red.Position);
        Assert.Equal([new Coordinate(0, 0), new Coordinate(1, 0)], engine.Red.Trail);
    }

    [Fact]
    public void Move_ThrowsWhenRevisitingCurrentLegTrail()
    {
        var engine = GameEngineFactory.CreateHunt();

        engine.Move(TeamColor.Red, Direction.East);

        // Immediately reversing revisits the starting dot — still part of this leg's trail.
        Assert.Throws<InvalidOperationException>(() => engine.Move(TeamColor.Red, Direction.West));
    }

    [Fact]
    public void Move_ThrowsWhenOffMap()
    {
        var engine = GameEngineFactory.CreateHunt();

        Assert.Throws<InvalidOperationException>(() => engine.Move(TeamColor.Red, Direction.North));
    }

    [Fact]
    public void ActivateSilence_MovesUpToFourDotsWithoutRevealingDirectionSeparately()
    {
        var engine = GameEngineFactory.CreateHunt();
        GameEngineFactory.FullyChargeGauge(engine, TeamColor.Red, SystemType.Silence);

        engine.ActivateSilence(TeamColor.Red, Direction.East, 3);

        Assert.Equal(new Coordinate(3, 0), engine.Red.Position);
        Assert.False(engine.Red.Gauges.IsReady(SystemType.Silence));
    }

    [Fact]
    public void Move_UpdatesSector()
    {
        var engine = GameEngineFactory.CreateHunt();

        Assert.Equal(1, engine.SectorFor(engine.Red));

        engine.Move(TeamColor.Red, Direction.South);
        engine.Move(TeamColor.Red, Direction.South);
        engine.Move(TeamColor.Red, Direction.South);
        engine.Move(TeamColor.Red, Direction.South);
        engine.Move(TeamColor.Red, Direction.South);

        Assert.Equal(5, engine.SectorFor(engine.Red));
    }
}
