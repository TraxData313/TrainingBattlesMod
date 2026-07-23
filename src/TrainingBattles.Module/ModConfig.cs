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
        public int ConfigVersion { get; set; } = 6;

        /// <summary>The split-army drill: divide your own company in two and fight yourself —
        /// THE CORE OF THE MOD, on by default and deliberately NOT in the MCM menu (Anton,
        /// 2026.07.23: it's not cheating, and a player who only wants to scout simply doesn't
        /// drill). This key stays as a hand-edit escape hatch only.</summary>
        public bool EnableSplitTraining { get; set; } = true;

        /// <summary>The developer option: compose a MOCK ENEMY of any culture (full troop tree,
        /// any counts) and drill against it. The enemy are phantoms — fresh troops that never
        /// touch the player's roster and vanish after the fight; the player's side follows the
        /// normal training rules (cost, cooldown, XP kept, wounded-not-dead). Off by default.</summary>
        public bool EnableMockEnemyTraining { get; set; } = false;

        /// <summary>How much of the experience earned in a training battle the troops KEEP,
        /// as a percent (0..100). Default 75 — the decided shipping default (Anton, 2026.07.23);
        /// the testing period ran at 100.</summary>
        public int XpKeptPercent { get; set; } = 75;

        /// <summary>Of ALL the drill's casualties (the "dead" and the battle-wounded alike) whom the
        /// surgeon could not patch up on the spot, this percent (0..100) wake up truly WOUNDED; the
        /// rest shrug it off. The surgeon's save uses the game's own Medicine-driven survival chance
        /// first — a good doctor helps. Default 10 (Anton's testing call, 2026.07.23). Nobody ever
        /// dies in training.</summary>
        public int WoundedPercent { get; set; } = 10;

        /// <summary>After a drill every HERO in it (the player and the companions) is healed back to
        /// at least this percent of their max health — but never above what they walked in with, so
        /// the drill can't be used as a free hospital. 0 disables the restore. Default 90: a training
        /// bruise may sting a little, but it must never bench a companion for days.</summary>
        public int HeroHealthRestorePercent { get; set; } = 90;

        /// <summary>When opening the muster's "divide the men" screen with no pick yet made, deal the
        /// company in half first: every companion and every man flips a fair coin for a side (the
        /// player always stays on their own side). The picker opens pre-dealt and fully editable.
        /// Default true.</summary>
        public bool AutoSplitInHalf { get; set; } = true;

        /// <summary>What a drill costs, in DAYS OF WAGES of every soldier on the training field
        /// (both halves — they all drill). 0 = free. Default 1: one day's extra pay for a day of
        /// hard knocks. (For scale: the Steward donation perks convert roughly 1 denar of cheap
        /// gear into 1 XP — a drill's XP haul priced that way would cost ten times more.)</summary>
        public int TrainingCostWages { get; set; } = 1;

        /// <summary>Whether the opposing half flies the training banner (and its colors) instead of
        /// whatever bandit clan lends us the temp party. Default true.</summary>
        public bool UseOpponentBanner { get; set; } = true;

        /// <summary>The opposing half's banner as a vanilla banner code (the same format the game's
        /// banner editor copies with Ctrl+C — paste any code here). Default: an orange field with a
        /// white cross. The banner's first color also becomes the opposing team's uniform color.</summary>
        public string OpponentBannerCode { get; set; } = DefaultOpponentBannerCode;

        /// <summary>Orange field (palette 223), white cross built from two stretched bars (shape 510,
        /// palette 172 white) — layers: solid background, vertical bar, horizontal bar.</summary>
        public const string DefaultOpponentBannerCode =
            "11.223.223.1528.1528.764.764.1.0.0"
            + ".510.172.172.420.1700.764.764.0.0.0"
            + ".510.172.172.1700.420.764.764.0.0.0";

        /// <summary>In-game hours between training battles (0 = unlimited). Default 24.</summary>
        public int CooldownHours { get; set; } = 24;

        /// <summary>Whether the party becomes DISORGANIZED after a training battle (slower on the
        /// map for a while, the vanilla post-battle state). Default true — drilling is tiring.</summary>
        public bool DisorganizedAfterTraining { get; set; } = true;

        /// <summary>The campaign-map key that opens the training muster menu. Default "G"
        /// ("T" was the v1 default until it turned out vanilla uses T for the combat log).</summary>
        public string OpenMenuHotkey { get; set; } = "G";

        /// <summary>When the player DEFENDS a real field battle, the encounter menu offers a
        /// choice among the battlefield variants the game would otherwise pick from at random —
        /// you chose where to stand and wait, so you choose the ground. The attacker always gets
        /// vanilla's pick. Default true.</summary>
        public bool ChooseGroundWhenDefending { get; set; } = true;

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
            HeroHealthRestorePercent = Clamp(HeroHealthRestorePercent, 0, 100);
            TrainingCostWages = Clamp(TrainingCostWages, 0, 30);
            if (string.IsNullOrWhiteSpace(OpponentBannerCode)) OpponentBannerCode = DefaultOpponentBannerCode;
            // v1 shipped with "T" as the default — which vanilla uses for the combat log. Configs
            // still carrying that default follow to "G"; a key set by hand later is honored.
            if (ConfigVersion < 2 && string.Equals(OpenMenuHotkey?.Trim(), "T", StringComparison.OrdinalIgnoreCase))
                OpenMenuHotkey = "G";
            // v2 ran the testing period at XpKept 100; the decided shipping default is 75. A
            // config still on the old default follows; any other value is a hand pick and stays.
            if (ConfigVersion < 3 && XpKeptPercent == 100)
                XpKeptPercent = 75;
            // v4 added HeroHealthRestorePercent, AutoSplitInHalf, TrainingCostWages and the opponent
            // banner — all new keys with safe defaults, nothing to migrate.
            // v5 added EnableSplitTraining (then default false) and EnableMockEnemyTraining
            // (developer option — default false). v6, the same day: the split drill is the CORE
            // of the mod — always on, its MCM toggle removed. Any v5 config carries false only
            // because that was v5's short-lived default, so the migration flips it back on.
            if (ConfigVersion < 6)
                EnableSplitTraining = true;
            ConfigVersion = 6;
            if (string.IsNullOrWhiteSpace(OpenMenuHotkey)) OpenMenuHotkey = "G";
        }

        private static int Clamp(int value, int min, int max) =>
            value < min ? min : (value > max ? max : value);
    }
}
