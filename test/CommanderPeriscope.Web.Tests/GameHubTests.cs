using CommanderPeriscope.Domain.Common;
using CommanderPeriscope.Domain.Maps;
using CommanderPeriscope.Domain.Systems;
using CommanderPeriscope.Web.Tests.TestSupport;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace CommanderPeriscope.Web.Tests;

public class GameHubTests : IAsyncLifetime
{
    private GameHubFixture _fixture = null!;

    public ValueTask InitializeAsync()
    {
        _fixture = new GameHubFixture();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();

    [Fact]
    public async Task JoinGame_CaptainSeesCaptainFirstMateAndEngineerButNotRadioOperator()
    {
        // At a 2-player team size, First Mate and Engineer both alias to Captain — but Radio Operator is a
        // separate, real seat (held by red2) — so the Captain's own join should never see it.
        var lobby = LobbySeeding.CreateStartedGame(_fixture.LobbyService);
        await using var red = await _fixture.ConnectAsync();
        var redMessages = ReceivedMessages.AttachTo(red);

        await red.InvokeAsync("JoinGame", lobby.Code, LobbySeeding.RedCaptain);
        await redMessages.WaitUntilAsync(m => m.Any("EngineerView"));

        Assert.True(redMessages.Any("StatusBarView"));
        Assert.True(redMessages.Any("CaptainView"));
        Assert.True(redMessages.Any("FirstMateView"));
        Assert.True(redMessages.Any("EngineerView"));
        Assert.False(redMessages.Any("RadioOperatorView"));
    }

    [Fact]
    public async Task JoinGame_RadioOperatorSeesOnlyItsOwnScopeNotCaptainFirstMateOrEngineer()
    {
        var lobby = LobbySeeding.CreateStartedGame(_fixture.LobbyService);
        await using var redRadio = await _fixture.ConnectAsync();
        var messages = ReceivedMessages.AttachTo(redRadio);

        await redRadio.InvokeAsync("JoinGame", lobby.Code, LobbySeeding.RedRadio);
        await messages.WaitUntilAsync(m => m.Any("RadioOperatorView"));

        Assert.True(messages.Any("StatusBarView"));
        Assert.True(messages.Any("RadioOperatorView"));
        Assert.False(messages.Any("CaptainView"));
        Assert.False(messages.Any("FirstMateView"));
        Assert.False(messages.Any("EngineerView"));
    }

    [Fact]
    public async Task JoinGame_RejectsAPlayerNotSeatedInThatLobby()
    {
        var lobby = LobbySeeding.CreateStartedGame(_fixture.LobbyService);
        await using var connection = await _fixture.ConnectAsync();

        var ex = await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("JoinGame", lobby.Code, "not-a-real-player"));
        Assert.Contains("not seated", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Move_RefreshesTheMovingCaptainAndNeverReachesTheEnemy()
    {
        var lobby = LobbySeeding.CreateStartedGame(_fixture.LobbyService);
        await using var red = await _fixture.ConnectAsync();
        await using var blue = await _fixture.ConnectAsync();
        var redMessages = ReceivedMessages.AttachTo(red);
        var blueMessages = ReceivedMessages.AttachTo(blue);

        await red.InvokeAsync("JoinGame", lobby.Code, LobbySeeding.RedCaptain);
        await blue.InvokeAsync("JoinGame", lobby.Code, LobbySeeding.BlueCaptain);
        await blueMessages.WaitUntilAsync(m => m.Any("CaptainView")); // Blue's own join legitimately seeds Blue's own CaptainView.
        var blueCaptainViewsBeforeRedMoved = blueMessages.Snapshot().Count(x => x.Type == "CaptainView");

        await red.InvokeAsync("SetStartingPosition", lobby.Code, LobbySeeding.RedCaptain, new Coordinate(0, 0));
        await redMessages.WaitUntilAsync(m => m.Last("CaptainView").GetProperty("position").ValueKind != System.Text.Json.JsonValueKind.Null);

        await red.InvokeAsync("Move", lobby.Code, LobbySeeding.RedCaptain, Direction.East);
        await redMessages.WaitUntilAsync(m => m.Snapshot().Count(x => x.Type == "CaptainView") >= 3);

        var position = redMessages.Last("CaptainView").GetProperty("position");
        Assert.Equal(1, position.GetProperty("col").GetInt32());
        Assert.Equal(0, position.GetProperty("row").GetInt32());

        // Give any stray broadcast a moment to (not) arrive, then confirm Blue never received anything new from Red's move.
        await Task.Delay(200);
        Assert.Equal(blueCaptainViewsBeforeRedMoved, blueMessages.Snapshot().Count(x => x.Type == "CaptainView"));
    }

    [Fact]
    public async Task Move_ThrowsForTheWrongRole()
    {
        var lobby = LobbySeeding.CreateStartedGame(_fixture.LobbyService);
        await using var redRadio = await _fixture.ConnectAsync();

        await redRadio.InvokeAsync("JoinGame", lobby.Code, LobbySeeding.RedRadio);

        var ex = await Assert.ThrowsAsync<HubException>(() =>
            redRadio.InvokeAsync("Move", lobby.Code, LobbySeeding.RedRadio, Direction.East));
        Assert.Contains("Captain", ex.Message);
    }

    [Fact]
    public async Task MarkGauge_OnlyRefreshesTheOwnTeamsFirstMateGroup()
    {
        var lobby = LobbySeeding.CreateStartedGame(_fixture.LobbyService);
        await using var red = await _fixture.ConnectAsync();
        await using var blue = await _fixture.ConnectAsync();
        var redMessages = ReceivedMessages.AttachTo(red);
        var blueMessages = ReceivedMessages.AttachTo(blue);

        await red.InvokeAsync("JoinGame", lobby.Code, LobbySeeding.RedCaptain);
        await blue.InvokeAsync("JoinGame", lobby.Code, LobbySeeding.BlueCaptain);
        await blueMessages.WaitUntilAsync(m => m.Any("FirstMateView"));
        var blueFirstMateViewsBefore = blueMessages.Snapshot().Count(x => x.Type == "FirstMateView");

        // 2-player team: Red's Captain also holds First Mate, so this is a legal call for red1.
        await red.InvokeAsync("MarkGauge", lobby.Code, LobbySeeding.RedCaptain, SystemType.Torpedo);
        await redMessages.WaitUntilAsync(m => m.Snapshot().Count(x => x.Type == "FirstMateView") >= 2);

        await Task.Delay(200);
        Assert.Equal(blueFirstMateViewsBefore, blueMessages.Snapshot().Count(x => x.Type == "FirstMateView"));
    }

    [Fact]
    public async Task MarkBreakdown_NeverReachesAnyGroupButTheOwnTeamsEngineer()
    {
        var lobby = LobbySeeding.CreateStartedGame(_fixture.LobbyService);
        await using var red = await _fixture.ConnectAsync();
        await using var blue = await _fixture.ConnectAsync();
        var redMessages = ReceivedMessages.AttachTo(red);
        var blueMessages = ReceivedMessages.AttachTo(blue);

        await red.InvokeAsync("JoinGame", lobby.Code, LobbySeeding.RedCaptain);
        await blue.InvokeAsync("JoinGame", lobby.Code, LobbySeeding.BlueCaptain);
        await blueMessages.WaitUntilAsync(m => m.Any("EngineerView"));
        var blueEngineerViewsBefore = blueMessages.Snapshot().Count(x => x.Type == "EngineerView");

        await red.InvokeAsync("MarkBreakdown", lobby.Code, LobbySeeding.RedCaptain, 0);
        await redMessages.WaitUntilAsync(m => m.Snapshot().Count(x => x.Type == "EngineerView") >= 2);

        var view = redMessages.Last("EngineerView");
        var breakdowns = view.GetProperty("breakdowns");
        Assert.True(breakdowns[0].GetProperty("isMarked").GetBoolean());

        await Task.Delay(200);
        Assert.Equal(blueEngineerViewsBefore, blueMessages.Snapshot().Count(x => x.Type == "EngineerView"));
    }
}
