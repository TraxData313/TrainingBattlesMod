# AI notes — Claude's detail companion to TASKS_TODO.md

TASKS_TODO.md is Anton's board: short idea lines, readable at a glance. The details, designs,
research pointers and gotchas behind those ideas live HERE, one section per idea. When picking
up a TODO line, read its section first.

## Ship divide GUI (sea drill)

A real picker for WHICH ships go to which side in the naval split drill (the V1 naval solution
auto-splits proportional to crew, flagship pinned to the player — `FleetSplitMath`). No vanilla
"divide the fleet" screen exists — research candidates: the fleet management screen's roster
machinery, or a custom inquiry/Gauntlet list. Pairs with the men picker: divide the men, then
divide the hulls.

## Phantom fleets for the mock enemy at sea

Blocked today with an honest tooltip ("phantoms do not sail yet"). Conjure `new Ship(hull)` per
culture for the mock party and dissolve them after — the Ship ctor is public, hulls come from
the object manager. Decide the fleet recipe (culture's shipyard list? player-mirrored?) with
Anton first.

## Sea scout ride

The muster scout hides at sea today (same rule as the real-encounter door): walking a naval
scene alone means standing on a deck — needs its own ship-spawn mission shape, not the Camp
walk-around.

## Naval regression check (v1.1.0)

One REAL naval battle WITHOUT training — confirm figurehead drops and captured-ship
distribution came back (the v1.0.x reward-model bug's regression check; Anton's 2026.07.25
playtest covered the drills, not a plain DLC sea fight).

## "Choose the time of day" on the siege/naval doors

Anton wants the option before EVERY battle type. The CONFIG already applies everywhere (the
MapWeatherModel decorator fires for any player map event — sieges and sea included); what's
missing is only the menu entry on vanilla's siege menus (menu ids to research: the
besiege/assault menus) and wherever naval encounters diverge from "encounter". The muster, the
encounter menu and MCM all edit the same key already.

## Scouting duel — decisions log

DECIDED (Anton, 2026.07.25): the lone scout RIDE is never gated. Ratios retuned same day
after playtest: defend 50%, attack 150% (were 75/125).

The old "standing hour beats a lost duel" softness is RESOLVED since v1.2.0: the menus' hour
pick is one-battle-only (PendingBattleHour, cleared at map-event end), so the only standing
hour is the MCM default — which remains ungated by design (it's the player's global QoL
setting, not battlefield intel). An MCM default of "night" does apply to a lost-duel battle;
if a playtest ever minds, gate EffectiveBattleHour for real battles behind the duel too.

## Garrison training at castles/towns

Anton's numbers: castles pay ~10× the wage cost, once per 7 days; cities 50×, once per 14
days. The ENGINEER joins the officers table here (siege drills — their band/group slot is
ready: `Officers.cs` map + the per-officer MCM groups were built to take the new row).
Practice siege defense/assault on your own walls; maybe garrison vs. party mock sieges.

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
