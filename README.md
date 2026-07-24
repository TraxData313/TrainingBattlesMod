# Training Battles

**Drill your army against itself. Scout the ground like a real general. Fight where —
and when — you choose to fight.**

A mod for *Mount & Blade II: Bannerlord* built on two pillars:

## Pillar 1 — Training battles

Split your army into two halves through a troop-picking GUI, choose which half you command
(attacking or defending), and fight a full mock battle on the very terrain you're standing
on — or watch it resolve from the hill via the send-troops simulation.

**Nobody really dies.** Every casualty of a drill — the "killed" and the hurt alike —
walks the same path afterward:

1. **He gets back up.** Training deaths are not real; every man returns to the roster.
2. **The surgeon sees him first.** The game's own Medicine-driven save — a better doctor
   sends more men off without a scratch.
3. **Only the rest roll for a wound.** Of the men the surgeon could *not* patch, a
   configurable share (default 10%) wake up truly wounded for a while; the others shrug
   it off.

So a drill with 20 casualties typically ends with one or two wounded — fewer with a good
doctor — and **zero dead**. The men keep a configurable share of the XP they earned
(default 75%), the party goes disorganized for a while, and a cooldown (default 24 in-game
hours) gates the next drill. **No spoils in sparring**: the post-battle loot and prisoner
screens simply never appear after a drill — nothing is looted, nobody is captured, no
renown or relations are touched. War as a school, not a funeral. **Cancel training** is
always available before the fight and leaves the campaign exactly as it was.

**The drill pay** (configurable, default one day's wages per soldier on the field) goes for
equipment — javelins, arrows, upkeep after the battle — and rewards to keep the good
fighters motivated. Set it to 0 to drill for free.

**Mock enemy** *(optional, off by default)* — compose a phantom enemy force from **every
troop in the game**, all cultures in one picker screen, any mix, up to 1000 men — and drill
the whole company against it. The phantoms never touch your roster and vanish afterward;
your own men follow the normal training rules. The test bench for "how would we fare
against X?".

## Pillar 2 — Scouting & choosing your ground

What a real general does before the battle: know the field — and pick the hour.

- **Choose the time of day** — pin the hour **every** battle opens at (morning, noon,
  afternoon, evening, night — or the honest campaign clock, the default). It applies to
  drills, real field battles, siege assaults and sea battles alike, and it's the first
  option on every menu, because a night battle you cannot see is no battle at all. An
  admitted immersion trade — that's why it's a choice.
- **Select the battlefield** — see every battlefield the game could put your fight on: the
  map patch's own scene (marked *"this ground"*) plus every scene of the local terrain type
  — and pick the one the fight is fought on.
- **Ride out and scout** — enter any of those battlefields **alone**: no battle, no enemy,
  no cost, no cooldown. You spawn **on your deployment line**, facing the enemy's, with the
  distance called out — so you can judge both the terrain *and* the deploy before you ever
  have to fight there. If the map is good but the lines are bad, take another map or move.
- **The scouted lines are the drilled lines** — training battles and the scout preview use
  the same deterministic deployment recipe (the game's own map-patch machinery), so what
  you scouted is what you get. One honest caveat: that promise is for **drills**. In a
  *real* battle the ground holds, but the enemy's **true approach direction** decides the
  facing and which end is theirs — so a field scouted in advance from the muster may fight
  "turned around" when the real enemy arrives from elsewhere.
- **The same tools in REAL battles** *(toggleable per side, both on by default)* — when a
  real field battle is about to start, the encounter menu offers the same options in the
  same order — choose the hour, scout, select the battlefield — defending **or** attacking.
  **This is where you scout when you want 100% certainty**: with an army actually facing
  you, both sides stand frozen on the map, so the attacker's true approach direction is
  already known — the lines, ends, and facings you scout are precisely the ones the battle
  will use. No time passes; the fight waits for your return. Rule of thumb: scout from the
  muster to *learn* the battlefields, scout from the attack screen to *plan the actual
  fight*.

## The knobs

Every parameter lives in a JSON config
(`Documents\...\Configs\TrainingBattles\config.json`) **and** in the in-game **Mod
Configuration Menu (MCM)** — XP kept %, wounded %, hero health restored %, cooldown hours,
drill pay (days of wages), disorganized toggle, auto-split-in-half toggle, the opponent
half's banner (any banner-editor code), time of day for battles, the real-battle
survey/scout toggles (defending and attacking separately), the mock-enemy toggle, and the
hotkey (default `G` on the campaign map). MCM is a soft dependency — without it the config
file alone runs the show.

## Status

**Playtested and working**: the whole drill loop (divide → fight or hill-watch → nobody
dies, wounded filtered through the surgeon, XP banked, disorganized, cooldown), the
mock-enemy drill, the no-loot guarantee (holds even alongside loot mods), the time-of-day
pick for drills and real battles, and the real-battle ground tools. See
[TASKS_TODO.md](TASKS_TODO.md) for what's next: a search box in the mock-enemy picker,
the time-of-day option on the siege menus, scouting with companions, garrison training,
naval training battles (War Sails).

## Freely given

This work is **public domain** — no license, no strings, no permission to ask
([The Unlicense](LICENSE)). Use it, share it, clone it, change it, sell it, do whatever you
want with it. *"Freely you have received; freely give."*

---

## For developers

Built by the same two-hands team as [Immersive AI](https://github.com/TraxData313/ImmersiveAI)
— Anton dreams and playtests, Claude designs and writes the code — and follows the same
conventions: ideas land in [TASKS_TODO.md](TASKS_TODO.md), finished work moves to
[TASKS_DONE.md](TASKS_DONE.md) with a timestamp, and the deep documentation lives in
[CLAUDE.md](CLAUDE.md).

| Project | Target | Purpose |
|---|---|---|
| `src/TrainingBattles.Core` | netstandard2.0 | Game-independent logic: aftermath math, cooldown. Unit-tested. |
| `src/TrainingBattles.Module` | net472 | The Bannerlord module: menus, picker GUI, the battle, scouting, aftermath. References game DLLs. |
| `tests/TrainingBattles.Core.Tests` | net8.0 | xUnit tests for Core. |

**Build & deploy** (requires the .NET 8 SDK and a Bannerlord install; path in
`Directory.Build.props`):

```powershell
dotnet build -c Release                                      # build everything
dotnet test  -c Release                                      # Core unit tests (keep green)
powershell -ExecutionPolicy Bypass -File tools\deploy.ps1    # build + install into the game
```
