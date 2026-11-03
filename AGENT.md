# AGENT.md

Guide for AI agents working in this repository: what it is, where to find answers, how it is laid out, and the rules
for code and commits.

## What this is

**console-engine** is a small **game engine** that renders into the Windows console, written in C# / .NET 8. It ships as
the `ConsoleEngine` library (NuGet package, `PackageVersion` 0.1.0) with example games under `Examples/`.

The two priorities for every change:

1. **Performance.** Everything inside the frame loop runs many times per second, uncapped. Treat it as a hot path
   ([§ Performance](#performance)).
2. **Clean code.** Follow best practices, use precise and consistent names, keep code small and readable
   ([§ Code style](#code-style), [§ Naming](#naming)).

Platform: **Windows only.** Rendering and input go through Win32 P/Invoke (`kernel32.dll`, `user32.dll`).

## Where to look for answers

| Question | Look here |
|---|---|
| "Why did X not happen for this user?", support/triage | [`docs/agent/README.md`](docs/agent/README.md) → [`05-triage.md`](docs/agent/05-triage.md) |
| What a rule/number is (timing, keys, scenes, TerraForM power/score/maps) | [`docs/agent/03-rules.md`](docs/agent/03-rules.md) |
| Order of calls, early returns, what fails silently | [`docs/agent/04-flows.md`](docs/agent/04-flows.md) |
| Processes, ports, game sizes/flags | [`docs/agent/01-system-map.md`](docs/agent/01-system-map.md) |
| User-visible strings and exception messages | [`docs/agent/06-diagnostics.md`](docs/agent/06-diagnostics.md) |
| Game loop and public engine API | `ConsoleEngine/GameBase.cs`, `ConsoleEngine/Infrastructure/**` |
| Rendering abstraction vs. Win32 implementation | `ConsoleEngine.Abstractions/Rendering/IConsoleHandler.cs` vs. `Libs/ConsoleEngine.Windows/ConsoleHandler.cs` |
| Win32 signatures and structs | `Libs/ConsoleEngine.Windows/LowLevel/` (`Kernel32.cs`, `User32.cs`) |
| Vector/matrix math | `Libs/Microsoft.Xna.Framework/` (copied from MonoGame, change only with good reason) |
| How to use the engine | `Examples/Snake` (smallest), `Examples/TerraForM` (scenes, assets, tests) |
| Build/test pipeline | `.github/workflows/build-and-test.yml` |
| Formatting rules | `.editorconfig` + [§ Code style](#code-style) |
| Open feature requests | GitHub issues: mouse input (8), macOS (7), more colours (6) |

Every name in `docs/agent/` is grep-able. Search for the identifier you are interested in before reading code.

## Project setup

| Project | Path | Kind | Depends on |
|---|---|---|---|
| `ConsoleEngine` | `ConsoleEngine/` | Library (NuGet) | Abstractions, Windows, Logger, Xna |
| `ConsoleEngine.Abstractions` | `ConsoleEngine.Abstractions/` | Library: interfaces + value types | — |
| `ConsoleEngine.Windows` | `Libs/ConsoleEngine.Windows/` | Library, namespace `ConsoleEngine.Native` | Abstractions |
| `ConsoleEngine.Logger` | `Libs/ConsoleEngine.Logger/` | Exe: TCP log viewer (port 11000) | — |
| `Microsoft.Xna.Framework` | `Libs/Microsoft.Xna.Framework/` | Library: math types | — |
| `TerraForM`, `Snake`, `Asteroids`, `Platformer`, `ConsoleGame` | `Examples/*` | Exe | ConsoleEngine |
| `TerraForM.Tests` | `Examples/TerraForM.Tests/` | NUnit 4 + NSubstitute | TerraForM |

All projects: `net8.0`, `LangVersion` 12, `TreatWarningsAsErrors` = `True`. Solution: `ConsoleEngine.sln`.

```bash
dotnet build
```

```bash
dotnet test
```

Games need a real Windows console, so an agent cannot run them headless. TerraForM loads `Assets/Maps/*.txt` and the
logger exe by **relative path**, so run a game's exe from its output folder (e.g.
`Examples/TerraForM/bin/Debug/net8.0/`). CI runs restore → build → test on `windows-latest` for PRs to `main`.

## Folder structure

```
ConsoleEngine.sln
.editorconfig                     formatting rules (see Code style)
AGENT.md / CLAUDE.md              agent instructions
docs/agent/                       support/triage docs for agents (01–09 + README)
ConsoleEngine/                    the engine
  GameBase.cs                       abstract game: Initialize/Start/Stop, frame loop
  Collections/Deque.cs              double-ended queue (used by Snake)
  Extensions/ArrayExtensions.cs     To1D for 2D arrays
  Infrastructure/
    GameTime.cs                     Delta, Fps, SetInterval
    Inputs/Input.cs                 Input.Instance singleton over IInputHandler
    Logging/Log.cs                  TCP client to ConsoleEngine.Logger
    Rendering/RenderConsole.cs      frame buffer + Draw* API
    Rendering/Sprite.cs             Pixel[,] sprites, FromString/FromStringArray
    Scenery/Scene.cs, SceneManager.cs  scene stack (Push/Pop/Set)
ConsoleEngine.Abstractions/
  Inputs/  IInputHandler, Key, KeyState
  Rendering/  IConsoleHandler, Pixel, FontInfo
Libs/
  ConsoleEngine.Windows/          ConsoleHandler, InputHandler, LowLevel/ (P/Invoke + structs)
  ConsoleEngine.Logger/           Program.cs (logger exe)
  Microsoft.Xna.Framework/        Vector2/3/4, Point, Rectangle, Matrix, …
Examples/
  <Game>/Program.cs               creates the game, Initialize(), Start()
  <Game>/<Name>Game.cs            GameBase subclass
  <Game>/GameObjects/             game entities (TerraForM: Tiles/ for map tiles)
  TerraForM/Scenes/               GameScene, MenuScene, InputNameScene
  TerraForM/Commands/             Command, Move, Plant, Wait
  TerraForM/Assets/               Maps/*.txt, Sprites.cs, Texts/HudTexts.cs, Story/ (unused)
  TerraForM.Tests/                RoverTests.cs
.github/workflows/                build-and-test.yml
```

## Architecture in one minute

- A game derives from `GameBase`, implements `OnInitialize`/`OnUpdate`/`OnRender`, and calls `Initialize()` then
  `Start()`.
- Each frame, `GameBase.Start` runs: `GameTime.Update` → `Input.Instance.Update` → clear → `Scenes.Update` → `OnUpdate`
  → `Scenes.Render` → `OnRender` → `Display` (one `WriteConsoleOutput`) → `Scenes.Cleanup`. Details are in
  [`docs/agent/03-rules.md`](docs/agent/03-rules.md#frame-loop).
- Drawing writes into one preallocated `Pixel[]` (`RenderConsole._pixels`). Nothing touches the console until `Display`.
- The engine depends on `IConsoleHandler`/`IInputHandler`. **Win32 lives only in `ConsoleEngine.Windows`.** Keep it
  that way: a future macOS backend (issue 7) must be able to replace that project alone. One known leak:
  `GameBase` and `Input` currently `new` up `ConsoleHandler`/`InputHandler` directly.
- Win32 return values are currently ignored. When touching `ConsoleHandler`, prefer checking them over adding more
  silent calls.

<a id="performance"></a>
## Performance

**Hot path** = anything reachable from the frame loop: game and scene `OnUpdate`/`OnRender`, `RenderConsole.Draw*`,
`Sprite` drawing, `InputHandler.Update`, `ConsoleHandler.Render`.

| Do | Don't |
|---|---|
| Preallocate and reuse buffers (`RenderConsole._pixels`, `ConsoleHandler._chars`) | Allocate per frame: `new` of classes, `List<T>`, arrays |
| Build sprites once as `static readonly` (see `Rover`, `Rock`) | Build sprites or strings inside `OnRender` |
| Use structs for small data (`Pixel`, `KeyState`, `Coord`) and `Span<T>`/`Span2D<T>` | Box structs or copy large arrays |
| Plain `for` loops in hot code | LINQ, `foreach` over interfaces, lambdas/closures capturing state |
| Keep exactly one console write per frame (`WriteConsoleOutput`) | Add per-cell or per-line Win32 calls |
| Scale movement/timers by `GameTime.Delta` | Add per-frame amounts. The loop is uncapped, so behaviour changes with FPS |
| Measure: `ShowFps = true` + `EnableLogger = true` shows FPS in the logger title | Guess about performance |

String interpolation in debug overlays (`IsDebugMode`) is acceptable. Known debt, which you should not copy and should
fix when nearby:
- `Deque<T>` allocates a node per push (its own `TODO`).
- `InputHandler.GetPressedKeyCodes` uses LINQ.
- TerraForM `World.Intersects` allocates a `List` per call.
- `SceneManager` calls `GC.Collect()` on unload.
- `Rover`/Asteroids `Player` move without `Delta`.

Tag performance work as `perf:` commits.

<a id="code-style"></a>
## Code style (`.editorconfig`)

- **Indentation:** 4 spaces (C#), 2 spaces (YAML). `.csproj`/XML 4 spaces.
- **Line endings:** LF in the repository (`end_of_line = lf`). Git may check out CRLF on Windows (`core.autocrlf`).
  That's fine. Don't commit mass line-ending changes.
- **Max line length:** 125. **Final newline** required. **Trailing whitespace** trimmed. **Charset** UTF-8. Sprites use
  box-drawing characters, so keep files UTF-8. Most existing `.cs` files start with a BOM, but `charset = utf-8` means
  no BOM. Editors may strip it on save, so leave BOM-only diffs out of feature commits.
- **Namespaces:** file-scoped (`namespace ConsoleEngine.Infrastructure.Rendering;`). All files already follow this.
- **`var`:** everywhere (`csharp_style_var_*` = `true`).
- **Primary constructors:** `.editorconfig` says `csharp_style_prefer_primary_constructors = false` (don't convert
  existing classes). The codebase does use them for small types that take dependencies or values (`Camera(GameBase game)`,
  `Hud(GameScene scene)`, `Move(Direction direction, float duration = 100)`). Follow the surrounding file.
- **Braces:** required for multi-line bodies, optional for single-line (`csharp_prefer_braces = when_multiline`).
- **Expression-bodied members:** when they fit on one line.
- **`using`s:** `System.*` first, no blank lines between groups.
- **`TreatWarningsAsErrors`:** on in every project. Fix warnings, don't suppress them.
- **`Nullable`:** currently enabled **only** in `TerraForM.Tests`. Don't sprinkle `?` annotations into other projects.
  Enabling nullability is a per-project change in its own commit.
- **Section banners:** classes are grouped with `//****` banner comments (`//** fields`, `//** ctor`, `//** props`,
  `//** public methods`, `//** internals`). Keep that layout when adding members.
- IDE style rules are **not** enforced at build (`EnforceCodeStyleInBuild` is not set). Your editor/IDE applies them.

<a id="naming"></a>
## Naming

| Type | Convention | Example |
|---|---|---|
| Classes / structs / enums | PascalCase noun | `RenderConsole`, `SceneManager`, `Pixel`, `KeyState` |
| Interfaces | `I` prefix | `IConsoleHandler`, `IInputHandler` |
| Private fields | `_camelCase` | `_consoleHandler`, `_pixels`, `_scenes` |
| Static readonly / const | PascalCase | `Input.Instance`, `Pixel.Blank`, `World.TileSize`, `EndOfMessage` |
| Games | `<Name>Game : GameBase` | `SnakeGame`, `TerraformGame`, `AsteroidsGame` |
| Scenes | `<Name>Scene : Scene<TGame>` in `Scenes/` | `GameScene`, `MenuScene`, `InputNameScene` |
| Game objects | Noun in `GameObjects/` | `Rover`, `Asteroid`, `Player`, `Camera` |
| Map tiles | Noun deriving `MapTile`, in `GameObjects/Tiles/` | `Rock`, `PlantSpot`, `FinishPoint` |
| Commands (TerraForM) | Verb deriving `Command`, in `Commands/` | `Move`, `Plant`, `Wait` |
| Events / handlers | `On<Event>` / `<Sender>On<Event>` | `OnFinishReached` / `RoverOnOnFinishReached` |
| Engine lifecycle hooks | `On<Phase>` | `OnInitialize`, `OnUpdate`, `OnRender`, `OnLoad`, `OnUnload` |
| Win32 interop | `internal`, in `LowLevel/`. Keep Win32 names for functions, structs PascalCase | `SetConsoleWindowInfo`, `CharInfo`, `SmallRect`, `Coord` |
| Win32 constants / virtual keys | Win32 spelling (UPPER_CASE) | `SC_MAXIMIZE`, `MF_BYCOMMAND`, `Key.ESCAPE`, `Key.LEFT` |
| Assets | `Assets/<Kind>/`, maps `map<N>.txt`, sprites as `static readonly` on `Sprites` | `Assets/Maps/map1.txt`, `Sprites.GameOver` |
| UI text constants | `const string` on a `*Texts` class | `HudTexts.NoPower` |
| Test classes | `<ClassUnderTest>Tests` | `RoverTests` |
| Test names | `Method_State_Expected` | `PowerDepleted_WhenRemainingPowerIsZero_ReturnsTrue` |

Names should say what something is in game terms. Spell them correctly in new code, and don't propagate existing
typos (`AthmosphereGeneratorsPlanted`, `_ellapsed`). Renaming public members of `ConsoleEngine` is a breaking change.

## Commits: Conventional Commits

Use [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/#specification):

```
<type>[optional scope][!]: <description>

[optional body]

[optional footer(s)]
```

| Type | Use for |
|---|---|
| `feat` | New capability (engine API, game feature) |
| `fix` | Bug fix |
| `perf` | Performance improvement (allocations, frame time) |
| `refactor` | Code change with no behaviour change |
| `test` | Tests only |
| `docs` | Documentation only (`docs/agent`, `AGENT.md`, README) |
| `build` | `.csproj`, NuGet packages, target framework |
| `ci` | `.github/workflows` |
| `style` | Formatting only |
| `chore` | Anything else |

- **Scopes** (optional): `engine`, `abstractions`, `windows`, `logger`, `xna`, `terraform`, `snake`, `asteroids`,
  `platformer`, `consolegame`, `docs`, `ci`.
- **Description:** imperative, lower case, no trailing period. For example `fix(terraform): reset score on restart`.
- **Breaking changes** to the public `ConsoleEngine` API or target framework: `!` after the type/scope and/or a
  `BREAKING CHANGE:` footer. For example `chore!: net8.0` (#10).
- One logical change per commit. Don't mix formatting with behaviour changes.

## Before you finish a change

1. `dotnet build` with 0 warnings and `dotnet test` passing.
2. No new per-frame allocations in the hot path.
3. Update `docs/agent/` as described in the maintenance table in [`CLAUDE.md`](CLAUDE.md#documentation), and update this
   file if the project layout, conventions or commands change.
