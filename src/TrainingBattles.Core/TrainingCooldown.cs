namespace TrainingBattles.Core
{
    /// <summary>
    /// The "once per 24 hours" clock, as pure math over campaign hours. A cooldown of zero (or
    /// less) means unlimited drilling; a last-training time of zero or less means "never trained".
    /// Since 2026.07.26 the QUARTERMASTER's Steward speeds the clock (Anton's polish): the
    /// configured wait is DIVIDED by <see cref="DivisorForSkill"/> — /1 at skill 0 rising
    /// linearly to the config ceiling (default /4) at skill 300.
    /// </summary>
    public static class TrainingCooldown
    {
        /// <summary>
        /// The quartermaster's speed-up: the factor the configured cooldown is divided by,
        /// linear from 1 at skill 0 to <paramref name="divisorAtSkillCap"/> at skill
        /// <see cref="AftermathMath.OfficerSkillCap"/> (skill clamped into the band; a ceiling
        /// below 1 counts as 1 — the clock never runs SLOWER for having a quartermaster).
        /// </summary>
        public static double DivisorForSkill(int skill, double divisorAtSkillCap)
        {
            if (divisorAtSkillCap < 1.0) divisorAtSkillCap = 1.0;
            var s = skill < 0 ? 0 : (skill > AftermathMath.OfficerSkillCap ? AftermathMath.OfficerSkillCap : skill);
            return 1.0 + (divisorAtSkillCap - 1.0) * (s / (double)AftermathMath.OfficerSkillCap);
        }

        /// <summary>True when the men are rested enough for another training battle.</summary>
        public static bool IsReady(double nowHours, double lastTrainingHours, double cooldownHours)
        {
            if (cooldownHours <= 0.0) return true;
            if (lastTrainingHours <= 0.0) return true;
            return nowHours - lastTrainingHours >= cooldownHours;
        }

        /// <summary>Hours until the next training battle is allowed; 0 when ready now.</summary>
        public static double HoursRemaining(double nowHours, double lastTrainingHours, double cooldownHours)
        {
            if (IsReady(nowHours, lastTrainingHours, cooldownHours)) return 0.0;
            return lastTrainingHours + cooldownHours - nowHours;
        }
    }
}
