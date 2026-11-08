# 01 — System map

## Deployable units

Nothing is hosted and nothing is exposed to the internet. Every unit is a local Windows executable or a library.

| Project (path) | Kind | Purpose |
|---|---|---|
| `ConsoleEngine` (`ConsoleEngine/ConsoleEngine.csproj`) | Library, NuGet `PackageVersion` 0.2.0, `GeneratePackageOnBuild` | `GameBase` game loop, `RenderConsole`, `Input`, `GameTime`, `SceneManager`, `Log`, `Deque<T>`, ECS (namespace `ConsoleEngine.Ecs`: `World`, `Query`, `CommandBuffer`, `EntitySystem`, `SystemGroup`, `EcsScene<TGame>`, built-in components and systems). Runs only inside an `EcsScene` or game code that calls it |
| `ConsoleEngine.Abstractions` | Library | `IConsoleHandler`, `IInputHandler`, `Pixel`, `FontInfo`, `Key`, `KeyState` |
| `ConsoleEngine.Windows` (`Libs/ConsoleEngine.Windows`, namespace `ConsoleEngine.Native`) | Library | Win32 implementation: `ConsoleHandler`, `InputHandler`, P/Invoke in `LowLevel/Kernel32.cs`, `LowLevel/User32.cs` |
| `ConsoleEngine.Logger` (`Libs/ConsoleEngine.Logger`) | **Exe** `ConsoleEngine.Logger.exe` | Separate console window that shows `Log.WriteLine` output and FPS. Referenced by `ConsoleEngine.csproj`, so the build copies it next to every game |
| `Microsoft.Xna.Framework` (`Libs/Microsoft.Xna.Framework`) | Library | Math types (`Vector2`, `Point`, `Rectangle`, `Matrix`…) copied from MonoGame |
| `TerraForM` (`Examples/TerraForM`) | Exe | Mars-rover puzzle game: scenes, maps in `Assets/Maps/*.txt`, scoring |
| `TerraForM.Tests` | NUnit test project | One test: `Rover_PowerIsDepletedWhenRemainingPowerIsZeroOrLess` |
| `ConsoleEngine.Tests` (`ConsoleEngine.Tests/`) | NUnit test project | Engine tests: `RenderConsole`, `Sprite`, `SceneManager`, `GameTime.Interval`, `Deque<T>`, `ArrayExtensions`, ECS (`Ecs/`: `World`, `Query`, `CommandBuffer`, `SystemGroup`, `EcsScene`, `Archetype`, `ComponentMask`, `Systems/`). Uses `Fakes/`, no Win32 |
| `ConsoleEngine.Benchmarks` (`ConsoleEngine.Benchmarks/`) | **Exe**, BenchmarkDotNet 0.15.8 | `Baseline/OopUpdateBenchmarks` (list of classes vs. struct arrays), `Ecs/WorldBenchmarks`, `Ecs/QueryBenchmarks`. Built by CI, never run by CI |
| `Snake`, `Asteroids`, `Platformer`, `ConsoleGame` and their ECS ports `Snake.Ecs`, `Asteroids.Ecs`, `Platformer.Ecs`, `ConsoleGame.Ecs`, `TerraForM.Ecs` (`Examples/*`) | Exe | Small demo games. Each `*.Ecs` project is the same game rebuilt on the ECS (`ConsoleEngine.Ecs`) for comparison |
| `Swarm` (`Examples/Swarm`) | Exe | ECS-only stress test: 100 000 particles (first argument) chasing a moving attractor |
| `Launcher` (`Examples/Launcher`) | **Exe** `Launcher.exe`, no engine dependency (plain `System.Console`) | Text menu that starts the ECS games (`GameCatalog.Games`: Snake.Ecs, Asteroids.Ecs, Platformer.Ecs, TerraForM.Ecs, ConsoleGame.Ecs, Swarm, EternalDigger). Build-order `ProjectReference`s (`ReferenceOutputAssembly=false, Private=false as default metadata`) make building it build those games, without copying anything into its output |
| `Launcher.Tests` (`Examples/Launcher.Tests`) | NUnit test project | `LauncherMenu` keys, `GameLocator` paths, and that every `GameCatalog` game has a `ProjectReference` in `Launcher.csproj` |
| `EternalDigger` (`Examples/EternalDigger`) | Exe | ECS-only digging game: dig ore in an endless mine, sell it at the house, buy ladders. Design: `docs/design/eternal-digger.md` |
| `EternalDigger.Tests` | NUnit test project | `MineTests`, `TilesTests`, `ShopTests`, `LightMapTests`, `ParticleLooksTests`, `MinerControlSystemTests` (plays frames with scripted `MinerInput`, no keyboard), `MinerVisibilityTests` (renders into a `RenderConsole` over `Fakes/FakeConsoleHandler`), `ElevatorTests`, `DynamiteTests`, `ShopCounterTests`, `UpgradesTests`, `LampOilTests`, `BagWeightTests`, `SaveFileTests`, `SaveSystemTests` (temp folders), `TitleMenuTests`, `InventorySystemTests` |

Hard dependency: **Windows only.** `ConsoleHandler` and `InputHandler` call `kernel32.dll`/`user32.dll` directly
(`GetStdHandle`, `WriteConsoleOutputW`, `SetCurrentConsoleFontEx`, `GetAsyncKeyState`…). No other platform
implementation exists; macOS support is GitHub issue 7, open.

## Processes and what talks to what

```
 Game.exe (e.g. TerraForM.exe)                       ConsoleEngine.Logger.exe   (only if EnableLogger = true)
 ├─ main thread: GameBase.Start() loop                ├─ Socket.Connect(localhost:11000)
 │   GameTime → Input → Scenes → Display              └─ prints lines / sets title "Game FPS: n"
 ├─ thread "Logger" (Log.DispatchMessages)  ──TCP──▶  (game is the server, logger is the client)
 │   Socket.Bind/Listen localhost:11000
 └─ Win32 console APIs (kernel32/user32) ──▶ the console window the game was started in
```

| Link | Direction | Detail | Where |
|---|---|---|---|
| Launcher → game process | spawn | `Process.Start` of `%SystemRoot%System32nhost.exe cmd.exe /c ".<ProjectName>.exe || pause"` with `UseShellExecute = true` and `WorkingDirectory` = the exe's folder, so the game gets a **classic console window** even when Windows Terminal is the default terminal. The launcher waits for conhost to exit (`WaitForExit`), so one game runs at a time. Exe path: `Examples/<ProjectName>/<launcher's output path, e.g. bin/Debug/net10.0>/<ProjectName>.exe`. **On macOS and Linux** there is no conhost: the apphost has no extension (`<ProjectName>`) and is started directly (`UseShellExecute = false`, same working directory), inheriting the launcher's terminal | `GameRunner.Run`, `GameRunner.BuildArguments`, `GameLocator.GetExecutablePath` |
| Game → console window | out | Whole frame written each frame with `WriteConsoleOutput` | `ConsoleHandler.Render` |
| Keyboard → game | in | `GetAsyncKeyState(i)` polled for all 256 virtual keys every frame | `InputHandler.Update` |
| Game → logger | out, TCP | Game **listens** on `Dns.GetHostEntry("localhost").AddressList[0]`, port **11000** (hard-coded). No auth. Messages are UTF-8 and end with `'\0'` | `Log` static ctor, `Log.Start` |
| Game → logger process | spawn | `Process.Start` of `"ConsoleEngine.Logger.exe"` with `UseShellExecute = true` (relative file name) | `Log.Start` |
| TerraForM → filesystem | in | `File.ReadAllLines("Assets/Maps/{name}.txt")`, a path **relative to the working directory** | `GameScene.MapAssetPath`, `WorldLoader.LoadWorld` |

No async machinery exists beyond this: no queues and no scheduled jobs. "Timers" are `GameTime.SetInterval`
callbacks that run on the game loop thread ([03 § Intervals](03-rules.md#intervals)).

## Example game configuration (as constructed)

| Game | Width × Height (cells) | Font (W×H px, face) | Flags set in `Program.cs` / ctor |
|---|---|---|---|
| `TerraformGame` | 72 × 50 | 15×15 Consolas | `Console.ClearColor = Black` |
| `TerraformEcsGame` | 72 × 50 | 15×15 Consolas | `Name = "TerraForM (ECS)"`, `Console.ClearColor = Black` |
| `SnakeGame` | 30 × 30 | 14×14 Consolas | `ShowFps = true`, `ClearScreenOnEachFrame = false` (in `OnInitialize`) |
| `SnakeEcsGame` | 30 × 30 | 14×14 Consolas | `Name = "Snake (ECS)"`, `ShowFps = true` (no logger, so the FPS is shown nowhere) |
| `AsteroidsGame` | 200 × 200 | 4×4 Consolas | `EnableLogger = true` |
| `AsteroidsEcsGame` | 200 × 200 | 4×4 Consolas | `Name = "Asteroids (ECS)"`, `EnableLogger = true`, `ShowFps = true`. First command-line argument = asteroid count (default 50) |
| `PlatformerGame` | 70 × 35 | 10×10 Consolas | `IsDebugMode = true`, `ShowFps = true` (in `OnInitialize`), `Name = "Platformer game"` |
| `PlatformerEcsGame` | 70 × 35 | 10×10 Consolas | `Name = "Platformer (ECS)"`, `IsDebugMode = true`, `ShowFps = true` (in `Program.cs`) |
| `ConsoleGame.Game` | 64 × 64 | 8×8 Consolas | `ShowFps = true`, `EnableLogger = true` |
| `ConsoleEcsGame` | 64 × 64 | 8×8 Consolas | `ShowFps = true`, `EnableLogger = true` |
| `SwarmGame` | 160 × 100 | 6×6 Consolas | `Name = "Swarm (ECS)"`, `EnableLogger = true`, `ShowFps = true`. First command-line argument = starting particle count (default 100 000) |
| `EternalDiggerGame` | 120 × 48 (`Layout.ScreenWidth` × `Layout.ScreenHeight`) | 8×16 Consolas | `Name = "EternalDigger"`. First command-line argument = seed for a NEW MINE (default `Environment.TickCount`); CONTINUE uses the saved seed. Reads and writes `<LocalApplicationData>/EternalDigger/save.dat` (+ `.tmp`, `.bad`). No logger, no FPS |

`GameBase(width, height, fontWidth, fontHeight)` always uses face `"Consolas"`.

<a id="environments"></a>
## Environments

There is no dev/staging/prod split. What changes behaviour:

| Dimension | Values | Effect |
|---|---|---|
| OS | Windows / other | Other OS: fails at first P/Invoke (`ConsoleHandler` ctor calls `GetStdHandle`). See [P1](05-triage.md#p1) |
| Console host **[external]** | classic `conhost` / Windows Terminal | The resize and font calls (`SetConsoleWindowInfo`, `SetConsoleScreenBufferSize`, `SetCurrentConsoleFontEx`) target the classic console. Under Windows Terminal (the default host on Windows 11) they are generally ignored, so the size and font are wrong. Return values are not checked, so nothing reports this. See [P2](05-triage.md#p2) |
| Working directory | game's output folder / elsewhere | TerraForM maps and the logger exe are found by relative path. See [P1](05-triage.md#p1) |
| Machine speed (FPS) | any | The loop is uncapped (no sleep in `GameBase.Start`), and TerraForM physics/power depend on FPS. See [03 § Frame-rate dependence](03-rules.md#fps) |
| Build config | `Debug` / `Release` | No behavioural difference in code. No `#if DEBUG` exists |

## CI

`.github/workflows/build-and-test.yml`: on push/PR to `main`, `windows-latest`, .NET 10.0.x,
`dotnet restore` → `dotnet build --no-restore` → `dotnet test --no-build` with `--settings coverage.runsettings
--collect:"XPlat Code Coverage"` → `dotnet reportgenerator` (local tool from `dotnet-tools.json`). The Markdown summary
goes to the job summary, and the HTML report is uploaded as the `coverage-report` artifact. There is no coverage
threshold, so coverage never fails the build. All projects set `TreatWarningsAsErrors`.
