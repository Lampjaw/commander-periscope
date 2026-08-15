using CommanderPeriscope.Domain.Common;
using CommanderPeriscope.Domain.Engineering;
using CommanderPeriscope.Domain.Maps;
using CommanderPeriscope.Domain.Systems;

namespace CommanderPeriscope.Domain.Submarines;

public sealed class Submarine(TeamColor team, int damageCapacity)
{
    public TeamColor Team { get; } = team;
    public Coordinate? Position { get; private set; }

    /// <summary>Dots visited since the last surface (or game start) — a move can't revisit any of these.</summary>
    public List<Coordinate> Trail { get; } = [];

    public List<Coordinate> Mines { get; } = [];
    public bool Surfaced { get; private set; }
    public GaugeBoard Gauges { get; } = new();
    public EngineerBoard Engineer { get; } = new();
    public DamageTrack Damage { get; } = new(damageCapacity);

    /// <summary>
    /// False immediately after a system activation until the next direction move (or Silence move) — enforces
    /// "you cannot activate two systems back-to-back". Surfacing doesn't consume this.
    /// </summary>
    public bool CanActivateSystem { get; private set; } = true;

    public void PlaceStartingPosition(Coordinate start)
    {
        if (Position is not null)
        {
            throw new InvalidOperationException("Starting position is already set.");
        }

        Position = start;
        Trail.Add(start);
    }

    /// <summary>Moves one dot in the given direction. Legality (water, not previously visited this leg) is the caller's responsibility to check first.</summary>
    public void MoveTo(Coordinate destination)
    {
        if (Position is null)
        {
            throw new InvalidOperationException("Cannot move before a starting position is set.");
        }

        Position = destination;
        Trail.Add(destination);
        CanActivateSystem = true;
    }

    public void PlaceMine(Coordinate at) => Mines.Add(at);

    /// <summary>Removes and returns the mine at the given dot so its resolution can be applied by the caller.</summary>
    public Coordinate DetonateMine(Coordinate at)
    {
        if (!Mines.Remove(at))
        {
            throw new InvalidOperationException($"No mine placed at {at}.");
        }

        return at;
    }

    /// <summary>Marks a system as activated: clears its gauge and blocks the next activation until a move happens.</summary>
    public void ActivateSystem(SystemType type)
    {
        Gauges.Reset(type);
        CanActivateSystem = false;
    }

    public void Surface()
    {
        Surfaced = true;
        CanActivateSystem = true;
    }

    public void CompleteRepair()
    {
        if (Position is not null)
        {
            Trail.Clear();
            Trail.Add(Position.Value);
        }

        Engineer.ClearAllBreakdowns();
        Surfaced = false;
    }
}
