using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Map;

namespace TrainingBattles.Models
{
    /// <summary>
    /// The ground-choice gate. When the player has picked a battlefield, the next scene query
    /// gets that answer; every other call falls through to whatever SceneModel was registered
    /// before us — vanilla's, or another mod's: the game's own AddModel hands us the previous
    /// model as <c>BaseModel</c>, so we decorate the chain instead of replacing it. The choice
    /// is consumed on read and additionally cleared when the player's map event ends (see
    /// <see cref="RealBattleGroundBehavior"/>), so it can never leak into an unrelated battle.
    /// </summary>
    public sealed class TrainingBattlesSceneModel : SceneModel
    {
        /// <summary>The scene the player chose for the imminent battle; null = let vanilla pick.
        /// One-shot: the first scene query eats it.</summary>
        internal static string? PendingSceneId;

        private SceneModel? _fallback;

        // BaseModel is null only if nothing registered a SceneModel before us — should never
        // happen (vanilla always does), but a battle must open regardless.
        private SceneModel Chain => BaseModel ?? (_fallback ??= new DefaultSceneModel());

        public override string GetBattleSceneForMapPatch(MapPatchData mapPatch, bool isNavalEncounter)
        {
            var chosen = PendingSceneId;
            if (!string.IsNullOrEmpty(chosen))
            {
                PendingSceneId = null;
                return chosen!;
            }
            return Chain.GetBattleSceneForMapPatch(mapPatch, isNavalEncounter);
        }

        public override string GetConversationSceneForMapPosition(CampaignVec2 campaignPosition)
        {
            return Chain.GetConversationSceneForMapPosition(campaignPosition);
        }
    }
}
