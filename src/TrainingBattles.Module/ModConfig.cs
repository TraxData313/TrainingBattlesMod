using System;
using System.IO;
using Newtonsoft.Json;

namespace TrainingBattles
{
    /// <summary>
    /// Mod configuration, stored as JSON under the Bannerlord Documents config folder — the single
    /// source of truth. The in-game MCM menu (see <see cref="Mcm.McmBridge"/>) is a live editor over
    /// it: startup pushes these values into the menu, every menu change writes straight back here.
    /// </summary>
    public sealed class ModConfig
    {
        /// <summary>The config format's version stamp, so a later release can migrate defaults
        /// without clobbering hand-edits. Do not edit.</summary>
        public int ConfigVersion { get; set; } = 2;

        /// <summary>How much of the experience earned in a training battle the troops KEEP,
        /// as a percent (0..100). Default 75 — drilling teaches, but not quite like real blood.</summary>
        public int XpKeptPercent { get; set; } = 75;

        /// <summary>Of the men who "fell" in training and whom the surgeon could not patch up on the
        /// spot, this percent (0..100) wake up truly WOUNDED; the rest shrug it off. The surgeon's
        /// save uses the game's own Medicine-driven survival chance first — a good doctor helps.
        /// Default 50 (Anton's "reduced by 2"). Nobody ever dies in training.</summary>
        public int WoundedPercent { get; set; } = 50;

        /// <summary>In-game hours between training battles (0 = unlimited). Default 24.</summary>
        public int CooldownHours { get; set; } = 24;

        /// <summary>Whether the party becomes DISORGANIZED after a training battle (slower on the
        /// map for a while, the vanilla post-battle state). Default true — drilling is tiring.</summary>
        public bool DisorganizedAfterTraining { get; set; } = true;

        /// <summary>The campaign-map key that opens the training muster menu. Default "G"
        /// ("T" was the v1 default until it turned out vanilla uses T for the combat log).</summary>
        public string OpenMenuHotkey { get; set; } = "G";

        // ------------------------------------------------------------------

        public static string ConfigDirectory =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Mount and Blade II Bannerlord", "Configs", "TrainingBattles");

        public static string ConfigFilePath => Path.Combine(ConfigDirectory, "config.json");

        public static ModConfig LoadOrCreate()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    var loaded = JsonConvert.DeserializeObject<ModConfig>(File.ReadAllText(ConfigFilePath));
                    if (loaded != null)
                    {
                        loaded.Normalize();
                        loaded.Save(); // writes back any migrated/clamped values
                        return loaded;
                    }
                }

                var fresh = new ModConfig();
                fresh.Normalize();
                Directory.CreateDirectory(ConfigDirectory);
                File.WriteAllText(ConfigFilePath, JsonConvert.SerializeObject(fresh, Formatting.Indented));
                return fresh;
            }
            catch
            {
                // A broken file must never brick the mod — run on defaults for this session.
                var fallback = new ModConfig();
                fallback.Normalize();
                return fallback;
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(ConfigDirectory);
                File.WriteAllText(ConfigFilePath, JsonConvert.SerializeObject(this, Formatting.Indented));
            }
            catch { /* the value still lives for this session */ }
        }

        /// <summary>Clamps every value into its honest range; hand-edits outside it are pulled back.</summary>
        public void Normalize()
        {
            XpKeptPercent = Clamp(XpKeptPercent, 0, 100);
            WoundedPercent = Clamp(WoundedPercent, 0, 100);
            CooldownHours = Clamp(CooldownHours, 0, 168);
            // v1 shipped with "T" as the default — which vanilla uses for the combat log. Configs
            // still carrying that default follow to "G"; a key set by hand later is honored.
            if (ConfigVersion < 2 && string.Equals(OpenMenuHotkey?.Trim(), "T", StringComparison.OrdinalIgnoreCase))
                OpenMenuHotkey = "G";
            ConfigVersion = 2;
            if (string.IsNullOrWhiteSpace(OpenMenuHotkey)) OpenMenuHotkey = "G";
        }

        private static int Clamp(int value, int min, int max) =>
            value < min ? min : (value > max ? max : value);
    }
}
