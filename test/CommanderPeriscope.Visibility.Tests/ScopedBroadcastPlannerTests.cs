using CommanderPeriscope.Domain.Common;
using CommanderPeriscope.Domain.Game;
using CommanderPeriscope.Domain.Systems;
using CommanderPeriscope.Visibility.Tests.TestSupport;
using CommanderPeriscope.Visibility.Views;
using Xunit;

namespace CommanderPeriscope.Visibility.Tests;

public class ScopedBroadcastPlannerTests
{
    private const string GameId = "g1";

    [Fact]
    public void PlanFullStateRefresh_EngineerViewOnlyGoesToItsOwnTeamsEngineerGroup()
    {
        var engine = GameEngineFactory.Create();
        var broadcasts = ScopedBroadcastPlanner.PlanFullStateRefresh(GameId, engine);

        var redEngineerGroup = GroupNames.Role(GameId, TeamColor.Red, CrewRole.Engineer);
        var blueEngineerGroup = GroupNames.Role(GameId, TeamColor.Blue, CrewRole.Engineer);

        foreach (var broadcast in broadcasts)
        {
            if (broadcast.Payload is EngineerView)
            {
                Assert.True(broadcast.Group == redEngineerGroup || broadcast.Group == blueEngineerGroup);
            }
        }

        Assert.Contains(broadcasts, b => b.Group == redEngineerGroup && b.Payload is EngineerView);
        Assert.Contains(broadcasts, b => b.Group == blueEngineerGroup && b.Payload is EngineerView);
    }

    [Fact]
    public void PlanFullStateRefresh_CaptainViewOnlyGoesToItsOwnTeamsCaptainGroup()
    {
        var engine = GameEngineFactory.Create();
        var broadcasts = ScopedBroadcastPlanner.PlanFullStateRefresh(GameId, engine);

        var redCaptainGroup = GroupNames.Role(GameId, TeamColor.Red, CrewRole.Captain);
        var blueCaptainGroup = GroupNames.Role(GameId, TeamColor.Blue, CrewRole.Captain);

        foreach (var broadcast in broadcasts)
        {
            if (broadcast.Payload is CaptainView)
            {
                Assert.True(broadcast.Group == redCaptainGroup || broadcast.Group == blueCaptainGroup);
            }
        }
    }

    [Fact]
    public void PlanFullStateRefresh_NoGroupEverReceivesTheEnemysPrivateViews()
    {
        var engine = GameEngineFactory.Create();
        var broadcasts = ScopedBroadcastPlanner.PlanFullStateRefresh(GameId, engine);

        var blueEngineerGroup = GroupNames.Role(GameId, TeamColor.Blue, CrewRole.Engineer);

        var sentToBlueEngineer = broadcasts.Where(b => b.Group == blueEngineerGroup).ToList();
        Assert.All(sentToBlueEngineer, b => Assert.IsType<EngineerView>(b.Payload));
    }

    [Fact]
    public void PlanBreakdownMark_SendsToTheMarkingTeamsOwnEngineerAndCaptainGroups()
    {
        var engine = GameEngineFactory.Create();
        var symbol = engine.Red.Engineer.Breakdowns[0];
        engine.MarkBreakdown(TeamColor.Red, symbol);

        var broadcasts = ScopedBroadcastPlanner.PlanBreakdownMark(GameId, engine, TeamColor.Red);

        Assert.Contains(broadcasts, b => b.Group == GroupNames.Role(GameId, TeamColor.Red, CrewRole.Engineer) && b.Payload is EngineerView);
        Assert.Contains(broadcasts, b => b.Group == GroupNames.Role(GameId, TeamColor.Red, CrewRole.Captain) && b.Payload is CaptainView);
        Assert.Equal(2, broadcasts.Count);
    }

    [Fact]
    public void PlanBreakdownMark_NeverTargetsAnyOtherGroup()
    {
        var engine = GameEngineFactory.Create();
        var symbol = engine.Red.Engineer.Breakdowns[0];
        engine.MarkBreakdown(TeamColor.Red, symbol);

        var broadcasts = ScopedBroadcastPlanner.PlanBreakdownMark(GameId, engine, TeamColor.Red);

        var forbiddenGroups = new[]
        {
            GroupNames.AllPlayers(GameId),
            GroupNames.Team(GameId, TeamColor.Red),
            GroupNames.Team(GameId, TeamColor.Blue),
            GroupNames.Role(GameId, TeamColor.Red, CrewRole.FirstMate),
            GroupNames.Role(GameId, TeamColor.Red, CrewRole.RadioOperator),
            GroupNames.Role(GameId, TeamColor.Blue, CrewRole.Captain),
            GroupNames.Role(GameId, TeamColor.Blue, CrewRole.FirstMate),
            GroupNames.Role(GameId, TeamColor.Blue, CrewRole.Engineer),
            GroupNames.Role(GameId, TeamColor.Blue, CrewRole.RadioOperator),
        };

        Assert.DoesNotContain(broadcasts, b => forbiddenGroups.Contains(b.Group));
    }

    [Fact]
    public void PlanMove_WithRadioRelayDisabled_EmitsNoRelayPayload()
    {
        var engine = GameEngineFactory.Create(new GameOptions(GameMode.Hunt, PacingMode.RealTime, RadioRelayEnabled: false));
        engine.Move(TeamColor.Red, Direction.East);

        var broadcasts = ScopedBroadcastPlanner.PlanMove(GameId, engine, TeamColor.Red, Direction.East);

        Assert.DoesNotContain(broadcasts, b => b.Payload is RadioRelayPayload);
    }

    [Fact]
    public void PlanMove_WithRadioRelayEnabled_SendsHeadingOnlyToTheEnemysRadioOperator()
    {
        var engine = GameEngineFactory.Create(new GameOptions(GameMode.Hunt, PacingMode.RealTime, RadioRelayEnabled: true));
        engine.Move(TeamColor.Red, Direction.East);

        var broadcasts = ScopedBroadcastPlanner.PlanMove(GameId, engine, TeamColor.Red, Direction.East);

        var relay = Assert.Single(broadcasts, b => b.Payload is RadioRelayPayload);
        Assert.Equal(GroupNames.Role(GameId, TeamColor.Blue, CrewRole.RadioOperator), relay.Group);
        Assert.Equal(Direction.East, ((RadioRelayPayload)relay.Payload).Heading);
    }

    [Fact]
    public void RadioRelayPayload_StructurallyCannotCarryMoreThanTheHeading()
    {
        var properties = typeof(RadioRelayPayload).GetProperties();

        var property = Assert.Single(properties);
        Assert.Equal(nameof(RadioRelayPayload.Heading), property.Name);
        Assert.Equal(typeof(Direction), property.PropertyType);
    }

    [Fact]
    public void PlanSurface_RevealsBlockedColorsAndSectorToTheEnemyTeamOnly()
    {
        var engine = GameEngineFactory.Create();
        var redSymbol = engine.Red.Engineer.Breakdowns
            .OfType<Domain.Engineering.SystemBreakdown>()
            .First(s => s.Color == SystemColor.Red);
        engine.MarkBreakdown(TeamColor.Red, redSymbol);
        engine.Surface(TeamColor.Red);

        var broadcasts = ScopedBroadcastPlanner.PlanSurface(GameId, engine, TeamColor.Red);

        var reveal = Assert.Single(broadcasts, b => b.Payload is SurfaceRevealPayload);
        Assert.Equal(GroupNames.Team(GameId, TeamColor.Blue), reveal.Group);

        var payload = (SurfaceRevealPayload)reveal.Payload;
        Assert.Equal(TeamColor.Red, payload.SurfacedTeam);
        Assert.Contains(SystemColor.Red, payload.BlockedColors);
    }

    [Fact]
    public void PlanSurface_NeverLeaksPositionTrailMinesOrTheFullEngineerBoard()
    {
        var engine = GameEngineFactory.Create();
        engine.Surface(TeamColor.Red);

        var broadcasts = ScopedBroadcastPlanner.PlanSurface(GameId, engine, TeamColor.Red);

        Assert.DoesNotContain(broadcasts, b => b.Payload is CaptainView or EngineerView or FirstMateView);
    }

    [Fact]
    public void SurfaceRevealPayload_HasNoPositionalOrTrailData()
    {
        var properties = typeof(SurfaceRevealPayload).GetProperties().Select(p => p.PropertyType).ToList();

        Assert.DoesNotContain(properties, t => t == typeof(Domain.Maps.Coordinate) || t == typeof(Domain.Maps.Coordinate?));
        Assert.DoesNotContain(properties, t => typeof(System.Collections.IEnumerable).IsAssignableFrom(t) && t != typeof(string) && t.IsGenericType && t.GetGenericArguments()[0] == typeof(Domain.Maps.Coordinate));
    }
}
