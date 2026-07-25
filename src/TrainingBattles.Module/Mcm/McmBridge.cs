using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace TrainingBattles.Mcm
{
    /// <summary>
    /// Binds the in-game MCM menu to the live <see cref="ModConfig"/> as a SOFT dependency: if the
    /// Mod Configuration Menu module is not installed, nothing here ever touches an MCM type, so the
    /// mod runs on <c>config.json</c> alone. All MCM contact is quarantined in <see cref="Bind"/>
    /// (kept non-inlined), called only after the MCM assembly is confirmed loaded — the CLR never
    /// tries to resolve MCM types when the module is absent. Pattern proven in ImmersiveAI.
    /// </summary>
    internal static class McmBridge
    {
        private static bool _bound;

        /// <summary>Binds the menu to <paramref name="live"/> if MCM is present. Safe to call when it
        /// is not — returns quietly. Best-effort: any failure only costs the menu, never the mod.</summary>
        public static void TryBind(ModConfig live)
        {
            if (_bound || live == null) return;
            var mcmLoaded = AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => string.Equals(a.GetName().Name, "MCMv5", StringComparison.OrdinalIgnoreCase));
            if (!mcmLoaded) return;

            try
            {
                // Bind returns false if MCM is up but our settings aren't registered yet; leave
                // _bound false so a later call retries. A real exception won't heal itself — give up.
                if (Bind(live)) _bound = true;
            }
            catch (Exception ex)
            {
                _bound = true;
                TaleWorlds.Library.InformationManager.DisplayMessage(
                    new TaleWorlds.Library.InformationMessage("Training Battles: mod menu unavailable (" + ex.Message + ")."));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Bind(ModConfig live)
        {
            var s = TrainingBattlesMcmSettings.Instance;
            if (s == null) return false; // MCM present but our settings not registered yet — retry later.

            PushConfigToMenu(s, live);
            s.PropertyChanged += (_, __) =>
            {
                try
                {
                    PullMenuToConfig(s, live);
                    live.Normalize();
                    live.Save();
                }
                catch { /* a bad edit must not crash the menu */ }
            };
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PushConfigToMenu(TrainingBattlesMcmSettings s, ModConfig c)
        {
            s.EnableMockEnemyTraining = c.EnableMockEnemyTraining;
            s.EnableCastleTraining = c.EnableCastleTraining;
            s.EngineerTier1Skill = c.EngineerTier1Skill;
            s.EngineerTier2Skill = c.EngineerTier2Skill;
            s.EngineerTier3Skill = c.EngineerTier3Skill;
            s.SiegeEngineGoldPerManDay = c.SiegeEngineGoldPerManDay;
            s.CastleTrainingCostWages = c.CastleTrainingCostWages;
            s.CastleTrainingCooldownHours = c.CastleTrainingCooldownHours;
            s.CastleDrillRenownPer100Men = (float)c.CastleDrillRenownPer100Men;
            s.CastleDrillInfluencePer100Men = (float)c.CastleDrillInfluencePer100Men;
            s.XpKeptMinPercent = c.XpKeptMinPercent;
            s.XpKeptMaxPercent = c.XpKeptMaxPercent;
            s.RealDeathPercentAtMedicine0 = (float)c.RealDeathPercentAtMedicine0;
            s.RealDeathPercentAtMedicine300 = (float)c.RealDeathPercentAtMedicine300;
            s.KiaWoundedPercentAtMedicine0 = (float)c.KiaWoundedPercentAtMedicine0;
            s.KiaWoundedPercentAtMedicine300 = (float)c.KiaWoundedPercentAtMedicine300;
            s.DownedWoundedPercentAtMedicine0 = (float)c.DownedWoundedPercentAtMedicine0;
            s.DownedWoundedPercentAtMedicine300 = (float)c.DownedWoundedPercentAtMedicine300;
            s.DisorganizedAfterTraining = c.DisorganizedAfterTraining;
            s.HeroHealthRestorePercent = c.HeroHealthRestorePercent;
            s.CooldownHours = c.CooldownHours;
            s.TrainingCostWagesLand = c.TrainingCostWagesLand;
            s.TrainingCostWagesSea = c.TrainingCostWagesSea;
            s.AutoSplitInHalf = c.AutoSplitInHalf;
            s.DebugLogging = c.DebugLogging;
            s.UseOpponentBanner = c.UseOpponentBanner;
            s.OpponentBannerCode = c.OpponentBannerCode;
            s.ChooseGroundWhenDefending = c.ChooseGroundWhenDefending;
            s.ChooseGroundWhenAttacking = c.ChooseGroundWhenAttacking;
            s.ScoutingGateEnabled = c.ScoutingGateEnabled;
            s.ScoutingGateDefendPercent = c.ScoutingGateDefendPercent;
            s.ScoutingGateAttackPercent = c.ScoutingGateAttackPercent;
            // Rebuild rather than select: the dropdown's index convention (0 = clock, i+1 =
            // SupportedBattleHours[i]) lives in TimeOfDayChoices — one authority for both sides.
            s.BattleTimeOfDay = TrainingBattlesMcmSettings.TimeOfDayChoices(c.BattleTimeOfDay);
            SelectOrAdd(s.OpenMenuHotkey, c.OpenMenuHotkey);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PullMenuToConfig(TrainingBattlesMcmSettings s, ModConfig c)
        {
            c.EnableMockEnemyTraining = s.EnableMockEnemyTraining;
            c.EnableCastleTraining = s.EnableCastleTraining;
            c.EngineerTier1Skill = s.EngineerTier1Skill;
            c.EngineerTier2Skill = s.EngineerTier2Skill;
            c.EngineerTier3Skill = s.EngineerTier3Skill;
            c.SiegeEngineGoldPerManDay = s.SiegeEngineGoldPerManDay;
            c.CastleTrainingCostWages = s.CastleTrainingCostWages;
            c.CastleTrainingCooldownHours = s.CastleTrainingCooldownHours;
            c.CastleDrillRenownPer100Men = s.CastleDrillRenownPer100Men;
            c.CastleDrillInfluencePer100Men = s.CastleDrillInfluencePer100Men;
            c.XpKeptMinPercent = s.XpKeptMinPercent;
            c.XpKeptMaxPercent = s.XpKeptMaxPercent;
            c.RealDeathPercentAtMedicine0 = s.RealDeathPercentAtMedicine0;
            c.RealDeathPercentAtMedicine300 = s.RealDeathPercentAtMedicine300;
            c.KiaWoundedPercentAtMedicine0 = s.KiaWoundedPercentAtMedicine0;
            c.KiaWoundedPercentAtMedicine300 = s.KiaWoundedPercentAtMedicine300;
            c.DownedWoundedPercentAtMedicine0 = s.DownedWoundedPercentAtMedicine0;
            c.DownedWoundedPercentAtMedicine300 = s.DownedWoundedPercentAtMedicine300;
            c.DisorganizedAfterTraining = s.DisorganizedAfterTraining;
            c.HeroHealthRestorePercent = s.HeroHealthRestorePercent;
            c.CooldownHours = s.CooldownHours;
            c.TrainingCostWagesLand = s.TrainingCostWagesLand;
            c.TrainingCostWagesSea = s.TrainingCostWagesSea;
            c.AutoSplitInHalf = s.AutoSplitInHalf;
            c.DebugLogging = s.DebugLogging;
            TbLog.Enabled = c.DebugLogging; // the toggle bites immediately, not on restart
            c.UseOpponentBanner = s.UseOpponentBanner;
            c.OpponentBannerCode = string.IsNullOrWhiteSpace(s.OpponentBannerCode)
                ? c.OpponentBannerCode : s.OpponentBannerCode;
            c.ChooseGroundWhenDefending = s.ChooseGroundWhenDefending;
            c.ChooseGroundWhenAttacking = s.ChooseGroundWhenAttacking;
            c.ScoutingGateEnabled = s.ScoutingGateEnabled;
            c.ScoutingGateDefendPercent = s.ScoutingGateDefendPercent;
            c.ScoutingGateAttackPercent = s.ScoutingGateAttackPercent;
            var hourIndex = s.BattleTimeOfDay?.SelectedIndex ?? 0;
            c.BattleTimeOfDay = hourIndex > 0 && hourIndex <= ModConfig.SupportedBattleHours.Length
                ? ModConfig.SupportedBattleHours[hourIndex - 1]
                : -1;
            c.OpenMenuHotkey = s.OpenMenuHotkey.SelectedValue ?? c.OpenMenuHotkey;
        }

        /// <summary>Selects a dropdown value, adding it first if the list does not already carry it —
        /// so a key the player set by hand in config.json shows up instead of being silently dropped.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SelectOrAdd(MCM.Common.Dropdown<string> dropdown, string value)
        {
            if (dropdown == null || string.IsNullOrWhiteSpace(value)) return;
            var index = dropdown.IndexOf(value);
            if (index < 0)
            {
                dropdown.Add(value);
                index = dropdown.Count - 1;
            }
            dropdown.SelectedIndex = index;
        }
    }
}
