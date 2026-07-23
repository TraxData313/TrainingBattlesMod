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
            HintText = "How much of the experience earned in a training battle the troops keep. 100 = training teaches as well as real war; 0 = pure sparring, nothing sticks. Default 75.")]
        [SettingPropertyGroup("Aftermath", GroupOrder = 0)]
        public int XpKeptPercent { get; set; } = 75;

        [SettingPropertyInteger("Wounded among the fallen (%)", 0, 100, "0'%'", Order = 1, RequireRestart = false,
            HintText = "Nobody dies in training. Of the men who 'fell' and whom the surgeon could not patch up on the spot (the surgeon's save uses the game's own Medicine-driven survival chance — a good doctor helps), this share wake up truly wounded; the rest shrug it off. Default 50.")]
        [SettingPropertyGroup("Aftermath", GroupOrder = 0)]
        public int WoundedPercent { get; set; } = 50;

        [SettingPropertyBool("Disorganized after training", Order = 2, RequireRestart = false,
            HintText = "The party becomes disorganized after a training battle (the vanilla slower-on-the-map post-battle state). Drilling is tiring. Default on.")]
        [SettingPropertyGroup("Aftermath", GroupOrder = 0)]
        public bool DisorganizedAfterTraining { get; set; } = true;

        [SettingPropertyInteger("Cooldown (in-game hours)", 0, 168, "0", Order = 0, RequireRestart = false,
            HintText = "How long the men need to rest between training battles. 0 = unlimited drilling. Default 24 — once a day.")]
        [SettingPropertyGroup("Pacing", GroupOrder = 1)]
        public int CooldownHours { get; set; } = 24;

        [SettingPropertyDropdown("Open-menu key", Order = 0, RequireRestart = false,
            HintText = "The campaign-map key that calls the men to a training muster. Pick one that does not clash with your other map keys. Default T.")]
        [SettingPropertyGroup("Controls", GroupOrder = 2)]
        public Dropdown<string> OpenMenuHotkey { get; set; } = HotkeyChoices("T");

        private static Dropdown<string> HotkeyChoices(string selected)
        {
            var keys = new[]
            {
                "T", "G", "H", "K", "O", "U", "Y", "X", "Z",
                "F9", "F10", "F11", "F12",
            };
            var index = System.Array.IndexOf(keys, selected);
            return new Dropdown<string>(keys, index >= 0 ? index : 0);
        }
    }
}
