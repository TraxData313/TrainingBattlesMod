using TrainingBattles.Core;

namespace TrainingBattles.Core.Tests;

public class FleetSplitMathTests
{
    [Fact]
    public void TwoEqualShips_EvenCrews_OneEach()
    {
        var opponent = FleetSplitMath.OpponentShips(new[] { 40, 40 }, flagshipIndex: 0, 50, 50);
        Assert.Equal(new[] { 1 }, opponent);
    }

    [Fact]
    public void FlagshipNeverCrosses_EvenWhenOpponentTakesMostMen()
    {
        // 9 of 10 men cross over — the opponent deserves nearly the whole fleet, but the
        // flagship (index 2, the biggest hull) stays with the player regardless.
        var opponent = FleetSplitMath.OpponentShips(new[] { 30, 30, 80 }, flagshipIndex: 2, 1, 9);
        Assert.DoesNotContain(2, opponent);
        Assert.Equal(new[] { 0, 1 }, opponent);
    }

    [Fact]
    public void OpponentAlwaysGetsAtLeastOneHull()
    {
        // A lone man crosses over out of a hundred — proportionality says zero ships, the
        // "both sides sail" rule says one, and it is the smallest non-flagship hull.
        var opponent = FleetSplitMath.OpponentShips(new[] { 80, 20, 60 }, flagshipIndex: 0, 99, 1);
        Assert.Equal(new[] { 1 }, opponent);
    }

    [Fact]
    public void PlayerAlwaysKeepsAtLeastOneHull_WithoutAFlagshipPin()
    {
        // No pinned flagship (index out of range) and nearly everyone crossing: the greedy
        // fill would hand every hull across — the guard hands the smallest one back.
        var opponent = FleetSplitMath.OpponentShips(new[] { 40, 40 }, flagshipIndex: -1, 1, 99);
        Assert.Single(opponent);
    }

    [Fact]
    public void SplitTracksCrewShares()
    {
        // Six equal hulls, a third of the men crossing → the opponent sails two of them.
        var opponent = FleetSplitMath.OpponentShips(new[] { 50, 50, 50, 50, 50, 50 }, flagshipIndex: 0, 200, 100);
        Assert.Equal(2, opponent.Count);
    }

    [Fact]
    public void EqualHullsSplitTheSameWayEveryTime()
    {
        var first = FleetSplitMath.OpponentShips(new[] { 40, 40, 40, 40 }, flagshipIndex: 1, 60, 60);
        var second = FleetSplitMath.OpponentShips(new[] { 40, 40, 40, 40 }, flagshipIndex: 1, 60, 60);
        Assert.Equal(first, second);
    }

    [Fact]
    public void BigHullChasesTheBigCrew()
    {
        // One warship, two boats; two thirds of the men cross — the warship crosses with them.
        var opponent = FleetSplitMath.OpponentShips(new[] { 100, 25, 25 }, flagshipIndex: 1, 50, 100);
        Assert.Contains(0, opponent);
    }

    [Fact]
    public void FewerThanTwoHulls_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            FleetSplitMath.OpponentShips(new[] { 40 }, flagshipIndex: 0, 10, 10));
    }

    [Fact]
    public void ZeroCrews_StillSplitsAndBothSidesSail()
    {
        // Degenerate inputs must never produce an empty side or a crash.
        var opponent = FleetSplitMath.OpponentShips(new[] { 40, 30, 20 }, flagshipIndex: 0, 0, 0);
        Assert.NotEmpty(opponent);
        Assert.True(opponent.Count < 3);
        Assert.DoesNotContain(0, opponent);
    }
}
