namespace CommanderPeriscope.Lobby;

/// <summary>Generates a fierce-sounding default submarine name for a freshly created team (§3.2).</summary>
public static class SubmarineNameGenerator
{
    private static readonly string[] Adjectives =
    [
        "Ravenous", "Silent", "Iron", "Crimson", "Merciless", "Abyssal", "Rogue", "Savage", "Grim", "Void",
    ];

    private static readonly string[] Nouns =
    [
        "Leviathan", "Kraken", "Marauder", "Harpoon", "Reaper", "Trident", "Serpent", "Wraith", "Tempest", "Depths",
    ];

    public const int MaxLength = 64;

    public static string Generate(Random? random = null)
    {
        random ??= Random.Shared;
        return $"{Adjectives[random.Next(Adjectives.Length)]} {Nouns[random.Next(Nouns.Length)]}";
    }
}
