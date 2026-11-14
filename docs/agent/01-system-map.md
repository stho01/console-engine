# 01 — System map

## Deployable units

Nothing is hosted and nothing is exposed to the internet. Every unit is a local Windows executable or a library.

| Project (path) | Kind | Purpose |
|---|---|---|
| `ConsoleEngine` (`ConsoleEngine/ConsoleEngine.csproj`) | Library, NuGet `PackageVersion` 0.2.0, `GeneratePackageOnBuild` | `GameBase` game loop, `RenderConsole`, `Input`, `GameTime`, `SceneManager`, `Log`, `Deque<T>`, ECS (namespace `ConsoleEngine.Ecs`: `World`, `Query`, `CommandBuffer`, `EntitySystem`, `SystemGroup`, `EcsScene<TGame>`, `FixedTimestep`, built-in components and systems). Runs only inside an `EcsScene` or game code that calls it |
| `ConsoleEngine.Physics` (`ConsoleEngine.Physics/ConsoleEngine.Physics.csproj`) | Library, NuGet `PackageVersion` 0.2.0, `GeneratePackageOnBuild`. Optional | 2D physics on the ECS (namespace `ConsoleEngine.Physics`): `PhysicsSystem` (implements `IPhysicsEvents` and `IPhysicsQueries`), `RigidBody`, `Collider`, `AngularVelocity`, `PolygonLibrary`, `TileGrid`, `CharacterMover`, `Interpolation`/`InterpolationSystem`, events. Depends on `ConsoleEngine` only; the engine knows nothing about it. Used by Asteroids.Ecs, PhysicsSandbox, PhysicsShowcase and Platformer.Ecs |
| `ConsoleEngine.Rendering3D` (`ConsoleEngine.Rendering3D/ConsoleEngine.Rendering3D.csproj`) | Library, NuGet `PackageVersion` 0.2.0, `GeneratePackageOnBuild`. Optional | 3D on the console (namespace `ConsoleEngine.Rendering3D`): `IRenderer3D` (`Clear`, `Draw`, `Finish`, `Colors`), the CPU `Rasterizer`, `Mesh` (`Version`/`MarkChanged`), `ObjFile`, `ConsolePalette`. Depends on .NET only (`System.Numerics`), not on `ConsoleEngine` or Xna. Used by Kartster |
| `ConsoleEngine.Rendering3D.OpenGL` (`ConsoleEngine.Rendering3D.OpenGL/ConsoleEngine.Rendering3D.OpenGL.csproj`) | Library, NuGet `PackageVersion` 0.2.0, `GeneratePackageOnBuild`. Optional | `GlRenderer : IRenderer3D`: draws the same picture as `Rasterizer` on the GPU (OpenGL 3.3 core in a hidden GLFW window, Silk.NET 2.23.0) into an `R8UI` palette-index framebuffer and reads it back in `Finish`. The ctor throws `InvalidOperationException` without OpenGL 3.3; `GlRenderer.TryCreate` returns false instead, so callers fall back to `Rasterizer`. Use it only from the thread that created it. Not used by any game yet |
| `ConsoleEngine.Abstractions` | Library | `IConsoleHandler`, `IInputHandler`, `Pixel`, `FontInfo`, `Key`, `KeyState` |
| `ConsoleEngine.Windows` (`Libs/ConsoleEngine.Windows`, namespace `ConsoleEngine.Native`) | Library | Win32 implementation: `ConsoleHandler`, `InputHandler`, P/Invoke in `LowLevel/Kernel32.cs`, `LowLevel/User32.cs` |
| `ConsoleEngine.Logger` (`Libs/ConsoleEngine.Logger`) | **Exe** `ConsoleEngine.Logger.exe` | Separate console window that shows `Log.WriteLine` output and FPS. Referenced by `ConsoleEngine.csproj`, so the build copies it next to every game |
| `Microsoft.Xna.Framework` (`Libs/Microsoft.Xna.Framework`) | Library | Math types (`Vector2`, `Point`, `Rectangle`, `Matrix`…) copied from MonoGame |
| `TerraForM` (`Examples/TerraForM`) | Exe | Mars-rover puzzle game: scenes, maps in `Assets/Maps/*.txt`, scoring |
| `TerraForM.Tests` | NUnit test project | One test: `Rover_PowerIsDepletedWhenRemainingPowerIsZeroOrLess` |
| `ConsoleEngine.Tests` (`ConsoleEngine.Tests/`) | NUnit test project | Engine tests: `RenderConsole`, `Sprite`, `SceneManager`, `GameTime.Interval`, `Deque<T>`, `ArrayExtensions`, ECS (`Ecs/`: `World`, `Query`, `CommandBuffer`, `SystemGroup`, `EcsScene`, `FixedTimestep`, `Archetype`, `ComponentMask`, `Systems/`), Lua (`Scripting/LuaScriptSystemTests`). Uses `Fakes/`, no Win32 |
| `ConsoleEngine.Physics.Tests` (`ConsoleEngine.Physics.Tests/`) | NUnit test project | Physics tests: `Collision`, `SpatialHashGrid`, `PhysicsSystem` (rules, events), `PhysicsSolverTests` (contact response over whole steps), `TileGridTests`, `PhysicsQueryTests`, `CharacterMoverTests`, `RotationTests`, `TileContactTests`, `SleepTests`, `InterpolationTests`. `InternalsVisibleTo` from `ConsoleEngine.Physics` |
| `ConsoleEngine.Rendering3D.Tests` (`ConsoleEngine.Rendering3D.Tests/`) | NUnit test project | `RasterizerTests` (culling, near-plane clipping, depth test, lighting), `ScreenDoorTests` (screen-door transparency, unlit meshes), `ObjFileTests` (OBJ parsing), `MeshTests` (`MarkChanged` bumps `Version`) |
| `ConsoleEngine.Rendering3D.OpenGL.Tests` (`ConsoleEngine.Rendering3D.OpenGL.Tests/`) | NUnit test project, `[Category("Gpu")]` | `GlRendererTests`: the CPU tests' scenes rendered by `Rasterizer` and `GlRenderer`, ≥ 97 % equal pixels; steady-state frame allocates nothing; fallback exception; dispose and create again. `Assert.Ignore("OpenGL 3.3 not available")` when `TryCreate` fails or the OS is not Windows (so in CI) |
| `ConsoleEngine.Benchmarks` (`ConsoleEngine.Benchmarks/`) | **Exe**, BenchmarkDotNet 0.15.8 | `Baseline/OopUpdateBenchmarks` (list of classes vs. struct arrays), `Ecs/WorldBenchmarks`, `Ecs/QueryBenchmarks`, `Physics/BruteForceCollisionBenchmarks` (all-pairs circle test, the baseline) and `Physics/PhysicsStepBenchmarks` (one whole `PhysicsSystem` step on the same bodies), `Physics/PhysicsQueryBenchmarks` (1 000 raycasts / box casts), `Scripting/LuaScriptBenchmarks` (`LuaScriptSystem` vs. the same logic in C#). Built by CI, never run by CI |
| `ConsoleEngine.Scripting` (`ConsoleEngine.Scripting/`) | Library (not packed) | Lua 5.4 scripting for ECS entities on KeraLua 1.4.11: components `Script`, `ScriptFaulted`, systems `LuaScriptSystem`, `LuaRenderSystem`, and `LuaRuntime`, `LuaScript`, `LuaScriptException`. Only games that reference it get Lua |
| `Scripted` (`Examples/Scripted`) | Exe | ECS demo: a player and 30 fireflies, each scripted in `Assets/Scripts/*.lua`. F5 reloads the scripts |
| `Kartster` (`Examples/Kartster`) | Exe, references `ConsoleEngine`, `ConsoleEngine.Physics` and `ConsoleEngine.Rendering3D` | 3D experiment: drive a Kartster kart on a Kartster track with a chase camera (`TrackScene`: Kartster's kart model on ECS physics at 120 Hz), or orbited in a garage (`GarageScene`), drawn through `IRenderer3D` (`KartsterGame.Renderer`, the CPU `Rasterizer` from `ConsoleEngine.Rendering3D`) into one coloured cell per pixel. Karts: every sub-folder of `Assets/Karts`. Track: the first `<id>.ribbon.obj` + `<id>.spline.obj` in `Assets/Tracks` (Kartster's track-editor export format) |
| `Kartster.Tests` (`Examples/Kartster.Tests`) | NUnit test project | `KartModelTests` (loading the shipped `olabil`), `TrackTests` (the shipped `star-circuit`: closed loop, wrap, direction, road height), `KartPhysicsSystemTests` (4 s throttle-then-right-steer through `KartPhysicsSystem` + `PhysicsSystem` matches `KartIntegrator` within 1 cm / 0.5° for Kart, wagon and a `YawInertiaScale` 1.5 kart; no allocation per step; a left torque turns counter-clockwise seen from above), `KartFrameTests` (frame conversion round trip and signs), `KartInputSystemTests` (keyboard steering ramp), `Effects/EffectsTests` (`SkidMarksTests`, `SlideSmokeTests`), `Driving/KartDynamicsTests` (Kartster-like model at 1/120 s: power-balance top speed for Kart/olabil/wagon, grass half speed, braking from 80 km/h, reverse cap, steering, handbrake slide, wagon upshifts, no allocation, `KartCatalog.ForKart`), `Driving/KartReferenceTests` (Kart/olabil/wagon against numbers measured from Kartster's own physics: top speed, 0–50/0–80 km/h, braking from 80 km/h, turn radius at 20/50 km/h, grass top speed, handbrake turn) |
| `Snake`, `Asteroids`, `Platformer`, `ConsoleGame` and their ECS ports `Snake.Ecs`, `Asteroids.Ecs`, `Platformer.Ecs`, `ConsoleGame.Ecs`, `TerraForM.Ecs` (`Examples/*`) | Exe | Small demo games. Each `*.Ecs` project is the same game rebuilt on the ECS (`ConsoleEngine.Ecs`) for comparison |
| `Swarm` (`Examples/Swarm`) | Exe | ECS-only stress test: 100 000 particles (first argument) chasing a moving attractor |
| `PhysicsSandbox` (`Examples/PhysicsSandbox`) | Exe | ECS-only physics demo: balls and boxes dropped into a walled box with two ledges and a moving platform (`ConsoleEngine.Physics`) |
| `PhysicsShowcase` (`Examples/PhysicsShowcase`) | Exe | ECS-only demo of every physics feature: tile level, turning boxes and polygons, a kinematic platform and windmill, a sensor zone, collision layers, a player with `MoveAndSlide`, a raycast laser, an overlap blast, sleeping and interpolation |
| `Launcher` (`Examples/Launcher`) | **Exe** `Launcher.exe`, no engine dependency (plain `System.Console`) | Text menu that starts the ECS games (`GameCatalog.Games`: Snake.Ecs, Asteroids.Ecs, Platformer.Ecs, TerraForM.Ecs, ConsoleGame.Ecs, Swarm, PhysicsSandbox, PhysicsShowcase, EternalDigger, Scripted, Kartster). Build-order `ProjectReference`s (`ReferenceOutputAssembly=false, Private=false as default metadata`) make building it build those games, without copying anything into its output |
| `Launcher.Tests` (`Examples/Launcher.Tests`) | NUnit test project | `LauncherMenu` keys, `GameLocator` paths, and that every `GameCatalog` game has a `ProjectReference` in `Launcher.csproj` |
| `EternalDigger` (`Examples/EternalDigger`) | Exe | ECS-only digging game: dig ore in an endless mine, sell it at the house, buy ladders. Design: `docs/design/eternal-digger.md` |
| `EternalDigger.Tests` | NUnit test project | `MineTests`, `TilesTests`, `ShopTests`, `LightMapTests`, `ParticleLooksTests`, `MinerControlSystemTests` (plays frames with scripted `MinerInput`, no keyboard), `MinerVisibilityTests` (renders into a `RenderConsole` over `Fakes/FakeConsoleHandler`), `ElevatorTests`, `DynamiteTests`, `ShopCounterTests`, `UpgradesTests`, `LampOilTests`, `BagWeightTests`, `SaveFileTests`, `SaveSystemTests` (temp folders), `TitleMenuTests`, `InventorySystemTests`, `DeepCampTests`, `GasTests`, `DarknessTests`, `ProgressTests` |

Hard dependency: **Windows only.** `ConsoleHandler` and `InputHandler` call `kernel32.dll`/`user32.dll` directly
(`GetStdHandle`, `WriteConsoleOutputW`, `SetCurrentConsoleFontEx`, `Get`/`SetConsoleScreenBufferInfoEx` (palette), `GetAsyncKeyState`…). No other platform
implementation exists; macOS support is GitHub issue 7, open.

Games that reference `ConsoleEngine.Scripting` also load the native Lua library `lua54.dll` from
`runtimes/<rid>/native/` in their output folder (shipped by the KeraLua package for win-x64/x86/arm/arm64, and
macOS/Linux). Besides KeraLua's own calls, `LuaNative.lua_tolstring` and `LuaNative.lua_pushlstring`
(`ConsoleEngine.Scripting/LowLevel/`) are direct P/Invokes into it.

Code that creates a `GlRenderer` (`ConsoleEngine.Rendering3D.OpenGL`) loads GLFW (`glfw3.dll` on Windows,
`libglfw.so.3` on Linux, from `runtimes/<rid>/native/`, shipped by `Ultz.Native.GLFW` through Silk.NET.Windowing) and
the system OpenGL driver (`opengl32.dll` on Windows). It needs an OpenGL 3.3 core context. There are no P/Invokes in
the repo for these: Silk.NET makes all the calls.

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
| Launcher → game process | spawn | `Process.Start` of `%SystemRoot%System32nhost.exe cmd.exe /c ".<ProjectName>.exe || pause"` with `UseShellExecute = true` and `WorkingDirectory` = the exe's folder, so the game gets a **classic console window** even when Windows Terminal is the default terminal. The launcher waits for conhost to exit (`WaitForExit`), so one game runs at a time. Exe path: `Examples/<ProjectName>/<launcher's output path, e.g. bin/Debug/net11.0>/<ProjectName>.exe`. **On macOS and Linux** there is no conhost: the apphost has no extension (`<ProjectName>`) and is started directly (`UseShellExecute = false`, same working directory), inheriting the launcher's terminal | `GameRunner.Run`, `GameRunner.BuildArguments`, `GameLocator.GetExecutablePath` |
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
| `PhysicsSandboxGame` | 100 × 60 | 10×10 Consolas (square cells, so circles look round) | `Name = "Physics Sandbox"`, `ShowFps = true` |
| `PhysicsShowcaseGame` | 120 × 60 | 10×10 Consolas (square cells) | `Name = "Physics Showcase"`, `ShowFps = true` |
| `EternalDiggerGame` | 120 × 48 (`Layout.ScreenWidth` × `Layout.ScreenHeight`) | 8×16 Consolas | `Name = "EternalDigger"`. First command-line argument = seed for a NEW MINE (default `Environment.TickCount`); CONTINUE uses the saved seed. Reads and writes `<LocalApplicationData>/EternalDigger/save.dat` (+ `.tmp`, `.bad`). No logger, no FPS |
| `ScriptedGame` | 80 × 40 | 12×12 Consolas | `ShowFps = true` |
| `KartsterGame` | 320 × 180 | 3×3 Consolas (square cells) | `Name = "Kartster"`, renderer: CPU `Rasterizer` by default, OpenGL `GlRenderer` with `--gpu` or G (falls back to CPU), `Console.PixelMode = true`, `Console.Palette = ScenePalette.Colors` (its own 16 colours), `ClearScreenOnEachFrame = false`. First command-line argument = folder of kart folders (default `Assets/Karts`), second = folder of tracks (default `Assets/Tracks`, optional: without a track it starts in the garage). Near 0.2, far 400. `TrackScene`: `FixedTimestep(1/120 s, 8)`, `PhysicsSystem` with `SubstepCount = 1`, `EnableSleep = false`. Title every 250 ms: on the track kart, track, speed, gear, rpm and FPS; in the garage kart, triangle count and FPS |

`GameBase(width, height, fontWidth, fontHeight)` always uses face `"Consolas"`.

<a id="environments"></a>
## Environments

There is no dev/staging/prod split. What changes behaviour:

| Dimension | Values | Effect |
|---|---|---|
| OS | Windows / other | Other OS: fails at first P/Invoke (`ConsoleHandler` ctor calls `GetStdHandle`). See [P1](05-triage.md#p1) |
| Console host **[external]** | classic `conhost` / Windows Terminal | The resize and font calls (`SetConsoleWindowInfo`, `SetConsoleScreenBufferSize`, `SetCurrentConsoleFontEx`) target the classic console. Under Windows Terminal (the default host on Windows 11) they are generally ignored, so the size and font are wrong. Return values are not checked, so nothing reports this. `start-conhost.cmd` (in every example's output folder) and `run-game.ps1` always start the game in `conhost.exe`. See [P2](05-triage.md#p2) |
| Working directory | game's output folder / elsewhere | TerraForM maps and the logger exe are found by relative path. See [P1](05-triage.md#p1). Lua scripts are not: `LuaRuntime.Load` resolves relative paths against the exe's folder (`AppContext.BaseDirectory`) |
| Machine speed (FPS) | any | The loop is uncapped (no sleep in `GameBase.Start`), and TerraForM physics/power depend on FPS. See [03 § Frame-rate dependence](03-rules.md#fps) |
| Build config | `Debug` / `Release` | No behavioural difference in code. No `#if DEBUG` exists |

## CI

`.github/workflows/build-and-test.yml`: on push/PR to `main`, on `windows-latest`, `macos-latest` and `ubuntu-latest` (matrix, `fail-fast: false`), .NET 11.0.x,
`dotnet restore` → `dotnet build --no-restore` → `dotnet test --no-build` with `--settings coverage.runsettings
--collect:"XPlat Code Coverage"` → `dotnet reportgenerator` (local tool from `dotnet-tools.json`). The Markdown summary
goes to the job summary, and the HTML report is uploaded as the `coverage-report-<os>` artifact. The `Gpu` tests (`ConsoleEngine.Rendering3D.OpenGL.Tests`) are ignored there (no OpenGL 3.3, or not Windows), which does not fail the job. There is no coverage
threshold, so coverage never fails the build. All projects set `TreatWarningsAsErrors`.
