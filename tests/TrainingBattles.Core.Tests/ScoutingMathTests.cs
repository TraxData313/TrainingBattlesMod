using TrainingBattles.Core;

namespace TrainingBattles.Core.Tests;

public class ScoutingMathTests
{
    [Theory]
    [InlineData(100, 75, 75)]    // the defender's default bar
    [InlineData(100, 125, 125)]  // the attacker's default bar
    [InlineData(90, 75, 68)]     // 67.5 rounds UP — the bar must truly be reached
    [InlineData(1, 75, 1)]       // 0.75 rounds up to 1
    [InlineData(0, 75, 0)]       // an enemy with no scout is always out-scouted
    [InlineData(0, 125, 0)]
    [InlineData(100, 0, 0)]      // a zero threshold gates nothing
    [InlineData(-50, 75, 0)]     // negative inputs are treated as zero
    [InlineData(100, -10, 0)]
    public void RequiredSkill_IsTheCeilingOfTheRatio(int enemy, int percent, int expected)
    {
        Assert.Equal(expected, ScoutingMath.RequiredSkill(enemy, percent));
    }

    [Theory]
    [InlineData(75, 100, 75, true)]    // exactly at the bar passes
    [InlineData(74, 100, 75, false)]   // one under fails
    [InlineData(125, 100, 125, true)]
    [InlineData(124, 100, 125, false)]
    [InlineData(0, 0, 125, true)]      // two scoutless armies: nobody screens anything
    [InlineData(-5, 0, 75, true)]      // negative own skill clamps to zero, bar is zero
    [InlineData(-5, 10, 75, false)]
    public void OutScouts_ComparesAgainstTheBar(int yours, int enemy, int percent, bool expected)
    {
        Assert.Equal(expected, ScoutingMath.OutScouts(yours, enemy, percent));
    }
}
