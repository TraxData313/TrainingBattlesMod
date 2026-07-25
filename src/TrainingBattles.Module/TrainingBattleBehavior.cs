using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TrainingBattles.Core;

namespace TrainingBattles
{
    /// <summary>
    /// The heart of the mod: the training muster. From the campaign map (hotkey) the player opens
    /// the muster menu, divides the company into two halves in the party screen, chooses which half
    /// he attacks with or defends with, and fights a real field battle against his own men on the
    /// actual local terrain. Afterward nobody has died: the fallen wake up (some wounded — the
    /// surgeon's Medicine helps), earned XP is kept at a configured percent, the party goes
    /// disorganized, and the cooldown clock starts.
    ///
    /// The battle recipe is a copy of vanilla's "Company of Trouble" quest (see
    /// docs/training-battle-research.md): a temp bandit-component party built from the player's own
    /// troops, a GameMenu that owns the whole flow, PlayerEncounter.Start/SetupFields/StartBattle,
    /// and CampaignMission.OpenBattleMission on the local map-patch scene.
    ///
    /// Since 2026.07.23 the muster's features are config-gated: the split-army drill hides behind
    /// EnableSplitTraining (OFF for V1 — scouting ships first, the drill returns once playtested),
    /// and EnableMockEnemyTraining (developer option, off) adds a second foe: a MOCK ENEMY composed
    /// culture by culture from synthetic troop pools. The phantoms ride the same battle recipe but
    /// are never the player's men — they spawn fresh into the temp party, never merge home, and the
    /// aftermath sweeps any "prisoners" they'd have yielded.
    /// </summary>
    public sealed class TrainingBattleBehavior : CampaignBehaviorBase
    {
        public const string MenuId = "training_battle_menu";
        private const string OpponentPartyIdPrefix = "training_opponents";
        // The mock-enemy drill's temp party: its men are PHANTOMS (never the player's), so the
        // stale-party recovery must destroy it WITHOUT merging — a distinct prefix keeps the two
        // recovery paths apart. Keep it outside OpponentPartyIdPrefix's StartsWith reach.
        private const string MockEnemyPartyIdPrefix = "training_mock_enemy";
        // The composer's supply: this many of EVERY troop of the culture's tree on the pool side.
        private const int MockPoolPerTroop = 500;
        // And the ceiling on how many men the composed enemy may field.
        private const int MockEnemyMaxMen = 1000;

        private readonly ModConfig _config;

        /// <summary>Read by <see cref="Models.TrainingBattleRewardModel"/> to zero out every
        /// campaign consequence (renown, loot, prisoners...) while a training battle runs.</summary>
        public static bool TrainingActive { get; private set; }

        internal static TrainingBattleBehavior? Instance { get; private set; }

        // Persisted: when the last training battle ended, in campaign hours (0 = never).
        private float _lastTrainingHours;

        // Persisted: while a drill has the lender bandit clan wearing our training colors, this
        // holds "clanId|color|color2|bannerCode" so ANY later session can dress the clan back —
        // even after a crash mid-drill (clan colors and banner live in the save file).
        private string _clanRestoreData = string.Empty;

        // Persisted one-shot: the 2026.07.23 builds could leave drill-scattered companions stuck
        // as fugitives ("Regrouping"); the first launch after the fix walks them home, once.
        private bool _fugitiveRescueDone;

        // Transient flow state — never saved; a mid-flow save/load resolves via the stale-party
        // recovery in OnSessionLaunched.
        private TroopRoster? _pickedTeam;          // the chosen opponents; real rosters untouched until Begin
        private TroopRoster? _mockEnemyTeam;       // the composed mock enemy (developer option) —
                                                   // phantom troops, never taken from the real roster;
                                                   // mutually exclusive with _pickedTeam
        private bool _opponentIsMockEnemy;         // live-battle flag: the temp party's men are NOT
                                                   // ours — never merge them home, sweep their capture
        private MobileParty? _opponentParty;
        private TroopRoster? _mainSnapshot;        // main party AFTER the split, before the fight
        private TroopRoster? _opponentSnapshot;    // opponent party before the fight
        private TroopRoster? _prisonSnapshot;      // main party's prisoners before the fight — to spot
                                                   // own men who ended up "captured" by the drill
        private string? _chosenSceneId;            // the battlefield the player picked for the drill
        private List<Ship>? _shipDividePick;       // the hulls the player sent OPPOSITE in the
                                                   // ship-divide window (null = follow the men:
                                                   // the FleetSplitMath auto-split)
        private List<KeyValuePair<ShipHull, int>>? _mockFleetPick;
                                                   // the phantom fleet's composition (hull class →
                                                   // how many), from the shipyard window
        private int _mockFleetTier = 1;            // the phantom fleet's fittings tier (0 = bare
                                                   // hulls, 1..3 = the best pieces of that harbor)
        private readonly Dictionary<CharacterObject, int> _battleDead = new Dictionary<CharacterObject, int>();
                                                   // per-troop TRUE dead of the drill, harvested
                                                   // from the map event's own DiedInBattle rosters
                                                   // at event end — the ONLY honest source: the
                                                   // roster diff cannot tell a dead man from a
                                                   // KO'd one VANISHED by the no-capture guard
                                                   // (empty captor list = removed, received by
                                                   // nobody)
        private bool _battleDeadHarvested;         // false = harvest never ran; the filter then
                                                   // falls back to the roster diff (conservative)
        private readonly Dictionary<Hero, int> _heroHpBefore = new Dictionary<Hero, int>();
                                                   // heroes' health walking INTO the drill — the
                                                   // restore ceiling (training is not a hospital)
        private readonly Dictionary<Hero, List<PartyRole>> _heroRolesBefore = new Dictionary<Hero, List<PartyRole>>();
                                                   // every hero's party roles (scout, surgeon...)
                                                   // walking in — the engine WIPES a hero's roles
                                                   // the moment they change party, so the opposing
                                                   // half's officers would come home demoted
        private readonly List<(Ship Ship, float HitPoints, float SailHitPoints)> _fleetSnapshot
            = new List<(Ship, float, float)>();    // every hull and its health walking into a sea
                                                   // drill (empty = land drill). "Sinking" is
                                                   // DestroyShipAction, which only sets Owner=null
                                                   // — the Ship object survives, so restore =
                                                   // re-own + re-heal
        private Dictionary<(ItemObject, ItemModifier?), int>? _itemSnapshot;
                                                   // the baggage train before the fight — anything
                                                   // ABOVE it afterward is drill loot and is removed,
                                                   // no matter which mod's loot pipeline handed it out
        private int _lootSweepTicks;               // clean-map ticks counted before the FINAL loot
                                                   // sweep — loot screens grant items only when the
                                                   // player closes them, AFTER the aftermath ran
        private int _chargedCost;                  // the pay-chest already charged; refunded on abort
        private bool _playerDefendsChoice;         // the muster's side toggle: false = you attack
                                                   // (the old default); honored by Begin AND the
                                                   // send-troops hill-watch alike (Anton's
                                                   // 2026.07.25 catch: auto-resolve had no side)
        private Settlement? _siegeSettlement;      // non-null = the muster is the CASTLE SIEGE
                                                   // drill at this owned castle (the castle
                                                   // update, 2026.07.25): the drill storms or
                                                   // holds these very walls; garrison and
                                                   // militia stand with the defense
        private List<KeyValuePair<SiegeEngineType, int>>? _siegeAtkPick;
                                                   // the engineer's bench: engines for the
                                                   // assault side (type → count)...
        private List<KeyValuePair<SiegeEngineType, int>>? _siegeDefPick;
                                                   // ...and for the walls
        private SiegeEvent? _drillSiegeEvent;      // the drill's campaign-side siege wrapper —
                                                   // a REAL SiegeEvent (the siege mission's
                                                   // engine-writeback null-refs without one),
                                                   // dismantled on every exit road; the map
                                                   // event's _keepSiegeEvent flag keeps the
                                                   // capture/sack machinery from ever seeing it
        private readonly List<(MobileParty Party, TroopRoster Snapshot)> _friendPartySnapshots
            = new List<(MobileParty, TroopRoster)>();
                                                   // every friendly party the siege event drags
                                                   // in (garrison, militia, guesting lords) —
                                                   // snapshotted walking in, restored per party
                                                   // by the same surgeon/XP arithmetic
        private List<float>? _wallSnapshot;        // wall-section HP ratios walking in (only
                                                   // campaign bombardment ticks damage walls —
                                                   // this is the belt to that finding's braces)
        private string _castleCooldownData = string.Empty;
                                                   // persisted per-castle drill clocks:
                                                   // "settlementId=lastHours;..." — each castle
                                                   // rests on its own, apart from the field
                                                   // drill's single clock
        private string? _clanResweepClanId;        // the lender clan gets ONE more visual sweep
                                                   // shortly after the aftermath (see
                                                   // RestoreOpponentClanLook) — transient
        private int _clanResweepTicks;
        private bool _launching;                   // inside LaunchTrainingCore's heavy work — the
                                                   // muster menu's init and the tick's finalize
                                                   // triggers must NOT run the aftermath while
                                                   // the launch itself shuffles menus/encounters
                                                   // (the castle drill's first crash: a menu
                                                   // re-init mid-launch ran FinishTrainingBattle
                                                   // over a half-built siege)
        private bool _checkResults;
        private bool _battleRan;                   // the drill's mission (or hill-watch simulation)
                                                   // truly started — gates the "finalize the moment
                                                   // the battle closes" trigger so it can never fire
                                                   // in the frames BEFORE the mission opens
        private bool _returnToMenuPending;         // set by the picker's close; honored on the next tick
        private bool _aftermathReady;              // our map event has truly ended (set by MapEventEnded)
        private bool? _pendingPlayerWon;           // winner captured at MapEventEnded, for flows where
                                                   // PlayerEncounter.Battle is already gone at aftermath time

        public TrainingBattleBehavior(ModConfig config)
        {
            _config = config;
            Instance = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
        }

        /// <summary>The one reliable "the fight is truly over" signal, for every flow — the mission
        /// path returns to our menu on its own, but the auto-resolve path leaves the menu first.</summary>
        private void OnMapEventEnded(TaleWorlds.CampaignSystem.MapEvents.MapEvent mapEvent)
        {
            if (!TrainingActive || _opponentParty == null || mapEvent == null) return;
            try
            {
                foreach (var party in mapEvent.InvolvedParties)
                {
                    if (party != _opponentParty.Party) continue;
                    _pendingPlayerWon = mapEvent.WinningSide == mapEvent.PlayerSide;
                    // The event dies here — read its DiedInBattle books NOW, while they exist.
                    // This fires for every finalize road: trigger (a) naturally, and (b)/(c)
                    // mid-FinishTrainingBattle (its PlayerEncounter.Finish ends the event
                    // BEFORE the aftermath's arithmetic runs, so the harvest is always fresh).
                    HarvestBattleDead(mapEvent);
                    _aftermathReady = true;
                    return;
                }
            }
            catch
            {
                _aftermathReady = true;
            }
        }

        /// <summary>Per-troop TRUE dead of the drill, from the event's own accounting
        /// (MapEventParty.DiedInBattle — filled by OnTroopKilled for exactly the men whose
        /// survival roll failed). The roster diff cannot supply this: vanilla hands the
        /// DEFEATED side's downed men to the winners as prisoners, and our no-capture guard
        /// empties the captor list — so those men are REMOVED and received by nobody,
        /// indistinguishable on the roster from the truly dead (Anton's lost drill: "130
        /// fell" from ~10 real KIA).</summary>
        private void HarvestBattleDead(TaleWorlds.CampaignSystem.MapEvents.MapEvent mapEvent)
        {
            try
            {
                _battleDead.Clear();
                foreach (var side in new[] { mapEvent.AttackerSide, mapEvent.DefenderSide })
                {
                    if (side?.Parties == null) continue;
                    foreach (var eventParty in side.Parties)
                    {
                        var died = eventParty?.DiedInBattle;
                        if (died == null) continue;
                        foreach (var el in died.GetTroopRoster())
                        {
                            if (el.Character == null || el.Character.IsHero) continue;
                            _battleDead.TryGetValue(el.Character, out var have);
                            _battleDead[el.Character] = have + el.Number;
                        }
                    }
                }
                _battleDeadHarvested = true;
            }
            catch { /* the filter falls back to the roster diff — never worse than before */ }
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("TrainingBattles_LastTrainingHours", ref _lastTrainingHours);
            dataStore.SyncData("TrainingBattles_ClanRestore", ref _clanRestoreData);
            dataStore.SyncData("TrainingBattles_FugitiveRescueDone", ref _fugitiveRescueDone);
            dataStore.SyncData("TrainingBattles_CastleCooldowns", ref _castleCooldownData);
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // Fresh session: no picked team, no live battle — whatever the previous session left.
            TrainingActive = false;
            _pickedTeam = null;
            _mockEnemyTeam = null;
            _opponentIsMockEnemy = false;
            _chosenSceneId = null;
            _shipDividePick = null;
            _mockFleetPick = null;
            _opponentParty = null;
            _mainSnapshot = null;
            _opponentSnapshot = null;
            _checkResults = false;
            _battleRan = false;
            _heroHpBefore.Clear();
            _chargedCost = 0;
            _fleetSnapshot.Clear();
            _battleDead.Clear();
            _battleDeadHarvested = false;
            _playerDefendsChoice = false; // every session opens on the attack, like the old default
            _siegeSettlement = null;
            _siegeAtkPick = null;
            _siegeDefPick = null;
            _drillSiegeEvent = null;
            _friendPartySnapshots.Clear();
            _wallSnapshot = null;
            _clanResweepClanId = null;
            _clanResweepTicks = 0;
            RestoreOpponentClanLook(); // a crash mid-drill must not leave a bandit clan in our colors
            RefreshBanditClanVisuals(); // and any historically orange looter icon heals on load
            RecoverStaleDrillSieges(); // BEFORE the party recovery — dismantle the siege shell first
            RecoverStaleOpponentParties();
            RescueStuckFugitiveCompanions();
            // Saves touched by pre-fix drills carry stale "separated after a battle" tracker
            // entries — this sweep also cleans them on load, not just after a drill.
            SweepCompanionSeparationTracker();
            AddMenus(starter);
        }

        // ------------------------------ the muster menu ------------------------------

        private void AddMenus(CampaignGameStarter starter)
        {
            starter.AddGameMenu(MenuId, "{TRAINING_MENU_TEXT}", MenuInit);
            // The tools come BEFORE the drills, in the same order on every door: the HOUR first
            // (Anton's playtest call, 2026.07.24 — it's the thing he sets before anything else),
            // then scout, then choose the ground — then the drills.
            starter.AddGameMenuOption(MenuId, "training_time",
                BattleSceneCatalog.ChooseTimeOfDayOptionText,
                TimeOfDayCondition, _ => ChooseTimeOfDay(() => GameMenu.SwitchToMenu(MenuId)));
            starter.AddGameMenuOption(MenuId, "training_scout",
                BattleSceneCatalog.ScoutBattlefieldOptionText,
                ScoutCondition, _ => ScoutGround());
            starter.AddGameMenuOption(MenuId, "training_ground",
                BattleSceneCatalog.SelectBattlefieldOptionText,
                GroundCondition, _ => ChooseGround());
            // The castle drill's engineer bench: which engines stand on each side of the walls.
            starter.AddGameMenuOption(MenuId, "training_siege_equip",
                "{=TB_opt_equip}Prepare siege equipment — {TB_EQUIP_NOW}",
                SiegeEquipCondition, _ => OpenSiegeEquip());
            starter.AddGameMenuOption(MenuId, "training_pick",
                "{=TB_opt_pick}Divide the men for a training battle",
                PickCondition, _ => OpenPicker());
            // The sea drill's second hand: WHICH hulls sail opposite (default: the auto split
            // that follows the men). Only shown afloat with a fleet worth dividing.
            starter.AddGameMenuOption(MenuId, "training_ships",
                "{=TB_opt_ships}Divide the ships — {TB_SHIPS_NOW}",
                ShipDivideCondition, _ => OpenShipDivide());
            starter.AddGameMenuOption(MenuId, "training_mock_enemy",
                "{=TB_opt_mock}Compose a mock enemy to drill against",
                MockEnemyCondition, _ => OpenMockEnemyComposer());
            // The phantoms' hulls: at sea a mock enemy must sail something — the shipyard
            // window lays its fleet down, hull class by hull class.
            starter.AddGameMenuOption(MenuId, "training_mock_fleet",
                "{=TB_opt_mock_fleet}Lay down the phantom fleet — {TB_MOCK_FLEET_NOW}",
                MockFleetCondition, _ => OpenFleetCompose());
            // The side is chosen ONCE, then both roads honor it — fight it yourself or watch
            // it resolve from the hill. (Anton's 2026.07.25 catch: the old menu carried the
            // side only on the two Begin options, so the send-troops auto-resolve silently
            // always made him the attacker — on land and at sea alike.)
            starter.AddGameMenuOption(MenuId, "training_side",
                "{=TB_opt_side}Choose your side — {TB_SIDE_NOW}",
                SideCondition, _ => ToggleSide());
            starter.AddGameMenuOption(MenuId, "training_begin",
                "{=TB_opt_begin}Begin the battle — {TB_BEGIN_SIDE}{TB_COST_SUFFIX}",
                args => BeginCondition(args), _ => BeginBattle(_playerDefendsChoice));
            starter.AddGameMenuOption(MenuId, "training_send_troops",
                "{=TB_opt_send2}Send the men in — watch it resolve from the hill{TB_COST_SUFFIX}",
                SendTroopsCondition, _ => LaunchTraining(_playerDefendsChoice, simulate: true));
            starter.AddGameMenuOption(MenuId, "training_cancel",
                "{=TB_opt_cancel}Cancel training",
                CancelCondition, _ => CancelTraining(), isLeave: true);

            // The CASTLE door (the castle update, 2026.07.25): at an owned castle, the same
            // muster over the settlement's own walls — the siege drill.
            starter.AddGameMenuOption("castle", "training_castle_drill",
                "{=TB_opt_castle_door}Hold a training siege on these walls",
                CastleDoorCondition, _ => OpenCastleMuster(), isLeave: false, index: 4);
        }

        // ------------------------------ the castle door ------------------------------

        private bool CastleDoorCondition(MenuCallbackArgs args)
        {
            if (!_config.EnableCastleTraining) return false;
            if (!_config.EnableSplitTraining && !_config.EnableMockEnemyTraining) return false;
            var settlement = Settlement.CurrentSettlement;
            if (settlement == null || !settlement.IsCastle) return false;
            if (settlement.OwnerClan != Clan.PlayerClan) return false;
            args.optionLeaveType = GameMenuOption.LeaveType.Manage;
            if (settlement.SiegeEvent != null || MobileParty.MainParty.MapEvent != null)
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject("{=TB_tip_castle_real}A real fight owns these walls — no drilling now.");
            }
            else if (!CastleCooldownReady(settlement, out var remaining))
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject("{=TB_tip_castle_rest}These walls were drilled recently — ready in "
                    + FormatRemaining(remaining) + ".");
            }
            else
            {
                args.Tooltip = new TextObject("{=TB_tip_castle_door}A mock siege on your own walls — storm them "
                    + "or hold them. The garrison and militia stand with the DEFENSE and follow the same "
                    + "training rules (wounds healed, XP kept, the surgeon's small real-death chance); take "
                    + "garrison men into your party first if you want them under your own banner. "
                    + "Your engineer's skill decides the siege equipment.");
            }
            return true;
        }

        private void OpenCastleMuster()
        {
            _siegeSettlement = Settlement.CurrentSettlement;
            try { GameMenu.SwitchToMenu(MenuId); } catch { _siegeSettlement = null; }
        }

        /// <summary>"Prepare siege equipment" — the engineer's bench, shown only on the castle
        /// muster. The label carries the standing pick so the menu tells the whole state.</summary>
        private bool SiegeEquipCondition(MenuCallbackArgs args)
        {
            if (_siegeSettlement == null) return false;
            args.optionLeaveType = GameMenuOption.LeaveType.Manage;
            var engineer = Officers.EngineerOfficer(MobileParty.MainParty);
            var tier = SiegeDrillMath.TierForSkill(engineer.Skill,
                _config.EngineerTier1Skill, _config.EngineerTier2Skill, _config.EngineerTier3Skill);
            var atk = CountEngines(_siegeAtkPick);
            var def = CountEngines(_siegeDefPick);
            var bill = SiegeEquipmentBill();
            MBTextManager.SetTextVariable("TB_EQUIP_NOW",
                atk + def == 0
                    ? "ladders only"
                    : atk + " attacking, " + def + " on the walls"
                        + (bill > 0 ? " (" + bill + " denars)" : ""), false);
            args.Tooltip = new TextObject("{=TB_tip_equip}Choose the engines for BOTH sides of the "
                + "walls — rams, towers, ballistae, mangonels, the trebuchet. " + engineer.Describe()
                + " builds tier " + tier + " of 3 (better engineers, bigger toys — thresholds in the "
                + "mod options). Each engine adds its worth to the drill's bill. Assault ladders "
                + "always stand, so no engines is a fair drill too.");
            return true;
        }

        private void OpenSiegeEquip()
        {
            try
            {
                var engineer = Officers.EngineerOfficer(MobileParty.MainParty);
                var vm = new UI.SiegeEquipVM(
                    engineer.Describe(), engineer.Skill,
                    _config.EngineerTier1Skill, _config.EngineerTier2Skill, _config.EngineerTier3Skill,
                    _config.SiegeEngineGoldPerManDay,
                    _siegeAtkPick, _siegeDefPick,
                    (atk, def) =>
                    {
                        _siegeAtkPick = atk.Count > 0 ? atk : null;
                        _siegeDefPick = def.Count > 0 ? def : null;
                        TbLog.Info("siege", "equipment picked: " + CountEngines(_siegeAtkPick)
                            + " attacking, " + CountEngines(_siegeDefPick) + " defending, bill "
                            + SiegeEquipmentBill());
                        UI.TrainingWindow.Close();
                        try { GameMenu.SwitchToMenu(MenuId); } catch { }
                    },
                    () =>
                    {
                        UI.TrainingWindow.Close();
                        try { GameMenu.SwitchToMenu(MenuId); } catch { }
                    });
                UI.TrainingWindow.Open("TrainingBattlesSiegeEquip", vm, vm.ExecuteCancel);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Training Battles: the engineer's bench could not open (" + ex.Message + ")."));
            }
        }

        private static int CountEngines(List<KeyValuePair<SiegeEngineType, int>>? pick)
        {
            var total = 0;
            if (pick != null)
                foreach (var pair in pick) total += pair.Value;
            return total;
        }

        /// <summary>What the picked engines add to the drill's bill: each engine's man-day
        /// construction cost times the configured gold-per-man-day rate, both sides.</summary>
        private int SiegeEquipmentBill()
        {
            var engines = new List<(int ManDayCost, int Count)>();
            foreach (var pick in new[] { _siegeAtkPick, _siegeDefPick })
            {
                if (pick == null) continue;
                foreach (var pair in pick)
                {
                    try { engines.Add((pair.Key.ManDayCost, pair.Value)); } catch { }
                }
            }
            return SiegeDrillMath.EquipmentBill(engines, _config.SiegeEngineGoldPerManDay);
        }

        // ------------------------------ the castle clocks ------------------------------

        /// <summary>Each castle rests on its own clock (config CastleTrainingCooldownHours),
        /// apart from the field drill's single one. The stamps ride the save as one string.</summary>
        private bool CastleCooldownReady(Settlement settlement, out double hoursRemaining)
        {
            hoursRemaining = 0;
            if (_config.CastleTrainingCooldownHours <= 0 || settlement == null) return true;
            var last = ReadCastleStamp(settlement);
            var now = CampaignTime.Now.ToHours;
            hoursRemaining = TrainingCooldown.HoursRemaining(now, last, _config.CastleTrainingCooldownHours);
            return TrainingCooldown.IsReady(now, last, _config.CastleTrainingCooldownHours);
        }

        private float ReadCastleStamp(Settlement settlement)
        {
            try
            {
                foreach (var entry in _castleCooldownData.Split(';'))
                {
                    var eq = entry.IndexOf('=');
                    if (eq <= 0 || entry.Substring(0, eq) != settlement.StringId) continue;
                    if (float.TryParse(entry.Substring(eq + 1),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var hours))
                        return hours;
                }
            }
            catch { }
            return 0f;
        }

        private void StampCastleCooldown(Settlement settlement)
        {
            try
            {
                var now = ((float)CampaignTime.Now.ToHours).ToString(System.Globalization.CultureInfo.InvariantCulture);
                var kept = new List<string> { settlement.StringId + "=" + now };
                foreach (var entry in _castleCooldownData.Split(';'))
                {
                    var eq = entry.IndexOf('=');
                    if (eq <= 0 || entry.Substring(0, eq) == settlement.StringId) continue;
                    kept.Add(entry);
                }
                _castleCooldownData = string.Join(";", kept);
            }
            catch { }
        }

        private void MenuInit(MenuCallbackArgs args)
        {
            // Without a real background mesh the menu shows the engine's placeholder — a big red
            // "temp". Same field-camp image the Company of Trouble quest menu uses.
            try { args.MenuContext.SetBackgroundMeshName("wait_ambush"); } catch { }
            if (_checkResults && !_launching)
            {
                FinishTrainingBattle();
                return;
            }
            MBTextManager.SetTextVariable("TRAINING_MENU_TEXT", BuildMenuText(), false);
        }

        private string BuildMenuText()
        {
            // Three short lines — Battlefield / Time / Drill — the whole state at a glance
            // (Anton, 2026.07.24: brief, real numbers, no empty big words; the options'
            // tooltips carry the longer explanations).
            string head;
            if (_pickedTeam != null && _pickedTeam.TotalManCount > 0)
            {
                var yours = MobileParty.MainParty.MemberRoster.TotalHealthyCount - _pickedTeam.TotalHealthyCount;
                head = "The two halves stand ready: " + _pickedTeam.TotalHealthyCount
                     + " men opposite, " + Math.Max(yours, 0)
                     + " with you." + FleetSplitPreview(Math.Max(yours, 1))
                     + " Set your side and begin — or call it off.";
            }
            else if (_mockEnemyTeam != null && _mockEnemyTeam.TotalManCount > 0)
            {
                head = "The mock enemy stands ready: " + _mockEnemyTeam.TotalHealthyCount
                     + " " + DescribeMockEnemyCultures(_mockEnemyTeam) + " men opposite, "
                     + MobileParty.MainParty.MemberRoster.TotalHealthyCount
                     + " with you — phantoms, your own ranks untouched. Set your side and begin — or call it off.";
            }
            else
            {
                head = _siegeSettlement != null
                    ? "A training siege at " + _siegeSettlement.Name
                    : "Battlefield setup and Training Battles";
            }
            if (_siegeSettlement != null)
            {
                var walls = FriendlyWallCount(_siegeSettlement);
                head += "{newline} {newline}The garrison and militia stand with the DEFENSE — "
                     + walls + (walls == 1 ? " man" : " men")
                     + " on the walls before the halves are counted, same training rules for "
                     + "everyone. Take garrison men into your party first if you want them under "
                     + "your own banner.";
            }
            var text = head + "{newline} {newline}" + BattlefieldLine() + "{newline} {newline}" + TimeOfDayLine();
            if (_config.EnableSplitTraining || _config.EnableMockEnemyTraining)
                text += "{newline} {newline}" + DrillSetupLine();
            return text;
        }

        /// <summary>"Battlefield: <name> (default — this ground)" or "(your pick)" — updates the
        /// moment the player picks a different ground. The default is shown by NAME when it is
        /// knowable (one local candidate — the usual case, each map patch is claimed by at most
        /// one scene in this game version); only a true multi-candidate patch says "random".</summary>
        private string BattlefieldLine()
        {
            if (_siegeSettlement != null)
            {
                var level = 1;
                try { level = _siegeSettlement.Town?.GetWallLevel() ?? 1; } catch { }
                return "Battlefield - the walls of " + _siegeSettlement.Name + " (level " + level + " walls).";
            }
            var pool = TrainingGroundPool(out var localCount);
            if (_chosenSceneId != null)
            {
                var name = _chosenSceneId;
                foreach (var scene in pool)
                    if (scene.SceneID == _chosenSceneId) { name = BattleSceneCatalog.Describe(scene); break; }
                return "Battlefield - " + name + " (your pick).";
            }
            if (localCount == 1)
                return "Battlefield - " + BattleSceneCatalog.Describe(pool[0]) + " (default — this ground).";
            return "Battlefield - random on this kind of ground (default).";
        }

        private string TimeOfDayLine()
        {
            var hour = Models.TrainingBattlesMapWeatherModel.EffectiveBattleHour(_config);
            var oneBattle = Models.TrainingBattlesMapWeatherModel.PendingBattleHour != null;
            return hour >= 0
                ? "Time - " + Models.AtmospherePresets.Label(hour).ToLowerInvariant()
                    + (oneBattle ? " (your pick, next battle)." : " (mod-options default).")
                : "Time - the campaign clock" + (oneBattle ? " (your pick, next battle)." : " (default).");
        }

        /// <summary>The whole drill contract in one line: wounded math, surgeon, XP, cost,
        /// disorganized, cooldown — each piece only when it actually applies.</summary>
        private string DrillSetupLine()
        {
            var line = "Training battle - " + CasualtyNote() + " " + XpKeptNote();
            var cost = ComputeTrainingCost();
            if (cost > 0)
            {
                line += " " + cost + " denars (" + CostDays()
                      + FormatDaysWages(CostDays())
                      + " a man"
                      + (_siegeSettlement != null ? ", castle rates" : (MainPartyAtSea() ? ", sea rates" : ""))
                      + (_siegeSettlement != null && SiegeEquipmentBill() > 0
                          ? ", " + SiegeEquipmentBill() + " of it engines" : "")
                      + ") for training equipment and troop rewards.";
            }
            if (_siegeSettlement != null)
            {
                var renown = CastleDrillRenown(out var influence);
                if (renown > 0f || influence > 0f)
                    line += " The realm notices a grand muster: +" + renown.ToString("0.#")
                          + " renown, +" + influence.ToString("0.#") + " influence.";
            }
            if (_config.DisorganizedAfterTraining)
                line += " Party becomes disorganized.";
            if (!DrillCooldownReady(out var remaining))
                line += " Next drill in " + FormatRemaining(remaining) + ".";
            else if (_siegeSettlement != null && _config.CastleTrainingCooldownHours > 0)
                line += " One siege drill per " + _config.CastleTrainingCooldownHours + " hours at each castle.";
            else if (_siegeSettlement == null && _config.CooldownHours > 0)
                line += " One drill per " + _config.CooldownHours + " hours.";
            return line;
        }

        /// <summary>The muster's cooldown, whichever clock owns this drill: the castle's own in
        /// siege mode, the field drill's single clock otherwise.</summary>
        private bool DrillCooldownReady(out double hoursRemaining)
        {
            return _siegeSettlement != null
                ? CastleCooldownReady(_siegeSettlement, out hoursRemaining)
                : CooldownReady(out hoursRemaining);
        }

        /// <summary>The grand muster's prestige: renown and influence per 100 friendly men on
        /// the field (both halves, garrison and militia), by the config rates.</summary>
        private float CastleDrillRenown(out float influence)
        {
            var men = MobileParty.MainParty?.MemberRoster?.TotalManCount ?? 0;
            if (_siegeSettlement != null) men += FriendlyWallCount(_siegeSettlement);
            var renown = (float)(men / 100.0 * _config.CastleDrillRenownPer100Men);
            influence = (float)(men / 100.0 * _config.CastleDrillInfluencePer100Men);
            return renown;
        }

        /// <summary>How many men the castle itself sends to the walls — the garrison's and the
        /// militia's healthy counts (guesting lord parties fight too but are their own men).</summary>
        private static int FriendlyWallCount(Settlement settlement)
        {
            var total = 0;
            try
            {
                foreach (var party in settlement.Parties)
                {
                    if (party == null || party == MobileParty.MainParty) continue;
                    if (!party.IsGarrison && !party.IsMilitia) continue;
                    total += party.MemberRoster?.TotalHealthyCount ?? 0;
                }
            }
            catch { }
            return total;
        }

        /// <summary>"85% XP kept (Quartermaster Ansif (Leadership 140))." — the XP officer by
        /// name, so the player sees WHO sets the rate and how good they are: the quartermaster's
        /// Leadership on land, the First Mate's Boatswain at sea (see <see cref="Officers"/>).
        /// The rate runs linearly from the config floor at skill 0 to the ceiling at 300; past
        /// 100% the drill grants bonus XP.</summary>
        private string XpKeptNote()
        {
            var pct = EffectiveXpKeptPercent(out var officer);
            return pct + "% XP kept (" + officer.Describe() + ").";
        }

        /// <summary>The drill's live XP-kept percent: <see cref="AftermathMath.XpKeptPercentForSkill"/>
        /// over the party's XP officer. A missing officer scores as skill 0 — the honest floor,
        /// never a crash.</summary>
        private int EffectiveXpKeptPercent(out Officers.Officer officer)
        {
            officer = Officers.XpOfficer(MobileParty.MainParty, MainPartyAtSea());
            return AftermathMath.XpKeptPercentForSkill(
                officer.Skill, _config.XpKeptMinPercent, _config.XpKeptMaxPercent);
        }

        /// <summary>"Of the fallen 1.6% truly die and 17.5% wake wounded; 8% of the downed stay
        /// wounded (Surgeon Aeron (Medicine 80))." — the surgeon's three live bands, so the
        /// player sees the doctor's worth (and the real stakes) before committing.</summary>
        private string CasualtyNote()
        {
            var surgeon = Officers.SurgeonOfficer(MobileParty.MainParty);
            var death = AftermathMath.ChancePercentForSkill(
                _config.RealDeathPercentAtMedicine0, _config.RealDeathPercentAtMedicine300, surgeon.Skill);
            var kiaWounded = AftermathMath.ChancePercentForSkill(
                _config.KiaWoundedPercentAtMedicine0, _config.KiaWoundedPercentAtMedicine300, surgeon.Skill);
            var stayWounded = AftermathMath.ChancePercentForSkill(
                _config.DownedWoundedPercentAtMedicine0, _config.DownedWoundedPercentAtMedicine300, surgeon.Skill);
            return "Of the fallen "
                + (death > 0.0 ? death.ToString("0.##") + "% truly die and " : "none truly die and ")
                + kiaWounded.ToString("0.##") + "% wake wounded; "
                + stayWounded.ToString("0.##") + "% of the downed stay wounded ("
                + surgeon.Describe() + ").";
        }

        /// <summary>The drill's price in days of wages — castle, sea or land rates, by where
        /// the drill stands (Anton, 2026.07.25: the sea drill costs double, the castle drill
        /// five days — a siege takes real organization).</summary>
        private int CostDays() =>
            _siegeSettlement != null ? _config.CastleTrainingCostWages
            : MainPartyAtSea() ? _config.TrainingCostWagesSea : _config.TrainingCostWagesLand;

        /// <summary>" The fleet divides with the men: 2 hulls opposite, 3 with you (the flagship
        /// yours)." — the same split <see cref="SplitFleet"/> will actually make, computed on the
        /// same inputs, so the muster text is a promise and not an estimate. A standing manual
        /// pick from the ship-divide window is told as "at your word". Empty on land, with a
        /// lone hull, or with no divided company yet.</summary>
        private string FleetSplitPreview(int yourHealthyMen)
        {
            try
            {
                if (!MainPartyAtSea() || _pickedTeam == null) return string.Empty;
                var ships = MobileParty.MainParty.Ships;
                if (ships.Count < 2) return string.Empty;
                if (_shipDividePick != null)
                {
                    var picked = CountValidManualCrossings();
                    if (picked > 0 && picked < ships.Count)
                        return " The fleet divides at your word: "
                            + picked + (picked == 1 ? " hull" : " hulls") + " opposite, "
                            + (ships.Count - picked) + " with you (the flagship yours).";
                }
                var capacities = new List<int>(ships.Count);
                var flagship = 0;
                for (var i = 0; i < ships.Count; i++)
                {
                    capacities.Add(ships[i].TotalCrewCapacity);
                    try { if (ships[i].FlagshipScore > ships[flagship].FlagshipScore) flagship = i; }
                    catch { }
                }
                var crossing = FleetSplitMath.OpponentShips(capacities, flagship,
                    yourHealthyMen, _pickedTeam.TotalHealthyCount);
                var keep = ships.Count - crossing.Count;
                return " The fleet divides with the men: "
                    + crossing.Count + (crossing.Count == 1 ? " hull" : " hulls") + " opposite, "
                    + keep + (keep == 1 ? " hull" : " hulls") + " with you (the flagship yours).";
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>"Khuzait", or "mixed" when the composer was run more than once across
        /// cultures — read straight off the composed roster, no extra state to go stale.</summary>
        private static string DescribeMockEnemyCultures(TroopRoster team)
        {
            try
            {
                string? name = null;
                foreach (var el in team.GetTroopRoster())
                {
                    var culture = el.Character?.Culture?.Name?.ToString();
                    if (culture == null) continue;
                    if (name == null) name = culture;
                    else if (name != culture) return "mixed";
                }
                return name ?? "unknown";
            }
            catch
            {
                return "unknown";
            }
        }

        /// <summary>"in 20 hours and 15 minutes", "in 45 minutes" — the honest clock, not "about N hours".</summary>
        private static string FormatRemaining(double hours)
        {
            var totalMinutes = (int)Math.Ceiling(hours * 60.0);
            if (totalMinutes < 1) totalMinutes = 1;
            var h = totalMinutes / 60;
            var m = totalMinutes % 60;
            if (h <= 0) return m + (m == 1 ? " minute" : " minutes");
            var text = h + (h == 1 ? " hour" : " hours");
            return m > 0 ? text + " and " + m + (m == 1 ? " minute" : " minutes") : text;
        }

        private static string FormatDaysWages(int days) => days == 1 ? " day's wages" : " days' wages";

        private bool PickCondition(MenuCallbackArgs args)
        {
            if (!_config.EnableSplitTraining) return false;
            args.optionLeaveType = GameMenuOption.LeaveType.Manage;
            if (!DrillCooldownReady(out var remaining))
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject("{=TB_tip_rest}The men need rest — ready in " + FormatRemaining(remaining) + ".");
            }
            else if (MobileParty.MainParty.MemberRoster.TotalHealthyCount < 2)
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject("{=TB_tip_few}You need at least two healthy souls to hold a drill.");
            }
            else if (MainPartyAtSea() && MobileParty.MainParty.Ships.Count < 2)
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject("{=TB_tip_one_hull}A sea drill needs a fleet to divide — "
                    + "two hulls at the least, and you sail " + MobileParty.MainParty.Ships.Count + ".");
            }
            else
            {
                args.Tooltip = new TextObject("{=TB_tip_pick2}A mock battle against your own men. "
                    + CasualtyNote() + " " + XpKeptNote()
                    + (_config.DisorganizedAfterTraining ? " The party is disorganized for a while after." : ""));
            }
            return true;
        }

        private bool MockEnemyCondition(MenuCallbackArgs args)
        {
            if (!_config.EnableMockEnemyTraining) return false;
            args.optionLeaveType = GameMenuOption.LeaveType.Manage;
            if (!DrillCooldownReady(out var remaining))
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject("{=TB_tip_rest}The men need rest — ready in " + FormatRemaining(remaining) + ".");
            }
            else if (MobileParty.MainParty.MemberRoster.TotalHealthyCount < 1)
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject("{=TB_tip_mock_none}Not a healthy soul to drill.");
            }
            else
            {
                args.Tooltip = new TextObject("{=TB_tip_mock2}Build a phantom force from every troop in the "
                    + "game — any cultures, any mix — and test the whole company against it. Your men follow "
                    + "the normal training rules; the phantoms vanish afterward."
                    + (MainPartyAtSea() ? " At sea, lay down their fleet too — the option below." : ""));
            }
            return true;
        }

        private bool GroundCondition(MenuCallbackArgs args)
        {
            if (_siegeSettlement != null) return false; // the castle drill's ground IS the castle
            if (!_config.EnableSplitTraining && !_config.EnableMockEnemyTraining) return false;
            args.optionLeaveType = GameMenuOption.LeaveType.Manage;
            var candidates = TrainingGroundPool(out _);
            if (candidates.Count == 0) return false;
            if (candidates.Count == 1)
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject("{=TB_tip_one_ground}Only one battlefield fits this ground: "
                    + BattleSceneCatalog.Describe(candidates[0]));
            }
            else
            {
                args.Tooltip = new TextObject(_chosenSceneId == null
                    ? "{=TB_tip_ground}See the " + candidates.Count + " battlefields for this kind of country and pick where the drill is fought."
                    : "{=TB_tip_ground_set}Battlefield chosen: " + _chosenSceneId + ". Select again to change it.");
            }
            return true;
        }

        private void ChooseGround()
        {
            var candidates = TrainingGroundPool(out var localCount);
            if (candidates.Count < 2) return;
            BattleSceneCatalog.ShowPicker(
                BattleSceneCatalog.SelectPickerTitle,
                "The battlefields for this kind of country. Pick where the drill is fought.",
                candidates, localCount, _chosenSceneId, offerFate: true, sceneId =>
            {
                _chosenSceneId = sceneId;
                // Re-init the muster menu so its text shows (or drops) the chosen ground.
                try { GameMenu.SwitchToMenu(MenuId); } catch { }
            });
        }

        private bool TimeOfDayCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Wait;
            args.Tooltip = new TextObject("{=TB_tip_time}Pick the hour for the NEXT battle only — "
                + "drill, field, siege or sea; the standing default lives in the mod options. Next battle: "
                + Models.AtmospherePresets.Label(
                    Models.TrainingBattlesMapWeatherModel.EffectiveBattleHour(_config)).ToLowerInvariant()
                + (Models.TrainingBattlesMapWeatherModel.PendingBattleHour != null ? " (your pick)." : "."));
            return true;
        }

        /// <summary>The one battle-hour dialog; the encounter menu door calls this too, so the
        /// choice looks and behaves identically everywhere.</summary>
        internal void ChooseTimeOfDay(Action refreshMenu)
        {
            BattleSceneCatalog.ShowTimeOfDayPicker(_config, () =>
            {
                try { refreshMenu(); } catch { }
            });
        }

        private bool ScoutCondition(MenuCallbackArgs args)
        {
            if (_siegeSettlement != null) return false; // you can walk your own walls any day
            args.optionLeaveType = GameMenuOption.LeaveType.Mission;
            if (TrainingGroundPool(out _).Count == 0) return false;
            if (MainPartyAtSea() && MobileParty.MainParty.Ships.Count == 0)
                return false; // a sea scout rides the flagship — no hull, no ride
            if (Hero.MainHero?.IsWounded == true)
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject("{=TB_tip_scout_wounded}You are wounded — scouting means riding.");
            }
            else if (MainPartyAtSea())
            {
                args.Tooltip = new TextObject("{=TB_tip_scout_sea}Take the flagship out alone — no battle, "
                    + "no cost, no clock. Sail the water the pickers name, row with the men, and hold Tab "
                    + "to make for home when you have seen enough.");
            }
            else
            {
                args.Tooltip = new TextObject("{=TB_tip_scout}Enter a battlefield alone — no battle, no cost. "
                    + "You spawn where your line would form, the enemy's line marked ahead: judge the ground "
                    + "AND the deployment before you ever have to fight on it. (A drill forms exactly the "
                    + "scouted lines; a real defence keeps the ground, but the enemy's approach picks their end.)");
            }
            return true;
        }

        private void ScoutGround()
        {
            var candidates = TrainingGroundPool(out var localCount);
            if (candidates.Count == 0) return;
            if (candidates.Count == 1)
            {
                LaunchScout(candidates[0].SceneID);
                return;
            }
            BattleSceneCatalog.ShowPicker(
                BattleSceneCatalog.ScoutPickerTitle,
                "Pick a battlefield and ride out alone. Walk the ground, stand on your deployment line, "
                + "and see if the deploy is good for your battle — if the map is fine but the lines are "
                + "bad, take another map or another spot.",
                candidates, localCount, null, offerFate: false, sceneId =>
            {
                if (sceneId != null) LaunchScout(sceneId);
            });
        }

        private void LaunchScout(string sceneId)
        {
            // The effective battle hour rides along, so the preview lighting is the drill's own.
            // At sea the ride is the FLAGSHIP's (SeaScoutMission — its own naval mission shape);
            // ashore it is the horse's (ScoutMission). Same doors, same hour, same freedom.
            try
            {
                var hour = Models.TrainingBattlesMapWeatherModel.EffectiveBattleHour(_config);
                if (MainPartyAtSea()) SeaScoutMission.Open(sceneId, hour);
                else ScoutMission.Open(sceneId, hour);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Training Battles: could not ride out (" + ex.Message + ")."));
            }
        }

        /// <summary>Sailing = sea drill. At muster time there IS no PlayerEncounter (CanMusterNow
        /// forbids one), so IsNavalEncounter() would always say land — the party's own navigation
        /// state is the truth here, exactly what MapEvent itself derives naval-ness from
        /// (IsNavalMapEvent = the event position is not on land).</summary>
        private static bool MainPartyAtSea()
        {
            try { return MobileParty.MainParty?.IsCurrentlyAtSea == true; }
            catch { return false; }
        }

        private static List<SingleplayerBattleSceneData> TrainingGroundPool(out int localCount)
        {
            try
            {
                return BattleSceneCatalog.WiderPoolAt(
                    MobileParty.MainParty.Position, MainPartyAtSea(), out localCount);
            }
            catch
            {
                localCount = 0;
                return new List<SingleplayerBattleSceneData>();
            }
        }

        /// <summary>The side toggle: one click flips attacker/defender, and BOTH roads — Begin
        /// and the send-troops hill-watch — honor the standing choice. Shown whenever a drill
        /// mode is on, so the side can be set before or after dividing the men.</summary>
        private bool SideCondition(MenuCallbackArgs args)
        {
            if (!_config.EnableSplitTraining && !_config.EnableMockEnemyTraining) return false;
            args.optionLeaveType = GameMenuOption.LeaveType.Manage;
            MBTextManager.SetTextVariable("TB_SIDE_NOW",
                _siegeSettlement != null
                    ? (_playerDefendsChoice ? "you hold the walls" : "you storm the walls")
                    : (_playerDefendsChoice ? "you defend" : "you attack"), false);
            args.Tooltip = new TextObject(_siegeSettlement != null
                ? "{=TB_tip_side_siege}Storm the walls or hold them — the garrison and militia "
                    + "always defend, so attacking means fighting through your own garrison. "
                    + "Select to switch."
                : "{=TB_tip_side}Attack or defend — one choice for both "
                    + "roads: fighting the battle yourself, or sending the men in and watching from "
                    + "the hill. Select to switch.");
            return true;
        }

        private void ToggleSide()
        {
            _playerDefendsChoice = !_playerDefendsChoice;
            try { GameMenu.SwitchToMenu(MenuId); } catch { } // re-init so every label tells the new side
        }

        private bool BeginCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Mission;
            return ReadyToBegin(args);
        }

        private bool SendTroopsCondition(MenuCallbackArgs args)
        {
            // No hill to watch a siege from — the send-troops road stays a field affair (the
            // siege simulation runs vanilla's own strategy machinery we deliberately never arm).
            if (_siegeSettlement != null) return false;
            args.optionLeaveType = GameMenuOption.LeaveType.Continue;
            return ReadyToBegin(args);
        }

        /// <summary>The shared gate for every "start the drill" option: a divided company and, when
        /// training costs wages, enough gold in the purse to fill the pay-chest. Also stamps the
        /// "(N denars)" suffix the Begin/send option labels carry.</summary>
        private bool ReadyToBegin(MenuCallbackArgs args)
        {
            if (!_config.EnableSplitTraining && !_config.EnableMockEnemyTraining) return false;
            var costNow = ComputeTrainingCost();
            MBTextManager.SetTextVariable("TB_COST_SUFFIX",
                costNow > 0 ? " (" + costNow + " denars)" : string.Empty, false);
            var mockDrill = _pickedTeam == null && _mockEnemyTeam != null;
            MBTextManager.SetTextVariable("TB_BEGIN_SIDE",
                _siegeSettlement != null
                    ? (mockDrill
                        ? (_playerDefendsChoice ? "you hold the walls against the mock enemy" : "you storm walls held by the mock enemy")
                        : (_playerDefendsChoice ? "your half holds the walls" : "your half storms the walls"))
                    : mockDrill
                        ? (_playerDefendsChoice ? "you defend against the mock enemy" : "you attack the mock enemy")
                        : (_playerDefendsChoice ? "your half defends" : "your half attacks"), false);
            var team = _pickedTeam ?? _mockEnemyTeam;
            if (team == null || team.TotalHealthyCount < 1)
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject(_config.EnableSplitTraining
                    ? (_config.EnableMockEnemyTraining
                        ? "{=TB_tip_pick_first2}Divide the men first — or compose a mock enemy."
                        : "{=TB_tip_pick_first}Divide the men first.")
                    : "{=TB_tip_mock_first}Compose the mock enemy first.");
                return true;
            }
            if (MainPartyAtSea() && mockDrill && (_mockFleetPick == null || _mockFleetPick.Count == 0))
            {
                // At sea the phantoms must sail something — the shipyard lays their fleet down.
                args.IsEnabled = false;
                args.Tooltip = new TextObject("{=TB_tip_mock_no_fleet}The phantoms have no hulls — "
                    + "lay down their fleet first (the shipyard option above).");
                return true;
            }
            if (MainPartyAtSea() && !mockDrill && MobileParty.MainParty.Ships.Count < 2)
            {
                // Divided ashore, sailed out since — a fleet of one cannot fight itself.
                args.IsEnabled = false;
                args.Tooltip = new TextObject("{=TB_tip_one_hull}A sea drill needs a fleet to divide — "
                    + "two hulls at the least, and you sail " + MobileParty.MainParty.Ships.Count + ".");
                return true;
            }
            if (costNow > 0 && (Hero.MainHero?.Gold ?? 0) < costNow)
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject("{=TB_tip_poor}The drill's pay-chest wants " + costNow
                    + " denars (" + CostDays() + FormatDaysWages(CostDays())
                    + " for every soul on the field" + (MainPartyAtSea() ? ", sea rates" : "")
                    + ") — you carry " + (Hero.MainHero?.Gold ?? 0) + ".");
            }
            return true;
        }

        /// <summary>What this drill costs: every soldier on the field (both halves — they all train)
        /// earns <see cref="CostDays"/> extra days of their daily wage (land or sea rates). Uses the
        /// game's own wage model, so mercenaries cost their usual half-again more.</summary>
        private int ComputeTrainingCost()
        {
            var total = 0;
            var days = CostDays();
            if (days > 0)
            {
                try
                {
                    var model = Campaign.Current.Models.PartyWageModel;
                    var wages = 0;
                    foreach (var el in MobileParty.MainParty.MemberRoster.GetTroopRoster())
                    {
                        if (el.Character == null || el.Character == CharacterObject.PlayerCharacter) continue;
                        wages += model.GetCharacterWage(el.Character) * el.Number;
                    }
                    // The castle drill pays the walls too: the garrison and the militia drill
                    // beside the halves (guesting lords cover their own men).
                    if (_siegeSettlement != null)
                    {
                        foreach (var party in _siegeSettlement.Parties)
                        {
                            if (party == null || party == MobileParty.MainParty) continue;
                            if (!party.IsGarrison && !party.IsMilitia) continue;
                            foreach (var el in party.MemberRoster.GetTroopRoster())
                            {
                                if (el.Character == null || el.Character.IsHero) continue;
                                wages += model.GetCharacterWage(el.Character) * el.Number;
                            }
                        }
                    }
                    total = wages * days;
                }
                catch
                {
                    total = 0;
                }
            }
            if (_siegeSettlement != null) total += SiegeEquipmentBill();
            return total;
        }

        private bool CancelCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Leave;
            return true;
        }

        private void CancelTraining()
        {
            // Nothing has touched the real rosters yet — dropping the pick is the whole cancel.
            // (isLeave only styles the option; leaving the menu is on us.)
            _pickedTeam = null;
            _mockEnemyTeam = null;
            _chosenSceneId = null;
            _shipDividePick = null;
            _mockFleetPick = null;
            var wasSiege = _siegeSettlement != null;
            _siegeSettlement = null;
            _siegeAtkPick = null;
            _siegeDefPick = null;
            // A castle muster came from the castle menu — go back to it, not out of the walls.
            try
            {
                if (wasSiege) GameMenu.SwitchToMenu("castle");
                else GameMenu.ExitToLast();
            }
            catch { }
        }

        // ------------------------------ the hotkey door ------------------------------

        /// <summary>Called every application tick from <see cref="SubModule"/>.</summary>
        internal void TickHotkey()
        {
            if (Campaign.Current == null || TaleWorlds.MountAndBlade.Mission.Current != null)
            {
                // A live mission while results are armed = the drill's battle truly running.
                if (_checkResults && TaleWorlds.MountAndBlade.Mission.Current != null) _battleRan = true;
                return;
            }
            if (!(Game.Current?.GameStateManager?.ActiveState is MapState mapState)) return;
            // The hill-watch counts as the battle running too (no mission ever opens on that road).
            if (_checkResults && PlayerEncounter.Current?.BattleSimulation != null) _battleRan = true;

            // The FINAL loot sweep. The aftermath's own sweep runs before any loot SCREEN is
            // closed — and a loot screen grants its items only on close. So the snapshot stays
            // alive until the map has been the active state for a moment (any loot/inventory
            // screen would be its own state and pause this counter), then one last diff removes
            // whatever the screen handed out, and the snapshot retires.
            if (_itemSnapshot != null && !TrainingActive && !_checkResults)
            {
                if (++_lootSweepTicks >= 30)
                {
                    RemoveDrillLoot();
                    _itemSnapshot = null;
                    _lootSweepTicks = 0;
                }
            }

            // The lender clan's SECOND visual sweep (see RestoreOpponentClanLook): catches a
            // party the world spawned into that clan in the same frame window as the aftermath.
            if (_clanResweepClanId != null && !TrainingActive && !_checkResults)
            {
                if (++_clanResweepTicks >= 30) ResweepLenderClanVisuals();
            }

            // Finalize the training the moment it is truly decided — WITHOUT waiting for (or
            // trusting) vanilla's wrap-up menus. Vanilla owns every non-happy path (the
            // auto-resolve wrap, retreat, defeat: capture menus, member scatter, re-attack
            // screens); politely waiting for them left the aftermath late or lost (Anton's
            // second playtest). Never while the launch itself is still shuffling state. Three
            // triggers:
            if (_checkResults && !_launching && TaleWorlds.MountAndBlade.Mission.Current == null)
            {
                // (a) Our map event has ended (fought out, auto-resolved, or captured) — run the
                //     aftermath NOW; PlayerEncounter.Finish inside it tears down whatever wrap
                //     menu vanilla managed to push.
                if (_aftermathReady)
                {
                    FinishTrainingBattle();
                    return;
                }
                // (b) The player bailed out — retreated from the mission OR canceled the
                //     auto-resolve — and vanilla shows its re-attack/send-troops encounter menu.
                //     A drill you walk away from is a drill that is over: finalize with the
                //     casualties so far. (Letting that menu live re-starts the fight as a PURE
                //     VANILLA battle with all our protections closed — Anton lost real upgrade
                //     XP to exactly that.)
                var encounter = PlayerEncounter.Current;
                if (encounter != null && mapState.AtMenu
                    && Campaign.Current.CurrentMenuContext?.GameMenu?.StringId != MenuId)
                {
                    FinishTrainingBattle();
                    return;
                }
                // (c) The battle RAN and is now over (mission closed / simulation done) but the
                //     encounter still lives mid-state — finalize on THIS first tick, before
                //     vanilla's state machine can walk the defeat road at all. On land the
                //     defeat wrap needs a menu and trigger (b) preempts it; at sea it moves
                //     faster and took the player CAPTIVE ("defeated_and_taken_prisoner" →
                //     captivity → our party-destroy read as "captors dispersed", plus a
                //     "stranded at sea" flash — Anton's naval defeat, 2026.07.25).
                if (encounter != null && _battleRan && encounter.BattleSimulation == null
                    && !mapState.AtMenu)
                {
                    FinishTrainingBattle();
                    return;
                }
            }

            // The picker closed last frame — now that the map state is truly back, re-enter the
            // muster menu so its text and options reflect the pick.
            if (_returnToMenuPending)
            {
                _returnToMenuPending = false;
                try
                {
                    if (mapState.AtMenu) GameMenu.SwitchToMenu(MenuId);
                    else GameMenu.ActivateGameMenu(MenuId);
                }
                catch { /* worst case the player reopens the menu by hotkey */ }
                return;
            }

            if (mapState.AtMenu || mapState.MapConversationActive) return;
            if (InformationManager.IsAnyInquiryActive()) return;
            if (!Input.IsKeyReleased(ParseKey(_config.OpenMenuHotkey))) return;

            if (!CanMusterNow(out var reason))
            {
                InformationManager.DisplayMessage(new InformationMessage("Training Battles: " + reason));
                return;
            }
            GameMenu.ActivateGameMenu(MenuId);
        }

        private static InputKey ParseKey(string name)
        {
            return Enum.TryParse<InputKey>((name ?? string.Empty).Trim(), ignoreCase: true, out var key)
                ? key
                : InputKey.G;
        }

        private static bool CanMusterNow(out string reason)
        {
            var main = MobileParty.MainParty;
            if (main == null) { reason = "no party to muster."; return false; }
            if (main.MapEvent != null || PlayerEncounter.Current != null) { reason = "you are already engaged."; return false; }
            if (main.CurrentSettlement != null) { reason = "drill in the open field, not within walls."; return false; }
            if (main.Army != null) { reason = "the army's banners answer to sterner plans — leave the army to drill."; return false; }
            if (main.BesiegedSettlement != null) { reason = "not in the middle of a siege."; return false; }
            reason = string.Empty;
            return true;
        }

        private bool CooldownReady(out double hoursRemaining)
        {
            var now = CampaignTime.Now.ToHours;
            hoursRemaining = TrainingCooldown.HoursRemaining(now, _lastTrainingHours, _config.CooldownHours);
            return TrainingCooldown.IsReady(now, _lastTrainingHours, _config.CooldownHours);
        }

        // ------------------------------ dividing the men ------------------------------

        private void OpenPicker()
        {
            // The screen works on a CLONE of the real roster: nothing leaves the party until the
            // battle truly begins, so a quit or crash mid-pick can never lose a man. A previous
            // pick is remembered — it opens pre-loaded on the left, subtracted from the right.
            var left = TroopRoster.CreateDummyTroopRoster();
            var leftPrisoners = TroopRoster.CreateDummyTroopRoster();
            var right = MobileParty.MainParty.MemberRoster.CloneRosterData();
            var rightPrisoners = TroopRoster.CreateDummyTroopRoster();
            if (_pickedTeam != null)
            {
                var live = ToDictionary(right);
                foreach (var el in _pickedTeam.GetTroopRoster())
                {
                    if (el.Character == null || el.Character == CharacterObject.PlayerCharacter) continue;
                    if (!live.TryGetValue(el.Character, out var have)) continue;
                    var take = Math.Min(el.Number, have.Number);
                    if (take <= 0) continue;
                    var takeWounded = Math.Min(Math.Min(el.WoundedNumber, have.Wounded), take);
                    right.AddToCounts(el.Character, -take, false, -takeWounded);
                    left.AddToCounts(el.Character, take, false, takeWounded);
                }
            }
            else if (_config.AutoSplitInHalf)
            {
                AutoSplitInHalf(left, right);
            }
            PartyScreenHelper.OpenScreenWithDummyRoster(
                left, leftPrisoners, right, rightPrisoners,
                new TextObject("{=TB_team_opponents}Training opponents"),
                MobileParty.MainParty.Name,
                MobileParty.MainParty.Party.PartySizeLimit,
                MobileParty.MainParty.Party.PartySizeLimit,
                PickerDoneCondition,
                PickerClosed,
                PickerTransferable);
        }

        /// <summary>Deals the company in half before the picker opens: every companion and every man
        /// flips a fair coin for a side (the player never crosses). Just a PRE-DEAL of the picker's
        /// rosters — the player still edits and confirms; Cancel still discards everything.</summary>
        private static void AutoSplitInHalf(TroopRoster left, TroopRoster right)
        {
            // GetTroopRoster hands out the LIVE internal list — copy before mutating the roster.
            var stacks = new List<TroopRosterElement>(right.GetTroopRoster());
            foreach (var el in stacks)
            {
                var character = el.Character;
                if (character == null || character == CharacterObject.PlayerCharacter) continue;
                var wounded = el.WoundedNumber;
                var healthy = el.Number - wounded;
                var takeHealthy = CoinFlips(healthy);
                var takeWounded = CoinFlips(wounded);
                var take = takeHealthy + takeWounded;
                if (take <= 0) continue;
                right.AddToCounts(character, -take, false, -takeWounded);
                left.AddToCounts(character, take, false, takeWounded);
            }
            // The drill needs a healthy man on EACH side. If every coin fell one way, walk one back.
            if (left.TotalHealthyCount < 1) MoveOneHealthyNonPlayer(right, left);
            if (right.TotalHealthyCount < 1) MoveOneHealthyNonPlayer(left, right);
        }

        private static int CoinFlips(int men)
        {
            var crossed = 0;
            for (var i = 0; i < men; i++)
                if (MBRandom.RandomFloat < 0.5f) crossed++;
            return crossed;
        }

        private static void MoveOneHealthyNonPlayer(TroopRoster from, TroopRoster to)
        {
            foreach (var el in new List<TroopRosterElement>(from.GetTroopRoster()))
            {
                if (el.Character == null || el.Character == CharacterObject.PlayerCharacter) continue;
                if (el.Number - el.WoundedNumber < 1) continue;
                from.AddToCounts(el.Character, -1, false, 0);
                to.AddToCounts(el.Character, 1, false, 0);
                return;
            }
        }

        private static bool PickerTransferable(CharacterObject character, PartyScreenLogic.TroopType type, PartyScreenLogic.PartyRosterSide side, PartyBase leftOwnerParty)
        {
            // Everyone may cross to the opposing half except the player — you command your side.
            return character != CharacterObject.PlayerCharacter;
        }

        private static Tuple<bool, TextObject> PickerDoneCondition(TroopRoster leftMemberRoster, TroopRoster leftPrisonRoster, TroopRoster rightMemberRoster, TroopRoster rightPrisonRoster, int leftLimitNum, int rightLimitNum)
        {
            try
            {
                if (leftMemberRoster == null || leftMemberRoster.TotalHealthyCount < 1)
                    return new Tuple<bool, TextObject>(false, new TextObject("{=TB_pick_need_opp}Send at least one healthy man to the opposing half."));
                if (rightMemberRoster == null || rightMemberRoster.TotalHealthyCount < 1)
                    return new Tuple<bool, TextObject>(false, new TextObject("{=TB_pick_need_own}Keep at least one healthy soul on your side."));
                return new Tuple<bool, TextObject>(true, TextObject.GetEmpty());
            }
            catch
            {
                // A throwing condition must never lock the Done button shut.
                return new Tuple<bool, TextObject>(true, TextObject.GetEmpty());
            }
        }

        private void PickerClosed(PartyBase leftOwnerParty, TroopRoster leftMemberRoster, TroopRoster leftPrisonRoster, PartyBase rightOwnerParty, TroopRoster rightMemberRoster, TroopRoster rightPrisonRoster, bool fromCancel)
        {
            // This fires MID state-transition (the party screen is still popping) — touch nothing of
            // the menu here; just record the pick and let the next tick re-enter the muster menu.
            try
            {
                if (!fromCancel && leftMemberRoster != null && leftMemberRoster.TotalManCount > 0)
                {
                    _pickedTeam = leftMemberRoster.CloneRosterData();
                    _mockEnemyTeam = null; // one foe at a time — the split pick displaces the phantom one
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Training Battles: opposing half set — " + _pickedTeam.TotalHealthyCount + " able men."));
                }
                else if (fromCancel)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Training Battles: selection closed without changes."));
                }
            }
            catch { }
            _returnToMenuPending = true;
        }

        // ------------------------------ the mock enemy (developer) ------------------------------

        /// <summary>The composer: ONE party screen over every fighting man the game knows — no
        /// culture door first (Anton, 2026.07.24: pick from everything at once; mixing cultures is
        /// then just picking). The pool side is a synthetic supply of every culture's troop tree,
        /// main cultures first, each tree in tier order. Everything stays a dummy roster — nothing
        /// here can touch the real party. Done with an empty left side clears the pick.</summary>
        private void OpenMockEnemyComposer()
        {
            var left = TroopRoster.CreateDummyTroopRoster();
            var leftPrisoners = TroopRoster.CreateDummyTroopRoster();
            var pool = TroopRoster.CreateDummyTroopRoster();
            var rightPrisoners = TroopRoster.CreateDummyTroopRoster();
            foreach (var troop in AllTroopsOfTheWorld())
                pool.AddToCounts(troop, MockPoolPerTroop);
            if (pool.Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Training Battles: no culture offers a troop tree to compose from."));
                return;
            }
            if (_mockEnemyTeam != null)
            {
                // The previous composition opens pre-loaded — edit it or clear it.
                foreach (var el in _mockEnemyTeam.GetTroopRoster())
                {
                    if (el.Character == null) continue;
                    left.AddToCounts(el.Character, el.Number);
                }
            }
            PartyScreenHelper.OpenScreenWithDummyRoster(
                left, leftPrisoners, pool, rightPrisoners,
                new TextObject("{=TB_mock_team}Mock enemy"),
                new TextObject("{=TB_mock_pool}The world's muster rolls"),
                MockEnemyMaxMen,
                pool.TotalManCount + left.TotalManCount,
                MockPickerDoneCondition,
                MockPickerClosed,
                (character, _, _, _) => true);
        }

        /// <summary>Every fighting man of every culture that fields a troop tree — main cultures
        /// first (alphabetical), the bandit cultures after, each culture's tree in tier order.</summary>
        private static List<CharacterObject> AllTroopsOfTheWorld()
        {
            var result = new List<CharacterObject>();
            try
            {
                var cultures = new List<CultureObject>();
                foreach (var culture in TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObjectTypeList<CultureObject>())
                {
                    if (culture != null) cultures.Add(culture);
                }
                cultures.Sort((a, b) => a.IsMainCulture != b.IsMainCulture
                    ? (a.IsMainCulture ? -1 : 1)
                    : string.Compare(a.Name?.ToString(), b.Name?.ToString(), StringComparison.Ordinal));
                var seen = new HashSet<CharacterObject>();
                foreach (var culture in cultures)
                    foreach (var troop in TroopTreeOf(culture))
                        if (seen.Add(troop)) result.Add(troop);
            }
            catch { }
            return result;
        }

        /// <summary>The culture's fighting men: breadth-first over the upgrade tree from the
        /// regular and noble lines (and the bandit line where the culture has one), sorted by tier.</summary>
        private static List<CharacterObject> TroopTreeOf(CultureObject culture)
        {
            var seen = new HashSet<CharacterObject>();
            var queue = new Queue<CharacterObject>();
            var seeds = new[]
            {
                culture.BasicTroop, culture.EliteBasicTroop,
                culture.BanditBandit, culture.BanditRaider, culture.BanditChief, culture.BanditBoss,
            };
            foreach (var seed in seeds)
                if (seed != null && seen.Add(seed)) queue.Enqueue(seed);
            while (queue.Count > 0)
            {
                var troop = queue.Dequeue();
                var targets = troop.UpgradeTargets;
                if (targets == null) continue;
                foreach (var target in targets)
                    if (target != null && seen.Add(target)) queue.Enqueue(target);
            }
            var result = new List<CharacterObject>(seen);
            result.Sort((a, b) => a.Tier != b.Tier
                ? a.Tier.CompareTo(b.Tier)
                : string.Compare(a.Name?.ToString(), b.Name?.ToString(), StringComparison.Ordinal));
            return result;
        }

        private static Tuple<bool, TextObject> MockPickerDoneCondition(TroopRoster leftMemberRoster, TroopRoster leftPrisonRoster, TroopRoster rightMemberRoster, TroopRoster rightPrisonRoster, int leftLimitNum, int rightLimitNum)
        {
            // An empty enemy side is a valid Done — it clears the composition.
            return new Tuple<bool, TextObject>(true, TextObject.GetEmpty());
        }

        private void MockPickerClosed(PartyBase leftOwnerParty, TroopRoster leftMemberRoster, TroopRoster leftPrisonRoster, PartyBase rightOwnerParty, TroopRoster rightMemberRoster, TroopRoster rightPrisonRoster, bool fromCancel)
        {
            // Mirrors PickerClosed: this fires MID state-transition — record only, act on next tick.
            try
            {
                if (!fromCancel)
                {
                    if (leftMemberRoster != null && leftMemberRoster.TotalManCount > 0)
                    {
                        _mockEnemyTeam = leftMemberRoster.CloneRosterData();
                        _pickedTeam = null; // one foe at a time — the phantom displaces the split pick
                        InformationManager.DisplayMessage(new InformationMessage(
                            "Training Battles: mock enemy composed — " + _mockEnemyTeam.TotalHealthyCount + " phantoms."));
                    }
                    else
                    {
                        _mockEnemyTeam = null;
                        InformationManager.DisplayMessage(new InformationMessage(
                            "Training Battles: the mock enemy was dismissed."));
                    }
                }
            }
            catch { }
            _returnToMenuPending = true;
        }

        // ------------------------------ the ship windows ------------------------------

        /// <summary>"Divide the ships" shows only where it means something: afloat, a fleet of
        /// two or more, the split drill on. Without a divided company it sits disabled — the
        /// default split follows the men, so the men must be divided first.</summary>
        private bool ShipDivideCondition(MenuCallbackArgs args)
        {
            if (!_config.EnableSplitTraining) return false;
            if (!MainPartyAtSea() || MobileParty.MainParty.Ships.Count < 2) return false;
            args.optionLeaveType = GameMenuOption.LeaveType.Manage;
            var shipCount = MobileParty.MainParty.Ships.Count;
            string now;
            if (_pickedTeam == null || _pickedTeam.TotalHealthyCount < 1)
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject("{=TB_tip_ships_first}Divide the men first — the "
                    + "fleet divides around the halves.");
                now = "divide the men first";
            }
            else if (_shipDividePick != null)
            {
                var crossing = CountValidManualCrossings();
                now = crossing + " of " + shipCount + " hulls opposite (your pick)";
                args.Tooltip = new TextObject("{=TB_tip_ships_set}Your hulls, your call — these "
                    + "very ships sail opposite. Select again to change the division, or reset "
                    + "it to follow the men.");
            }
            else
            {
                now = "following the men";
                args.Tooltip = new TextObject("{=TB_tip_ships}Choose WHICH hulls the opposing "
                    + "half sails. Left alone, the fleet divides itself in proportion to the "
                    + "men — the flagship always stays with you.");
            }
            MBTextManager.SetTextVariable("TB_SHIPS_NOW", now, false);
            return true;
        }

        /// <summary>How many hulls of the manual pick still exist in the live fleet (ships can be
        /// sold or sunk between the pick and the muster) — the menu label's honest number.</summary>
        private int CountValidManualCrossings()
        {
            var count = 0;
            try
            {
                if (_shipDividePick == null) return 0;
                foreach (var ship in _shipDividePick)
                    if (MobileParty.MainParty.Ships.Contains(ship)) count++;
            }
            catch { }
            return count;
        }

        private void OpenShipDivide()
        {
            try
            {
                var main = MobileParty.MainParty;
                if (_pickedTeam == null || main.Ships.Count < 2) return;
                var ships = new List<Ship>(main.Ships);
                var flagship = 0;
                for (var i = 1; i < ships.Count; i++)
                {
                    try { if (ships[i].FlagshipScore > ships[flagship].FlagshipScore) flagship = i; }
                    catch { }
                }
                var opponentMen = _pickedTeam.TotalHealthyCount;
                var yourMen = Math.Max(main.MemberRoster.TotalHealthyCount - opponentMen, 1);
                var vm = new UI.ShipDivideVM(ships, flagship, yourMen, opponentMen, _shipDividePick,
                    pick =>
                    {
                        _shipDividePick = pick;
                        TbLog.Info("ships", pick == null
                            ? "divide: following the men"
                            : "divide: " + pick.Count + " hulls picked to cross");
                        UI.TrainingWindow.Close();
                        try { GameMenu.SwitchToMenu(MenuId); } catch { }
                    },
                    () =>
                    {
                        UI.TrainingWindow.Close();
                        try { GameMenu.SwitchToMenu(MenuId); } catch { }
                    });
                UI.TrainingWindow.Open("TrainingBattlesShipDivide", vm, vm.ExecuteCancel);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Training Battles: the ship-divide window could not open (" + ex.Message + ")."));
            }
        }

        /// <summary>The phantom shipyard's door: afloat with a composed mock enemy. Ashore the
        /// phantoms march on their own feet and the option stays out of the way.</summary>
        private bool MockFleetCondition(MenuCallbackArgs args)
        {
            if (!_config.EnableMockEnemyTraining) return false;
            if (!MainPartyAtSea()) return false;
            args.optionLeaveType = GameMenuOption.LeaveType.Manage;
            string now;
            if (_mockEnemyTeam == null || _mockEnemyTeam.TotalHealthyCount < 1)
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject("{=TB_tip_mock_fleet_first}Compose the mock enemy "
                    + "first — the fleet is laid down for its men.");
                now = "compose the enemy first";
            }
            else if (_mockFleetPick != null && _mockFleetPick.Count > 0)
            {
                var hulls = 0;
                foreach (var pair in _mockFleetPick) hulls += pair.Value;
                now = hulls + (hulls == 1 ? " hull" : " hulls") + " laid down";
                args.Tooltip = new TextObject("{=TB_tip_mock_fleet_set}The phantom fleet stands "
                    + "ready on the slips. Select again to rework it.");
            }
            else
            {
                now = "no hulls yet";
                args.Tooltip = new TextObject("{=TB_tip_mock_fleet}Give the phantoms their ships — "
                    + "any culture's hulls, with fittings if you wish. At sea the mock enemy "
                    + "cannot fight without them.");
            }
            MBTextManager.SetTextVariable("TB_MOCK_FLEET_NOW", now, false);
            return true;
        }

        private void OpenFleetCompose()
        {
            try
            {
                var phantomMen = _mockEnemyTeam?.TotalHealthyCount ?? 0;
                if (phantomMen < 1) return;
                var vm = new UI.FleetComposeVM(phantomMen, _mockFleetPick, _mockFleetTier,
                    (pick, tier) =>
                    {
                        _mockFleetPick = pick.Count > 0 ? pick : null;
                        _mockFleetTier = tier;
                        var hulls = 0;
                        foreach (var pair in pick) hulls += pair.Value;
                        TbLog.Info("ships", "phantom fleet: " + hulls + " hulls, tier " + tier);
                        UI.TrainingWindow.Close();
                        try { GameMenu.SwitchToMenu(MenuId); } catch { }
                    },
                    () =>
                    {
                        UI.TrainingWindow.Close();
                        try { GameMenu.SwitchToMenu(MenuId); } catch { }
                    });
                UI.TrainingWindow.Open("TrainingBattlesFleetCompose", vm, vm.ExecuteCancel);
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Training Battles: the shipyard window could not open (" + ex.Message + ")."));
            }
        }

        // ------------------------------ the battle ------------------------------

        private void BeginBattle(bool playerDefends)
        {
            LaunchTraining(playerDefends, simulate: false);
        }

        private void LaunchTraining(bool playerDefends, bool simulate)
        {
            try
            {
                _launching = true;
                LaunchTrainingCore(playerDefends, simulate);
            }
            catch (Exception ex)
            {
                TbLog.Info("drill", "LAUNCH FAILED: " + ex);
                InformationManager.DisplayMessage(new InformationMessage(
                    "Training Battles: the drill could not start (" + ex.Message + ")."));
                AbortLiveBattle();
            }
            finally
            {
                _launching = false;
            }
        }

        private void LaunchTrainingCore(bool playerDefends, bool simulate)
        {
            var picked = _pickedTeam;
            var mock = _pickedTeam == null ? _mockEnemyTeam : null;
            if ((picked == null || picked.TotalHealthyCount < 1)
                && (mock == null || mock.TotalHealthyCount < 1))
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Training Battles: divide the men — or compose a mock enemy — first."));
                return;
            }
            if (!DrillCooldownReady(out _)) return;
            if (_siegeSettlement != null)
            {
                if (!CanCastleMusterNow(out var castleReason))
                {
                    InformationManager.DisplayMessage(new InformationMessage("Training Battles: " + castleReason));
                    return;
                }
            }
            else if (!CanMusterNow(out var reason))
            {
                InformationManager.DisplayMessage(new InformationMessage("Training Battles: " + reason));
                return;
            }
            // The sea's own gates, checked before anything is touched: at sea a mock enemy needs
            // a laid-down phantom fleet (a shipless side loses the naval event instantly), and
            // a fleet of one cannot fight itself. The menu conditions already say both — these
            // catch a pick carried from shore out onto the water.
            if (MainPartyAtSea())
            {
                if (mock != null && (_mockFleetPick == null || _mockFleetPick.Count == 0))
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Training Battles: the phantoms have no hulls — lay down their fleet first."));
                    return;
                }
                if (mock == null && MobileParty.MainParty.Ships.Count < 2)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Training Battles: a sea drill needs at least two hulls to divide."));
                    return;
                }
            }

            // The pay-chest is counted over the WHOLE company (both halves drill), before the split.
            var cost = ComputeTrainingCost();
            if (cost > 0 && (Hero.MainHero?.Gold ?? 0) < cost)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Training Battles: the pay-chest wants " + cost + " denars — the purse is short."));
                return;
            }

            var main = MobileParty.MainParty;
            var opponent = CreateOpponentParty(mock != null);
            if (opponent == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("Training Battles: could not raise the opposing half."));
                return;
            }

            // Who holds which party role, BEFORE anyone crosses over: the engine wipes a hero's
            // roles the instant they change party (Hero.SetPartyBelongedTo →
            // RemoveAllPartyRolesOfHero), and the losing half's fugitive path wipes them too —
            // the aftermath hands the roles back once everyone is home.
            SnapshotPartyRoles();

            if (mock != null)
            {
                // The phantoms muster fresh from the rolls — no man and no XP leaves the real
                // roster; the whole company stands on the player's side.
                foreach (var el in mock.GetTroopRoster())
                {
                    if (el.Character == null || el.Character.IsHero) continue;
                    opponent.MemberRoster.AddToCounts(el.Character, el.Number);
                }
                if (opponent.MemberRoster.TotalHealthyCount < 1 || main.MemberRoster.TotalHealthyCount < 1)
                {
                    DestroyOpponentParty(opponent);
                    _mockEnemyTeam = null;
                    _heroRolesBefore.Clear(); // nobody crossed — nothing to hand back
                    InformationManager.DisplayMessage(new InformationMessage("Training Battles: the mock enemy could not be formed."));
                    return;
                }
            }
            else
            {
                // Move the picked men across — clamped against the live roster so a stale pick can
                // never take more than the party truly has.
                var have = ToDictionary(main.MemberRoster);
                var moved = 0;
                foreach (var el in picked!.GetTroopRoster())
                {
                    if (el.Character == null || el.Character == CharacterObject.PlayerCharacter) continue;
                    if (!have.TryGetValue(el.Character, out var live)) continue;
                    var take = Math.Min(el.Number, live.Number);
                    if (take <= 0) continue;
                    var takeWounded = Math.Min(Math.Min(el.WoundedNumber, live.Wounded), take);
                    // The men's share of the stack's XP crosses with them (the game clamps a stack's
                    // XP to men × upgrade cost — leaving it all behind would see it clamped away).
                    var xpShare = (!el.Character.IsHero && live.Number > 0)
                        ? (int)((long)live.Xp * take / live.Number)
                        : 0;
                    main.MemberRoster.AddToCounts(el.Character, -take, false, -takeWounded, -xpShare);
                    opponent.MemberRoster.AddToCounts(el.Character, take, false, takeWounded, xpShare);
                    moved += take;
                }
                if (moved == 0 || main.MemberRoster.TotalHealthyCount < 1)
                {
                    // Nothing (or everything) crossed over — put it all back and stand down.
                    MergePartyBackIntoMain(opponent);
                    DestroyOpponentParty(opponent);
                    RestorePartyRoles(); // any hero who crossed and merged back was already demoted
                    _pickedTeam = null;
                    InformationManager.DisplayMessage(new InformationMessage("Training Battles: the halves could not be formed."));
                    return;
                }
            }

            // One company, one spirit: the opposing half fights at the SAME party morale as
            // yours. Every agent's starting battle morale carries (party morale - 50) / 2
            // (CampaignAgentComponent.GetMoraleAddition), and the temp party is a freshly
            // minted bandit party — no food (starving penalty), a tiny bandit size limit
            // (overcrowding penalty) — so without this your own men fought ~20-30 initial
            // morale worse from the opposite bank (Anton's dim-eagles catch, 2026.07.25).
            MatchOpponentMorale(main, opponent);

            // The sea drill divides the FLEET as the men were divided — the player's own hand
            // (the ship-divide window) or, left alone, proportional to each side's healthy
            // crew — the flagship never crossing. Every OWN hull's health is snapshotted first:
            // the aftermath re-owns and re-heals them all, so a drill can sink nothing for
            // keeps. A MOCK sea drill splits nothing — the whole fleet stays with the player
            // (snapshotted all the same) and the phantoms sail CONJURED hulls, minted from the
            // shipyard composition and dissolved afterward.
            _fleetSnapshot.Clear();
            if (MainPartyAtSea())
            {
                if (mock == null)
                {
                    SplitFleet(main, opponent);
                }
                else
                {
                    SnapshotOwnFleet(main);
                    BuildPhantomFleet(opponent);
                    if (opponent.Ships.Count == 0)
                    {
                        // The shipyard failed — a shipless side loses the naval event instantly,
                        // so stand down honestly rather than start a rigged fight.
                        DestroyOpponentParty(opponent);
                        _heroRolesBefore.Clear();
                        InformationManager.DisplayMessage(new InformationMessage(
                            "Training Battles: the phantom fleet could not be launched."));
                        return;
                    }
                }
            }
            _shipDividePick = null; // the division served its drill; the next one starts fresh

            // The men are paid BEFORE the first bruise; an abort refunds the chest in full.
            if (cost > 0)
            {
                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, cost);
                _chargedCost = cost;
            }

            // Every hero's health walking in — the aftermath heals them back toward
            // HeroHealthRestorePercent of max, but never above this mark.
            _heroHpBefore.Clear();
            SnapshotHeroHealth(main.MemberRoster);
            SnapshotHeroHealth(opponent.MemberRoster);

            // The baggage train before the fight: whatever appears above this afterward is drill
            // loot — vanilla's or any loot mod's — and the aftermath removes it. (Our reward model
            // zeroes vanilla's loot rolls, but Harmony loot mods hook the same battle commit we
            // deliberately let run for the XP books — Anton caught BannerLoot paying out.)
            _itemSnapshot = SnapshotItems(main.Party.ItemRoster);

            _opponentParty = opponent;
            _opponentIsMockEnemy = mock != null;
            _pickedTeam = null;
            _mockEnemyTeam = null;
            // NOT CloneRosterData: the game's clone silently drops each stack's Xp (counts and
            // wounded only) — zeroed snapshots made the aftermath treat the ENTIRE pool as "drill
            // earnings" and tax it to the kept-percent, eating stored upgrades every training.
            _mainSnapshot = CloneWithXp(main.MemberRoster);
            // The phantoms are nobody's men: an empty opponent snapshot keeps the aftermath's
            // restore/XP arithmetic (and the scattered-hero walk-back) to OUR side only.
            _opponentSnapshot = mock != null
                ? TroopRoster.CreateDummyTroopRoster()
                : CloneWithXp(opponent.MemberRoster);
            _prisonSnapshot = CloneWithXp(main.PrisonRoster);
            TrainingActive = true;
            _checkResults = true;
            _aftermathReady = false;
            _pendingPlayerWon = null;
            _battleDead.Clear();
            _battleDeadHarvested = false;

            // Arm the chosen battlefield (if the player picked one and it still fits where the
            // party now stands). The scene model gives it out on the next scene query — which is
            // OUR OpenBattleMission call below, or, on the send-troops road, a Break In from the
            // simulation. It self-clears on read and at map-event end, so nothing leaks.
            if (_chosenSceneId != null)
            {
                var stillFits = false;
                foreach (var scene in TrainingGroundPool(out _))
                    if (scene.SceneID == _chosenSceneId) { stillFits = true; break; }
                if (stillFits)
                {
                    Models.TrainingBattlesSceneModel.PendingSceneId = _chosenSceneId;
                }
                else
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Training Battles: the chosen battlefield no longer fits this ground — fate picks instead."));
                }
                _chosenSceneId = null;
            }

            TbLog.Info("drill", "begin | " + (playerDefends ? "player defends" : "player attacks")
                + " | atSea " + MainPartyAtSea() + " | siege " + (_siegeSettlement?.Name?.ToString() ?? "no")
                + " | simulate " + simulate
                + " | " + Officers.XpOfficer(MobileParty.MainParty, MainPartyAtSea()).Describe()
                + " | " + Officers.SurgeonOfficer(MobileParty.MainParty).Describe());

            // The CASTLE SIEGE road: its own encounter shape (a real SiegeEvent around the same
            // temp party) and the siege mission with the engineer's engines. Shares everything
            // above — validation, the crossing, morale, pay, snapshots, flags.
            if (_siegeSettlement != null)
            {
                LaunchSiegeBattle(opponent, playerDefends);
                return;
            }

            // The vanilla forced-battle recipe (Company of Trouble quest); a throw here is caught by
            // LaunchTraining and unwinds honestly: men home, party gone, no cooldown burned.
            PlayerEncounter.Start();
            PlayerEncounter.Current.SetupFields(
                playerDefends ? opponent.Party : PartyBase.MainParty,
                playerDefends ? PartyBase.MainParty : opponent.Party);
            PlayerEncounter.StartBattle();

            if (simulate)
            {
                // Vanilla's own "send troops" road (MenuHelper.EncounterOrderAttack): leave the menu
                // and open the battle-simulation view — live casualties, and Break In stays available.
                // The aftermath is triggered by MapEventEnded + the tick below, since our menu is gone.
                GameMenu.ExitToLast();
                PlayerEncounter.InitSimulation(null, null);
                if (PlayerEncounter.Current?.BattleSimulation != null
                    && Game.Current.GameStateManager.ActiveState is MapState mapState)
                {
                    mapState.StartBattleSimulation();
                }
                return;
            }

            // The map event is live now, so ITS naval verdict is the truth (IsNavalMapEvent =
            // the event position is off land) — the scene query and the mission opener must
            // agree with it, or a sea scene opens under the land opener (or worse, reversed).
            var navalEvent = PlayerEncounter.IsNavalEncounter();
            var mapSceneWrapper = Campaign.Current.MapSceneWrapper;
            var position = MobileParty.MainParty.Position;
            var mapPatch = mapSceneWrapper.GetMapPatchAtPosition(in position);
            var sceneId = Campaign.Current.Models.SceneModel.GetBattleSceneForMapPatch(mapPatch, navalEvent);
            // The patch-aware record makes deployment DETERMINISTIC (same lines every drill, and
            // the same lines the scout ride previews). The old string overload carried no patch
            // data, and without it the game picks a random spawn path and pivot per battle.
            // The pinned battle hour (if any) rides in the record's atmosphere.
            var record = ScoutMission.CreatePatchAwareRecord(
                sceneId, Models.TrainingBattlesMapWeatherModel.EffectiveBattleHour(_config));
            if (navalEvent)
                CampaignMission.OpenNavalBattleMission(record);
            else
                CampaignMission.OpenBattleMission(record);
        }

        // ------------------------------ the siege battle ------------------------------

        /// <summary>The castle muster's own validation: the company must stand inside the owned,
        /// unbesieged castle. (Unlike the field muster, a LIVE PlayerEncounter is expected here —
        /// being inside a settlement IS an encounter; the launch stands it down itself.)</summary>
        private bool CanCastleMusterNow(out string reason)
        {
            var settlement = _siegeSettlement;
            var main = MobileParty.MainParty;
            if (settlement == null || main == null) { reason = "no castle to drill at."; return false; }
            if (main.CurrentSettlement != settlement) { reason = "the company must stand inside " + settlement.Name + "."; return false; }
            if (settlement.OwnerClan != Clan.PlayerClan) { reason = "these walls are not yours."; return false; }
            if (settlement.SiegeEvent != null) { reason = "a real siege owns these walls."; return false; }
            if (main.MapEvent != null) { reason = "you are already engaged."; return false; }
            if (main.Army != null) { reason = "the army's banners answer to sterner plans — leave the army to drill."; return false; }
            reason = string.Empty;
            return true;
        }

        /// <summary>The siege drill's encounter and mission. Shape verified against this game
        /// version's decompiled corpus: a REAL SiegeEvent wraps the fight (the siege mission's
        /// engine-writeback reads the defender leader's SiegeEvent at mission end — a null there
        /// is a crash), PlayerEncounter.StartBattle makes the event a Siege on its own (the
        /// defender is a fortification), and the map event's private _keepSiegeEvent flag —
        /// set the moment the event exists — keeps FinalizeEvent from ever dispatching
        /// SiegeCompleted, so the capture/sack/devastation machinery never sees a drill. The
        /// mission gets its engines as plain MissionSiegeWeapon data (the engineer's bench);
        /// with the siege event's own construction lists empty, vanilla's writeback no-ops.</summary>
        private void LaunchSiegeBattle(MobileParty opponent, bool playerDefends)
        {
            var siege = _siegeSettlement!;
            var main = MobileParty.MainParty;

            // Walking in: the walls' health (only campaign bombardment ever damages walls, and
            // the drill never ticks one — but honest is cheap) and every friendly party the
            // event will drag onto the defense (garrison, militia, guesting lords).
            _wallSnapshot = new List<float>(siege.SettlementWallSectionHitPointsRatioList);
            SnapshotFriendlySettlementParties(siege);
            TbLog.Info("siege", "snapshots taken | walls " + _wallSnapshot.Count
                + " sections | friendly parties " + _friendPartySnapshots.Count);

            // BOTH roads below walk vanilla's own exercised paths. The first build invented a
            // shape of its own (finish the settlement encounter, hand-build a new one) and
            // hard-crashed mid-launch — never leave the beaten path around sieges.
            TaleWorlds.CampaignSystem.MapEvents.MapEvent mapEvent;
            if (playerDefends)
            {
                // Vanilla shape: your castle is besieged and assaulted while you sit inside.
                // The settlement-visit encounter STAYS (being inside IS an encounter, and it
                // is the one vanilla itself uses for an inside defense); the assault event is
                // raised by the same StartBattleAction an AI assault uses, and the player
                // JOINS the defense — PlayerEncounter.JoinBattle, the join_siege_event road.
                _drillSiegeEvent = CreateDrillSiegeAroundOpponent(siege, opponent);
                TbLog.Info("siege", "siege event up | besieger: the opposing half (camp-swapped)");
                StartBattleAction.ApplyStartAssaultAgainstWalls(opponent, siege);
                mapEvent = siege.Party.MapEvent;
                TbLog.Info("siege", "assault event up | type " + (mapEvent?.EventType.ToString() ?? "NULL"));
                if (mapEvent == null) throw new InvalidOperationException("the assault event did not form");
                KeepSiegeEventThroughFinalize(mapEvent);
                if (PlayerEncounter.Current != null)
                {
                    PlayerEncounter.JoinBattle(BattleSideEnum.Defender);
                }
                else
                {
                    // No settlement encounter to join (shouldn't happen inside) — seat the
                    // party on the defense directly.
                    PlayerEncounter.Start();
                    PartyBase.MainParty.MapEventSide = mapEvent.DefenderSide;
                }
                TbLog.Info("siege", "player joined the defense");
                SeatFriendliesOnTheWalls(mapEvent, siege);
            }
            else
            {
                // Vanilla shape: walk out of the gate (Finish(true) IS the leave-settlement
                // road), besiege your own walls as the player siege, and assault at once. The
                // opposing half must sit INSIDE before joining the defense, or the event
                // silently degrades to a field battle outside the walls.
                try { if (PlayerEncounter.Current != null) PlayerEncounter.Finish(); } catch { }
                TbLog.Info("siege", "walked out of the gate");
                EnterSettlementAction.ApplyForParty(opponent, siege);
                _drillSiegeEvent = Campaign.Current.SiegeEventManager.StartSiegeEvent(siege, main);
                try { PlayerSiege.StartPlayerSiege(BattleSideEnum.Attacker, isSimulation: false, siege); }
                catch { /* the siege HUD is cosmetics; the drill fights on without it */ }
                TbLog.Info("siege", "siege event up | besieger: you");
                PlayerEncounter.Start();
                PlayerEncounter.Current.SetupFields(PartyBase.MainParty, siege.Party);
                mapEvent = PlayerEncounter.StartBattle();
                TbLog.Info("siege", "assault event up | type " + (mapEvent?.EventType.ToString() ?? "NULL"));
                if (mapEvent == null) throw new InvalidOperationException("the assault event did not form");
                KeepSiegeEventThroughFinalize(mapEvent);
                if (opponent.Party.MapEventSide == null)
                    opponent.Party.MapEventSide = mapEvent.DefenderSide;
                SeatFriendliesOnTheWalls(mapEvent, siege);
            }
            TbLog.Info("siege", "sides seated | attacker " + (mapEvent.AttackerSide?.LeaderParty?.Name?.ToString() ?? "?")
                + " | defender " + (mapEvent.DefenderSide?.LeaderParty?.Name?.ToString() ?? "?"));

            // The mission: the castle's own scene at its true wall level, the walls' true
            // health, and the engineer's engines on both sides.
            var wallLevel = 1;
            try { wallLevel = siege.Town?.GetWallLevel() ?? 1; } catch { }
            var scene = siege.LocationComplex.GetLocationWithId("center").GetSceneName(wallLevel);
            var atk = BuildMissionWeapons(_siegeAtkPick);
            var def = BuildMissionWeapons(_siegeDefPick);
            var hasTower = false;
            foreach (var weapon in atk)
                if (weapon.Type == DefaultSiegeEngineTypes.SiegeTower) { hasTower = true; break; }
            _siegeAtkPick = null; // the bench served its drill; the next one starts fresh
            _siegeDefPick = null;
            TbLog.Info("siege", "assault opens | " + siege.Name + " | walls L" + wallLevel
                + " | scene " + scene + " | atk engines " + atk.Count + " | def engines " + def.Count
                + " | player " + (playerDefends ? "defends" : "attacks"));
            CampaignMission.OpenSiegeMissionWithDeployment(scene,
                siege.SettlementWallSectionHitPointsRatioList.ToArray(),
                hasTower, atk, def, isPlayerAttacker: !playerDefends, wallLevel);
        }

        /// <summary>The engineer's bench as the mission's own data: one MissionSiegeWeapon per
        /// engine, at full health — the same struct vanilla mints from a real siege camp.</summary>
        private static List<MissionSiegeWeapon> BuildMissionWeapons(List<KeyValuePair<SiegeEngineType, int>>? pick)
        {
            var result = new List<MissionSiegeWeapon>();
            if (pick == null) return result;
            var index = 0;
            foreach (var pair in pick)
            {
                if (pair.Key == null) continue;
                for (var i = 0; i < pair.Value; i++)
                {
                    try
                    {
                        result.Add(MissionSiegeWeapon.CreateCampaignWeapon(
                            pair.Key, index++, pair.Key.BaseHitPoints, pair.Key.BaseHitPoints));
                    }
                    catch { /* one engine failing must not stop the assault */ }
                }
            }
            return result;
        }

        /// <summary>The DEFEND road's siege wrapper. A LEADERLESS party cannot found a siege:
        /// BesiegerCamp.AddSiegePartyInternal resolves the siege's leader HERO and dereferences
        /// it unguarded — our hero-less temp bandit party NRE'd there, and worse, the SiegeEvent
        /// constructor stamps Settlement.SiegeEvent BEFORE that line, so the failed launch left
        /// a half-built ghost siege on the castle that crashed the campaign minutes later
        /// (crash round 2, 2026.07.25 21:32). So: the siege is FOUNDED by the hero-led MAIN
        /// party (the exact shape the attack road already proved), and the camp's membership is
        /// then swapped to the temp party by direct field writes — the temp party ends up
        /// member, leader-party and faction of the camp (so the mission-end engine writeback's
        /// attackerLeader.SiegeEvent resolves through it, and the defenders' hostility checks
        /// read the bandit faction), while the main party walks free. Field writes deliberately
        /// skip the property setters: the leader arithmetic that cannot handle a hero-less
        /// party, and the remove-path that would finalize an emptied camp.</summary>
        private SiegeEvent CreateDrillSiegeAroundOpponent(Settlement siege, MobileParty opponent)
        {
            var main = MobileParty.MainParty;
            var lastAttacker = siege.LastAttackerParty; // founding a siege stamps this — restore below
            var siegeEvent = Campaign.Current.SiegeEventManager.StartSiegeEvent(siege, main);
            try
            {
                var camp = siegeEvent.BesiegerCamp;
                var campOnParty = typeof(MobileParty).GetField("_besiegerCamp", BindingFlags.Instance | BindingFlags.NonPublic);
                var partiesField = typeof(BesiegerCamp).GetField("_besiegerParties", BindingFlags.Instance | BindingFlags.NonPublic);
                var leaderField = typeof(BesiegerCamp).GetField("_leaderParty", BindingFlags.Instance | BindingFlags.NonPublic);
                var factionField = typeof(BesiegerCamp).GetField("_faction", BindingFlags.Instance | BindingFlags.NonPublic);
                if (campOnParty == null || partiesField == null || leaderField == null || factionField == null)
                    throw new InvalidOperationException("the siege camp's fields moved — game update?");
                if (!(partiesField.GetValue(camp) is System.Collections.IList parties))
                    throw new InvalidOperationException("the camp's party list is not a list");
                campOnParty.SetValue(main, null);
                parties.Remove(main);
                if (!parties.Contains(opponent)) parties.Add(opponent);
                leaderField.SetValue(camp, opponent);
                factionField.SetValue(camp, opponent.MapFaction);
                campOnParty.SetValue(opponent, camp);
                try { siege.LastAttackerParty = lastAttacker; } catch { }
                TbLog.Info("siege", "camp swapped to the opposing half | camp leader "
                    + (camp.LeaderParty?.Name?.ToString() ?? "NULL"));
                return siegeEvent;
            }
            catch
            {
                // The swap failed — tear the WHOLE siege down before rethrowing; a half-built
                // siege left on the settlement is exactly what crashed round 2.
                try { siegeEvent.FinalizeSiegeEvent(); } catch { }
                try { if (siege.SiegeEvent != null) siege.FinalizeSiegeEvent(); } catch { }
                try { siege.LastAttackerParty = lastAttacker; } catch { }
                throw;
            }
        }

        /// <summary>The last net under every siege-drill exit: if the drill's settlement STILL
        /// carries a SiegeEvent (a launch that died inside vanilla's own constructor leaves a
        /// half-built one — Settlement.SiegeEvent is stamped before the constructor finishes,
        /// while our _drillSiegeEvent never got assigned), tear it down. Idempotent.</summary>
        private void DismantleGhostSiege()
        {
            var siege = _siegeSettlement;
            if (siege?.SiegeEvent == null) return;
            TbLog.Info("siege", "ghost siege found at " + siege.Name + " — dismantling");
            try { siege.SiegeEvent.FinalizeSiegeEvent(); } catch { }
            try { if (siege.SiegeEvent != null) siege.FinalizeSiegeEvent(); } catch { }
        }

        /// <summary>Sets the map event's private _keepSiegeEvent flag the moment the event is
        /// born — vanilla's own "the siege continues past this battle" switch (public only via
        /// FinishBattleAndKeepSiegeEvent, which also ends the battle). With it set, FinalizeEvent
        /// skips the whole SiegeCompleted dispatch: no capture, no sack, no devastation — on
        /// EVERY road out, even ones vanilla walks before our aftermath runs. Reflection, no
        /// Harmony (the SweepCompanionSeparationTracker precedent); a miss costs nothing here
        /// because the aftermath restores the settlement anyway — this is the first lock of two.</summary>
        private static void KeepSiegeEventThroughFinalize(TaleWorlds.CampaignSystem.MapEvents.MapEvent mapEvent)
        {
            try
            {
                typeof(TaleWorlds.CampaignSystem.MapEvents.MapEvent)
                    .GetField("_keepSiegeEvent", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(mapEvent, true);
            }
            catch { }
        }

        /// <summary>Seats every snapshotted friendly party still inside the walls on the
        /// DEFENDER side. On the defend road (bandit besieger) the game auto-joins them and
        /// this is a no-op guard; on the ATTACK road it is load-bearing — the garrison shares
        /// the attacker's faction, so vanilla's hostility check never auto-joins them against
        /// their own lord, and without this the walls would stand empty of their keepers.</summary>
        private void SeatFriendliesOnTheWalls(TaleWorlds.CampaignSystem.MapEvents.MapEvent mapEvent, Settlement siege)
        {
            foreach (var (party, _) in _friendPartySnapshots)
            {
                try
                {
                    if (party == null || party.CurrentSettlement != siege) continue;
                    if (party.Party.MapEventSide == null)
                        party.Party.MapEventSide = mapEvent.DefenderSide;
                }
                catch { }
            }
        }

        /// <summary>Every friendly party inside the settlement that the siege event will drag
        /// onto the defense — the garrison, the militia, and any guesting lord (trade traffic
        /// mans no wall). Snapshotted with XP; their heroes' health too. The aftermath walks
        /// each one back by the same surgeon/XP arithmetic as the main party.</summary>
        private void SnapshotFriendlySettlementParties(Settlement settlement)
        {
            _friendPartySnapshots.Clear();
            try
            {
                foreach (var party in new List<MobileParty>(settlement.Parties))
                {
                    if (party == null || party == MobileParty.MainParty) continue;
                    if (party.IsCaravan || party.IsVillager) continue;
                    if (party.MemberRoster == null || party.MemberRoster.TotalManCount == 0) continue;
                    _friendPartySnapshots.Add((party, CloneWithXp(party.MemberRoster)));
                    SnapshotHeroHealth(party.MemberRoster);
                }
            }
            catch { }
        }

        /// <summary>The drill siege's send-off: the campaign-side SiegeEvent dismantled through
        /// vanilla's own FinalizeSiegeEvent (camps unhooked, the settlement's siege state and
        /// SiegeEvent reference reset, visuals dirtied). Safe to call twice; call BEFORE the
        /// menu pops — finalize may push a vanilla "the attackers left" menu the pops then clear.</summary>
        private void DismantleDrillSiege()
        {
            var siegeEvent = _drillSiegeEvent;
            _drillSiegeEvent = null;
            if (siegeEvent == null) return;
            try { siegeEvent.FinalizeSiegeEvent(); }
            catch { }
        }

        /// <summary>Walls exactly as they stood — the belt to the "only bombardment ticks damage
        /// walls" finding's braces.</summary>
        private void RestoreWalls()
        {
            var snapshot = _wallSnapshot;
            _wallSnapshot = null;
            var siege = _siegeSettlement;
            if (snapshot == null || siege == null) return;
            try
            {
                for (var i = 0; i < snapshot.Count && i < siege.SettlementWallSectionHitPointsRatioList.Count; i++)
                    siege.SetWallSectionHitPointsRatioAtIndex(i, snapshot[i]);
            }
            catch { }
        }

        /// <summary>A crash mid-siege-drill leaves the SiegeEvent in the save. On load, any siege
        /// whose besieger is a training party — or the player besieging their OWN settlement,
        /// the player-attacks drill's shape — is a drill leftover and is dismantled.</summary>
        private static void RecoverStaleDrillSieges()
        {
            try
            {
                var stale = new List<SiegeEvent>();
                foreach (var siegeEvent in Campaign.Current.SiegeEventManager.SiegeEvents)
                {
                    if (siegeEvent == null) continue;
                    var leader = siegeEvent.BesiegerCamp?.LeaderParty;
                    var trainingBesieger = leader?.StringId != null
                        && (leader.StringId.StartsWith(OpponentPartyIdPrefix, StringComparison.Ordinal)
                            || leader.StringId.StartsWith(MockEnemyPartyIdPrefix, StringComparison.Ordinal));
                    var selfSiege = leader == MobileParty.MainParty
                        && siegeEvent.BesiegedSettlement?.OwnerClan == Clan.PlayerClan;
                    // A LEADERLESS siege on a player-owned settlement is a drill ghost — a
                    // launch that died inside vanilla's own constructor left it half-built
                    // (crash round 2); no vanilla siege ever stands leaderless.
                    var ghost = leader == null
                        && siegeEvent.BesiegedSettlement?.OwnerClan == Clan.PlayerClan;
                    if (trainingBesieger || selfSiege || ghost) stale.Add(siegeEvent);
                }
                foreach (var siegeEvent in stale)
                {
                    try { siegeEvent.FinalizeSiegeEvent(); } catch { }
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Training Battles: an interrupted siege drill was found — the siege camp has been struck."));
                }
            }
            catch { }
        }

        private MobileParty? CreateOpponentParty(bool mockEnemy)
        {
            try
            {
                var hideoutSettlement = SettlementHelper.FindRandomSettlement(s => s.IsHideout);
                var hideout = hideoutSettlement?.Hideout;
                var clan = hideoutSettlement?.OwnerClan;
                if (hideout == null || clan == null) return null;
                var party = BanditPartyComponent.CreateBanditParty(
                    (mockEnemy ? MockEnemyPartyIdPrefix : OpponentPartyIdPrefix) + "_" + DateTime.UtcNow.Ticks,
                    clan, hideout, isBossParty: false, null, MobileParty.MainParty.Position);
                party.Party.SetCustomName(mockEnemy
                    ? new TextObject("{=TB_mock_name}Mock Enemy")
                    : new TextObject("{=TB_opponents_name}Training Opponents"));
                party.SetPartyUsedByQuest(isActivelyUsed: true);
                if (_config.UseOpponentBanner) ApplyOpponentLook(party, clan);
                return party;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Levels the temp party's morale to the main party's, through the same knob
        /// vanilla's events use (RecentEventsMorale — its setter clamps to ±100, plenty for the
        /// bandit-party penalties). Called AFTER the men have crossed: the party-size morale
        /// penalty depends on the roster the model sees. No restore needed — the temp party
        /// dies with the drill.</summary>
        private static void MatchOpponentMorale(MobileParty main, MobileParty opponent)
        {
            try
            {
                var delta = main.Morale - opponent.Morale;
                if (Math.Abs(delta) < 0.01f) return;
                opponent.RecentEventsMorale += delta;
            }
            catch { /* a morale mismatch is a fairness papercut, never a reason to stop a drill */ }
        }

        // ------------------------------ the fleet ------------------------------

        /// <summary>Divides the fleet for a sea drill, proportional to each side's healthy crew
        /// (<see cref="FleetSplitMath"/>), the best hull — the flagship by the game's own
        /// FlagshipScore — always staying with the player. Snapshots every hull's health FIRST:
        /// "sunk" in this game is <c>DestroyShipAction</c>, which only unhooks the owner and
        /// leaves the Ship object alive, so <see cref="RestoreFleet"/> can raise the whole fleet
        /// afterward. Moving a hull is just writing <c>Ship.Owner</c> — the setter does the
        /// roster bookkeeping on both parties.</summary>
        private void SplitFleet(MobileParty main, MobileParty opponent)
        {
            // The player's own division (the ship-divide window) outranks the arithmetic —
            // validated against the live fleet, since hulls can be sold or sunk between the
            // pick and the drill; a pick gone stale falls back to the auto split, said aloud.
            // The Owner setter edits the party's LIVE ship list — copy before the loop, the
            // same footgun as TroopRoster.GetTroopRoster.
            var ships = new List<Ship>(main.Ships);
            foreach (var ship in ships)
                _fleetSnapshot.Add((ship, ship.HitPoints, ship.SailHitPoints));
            var capacities = new List<int>(ships.Count);
            var flagship = 0;
            for (var i = 0; i < ships.Count; i++)
            {
                capacities.Add(ships[i].TotalCrewCapacity);
                try { if (ships[i].FlagshipScore > ships[flagship].FlagshipScore) flagship = i; }
                catch { }
            }
            var crossing = ManualCrossingIndices(ships, flagship)
                ?? FleetSplitMath.OpponentShips(capacities, flagship,
                    main.MemberRoster.TotalHealthyCount, opponent.MemberRoster.TotalHealthyCount);
            foreach (var index in crossing)
                ships[index].Owner = opponent.Party;
            // The event's shipless-side check and the mission's spawners read the party lists
            // we just wrote; the at-sea flag is only the map visual's truth, set for tidiness.
            try { opponent.IsCurrentlyAtSea = true; } catch { }
            try { main.SetNavalVisualAsDirty(); } catch { }
            InformationManager.DisplayMessage(new InformationMessage(
                "Training Battles: the fleet divides with the men — "
                + crossing.Count + (crossing.Count == 1 ? " hull" : " hulls") + " opposite, "
                + (ships.Count - crossing.Count) + " under your banner."));
        }

        /// <summary>The ship-divide window's pick, as indices into the live fleet — or null when
        /// no pick stands (follow the men) or it went stale enough to be no division at all
        /// (all its hulls gone, or somehow the whole fleet). The flagship never crosses even if
        /// a stale reference tries.</summary>
        private List<int>? ManualCrossingIndices(List<Ship> ships, int flagship)
        {
            if (_shipDividePick == null) return null;
            var indices = new List<int>();
            foreach (var ship in _shipDividePick)
            {
                var index = ships.IndexOf(ship);
                if (index >= 0 && index != flagship && !indices.Contains(index)) indices.Add(index);
            }
            if (indices.Count == 0 || indices.Count >= ships.Count)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Training Battles: the picked hulls are no longer in the fleet — it divides "
                    + "with the men instead."));
                return null;
            }
            if (indices.Count < _shipDividePick.Count)
                InformationManager.DisplayMessage(new InformationMessage(
                    "Training Battles: " + (_shipDividePick.Count - indices.Count)
                    + " picked hull(s) left the fleet — the rest sail as chosen."));
            return indices;
        }

        /// <summary>The mock sea drill's snapshot: nothing crosses, but the player's own hulls
        /// can still be hurt or "sunk" by the phantoms — remember every hull's health so
        /// <see cref="RestoreFleet"/> can raise and heal the whole fleet afterward.</summary>
        private void SnapshotOwnFleet(MobileParty main)
        {
            try
            {
                foreach (var ship in new List<Ship>(main.Ships))
                    _fleetSnapshot.Add((ship, ship.HitPoints, ship.SailHitPoints));
            }
            catch { }
        }

        /// <summary>Launches the phantom fleet: every hull of the shipyard composition conjured
        /// fresh (<c>new Ship(hull)</c> — the same recipe vanilla's naval quests use), dressed
        /// per slot with the best upgrade piece the chosen fittings tier affords
        /// (<see cref="PhantomFleetMath.UpgradePickIndex"/>, deterministic), marked quest-bound
        /// and untradeable, and handed to the mock party. The hulls are dissolved by
        /// <see cref="SinkPhantomFleet"/> on every exit road — they must never reach the
        /// player's fleet or the save's live world.</summary>
        private void BuildPhantomFleet(MobileParty opponent)
        {
            if (_mockFleetPick == null) return;
            var launched = 0;
            foreach (var pair in _mockFleetPick)
            {
                var hull = pair.Key;
                if (hull == null) continue;
                for (var i = 0; i < pair.Value; i++)
                {
                    try
                    {
                        var ship = new Ship(hull);
                        ship.IsTradeable = false;
                        ship.IsUsedByQuest = true;
                        FitPhantomShip(ship, _mockFleetTier);
                        ship.Owner = opponent.Party;
                        launched++;
                    }
                    catch { /* one hull failing must not scuttle the fleet */ }
                }
            }
            try { opponent.IsCurrentlyAtSea = true; } catch { }
            TbLog.Info("ships", "phantom fleet launched: " + launched + " hulls, tier " + _mockFleetTier);
            if (launched > 0)
                InformationManager.DisplayMessage(new InformationMessage(
                    "Training Battles: the phantom fleet stands out to sea — "
                    + launched + (launched == 1 ? " hull" : " hulls") + "."));
        }

        /// <summary>Dresses one conjured hull's slots for the fittings tier: per slot, the best
        /// matching piece whose harbor level the tier affords. Tier 0 leaves the hull bare.</summary>
        private static void FitPhantomShip(Ship ship, int tier)
        {
            if (tier <= 0) return;
            try
            {
                foreach (var slot in ship.ShipHull.AvailableSlots)
                {
                    try
                    {
                        var pieces = slot.Value.MatchingPieces;
                        if (pieces == null || pieces.Count == 0) continue;
                        var levels = new List<int>(pieces.Count);
                        foreach (var piece in pieces)
                            levels.Add(piece?.RequiredPortLevel ?? int.MaxValue);
                        var pick = PhantomFleetMath.UpgradePickIndex(levels, tier);
                        if (pick >= 0) ship.EquipUpgradePiece(slot.Key, pieces[pick]);
                    }
                    catch { /* a slot that will not dress sails bare */ }
                }
                ship.HitPoints = ship.MaxHitPoints;       // fittings raise the ceilings —
                ship.SailHitPoints = ship.MaxSailHitPoints; // a phantom sails at full strength
            }
            catch { }
        }

        /// <summary>The phantoms' send-off: every hull still owned by the mock party is orphaned
        /// (Owner = null — exactly what vanilla's own "sinking" leaves behind), so a conjured
        /// ship can never linger in the save or be reclaimed into the player's fleet. Run before
        /// the mock party is destroyed, on every exit road including the stale-party recovery.</summary>
        private static void SinkPhantomFleet(MobileParty party)
        {
            try
            {
                var ships = new List<Ship>(party.Ships); // Owner writes mutate the live list
                foreach (var ship in ships)
                {
                    try { ship.Owner = null; } catch { }
                }
            }
            catch { }
        }

        /// <summary>The mock sea drill's capture sweep: any hull in the player's fleet that was
        /// NOT there when the drill began is a "captured" phantom — vanilla's victory path (or a
        /// mod's) handed it over; it dissolves like its crew. The reward model already forbids
        /// ship transfers while training; this is the belt to those suspenders.</summary>
        private static void SweepForeignHulls(HashSet<Ship> ownHulls)
        {
            try
            {
                var swept = 0;
                foreach (var ship in new List<Ship>(MobileParty.MainParty.Ships))
                {
                    if (ownHulls.Contains(ship)) continue;
                    try { ship.Owner = null; swept++; } catch { }
                }
                if (swept > 0)
                {
                    MobileParty.MainParty.SetNavalVisualAsDirty();
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Training Battles: " + swept + " captured phantom hull"
                        + (swept == 1 ? "" : "s") + " dissolved — there are no spoils in sparring."));
                }
            }
            catch { }
        }

        /// <summary>Every hull comes home whole: re-owned by the main party (whether it crossed
        /// for the drill, was "captured", or "sank" — sinking only orphaned it) and healed back
        /// to the health it sailed in with. Run BEFORE the temp party is destroyed, on every
        /// exit road: the aftermath, the abort, and nowhere else — the stale-party recovery has
        /// its own <see cref="ReclaimShips"/> (a crashed session has no snapshot to heal from).</summary>
        private void RestoreFleet()
        {
            if (_fleetSnapshot.Count == 0) return;
            try
            {
                foreach (var entry in _fleetSnapshot)
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
            }
            catch { }
            _fleetSnapshot.Clear();
        }

        /// <summary>The crash road's fleet walk-back: hulls still owned by a stale temp party
        /// (ship ownership rides in the save) return to the main party as they stand — hurt
        /// hulls stay hurt, and a hull sunk in the lost session is truly gone; without a
        /// persisted snapshot there is nothing honest to heal from.</summary>
        private static void ReclaimShips(MobileParty party)
        {
            try
            {
                var ships = new List<Ship>(party.Ships); // Owner writes mutate the live list
                if (ships.Count == 0) return;
                foreach (var ship in ships)
                {
                    try { ship.Owner = PartyBase.MainParty; } catch { }
                }
                MobileParty.MainParty.SetNavalVisualAsDirty();
                InformationManager.DisplayMessage(new InformationMessage(
                    "Training Battles: " + ships.Count + (ships.Count == 1 ? " hull" : " hulls")
                    + " from the interrupted drill rejoined your fleet."));
            }
            catch { }
        }

        // ------------------------------ the training colors ------------------------------

        /// <summary>Dresses the opposing half in the training banner. The banner itself goes on the
        /// party (<c>SetCustomBanner</c> — the team's flag). The team COLORS and the men's shield
        /// heraldry, though, are read straight from the party's MAP FACTION at spawn time (verified
        /// in Mission.SpawnTroop: ClothingColor1/2 = team color = faction color pair; leaderless
        /// origins take the faction's banner) — so the lender bandit clan briefly wears our colors
        /// too, and <see cref="RestoreOpponentClanLook"/> dresses it back after the drill. The
        /// restore data is SAVED (SyncData) because clan colors persist in the save file: a crash
        /// mid-drill must not leave Calradia's looters flying an orange cross forever.</summary>
        private void ApplyOpponentLook(MobileParty party, Clan clan)
        {
            try
            {
                var banner = new Banner(_config.OpponentBannerCode);
                party.Party.SetCustomBanner(banner);
                if (_clanRestoreData.Length == 0) // never stack two restores onto one clan
                {
                    _clanRestoreData = clan.StringId + "|" + clan.Color + "|" + clan.Color2 + "|"
                        + (clan.Banner?.Serialize() ?? string.Empty);
                }
                clan.Color = banner.GetPrimaryColor();
                clan.Color2 = banner.GetFirstIconColor();
                clan.Banner = banner;
                TbLog.Info("clan", "dressed " + clan.Name + " (" + clan.StringId + ") in training colors");
            }
            catch { /* colors are decoration — a bad banner code must never stop a drill */ }
        }

        /// <summary>Undoes <see cref="ApplyOpponentLook"/>'s clan changes, whether this session made
        /// them or a crashed one did (the restore data rides in the save) — and then REBUILDS the
        /// map icon of every party of that clan. Party visuals are built once and never refreshed
        /// on a clan-banner change (vanilla never mutates clan banners, so no refresh path exists):
        /// a looter party the world spawned WHILE the drill ran — the hideout replenisher answers
        /// the temp party's destruction, and map time passes around the drill's edges — kept our
        /// orange training banner until a save/load rebuilt it (Anton's coast looters, 2026.07.25).
        /// SetVisualAsDirty is the engine's own on-load rebuild call, so this is exactly that heal,
        /// run at once.</summary>
        private void RestoreOpponentClanLook()
        {
            if (string.IsNullOrEmpty(_clanRestoreData)) return;
            try
            {
                var parts = _clanRestoreData.Split(new[] { '|' }, 4);
                if (parts.Length == 4)
                {
                    foreach (var clan in Clan.All)
                    {
                        if (clan?.StringId != parts[0]) continue;
                        if (uint.TryParse(parts[1], out var color)) clan.Color = color;
                        if (uint.TryParse(parts[2], out var color2)) clan.Color2 = color2;
                        if (parts[3].Length > 0) clan.Banner = new Banner(parts[3]);
                        TbLog.Info("clan", "restored " + clan.Name + " (" + clan.StringId + ")");
                        // One more visual sweep half a second after the map quiets down —
                        // a replenisher party spawned in this very frame window (answering
                        // the temp party's destruction) would otherwise keep the training
                        // colors it was born with (Anton's recurring orange looters).
                        _clanResweepClanId = clan.StringId;
                        _clanResweepTicks = 0;
                        foreach (var party in MobileParty.All)
                        {
                            try
                            {
                                if (party?.ActualClan == clan) party.Party.SetVisualAsDirty();
                            }
                            catch { }
                        }
                        break;
                    }
                }
            }
            catch { }
            _clanRestoreData = string.Empty;
        }

        /// <summary>The lender clan's delayed second visual sweep — half a second after the map
        /// quiets down, every party of the clan is dirtied once more, so even a party born
        /// during the aftermath's own frame window rebuilds in the clan's true colors.</summary>
        private void ResweepLenderClanVisuals()
        {
            var clanId = _clanResweepClanId;
            _clanResweepClanId = null;
            _clanResweepTicks = 0;
            if (clanId == null) return;
            try
            {
                var swept = 0;
                foreach (var party in MobileParty.All)
                {
                    try
                    {
                        if (party?.ActualClan?.StringId != clanId) continue;
                        party.Party.SetVisualAsDirty();
                        swept++;
                    }
                    catch { }
                }
                TbLog.Info("clan", "second sweep over " + clanId + " | " + swept + " parties re-dirtied");
            }
            catch { }
        }

        /// <summary>The historical healer: one visual-dirty pass over every bandit-clan party
        /// at session launch. Any looter icon that somehow kept the training colors from an
        /// earlier session's drill window rebuilds in its clan's true colors on load.</summary>
        private static void RefreshBanditClanVisuals()
        {
            try
            {
                foreach (var party in MobileParty.All)
                {
                    try
                    {
                        if (party?.ActualClan?.IsBanditFaction == true) party.Party.SetVisualAsDirty();
                    }
                    catch { }
                }
            }
            catch { }
        }

        // ------------------------------ hero health ------------------------------

        private void SnapshotHeroHealth(TroopRoster roster)
        {
            try
            {
                foreach (var el in roster.GetTroopRoster())
                {
                    var hero = el.Character?.HeroObject;
                    if (hero != null) _heroHpBefore[hero] = hero.HitPoints;
                }
            }
            catch { }
        }

        /// <summary>Nobody limps home from training — not the heroes either. Each hero is healed
        /// back to at least <see cref="ModConfig.HeroHealthRestorePercent"/> of max health, but
        /// never above what they walked in with (a drill can't be used as a free hospital), and a
        /// battle that somehow left them healthier is left alone.</summary>
        private void RestoreHeroHealth()
        {
            if (_config.HeroHealthRestorePercent <= 0) { _heroHpBefore.Clear(); return; }
            try
            {
                foreach (var pair in _heroHpBefore)
                {
                    var hero = pair.Key;
                    if (hero == null || hero.IsDead) continue;
                    var floor = Math.Min(pair.Value,
                        (int)(hero.MaxHitPoints * (_config.HeroHealthRestorePercent / 100.0)));
                    if (hero.HitPoints < floor) hero.HitPoints = floor;
                }
            }
            catch { }
            _heroHpBefore.Clear();
        }

        /// <summary>Every hero's party roles (scout, engineer, quartermaster, surgeon — and War
        /// Sails' first mate and navigator) before the drill. The engine wipes a hero's roles the
        /// moment they leave the party (Hero.SetPartyBelongedTo → RemoveAllPartyRolesOfHero,
        /// verified in the decompiled corpus), and a losing half's companions can lose them again
        /// through the fugitive scatter — either way the officer would come home demoted.</summary>
        private void SnapshotPartyRoles()
        {
            _heroRolesBefore.Clear();
            try
            {
                var main = MobileParty.MainParty;
                foreach (var el in main.MemberRoster.GetTroopRoster())
                {
                    var hero = el.Character?.HeroObject;
                    if (hero == null) continue;
                    var roles = main.GetHeroPartyRoles(hero);
                    if (roles.Count > 0) _heroRolesBefore[hero] = roles;
                }
            }
            catch { }
        }

        /// <summary>Hands every hero back the roles they held walking in — call only AFTER the
        /// merge home and the scattered-hero walk-back, so the engine sees them in the party.
        /// Re-setting a role a hero never lost is a harmless same-name overwrite, and a hero's own
        /// old role set can never trip the game's roles-per-hero cap.</summary>
        private void RestorePartyRoles()
        {
            try
            {
                var main = MobileParty.MainParty;
                foreach (var pair in _heroRolesBefore)
                {
                    var hero = pair.Key;
                    if (hero == null || !hero.IsAlive || hero.PartyBelongedTo != main) continue;
                    foreach (var role in pair.Value)
                    {
                        try { main.SetHeroPartyRole(hero, role); } catch { }
                    }
                }
            }
            catch { }
            _heroRolesBefore.Clear();
        }

        /// <summary>Every hero from the drill who is alive but no longer riding with the main party
        /// (vanilla scattered them as fugitives — "Regrouping" on the clan screen) returns to the
        /// ranks at once. Training scatters nobody.</summary>
        private static void RecoverScatteredHeroes(TroopRoster? snapshot)
        {
            if (snapshot == null) return;
            try
            {
                foreach (var el in new List<TroopRosterElement>(snapshot.GetTroopRoster()))
                {
                    var hero = el.Character?.HeroObject;
                    if (hero == null || hero == Hero.MainHero || !hero.IsAlive) continue;
                    if (hero.PartyBelongedTo == MobileParty.MainParty) continue;
                    if (hero.IsPrisoner) continue; // 1b's walk-back owns the prisoner path
                    try
                    {
                        if (hero.IsFugitive) hero.ChangeState(Hero.CharacterStates.Active);
                        AddHeroToPartyAction.Apply(hero, MobileParty.MainParty, showNotification: false);
                        InformationManager.DisplayMessage(new InformationMessage(
                            "Training Battles: " + hero.Name + " rejoined the company — nobody scatters after a drill."));
                    }
                    catch { }
                }
            }
            catch { }
            SweepCompanionSeparationTracker();
        }

        /// <summary>Vanilla's PlayerTrackCompanionBehavior files every fugitive companion into a
        /// save-persisted "scattered" dictionary and announces them in every settlement they sit in
        /// ("Tracking: …separated from you after a battle…"). Walking a hero home through
        /// AddHeroToPartyAction is NOT one of its removal paths (only hire/fire, teleport and
        /// party-creation are), so drill-scattered companions who are already back in the ranks
        /// would keep triggering the popup — Anton got one per companion at a village gate
        /// (2026.07.24). A tracked hero RIDING WITH the main party is stale by definition; this
        /// sweep drops exactly those, via reflection into the behavior's private dictionary
        /// (read-only otherwise; no Harmony, fails silently if TaleWorlds renames the field).</summary>
        private static void SweepCompanionSeparationTracker()
        {
            try
            {
                var tracker = Campaign.Current?.GetCampaignBehavior<PlayerTrackCompanionBehavior>();
                if (tracker == null) return;
                var field = typeof(PlayerTrackCompanionBehavior).GetField("_scatteredCompanions",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (field?.GetValue(tracker) is not Dictionary<Hero, CampaignTime> scattered) return;
                foreach (var hero in new List<Hero>(scattered.Keys))
                {
                    if (hero != null && hero.PartyBelongedTo == MobileParty.MainParty)
                        scattered.Remove(hero);
                }
            }
            catch { }
        }

        /// <summary>One-shot repair for saves touched by the 2026.07.23 builds: drills could leave
        /// companions of the losing half stuck as fugitives ("Regrouping" on the clan screen, and
        /// unlike vanilla scatter they never walked home). Runs once per save, then never again —
        /// so a companion who legitimately scatters in a REAL battle later keeps vanilla's rules.</summary>
        private void RescueStuckFugitiveCompanions()
        {
            if (_fugitiveRescueDone) return;
            _fugitiveRescueDone = true;
            try
            {
                var companions = Clan.PlayerClan?.Companions;
                if (companions == null) return;
                foreach (var hero in new List<Hero>(companions))
                {
                    if (hero == null || !hero.IsAlive || !hero.IsFugitive) continue;
                    try
                    {
                        hero.ChangeState(Hero.CharacterStates.Active);
                        AddHeroToPartyAction.Apply(hero, MobileParty.MainParty, showNotification: false);
                        InformationManager.DisplayMessage(new InformationMessage(
                            "Training Battles: " + hero.Name + " found their way back to the company."));
                    }
                    catch { }
                }
            }
            catch { }
            SweepCompanionSeparationTracker();
        }

        /// <summary>The baggage train, itemized — keyed by item AND modifier so a "fine" sword the
        /// party already owned is never confused with a looted plain one.</summary>
        private static Dictionary<(ItemObject, ItemModifier?), int> SnapshotItems(ItemRoster roster)
        {
            var result = new Dictionary<(ItemObject, ItemModifier?), int>();
            try
            {
                for (var i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    if (el.EquipmentElement.Item == null) continue;
                    var key = (el.EquipmentElement.Item, (ItemModifier?)el.EquipmentElement.ItemModifier);
                    result.TryGetValue(key, out var have);
                    result[key] = have + el.Amount;
                }
            }
            catch { }
            return result;
        }

        /// <summary>Removes every item the party GAINED since the pre-fight snapshot — the drill's
        /// loot, whichever pipeline granted it (vanilla's commit or a Harmony loot mod's). Items the
        /// party lost are not touched: nothing is ever added. Does NOT clear the snapshot — the
        /// caller owns its lifetime, because loot screens hand items over only when the player
        /// CLOSES them (Anton took a full loot screen home past the first, immediate sweep), so the
        /// tick runs one final sweep after the map is truly quiet again.</summary>
        private void RemoveDrillLoot()
        {
            var before = _itemSnapshot;
            if (before == null) return;
            try
            {
                var roster = MobileParty.MainParty.Party.ItemRoster;
                var gained = new List<(EquipmentElement Element, int Extra)>();
                for (var i = 0; i < roster.Count; i++)
                {
                    var el = roster.GetElementCopyAtIndex(i);
                    if (el.EquipmentElement.Item == null) continue;
                    var key = (el.EquipmentElement.Item, (ItemModifier?)el.EquipmentElement.ItemModifier);
                    before.TryGetValue(key, out var had);
                    var extra = el.Amount - had;
                    if (extra > 0) gained.Add((el.EquipmentElement, extra));
                }
                var removed = 0;
                foreach (var pair in gained)
                {
                    roster.AddToCounts(pair.Element, -pair.Extra);
                    removed += pair.Extra;
                }
                if (removed > 0)
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Training Battles: " + removed + " looted item" + (removed == 1 ? "" : "s")
                        + " returned — there are no spoils in sparring."));
            }
            catch { }
        }

        /// <summary>Tears down a battle that failed to start: men merged home, temp party destroyed,
        /// no aftermath, no cooldown.</summary>
        private void AbortLiveBattle()
        {
            // Finish(false): a failed CASTLE launch must not also throw the player out of
            // their own castle (crash round 2's insult on top of injury); the field and sea
            // drills never sit inside a settlement, so nothing changes for them.
            try { if (PlayerEncounter.Current != null) PlayerEncounter.Finish(false); } catch { }
            DismantleDrillSiege(); // a half-born siege drill strikes its camp before anything else
            DismantleGhostSiege(); // ...and one that died inside vanilla's own constructor too
            RestoreWalls();
            _friendPartySnapshots.Clear(); // nobody fought — nothing to walk back
            var abortedSiege = _siegeSettlement;
            _siegeSettlement = null;
            RestoreFleet(); // hulls home and healed BEFORE their borrower party dissolves
            var opponent = _opponentParty;
            if (opponent != null)
            {
                if (_opponentIsMockEnemy)
                {
                    SinkPhantomFleet(opponent); // conjured hulls dissolve with their crews
                    MergeMockPrisonersHome(opponent); // phantoms dissolve, our men come home
                }
                else
                {
                    MergePartyBackIntoMain(opponent);
                }
                DestroyOpponentParty(opponent);
            }
            _opponentIsMockEnemy = false;
            RestoreOpponentClanLook();
            RestorePartyRoles(); // the officers keep their posts through an aborted drill
            // A drill that never happened is a drill nobody gets paid for.
            if (_chargedCost > 0)
            {
                try { GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, _chargedCost); } catch { }
                _chargedCost = 0;
            }
            _heroHpBefore.Clear();
            _itemSnapshot = null;
            _lootSweepTicks = 0;
            TrainingActive = false;
            _checkResults = false;
            _battleRan = false;
            _aftermathReady = false;
            _pendingPlayerWon = null;
            _battleDead.Clear();
            _battleDeadHarvested = false;
            _opponentParty = null;
            _mainSnapshot = null;
            _opponentSnapshot = null;
            _prisonSnapshot = null;
            Models.TrainingBattlesSceneModel.PendingSceneId = null;
            // A castle launch that failed leaves the player standing in their own castle with
            // the settlement encounter closed — re-open it the vanilla way so the castle menu
            // (and the Leave option) work again.
            if (abortedSiege != null && MobileParty.MainParty?.CurrentSettlement == abortedSiege)
            {
                try
                {
                    if (PlayerEncounter.Current == null)
                        EncounterManager.StartSettlementEncounter(MobileParty.MainParty, abortedSiege);
                }
                catch { }
            }
        }

        // ------------------------------ the aftermath ------------------------------

        private void FinishTrainingBattle()
        {
            _checkResults = false;
            _battleRan = false;
            var main = MobileParty.MainParty;
            var opponent = _opponentParty;

            bool playerWon = _pendingPlayerWon ?? false;
            try
            {
                var battle = PlayerEncounter.Battle;
                if (battle != null) playerWon = battle.WinningSide == battle.PlayerSide;
            }
            catch { }

            // Kill the encounter POSITIVELY — but let vanilla SETTLE THE BOOKS first. The XP and
            // casualty commit for player battles runs inside PlayerEncounter's own update
            // (DoApplyMapEventResults → CalculateAndCommitMapEventResults, guarded so it runs only
            // once) — finishing without it silently discards everything the men earned (Anton:
            // promotions on the battle scoreboard, +0 on the roster). So: give the event a winner
            // if it lacks one, drive one PlayerEncounter.Update() to run the commit, THEN finish.
            // A merely-Finished encounter with an undecided event would also leave vanilla's
            // send-troops menu armed — and a re-run from there is a pure vanilla battle.
            try
            {
                // The player's side is DECLARED the winner UNCONDITIONALLY (the honest result
                // was already read into playerWon above, and every reward channel is zeroed
                // while training, so the stomp buys nothing and costs nothing) — because a
                // defeated player walks vanilla's PlayerTotalDefeat road: the
                // "defeated_and_taken_prisoner" menu takes the player CAPTIVE of the temp
                // party, whose destruction then reads as "your captors dispersed", stranding
                // the party shipless at sea for a beat (Anton's naval defeat, 2026.07.25).
                // The old HasWinner check ("never stomp a real result") protected exactly the
                // road we must close.
                var mapEvent = MobileParty.MainParty.MapEvent;
                if (mapEvent != null && !mapEvent.IsFinalized)
                    mapEvent.SetOverrideWinner(mapEvent.PlayerSide);
                for (var i = 0; i < 3 && PlayerEncounter.Current != null; i++)
                {
                    // No spoils screens after sparring: the reward model already keeps vanilla
                    // from minting item loot, and emptying the receive-rosters between passes
                    // keeps the encounter's Loot* states from opening a screen over whatever
                    // some other mod slipped in ahead of this pass.
                    try
                    {
                        PlayerEncounter.Current.RosterToReceiveLootItems.Clear();
                        PlayerEncounter.Current.RosterToReceiveLootMembers.Clear();
                        PlayerEncounter.Current.RosterToReceiveLootPrisoners.Clear();
                    }
                    catch { }
                    PlayerEncounter.Update();
                }
            }
            catch { }
            // Finish(false): the field and sea drills never sit inside a settlement (no
            // behavior change there), and the castle drill's DEFENDER must not be thrown out
            // of their own gate by the wrap-up.
            try { if (PlayerEncounter.Current != null) PlayerEncounter.Finish(false); } catch { }
            // The belt to the winner-stomp's suspenders: if the defeat road was somehow faster
            // and the player already sits in the temp party's prisoner wagon, the captivity
            // ends here — quietly, before the captor party is destroyed, so no "your captors
            // have been dispersed" theater and no shipless "stranded at sea" flash.
            try
            {
                if (Hero.MainHero != null && Hero.MainHero.IsPrisoner)
                    EndCaptivityAction.ApplyByReleasedAfterBattle(Hero.MainHero);
            }
            catch { }
            // The siege drill's campaign shell comes down BEFORE the menu pops — finalize may
            // push vanilla's "the attackers left" menu, and the pops then clear it.
            DismantleDrillSiege();
            DismantleGhostSiege();
            RestoreWalls();
            try
            {
                // Pop whatever wrap/encounter menus linger (bounded — never spin).
                for (var i = 0; i < 3 && Campaign.Current?.CurrentMenuContext != null; i++)
                    GameMenu.ExitToLast();
            }
            catch { }
            TrainingActive = false;
            _aftermathReady = false;
            _pendingPlayerWon = null;
            _chargedCost = 0; // the drill happened — the chest is spent
            Models.TrainingBattlesSceneModel.PendingSceneId = null; // never read = never armed again
            RestoreOpponentClanLook(); // the lender clan gets its own colors back
            // Who the fleet truly was before the drill — read BEFORE RestoreFleet clears the
            // snapshot; the mock sea drill's capture sweep below tells own from phantom by it.
            var ownHulls = new HashSet<Ship>();
            foreach (var entry in _fleetSnapshot)
                if (entry.Ship != null) ownHulls.Add(entry.Ship);
            var wasSeaDrill = ownHulls.Count > 0;
            RestoreFleet(); // every hull re-owned and re-healed, sunk or "captured" or crossed

            var mainSnapshot = _mainSnapshot;
            var opponentSnapshot = _opponentSnapshot;
            var prisonSnapshot = _prisonSnapshot;
            var opponentWasMock = _opponentIsMockEnemy;
            _mainSnapshot = null;
            _opponentSnapshot = null;
            _prisonSnapshot = null;
            _opponentParty = null;
            _opponentIsMockEnemy = false;

            // 1. Everyone comes home: survivors of the opposing half (and anyone who somehow ended
            //    up a prisoner) rejoin the main party. A MOCK enemy's men are phantoms — they
            //    dissolve with their party; only its prisoner wagons (ours, if anyone) come home.
            if (opponent != null)
            {
                if (opponentWasMock)
                {
                    SinkPhantomFleet(opponent); // conjured hulls dissolve with their crews
                    MergeMockPrisonersHome(opponent);
                }
                else
                {
                    MergePartyBackIntoMain(opponent);
                }
                DestroyOpponentParty(opponent);
            }
            // A mock SEA drill can end with vanilla handing the player "captured" phantom
            // hulls — anything in the fleet that was not there before the drill dissolves.
            if (opponentWasMock && wasSeaDrill) SweepForeignHulls(ownHulls);

            // 1b. A won field battle makes the losers' wounded the winner's PRISONERS in vanilla.
            //     The reward model forbids that during training, but if any of our own slipped into
            //     the prisoner wagons anyway, walk them back to the ranks — only the ones the drill
            //     added (delta vs. the pre-battle prisoner snapshot; real prisoners stay prisoners).
            //     (GetTroopRoster hands out the LIVE list — iterate a copy, the loop mutates it.)
            if (prisonSnapshot != null && mainSnapshot != null && opponentSnapshot != null)
            {
                var prisonBefore = ToDictionary(prisonSnapshot);
                var ours = Combine(ToDictionary(mainSnapshot), ToDictionary(opponentSnapshot));
                foreach (var el in new List<TroopRosterElement>(main.PrisonRoster.GetTroopRoster()))
                {
                    var character = el.Character;
                    if (character == null || character.IsHero || !ours.ContainsKey(character)) continue;
                    prisonBefore.TryGetValue(character, out var was);
                    var extra = el.Number - was.Number;
                    if (extra <= 0) continue;
                    var extraWounded = Math.Min(Math.Max(el.WoundedNumber - was.Wounded, 0), extra);
                    main.PrisonRoster.AddToCounts(character, -extra, false, -extraWounded);
                    main.MemberRoster.AddToCounts(character, extra, false, extraWounded);
                }
            }

            // 1b-mock. The reverse leak: phantoms WE "captured" would sit in the wagons as free
            //     prisoners (recruits to press, bodies to sell). Sweep out exactly what the drill
            //     added — a real prisoner of the same troop type from before the drill stays.
            if (opponentWasMock && prisonSnapshot != null && mainSnapshot != null)
            {
                var prisonBefore = ToDictionary(prisonSnapshot);
                var ownMen = ToDictionary(mainSnapshot);
                foreach (var el in new List<TroopRosterElement>(main.PrisonRoster.GetTroopRoster()))
                {
                    var character = el.Character;
                    if (character == null || character.IsHero || ownMen.ContainsKey(character)) continue;
                    prisonBefore.TryGetValue(character, out var was);
                    var extra = el.Number - was.Number;
                    if (extra <= 0) continue;
                    var extraWounded = Math.Min(Math.Max(el.WoundedNumber - was.Wounded, 0), extra);
                    main.PrisonRoster.AddToCounts(character, -extra, false, -extraWounded);
                }
            }

            // 1c. Companions who "scattered": vanilla makes the losing side's uncaptured heroes
            //     FUGITIVE at map-event end (the clan screen calls it "Regrouping" — Anton found
            //     two companions stuck there). Our no-capture guard is exactly what routes them to
            //     that path, so every hero of the drill who is no longer in the party walks
            //     straight back into the ranks.
            RecoverScatteredHeroes(mainSnapshot);
            RecoverScatteredHeroes(opponentSnapshot);

            // 1c-bis. …and with their posts: the engine stripped every crossing (and every
            //     scattered) hero of their party roles — scout, engineer, quartermaster, surgeon —
            //     the moment they left the party. Now that everyone is back in the ranks, each
            //     hero gets back exactly the roles they held walking in (Anton's playtest catch,
            //     2026.07.24: the opposing half's scout came home unassigned).
            RestorePartyRoles();

            // 1d. No spoils from sparring: anything the baggage train gained over the pre-fight
            //     snapshot is drill loot (whoever's pipeline granted it) and is quietly removed.
            //     First sweep here; the snapshot stays alive for the tick's FINAL sweep, because a
            //     loot screen (BannerLoot pushes one) grants its items only when the player closes
            //     it — after this very method has come and gone.
            _lootSweepTicks = 0;
            RemoveDrillLoot();

            // 1e. The heroes walk out on their own legs: healed back toward the configured floor
            //     (never above their pre-drill health) — a bruise may sting, but the player and the
            //     companions are never benched by training.
            RestoreHeroHealth();

            // 2. Nobody dies in training: the fallen return — some wounded, per the surgeon's own
            //    Medicine-driven save and the configured share — and XP is restored ABSOLUTELY:
            //    the game clamps a stack's XP to (men in stack × max upgrade cost), so battle
            //    deaths silently destroy the fallen men's stored upgrade progress (found by Anton:
            //    8 waiting upgrades melting to 6). Deltas cannot see clamped-away XP — instead,
            //    the men come back FIRST (raising the clamp ceiling to full), then each stack's XP
            //    is SET to its pre-battle pool plus the kept share of what the drill visibly earned.
            var restored = 0;
            var casualtiesTotal = 0;    // the event's dead — the surgeon's whole KIA docket
            var diedTotal = 0;          // of those, the truly, permanently DEAD (the real-death band)
            var batteredTotal = 0;      // downed but never dead (KO'd, battle-wounded) — the stay-wounded band's docket
            var woundedTotal = 0;       // wake up wounded, from either band
            var xpRestored = 0;
            var xpKeptFromDrill = 0;
            // Every drill writes a full account of the aftermath arithmetic to
            // Configs\TrainingBattles\last_drill_report.txt — per stack: what the snapshots held,
            // what the battle left, what was restored/filtered and with what rolls. When a number
            // on the party screen looks wrong, this file is the witness.
            // The officers set this drill's rates — read once, before the loop, and spelled
            // out in the report (if a hero has not walked home yet the read falls back to the
            // leader; the report shows exactly whose skill was used).
            var keptPercent = EffectiveXpKeptPercent(out var xpOfficer);
            var drillSurgeon = Officers.SurgeonOfficer(main);
            var deathChance = AftermathMath.ChancePercentForSkill(
                _config.RealDeathPercentAtMedicine0, _config.RealDeathPercentAtMedicine300, drillSurgeon.Skill) / 100.0;
            var kiaWoundChance = AftermathMath.ChancePercentForSkill(
                _config.KiaWoundedPercentAtMedicine0, _config.KiaWoundedPercentAtMedicine300, drillSurgeon.Skill) / 100.0;
            var stayWoundChance = AftermathMath.ChancePercentForSkill(
                _config.DownedWoundedPercentAtMedicine0, _config.DownedWoundedPercentAtMedicine300, drillSurgeon.Skill) / 100.0;
            var report = new StringBuilder();
            report.AppendLine("Training drill report — " + CampaignTime.Now + " | playerWon " + playerWon);
            report.AppendLine("XP: " + keptPercent + "% kept (" + xpOfficer.Describe()
                + ", band " + _config.XpKeptMinPercent + "-" + _config.XpKeptMaxPercent + "%)");
            report.AppendLine("Casualties: " + drillSurgeon.Describe()
                + " | real death " + (deathChance * 100.0).ToString("0.##")
                + "% | KIA→wounded " + (kiaWoundChance * 100.0).ToString("0.##")
                + "% | downed→wounded " + (stayWoundChance * 100.0).ToString("0.##") + "%");
            report.AppendLine("harvest " + (_battleDeadHarvested ? "event-DiedInBattle" : "roster-diff fallback"));
            report.AppendLine("stack | before N/W/xp | after N/W/xp | fallen | eventDead | kiaDocket | DIED | kiaWounded | downed | stayWounded | woundedAdjust | xpAdjust");

            // 2-walls. The castle drill's OTHER friendly parties first — garrison, militia,
            //    guesting lords — each walked back on its own roster by the same surgeon and
            //    XP arithmetic. They run BEFORE the main pass and CONSUME their share of the
            //    event's death book (_battleDead), so a troop type shared with the main party
            //    is never judged dead twice.
            RestoreFriendlyParties(report, keptPercent, deathChance, kiaWoundChance, stayWoundChance,
                ref restored, ref casualtiesTotal, ref diedTotal, ref batteredTotal, ref woundedTotal,
                ref xpRestored, ref xpKeptFromDrill);
            if (mainSnapshot != null && opponentSnapshot != null)
            {
                var before = Combine(ToDictionary(mainSnapshot), ToDictionary(opponentSnapshot));
                var after = ToDictionary(main.MemberRoster);
                foreach (var pair in before)
                {
                    var character = pair.Key;
                    if (character == null || character.IsHero) continue; // heroes never die here; game wounds them
                    after.TryGetValue(character, out var now);

                    // The event's DEAD go to the surgeon's KIA verdict (real death, then
                    // wounded); the merely DOWNED go to the softer stay-wounded band below.
                    // History: round 9 fed one flat filter dead AND battle-wounded, because on
                    // a WIN the two together approximate the men who actually dropped
                    // (vanilla's surgeon converts most mission KIA to roster-wounded before we
                    // run). But on a LOSS the entire beaten side comes home roster-wounded
                    // (every man was downed or KO'd), so the knob chewed on all 150 instead of
                    // the ~20 truly killed (Anton, 2026.07.25, land and sea alike). The
                    // officers update kept that separation and gave each pool its own
                    // Medicine-scaled band.
                    var fallen = pair.Value.Number - now.Number;
                    var newWounded = Math.Max(0, now.Wounded - pair.Value.Wounded);
                    // The knob's input is the event's OWN death book, not the roster hole: the
                    // hole also swallows the defeated side's KO'd men (the no-capture guard
                    // leaves their prisoner-distribution without a receiver — see
                    // HarvestBattleDead). Clamped to the hole; roster-diff fallback if the
                    // harvest never ran.
                    _battleDead.TryGetValue(character, out var trulyDead);
                    var casualties = _battleDeadHarvested
                        ? Math.Min(trulyDead, Math.Max(fallen, 0))
                        : Math.Max(fallen, 0);
                    var died = 0;
                    var kiaWounded = 0;
                    var stayWounded = 0;
                    var woundedAdjust = 0;
                    if (casualties > 0 || newWounded > 0 || fallen > 0)
                    {
                        // The surgeon's verdict on the would-have-died: a few truly die (the
                        // real-death band — the drill's one permanent cost), some wake wounded,
                        // the rest shrug it off. The truly dead are NOT restored below.
                        var verdict = AftermathMath.JudgeFallen(
                            casualties, deathChance, kiaWoundChance, () => MBRandom.RandomFloat);
                        died = verdict.Died;
                        kiaWounded = verdict.Wounded;
                        var comeBack = Math.Max(fallen, 0) - died; // men back first — minus the dead
                        if (comeBack > 0)
                            main.MemberRoster.AddToCounts(character, comeBack, false, 0);
                        // The downed pool — roster-wounded plus the vanished would-be-captured
                        // (everyone who dropped without dying) — rolls the stay-wounded band.
                        var downed = newWounded + (Math.Max(fallen, 0) - casualties);
                        stayWounded = AftermathMath.StayWounded(
                            downed, stayWoundChance, () => MBRandom.RandomFloat);
                        var finalNumber = now.Number + comeBack;
                        var desiredWounded = Math.Min(pair.Value.Wounded + kiaWounded + stayWounded, finalNumber);
                        woundedAdjust = desiredWounded - now.Wounded;
                        if (woundedAdjust != 0)
                            main.MemberRoster.AddToCounts(character, 0, false, woundedAdjust);
                        restored += comeBack;
                        casualtiesTotal += casualties;
                        diedTotal += died;
                        batteredTotal += downed;
                        woundedTotal += kiaWounded + stayWounded;
                        CreditSurgeon(main, character, casualties - died);
                    }

                    // Visible earnings only — anything the clamp already ate mid-battle counts as
                    // unearned (conservative, never negative). Target = old pool + kept earnings.
                    var earned = Math.Max(0, now.Xp - pair.Value.Xp);
                    var kept = AftermathMath.XpKept(earned, keptPercent);
                    var targetXp = pair.Value.Xp + kept;
                    var xpAdjust = targetXp - now.Xp;
                    if (xpAdjust != 0)
                        main.MemberRoster.AddToCounts(character, 0, false, 0, xpAdjust);
                    xpKeptFromDrill += kept;
                    if (xpAdjust > 0) xpRestored += xpAdjust;

                    if (casualties > 0 || newWounded > 0 || xpAdjust != 0 || fallen != 0)
                    {
                        report.AppendLine(character.Name + " | "
                            + pair.Value.Number + "/" + pair.Value.Wounded + "/" + pair.Value.Xp + " | "
                            + now.Number + "/" + now.Wounded + "/" + now.Xp + " | "
                            + fallen + " | " + trulyDead + " | " + casualties + " | "
                            + died + " | " + kiaWounded + " | "
                            + (newWounded + (Math.Max(fallen, 0) - casualties)) + " | " + stayWounded + " | "
                            + woundedAdjust + " | " + xpAdjust);
                    }
                }
            }
            report.AppendLine("TOTALS: kia docket " + casualtiesTotal + " | TRULY DIED " + diedTotal
                + " | wake wounded " + woundedTotal + " | downed " + batteredTotal
                + " | restored " + restored + " | xp kept " + xpKeptFromDrill + " | xp restored " + xpRestored);
            try
            {
                System.IO.Directory.CreateDirectory(ModConfig.ConfigDirectory);
                System.IO.File.WriteAllText(
                    System.IO.Path.Combine(ModConfig.ConfigDirectory, "last_drill_report.txt"),
                    report.ToString());
            }
            catch { }

            // 3. The drill leaves its honest marks — each drill on its own clock: the castle's
            //    (per settlement) for a siege drill, the field drill's single one otherwise.
            if (_config.DisorganizedAfterTraining)
            {
                try { main.SetDisorganized(true); } catch { }
            }
            var siegeSettlement = _siegeSettlement;
            _siegeSettlement = null;
            if (siegeSettlement != null) StampCastleCooldown(siegeSettlement);
            else _lastTrainingHours = (float)CampaignTime.Now.ToHours;

            // 3b. The grand muster's prestige (Anton, 2026.07.25): a castle drill is a big,
            //     expensive, very public event — renown and influence per 100 friendly men on
            //     the field, paid HERE and never through the battle's reward books (those stay
            //     zeroed while training, so no kill or loot can ever farm this).
            var prestigeNote = string.Empty;
            if (siegeSettlement != null)
            {
                try
                {
                    var renown = CastleDrillRenown(out var influence);
                    if (renown > 0f && Hero.MainHero != null)
                        GainRenownAction.Apply(Hero.MainHero, renown, doNotNotify: true);
                    if (influence > 0f && Clan.PlayerClan != null)
                        ChangeClanInfluenceAction.Apply(Clan.PlayerClan, influence);
                    if (renown > 0f || influence > 0f)
                        prestigeNote = " The realm took note: +" + renown.ToString("0.#")
                            + " renown, +" + influence.ToString("0.#") + " influence.";
                }
                catch { }
            }

            var summary = (siegeSettlement != null
                    ? (opponentWasMock
                        ? (playerWon ? "You carried the walls of " + siegeSettlement.Name + ". "
                                     : "The mock enemy carried the walls. ")
                        : (playerWon ? "Your side carried the walls of " + siegeSettlement.Name + ". "
                                     : "The other side carried the walls. "))
                    : opponentWasMock
                        ? (playerWon ? "You carried the field. " : "The mock enemy carried the field. ")
                        : (playerWon ? "Your half carried the field. " : "The other half carried the field. "))
                + (casualtiesTotal > 0 || batteredTotal > 0
                    ? casualtiesTotal + " fell and " + batteredTotal + " were battered — "
                        + (diedTotal > 0
                            ? diedTotal + (diedTotal == 1 ? " man TRULY DIED" : " men TRULY DIED")
                                + " despite the surgeon, "
                            : "")
                        + woundedTotal + " wake up wounded, the rest shrug it off."
                    : "Not a man stayed down.")
                + " Drill XP kept: " + xpKeptFromDrill + " (at " + keptPercent + "%)"
                + (xpRestored > 0 ? " (and " + xpRestored + " upgrade XP restored to the stacks)." : ".")
                + prestigeNote;
            InformationManager.DisplayMessage(new InformationMessage("Training over. " + summary));
            TbLog.Info("drill", (siegeSettlement != null ? "siege " : "")
                + (opponentWasMock ? "mock" : "split") + " drill done | playerWon " + playerWon
                + " | " + xpOfficer.Describe() + " → " + keptPercent + "% kept (" + xpKeptFromDrill + " xp)"
                + " | " + drillSurgeon.Describe() + " → kia " + casualtiesTotal + ", died " + diedTotal
                + ", wounded " + woundedTotal + ", downed " + batteredTotal
                + " | chances d/kw/dw " + (deathChance * 100).ToString("0.##") + "/"
                + (kiaWoundChance * 100).ToString("0.##") + "/" + (stayWoundChance * 100).ToString("0.##") + "%");

            try { if (Campaign.Current?.CurrentMenuContext != null) GameMenu.ExitToLast(); } catch { }

            // A siege drill ends where it began: INSIDE the castle (Anton's ask — the drill
            // started from the castle menu, so it returns there). The defender never left;
            // the attacker walks back through their own gate. Both roads use vanilla's own
            // arrive-at-settlement door (StartSettlementEncounter → the castle menu).
            if (siegeSettlement != null)
            {
                try
                {
                    var mainParty = MobileParty.MainParty;
                    if (mainParty?.CurrentSettlement == siegeSettlement)
                    {
                        if (PlayerEncounter.Current == null)
                            EncounterManager.StartSettlementEncounter(mainParty, siegeSettlement);
                    }
                    else if (mainParty != null && mainParty.CurrentSettlement == null
                        && mainParty.MapEvent == null && PlayerEncounter.Current == null
                        && siegeSettlement.SiegeEvent == null)
                    {
                        EncounterManager.StartSettlementEncounter(mainParty, siegeSettlement);
                    }
                }
                catch { /* worst case the player rides back in themselves */ }
            }
        }

        /// <summary>The castle drill's garrison/militia/guest walk-back: each friendly party's
        /// roster restored in place by the same arithmetic as the main party's — men back first
        /// (minus the surgeon's real-death band), wounded per the bands, XP kept at the officer's
        /// rate and restored absolutely against the clamp. Runs BEFORE the main pass and consumes
        /// its share of the event's death book, so a troop type shared across parties is never
        /// judged dead twice. The main party's own officers set every rate — it is their drill.</summary>
        private void RestoreFriendlyParties(StringBuilder report, int keptPercent,
            double deathChance, double kiaWoundChance, double stayWoundChance,
            ref int restored, ref int casualtiesTotal, ref int diedTotal, ref int batteredTotal,
            ref int woundedTotal, ref int xpRestored, ref int xpKeptFromDrill)
        {
            if (_friendPartySnapshots.Count == 0) return;
            var main = MobileParty.MainParty;
            foreach (var (party, snapshot) in _friendPartySnapshots)
            {
                try
                {
                    if (party == null || snapshot == null || !party.IsActive) continue;
                    report.AppendLine("— " + (party.Name?.ToString() ?? "friendly party") + " —");
                    var before = ToDictionary(snapshot);
                    var after = ToDictionary(party.MemberRoster);
                    foreach (var pair in before)
                    {
                        var character = pair.Key;
                        if (character == null || character.IsHero) continue;
                        after.TryGetValue(character, out var now);
                        var fallen = pair.Value.Number - now.Number;
                        var newWounded = Math.Max(0, now.Wounded - pair.Value.Wounded);
                        _battleDead.TryGetValue(character, out var trulyDead);
                        var casualties = _battleDeadHarvested
                            ? Math.Min(trulyDead, Math.Max(fallen, 0))
                            : Math.Max(fallen, 0);
                        if (_battleDeadHarvested && casualties > 0)
                            _battleDead[character] = trulyDead - casualties; // consumed
                        var died = 0;
                        var kiaWounded = 0;
                        var stayWounded = 0;
                        var woundedAdjust = 0;
                        if (casualties > 0 || newWounded > 0 || fallen > 0)
                        {
                            var verdict = AftermathMath.JudgeFallen(
                                casualties, deathChance, kiaWoundChance, () => MBRandom.RandomFloat);
                            died = verdict.Died;
                            kiaWounded = verdict.Wounded;
                            var comeBack = Math.Max(fallen, 0) - died;
                            if (comeBack > 0)
                                party.MemberRoster.AddToCounts(character, comeBack, false, 0);
                            var downed = newWounded + (Math.Max(fallen, 0) - casualties);
                            stayWounded = AftermathMath.StayWounded(
                                downed, stayWoundChance, () => MBRandom.RandomFloat);
                            var finalNumber = now.Number + comeBack;
                            var desiredWounded = Math.Min(pair.Value.Wounded + kiaWounded + stayWounded, finalNumber);
                            woundedAdjust = desiredWounded - now.Wounded;
                            if (woundedAdjust != 0)
                                party.MemberRoster.AddToCounts(character, 0, false, woundedAdjust);
                            restored += comeBack;
                            casualtiesTotal += casualties;
                            diedTotal += died;
                            batteredTotal += downed;
                            woundedTotal += kiaWounded + stayWounded;
                            CreditSurgeon(main, character, casualties - died);
                        }
                        var earned = Math.Max(0, now.Xp - pair.Value.Xp);
                        var kept = AftermathMath.XpKept(earned, keptPercent);
                        var targetXp = pair.Value.Xp + kept;
                        var xpAdjust = targetXp - now.Xp;
                        if (xpAdjust != 0)
                            party.MemberRoster.AddToCounts(character, 0, false, 0, xpAdjust);
                        xpKeptFromDrill += kept;
                        if (xpAdjust > 0) xpRestored += xpAdjust;
                        if (casualties > 0 || newWounded > 0 || xpAdjust != 0 || fallen != 0)
                        {
                            report.AppendLine(character.Name + " | "
                                + pair.Value.Number + "/" + pair.Value.Wounded + "/" + pair.Value.Xp + " | "
                                + now.Number + "/" + now.Wounded + "/" + now.Xp + " | "
                                + fallen + " | " + trulyDead + " | " + casualties + " | "
                                + died + " | " + kiaWounded + " | "
                                + (newWounded + (Math.Max(fallen, 0) - casualties)) + " | " + stayWounded + " | "
                                + woundedAdjust + " | " + xpAdjust);
                        }
                    }
                }
                catch { /* one party's walk-back failing must not strand the rest */ }
            }
            _friendPartySnapshots.Clear();
        }

        private static void CreditSurgeon(MobileParty party, CharacterObject character, int menSaved)
        {
            // In a real battle every survival roll credits the surgeon; a drill's saves do too.
            try
            {
                for (var i = 0; i < menSaved; i++)
                    SkillLevelingManager.OnSurgeryApplied(party, surgerySuccess: true, character.Tier);
            }
            catch { }
        }

        // ------------------------------ roster plumbing ------------------------------

        private readonly struct RosterLine
        {
            public RosterLine(int number, int wounded, int xp) { Number = number; Wounded = wounded; Xp = xp; }
            public int Number { get; }
            public int Wounded { get; }
            public int Xp { get; }
        }

        /// <summary>A roster clone that keeps each stack's XP. The game's own
        /// <c>CloneRosterData</c> copies counts and wounded but silently DROPS Xp — snapshots made
        /// with it read as zero-XP and poison every downstream XP computation.</summary>
        private static TroopRoster CloneWithXp(TroopRoster source)
        {
            var clone = TroopRoster.CreateDummyTroopRoster();
            if (source == null) return clone;
            foreach (var el in source.GetTroopRoster())
            {
                if (el.Character == null) continue;
                clone.AddToCounts(el.Character, el.Number, false, el.WoundedNumber, el.Xp);
            }
            return clone;
        }

        private static Dictionary<CharacterObject, RosterLine> ToDictionary(TroopRoster roster)
        {
            var result = new Dictionary<CharacterObject, RosterLine>();
            if (roster == null) return result;
            foreach (var el in roster.GetTroopRoster())
            {
                if (el.Character == null) continue;
                result.TryGetValue(el.Character, out var prior);
                result[el.Character] = new RosterLine(prior.Number + el.Number, prior.Wounded + el.WoundedNumber, prior.Xp + el.Xp);
            }
            return result;
        }

        private static Dictionary<CharacterObject, RosterLine> Combine(Dictionary<CharacterObject, RosterLine> a, Dictionary<CharacterObject, RosterLine> b)
        {
            var result = new Dictionary<CharacterObject, RosterLine>(a);
            foreach (var pair in b)
            {
                result.TryGetValue(pair.Key, out var prior);
                result[pair.Key] = new RosterLine(prior.Number + pair.Value.Number, prior.Wounded + pair.Value.Wounded, prior.Xp + pair.Value.Xp);
            }
            return result;
        }

        private static void MergePartyBackIntoMain(MobileParty party)
        {
            try
            {
                var main = MobileParty.MainParty;
                foreach (var el in party.MemberRoster.GetTroopRoster())
                {
                    if (el.Character == null) continue;
                    main.MemberRoster.AddToCounts(el.Character, el.Number, false, el.WoundedNumber, el.Xp);
                }
                party.MemberRoster.Clear();
                // Belt and braces: the reward model forbids prisoner-taking in training, but if any
                // of our own ended up in the wagons, they walk home too.
                foreach (var el in party.PrisonRoster.GetTroopRoster())
                {
                    if (el.Character == null) continue;
                    main.MemberRoster.AddToCounts(el.Character, el.Number, false, el.WoundedNumber);
                }
                party.PrisonRoster.Clear();
            }
            catch { }
        }

        /// <summary>The mock-enemy party's send-off: its MEMBERS are phantoms and dissolve with it,
        /// but anyone in its prisoner wagons could only have been captured from OUR side — those
        /// walk straight back into the ranks.</summary>
        private static void MergeMockPrisonersHome(MobileParty party)
        {
            try
            {
                var main = MobileParty.MainParty;
                foreach (var el in party.PrisonRoster.GetTroopRoster())
                {
                    if (el.Character == null) continue;
                    main.MemberRoster.AddToCounts(el.Character, el.Number, false, el.WoundedNumber);
                }
                party.PrisonRoster.Clear();
                party.MemberRoster.Clear();
            }
            catch { }
        }

        private static void DestroyOpponentParty(MobileParty party)
        {
            try
            {
                if (party.IsActive) DestroyPartyAction.Apply(null, party);
            }
            catch { }
        }

        /// <summary>A crash or force-quit mid-training leaves the temp party in the save. On every
        /// session launch, any such party hands its men back and dissolves.</summary>
        private static void RecoverStaleOpponentParties()
        {
            try
            {
                var stale = new List<MobileParty>();
                var staleMock = new List<MobileParty>();
                foreach (var party in MobileParty.All)
                {
                    if (party?.StringId == null) continue;
                    if (party.StringId.StartsWith(OpponentPartyIdPrefix, StringComparison.Ordinal))
                        stale.Add(party);
                    else if (party.StringId.StartsWith(MockEnemyPartyIdPrefix, StringComparison.Ordinal))
                        staleMock.Add(party);
                }
                foreach (var party in stale)
                {
                    MergePartyBackIntoMain(party);
                    ReclaimShips(party); // sea drills lend hulls too, and ownership rides the save
                    DestroyOpponentParty(party);
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Training Battles: an interrupted drill was found — the men have returned to the company."));
                }
                foreach (var party in staleMock)
                {
                    // Its men were never ours — merging them home would MINT free troops. The
                    // phantoms dissolve; only its prisoner wagons (ours, if anyone) walk back.
                    // Its HULLS were never ours either: they are conjured phantom ships, and
                    // reclaiming them would mint a free fleet — they sink with their party.
                    MergeMockPrisonersHome(party);
                    SinkPhantomFleet(party);
                    DestroyOpponentParty(party);
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Training Battles: an interrupted mock drill was found — the phantom enemy has dissolved."));
                }
            }
            catch { }
        }
    }
}
