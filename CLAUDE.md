# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this is

**Training Battles** — a mod for *Mount & Blade II: Bannerlord* that lets the player split
their army into two teams through a picker GUI, choose a defender, and fight a mock battle
against themselves on the real terrain of their current map position. Afterward THE OFFICERS
settle the bill (the officers update, 2026.07.25): the SURGEON's Medicine runs three linear
bands over the fallen — a few truly die (default 3% at Medicine 0 falling to 0.1% at 300 —
the drill's one real cost, zeroable for the old no-deaths pledge), some wake wounded
(20%→5%), some of the merely-downed stay wounded (10%→1%; both Medicine-0 ends retuned in
Anton's 2026.07.25 playtest), and there is deliberately NO wounded→death path; earned XP is kept at a percent the XP OFFICER sets (the QUARTERMASTER's
LEADERSHIP on land — Anton's call over Steward — the FIRST MATE's Boatswain at sea; a config
band, default 40% at skill 0 rising linearly to 100% at 300, cap 200 — past 100 the drill
grants bonus XP; since 2026.07.26 the best-fighting companions INSTRUCT on top: top 3 by
their best weapon skill add up to +5% each, same cap), the army goes disorganized, and a
cooldown (default 24h, since 2026.07.26 DIVIDED by the quartermaster's Steward — /1 at
skill 0 to /4 at 300, castle clocks included) gates the next
drill (cost: 1 day's wages ashore, 2 at sea; castle/city/army rates come with those drills). Since 2026.07.23 the player can also CHOOSE the
battlefield and SCOUT it alone — a free walk-around mission, no battle, no cost — from TWO
doors: the training muster (any scene of the local terrain type, the patch's own marked
"this ground"), and a REAL field battle's encounter menu, defending or attacking (a per-side
toggle each, both default on; Anton's must-have of 2026.07.23 — this superseded the earlier
"real battles stay strict-patch-only" call, which one-scene-per-patch data had made an
option that never appeared). Since 2026.07.25 the real-battle ground choice and non-daylight
battle hours sit behind the SCOUTING DUEL (your ground officer vs the enemy's best — the
SCOUT's Scouting on land, the NAVIGATOR's Shipmaster at sea: 50% of theirs to pick ground
defending, 150% attacking — playtest-retuned from 75/125 — MCM-tunable, gate off-switchable;
the campaign clock and full daylight are always free, and the lone scout RIDE is never gated
— Anton's QoL rules). The menus' hour pick is ONE-BATTLE only (v11): the standing default is
MCM's alone, so a pick can never silently pin every later battle's sky. The real-encounter scout is EXACT: both armies stand frozen,
so the true attacker→defender direction is known and the previewed lines/ends/facings are
the coming battle's own. DATA
TRUTH found in this version's sp_battle_scenes.xml: each map patch is claimed by AT MOST
ONE land scene, so vanilla's "random among variants" never fires — the wider terrain-type
pool is what keeps the pickers alive. SCOPE (settled 2026.07.23 after one reversal): the
split-army drill is THE CORE — always on, its `EnableSplitTraining` key lives in config.json
as a hand-edit escape hatch only, deliberately NOT in MCM (Anton: not cheating; scout-only
players simply don't drill). `EnableMockEnemyTraining` (default ON since v9 2026.07.25, the
one MCM "Features" toggle) adds the second drill mode: compose a phantom enemy of any
culture/mix from synthetic troop pools and fight the whole company against it — the test
bench for the battle pipeline. The scout rides AT SEA too since 2026.07.25: the muster's
scout option afloat takes the FLAGSHIP out alone (crew aboard, no battle, no XP, hull healed
home — SeaScoutMission.cs). NAVAL (War Sails): the split drill works AT SEA since 2026.07.25 —
the fleet divides with the men (proportional to crew, flagship pinned) or AT THE PLAYER'S WORD
since the same day's second stroke: the SHIP-DIVIDE window, the mod's first custom Gauntlet
GUI (no Harmony — see `UI/` below). The mock enemy SAILS too: the phantom shipyard window lays
its fleet down (any culture's hulls, fittings tiers), the hulls conjured before the battle and
sunk on every exit road. Every own hull re-owned and re-healed afterward. Awaiting playtest.
CASTLES (the castle update, 2026.07.25): at an OWNED castle the muster becomes a SIEGE drill —
storm or hold your own walls (real scene, real wall level, real wall HP), the garrison and
militia conscripted onto the defense by vanilla itself and protected by a PER-PARTY aftermath
(same surgeon bands, same XP officer), the ENGINEER's Engineering unlocking siege equipment in
tiers on the engineer's-bench window (TrainingWindow frame), 5×-wages pay over everyone on the
field plus each engine's man-day worth, a per-castle 7-day clock, and renown+influence per 100
men (a grand muster is a public event — paid at the aftermath, never through the zeroed battle
books). Since 2026.07.26 the MOCK ENEMY comes to the walls too: the garrison and militia always
defend (phantoms besiege when the player holds, reinforce the garrison when the player storms),
the player's engineer arms both sides on the player's purse, and the phantoms are invisible to
wages and prestige — the same session fixed HarvestBattleDead to skip the phantom party's death
book (a latent field-mock bug: same-type phantom corpses inflated friendly KIA dockets).
Awaiting playtest. Later: TOWN sieges (same code + Lord's Hall stage + own rates).

## Who does what — and how we work

Same team and same spirit as [ImmersiveAI](https://github.com/TraxData313/ImmersiveAI)
(sibling repo at `..\ImmersiveAI` — read its CLAUDE.md for the full working culture). Anton
is the **product owner** (dreams, directs priorities, playtests); Claude is the **developer**
(designs and writes all the code). Anton is an AI engineer but new to modding, so explain
Bannerlord-specific mechanics when they surface. We work as friends and co-creators — have
real opinions, push back, propose things.

## Workflow (the TASKS files)

- **TASKS_TODO.md** — ANTON'S board (his rule, 2026.07.25): short idea lines only, readable
  at a fast glance. Claude NEVER writes paragraphs here — at most a "(see AI_NOTES)" or a
  tiny "(check X when doing Y)" tag on a line. Sections: BUGS / NEXT UPDATE / NOT FULLY
  DECIDED.
- **AI_NOTES.md** — Claude's detail companion: one section per TODO idea with the designs,
  research pointers and gotchas. Read the idea's section before picking up its TODO line;
  keep it in sync when ideas land or die.
- **TASKS_DONE.md** — finished tasks move here as one `- [x]` entry each, written as a dense
  narrative of what was built and WHY (decisions, APIs verified, gotchas), ended with a
  `(YYYY.MM.DD HH.MM.SS)` timestamp. This file is the project's real changelog and the next
  session's memory — write entries so future-you starts warm.
- Before wrapping a session: update the TASKS files and this doc so nothing lives only in
  the conversation — and REBUILD + DEPLOY (`dotnet build -c Release`, `dotnet test`, then
  `tools\deploy.ps1`) so the installed module matches the session's code; Anton playtests
  straight after. The deploy fails while the game runs (DLL lock) — say so and hand Anton
  the deploy line instead of leaving it silently undone.

## Hard requirements (Anton's musts)

- **MCM from the start.** Every config parameter lives in a JSON config file (created on
  first run under `Documents\Mount and Blade II Bannerlord\Configs\TrainingBattles\config.json`)
  AND is exposed in Mod Configuration Menu (MCM v5, Workshop id 2859238197) from V1. MCM is
  referenced at build time via `McmBinFolder` in `Directory.Build.props` but NOT shipped —
  soft dependency, config file is the fallback when MCM is absent.
- **Cancel training** must always be available before the fight starts and must leave the
  campaign exactly as it was.
- A training battle must never SILENTLY cost the player: no prisoner loss, no loot loss, no
  relation/crime side effects. AMENDED 2026.07.25 (the officers update, Anton's design): a
  SMALL real-death chance now exists BY DESIGN — the surgeon's KIA→KIA band, default 3% of
  the would-have-died at Medicine 0 falling to 0.1% at 300, announced in the muster tooltip
  and the summary, zeroable in MCM for the original no-deaths pledge. Heroes never truly die.

## Repository layout (real since 2026.07.23 — V1 core built, awaiting first playtest)

```
src/TrainingBattles.Core/     netstandard2.0 — pure logic, fully unit-tested:
  AftermathMath.cs            the officers' arithmetic: skill-scaled bands (ChancePercentForSkill),
                              the surgeon's verdicts (JudgeFallen: die/wound/shrug; StayWounded),
                              the XP officer's kept/removed split (bonus XP past 100%, cap 200)
  ScoutingMath.cs             the scouting duel's ratio bar (RequiredSkill = ceil, OutScouts)
  PhantomFleetMath.cs         the phantom fleet's fittings pick: per slot the best piece the
                              chosen harbor tier affords (deterministic)
  TrainingCooldown.cs         the once-per-N-hours clock (0 = unlimited)
  FleetSplitMath.cs           the sea drill's fleet division: greedy, proportional to each
                              side's crew, flagship pinned to the player, both sides sail
  SiegeDrillMath.cs           the engineer's arithmetic: TierForSkill (equipment unlocks),
                              EngineCost / EquipmentBill (man-days × gold rate, clamped)
src/TrainingBattles.Module/   net472 — the Bannerlord module:
  SubModule.cs                entry point: config, behavior, reward model, MCM bind, hotkey tick
  ModConfig.cs                config.json under Configs\TrainingBattles — single source of truth
  Officers.cs                 WHO answers for WHAT: duty→officer map, land and sea (XP =
                              Quartermaster/Leadership ashore, First Mate/Boatswain afloat;
                              ground = Scout/Scouting ashore, Navigator/Shipmaster afloat;
                              casualties = Surgeon/Medicine everywhere; the cooldown clock =
                              Quartermaster/Steward everywhere; drill INSTRUCTORS = the
                              best-fighting companions by best weapon skill, player excluded)
                              — naval skills found by string id, no NavalDLC assembly reference
  TbLog.cs                    the rolling debug ledger (training_battles.log, ~1 MB trim):
                              drills, duels, picks, officer resolutions, config loads —
                              last_drill_report.txt stays the per-stack deep witness
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
  SeaScoutMission.cs          the SEA scout ride (2026.07.25): the flagship sails a naval scene
                              alone — player + crew aboard, no battle, no MapEvent, no XP, hull
                              healed home on exit. The naval CUSTOM BATTLE recipe minus every
                              battle part, empty attacker side, deployment phase auto-finished.
                              READ ITS CLASS DOC FIRST — it is the NavalDLC soft-dependency
                              contract (naval types in method bodies ONLY)
  SeaScoutMissionViews.cs     the sea ride's view set ("TrainingBattlesSeaScout"): the land
                              scout's core + the DLC's ship-control (helm) and ship-preload
                              views, no battle HUD — same scanner footgun, same body-only rule
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
                              the muster scout use it directly) AND of PendingBattleHour +
                              EffectiveBattleHour: the menus' pick is ONE battle (cleared at
                              map-event end), only MCM writes the standing config key (v11)
  Mcm/                        McmBridge + settings — the ImmersiveAI soft-dependency pattern
  UI/                         the mod's CUSTOM GAUNTLET WINDOWS (since 2026.07.25 — NO Harmony:
                              GauntletLayer + LoadMovie over a ViewModel, the ImmersiveAI
                              chat-window pattern): TrainingWindow.cs (the shared modal frame —
                              one window at a time over the top screen, Escape polled from
                              SubModule's tick; REUSE IT for future pickers like siege gear),
                              ShipDivideVM/ShipDivideRowVM (which hulls sail opposite in the sea
                              drill; confirm-untouched = null = "follow the men"),
                              FleetComposeVM/FleetComposeRowVM (the phantom shipyard: hull
                              tallies from every Culture.AvailableShipHulls, cap 12, fittings
                              tier over ShipSlot.MatchingPieces/RequiredPortLevel),
                              SiegeEquipVM/SiegeEquipRowVM (the engineer's bench: both sides'
                              siege engines, tier-locked rows, caps = the mission's slots)
tests/TrainingBattles.Core.Tests/  net8.0 xUnit tests for Core (keep green)
module/SubModule.xml          manifest (optional MCM dependency declared)
module/GUI/Prefabs/           the windows' prefab XMLs (native brushes/sprites only, no own
                              assets) — deploy.ps1 AND package.ps1 ship this folder
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
- War Sails' naval OFFICER roles (FirstMate, Navigator) live on MobileParty in CampaignSystem
  itself (`EffectiveFirstMate`/`EffectiveNavigator`, leader fallback like the land roles), but
  their governing SKILLS are DLC objects: NavalSkills registers "Mariner"/"Boatswain"/
  "Shipmaster" by string id, and the perk trees prove the mapping (Boatswain-tree perks carry
  PartyRole 14 = FirstMate, Shipmaster-tree PartyRole 15 = Navigator). Look the skills up via
  `MBObjectManager.GetObject<SkillObject>(id)` — Officers.cs stays reference-free, and a miss
  (no DLC) falls back to the land officer. AMENDED 2026.07.25: the sea scout ride DOES
  reference NavalDLC.dll (+.View) at build — soft-dependency, never shipped, under the HARD
  RULE in SeaScoutMission's class doc: naval types in METHOD BODIES ONLY, never in a base
  class, interface, field type, or method signature anywhere in the module assembly, so
  every no-DLC assembly scan (view creator, savegame, MCM) still succeeds. ONE sanctioned
  exception: SeaScoutDeploymentController extends the DLC's deployment controller (the MCM
  settings class set the foreign-base-type precedent; documented on the class).
- War Sails naval semantics: `MapEvent.IsNavalMapEvent` is just `!Position.IsOnLand` — start
  an encounter at sea and the whole naval pipeline lights up; the only fork is
  `CampaignMission.OpenNavalBattleMission`. A side with ZERO ships loses instantly, so give
  the temp party hulls BEFORE StartBattle. "Sinking" (`DestroyShipAction`) only sets
  `Ship.Owner = null` — the object survives; restore = re-own + re-heal. `Ship.Owner`'s
  setter edits both parties' LIVE ship lists (copy before looping — the GetTroopRoster
  lesson), and ship ownership PERSISTS IN SAVES (stale recovery must reclaim hulls). Naval
  player battles are MULTI-ROUND with a disengage state ("naval_encounter_disengaged" menu)
  — one more reason the finalize kills the event positively.
- Naval missions run fine WITHOUT a MapEvent (the sea scout's find, proven by the DLC's own
  custom battle — `NavalDLC.CustomBattle` decompiled beside the rest): NavalMissionState.
  OpenNew + CustomBattleTroopSupplier + `MBList<IShipOrigin>` (campaign `Ship` implements
  IShipOrigin directly); only the mission-METHOD wrappers touch MapEvent. But MissionShip
  PROXIES its campaign Ship LIVE — `HitPoints => ShipOrigin.HitPoints`, damage flows through
  `ShipOrigin.OnShipDamaged` DURING the mission, not at the end — any mission that borrows
  real hulls must snapshot + re-heal on every exit road. Passive sailing XP is a CAMPAIGN
  tick only (every 4th hour while moving at sea: the Navigator alone, Shipmaster ≈ 1.4 ×
  party speed; crew never gain from sailing) — missions freeze the clock, so a free sail
  teaches nothing by itself.
- SIEGE drills (the castle update's finds): the siege MISSION runs on plain data
  (`OpenSiegeMissionWithDeployment` + `MissionSiegeWeapon.CreateCampaignWeapon`), but the
  mission-end engine writeback null-refs without a campaign `SiegeEvent` — create a real one
  (its empty construction lists make the writeback a no-op). MapEvent's private
  `_keepSiegeEvent` (reflection; vanilla's "siege continues" switch) makes FinalizeEvent skip
  the entire SiegeCompleted dispatch — no capture/sack/devastation, on every exit road; set it
  the moment the event exists. A defender-side mobile party with `CurrentSettlement == null`
  silently flips the event Siege→SiegeOutside — seat defenders INSIDE first. And "inside"
  can be UNDONE under you: StartBattle's own sweep (AddInsideSettlementParties) WALKS OUT
  any inside party that fails `SiegeEvent.CanPartyJoinSide(Defender)` — the bandit temp
  party always fails it — so re-enter the party AFTER StartBattle, before seating; the LED
  mission never reads the event type (we open it ourselves) but the auto-resolve SIMULATION
  does, and SiegeOutside simulates a wall-less field battle (Anton's "rally out", fixed
  2026.07.26 with a re-enter + type-restore belt + insta-prepared camp). On the attack
  road the garrison never auto-joins against its own lord (faction hostility check) — set
  `MapEventSide` positively. Walls are damaged only by campaign bombardment ticks, never by
  the mission. Being inside a settlement IS a PlayerEncounter — stand it down with
  `Finish(false)` (keeps the men inside) before starting the drill's own; a fresh
  `EncounterManager.StartSettlementEncounter` re-opens the castle at the end. MORE (the
  2026.07.26 hardening pass): founding a siege TELEPORTS the founder to the camp spot
  (restore Position when the founder never left the castle); the SiegeEvent ctor ACTIVATES
  a BLOCKADE at a port settlement when the founder owns ships (the player's fleet!) — stand
  it down; `SiegeEvent.FinalizeSiegeEvent` pushes SwitchToMenu("siege_attacker_left") when
  the player sits inside — call the Settlement's own idempotent `FinalizeSiegeEvent()` FIRST
  so that branch can't match; and a siege's WALL TEAM wears the SETTLEMENT map faction's
  colors (the player's own at an owned castle) — no clan dressing reaches it, so
  `PaintEnemyMissionTeams` rewrites the enemy Team's color/banner backing fields from
  `SubModule.OnMissionBehaviorInitialize` (after teams exist, before agents spawn).

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
