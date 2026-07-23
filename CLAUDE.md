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

## Repository layout (planned — mirror ImmersiveAI)

```
src/TrainingBattles.Core/     netstandard2.0 — pure logic, fully unit-tested:
                              team split rules, XP-keep math, dead→wounded math
                              (Medicine-scaled), cooldown math
src/TrainingBattles.Module/   net472 — the Bannerlord module: SubModule, campaign behavior,
                              map menu entry, picker GUI (Gauntlet), the mock battle event,
                              aftermath application, MCM settings
tests/TrainingBattles.Core.Tests/  net8.0 xUnit tests for Core
module/                       SubModule.xml + any GUI prefabs, copied on deploy
tools/deploy.ps1              build + install as "Training Battles (dev)" into the game's
                              Modules folder (own module id, safe beside a Workshop copy)
tools/package.ps1             clean dist layout + Workshop zip
```

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

## Design notes so far (pre-code)

- **Picker GUI**: model it on the lair-attack "send troops" troop selection screen — find the
  vanilla screen/VM it uses and either reuse or clone the pattern. Must support assigning
  heroes (including the player) to sides.
- **The mock battle**: likely a custom MapEvent/encounter between the player's party and a
  temporary "mirror" party holding Team 2's roster, so the vanilla encounter menu (fight /
  send troops / try to escape) comes for free; "Cancel training" tears the temporary party
  and event down. Simulation details (XP flow, casualty interception) are the heart of the
  research — casualty interception probably via the battle's `MapEventSide`/`TroopRoster`
  results or a Harmony touch on the casualty application, decided when the real APIs are on
  the table.
- **Aftermath**: apply XP with `TrainingXpPercent`; convert kills to wounded with
  `TrainingDeathToWoundedFactor` scaled by the surgeon's Medicine; set the disorganized
  state the same way vanilla does after battles; start the cooldown clock
  (`TrainingCooldownHours`, 0 = off).
- **Terrain picking (later)**: research how the game maps a world position to battle scenes
  (scene variants per terrain type) — the training door first, the real-defense door after.
