using CommanderPeriscope.Domain.Common;
using CommanderPeriscope.Domain.Engineering;
using CommanderPeriscope.Domain.Game;
using CommanderPeriscope.Domain.Maps;
using CommanderPeriscope.Domain.Submarines;
using CommanderPeriscope.Domain.Systems;
using CommanderPeriscope.Visibility.Views;

namespace CommanderPeriscope.Visibility;

/// <summary>Pure projections from domain state to the role-scoped views defined in <see cref="Views"/>.</summary>
public static class GameStateViews
{
    public static StatusBarView StatusBarFor(Submarine sub) =>
        new(sub.Team, sub.Damage.Checked, sub.Damage.Capacity, sub.Surfaced);

    public static MapView MapViewFor(GameMap map)
    {
        var land = new List<Coordinate>();
        for (var row = 0; row < map.Size; row++)
        {
            for (var col = 0; col < map.Size; col++)
            {
                var coordinate = new Coordinate(col, row);
                if (map.IsLand(coordinate))
                {
                    land.Add(coordinate);
                }
            }
        }

        return new MapView(map.Size, land);
    }

    public static CaptainView CaptainViewFor(GameEngine engine, Submarine sub)
    {
        var gauges = sub.Gauges.All
            .Select(g => new GaugeView(g.Type, g.Charge, g.Capacity, g.IsReady))
            .ToList();

        return new CaptainView(
            sub.Position,
            engine.SectorFor(sub),
            sub.Trail,
            sub.Mines,
            sub.CanActivateSystem,
            gauges,
            BlockedColorsFor(sub),
            sub.PendingDamageReport);
    }

    public static FirstMateView FirstMateViewFor(Submarine sub)
    {
        var gauges = sub.Gauges.All
            .Select(g => new GaugeView(g.Type, g.Charge, g.Capacity, g.IsReady))
            .ToList();

        return new FirstMateView(gauges, sub.Damage.Checked, sub.Damage.Capacity);
    }

    public static EngineerView EngineerViewFor(Submarine sub)
    {
        var breakdowns = sub.Engineer.Breakdowns
            .Select(ToView)
            .ToList();

        return new EngineerView(breakdowns, sub.PendingDamageReport);
    }

    public static RadioOperatorView RadioOperatorViewFor() => new();

    public static RepairSessionView RepairSessionViewFor(GameEngine engine)
    {
        if (engine.ActiveRealTimeRepair is { } realTime)
        {
            var team = engine.Red.Surfaced ? TeamColor.Red : engine.Blue.Surfaced ? TeamColor.Blue : (TeamColor?)null;
            return new RepairSessionView(Active: true, RealTime: true, team, realTime.SecuredRoles.ToList(), BonusActionsRemaining: null);
        }

        if (engine.ActiveTurnByTurnSurface is { IsComplete: false } turnByTurn)
        {
            return new RepairSessionView(Active: true, RealTime: false, turnByTurn.SurfacingTeam, [], turnByTurn.BonusActionsRemaining);
        }

        return new RepairSessionView(Active: false, RealTime: false, SurfacingTeam: null, [], BonusActionsRemaining: null);
    }

    /// <summary>
    /// Which system colors are currently blocked for this sub — the only detail the Engineer's board ever
    /// reveals to the enemy, and only while surfaced. A list, not a set: System.Text.Json cannot deserialize
    /// into IReadOnlySet&lt;T&gt; client-side (it only knows how to instantiate concrete/list-shaped collections).
    /// </summary>
    public static IReadOnlyList<SystemColor> BlockedColorsFor(Submarine sub) =>
        Enum.GetValues<SystemColor>().Where(sub.Engineer.IsSystemBlocked).ToList();

    private static bool IsSystemBlocked(this EngineerBoard board, SystemColor color) =>
        board.Breakdowns.OfType<SystemBreakdown>().Any(s => s.Color == color && s.IsMarked);

    private static BreakdownItemView ToView(BreakdownItem item) => item switch
    {
        SystemBreakdown sb => new BreakdownItemView(sb.Column, sb.Color, sb.CircuitGroupId, IsRadiation: false, sb.IsMarked),
        RadiationBreakdown rb => new BreakdownItemView(rb.Column, Color: null, CircuitGroupId: null, IsRadiation: true, rb.IsMarked),
        _ => throw new ArgumentOutOfRangeException(nameof(item)),
    };
}
