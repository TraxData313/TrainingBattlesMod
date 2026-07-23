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
}
