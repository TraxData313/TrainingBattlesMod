BUGS:
- (none known — the 4 bugs of 2026.07.23 batch 3 are fixed, see TASKS_DONE: loot removed by
  baggage-train diff, "Regrouping" companions walked home, battlefield line + renamed
  "Select the battlefield", cost on the Begin buttons with the materials-and-pay wording)

NEXT UPDATE:
- (empty — the whole 2026.07.23 batch shipped: hero health restore, scout leave bar,
  50/50 auto-deal, honest cooldown clock, orange-cross opponent banner, drill pay-chest.
  All six are new config.json + MCM knobs; see TASKS_DONE for the details.)

TESTS for the player:
- [ ] NEW (2026.07.23 afternoon, batch 3): the four bug fixes:
    1. LOOT (fix v2 — the first sweep ran before the loot screen closed): win a drill,
       take EVERYTHING from the loot screen — within a heartbeat of being back on the
       map the items must vanish with "N looted items returned — there are no spoils in
       sparring". Check the baggage train matches its pre-drill state.
    1b. The big red "temp" on the muster menu is gone — a proper field-camp background
       (the Company of Trouble quest's own) shows instead.
    2. REGROUPING: send two companions to the opposing half, win — after the aftermath
       they must be IN the party again (message "X rejoined the company"), not
       "Regrouping" on the clan screen. AND: your two currently-stuck companions should
       walk home the moment you load the save (a one-shot rescue runs on first launch
       with the fixed build — message "X found their way back to the company").
    3. BATTLEFIELD LINE: the muster text now always shows "Battlefield: ..." (fate or
       your choice), the option is renamed "Select the battlefield", and picking a
       different ground updates the line immediately.
    4. COST ON THE BUTTONS: "Begin — your half attacks (N denars)" on all three start
       options, and the muster text explains the materials-and-pay cost.
- [ ] NEW (2026.07.23 afternoon, batch 2): the bug fixes + the update batch:
    1. HERO HEALTH: get the player or a companion hurt in a drill — after the aftermath
       they should stand at ≥90% health (never above what they entered with). MCM knob
       "Hero health restored after a drill".
    2. SCOUT LEAVE BAR: in a scouting ride, hold Tab — the vanilla "leaving area" bar
       should now draw while the leave timer runs (the scout opens under its own view set).
    3. AUTO-SPLIT: open "Divide the men" with no previous pick — the two halves should
       come pre-dealt roughly 50/50 (companions coin-flip too, the player stays put); the
       deal must be fully editable and Cancel must still discard it.
    4. COOLDOWN CLOCK: press G while the men are resting — the muster text should say
       "ready to muster again in H hours and M minutes" (same clock in the divide tooltip).
    5. OPPONENTS' COLORS: the opposing half should fly an ORANGE banner with a WHITE CROSS
       (uniform tint orange too, shields carrying the banner). WATCH: shape 510 as the
       cross bars comes from a community banner code — if the "cross" looks wrong, report
       what it looks like; the code is the OpponentBannerCode config (any banner-editor
       Ctrl+C code works). ALSO: after the drill (and after a canceled one) ride past some
       looters — their vanilla colors must be back.
    6. PAY-CHEST: the muster text should price the drill (default 1 day's wages for every
       man, config TrainingCostWages); gold leaves at Begin, Begin greys out when the purse
       is short, and a drill that fails to launch refunds in full.
- [ ] NEW (2026.07.23 afternoon): scouting + the widened survey + the deployment preview:
    1. "Ride out and scout a battlefield" in the muster menu: does the mission open, do you
       spawn ON YOUR DEPLOYMENT LINE (facing the enemy's, distance message shown), horse
       present, does Tab (or escape menu) bring you back to the muster menu with nothing lost? DONE
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
- [ ] I don't know how to test a real deffence, not in a war right now, is there a way to generate an incoming party that attacks me and I defend window?
    CLAUDE'S ANSWER (no dev tools needed): bandits attack WEAK parties, and the defender
    is whoever gets caught. Stash most of your troops in a town (or send them to a clan
    party) so your party looks weak, then ride slowly near looters/forest bandits — they
    will chase and jump you, and vanilla opens the DEFEND encounter window with our
    "Survey the ground" option on it (if ≥2 scenes fit the patch — with vanilla's
    one-scene-per-patch data the option may stay hidden; that's the known data truth,
    not a bug). Get your men back afterward. Alternative for a repeatable rig: the parked
    "custom-composed enemy" dev option in POST V1 would double as exactly this test tool.


POST V1 or NOT FULLY DECIDED:
- [ ] Training against a custom-composed enemy
    Instead of splitting your own army, spawn a mock enemy of chosen composition (culture,
    tiers, counts) to drill against specific threats. (Idea parked — V1 is army-splitting.)
    Maybe this option should be marked as developer option but it is good to test the defence/attack against other parties and if the user wants use it to see how they woldiers would fare agains an enemy army of diverse troops
- [ ] Scouting with companions (V1 scouts ALONE) — spawn picked companions alongside with
    follow-AI so the ride has an escort; needs agent-AI/order plumbing, own session.
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
