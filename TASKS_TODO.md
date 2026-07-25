BUGS:

NEXT UPDATE:
- [ ] Ship divide GUI — MUST for the next release (Anton, 2026.07.25)
    A real picker for WHICH ships go to which side in the naval split drill (the V1 naval
    solution auto-splits proportional to crew). No vanilla "divide the fleet" screen exists —
    research candidates: the fleet management screen's roster machinery, or a custom
    inquiry/Gauntlet list. Pairs with the men picker: divide the men, then divide the hulls.
- [ ] Phantom FLEETS for the mock enemy at sea (blocked with an honest tooltip today):
    conjure `new Ship(hull)` per culture for the mock party and dissolve them after —
    the Ship ctor is public, hulls come from the object manager; decide the fleet recipe
    (culture's shipyard list? player-mirrored?) with Anton first.
- [ ] Sea scout ride (the muster scout hides at sea today, same rule as the real-encounter
    door): walking a naval scene alone means standing on a deck — needs its own ship-spawn
    mission shape, not the Camp walk-around.
- [ ] One REAL naval battle WITHOUT training on v1.1.0 — confirm figurehead drops and
    captured-ship distribution came back (the v1.0.x reward-model bug's regression check;
    Anton's 2026.07.25 playtest covered the drills, not a plain DLC sea fight).
- [ ] "Choose the time of day" as a MENU OPTION on the siege/naval doors too (Anton wants
    the option before EVERY battle type). The CONFIG already applies everywhere (the
    MapWeatherModel decorator fires for any player map event — sieges and sea included);
    what's missing is only the menu entry on vanilla's siege menus (menu ids to research:
    the besiege/assault menus) and wherever naval encounters diverge from "encounter".
    The muster, the encounter menu and MCM all edit the same key already.


NEXT UPDATEs or NOT FULLY DECIDED:
- [ ] Garrison training at owned castles/towns
    Run training battles with the garrison of a castle/town the player owns — practice siege
    defense/assault on your own walls, maybe garrison vs. party mock sieges.
    - castles cost 10x by default to pay, and can be done once in 7 days
    - cities 50x, once per 14 days
- [ ] A menu door besides the hotkey (e.g. a party-screen button or clan-screen entry), for
    players who never read hotkey hints.
- [ ] Scouting with companions (V1 scouts ALONE) — spawn picked companions alongside with
    follow-AI so the ride has an escort; needs agent-AI/order plumbing, own session.
- [ ] Training while leading an ARMY (V1 blocks it with an honest message) — RESEARCH NOTES
    READY (2026.07.23). The two vanilla wires that make armies hard: (1) PartyBase.
    MapEventSide's setter CASCADES the event side to every AttachedParties member the moment
    the leader's battle starts — there is no "just my party fights" switch; (2) MobileParty.
    SetAttachedToInternal makes any party that attaches MID-event inherit the event side, so
    stragglers rejoining would wander into a running drill. Lifting the block naively = other
    lords' parties fight, take REAL casualties/clamp-destroyed XP/fugitive heroes, because the
    aftermath only restores the main party. Three tiers, do B first:
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
      multi-party roster division and a real enemy command structure; overlaps the companion-
      commander research below.