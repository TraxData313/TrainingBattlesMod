using System;

namespace TrainingBattles.Core
{
    /// <summary>
    /// The pure arithmetic of "war is a school, not a funeral": what happens to the fallen and
    /// to the experience once a training battle ends. No game types — fully unit-tested.
    /// </summary>
    public static class AftermathMath
    {
        /// <summary>
        /// Of <paramref name="fallen"/> men who would have died in a real battle, decides how many
        /// wake up WOUNDED. Nobody dies in training; the rest walk away unhurt.
        ///
        /// Per man, two rolls:
        /// 1. The surgeon's save — with <paramref name="surgeonSaveChance"/> (the game's own
        ///    Medicine-driven survival chance) the man is patched up on the spot and walks away
        ///    unhurt. A good doctor helps.
        /// 2. Of those the surgeon could not spare, <paramref name="woundedFactor"/> decides who is
        ///    truly wounded versus who just shrugs it off (bruises, dented pride).
        /// </summary>
        /// <param name="fallen">How many would have died. Negative is treated as zero.</param>
        /// <param name="surgeonSaveChance">0..1 chance per man to walk away unhurt (clamped).</param>
        /// <param name="woundedFactor">0..1 share of the unsaved who end up wounded (clamped).</param>
        /// <param name="roll">Random source returning [0,1) — injectable for tests.</param>
        public static int WoundedAmongFallen(int fallen, double surgeonSaveChance, double woundedFactor, Func<double> roll)
        {
            if (roll == null) throw new ArgumentNullException(nameof(roll));
            var save = Clamp01(surgeonSaveChance);
            var factor = Clamp01(woundedFactor);
            var wounded = 0;
            for (var i = 0; i < fallen; i++)
            {
                if (roll() < save) continue;        // the surgeon's work — walks away unhurt
                if (roll() < factor) wounded++;     // truly hurt; the rest shrug it off
            }
            return wounded;
        }

        /// <summary>
        /// How much of the XP earned in training is KEPT, at <paramref name="keepPercent"/> percent
        /// (0..100, clamped). Rounds to nearest; earning nothing keeps nothing.
        /// </summary>
        public static int XpKept(int xpEarned, int keepPercent)
        {
            if (xpEarned <= 0) return 0;
            var pct = keepPercent < 0 ? 0 : (keepPercent > 100 ? 100 : keepPercent);
            return (int)Math.Round(xpEarned * (pct / 100.0), MidpointRounding.AwayFromZero);
        }

        /// <summary>The counterpart of <see cref="XpKept"/>: how much earned XP to take back.</summary>
        public static int XpToRemove(int xpEarned, int keepPercent)
        {
            if (xpEarned <= 0) return 0;
            return xpEarned - XpKept(xpEarned, keepPercent);
        }

        private static double Clamp01(double value) =>
            value < 0.0 ? 0.0 : (value > 1.0 ? 1.0 : value);
    }
}
