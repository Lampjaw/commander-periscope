using CommanderPeriscope.Domain.Maps;
using CommanderPeriscope.Domain.Weapons;
using Xunit;

namespace CommanderPeriscope.Domain.Tests.Weapons;

public class WeaponResolutionTests
{
    [Fact]
    public void Classify_DirectHitOnExactPosition()
    {
        var pos = new Coordinate(5, 5);
        Assert.Equal(ImpactResult.Direct, WeaponResolution.Classify(pos, pos));
    }

    [Theory]
    [InlineData(4, 4)]
    [InlineData(5, 4)]
    [InlineData(6, 6)]
    public void Classify_IndirectOnAnySurroundingDot(int col, int row)
    {
        var target = new Coordinate(5, 5);
        Assert.Equal(ImpactResult.Indirect, WeaponResolution.Classify(new Coordinate(col, row), target));
    }

    [Fact]
    public void Classify_ClearWhenMoreThanOneDotAway()
    {
        var target = new Coordinate(5, 5);
        Assert.Equal(ImpactResult.Clear, WeaponResolution.Classify(new Coordinate(7, 5), target));
    }

    [Theory]
    [InlineData(ImpactResult.Direct, 2)]
    [InlineData(ImpactResult.Indirect, 1)]
    [InlineData(ImpactResult.Clear, 0)]
    public void DamageFor_MatchesRules(ImpactResult result, int expectedDamage)
    {
        Assert.Equal(expectedDamage, WeaponResolution.DamageFor(result));
    }

    [Theory]
    [InlineData(4, 0, true)]
    [InlineData(5, 0, false)]
    [InlineData(2, 2, true)]
    public void IsTorpedoInRange_AllowsUpToFourManhattanDots(int col, int row, bool expectedInRange)
    {
        var origin = new Coordinate(0, 0);
        Assert.Equal(expectedInRange, WeaponResolution.IsTorpedoInRange(origin, new Coordinate(col, row)));
    }

    [Fact]
    public void CausesSelfDamage_TrueOnlyWhenExactlyOneDotAway()
    {
        var own = new Coordinate(5, 5);
        Assert.True(WeaponResolution.CausesSelfDamage(own, new Coordinate(5, 4)));
        Assert.True(WeaponResolution.CausesSelfDamage(own, new Coordinate(6, 6)));
        Assert.False(WeaponResolution.CausesSelfDamage(own, own));
        Assert.False(WeaponResolution.CausesSelfDamage(own, new Coordinate(7, 5)));
    }

    [Fact]
    public void IsCardinallyAdjacent_ExcludesDiagonals()
    {
        var origin = new Coordinate(5, 5);
        Assert.True(WeaponResolution.IsCardinallyAdjacent(origin, new Coordinate(5, 4)));
        Assert.False(WeaponResolution.IsCardinallyAdjacent(origin, new Coordinate(6, 4)));
    }
}
