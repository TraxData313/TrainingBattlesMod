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
        /// <summary>The ONE name each ground tool goes by, on every door that offers it (the
        /// training muster and the real-battle encounter menu) — same label, same localization
        /// key, so the player never wonders whether two names are two features (Anton's call,
        /// 2026.07.23). Change the text here and every menu follows.</summary>
        public const string SelectBattlefieldOptionText = "{=TB_opt_ground2}Select the battlefield";

        /// <summary>See <see cref="SelectBattlefieldOptionText"/> — the scout ride's one name.</summary>
        public const string ScoutBattlefieldOptionText = "{=TB_opt_scout}Ride out and scout a battlefield";

        /// <summary>The battle-hour option's one name (muster menu and encounter menu alike) —
        /// it edits the single ModConfig.BattleTimeOfDay every battle type reads.</summary>
        public const string ChooseTimeOfDayOptionText = "{=TB_opt_time}Choose the time of day";

        /// <summary>The shared picker-dialog titles, one per tool, same everywhere.</summary>
        public const string SelectPickerTitle = "Choose the ground";
        public const string ScoutPickerTitle = "Scout the ground";

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

        /// <summary>The one battle-hour dialog, shared by every door that offers "Choose the time
        /// of day". Writes the pick straight into <see cref="ModConfig.BattleTimeOfDay"/> (the
        /// single key drills, scouts and every real battle read) and saves; <paramref name="onDecided"/>
        /// then refreshes whichever menu asked. Cancel changes nothing.
        /// <paramref name="hourLock"/> (optional) gates single hours: given an hour it returns
        /// null to allow or the reason to show it LOCKED — the real-battle door uses it for the
        /// scouting duel (the campaign clock and full daylight stay free; see
        /// <see cref="ModConfig.ScoutingGateEnabled"/>). The muster passes nothing.</summary>
        public static void ShowTimeOfDayPicker(ModConfig config, Action onDecided, Func<int, string?>? hourLock = null)
        {
            // "— your pick" marks the STANDING SETTING, never the hour outside: Anton read the
            // old "— current" as "the current time of day" and filed the pinned-noon sky as a
            // sync bug (2026.07.24) — so the true clock is now spelled out on the clock entry.
            var nowHour = CampaignTime.Now.GetHourOfDay;
            var elements = new List<InquiryElement>
            {
                new InquiryElement(-1,
                    Models.AtmospherePresets.Label(-1) + " (now " + nowHour.ToString("00") + ":00)"
                        + (config.BattleTimeOfDay < 0 ? " — your pick" : ""),
                    null, true, "Battles are fought whenever they happen — vanilla's honest clock."),
            };
            foreach (var hour in ModConfig.SupportedBattleHours)
            {
                var lockReason = hourLock?.Invoke(hour);
                elements.Add(new InquiryElement(hour,
                    Models.AtmospherePresets.Label(hour) + (config.BattleTimeOfDay == hour ? " — your pick" : ""),
                    null, lockReason == null,
                    lockReason ?? "Every battle — drills, field battles, sieges, sea — opens at this hour."));
            }
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                "Choose the time of day",
                "Pin the hour every battle is fought at — an immersion trade for a field you can "
                + "actually see. The pick is a standing setting: it holds for every later battle "
                + "until changed. \"" + Models.AtmospherePresets.Label(-1) + "\" returns to the true clock.",
                elements, isExitShown: true, 1, 1, "Choose", "Cancel",
                picked =>
                {
                    if (picked == null || picked.Count == 0 || picked[0].Identifier is not int hour) return;
                    config.BattleTimeOfDay = hour;
                    config.Save();
                    TbLog.Info("hour", "battle hour pinned: "
                        + (hour < 0 ? "campaign clock" : hour.ToString("00") + ":00"));
                    Mcm.McmBridge.TryPushBattleTimeOfDay(config); // the MCM menu shows the same truth
                    onDecided();
                },
                _ => { }), pauseGameActiveState: true);
        }
    }
}
