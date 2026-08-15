namespace CommanderPeriscope.Domain.Common;

public enum TeamColor
{
    Red,
    Blue,
}

public static class TeamColorExtensions
{
    public static TeamColor Opponent(this TeamColor team) => team switch
    {
        TeamColor.Red => TeamColor.Blue,
        TeamColor.Blue => TeamColor.Red,
        _ => throw new ArgumentOutOfRangeException(nameof(team)),
    };
}
