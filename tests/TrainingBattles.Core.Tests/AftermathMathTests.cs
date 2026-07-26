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

    // ---- the surgeon's bands -------------------------------------------------

    [Theory]
    [InlineData(3.0, 0.1, 0, 3.0)]       // no doctor → the bad end
    [InlineData(3.0, 0.1, 300, 0.1)]     // master doctor → the good end
    [InlineData(3.0, 0.1, 150, 1.55)]    // halfway
    [InlineData(30.0, 5.0, 150, 17.5)]   // the KIA→wounded defaults, halfway
    [InlineData(15.0, 1.0, 300, 1.0)]    // the downed→wounded defaults, best end
    [InlineData(3.0, 0.1, -5, 3.0)]      // skill clamps low
    [InlineData(3.0, 0.1, 400, 0.1)]     // skill clamps high
    [InlineData(150.0, -5.0, 0, 100.0)]  // ends clamp to 0..100 (they are chances)
    [InlineData(8.0, 8.0, 200, 8.0)]     // equal ends: flat, skill ignored
    public void ChancePercentForSkill_RunsTheBandInEitherDirection(
        double at0, double atCap, int skill, double expected)
    {
        Assert.Equal(expected, AftermathMath.ChancePercentForSkill(at0, atCap, skill), precision: 10);
    }

    [Fact]
    public void JudgeFallen_NoFallen_NothingHappens()
    {
        var verdict = AftermathMath.JudgeFallen(0, 0.5, 0.5, Rolls(0.0));
        Assert.Equal(0, verdict.Died);
        Assert.Equal(0, verdict.Wounded);
        verdict = AftermathMath.JudgeFallen(-3, 0.5, 0.5, Rolls(0.0));
        Assert.Equal(0, verdict.Died);
        Assert.Equal(0, verdict.Wounded);
    }

    [Fact]
    public void JudgeFallen_DeathFirstThenWound_OnePathPerMan()
    {
        // Man 1: death roll 0.4 (< 0.5 → dies); wound roll 0.9 drawn but irrelevant.
        // Man 2: death roll 0.6 (survives); wound roll 0.4 (< 0.5 → wounded).
        // Man 3: death roll 0.9 (survives); wound roll 0.9 (shrugs it off).
        var verdict = AftermathMath.JudgeFallen(3, 0.5, 0.5, Rolls(0.4, 0.9, 0.6, 0.4, 0.9, 0.9));
        Assert.Equal(1, verdict.Died);
        Assert.Equal(1, verdict.Wounded);
    }

    [Fact]
    public void JudgeFallen_TwoRollsPerManAlways_StreamPositionIsOutcomeIndependent()
    {
        // Six rolls exactly cover three men; the seventh value would flip man 3 to dead if
        // it were ever read early — it is not.
        var verdict = AftermathMath.JudgeFallen(3, 0.5, 0.0, Rolls(0.4, 0.0, 0.6, 0.0, 0.6, 0.0, 0.0));
        Assert.Equal(1, verdict.Died);
        Assert.Equal(0, verdict.Wounded);
    }

    [Fact]
    public void JudgeFallen_CertainDeath_EveryoneDies()
    {
        var verdict = AftermathMath.JudgeFallen(10, 1.0, 1.0, Rolls(0.999));
        Assert.Equal(10, verdict.Died);
        Assert.Equal(0, verdict.Wounded);
    }

    [Fact]
    public void JudgeFallen_NoDeathFullWound_EveryoneWakesWounded()
    {
        var verdict = AftermathMath.JudgeFallen(10, 0.0, 1.0, Rolls(0.5));
        Assert.Equal(0, verdict.Died);
        Assert.Equal(10, verdict.Wounded);
    }

    [Fact]
    public void JudgeFallen_ClampsChancesAndThrowsOnNullRoll()
    {
        var verdict = AftermathMath.JudgeFallen(5, -1.0, 2.0, Rolls(0.999));
        Assert.Equal(0, verdict.Died);      // -1 clamps to 0
        Assert.Equal(5, verdict.Wounded);   // 2 clamps to 1
        Assert.Throws<ArgumentNullException>(() => AftermathMath.JudgeFallen(1, 0.5, 0.5, null!));
    }

    [Theory]
    [InlineData(10, 0.0, 0)]
    [InlineData(10, 1.0, 10)]
    [InlineData(-4, 1.0, 0)]
    public void StayWounded_Extremes(int downed, double chance, int expected)
    {
        Assert.Equal(expected, AftermathMath.StayWounded(downed, chance, Rolls(0.5)));
    }

    [Fact]
    public void StayWounded_OneRollPerMan()
    {
        // 0.1 stays, 0.9 healed, 0.1 stays.
        Assert.Equal(2, AftermathMath.StayWounded(3, 0.15, Rolls(0.1, 0.9, 0.1)));
        Assert.Throws<ArgumentNullException>(() => AftermathMath.StayWounded(1, 0.5, null!));
    }

    // ---- the quartermaster's band --------------------------------------------

    [Theory]
    [InlineData(100, 75, 75)]
    [InlineData(100, 100, 100)]
    [InlineData(100, 0, 0)]
    [InlineData(0, 75, 0)]
    [InlineData(-50, 75, 0)]     // negative earned keeps nothing
    [InlineData(3, 50, 2)]       // 1.5 rounds away from zero → 2
    [InlineData(100, 150, 150)]  // above 100: the drill grants bonus XP
    [InlineData(100, 200, 200)]  // the ceiling itself
    [InlineData(100, 400, 200)]  // percent clamps at MaxKeepPercent
    [InlineData(100, -10, 0)]    // percent clamps low
    public void XpKept_ScalesAndClamps(int earned, int percent, int expected)
    {
        Assert.Equal(expected, AftermathMath.XpKept(earned, percent));
    }

    [Theory]
    [InlineData(100, 75, 25)]
    [InlineData(100, 100, 0)]
    [InlineData(100, 0, 100)]
    [InlineData(100, 150, -50)]  // negative = bonus XP to grant
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
            for (var pct = 0; pct <= AftermathMath.MaxKeepPercent; pct += 13)
                Assert.Equal(earned, AftermathMath.XpKept(earned, pct) + AftermathMath.XpToRemove(earned, pct));
    }

    [Theory]
    [InlineData(0, 40, 100, 40)]     // the design floor: no officer worth the name
    [InlineData(150, 40, 100, 70)]   // halfway up the band
    [InlineData(300, 40, 100, 100)]  // the cap → the ceiling
    [InlineData(75, 40, 100, 55)]    // quarter of the band
    [InlineData(400, 40, 100, 100)]  // skill clamps to the cap
    [InlineData(-20, 40, 100, 40)]   // negative skill clamps to zero
    [InlineData(150, 80, 80, 80)]    // min == max: a flat rate, skill ignored
    [InlineData(150, 100, 50, 100)]  // max below min is pulled up to min (flat)
    [InlineData(300, 0, 400, 200)]   // percents clamp to 0..MaxKeepPercent
    [InlineData(150, 0, 200, 100)]   // halves land exactly
    public void XpKeptPercentForSkill_LerpsAcrossTheBand(int skill, int min, int max, int expected)
    {
        Assert.Equal(expected, AftermathMath.XpKeptPercentForSkill(skill, min, max));
    }

    // ---- the drill instructors -----------------------------------------------

    [Fact]
    public void InstructorBonusPercent_SumsLinearContributions()
    {
        // Skill 300 earns the full rate, 150 half of it, 0 nothing.
        Assert.Equal(5.0, AftermathMath.InstructorBonusPercent(new[] { 300 }, 5.0), precision: 10);
        Assert.Equal(7.5, AftermathMath.InstructorBonusPercent(new[] { 300, 150 }, 5.0), precision: 10);
        Assert.Equal(7.5, AftermathMath.InstructorBonusPercent(new[] { 300, 150, 0 }, 5.0), precision: 10);
    }

    [Fact]
    public void InstructorBonusPercent_ClampsSkillsAndRate()
    {
        // Skills clamp into 0..300; a negative rate teaches nothing (never subtracts).
        Assert.Equal(5.0, AftermathMath.InstructorBonusPercent(new[] { 500 }, 5.0), precision: 10);
        Assert.Equal(0.0, AftermathMath.InstructorBonusPercent(new[] { -100 }, 5.0), precision: 10);
        Assert.Equal(0.0, AftermathMath.InstructorBonusPercent(new[] { 300, 300 }, -5.0), precision: 10);
    }

    [Fact]
    public void InstructorBonusPercent_NobodyTeachesNothing()
    {
        Assert.Equal(0.0, AftermathMath.InstructorBonusPercent(System.Array.Empty<int>(), 5.0));
        Assert.Equal(0.0, AftermathMath.InstructorBonusPercent(null!, 5.0));
        Assert.Equal(0.0, AftermathMath.InstructorBonusPercent(new[] { 300 }, 0.0));
    }
}
