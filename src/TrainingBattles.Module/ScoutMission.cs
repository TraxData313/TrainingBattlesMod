using SandBox.Missions.MissionLogics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Missions.Handlers;
using TaleWorlds.MountAndBlade.Source.Missions;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers.Logic;

namespace TrainingBattles
{
    /// <summary>
    /// The scouting ride: enter a battlefield ALONE — no battle, no enemy, no encounter, no
    /// consequence — walk or ride the ground, then leave (Tab, or the escape menu). This is how a
    /// commander learns what the scene names in the ground pickers actually mean, and where the
    /// lines would FORM: the player spawns on the defender's deployment line, facing the enemy's.
    ///
    /// Built from parts vanilla already ships: the mission opens under the name "Camp" so the
    /// campaign walk-around VIEW set attaches (camera, HUD, escape menu, photo mode — verified to
    /// carry no battle/conversation dependencies), while the BEHAVIOR list is our own minimal one,
    /// so nothing ever asks for a MapEvent or an encounter. The player rides in with their real
    /// battle equipment — horse included, so the ride is a ride.
    ///
    /// WHY THE PREVIEWED LINES ARE TRUE (verified in BattleSpawnPathSelector.FindBestInitialPath):
    /// when the mission record carries map-patch data, the deployment spawn path and both sides'
    /// ends are chosen DETERMINISTICALLY from the encounter position and direction — no dice.
    /// Without patch data (what training battles passed before this), vanilla picks a RANDOM path
    /// and pivot. So scout and drill share <see cref="CreatePatchAwareRecord"/>: same scene, same
    /// map spot, same fixed approach direction — identical lines. A REAL defence keeps the ground
    /// but the enemy's true approach direction picks the path ends, which can differ.
    /// </summary>
    internal static class ScoutMission
    {
        /// <summary>The approach direction every drill and scout preview assumes. Fixed so the two
        /// always agree; a real battle's direction comes from where the attacker actually rides in.</summary>
        internal static readonly Vec2 AssumedEncounterDirection = new Vec2(0f, 1f);

        /// <summary>The mission record both the TRAINING BATTLE and the scout ride open with —
        /// carrying the current map patch and the fixed approach direction, exactly like vanilla's
        /// real field battles (MenuHelper.EncounterAttackConsequence), so deployment is computed
        /// deterministically and the scouted lines are the drilled lines.</summary>
        public static MissionInitializerRecord CreatePatchAwareRecord(string sceneId)
        {
            var position = MobileParty.MainParty.Position;
            var wrapper = Campaign.Current.MapSceneWrapper;
            var patch = wrapper.GetMapPatchAtPosition(in position);
            var damageToFriends = Campaign.Current.Models.DifficultyModel.GetPlayerTroopsReceivedDamageMultiplier();
            return new MissionInitializerRecord(sceneId)
            {
                DamageToFriendsMultiplier = damageToFriends,
                DamageFromPlayerToFriendsMultiplier = damageToFriends,
                TerrainType = (int)wrapper.GetFaceTerrainType(MobileParty.MainParty.CurrentNavigationFace),
                NeedsRandomTerrain = false,
                PlayingInCampaignMode = true,
                RandomTerrainSeed = MBRandom.RandomInt(10000),
                AtmosphereOnCampaign = Campaign.Current.Models.MapWeatherModel.GetAtmosphereModel(position),
                SceneHasMapPatch = true,
                DecalAtlasGroup = 2,
                PatchCoordinates = patch.normalizedCoordinates,
                PatchEncounterDir = AssumedEncounterDirection,
            };
        }

        public static void Open(string sceneId)
        {
            var rec = CreatePatchAwareRecord(sceneId);
            MissionState.OpenNew("Camp", rec, _ => new MissionBehavior[]
            {
                new MissionOptionsComponent(),
                new CampaignMissionComponent(),
                new BasicLeaveMissionLogic(),
                new MissionHardBorderPlacer(),
                new MissionBoundaryPlacer(),
                new MissionBoundaryCrossingHandler(10f),
                new EquipmentControllerLeaveLogic(),
                new ScoutMissionLogic(),
            });
        }
    }

    /// <summary>Spawns the player (alone, real gear, player-controlled) and ends the mission on the
    /// Leave key — the same shape as vanilla's SimpleMountedPlayerMissionController, minus its
    /// hardcoded test horseman.</summary>
    internal sealed class ScoutMissionLogic : MissionLogic
    {
        public override void AfterStart()
        {
            base.AfterStart();
            var character = CharacterObject.PlayerCharacter;
            MatrixFrame frame;
            Vec2 direction;
            string? deploymentReport = null;
            if (TryGetDeploymentLines(out var defenderFrame, out var attackerFrame))
            {
                // The commander's preview: stand ON your line, look at theirs.
                frame = defenderFrame;
                var toEnemy = attackerFrame.origin.AsVec2 - defenderFrame.origin.AsVec2;
                direction = toEnemy.Normalized();
                deploymentReport = "You stand where YOUR line would form — the enemy's line is about "
                    + (int)toEnemy.Length + " paces ahead. A drill here forms exactly these lines; "
                    + "in a real defence the enemy's approach decides which end is theirs.";
            }
            else
            {
                frame = FindSpawnFrame();
                direction = frame.rotation.f.AsVec2.Normalized();
            }
            var agent = Mission.SpawnAgent(new AgentBuildData(character)
                .InitialPosition(in frame.origin)
                .InitialDirection(in direction)
                .Equipment(character.FirstBattleEquipment)
                .CivilianEquipment(false)
                .TroopOrigin(new SimpleAgentOrigin(character))
                .Controller(AgentControllerType.Player));
            agent.WieldInitialWeapons();
            InformationManager.DisplayMessage(new InformationMessage(
                "Scouting the ground. Ride, look, remember — and leave (Tab) when you have seen enough."));
            if (deploymentReport != null)
                InformationManager.DisplayMessage(new InformationMessage(deploymentReport));
        }

        /// <summary>Runs the game's own deployment path selection — deterministic because the scout
        /// record carries map-patch data — and returns both sides' initial line frames.</summary>
        private bool TryGetDeploymentLines(out MatrixFrame defenderFrame, out MatrixFrame attackerFrame)
        {
            defenderFrame = MatrixFrame.Identity;
            attackerFrame = MatrixFrame.Identity;
            try
            {
                var selector = new BattleSpawnPathSelector(Mission);
                selector.Initialize();
                if (!selector.IsInitialized) return false;
                if (!selector.GetInitialPathDataOfSide(BattleSideEnum.Defender, out var defender)
                    || !selector.GetInitialPathDataOfSide(BattleSideEnum.Attacker, out var attacker))
                    return false;
                defenderFrame = defender.GetSpawnFrame(0f);
                attackerFrame = attacker.GetSpawnFrame(0f);
                defenderFrame.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
                defenderFrame.origin.z = Mission.Scene.GetTerrainHeight(defenderFrame.origin.AsVec2);
                attackerFrame.origin.z = Mission.Scene.GetTerrainHeight(attackerFrame.origin.AsVec2);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public override bool MissionEnded(ref MissionResult missionResult)
        {
            // The Leave game key (Tab); the escape menu's leave goes through BasicLeaveMissionLogic.
            return Mission.InputManager.IsGameKeyPressed(4);
        }

        private MatrixFrame FindSpawnFrame()
        {
            var scene = Mission.Scene;
            try
            {
                var paths = MBSceneUtilities.GetAllSpawnPaths(scene);
                if (paths.Count > 0)
                {
                    var frame = paths[0].GetFrameForDistance(0f);
                    frame.rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
                    frame.origin.z = scene.GetTerrainHeight(frame.origin.AsVec2);
                    return frame;
                }
            }
            catch { }
            try
            {
                var points = MBSceneUtilities.GetSoftBoundaryPoints(scene);
                if (points.Count > 2)
                {
                    var center = Vec2.Zero;
                    foreach (var p in points) center += p;
                    center *= 1f / points.Count;
                    var frame = MatrixFrame.Identity;
                    frame.origin = new Vec3(center.x, center.y, scene.GetTerrainHeight(center));
                    return frame;
                }
            }
            catch { }
            var fallback = MatrixFrame.Identity;
            try
            {
                MBSceneUtilities.GetSceneLimitPoints(scene, out var min, out var max);
                var mid = (min + max) * 0.5f;
                fallback.origin = new Vec3(mid.x, mid.y, scene.GetTerrainHeight(mid));
            }
            catch { }
            return fallback;
        }
    }
}
