using TrainingBattles.Core;

namespace TrainingBattles.Core.Tests;

public class AftermathMathTests
{
    /// <summary>A roll source that returns a fixed sequence, then repeats the last value.</summary>
    private static Func<double> Rolls(params double[] values)
    {
        var i = 0;
        return () => values[i < values.Length ? i++ : values.Length - 1];
    }

    [Fact]
    public void WoundedAmongFallen_NoFallen_NoWounded()
    {
        Assert.Equal(0, AftermathMath.WoundedAmongFallen(0, 0.5, 0.5, Rolls(0.0)));
        Assert.Equal(0, AftermathMath.WoundedAmongFallen(-3, 0.5, 0.5, Rolls(0.0)));
    }

    [Fact]
    public void WoundedAmongFallen_PerfectSurgeon_SavesEveryone()
    {
        // Save chance 1.0 → every man walks away unhurt, factor never matters.
        Assert.Equal(0, AftermathMath.WoundedAmongFallen(10, 1.0, 1.0, Rolls(0.999)));
    }

    [Fact]
    public void WoundedAmongFallen_NoSurgeonFullFactor_WoundsEveryone()
    {
        // Save 0 and factor 1 → every fallen man is wounded.
        Assert.Equal(10, AftermathMath.WoundedAmongFallen(10, 0.0, 1.0, Rolls(0.5)));
    }

    [Fact]
    public void WoundedAmongFallen_NoSurgeonZeroFactor_WoundsNobody()
    {
        Assert.Equal(0, AftermathMath.WoundedAmongFallen(10, 0.0, 0.0, Rolls(0.5)));
    }

    [Fact]
    public void WoundedAmongFallen_TwoRollsPerMan_SaveThenFactor()
    {
        // Man 1: save roll 0.9 (fails save at 0.5), factor roll 0.1 (< 0.6 → wounded).
        // Man 2: save roll 0.2 (saved — no factor roll consumed for the factor outcome).
        // Man 3: save roll 0.9 (fails), factor roll 0.9 (not wounded).
        var rolls = Rolls(0.9, 0.1, 0.2, 0.9, 0.9);
        Assert.Equal(1, AftermathMath.WoundedAmongFallen(3, 0.5, 0.6, rolls));
    }

    [Fact]
    public void WoundedAmongFallen_ClampsChancesOutsideZeroOne()
    {
        // Save chance 2.0 clamps to 1.0 → all saved even with high rolls... except roll 0.999 < 1.0 saves.
        Assert.Equal(0, AftermathMath.WoundedAmongFallen(5, 2.0, 1.0, Rolls(0.999)));
        // Factor -1 clamps to 0 → nobody wounded.
        Assert.Equal(0, AftermathMath.WoundedAmongFallen(5, -1.0, -1.0, Rolls(0.5)));
    }

    [Fact]
    public void WoundedAmongFallen_NullRoll_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => AftermathMath.WoundedAmongFallen(1, 0.5, 0.5, null!));
    }

    [Theory]
    [InlineData(100, 75, 75)]
    [InlineData(100, 100, 100)]
    [InlineData(100, 0, 0)]
    [InlineData(0, 75, 0)]
    [InlineData(-50, 75, 0)]     // negative earned keeps nothing
    [InlineData(3, 50, 2)]       // 1.5 rounds away from zero → 2
    [InlineData(100, 150, 100)]  // percent clamps high
    [InlineData(100, -10, 0)]    // percent clamps low
    public void XpKept_ScalesAndClamps(int earned, int percent, int expected)
    {
        Assert.Equal(expected, AftermathMath.XpKept(earned, percent));
    }

    [Theory]
    [InlineData(100, 75, 25)]
    [InlineData(100, 100, 0)]
    [InlineData(100, 0, 100)]
    [InlineData(0, 50, 0)]
    [InlineData(-10, 50, 0)]
    public void XpToRemove_IsTheComplement(int earned, int percent, int expected)
    {
        Assert.Equal(expected, AftermathMath.XpToRemove(earned, percent));
    }

    [Fact]
    public void XpKeptPlusRemoved_AlwaysEqualsEarned()
    {
        for (var earned = 0; earned <= 250; earned += 7)
            for (var pct = 0; pct <= 100; pct += 13)
                Assert.Equal(earned, AftermathMath.XpKept(earned, pct) + AftermathMath.XpToRemove(earned, pct));
    }
}
