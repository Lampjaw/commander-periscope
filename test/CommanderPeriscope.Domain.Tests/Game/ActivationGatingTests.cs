using CommanderPeriscope.Domain.Common;
using CommanderPeriscope.Domain.Engineering;
using CommanderPeriscope.Domain.Systems;
using CommanderPeriscope.Domain.Tests.TestSupport;
using Xunit;

namespace CommanderPeriscope.Domain.Tests.Game;

public class ActivationGatingTests
{
    [Fact]
    public void RequestSonar_ThrowsWhenGaugeNotReady()
    {
        var engine = GameEngineFactory.CreateHunt();
        Assert.Throws<InvalidOperationException>(() => engine.RequestSonar(TeamColor.Red));
    }

    [Fact]
    public void RequestSonar_ThrowsWhenBreakdownBlocksItsColor()
    {
        var engine = GameEngineFactory.CreateHunt();
        GameEngineFactory.FullyChargeGauge(engine, TeamColor.Red, SystemType.Sonar);

        var greenSymbol = engine.Red.Engineer.Breakdowns.OfType<SystemBreakdown>().First(s => s.Color == SystemColor.Green);
        engine.Red.Engineer.MarkBreakdown(greenSymbol);

        Assert.Throws<InvalidOperationException>(() => engine.RequestSonar(TeamColor.Red));
    }

    [Fact]
    public void ActivateSystem_CannotFireTwoSystemsBackToBack()
    {
        var engine = GameEngineFactory.CreateHunt();
        GameEngineFactory.FullyChargeGauge(engine, TeamColor.Red, SystemType.Sonar);
        GameEngineFactory.FullyChargeGauge(engine, TeamColor.Red, SystemType.Drone);

        engine.RequestSonar(TeamColor.Red);

        Assert.Throws<InvalidOperationException>(() => engine.RequestDrone(TeamColor.Red));
    }

    [Fact]
    public void ActivateSystem_AllowedAgainAfterAMoveInBetween()
    {
        var engine = GameEngineFactory.CreateHunt();
        GameEngineFactory.FullyChargeGauge(engine, TeamColor.Red, SystemType.Sonar);
        GameEngineFactory.FullyChargeGauge(engine, TeamColor.Red, SystemType.Drone);

        engine.RequestSonar(TeamColor.Red);
        engine.Move(TeamColor.Red, Direction.East);
        engine.RequestDrone(TeamColor.Red);

        Assert.True(true); // no exception — the second activation succeeded.
    }

    [Fact]
    public void Surfacing_DoesNotConsumeTheBackToBackGate()
    {
        var engine = GameEngineFactory.CreateHunt();
        GameEngineFactory.FullyChargeGauge(engine, TeamColor.Red, SystemType.Sonar);
        GameEngineFactory.FullyChargeGauge(engine, TeamColor.Red, SystemType.Drone);

        engine.RequestSonar(TeamColor.Red);
        engine.Surface(TeamColor.Red);

        Assert.True(engine.Red.CanActivateSystem);
    }
}
