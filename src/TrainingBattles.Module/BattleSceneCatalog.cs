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

        /// <summary>The one ground-choice dialog: the candidates plus a "let fate pick" entry.
        /// <paramref name="onDecided"/> receives the chosen SceneID, null for fate, and is not
        /// called at all on Cancel.</summary>
        public static void ShowPicker(List<SingleplayerBattleSceneData> candidates, string? currentChoice, Action<string?> onDecided)
        {
            var elements = new List<InquiryElement>
            {
                new InquiryElement(null,
                    currentChoice == null ? "As fate wills (random) — current" : "As fate wills (random)",
                    null, true, "Let the game pick among the variants, as it normally would."),
            };
            foreach (var scene in candidates)
            {
                var title = Describe(scene);
                if (scene.SceneID == currentChoice) title += " — current";
                elements.Add(new InquiryElement(scene.SceneID, title, null, true, scene.SceneID));
            }
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                "Choose the ground",
                "These battlefields fit the ground you stand on. Pick where the lines will form.",
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
