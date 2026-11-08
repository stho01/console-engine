# AGENTS.md

Guide for AI agents working in this repository: what it is, where to find answers, how it is laid out, and the rules
for code and commits.

## What this is

**console-engine** is a small **game engine** that renders into the Windows console, written in C# / .NET 10. It ships as
the `ConsoleEngine` library (NuGet package, `PackageVersion` 0.2.0) with example games under `Examples/`.

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
| ECS (entities, components, archetypes, chunks) | `ConsoleEngine/Ecs/`, design and phases in [`docs/design/ecs-plan.md`](docs/design/ecs-plan.md) |
| Performance measurements | `ConsoleEngine.Benchmarks/` (BenchmarkDotNet) |
| How to use the engine | `Examples/Snake` (smallest), `Examples/TerraForM` (scenes, assets, tests), `Examples/<Game>.Ecs` (ECS port of each game: components, systems, queries, `EcsScene`; compare with `Examples/<Game>`. `Snake.Ecs` is the smallest, `TerraForM.Ecs` the fullest), `Examples/Swarm` (ECS at scale, system ordering attributes), `Examples/EternalDigger` (a full original ECS game: endless grid world, particle effects, lighting; start at [`Examples/EternalDigger/README.md`](Examples/EternalDigger/README.md), design contract in [`docs/design/eternal-digger.md`](docs/design/eternal-digger.md)) |
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
| `TerraForM`, `Snake`, `Asteroids`, `Platformer`, `ConsoleGame`, their ports `<Game>.Ecs`, `Swarm` (ECS stress test) and `EternalDigger` (ECS game) | `Examples/*` | Exe | ConsoleEngine |
| `Launcher` | `Examples/Launcher/` | Exe: text menu that starts the ECS games (plain `System.Console`, no engine) | ECS games (build order only) |
| `TerraForM.Tests` | `Examples/TerraForM.Tests/` | NUnit 5 + NSubstitute | TerraForM |
| `Launcher.Tests` | `Examples/Launcher.Tests/` | NUnit 5 | Launcher |
| `EternalDigger.Tests` | `Examples/EternalDigger.Tests/` | NUnit 5, hand-written `Fakes/FakeConsoleHandler` | EternalDigger |
| `ConsoleEngine.Tests` | `ConsoleEngine.Tests/` | NUnit 5, hand-written fakes in `Fakes/` | ConsoleEngine |
| `ConsoleEngine.Benchmarks` | `ConsoleEngine.Benchmarks/` | Exe: BenchmarkDotNet 0.15.8 | ConsoleEngine |

All projects: `net10.0`, `LangVersion` 14, `TreatWarningsAsErrors` = `True`. Solution: `ConsoleEngine.sln`.

```bash
dotnet build
```

```bash
dotnet test
```

Coverage (same as CI; HTML report in `coverage/index.html`, gitignored):

```bash
dotnet test --settings coverage.runsettings --collect:"XPlat Code Coverage" --results-directory TestResults
```

```bash
dotnet tool restore
```

```bash
dotnet reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:coverage -reporttypes:Html
```

Benchmarks (always `Release`; `--job short` for a quick run; add `--counters CacheMisses+BranchMispredictions` for
hardware counters, which needs an elevated prompt on Windows):

```bash
dotnet run -c Release --project ConsoleEngine.Benchmarks -- --filter "*"
```

`coverage.runsettings` excludes `Microsoft.Xna.Framework`, `ConsoleEngine.Windows`, `ConsoleEngine.Logger` and `Program.cs`.
ReportGenerator is a local tool pinned in `dotnet-tools.json`. There is no coverage threshold yet.

Games need a real Windows console, so an agent cannot run them headless. TerraForM loads `Assets/Maps/*.txt` and the
logger exe by **relative path**, so run a game's exe from its output folder (e.g.
`Examples/TerraForM/bin/Debug/net10.0/`). CI runs restore → build → test with coverage on `windows-latest` for PRs to
`main`. The coverage summary is in the job summary, and the HTML report is the `coverage-report` artifact.

To play the ECS games, start the launcher. It builds them first and starts each one from its output folder: on Windows in its
own classic console window (`conhost.exe`, even when Windows Terminal is the default), on macOS and Linux in the
launcher's own terminal. A new game in `Examples/` is added
to `GameCatalog.Games` **and** as a build-order `ProjectReference` in `Launcher.csproj`; `GameLocatorTests` checks both:

```bash
dotnet run --project Examples/Launcher
```

## Folder structure

```
ConsoleEngine.sln
.editorconfig                     formatting rules (see Code style)
coverage.runsettings              coverlet settings for `dotnet test` coverage
dotnet-tools.json                 local tools (ReportGenerator)
AGENTS.md / CLAUDE.md             agent instructions (CLAUDE.md imports AGENTS.md)
docs/agent/                       support/triage docs for agents (01–09 + README)
docs/design/                      design plans (ecs-plan.md, eternal-digger.md)
ConsoleEngine/                    the engine
  GameBase.cs                       abstract game: Initialize/Start/Stop, frame loop
  Collections/Deque.cs              double-ended queue (used by Snake)
  Ecs/                              World (+ World.Create.cs overloads), Entity, Archetype, Chunk, ChunkPool,
                                    ComponentMask, ComponentType<T>/ComponentRegistry, EcsThrowHelper,
                                    Query, QueryDescription, EntityChunk, IForEach<…>, CommandBuffer
                                    (+ .Create.cs), EntitySystem, SystemGroup, UpdateBefore/UpdateAfter
                                    attributes (UpdateOrderAttributes.cs), EcsScene<TGame>
    Components/                     built-in components: Position, Velocity, Rotation, Glyph, Outline, SpriteRef
    Rendering/                      Camera, OutlineLibrary, SpriteLibrary (namespace ConsoleEngine.Ecs.Rendering)
    Systems/                        built-in systems: MovementSystem, GlyphRenderSystem, OutlineRenderSystem,
                                    SpriteRenderSystem
  Extensions/ArrayExtensions.cs     To1D for 2D arrays
  Infrastructure/
    GameTime.cs                     Delta, Fps, SetInterval
    Inputs/Input.cs                 Input.Instance singleton over IInputHandler
    Logging/Log.cs                  TCP client to ConsoleEngine.Logger
    Rendering/RenderConsole.cs      frame buffer + Draw* API
    Rendering/Sprite.cs             Pixel[,] sprites, FromString/FromStringArray
    Scenery/Scene.cs, SceneManager.cs  scene stack (Push/Pop/Set)
ConsoleEngine.Tests/              engine unit tests (mirrors ConsoleEngine/ folders), Fakes/ (console handler, scene)
ConsoleEngine.Benchmarks/         BenchmarkDotNet: Baseline/ (OOP vs. struct arrays), Ecs/ (World, Query)
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
  <Game>.Ecs/                     ECS port of <Game>: <Name>EcsGame.cs, Components/ (game-only, Tags.cs for tags),
                                  Systems/ (EntitySystem subclasses), Scenes/ (an EcsScene that spawns entities and
                                  adds systems in run order). TerraForM.Ecs links Assets/ from Examples/TerraForM
  Swarm/                          ECS stress test (no OOP original): Components/, Systems/, Scenes/SwarmScene
  Launcher/                       game launcher: GameCatalog (menu entries), GameLocator (exe paths), LauncherMenu
                                  (keys), LauncherScreen (drawing), GameRunner (Process.Start + wait), LauncherTexts
  Launcher.Tests/                 LauncherMenuTests, GameLocatorTests
  EternalDigger/                  original ECS digging game (no OOP original): Mining/ (Mine grid, Tiles rules, Elevator),
                                  Components/, Systems/, Effects/ (EffectSpawner, ParticleLooks), Rendering/
                                  (Canvas, LightMap, TileLooks), Saving/ (SaveData, SaveFile, GameSaver),
                                  Scenes/ (TitleScene + TitleMenu, MineScene), Layout, Shop, ShopCounter, DeepCamp, Progress, Upgrades,
                                  LampOil, MinerInput.
                                  README.md + docs/ (gameplay, architecture, rendering-and-effects,
                                  working-with-the-code, testing, reference)
  EternalDigger.Tests/            Mine, Tiles, Shop, LightMap, ParticleLooks, frame-by-frame MinerControlSystem,
                                  MinerVisibility (render), Elevator, Dynamite, ShopCounter, Upgrades, LampOil,
                                  BagWeight, SaveFile, SaveSystem, TitleMenu, InventorySystem, DeepCamp, Gas, Darkness and Progress tests, Fakes/ (console handler)
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
- ECS games use an `EcsScene<TGame>`: it owns a `World`, runs its `Simulation` systems in `Scenes.Update` and its
  `Presentation` systems in `Scenes.Render`, and applies each group's `CommandBuffer` after the group
  ([`docs/agent/04-flows.md` F9](docs/agent/04-flows.md#f9)).
- The engine depends on `IConsoleHandler`/`IInputHandler`. **Win32 lives only in `ConsoleEngine.Windows`.** Keep it
  that way: a future macOS backend (issue 7) must be able to replace that project alone. One known leak:
  `GameBase` and `Input` currently `new` up `ConsoleHandler`/`InputHandler` directly.
- Win32 return values are currently ignored. When touching `ConsoleHandler`, prefer checking them over adding more
  silent calls.

<a id="performance"></a>
## Performance

**Hot path** = anything reachable from the frame loop: game and scene `OnUpdate`/`OnRender`, `RenderConsole.Draw*`,
`Sprite` drawing, `InputHandler.Update`, `ConsoleHandler.Render`, and all of `ConsoleEngine/Ecs/` (`World.Get` is
called per entity). In `Ecs/`, keep throws in `EcsThrowHelper` so checked methods stay inlinable, and run
`ConsoleEngine.Benchmarks` before and after a change.

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
| ECS components | Noun `struct` (data only, `unmanaged`) in `Components/`. Tags have no fields. Reuse the engine's (`ConsoleEngine.Ecs.Components`) before adding a game one | `Position`, `Velocity`, `Collider`, `Asteroid` |
| ECS systems | `<Name>System : EntitySystem` in `Systems/`. Queries are fields, created in `OnCreate`. Structural changes go through `Commands`. Prefer `Add` order in the scene; use `[UpdateBefore]`/`[UpdateAfter]` when a system must keep its place wherever it is added | `MovementSystem`, `CollisionSystem` |
| ECS jobs | `<Verb>Job : IForEach<…>`, a private struct inside its system | `MoveJob` |
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
| `docs` | Documentation only (`docs/agent`, `AGENTS.md`, README) |
| `build` | `.csproj`, NuGet packages, target framework |
| `ci` | `.github/workflows` |
| `style` | Formatting only |
| `chore` | Anything else |

- **Scopes** (optional): `engine`, `abstractions`, `windows`, `logger`, `xna`, `terraform`, `snake`, `asteroids`,
  `platformer`, `consolegame`, `launcher`, `docs`, `ci`.
- **Description:** imperative, lower case, no trailing period. For example `fix(terraform): reset score on restart`.
- **Breaking changes** to the public `ConsoleEngine` API or target framework: `!` after the type/scope and/or a
  `BREAKING CHANGE:` footer. For example `chore!: net8.0` (#10).
- One logical change per commit. Don't mix formatting with behaviour changes.

## Gotchas for agents

| Topic | What to know |
|---|---|
| **Coordinates** | 2D data is indexed `[x, y]`, width first: `Pixel[,]`, `char[,]`, `Sprite.Data`, and `Sprite.Width = GetLength(0)`. With `Span2D<T>`, `.Height` is dimension 0, i.e. x. That's why the loops in `RenderConsole.Draw(Span2D…)` look swapped. The frame buffer is 1D: `index = y * Width + x` |
| **Two `Vector2` types** | Engine and games use `Microsoft.Xna.Framework.Vector2`, but `Sprite.Origin` is `System.Numerics.Vector2` (alias in `Sprite.cs`), and `PlatformerGame` imports `System.Numerics`. Be explicit when you mix them |
| **Characters** | A cell holds one UTF-16 `char` (`CharInfo.UnicodeChar`). Box-drawing/block characters work with Consolas. Emoji and other surrogate pairs don't fit in a cell |
| **Global/static state** | `Input.Instance`, `GameTime` and `Log` are static and process-wide. Constructing any `GameBase` calls Win32 (`GetStdHandle` in `ConsoleHandler`), so don't instantiate games in unit tests. Test game logic against scenes/objects instead |
| **Test seams** | `GameScene.World` is `virtual` and `World` has a parameterless ctor so NSubstitute can mock them (`RoverTests`). Don't remove them. Engine tests use `FakeConsoleHandler` (NSubstitute can't capture `Span<Pixel>` in `Render`) and `InternalsVisibleTo` for `RenderConsole.Initialize`/`Display` and `SceneManager`'s internal ctor (tests pass `null` as the game, with `Fakes/RecordingScene`). `GameTime` is only tested where it doesn't depend on elapsed time. `GameBase`, `Input` and `Log` have no tests: they need Win32, sockets or a real clock. Add tests in `Method_State_Expected` style when you change logic |
| **ECS memory** | `World` keeps component data in native memory (`NativeMemory.AlignedAlloc`, 16 KiB chunks, 64-byte aligned columns). Always `Dispose` a world (tests: `using var world = new World();`): there is no finalizer, so an undisposed world leaks its chunks. Components must be `unmanaged` structs. Never store a `ref` from `World.Get<T>` across a structural change. Create/Add/Remove/Destroy inside a query loop throws: record them in a `CommandBuffer` (`EntitySystem.Commands`). `Destroy`/`Add`/`Remove` of a dead entity are skipped at playback. Test components are named `Location`/`Speed` so they don't shadow the engine's `Position`/`Velocity` (namespace lookup beats `using`). `Archetype`, `Chunk` and `ComponentMask` are `internal`: tests reach them through `World.GetArchetype` / `World.ChunkPool` (`InternalsVisibleTo`) |
| **Threading** | Everything runs on the game-loop thread, including `GameTime.SetInterval` callbacks. The only other thread is `Log`'s `"Logger"` thread. There is no locking, so never touch engine state from another thread |
| **NuGet package** | `ConsoleEngine` has `GeneratePackageOnBuild`, so every build produces a `.nupkg` (gitignored). `PackageVersion` (0.2.0) is bumped by hand, and no pipeline publishes it. Any change to its public API is a breaking change for consumers (`!` commit) |
| **Dependencies** | `CommunityToolkit.HighPerformance` 8.4.2 supplies `Span2D<T>` (namespace `CommunityToolkit.HighPerformance`). It replaced the deprecated `Microsoft.Toolkit.HighPerformance` |
| **Third-party code / licence** | `Libs/Microsoft.Xna.Framework` is copied from MonoGame (see its `README.md`). The repo has **no LICENSE file**. Don't copy in more third-party code, and don't add or change licensing, without asking the owner |
| **Running games** | Not possible headless. Verify with `dotnet build` + `dotnet test`, and say plainly that a change was not checked in a real console |

<a id="keep-docs-current"></a>
## Keeping agent docs up to date (mandatory)

**Every agent working in this repository must keep `docs/agent/` and this file current.** The docs describe behaviour
through exact identifiers, file paths and user-visible strings, so any code change can silently make them wrong. A
change that alters what they describe is **not finished** until the docs are updated **in the same commit or PR**.

| If you change… | Update |
|---|---|
| A project, process, port, P/Invoke surface, game size/font/flags, CI | `docs/agent/01-system-map.md` |
| Runtime state fields, TerraForM state, the map file format or a shipped map | `docs/agent/02-state-model.md` |
| A rule: timing, key semantics, scenes, drawing, power, damage, sequences, game over, score, planting, map rotation, name entry | `docs/agent/03-rules.md` (+ the affected playbook in `05-triage.md`) |
| A guard clause, early return, thrown exception or ignored Win32 result in a flow | `docs/agent/04-flows.md`, `06-diagnostics.md` |
| Any user-visible string (`HudTexts`, `Sprites`, logger output, exception messages) | `docs/agent/06-diagnostics.md` (verbatim) |
| A new failure mode or common complaint | `docs/agent/05-triage.md` + the routing table in `docs/agent/README.md` |
| A file/port/runtime prerequisite | `docs/agent/07-checks.md` |
| A control key or anything a user can do without code | `docs/agent/08-operator-actions.md` |
| A new domain term | `docs/agent/09-glossary.md` |
| Project/folder layout, build commands, code style, naming or commit conventions | `AGENTS.md` |

Rules:
- Use real identifiers and quote strings verbatim from source. Never write a name you haven't seen in the code.
- If you find a doc that is already wrong, fix it, even if your change didn't cause it.
- Delete statements you can no longer back with a file and line. An admitted gap is better than a wrong claim.
- Rows tagged **[inferred]** are guesses about support's wording. Replace them with real ticket language when you have it.
- Docs-only updates use `docs:` commits. When the docs change together with code, include them in the code's commit.

## Before you finish a change

1. `dotnet build` with 0 warnings and `dotnet test` passing.
2. No new per-frame allocations in the hot path.
3. Docs updated per [§ Keeping agent docs up to date](#keep-docs-current).
