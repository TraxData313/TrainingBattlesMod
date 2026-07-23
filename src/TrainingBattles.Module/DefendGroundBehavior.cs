using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TrainingBattles.Models;

namespace TrainingBattles
{
    /// <summary>
    /// "You chose where to stand — so choose the ground." When the player DEFENDS a real field
    /// battle, the vanilla encounter menu gains a "Survey the ground" option listing the
    /// battlefield variants the game would otherwise pick from at random; the pick is handed to
    /// <see cref="TrainingBattlesSceneModel"/> and eaten by the next mission start. Attackers
    /// keep vanilla behavior — the attacker comes to the defender's ground, not the other way
    /// around. The choice clears when the player's map event ends, however it ends, so a battle
    /// that never happens (got away, auto-resolved) leaves nothing armed. Toggle:
    /// <see cref="ModConfig.ChooseGroundWhenDefending"/>.
    /// </summary>
    public sealed class DefendGroundBehavior : CampaignBehaviorBase
    {
        private readonly ModConfig _config;

        public DefendGroundBehavior(ModConfig config)
        {
            _config = config;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            TrainingBattlesSceneModel.PendingSceneId = null;
            // Index 4 slots the option right before vanilla's "{ATTACK_TEXT}!" — cosmetic only;
            // wherever another mod pushes it, it still works.
            starter.AddGameMenuOption("encounter", "training_battles_defend_ground",
                "{=TB_opt_defend_ground}Survey the ground",
                ChooseGroundCondition, _ => ChooseGround(), isLeave: false, index: 4);
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            // The fight this choice was made for is over (fought, auto-resolved, or walked away
            // from) — nothing may linger for a later, unrelated battle.
            if (mapEvent != null && mapEvent.IsPlayerMapEvent)
                TrainingBattlesSceneModel.PendingSceneId = null;
        }

        private bool ChooseGroundCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Manage;
            if (!_config.ChooseGroundWhenDefending || TrainingBattleBehavior.TrainingActive) return false;
            var mapEvent = MapEvent.PlayerMapEvent;
            if (mapEvent == null || !mapEvent.IsFieldBattle) return false;
            if (mapEvent.PlayerSide != BattleSideEnum.Defender) return false;
            var candidates = Candidates();
            if (candidates.Count < 2) return false; // nothing to choose between
            var chosen = TrainingBattlesSceneModel.PendingSceneId;
            args.Tooltip = new TextObject(chosen == null
                ? "{=TB_tip_defend_ground}You stand and wait — so the ground is yours to choose. See the possible battlefields and pick where your line will form."
                : "{=TB_tip_defend_ground_set}Ground chosen: " + chosen + ". Survey again to change it.");
            return true;
        }

        private void ChooseGround()
        {
            var candidates = Candidates();
            if (candidates.Count == 0) return;
            BattleSceneCatalog.ShowPicker(candidates, TrainingBattlesSceneModel.PendingSceneId, sceneId =>
            {
                TrainingBattlesSceneModel.PendingSceneId = sceneId;
                InformationManager.DisplayMessage(new InformationMessage(sceneId == null
                    ? "Training Battles: the ground is left to fate."
                    : "Training Battles: your line will form on " + sceneId + "."));
            });
        }

        private static List<SingleplayerBattleSceneData> Candidates()
        {
            try
            {
                return BattleSceneCatalog.CandidatesAt(
                    MobileParty.MainParty.Position, PlayerEncounter.IsNavalEncounter());
            }
            catch
            {
                return new List<SingleplayerBattleSceneData>();
            }
        }
    }
}
