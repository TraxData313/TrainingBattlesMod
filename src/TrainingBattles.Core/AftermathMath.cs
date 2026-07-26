using System;
using System.Collections.Generic;

namespace TrainingBattles.Core
{
    /// <summary>
    /// The pure arithmetic of "war is a school, not a funeral": what happens to the fallen and
    /// to the experience once a training battle ends. No game types — fully unit-tested.
    ///
    /// THE OFFICERS UPDATE (Anton, 2026.07.25): every number here is set by an officer's skill,
    /// scaled LINEARLY across 0..<see cref="OfficerSkillCap"/> between two config ends. The XP
    /// band belongs to the quartermaster (Leadership on land, the First Mate's Boatswain at
    /// sea); the three casualty bands belong to the surgeon (Medicine). A band's two ends equal
    /// = a flat rate, skill ignored.
    /// </summary>
    public static class AftermathMath
    {
        /// <summary>An officer's skill beyond this teaches nothing more — every band's right
        /// edge (the game's soft skill cap).</summary>
        public const int OfficerSkillCap = 300;

        /// <summary>The keep-percent ceiling: past 100 the drill GRANTS bonus XP (a great
        /// quartermaster squeezes more lessons out of every bruise), capped at double
        /// (Anton, 2026.07.25 — bonus XP exists, so cap at 200).</summary>
        public const int MaxKeepPercent = 200;

        /// <summary>
        /// A band read at a skill: linear from <paramref name="atSkill0"/> (skill 0) to
        /// <paramref name="atSkillCap"/> (skill <see cref="OfficerSkillCap"/>), skill clamped
        /// into the band. The ends may run in either direction — the surgeon's bands FALL as
        /// Medicine rises. Ends are clamped to 0..100 (these are chances).
        /// </summary>
        public static double ChancePercentForSkill(double atSkill0, double atSkillCap, int skill)
        {
            var lo = ClampChance(atSkill0);
            var hi = ClampChance(atSkillCap);
            return lo + (hi - lo) * (ClampSkill(skill) / (double)OfficerSkillCap);
        }

        /// <summary>
        /// What percent of the drill's XP the troops keep, scaled linearly by the XP officer's
        /// skill: skill 0 keeps <paramref name="minPercent"/>, skill <see cref="OfficerSkillCap"/>
        /// keeps <paramref name="maxPercent"/> (defaults 40 and 100). Percents clamp to
        /// 0..<see cref="MaxKeepPercent"/>; a max below the min is pulled up to it (equal min and
        /// max = a flat rate, skill ignored). Rounds to nearest.
        /// </summary>
        public static int XpKeptPercentForSkill(int officerSkill, int minPercent, int maxPercent)
        {
            var lo = ClampPercent(minPercent);
            var hi = ClampPercent(maxPercent);
            if (hi < lo) hi = lo;
            return (int)Math.Round(lo + (hi - lo) * (ClampSkill(officerSkill) / (double)OfficerSkillCap),
                MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// The drill INSTRUCTORS' stake (Anton's polish, 2026.07.26): each good-fighter
        /// companion sent to the drill adds percentage points to the kept-XP rate, linear from
        /// 0 at fighting skill 0 to <paramref name="perInstructorAtSkillCap"/> at skill
        /// <see cref="OfficerSkillCap"/> (skills clamped into the band, a negative rate counts
        /// as 0). The caller picks WHO instructs (the top fighters by their best weapon skill)
        /// and adds the sum onto the XP officer's band — the <see cref="MaxKeepPercent"/> cap
        /// still rules the total. A null or empty roster teaches nothing.
        /// </summary>
        public static double InstructorBonusPercent(IEnumerable<int> fighterSkills, double perInstructorAtSkillCap)
        {
            if (fighterSkills == null) return 0.0;
            var per = perInstructorAtSkillCap < 0.0 ? 0.0 : perInstructorAtSkillCap;
            var total = 0.0;
            foreach (var skill in fighterSkills)
                total += per * (ClampSkill(skill) / (double)OfficerSkillCap);
            return total;
        }

        /// <summary>
        /// The surgeon's verdict on the men who would have died: per man ONE path — with
        /// <paramref name="deathChance"/> he truly DIES (the drill's one real cost, the
        /// KIA→KIA band); failing that, with <paramref name="woundChance"/> he wakes wounded
        /// (KIA→wounded); failing both he shrugs it off. There is deliberately no
        /// wounded→KIA path anywhere (Anton, 2026.07.25). Chances clamp to 0..1; negative
        /// <paramref name="fallen"/> counts as zero.
        /// </summary>
        /// <param name="roll">Random source returning [0,1) — injectable for tests.
        /// Consumes exactly two rolls per fallen man (death roll, then wound roll —
        /// the wound roll is drawn even for the dead, keeping the stream position
        /// independent of outcomes).</param>
        public static FallenVerdict JudgeFallen(int fallen, double deathChance, double woundChance, Func<double> roll)
        {
            if (roll == null) throw new ArgumentNullException(nameof(roll));
            var die = Clamp01(deathChance);
            var wound = Clamp01(woundChance);
            var died = 0;
            var wounded = 0;
            for (var i = 0; i < fallen; i++)
            {
                var dies = roll() < die;
                var wounds = roll() < wound;
                if (dies) died++;
                else if (wounds) wounded++;
            }
            return new FallenVerdict(died, wounded);
        }

        /// <summary>
        /// Of <paramref name="downed"/> men who were battle-wounded or knocked out (but never
        /// died), how many STAY wounded after the drill at <paramref name="stayChance"/> each
        /// (the wounded→wounded band); the rest are patched up on the spot. One roll per man.
        /// </summary>
        public static int StayWounded(int downed, double stayChance, Func<double> roll)
        {
            if (roll == null) throw new ArgumentNullException(nameof(roll));
            var stay = Clamp01(stayChance);
            var wounded = 0;
            for (var i = 0; i < downed; i++)
                if (roll() < stay) wounded++;
            return wounded;
        }

        /// <summary>
        /// How much of the XP earned in training is KEPT, at <paramref name="keepPercent"/> percent
        /// (0..<see cref="MaxKeepPercent"/>, clamped — above 100 the drill grants bonus XP).
        /// Rounds to nearest; earning nothing keeps nothing.
        /// </summary>
        public static int XpKept(int xpEarned, int keepPercent)
        {
            if (xpEarned <= 0) return 0;
            return (int)Math.Round(xpEarned * (ClampPercent(keepPercent) / 100.0), MidpointRounding.AwayFromZero);
        }

        /// <summary>The counterpart of <see cref="XpKept"/>: how much earned XP to take back.
        /// NEGATIVE when the keep-percent tops 100 — that many bonus points to grant.</summary>
        public static int XpToRemove(int xpEarned, int keepPercent)
        {
            if (xpEarned <= 0) return 0;
            return xpEarned - XpKept(xpEarned, keepPercent);
        }

        private static int ClampSkill(int skill) =>
            skill < 0 ? 0 : (skill > OfficerSkillCap ? OfficerSkillCap : skill);

        private static int ClampPercent(int percent) =>
            percent < 0 ? 0 : (percent > MaxKeepPercent ? MaxKeepPercent : percent);

        private static double ClampChance(double percent) =>
            percent < 0.0 ? 0.0 : (percent > 100.0 ? 100.0 : percent);

        private static double Clamp01(double value) =>
            value < 0.0 ? 0.0 : (value > 1.0 ? 1.0 : value);
    }

    /// <summary>The surgeon's tally over one stack's would-have-died: who truly died and who
    /// wakes wounded (everyone else shrugs it off).</summary>
    public readonly struct FallenVerdict
    {
        public FallenVerdict(int died, int wounded)
        {
            Died = died;
            Wounded = wounded;
        }

        /// <summary>Truly dead — the drill's one real, permanent cost.</summary>
        public int Died { get; }

        /// <summary>Wake up wounded; they heal like any battle wound.</summary>
        public int Wounded { get; }
    }
}
