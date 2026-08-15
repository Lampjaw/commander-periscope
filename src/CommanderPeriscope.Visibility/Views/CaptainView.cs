using CommanderPeriscope.Domain.Maps;

namespace CommanderPeriscope.Visibility.Views;

/// <summary>
/// What the Captain's own client sees: full position/trail/mine detail for their own sub only. Never sent to
/// any group but the owning team's own Captain — the enemy's true position is only ever inferred, never announced.
/// </summary>
public sealed record CaptainView(
    Coordinate? Position,
    int? Sector,
    IReadOnlyList<Coordinate> Trail,
    IReadOnlyList<Coordinate> Mines,
    bool CanActivateSystem);
