using CommanderPeriscope.Domain.Common;
using CommanderPeriscope.Domain.Systems;

namespace CommanderPeriscope.Visibility.Views;

/// <summary>
/// The deliberate, one-moment information leak when a sub surfaces: the opposing team learns which system
/// color(s) are currently broken and the revealed sector. It never carries position, trail, or mine data —
/// those stay private even while surfaced.
/// </summary>
public sealed record SurfaceRevealPayload(TeamColor SurfacedTeam, int? Sector, IReadOnlyList<SystemColor> BlockedColors);
