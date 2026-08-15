namespace CommanderPeriscope.Domain.Systems;

/// <summary>One system's charge track: fills one box at a time, empties fully when the system is used.</summary>
public sealed class SystemGauge(SystemType type, int capacity)
{
    public SystemType Type { get; } = type;
    public int Capacity { get; } = capacity;
    public int Charge { get; private set; }

    public bool IsReady => Charge >= Capacity;

    /// <summary>Marks one empty box. Returns true if this mark just made the gauge ready.</summary>
    public bool Mark()
    {
        if (IsReady)
        {
            throw new InvalidOperationException($"{Type} gauge is already full — it can't be marked again until used.");
        }

        Charge++;
        return IsReady;
    }

    /// <summary>Empties the gauge — happens whenever the system is activated.</summary>
    public void Reset() => Charge = 0;
}
