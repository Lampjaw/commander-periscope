namespace CommanderPeriscope.Lobby;

/// <summary>Generates short, human-typeable lobby codes — excludes visually ambiguous characters (0/O, 1/I).</summary>
public static class LobbyCodeGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    public const int Length = 5;

    public static string Generate(Random? random = null)
    {
        random ??= Random.Shared;
        return string.Create(Length, random, static (span, rnd) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = Alphabet[rnd.Next(Alphabet.Length)];
            }
        });
    }
}
