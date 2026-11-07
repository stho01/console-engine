# 04 — Flows, hop by hop

**No step in any flow writes a log line** ([06](06-diagnostics.md#no-logs)). The "Observable" column is what the user
sees instead: an unhandled exception (the process dies), a silent no-op, or a visible screen state. "Silent" means
the code continues with no signal at all.

<a id="f1"></a>
## F1 — Startup (`Program.cs` → `GameBase` ctor → `Initialize()` → `Start()`)

| # | Step | Class.method | Stops / skips if | Observable |
|---|---|---|---|---|
| 1 | Construct game. Creates `ConsoleHandler(width, height, fontInfo)`, which calls `GetStdHandle(StdHandle.OutputHandle)` | `GameBase` ctor, `ConsoleHandler` ctor | Not Windows (no `kernel32.dll`) | Unhandled `DllNotFoundException`, process exits **[external]** |
| 2 | `RenderConsole` ctor allocates the buffer and **sets font Consolas 14×14** via `SetFont` | `RenderConsole` ctor | `SetCurrentConsoleFontEx` fails: return value ignored | Silent |
| 3 | `TerraformGame` ctor etc. set `Console.ClearColor` | game ctor | — | — |
| 4 | If `EnableLogger`: `Log.Start(this)`. First touch of `Log` runs its static ctor (`Dns.GetHostEntry("localhost")`) | `GameBase.Initialize` → `Log.Start` | see [F4](#f4) | Exceptions from F4 abort startup |
| 5 | `OnInitialize()`: game-specific setup. TerraForM: `Scenes.Set<InputNameScene>()`, `Camera = new Camera(this)` | game | Game code throws | Unhandled exception |
| 6 | `SetTitle(Name)` | `RenderConsole.SetTitle` → `SetConsoleTitle` | fails: ignored | Silent: title stays as before |
| 7 | `_console.Initialize()`: shrink window to 1×1 → `SetConsoleScreenBufferSize(width,height)` → `SetConsoleActiveScreenBuffer` → `SetFont(game font)` → `SetConsoleWindowInfo(0,0,width-1,height-1)` | `ConsoleHandler.InitializeConsole` | **Any call fails: every return value is ignored.** Typical causes **[external]**: window larger than the screen allows at that font size, or a host that doesn't support these calls (Windows Terminal) | Silent. Wrong window size, scrollbars, wrapped/garbled picture ([P2](05-triage.md#p2)) |
| 8 | If `!Resizeable` (default): remove Maximize/Size from the system menu | `ConsoleHandler.Resizable(false)` | `GetConsoleWindow()` returns `IntPtr.Zero` → `return` | Silent: window stays resizable |
| 9 | `SetCursorVisible(!HideCursor)` | `ConsoleHandler.SetCursorVisible` | fails: ignored | Silent: blinking cursor visible |
| 10 | If `ShowFps`: `GameTime.SetInterval(100, () => Log.ReportFps(GameTime.Fps))` | `GameBase.Initialize` | — | FPS reaches the logger **only if** `EnableLogger` ([P4](05-triage.md#p4)) |
| 11 | `Start()`: `Thread.Sleep(100)`, then loop until `_running = false` ([F2](#f2)) | `GameBase.Start` | — | — |

Note: `OnInitialize` (step 5) runs **before** the window is sized (step 7). Scene `OnLoad`s triggered there already see
the final `Width`/`Height`, because those come from the ctor arguments.

<a id="f2"></a>
## F2 — One frame (`GameBase.Start` loop body)

| # | Step | Class.method | Stops / skips if | Observable |
|---|---|---|---|---|
| 1 | Update clock, fire due intervals | `GameTime.Update` | Interval has `Pause = true` → skipped | Callback doesn't run |
| 2 | Poll 256 keys | `Input.Instance.Update` → `InputHandler.Update` | — | See [03 § Keys](03-rules.md#keys) |
| 3 | Clear buffer to `ClearColor` | `RenderConsole.Clear` | `ClearScreenOnEachFrame == false` → skipped | Old characters persist (Snake by design) |
| 4 | Update top scene | `SceneManager.Update` → `Scene.Update` | No scene → skipped. Top scene `IsPaused` → `return` | Nothing moves |
| 5 | Game `OnUpdate` | game | — | — |
| 6 | Render top scene | `SceneManager.Render` → `Scene.Render` | No scene → skipped. `IsPaused` → `return` | Scene not drawn |
| 7 | Game `OnRender` | game | — | — |
| 8 | Write buffer to console | `ConsoleHandler.Render` → `WriteConsoleOutput` | fails: ignored | Silent: screen not updated |
| 9 | Unload queued scenes, `GC.Collect()` | `SceneManager.Cleanup` | Queue empty → `return` | — |

<a id="f3"></a>
## F3 — Shutdown

| # | Step | Class.method | Note |
|---|---|---|---|
| 1 | Game calls `Stop()` on `ESC`: `ConsoleGame`, `ConsoleGame.Ecs`, `Swarm`, `EternalDigger` (after `MineScene.Save`) | `GameBase.Stop` | Other games have no quit path: the user closes the window. EternalDigger: closing the window without `ESC` loses progress since the last save |
| 2 | `_running = false`, then `_console.Close()` | `RenderConsole.Close` | **Calls `Environment.Exit(0)`: the process ends here** |
| 3 | `if (EnableLogger) Log.Stop()` | `GameBase.Stop` | **Never reached** (step 2 exited). The logger socket isn't closed by `Log.Stop` |

`ConsoleHandler.Close()` is an empty method and is not called.

<a id="f4"></a>
## F4 — Logger (`Log`, game side; `ConsoleEngine.Logger/Program.cs`, logger side)

Game side:

| # | Step | Stops / skips if | Observable |
|---|---|---|---|
| 1 | Static ctor: `Dns.GetHostEntry("localhost").AddressList[0]`, endpoint port **11000**, TCP socket | DNS fails | `TypeInitializationException` on first `Log` use |
| 2 | `Log.Start`: `Socket.Bind(EndPoint)` | Port 11000 already in use (a second game instance, or a previous one still alive) | Unhandled `SocketException`. **The game crashes at startup** ([P1](05-triage.md#p1)) |
| 3 | `Socket.Listen(10)`, start background thread `"Logger"` (`DispatchMessages`) | — | — |
| 4 | `Process.Start("ConsoleEngine.Logger.exe")` with `UseShellExecute = true` | Exe not found by the shell (relative name, resolved via the working directory / PATH **[external]**) | Unhandled `Win32Exception`. **The game crashes at startup** |
| 5 | `DispatchMessages` loop: if not connected, **block** in `Socket.Accept()`. Then send all queued messages. `Thread.Sleep(100)`. Repeat | Logger never connects → blocks forever. Messages keep queuing in memory | Silent. Game runs normally |
| 6 | `EnqueueMessage` (from `Log.WriteLine` / `Log.ReportFps`) | `!(_game?.EnableLogger ?? false)` → `return` | Silent drop |

Logger side (`ConsoleEngine.Logger.Program.Main`):

| # | Step | Stops / skips if | Observable |
|---|---|---|---|
| 1 | Print `Logger up and running...` | — | First line in the logger window |
| 2 | `Connect` to `localhost:11000` (same address resolution as the game) | Game not listening | `SocketException` → `Environment.Exit(0)`. **The logger window closes silently** |
| 3 | Receive into 1024-byte chunks until the text contains `'\0'`. Split on `'\0'` | Text after the last `'\0'` in that read is **discarded** | Log lines occasionally lost or cut ([P4](05-triage.md#p4)) |
| 4 | Lines starting `#<FPS>:` → window title `Game FPS: {n}`. Others → `Console.WriteLine` | — | — |
| 5 | Any other exception → printed, then `Thread.Sleep(10000)` + `Console.ReadKey()` | — | Exception text shown in the logger window, which stays open |

<a id="f5"></a>
## F5 — Scene transitions

See [03 § Scenes](03-rules.md#scenes). TerraForM's scene graph:

| From | Trigger | Call | To |
|---|---|---|---|
| start | `OnInitialize` | `Scenes.Set<InputNameScene>()` | `InputNameScene` |
| `InputNameScene` | `ENTER` Pressed | `Game.Playername = …; Scenes.Set<MenuScene>()` | `MenuScene` (only scene) |
| `MenuScene` | `H` Pressed, `Scenes.Count == 1` | `Scenes.Set(new GameScene($"map{Game.CurrentMap}"))` | `GameScene` ([F6](#f6)) |
| `GameScene` | `ESC` Held | `Scenes.Push<MenuScene>()` | `MenuScene` over paused level |
| `MenuScene` (pushed) | `H` Pressed, `Count > 1` | `Scenes.Pop()` | back to level |
| `GameScene` | `R` Pressed | `Scenes.Set(new GameScene(_worldName))` | same map, fresh |
| `GameScene` | rover touches `F` | `RoverOnOnFinishReached` → `TerraformGame.RotateMap()` → `Scenes.Set(new GameScene(...))` | next map ([03 § Map rotation](03-rules.md#map-rotation)) |

<a id="f6"></a>
## F6 — TerraForM level load (`GameScene.OnLoad` → `WorldLoader`)

| # | Step | Class.method | Stops if | Observable |
|---|---|---|---|---|
| 1 | `new GameScene(worldName)` | ctor | `worldName == null` | `ArgumentNullException` (`worldName`) |
| 2 | `Camera.Position = Vector2.Zero` | `GameScene.OnLoad` | — | — |
| 3 | `File.ReadAllLines("Assets/Maps/{worldName}.txt")`, **relative to the working directory** | `WorldLoader.LoadWorld` | Working directory isn't the game's output folder, or the file is missing | `DirectoryNotFoundException` / `FileNotFoundException`, crash when the level starts (after pressing `H`) |
| 4 | Header line without `:` | `WorldLoader.Parse` (`keyValue[1]`) | — | `IndexOutOfRangeException` |
| 5 | `!power`/`!sequences`/`!width`/`!height` value not an integer | `int.Parse` | — | `FormatException` |
| 6 | Grid rows **before** both `!width` and `!height` | `Parse` (`tiles != null` guard) | — | **Silent:** those rows are dropped |
| 7 | Header line **after** grid rows (once width and height are set) | `Parse` reallocates `tiles` on every `!` line | — | **Silent:** earlier rows wiped |
| 8 | Row longer than `!width`, or more rows than `!height` | `tiles[x, y] = tile` | — | `IndexOutOfRangeException` |
| 9 | No `!width`/`!height` at all | `new World(game, name, tiles)` with `tiles == null` → `map.GetLength(0)` | — | `NullReferenceException` |
| 10 | `World.Init()`: draw tiles, build rock border | `World.Init` | — | — |
| 11 | `new Rover(this) { Position = World.StartingPoint.Position }` | `GameScene.OnLoad` | No `S` in the grid | `NullReferenceException` |
| 12 | Subscribe `OnFinishReached`, `OnAthmosphereGeneratorsPlanted`. `new Hud(this)`. `Camera.Follow(Rover)` | — | — | Level visible |

<a id="f7"></a>
## F7 — TerraForM one level frame (`GameScene.OnUpdate`, then `OnRender`)

| # | Step | Stops / skips if | Observable |
|---|---|---|---|
| 1 | `GameOver = Rover.PowerDepleted() \|\| Rover.RemainingSequences < 0` | — | HUD swaps to the game-over sprite ([03 § Game over](03-rules.md#game-over)) |
| 2 | `Hud.OnUpdate`: arrows/`P` Pressed → append command | — | `QUEUED COMMANDS …` line |
| 3 | `ESC` Held → `Push<MenuScene>()` | — | Menu |
| 4 | `HandleDebugInput` | `!Game.IsDebugMode` → `return` (always, see [02](02-state-model.md)) | WASD do nothing |
| 5 | `SPACE` Pressed **and** `!_currentCommands.Any()` → `CommitCommands` | Commands still running → SPACE ignored. Hud queue empty → no decrement, nothing queued | — |
| 6 | `UpdateCurrentCommand`: run head command, dequeue when done | Queue empty → `return` | — |
| 7 | `Rover.Update`: drag, accel, move, `HandleCollision` (Rock/Craves/PlantSpot/BonusPoint/FinishPoint) | — | Finish → score + `RotateMap` ([03 § Score](03-rules.md#score)) |
| 8 | `PlantEmitters.ForEach(Update)` (grass animation, `LifeTime = 3000` ms) | — | — |
| 9 | `R` Pressed → restart same map | — | — |
| 10 | Render: `World.Draw`, emitters, `Rover.Draw`, `Hud.Draw` | `Hud.Draw`: if `scene.GameOver` → draw `Sprites.GameOver` and `return` (no HUD, no score) | — |
| 11 | `RenderDebugInfo` | `!Game.IsDebugMode` → `return` | — |

`Rover.ApplyForce`: after deducting cost, `if (PowerDepleted()) return;` means no acceleration. `Rover.Plant`: needs
`StandingOnPlantingSpot && World.Intersects(...)`, otherwise nothing. `Rover.MoveNorth`: `if (!_scene.World.Intersects(this, out var with)) return;`.

<a id="f8"></a>
## F8 — ECS structural change (`World.Add<T>` / `Remove<T>` / `Destroy`)

| # | Step | Class.method | Stops / skips if | Observable |
|---|---|---|---|---|
| 0 | World disposed, or a query on this world is being iterated (`_iterationDepth != 0`) | `World.ThrowIfStructuralChangeNotAllowed` | Either | `ObjectDisposedException` / `InvalidOperationException` "Cannot create, destroy, add or remove while a query is being iterated…" |
| 1 | Look up the entity's record. Id out of range, or `Version` differs | `World.GetAliveRecord` → `ThrowNotAliveOrDisposed` | Not alive | `ObjectDisposedException` if the world is disposed, otherwise `InvalidOperationException` "… is not alive." |
| 2 | `Add`: component already in the source mask. `Remove`: not in it | `World.AddComponent` / `RemoveComponent` | Present / missing | `InvalidOperationException` ([06](06-diagnostics.md)) |
| 3 | Find the target archetype through the cached edge (array indexed by component id), or create it once (`GetOrCreateArchetype`) | `World.GetAddTarget`, `Archetype.GetAddEdge` / `GetRemoveEdge` | — | — |
| 4 | Append a row in the target. A new chunk comes from `ChunkPool` (pooled first, otherwise `NativeMemory.AlignedAlloc`) | `Archetype.Allocate` | — | — |
| 5 | Copy the components both archetypes share | `Archetype.CopyShared` | — | — |
| 6 | Remove the old row: the archetype's last row is copied into it (swap-back), and the moved entity's record is repointed. An emptied last chunk goes back to the pool | `World.RemoveFromArchetype` → `Archetype.RemoveAt` | — | Earlier `ref`s from `Get<T>` in that archetype now point at other data |
| 7 | `Add`: write the new value (skipped for tags). `Destroy` instead does step 6 only, then bumps `Version` and frees the id (or retires it at `int.MaxValue`) | `World.Write`, `World.Destroy` | — | — |

<a id="f9"></a>
## F9 — ECS frame in an `EcsScene` (`SceneManager.Update` → `Simulation`, `SceneManager.Render` → `Presentation`)

Runs inside [F2](#f2) steps "Scenes.Update" and "Scenes.Render". Both are skipped while the scene is paused.

| # | Step | Class.method | Stops / skips if | Observable |
|---|---|---|---|---|
| 1 | `Simulation.Update((float)GameTime.Delta.TotalSeconds)`: each enabled system's `OnUpdate`, in `SystemGroup.RunOrder` (`Add` order adjusted by `UpdateBefore`/`UpdateAfter`) | `EcsScene.OnUpdate` → `SystemGroup.Update` | `EntitySystem.Enabled == false` → that system is skipped | — |
| 2 | Systems iterate queries and record structural changes in `Commands` | game systems, `MovementSystem` | Direct `World.Create/Destroy/Add/Remove` inside the loop | `InvalidOperationException` ([F8](#f8) step 0) |
| 3 | Sync point: `Commands.Playback(World)` applies the recorded changes in order (deferred handles from `Create` are swapped for the created entities), then clears the buffer | `CommandBuffer.Playback` | Target entity no longer alive → that `Destroy`/`Add`/`Remove` is **skipped silently**. Foreign or stale deferred handle → `InvalidOperationException` | Entities created/destroyed this frame show up in the render below |
| 4 | `Presentation.Update(delta)` runs the render systems into the frame buffer, which `GameBase.Start` cleared at the start of the frame (if `ClearScreenOnEachFrame`) | `EcsScene.OnRender` → `SystemGroup.Update` | `OutlineRenderSystem`: bounding circle off-screen → entity skipped | Drawn cells |
| 5 | `Presentation` plays back its own `Commands` (normally empty) | `CommandBuffer.Playback` | — | — |
| 6 | On scene unload: `OnDestroy`, then `World.Dispose()` frees all chunks | `EcsScene.OnUnload` | — | Old entity handles stop being alive |

<a id="f10"></a>
## F10 — Launcher (`Examples/Launcher/Program.cs` → `LauncherApp.Run`)

| # | Step | Class.method | Stops / skips if | Observable |
|---|---|---|---|---|
| 1 | Find `Launcher.csproj` above `AppContext.BaseDirectory`; its parent is `Examples/`, the path between is the output path | `GameLocator.FromLauncherOutput` | No `Launcher.csproj` above the exe (exe copied out of the repo) → `InvalidOperationException` | Message on stderr, exit code 1, no menu |
| 2 | Compute each game's exe path | `LauncherApp` ctor, `GameLocator.GetExecutablePath` | — | — |
| 3 | Loop: check which exes exist, draw, `Console.ReadKey` | `LauncherApp.Run`, `RefreshBuilt`, `LauncherScreen.Draw` | Esc/Q → loop ends, screen cleared | Menu; unbuilt games show `(not built)` |
| 4 | Enter/Space/digit → `Launch(index)` | `LauncherMenu.HandleKey`, `LauncherApp.Launch` | Exe missing → status `LauncherTexts.NotBuilt`, nothing started | Yellow status line |
| 5 | Start the exe through `conhost.exe` + `cmd.exe` in a new classic console window, working directory = exe folder, and wait | `GameRunner.Run` | `Process.Start` fails → `Win32Exception` caught | Status `LauncherTexts.StartFailed` |
| 6 | Window closes → status `Closed`, discard buffered keys, back to step 3. A crashing game first stops at `pause` | `DiscardBufferedKeys` | — | `LauncherTexts.Closed` |
<a id="f11"></a>
## F11 — EternalDigger miner frame (`MinerControlSystem` → `InteractionSystem` → `MinerMotionSystem`)

Runs in [F9](#f9) step 1, in this order. The keyboard is read once into a `MinerInput` (`MinerInput.FromKeyboard`).

| # | Step | Class.method | Stops / skips if | Observable |
|---|---|---|---|---|
| 1 | Rescue: `R` held adds to `RescueHold`; at 1.5 s the miner jumps to the spawn and the bag empties | `MinerControlSystem.UpdateRescue` | `R` not held → `RescueHold = 0`, continue. Rescue done → rest of the frame's control skipped | HUD rescue bar, pop at the spawn |
| 2 | — | `MinerControlSystem.Control` | `Miner.IsMoving` → no keys read this frame. Riding: car moving → nothing; Up (not at top) / Down (at top) → send the car, rest skipped; Left/Right or Down at the bottom → get out and go on; anything else → rest skipped | — |
| 3 | Fall one tile if unsupported | same | `Mine.IsSupported` false → fall starts, keys ignored | Miner drops |
| 3b | Hold `E` to plant dynamite | `UpdatePlanting` | `E` not held, on the surface or a lift landing → hold reset, go on. Holding (< 0.5 s) → rest of the frame skipped. At 0.5 s: no kit, no sticks, or no rock in the faced tile → dust puff; else a `Dynamite` entity is recorded. After that, `E` must be released | Fuse bar in the HUD, then the stick `¡` |
| 4 | `SPACE`: ladder | `PlaceLadder` → `Mine.TryPlaceLadder` | `Ladders == 0`, row ≤ 10, tile not air or already has a ladder → nothing placed (one dust puff) | Ladder drawn, HUD count −1 |
| 5 | Side key, else down, else up (only on a ladder): walk/climb into air, or dig | `MoveOrDig` | Target unbreakable (grass, bedrock, outside the mine) → nothing. `DigCooldown > 0` → no hit this frame | Debris, cracks, burst on break, ore +1 in the HUD bag |
| 6 | At the counter `↑`/`↓` move the selection; `E` (pressed): deliver, buy the selected item, build a lift segment, or get in / call the lift | `InteractionSystem.Interact` | `E` not pressed this frame, miner moving or riding, or not on a spot (row 9 at columns 3–6 / 8–11 / 23–24) or a landing in column 21 → nothing. Empty bag / not enough gold → dust puff only | Coins, `+<n>g` / `-10g` / `ELEVATOR +10m` |
| 7 | Slide `Position`, finish the move, landing dust | `MinerMotionSystem.Move` | — | — |
| 8 | Move the car; carry the rider; a called car picks up the miner waiting on its landing | `ElevatorSystem` → `Elevator.Update`, `Carry` | Not riding and (car didn't arrive this frame, not called, or the miner isn't idle on that landing) → nothing | Car and miner move together |
| 9 | Burn fuses; at 0 blast the 3 × 3, push the miner, chain | `DynamiteSystem` | Fuse > 0 → only sparks. No miner entity → nothing burns | Flash, rubble, shake |
| 10 | Burn oil underground; set the headlamp's reach, flicker and low/empty state; smoke when low | `LampSystem` | On the surface → no burn | HUD oil gauge, shorter / flickering cone |

<a id="f12"></a>
## F12 — EternalDigger start, save and load (`TitleScene` → `MineScene` → `SaveSystem`)

| # | Step | Class.method | Stops / skips if | Observable |
|---|---|---|---|---|
| 1 | `OnInitialize` sets the title scene | `EternalDiggerGame.OnInitialize` | — | Title screen |
| 2 | Load the save once | `TitleScene.OnLoad` → `SaveFile.Load` | No file → `Missing`, only NEW MINE. Bad magic, early end, trailing bytes, out-of-range value → `Corrupt`; version ≠ 1 → `Unsupported`; I/O error → `Corrupt`. Not `Loaded` → file moved to `save.dat.bad` (if that fails, silently left in place) | Red message, only NEW MINE |
| 3 | `E` on CONTINUE | `TitleMenu.Select` → `Scenes.Set(new MineScene(save, path))` | — | Saved mine |
| 4 | `E` on NEW MINE | `TitleMenu.Select` | With a save: switch to the confirmation, nothing starts until `YES, START OVER` | Confirmation |
| 5 | New mine: write its first save, then start | `TitleScene.StartNewMine` → `SaveFile.Save` | I/O error is **ignored** (the mine still starts; the next save retries) | — |
| 6 | Sale (earned > 0) or purchase → `GameSaver.Request` | `InteractionSystem.Interact` | Empty bag, not enough gold → no request | — |
| 7 | Save when requested or after 60 s underground | `SaveSystem.OnUpdate` → `GameSaver.Save` → `SaveData.Capture` + `SaveFile.Save` | No request and timer not reached → nothing. `IOException`/`UnauthorizedAccessException` → caught, `LastSaveFailed = true` | ` SAVED ` / ` SAVE FAILED` |
| 8 | `ESC` | `EternalDiggerGame.OnUpdate` → `MineScene.Save` → `Stop()` | On the title: no save | Process exits |

