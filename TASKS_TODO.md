BUGS:
- [ ] VERIFY in playtest (both fixed 2026.07.24 evening, see TASKS_DONE): (a) time of day —
    NOT a sync bug: config.json had noon PINNED (BattleTimeOfDay: 12; pick "Campaign clock"
    in the picker once to go back to the true clock); the picker's "— current" mislabel that
    caused the confusion is now "— your pick" + the clock entry shows the live hour;
    (b) party roles (scout etc.) — real bug, the engine strips a hero's roles the moment they
    change party; drills now snapshot roles at launch and hand them back in the aftermath.
    Test: assign all four roles, put those companions in the OPPOSING half, drill, lose one
    drill on purpose (fugitive path), check the clan screen roles after each.
    (c) "Tracking: …separated after a battle…" village popups (stale vanilla tracker entries
    from pre-fix drills) — swept on load and after every drill since 2026.07.24 evening;
    verify villages go quiet after ONE game restart on the fixed build.

NEXT UPDATE:
- [ ] Naval training battles (War Sails)
    Divide the ships and the troops at sea and run mock naval battles — same rules (XP kept,
    wounded not dead, disorganized, cooldown). The scene API already carries the naval flag.
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