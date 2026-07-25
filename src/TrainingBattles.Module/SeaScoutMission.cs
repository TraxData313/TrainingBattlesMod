using System;
using System.Collections.Generic;
using NavalDLC;
using NavalDLC.Missions;
using NavalDLC.Missions.Deployment;
using NavalDLC.Missions.Handlers;
using NavalDLC.Missions.MissionLogics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using SandBox.Missions.MissionLogics;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Missions.Handlers;
using TaleWorlds.MountAndBlade.Source.Missions;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers;
using TaleWorlds.MountAndBlade.Source.Missions.Handlers.Logic;

namespace TrainingBattles
{
    /// <summary>
    /// The SEA scout ride: take the FLAGSHIP out alone — no battle, no enemy, no encounter, no
    /// cost, no XP (verified: vanilla's only passive sailing XP is a campaign-map tick, and
    /// campaign time is frozen inside missions — this ride is truly free). The player and as much
    /// of the company as the flagship holds spawn aboard on open water; sail, row, look at the
    /// sea-lane the pickers name, then hold Tab to leave.
    ///
    /// THE SHAPE (found 2026.07.25 in NavalDLC.CustomBattle's decompile — the naval CUSTOM BATTLE
    /// proves the whole ship stack runs WITHOUT a MapEvent): the custom battle's own recipe
    /// (NavalMissionState.OpenNew + CustomBattleTroopSupplier + plain IShipOrigin lists) minus
    /// every battle part — no battle-end logic, no ship-retreat, no collision outcomes, no order
    /// UI, an EMPTY attacker side (teams exist, nothing spawns; every empty-side road verified:
    /// InitializeShipAssignments returns early, DeployBattleSide finds no framed plans,
    /// Team.ResetTactic self-guards on HasTeamAi) and MissionTeamAITypeEnum.NoTeamAI so no naval
    /// tactics ever query the empty sea.
    ///
    /// The deployment PHASE is skipped: <see cref="SeaScoutRideLogic"/> hooks the controller's
    /// OnAfterSetupTeams and calls FinishDeployment on the spot (guarded by IsDeploymentFinished —
    /// with a deck's worth of men the BattleInitializationModel usually auto-finishes first), so
    /// the ride opens under sail, anchors up.
    ///
    /// SOFT DEPENDENCY (the McmBridge discipline, applied to War Sails): NavalDLC types live ONLY
    /// in method bodies here and in <see cref="SeaScoutMissionViews"/> — never in a base class,
    /// interface, field type, or method signature — so assembly scans (view creators, savegame,
    /// MCM) succeed without the DLC, and these bodies can only run when the party is at sea,
    /// which without War Sails is impossible.
    ///
    /// HULL SAFETY: MissionShip proxies its campaign Ship LIVE (HitPoints => ShipOrigin.HitPoints,
    /// damage flows through ShipOrigin.OnShipDamaged) — a scraped rock would scar the real hull.
    /// So the ride snapshots the flagship's health on the way out and heals it home on EVERY
    /// mission end, the drill's own RestoreFleet pledge in miniature.
    /// </summary>
    internal static class SeaScoutMission
    {
        /// <summary>The mission (and view-set) name. The matching naval view list lives in
        /// <see cref="SeaScoutMissionViews"/>.</summary>
        public const string MissionName = "TrainingBattlesSeaScout";

        /// <summary>Opens the ride on the given naval scene. <paramref name="timeOfDayOverride"/>
        /// is the pinned battle hour (-1 = campaign clock), same contract as the land scout.</summary>
        public static void Open(string sceneId, int timeOfDayOverride = -1)
        {
            var party = MobileParty.MainParty;
            var ships = party.Ships;
            if (ships == null || ships.Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Training Battles: no hull to ride — the sea scout needs a ship."));
                return;
            }

            // The flagship: the same pick the sea drill pins to the player.
            Ship flagship = ships[0];
            for (int i = 1; i < ships.Count; i++)
            {
                try { if (ships[i].FlagshipScore > flagship.FlagshipScore) flagship = ships[i]; }
                catch { }
            }

            // How many hands the deck takes — the DLC's own deployment arithmetic, with the
            // hull's raw crew capacity as the fallback answer.
            var playerShips = new MBList<IShipOrigin> { flagship };
            int crewCap;
            try
            {
                crewCap = NavalDLCManager.Instance.GameModels.ShipDeploymentModel
                    .GetMaximumDeployableTroopCountForTeam(playerShips, isPlayerTeam: true);
            }
            catch { crewCap = flagship.TotalCrewCapacity; }
            if (crewCap < 1) crewCap = 1;

            // The ship's company as synthetic combatants: the player at the helm, then the
            // roster's healthy men until the deck is full. Synthetic (CustomBattleCombatant)
            // on purpose — mission casualties, XP and states never touch the campaign roster,
            // the same "it never happened" guarantee the land scout gets for free by walking.
            var mainHero = Hero.MainHero;
            var banner = mainHero?.ClanBanner ?? Banner.CreateRandomBanner();
            var culture = (BasicCultureObject?)mainHero?.Culture ?? party.Party.Culture;
            var company = new CustomBattleCombatant(
                new TextObject("{=TB_sea_scout_company}The Ship's Company"), culture, banner)
            { Side = BattleSideEnum.Defender };
            var playerCharacter = CharacterObject.PlayerCharacter;
            company.AddCharacter(playerCharacter, 1);
            company.SetGeneral(playerCharacter);
            int seats = crewCap - 1;
            try
            {
                foreach (var element in party.MemberRoster.GetTroopRoster())
                {
                    if (seats <= 0) break;
                    var character = element.Character;
                    if (character == null || character == playerCharacter) continue;
                    if (character.IsHero)
                    {
                        if (character.HeroObject?.IsWounded == false)
                        {
                            company.AddCharacter(character, 1);
                            seats--;
                        }
                        continue;
                    }
                    int healthy = element.Number - element.WoundedNumber;
                    if (healthy <= 0) continue;
                    int take = Math.Min(healthy, seats);
                    company.AddCharacter(character, take);
                    seats -= take;
                }
            }
            catch { }

            // The empty horizon: a combatant with no men and no hulls, so both teams exist
            // (much of the mission stack indexes them) while nothing ever spawns opposite.
            var horizon = new CustomBattleCombatant(
                new TextObject("{=TB_sea_scout_horizon}The Open Sea"), culture, banner)
            { Side = BattleSideEnum.Attacker };

            var suppliers = new IMissionTroopSupplier[2];
            suppliers[(int)BattleSideEnum.Defender] = new CustomBattleTroopSupplier(
                company, isPlayerSide: true, isPlayerGeneral: true, isSallyOut: false);
            suppliers[(int)BattleSideEnum.Attacker] = new CustomBattleTroopSupplier(
                horizon, isPlayerSide: false, isPlayerGeneral: false, isSallyOut: false);

            // The record is the land scout's own (map patch + fixed approach + pinned hour), with
            // the one naval stroke every War Sails mission adds: simulated water on.
            var rec = ScoutMission.CreatePatchAwareRecord(sceneId, timeOfDayOverride);
            rec.AtmosphereOnCampaign.NauticalInfo.UsesNavalSimulatedWater = 1;

            // Sailed-out health, healed home on every exit road (see class doc).
            var hullSnapshot = new List<(Ship Ship, float HitPoints, float SailHitPoints)>
            {
                (flagship, flagship.HitPoints, flagship.SailHitPoints)
            };

            var enemyShips = new MBList<IShipOrigin>();
            int[] maxTroopsPerTeam = { crewCap, 0, 0 };

            TbLog.Info("sea-scout", "Riding out: scene=" + sceneId + " flagship='" + (flagship.Name?.ToString() ?? "?")
                + "' crew=" + (crewCap - seats) + "/" + crewCap + " hour=" + timeOfDayOverride);

            NavalMissionState.OpenNew(MissionName, rec, mission => new MissionBehavior[]
            {
                new NavalShipsLogic(),
                new NavalFloatsamLogic(),
                new NavalAgentsLogic(),
                new DefaultNavalMissionLogic(playerShips, null, enemyShips,
                    NavalShipDeploymentLimit.Max(), NavalShipDeploymentLimit.Invalid(),
                    NavalShipDeploymentLimit.Max()),
                new NavalTrajectoryPlanningLogic(),
                new DefaultNavalMissionAgentSpawnLogic(suppliers, BattleSideEnum.Defender,
                    deployablePlayerShipCount: 1, maxTroopsPerTeam),
                new NavalMissionDeploymentPlanningLogic(mission),
                new BattlePowerCalculationLogic(),
                new WaveParametersComputerLogic(),
                new MissionOptionsComponent(),
                new CampaignMissionComponent(),
                new NavalMissionCombatantsLogic(
                    new IBattleCombatant[] { company, horizon }, company, company, horizon,
                    Mission.MissionTeamAITypeEnum.NoTeamAI, isPlayerSergeant: false),
                new AgentHumanAILogic(),
                new NavalBoundaryForceFieldLogic(),
                new NavalAssignPlayerRoleInTeamMissionController(
                    isPlayerGeneral: true, isPlayerSergeant: false, isPlayerInArmy: false),
                new EquipmentControllerLeaveLogic(),
                new MissionHardBorderPlacer(),
                new MissionBoundaryPlacer(),
                new MissionBoundaryCrossingHandler(30f),
                new BasicLeaveMissionLogic(),
                new NavalDeploymentMissionController(isPlayerAttacker: false),
                new NavalDeploymentHandler(isPlayerAttacker: false),
                new SeaScoutRideLogic(hullSnapshot),
            });
        }
    }

    /// <summary>Skips the deployment phase (the ride opens sailing, not planning) and heals the
    /// flagship home on every mission end. Skeleton deliberately vanilla-typed throughout — the
    /// soft-dependency discipline of <see cref="SeaScoutMission"/>'s class doc.</summary>
    internal sealed class SeaScoutRideLogic : MissionLogic
    {
        private readonly List<(Ship Ship, float HitPoints, float SailHitPoints)> _hullSnapshot;
        private DeploymentMissionController? _deployment;
        private bool _restored;

        public SeaScoutRideLogic(List<(Ship, float, float)> hullSnapshot)
        {
            _hullSnapshot = hullSnapshot;
        }

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();
            _deployment = Mission.GetMissionBehavior<DeploymentMissionController>();
            if (_deployment != null) _deployment.OnAfterSetupTeams += SetSail;
        }

        /// <summary>Fires once, the tick the teams stand ready. With a deck's worth of men the
        /// BattleInitializationModel usually auto-finished deployment inside SetupTeams already —
        /// IsDeploymentFinished guards the double call (FinishDeployment removes the controller
        /// from the mission, but the event invocation later in that same tick still reaches us).</summary>
        private void SetSail()
        {
            var deployment = _deployment;
            _deployment = null;
            if (deployment == null) return;
            deployment.OnAfterSetupTeams -= SetSail;
            try
            {
                if (!Mission.IsDeploymentFinished) deployment.FinishDeployment();
            }
            catch (Exception ex)
            {
                TbLog.Info("sea-scout", "FinishDeployment failed: " + ex.Message);
            }
        }

        public override void OnDeploymentFinished()
        {
            base.OnDeploymentFinished();
            InformationManager.DisplayMessage(new InformationMessage(
                "The sea is yours: sail her, row with the men, learn the water. "
                + "No cost, no clock — and hold Tab to make for home when you have seen enough."));
        }

        protected override void OnEndMission()
        {
            RestoreHulls();
            base.OnEndMission();
        }

        public override void OnRemoveBehavior()
        {
            // The belt behind the belt: whatever exit road the mission takes, the behavior is
            // always removed — the flagship never keeps a scratch from a free ride.
            RestoreHulls();
            if (_deployment != null)
            {
                try { _deployment.OnAfterSetupTeams -= SetSail; } catch { }
                _deployment = null;
            }
            base.OnRemoveBehavior();
        }

        private void RestoreHulls()
        {
            if (_restored) return;
            _restored = true;
            try
            {
                foreach (var entry in _hullSnapshot)
                {
                    var ship = entry.Ship;
                    if (ship == null) continue;
                    try
                    {
                        if (ship.Owner != PartyBase.MainParty) ship.Owner = PartyBase.MainParty;
                        ship.HitPoints = entry.HitPoints;
                        ship.SailHitPoints = entry.SailHitPoints;
                    }
                    catch { }
                }
                MobileParty.MainParty.SetNavalVisualAsDirty();
                TbLog.Info("sea-scout", "Ride over — flagship healed home.");
            }
            catch { }
        }
    }
}
