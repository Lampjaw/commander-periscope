using CommanderPeriscope.Domain.Maps;

namespace CommanderPeriscope.Visibility.Views;

/// <summary>
/// The map's shape — size and which dots are land. Public information: islands are visible to every player
/// at all times on the tabletop board too. Only submarine positions/trails are ever hidden, never the map itself.
/// </summary>
public sealed record MapView(int Size, IReadOnlyList<Coordinate> LandCells);
