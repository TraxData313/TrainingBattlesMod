BUGS:


NEXT UPDATE:


POST V1 or NOT FULLY DECIDED:
- [ ] Training against a custom-composed enemy
    Instead of splitting your own army, spawn a mock enemy of chosen composition (culture,
    tiers, counts) to drill against specific threats. (Idea parked — V1 is army-splitting.)
    Maybe this option should be marked as developer option but it is good to test the defence/attack against other parties and if the user wants use it to see how they woldiers would fare agains an enemy army of diverse troops
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
- [ ] Training while leading an ARMY (V1 blocks it with an honest message) — split across
    the whole army's parties, or at least allow the main party to drill beside the army.
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
