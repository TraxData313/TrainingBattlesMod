# Training Battles

**Drill your army against itself. Scout the ground like a real general. Fight where you
choose to fight.**

A mod for *Mount & Blade II: Bannerlord* built on two pillars:

## Pillar 1 — Training battles

Split your army into two halves through a troop-picking GUI, choose which half you command
(attacking or defending), and fight a full mock battle on the very terrain you're standing
on — or watch it resolve from the hill via the send-troops simulation.

**Nobody really dies.** Afterward the fallen wake up: your surgeon's Medicine saves most on
the spot, a configurable share of the rest (default 10%) are wounded, and the others shrug
it off. The men keep a configurable share of the XP they earned (default 75%), the party
goes disorganized for a while, and a cooldown (default 24 in-game hours) gates the next
drill. No loot lost, no prisoners taken, no renown or relations touched — war as a school,
not a funeral. **Cancel training** is always available before the fight and leaves the
campaign exactly as it was.

## Pillar 2 — Scouting & choosing your ground

What a real general does before the battle: know the field.

- **Select the battlefield** — see every battlefield the game could put your fight on: the
  map patch's own scene (marked *"this ground"*) plus every scene of the local terrain type
  — and pick the one a drill is fought on.
- **Ride out and scout** — enter any of those battlefields **alone**: no battle, no enemy,
  no cost, no cooldown. You spawn **on your deployment line**, facing the enemy's, with the
  distance called out — so you can judge both the terrain *and* the deploy before you ever
  have to fight there. If the map is good but the lines are bad, take another map or move.
- **The scouted lines are the drilled lines** — training battles and the scout preview use
  the same deterministic deployment recipe (the game's own map-patch machinery), so what
  you scouted is what you get.
- **Survey & scout in REAL battles** *(a must-have pillar — toggleable per side, both on by
  default)* — when a real field battle is about to start, the encounter menu offers the same
  two tools, defending **or** attacking — same names, same tools as in the muster:
  - **Select the battlefield** — pick it from the local terrain's variants (your true
    ground marked *"this ground"*), or leave it to fate.
  - **Ride out and scout a battlefield** — walk the field alone *before committing*. And
    here the preview is **exact**: both armies stand frozen on the map, so the attacker's
    true approach direction is already known — the lines, ends, and facings you scout are
    precisely the ones the battle will use. No time passes; the fight waits for your return.

## The knobs

Every parameter lives in a JSON config
(`Documents\...\Configs\TrainingBattles\config.json`) **and** in the in-game **Mod
Configuration Menu (MCM)** — XP kept %, wounded %, cooldown hours, disorganized toggle,
the real-battle survey/scout toggles (defending and attacking separately), hotkey
(default `G` on the campaign map).

## Status

The V1 core loop is **playtested and working** (divide → fight → nobody dies, wounded
filtered through the surgeon, XP banked, disorganized, cooldown). The real-battle
survey/scout tools are built and awaiting playtest. See [TASKS_TODO.md](TASKS_TODO.md) for
what's next: paying salaries for drills, scouting with companions, garrison training,
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
