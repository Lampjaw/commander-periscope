using System.Diagnostics;
using CommanderPeriscope.Domain.Common;
using CommanderPeriscope.Domain.Maps;
using CommanderPeriscope.Web.Tests.TestSupport;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;
using LobbySession = CommanderPeriscope.Lobby.Lobby;

namespace CommanderPeriscope.Web.Tests;

/// <summary>
/// Phase 8 performance/latency check: a full 8-client game (4 players per team, every role independently
/// staffed) is the maximum fan-out this app is designed for (§7.3: single-instance, no horizontal scaling).
/// These aren't tight micro-benchmarks — timing over a real in-process TestServer plus real SignalR framing
/// is inherently noisy — but a generous upper bound still catches the failure mode that actually matters
/// here: an accidental O(n²) broadcast, a blocking call inside the fan-out loop, or a deadlock, any of which
/// would blow past even a loose bound by an order of magnitude.
/// </summary>
public class GameHubPerformanceTests : IAsyncLifetime
{
    private GameHubFixture _fixture = null!;

    public ValueTask InitializeAsync()
    {
        _fixture = new GameHubFixture();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();

    private static readonly string[] RedPlayers = ["redCaptain", "redFirstMate", "redEngineer", "redRadio"];
    private static readonly string[] BluePlayers = ["blueCaptain", "blueFirstMate", "blueEngineer", "blueRadio"];
    private static readonly CrewRole[] Roles = [CrewRole.Captain, CrewRole.FirstMate, CrewRole.Engineer, CrewRole.RadioOperator];

    private LobbySession CreateFullEightPlayerGame()
    {
        var service = _fixture.LobbyService;
        var lobby = service.CreateLobby(RedPlayers[0], "Red Captain", "15x15");

        foreach (var id in RedPlayers.Skip(1).Concat(BluePlayers))
        {
            service.Join(lobby.Code, id, id);
        }

        for (var i = 0; i < 4; i++)
        {
            service.ClaimRole(lobby.Code, RedPlayers[i], TeamColor.Red, Roles[i]);
            service.ClaimRole(lobby.Code, BluePlayers[i], TeamColor.Blue, Roles[i]);
        }

        foreach (var id in RedPlayers.Concat(BluePlayers))
        {
            service.SetReady(lobby.Code, id, true);
        }

        service.StartGame(lobby.Code, RedPlayers[0]);
        return lobby;
    }

    [Fact]
    public async Task FullEightClientGame_MoveBroadcastFanOut_CompletesWellUnderTwoSeconds()
    {
        var lobby = CreateFullEightPlayerGame();

        var connections = new List<(string PlayerId, HubConnection Connection, ReceivedMessages Messages)>();
        foreach (var id in RedPlayers.Concat(BluePlayers))
        {
            var connection = await _fixture.ConnectAsync();
            var messages = ReceivedMessages.AttachTo(connection);
            await connection.InvokeAsync("JoinGame", lobby.Code, id);
            connections.Add((id, connection, messages));
        }

        try
        {
            var (_, redCaptainConn, _) = connections[0];
            await redCaptainConn.InvokeAsync("SetStartingPosition", lobby.Code, RedPlayers[0], new Coordinate(0, 0));

            var stopwatch = Stopwatch.StartNew();
            await redCaptainConn.InvokeAsync("Move", lobby.Code, RedPlayers[0], Direction.East);

            // Move refreshes Red's own Captain + Engineer groups — those two connections are the ones expected
            // to receive a *second* CaptainView/EngineerView (the first came from JoinGame's initial seed).
            var redCaptainMessages = connections[0].Messages;
            var redEngineerMessages = connections[2].Messages;
            await redCaptainMessages.WaitUntilAsync(m => m.Snapshot().Count(x => x.Type == "CaptainView") >= 2, TimeSpan.FromSeconds(2));
            await redEngineerMessages.WaitUntilAsync(m => m.Snapshot().Count(x => x.Type == "EngineerView") >= 2, TimeSpan.FromSeconds(2));
            stopwatch.Stop();

            Console.WriteLine($"8-client Move fan-out (2 targeted recipients out of 8 connections): {stopwatch.ElapsedMilliseconds}ms");
            Assert.True(stopwatch.ElapsedMilliseconds < 2000, $"Move fan-out took {stopwatch.ElapsedMilliseconds}ms, expected well under 2000ms.");
        }
        finally
        {
            foreach (var (_, connection, _) in connections)
            {
                await connection.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task FullEightClientGame_TorpedoBroadcastReachesAllEightConnectionsWellUnderTwoSeconds()
    {
        var lobby = CreateFullEightPlayerGame();
        var engine = lobby.Game!;
        engine.SetStartingPosition(TeamColor.Red, new Coordinate(0, 0));
        engine.SetStartingPosition(TeamColor.Blue, new Coordinate(2, 0));
        for (var i = 0; i < 3; i++)
        {
            engine.MarkGauge(TeamColor.Red, CommanderPeriscope.Domain.Systems.SystemType.Torpedo);
        }

        var connections = new List<(string PlayerId, HubConnection Connection, ReceivedMessages Messages)>();
        foreach (var id in RedPlayers.Concat(BluePlayers))
        {
            var connection = await _fixture.ConnectAsync();
            var messages = ReceivedMessages.AttachTo(connection);
            await connection.InvokeAsync("JoinGame", lobby.Code, id);
            connections.Add((id, connection, messages));
        }

        try
        {
            var (_, redCaptainConn, _) = connections[0];

            // GameToastPayload for the torpedo result broadcasts to AllPlayers — every one of the 8
            // connections should receive it, making this the widest fan-out a single intent can trigger.
            var stopwatch = Stopwatch.StartNew();
            await redCaptainConn.InvokeAsync("FireTorpedo", lobby.Code, RedPlayers[0], new Coordinate(2, 0));

            await Task.WhenAll(connections.Select(c => c.Messages.WaitUntilAsync(m => m.Any("GameToastPayload"), TimeSpan.FromSeconds(2))));
            stopwatch.Stop();

            Console.WriteLine($"8-client Torpedo fan-out (all 8 connections receive the toast): {stopwatch.ElapsedMilliseconds}ms");
            Assert.True(stopwatch.ElapsedMilliseconds < 2000, $"Torpedo fan-out took {stopwatch.ElapsedMilliseconds}ms, expected well under 2000ms.");
        }
        finally
        {
            foreach (var (_, connection, _) in connections)
            {
                await connection.DisposeAsync();
            }
        }
    }
}
