using CommanderPeriscope.Domain.Common;
using CommanderPeriscope.Domain.Game;
using CommanderPeriscope.Lobby;

namespace CommanderPeriscope.Web.Tests.TestSupport;

internal static class LobbySeeding
{
    public const string RedCaptain = "red1";
    public const string RedRadio = "red2";
    public const string BlueCaptain = "blue1";
    public const string BlueRadio = "blue2";

    /// <summary>
    /// A ready-to-play 2v2 game (Captain + Radio Operator per side — First Mate and Engineer both alias to
    /// Captain at this team size, so a single Captain connection can exercise every role's intents).
    /// </summary>
    public static Lobby.Lobby CreateStartedGame(LobbyService service, PacingMode pacing = PacingMode.RealTime, GameMode mode = GameMode.Hunt, string mapId = "10x10")
    {
        var lobby = service.CreateLobby(RedCaptain, "Red Captain", mapId);
        service.UpdateOptions(lobby.Code, RedCaptain, lobby.Options with { RedTeamSize = 2, BlueTeamSize = 2, Pacing = pacing, Mode = mode });

        service.Join(lobby.Code, RedRadio, "Red Radio");
        service.Join(lobby.Code, BlueCaptain, "Blue Captain");
        service.Join(lobby.Code, BlueRadio, "Blue Radio");

        service.ClaimRole(lobby.Code, RedCaptain, TeamColor.Red, CrewRole.Captain);
        service.ClaimRole(lobby.Code, RedRadio, TeamColor.Red, CrewRole.RadioOperator);
        service.ClaimRole(lobby.Code, BlueCaptain, TeamColor.Blue, CrewRole.Captain);
        service.ClaimRole(lobby.Code, BlueRadio, TeamColor.Blue, CrewRole.RadioOperator);

        foreach (var playerId in new[] { RedCaptain, RedRadio, BlueCaptain, BlueRadio })
        {
            service.SetReady(lobby.Code, playerId, true);
        }

        service.StartGame(lobby.Code, RedCaptain);
        return lobby;
    }
}
