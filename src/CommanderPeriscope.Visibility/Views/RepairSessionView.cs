using CommanderPeriscope.Domain.Common;

namespace CommanderPeriscope.Visibility.Views;

/// <summary>
/// Public repair-window state — safe for every connected player, since a surfaced sub and its repair timer
/// are already visible/audible at the table. Drives the sliding repair panel and its per-role indicator lights.
/// </summary>
public sealed record RepairSessionView(
    bool Active,
    bool RealTime,
    TeamColor? SurfacingTeam,
    IReadOnlyList<CrewRole> SecuredRoles,
    int? BonusActionsRemaining);
