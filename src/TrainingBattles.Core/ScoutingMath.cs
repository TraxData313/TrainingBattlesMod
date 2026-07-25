using System;

namespace TrainingBattles.Core
{
    /// <summary>
    /// The scouting duel that earns a commander the battlefield tools on a REAL battle: whoever
    /// out-scouts the other side controls the ground. Pure ratio arithmetic — no game types,
    /// fully unit-tested. The thresholds are percents of the ENEMY's best scouting: the design
    /// defaults are 75 when defending (you stand on ground you already hold — a modest screen of
    /// outriders suffices) and 125 when attacking (dictating WHERE the enemy must fight takes a
    /// real intelligence edge).
    /// </summary>
    public static class ScoutingMath
    {
        /// <summary>
        /// The scouting skill needed to pass: <paramref name="enemySkill"/> ×
        /// <paramref name="requiredPercent"/>/100, rounded UP (you must truly reach the bar,
        /// not sit a fraction under it). Negative inputs are treated as zero — an enemy with
        /// no scout at all (skill 0) is always out-scouted.
        /// </summary>
        public static int RequiredSkill(int enemySkill, int requiredPercent)
        {
            if (enemySkill < 0) enemySkill = 0;
            if (requiredPercent < 0) requiredPercent = 0;
            return (int)Math.Ceiling(enemySkill * (requiredPercent / 100.0));
        }

        /// <summary>Whether <paramref name="yourSkill"/> meets the bar of
        /// <see cref="RequiredSkill"/>.</summary>
        public static bool OutScouts(int yourSkill, int enemySkill, int requiredPercent) =>
            (yourSkill < 0 ? 0 : yourSkill) >= RequiredSkill(enemySkill, requiredPercent);
    }
}
