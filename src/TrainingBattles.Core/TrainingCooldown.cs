namespace TrainingBattles.Core
{
    /// <summary>
    /// The "once per 24 hours" clock, as pure math over campaign hours. A cooldown of zero (or
    /// less) means unlimited drilling; a last-training time of zero or less means "never trained".
    /// </summary>
    public static class TrainingCooldown
    {
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
