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

## Siege drill equipment picker (future)

When garrison/siege training lands, the equipment choice (engines, ladders?) gets its own
VM + prefab over the same `UI\TrainingWindow` frame — that generality is why the window
manager is separate from the ship VMs.

## Sea scout ride

The muster scout hides at sea today (same rule as the real-encounter door): walking a naval
scene alone means standing on a deck — needs its own ship-spawn mission shape, not the Camp
walk-around.

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
