# Training Battles

**Split your army. Pick a side. Fight yourself — and get better at it.**

A training mod for *Mount & Blade II: Bannerlord*. Divide your army into two teams through a
troop-picking GUI, choose which side defends, and run a full mock battle on the very terrain
you're standing on — then walk away with the experience and (almost) all of your men.

**Status: early — the ideas are written down, the code is being built. See
[TASKS_TODO.md](TASKS_TODO.md) for the plan.**

## What it will do

- **Divide your army in teams** — a picker GUI (in the spirit of the lair-attack troop
  selection) where you choose the heroes and troops of Team 1 and Team 2, and which team you
  personally command.
- **Pick the defender, then fight** — a real battle event between your two halves, with the
  usual options (fight, send troops, try to escape...) plus **Cancel training** to call the
  whole thing off, no harm done.
- **Nobody really dies** — after the battle, "dead" troops become wounded instead (rate
  configurable — and your surgeon's Medicine skill helps), everyone keeps the XP they earned
  (reduced by a configurable percent), and the army becomes disorganized. War is a school,
  not a funeral.
- **Once a day** — by default one training battle per 24 in-game hours (configurable).
- **See your ground** — practice your commanding on the actual terrain of your current map
  position; later, preview and pick between the possible battle maps for the spot — including
  when you *defend* in real battles.
- **Configurable everything** — every parameter in a JSON config file AND in the in-game
  **Mod Configuration Menu (MCM)** from the start.

## Why

To practice commanding and mock-fight without consequences, and to scout the battlefield you
would really fight on — see the hills before you have to hold them.

## Further dreams

Garrison training at your own castles and towns; naval training battles at sea (dividing
ships and troops, War Sails). See the bottom of [TASKS_TODO.md](TASKS_TODO.md).

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

Planned layout (mirrors ImmersiveAI):

| Project | Target | Purpose |
|---|---|---|
| `src/TrainingBattles.Core` | netstandard2.0 | Game-independent logic: team split rules, XP/casualty math, cooldown. Unit-tested. |
| `src/TrainingBattles.Module` | net472 | The Bannerlord module: menus, the picker GUI, the battle event, aftermath. References game DLLs. |
| `tests/TrainingBattles.Core.Tests` | net8.0 | xUnit tests for Core. |

**Build & deploy** (requires the .NET 8 SDK and a Bannerlord install; path in
`Directory.Build.props`):

```powershell
dotnet build -c Release                                      # build everything
dotnet test  -c Release                                      # Core unit tests (keep green)
powershell -ExecutionPolicy Bypass -File tools\deploy.ps1    # build + install into the game
```
