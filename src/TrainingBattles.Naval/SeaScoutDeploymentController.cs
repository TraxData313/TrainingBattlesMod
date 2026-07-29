using System;
using System.Reflection;
using NavalDLC.Missions.Deployment;
using NavalDLC.Missions.MissionLogics;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TrainingBattles.Naval
{
    /// <summary>The sea scout ride's deployment controller: vanilla's naval one wearing a FLIGHT
    /// RECORDER — a TbLog line at every phase boundary of the team-setup tick (crash round 2 died
    /// somewhere inside it, between the flagship's spawn and the formation stage, with nothing to
    /// say where) — plus one targeted self-heal: vanilla's SetupTeams dereferences
    /// Mission.InitialPlayerAgent UNGUARDED right after the player side stands up, so if the player
    /// agent is missing after base setup (the round-2 prime suspect), the recorder spawns the hero
    /// onto the flagship by hand (the same SpawnExistingHero road vanilla's captain reassignment
    /// rides) instead of letting the deref kill the mission.
    ///
    /// WHY IT LIVES IN ITS OWN ASSEMBLY (2026.07.28): a base type is resolved EAGERLY when the CLR
    /// loads a type, so this one class — sitting in the main module assembly, as it did in
    /// v1.3.0-v1.3.3 — made every <c>Assembly.GetTypes()</c> walk over TrainingBattles.dll throw
    /// on an install without War Sails. The view-creator scan, MCM's settings scan and the savegame
    /// scan all take that walk at startup, so the mod died on the launcher for every player
    /// without the DLC (Nexus report, 2026.07.28). The old class doc called this the "one
    /// sanctioned break" of the bodies-only soft-dependency rule and claimed it was proven
    /// harmless; it was neither. The rule has no exceptions now: naval types in the module's
    /// method BODIES only, and anything that needs a naval type in its type surface comes here,
    /// to an assembly the module loads by hand (see NavalBridge) and only when the DLC is
    /// already loaded.</summary>
    public sealed class SeaScoutDeploymentController : NavalDeploymentMissionController
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
                BindingFlags.Instance | BindingFlags.NonPublic);
            var allocate = typeof(DefaultNavalMissionAgentSpawnLogic).GetMethod("AllocateAndDeployInitialTroops",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var sideOver = typeof(DefaultNavalMissionAgentSpawnLogic).GetMethod("OnSideDeploymentOver",
                BindingFlags.Instance | BindingFlags.NonPublic);
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
                var inner = (ex as TargetInvocationException)?.InnerException ?? ex;
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
}
