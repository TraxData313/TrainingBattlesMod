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
    /// and pivot. So scout and drill share <see cref="CreatePatchAwareRecord(string)"/>: same
    /// scene, same map spot, same fixed approach direction — identical lines. A REAL battle's
    /// direction comes from where the attacker actually stands — and the encounter-menu scout
    /// (<see cref="OpenForRealEncounter"/>) passes exactly that, so scouting an imminent battle
    /// previews its true lines, ends and facings included.
    /// </summary>
    internal static class ScoutMission
    {
        /// <summary>The mission (and view-set) name the scout ride opens under. The matching view
        /// list lives in <see cref="ScoutMissionViews"/>: vanilla's "Camp" walk-around set plus the
        /// hold-Tab "leaving area" bar that "Camp" does not carry.</summary>
        public const string MissionName = "TrainingBattlesScout";

        /// <summary>The approach direction every drill and scout preview assumes. Fixed so the two
        /// always agree; a real battle's direction comes from where the attacker actually rides in.</summary>
        internal static readonly Vec2 AssumedEncounterDirection = new Vec2(0f, 1f);

        /// <summary>The mission record both the TRAINING BATTLE and the scout ride open with —
        /// carrying the current map patch and the fixed approach direction, exactly like vanilla's
        /// real field battles (MenuHelper.EncounterAttackConsequence), so deployment is computed
        /// deterministically and the scouted lines are the drilled lines.</summary>
        public static MissionInitializerRecord CreatePatchAwareRecord(string sceneId, int timeOfDayOverride = -1)
            => CreatePatchAwareRecord(sceneId, AssumedEncounterDirection, timeOfDayOverride);

        /// <summary>Same record with an explicit approach direction — a REAL encounter's scout
        /// passes the true attacker-to-defender direction (vanilla's own formula), so the previewed
        /// path ends are the coming battle's, not the drill's assumed ones.
        /// <paramref name="timeOfDayOverride"/> (-1 = campaign clock) swaps the campaign atmosphere
        /// for one of vanilla custom battle's fixed-hour presets — see <see cref="AtmosphereFor"/>.</summary>
        public static MissionInitializerRecord CreatePatchAwareRecord(string sceneId, Vec2 encounterDirection, int timeOfDayOverride = -1)
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
                AtmosphereOnCampaign = AtmosphereFor(position, timeOfDayOverride),
                SceneHasMapPatch = true,
                DecalAtlasGroup = 2,
                PatchCoordinates = patch.normalizedCoordinates,
                PatchEncounterDir = encounterDirection,
            };
        }

        /// <summary>The mission's sky: the campaign clock's own atmosphere — or, when the player
        /// pinned the battle hour (ModConfig.BattleTimeOfDay), the same fixed preset vanilla's
        /// CUSTOM BATTLE uses for that hour (see <see cref="Models.AtmospherePresets"/>).
        /// Streamer-proofing: a drill should never be fought blind in the dark unless wanted.</summary>
        private static AtmosphereInfo AtmosphereFor(CampaignVec2 position, int timeOfDayOverride)
        {
            return Models.AtmospherePresets.For(timeOfDayOverride)
                ?? Campaign.Current.Models.MapWeatherModel.GetAtmosphereModel(position);
        }

        /// <summary>The training/muster scout: assumed approach direction, the player previews the
        /// defender's line (a drill later forms exactly these lines). <paramref name="timeOfDayOverride"/>
        /// is the pinned battle hour (-1 = campaign clock), so the preview lighting is the drill's.</summary>
        public static void Open(string sceneId, int timeOfDayOverride = -1)
            => Open(sceneId, AssumedEncounterDirection, BattleSideEnum.Defender, realEncounter: false, timeOfDayOverride);

        /// <summary>The REAL-encounter scout, launched from the encounter menu while the armies
        /// stand facing each other: the true approach direction and the player's true side, so the
        /// ride previews the exact lines and facings of the imminent battle. The encounter and its
        /// map event are left untouched — campaign time is frozen inside missions, and on leaving
        /// the ride the encounter menu re-activates (the same mission-under-a-menu shape as
        /// vanilla's pre-battle conversation).</summary>
        public static void OpenForRealEncounter(string sceneId, BattleSideEnum playerSide, Vec2 encounterDirection)
            => Open(sceneId, encounterDirection, playerSide, realEncounter: true);

        private static void Open(string sceneId, Vec2 encounterDirection, BattleSideEnum playerSide, bool realEncounter, int timeOfDayOverride = -1)
        {
            var rec = CreatePatchAwareRecord(sceneId, encounterDirection, timeOfDayOverride);
            MissionState.OpenNew(MissionName, rec, _ => new MissionBehavior[]
            {
                new MissionOptionsComponent(),
                new CampaignMissionComponent(),
                new BasicLeaveMissionLogic(),
                new MissionHardBorderPlacer(),
                new MissionBoundaryPlacer(),
                new MissionBoundaryCrossingHandler(10f),
                new EquipmentControllerLeaveLogic(),
                new ScoutMissionLogic(playerSide, realEncounter),
            });
        }
    }

    /// <summary>Spawns the player (alone, real gear, player-controlled) and ends the mission on the
    /// Leave key — the same shape as vanilla's SimpleMountedPlayerMissionController, minus its
    /// hardcoded test horseman.</summary>
    internal sealed class ScoutMissionLogic : MissionLogic
    {
        private readonly BattleSideEnum _playerSide;
        private readonly bool _realEncounter;

        /// <param name="playerSide">Whose line the player stands on. The muster scout always
        /// previews the defender's; a real encounter passes the player's true side.</param>
        /// <param name="realEncounter">True when scouting an IMMINENT battle from the encounter
        /// menu — the record then carries the enemy's true approach, so the report may promise
        /// the lines instead of hedging.</param>
        public ScoutMissionLogic(BattleSideEnum playerSide = BattleSideEnum.Defender, bool realEncounter = false)
        {
            _playerSide = playerSide;
            _realEncounter = realEncounter;
        }

        /// <summary>The ride is a battlefield without a battle — but OTHER MODS' mission behaviors
        /// assume a battle's SHAPE. A teamless mission is the one thing no vanilla mission ever is
        /// (even the empty village/camp scenes run SandBox's MissionBasicTeamLogic), so a third-party
        /// tick that reaches for Mission.PlayerTeam / agent.Team / PlayerEnemyTeam finds null and
        /// takes the game down with it — War Horns' captain scan did exactly that (Nexus report,
        /// 2026.07.26). So the scout mission now wears the same two-team skin vanilla wears: one team
        /// per side, the player's carrying their faction's colors, PlayerTeam set, the lone rider
        /// spawned INTO it. No agents on either side, no combat logic, nothing else changes — but
        /// every "give me the teams" reflex in the modding ecosystem now finds what it expects.</summary>
        public override void EarlyStart()
        {
            base.EarlyStart();
            if (Mission.Teams.Count > 0) return;   // never fight a behavior that got there first
            var playerIsAttacker = _playerSide == BattleSideEnum.Attacker;
            var own1 = Hero.MainHero.MapFaction.Color;
            var own2 = Hero.MainHero.MapFaction.Color2;
            Mission.Teams.Add(BattleSideEnum.Defender,
                playerIsAttacker ? uint.MaxValue : own1,
                playerIsAttacker ? uint.MaxValue : own2);
            Mission.Teams.Add(BattleSideEnum.Attacker,
                playerIsAttacker ? own1 : uint.MaxValue,
                playerIsAttacker ? own2 : uint.MaxValue);
            Mission.PlayerTeam = playerIsAttacker ? Mission.AttackerTeam : Mission.DefenderTeam;
        }

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
                var ownFrame = _playerSide == BattleSideEnum.Attacker ? attackerFrame : defenderFrame;
                var enemyFrame = _playerSide == BattleSideEnum.Attacker ? defenderFrame : attackerFrame;
                frame = ownFrame;
                var toEnemy = enemyFrame.origin.AsVec2 - ownFrame.origin.AsVec2;
                direction = toEnemy.Normalized();
                deploymentReport = _realEncounter
                    ? "You stand where YOUR line will form — the enemy's line is about "
                        + (int)toEnemy.Length + " paces ahead. Their approach is known, so these "
                        + "are the true lines and facings of the coming battle."
                    : "You stand where YOUR line would form — the enemy's line is about "
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
                .Team(Mission.PlayerTeam)
                .Controller(AgentControllerType.Player));
            agent.WieldInitialWeapons();
            InformationManager.DisplayMessage(new InformationMessage(
                "Scouting the ground. Ride, look, remember — and hold Tab to leave when you have seen enough."));
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

        // Leaving is entirely vanilla's: the mission is friendly, so holding the Leave key (Tab)
        // runs Mission's own 0.6s leave timer — drawn by the MissionLeaveView in our view set —
        // and the escape menu's leave goes through BasicLeaveMissionLogic. (The old key-press
        // MissionEnded override here ended the mission INSTANTLY, before the bar could ever show.)

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
