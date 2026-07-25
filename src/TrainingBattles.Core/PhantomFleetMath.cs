using System.Collections.Generic;

namespace TrainingBattles.Core
{
    /// <summary>
    /// The pure arithmetic of fitting a PHANTOM fleet (the mock enemy's conjured hulls) with
    /// upgrade pieces. The composer offers a fleet-wide fitting tier — 0 means bare hulls,
    /// 1..N mean "the best piece a harbor of that level sells" — and each ship slot holds a
    /// list of candidate pieces, each demanding some harbor level. No game types; unit-tested.
    /// </summary>
    public static class PhantomFleetMath
    {
        /// <summary>
        /// Which candidate piece a slot takes at the given fitting tier: the piece with the
        /// HIGHEST harbor level not above <paramref name="tier"/> — deterministic, first such
        /// index on ties (so the same composition fits the same fleet every drill). Returns -1
        /// when the tier is 0 (bare hulls) or nothing fits.
        /// </summary>
        /// <param name="piecePortLevels">Each candidate's required harbor level, in slot order.</param>
        /// <param name="tier">The chosen fleet-wide fitting tier (0 = bare).</param>
        public static int UpgradePickIndex(IReadOnlyList<int> piecePortLevels, int tier)
        {
            if (piecePortLevels == null || tier <= 0) return -1;
            var best = -1;
            for (var i = 0; i < piecePortLevels.Count; i++)
            {
                var level = piecePortLevels[i];
                if (level > tier) continue;
                if (best < 0 || level > piecePortLevels[best]) best = i;
            }
            return best;
        }
    }
}
