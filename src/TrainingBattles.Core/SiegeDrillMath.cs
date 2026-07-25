using System;

namespace TrainingBattles.Core
{
    /// <summary>
    /// The pure arithmetic of the CASTLE SIEGE drill's engineer (the castle update, 2026.07.25).
    /// The ENGINEER's Engineering skill unlocks siege equipment in TIERS — tier 0 is always open
    /// (the ram and the walls' own works), the three higher tiers open at configurable skill
    /// thresholds — and each engine built adds its worth to the drill's bill: the game's own
    /// man-day construction cost times a gold-per-man-day rate. No game types; unit-tested.
    /// </summary>
    public static class SiegeDrillMath
    {
        /// <summary>The highest equipment tier an Engineering skill unlocks: 0 always; 1..3 at
        /// the given thresholds. Thresholds are read in order — a threshold at or below a lower
        /// tier's still counts (a hand-edit making tier 3 cheaper than tier 2 simply opens both).</summary>
        public static int TierForSkill(int skill, int tier1Skill, int tier2Skill, int tier3Skill)
        {
            var tier = 0;
            if (skill >= tier1Skill) tier = 1;
            if (skill >= tier2Skill) tier = 2;
            if (skill >= tier3Skill) tier = 3;
            return tier;
        }

        /// <summary>One engine's price on the drill's bill: its construction cost in man-days
        /// times the configured gold per man-day. 0 gold per man-day = free engines; negative
        /// inputs are treated as zero.</summary>
        public static int EngineCost(int manDayCost, int goldPerManDay)
        {
            if (manDayCost <= 0 || goldPerManDay <= 0) return 0;
            return manDayCost * goldPerManDay;
        }

        /// <summary>The whole equipment bill: each picked engine's man-day cost times its count,
        /// priced at the gold-per-man-day rate. Overflow-safe (clamps at int.MaxValue).</summary>
        public static int EquipmentBill(System.Collections.Generic.IEnumerable<(int ManDayCost, int Count)> engines, int goldPerManDay)
        {
            if (engines == null || goldPerManDay <= 0) return 0;
            double total = 0;
            foreach (var (manDays, count) in engines)
            {
                if (manDays <= 0 || count <= 0) continue;
                total += (double)manDays * count * goldPerManDay;
                if (total > int.MaxValue) return int.MaxValue;
            }
            return (int)total;
        }
    }
}
