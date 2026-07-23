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
            s.XpKeptPercent = c.XpKeptPercent;
            s.WoundedPercent = c.WoundedPercent;
            s.DisorganizedAfterTraining = c.DisorganizedAfterTraining;
            s.CooldownHours = c.CooldownHours;
            SelectOrAdd(s.OpenMenuHotkey, c.OpenMenuHotkey);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PullMenuToConfig(TrainingBattlesMcmSettings s, ModConfig c)
        {
            c.XpKeptPercent = s.XpKeptPercent;
            c.WoundedPercent = s.WoundedPercent;
            c.DisorganizedAfterTraining = s.DisorganizedAfterTraining;
            c.CooldownHours = s.CooldownHours;
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
