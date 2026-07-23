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

        [SettingPropertyInteger("Cooldown (in-game hours)", 0, 168, "0", Order = 0, RequireRestart = false,
            HintText = "Rest needed between drills. 0 = unlimited. Default 24.")]
        [SettingPropertyGroup("Pacing", GroupOrder = 1)]
        public int CooldownHours { get; set; } = 24;

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
