BUGS:

NEXT UPDATE:
- [ ] FIRST PLAYTEST of the V1 core (built 2026.07.23, all code in, 25 tests green, deployed
    as "Training Battles (dev)" — enable it in the launcher). Anton drives, checking the
    ranked risks from docs/training-battle-research.md §6:
    1. Companion heroes sent to the opposing half — do they fight, and come home clean
       (no relation hit, no capture, no odd hero state)?
    2. LOSE a training battle on purpose — the defeat must end at the summary message,
       never at "taken prisoner".
    3. XP — do troops actually show upgrade progress after a drill (and does the configured
       percent feel right)?
    4. Loot/residue — no loot screen, no gold change, no morale/renown/influence lines,
       no prisoners, food untouched.
    5. The fallen — counts in the summary line vs. the party screen after; does a high-
       Medicine surgeon visibly reduce the wounded?
    6. MCM menu shows all five settings and edits stick (and land in config.json).
    7. Cancel at every stage (picker cancel, menu cancel) leaves the party exactly as before.
    8. Save/load mid-flow + the stale-party recovery message on a save made mid-drill.

POST V1 or NOT FULLY DECIDED:
- [ ] See and pick the battle terrain (training) — RESEARCHED, surprisingly easy
    A big reason for the mod: practicing on the ACTUAL terrain you're standing on. The game
    keeps a list (`GameSceneDataManager.SingleplayerBattleScenes`) of battle scenes per map
    patch, and when several match it picks RANDOMLY — so the variants exist and are
    enumerable. A custom `SceneModel` (stock model override, no Harmony) returns the
    player's chosen scene when a choice is pending. See research doc §4.
- [ ] Pick your ground when DEFENDING in real battles
    The same terrain-variant picker offered in real battles when the player is the defender —
    you chose where to stand and wait, so you should choose the ground. (Attacker keeps
    vanilla behavior.) Probably its own toggle, and worth checking mod-compat carefully.
    → Same `SceneModel` override as the training picker serves this for free; only the
    "offer the choice at the right moment" hook differs.
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
