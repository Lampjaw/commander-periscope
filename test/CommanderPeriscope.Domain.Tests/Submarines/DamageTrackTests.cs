using CommanderPeriscope.Domain.Submarines;
using Xunit;

namespace CommanderPeriscope.Domain.Tests.Submarines;

public class DamageTrackTests
{
    [Fact]
    public void ApplyDamage_OverkillStillNeutralizesWithoutOverfillingBoxes()
    {
        var track = new DamageTrack(4);
        track.ApplyDamage(3);

        track.ApplyDamage(2); // only 1 box remained — the extra point of damage isn't wasted or rejected

        Assert.True(track.IsNeutralized);
        Assert.Equal(4, track.Checked); // display never exceeds capacity
    }

    [Fact]
    public void ApplyDamage_SuddenDeathCapacityOneNeutralizesOnAnyHit()
    {
        var track = new DamageTrack(1);
        Assert.False(track.IsNeutralized);

        track.ApplyDamage(1);

        Assert.True(track.IsNeutralized);
    }
}
