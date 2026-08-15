using CommanderPeriscope.Domain.Common;

namespace CommanderPeriscope.Visibility;

/// <summary>
/// The canonical SignalR group taxonomy: one group per visibility scope, not per team. A role-scoped group
/// only ever receives that role's own hidden state; nothing broadcasts raw full game state to everyone.
/// </summary>
public static class GroupNames
{
    /// <summary>Every connected player, for information that's public to the whole game (status bars, toasts, win/loss).</summary>
    public static string AllPlayers(string gameId) => $"game:{gameId}:all";

    /// <summary>Every player on one team, for information public to that team but not the enemy (e.g. a surface reveal).</summary>
    public static string Team(string gameId, TeamColor team) => $"game:{gameId}:team:{team}";

    /// <summary>One specific role on one team — the only scope allowed to carry that role's private state.</summary>
    public static string Role(string gameId, TeamColor team, CrewRole role) => $"game:{gameId}:team:{team}:{role}";
}
