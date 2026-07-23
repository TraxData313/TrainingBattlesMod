BUGS:

NEXT UPDATE:
- [ ] Playtest round 3 (build ready; installs itself the moment the game is closed — a
    watcher retries the deploy every 30s. If in doubt, run tools\deploy.ps1 by hand with
    the game closed, then restart the game):
    1. The aftermath is now IMMEDIATE and menu-independent — the instant the fight is
       decided (fought, hill-watched, or captured) the training wraps itself up; vanilla's
       "capture opponent / leave" screen should no longer stick around, and RETREATING from
       the mission now honestly ends the drill (no re-attack screen, no swinging at air).
    2. The summary line now prints "Drill XP kept: N (and M upgrade XP restored)" — THE
       verification for the battle-sisters upgrade bug. Check waiting upgrades before vs
       after on both the fought path AND the hill-watch path.
    3. Defeat: no member scatter, no gold loss, and the captured→released flash should be
       gone (the aftermath cuts in before the capture wrap).
    4. Round 2 items if not yet seen: hotkey now G; MCM tooltips fit; hill-watch works.
    Still open from round 1: companions on the opposing half; surgeon Medicine visibly
    reducing wounded.
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
- [ ] "Try to escape" option — PROBABLY NOT NEEDED, discuss
    In vanilla that option exists when someone TRAPS you and you try to break away (speed
    check, possible running battle). In a drill nobody chases you — "Cancel training" is
    the honest escape, free and always available. If the wish is to PRACTICE escaping (see
    the mechanics fire), that needs the opponent to chase the player on the map — which is
    the map-parties redesign above. Parked until that lands.

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
