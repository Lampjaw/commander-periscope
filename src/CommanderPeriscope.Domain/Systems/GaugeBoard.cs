namespace CommanderPeriscope.Domain.Systems;

/// <summary>The First Mate's five gauges, at their tabletop capacities.</summary>
public sealed class GaugeBoard
{
    private static readonly IReadOnlyDictionary<SystemType, int> Capacities = new Dictionary<SystemType, int>
    {
        [SystemType.Mine] = 3,
        [SystemType.Torpedo] = 3,
        [SystemType.Drone] = 4,
        [SystemType.Sonar] = 3,
        [SystemType.Silence] = 6,
    };

    private readonly Dictionary<SystemType, SystemGauge> _gauges;

    public GaugeBoard()
    {
        _gauges = Capacities.ToDictionary(kv => kv.Key, kv => new SystemGauge(kv.Key, kv.Value));
    }

    public SystemGauge this[SystemType type] => _gauges[type];

    public IReadOnlyCollection<SystemGauge> All => _gauges.Values;

    public bool IsReady(SystemType type) => _gauges[type].IsReady;

    /// <summary>Marks one box on the chosen gauge — the First Mate's response to every Captain order.</summary>
    public bool Mark(SystemType type) => _gauges[type].Mark();

    /// <summary>Empties the gauge — happens when its system activates.</summary>
    public void Reset(SystemType type) => _gauges[type].Reset();
}
