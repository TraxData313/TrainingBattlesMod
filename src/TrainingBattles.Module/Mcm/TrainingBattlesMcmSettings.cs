using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;

namespace TrainingBattles.Mcm
{
    /// <summary>
    /// The in-game Mod Configuration Menu (MCM) face of <see cref="ModConfig"/>. MCM auto-discovers
    /// this class when the Mod Configuration Menu module is installed; when it is NOT, nothing ever
    /// touches this type, so the mod runs on <c>config.json</c> alone (see <see cref="McmBridge"/>).
    ///
    /// <c>config.json</c> remains the authoritative store: <see cref="McmBridge"/> pushes the current
    /// config values into this menu on startup and writes every menu change straight back, so no menu
    /// default can ever wipe a value the player set by hand.
    ///
    /// LAYOUT (Anton, 2026.07.25 — the officers update): one group PER OFFICER, each headed by the
    /// role that owns the numbers, so the design reads straight off the menu — the quartermaster
    /// earns the XP, the surgeon answers for the fallen, the scout wins the ground, and since
    /// the castle update the engineer builds the siege equipment.
    /// </summary>
    public sealed class TrainingBattlesMcmSettings : AttributeGlobalSettings<TrainingBattlesMcmSettings>
    {
        public override string Id => "TrainingBattles_v1";
        public override string DisplayName => "Training Battles";
        public override string FolderName => "TrainingBattles";
        public override string FormatType => "json2";

        // ---- Features -----------------------------------------------------------------

        [SettingPropertyBool("Mock-enemy training", Order = 0, RequireRestart = false,
            HintText = "Compose an enemy force of any culture (full troop tree, any counts) and drill against it. The enemy are phantoms — they never touch your roster; your side follows the normal training rules. Default on.")]
        [SettingPropertyGroup("Features", GroupOrder = 0)]
        public bool EnableMockEnemyTraining { get; set; } = true;

        [SettingPropertyBool("Castle siege training", Order = 1, RequireRestart = false,
            HintText = "At a castle you OWN: muster the company and storm — or hold — your own walls, siege equipment and all. The garrison and militia stand with the defense and follow the same training rules (wounds healed, XP kept, the surgeon's small real-death band). Default on.")]
        [SettingPropertyGroup("Features", GroupOrder = 0)]
        public bool EnableCastleTraining { get; set; } = true;

        // ---- The Quartermaster — drill XP ---------------------------------------------
        // (At sea the First Mate takes over, judged by War Sails' Boatswain skill.)

        [SettingPropertyInteger("XP kept at skill 0 (%)", 0, 200, "0'%'", Order = 0, RequireRestart = false,
            HintText = "The share of drill XP the troops keep scales with your XP officer: the QUARTERMASTER's Leadership on land (yours, when nobody holds the role), the FIRST MATE's Boatswain at sea. This is the floor at skill 0. Defaults 40 to 100 — past 100% a drill GRANTS bonus XP. Set both sliders equal for a flat rate.")]
        [SettingPropertyGroup("The Quartermaster — drill XP", GroupOrder = 1)]
        public int XpKeptMinPercent { get; set; } = 40;

        [SettingPropertyInteger("XP kept at skill 300 (%)", 0, 200, "0'%'", Order = 1, RequireRestart = false,
            HintText = "The band's ceiling at skill 300. Above 100% a master quartermaster (or first mate) squeezes bonus XP out of every drill. Default 100.")]
        [SettingPropertyGroup("The Quartermaster — drill XP", GroupOrder = 1)]
        public int XpKeptMaxPercent { get; set; } = 100;

        [SettingPropertyFloatingInteger("Instructor bonus at weapon skill 300 (%)", 0f, 50f, "0.0'%'", Order = 2, RequireRestart = false,
            HintText = "Your best-fighting companions instruct the drill: each adds up to this many points to the XP kept (linear with their best weapon skill, 0 at skill 0). Added on top of the officer's band; the 200% cap still rules. 0 = off. Default 5.")]
        [SettingPropertyGroup("The Quartermaster — drill XP", GroupOrder = 1)]
        public float XpInstructorBonusPercentAt300 { get; set; } = 5.0f;

        [SettingPropertyInteger("Instructors counted", 0, 10, "0", Order = 3, RequireRestart = false,
            HintText = "How many companions instruct, best fighters first. Default 3.")]
        [SettingPropertyGroup("The Quartermaster — drill XP", GroupOrder = 1)]
        public int XpInstructorMaxCount { get; set; } = 3;

        // ---- The Surgeon — the fallen -------------------------------------------------
        // Three bands, each scaled linearly by the surgeon's Medicine from the "no doctor"
        // end (skill 0) to the "master doctor" end (skill 300). No wounded man ever dies.

        [SettingPropertyFloatingInteger("Real deaths at Medicine 0 (%)", 0f, 25f, "0.0'%'", Order = 0, RequireRestart = false,
            HintText = "KIA→KIA: of the men who would have died, this share TRULY DIES with no doctor — the drill's one real, permanent cost. Default 3. Set both real-death sliders to 0 for the old 'nobody ever dies' training.")]
        [SettingPropertyGroup("The Surgeon — the fallen", GroupOrder = 2)]
        public float RealDeathPercentAtMedicine0 { get; set; } = 3.0f;

        [SettingPropertyFloatingInteger("Real deaths at Medicine 300 (%)", 0f, 25f, "0.0'%'", Order = 1, RequireRestart = false,
            HintText = "The same share under a master surgeon. Default 0.1 — a great doctor makes hard training nearly safe.")]
        [SettingPropertyGroup("The Surgeon — the fallen", GroupOrder = 2)]
        public float RealDeathPercentAtMedicine300 { get; set; } = 0.1f;

        [SettingPropertyFloatingInteger("Fallen wake wounded at Medicine 0 (%)", 0f, 100f, "0.0'%'", Order = 2, RequireRestart = false,
            HintText = "KIA→wounded: of the would-have-died who live, this share wakes truly WOUNDED with no doctor; the rest shrug it off. Default 20.")]
        [SettingPropertyGroup("The Surgeon — the fallen", GroupOrder = 2)]
        public float KiaWoundedPercentAtMedicine0 { get; set; } = 20.0f;

        [SettingPropertyFloatingInteger("Fallen wake wounded at Medicine 300 (%)", 0f, 100f, "0.0'%'", Order = 3, RequireRestart = false,
            HintText = "The same share under a master surgeon. Default 5.")]
        [SettingPropertyGroup("The Surgeon — the fallen", GroupOrder = 2)]
        public float KiaWoundedPercentAtMedicine300 { get; set; } = 5.0f;

        [SettingPropertyFloatingInteger("Downed stay wounded at Medicine 0 (%)", 0f, 100f, "0.0'%'", Order = 4, RequireRestart = false,
            HintText = "Wounded→wounded: of the men merely downed in the drill (knocked out, battle-wounded — they never died), this share STAYS wounded with no doctor; the rest are patched up on the spot. Default 10. There is deliberately no wounded→death path.")]
        [SettingPropertyGroup("The Surgeon — the fallen", GroupOrder = 2)]
        public float DownedWoundedPercentAtMedicine0 { get; set; } = 10.0f;

        [SettingPropertyFloatingInteger("Downed stay wounded at Medicine 300 (%)", 0f, 100f, "0.0'%'", Order = 5, RequireRestart = false,
            HintText = "The same share under a master surgeon. Default 1.")]
        [SettingPropertyGroup("The Surgeon — the fallen", GroupOrder = 2)]
        public float DownedWoundedPercentAtMedicine300 { get; set; } = 1.0f;

        [SettingPropertyInteger("Hero health restored after a drill (%)", 0, 100, "0'%'", Order = 6, RequireRestart = false,
            HintText = "The player and companions are healed back to at least this share of max health after a drill (never above what they walked in with). 0 disables. Default 90.")]
        [SettingPropertyGroup("The Surgeon — the fallen", GroupOrder = 2)]
        public int HeroHealthRestorePercent { get; set; } = 90;

        // ---- The Scout — the ground ---------------------------------------------------
        // (At sea the Navigator takes over, judged by War Sails' Shipmaster skill.)

        [SettingPropertyBool("Scouting earns the ground tools", Order = 0, RequireRestart = false,
            HintText = "In a REAL battle, choosing the battlefield and pinning a non-daylight hour must be EARNED: your ground officer (the SCOUT's Scouting on land, the NAVIGATOR's Shipmaster at sea) against the enemy's best, at the two ratios below. When out-scouted the options show locked, with the exact numbers. The campaign clock and full daylight stay always available. Off = every tool unlocked regardless of scouts. Default on.")]
        [SettingPropertyGroup("The Scout — the ground", GroupOrder = 3)]
        public bool ScoutingGateEnabled { get; set; } = true;

        [SettingPropertyInteger("Defending: skill needed (% of enemy's)", 0, 500, "0'%'", Order = 1, RequireRestart = false,
            HintText = "When you DEFEND, your ground officer's skill must be at least this share of the enemy side's best to choose the ground — you already hold it, so a modest screen of outriders will do. Default 50.")]
        [SettingPropertyGroup("The Scout — the ground", GroupOrder = 3)]
        public int ScoutingGateDefendPercent { get; set; } = 50;

        [SettingPropertyInteger("Attacking: skill needed (% of enemy's)", 0, 500, "0'%'", Order = 2, RequireRestart = false,
            HintText = "When you ATTACK, the bar rises — dictating WHERE the enemy must fight takes a real intelligence edge. Default 150.")]
        [SettingPropertyGroup("The Scout — the ground", GroupOrder = 3)]
        public int ScoutingGateAttackPercent { get; set; } = 150;

        // ---- The Engineer — siege equipment -------------------------------------------

        [SettingPropertyInteger("Tier 1 opens at Engineering", 0, 300, "0", Order = 0, RequireRestart = false,
            HintText = "The castle drill's siege equipment unlocks in tiers by your ENGINEER's Engineering skill (yours, when nobody holds the role). The ram is always available; tier 1 adds the ballista and the onager. Default 50.")]
        [SettingPropertyGroup("The Engineer — siege equipment", GroupOrder = 4)]
        public int EngineerTier1Skill { get; set; } = 50;

        [SettingPropertyInteger("Tier 2 opens at Engineering", 0, 300, "0", Order = 1, RequireRestart = false,
            HintText = "Tier 2 adds the siege tower and the fire variants. Default 100.")]
        [SettingPropertyGroup("The Engineer — siege equipment", GroupOrder = 4)]
        public int EngineerTier2Skill { get; set; } = 100;

        [SettingPropertyInteger("Tier 3 opens at Engineering", 0, 300, "0", Order = 2, RequireRestart = false,
            HintText = "Tier 3 is the trebuchet. Default 150.")]
        [SettingPropertyGroup("The Engineer — siege equipment", GroupOrder = 4)]
        public int EngineerTier3Skill { get; set; } = 150;

        [SettingPropertyInteger("Engine cost (gold per man-day)", 0, 1000, "0", Order = 3, RequireRestart = false,
            HintText = "Each engine built for the drill adds its worth to the bill: the game's own construction cost in man-days times this rate — a trebuchet costs real money, a ballista is pocket change. 0 = engines are free. Default 20.")]
        [SettingPropertyGroup("The Engineer — siege equipment", GroupOrder = 4)]
        public int SiegeEngineGoldPerManDay { get; set; } = 20;

        // ---- Pacing & costs -----------------------------------------------------------

        [SettingPropertyInteger("Cooldown (in-game hours)", 0, 168, "0", Order = 0, RequireRestart = false,
            HintText = "Rest needed between drills. 0 = unlimited. Default 24.")]
        [SettingPropertyGroup("Pacing & costs", GroupOrder = 5)]
        public int CooldownHours { get; set; } = 24;

        [SettingPropertyFloatingInteger("Steward divides the cooldown (at 300)", 1f, 10f, "'/'0.0", Order = 1, RequireRestart = false,
            HintText = "Your QUARTERMASTER's Steward (yours, when nobody holds the role) speeds the camp: every drill cooldown — the field clock and each castle's — is divided by a factor rising from /1 at Steward 0 to this at 300. Default /4. Set 1 to turn the speed-up off.")]
        [SettingPropertyGroup("Pacing & costs", GroupOrder = 5)]
        public float CooldownDivisorAtSteward300 { get; set; } = 4.0f;

        [SettingPropertyInteger("Land drill cost (days of wages)", 0, 30, "0", Order = 2, RequireRestart = false,
            HintText = "A land drill costs this many days of wages for every soldier on the field — equipment, upkeep, and rewards to keep the good fighters motivated. 0 = free. Default 1. (Future castle, city and army drills will get their own rates.)")]
        [SettingPropertyGroup("Pacing & costs", GroupOrder = 5)]
        public int TrainingCostWagesLand { get; set; } = 1;

        [SettingPropertyInteger("Sea drill cost (days of wages)", 0, 60, "0", Order = 3, RequireRestart = false,
            HintText = "The sea drill's rate — rigging wears, spars crack, salt eats everything. Default 2, double the land drill.")]
        [SettingPropertyGroup("Pacing & costs", GroupOrder = 5)]
        public int TrainingCostWagesSea { get; set; } = 2;

        [SettingPropertyInteger("Castle drill cost (days of wages)", 0, 60, "0", Order = 4, RequireRestart = false,
            HintText = "The castle siege drill's rate, paid for every soul on the field — both halves, garrison and militia alike; a siege takes real organization. Picked siege engines add their own price on top (the Engineer group). Default 5.")]
        [SettingPropertyGroup("Pacing & costs", GroupOrder = 5)]
        public int CastleTrainingCostWages { get; set; } = 5;

        [SettingPropertyInteger("Castle drill cooldown (in-game hours)", 0, 720, "0", Order = 5, RequireRestart = false,
            HintText = "Rest between castle drills AT THE SAME CASTLE — each castle keeps its own clock, separate from the field drill's. 0 = unlimited. Default 168 (one week).")]
        [SettingPropertyGroup("Pacing & costs", GroupOrder = 5)]
        public int CastleTrainingCooldownHours { get; set; } = 168;

        [SettingPropertyBool("Deal the company in half by default", Order = 6, RequireRestart = false,
            HintText = "Opening the divide screen with no pick made deals every man and companion 50/50 into the two halves first (still fully editable). Default on.")]
        [SettingPropertyGroup("Pacing & costs", GroupOrder = 5)]
        public bool AutoSplitInHalf { get; set; } = true;

        [SettingPropertyBool("Disorganized after training", Order = 7, RequireRestart = false,
            HintText = "The party is disorganized for a while after a drill. Default on.")]
        [SettingPropertyGroup("Pacing & costs", GroupOrder = 5)]
        public bool DisorganizedAfterTraining { get; set; } = true;

        [SettingPropertyFloatingInteger("Castle drill renown (per 100 men)", 0f, 10f, "0.0", Order = 8, RequireRestart = false,
            HintText = "A castle siege drill is a grand muster and the realm notices: renown earned per 100 friendly men on the field (both halves, garrison and militia), once per drill. Paid at the drill's end, never through battle rewards — kills and loot stay worthless in training. 0 = none. Default 1.")]
        [SettingPropertyGroup("Pacing & costs", GroupOrder = 5)]
        public float CastleDrillRenownPer100Men { get; set; } = 1.0f;

        [SettingPropertyFloatingInteger("Castle drill influence (per 100 men)", 0f, 10f, "0.0", Order = 9, RequireRestart = false,
            HintText = "The same muster's influence — the kingdom sees a lord who keeps a sharp garrison. 0 = none. Default 1.")]
        [SettingPropertyGroup("Pacing & costs", GroupOrder = 5)]
        public float CastleDrillInfluencePer100Men { get; set; } = 1.0f;

        // ---- Battlefield --------------------------------------------------------------

        [SettingPropertyBool("Survey & scout ground when defending", Order = 0, RequireRestart = false,
            HintText = "When you DEFEND a real field battle, the encounter menu offers the ground tools: pick the battlefield (the local terrain's variants, your true ground marked) and scout it alone before the fight. The scouting duel above decides whether the pick is unlocked. Default on.")]
        [SettingPropertyGroup("Battlefield", GroupOrder = 6)]
        public bool ChooseGroundWhenDefending { get; set; } = true;

        [SettingPropertyBool("Survey & scout ground when attacking", Order = 1, RequireRestart = false,
            HintText = "The same battlefield tools when you ATTACK a real field battle — walk the field before ordering the assault. Default on.")]
        [SettingPropertyGroup("Battlefield", GroupOrder = 6)]
        public bool ChooseGroundWhenAttacking { get; set; } = true;

        [SettingPropertyBool("Training banner for the opposing half", Order = 2, RequireRestart = false,
            HintText = "The opposing half flies the training banner and its colors instead of random bandit colors. Default on.")]
        [SettingPropertyGroup("Battlefield", GroupOrder = 6)]
        public bool UseOpponentBanner { get; set; } = true;

        [SettingPropertyText("Opponent banner code", Order = 3, RequireRestart = false,
            HintText = "The opposing half's banner, as a banner code (copy one from the game's banner editor with Ctrl+C). Default: orange field, white cross.")]
        [SettingPropertyGroup("Battlefield", GroupOrder = 6)]
        public string OpponentBannerCode { get; set; } = ModConfig.DefaultOpponentBannerCode;

        [SettingPropertyDropdown("Time of day for battles (standing default)", Order = 4, RequireRestart = false,
            HintText = "The STANDING default hour battles open at — drills, field battles, sieges, sea battles (an immersion trade for a field you can see). The muster and encounter menus can override it for ONE battle without touching this. Default: campaign clock.")]
        [SettingPropertyGroup("Battlefield", GroupOrder = 6)]
        public Dropdown<string> BattleTimeOfDay { get; set; } = TimeOfDayChoices(-1);

        /// <summary>Labels in lockstep with ModConfig.SupportedBattleHours: index 0 is the campaign
        /// clock, index i+1 is SupportedBattleHours[i] — McmBridge maps by this convention.</summary>
        internal static Dropdown<string> TimeOfDayChoices(int selectedHour)
        {
            var labels = new string[ModConfig.SupportedBattleHours.Length + 1];
            labels[0] = TrainingBattles.Models.AtmospherePresets.Label(-1);
            var selected = 0;
            for (var i = 0; i < ModConfig.SupportedBattleHours.Length; i++)
            {
                labels[i + 1] = TrainingBattles.Models.AtmospherePresets.Label(ModConfig.SupportedBattleHours[i]);
                if (ModConfig.SupportedBattleHours[i] == selectedHour) selected = i + 1;
            }
            return new Dropdown<string>(labels, selected);
        }

        // ---- Controls & debug ---------------------------------------------------------

        [SettingPropertyDropdown("Open-menu key", Order = 0, RequireRestart = false,
            HintText = "Campaign-map key for the training muster. Default G (T is the vanilla combat log).")]
        [SettingPropertyGroup("Controls", GroupOrder = 7)]
        public Dropdown<string> OpenMenuHotkey { get; set; } = HotkeyChoices("G");

        [SettingPropertyBool("Debug logging", Order = 0, RequireRestart = false,
            HintText = "Writes a rolling ledger of drills, duels and picks to Configs\\TrainingBattles\\training_battles.log (the per-drill arithmetic is always in last_drill_report.txt). Cheap; trimmed at ~1 MB. Default on.")]
        [SettingPropertyGroup("Debug", GroupOrder = 8)]
        public bool DebugLogging { get; set; } = true;

        private static Dropdown<string> HotkeyChoices(string selected)
        {
            var keys = new[]
            {
                "G", "H", "K", "O", "U", "Y", "X", "Z", "T",
                "F9", "F10", "F11", "F12",
            };
            var index = System.Array.IndexOf(keys, selected);
            return new Dropdown<string>(keys, index >= 0 ? index : 0);
        }
    }
}
