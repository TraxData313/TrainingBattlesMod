using TrainingBattles.Core;

namespace TrainingBattles.Core.Tests;

public class TrainingCooldownTests
{
    [Fact]
    public void IsReady_NeverTrained_AlwaysReady()
    {
        Assert.True(TrainingCooldown.IsReady(nowHours: 100, lastTrainingHours: 0, cooldownHours: 24));
        Assert.True(TrainingCooldown.IsReady(nowHours: 100, lastTrainingHours: -5, cooldownHours: 24));
    }

    [Fact]
    public void IsReady_ZeroCooldown_AlwaysReady()
    {
        Assert.True(TrainingCooldown.IsReady(nowHours: 100, lastTrainingHours: 99.9, cooldownHours: 0));
        Assert.True(TrainingCooldown.IsReady(nowHours: 100, lastTrainingHours: 99.9, cooldownHours: -1));
    }

    [Fact]
    public void IsReady_HonorsTheClock()
    {
        Assert.False(TrainingCooldown.IsReady(nowHours: 110, lastTrainingHours: 100, cooldownHours: 24));
        Assert.True(TrainingCooldown.IsReady(nowHours: 124, lastTrainingHours: 100, cooldownHours: 24));
        Assert.True(TrainingCooldown.IsReady(nowHours: 130, lastTrainingHours: 100, cooldownHours: 24));
    }

    [Fact]
    public void HoursRemaining_CountsDownToZero()
    {
        Assert.Equal(14.0, TrainingCooldown.HoursRemaining(nowHours: 110, lastTrainingHours: 100, cooldownHours: 24), 5);
        Assert.Equal(0.0, TrainingCooldown.HoursRemaining(nowHours: 124, lastTrainingHours: 100, cooldownHours: 24));
        Assert.Equal(0.0, TrainingCooldown.HoursRemaining(nowHours: 500, lastTrainingHours: 100, cooldownHours: 24));
        Assert.Equal(0.0, TrainingCooldown.HoursRemaining(nowHours: 100, lastTrainingHours: 0, cooldownHours: 24));
    }

    // ---- the quartermaster's speed-up ---------------------------------------

    [Theory]
    [InlineData(0, 4.0, 1.0)]      // no steward → the full wait
    [InlineData(300, 4.0, 4.0)]    // master steward → the config ceiling
    [InlineData(150, 4.0, 2.5)]    // halfway
    [InlineData(75, 4.0, 1.75)]    // a quarter along
    [InlineData(-5, 4.0, 1.0)]     // skill clamps low
    [InlineData(400, 4.0, 4.0)]    // skill clamps high
    [InlineData(300, 0.5, 1.0)]    // a ceiling below 1 never SLOWS the clock
    [InlineData(150, 1.0, 1.0)]    // ceiling 1 = the polish off, flat
    public void DivisorForSkill_RunsLinearFromOneToTheCeiling(int skill, double ceiling, double expected)
    {
        Assert.Equal(expected, TrainingCooldown.DivisorForSkill(skill, ceiling), precision: 10);
    }

    [Fact]
    public void DivisorForSkill_DividesTheWaitAsDesigned()
    {
        // The design line: 24h at Steward 0 stays 24h; at 300 it becomes 6h (/4).
        Assert.Equal(24.0, 24.0 / TrainingCooldown.DivisorForSkill(0, 4.0), precision: 10);
        Assert.Equal(6.0, 24.0 / TrainingCooldown.DivisorForSkill(300, 4.0), precision: 10);
    }
}
