using CommanderPeriscope.Domain.Common;
using Xunit;

namespace CommanderPeriscope.Lobby.Tests;

public class LobbyServiceSessionTests
{
    private static LobbyService NewService() => new(new InMemoryLobbyRepository());

    private static Lobby StartMinimalGame(LobbyService service, bool pauseOnDisconnect = true)
    {
        var lobby = service.CreateLobby("host", "Host", "10x10");
        service.UpdateOptions(lobby.Code, "host", lobby.Options with { RedTeamSize = 2, BlueTeamSize = 2, PauseOnDisconnect = pauseOnDisconnect });

        service.Join(lobby.Code, "r1", "Red Captain");
        service.Join(lobby.Code, "r2", "Red Radio");
        service.Join(lobby.Code, "b1", "Blue Captain");
        service.Join(lobby.Code, "b2", "Blue Radio");
        service.ClaimRole(lobby.Code, "r1", TeamColor.Red, CrewRole.Captain);
        service.ClaimRole(lobby.Code, "r2", TeamColor.Red, CrewRole.RadioOperator);
        service.ClaimRole(lobby.Code, "b1", TeamColor.Blue, CrewRole.Captain);
        service.ClaimRole(lobby.Code, "b2", TeamColor.Blue, CrewRole.RadioOperator);
        foreach (var playerId in new[] { "r1", "r2", "b1", "b2" })
        {
            service.SetReady(lobby.Code, playerId, true);
        }

        service.StartGame(lobby.Code, "host");
        return lobby;
    }

    [Fact]
    public void Pause_HostOnly_FreezesAnInProgressGame()
    {
        var service = NewService();
        var lobby = StartMinimalGame(service);

        service.Pause(lobby.Code, "host");

        Assert.Equal(LobbyStatus.Paused, lobby.Status);
        Assert.Equal(PauseReason.Host, lobby.PauseReason);
    }

    [Fact]
    public void Pause_ThrowsForNonHost()
    {
        var service = NewService();
        var lobby = StartMinimalGame(service);

        Assert.Throws<InvalidOperationException>(() => service.Pause(lobby.Code, "r1"));
    }

    [Fact]
    public void Resume_ClearsAHostPause()
    {
        var service = NewService();
        var lobby = StartMinimalGame(service);
        service.Pause(lobby.Code, "host");

        service.Resume(lobby.Code, "host");

        Assert.Equal(LobbyStatus.InProgress, lobby.Status);
        Assert.Equal(PauseReason.None, lobby.PauseReason);
    }

    [Fact]
    public void HandleDisconnect_AutoPausesWhenOptionEnabled()
    {
        var service = NewService();
        var lobby = StartMinimalGame(service, pauseOnDisconnect: true);

        service.HandleDisconnect(lobby.Code, "r1");

        Assert.Equal(LobbyStatus.Paused, lobby.Status);
        Assert.Equal(PauseReason.Disconnect, lobby.PauseReason);
    }

    [Fact]
    public void HandleDisconnect_DoesNotPauseWhenOptionDisabled()
    {
        var service = NewService();
        var lobby = StartMinimalGame(service, pauseOnDisconnect: false);

        service.HandleDisconnect(lobby.Code, "r1");

        Assert.Equal(LobbyStatus.InProgress, lobby.Status);
    }

    [Fact]
    public void HandleReconnect_ResumesOnlyOnceEveryoneIsBack()
    {
        var service = NewService();
        var lobby = StartMinimalGame(service);
        service.HandleDisconnect(lobby.Code, "r1");
        service.HandleDisconnect(lobby.Code, "b1");

        service.HandleReconnect(lobby.Code, "r1");
        Assert.Equal(LobbyStatus.Paused, lobby.Status); // b1 still gone

        service.HandleReconnect(lobby.Code, "b1");
        Assert.Equal(LobbyStatus.InProgress, lobby.Status);
        Assert.Equal(PauseReason.None, lobby.PauseReason);
    }

    [Fact]
    public void HandleReconnect_DoesNotResumeAHostInitiatedPause()
    {
        var service = NewService();
        var lobby = StartMinimalGame(service);
        service.HandleDisconnect(lobby.Code, "r1");
        service.Pause(lobby.Code, "host"); // host escalates to a manual pause
        service.HandleReconnect(lobby.Code, "r1");

        Assert.Equal(LobbyStatus.Paused, lobby.Status);
        Assert.Equal(PauseReason.Host, lobby.PauseReason);
    }

    [Fact]
    public void RestartToLobby_ReturnsToOpenKeepsRosterAndResetsReady()
    {
        var service = NewService();
        var lobby = StartMinimalGame(service);

        service.RestartToLobby(lobby.Code, "host");

        Assert.Equal(LobbyStatus.Open, lobby.Status);
        Assert.Null(lobby.Game);
        Assert.Equal("r1", lobby.Red.OccupantOf(CrewRole.Captain)); // roster kept
        Assert.False(lobby.Red.IsReady("r1")); // ready reset
    }

    [Fact]
    public void RestartToLobby_AllowsRejoiningAndReadyingUpAgain()
    {
        var service = NewService();
        var lobby = StartMinimalGame(service);
        service.RestartToLobby(lobby.Code, "host");

        service.SetReady(lobby.Code, "r1", true);

        Assert.True(lobby.Red.IsReady("r1"));
    }
}
