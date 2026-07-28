using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements;
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
        /// it arms the ONE-BATTLE hour pick every battle type reads (the standing default is
        /// MCM's alone; see ShowTimeOfDayPicker).</summary>
        public const string ChooseTimeOfDayOptionText = "{=TB_opt_time}Choose the time of day";

        /// <summary>The shared picker-dialog titles, one per tool, same everywhere.</summary>
        public const string SelectPickerTitle = "Choose the ground";
        public const string ScoutPickerTitle = "Scout the ground";

        /// <summary>The scene-LEVEL a village battlefield loads under — vanilla's own
        /// (PlayerEncounter.StartVillageBattleMission passes it): the village scene's combat
        /// variant, with the battle spawn paths. A village scene loaded without it is the
        /// peaceful walk-around level.</summary>
        public const string VillageBattleSceneLevels = "land_raid";

        /// <summary>THE GROUND A REAL FIELD BATTLE WILL TRULY BE FOUGHT ON when a village is near
        /// (researched 2026.07.28 — Anton's "Basil Near Village Looter Atk" save). Vanilla's
        /// <c>MapEvent.Initialize</c> hangs the nearest village within
        /// <c>EncounterModel.GetSettlementBeingNearFieldBattleRadius</c> (3.0 map units) on ANY
        /// land field battle the player is in — the event stays a FieldBattle, only
        /// <c>MapEventSettlement</c> changes — and <c>MenuHelper.EncounterAttackConsequence</c>
        /// then forks to <c>PlayerEncounter.StartVillageBattleMission</c>, which opens the
        /// VILLAGE's own scene and never asks <c>SceneModel.GetBattleSceneForMapPatch</c>. So on
        /// these fights our survey pick can change nothing and the field our scout used to ride
        /// was never the battlefield. Returns the village's scene id, or null when this battle
        /// really is fought on the open map patch.</summary>
        public static string? VillageBattlegroundFor(MapEvent? mapEvent, out Settlement? village)
        {
            village = null;
            try
            {
                if (mapEvent == null || !mapEvent.IsFieldBattle) return null;
                var settlement = mapEvent.MapEventSettlement;
                if (settlement == null || !settlement.IsVillage) return null;
                // A sea-borne raider takes the ship road instead (MenuHelper's own fork); the
                // lone scout never rides open water anyway.
                if (PlayerEncounter.IsNavalEncounter()) return null;
                var scene = settlement.LocationComplex?.GetScene("village_center", 1);
                if (string.IsNullOrEmpty(scene)) return null;
                village = settlement;
                return scene;
            }
            catch { return null; }
        }

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
        /// game version — vanilla's "random among several" is legacy), then the widening tiers.
        /// <paramref name="localCount"/> says how many entries at the front are the ground the
        /// player actually stands on.</summary>
        public static List<SingleplayerBattleSceneData> WiderPoolAt(CampaignVec2 position, bool isNaval, out int localCount)
            => WiderPoolAt(position, isNaval, out localCount, out _);

        /// <summary>The full pool in TIERS, nearest ground first (the 2026.07.27 fix — Anton
        /// found no "Select the battlefield" and a scout ride that skipped its picker while
        /// attacking bandits in the woods; both are the same starved pool):
        /// <list type="number">
        /// <item>the scenes the map patch itself claims — <paramref name="localCount"/>;</item>
        /// <item>every scene of the local terrain TYPE (vanilla's own fallback);</item>
        /// <item>every scene that LISTS this terrain among its features (the data's
        /// &lt;TerrainTypes&gt; block — a Plain field with a mountain in it);</item>
        /// <item>the KIN terrains (see <see cref="KinTerrains"/>);</item>
        /// <item>last resort, the whole board.</item>
        /// </list>
        /// Tiers 1-3 are the country the player truly stands in — <paramref name="nativeCount"/>
        /// marks where that ends so the picker can say so. Tiers 4-5 exist because THE SHIPPED
        /// DATA HAS NO BATTLEFIELD FOR MOST TERRAINS: every land scene in the game is tagged
        /// Plain, Desert, Steppe or Swamp, so a party standing on Forest, Mountain, Snow, Canyon,
        /// Bridge, RuralArea or Beach matched nothing at all and the pool collapsed to the one
        /// patch scene — which hid the ground choice (it needs two) and made the scout ride
        /// launch without ever asking.</summary>
        public static List<SingleplayerBattleSceneData> WiderPoolAt(CampaignVec2 position, bool isNaval,
            out int localCount, out int nativeCount)
        {
            var result = CandidatesAt(position, isNaval);
            localCount = nativeCount = result.Count;
            try
            {
                var scenes = GameSceneDataManager.Instance?.SingleplayerBattleScenes;
                var wrapper = Campaign.Current?.MapSceneWrapper;
                if (scenes == null || wrapper == null) return result;
                wrapper.GetEnvironmentTerrainTypesCount(in position, out var terrain);
                Absorb(result, scenes, isNaval, scene => scene.Terrain == terrain);
                Absorb(result, scenes, isNaval, scene => Features(scene).Contains(terrain));
                nativeCount = result.Count;
                foreach (var kin in KinTerrains(terrain))
                {
                    var from = result.Count;
                    Absorb(result, scenes, isNaval,
                        scene => scene.Terrain == kin || Features(scene).Contains(kin));
                    // Standing in the woods, the thickest fields come first — the closest thing
                    // the data has to the ground under your feet.
                    if (terrain == TerrainType.Forest)
                        result.Sort(from, result.Count - from,
                            Comparer<SingleplayerBattleSceneData>.Create(
                                (a, b) => b.ForestDensity.CompareTo(a.ForestDensity)));
                }
                // Nothing kin either (a terrain we never foresaw): the whole board beats no choice.
                if (result.Count < 2) Absorb(result, scenes, isNaval, _ => true);
                LogPoolOnce(terrain, isNaval, localCount, nativeCount, result.Count);
            }
            catch { }
            return result;
        }

        private static List<TerrainType> Features(SingleplayerBattleSceneData scene)
            => scene.TerrainTypes ?? _noFeatures;

        private static readonly List<TerrainType> _noFeatures = new List<TerrainType>();

        /// <summary>Appends every scene of the right naval-ness that passes <paramref name="wanted"/>
        /// and is not already in the pool — the tiers stay in order, no scene twice.</summary>
        private static void Absorb(List<SingleplayerBattleSceneData> pool,
            IEnumerable<SingleplayerBattleSceneData> scenes, bool isNaval,
            Func<SingleplayerBattleSceneData, bool> wanted)
        {
            foreach (var scene in scenes)
            {
                if (scene.IsNaval != isNaval || !wanted(scene)) continue;
                var known = false;
                foreach (var have in pool)
                    if (have.SceneID == scene.SceneID) { known = true; break; }
                if (!known) pool.Add(scene);
            }
        }

        /// <summary>What a terrain may BORROW when the shipped data gives it no battlefield of its
        /// own — ordered nearest-looking first. Land and sea share one table; the naval-ness filter
        /// keeps a sea pool from ever taking a field (and the reverse).</summary>
        private static TerrainType[] KinTerrains(TerrainType terrain)
        {
            switch (terrain)
            {
                case TerrainType.Forest:
                    return new[] { TerrainType.Plain, TerrainType.Swamp, TerrainType.Steppe };
                case TerrainType.Snow:
                    return new[] { TerrainType.Plain, TerrainType.Steppe };
                case TerrainType.Mountain:
                    return new[] { TerrainType.Steppe, TerrainType.Plain, TerrainType.Desert };
                case TerrainType.Canyon:
                    return new[] { TerrainType.Desert, TerrainType.Steppe };
                case TerrainType.Dune:
                    return new[] { TerrainType.Desert, TerrainType.Steppe };
                case TerrainType.RuralArea:
                case TerrainType.Beach:
                case TerrainType.Cliff:
                    return new[] { TerrainType.Plain, TerrainType.Steppe };
                case TerrainType.Bridge:
                case TerrainType.UnderBridge:
                case TerrainType.Fording:
                case TerrainType.River:
                case TerrainType.NonNavigableRiver:
                    return new[] { TerrainType.River, TerrainType.UnderBridge, TerrainType.Plain, TerrainType.Swamp };
                case TerrainType.Lake:
                    return new[] { TerrainType.River, TerrainType.CoastalSea, TerrainType.Plain };
                case TerrainType.Water:
                    return new[] { TerrainType.CoastalSea, TerrainType.OpenSea, TerrainType.River };
                case TerrainType.CoastalSea:
                    return new[] { TerrainType.OpenSea, TerrainType.River };
                case TerrainType.OpenSea:
                    return new[] { TerrainType.CoastalSea };
                default:
                    return new[] { TerrainType.Plain, TerrainType.Steppe, TerrainType.Desert, TerrainType.Swamp };
            }
        }

        /// <summary>Menu conditions call the pool every frame — one line per changed answer, so
        /// the next "the option isn't there" report is one grep away.</summary>
        private static string? _lastPoolLogged;

        private static void LogPoolOnce(TerrainType terrain, bool isNaval, int localCount, int nativeCount, int total)
        {
            var line = (isNaval ? "sea" : "land") + " | terrain " + terrain
                + " | patch " + localCount + " | native " + nativeCount + " | pool " + total;
            if (line == _lastPoolLogged) return;
            _lastPoolLogged = line;
            TbLog.Info("ground", "scene pool: " + line);
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
        /// marked as the ground the player stands on, and everything from
        /// <paramref name="nativeCount"/> on is marked as borrowed from another country (see
        /// <see cref="WiderPoolAt(CampaignVec2,bool,out int,out int)"/> — most terrains own no
        /// battlefield at all in the shipped data, so the far tiers are the choice);
        /// <paramref name="offerFate"/> adds the "let the game pick" entry.
        /// <paramref name="onDecided"/> receives the chosen SceneID (null for fate) and is not
        /// called at all on Cancel.</summary>
        public static void ShowPicker(string title, string description,
            List<SingleplayerBattleSceneData> candidates, int localCount, int nativeCount,
            string? currentChoice, bool offerFate, Action<string?> onDecided)
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
                else if (i >= nativeCount) label += " — another country";
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
        /// of day". The pick is for ONE battle only (Anton, 2026.07.25 — a menu pick must never
        /// rewrite the standing default, which lives in MCM alone): it arms
        /// <see cref="Models.TrainingBattlesMapWeatherModel.PendingBattleHour"/>, which every
        /// battle door reads through EffectiveBattleHour and which clears when the player's map
        /// event ends. <paramref name="onDecided"/> then refreshes whichever menu asked; Cancel
        /// changes nothing.
        /// <paramref name="hourLock"/> (optional) gates single hours: given an hour it returns
        /// null to allow or the reason to show it LOCKED — the real-battle door uses it for the
        /// scouting duel (the campaign clock and full daylight stay free; see
        /// <see cref="ModConfig.ScoutingGateEnabled"/>). The muster passes nothing.</summary>
        public static void ShowTimeOfDayPicker(ModConfig config, Action onDecided, Func<int, string?>? hourLock = null)
        {
            // "— your pick" marks the hour the NEXT battle will actually use; the true clock is
            // spelled out on the clock entry (Anton read the old "— current" as "the current
            // time of day" and filed the pinned-noon sky as a sync bug, 2026.07.24).
            var effective = Models.TrainingBattlesMapWeatherModel.EffectiveBattleHour(config);
            var nowHour = CampaignTime.Now.GetHourOfDay;
            var elements = new List<InquiryElement>
            {
                new InquiryElement(-1,
                    Models.AtmospherePresets.Label(-1) + " (now " + nowHour.ToString("00") + ":00)"
                        + (effective < 0 ? " — your pick" : ""),
                    null, true, "Battles are fought whenever they happen — vanilla's honest clock."),
            };
            foreach (var hour in ModConfig.SupportedBattleHours)
            {
                var lockReason = hourLock?.Invoke(hour);
                elements.Add(new InquiryElement(hour,
                    Models.AtmospherePresets.Label(hour) + (effective == hour ? " — your pick" : ""),
                    null, lockReason == null,
                    lockReason ?? "The NEXT battle — drill, field, siege or sea — opens at this hour."));
            }
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                "Choose the time of day",
                "Pick the hour for the NEXT battle — an immersion trade for a field you can "
                + "actually see. The pick lasts one battle; the standing default lives in the "
                + "mod options (MCM). \"" + Models.AtmospherePresets.Label(-1) + "\" is the true clock.",
                elements, isExitShown: true, 1, 1, "Choose", "Cancel",
                picked =>
                {
                    if (picked == null || picked.Count == 0 || picked[0].Identifier is not int hour) return;
                    Models.TrainingBattlesMapWeatherModel.PendingBattleHour = hour;
                    TbLog.Info("hour", "next battle's hour picked: "
                        + (hour < 0 ? "campaign clock" : hour.ToString("00") + ":00")
                        + " (one battle; standing default "
                        + (config.BattleTimeOfDay < 0 ? "clock" : config.BattleTimeOfDay.ToString("00") + ":00") + ")");
                    onDecided();
                },
                _ => { }), pauseGameActiveState: true);
        }
    }
}
