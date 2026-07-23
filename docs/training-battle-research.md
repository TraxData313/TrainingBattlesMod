# Training battle research — verified against the real game DLLs (2026.07.23)

Everything below was read from the game's own decompiled source (this game version, the one
installed on disk), not from wiki lore. The decompiled corpus lives at
`..\reference\game-decompiled\` (TaleWorlds.CampaignSystem, SandBox, TaleWorlds.MountAndBlade)
— regenerate with `ilspycmd -p -o <out> <dll>` (needs `$env:DOTNET_ROLL_FORWARD='LatestMajor'`
on this machine; SandBox.dll lives under `Modules\SandBox\bin\Win64_Shipping_Client`, not the
main `bin`).

**Verdict: the whole V1 is achievable with public APIs and exact vanilla precedent.
No Harmony strictly required for the core loop.**

## 1. The troop-picker GUI — `PartyScreenHelper` (namespace `Helpers`, TaleWorlds.CampaignSystem)

`Helpers\PartyScreenHelper.cs` is the door to every party-screen variant. The one made for us:

```csharp
PartyScreenHelper.OpenScreenWithDummyRosterWithMainParty(
    TroopRoster leftMemberRoster,       // Team 1 — starts empty, player fills it
    TroopRoster leftPrisonerRoster,     // dummy
    TextObject leftPartyName,           // "Training — Team 1"
    int leftPartySizeLimit,
    PartyPresentationDoneButtonConditionDelegate doneButtonCondition,  // e.g. both sides non-empty
    PartyScreenClosedDelegate onPartyScreenClosed,                     // fromCancel flag included!
    IsTroopTransferableDelegate isTroopTransferable,
    PartyPresentationCancelButtonActivateDelegate cancelActivate = null);
```

It delegates to `OpenScreenWithDummyRoster(...)` which sets
`AccompanyingTransferState = Transferable` — **heroes/companions can be moved across**, and
`PartyScreenClosedDelegate` receives `fromCancel`, so Cancel is free. This same family serves
the alley/quest "pick your men" screens (the lair-attack feel Anton wants).
`OpenScreenAsQuest(...)` is the alternative shape with a progress bar.

## 2. The mock battle — exact vanilla recipe (Company of Trouble quest)

`TaleWorlds.CampaignSystem.Issues\LandLordCompanyOfTroubleIssueBehavior.cs` (~line 686 and
905) runs a forced field battle against a temp party made FROM THE PLAYER'S OWN TROOPS —
literally our feature, shipped in vanilla:

```csharp
// Build the opponent from the player's own roster:
MobileParty.MainParty.MemberRoster.AddToCounts(troop, -count);        // pull Team 2 out
var hideout = SettlementHelper.FindRandomSettlement(x => x.IsHideout);
_party = BanditPartyComponent.CreateBanditParty("training_" + id, hideout.OwnerClan,
         hideout.Hideout, isBossParty: false, null, MobileParty.MainParty.Position);
_party.MemberRoster.AddToCounts(troop, count);                        // put Team 2 in
_party.Party.SetCustomName(new TextObject("..."));
_party.SetPartyUsedByQuest(isActivelyUsed: true);                     // shields it from world AI

// In our own GameMenu (menu owns the whole flow — no vanilla encounter menu involved):
PlayerEncounter.Start();
PlayerEncounter.Current.SetupFields(PartyBase.MainParty, _party.Party);
PlayerEncounter.StartBattle();
var patch = Campaign.Current.MapSceneWrapper.GetMapPatchAtPosition(MobileParty.MainParty.Position);
CampaignMission.OpenBattleMission(
    Campaign.Current.Models.SceneModel.GetBattleSceneForMapPatch(patch, PlayerEncounter.IsNavalEncounter()),
    usesTownDecalAtlas: false);

// After the mission the menu's on_init runs again:
bool playerWon = PlayerEncounter.Battle.WinningSide == PlayerEncounter.Battle.PlayerSide;
PlayerEncounter.Finish();                                             // skips loot screen in this flow
DestroyPartyAction.Apply(null, _party);
```

The bandit-clan ownership is what makes the encounter "hostile enough" without touching real
diplomacy; `SetPartyUsedByQuest` keeps world AI off it. Cancel training = never call
`PlayerEncounter.Start()`, merge Team 2's roster back, exit menu.

## 3. Aftermath — all public APIs

- **Snapshot/diff**: clone both rosters before the fight (`TroopRoster.CloneRosterData()`),
  diff after (count, woundedCount, xp per element) → who "died", what XP was earned.
  `AddToCounts(character, count, insertAtFront, woundedCount, xpChange)` lets us put
  everything back with exact wounded/xp arithmetic.
- **Dead → wounded**: the vanilla surgeon math is
  `Campaign.Current.Models.PartyHealingModel.GetSurvivalChance(party, character, damageType,
  canDamageKillEvenIfBlunt, enemyParty)` (Medicine-driven — "a good doctor helps" comes from
  the game's own model), and `SkillLevelingManager.OnSurgeryApplied(party, success, troopTier)`
  gives the surgeon their Medicine XP per save. Our config factor stacks on top.
- **Disorganized**: `MobileParty.SetDisorganized(true)` — public, one call; it's exactly what
  `DisorganizedStateCampaignBehavior` does after real battles. Duration is governed by
  `PartyImpairmentModel` (base 6h).
- **No renown/influence/morale from training**: `MapEvent` end-of-battle calls
  `BattleRewardModel.CalculateRenownGain/InfluenceGain/MoraleGain...`. Register our own
  `BattleRewardModel` (campaign model overrides are stock modding: add in
  `OnGameStart` after vanilla models) that returns zero while a training battle is active and
  delegates otherwise. Same trick powers the terrain picker (below). No Harmony.

## 4. Terrain seeing/picking — `SceneModel` override

`DefaultSceneModel.GetBattleSceneForMapPatch(MapPatchData, bool isNavalEncounter)`:

```csharp
var candidates = GameSceneDataManager.Instance.SingleplayerBattleScenes
    .Where(s => s.MapIndices.Contains(mapPatch.sceneIndex) && s.IsNaval == isNaval);
// multiple matches → vanilla picks RANDOM (their own comment admits it)
```

`SingleplayerBattleSceneData` carries `SceneID`, `Terrain`, `IsNaval`, `MapIndices` — so we
can ENUMERATE the candidate battle maps for the player's current position and let them choose.
Our `SceneModel` subclass returns the chosen `SceneID` when a choice is pending, else
delegates to vanilla. That one override serves BOTH training battles and (later) picking your
ground when defending real battles — the real-defense feature is just "set the pending choice
when the player is the defender before the mission opens". The naval flag rides the same API,
so the future sea-training task aligns for free.

## 5. MCM

Copy the proven pattern from ImmersiveAI: `src\ImmersiveAI.Module\Mcm\McmBridge.cs` +
`ImmersiveAiMcmSettings.cs` (reflection-based soft dependency, referenced from
`McmBinFolder`, never shipped) + `ModConfig.cs` for the JSON file.

## 6. Risks / verify in first playtest (ranked)

1. **Companion heroes riding Team 2** (a player-clan hero inside a bandit-component party):
   roster-wise it's just a `CharacterObject` add, but watch for hero-state side effects.
   Fallback for V1: only troops + at most the player's pick of companions on Team 2, or
   Team 2 led by troops only.
2. **Player defeat path**: the quest reads `WinningSide` and finishes with no capture — our
   menu owns the flow the same way; still, verify losing a training battle never routes into
   the taken-prisoner logic. Fallback: a small Harmony guard during training only.
3. **XP flow**: confirm troop upgrade XP lands on roster elements from a real mission battle
   (roster diff will show it); scale the delta by `TrainingXpPercent`.
4. **Loot/prisoner residue**: the forced `PlayerEncounter.Finish()` skips the loot screen in
   the quest flow; verify nothing (gear, prisoners, food) leaks between the halves.
5. **Army case**: if the player leads a real Army (multiple lord parties), V1 splits only
   MainParty; army-wide training is a later task.

## 7. Prior art (2026.07.23 search)

No mod does this. Closest: "Troop Training Expanded" (arena duels to promote troops, has team
battles IN THE ARENA — not field), "SplitParty" (splits parties for travel, no battles),
Bannerlord Together (co-op friendly battles — multiplayer only). The niche — mock field battle
of your own split army on your actual terrain, casualties forgiven — is open.
