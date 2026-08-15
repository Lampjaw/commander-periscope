namespace CommanderPeriscope.Domain.Systems;

public enum SystemType
{
    Mine,
    Torpedo,
    Silence,
    Drone,
    Sonar,
}

public enum SystemColor
{
    Red,
    Yellow,
    Green,
}

public static class SystemTypeExtensions
{
    /// <summary>Which of the Engineer's three breakdown colors gates this system.</summary>
    public static SystemColor Color(this SystemType type) => type switch
    {
        SystemType.Mine or SystemType.Torpedo => SystemColor.Red,
        SystemType.Silence => SystemColor.Yellow,
        SystemType.Drone or SystemType.Sonar => SystemColor.Green,
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    /// <summary>Only the Captain may activate red/yellow systems; only the First Mate may activate green ones.</summary>
    public static bool IsCaptainSystem(this SystemType type) => type is SystemType.Mine or SystemType.Torpedo or SystemType.Silence;

    public static bool IsFirstMateSystem(this SystemType type) => !type.IsCaptainSystem();
}
