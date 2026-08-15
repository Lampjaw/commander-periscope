using CommanderPeriscope.Domain.Common;

namespace CommanderPeriscope.Visibility.Views;

/// <summary>
/// The persistent status bar every player sees for both teams at all times: hull damage and surfaced state.
/// Never carries position, trail, or breakdown detail — those stay scoped to the owning team's own roles.
/// </summary>
public sealed record StatusBarView(TeamColor Team, int DamageChecked, int DamageCapacity, bool Surfaced);
