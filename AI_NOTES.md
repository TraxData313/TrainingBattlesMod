# AI notes — Claude's detail companion to TASKS_TODO.md

TASKS_TODO.md is Anton's board: short idea lines, readable at a glance. The details, designs,
research pointers and gotchas behind those ideas live HERE, one section per idea. When picking
up a TODO line, read its section first.

## Ship divide GUI + phantom fleets — BUILT 2026.07.25, awaiting playtest

Both landed in one session, on the mod's FIRST custom Gauntlet windows (`UI\TrainingWindow` —
the ImmersiveAI chat-window pattern: GauntletLayer + LoadMovie over a ViewModel, prefab XML in
`module\GUI\Prefabs`, native brushes only, NO Harmony needed). The window frame is deliberately
generic — the future siege-equipment picker reuses `TrainingWindow` + a new VM/prefab pair.

Playtest points (sea drill, split): the muster's "Divide the ships" option (afloat, ≥2 hulls,
men divided first); flagship pinned; confirm-with-untouched-default stores NULL ("follow the
men") so re-dividing the men re-divides the fleet; a stale pick (hull sold between pick and
Begin) falls back to auto with a message. Escape = Cancel (polled in SubModule tick).

Playtest points (mock at sea): "Lay down the phantom fleet" (afloat, mock enemy composed
first) — hull rows from every culture's `AvailableShipHulls`, +/− tallies, cap 12 hulls
(mission perf), fittings tier cycler (bare / harbor I–III; per slot the BEST
`ShipSlot.MatchingPieces` piece whose `RequiredPortLevel` the tier affords —
`PhantomFleetMath.UpgradePickIndex`, deterministic). Begin at sea requires a laid-down fleet
(shipless side loses the naval event instantly). Phantom hulls are `new Ship(hull)` marked
IsTradeable=false + IsUsedByQuest, owned by the mock party before StartBattle; every exit road
(finish, abort, stale recovery) SINKS them (Owner=null — vanilla's own sinking leftover shape),
NEVER reclaims (reclaiming would mint a free fleet — the old stale-recovery ReclaimShips call
for mock parties was exactly that trap, now fixed). The aftermath also sweeps any hull in the
player's fleet that was not in the pre-drill snapshot ("captured" phantoms dissolve). The
player's own fleet is snapshotted and healed even in mock sea drills (phantoms can hurt it).

Fleet recipe DECIDED by building the GUI: the player composes it hull by hull from every
culture's shipyard list — no auto-mirror; revisit only if the playtest wants a "mirror my
fleet" convenience button.

## Castle siege drill — BUILT 2026.07.25, awaiting playtest

Anton's castle update, decided in-session: garrison EMBRACED (vanilla conscripts every inside
party onto the defense anyway — keeping them out is harder than protecting them), engine cost
= vanilla `ManDayCost` × gold-per-man-day (default 20, MCM), engineer skill unlocks engine
TIERS (0: ram always; 1: ballista+onager @50; 2: tower+fire variants @100; 3: trebuchet @150),
CASTLES first (towns later, same code + own pay/cooldown + the Lord's Hall stage). Renown +
influence added mid-session on Anton's ask: per-100-men rates (default 1.0/1.0, MCM 0..10),
paid ONCE at the aftermath via GainRenownAction/ChangeClanInfluenceAction — never through the
battle books, which stay zeroed while training. Castle-only for now; extending prestige to
field/sea drills is UNDECIDED.

The wires (all verified in the decompiled corpus):
- Door: "castle" menu option (index 4), owner-clan castles only, per-castle 7-day clock
  (`_castleCooldownData` "id=hours;..." SyncData string). Muster menu reused in "siege mode"
  (`_siegeSettlement != null`): scout/ground/send-troops hidden, engineer bench option shown.
- Encounter shape: stand down the settlement-visit encounter with `PlayerEncounter.Finish(false)`
  (being inside IS an encounter), then a REAL `SiegeEvent` (StartSiegeEvent) — the siege
  mission's engine-writeback (`CampaignMissionComponent.OnEndMission` →
  `GetLeaderParty(Attacker).SiegeEvent.SetSiegeEngineStates...`) NULL-REFS without one, and
  with the event's construction lists empty the writeback no-ops safely (it walks
  DeployedSiegeEngines, not the mission list). Defend road: temp party besieges, player +
  garrison inside. Attack road: LeaveSettlementAction(player) + EnterSettlementAction(temp,
  castle) — a defender-side mobile party with CurrentSettlement==null silently FLIPS the
  event to SiegeOutside (MapEvent.AddInvolvedPartyInternal), so the defenders must sit inside
  BEFORE joining. `PlayerEncounter.StartBattle` makes the event Siege on its own (defender
  IsFortification → StartSiegeMapEvent).
- THE capture lock: MapEvent's private `_keepSiegeEvent` set by reflection the moment the
  event exists (vanilla's own "siege continues" switch; public only via
  FinishBattleAndKeepSiegeEvent which also ends the battle). With it, FinalizeEvent skips the
  whole SiegeCompleted/AfterSiegeCompleted dispatch — no capture, no sack, no devastation, on
  every exit road. Second lock: aftermath restores walls
  (SetWallSectionHitPointsRatioAtIndex; mission never writes wall damage — only campaign
  bombardment ticks do) and dismantles the siege via `SiegeEvent.FinalizeSiegeEvent()`
  (resets CurrentSiegeState, clears Settlement.SiegeEvent, unhooks camps).
- Garrison/militia/guest lords: snapshotted (CloneWithXp) + hero HP before battle; on the
  ATTACK road they never auto-join (same faction as the attacker — vanilla's hostility check
  refuses), so `SeatFriendliesOnTheWalls` sets `MapEventSide = DefenderSide` positively.
  Aftermath walks each party back IN PLACE (`RestoreFriendlyParties`): same surgeon bands and
  XP-kept rate (the MAIN party's officers — it is their drill), runs BEFORE the main pass and
  CONSUMES its share of the `_battleDead` harvest so shared troop types are never judged twice.
- Engineer bench: `UI\SiegeEquipVM` + `TrainingBattlesSiegeEquip.xml` over TrainingWindow
  (FleetCompose's frame); both sides on one list; caps mirror the mission slots (1 ram, 2
  towers, 4 ranged/side); mission engines minted directly via
  `MissionSiegeWeapon.CreateCampaignWeapon` (attacker cat: Ram/Ballista/Onager/Tower/Fire*/
  Trebuchet; defender: Ballista/Catapult/Fire* — vanilla's own defender list; ladders are
  free, scene-owned, not picked). Scene: `LocationComplex("center").GetSceneName(wallLevel)`,
  real wall HP array, `OpenSiegeMissionWithDeployment`.
- Stale recovery: `RecoverStaleDrillSieges` on session launch — any siege whose besieger is a
  training party OR the player besieging their OWN settlement is a drill leftover, finalized
  before the party recovery runs.

CRASH ROUND 1 (2026.07.25 19:35, Tirby Castle, defend road — hard process crash, dump
declined): the first build invented its own encounter shape — Finish(false) the settlement
encounter (which pops the muster menu MID-CONSEQUENCE while MapStateData.GameMenuId still
points at it), then hand-build a new encounter. A menu re-init during the launch runs
MenuInit → `_checkResults` was already armed → FinishTrainingBattle ran over the HALF-BUILT
siege, destroyed the opponent party, then control returned into the launch which kept using
it — state soup, native death. THREE fixes, all shipped: (1) `_launching` reentrancy guard —
MenuInit and every tick finalize-trigger stand down while the launch runs; (2) both roads
reshaped onto vanilla's exercised paths — DEFEND: settlement encounter STAYS,
`StartBattleAction.ApplyStartAssaultAgainstWalls(temp, castle)` raises the event exactly like
an AI assault, `PlayerEncounter.JoinBattle(Defender)` joins it (the join_siege_event road),
no menu is ever popped; ATTACK: `PlayerEncounter.Finish()` (the true leave-settlement road),
temp party enters the walls, `StartPlayerSiege(Attacker)` arms the player-siege machinery,
then Start/SetupFields/StartBattle as before; (3) per-step TbLog "siege" lines (snapshots →
siege event up → assault event up → joined → seated → assault opens) so any next crash names
its exact step in training_battles.log. The aftermath's Finish is now Finish(false) — the
defender must not be thrown out of their own gate (field/sea unchanged: never inside).

PLAYTEST ROUND 2 (2026.07.25 evening, two clean attack-road drills at Tirby Castle — the
recipe works, engines billed, one true KIA from the surgeon's band): two follow-ups shipped
same evening. (1) END INSIDE THE CASTLE (Anton's ask): the aftermath now walks the attacker
back through their own gate — `EncounterManager.StartSettlementEncounter(MainParty, castle)`,
vanilla's own arrive door (guarded: no map event, no encounter, castle not under real siege);
the defender path keeps its fresh-encounter re-open. (2) ORANGE LOOTERS STILL SEEN "from time
to time, after the siege": three belts added — the lender clan is now NAMED in the log at
dress AND restore time ([clan] lines — the next report becomes diagnosable), the restore arms
a SECOND visual sweep ~half a second after the map quiets (a replenisher party born in the
aftermath's own frame window kept the training colors it spawned with), and session launch
runs a blanket visual-dirty over every bandit-clan party (any historically stale icon heals
on load). If orange persists past this, compare the sighting time with the [clan] ledger.

CRASH ROUND 2 (2026.07.25 21:32, defend road — the step logging paid off: full stack in the
ledger): `BesiegerCamp.AddSiegePartyInternal` NREs for a LEADERLESS besieger —
`GetLeaderOfSiegeEvent(...)` returns the single party's `LeaderHero` (null for our bandit
temp party) and dereferences it unguarded. A leaderless party cannot found a siege, ever.
Worse: `SiegeEvent`'s constructor stamps `Settlement.SiegeEvent = this` BEFORE that line, so
the failed launch left a half-built GHOST siege on the castle (our `_drillSiegeEvent` never
got assigned — nothing dismantled it) which crashed the campaign a few moments later; and
AbortLiveBattle's `Finish()` (default forcePlayerOut=true) threw Anton out of his own gate.
FIXES: (1) `CreateDrillSiegeAroundOpponent` — the defend road's siege is FOUNDED by the
hero-led MAIN party (the shape the attack road proved), then the camp's membership is
SWAPPED to the temp party by direct field writes (`MobileParty._besiegerCamp`,
`BesiegerCamp._besiegerParties/_leaderParty/_faction`) — skipping the leader arithmetic that
cannot handle hero-less parties and the remove-path that would finalize an emptied camp;
`Settlement.LastAttackerParty` (stamped by the founding) is restored; a failed swap tears
the whole siege down before rethrowing. (2) `DismantleGhostSiege` — any SiegeEvent still on
the drill's settlement is torn down on EVERY exit road (abort + aftermath), and the on-load
sweep now also finalizes any LEADERLESS siege on a player-owned settlement (no vanilla siege
stands leaderless). (3) Abort uses `Finish(false)` (nobody gets ejected by a failed launch)
and re-opens the castle's settlement encounter so the menu works again.

HARDENING PASS (2026.07.26, pre-round-3 — Anton reported "defense totally crashed": that was
crash round 2 above; its camp-swap fix landed 21:40, EIGHT MINUTES after his 21:32 crash, so
the repo's defend road is newer than anything he has played. Audited the whole swapped shape
against the decompiled corpus and closed what remained):
- Verified sound against vanilla: `AddInsideSettlementParties` auto-joins garrison/militia to
  the defense and EXCLUDES the main party (so no `InterruptEncounter` menu fires mid-launch);
  `Town.GetDefenderParties` excludes caravans/villagers (our snapshot exclusion matches
  vanilla exactly); `CheckNearbyPartiesToJoinPlayerMapEvent` early-outs for IsSiegeAssault
  (no nearby lord can wander into a siege drill); `JoinBattle(Defender)` reads
  `EncounteredBattle` = the visit encounter's settlement MapEvent — our exact shape.
- FIXED — founding teleport: `StartSiegeEvent(castle, main)` on the defend road moves the
  main party to the besieger-camp spot outside the walls (`OnPartyJoinedSiegeInternal` sets
  Position) while the defender never left the castle; position snapshotted and restored.
- FIXED — port-castle blockade: the ctor ACTIVATES a blockade when the settlement HasPort and
  the founding besieger owns ships (the player's fleet at anchor!) — the swapped-in temp
  party is shipless and the drill wants no blockade at all. `DeactivateDrillBlockade`
  (reflection to the internal `DeactivateBlockade`) stands it down on BOTH roads.
- FIXED — teardown menu push: `SiegeEvent.FinalizeSiegeEvent` calls
  `GameMenu.SwitchToMenu("siege_attacker_left")` when the player sits inside the besieged
  settlement (defend road: always). At a menu-less moment that throw would abandon the rest
  of the teardown → ghost siege in the save. Both dismantlers now call the SETTLEMENT's own
  idempotent `FinalizeSiegeEvent()` FIRST (nulls Settlement.SiegeEvent → vanilla's menu
  branch can't match) before finalizing the event.
- LOGGING — every siege step now writes to training_battles.log: founding, blockade state,
  swap result (camp leader/faction, main freed), defenders auto-joined count, which join
  branch ran (visit-encounter JoinBattle vs fallback), seated/already counts, dismantle and
  wall-restore outcomes, aftermath/abort entry state (encounter? mapEvent? menu?), castle
  re-entry. A round-3 failure names its exact step.
- ORANGE WALLS (Anton's ask): the siege wall team wore the CASTLE OWNER's faction colors —
  the player's own — because mission team color/banner come from the side LEADER combatant,
  and a settlement's `PrimaryColorPair` is its map faction's (verified:
  MissionCombatantsLogic.AddEnemyTeam + PartyBase.PrimaryColorPair + Mission.SpawnTroop
  ClothingColor1/2 = team colors at spawn). Clan dressing can never reach it. New
  `PaintEnemyMissionTeams` (static, TrainingBattleBehavior) writes the training banner's
  colors + banner onto every enemy team's backing fields (`<Color>k__BackingField` etc. —
  Team has no setters) from `SubModule.OnMissionBehaviorInitialize` — which runs AFTER
  MissionCombatantsLogic built the teams and BEFORE any agent spawns (verified in
  Mission.AfterStart). No-op unless TrainingActive; covers field/sea/siege alike (field/sea
  were already orange via the dressed clan — now doubly guaranteed).

PLAYTEST RISKS (in rough order of worry):
1. ATTACK road novelties: same-faction garrison forced onto the enemy side (agent hostility
   flags?), LeaveSettlement/EnterSettlement mid-menu, `AddInsideSettlementParties` may push a
   GUESTING lord's party out of the castle (they walk out unharmed — cosmetic but surprising).
2. Post-battle menu dance: FinalizeSiegeEvent can push "siege_attacker_left" for an inside
   player; the bounded pops + fresh settlement encounter should land back on the castle menu.
3. Defeat roads at a siege (retreat mid-assault, walls lost while defending) — the winner
   stomp + captivity guard are the same as the field drill's, but sieges add
   `SetNextSiegeState`/pull-back states; FinalizeSiegeEvent's ResetSiegeState covers it.
4. Mock phantoms defending walls: culture-mixed defenders on siege engines (vanilla AI mans
   defender engines from the defender roster — should just work).
5. The hour door: the drill uses EffectiveBattleHour through the same MapWeatherModel
   decorator; verify a siege drill at night actually opens dark.

## Sea scout ride — SAILED 2026.07.26 ("worked as a charm" — Anton, round 5)

The flagship free sail: at sea the muster's scout option now rides the FLAGSHIP instead of
hiding — player + as many healthy men as the deck takes, alone on the chosen naval scene,
hold Tab home. Zero cost, zero clock, zero XP BY DESIGN (Anton: "just a free relaxing
ride"). Files: `SeaScoutMission.cs` (mission + SeaScoutRideLogic), `SeaScoutMissionViews.cs`
(view set), the ScoutCondition/LaunchScout fork in TrainingBattleBehavior, NavalDLC(+.View)
references in the csproj/props (soft-dependency — see below).

HOW (the research that shaped it, verified in decompile): War Sails' NAVAL CUSTOM BATTLE
(`..\reference\game-decompiled\NavalDLC.CustomBattle\`, decompiled this session, plus
`NavalDLC.View\` and `NavalDLC.GauntletUI\`) proves the whole ship stack runs WITHOUT a
MapEvent: `NavalMissionState.OpenNew` + `CustomBattleTroopSupplier` + plain
`MBList<IShipOrigin>` (campaign `Ship : IShipOrigin` passes directly). The core ship chain
(NavalShipsLogic/NavalAgentsLogic/DefaultNavalMissionLogic/spawn logic) has ZERO MapEvent
references — the coupling lives only in the mission-method wrappers. The ride is that
custom-battle recipe MINUS every battle part (no NavalBattleEndLogic, ShipRetreatLogic,
ShipCollisionOutcomeLogic, AgentVictoryLogic, morale/order/scoreboard/deployment views),
with an EMPTY attacker side — both teams exist (much of the stack indexes them) but nothing
spawns opposite; every empty-side road audited: InitializeShipAssignments returns early,
DeployBattleSide finds no framed plans, Team.ResetTactic self-guards on HasTeamAi — and
`MissionTeamAITypeEnum.NoTeamAI` so no naval tactic ever queries the empty sea. The
deployment PHASE is skipped: SeaScoutRideLogic hooks DeploymentMissionController's
OnAfterSetupTeams and calls FinishDeployment (IsDeploymentFinished guards the double-call —
FinishDeployment removes the controller mid-tick but the event still fires after). Crew are
SYNTHETIC combatants (CustomBattleCombatant), so mission states never touch the campaign
roster. Crew cap = the DLC's own `ShipDeploymentModel.GetMaximumDeployableTroopCountForTeam`.

SOFT DEPENDENCY (the McmBridge discipline, now applied twice): NavalDLC.dll + NavalDLC.View
.dll referenced at build, never shipped. HARD RULE for future edits: naval types ONLY in
method bodies — never in a base class, interface, FIELD TYPE, or METHOD SIGNATURE of any
class in the module assembly (assembly scans — view creator, savegame, MCM — must succeed
without the DLC; SeaScoutRideLogic's skeleton is deliberately all-vanilla:
DeploymentMissionController and campaign Ship are base-game types). The bodies can only run
at sea, impossible without War Sails.

HULL SAFETY (verified live): MissionShip PROXIES the campaign Ship — `HitPoints =>
ShipOrigin.HitPoints`, damage flows through `ShipOrigin.OnShipDamaged` DURING the mission.
So the ride snapshots the flagship's HP/sail-HP at Open and heals it home in
SeaScoutRideLogic.OnRemoveBehavior (idempotent `_restored` flag; OnEndMission too) — the
drill's RestoreFleet pledge in miniature.

CRASH ROUND 1 (2026.07.25 21:39, first ride, hard native crash in the loading screen —
DIAGNOSED from the crash folder's rgl_log + a healthy sea drill log side by side): the view
set carried SandBox.View's `MissionPreloadView` — the CAMPAIGN preloader, whose first
pre-mission tick walks `MapEvent.PlayerMapEvent.InvolvedParties`, and this mission has NO
map event → death exactly between "water_prefabs.xml" and the first "Preload physics" line.
FIX: `MissionCustomBattlePreloadView` (vanilla's own choice for its MapEvent-less naval
missions — reads MissionCombatantsLogic.GetAllCombatants, our CustomBattleCombatants
exactly). LESSON for the corpus: when borrowing views for a MapEvent-less mission, take the
CUSTOM BATTLE variant of anything that has one — the campaign twin may assume the encounter.
SandBox.View and TaleWorlds.MountAndBlade.View are now decompiled beside the rest.

CRASH ROUND 2 (21:50, same session): preload fix HELD — engine log shows teams added,
equipment preloaded, the flagship SPAWNED (CreateMissionShip: drakkar_ship_nested), nav
mesh loaded — then native death where a healthy drill starts crew/formation work. Response:
`SeaScoutDeploymentController` (subclass of the naval deployment controller — the ONE
sanctioned break of the bodies-only rule, MCM-settings-class precedent, documented on the
class) wearing a FLIGHT RECORDER: TbLog at every phase boundary, plus a self-heal that
hand-spawns the player onto the flagship if vanilla's allocation left them missing (base
SetupTeams derefs Mission.InitialPlayerAgent UNGUARDED).

CRASH ROUND 3 (22:01): the recorder spoke — enemy (empty) side sets up CLEAN (watch-list
item 1 RETIRED), death is INSIDE base OnSetupTeamsOfSide(Defender), i.e. the player side's
own setup, downstream of the ship spawn. Response: the recorder now REPLICATES the base's
four steps for the player side (the two internal ones via reflection, miss falls back to
the plain base call) with a log between each, catches any managed exception with its FULL
INNER STACK into training_battles.log, and SeaScoutRideLogic.OnAgentBuild logs EVERY agent
build by name.

CRASH ROUND 4 (2026.07.26 08:23) — THE RECORDER DELIVERED THE WHOLE STORY, root cause
FOUND AND FIXED: step 2/4 (crew spawn), first agent build, `InvalidCastException: Unable
to cast CustomBattleCombatant to PartyBase` at
`SandboxAgentStatCalculateModel.InitializeMissionEquipment` — the CAMPAIGN's agent-stat
model HARD-CASTS every agent's `Origin.BattleCombatant` to PartyBase (verified in the
SandBox decompile: `(PartyBase)obj`, null flows through, foreign types die). Vanilla never
mixes CustomBattleTroopSupplier with a campaign, so the mine was theirs but the field was
ours. THE FIX (deployed 2026.07.26, awaiting ROUND 5): our own `SeaScoutAgentOrigin` +
`SeaScoutTroopSupplier` (all-vanilla interfaces, soft-dependency clean) — BattleCombatant
presents the REAL `PartyBase.MainParty` (campaign-native for every model, and truthful:
the crew ARE the main party's men, so faces/colors/perk context read right), while every
casualty callback (SetKilled/SetWounded/SetRouted/OnScoreHit) is a NO-OP — the
consequence-free pledge enforced at the origin itself. (Vanilla's SimpleAgentOrigin was
almost the answer but its SetKilled TRULY kills heroes — noted for the corpus.) The
CustomBattleCombatants remain for TEAM identity (side/banner/colors) and the preloader's
character walk only; player still found by IsPlayerCharacter, still helmed via
Game.PlayerTroop (campaign sets it to main_hero — verified, Campaign.cs:1318). LESSON for
the corpus (generalizes round 1): in a campaign, EVERY per-agent game model may assume
campaign types — a MapEvent-less campaign mission must feed campaign-shaped origins, not
custom-battle ones; the supplier was fine, the origin was the poison. Same session,
Anton's naming: the muster scout option is now a text variable — "Scout out the sea with
your flagship" afloat, the classic ride-out line ashore. AFTER ROUND 5 SAILS: strip the
per-crewman OnAgentBuild log spam + the step-by-step reflection replication (keep the
phase-boundary lines), and swap the tooltip/menu texts if Anton wants more sea flavor.

PLAYTEST WATCH-LIST:
1. ~~The empty attacker side actually booting~~ RETIRED — round 3 proved it sets up clean.
2. The helm: ship control view + input on a campaign mission under our own mission name.
3. Spawn spot: naval scenes' deployment frames for the defender formation (ship shouldn't
   spawn beached or outside the boundary).
4. Leave roads: hold-Tab bar at sea, escape-menu leave, boundary crossing at 30f.
5. The muster menu returning cleanly after the ride (campaign frozen throughout, same as
   the land scout).
6. Pinned-hour presets at sea (AtmospherePresets have no NauticalInfo of their own; we set
   UsesNavalSimulatedWater=1 after — check water looks right at a pinned night hour).

XP TRUTH (Anton's "are the men gaining sea levels from the ride?"): NO ONE gains ANYTHING,
in vanilla, from the ride — and sailing teaches far less than assumed even on the map:
- Campaign map only, every 4th game hour while MOVING at sea
  (`MobilePartyTrainingBehavior.CheckMovementSkills` → `NavalSkillLevellingManager
  .OnTravelOnWater`): the NAVIGATOR alone gains Shipmaster XP = round(1.4 × party speed) —
  roughly 40–70 XP per full day's sail. That's the whole passive-sailing economy.
- The SCOUT gains zero at sea (CheckScouting explicitly skips `IsCurrentlyAtSea`); the
  land rule "every main-party hero ticks Riding/Athletics" does NOT run at sea; regular
  crew gains zero from sailing, ever (only battles + the usual daily-XP perks).
- Other naval XP faucets: First Mate gains Boatswain from storm damage PREVENTED (0.1×)
  and ship repairs (0.05× HP); Mariner comes only from hero hits in naval battles
  (1× hit XP) + the VeteransWisdom perk (daily random naval skill dribble to companions).
- Missions freeze campaign time → the ride grants nothing and costs nothing. TRULY free.
DECIDED (Anton, 2026.07.25): the ride stays ZERO-XP — "just a free relaxing ride". If that
ever changes, grant on leaving the ride (hero: `AddSkillXp`, scalable by an officer band à
la ChancePercentForSkill; crew: `AddXpToTroop`) — but mind the farm: the ride is free and
repeatable, so crew XP would mint free upgrades.

## Naval regression check (v1.1.0)

One REAL naval battle WITHOUT training — confirm figurehead drops and captured-ship
distribution came back (the v1.0.x reward-model bug's regression check; Anton's 2026.07.25
playtest covered the drills, not a plain DLC sea fight).

## "Choose the time of day" on the siege/naval doors — BUILT 2026.07.25, awaiting playtest

Menu research settled it: NAVAL pre-battle encounters use the plain "encounter" menu (already
carried our tools), so the only missing doors were the SIEGE wait menu and the naval
disengage state. Added in `RealBattleGroundBehavior`: the hour option on
`menu_siege_strategies` (BOTH sides sit on it — besieger strategies and the besieged's
sally options; ungated by the scouting duel — when to assault/sally is the commander's own
call, a deliberate design choice for Anton to veto) and on `naval_encounter_disengaged`
(multi-round sea fights breathe there; duel-gated like the pre-battle door). Same
one-battle-only PendingBattleHour everywhere.

## Scouting duel — decisions log

DECIDED (Anton, 2026.07.25): the lone scout RIDE is never gated. Ratios retuned same day
after playtest: defend 50%, attack 150% (were 75/125).

The old "standing hour beats a lost duel" softness is RESOLVED since v1.2.0: the menus' hour
pick is one-battle-only (PendingBattleHour, cleared at map-event end), so the only standing
hour is the MCM default — which remains ungated by design (it's the player's global QoL
setting, not battlefield intel). An MCM default of "night" does apply to a lost-duel battle;
if a playtest ever minds, gate EffectiveBattleHour for real battles behind the duel too.

## Town siege training (the castle drill's follow-up)

Castles landed 2026.07.25 (section above). Towns ride the same code with: their own pay
multiplier and cooldown (Anton's old sketch said ~50× / 14 days — re-ask, castle went 5×/7d),
bigger scenes, and the Lord's Hall pull-back stage (`SiegeLordsHallFightModel`,
`Settlement.SiegeState.InTheLordsHall`) that castles never enter. Gate: `Settlement.IsTown`
beside the existing IsCastle door.

## Scouting with companions

V1 scouts ALONE. Spawn picked companions alongside with follow-AI so the ride has an escort —
needs agent-AI/order plumbing, own session.

## Training while leading an ARMY

V1 blocks it with an honest message. RESEARCH NOTES READY (2026.07.23). The two vanilla wires
that make armies hard: (1) `PartyBase.MapEventSide`'s setter CASCADES the event side to every
AttachedParties member the moment the leader's battle starts — there is no "just my party
fights" switch; (2) `MobileParty.SetAttachedToInternal` makes any party that attaches
MID-event inherit the event side, so stragglers rejoining would wander into a running drill.
Lifting the block naively = other lords' parties fight, take REAL casualties/clamp-destroyed
XP/fugitive heroes, because the aftermath only restores the main party. Three tiers, do B
first:

- TIER B (recommended, 1–2 sessions): ARMY vs MOCK ENEMY — embrace the cascade (the whole
  army joins against the phantoms; nobody crosses sides, no picker bookkeeping). Needs the
  aftermath generalized per-party: CloneWithXp snapshot + fallen-restore + absolute XP
  restore for EVERY friendly party in the event, per-party prisoner walk-back and loot
  sweep, scattered heroes walked to their OWN parties. Reward-model guards are already
  global while TrainingActive. This is Anton's "how would my army fare against X".
- TIER A (moderate, fiddly): main party drills BESIDE the army — detach AttachedParties
  before StartBattle, re-attach after, guard re-attachment per tick; block the send-troops
  sim path (it runs map time); member-of-army case is ugly, maybe leader-only.
- TIER C (hard, parked): split the ARMY against itself — Tier B's per-party aftermath PLUS
  multi-party roster division and a real enemy command structure; overlaps the
  companion-commander research above.
