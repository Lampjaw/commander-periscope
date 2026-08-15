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

    /// <summary>Refreshes just one team's own Captain view — the atomic building block for any intent that moves the sub, changes its mines, or flips its activation gate.</summary>
    public static ScopedBroadcast PlanCaptainRefresh(string gameId, GameEngine engine, TeamColor team) =>
        new(GroupNames.Role(gameId, team, CrewRole.Captain), GameStateViews.CaptainViewFor(engine, engine.SubFor(team)));

    /// <summary>Refreshes just one team's own First Mate view — the atomic building block for any intent that charges or clears a gauge.</summary>
    public static ScopedBroadcast PlanFirstMateRefresh(string gameId, GameEngine engine, TeamColor team) =>
        new(GroupNames.Role(gameId, team, CrewRole.FirstMate), GameStateViews.FirstMateViewFor(engine.SubFor(team)));

    /// <summary>Refreshes just one team's own Engineer view — the atomic building block for any intent that clears breakdowns (e.g. completing a repair).</summary>
    public static ScopedBroadcast PlanEngineerRefresh(string gameId, GameEngine engine, TeamColor team) =>
        new(GroupNames.Role(gameId, team, CrewRole.Engineer), GameStateViews.EngineerViewFor(engine.SubFor(team)));

    /// <summary>A public toast — only ever built from information the tabletop rules already require announcing aloud.</summary>
    public static ScopedBroadcast PlanToast(string gameId, string message) =>
        new(GroupNames.AllPlayers(gameId), new GameToastPayload(message));

    /// <summary>Once the game has ended, tells every player how — win, loss, or tie.</summary>
    public static IReadOnlyList<ScopedBroadcast> PlanGameOver(string gameId, GameEngine engine) =>
        engine.IsOver ? [new ScopedBroadcast(GroupNames.AllPlayers(gameId), new GameOverPayload(engine.Result))] : [];

    /// <summary>
    /// After a repair completes (real-time or turn-by-turn): the trail clears, breakdowns wipe, and the sub
    /// stops being surfaced — refresh that team's Captain, Engineer, and both teams' public status bars.
    /// </summary>
    public static IReadOnlyList<ScopedBroadcast> PlanRepairComplete(string gameId, GameEngine engine, TeamColor team) =>
        [.. PlanStatusBars(gameId, engine), PlanCaptainRefresh(gameId, engine, team), PlanEngineerRefresh(gameId, engine, team)];
}
