using CommanderPeriscope.Domain.Common;
using CommanderPeriscope.Domain.Game;
using Xunit;

namespace CommanderPeriscope.Lobby.Tests;

public class LobbyServiceTests
{
    private static LobbyService NewService() => new(new InMemoryLobbyRepository());

    /// <summary>Seats a 2v2 game, with <paramref name="hostId"/> (default "r1") as the Red Captain — the host is always one of the seated players.</summary>
    private static Lobby CreateAndSeatMinimalLobby(LobbyService service, string hostId = "r1")
    {
        var lobby = service.CreateLobby(hostId, "Red Captain", "10x10");
        service.UpdateOptions(lobby.Code, hostId, lobby.Options with { RedTeamSize = 2, BlueTeamSize = 2 });

        service.Join(lobby.Code, "r2", "Red Radio");
        service.Join(lobby.Code, "b1", "Blue Captain");
        service.Join(lobby.Code, "b2", "Blue Radio");

        service.ClaimRole(lobby.Code, hostId, TeamColor.Red, CrewRole.Captain);
        service.ClaimRole(lobby.Code, "r2", TeamColor.Red, CrewRole.RadioOperator);
        service.ClaimRole(lobby.Code, "b1", TeamColor.Blue, CrewRole.Captain);
        service.ClaimRole(lobby.Code, "b2", TeamColor.Blue, CrewRole.RadioOperator);

        foreach (var playerId in new[] { hostId, "r2", "b1", "b2" })
        {
            service.SetReady(lobby.Code, playerId, true);
        }

        return lobby;
    }

    [Fact]
    public void CreateLobby_GeneratesCodeAndSeatsHostAsAPlayer()
    {
        var service = NewService();
        var lobby = service.CreateLobby("host", "Host", "10x10");

        Assert.Equal(5, lobby.Code.Length);
        Assert.Contains("host", lobby.Players.Keys);
        Assert.Null(lobby.RosterOf("host")); // joined the session, but hasn't claimed a role yet
    }

    [Fact]
    public void Join_AddsANewPlayerToTheLobby()
    {
        var service = NewService();
        var lobby = service.CreateLobby("host", "Host", "10x10");

        service.Join(lobby.Code, "p2", "Second Player");

        Assert.Contains("p2", lobby.Players.Keys);
    }

    [Fact]
    public void Join_AllowsMoreConnectedPlayersThanTheConfiguredCapacity()
    {
        // No hard cap on joining — an over-capacity lobby is trimmed at StartGame instead (see below).
        var service = NewService();
        var lobby = CreateAndSeatMinimalLobby(service); // already at 2v2 capacity

        service.Join(lobby.Code, "extra", "One More");

        Assert.Contains("extra", lobby.Players.Keys);
    }

    [Fact]
    public void Join_ThrowsOnceTheGameHasStarted()
    {
        var service = NewService();
        var lobby = CreateAndSeatMinimalLobby(service);
        service.StartGame(lobby.Code, "r1");

        Assert.Throws<InvalidOperationException>(() => service.Join(lobby.Code, "latecomer", "Nope"));
    }

    [Fact]
    public void ClaimRole_AssignsTheSeat()
    {
        var service = NewService();
        var lobby = service.CreateLobby("host", "Host", "10x10");
        service.Join(lobby.Code, "p1", "Player 1");

        service.ClaimRole(lobby.Code, "p1", TeamColor.Red, CrewRole.Captain);

        Assert.Equal("p1", lobby.Red.OccupantOf(CrewRole.Captain));
    }

    [Fact]
    public void ClaimRole_SwitchingSeatsReleasesThePreviousOne()
    {
        var service = NewService();
        var lobby = service.CreateLobby("host", "Host", "10x10");
        service.Join(lobby.Code, "p1", "Player 1");
        service.ClaimRole(lobby.Code, "p1", TeamColor.Red, CrewRole.Captain);

        service.ClaimRole(lobby.Code, "p1", TeamColor.Blue, CrewRole.Engineer);

        Assert.Null(lobby.Red.OccupantOf(CrewRole.Captain));
        Assert.Equal("p1", lobby.Blue.OccupantOf(CrewRole.Engineer));
    }

    [Fact]
    public void ClaimRole_ThrowsWhenAlreadyClaimedByAnotherPlayer()
    {
        var service = NewService();
        var lobby = service.CreateLobby("host", "Host", "10x10");
        service.Join(lobby.Code, "p1", "Player 1");
        service.Join(lobby.Code, "p2", "Player 2");
        service.ClaimRole(lobby.Code, "p1", TeamColor.Red, CrewRole.Captain);

        Assert.Throws<InvalidOperationException>(() => service.ClaimRole(lobby.Code, "p2", TeamColor.Red, CrewRole.Captain));
    }

    [Fact]
    public void ClaimRole_ThrowsForARoleThatDoesNotExistAtTheCurrentTeamSize()
    {
        var service = NewService();
        var lobby = service.CreateLobby("host", "Host", "10x10");
        service.UpdateOptions(lobby.Code, "host", lobby.Options with { RedTeamSize = 3 });
        service.Join(lobby.Code, "p1", "Player 1");

        Assert.Throws<InvalidOperationException>(() => service.ClaimRole(lobby.Code, "p1", TeamColor.Red, CrewRole.FirstMate));
    }

    [Fact]
    public void RenameSubmarine_OnlyTheCaptainCanRename()
    {
        var service = NewService();
        var lobby = service.CreateLobby("host", "Host", "10x10");
        service.Join(lobby.Code, "p1", "Player 1");
        service.ClaimRole(lobby.Code, "p1", TeamColor.Red, CrewRole.RadioOperator);

        Assert.Throws<InvalidOperationException>(() => service.RenameSubmarine(lobby.Code, "p1", TeamColor.Red, "The Impostor"));
    }

    [Fact]
    public void RenameSubmarine_RejectsEmptyOrWhitespaceNames()
    {
        var service = NewService();
        var lobby = service.CreateLobby("host", "Host", "10x10");
        service.Join(lobby.Code, "p1", "Player 1");
        service.ClaimRole(lobby.Code, "p1", TeamColor.Red, CrewRole.Captain);

        Assert.Throws<ArgumentException>(() => service.RenameSubmarine(lobby.Code, "p1", TeamColor.Red, "   "));
    }

    [Fact]
    public void RenameSubmarine_RejectsNamesOverTheLengthLimit()
    {
        var service = NewService();
        var lobby = service.CreateLobby("host", "Host", "10x10");
        service.Join(lobby.Code, "p1", "Player 1");
        service.ClaimRole(lobby.Code, "p1", TeamColor.Red, CrewRole.Captain);

        Assert.Throws<ArgumentException>(() => service.RenameSubmarine(lobby.Code, "p1", TeamColor.Red, new string('x', 65)));
    }

    [Fact]
    public void UpdateOptions_ThrowsForNonHost()
    {
        var service = NewService();
        var lobby = service.CreateLobby("host", "Host", "10x10");

        Assert.Throws<InvalidOperationException>(() => service.UpdateOptions(lobby.Code, "not-host", lobby.Options));
    }

    [Fact]
    public void UpdateOptions_ThrowsOnceTheGameHasStarted()
    {
        var service = NewService();
        var lobby = CreateAndSeatMinimalLobby(service);
        service.StartGame(lobby.Code, "r1");

        Assert.Throws<InvalidOperationException>(() => service.UpdateOptions(lobby.Code, "r1", lobby.Options));
    }

    [Fact]
    public void UpdateOptions_RejectsInvalidTeamSizes()
    {
        var service = NewService();
        var lobby = service.CreateLobby("host", "Host", "10x10");

        Assert.Throws<ArgumentException>(() => service.UpdateOptions(lobby.Code, "host", lobby.Options with { RedTeamSize = 5 }));
    }

    [Fact]
    public void UpdateOptions_RejectsShrinkingATeamThatWouldOrphanAClaimedRole()
    {
        var service = NewService();
        var lobby = service.CreateLobby("host", "Host", "10x10");
        service.Join(lobby.Code, "p1", "Player 1");
        service.ClaimRole(lobby.Code, "p1", TeamColor.Red, CrewRole.FirstMate);

        Assert.Throws<InvalidOperationException>(() => service.UpdateOptions(lobby.Code, "host", lobby.Options with { RedTeamSize = 3 }));
    }

    [Fact]
    public void StartGame_ThrowsWhenNotEveryClaimedRoleIsReady()
    {
        var service = NewService();
        var lobby = service.CreateLobby("host", "Host", "10x10");
        service.UpdateOptions(lobby.Code, "host", lobby.Options with { RedTeamSize = 2, BlueTeamSize = 2 });
        service.Join(lobby.Code, "r1", "R1");
        service.ClaimRole(lobby.Code, "r1", TeamColor.Red, CrewRole.Captain);

        Assert.Throws<InvalidOperationException>(() => service.StartGame(lobby.Code, "host"));
    }

    [Fact]
    public void StartGame_ThrowsForNonHost()
    {
        var service = NewService();
        var lobby = CreateAndSeatMinimalLobby(service);

        Assert.Throws<InvalidOperationException>(() => service.StartGame(lobby.Code, "r2"));
    }

    [Fact]
    public void StartGame_DropsConnectedPlayersWhoNeverClaimedASeat()
    {
        var service = NewService();
        var lobby = CreateAndSeatMinimalLobby(service);
        service.Join(lobby.Code, "spectator", "Never Seated");

        service.StartGame(lobby.Code, "r1");

        Assert.DoesNotContain("spectator", lobby.Players.Keys);
        Assert.Contains("r1", lobby.Players.Keys);
    }

    [Fact]
    public void StartGame_TransitionsToInProgressAndBuildsTheGameEngine()
    {
        var service = NewService();
        var lobby = CreateAndSeatMinimalLobby(service);

        service.StartGame(lobby.Code, "r1");

        Assert.Equal(LobbyStatus.InProgress, lobby.Status);
        Assert.NotNull(lobby.Game);
    }

    [Fact]
    public void LobbyChanged_FiresOnJoin()
    {
        var service = NewService();
        var lobby = service.CreateLobby("host", "Host", "10x10");
        string? firedFor = null;
        service.LobbyChanged += code => firedFor = code;

        service.Join(lobby.Code, "p1", "Player 1");

        Assert.Equal(lobby.Code, firedFor);
    }
}
