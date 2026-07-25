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
        public int ConfigVersion { get; set; } = 10;

        /// <summary>The split-army drill: divide your own company in two and fight yourself —
        /// THE CORE OF THE MOD, on by default and deliberately NOT in the MCM menu (Anton,
        /// 2026.07.23: it's not cheating, and a player who only wants to scout simply doesn't
        /// drill). This key stays as a hand-edit escape hatch only.</summary>
        public bool EnableSplitTraining { get; set; } = true;

        /// <summary>The second drill mode: compose a MOCK ENEMY of any culture (full troop tree,
        /// any counts) and drill against it. The enemy are phantoms — fresh troops that never
        /// touch the player's roster and vanish after the fight; the player's side follows the
        /// normal training rules (cost, cooldown, XP kept, wounded-not-dead). ON by default since
        /// v9 (Anton, 2026.07.25) — it graduated from developer option to shipped feature.</summary>
        public bool EnableMockEnemyTraining { get; set; } = true;

        /// <summary>The XP OFFICER's stake in the drill: the percent of earned XP the troops keep
        /// scales linearly with the officer's skill from this floor (skill 0) up to
        /// <see cref="XpKeptMaxPercent"/> (skill 300). The officer is the QUARTERMASTER and the
        /// skill is LEADERSHIP on land (Anton's call, 2026.07.25 — the player's own Leadership
        /// when no quartermaster is assigned); at sea the FIRST MATE and War Sails' BOATSWAIN
        /// take over. Defaults 40..100. Set both keys equal for a flat rate, skill ignored.</summary>
        public int XpKeptMinPercent { get; set; } = 40;

        /// <summary>The ceiling of the XP officer's band — the percent kept at skill 300. May top
        /// 100 (up to 200): the excess is granted as bonus XP. See
        /// <see cref="XpKeptMinPercent"/>. Default 100.</summary>
        public int XpKeptMaxPercent { get; set; } = 100;

        /// <summary>v8's flat "XP kept" knob — kept only so a hand-set value in an old
        /// config.json migrates into the quartermaster band (as a flat min = max = the old pick).
        /// Null once migrated; never written back.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? XpKeptPercent { get; set; }

        // ---- The SURGEON's three bands (Anton, 2026.07.25) --------------------------------
        // Each casualty outcome scales LINEARLY with the effective surgeon's Medicine from the
        // AtMedicine0 end (no doctor) to the AtMedicine300 end (a master), read through
        // AftermathMath.ChancePercentForSkill. Heroes are always exempt, and there is
        // deliberately NO wounded→death path. Set a pair equal for a flat rate.

        /// <summary>KIA→KIA: of the men who would have died, this percent TRULY DIES — the
        /// drill's one real, permanent cost (Anton, 2026.07.25: a hard drill has real stakes).
        /// Default 3% with no doctor, 0.1% at Medicine 300. Set both to 0 to restore the old
        /// "nobody ever dies" pledge.</summary>
        public double RealDeathPercentAtMedicine0 { get; set; } = 3.0;
        public double RealDeathPercentAtMedicine300 { get; set; } = 0.1;

        /// <summary>KIA→wounded: of the would-have-died who live, this percent wakes truly
        /// WOUNDED; the rest shrug it off. Default 30% with no doctor, 5% at Medicine 300.</summary>
        public double KiaWoundedPercentAtMedicine0 { get; set; } = 30.0;
        public double KiaWoundedPercentAtMedicine300 { get; set; } = 5.0;

        /// <summary>Wounded→wounded: of the men merely DOWNED in the drill (battle-wounded and
        /// knocked out — they never died), this percent STAYS wounded afterward; the rest are
        /// patched up on the spot. Default 15% with no doctor, 1% at Medicine 300.</summary>
        public double DownedWoundedPercentAtMedicine0 { get; set; } = 15.0;
        public double DownedWoundedPercentAtMedicine300 { get; set; } = 1.0;

        /// <summary>v9's flat "wounded among the fallen" knob — kept only so a hand-set value in
        /// an old config.json migrates into the KIA→wounded band (flat: both ends = the old
        /// pick). Null once migrated; never written back.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? WoundedPercent { get; set; }

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

        /// <summary>What a LAND drill costs, in DAYS OF WAGES of every soldier on the training
        /// field (both halves — they all drill). 0 = free. Default 1: one day's extra pay for a
        /// day of hard knocks. (For scale: the Steward donation perks convert roughly 1 denar of
        /// cheap gear into 1 XP — a drill's XP haul priced that way would cost ten times more.)
        /// The cost splits by battle ground (Anton, 2026.07.25) — sea below; the planned castle
        /// (~10×), city and army drills will add their own keys here when they land.</summary>
        public int TrainingCostWagesLand { get; set; } = 1;

        /// <summary>The SEA drill's price in the same days-of-wages coin — rigging wears, spars
        /// crack, salt eats everything. Default 2, double the land drill.</summary>
        public int TrainingCostWagesSea { get; set; } = 2;

        /// <summary>v9's single cost knob — kept only so a hand-set value migrates (land = the
        /// old pick, sea = double it). Null once migrated; never written back.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? TrainingCostWages { get; set; }

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

        /// <summary>The hour EVERY battle is fought at — drills, the muster scout ride, and real
        /// battles of every type (field, siege assault, sea): -1 follows the campaign clock (the
        /// default); otherwise one of the game's own custom-battle hours — 6 (morning), 12 (noon),
        /// 15 (afternoon), 18 (evening), 22 (night). An admitted immersion-breaker Anton wants
        /// anyway (2026.07.24): players and streamers alike cannot see a thing in night battles.
        /// Drills read it into their mission record; real battles get it through
        /// <see cref="Models.TrainingBattlesMapWeatherModel"/>, which only overrides while a
        /// player map event is opening its mission. Editable from the muster and encounter menus
        /// ("Choose the time of day") as well as MCM — all write this one key.</summary>
        public int BattleTimeOfDay { get; set; } = -1;

        /// <summary>The hours vanilla's custom battle supports — each has a hand-made atmosphere
        /// preset; any other hour would fall back to midnight lighting.</summary>
        public static readonly int[] SupportedBattleHours = { 6, 12, 15, 18, 22 };

        /// <summary>Noon — the one pinned hour the scouting gate NEVER locks on a real battle:
        /// waiting for full daylight is quality-of-life (dark screens, streams), not an
        /// intelligence coup. The other hours must be earned; see
        /// <see cref="ScoutingGateEnabled"/>.</summary>
        public const int FullDaylightHour = 12;

        /// <summary>In-game hours between training battles (0 = unlimited). Default 24.</summary>
        public int CooldownHours { get; set; } = 24;

        /// <summary>Whether the party becomes DISORGANIZED after a training battle (slower on the
        /// map for a while, the vanilla post-battle state). Default true — drilling is tiring.</summary>
        public bool DisorganizedAfterTraining { get; set; } = true;

        /// <summary>The campaign-map key that opens the training muster menu. Default "G"
        /// ("T" was the v1 default until it turned out vanilla uses T for the combat log).</summary>
        public string OpenMenuHotkey { get; set; } = "G";

        /// <summary>The rolling debug ledger (<c>training_battles.log</c> beside this file):
        /// drills fought, duels judged, grounds picked, officers resolved. Cheap and trimmed;
        /// default on so every playtest is debuggable after the fact (Anton, 2026.07.25).</summary>
        public bool DebugLogging { get; set; } = true;

        /// <summary>When the player DEFENDS a real field battle, the encounter menu offers the
        /// ground tools: SURVEY (pick the battlefield from the same wider same-terrain pool the
        /// training muster offers, the true local ground marked) and SCOUT (ride the field alone
        /// before committing — the armies stand frozen, so the previewed lines and facings are the
        /// coming battle's true ones). You chose where to stand and wait, so the ground is yours.
        /// Default true.</summary>
        public bool ChooseGroundWhenDefending { get; set; } = true;

        /// <summary>The same ground tools when the player ATTACKS a real field battle — Anton's
        /// must-have (2026.07.23): the commander walks the field before ordering the assault.
        /// Default true.</summary>
        public bool ChooseGroundWhenAttacking { get; set; } = true;

        /// <summary>The SCOUTING DUEL (Anton, 2026.07.25): on a real battle, choosing the ground
        /// and dictating a non-daylight hour must be EARNED — your party's effective Scouting
        /// against the enemy side's best, at the ratios below. When the duel is lost the options
        /// stay visible but locked, telling the player the exact numbers. The campaign clock and
        /// full daylight stay pickable always (QoL, not gameplay — streamers and dark screens).
        /// False = the old free-for-all: every tool unlocked regardless of scouts. Default true.</summary>
        public bool ScoutingGateEnabled { get; set; } = true;

        /// <summary>When DEFENDING, your scouting must be at least this percent of the enemy's
        /// best to control the ground (you already hold it — a modest screen of outriders will
        /// do). Default 75.</summary>
        public int ScoutingGateDefendPercent { get; set; } = 75;

        /// <summary>When ATTACKING, the bar rises: dictating WHERE the enemy must fight takes a
        /// real intelligence edge. Default 125.</summary>
        public int ScoutingGateAttackPercent { get; set; } = 125;

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
            XpKeptMinPercent = Clamp(XpKeptMinPercent, 0, Core.AftermathMath.MaxKeepPercent);
            XpKeptMaxPercent = Clamp(XpKeptMaxPercent, 0, Core.AftermathMath.MaxKeepPercent);
            if (XpKeptMaxPercent < XpKeptMinPercent) XpKeptMaxPercent = XpKeptMinPercent;
            ScoutingGateDefendPercent = Clamp(ScoutingGateDefendPercent, 0, 500);
            ScoutingGateAttackPercent = Clamp(ScoutingGateAttackPercent, 0, 500);
            RealDeathPercentAtMedicine0 = ClampChance(RealDeathPercentAtMedicine0);
            RealDeathPercentAtMedicine300 = ClampChance(RealDeathPercentAtMedicine300);
            KiaWoundedPercentAtMedicine0 = ClampChance(KiaWoundedPercentAtMedicine0);
            KiaWoundedPercentAtMedicine300 = ClampChance(KiaWoundedPercentAtMedicine300);
            DownedWoundedPercentAtMedicine0 = ClampChance(DownedWoundedPercentAtMedicine0);
            DownedWoundedPercentAtMedicine300 = ClampChance(DownedWoundedPercentAtMedicine300);
            CooldownHours = Clamp(CooldownHours, 0, 168);
            HeroHealthRestorePercent = Clamp(HeroHealthRestorePercent, 0, 100);
            TrainingCostWagesLand = Clamp(TrainingCostWagesLand, 0, 30);
            TrainingCostWagesSea = Clamp(TrainingCostWagesSea, 0, 60);
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
            // v7 added ChooseGroundWhenAttacking (default true) and widened the real-battle
            // ground tools to the terrain pool — new key with a safe default, nothing to migrate.
            // v8 added BattleTimeOfDay (-1 = campaign clock) — safe default, nothing to migrate;
            // a hand-edited hour snaps to the nearest supported custom-battle hour.
            if (BattleTimeOfDay != -1)
            {
                var best = -1;
                var bestDistance = int.MaxValue;
                foreach (var hour in SupportedBattleHours)
                {
                    var distance = Math.Abs(BattleTimeOfDay - hour);
                    if (distance < bestDistance) { bestDistance = distance; best = hour; }
                }
                BattleTimeOfDay = best;
            }
            // v9 (2026.07.25): the mock-enemy drill graduated from developer option to shipped
            // feature — any older config carrying false carries the old default, so it follows to
            // on (the same call as v6's split-training flip). And the flat XpKeptPercent became
            // the quartermaster band (min..max over Steward 0..300): a value hand-set away from
            // the old default of 75 was a deliberate pick, so it migrates as a FLAT band
            // (min = max = the pick); the untouched default takes the new 50..150 design.
            if (ConfigVersion < 9)
            {
                EnableMockEnemyTraining = true;
                if (XpKeptPercent.HasValue && XpKeptPercent.Value != 75)
                {
                    XpKeptMinPercent = Clamp(XpKeptPercent.Value, 0, 100);
                    XpKeptMaxPercent = XpKeptMinPercent;
                }
            }
            XpKeptPercent = null; // migrated — never written back
            // v10 (2026.07.25, the officers update): the XP band's governing skill became
            // LEADERSHIP (was Steward, for hours) and its defaults 40..100 with a 200 cap — a
            // config still on v9's short-lived 50..150 defaults follows; hand picks stay (the
            // clamp above pulls anything over 200 down). The flat WoundedPercent became the
            // surgeon's three Medicine bands: a hand-set value away from the old default of 10
            // migrates as a FLAT KIA→wounded band; the untouched default takes the new design.
            // The single cost knob split into land/sea: a hand pick keeps land and doubles for
            // sea, the default takes 1/2.
            if (ConfigVersion < 10)
            {
                if (XpKeptMinPercent == 50 && XpKeptMaxPercent == 150)
                {
                    XpKeptMinPercent = 40;
                    XpKeptMaxPercent = 100;
                }
                if (WoundedPercent.HasValue && WoundedPercent.Value != 10)
                {
                    KiaWoundedPercentAtMedicine0 = ClampChance(WoundedPercent.Value);
                    KiaWoundedPercentAtMedicine300 = KiaWoundedPercentAtMedicine0;
                }
                if (TrainingCostWages.HasValue && TrainingCostWages.Value != 1)
                {
                    TrainingCostWagesLand = Clamp(TrainingCostWages.Value, 0, 30);
                    TrainingCostWagesSea = Clamp(TrainingCostWages.Value * 2, 0, 60);
                }
            }
            WoundedPercent = null;    // migrated — never written back
            TrainingCostWages = null; // migrated — never written back
            ConfigVersion = 10;
            if (string.IsNullOrWhiteSpace(OpenMenuHotkey)) OpenMenuHotkey = "G";
        }

        private static int Clamp(int value, int min, int max) =>
            value < min ? min : (value > max ? max : value);

        /// <summary>Chance percents live in 0..100 — fractions welcome (0.1% real deaths).</summary>
        private static double ClampChance(double value) =>
            value < 0.0 ? 0.0 : (value > 100.0 ? 100.0 : value);
    }
}
