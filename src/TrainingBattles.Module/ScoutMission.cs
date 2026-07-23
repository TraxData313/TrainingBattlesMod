using SandBox;
using SandBox.Missions.MissionLogics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
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
    /// commander learns what the scene names in the ground pickers actually mean.
    ///
    /// Built from parts vanilla already ships: the mission opens under the name "Camp" so the
    /// campaign walk-around VIEW set attaches (camera, HUD, escape menu, photo mode — verified to
    /// carry no battle/conversation dependencies), while the BEHAVIOR list is our own minimal one,
    /// so nothing ever asks for a MapEvent or an encounter. The player spawns at the scene's first
    /// spawn path (every battle terrain has them; boundary-center and scene-center fallbacks
    /// otherwise) with their real battle equipment — horse included, so the ride is a ride.
    /// </summary>
    internal static class ScoutMission
    {
        public static void Open(string sceneId)
        {
            var rec = SandBoxMissions.CreateSandBoxMissionInitializerRecord(
                sceneId, "", doNotUseLoadingScreen: false, DecalAtlasGroup.Battle);
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
            var frame = FindSpawnFrame();
            var direction = frame.rotation.f.AsVec2.Normalized();
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
