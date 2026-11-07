# EternalDigger

A side-view, tile-by-tile digging game built on the ConsoleEngine ECS. Dig down an endless mine, carry copper, silver
and gold back to the house on the surface, sell it, and buy ladders so you can climb out of ever deeper shafts.

```text
 ETERNAL DIGGER   GOLD 0g  ╟╢8  OIL ▮▮▮▮▮▮▮▮  DEPTH 0m  BAG ◆0 ◆0 ◆0 ◆0 = 0g
 ←→ walk/dig   ↓ dig   ↑ climb   SPACE ladder   E use/buy   hold E dynamite   hold R rescue   ESC save & quit
        ▄▄▀▄▀▄▀▄▀▄▀▄▀▄▀▄▀▄▀▄▀▄▀▄
         ─── EternalDigger ─────
            ▀             ▀     ▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄                                    MINE
          ┌┬┐     ▄▄▄▄    ┌┬┐      ╟─╢ ╟─╢                                         ╔════╗
         ─└┴┘─────║║║║────└┴┘───   ╟─╢ ╟─╢                                         ║    ║
                  ║║║•                                                             ║    ║
         ─────────║║║║────────── ▀▀▀▀▀▀▀▀▀▀▀▀▀▀              ☻•                    ║    ║
                  ║║║║              LADDERS                 ▐█/                    ║    ║
▀▀▀▓▓▓▀▀▀▀▀▀▓▀▀▓▓▀▓▓▓▀▓▀▓▀▀▀▓▓▓▀▀▓▀▀▀▀▀▀▀▀▓▀▓▓▀▀▀▀▓▀▓▀▀▀▀▀▀▀▀▓▀▀▓▀▀▀▀▀▓▀▀▓▀▀▀▀▓▀▓▀▀▀╟──╢▀▀▓▀▀▀▓▀▀▀▓▀▓▀▓▀▓▀▀▓▀▀▀
·▒·▒░ ░·▒ ·░▒░ ·▒░▒▒··░▒▒▒░▒░▒░▒░·░░░▒·▒··▒░·▒·▒▒░ ░ ░ ░ ▒▒▒▒░▒▒▒·░░▒· ▒·░   ░░░▒░▒▒╟──╢░▒ ░░░ ░ ▒· ▒░·▒░▒░
                                                                                ░░ ░╟──╢░▒▒▒
                                                                                    ░░░░
```

*The start screen, simplified (no colours, sky or hills): the house and ladder shop on the left, the miner in the
middle, the shaft under the `MINE` frame on the right. Everything below the grass is dark until the headlamp lights
it.*

## Run it

The game needs a real console or terminal, so it can't run headless. Build it, then start the exe from its output
folder:

```bash
dotnet build Examples/EternalDigger
```

```bash
cd Examples/EternalDigger/bin/Debug/net10.0 && ./EternalDigger
```

On Windows run `EternalDigger.exe`. The optional first argument is the **seed** for a new mine: the same number always
digs the same mine. Without it, every new mine is different (`Environment.TickCount`).

```bash
./EternalDigger 42
```

The game opens on a title menu: `CONTINUE` (when there is a save) or `NEW MINE`, picked with `↑` `↓` and `E`. It
saves on every sale and ladder purchase, every 60 s underground, and on `ESC`, to one slot:
`EternalDigger/save.dat` in the user's local app-data folder (`%LOCALAPPDATA%` on Windows,
`~/Library/Application Support` on macOS, `~/.local/share` on Linux). Delete that file to start over without the menu.

On Windows the game sets a 120 × 48 console with an 8 × 16 font. In a macOS or Linux terminal, make the window at least
120 columns by 48 rows.

## Controls

| Key | What it does |
|---|---|
| `←` `→` or `A` `D` (hold) | Walk. Into rock, dig it instead |
| `↓` or `S` (hold) | Dig down, or step down a ladder |
| `↑` or `W` (hold) | Climb up. Only works while standing on a ladder |
| `SPACE` | Place a ladder in the tile you stand in |
| `E` | At the house door: sell your ore. At the shop counter: buy the selected item (ladders, lamp oil, upgrades, the blasting kit, then dynamite). At the `LIFT` sign: build 10 m of lift. On the lift's top or bottom landing: call the car, or get in |
| `E` (hold 0.5 s) underground | Plant a lit stick of dynamite in the rock you face. The fuse burns 2 s: walk away. The blast clears 3 × 3 tiles of any rock, obsidian too, and bags the ore |
| `↑` / `↓` at the shop counter | Choose what to buy. The panel above the miner shows the item, what you have and the price |
| `↑` / `↓` in the lift car | Ride to the top / the bottom. `←` `→` (or `↓` at the bottom) gets out |
| `R` (hold 1.5 s) | Rescue: back to the surface. You keep gold and ladders, but **lose the ore in your bag** |
| `ESC` | Save and quit |

## The docs

| Read this | When you want to know… |
|---|---|
| [docs/game-brief.md](docs/game-brief.md) | The game with no tech at all: a self-contained brief for design conversations (paste it into a new chat) |
| [docs/gameplay.md](docs/gameplay.md) | How the game plays: the loop, digging, ladders, the economy, light and every number behind them |
| [docs/architecture.md](docs/architecture.md) | How the game is built on the engine: `GameBase`, `EcsScene`, the `World`, systems and one frame step by step |
| [docs/rendering-and-effects.md](docs/rendering-and-effects.md) | How the screen is drawn: `Canvas`, the light map, tile looks, the particle pipeline and every effect |
| [docs/working-with-the-code.md](docs/working-with-the-code.md) | How to change things: recipes for new tiles, effects, systems and HUD values, and the rules to follow |
| [docs/testing.md](docs/testing.md) | What is tested, how to run the tests, and how the frame-by-frame gameplay tests work |
| [docs/reference.md](docs/reference.md) | Lookup tables: files, systems in run order, components, constants |

The original design contract the game was built from is in
[`docs/design/eternal-digger.md`](../../docs/design/eternal-digger.md). Support and triage notes for agents are in
[`docs/agent/`](../../docs/agent/README.md) (rules in `03-rules.md` § EternalDigger, playbook P16 in `05-triage.md`).

## At a glance

- **Project:** `Examples/EternalDigger/EternalDigger.csproj` (exe), tests in `Examples/EternalDigger.Tests`.
- **Engine features used:** `GameBase`, `EcsScene<TGame>`, `World`, `Query`, `EntitySystem`, `CommandBuffer`, the
  `Position`/`Velocity`/`Glyph` components, `Camera`, `RenderConsole`, `Input`.
- **Not entities:** the terrain. The mine is a plain grid (`Mining/Mine.cs`) that is drawn from the rows the camera
  can see, so an endless mine costs nothing per frame. Entities are the miner, particles and floating numbers.
- **Status:** builds with 0 warnings, 216 tests pass. It has been run in a macOS terminal, but has not been played with
  a real keyboard or checked in the Windows console. See [Known limitations](docs/gameplay.md#known-limitations).
