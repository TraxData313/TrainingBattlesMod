using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace TrainingBattles
{
    /// <summary>
    /// The battlefield variants the game itself would consider for a map position — the same
    /// candidate chain vanilla's DefaultSceneModel walks (scenes registered for the exact map
    /// patch first, then any scene of the local terrain type) before picking one AT RANDOM.
    /// Whatever this returns is exactly the pool the player may choose from; zero or one
    /// candidates means there is nothing to choose and the pickers stay hidden. Serves both the
    /// training muster and the defend-a-real-battle ground choice, and the shared selection
    /// inquiry lives here too so the two doors look identical.
    /// </summary>
    internal static class BattleSceneCatalog
    {
        public static List<SingleplayerBattleSceneData> CandidatesAt(CampaignVec2 position, bool isNaval)
        {
            var result = new List<SingleplayerBattleSceneData>();
            try
            {
                var scenes = GameSceneDataManager.Instance?.SingleplayerBattleScenes;
                var wrapper = Campaign.Current?.MapSceneWrapper;
                if (scenes == null || wrapper == null) return result;
                var patch = wrapper.GetMapPatchAtPosition(in position);
                foreach (var scene in scenes)
                {
                    if (scene.IsNaval == isNaval && scene.MapIndices != null
                        && scene.MapIndices.Contains(patch.sceneIndex))
                        result.Add(scene);
                }
                if (result.Count == 0)
                {
                    // No scene claims this patch — vanilla falls back to the local terrain type.
                    wrapper.GetEnvironmentTerrainTypesCount(in position, out var terrain);
                    foreach (var scene in scenes)
                    {
                        if (scene.IsNaval == isNaval && scene.Terrain == terrain)
                            result.Add(scene);
                    }
                }
            }
            catch { /* an unreadable catalog only hides the choice, never breaks a battle */ }
            return result;
        }

        /// <summary>The scene the patch truly owns first (Anton's playtest + the shipped
        /// sp_battle_scenes.xml showed each map patch is claimed by AT MOST ONE land scene in this
        /// game version — vanilla's "random among several" is legacy), then every other scene of
        /// the local terrain type. <paramref name="localCount"/> says how many entries at the front
        /// are the ground the player actually stands on.</summary>
        public static List<SingleplayerBattleSceneData> WiderPoolAt(CampaignVec2 position, bool isNaval, out int localCount)
        {
            var result = CandidatesAt(position, isNaval);
            localCount = result.Count;
            try
            {
                var scenes = GameSceneDataManager.Instance?.SingleplayerBattleScenes;
                var wrapper = Campaign.Current?.MapSceneWrapper;
                if (scenes != null && wrapper != null)
                {
                    wrapper.GetEnvironmentTerrainTypesCount(in position, out var terrain);
                    foreach (var scene in scenes)
                    {
                        if (scene.IsNaval != isNaval || scene.Terrain != terrain) continue;
                        var known = false;
                        foreach (var have in result)
                            if (have.SceneID == scene.SceneID) { known = true; break; }
                        if (!known) result.Add(scene);
                    }
                }
            }
            catch { }
            return result;
        }

        public static string Describe(SingleplayerBattleSceneData scene)
        {
            string ground;
            switch (scene.ForestDensity)
            {
                case ForestDensity.Low: ground = "scattered trees"; break;
                case ForestDensity.High: ground = "dense forest"; break;
                default: ground = "open ground"; break;
            }
            return scene.Terrain + ", " + ground + "  (" + scene.SceneID + ")";
        }

        /// <summary>The one ground-choice dialog, shared by every door (training survey, scout
        /// ride, real-defense choice). The first <paramref name="localCount"/> candidates are
        /// marked as the ground the player stands on; <paramref name="offerFate"/> adds the
        /// "let the game pick" entry. <paramref name="onDecided"/> receives the chosen SceneID
        /// (null for fate) and is not called at all on Cancel.</summary>
        public static void ShowPicker(string title, string description,
            List<SingleplayerBattleSceneData> candidates, int localCount, string? currentChoice,
            bool offerFate, Action<string?> onDecided)
        {
            var elements = new List<InquiryElement>();
            if (offerFate)
            {
                elements.Add(new InquiryElement(null,
                    currentChoice == null ? "As fate wills (random) — current" : "As fate wills (random)",
                    null, true, "Let the game pick, as it normally would."));
            }
            for (var i = 0; i < candidates.Count; i++)
            {
                var scene = candidates[i];
                var label = Describe(scene);
                if (i < localCount) label += " — this ground";
                if (scene.SceneID == currentChoice) label += " — current";
                elements.Add(new InquiryElement(scene.SceneID, label, null, true, scene.SceneID));
            }
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                title, description,
                elements, isExitShown: true, 1, 1, "Choose", "Cancel",
                picked =>
                {
                    if (picked != null && picked.Count > 0)
                        onDecided(picked[0].Identifier as string);
                },
                _ => { }), pauseGameActiveState: true);
        }
    }
}
