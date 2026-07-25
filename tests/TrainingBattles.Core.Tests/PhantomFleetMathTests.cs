using TrainingBattles.Core;
using Xunit;

namespace TrainingBattles.Core.Tests
{
    public class PhantomFleetMathTests
    {
        [Fact]
        public void TierZeroMeansBareHulls()
        {
            Assert.Equal(-1, PhantomFleetMath.UpgradePickIndex(new[] { 1, 2, 3 }, 0));
        }

        [Fact]
        public void PicksTheBestPieceTheTierAffords()
        {
            // Tier 2: the level-2 piece wins over the level-1, the level-3 stays on the shelf.
            Assert.Equal(1, PhantomFleetMath.UpgradePickIndex(new[] { 1, 2, 3 }, 2));
        }

        [Fact]
        public void FullTierTakesTheTopPiece()
        {
            Assert.Equal(2, PhantomFleetMath.UpgradePickIndex(new[] { 1, 2, 3 }, 3));
        }

        [Fact]
        public void NothingAffordableMeansNoPiece()
        {
            Assert.Equal(-1, PhantomFleetMath.UpgradePickIndex(new[] { 2, 3 }, 1));
        }

        [Fact]
        public void TiesGoToTheFirstCandidate()
        {
            // Two level-1 pieces at tier 1: the first is the deterministic pick.
            Assert.Equal(0, PhantomFleetMath.UpgradePickIndex(new[] { 1, 1 }, 1));
        }

        [Fact]
        public void EmptyOrMissingListsAreBare()
        {
            Assert.Equal(-1, PhantomFleetMath.UpgradePickIndex(new int[0], 3));
            Assert.Equal(-1, PhantomFleetMath.UpgradePickIndex(null!, 3));
        }

        [Fact]
        public void TiersAboveEveryPieceStillPickTheTop()
        {
            // A tier past the deepest harbor just buys the best there is.
            Assert.Equal(0, PhantomFleetMath.UpgradePickIndex(new[] { 3, 1 }, 9));
        }
    }
}
