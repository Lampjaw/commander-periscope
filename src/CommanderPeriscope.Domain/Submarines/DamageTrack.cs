namespace CommanderPeriscope.Domain.Submarines;

/// <summary>
/// A sub's damage boxes. Capacity is 1 for Sudden Death, 4 for Hunt. Overkill never over-fills the
/// track but always neutralizes — damage in excess of the remaining boxes is not "wasted" or capped away.
/// </summary>
public sealed class DamageTrack(int capacity)
{
    public int Capacity { get; } = capacity;
    public int Checked { get; private set; }
    public bool IsNeutralized => Checked >= Capacity;

    public void ApplyDamage(int amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Damage amount must be positive.");
        }

        Checked = Math.Min(Checked + amount, Capacity);
    }
}
