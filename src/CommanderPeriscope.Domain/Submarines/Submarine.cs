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

    /// <summary>
    /// "Mandatory Damage Report" only: set after every direction order, cleared the moment the Engineer marks
    /// any breakdown. While true and the option is on, the Captain's next order is blocked — the Engineer's
    /// marking action is what unblocks it, per the printed rule.
    /// </summary>
    public bool PendingDamageReport { get; private set; }

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

    /// <summary>Called by GameEngine after a direction order when the Mandatory Damage Report option is on.</summary>
    public void SetPendingDamageReport() => PendingDamageReport = true;

    /// <summary>Called by GameEngine when the Engineer marks a breakdown, lifting the block on the Captain's next order.</summary>
    public void ClearPendingDamageReport() => PendingDamageReport = false;

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
        PendingDamageReport = false;
    }
}
