using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TrainingBattles.Models
{
    /// <summary>
    /// The "it was only training" guard: while a training battle is active, no renown, no
    /// influence, no morale swings, no relation changes, no gold plundered or lost, no items
    /// looted from the fallen, nobody taken prisoner — and at sea, no ships changing hands, no
    /// post-defeat hull damage, no figurehead loot. Sparring must leave the campaign exactly as
    /// it found it.
    ///
    /// A DECORATOR, not a replacement (2026.07.25): the model extends the abstract
    /// BattleRewardModel and delegates every call to whichever model was registered before us —
    /// AddModel hands it over as BaseModel. The old version extended DefaultBattleRewardModel
    /// directly, which silently REPLACED War Sails' NavalDLCBattleRewardModel in every REAL
    /// battle: no figurehead drops, no ship distribution among winners, vanilla capture chances
    /// without the naval perks. Same class of bug the SceneModel/MapWeatherModel decorators
    /// were built against — the reward model just predated the lesson.
    /// </summary>
    public sealed class TrainingBattleRewardModel : BattleRewardModel
    {
        private static bool Training => TrainingBattleBehavior.TrainingActive;

        private BattleRewardModel? _fallback;

        // BaseModel is null only if nothing registered a BattleRewardModel before us — should
        // never happen (vanilla always does), but a battle must settle regardless.
        private BattleRewardModel Chain => BaseModel ?? (_fallback ??= new DefaultBattleRewardModel());

        // ------------------------------ guarded while training ------------------------------

        public override ExplainedNumber CalculateRenownGain(PartyBase winnerParty, float renownValueOfBattleForWinnerSide, float contributionShareOfWinnerParty, float renownMultiplierForWinnerSide, bool includeDescriptions)
        {
            return Training
                ? new ExplainedNumber(0f)
                : Chain.CalculateRenownGain(winnerParty, renownValueOfBattleForWinnerSide, contributionShareOfWinnerParty, renownMultiplierForWinnerSide, includeDescriptions);
        }

        public override ExplainedNumber CalculateInfluenceGain(PartyBase winnerParty, float influenceValueOfBattleForWinnerSide, float contributionShareOfWinnerParty, float influenceMultiplierForWinnerSide, bool includeDescriptions)
        {
            return Training
                ? new ExplainedNumber(0f)
                : Chain.CalculateInfluenceGain(winnerParty, influenceValueOfBattleForWinnerSide, contributionShareOfWinnerParty, influenceMultiplierForWinnerSide, includeDescriptions);
        }

        public override ExplainedNumber CalculateMoraleGainVictory(PartyBase winnerParty, float renownValueOfBattleForWinnerSide, float contributionShareOfWinnerParty, bool includeDescriptions)
        {
            return Training
                ? new ExplainedNumber(0f)
                : Chain.CalculateMoraleGainVictory(winnerParty, renownValueOfBattleForWinnerSide, contributionShareOfWinnerParty, includeDescriptions);
        }

        public override float CalculateMoraleChangeOnRoundVictory(PartyBase party, MapEventSide partySide, BattleSideEnum roundWinner)
        {
            return Training ? 0f : Chain.CalculateMoraleChangeOnRoundVictory(party, partySide, roundWinner);
        }

        public override int GetPlayerGainedRelationAmount(MapEvent mapEvent, Hero hero)
        {
            return Training ? 0 : Chain.GetPlayerGainedRelationAmount(mapEvent, hero);
        }

        public override int CalculateGoldLossAfterDefeat(Hero partyLeaderHero)
        {
            return Training ? 0 : Chain.CalculateGoldLossAfterDefeat(partyLeaderHero);
        }

        public override int CalculatePlunderedGoldAmountFromDefeatedParty(PartyBase defeatedParty)
        {
            return Training ? 0 : Chain.CalculatePlunderedGoldAmountFromDefeatedParty(defeatedParty);
        }

        /// <summary>Nobody rifles the baggage of his own quartermaster: with no winner in the
        /// chance list, MapEvent.LootDefeatedPartyItems never distributes the defeated party's
        /// inventory — so nothing enters RosterToReceiveLootItems and the post-battle loot
        /// screen (PlayerEncounter.DoLootInventory) simply never opens. Players seeing a full
        /// loot screen before the "returned" message read it as a dupe bug (Anton, 2026.07.24).</summary>
        public override MBList<KeyValuePair<MapEventParty, float>> GetLootItemChancesForWinnerParties(MBReadOnlyList<MapEventParty> winnerParties, PartyBase defeatedParty)
        {
            return Training
                ? new MBList<KeyValuePair<MapEventParty, float>>()
                : Chain.GetLootItemChancesForWinnerParties(winnerParties, defeatedParty);
        }

        /// <summary>Same door for the fallen's gear: an empty chance list makes
        /// MapEvent.LootDefeatedPartyCasualties skip every body untouched.</summary>
        public override MBReadOnlyList<KeyValuePair<MapEventParty, float>> GetLootCasualtyChances(MBReadOnlyList<MapEventParty> winnerParties, PartyBase defeatedParty)
        {
            return Training
                ? new MBList<KeyValuePair<MapEventParty, float>>()
                : Chain.GetLootCasualtyChances(winnerParties, defeatedParty);
        }

        public override EquipmentElement GetLootedItemFromTroop(CharacterObject character, float targetValue)
        {
            return Training ? default : Chain.GetLootedItemFromTroop(character, targetValue);
        }

        public override float GetExpectedLootedItemValueFromCasualty(Hero winnerPartyLeaderHero, CharacterObject casualtyCharacter)
        {
            return Training ? 0f : Chain.GetExpectedLootedItemValueFromCasualty(winnerPartyLeaderHero, casualtyCharacter);
        }

        public override float GetBannerLootChanceFromDefeatedHero(Hero defeatedHero)
        {
            return Training ? 0f : Chain.GetBannerLootChanceFromDefeatedHero(defeatedHero);
        }

        public override ItemObject GetBannerRewardForWinningMapEvent(MapEvent mapEvent)
        {
            return Training ? null! : Chain.GetBannerRewardForWinningMapEvent(mapEvent);
        }

        public override bool CanTroopBeTakenPrisoner(CharacterObject troop)
        {
            return !Training && Chain.CanTroopBeTakenPrisoner(troop);
        }

        public override float GetMainPartyMemberScatterChance()
        {
            // On a real defeat some of the player's men scatter and desert — after a lost DRILL
            // they just dust themselves off. (Found via Anton's defeat playtest.)
            return Training ? 0f : Chain.GetMainPartyMemberScatterChance();
        }

        public override void GetCaptureMemberChancesForWinnerParties(MapEvent endedMapEvent, MBReadOnlyList<MapEventParty> winnerParties, out MBList<KeyValuePair<MapEventParty, float>> woundedMemberChances, out MBList<KeyValuePair<MapEventParty, float>> healthyMemberChances)
        {
            if (Training)
            {
                // Nobody carries a comrade off in chains after a drill.
                woundedMemberChances = new MBList<KeyValuePair<MapEventParty, float>>();
                healthyMemberChances = new MBList<KeyValuePair<MapEventParty, float>>();
                return;
            }
            Chain.GetCaptureMemberChancesForWinnerParties(endedMapEvent, winnerParties, out woundedMemberChances, out healthyMemberChances);
        }

        // ------------------------------ the sea's own spoils ------------------------------

        /// <summary>No hulls change hands over a drill: an empty distribution makes
        /// MapEvent.LootDefeatedPartyShips transfer nothing (and strip no figureheads — the
        /// distributor removes them from half the loot before dealing). The aftermath's fleet
        /// restore is the belt to this suspender.</summary>
        public override MBReadOnlyList<KeyValuePair<Ship, MapEventParty>> DistributeDefeatedPartyShipsAmongWinners(MapEvent mapEvent, MBReadOnlyList<Ship> shipsToLoot, MBReadOnlyList<MapEventParty> winnerParties)
        {
            return Training
                ? new MBList<KeyValuePair<Ship, MapEventParty>>()
                : Chain.DistributeDefeatedPartyShipsAmongWinners(mapEvent, shipsToLoot, winnerParties);
        }

        /// <summary>The defeated side's surviving hulls take a 20–50% damage roll after a real
        /// battle — a drill's hulls are healed back anyway, but zeroing the roll keeps any
        /// mid-flow observer (and any other mod's hooks) from ever seeing the dent.</summary>
        public override float CalculateShipDamageAfterDefeat(Ship ship)
        {
            return Training ? 0f : Chain.CalculateShipDamageAfterDefeat(ship);
        }

        public override float GetSunkenShipMoraleEffect(PartyBase shipOwner, Ship ship)
        {
            // Watching your own training hulk go under stings nobody's morale ledger.
            return Training ? 0f : Chain.GetSunkenShipMoraleEffect(shipOwner, ship);
        }

        public override float GetShipSiegeEngineHitMoraleEffect(Ship ship, SiegeEngineType siegeEngineType)
        {
            return Training ? 0f : Chain.GetShipSiegeEngineHitMoraleEffect(ship, siegeEngineType);
        }

        public override Figurehead GetFigureheadLoot(MBReadOnlyList<MapEventParty> defeatedParties, PartyBase defeatedSideLeaderParty)
        {
            // You cannot "loot" a figurehead off your own opposing half.
            return Training ? null! : Chain.GetFigureheadLoot(defeatedParties, defeatedSideLeaderParty);
        }

        public override MBReadOnlyList<MapEventParty> GetWinnerPartiesThatCanPlunderGoldFromShips(MBReadOnlyList<MapEventParty> winnerParties)
        {
            return Training
                ? new MBList<MapEventParty>()
                : Chain.GetWinnerPartiesThatCanPlunderGoldFromShips(winnerParties);
        }

        // ------------------------------ pure pass-throughs ------------------------------

        public override MBReadOnlyList<KeyValuePair<MapEventParty, float>> GetLootGoldChances(MBReadOnlyList<MapEventParty> winnerParties)
        {
            // Gold plunder is already zeroed via CalculatePlunderedGoldAmountFromDefeatedParty;
            // the chance table itself can stay honest.
            return Chain.GetLootGoldChances(winnerParties);
        }

        public override MBReadOnlyList<KeyValuePair<MapEventParty, float>> GetLootPrisonerChances(MBReadOnlyList<MapEventParty> winnerParties, TroopRosterElement prisonerElement)
        {
            return Chain.GetLootPrisonerChances(winnerParties, prisonerElement);
        }

        public override float GetAITradePenalty()
        {
            return Chain.GetAITradePenalty();
        }
    }
}
