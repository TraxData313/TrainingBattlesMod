using System.Collections.Generic;
using NavalDLC.View;
using SandBox.View.Missions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.MountAndBlade.View.MissionViews.Singleplayer;

namespace TrainingBattles
{
    /// <summary>
    /// The SEA scout ride's view set: the land scout's walk-around core (escape menu, HUD,
    /// equipment, boundaries, photo mode, the hold-Tab leave bar) plus the naval essentials from
    /// War Sails' own "NavalCustomBattle" list — the SHIP CONTROL view (the helm HUD and its
    /// input, the whole point of the ride) and the ship preload view (hull resources load before
    /// the sea does, no pop-in) — and minus every battle view: no scoreboard, no order UI, no
    /// deployment views (the ride skips the phase), no kill feed, no battle music.
    ///
    /// Same scanner CAUTION as <see cref="ScoutMissionViews"/>: ViewCreatorManager reflects over
    /// EVERY static member of this class and reads its ViewMethod attribute unchecked — keep the
    /// class exactly one method. And the soft-dependency discipline of
    /// <see cref="SeaScoutMission"/>: the signature below is vanilla-only (Mission → MissionView[]),
    /// so the no-DLC scan never trips; NavalDLC.View types live in the body alone, which only a
    /// sea ride — impossible without War Sails — ever JITs.
    /// </summary>
    [ViewCreatorModule]
    public sealed class SeaScoutMissionViews
    {
        [ViewMethod(SeaScoutMission.MissionName)]
        public static MissionView[] OpenSeaScoutMission(Mission mission)
        {
            return new List<MissionView>
            {
                (MissionView)(object)new MissionCampaignView(),
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
                ViewCreator.CreatePhotoModeView(),
                // The land scout's one addition over "Camp": the hold-Tab leave bar.
                ViewCreator.CreateMissionLeaveView(),
                // The sea's own: the helm (ship control HUD + input) and the hull preloader.
                NavalViewCreator.CreateMissionShipControlView(mission),
                (MissionView)new MissionPreloadView(),
                (MissionView)(object)new NavalShipsPreloadView(),
            }.ToArray();
        }
    }
}
