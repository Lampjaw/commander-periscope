using CommanderPeriscope.Domain.Common;
using CommanderPeriscope.Domain.Game;
using CommanderPeriscope.Visibility.Views;

namespace CommanderPeriscope.Visibility;

/// <summary>One payload destined for one visibility-scoped group.</summary>
public sealed record ScopedBroadcast(string Group, object Payload);

/// <summary>
/// The single choke point translating a domain-state change into the set of scoped broadcasts it's allowed
/// to produce. Nothing outside this class should ever hand a client a bigger slice of <see cref="GameEngine"/>
/// state than the plans here compute — that's what keeps a client from inferring the enemy sub's position or
/// seeing another role's private breakdown marks.
/// </summary>
public static class ScopedBroadcastPlanner
{
    /// <summary>Both teams' public status bars — safe for every connected player, always.</summary>
    public static IReadOnlyList<ScopedBroadcast> PlanStatusBars(string gameId, GameEngine engine)
    {
        var all = GroupNames.AllPlayers(gameId);
        return
        [
            new ScopedBroadcast(all, GameStateViews.StatusBarFor(engine.Red)),
            new ScopedBroadcast(all, GameStateViews.StatusBarFor(engine.Blue)),
        ];
    }

    /// <summary>The full set of role-scoped views for both teams — used to seed a client on connect/reconnect.</summary>
    public static IReadOnlyList<ScopedBroadcast> PlanFullStateRefresh(string gameId, GameEngine engine)
    {
        var broadcasts = new List<ScopedBroadcast>(PlanStatusBars(gameId, engine));

        foreach (var team in new[] { TeamColor.Red, TeamColor.Blue })
        {
            var sub = engine.SubFor(team);
            broadcasts.Add(new ScopedBroadcast(GroupNames.Role(gameId, team, CrewRole.Captain), GameStateViews.CaptainViewFor(engine, sub)));
            broadcasts.Add(new ScopedBroadcast(GroupNames.Role(gameId, team, CrewRole.FirstMate), GameStateViews.FirstMateViewFor(sub)));
            broadcasts.Add(new ScopedBroadcast(GroupNames.Role(gameId, team, CrewRole.Engineer), GameStateViews.EngineerViewFor(sub)));
            broadcasts.Add(new ScopedBroadcast(GroupNames.Role(gameId, team, CrewRole.RadioOperator), GameStateViews.RadioOperatorViewFor()));
        }

        return broadcasts;
    }

    /// <summary>
    /// After a Move intent resolves: refresh the moving team's own Captain view, and — only if Radio Relay is
    /// enabled — tell the enemy's Radio Operator the heading. Never the position.
    /// </summary>
    public static IReadOnlyList<ScopedBroadcast> PlanMove(string gameId, GameEngine engine, TeamColor team, Direction heading)
    {
        var sub = engine.SubFor(team);
        var broadcasts = new List<ScopedBroadcast>
        {
            new(GroupNames.Role(gameId, team, CrewRole.Captain), GameStateViews.CaptainViewFor(engine, sub)),
        };

        if (engine.Options.RadioRelayEnabled)
        {
            var enemy = team.Opponent();
            broadcasts.Add(new ScopedBroadcast(GroupNames.Role(gameId, enemy, CrewRole.RadioOperator), new RadioRelayPayload(heading)));
        }

        return broadcasts;
    }

    /// <summary>After an Engineer marks a breakdown or Reactor slot: refresh only that team's own Engineer group. Nothing else ever sees this.</summary>
    public static IReadOnlyList<ScopedBroadcast> PlanBreakdownMark(string gameId, GameEngine engine, TeamColor team) =>
        [new ScopedBroadcast(GroupNames.Role(gameId, team, CrewRole.Engineer), GameStateViews.EngineerViewFor(engine.SubFor(team)))];

    /// <summary>After a First Mate marks a gauge: refresh only that team's own First Mate group.</summary>
    public static IReadOnlyList<ScopedBroadcast> PlanGaugeMark(string gameId, GameEngine engine, TeamColor team) =>
        [new ScopedBroadcast(GroupNames.Role(gameId, team, CrewRole.FirstMate), GameStateViews.FirstMateViewFor(engine.SubFor(team)))];

    /// <summary>
    /// After a Surface intent resolves: the status bars flip publicly (surfaced is never hidden), and the
    /// enemy TEAM — every role, not just its Engineer — receives the one-moment reveal of which system colors
    /// are down and the sector. Position, trail, and mines are never part of that reveal.
    /// </summary>
    public static IReadOnlyList<ScopedBroadcast> PlanSurface(string gameId, GameEngine engine, TeamColor team)
    {
        var sub = engine.SubFor(team);
        var broadcasts = new List<ScopedBroadcast>(PlanStatusBars(gameId, engine));

        var reveal = new SurfaceRevealPayload(team, engine.SectorFor(sub), GameStateViews.BlockedColorsFor(sub));
        broadcasts.Add(new ScopedBroadcast(GroupNames.Team(gameId, team.Opponent()), reveal));

        return broadcasts;
    }
}
