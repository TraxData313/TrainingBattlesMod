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

- **Survey the ground** — see every battlefield the game could put your fight on: the map
  patch's own scene (marked *"this ground"*) plus every scene of the local terrain type —
  and pick the one a drill is fought on.
- **Ride out and scout** — enter any of those battlefields **alone**: no battle, no enemy,
  no cost, no cooldown. You spawn **on your deployment line**, facing the enemy's, with the
  distance called out — so you can judge both the terrain *and* the deploy before you ever
  have to fight there. If the map is good but the lines are bad, take another map or move.
- **The scouted lines are the drilled lines** — training battles and the scout preview use
  the same deterministic deployment recipe (the game's own map-patch machinery), so what
  you scouted is what you get. In a *real* defence the ground holds, but the enemy's
  approach direction decides which end is theirs — the mod tells you so, honestly.
- **Choose your ground when defending** (toggleable) — when you're the defender in a real
  field battle and more than one scene truly fits your map patch, the encounter menu offers
  the choice. (With the vanilla scene data each patch is claimed by exactly one scene, so
  this mostly matters with scene-pack mods.)

## The knobs

Every parameter lives in a JSON config
(`Documents\...\Configs\TrainingBattles\config.json`) **and** in the in-game **Mod
Configuration Menu (MCM)** — XP kept %, wounded %, cooldown hours, disorganized toggle,
defender ground choice, hotkey (default `G` on the campaign map).

## Status

The V1 core loop is **playtested and working** (divide → fight → nobody dies, wounded
filtered through the surgeon, XP banked, disorganized, cooldown). The ground pickers and
the scouting ride are built and awaiting playtest. See [TASKS_TODO.md](TASKS_TODO.md) for
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
