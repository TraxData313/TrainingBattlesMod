BUGS:

NEXT UPDATE:
- [ ] Pay for the Training Battle it should be X mod configurable daily salaries for the soldiers involved. Also could you research how much it would cost if I had had to have the perk where I can buy equipment and give it to them for XP in salaries?
- [ ] Scouting with companions (V1 scouts ALONE) — spawn picked companions alongside with
    follow-AI so the ride has an escort; needs agent-AI/order plumbing, own session.
- [ ] DECIDE (Anton): should the real-defense ground choice also use the WIDER same-terrain
    pool? Today it stays strict (only scenes truly claiming the map patch — with vanilla
    data that is at most ONE, so the defend option effectively never shows). Widening gives
    the choice back but lets you defend on ground you are not literally standing on.

TESTS for the player:
- [ ] NEW (2026.07.23 afternoon): scouting + the widened survey + the deployment preview:
    1. "Ride out and scout a battlefield" in the muster menu: does the mission open, do you
       spawn ON YOUR DEPLOYMENT LINE (facing the enemy's, distance message shown), horse
       present, does Tab (or escape menu) bring you back to the muster menu with nothing lost?
    2. THE DEPLOY PROMISE: scout a map, note where your line stands — then drill on the same
       map from the same spot: the deployment screen's lines should match the scouted spot.
       (Both now run the game's deterministic patch-based deployment; if they DON'T match,
       that promise in the menu text must be softened — report it.)
    3. "Survey the ground" now lists ALL battlefields of the local terrain type (the patch's
       own scene marked "this ground") — pick a far one, Begin: does the drill open there?
    4. The muster menu text: two pillars readable, real percentages shown, "{newline}"
       rendering as actual line breaks (if you see the literal word "newline", report it).
    5. The one-map mystery is solved (data truth: each patch has at most one scene) — but
       verify battle_terrain_a isn't ALSO showing at, say, steppe or desert spots: the list
       should change with the country you ride through.
- [ ] Remaining playtest items (the core loop is VERIFIED WORKING as of 2026.07.23 —
    XP: "super" per Anton, wounded knob: 42 casualties → 3 wounded at 10% with the whole
    arithmetic confirmed in last_drill_report.txt, incl. +7.9k/+7.4k XP restored on two
    fully wiped stacks; retreat/cancel end the drill cleanly):
    1. Companions sent to the opposing half — do they fight, and come home clean?
    2. LOSE on purpose with the current build — defeat must end at the summary, no capture
       flash, no member scatter.
    3. The earlier 22-KIA→7-wounded outlier: if a weird wounded count ever shows again,
       read Configs\TrainingBattles\last_drill_report.txt — it names the source.


POST V1 or NOT FULLY DECIDED:
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
- [ ] Training against a custom-composed enemy
    Instead of splitting your own army, spawn a mock enemy of chosen composition (culture,
    tiers, counts) to drill against specific threats. (Idea parked — V1 is army-splitting.)
- [ ] A menu door besides the hotkey (e.g. a party-screen button or clan-screen entry), for
    players who never read hotkey hints.
