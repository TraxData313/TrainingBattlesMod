using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TrainingBattles
{
    public class SubModule : MBSubModuleBase
    {
        private bool _announced;

        // One config for the whole process: loaded once, shared by the behavior and the MCM menu,
        // so a change made in either is seen by both.
        private static ModConfig? _config;
        internal static ModConfig Config
        {
            get
            {
                if (_config == null)
                {
                    _config = ModConfig.LoadOrCreate();
                    TbLog.Enabled = _config.DebugLogging;
                    TbLog.Info("config", "loaded v" + _config.ConfigVersion
                        + " | xp band " + _config.XpKeptMinPercent + "-" + _config.XpKeptMaxPercent + "%"
                        + " | death " + _config.RealDeathPercentAtMedicine0 + "-" + _config.RealDeathPercentAtMedicine300 + "%"
                        + " | kia-wounded " + _config.KiaWoundedPercentAtMedicine0 + "-" + _config.KiaWoundedPercentAtMedicine300 + "%"
                        + " | downed-wounded " + _config.DownedWoundedPercentAtMedicine0 + "-" + _config.DownedWoundedPercentAtMedicine300 + "%"
                        + " | duel " + (_config.ScoutingGateEnabled
                            ? "on " + _config.ScoutingGateDefendPercent + "/" + _config.ScoutingGateAttackPercent + "%"
                            : "off")
                        + " | cost land/sea " + _config.TrainingCostWagesLand + "/" + _config.TrainingCostWagesSea
                        + " | mock " + _config.EnableMockEnemyTraining);
                }
                return _config;
            }
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            if (gameStarterObject is CampaignGameStarter starter)
            {
                var config = Config;
                // If MCM was not yet ready when the main menu came up, bind it now (no-op once bound).
                Mcm.McmBridge.TryBind(config);
                starter.AddBehavior(new TrainingBattleBehavior(config));
                // Survey (pick) and scout the ground on a real field battle's encounter menu,
                // defending or attacking (same picker and scout ride as training).
                starter.AddBehavior(new RealBattleGroundBehavior(config));
                // The "it was only training" guard: zero renown/loot/prisoners while a drill runs.
                starter.AddModel(new Models.TrainingBattleRewardModel());
                // The ground-choice gate: hands out the player's chosen battlefield, else
                // delegates to whichever SceneModel was registered before (BaseModel chain).
                starter.AddModel(new Models.TrainingBattlesSceneModel());
                // The battle-hour gate: when the player pinned a time of day, every REAL battle
                // mission (field, siege, sea) opens under that sky; weather itself is untouched.
                starter.AddModel(new Models.TrainingBattlesMapWeatherModel(config));
            }
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            if (!_announced)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Training Battles loaded — press " + Config.OpenMenuHotkey + " on the map to muster."));
                _announced = true;
            }
            // Bind the MCM menu as early as the main menu, so settings edited before a campaign is
            // even loaded take hold. A soft dependency: does nothing if MCM isn't installed.
            Mcm.McmBridge.TryBind(Config);
        }

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);
            TrainingBattleBehavior.Instance?.TickHotkey();
            UI.TrainingWindow.Tick(); // the custom windows' Escape road; cheap when none is up
        }
    }
}
