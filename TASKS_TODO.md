BUGS:

V1 — THE CORE:
- [ ] Project scaffold — solution + Core/Module split like ImmersiveAI
    `src/TrainingBattles.Core` (netstandard2.0, pure unit-tested logic: team math, XP/casualty
    rules, cooldown math) + `src/TrainingBattles.Module` (net472, the Bannerlord module) +
    `tests/TrainingBattles.Core.Tests` (net8.0, xUnit). `tools/deploy.ps1` installing as
    "Training Battles (dev)" beside a future Workshop copy, `tools/package.ps1` for the
    Workshop zip. SubModule.xml, save definer if we ever persist anything.
- [ ] Divide the army into two teams with a GUI
    A "Training battle" entry point (campaign map menu / party screen button — decide the most
    natural door). Opens a troop-picking GUI in the spirit of the lair-attack "send troops"
    screen: pick heroes AND troops for Team 1, the rest (or a second pick) become Team 2.
    Player chooses which team they personally command. Must handle companions/heroes as
    fightable leaders of the other side.
- [ ] Pick the defender + start the mock fight
    After the split, choose which team defends. Then create a battle event of the two split
    armies with the normal encounter options — fight (player commands their team on the real
    battlefield), send troops, try to escape, etc. — PLUS a "Cancel training" option that
    aborts the whole thing cleanly with no consequences, before the fighting starts.
- [ ] Aftermath: nobody really dies, everybody learns
    When the training battle ends:
    - Troops KEEP the experience they earned, reduced by a configurable percent
      (`TrainingXpPercent`, e.g. default 75%).
    - "Dead" troops don't die — they become WOUNDED instead. A configurable reduction applies
      (e.g. `TrainingDeathToWoundedFactor`, default /2 → half the would-be-dead are wounded,
      the rest walk away fine — exact rule to be decided), and the party surgeon's Medicine
      skill should matter here, so having a good doctor helps.
    - Heroes never die, only wounded.
    - The army becomes DISORGANIZED after the exercise (the vanilla disorganized state).
    - Everything restored: prisoners not taken, no loot, no relation/crime effects.
- [ ] Cooldown — once per 24 hours
    By default one training battle per 24 in-game hours (`TrainingCooldownHours`, default 24,
    0 = unlimited). The menu option shows when the men will be rested enough to drill again.
- [ ] Config file + MCM from the start — MUST HAVE
    Every parameter above in a JSON config file (created on first run under
    `Documents\Mount and Blade II Bannerlord\Configs\TrainingBattles\config.json`), AND all of
    it exposed in Mod Configuration Menu (MCM v5) from day one — Anton has MCM installed and
    considers in-game options a hard requirement, not a nice-to-have. MCM referenced but not
    shipped (soft dependency), config file remains the fallback when MCM is absent.

NEXT AFTER V1:
- [ ] See and pick the battle terrain (training)
    A big reason for the mod: practicing on the ACTUAL terrain you're standing on. If the
    game has several possible battle maps / spawn variants for the current map position, show
    them and let the player pick one manually for the training fight — see the ground before
    you bleed on it.
- [ ] Pick your ground when DEFENDING in real battles
    The same terrain-variant picker offered in real battles when the player is the defender —
    you chose where to stand and wait, so you should choose the ground. (Attacker keeps
    vanilla behavior.) Probably its own toggle, and worth checking mod-compat carefully.

POST V1 or NOT FULLY DECIDED:
- [ ] Garrison training at owned castles/towns
    Run training battles with the garrison of a castle/town the player owns — practice siege
    defense/assault on your own walls, maybe garrison vs. party mock sieges.
- [ ] Naval training battles (War Sails)
    Divide the ships and the troops at sea and run mock naval battles — same rules (XP kept,
    wounded not dead, disorganized, cooldown).
- [ ] Training against a custom-composed enemy
    Instead of splitting your own army, spawn a mock enemy of chosen composition (culture,
    tiers, counts) to drill against specific threats. (Idea parked — V1 is army-splitting.)
