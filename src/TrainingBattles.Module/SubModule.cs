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
        internal static ModConfig Config => _config ??= ModConfig.LoadOrCreate();

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);
            if (gameStarterObject is CampaignGameStarter starter)
            {
                var config = Config;
                // If MCM was not yet ready when the main menu came up, bind it now (no-op once bound).
                Mcm.McmBridge.TryBind(config);
                starter.AddBehavior(new TrainingBattleBehavior(config));
                // The "it was only training" guard: zero renown/loot/prisoners while a drill runs.
                starter.AddModel(new Models.TrainingBattleRewardModel());
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
        }
    }
}
