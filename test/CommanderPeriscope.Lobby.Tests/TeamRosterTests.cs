using CommanderPeriscope.Domain.Common;
using Xunit;

namespace CommanderPeriscope.Lobby.Tests;

public class TeamRosterTests
{
    [Fact]
    public void FourPlayerTeam_AllFourRolesAreIndependentlyClaimable()
    {
        var roster = new TeamRoster(TeamColor.Red) { ConfiguredSize = 4 };

        Assert.Equal([CrewRole.Captain, CrewRole.FirstMate, CrewRole.Engineer, CrewRole.RadioOperator], roster.ActiveRoles);
    }

    [Fact]
    public void ThreePlayerTeam_FirstMateMirrorsCaptain()
    {
        var roster = new TeamRoster(TeamColor.Red) { ConfiguredSize = 3 };

        roster.Claim(CrewRole.Captain, "p1");

        Assert.Equal("p1", roster.OccupantOf(CrewRole.FirstMate));
        Assert.DoesNotContain(CrewRole.FirstMate, roster.ActiveRoles);
    }

    [Fact]
    public void TwoPlayerTeam_FirstMateAndEngineerMirrorCaptain()
    {
        var roster = new TeamRoster(TeamColor.Red) { ConfiguredSize = 2 };

        roster.Claim(CrewRole.Captain, "p1");

        Assert.Equal("p1", roster.OccupantOf(CrewRole.FirstMate));
        Assert.Equal("p1", roster.OccupantOf(CrewRole.Engineer));
        Assert.Equal(2, roster.ActiveRoles.Count);
    }

    [Fact]
    public void Claim_ThrowsWhenRoleIsNotIndependentlyClaimableAtCurrentSize()
    {
        var roster = new TeamRoster(TeamColor.Red) { ConfiguredSize = 3 };

        Assert.Throws<InvalidOperationException>(() => roster.Claim(CrewRole.FirstMate, "p1"));
    }

    [Fact]
    public void Claim_ResetsReadyState()
    {
        var roster = new TeamRoster(TeamColor.Red);
        roster.Claim(CrewRole.Captain, "p1");
        roster.SetReady("p1", true);

        roster.Claim(CrewRole.Captain, "p1"); // re-claiming (e.g. switching seats) resets ready

        Assert.False(roster.IsReady("p1"));
    }

    [Fact]
    public void Vacate_FreesTheSeatAndClearsReady()
    {
        var roster = new TeamRoster(TeamColor.Red);
        roster.Claim(CrewRole.Captain, "p1");
        roster.SetReady("p1", true);

        roster.Vacate("p1");

        Assert.Null(roster.OccupantOf(CrewRole.Captain));
        Assert.False(roster.IsReady("p1"));
    }

    [Fact]
    public void SetReady_ThrowsForAnUnseatedPlayer()
    {
        var roster = new TeamRoster(TeamColor.Red);
        Assert.Throws<InvalidOperationException>(() => roster.SetReady("ghost", true));
    }

    [Fact]
    public void AllActiveRolesClaimedAndReady_FalseUntilEverySeatIsFilledAndReady()
    {
        var roster = new TeamRoster(TeamColor.Red) { ConfiguredSize = 2 };
        roster.Claim(CrewRole.Captain, "p1");
        roster.SetReady("p1", true);
        Assert.False(roster.AllActiveRolesClaimedAndReady());

        roster.Claim(CrewRole.RadioOperator, "p2");
        Assert.False(roster.AllActiveRolesClaimedAndReady()); // p2 not ready yet

        roster.SetReady("p2", true);
        Assert.True(roster.AllActiveRolesClaimedAndReady());
    }

    [Fact]
    public void DistinctPlayerIds_CountsEachPlayerOnceEvenWhenDoublingRoles()
    {
        var roster = new TeamRoster(TeamColor.Red) { ConfiguredSize = 2 };
        roster.Claim(CrewRole.Captain, "p1");
        roster.Claim(CrewRole.RadioOperator, "p2");

        Assert.Equal(2, roster.DistinctPlayerIds.Count);
    }
}
