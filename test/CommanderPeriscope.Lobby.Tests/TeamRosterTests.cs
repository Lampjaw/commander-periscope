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

    private static TeamRoster FullyStaffedFourPlayerTeam()
    {
        var roster = new TeamRoster(TeamColor.Red) { ConfiguredSize = 4 };
        roster.Claim(CrewRole.Captain, "captain");
        roster.Claim(CrewRole.FirstMate, "firstMate");
        roster.Claim(CrewRole.Engineer, "engineer");
        roster.Claim(CrewRole.RadioOperator, "radio");
        return roster;
    }

    [Fact]
    public void ActingOccupantOf_ReturnsTheRealOccupantWhenConnected()
    {
        var roster = FullyStaffedFourPlayerTeam();

        Assert.Equal("firstMate", roster.ActingOccupantOf(CrewRole.FirstMate, _ => true));
    }

    [Fact]
    public void ActingOccupantOf_FirstMateDisconnected_FallsBackToTheCaptain()
    {
        var roster = FullyStaffedFourPlayerTeam();

        var actingOccupant = roster.ActingOccupantOf(CrewRole.FirstMate, playerId => playerId != "firstMate");

        Assert.Equal("captain", actingOccupant);
    }

    [Fact]
    public void ActingOccupantOf_EngineerDisconnected_FallsBackToTheCaptainWithoutAffectingFirstMate()
    {
        var roster = FullyStaffedFourPlayerTeam();
        bool IsConnected(string playerId) => playerId != "engineer";

        Assert.Equal("captain", roster.ActingOccupantOf(CrewRole.Engineer, IsConnected));
        // Crucially, First Mate is still connected and must NOT be reassigned just because Engineer dropped —
        // a naive "shrink the whole team" implementation would wrongly hand First Mate to the Captain too.
        Assert.Equal("firstMate", roster.ActingOccupantOf(CrewRole.FirstMate, IsConnected));
    }

    [Fact]
    public void ActingOccupantOf_RadioOperatorDisconnected_HasNoFallback()
    {
        var roster = FullyStaffedFourPlayerTeam();

        var actingOccupant = roster.ActingOccupantOf(CrewRole.RadioOperator, playerId => playerId != "radio");

        Assert.Equal("radio", actingOccupant); // still the real (disconnected) occupant — nobody absorbs it
    }

    [Fact]
    public void ActingOccupantOf_CaptainDisconnected_HasNoFallback()
    {
        var roster = FullyStaffedFourPlayerTeam();

        var actingOccupant = roster.ActingOccupantOf(CrewRole.Captain, playerId => playerId != "captain");

        Assert.Equal("captain", actingOccupant);
    }

    [Fact]
    public void ActingOccupantOf_FirstMateAndItsFallbackBothDisconnected_ReturnsTheRealOccupant()
    {
        var roster = FullyStaffedFourPlayerTeam();

        var actingOccupant = roster.ActingOccupantOf(CrewRole.FirstMate, playerId => playerId is not ("firstMate" or "captain"));

        Assert.Equal("firstMate", actingOccupant);
    }
}
