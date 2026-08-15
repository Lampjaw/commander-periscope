using CommanderPeriscope.Domain.Engineering;
using CommanderPeriscope.Domain.Game;
using CommanderPeriscope.Domain.Submarines;
using CommanderPeriscope.Domain.Systems;
using CommanderPeriscope.Visibility.Views;

namespace CommanderPeriscope.Visibility;

/// <summary>Pure projections from domain state to the role-scoped views defined in <see cref="Views"/>.</summary>
public static class GameStateViews
{
    public static StatusBarView StatusBarFor(Submarine sub) =>
        new(sub.Team, sub.Damage.Checked, sub.Damage.Capacity, sub.Surfaced);

    public static CaptainView CaptainViewFor(GameEngine engine, Submarine sub) =>
        new(sub.Position, engine.SectorFor(sub), sub.Trail, sub.Mines, sub.CanActivateSystem);

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

        return new EngineerView(breakdowns);
    }

    public static RadioOperatorView RadioOperatorViewFor() => new();

    /// <summary>Which system colors are currently blocked for this sub — the only detail the Engineer's board ever reveals to the enemy, and only while surfaced.</summary>
    public static IReadOnlySet<SystemColor> BlockedColorsFor(Submarine sub) =>
        Enum.GetValues<SystemColor>().Where(sub.Engineer.IsSystemBlocked).ToHashSet();

    private static bool IsSystemBlocked(this EngineerBoard board, SystemColor color) =>
        board.Breakdowns.OfType<SystemBreakdown>().Any(s => s.Color == color && s.IsMarked);

    private static BreakdownItemView ToView(BreakdownItem item) => item switch
    {
        SystemBreakdown sb => new BreakdownItemView(sb.Column, sb.Color, sb.CircuitGroupId, IsRadiation: false, sb.IsMarked),
        RadiationBreakdown rb => new BreakdownItemView(rb.Column, Color: null, CircuitGroupId: null, IsRadiation: true, rb.IsMarked),
        _ => throw new ArgumentOutOfRangeException(nameof(item)),
    };
}
