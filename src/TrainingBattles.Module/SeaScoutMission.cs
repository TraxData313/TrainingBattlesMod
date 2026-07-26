using System;
using System.Collections.Generic;
using Helpers;
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
            // roster's healthy men until the deck is full. The ORIGINS are our own
            // SeaScoutAgentOrigin — campaign-native where it matters, consequence-free where it
            // counts: round 4's stack proved campaign game models HARD-CAST an agent's
            // BattleCombatant to PartyBase (SandboxAgentStatCalculateModel.InitializeMissionEquipment
            // died on the first crewman), so our origins present the REAL main party there, while
            // every casualty callback (SetKilled/SetWounded/SetRouted) is a no-op — mission states
            // never touch the campaign roster, the "it never happened" guarantee enforced at the
            // origin itself.
            var mainHero = Hero.MainHero;
            var banner = mainHero?.ClanBanner ?? Banner.CreateRandomBanner();
            var culture = (BasicCultureObject?)mainHero?.Culture ?? party.Party.Culture;
            var playerCharacter = CharacterObject.PlayerCharacter;
            var crew = new List<IAgentOriginBase> { new SeaScoutAgentOrigin(playerCharacter, banner) };
            int seats = crewCap - 1;
            try
            {
                // Heroes first (they stand the deck by name), then the ranks — spawn order is
                // supplier order, and the player must sail in the very first batch.
                foreach (var element in party.MemberRoster.GetTroopRoster())
                {
                    if (seats <= 0) break;
                    var character = element.Character;
                    if (character == null || character == playerCharacter || !character.IsHero) continue;
                    if (character.HeroObject?.IsWounded == false)
                    {
                        crew.Add(new SeaScoutAgentOrigin(character, banner));
                        seats--;
                    }
                }
                foreach (var element in party.MemberRoster.GetTroopRoster())
                {
                    if (seats <= 0) break;
                    var character = element.Character;
                    if (character == null || character.IsHero) continue;
                    int healthy = element.Number - element.WoundedNumber;
                    for (int i = 0; i < healthy && seats > 0; i++, seats--)
                        crew.Add(new SeaScoutAgentOrigin(character, banner));
                }
            }
            catch { }

            // The combatants exist for TEAM identity (side, banner, colors) and the preloader's
            // character walk — the ship's company mirrors the crew so equipment preloads true.
            var company = new CustomBattleCombatant(
                new TextObject("{=TB_sea_scout_company}The Ship's Company"), culture, banner)
            { Side = BattleSideEnum.Defender };
            foreach (var origin in crew)
                if (origin.Troop != null) company.AddCharacter(origin.Troop, 1);
            company.SetGeneral(playerCharacter);

            // The empty horizon: a combatant with no men and no hulls, so both teams exist
            // (much of the mission stack indexes them) while nothing ever spawns opposite.
            var horizon = new CustomBattleCombatant(
                new TextObject("{=TB_sea_scout_horizon}The Open Sea"), culture, banner)
            { Side = BattleSideEnum.Attacker };

            var suppliers = new IMissionTroopSupplier[2];
            suppliers[(int)BattleSideEnum.Defender] = new SeaScoutTroopSupplier(crew, playerCharacter);
            suppliers[(int)BattleSideEnum.Attacker] = new SeaScoutTroopSupplier(
                new List<IAgentOriginBase>(), null);

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
                + "' crew=" + crew.Count + "/" + crewCap + " hour=" + timeOfDayOverride);

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
                new SeaScoutDeploymentController(),
                new NavalDeploymentHandler(isPlayerAttacker: false),
                new SeaScoutRideLogic(hullSnapshot),
            });
        }
    }

    /// <summary>The ride's own agent origin — THE ROUND-4 FIX. Campaign game models hard-cast an
    /// agent's <c>Origin.BattleCombatant</c> to <c>PartyBase</c> (proven by round 4's stack:
    /// SandboxAgentStatCalculateModel.InitializeMissionEquipment threw InvalidCastException on the
    /// first crewman, because CustomBattleAgentOrigin hands back a CustomBattleCombatant). This
    /// origin presents the REAL main party there — campaign-native for every model, and truthful:
    /// the crew ARE the main party's men, so faces, colors and perk context all read right — while
    /// every casualty callback is a NO-OP: no kill, no wound, no rout ever reaches the campaign
    /// (vanilla's SimpleAgentOrigin was almost this, but its SetKilled truly kills heroes).
    /// All-vanilla types: soft-dependency clean.</summary>
    internal sealed class SeaScoutAgentOrigin : IAgentOriginBase
    {
        private readonly CharacterObject _troop;
        private Banner _banner;
        private readonly UniqueTroopDescriptor _descriptor;
        private readonly bool _hasThrownWeapon;
        private readonly bool _hasSpear;
        private readonly bool _hasShield;
        private readonly bool _hasHeavyArmor;

        public SeaScoutAgentOrigin(CharacterObject troop, Banner banner)
        {
            _troop = troop;
            _banner = banner;
            _descriptor = new UniqueTroopDescriptor(Game.Current.NextUniqueTroopSeed);
            Rank = MBRandom.RandomInt(10000);
            AgentOriginUtilities.GetDefaultTroopTraits(troop, out _hasThrownWeapon, out _hasSpear,
                out _hasShield, out _hasHeavyArmor);
        }

        public BasicCharacterObject Troop => _troop;

        /// <summary>The fix in one line: campaign models cast this to PartyBase — give them the
        /// real one.</summary>
        public IBattleCombatant BattleCombatant => PartyBase.MainParty;

        public Banner Banner => _banner;
        public bool IsUnderPlayersCommand => true;
        public bool IsInSameArmyAsPlayer => false;
        public uint FactionColor => PartyBase.MainParty?.MapFaction?.Color ?? 0u;
        public uint FactionColor2 => PartyBase.MainParty?.MapFaction?.Color2 ?? 0u;
        public int Rank { get; }
        public int Seed => CharacterHelper.GetPartyMemberFaceSeed(PartyBase.MainParty, _troop, Rank);
        public int UniqueSeed => _descriptor.UniqueSeed;
        bool IAgentOriginBase.HasThrownWeapon => _hasThrownWeapon;
        bool IAgentOriginBase.HasHeavyArmor => _hasHeavyArmor;
        bool IAgentOriginBase.HasShield => _hasShield;
        bool IAgentOriginBase.HasSpear => _hasSpear;

        // A free ride costs no one anything — the pledge, enforced at the origin.
        public void SetWounded() { }
        public void SetKilled() { }
        public void SetRouted(bool isOrderRetreat) { }
        public void OnAgentRemoved(float agentHealth) { }
        void IAgentOriginBase.OnScoreHit(BasicCharacterObject victim, BasicCharacterObject captain,
            int damage, bool isFatal, bool isTeamKill, WeaponComponentData attackerWeapon) { }
        public void SetBanner(Banner banner) { _banner = banner; }
        TroopTraitsMask IAgentOriginBase.GetTraitsMask()
            => AgentOriginUtilities.GetDefaultTraitsMask(this);
    }

    /// <summary>The ride's troop supplier: a plain ordered list of <see cref="SeaScoutAgentOrigin"/>s
    /// (player first — the first spawn batch must carry the helmsman), no campaign writeback, no
    /// custom-battle types. The empty-list instance serves the empty horizon.</summary>
    internal sealed class SeaScoutTroopSupplier : IMissionTroopSupplier
    {
        private readonly List<IAgentOriginBase> _all;
        private readonly BasicCharacterObject? _general;
        private int _supplied;

        public SeaScoutTroopSupplier(List<IAgentOriginBase> origins, BasicCharacterObject? general)
        {
            _all = origins;
            _general = general;
        }

        public int NumRemovedTroops => 0;
        public int NumTroopsNotSupplied => _all.Count - _supplied;
        public bool AnyTroopRemainsToBeSupplied => _supplied < _all.Count;

        public IEnumerable<IAgentOriginBase> SupplyTroops(int numberToAllocate)
        {
            var batch = new List<IAgentOriginBase>();
            while (numberToAllocate-- > 0 && _supplied < _all.Count) batch.Add(_all[_supplied++]);
            return batch;
        }

        public IAgentOriginBase? SupplyOneTroop()
            => _supplied < _all.Count ? _all[_supplied++] : null;

        public IEnumerable<IAgentOriginBase> GetAllTroops() => _all;
        public BasicCharacterObject? GetGeneralCharacter() => _general;
        public int GetNumberOfPlayerControllableTroops() => _all.Count;
    }

    /// <summary>The ride's deployment controller: vanilla's naval one wearing a FLIGHT RECORDER —
    /// a TbLog line at every phase boundary of the team-setup tick (crash round 2 died somewhere
    /// inside it, between the flagship's spawn and the formation stage, with nothing to say where)
    /// — plus one targeted self-heal: vanilla's SetupTeams dereferences Mission.InitialPlayerAgent
    /// UNGUARDED right after the player side stands up, so if the player agent is missing after
    /// base setup (the round-2 prime suspect), the recorder spawns the hero onto the flagship by
    /// hand (the same SpawnExistingHero road vanilla's captain reassignment rides) instead of
    /// letting the deref kill the mission.
    ///
    /// SOFT-DEPENDENCY NOTE: this class carries a NavalDLC BASE TYPE — the one sanctioned break
    /// of the bodies-only rule, same shape as the MCM settings class (a foreign base type in this
    /// assembly, proven harmless on installs without that module). Everything else keeps the rule.</summary>
    internal sealed class SeaScoutDeploymentController : NavalDeploymentMissionController
    {
        private int _loggedTicks;

        public SeaScoutDeploymentController() : base(isPlayerAttacker: false) { }

        public override void OnBehaviorInitialize()
        {
            TbLog.Info("sea-scout", "deploy: behavior-init begin");
            base.OnBehaviorInitialize();
            TbLog.Info("sea-scout", "deploy: behavior-init done");
        }

        public override void AfterStart()
        {
            TbLog.Info("sea-scout", "deploy: after-start begin");
            base.AfterStart();
            TbLog.Info("sea-scout", "deploy: after-start done");
        }

        public override void OnMissionTick(float dt)
        {
            // SetupTeams runs inside the base's first tick — sandwich the first few so a crash
            // inside leaves "tick N begin" as the last word.
            bool log = _loggedTicks < 3;
            if (log) TbLog.Info("sea-scout", "deploy: tick " + _loggedTicks + " begin (setupOver=" + TeamSetupOver + ")");
            base.OnMissionTick(dt);
            if (log)
            {
                TbLog.Info("sea-scout", "deploy: tick " + _loggedTicks + " end (setupOver=" + TeamSetupOver + ")");
                _loggedTicks++;
            }
        }

        protected override void OnSetupTeamsOfSide(BattleSideEnum side)
        {
            TbLog.Info("sea-scout", "deploy: setup side " + side + " begin");
            if (side == PlayerSide)
            {
                // Round-3 recorder: the crash lives INSIDE this base call for the player side,
                // between the flagship's spawn and the formation stage, and the base gives no
                // finer boundary to log at — so replicate its exact four steps (verified against
                // the decompile) with a log between each, reaching the two internal ones by
                // reflection. A managed exception gets its FULL inner stack into our log before
                // anything dies.
                SetUpPlayerSideStepByStep(side);
            }
            else
            {
                base.OnSetupTeamsOfSide(side);
            }
            var player = Mission.InitialPlayerAgent;
            TbLog.Info("sea-scout", "deploy: setup side " + side + " done; player agent "
                + (player != null ? "spawned" : "MISSING"));
            if (side == PlayerSide && player == null) SpawnPlayerByHand();
        }

        /// <summary>NavalDeploymentMissionController.OnSetupTeamsOfSide's own four steps, spelled
        /// out with the flight recorder between them. Two are internal to the DLC — reflection
        /// reaches them; a signature miss falls back to the plain base call (crash intact, but
        /// honest).</summary>
        private void SetUpPlayerSideStepByStep(BattleSideEnum side)
        {
            var missionLogic = Mission.GetMissionBehavior<DefaultNavalMissionLogic>();
            var spawnLogic = Mission.GetMissionBehavior<DefaultNavalMissionAgentSpawnLogic>();
            var deploy = typeof(DefaultNavalMissionLogic).GetMethod("DeployBattleSide",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var allocate = typeof(DefaultNavalMissionAgentSpawnLogic).GetMethod("AllocateAndDeployInitialTroops",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var sideOver = typeof(DefaultNavalMissionAgentSpawnLogic).GetMethod("OnSideDeploymentOver",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (missionLogic == null || spawnLogic == null || deploy == null || allocate == null || sideOver == null)
            {
                TbLog.Info("sea-scout", "deploy: step-by-step unavailable (reflection miss) — plain base call");
                base.OnSetupTeamsOfSide(side);
                return;
            }
            var args = new object[] { side };
            try
            {
                TbLog.Info("sea-scout", "deploy: step 1/4 DeployBattleSide (ship spawn)");
                deploy.Invoke(missionLogic, args);
                TbLog.Info("sea-scout", "deploy: step 2/4 AllocateAndDeployInitialTroops (crew spawn)");
                allocate.Invoke(spawnLogic, args);
                TbLog.Info("sea-scout", "deploy: step 3/4 agent AI states");
                SetupAgentAIStatesForSide(side);
                TbLog.Info("sea-scout", "deploy: step 4/4 OnSideDeploymentOver");
                sideOver.Invoke(spawnLogic, args);
                TbLog.Info("sea-scout", "deploy: player side fully set up");
            }
            catch (Exception ex)
            {
                var inner = (ex as System.Reflection.TargetInvocationException)?.InnerException ?? ex;
                TbLog.Info("sea-scout", "deploy: STEP FAILED — " + inner.GetType().Name + ": " + inner.Message
                    + Environment.NewLine + inner.StackTrace);
                throw;
            }
        }

        /// <summary>The self-heal: vanilla's player-team allocation should have put the player on
        /// the flagship's deck; if it did not, do it ourselves before SetupTeams' unguarded
        /// InitialPlayerAgent dereference turns the miss into a native crash.</summary>
        private void SpawnPlayerByHand()
        {
            try
            {
                var agents = Mission.GetMissionBehavior<NavalAgentsLogic>();
                var ships = Mission.GetMissionBehavior<NavalShipsLogic>();
                var origin = agents?.FindTroopOrigin((TeamSideEnum)0, o => o.Troop.IsPlayerCharacter);
                var assignment = ships != null ? ships.GetShipAssignment((TeamSideEnum)0, FormationClass.Infantry) : null;
                TbLog.Info("sea-scout", "deploy: hand-spawn — origin " + (origin != null ? "found" : "NULL")
                    + ", flagship assignment " + (assignment != null && assignment.MissionShip != null ? "set" : "NULL"));
                if (origin == null || agents == null || assignment?.MissionShip == null) return;
                agents.SpawnExistingHero(origin, assignment.MissionShip, out var agent);
                TbLog.Info("sea-scout", "deploy: hand-spawn " + (agent != null ? "SUCCEEDED" : "returned null")
                    + "; InitialPlayerAgent now " + (Mission.InitialPlayerAgent != null ? "set" : "STILL MISSING"));
            }
            catch (Exception ex)
            {
                TbLog.Info("sea-scout", "deploy: hand-spawn FAILED: " + ex.GetType().Name + " " + ex.Message);
            }
        }

        protected override void SetupAIOfEnemySide(BattleSideEnum enemySide)
        {
            TbLog.Info("sea-scout", "deploy: enemy AI setup begin (side " + enemySide + ")");
            base.SetupAIOfEnemySide(enemySide);
            TbLog.Info("sea-scout", "deploy: enemy AI setup done");
        }

        protected override void OnSetupTeamsFinished()
        {
            TbLog.Info("sea-scout", "deploy: setup-finished begin");
            base.OnSetupTeamsFinished();
            TbLog.Info("sea-scout", "deploy: setup-finished done (auto-finish check follows)");
        }

        protected override void BeforeDeploymentFinished()
        {
            TbLog.Info("sea-scout", "deploy: finish begin");
            base.BeforeDeploymentFinished();
        }

        protected override void AfterDeploymentFinished()
        {
            base.AfterDeploymentFinished();
            TbLog.Info("sea-scout", "deploy: finish done — under sail");
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
            TbLog.Info("sea-scout", "ride: behaviors initialized (controller "
                + (_deployment != null ? "hooked" : "NOT FOUND") + ")");
        }

        public override void EarlyStart()
        {
            base.EarlyStart();
            TbLog.Info("sea-scout", "ride: early-start done (teams exist, combatants placed)");
        }

        /// <summary>Round-3 recorder: every agent build, by name — if one crewman's build is the
        /// killer, the log's last line names them. Trim once the ride sails.</summary>
        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            base.OnAgentBuild(agent, banner);
            _agentsBuilt++;
            TbLog.Info("sea-scout", "agent " + _agentsBuilt + " built: "
                + (agent?.Character?.Name?.ToString() ?? "?"));
        }
        private int _agentsBuilt;

        public override void AfterStart()
        {
            base.AfterStart();
            TbLog.Info("sea-scout", "ride: after-start done (scene up, ship assignments made)");
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
            TbLog.Info("sea-scout", "ride: set-sail (deployment "
                + (Mission.IsDeploymentFinished ? "already finished by vanilla" : "finishing by hand") + ")");
            try
            {
                if (!Mission.IsDeploymentFinished) deployment.FinishDeployment();
            }
            catch (Exception ex)
            {
                TbLog.Info("sea-scout", "FinishDeployment failed: " + ex.GetType().Name + " " + ex.Message);
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
