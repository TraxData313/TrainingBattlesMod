using System;
using System.Collections.Generic;
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
    /// </summary>
    public sealed class TrainingBattleBehavior : CampaignBehaviorBase
    {
        public const string MenuId = "training_battle_menu";
        private const string OpponentPartyIdPrefix = "training_opponents";

        private readonly ModConfig _config;

        /// <summary>Read by <see cref="Models.TrainingBattleRewardModel"/> to zero out every
        /// campaign consequence (renown, loot, prisoners...) while a training battle runs.</summary>
        public static bool TrainingActive { get; private set; }

        internal static TrainingBattleBehavior? Instance { get; private set; }

        // Persisted: when the last training battle ended, in campaign hours (0 = never).
        private float _lastTrainingHours;

        // Transient flow state — never saved; a mid-flow save/load resolves via the stale-party
        // recovery in OnSessionLaunched.
        private TroopRoster? _pickedTeam;          // the chosen opponents; real rosters untouched until Begin
        private MobileParty? _opponentParty;
        private TroopRoster? _mainSnapshot;        // main party AFTER the split, before the fight
        private TroopRoster? _opponentSnapshot;    // opponent party before the fight
        private bool _checkResults;
        private bool _returnToMenuPending;         // set by the picker's close; honored on the next tick

        public TrainingBattleBehavior(ModConfig config)
        {
            _config = config;
            Instance = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("TrainingBattles_LastTrainingHours", ref _lastTrainingHours);
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // Fresh session: no picked team, no live battle — whatever the previous session left.
            TrainingActive = false;
            _pickedTeam = null;
            _opponentParty = null;
            _mainSnapshot = null;
            _opponentSnapshot = null;
            _checkResults = false;
            RecoverStaleOpponentParties();
            AddMenus(starter);
        }

        // ------------------------------ the muster menu ------------------------------

        private void AddMenus(CampaignGameStarter starter)
        {
            starter.AddGameMenu(MenuId, "{TRAINING_MENU_TEXT}", MenuInit);
            starter.AddGameMenuOption(MenuId, "training_pick",
                "{=TB_opt_pick}Divide the men for a training battle",
                PickCondition, _ => OpenPicker());
            starter.AddGameMenuOption(MenuId, "training_begin_attack",
                "{=TB_opt_attack}Begin — your half attacks",
                args => BeginCondition(args), _ => BeginBattle(playerDefends: false));
            starter.AddGameMenuOption(MenuId, "training_begin_defend",
                "{=TB_opt_defend}Begin — your half defends",
                args => BeginCondition(args), _ => BeginBattle(playerDefends: true));
            starter.AddGameMenuOption(MenuId, "training_cancel",
                "{=TB_opt_cancel}Cancel training",
                CancelCondition, _ => CancelTraining(), isLeave: true);
        }

        private void MenuInit(MenuCallbackArgs args)
        {
            if (_checkResults)
            {
                FinishTrainingBattle();
                return;
            }
            MBTextManager.SetTextVariable("TRAINING_MENU_TEXT", BuildMenuText(), false);
        }

        private string BuildMenuText()
        {
            if (_pickedTeam != null && _pickedTeam.TotalManCount > 0)
            {
                var yours = MobileParty.MainParty.MemberRoster.TotalHealthyCount - _pickedTeam.TotalHealthyCount;
                return "The two halves stand ready on the field: " + _pickedTeam.TotalHealthyCount
                     + " men opposite, " + Math.Max(yours, 0)
                     + " with you. Choose your side of the exercise — or call it off.";
            }
            if (!CooldownReady(out var remaining))
            {
                return "The men are still worn from the last drill. They will be ready to muster again in about "
                     + Math.Ceiling(remaining) + " hours.";
            }
            return "You call the company to a training muster. Divide the men into two halves, "
                 + "choose your side, and drill on the very ground you stand on. Nobody dies in "
                 + "training — but wounds, sweat and lessons are real.";
        }

        private bool PickCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Manage;
            if (!CooldownReady(out var remaining))
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject("{=TB_tip_rest}The men need rest — ready in about " + Math.Ceiling(remaining) + " hours.");
            }
            else if (MobileParty.MainParty.MemberRoster.TotalHealthyCount < 2)
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject("{=TB_tip_few}You need at least two healthy souls to hold a drill.");
            }
            return true;
        }

        private bool BeginCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Mission;
            if (_pickedTeam == null || _pickedTeam.TotalHealthyCount < 1)
            {
                args.IsEnabled = false;
                args.Tooltip = new TextObject("{=TB_tip_pick_first}Divide the men first.");
            }
            return true;
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
            try { GameMenu.ExitToLast(); } catch { }
        }

        // ------------------------------ the hotkey door ------------------------------

        /// <summary>Called every application tick from <see cref="SubModule"/>.</summary>
        internal void TickHotkey()
        {
            if (Campaign.Current == null || TaleWorlds.MountAndBlade.Mission.Current != null) return;
            if (!(Game.Current?.GameStateManager?.ActiveState is MapState mapState)) return;

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
                : InputKey.T;
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

        // ------------------------------ the battle ------------------------------

        private void BeginBattle(bool playerDefends)
        {
            try { BeginBattleCore(playerDefends); }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Training Battles: the drill could not start (" + ex.Message + ")."));
                AbortLiveBattle();
            }
        }

        private void BeginBattleCore(bool playerDefends)
        {
            var picked = _pickedTeam;
            if (picked == null || picked.TotalHealthyCount < 1)
            {
                InformationManager.DisplayMessage(new InformationMessage("Training Battles: divide the men first."));
                return;
            }
            if (!CooldownReady(out _)) return;
            if (!CanMusterNow(out var reason))
            {
                InformationManager.DisplayMessage(new InformationMessage("Training Battles: " + reason));
                return;
            }

            var main = MobileParty.MainParty;
            var opponent = CreateOpponentParty();
            if (opponent == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("Training Battles: could not raise the opposing half."));
                return;
            }

            // Move the picked men across — clamped against the live roster so a stale pick can
            // never take more than the party truly has.
            var have = ToDictionary(main.MemberRoster);
            var moved = 0;
            foreach (var el in picked.GetTroopRoster())
            {
                if (el.Character == null || el.Character == CharacterObject.PlayerCharacter) continue;
                if (!have.TryGetValue(el.Character, out var live)) continue;
                var take = Math.Min(el.Number, live.Number);
                if (take <= 0) continue;
                var takeWounded = Math.Min(Math.Min(el.WoundedNumber, live.Wounded), take);
                main.MemberRoster.AddToCounts(el.Character, -take, false, -takeWounded);
                opponent.MemberRoster.AddToCounts(el.Character, take, false, takeWounded);
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

            _opponentParty = opponent;
            _pickedTeam = null;
            _mainSnapshot = main.MemberRoster.CloneRosterData();
            _opponentSnapshot = opponent.MemberRoster.CloneRosterData();
            TrainingActive = true;
            _checkResults = true;

            try
            {
                // The vanilla forced-battle recipe (Company of Trouble quest).
                PlayerEncounter.Start();
                PlayerEncounter.Current.SetupFields(
                    playerDefends ? opponent.Party : PartyBase.MainParty,
                    playerDefends ? PartyBase.MainParty : opponent.Party);
                PlayerEncounter.StartBattle();
                var mapSceneWrapper = Campaign.Current.MapSceneWrapper;
                var position = MobileParty.MainParty.Position;
                var mapPatch = mapSceneWrapper.GetMapPatchAtPosition(in position);
                CampaignMission.OpenBattleMission(
                    Campaign.Current.Models.SceneModel.GetBattleSceneForMapPatch(mapPatch, PlayerEncounter.IsNavalEncounter()),
                    usesTownDecalAtlas: false);
            }
            catch (Exception ex)
            {
                // The field failed us — unwind honestly: men home, party gone, no cooldown burned.
                InformationManager.DisplayMessage(new InformationMessage("Training Battles: the drill could not start (" + ex.Message + ")."));
                AbortLiveBattle();
            }
        }

        private MobileParty? CreateOpponentParty()
        {
            try
            {
                var hideoutSettlement = SettlementHelper.FindRandomSettlement(s => s.IsHideout);
                var hideout = hideoutSettlement?.Hideout;
                var clan = hideoutSettlement?.OwnerClan;
                if (hideout == null || clan == null) return null;
                var party = BanditPartyComponent.CreateBanditParty(
                    OpponentPartyIdPrefix + "_" + DateTime.UtcNow.Ticks,
                    clan, hideout, isBossParty: false, null, MobileParty.MainParty.Position);
                party.Party.SetCustomName(new TextObject("{=TB_opponents_name}Training Opponents"));
                party.SetPartyUsedByQuest(isActivelyUsed: true);
                return party;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Tears down a battle that failed to start: men merged home, temp party destroyed,
        /// no aftermath, no cooldown.</summary>
        private void AbortLiveBattle()
        {
            try { if (PlayerEncounter.Current != null) PlayerEncounter.Finish(); } catch { }
            var opponent = _opponentParty;
            if (opponent != null)
            {
                MergePartyBackIntoMain(opponent);
                DestroyOpponentParty(opponent);
            }
            TrainingActive = false;
            _checkResults = false;
            _opponentParty = null;
            _mainSnapshot = null;
            _opponentSnapshot = null;
        }

        // ------------------------------ the aftermath ------------------------------

        private void FinishTrainingBattle()
        {
            _checkResults = false;
            var main = MobileParty.MainParty;
            var opponent = _opponentParty;

            bool playerWon = false;
            try
            {
                var battle = PlayerEncounter.Battle;
                playerWon = battle != null && battle.WinningSide == battle.PlayerSide;
            }
            catch { }
            try { PlayerEncounter.Finish(); } catch { }
            TrainingActive = false;

            var mainSnapshot = _mainSnapshot;
            var opponentSnapshot = _opponentSnapshot;
            _mainSnapshot = null;
            _opponentSnapshot = null;
            _opponentParty = null;

            // 1. Everyone comes home: survivors of the opposing half (and anyone who somehow ended
            //    up a prisoner) rejoin the main party.
            if (opponent != null)
            {
                MergePartyBackIntoMain(opponent);
                DestroyOpponentParty(opponent);
            }

            // 2. Nobody dies in training: the fallen return — some wounded, per the surgeon's own
            //    Medicine-driven save and the configured share — and earned XP is scaled.
            var restored = 0;
            var woundedTotal = 0;
            if (mainSnapshot != null && opponentSnapshot != null)
            {
                var before = Combine(ToDictionary(mainSnapshot), ToDictionary(opponentSnapshot));
                var after = ToDictionary(main.MemberRoster);
                foreach (var pair in before)
                {
                    var character = pair.Key;
                    if (character == null || character.IsHero) continue; // heroes never die here; game wounds them
                    after.TryGetValue(character, out var now);

                    var fallen = pair.Value.Number - now.Number;
                    if (fallen > 0)
                    {
                        var saveChance = SurgeonSaveChance(main.Party, character);
                        var wounded = AftermathMath.WoundedAmongFallen(
                            fallen, saveChance, _config.WoundedPercent / 100.0, () => MBRandom.RandomFloat);
                        main.MemberRoster.AddToCounts(character, fallen, false, wounded);
                        restored += fallen;
                        woundedTotal += wounded;
                        CreditSurgeon(main, character, fallen);
                    }

                    var xpEarned = now.Xp - pair.Value.Xp;
                    var xpToRemove = AftermathMath.XpToRemove(xpEarned, _config.XpKeptPercent);
                    if (xpToRemove > 0)
                        main.MemberRoster.AddToCounts(character, 0, false, 0, -xpToRemove);
                }
            }

            // 3. The drill leaves its honest marks.
            if (_config.DisorganizedAfterTraining)
            {
                try { main.SetDisorganized(true); } catch { }
            }
            _lastTrainingHours = (float)CampaignTime.Now.ToHours;

            var summary = (playerWon ? "Your half carried the field. " : "The other half carried the field. ")
                + (restored > 0
                    ? restored + " fallen picked themselves up — " + woundedTotal + " carried to the wagons wounded."
                    : "Not a man stayed down.")
                + (_config.XpKeptPercent < 100 ? " The lessons stick at " + _config.XpKeptPercent + "%." : "");
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
                foreach (var party in MobileParty.All)
                {
                    if (party?.StringId != null && party.StringId.StartsWith(OpponentPartyIdPrefix, StringComparison.Ordinal))
                        stale.Add(party);
                }
                foreach (var party in stale)
                {
                    MergePartyBackIntoMain(party);
                    DestroyOpponentParty(party);
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Training Battles: an interrupted drill was found — the men have returned to the company."));
                }
            }
            catch { }
        }
    }
}
