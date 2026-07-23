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
    /// The commander's tools on a REAL field battle's encounter menu — Anton's must-have
    /// (2026.07.23, the Luda save): before committing to the fight, SURVEY the ground (pick the
    /// battlefield from the same wider same-terrain pool the training muster offers — this
    /// supersedes the earlier strict-patch-only scope, which with this game's one-scene-per-patch
    /// data meant the option never actually appeared) and SCOUT it (ride the field alone via
    /// <see cref="ScoutMission.OpenForRealEncounter"/>). Because both armies stand frozen in the
    /// encounter, the true approach direction — vanilla's own attacker-to-defender formula, the
    /// one MenuHelper feeds the mission at "Attack!" — is already known, so the scouted lines,
    /// ends and facings are EXACTLY the coming battle's. Defender and attacker each have a toggle
    /// (<see cref="ModConfig.ChooseGroundWhenDefending"/> /
    /// <see cref="ModConfig.ChooseGroundWhenAttacking"/>, both default on). The survey pick is
    /// handed to <see cref="TrainingBattlesSceneModel"/> and eaten by the next mission start; the
    /// choice clears when the player's map event ends, however it ends, so a battle that never
    /// happens leaves nothing armed. The scout leaves the encounter untouched: campaign time is
    /// frozen inside missions and the encounter menu re-activates on return (the same
    /// mission-under-a-menu shape as vanilla's pre-battle conversation) — FIRST PLAYTEST POINT.
    /// </summary>
    public sealed class RealBattleGroundBehavior : CampaignBehaviorBase
    {
        private readonly ModConfig _config;

        public RealBattleGroundBehavior(ModConfig config)
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
            // Indices 4/5 slot the options right before vanilla's "{ATTACK_TEXT}!" — cosmetic
            // only; wherever another mod pushes them, they still work.
            // Same labels as the training muster's tools — one name per tool, everywhere
            // (the shared constants in BattleSceneCatalog are the single source of the text).
            starter.AddGameMenuOption("encounter", "training_battles_survey_ground",
                BattleSceneCatalog.SelectBattlefieldOptionText,
                SurveyGroundCondition, _ => SurveyGround(), isLeave: false, index: 4);
            starter.AddGameMenuOption("encounter", "training_battles_scout_ground",
                BattleSceneCatalog.ScoutBattlefieldOptionText,
                ScoutGroundCondition, _ => ScoutGround(), isLeave: false, index: 5);
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            // The fight this choice was made for is over (fought, auto-resolved, or walked away
            // from) — nothing may linger for a later, unrelated battle.
            if (mapEvent != null && mapEvent.IsPlayerMapEvent)
                TrainingBattlesSceneModel.PendingSceneId = null;
        }

        /// <summary>The shared gate: a real field battle, not our own drill, and the toggle for
        /// the player's side (defender or attacker) switched on.</summary>
        private bool GroundToolsAllowed(out MapEvent mapEvent)
        {
            mapEvent = MapEvent.PlayerMapEvent;
            if (mapEvent == null || !mapEvent.IsFieldBattle) return false;
            if (TrainingBattleBehavior.TrainingActive) return false;
            return mapEvent.PlayerSide == BattleSideEnum.Defender
                ? _config.ChooseGroundWhenDefending
                : _config.ChooseGroundWhenAttacking;
        }

        private bool SurveyGroundCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Manage;
            if (!GroundToolsAllowed(out var mapEvent)) return false;
            var candidates = Candidates(out _);
            if (candidates.Count < 2) return false; // nothing to choose between
            var chosen = TrainingBattlesSceneModel.PendingSceneId;
            args.Tooltip = new TextObject(chosen == null
                ? (mapEvent.PlayerSide == BattleSideEnum.Defender
                    ? "{=TB_tip_survey_def}You stand and wait — so the ground is yours to choose. See the possible battlefields and pick where your line will form."
                    : "{=TB_tip_survey_att}You bring the battle — see the possible battlefields and pick the field you drive them onto.")
                : "{=TB_tip_survey_set}Ground chosen: " + chosen + ". Select again to change it.");
            return true;
        }

        private void SurveyGround()
        {
            var candidates = Candidates(out var localCount);
            if (candidates.Count == 0) return;
            BattleSceneCatalog.ShowPicker(
                BattleSceneCatalog.SelectPickerTitle,
                "These battlefields fit the ground you stand on. Pick where your line will form.",
                candidates, localCount, TrainingBattlesSceneModel.PendingSceneId, offerFate: true, sceneId =>
            {
                TrainingBattlesSceneModel.PendingSceneId = sceneId;
                InformationManager.DisplayMessage(new InformationMessage(sceneId == null
                    ? "Training Battles: the ground is left to fate."
                    : "Training Battles: your line will form on " + sceneId + "."));
            });
        }

        private bool ScoutGroundCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Mission;
            if (!GroundToolsAllowed(out _)) return false;
            if (PlayerEncounter.IsNavalEncounter()) return false; // no lone ride on open water
            if (Candidates(out _).Count == 0) return false;
            args.Tooltip = new TextObject(
                "{=TB_tip_scout_real}Ride out alone and walk the field before you commit. The armies stand where they stand, so the lines and facings you see are the coming battle's true ones. No time passes; the fight waits for your return.");
            return true;
        }

        private void ScoutGround()
        {
            if (!GroundToolsAllowed(out var mapEvent)) return;
            var candidates = Candidates(out var localCount);
            if (candidates.Count == 0) return;
            var playerSide = mapEvent.PlayerSide;
            var direction = RealEncounterDirection(mapEvent);
            if (candidates.Count == 1)
            {
                LaunchScout(candidates[0].SceneID, playerSide, direction);
                return;
            }
            BattleSceneCatalog.ShowPicker(
                BattleSceneCatalog.ScoutPickerTitle,
                "Pick a battlefield and ride out alone — the fight waits. The first entries are the "
                + "ground you truly stand on; select the battlefield to make another field the real one.",
                candidates, localCount, TrainingBattlesSceneModel.PendingSceneId, offerFate: false, sceneId =>
            {
                if (sceneId != null) LaunchScout(sceneId, playerSide, direction);
            });
        }

        private static void LaunchScout(string sceneId, BattleSideEnum playerSide, Vec2 direction)
        {
            try { ScoutMission.OpenForRealEncounter(sceneId, playerSide, direction); }
            catch (System.Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Training Battles: could not ride out (" + ex.Message + ")."));
            }
        }

        /// <summary>The true approach of THIS battle — vanilla's own formula (MenuHelper.
        /// EncounterAttackConsequence): attacker leader towards defender leader, read while both
        /// stand frozen in the encounter, so it equals what "Attack!" will feed the mission.</summary>
        private static Vec2 RealEncounterDirection(MapEvent mapEvent)
        {
            try
            {
                var direction = (mapEvent.AttackerSide.LeaderParty.Position.ToVec2()
                    - mapEvent.DefenderSide.LeaderParty.Position.ToVec2()).Normalized();
                if (direction.IsValid && direction.LengthSquared > 0.5f) return direction;
            }
            catch { }
            return ScoutMission.AssumedEncounterDirection;
        }

        private static List<SingleplayerBattleSceneData> Candidates(out int localCount)
        {
            try
            {
                // The same wider same-terrain pool the training muster offers, the true local
                // ground marked first — strict patch alone is one scene in this game's data,
                // which is no choice at all.
                return BattleSceneCatalog.WiderPoolAt(
                    MobileParty.MainParty.Position, PlayerEncounter.IsNavalEncounter(), out localCount);
            }
            catch
            {
                localCount = 0;
                return new List<SingleplayerBattleSceneData>();
            }
        }
    }
}
