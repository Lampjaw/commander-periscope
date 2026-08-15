using CommanderPeriscope.Domain.Systems;

namespace CommanderPeriscope.Visibility.Views;

public sealed record GaugeView(SystemType Type, int Charge, int Capacity, bool IsReady);

/// <summary>What the First Mate's own client sees: their own team's gauge charges and damage boxes.</summary>
public sealed record FirstMateView(IReadOnlyList<GaugeView> Gauges, int DamageChecked, int DamageCapacity);
