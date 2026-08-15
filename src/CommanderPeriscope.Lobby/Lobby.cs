using CommanderPeriscope.Domain.Common;
using CommanderPeriscope.Domain.Game;

namespace CommanderPeriscope.Lobby;

/// <summary>One session: a 5-character-code-addressed lobby that becomes an in-progress game and can return to lobby for a rematch.</summary>
public sealed class Lobby(string code, string hostPlayerId, LobbyOptions options)
{
    /// <summary>Guards every mutation below — callers must take this lock before touching lobby state.</summary>
    public object Lock { get; } = new();

    public string Code { get; } = code;
    public string HostPlayerId { get; set; } = hostPlayerId;
    public LobbyOptions Options { get; set; } = options;
    public LobbyStatus Status { get; set; } = LobbyStatus.Open;
    public PauseReason PauseReason { get; set; } = PauseReason.None;

    public Dictionary<string, Player> Players { get; } = [];
    public TeamRoster Red { get; } = new(TeamColor.Red);
    public TeamRoster Blue { get; } = new(TeamColor.Blue);

    public GameEngine? Game { get; set; }

    public int Capacity => Options.RedTeamSize + Options.BlueTeamSize;

    public TeamRoster RosterFor(TeamColor team) => team == TeamColor.Red ? Red : Blue;

    public TeamRoster? RosterOf(string playerId) => Red.IsSeated(playerId) ? Red : Blue.IsSeated(playerId) ? Blue : null;
}
