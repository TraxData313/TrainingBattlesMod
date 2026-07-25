using TrainingBattles.Core;
using Xunit;

namespace TrainingBattles.Core.Tests
{
    public class SiegeDrillMathTests
    {
        [Fact]
        public void TierZeroIsAlwaysOpen()
        {
            Assert.Equal(0, SiegeDrillMath.TierForSkill(0, 50, 100, 150));
            Assert.Equal(0, SiegeDrillMath.TierForSkill(49, 50, 100, 150));
        }

        [Fact]
        public void TiersOpenAtTheirThresholds()
        {
            Assert.Equal(1, SiegeDrillMath.TierForSkill(50, 50, 100, 150));
            Assert.Equal(1, SiegeDrillMath.TierForSkill(99, 50, 100, 150));
            Assert.Equal(2, SiegeDrillMath.TierForSkill(100, 50, 100, 150));
            Assert.Equal(3, SiegeDrillMath.TierForSkill(150, 50, 100, 150));
            Assert.Equal(3, SiegeDrillMath.TierForSkill(300, 50, 100, 150));
        }

        [Fact]
        public void ADisorderedHandEditOpensEveryTierItReaches()
        {
            // Tier 3 set cheaper than tier 2: reaching it opens tier 3 regardless.
            Assert.Equal(3, SiegeDrillMath.TierForSkill(80, 50, 100, 60));
        }

        [Fact]
        public void ZeroThresholdsOpenEverythingAtSkillZero()
        {
            Assert.Equal(3, SiegeDrillMath.TierForSkill(0, 0, 0, 0));
        }

        [Fact]
        public void EngineCostIsManDaysTimesRate()
        {
            Assert.Equal(600, SiegeDrillMath.EngineCost(30, 20));
        }

        [Fact]
        public void FreeRateOrFreeEngineCostsNothing()
        {
            Assert.Equal(0, SiegeDrillMath.EngineCost(30, 0));
            Assert.Equal(0, SiegeDrillMath.EngineCost(0, 20));
            Assert.Equal(0, SiegeDrillMath.EngineCost(-5, 20));
        }

        [Fact]
        public void EquipmentBillSumsEveryPickedEngine()
        {
            var engines = new[] { (ManDayCost: 30, Count: 2), (ManDayCost: 10, Count: 1) };
            Assert.Equal((30 * 2 + 10) * 20, SiegeDrillMath.EquipmentBill(engines, 20));
        }

        [Fact]
        public void EquipmentBillIgnoresEmptyAndFreeLines()
        {
            var engines = new[] { (ManDayCost: 30, Count: 0), (ManDayCost: 0, Count: 5) };
            Assert.Equal(0, SiegeDrillMath.EquipmentBill(engines, 20));
            Assert.Equal(0, SiegeDrillMath.EquipmentBill(null!, 20));
        }

        [Fact]
        public void EquipmentBillClampsInsteadOfOverflowing()
        {
            var engines = new[] { (ManDayCost: int.MaxValue, Count: int.MaxValue) };
            Assert.Equal(int.MaxValue, SiegeDrillMath.EquipmentBill(engines, 1000));
        }
    }
}
