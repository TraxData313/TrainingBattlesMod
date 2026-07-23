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
    /// </summary>
    public sealed class TrainingBattlesMcmSettings : AttributeGlobalSettings<TrainingBattlesMcmSettings>
    {
        public override string Id => "TrainingBattles_v1";
        public override string DisplayName => "Training Battles";
        public override string FolderName => "TrainingBattles";
        public override string FormatType => "json2";

        [SettingPropertyInteger("XP kept (%)", 0, 100, "0'%'", Order = 0, RequireRestart = false,
            HintText = "Share of battle XP the troops keep from a drill. Default 75.")]
        [SettingPropertyGroup("Aftermath", GroupOrder = 0)]
        public int XpKeptPercent { get; set; } = 75;

        [SettingPropertyInteger("Wounded among the fallen (%)", 0, 100, "0'%'", Order = 1, RequireRestart = false,
            HintText = "Drill casualties your surgeon cannot save wake wounded at this rate; the rest shrug it off. Medicine skill helps. Default 10.")]
        [SettingPropertyGroup("Aftermath", GroupOrder = 0)]
        public int WoundedPercent { get; set; } = 10;

        [SettingPropertyBool("Disorganized after training", Order = 2, RequireRestart = false,
            HintText = "The party is disorganized for a while after a drill. Default on.")]
        [SettingPropertyGroup("Aftermath", GroupOrder = 0)]
        public bool DisorganizedAfterTraining { get; set; } = true;

        [SettingPropertyInteger("Hero health restored after a drill (%)", 0, 100, "0'%'", Order = 3, RequireRestart = false,
            HintText = "The player and companions are healed back to at least this share of max health after a drill (never above what they walked in with). 0 disables. Default 90.")]
        [SettingPropertyGroup("Aftermath", GroupOrder = 0)]
        public int HeroHealthRestorePercent { get; set; } = 90;

        [SettingPropertyInteger("Cooldown (in-game hours)", 0, 168, "0", Order = 0, RequireRestart = false,
            HintText = "Rest needed between drills. 0 = unlimited. Default 24.")]
        [SettingPropertyGroup("Pacing", GroupOrder = 1)]
        public int CooldownHours { get; set; } = 24;

        [SettingPropertyInteger("Training cost (days of wages)", 0, 30, "0", Order = 1, RequireRestart = false,
            HintText = "A drill costs this many days of wages for every soldier on the field. 0 = free. Default 1.")]
        [SettingPropertyGroup("Pacing", GroupOrder = 1)]
        public int TrainingCostWages { get; set; } = 1;

        [SettingPropertyBool("Deal the company in half by default", Order = 2, RequireRestart = false,
            HintText = "Opening the divide screen with no pick made deals every man and companion 50/50 into the two halves first (still fully editable). Default on.")]
        [SettingPropertyGroup("Pacing", GroupOrder = 1)]
        public bool AutoSplitInHalf { get; set; } = true;

        [SettingPropertyBool("Training banner for the opposing half", Order = 1, RequireRestart = false,
            HintText = "The opposing half flies the training banner and its colors instead of random bandit colors. Default on.")]
        [SettingPropertyGroup("Battlefield", GroupOrder = 2)]
        public bool UseOpponentBanner { get; set; } = true;

        [SettingPropertyText("Opponent banner code", Order = 2, RequireRestart = false,
            HintText = "The opposing half's banner, as a banner code (copy one from the game's banner editor with Ctrl+C). Default: orange field, white cross.")]
        [SettingPropertyGroup("Battlefield", GroupOrder = 2)]
        public string OpponentBannerCode { get; set; } = ModConfig.DefaultOpponentBannerCode;

        [SettingPropertyBool("Choose ground when defending", Order = 0, RequireRestart = false,
            HintText = "When you DEFEND a real field battle, the encounter menu offers a choice among the battlefield variants the game would pick from at random. Default on.")]
        [SettingPropertyGroup("Battlefield", GroupOrder = 2)]
        public bool ChooseGroundWhenDefending { get; set; } = true;

        [SettingPropertyDropdown("Open-menu key", Order = 0, RequireRestart = false,
            HintText = "Campaign-map key for the training muster. Default G (T is the vanilla combat log).")]
        [SettingPropertyGroup("Controls", GroupOrder = 3)]
        public Dropdown<string> OpenMenuHotkey { get; set; } = HotkeyChoices("G");

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
