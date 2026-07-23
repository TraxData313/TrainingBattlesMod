# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this is

**Training Battles** — a mod for *Mount & Blade II: Bannerlord* that lets the player split
their army into two teams through a picker GUI, choose a defender, and fight a mock battle
against themselves on the real terrain of their current map position. Afterward nobody truly
dies: the "dead" become wounded (the surgeon's Medicine skill softening it), earned XP is
kept at a configurable percent, the army goes disorganized, and a cooldown (default 24h)
gates the next drill. Later: previewing/picking between the possible battle maps for the
current spot (also when defending real battles), garrison training at owned fiefs, and naval
training battles.

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
src/TrainingBattles.Module/   net472 — the Bannerlord module:
  SubModule.cs                entry point: config, behavior, reward model, MCM bind, hotkey tick
  ModConfig.cs                config.json under Configs\TrainingBattles — single source of truth
  TrainingBattleBehavior.cs   THE HEART: muster menu, picker flow, battle recipe, aftermath,
                              cooldown, stale-party recovery — read its class doc first
  Models/TrainingBattleRewardModel.cs  the "it was only training" guard (zero renown/loot/
                              prisoners while TrainingActive)
  Mcm/                        McmBridge + settings — the ImmersiveAI soft-dependency pattern
tests/TrainingBattles.Core.Tests/  net8.0 xUnit tests for Core (keep green)
module/SubModule.xml          manifest (optional MCM dependency declared)
tools/deploy.ps1              build + install as "Training Battles (dev)" into the game's
                              Modules folder (module id TrainingBattles.Dev, safe beside a
                              future Workshop copy)
```

Flow in one breath: hotkey (default `T`) on the open map → muster menu (`training_battle_menu`)
→ "Divide the men" opens the party screen over a CLONE of the roster (nothing leaves the real
party until battle truly begins) → Begin attack/defend moves the picked men into a temp
bandit-component "Training Opponents" party and runs the Company-of-Trouble recipe → after the
mission the menu's re-init runs the aftermath (merge home, resurrect the fallen via
AftermathMath + the game's surgeon math, scale XP, disorganize, stamp cooldown) → summary
message, menu exits.

Conventions carried over from ImmersiveAI: **Core = pure and unit-tested, Module = game
glue**; raw game-API research goes through ilspycmd/decompilation of the real game DLLs when
docs are missing; `dotnet test` after touching Core; close the game (or sit at the main menu)
before deploying or the DLL is locked.

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
(CampaignSystem, SandBox, MountAndBlade). Regenerate: `ilspycmd -p -o <out> <dll>` with
`$env:DOTNET_ROLL_FORWARD='LatestMajor'`; SandBox.dll is under
`Modules\SandBox\bin\Win64_Shipping_Client`. Consult freely — it is the ground truth for
this game version's APIs.
