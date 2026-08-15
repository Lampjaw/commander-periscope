using System.Text.Json;
using CommanderPeriscope.Domain.Common;
using CommanderPeriscope.Domain.Game;
using CommanderPeriscope.Domain.Maps;
using CommanderPeriscope.Domain.Systems;
using CommanderPeriscope.Web.Tests.TestSupport;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace CommanderPeriscope.Web.Tests;

public class GameHubGameplayTests : IAsyncLifetime
{
    private GameHubFixture _fixture = null!;

    public ValueTask InitializeAsync()
    {
        _fixture = new GameHubFixture();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public async Task TurnByTurn_RejectsAMoveOutOfTurnThenAllowsItAfterTheOtherTeamActs()
    {
        var lobby = LobbySeeding.CreateStartedGame(_fixture.LobbyService, pacing: PacingMode.TurnByTurn);
        await using var red = await _fixture.ConnectAsync();
        await using var blue = await _fixture.ConnectAsync();
        await red.InvokeAsync("JoinGame", lobby.Code, LobbySeeding.RedCaptain);
        await blue.InvokeAsync("JoinGame", lobby.Code, LobbySeeding.BlueCaptain);
        await red.InvokeAsync("SetStartingPosition", lobby.Code, LobbySeeding.RedCaptain, new Coordinate(0, 0));
        await blue.InvokeAsync("SetStartingPosition", lobby.Code, LobbySeeding.BlueCaptain, new Coordinate(9, 9));

        // Red moves first by default.
        var ex = await Assert.ThrowsAsync<HubException>(() =>
            blue.InvokeAsync("Move", lobby.Code, LobbySeeding.BlueCaptain, Direction.West));
        Assert.Contains("turn", ex.Message, StringComparison.OrdinalIgnoreCase);

        await red.InvokeAsync("Move", lobby.Code, LobbySeeding.RedCaptain, Direction.East);

        // Now it's Blue's turn — Red trying to go again should fail, Blue should succeed.
        await Assert.ThrowsAsync<HubException>(() =>
            red.InvokeAsync("Move", lobby.Code, LobbySeeding.RedCaptain, Direction.East));
        await blue.InvokeAsync("Move", lobby.Code, LobbySeeding.BlueCaptain, Direction.West);
    }

    [Fact]
    public async Task TurnByTurn_DetonateMineIsNeverTurnGated()
    {
        var lobby = LobbySeeding.CreateStartedGame(_fixture.LobbyService, pacing: PacingMode.TurnByTurn);
        var engine = lobby.Game!;
        engine.SetStartingPosition(TeamColor.Red, new Coordinate(0, 0));
        engine.SetStartingPosition(TeamColor.Blue, new Coordinate(9, 9));

        await using var blue = await _fixture.ConnectAsync();
        await blue.InvokeAsync("JoinGame", lobby.Code, LobbySeeding.BlueCaptain);

        // It's Red's turn (default), but detonating a mine "can happen at any time" — should not throw for turn reasons.
        // (No mine placed, so this throws "no mine placed" — proving it got PAST the turn check, not blocked by it.)
        var ex = await Assert.ThrowsAsync<HubException>(() =>
            blue.InvokeAsync("DetonateMine", lobby.Code, LobbySeeding.BlueCaptain, new Coordinate(8, 9)));
        Assert.DoesNotContain("turn", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FireTorpedo_DirectHitEndsTheGameAndBroadcastsGameOverToEveryone()
    {
        var lobby = LobbySeeding.CreateStartedGame(_fixture.LobbyService, mode: GameMode.SuddenDeath);
        var engine = lobby.Game!;
        engine.SetStartingPosition(TeamColor.Red, new Coordinate(0, 0));
        engine.SetStartingPosition(TeamColor.Blue, new Coordinate(2, 0));
        for (var i = 0; i < 3; i++)
        {
            engine.MarkGauge(TeamColor.Red, SystemType.Torpedo);
        }

        await using var red = await _fixture.ConnectAsync();
        await using var blueRadio = await _fixture.ConnectAsync();
        var blueRadioMessages = ReceivedMessages.AttachTo(blueRadio);

        await red.InvokeAsync("JoinGame", lobby.Code, LobbySeeding.RedCaptain);
        await blueRadio.InvokeAsync("JoinGame", lobby.Code, LobbySeeding.BlueRadio);

        await red.InvokeAsync("FireTorpedo", lobby.Code, LobbySeeding.RedCaptain, new Coordinate(2, 0));

        // Blue's Radio Operator is only in the AllPlayers + Team groups, not Red's Captain group — but GameOver
        // and the public toast both go to AllPlayers, so it should still see those.
        await blueRadioMessages.WaitUntilAsync(m => m.Any("GameOverPayload"));

        var result = blueRadioMessages.Last("GameOverPayload").GetProperty("result").GetString();
        Assert.Equal(nameof(WinResult.RedWins), result);
        Assert.True(blueRadioMessages.Any("GameToastPayload"));
    }

    [Fact]
    public async Task Surface_RevealsBlockedColorsToTheEnemyTeamButNeverPositionOrMines()
    {
        var lobby = LobbySeeding.CreateStartedGame(_fixture.LobbyService);
        var engine = lobby.Game!;
        engine.SetStartingPosition(TeamColor.Red, new Coordinate(5, 5));

        var redSymbol = engine.Red.Engineer.Breakdowns.First(b => b is Domain.Engineering.SystemBreakdown sb && sb.Color == SystemColor.Red);
        engine.MarkBreakdown(TeamColor.Red, redSymbol);

        await using var blueRadio = await _fixture.ConnectAsync();
        var blueRadioMessages = ReceivedMessages.AttachTo(blueRadio);
        await blueRadio.InvokeAsync("JoinGame", lobby.Code, LobbySeeding.BlueRadio);

        await using var red = await _fixture.ConnectAsync();
        await red.InvokeAsync("JoinGame", lobby.Code, LobbySeeding.RedCaptain);
        await red.InvokeAsync("Surface", lobby.Code, LobbySeeding.RedCaptain);

        await blueRadioMessages.WaitUntilAsync(m => m.Any("SurfaceRevealPayload"));

        var reveal = blueRadioMessages.Last("SurfaceRevealPayload");
        Assert.Equal(nameof(TeamColor.Red), reveal.GetProperty("surfacedTeam").GetString());
        var blockedColors = reveal.GetProperty("blockedColors").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains(nameof(SystemColor.Red), blockedColors);

        // The reveal payload must never carry position/trail/mine fields at all.
        Assert.False(reveal.TryGetProperty("position", out _));
        Assert.False(reveal.TryGetProperty("trail", out _));
        Assert.False(reveal.TryGetProperty("mines", out _));
    }

    [Fact]
    public async Task RealTimeRepair_CompletingAllFourSectionsClearsTrailAndBreakdowns()
    {
        var lobby = LobbySeeding.CreateStartedGame(_fixture.LobbyService);
        var engine = lobby.Game!;
        engine.SetStartingPosition(TeamColor.Red, new Coordinate(0, 0));
        engine.Move(TeamColor.Red, Direction.East);
        engine.Surface(TeamColor.Red);

        await using var red = await _fixture.ConnectAsync();
        await using var redRadio = await _fixture.ConnectAsync();
        var redMessages = ReceivedMessages.AttachTo(red);
        await red.InvokeAsync("JoinGame", lobby.Code, LobbySeeding.RedCaptain);
        await redRadio.InvokeAsync("JoinGame", lobby.Code, LobbySeeding.RedRadio);

        // 2-player team: RedCaptain aliases Captain/First Mate/Engineer, but Radio Operator stays red2's own seat.
        foreach (var role in new[] { CrewRole.Captain, CrewRole.FirstMate, CrewRole.Engineer })
        {
            await red.InvokeAsync("SecureRealTimeRepairSection", lobby.Code, LobbySeeding.RedCaptain, role);
        }

        await redRadio.InvokeAsync("SecureRealTimeRepairSection", lobby.Code, LobbySeeding.RedRadio, CrewRole.RadioOperator);

        await redMessages.WaitUntilAsync(m => m.Any("CaptainView") &&
            m.Last("CaptainView").GetProperty("trail").GetArrayLength() == 1);

        Assert.Null(engine.ActiveRealTimeRepair);
        Assert.False(engine.Red.Surfaced);
    }
}
