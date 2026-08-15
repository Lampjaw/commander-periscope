using CommanderPeriscope.Domain.Common;
using CommanderPeriscope.Domain.Systems;
using CommanderPeriscope.Visibility.Tests.TestSupport;
using Xunit;

namespace CommanderPeriscope.Visibility.Tests;

public class GameStateViewsTests
{
    [Fact]
    public void StatusBarFor_ReflectsDamageAndSurfacedState()
    {
        var engine = GameEngineFactory.Create();

        var view = GameStateViews.StatusBarFor(engine.Red);

        Assert.Equal(TeamColor.Red, view.Team);
        Assert.Equal(0, view.DamageChecked);
        Assert.Equal(4, view.DamageCapacity);
        Assert.False(view.Surfaced);
    }

    [Fact]
    public void CaptainViewFor_ReflectsOwnPositionTrailAndMines()
    {
        var engine = GameEngineFactory.Create();
        engine.Move(TeamColor.Red, Direction.East);

        var view = GameStateViews.CaptainViewFor(engine, engine.Red);

        Assert.Equal(engine.Red.Position, view.Position);
        Assert.Equal(engine.Red.Trail, view.Trail);
        Assert.Equal(1, view.Sector);
    }

    [Fact]
    public void FirstMateViewFor_ReflectsAllFiveGauges()
    {
        var engine = GameEngineFactory.Create();
        engine.MarkGauge(TeamColor.Red, SystemType.Torpedo);

        var view = GameStateViews.FirstMateViewFor(engine.Red);

        Assert.Equal(5, view.Gauges.Count);
        var torpedo = view.Gauges.Single(g => g.Type == SystemType.Torpedo);
        Assert.Equal(1, torpedo.Charge);
        Assert.Equal(3, torpedo.Capacity);
    }

    [Fact]
    public void EngineerViewFor_ReflectsMarkedBreakdowns()
    {
        var engine = GameEngineFactory.Create();
        var symbol = engine.Red.Engineer.Breakdowns[0];
        engine.MarkBreakdown(TeamColor.Red, symbol);

        var view = GameStateViews.EngineerViewFor(engine.Red);

        Assert.Equal(engine.Red.Engineer.Breakdowns.Count, view.Breakdowns.Count);
        Assert.Single(view.Breakdowns, b => b.IsMarked);
    }

    [Fact]
    public void BlockedColorsFor_ReflectsWhichColorsAreCurrentlyMarked()
    {
        var engine = GameEngineFactory.Create();
        var redSymbol = engine.Red.Engineer.Breakdowns
            .OfType<Domain.Engineering.SystemBreakdown>()
            .First(s => s.Color == SystemColor.Red);
        engine.MarkBreakdown(TeamColor.Red, redSymbol);

        var blocked = GameStateViews.BlockedColorsFor(engine.Red);

        Assert.Contains(SystemColor.Red, blocked);
        Assert.DoesNotContain(SystemColor.Green, blocked);
        Assert.DoesNotContain(SystemColor.Yellow, blocked);
    }
}
