# 06 — Diagnostics: where the evidence is

<a id="no-logs"></a>
## There are no logs

- The engine and the example games **never write a log line**. No `ILogger`, no file, no level, no event log entry of
  their own. The only callers of `Log.WriteLine` are `ConsoleEngine.Scripting` (Lua errors and Lua `print`) and
  the Scripted demo's `ScriptReloadSystem`, and they reach the logger window only with `EnableLogger = true`, which
  no game that uses Lua sets.
- Consequence: **absence of output is never evidence.** Every success path and almost every failure path is silent.
  Win32 return values are all ignored ([04 § F1](04-flows.md#f1)).
- Evidence sources, best first:

| Source | What it shows | When available |
|---|---|---|
| Unhandled-exception text from the .NET runtime in the console | Exception type, message and stack. The only failure record | Only if the console stays open ([§ Capture](#capture)) |
| Windows Event Viewer → Application log, ".NET Runtime" / "Application Error" entries **[external]** | Same exception after the window closed | Usually, for unhandled crashes |
| Logger window (`ConsoleEngine.Logger.exe`) | `Log.WriteLine` text and title `Game FPS: n` | Only with `EnableLogger = true` ([04 § F4](04-flows.md#f4)) |
| On-screen state | HUD values, which scene is showing, debug overlays | Always (screenshot) |
| Debug overlays | Platformer: `POS:`, `VEL:`, `AIR:`, bounds and tile probes (F1). Platformer.Ecs: `POS:`, `VEL:`, `AIR:`, `HIT:` (the `SlideCollisions` of the last move, e.g. `Floor, Wall`, or `None`) (F1). TerraForM: `Pos  :`, `SPos :`, `BB   :`, `NPos :`, `Bonus:`, `Plant:`, but `IsDebugMode` can't be turned on without code | Platformer only |

<a id="capture"></a>
## Capturing a crash

A double-clicked exe gets its own console window, which closes the moment the process dies, taking the exception
text with it. Ask the user to:

1. Open a terminal (classic console preferred, [P2](05-triage.md#p2)).
2. `cd` to the game's output folder (the one holding the exe, and for TerraForM `Assets\Maps`).
3. Run the exe by name and copy the text it prints after the crash.

The game takes over the window (it resizes the buffer), but the runtime's exception text is written to it after the
loop dies. If it is unreadable, use Event Viewer **[external]**.

## Every user-visible string the code emits (verbatim)

Grep these to locate the code path. None of them has a log level.

### Engine / logger

| Text | Emitted by | Meaning |
|---|---|---|
| `Logger up and running...` | `ConsoleEngine.Logger.Program.Main`, first line | Logger exe started. Says nothing about whether it connected |
| `Game FPS: ` + n (window title) | `ConsoleEngine.Logger.Program.Main` on a `#<FPS>:` message | `ShowFps` **and** `EnableLogger` are on and connected |
| `#<FPS>:` + n | `Log.ReportFps` (wire format, never shown) | — |
| `Input handler not set` | `Input.GetKey` (`InvalidOperationException`) | Unreachable in practice: `Handler` is a static readonly `new InputHandler()` |
| `Empty deque` | `Deque<T>.PopFront` / `PopBack` (`InvalidOperationException`) | Game popped an empty `Deque` |
| `A palette needs exactly 16 colours.` (`ArgumentException`, param `value`; `ConsoleEngine.Rendering3D.ConsolePalette` ctor: param `colors`) | `RenderConsole.Palette` setter | A palette with another number of colours |
| `no go` | `Move.OnUpdate` / `GetVisualRepresentation` (`Exception`) | Invalid `Direction` value. Unreachable via the UI |
| `worldName` (param name of `ArgumentNullException`) | `GameScene` ctor | `GameScene` created with null name |
| `No game exe found next to start-conhost.cmd.` | `Examples/start-conhost.cmd` | The launcher was run outside a game's output folder, or the folder holds only `ConsoleEngine.Logger.exe` |
| Window title = `GameBase.Name` | `GameBase.Initialize` → `SetTitle` | Default `"Game"`. Platformer `"Platformer game"` |
| `Entity(<id>v<version>) is not alive.` | `World` (`EcsThrowHelper.ThrowNotAlive`, `InvalidOperationException`) | Entity destroyed, stale handle or `Entity.Null`. After `World.Dispose()` the same calls throw `ObjectDisposedException` instead ([03 § ECS](03-rules.md#ecs)) |
| `Entity(<id>v<version>) has no <Type> component.` | `World.Get`/`Set`/`Remove` (`InvalidOperationException`) | Component missing |
| `Entity(<id>v<version>) already has a <Type> component.` | `World.Add` (`InvalidOperationException`) | Use `Set` to overwrite |
| `<Type> is a tag component and has no data.` | `World.Get`/`Set`, `EntityChunk.GetSpan` (`InvalidOperationException`) | `Get` on a field-less struct |
| `Cannot create, destroy, add or remove while a query is being iterated. Use a CommandBuffer.` | `World.Create`/`Destroy`/`Add`/`Remove`, `CommandBuffer.Playback` (`InvalidOperationException`) | Structural change inside a `foreach` over `Query.GetChunks()` or a `ForEach` job ([03 § ECS](03-rules.md#ecs)) |
| `Entity(<negative id>v<generation>) is a deferred handle from another CommandBuffer or from before its last playback.` | `CommandBuffer.Playback` (`InvalidOperationException`) | A handle from `CommandBuffer.Create` was used with a different buffer, or kept past `Playback`/`Clear` |
| `The UpdateBefore/UpdateAfter attributes of these systems form a cycle: <System>, <System>….` | `SystemGroup.Update` / `RunOrder` (`InvalidOperationException`) | Ordering attributes contradict each other. The list names every system that couldn't be scheduled |
| `Must be a positive, finite number.` (`ArgumentOutOfRangeException`, param `mass` or `cellSize`) | `RigidBody.Dynamic`, `PhysicsSystem` ctor (via `SpatialHashGrid`) | Mass or broadphase cell size ≤ 0, NaN or infinite |
| `Must be positive and finite.` (`ArgumentOutOfRangeException`, param `tileSize`); zero/negative `columns`/`rows` → .NET's own `ThrowIfNegativeOrZero` message | `TileGrid` ctor | A tile size ≤ 0, NaN or infinite, or an empty grid |
| .NET's own `ThrowIfGreaterThanOrEqual` message (`ArgumentOutOfRangeException`, param `column` or `row`) | `TileGrid.SetSolid` | A tile outside the grid |
| `ArgumentNullException` (param `physics`) | `CharacterMover.MoveAndSlide` | Called without a physics system |
| `A polygon needs at least 3 points.` / `The points don't span an area.` / `The hull has <n> corners; at most 8 are supported.` (`ArgumentException`, param `points`) | `PolygonLibrary.Add` (via `ConvexPolygon.Hull`) | Too few points, all on one line, or too many corners after the convex hull |
| .NET's own `ThrowIfNegativeOrZero` message (`ArgumentOutOfRangeException`, param `value`) | `PhysicsSystem.SubstepCount` setter | `SubstepCount` set to 0 or less |
| `Must be a finite number.` (param `stepSeconds`, `ArgumentOutOfRangeException`); zero/negative `stepSeconds` or `maxStepsPerFrame` → .NET's own `ThrowIfNegativeOrZero` message | `FixedTimestep` ctor | A scene replaced `FixedTimestep` in `OnCreate` with a step that is NaN, infinite, ≤ 0, or a cap ≤ 0 |
| `<Type> is not in this chunk. Add it to the query with With<<Type>>().` | `EntityChunk.GetSpan` (`InvalidOperationException`) | A system read a component its query doesn't require |
| `Component type <Type> is listed more than once.` | `World.Create` (`ArgumentException`) | Same type twice in one `Create` |
| `Cannot register <Type>: the ECS supports at most 256 component types.` | `ComponentRegistry.Register` (`InvalidOperationException`, wrapped in `TypeInitializationException`) | 257th component type used in the process |
| `An archetype row needs <n> bytes and does not fit in one 16384-byte chunk.` | `Archetype` ctor (`InvalidOperationException`) | A component set too large for a chunk |
| Window title = `GameBase.Name` | `GameBase.Initialize` → `SetTitle` | Default `"Game"`. Platformer `"Platformer game"`, Asteroids.Ecs `"Asteroids (ECS)"`, Snake.Ecs `"Snake (ECS)"`, TerraForM.Ecs `"TerraForM (ECS)"`, Platformer.Ecs `"Platformer (ECS)"`, Swarm `"Swarm (ECS)"`, PhysicsSandbox `"Physics Sandbox"`, PhysicsShowcase `"Physics Showcase"`, EternalDigger `"EternalDigger"` |

### Lua scripting (`ConsoleEngine.Scripting`)

All entity errors below are what `LuaScriptSystem.GetError(entity)` returns once the entity has the `ScriptFaulted`
tag.

| Text | Emitted by | Meaning |
|---|---|---|
| `Lua script '<name>': <message>` | `LuaScriptException` (thrown by `LuaRuntime.Load`, `LuaScript.Reload`; returned by `LuaRuntime.ReloadAll`) | Script could not be loaded. `<name>` is the path as passed to `Load` ([04 § F15](04-flows.md#f15)) |
| `the script must return a table` | `LuaScript.Reload` (inside `LuaScriptException`) | The file ran but did not `return` its module table |
| `<name>:<line>: <message>` | Lua itself (syntax and runtime errors), inside `LuaScriptException` or an entity error | Where the script failed. Names over 60 bytes are shortened to `...<end>` |
| `<name>:<line>: unknown field '<key>' on an entity, put script data in self.data` | `guard` in `LuaRuntime.EngineSource` (`__newindex` of every `self`) | Script wrote a field that is no component field |
| `<name>:<line>: self.<field> needs a <Component> component on the entity` | `guard` in `LuaRuntime.EngineSource` | Script wrote e.g. `self.vx` on an entity without `Velocity` |
| `self.x must be a number` (also `self.y`, `self.vx`, `self.vy`, `self.color`, `self.background`) | `LuaRuntime.ReadResults` | Hook stored a non-number in that field |
| `self.char must be a string or a code point` | `LuaRuntime.ReadResults` | Hook stored something else in `self.char` |
| `<ExceptionType>: <message>` | `LuaRuntime.Call` | A .NET exception inside `input.*`, `console.*` or `print` |
| `Lua: <error>` | `LuaScriptSystem.RunHook` (`Log.WriteLine`) | An entity faulted ([04 § F16](04-flows.md#f16)). Logger only |
| `Lua: <print arguments, tab-separated>` | Lua `print` (`LuaRuntime.Print`) | Script debug output. Logger only |
| The error text on the bottom row, white on dark red | `ScriptErrorSystem` | Scripted demo: first `ScriptFaulted` entity's error |

Runtime exceptions without repo-authored text (message comes from .NET and varies): `DllNotFoundException`,
`SocketException`, `Win32Exception`, `FileNotFoundException`, `DirectoryNotFoundException`, `FormatException`,
`IndexOutOfRangeException`, `NullReferenceException`, `TypeInitializationException`. Match by **type + stack frame**
using [04](04-flows.md).

### KartViewer

| Text | Emitted by | Meaning |
|---|---|---|
| `No kart folders in <full path>` | `KartViewerGame.OnInitialize` (`DirectoryNotFoundException`) | The karts folder (default `Assets/Karts`, relative to the working directory) has no sub-folders |
| `No kart parts (*.obj) in <folder>` | `KartModel.Load` (`InvalidDataException`) | A kart folder has no drawable OBJ parts |
| `<path>:<line>: a face needs at least three corners` | `ObjFile.Read` (`InvalidDataException`) | Broken OBJ file |
| `<id>.spline.obj needs at least two centerline points` | `Track.Load` (`InvalidDataException`) | Broken or empty track spline export |
| `KartViewer - <kart> on <track> - <n> km/h - gear <g> - <rpm> rpm - <fps> fps` (window title) | `KartViewerGame.OnInitialize` interval, every 250 ms | Running, track mode |
| `KartViewer - <kart> (<n> triangles) - <fps> fps` (window title) | `KartViewerGame.OnInitialize` interval, every 250 ms | Running, garage mode |

### Launcher (`LauncherTexts`, `GameLocator`)

`{0}` is the game title (`GameEntry.Title`, e.g. `Snake (ECS)`).

| Text | Emitted by | Meaning |
|---|---|---|
| `ConsoleEngine launcher` (window title) | `LauncherTexts.WindowTitle`, `LauncherApp.Run` | Launcher is running |
| `ConsoleEngine - game launcher` | `LauncherTexts.Heading` | Menu heading |
| `Up/Down select   Enter start   1-{0} start directly   Esc quit` | `LauncherTexts.KeyHelp` (`{0}` = number of entries, max 9) | Key help |
| `(not built)` | `LauncherTexts.NotBuiltTag` | That game's exe is missing ([P16](05-triage.md#p16)) |
| `Running {0}. Close its window to come back here.` | `LauncherTexts.Running` | Launcher is waiting for the game process |
| `{0} closed.` | `LauncherTexts.Closed` | The game window closed. Shown after every game, crash or not (`conhost.exe` exits with 0) |
| `{0} is not built: {1}. Run dotnet build.` | `LauncherTexts.NotBuilt` (`{1}` = expected exe path) | Started a game whose exe is missing |
| `Could not start {0}: {1}` | `LauncherTexts.StartFailed` (`{1}` = `Win32Exception` message) | `Process.Start` failed |
| `No Launcher.csproj found above <dir>. Run the launcher from its build output (dotnet run --project Examples/Launcher).` | `GameLocator.FromLauncherOutput` (`InvalidOperationException`, printed to stderr by `Program.cs`, exit code 1) | Launcher exe is not inside the repository's build output |

### EternalDigger screen text

| Text | Where | Meaning |
|---|---|---|
| ` ETERNAL DIGGER ` | `HudSystem.Title`, row 0 | Title |
| `  GOLD <n>g  ╟╢<ladders>[  LIFT <n>m][  ¡<dynamite>]  OIL ▮▮▮▮▮▯▯▯[ δ<flasks>]  DEPTH <n>m  BAG ◆<copper> ◆<silver> ◆<gold> ◆<gem> ✦<finds> = <value>g` (gem `◆` magenta, `✦` yellow, the bag value yellow when the bag is heavy, `╟╢` yellow, `¡` red; the gauge white, yellow below half, blinking red when low) | `HudSystem.DrawStatus`, row 0. `N0` formatting in the user's culture | Inventory and depth |
| ` ←→↓ walk/dig  ↑ climb  ↑+←→ jump  SPACE ladder  E use  hold E+arrow dynamite  TAB inventory  hold R rescue  ESC quit` | `HudSystem.KeyHelp`, row 1 | Default help line |
| ` SAVED ` (grey, then dark grey, 1.5 s) | `HudSystem.SavedText`, right end of row 1 | The game was just saved |
| ` SAVE FAILED` (red, 1.5 s) | `HudSystem.SaveFailedText`, right end of row 1 | The save file couldn't be written (`GameSaver.LastSaveFailed`); play goes on |
| `CONTINUE`, `NEW MINE` | `TitleMenu.ContinueText`, `NewMineText` (title screen) | CONTINUE only with a readable save |
| `Start over? The saved mine will be lost.`, `NO`, `YES, START OVER` | `TitleMenu.ConfirmQuestion`, `NoText`, `YesText` | NEW MINE picked while a save exists |
| `↑↓ choose   E select   ESC quit` | `TitleScene.KeyHelp` | Title screen help |
| `The saved mine could not be read. Starting a new mine.` (red) | `TitleScene.CorruptMessage` | `SaveStatus.Corrupt`: file moved to `save.dat.bad` |
| `The saved mine is from another version of the game. Starting a new mine.` (red) | `TitleScene.UnsupportedMessage` | `SaveStatus.Unsupported`: file moved to `save.dat.bad` |
| `ETERNAL DIGGER` block logo | `TitleScene.Logo` | Title screen |
| ` RESCUE ` + bar + ` the bag is lost` | `HudSystem.RescueLabel`, `RescueNote` | `R` is held |
| ` NO LADDERS LEFT!  Hold R to be rescued (the bag is lost) ` (blinking) | `HudSystem.NoLaddersWarning` | 0 ladders and below the surface |
| ` DELIVER [E] ` | `PromptRenderSystem.DeliverText` | Standing at the door |
| ` BUILD LIFT +10m [E] <price>g ` (red when short) | `PromptRenderSystem.BuildLiftText` | Standing at the lift sign |
| ` ↑ LADDER ↓ ` / ` ↑ BLASTING KIT ↓ ` / ` ↑ DYNAMITE ↓ `, (or ` ↑ FILL LAMP ↓ `, ` ↑ OIL FLASK ↓ `, ` ↑ DEEP CAMP ↓ ` with `AT 100m BY THE LIFT` / `BUILT`, ` ↑ SHOVEL ↓ ` / `PICKAXE` / `LAMP` / `BAG` / `DETECTOR`), then `HAVE <n>`, `OIL <n>%`, `UNLOCKS DYNAMITE` or tier pips `●●○`, then `<price>g [E]` (red when short), `FULL`, `MAX` or `SOLD OUT` | `PromptRenderSystem.DrawCounterPanel`, `Shop.LadderName`/`BlastingKitName`/`DynamiteName`, `KitUnlocksText`, `SoldOutText` | At the shop counter |
| ` INVENTORY ` / ` TAB close ` (black on yellow, panel top), `PAUSED` (panel bottom), `GOLD`, `OIL`, `FLASKS`, `DYNAMITE` (`no blasting kit`), `LIFT` (`not built`), `BAG` (`HEAVY`), `weight <w>/<capacity>`, `worth <n>g`, `UPGRADES` with pips, `CAMP` (`built at 100m` / `not built`), `RECORD <n>m`, `FOUND FOR GOOD   §<n> fossils   ¥<n> relics` | `InventorySystem` | `TAB` was pressed: the game is paused until `TAB` again |
| ` ↑ SELL BAG ↓ ` / `HOME PAYS <n>g` or `BAG IS EMPTY` / `+<n>g [E]` (yellow) or `SOLD OUT`; other items as at the surface at camp prices, `KIT: SURFACE ONLY` for sticks without the kit | `PromptRenderSystem.DrawCounterPanel` (`atCamp`), `DeepCamp.SellBagName`, `EmptyBagText`, `KitAtSurfaceText` | At the deep camp's counter (row 110, columns 22–24) |
| `CAMP` (plank), `¤` lantern, `/▀\` tent | `CampRenderSystem.SignText`, `Lantern`, `Tent` | The deep camp at 100 m, once bought |
| ` LIGHTING FUSE ` + bar | `HudSystem.PlantLabel`, row 1 | `E` held underground to plant |
| `  ¡<n>` (red `¡`) | `HudSystem.DrawStatus`, row 0, after the lift | Dynamite, once the kit is bought |
| `BLASTING`, ` ¡ ¡ ¡ ` | `HouseRenderSystem.BlastingText`, `BlastingDisplay` (shed) | Scenery |
| ` LIFT BLOCKED BY OBSIDIAN ` (red) | `PromptRenderSystem.LiftBlockedText` | At the lift sign while the next segment has obsidian in the shaft |
| ` HOLD [E] + ARROW: DYNAMITE ` | `PromptRenderSystem.PlantHintText` | Idle underground facing obsidian with the kit and a stick; also the second line on the lift's bottom landing while carrying dynamite |
| ` TOO HARD: DYNAMITE AT THE SHOP ` (red) | `PromptRenderSystem.NeedDynamiteText` | Idle underground facing obsidian without the kit or sticks |
| `FOSSIL!` / `RELIC!` (white, floating) | `FloatingTextRenderSystem.FossilText`, `RelicText` | A rare find was dug out |
| `OIL +<n>` (yellow, floating) | `FloatingTextRenderSystem.OilLabel` | A carried oil flask poured itself into a low lamp |
| `GAS!` (yellow, floating) | `FloatingTextRenderSystem.GasText` | A gas pocket flared; the lamp is out for 3 s |
| `BLACK DAMP` (grey, floating) | `FloatingTextRenderSystem.BlackDampText` | The miner stepped into black damp: half the cone, twice the oil |
| `TOO HARD` (red, floating) | `FloatingTextRenderSystem.TooHardText` | The shovel hit obsidian (at most once a second) |
| ` CALL LIFT [E] ` / ` GET IN [E] ` | `PromptRenderSystem.CallLiftText`, `BoardLiftText` | On a lift landing, car away / there |
| ` ↑↓ RIDE   ←→ GET OUT ` | `PromptRenderSystem.RideText` | In the stopped car |
| `ELEVATOR +10m` (floating) | `FloatingTextRenderSystem.ElevatorLabel` | A lift segment was built |
| `  LIFT <n>m` | `HudSystem.DrawStatus`, row 0, after the ladders | Once a lift is built |
| ` LIFT ` | `HouseRenderSystem.LiftText` (sign beside the mine entrance) | Scenery |
| `+<n>g` / `-10g` (floating) | `FloatingTextRenderSystem` | Delivery / purchase |
| ` RECORD <n>m ` (white on dark yellow, over the `MINE` frame) | `HouseRenderSystem.DrawRecordSign` | The deepest depth reached (`Miner.DepthRecord`) |
| `TOOLS`, `CLOSED` (stall at columns 13–14) | `HouseRenderSystem.ToolsText`, `ClosedText` | The tool shop; `CLOSED` until the miner has been below the wall |
| ` TOOL SHOP: OPENS PAST THE WALL ` (grey) | `PromptRenderSystem.ToolShopClosedText` | At column 13 while the tool shop is closed |
| ` ↑ DYNAMITE x5 ↓ ` / ` ↑ LADDERS x10 ↓ `, `HAVE <n>` or `NEEDS THE KIT`, `<price>g [E]` | `Shop.DynamiteBundleName`, `LadderBundleName`, `PromptRenderSystem.NeedsKitText` | The open tool shop's counter |
| `HOUSE UPGRADE!` / `TOOL SHOP OPEN` (yellow, floating) | `FloatingTextRenderSystem.HouseUpgradeText`, `ToolShopOpenText` | A sale reached a gold milestone / first time below the wall |
| ` EternalDigger `, `SHOP`, `MINE` | `HouseRenderSystem` (house sign, shop, shaft frame) | Scenery |

### TerraForM screen text (`HudTexts`, `Sprites`)

| Text | Where | Meaning |
|---|---|---|
| `HUMAN, WHAT IS YOUR NAME?` | `Sprites.InputName` | Name scene (`InputNameScene`) |
| `press H to continue` | `Sprites.Story` | Menu scene (`MenuScene`) |
| `NOW ENTERING:  ` + map name | `HudTexts.MapPrefix` | In a level. The name is the map's `!name` ([P8](05-triage.md#p8)) |
| `POWER: … kWh` | `HudTexts.Power`, `PowerUnit`. Number formatted `ToString("N")` in the **user's culture** | Remaining power |
| `!OUT OF POWER!` | `HudTexts.NoPower` | `RemainingPower <= 0` |
| `DAMAGE TAKEN: n` | `HudTexts.DamageTaken` | — |
| `ATHMOSPHERE GENERATORS PLANTED: n` | `HudTexts.AtmosphereGeneratorsPlanted` (spelling as in code) | — |
| `REMAINING COMMAND SEQUENCES: n` | `HudTexts.RemainingCommandSequences` | Can show −1 ([03 § Game over](03-rules.md#game-over)) |
| `QUEUED COMMANDS ` + `→←↑↓P` | `HudTexts.QueuedCommands`, `Move`/`Plant.GetVisualRepresentation` | Typed, not yet sent with SPACE |
| `SCORE:` | `HudTexts.Score` | Top-right. **Hidden during game over** |
| `PRESS R TO RESTART` (inside the GAME OVER art) | `Sprites.GameOver` | `GameScene.GameOver` is true |

## Reading the screen as state (TerraForM)

| What the user sees | State |
|---|---|
| Name prompt | `InputNameScene` is the only scene |
| Story + `press H to continue`, nothing else | `MenuScene`. Either the only scene (H starts map1) or pushed over a hidden level (H resumes) |
| GAME OVER art, no HUD | `GameScene.GameOver == true` this frame. Input still live |
| `QUEUED COMMANDS` line stays while rover moves | Commands queued during execution. SPACE ignored until the queue drains |
