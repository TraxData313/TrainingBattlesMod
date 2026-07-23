using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TrainingBattles.Models
{
    /// <summary>
    /// The "it was only training" guard: while a training battle is active, no renown, no
    /// influence, no morale swings, no relation changes, no gold plundered or lost, no items
    /// looted from the fallen, and nobody taken prisoner — sparring must leave the campaign
    /// exactly as it found it. Outside training, everything delegates to the vanilla model.
    /// </summary>
    public sealed class TrainingBattleRewardModel : DefaultBattleRewardModel
    {
        private static bool Training => TrainingBattleBehavior.TrainingActive;

        public override ExplainedNumber CalculateRenownGain(PartyBase winnerParty, float renownValueOfBattleForWinnerSide, float contributionShareOfWinnerParty, float renownMultiplierForWinnerSide, bool includeDescriptions)
        {
            return Training
                ? new ExplainedNumber(0f)
                : base.CalculateRenownGain(winnerParty, renownValueOfBattleForWinnerSide, contributionShareOfWinnerParty, renownMultiplierForWinnerSide, includeDescriptions);
        }

        public override ExplainedNumber CalculateInfluenceGain(PartyBase winnerParty, float influenceValueOfBattleForWinnerSide, float contributionShareOfWinnerParty, float influenceMultiplierForWinnerSide, bool includeDescriptions)
        {
            return Training
                ? new ExplainedNumber(0f)
                : base.CalculateInfluenceGain(winnerParty, influenceValueOfBattleForWinnerSide, contributionShareOfWinnerParty, influenceMultiplierForWinnerSide, includeDescriptions);
        }

        public override ExplainedNumber CalculateMoraleGainVictory(PartyBase winnerParty, float renownValueOfBattleForWinnerSide, float contributionShareOfWinnerParty, bool includeDescriptions)
        {
            return Training
                ? new ExplainedNumber(0f)
                : base.CalculateMoraleGainVictory(winnerParty, renownValueOfBattleForWinnerSide, contributionShareOfWinnerParty, includeDescriptions);
        }

        public override float CalculateMoraleChangeOnRoundVictory(PartyBase party, MapEventSide partySide, BattleSideEnum roundWinner)
        {
            return Training ? 0f : base.CalculateMoraleChangeOnRoundVictory(party, partySide, roundWinner);
        }

        public override int GetPlayerGainedRelationAmount(MapEvent mapEvent, Hero hero)
        {
            return Training ? 0 : base.GetPlayerGainedRelationAmount(mapEvent, hero);
        }

        public override int CalculateGoldLossAfterDefeat(Hero partyLeaderHero)
        {
            return Training ? 0 : base.CalculateGoldLossAfterDefeat(partyLeaderHero);
        }

        public override int CalculatePlunderedGoldAmountFromDefeatedParty(PartyBase defeatedParty)
        {
            return Training ? 0 : base.CalculatePlunderedGoldAmountFromDefeatedParty(defeatedParty);
        }

        public override EquipmentElement GetLootedItemFromTroop(CharacterObject character, float targetValue)
        {
            return Training ? default : base.GetLootedItemFromTroop(character, targetValue);
        }

        public override float GetExpectedLootedItemValueFromCasualty(Hero winnerPartyLeaderHero, CharacterObject casualtyCharacter)
        {
            return Training ? 0f : base.GetExpectedLootedItemValueFromCasualty(winnerPartyLeaderHero, casualtyCharacter);
        }

        public override float GetBannerLootChanceFromDefeatedHero(Hero defeatedHero)
        {
            return Training ? 0f : base.GetBannerLootChanceFromDefeatedHero(defeatedHero);
        }

        public override ItemObject GetBannerRewardForWinningMapEvent(MapEvent mapEvent)
        {
            return Training ? null! : base.GetBannerRewardForWinningMapEvent(mapEvent);
        }

        public override bool CanTroopBeTakenPrisoner(CharacterObject troop)
        {
            return !Training && base.CanTroopBeTakenPrisoner(troop);
        }

        public override float GetMainPartyMemberScatterChance()
        {
            // On a real defeat some of the player's men scatter and desert — after a lost DRILL
            // they just dust themselves off. (Found via Anton's defeat playtest.)
            return Training ? 0f : base.GetMainPartyMemberScatterChance();
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
            base.GetCaptureMemberChancesForWinnerParties(endedMapEvent, winnerParties, out woundedMemberChances, out healthyMemberChances);
        }
    }
}
