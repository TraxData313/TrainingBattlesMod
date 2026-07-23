BUGS:


NEXT UPDATE:
- [ ] Playtest the real-battle ground tools with the LUDA SAVE (Anton has a save just before
    Luda's party closes in to attack). What to check, in order of risk:
    1. THE BIG ONE — "Scout the battlefield" from the encounter menu, then LEAVE the ride
       (hold Tab): the encounter menu must come back with Attack!/Try to get away intact and
       the map event unharmed (the mission-under-a-menu shape vanilla's pre-battle
       conversation uses; ours is the first non-conversation tenant). If the menu comes back
       wrong or the fight auto-starts, the scout option gets pulled from real encounters.
    2. "Survey the ground" as DEFENDER: pick a non-local scene, Attack!, confirm the battle
       opens on the picked scene; also confirm walking away (Try to get away) clears the pick.
    3. Scout precision: scout, note the lines, then fight — the real deployment should match
       the scouted lines exactly (same ends, same facing).
    4. The same two options when the PLAYER attacks someone (toggle "when attacking").
    5. ARMY battles (Anton's ask, VERIFIED IN CODE 2026.07.23 — every army road leads to the
       same "encounter" menu our options live on: "Attack army" on the army_encounter menu
       is just SwitchToMenu("encounter"), and join_encounter's help-a-side does
       PlayerEncounter.JoinBattle then SwitchToMenu("encounter"); the side gate, the scene
       query and the direction formula — AttackerSide.LeaderParty, i.e. the army leader —
       are all army-agnostic, so no code was needed). Confirm in play: survey+scout appear
       when the player's ARMY fights a field battle, as leader and as a mere member, and
       the chosen scene sticks. Note: as an army MEMBER the tools still show (the player
       fights the mission either way) — if that feels like overreach, an "army leader only"
       gate is a one-line decision.
- [ ] Finish playtesting the mock-enemy drill. CONFIRMED by Anton (2026.07.23): a mock battle
    works end-to-end — loot and the phantom troops disappeared as expected; and with the dev
    toggle off the V1 default menu is scout-only, as designed. STILL UNTESTED: a deliberate
    DEFEAT against the mock enemy (the capture/scatter guards on the losing path), mixing two
    cultures (run the composer twice), and a skim of last_drill_report.txt after a mock drill
    to confirm the XP/wounded arithmetic only touched our side.


POST V1 or NOT FULLY DECIDED:
- [ ] I want the other party to become as a real party, lead by one of the companions if there are any there, for the AI to assign the campanions as division leaders, if it normally does
- [ ] When I divide the army to see as I am starting a fight against a party, an animation where I fight with a party, my party being the men I got and the other party the men they got in their orange banner
- [ ] Pick the commander of the opposing half (Anton's ask) — RESEARCH NOTES READY
    Choose one of the companions sent across to LEAD the other party (and with none picked,
    it fights leaderless, like a party without its lord). Findings so far: `MobileParty.
    ChangePartyLeader(hero)` delegates to the party component; our bandit component likely
    ignores it. `CustomPartyComponent` carries a real settable leader BUT changing its leader
    also flips party OWNERSHIP to the leader — a player-clan hero owning the opponent party
    risks flipping its MapFaction and breaking the hostile encounter (the whole battle rides
    on the bandit clan's hostility). Plan: test `CustomPartyComponent` with clan=bandit-clan
    + leader=companion in a sandbox save; check `MapFaction` stays bandit and the mission
    gives the enemy side a real general. Deserves its own careful session — not rushed in.
- [ ] Show the two halves as real parties on the map before the fight (Anton's ask)
    After dividing, see the opposing half standing on the map (with its picked commander)
    and start the fight by riding at it — the vanilla party-encounter feel. This is the
    "vanilla encounter menu" redesign: spawn the party visibly, let the vanilla encounter
    flow own attack/send-troops/leave, and move our aftermath fully onto MapEventEnded
    (the listener already exists since round 2). Pairs naturally with the commander task.
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
- [ ] Garrison training at owned castles/towns
    Run training battles with the garrison of a castle/town the player owns — practice siege
    defense/assault on your own walls, maybe garrison vs. party mock sieges.
- [ ] Naval training battles (War Sails)
    Divide the ships and the troops at sea and run mock naval battles — same rules (XP kept,
    wounded not dead, disorganized, cooldown). The scene API already carries the naval flag.
- [ ] A menu door besides the hotkey (e.g. a party-screen button or clan-screen entry), for
    players who never read hotkey hints.
- [ ] Scouting with companions (V1 scouts ALONE) — spawn picked companions alongside with
    follow-AI so the ride has an escort; needs agent-AI/order plumbing, own session.
