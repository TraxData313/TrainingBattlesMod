using System;
using System.Collections.Generic;
using System.Text;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
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
        private readonly Dictionary<Hero, int> _heroHpBefore = new Dictionary<Hero, int>();
                                                   // heroes' health walking INTO the drill — the
                                                   // restore ceiling (training is not a hospital)
        private Dictionary<(ItemObject, ItemModifier?), int>? _itemSnapshot;
                                                   // the baggage train before the fight — anything
                                                   // ABOVE it afterward is drill loot and is removed,
                                                   // no matter which mod's loot pipeline handed it out
        private int _lootSweepTicks;               // clean-map ticks counted before the FINAL loot
                                                   // sweep — loot screens grant items only when the
                                                   // player closes them, AFTER the aftermath ran
        private int _chargedCost;                  // the pay-chest already charged; refunded on abort
        private bool _checkResults;
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
                    _aftermathReady = true;
                    return;
                }
            }
            catch
            {
                _aftermathReady = true;
            }
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("TrainingBattles_LastTrainingHours", ref _lastTrainingHours);
            dataStore.SyncData("TrainingBattles_ClanRestore", ref _clanRestoreData);
            dataStore.SyncData("TrainingBattles_FugitiveRescueDone", ref _fugitiveRescueDone);
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // Fresh session: no picked team, no live battle — whatever the previous session left.
            TrainingActive = false;
            _pickedTeam = null;
            _mockEnemyTeam = null;
            _opponentIsMockEnemy = false;
            _chosenSceneId = null;
            _opponentParty = null;
            _mainSnapshot = null;
            _opponentSnapshot = null;
            _checkResults = false;
            _heroHpBefore.Clear();
            _chargedCost = 0;
            RestoreOpponentClanLook(); // a crash mid-drill must not leave a bandit clan in our colors
            RecoverStaleOpponentParties();
            RescueStuckFugitiveCompanions();
            AddMenus(starter);
        }

        // ------------------------------ the muster menu ------------------------------

        private void AddMenus(CampaignGameStarter starter)
        {
            starter.AddGameMenu(MenuId, "{TRAINING_MENU_TEXT}", MenuInit);
            starter.AddGameMenuOption(MenuId, "training_pick",
                "{=TB_opt_pick}Divide the men for a training battle",
                PickCondition, _ => OpenPicker());
            starter.AddGameMenuOption(MenuId, "training_mock_enemy",
                "{=TB_opt_mock}Compose a mock enemy to drill against",
                MockEnemyCondition, _ => OpenMockEnemyComposer());
            starter.AddGameMenuOption(MenuId, "training_ground",
                "{=TB_opt_ground2}Select the battlefield",
                GroundCondition, _ => ChooseGround());
            starter.AddGameMenuOption(MenuId, "training_scout",
                "{=TB_opt_scout}Ride out and scout a battlefield",
                ScoutCondition, _ => ScoutGround());
            starter.AddGameMenuOption(MenuId, "training_begin_attack",
                "{=TB_opt_attack4}Begin — {TB_BEGIN_ATTACK}{TB_COST_SUFFIX}",
                args => BeginCondition(args), _ => BeginBattle(playerDefends: false));
            starter.AddGameMenuOption(MenuId, "training_begin_defend",
                "{=TB_opt_defend4}Begin — {TB_BEGIN_DEFEND}{TB_COST_SUFFIX}",
                args => BeginCondition(args), _ => BeginBattle(playerDefends: true));
            starter.AddGameMenuOption(MenuId, "training_send_troops",
                "{=TB_opt_send2}Send the men in — watch it resolve from the hill{TB_COST_SUFFIX}",
                SendTroopsCondition, _ => LaunchTraining(playerDefends: false, simulate: true));
            starter.AddGameMenuOption(MenuId, "training_cancel",
                "{=TB_opt_cancel}Cancel training",
                CancelCondition, _ => CancelTraining(), isLeave: true);
        }

        private void MenuInit(MenuCallbackArgs args)
        {
            // Without a real background mesh the menu shows the engine's placeholder — a big red
            // "temp". Same field-camp image the Company of Trouble quest menu uses.
            try { args.MenuContext.SetBackgroundMeshName("wait_ambush"); } catch { }
            if (_checkResults)
            {
                FinishTrainingBattle();
                return;
            }
            MBTextManager.SetTextVariable("TRAINING_MENU_TEXT", BuildMenuText(), false);
        }

        private string BuildMenuText()
        {
            var cost = ComputeTrainingCost();
            var costText = cost > 0
                ? " It will cost " + cost + " denars ("
                    + _config.TrainingCostWages + FormatDaysWages(_config.TrainingCostWages)
                    + " a man) to gather the training materials and pay the men for the drill."
                : "";
            if (_pickedTeam != null && _pickedTeam.TotalManCount > 0)
            {
                var yours = MobileParty.MainParty.MemberRoster.TotalHealthyCount - _pickedTeam.TotalHealthyCount;
                return "The two halves stand ready on the field: " + _pickedTeam.TotalHealthyCount
                     + " men opposite, " + Math.Max(yours, 0)
                     + " with you. " + DescribeChosenGround() + costText
                     + " Choose your side of the exercise — or call it off.";
            }
            if (_mockEnemyTeam != null && _mockEnemyTeam.TotalManCount > 0)
            {
                return "The mock enemy stands ready: " + _mockEnemyTeam.TotalHealthyCount
                     + " " + DescribeMockEnemyCultures(_mockEnemyTeam) + " men opposite, "
                     + MobileParty.MainParty.MemberRoster.TotalHealthyCount
                     + " with you — phantoms for the drill, your own ranks untouched. "
                     + DescribeChosenGround() + costText
                     + " Choose your side of the exercise — or call it off.";
            }
            // The muster is the mod's front door — it should SAY what a commander can do here,
            // with the real numbers from the config, not make the player guess. Only the features
            // switched on get a paragraph.
            var drill = "DRILL — divide the men, choose a side, and fight a mock battle on this very ground. "
                + "Nobody dies in training: the men keep " + _config.XpKeptPercent + "% of the experience they earn, and of the fallen "
                + "about " + _config.WoundedPercent + "% wake up truly wounded — a better surgeon saves more of them before that roll."
                + (_config.TrainingCostWages > 0 ? " Training materials and the men's drill pay cost "
                    + _config.TrainingCostWages + FormatDaysWages(_config.TrainingCostWages)
                    + " a man — about " + ComputeTrainingCost() + " denars right now." : "")
                + (_config.DisorganizedAfterTraining ? " The party is disorganized for a while after the drill." : "")
                + (_config.CooldownHours > 0 ? " One drill per " + _config.CooldownHours + " hours." : "");
            var mock = "MOCK ENEMY — compose an enemy force of any culture and any strength, and drill "
                + "the whole company against it. The enemy are phantoms: nothing of yours crosses over, "
                + "and your own men follow the training rules above.";
            var scout = "SCOUT — ride out alone to any battlefield of this country: walk the ground, stand where "
                + "your line would form and see where the enemy's would, so you can judge the field before a "
                + "chasing army — or your own next stand — chooses it for you.";
            var anyDrill = _config.EnableSplitTraining || _config.EnableMockEnemyTraining;
            if (anyDrill && !CooldownReady(out var remaining))
            {
                return "The men are still worn from the last drill — ready to muster again in "
                     + FormatRemaining(remaining) + ". Scouting needs no rest.{newline} {newline}" + scout;
            }
            var sections = new List<string>();
            if (_config.EnableSplitTraining) sections.Add(drill);
            if (_config.EnableMockEnemyTraining) sections.Add(mock);
            sections.Add(scout);
            var text = "You call the company to a training muster.";
            foreach (var section in sections) text += "{newline} {newline}" + section;
            if (anyDrill) text += "{newline} {newline}" + DescribeChosenGround();
            return text;
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

        /// <summary>The battlefield line of the muster text — always visible, updating the moment
        /// the player picks a different ground in "Select the battlefield".</summary>
        private string DescribeChosenGround()
        {
            if (_chosenSceneId == null)
                return "Battlefield: as fate wills — the ground you stand on decides.";
            foreach (var scene in TrainingGroundPool(out _))
                if (scene.SceneID == _chosenSceneId)
                    return "Battlefield: " + BattleSceneCatalog.Describe(scene) + " (your choice).";
            return "Battlefield: " + _chosenSceneId + " (your choice).";
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
            if (!CooldownReady(out var remaining))
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject("{=TB_tip_rest}The men need rest — ready in " + FormatRemaining(remaining) + ".");
            }
            else if (MobileParty.MainParty.MemberRoster.TotalHealthyCount < 2)
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject("{=TB_tip_few}You need at least two healthy souls to hold a drill.");
            }
            else
            {
                args.Tooltip = new TextObject("{=TB_tip_pick}A mock battle against your own men: they keep "
                    + _config.XpKeptPercent + "% of the XP they earn, about " + _config.WoundedPercent
                    + "% of the fallen wake wounded (a better doctor lowers that), nobody dies"
                    + (_config.DisorganizedAfterTraining ? ", and the party is disorganized for a while after." : "."));
            }
            return true;
        }

        private bool MockEnemyCondition(MenuCallbackArgs args)
        {
            if (!_config.EnableMockEnemyTraining) return false;
            args.optionLeaveType = GameMenuOption.LeaveType.Manage;
            if (!CooldownReady(out var remaining))
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
                args.Tooltip = new TextObject("{=TB_tip_mock}Pick a culture and build its force man by man — "
                    + "a phantom enemy to test the whole company against. Your men follow the normal training "
                    + "rules; the phantoms vanish afterward. Run it twice to mix cultures.");
            }
            return true;
        }

        private bool GroundCondition(MenuCallbackArgs args)
        {
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
                "Choose the ground",
                "The battlefields for this kind of country. Pick where the drill is fought.",
                candidates, localCount, _chosenSceneId, offerFate: true, sceneId =>
            {
                _chosenSceneId = sceneId;
                // Re-init the muster menu so its text shows (or drops) the chosen ground.
                try { GameMenu.SwitchToMenu(MenuId); } catch { }
            });
        }

        private bool ScoutCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Mission;
            if (TrainingGroundPool(out _).Count == 0) return false;
            if (Hero.MainHero?.IsWounded == true)
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject("{=TB_tip_scout_wounded}You are wounded — scouting means riding.");
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
                "Scout the ground",
                "Pick a battlefield and ride out alone. Walk the ground, stand on your deployment line, "
                + "and see if the deploy is good for your battle — if the map is fine but the lines are "
                + "bad, take another map or another spot.",
                candidates, localCount, null, offerFate: false, sceneId =>
            {
                if (sceneId != null) LaunchScout(sceneId);
            });
        }

        private static void LaunchScout(string sceneId)
        {
            try { ScoutMission.Open(sceneId); }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Training Battles: could not ride out (" + ex.Message + ")."));
            }
        }

        private static List<SingleplayerBattleSceneData> TrainingGroundPool(out int localCount)
        {
            try
            {
                return BattleSceneCatalog.WiderPoolAt(
                    MobileParty.MainParty.Position, PlayerEncounter.IsNavalEncounter(), out localCount);
            }
            catch
            {
                localCount = 0;
                return new List<SingleplayerBattleSceneData>();
            }
        }

        private bool BeginCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Mission;
            return ReadyToBegin(args);
        }

        private bool SendTroopsCondition(MenuCallbackArgs args)
        {
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
            MBTextManager.SetTextVariable("TB_BEGIN_ATTACK",
                mockDrill ? "you attack the mock enemy" : "your half attacks", false);
            MBTextManager.SetTextVariable("TB_BEGIN_DEFEND",
                mockDrill ? "you defend against the mock enemy" : "your half defends", false);
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
            if (costNow > 0 && (Hero.MainHero?.Gold ?? 0) < costNow)
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject("{=TB_tip_poor}The drill's pay-chest wants " + costNow
                    + " denars (" + _config.TrainingCostWages + FormatDaysWages(_config.TrainingCostWages)
                    + " for every soul on the field) — you carry " + (Hero.MainHero?.Gold ?? 0) + ".");
            }
            return true;
        }

        /// <summary>What this drill costs: every soldier on the field (both halves — they all train)
        /// earns <see cref="ModConfig.TrainingCostWages"/> extra days of their daily wage. Uses the
        /// game's own wage model, so mercenaries cost their usual half-again more.</summary>
        private int ComputeTrainingCost()
        {
            if (_config.TrainingCostWages <= 0) return 0;
            try
            {
                var model = Campaign.Current.Models.PartyWageModel;
                var total = 0;
                foreach (var el in MobileParty.MainParty.MemberRoster.GetTroopRoster())
                {
                    if (el.Character == null || el.Character == CharacterObject.PlayerCharacter) continue;
                    total += model.GetCharacterWage(el.Character) * el.Number;
                }
                return total * _config.TrainingCostWages;
            }
            catch
            {
                return 0;
            }
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
            try { GameMenu.ExitToLast(); } catch { }
        }

        // ------------------------------ the hotkey door ------------------------------

        /// <summary>Called every application tick from <see cref="SubModule"/>.</summary>
        internal void TickHotkey()
        {
            if (Campaign.Current == null || TaleWorlds.MountAndBlade.Mission.Current != null) return;
            if (!(Game.Current?.GameStateManager?.ActiveState is MapState mapState)) return;

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

            // Finalize the training the moment it is truly decided — WITHOUT waiting for (or
            // trusting) vanilla's wrap-up menus. Vanilla owns every non-happy path (the
            // auto-resolve wrap, retreat, defeat: capture menus, member scatter, re-attack
            // screens); politely waiting for them left the aftermath late or lost (Anton's
            // second playtest). Two triggers:
            if (_checkResults && TaleWorlds.MountAndBlade.Mission.Current == null)
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

        /// <summary>Step one of the composer: pick the enemy's culture. Main cultures first, then
        /// any other culture that fields a troop tree (looters, sea raiders, the bandit clans...).</summary>
        private void OpenMockEnemyComposer()
        {
            List<CultureObject> cultures;
            try
            {
                cultures = new List<CultureObject>();
                foreach (var culture in TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObjectTypeList<CultureObject>())
                {
                    if (culture != null && TroopTreeOf(culture).Count > 0) cultures.Add(culture);
                }
                cultures.Sort((a, b) => a.IsMainCulture != b.IsMainCulture
                    ? (a.IsMainCulture ? -1 : 1)
                    : string.Compare(a.Name?.ToString(), b.Name?.ToString(), StringComparison.Ordinal));
            }
            catch
            {
                cultures = new List<CultureObject>();
            }
            if (cultures.Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Training Battles: no culture offers a troop tree to compose from."));
                return;
            }
            var elements = new List<InquiryElement>();
            foreach (var culture in cultures)
            {
                var label = (culture.Name?.ToString() ?? culture.StringId)
                    + (culture.IsMainCulture ? "" : " (bandits)");
                elements.Add(new InquiryElement(culture, label, null, true,
                    TroopTreeOf(culture).Count + " troop types"));
            }
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
                "Compose a mock enemy",
                "Whose banners shall the phantoms carry? Pick a culture, then build its force man by man."
                + (_mockEnemyTeam != null ? " (The current composition opens pre-loaded — add another culture's men to mix.)" : ""),
                elements, isExitShown: true, 1, 1, "Choose", "Cancel",
                picked =>
                {
                    if (picked != null && picked.Count > 0 && picked[0].Identifier is CultureObject culture)
                        OpenMockEnemyPicker(culture);
                },
                _ => { }), pauseGameActiveState: true);
        }

        /// <summary>Step two: the same party screen the split drill uses, but the pool side is a
        /// synthetic supply of the culture's whole troop tree. Everything stays a dummy roster —
        /// nothing here can touch the real party. Done with an empty left side clears the pick.</summary>
        private void OpenMockEnemyPicker(CultureObject culture)
        {
            var left = TroopRoster.CreateDummyTroopRoster();
            var leftPrisoners = TroopRoster.CreateDummyTroopRoster();
            var pool = TroopRoster.CreateDummyTroopRoster();
            var rightPrisoners = TroopRoster.CreateDummyTroopRoster();
            foreach (var troop in TroopTreeOf(culture))
                pool.AddToCounts(troop, MockPoolPerTroop);
            if (_mockEnemyTeam != null)
            {
                // The previous composition opens pre-loaded — edit it, or add this culture's men
                // on top (running the composer once per culture is how a mixed force is built).
                foreach (var el in _mockEnemyTeam.GetTroopRoster())
                {
                    if (el.Character == null) continue;
                    left.AddToCounts(el.Character, el.Number);
                }
            }
            PartyScreenHelper.OpenScreenWithDummyRoster(
                left, leftPrisoners, pool, rightPrisoners,
                new TextObject("{=TB_mock_team}Mock enemy"),
                new TextObject((culture.Name?.ToString() ?? "Troop") + " muster rolls"),
                MockEnemyMaxMen,
                pool.TotalManCount + left.TotalManCount,
                MockPickerDoneCondition,
                MockPickerClosed,
                (character, _, _, _) => true);
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

        // ------------------------------ the battle ------------------------------

        private void BeginBattle(bool playerDefends)
        {
            LaunchTraining(playerDefends, simulate: false);
        }

        private void LaunchTraining(bool playerDefends, bool simulate)
        {
            try { LaunchTrainingCore(playerDefends, simulate); }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Training Battles: the drill could not start (" + ex.Message + ")."));
                AbortLiveBattle();
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
            if (!CooldownReady(out _)) return;
            if (!CanMusterNow(out var reason))
            {
                InformationManager.DisplayMessage(new InformationMessage("Training Battles: " + reason));
                return;
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
                    _pickedTeam = null;
                    InformationManager.DisplayMessage(new InformationMessage("Training Battles: the halves could not be formed."));
                    return;
                }
            }

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

            var mapSceneWrapper = Campaign.Current.MapSceneWrapper;
            var position = MobileParty.MainParty.Position;
            var mapPatch = mapSceneWrapper.GetMapPatchAtPosition(in position);
            var sceneId = Campaign.Current.Models.SceneModel.GetBattleSceneForMapPatch(mapPatch, PlayerEncounter.IsNavalEncounter());
            // The patch-aware record makes deployment DETERMINISTIC (same lines every drill, and
            // the same lines the scout ride previews). The old string overload carried no patch
            // data, and without it the game picks a random spawn path and pivot per battle.
            CampaignMission.OpenBattleMission(ScoutMission.CreatePatchAwareRecord(sceneId));
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
            }
            catch { /* colors are decoration — a bad banner code must never stop a drill */ }
        }

        /// <summary>Undoes <see cref="ApplyOpponentLook"/>'s clan changes, whether this session made
        /// them or a crashed one did (the restore data rides in the save).</summary>
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
                        break;
                    }
                }
            }
            catch { }
            _clanRestoreData = string.Empty;
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
            try { if (PlayerEncounter.Current != null) PlayerEncounter.Finish(); } catch { }
            var opponent = _opponentParty;
            if (opponent != null)
            {
                if (_opponentIsMockEnemy) MergeMockPrisonersHome(opponent); // phantoms dissolve, our men come home
                else MergePartyBackIntoMain(opponent);
                DestroyOpponentParty(opponent);
            }
            _opponentIsMockEnemy = false;
            RestoreOpponentClanLook();
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
            _aftermathReady = false;
            _pendingPlayerWon = null;
            _opponentParty = null;
            _mainSnapshot = null;
            _opponentSnapshot = null;
            _prisonSnapshot = null;
            Models.TrainingBattlesSceneModel.PendingSceneId = null;
        }

        // ------------------------------ the aftermath ------------------------------

        private void FinishTrainingBattle()
        {
            _checkResults = false;
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
                var mapEvent = MobileParty.MainParty.MapEvent;
                if (mapEvent != null && !mapEvent.IsFinalized && !mapEvent.HasWinner)
                    mapEvent.SetOverrideWinner(mapEvent.PlayerSide);
                for (var i = 0; i < 3 && PlayerEncounter.Current != null; i++)
                    PlayerEncounter.Update();
            }
            catch { }
            try { if (PlayerEncounter.Current != null) PlayerEncounter.Finish(); } catch { }
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
                if (opponentWasMock) MergeMockPrisonersHome(opponent);
                else MergePartyBackIntoMain(opponent);
                DestroyOpponentParty(opponent);
            }

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
            var casualtiesTotal = 0;
            var woundedTotal = 0;
            var xpRestored = 0;
            var xpKeptFromDrill = 0;
            // Every drill writes a full account of the aftermath arithmetic to
            // Configs\TrainingBattles\last_drill_report.txt — per stack: what the snapshots held,
            // what the battle left, what was restored/filtered and with what rolls. When a number
            // on the party screen looks wrong, this file is the witness.
            var report = new StringBuilder();
            report.AppendLine("Training drill report — " + CampaignTime.Now
                + " | XpKept " + _config.XpKeptPercent + "% | Wounded " + _config.WoundedPercent + "% | playerWon " + playerWon);
            report.AppendLine("stack | before N/W/xp | after N/W/xp | fallen | newWounded | casualties | saveChance | woundedFinal | woundedAdjust | xpAdjust");
            if (mainSnapshot != null && opponentSnapshot != null)
            {
                var before = Combine(ToDictionary(mainSnapshot), ToDictionary(opponentSnapshot));
                var after = ToDictionary(main.MemberRoster);
                foreach (var pair in before)
                {
                    var character = pair.Key;
                    if (character == null || character.IsHero) continue; // heroes never die here; game wounds them
                    after.TryGetValue(character, out var now);

                    // EVERY casualty of the drill goes through the wounded filter — the truly dead
                    // AND the battle-wounded. The game's own surgeon converts most mission "KIA"
                    // into roster-wounded before we ever run (high Medicine = high conversion), so
                    // filtering only the dead let vanilla's wounded sail past the WoundedPercent
                    // knob entirely (Anton: 14 KIA at 10% → 14 wounded).
                    var fallen = pair.Value.Number - now.Number;
                    var newWounded = Math.Max(0, now.Wounded - pair.Value.Wounded);
                    var casualties = Math.Max(fallen, 0) + newWounded;
                    var saveChance = 0.0;
                    var woundedFinal = 0;
                    var woundedAdjust = 0;
                    if (casualties > 0)
                    {
                        saveChance = SurgeonSaveChance(main.Party, character);
                        woundedFinal = AftermathMath.WoundedAmongFallen(
                            casualties, saveChance, _config.WoundedPercent / 100.0, () => MBRandom.RandomFloat);
                        if (fallen > 0)
                            main.MemberRoster.AddToCounts(character, fallen, false, 0); // men back first
                        var finalNumber = now.Number + Math.Max(fallen, 0);
                        var desiredWounded = Math.Min(pair.Value.Wounded + woundedFinal, finalNumber);
                        woundedAdjust = desiredWounded - now.Wounded;
                        if (woundedAdjust != 0)
                            main.MemberRoster.AddToCounts(character, 0, false, woundedAdjust);
                        restored += Math.Max(fallen, 0);
                        casualtiesTotal += casualties;
                        woundedTotal += woundedFinal;
                        CreditSurgeon(main, character, casualties);
                    }

                    // Visible earnings only — anything the clamp already ate mid-battle counts as
                    // unearned (conservative, never negative). Target = old pool + kept earnings.
                    var earned = Math.Max(0, now.Xp - pair.Value.Xp);
                    var kept = AftermathMath.XpKept(earned, _config.XpKeptPercent);
                    var targetXp = pair.Value.Xp + kept;
                    var xpAdjust = targetXp - now.Xp;
                    if (xpAdjust != 0)
                        main.MemberRoster.AddToCounts(character, 0, false, 0, xpAdjust);
                    xpKeptFromDrill += kept;
                    if (xpAdjust > 0) xpRestored += xpAdjust;

                    if (casualties > 0 || xpAdjust != 0 || fallen != 0)
                    {
                        report.AppendLine(character.Name + " | "
                            + pair.Value.Number + "/" + pair.Value.Wounded + "/" + pair.Value.Xp + " | "
                            + now.Number + "/" + now.Wounded + "/" + now.Xp + " | "
                            + fallen + " | " + newWounded + " | " + casualties + " | "
                            + saveChance.ToString("0.00") + " | " + woundedFinal + " | "
                            + woundedAdjust + " | " + xpAdjust);
                    }
                }
            }
            report.AppendLine("TOTALS: casualties " + casualtiesTotal + " | wake wounded " + woundedTotal
                + " | dead restored " + restored + " | xp kept " + xpKeptFromDrill + " | xp restored " + xpRestored);
            try
            {
                System.IO.Directory.CreateDirectory(ModConfig.ConfigDirectory);
                System.IO.File.WriteAllText(
                    System.IO.Path.Combine(ModConfig.ConfigDirectory, "last_drill_report.txt"),
                    report.ToString());
            }
            catch { }

            // 3. The drill leaves its honest marks.
            if (_config.DisorganizedAfterTraining)
            {
                try { main.SetDisorganized(true); } catch { }
            }
            _lastTrainingHours = (float)CampaignTime.Now.ToHours;

            var summary = (opponentWasMock
                    ? (playerWon ? "You carried the field. " : "The mock enemy carried the field. ")
                    : (playerWon ? "Your half carried the field. " : "The other half carried the field. "))
                + (casualtiesTotal > 0
                    ? casualtiesTotal + " men fell or were hurt — " + woundedTotal + " wake up wounded, the rest shrug it off."
                    : "Not a man stayed down.")
                + " Drill XP kept: " + xpKeptFromDrill
                + (xpRestored > 0 ? " (and " + xpRestored + " upgrade XP restored to the stacks)." : ".");
            InformationManager.DisplayMessage(new InformationMessage("Training over. " + summary));

            try { if (Campaign.Current?.CurrentMenuContext != null) GameMenu.ExitToLast(); } catch { }
        }

        private static double SurgeonSaveChance(PartyBase party, CharacterObject character)
        {
            try
            {
                // The game's own survival math — Medicine-driven, so a good doctor helps.
                return Campaign.Current.Models.PartyHealingModel.GetSurvivalChance(
                    party, character, DamageTypes.Cut, canDamageKillEvenIfBlunt: false, null);
            }
            catch
            {
                return 0.5;
            }
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
                    DestroyOpponentParty(party);
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Training Battles: an interrupted drill was found — the men have returned to the company."));
                }
                foreach (var party in staleMock)
                {
                    // Its men were never ours — merging them home would MINT free troops. The
                    // phantoms dissolve; only its prisoner wagons (ours, if anyone) walk back.
                    MergeMockPrisonersHome(party);
                    DestroyOpponentParty(party);
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Training Battles: an interrupted mock drill was found — the phantom enemy has dissolved."));
                }
            }
            catch { }
        }
    }
}
