using CommanderPeriscope.Domain.Common;
using CommanderPeriscope.Domain.Systems;

namespace CommanderPeriscope.Visibility.Views;

public sealed record BreakdownItemView(Direction Column, SystemColor? Color, int? CircuitGroupId, bool IsRadiation, bool IsMarked);

/// <summary>
/// What the Engineer's own client sees: the full breakdown board, marks and all. This is the single most
/// sensitive view in the game — it must never be sent to any group except the owning team's own Engineer.
/// </summary>
public sealed record EngineerView(IReadOnlyList<BreakdownItemView> Breakdowns);
