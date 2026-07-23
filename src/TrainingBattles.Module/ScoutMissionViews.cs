using System.Collections.Generic;
using SandBox.View.Missions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace TrainingBattles
{
    /// <summary>
    /// The scout ride's view set. It IS vanilla's "Camp" walk-around set (camera, HUD, escape menu,
    /// equipment, boundaries, photo mode — the set the scout borrowed since day one), plus the ONE
    /// view "Camp" lacks: <c>MissionLeaveView</c>, the hold-Tab "leaving area" bar. Without it the
    /// hold-to-leave WORKED (the timer lives in <c>Mission</c> itself) but drew nothing — Anton
    /// held Tab into a silent screen.
    ///
    /// The game discovers this class by itself: <c>ViewCreatorManager</c> scans every active module
    /// assembly that references TaleWorlds.MountAndBlade.View for <c>[ViewCreatorModule]</c> types
    /// and matches <c>[ViewMethod(name)]</c> to the name passed to <c>MissionState.OpenNew</c>.
    ///
    /// CAUTION: the scanner reflects over EVERY static method of this class and reads its
    /// ViewMethod attribute WITHOUT a null check — any static member without the attribute
    /// (including a property getter or a lambda cached in a static field) would crash the scan.
    /// Keep this class exactly one method.
    /// </summary>
    [ViewCreatorModule]
    public sealed class ScoutMissionViews
    {
        [ViewMethod(ScoutMission.MissionName)]
        public static MissionView[] OpenScoutMission(Mission mission)
        {
            return new List<MissionView>
            {
                (MissionView)(object)new MissionCampaignView(),
                (MissionView)(object)new MissionConversationCameraView(),
                ViewCreator.CreateMissionSingleplayerEscapeMenu(CampaignOptions.IsIronmanMode),
                ViewCreator.CreateOptionsUIHandler(),
                ViewCreator.CreateMissionMainAgentEquipDropView(mission),
                (MissionView)(object)new MissionSingleplayerViewHandler(),
                ViewCreator.CreateMissionAgentStatusUIHandler(mission),
                ViewCreator.CreateMissionMainAgentEquipmentController(mission),
                ViewCreator.CreateMissionMainAgentCheerBarkControllerView(mission),
                ViewCreator.CreateMissionAgentLockVisualizerView(mission),
                ViewCreator.CreateMissionBoundaryCrossingView(),
                (MissionView)new MissionBoundaryWallView(),
                (MissionView)(object)new MissionCampaignBattleSpectatorView(),
                ViewCreator.CreatePhotoModeView(),
                // The one addition over "Camp": the hold-Tab leave bar.
                ViewCreator.CreateMissionLeaveView(),
            }.ToArray();
        }
    }
}
