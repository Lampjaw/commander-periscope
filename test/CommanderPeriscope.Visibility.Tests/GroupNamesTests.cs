using CommanderPeriscope.Domain.Common;
using Xunit;

namespace CommanderPeriscope.Visibility.Tests;

public class GroupNamesTests
{
    [Fact]
    public void AllPlayers_IsScopedToTheGame()
    {
        Assert.Equal("game:abc123:all", GroupNames.AllPlayers("abc123"));
    }

    [Fact]
    public void Team_IsScopedToGameAndColor()
    {
        Assert.Equal("game:abc123:team:Red", GroupNames.Team("abc123", TeamColor.Red));
    }

    [Fact]
    public void Role_IsScopedToGameColorAndRole()
    {
        Assert.Equal("game:abc123:team:Blue:Engineer", GroupNames.Role("abc123", TeamColor.Blue, CrewRole.Engineer));
    }

    [Fact]
    public void Role_DiffersPerRoleEvenOnTheSameTeam()
    {
        var captain = GroupNames.Role("g1", TeamColor.Red, CrewRole.Captain);
        var engineer = GroupNames.Role("g1", TeamColor.Red, CrewRole.Engineer);

        Assert.NotEqual(captain, engineer);
    }

    [Fact]
    public void Role_DiffersPerTeamEvenForTheSameRole()
    {
        var redEngineer = GroupNames.Role("g1", TeamColor.Red, CrewRole.Engineer);
        var blueEngineer = GroupNames.Role("g1", TeamColor.Blue, CrewRole.Engineer);

        Assert.NotEqual(redEngineer, blueEngineer);
    }
}
