# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this is

**Training Battles** — a mod for *Mount & Blade II: Bannerlord* that lets the player split
their army into two teams through a picker GUI, choose a defender, and fight a mock battle
against themselves on the real terrain of their current map position. Afterward nobody truly
dies: the "dead" become wounded (the surgeon's Medicine skill softening it), earned XP is
kept at a configurable percent (default 75), the army goes disorganized, and a cooldown
(default 24h) gates the next drill. Since 2026.07.23 the player can also CHOOSE the
battlefield and SCOUT it alone — a free walk-around mission, no battle, no cost — from TWO
doors: the training muster (any scene of the local terrain type, the patch's own marked
"this ground"), and a REAL field battle's encounter menu, defending or attacking (a per-side
toggle each, both default on; Anton's must-have of 2026.07.23 — this superseded the earlier
"real battles stay strict-patch-only" call, which one-scene-per-patch data had made an
option that never appeared). The real-encounter scout is EXACT: both armies stand frozen,
so the true attacker→defender direction is known and the previewed lines/ends/facings are
the coming battle's own. DATA
TRUTH found in this version's sp_battle_scenes.xml: each map patch is claimed by AT MOST
ONE land scene, so vanilla's "random among variants" never fires — the wider terrain-type
pool is what keeps the pickers alive. SCOPE (settled 2026.07.23 after one reversal): the
split-army drill is THE CORE — always on, its `EnableSplitTraining` key lives in config.json
as a hand-edit escape hatch only, deliberately NOT in MCM (Anton: not cheating; scout-only
players simply don't drill). `EnableMockEnemyTraining` (default OFF, the one MCM "Features"
toggle) adds the second drill mode: compose a phantom enemy of any culture/mix from
synthetic troop pools and fight the whole company against it — the test bench for the
battle pipeline. NAVAL (War Sails): the split drill works AT SEA since 2026.07.25 (branch
`naval-training`, awaiting playtest) — the fleet auto-splits proportional to crew, flagship
stays with the player, every hull re-owned and re-healed afterward; the ship-divide GUI is
the NEXT release's must (Anton). Later: garrison training at owned fiefs.

## Who does what — and how we work

Same team and same spirit as [ImmersiveAI](https://github.com/TraxData313/ImmersiveAI)
(sibling repo at `..\ImmersiveAI` — read its CLAUDE.md for the full working culture). Anton
is the **product owner** (dreams, directs priorities, playtests); Claude is the **developer**
(designs and writes all the code). Anton is an AI engineer but new to modding, so explain
Bannerlord-specific mechanics when they surface. We work as friends and co-creators — have
real opinions, push back, propose things.

## Workflow (the TASKS files)

- **TASKS_TODO.md** — the plan. Anton drops raw ideas here (often stream-of-thought); Claude
  refines them in place when designing. Sections: BUGS / V1 — THE CORE / NEXT AFTER V1 /
  POST V1 or NOT FULLY DECIDED.
- **TASKS_DONE.md** — finished tasks move here as one `- [x]` entry each, written as a dense
  narrative of what was built and WHY (decisions, APIs verified, gotchas), ended with a
  `(YYYY.MM.DD HH.MM.SS)` timestamp. This file is the project's real changelog and the next
  session's memory — write entries so future-you starts warm.
- Before wrapping a session: update the TASKS files and this doc so nothing lives only in
  the conversation.

## Hard requirements (Anton's musts)

- **MCM from the start.** Every config parameter lives in a JSON config file (created on
  first run under `Documents\Mount and Blade II Bannerlord\Configs\TrainingBattles\config.json`)
  AND is exposed in Mod Configuration Menu (MCM v5, Workshop id 2859238197) from V1. MCM is
  referenced at build time via `McmBinFolder` in `Directory.Build.props` but NOT shipped —
  soft dependency, config file is the fallback when MCM is absent.
- **Cancel training** must always be available before the fight starts and must leave the
  campaign exactly as it was.
- A training battle must never permanently cost the player: no troop deaths (wounded
  instead), no prisoner loss, no loot loss, no relation/crime side effects.

## Repository layout (real since 2026.07.23 — V1 core built, awaiting first playtest)

```
src/TrainingBattles.Core/     netstandard2.0 — pure logic, fully unit-tested:
  AftermathMath.cs            per-fallen two-roll pipeline (surgeon save → wounded share),
                              XP kept/removed split
  TrainingCooldown.cs         the once-per-N-hours clock (0 = unlimited)
  FleetSplitMath.cs           the sea drill's fleet division: greedy, proportional to each
                              side's crew, flagship pinned to the player, both sides sail
src/TrainingBattles.Module/   net472 — the Bannerlord module:
  SubModule.cs                entry point: config, behavior, reward model, MCM bind, hotkey tick
  ModConfig.cs                config.json under Configs\TrainingBattles — single source of truth
  TrainingBattleBehavior.cs   THE HEART: muster menu, picker flow, battle recipe, aftermath,
                              cooldown, stale-party recovery — read its class doc first
  BattleSceneCatalog.cs       battlefield candidates for a position: strict patch chain +
                              the wider same-terrain pool + the shared ground-picker inquiry
  RealBattleGroundBehavior.cs "Select the battlefield" + "Ride out and scout a battlefield"
                              (exact lines — the true approach direction is known) on
                              vanilla's encounter menu for real field battles, defender and
                              attacker each behind their own toggle; labels are the muster's
                              own, shared via BattleSceneCatalog constants
  ScoutMission.cs             the scouting ride: enter any battlefield alone (no battle, no
                              encounter) — own behavior list; spawns the player ON the defender
                              deployment line (BattleSpawnPathSelector is deterministic WITH
                              map-patch record data, random without — so CreatePatchAwareRecord
                              is shared by scout AND training battles, making the scouted lines
                              the drilled lines); leaving is vanilla hold-Tab
  ScoutMissionViews.cs        the scout's view set ("TrainingBattlesScout"): vanilla's Camp
                              walk-around views + the hold-Tab leave bar Camp lacks — read its
                              class doc before touching (the ViewCreatorModule scanner footgun)
  Models/TrainingBattleRewardModel.cs  the "it was only training" guard (zero renown/loot/
                              prisoners — and at sea: no ship transfers, no post-defeat hull
                              damage, no figurehead loot — while TrainingActive). A DECORATOR
                              over BaseModel like the other two models, NOT a Default* subclass:
                              War Sails registers its own NavalDLCBattleRewardModel, and
                              extending DefaultBattleRewardModel silently replaced it in every
                              real battle (live bug in v1.0.1, fixed 2026.07.25)
  Models/TrainingBattlesSceneModel.cs  the ground-choice gate: one-shot PendingSceneId, else
                              delegates down the BaseModel chain (a DECORATOR — AddModel<T>
                              hands us the previously registered SceneModel, mod-compatible)
  Models/TrainingBattlesMapWeatherModel.cs  the battle-HOUR gate (config BattleTimeOfDay, -1 =
                              campaign clock): same decorator pattern over MapWeatherModel —
                              every battle mission's sky is filled via GetAtmosphereModel, so
                              overriding that one call (only while a player map event is live,
                              never while TrainingActive) pins the hour for field/siege/sea
                              battles using vanilla custom battle's own TOD_* presets; also
                              home of the shared AtmospherePresets table (drill records and
                              the muster scout use it directly). Edited from the muster menu,
                              the encounter menu and MCM — all write the one config key
  Mcm/                        McmBridge + settings — the ImmersiveAI soft-dependency pattern
tests/TrainingBattles.Core.Tests/  net8.0 xUnit tests for Core (keep green)
module/SubModule.xml          manifest (optional MCM dependency declared)
tools/deploy.ps1              build + install as "Training Battles (dev)" into the game's
                              Modules folder (module id TrainingBattles.Dev, safe beside
                              the Workshop copy — enable only ONE at a time)
tools/package.ps1             clean dist\TrainingBattles layout + versioned zip — what the
                              Workshop uploader ships (real module id, no .Dev)
tools/WORKSHOP-UPLOAD.md      the whole Steam release loop + the uploader's quirks;
                              item 3770681619, updates go through WorkshopUpdate.xml
                              (WorkshopCreate.xml already ran once — never again)
```

Flow in one breath: hotkey (default `G`) on the open map → muster menu (`training_battle_menu`)
→ "Divide the men" opens the party screen over a CLONE of the roster (nothing leaves the real
party until battle truly begins) → Begin attack/defend moves the picked men into a temp
bandit-component "Training Opponents" party and runs the Company-of-Trouble recipe → after the
mission the menu's re-init runs the aftermath (merge home, resurrect the fallen via
AftermathMath + the game's surgeon math, scale XP, disorganize, stamp cooldown) → summary
message, menu exits. The MOCK-ENEMY mode rides the same rails with phantoms: culture inquiry →
same party screen over a synthetic 500-per-troop pool → fresh troops into a `training_mock_enemy_*`
party that is NEVER merged home (stale recovery destroys, not merges — merging would mint free
troops), empty opponent snapshot keeps the aftermath to our side, and a delta sweep removes any
"captured" phantoms from the prisoner wagons.

Conventions carried over from ImmersiveAI: **Core = pure and unit-tested, Module = game
glue**; raw game-API research goes through ilspycmd/decompilation of the real game DLLs when
docs are missing; `dotnet test` after touching Core; close the game (or sit at the main menu)
before deploying or the DLL is locked.

**TaleWorlds footguns learned the hard way (blood was spilled for each line):**
- `TroopRoster.CloneRosterData()` silently DROPS each stack's Xp (copies counts/wounded
  only). Use the behavior's `CloneWithXp` whenever XP matters — a zero-XP snapshot cost
  Anton's parties 25% of their stored upgrades per drill for three playtest rounds.
- `PartyBase.OnXpChanged` clamps a stack's XP to (men × max upgrade cost) on every XP
  write — battle deaths silently destroy the fallen men's stored upgrade progress; restore
  men BEFORE writing XP.
- Vanilla's encounter state machine owns every non-happy battle path (auto-resolve wrap,
  retreat, defeat). Never wait for its menus: finalize from the tick, and kill the map
  event POSITIVELY (`SetOverrideWinner` + `PlayerEncounter.Finish` + bounded menu pops) or
  a lingering encounter menu restarts the fight as a pure vanilla battle with all
  protections disarmed. Vanilla's "leave" consequence even simulates one real combat round.
- `isLeave` on a GameMenuOption only styles it — exiting the menu is the consequence's job
  (`GameMenu.ExitToLast`).
- The party-screen closed callback fires MID state-transition — never touch menus inside
  it; set a flag and act on the next tick.
- `TroopRoster.GetTroopRoster()` returns the LIVE internal list, not a copy — copy it
  before a loop that mutates the same roster.
- `ViewCreatorManager` reflects over EVERY static method of a `[ViewCreatorModule]` class
  and reads its `[ViewMethod]` attribute with `[0]` and no length check — one attribute-less
  static member (even a property getter) crashes the scan. One method per class, nothing else.
- Mission team looks come from THREE places (all read live at spawn): the team flag from
  `PartyBase.CustomBanner`, but uniform tint from the MAP FACTION's color pair and leaderless
  troops' shield heraldry from the faction's banner — recoloring the opponents means dressing
  the lender bandit CLAN too, and clan colors PERSIST IN SAVES (restore data must ride SyncData).
- The battle commit we drive via `PlayerEncounter.Update()` settles XP AND LOOT in one guarded
  pass — our reward model empties BOTH loot-chance lists (`GetLootItemChancesForWinnerParties`,
  `GetLootCasualtyChances`), which skips vanilla's distribution loops entirely, so
  `RosterToReceiveLootItems` stays empty and the post-battle loot screens never open. Harmony
  loot mods hook that same commit anyway, so the aftermath still diffs the pre-fight ItemRoster
  snapshot and removes anything gained.
- Vanilla makes the losing side's UNCAPTURED heroes FUGITIVE at map-event end ("Regrouping" on
  the clan screen) and REMOVES them from the roster — a no-capture guard reroutes companions
  into exactly that hole; the aftermath must walk snapshot-heroes home explicitly.
- NEVER extend a Default*Model to override a game model — War Sails (module `NavalDLC`)
  registers ~40 of its own models (its own BattleRewardModel among them), and AddModel
  replaces by BASE type, so a Default* subclass silently strips the DLC's behavior from the
  whole campaign. Always decorate: extend the abstract model, delegate everything to
  BaseModel (AddModel hands over the previously registered model). All three of our models
  do this now.
- War Sails naval semantics: `MapEvent.IsNavalMapEvent` is just `!Position.IsOnLand` — start
  an encounter at sea and the whole naval pipeline lights up; the only fork is
  `CampaignMission.OpenNavalBattleMission`. A side with ZERO ships loses instantly, so give
  the temp party hulls BEFORE StartBattle. "Sinking" (`DestroyShipAction`) only sets
  `Ship.Owner = null` — the object survives; restore = re-own + re-heal. `Ship.Owner`'s
  setter edits both parties' LIVE ship lists (copy before looping — the GetTroopRoster
  lesson), and ship ownership PERSISTS IN SAVES (stale recovery must reclaim hulls). Naval
  player battles are MULTI-ROUND with a disengage state ("naval_encounter_disengaged" menu)
  — one more reason the finalize kills the event positively.

## Build & deploy

```powershell
dotnet build -c Release
dotnet test  -c Release
powershell -ExecutionPolicy Bypass -File tools\deploy.ps1
```

Game path and MCM path live in `Directory.Build.props`; personal overrides go in
`Directory.Build.props.user` (git-ignored via `*.user`).

## Design (researched 2026.07.23 — READ docs/training-battle-research.md FIRST)

The feasibility research is DONE and verified against this game version's decompiled DLLs.
The full write-up with code snippets is **docs/training-battle-research.md**; TASKS_DONE has
the narrative. The shape of V1:

- **Picker GUI**: `PartyScreenHelper.OpenScreenWithDummyRosterWithMainParty` (namespace
  `Helpers`, TaleWorlds.CampaignSystem) — the lair/alley/quest "pick your men" screen; heroes
  transferable, Cancel delegate built in.
- **The mock battle**: copy the Company of Trouble quest recipe
  (`LandLordCompanyOfTroubleIssueBehavior` in the decompiled corpus): temp party from the
  player's own troops via `BanditPartyComponent.CreateBanditParty` + `SetPartyUsedByQuest`,
  our own GameMenu owning the flow, `PlayerEncounter.Start/SetupFields/StartBattle`,
  `CampaignMission.OpenBattleMission(SceneModel.GetBattleSceneForMapPatch(...))`, then read
  `PlayerEncounter.Battle.WinningSide`, `Finish()`, `DestroyPartyAction`.
- **Aftermath**: roster snapshot/diff (`CloneRosterData`, `AddToCounts` with wounded+xp
  args); dead→wounded via `PartyHealingModel.GetSurvivalChance` × config factor +
  `SkillLevelingManager.OnSurgeryApplied`; XP delta × `TrainingXpPercent`;
  `MobileParty.SetDisorganized(true)`; a `BattleRewardModel` override zeroing
  renown/influence while training. Cooldown timestamp via `SyncData` primitive.
- **Terrain picking**: custom `SceneModel` override; candidates enumerable from
  `GameSceneDataManager.Instance.SingleplayerBattleScenes` filtered by the current map
  patch's `sceneIndex`. Same override later powers real-defense ground choice and naval.
- **Top playtest risks** (research doc §6): companions riding the temp Team-2 party;
  defeat path must never reach capture; XP-on-roster confirmation; loot residue.

**Decompiled game source** (this exact game version): `..\reference\game-decompiled\`
(CampaignSystem, SandBox, MountAndBlade, and since 2026.07.25 NavalDLC — War Sails' own
module, home of every NavalDLC*Model). Regenerate: `ilspycmd -p -o <out> <dll>` with
`$env:DOTNET_ROLL_FORWARD='LatestMajor'`; SandBox.dll is under
`Modules\SandBox\bin\Win64_Shipping_Client`, NavalDLC.dll under
`Modules\NavalDLC\bin\Win64_Shipping_Client`. Consult freely — it is the ground truth for
this game version's APIs.
