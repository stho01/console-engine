# 03 — Rules users can dispute

Each rule quotes the identifier that implements it. "Works as coded" is a valid triage outcome when one of these
rules explains the report. Say which rule applies.

## Engine

<a id="frame-loop"></a>
### Frame loop order (`GameBase.Start`)

Every frame, in this order: `GameTime.Update()` → `Input.Instance.Update()` → `_console.Clear()` (only if
`ClearScreenOnEachFrame`) → `Scenes.Update()` → game `OnUpdate()` → `Scenes.Render()` → game `OnRender()` →
`_console.Display()` → `Scenes.Cleanup()`.

- Game-level `OnUpdate`/`OnRender` run **every frame regardless of scene**, after the scene, so they draw on top.
- `Thread.Sleep(100)` runs once before the loop. There is **no frame cap**: the loop runs as fast as the machine
  allows.

<a id="fps"></a>
### Frame-rate dependence

- `GameTime.Delta` = wall time since the previous frame. `GameTime.Fps = (int)(1f / Delta.TotalSeconds)`, which is
  instantaneous rather than averaged, so it jumps around.
- Anything that adds per frame without multiplying by `Delta` runs faster on faster machines. In this repo that is
  **TerraForM `Rover`** (`Position += Velocity`, and power cost per `ApplyForce` call; TerraForM.Ecs scales by `Delta`, see
  [§ TerraForM.Ecs](#terraform-ecs)) and **Asteroids `Player`**
  (`Position += Velocity`). Snake uses a time threshold and does not depend on FPS.

<a id="intervals"></a>
### Intervals (`GameTime.SetInterval(ms, callback)`)

- Run on the game thread, at most **once per frame**, when the accumulated `Delta` reaches `ms`. The accumulator is
  then reset to 0, so the remainder is dropped and the real period is always ≥ `ms` and drifts longer at low FPS.
- `Interval.Pause = true` skips accumulation. `GameTime.ClearInterval` removes it.
- `ShowFps` registers a **100 ms** interval that calls `Log.ReportFps` (`GameBase.Initialize`).

<a id="keys"></a>
### Keys (`InputHandler.Update`, `KeyState`)

| Property | True when |
|---|---|
| `Held` | The key is down (bit `0x8000` of `GetAsyncKeyState`) |
| `Pressed` | **Exactly one frame**: the state changed this frame, the key is down, and it was not `Held` before |
| `Released` | One frame: the state changed and the key is not down |

Gotchas:
- **Global keyboard.** `GetAsyncKeyState` reads the physical keyboard whatever window has focus **[external]**. The
  game reacts to keys typed in other windows.
- **Very quick taps can be missed.** If a key goes down and up between two polls, only the low "pressed since last
  call" bit is set **[external]**. The code treats any change without `0x8000` as a release, so `Pressed` never fires.
  More likely at low FPS.
- `Key` enum (`ConsoleEngine.Abstractions/Inputs/Key.cs`) covers 0–9, A–Z, F1–F12, arrows, ENTER, ESCAPE, SPACE,
  BACKSPACE, TAB (0x09), numpad and a few others. **No Shift/Ctrl/Alt and no OEM keys** (æ, ø, å, punctuation). Raw codes
  0–255 can be read with `GetKey(int)`. 256 or more throws `IndexOutOfRangeException`.
- `Input.GetPressedKeyCodesSpace09AZ()` returns pressed codes that are `32` or `48..90`, i.e. space, digits and
  upper-case letters only.

<a id="scenes"></a>
### Scenes (`SceneManager`)

| Call | Effect |
|---|---|
| `Push(scene)` / `Push<T>()` | Pauses the current top scene, sets `scene.Game`, calls `scene.OnLoad()` **immediately**, pushes it |
| `Pop()` | Removes the top scene, queues it for unload, unpauses the new top. Returns `null` if the stack is empty |
| `Set(scene)` / `Set<T>()` | Queues **every** scene for unload, then `Push(scene)` |
| `Cleanup()` (end of frame) | Calls `OnUnload()` on queued scenes, then `GC.Collect()` |

- Only the **top** scene is updated and rendered. A paused scene neither updates **nor renders**, so whatever is under
  a menu disappears (the screen was cleared).
- On `Set`, the new scene's `OnLoad` runs **before** the old scene's `OnUnload`, which happens at end of frame.
- `PreviousScene` throws if the stack is empty, because it calls `Stack.Pop()` unguarded.

### Drawing (`RenderConsole`)

- `Draw(x, y, char…)` and everything built on it (`Draw(string)`, `Draw(Sprite)`, `DrawLine`) **silently clips**
  off-screen cells.
- `Draw(int x, int y, Span<string> rows, …)` has **no bounds check**. Off the right edge it wraps onto the next row.
  Off the bottom/top it throws `IndexOutOfRangeException`.
- `Sprite.Draw(x, y, str, …)` (writing into a sprite) has no bounds check either.
- `Sprite.FromString` splits on `Environment.NewLine` (`\r\n` on Windows). A string with bare `\n` becomes one row.
- Colours: only the 16 `ConsoleColor` values (`GetColorValue` packs fg | bg<<4). More colours is GitHub issue 6.

### Logger (`Log`)

- `Log.WriteLine` / `Log.ReportFps` silently drop messages unless the game has `EnableLogger = true`
  (`EnqueueMessage` guard).
- FPS is shown **only in the logger window's title** (`Game FPS: n`), never in the game window.
- Port 11000 and the exe name `ConsoleEngine.Logger.exe` are hard-coded.

<a id="ecs"></a>
### ECS (`ConsoleEngine.Ecs.World`)

`GameBase` and plain `Scene`s don't use it. An `EcsScene<TGame>` owns a `World` and runs it (example:
`Examples/Asteroids.Ecs`, plan: `docs/design/ecs-plan.md`). A game can also create a `World` and `SystemGroup`s
itself.

- Components are `unmanaged` structs. A struct with no instance fields and a size of 1 byte (what the runtime gives an
  empty struct) is a **tag** (`ComponentType<T>.IsTag`): it changes the archetype but has no data, so `Get<T>` on a
  tag throws. A field-less struct with an explicit `[StructLayout(Size = n)]` is stored as data.
- `Add<T>` throws if the entity already has `T`. `Remove<T>`, `Get<T>` and `Set<T>` throw if it doesn't.
  `Set<T>` never adds.
- `Create(...)` with the same component type twice throws `ArgumentException`.
- Every operation on a destroyed entity, a stale handle (old `Version`) or `Entity.Null` throws "is not alive".
  `IsAlive` is the non-throwing check.
- After `World.Dispose()` every operation (`Create`, `Destroy`, `Get`, `Set`, `Has`, `Add`, `Remove`) throws
  `ObjectDisposedException`, `IsAlive` is false, `ArchetypeCount` is 0, and existing queries yield nothing. There is
  **no finalizer**: a world that is never disposed leaks its native chunks (a finalizer could free memory still
  written through a `Get<T>` ref).
- Destroying an entity bumps its id's `Version` and puts the id on a free list. The next `Create` reuses it. When the
  version is already `int.MaxValue`, the id is **retired** instead (version −1, never reused), so old handles can
  never become valid again through wrap-around.
- The `ref` from `Get<T>` is only valid until the next structural change (`Create`, `Add`, `Remove`, `Destroy`) in
  that archetype: swap-back removal moves the archetype's last row into the freed row.
- At most 256 component types per process (`ComponentMask.Capacity`). A type gets its id the first time **any** ECS
  API names it, including read-only ones (`Has<T>`, `Get<T>`, `Remove<T>`, `Query.With<T>`), and keeps it. The 257th
  type throws `TypeInitializationException` and stays unusable for the rest of the process.
- An archetype whose row (8-byte `Entity` plus every component) doesn't fit in one 16 384-byte chunk throws on first
  use. `Create` reaches its archetype by walking add-edges from the empty archetype, so the first `Create` of a
  component set also creates the (empty) archetypes for its prefixes, e.g. {Position} for {Position, Velocity}.
- `World.Query(description)` matches archetypes with every `With<T>` and no `Without<T>` component. Describe it
  with `Query.With<…>()` or `Query.Without<T>()` and chain `.With`/`.Without` (no `new QueryDescription()`). A query also
  picks up archetypes created after it, on its next use.
- **No structural change while a query is iterated** (`GetChunks` or `ForEach`, any query on that world). From the
  start of the `foreach` until it ends (also on `break`, `return` or an exception), `World.Create`/`Destroy`/`Add`/
  `Remove` and `CommandBuffer.Playback` throw `InvalidOperationException`. `Get`/`Set` are allowed. Record changes
  in a `CommandBuffer` instead.
- `EntityChunk.GetSpan<T>()` throws if `T` is not in that chunk. Use `EntityChunk.Has<T>()` for optional components.

**Command buffer** (`CommandBuffer`):
- Commands are applied in recording order by `Playback(world)`, which then clears the buffer (also when it throws).
- At playback, `Destroy`/`Add`/`Remove` for an entity that is **no longer alive are skipped silently**: destroying
  twice is safe. `Add` of a component the entity already has, or `Remove` of one it lacks, still throws.
- `Create(...)` returns a **deferred handle**: an `Entity` with a negative `Id` and the buffer's current generation
  as `Version`. The entity exists only after playback, and the world treats the handle as not alive. Later
  `Add`/`Remove`/`Destroy` calls on the **same** buffer accept it, and playback swaps it for the created entity. A
  deferred handle from another buffer, or from before this buffer's last `Playback`/`Clear`, throws at playback.
- Duplicate component types in `Create` throw `ArgumentException` when recorded.
- Deferring costs roughly 2× a direct create+destroy and 1.25× a direct add/remove (`CommandBufferBenchmarks`).

**Systems** (`EntitySystem`, `SystemGroup`):
- `SystemGroup.Add` calls the system's `OnCreate` (where it builds its queries) with `World` and the group's
  `Commands` already set.
- `SystemGroup.Update(delta)` runs enabled systems in `RunOrder`, then plays back `Commands`. That playback is the
  only sync point: entities created by a system become visible to the **next** group, not to later systems in the
  same group.
- `RunOrder` is `Add` order, except where `[UpdateBefore(typeof(X))]` / `[UpdateAfter(typeof(X))]` on a system's class
  says otherwise. Among systems that are free to run, the one added first goes first. A constraint naming a type
  not in the group is ignored. Constraints that form a cycle throw `InvalidOperationException` on the first
  `Update` (or `RunOrder` read) after the systems were added.

**`EcsScene<TGame>`**: `OnLoad` creates `World`, `Simulation` and `Presentation`, then calls `OnCreate`. The scene's
`OnUpdate` runs `Simulation`, `OnRender` runs `Presentation`, both with `(float)GameTime.Delta.TotalSeconds`. `OnUnload`
calls `OnDestroy`, then disposes the world. A paused scene (another scene pushed on top) runs neither group.

**Built-in systems** (namespace `ConsoleEngine.Ecs.Systems`, components in `ConsoleEngine.Ecs.Components`):
- `MovementSystem`: `Position += Velocity * delta`. No wrapping, no bounds.
- `GlyphRenderSystem`: draws `Glyph.Character` at `floor(Position - Camera.Position)`. Off-screen cells are clipped
  by `RenderConsole.Draw`.
- `OutlineRenderSystem`: draws the `OutlineLibrary` polygon for `Outline.Id` around the position, rotated clockwise
  by `Rotation.Radians` if the entity has a `Rotation`. Skipped when its bounding circle (`OutlineLibrary.GetRadius`)
  is entirely off-screen.
- `SpriteRenderSystem`: draws the `SpriteLibrary` sprite for `SpriteRef.Id` with its top-left cell at
  `floor(Position - Camera.Position) + SpriteRef.Offset`. Skipped when the whole sprite is off-screen. The optional
  `QueryDescription` filter picks a layer (e.g. `With<Tile>()`): add one instance per layer in drawing order, because
  a query visits archetypes in no defined order.

<a id="terraform"></a>
## TerraForM

<a id="power"></a>
### Power

- Start: `Rover.RemainingPower = MaxPower = World.MaxPower` (map `!power`).
- Every call of `Rover.ApplyForce(force)` costs `force.LengthSquared()`. A move costs `Thrust² = 0.8² = 0.64`.
  Power is deducted **even when already depleted**, so it goes negative. Acceleration is applied only if power is
  still above 0 after the deduction.
- A `Move` command lasts **100 ms** (`Move(direction, float duration = 100)`) and calls `MoveEast`/`MoveWest`/`MoveNorth`/`MoveSouth` **once per frame**
  while active. **Cost per move ≈ 0.64 × (frames in 100 ms + 1)**. Approximate figures: ~4.5 at 60 FPS, ~65 at
  1000 FPS. With map1's 3500 power that is ~770 moves at 60 FPS but only ~54 at 1000 FPS. *A fast PC drains power
  much faster.*
- Distance also depends on FPS. `Velocity += Acceleration * dt` (FPS-independent), but `Position += Velocity` runs per
  frame without `dt`, so the rover travels further per move on a faster machine. `Drag = 5`.
- `Craves` contact sets `RemainingPower = 0`.
- `BonusPoint` adds **+1000** once (`HasBeenConsumed`). There is no cap, so power can exceed `MaxPower`.
- HUD (`Hud.GetPowerColor`): ≥ 75 % of `MaxPower` green, ≥ 50 % yellow, otherwise red. At ≤ 0 it shows `!OUT OF POWER!`.

### Damage

- Each frame the rover overlaps a `Rock` adds **+10** per overlapping rock. Motion stops and the position reverts
  (`Rover.HandleCollision`). The map border is rocks.
- Damage **never** causes game over. It only lowers the finish bonus and changes the HUD colour (> 75 red, > 50 yellow,
  > 25 dark green, otherwise green).
- Quirk: `MoveNorth` (only) moves the rover 1 more cell north if it is touching a `Rock`/`Craves` at that moment.

<a id="sequences"></a>
### Command sequences

- Arrows add `Move` commands and `P` adds `Plant` to the HUD queue (`Hud.OnUpdate`, uses `Pressed`).
- **SPACE commits** the queue (`GameScene.CommitCommands`) **only if no commands are still executing**
  (`!_currentCommands.Any()`). Keys typed meanwhile are kept and shown after `QUEUED COMMANDS`, and the next SPACE
  sends them.
- `RemainingSequences` starts at map `!sequences` and drops by 1 per non-empty commit.
- Commands run one at a time, in order, each until `_elapsed > DurationInMilliseconds` (`Command.IsDone`).

<a id="game-over"></a>
### Game over

- Computed **every frame** at the top of `GameScene.OnUpdate`:
  `GameOver = Rover.PowerDepleted() || Rover.RemainingSequences < 0`. It is **not latched**: it clears again if power
  rises above 0.
- With `!sequences: N`, commits 1…N are fine. Commit **N+1** makes it −1, so game over, **but that commit still
  executes**.
- Game over **only changes the HUD**: `Hud.Draw` shows `Sprites.GameOver` ("PRESS R TO RESTART") instead of the HUD.
  Input, commands, movement, planting, bonus and **finish all keep working**. A rover with power left can still
  reach `F` and score ([P10](05-triage.md#p10)).
- `R` (`Pressed`) at any time restarts the same map with `Scenes.Set(new GameScene(_worldName))`.

<a id="score"></a>
### Score (`TerraformGame.Score`)

| Event | Change | Where |
|---|---|---|
| Plant a not-yet-planted `PlantSpot` | +10 000 | `Rover.Plant` |
| Collect a `BonusPoint` (once) | +10 000 (and +1000 power) | `Rover.HandleBonusPointCollision` |
| Reach `FinishPoint` | `+ (int)RemainingPower − DamageTaken + AthmosphereGeneratorsPlanted × 10 000` | `GameScene.RoverOnOnFinishReached` |

- Every planted spot therefore counts **twice** if the level is finished: 20 000 in total.
- The finish term can be **negative** (damage > remaining power).
- Score is **never reset**, not on `R` restart, game over or a new level. Points from a failed attempt stay
  ([P7](05-triage.md#p7)).
- Each frame the rover overlaps a finish tile fires `OnFinishReached` once per overlapping `FinishPoint`. Two adjacent
  `F` tiles can award the finish bonus twice and rotate the map twice in one frame. Of the shipped maps only `map4`
  has two `F`s, and map4 is never loaded.

<a id="planting"></a>
### Planting

- `Rover.Plant()` acts only if `StandingOnPlantingSpot` is true. That flag is set by collision in the **previous**
  `Rover.Update`, because `UpdateCurrentCommand` runs before `Rover.Update` in `GameScene.OnUpdate`.
- It plants every overlapping `PlantSpot` with `HasBeenPlanted == false`. A spot can be planted once per level attempt.
- A `Plant` command lasts 1 ms (`Plant() : Command(1.0f)`), usually one or two frames.
- Overlap uses bounding boxes. The rover box is `Position` + sprite size (4×6 facing N/S, 6×4 facing E/W). The
  `PlantSpot` box is 10×10 from its tile position. **The rover sprite is drawn 1 cell up-left of its box**
  (`Rover.Draw` uses `screenPos - 1`), so it can look on the spot and not be, or the reverse.

<a id="map-rotation"></a>
### Map rotation (`TerraformGame.RotateMap`)

- `CurrentMap` starts at **1**. Menu `H` (first time) loads `map{CurrentMap}`, which is map1.
- On finish: `CurrentMap++`, and if `CurrentMap >= Maps.Length` (4) it becomes **0**. The map loaded is
  `$"map{CurrentMap}"`. **The `Maps` array's contents are ignored.**
- Resulting order: **map1 → map2 → map3 → map0 → map1 → …** `map4` ("Leviathan Falls") is **never** played, and
  `map0` ("Map Name", commented out in `Maps`) **is**.

### Name entry (`InputNameScene`)

- Letters are appended from `GetPressedKeyCodesSpace09AZ()`: **upper-case A–Z, digits 0–9 (top row only), space**.
  No lower case, æøå, punctuation or numpad digits. `BACKSPACE` deletes one, `ENTER` accepts. An empty name is allowed.
- The global keyboard applies ([§ Keys](#keys)): text typed in other windows is captured.
- `MenuScene.OnLoad` writes the first **30** characters over the `###` placeholder in `Sprites.Story` (row 22,
  column 32). It does not clear first, so a 1–2 character name leaves `#`/`##` visible. An empty name leaves `###`.

### Menu

- `ESC` (`Held`) in a level pushes `MenuScene`, which pauses the level and hides it ([§ Scenes](#scenes)).
- `H` in the menu: if it is the only scene (`Scenes.Count == 1`), start `map{CurrentMap}`. Otherwise `Pop()` back to
  the level.
- There is **no quit key**. Close the window.

<a id="terraform-ecs"></a>
### TerraForM.Ecs (`Examples/TerraForM.Ecs`)

The same game on the ECS. It shares the map files, `Sprites` and `HudTexts` with `Examples/TerraForM` through linked
files, so every rule above applies, except:

- **Movement and power are scaled by `Delta`.** The port plays like the original at 300 FPS
  (`CommandSystem.ReferenceFrameRate`):
  - Thrust is 240 cells/s². Drag is unchanged at 5/s.
  - Power drops by 192 per second of thrust (`PowerPerThrustSecond`). A move (100 ms) costs about **19.2** at any
    FPS, and the rover travels about 5 cells (one tile).
  - Map1's 3500 power is therefore about 176 moves.
- Unchanged and still applied per frame: the north-move nudge, +10 damage per overlapping rock, a crater setting
  power to 0, and the finish bonus.
- Debug mode (WASD free move, overlay), `TerraformGame.GameOver` and the `Wait` command are removed (unreachable in
  the original).
- Sprites are drawn by the engine's `SpriteRenderSystem`, which floors screen positions. The original truncates, so a
  sprite partly left of or above the screen edge is drawn one cell further left/up than in TerraForM.
- Window title is `"TerraForM (ECS)"`. A map without `S` throws `InvalidOperationException` instead of
  `NullReferenceException`.
- Systems run in the original order: `LevelInputSystem` → `CommandSystem` → `RoverPhysicsSystem` → `MovementSystem` →
  `RoverCollisionSystem` → `PlantEmitterSystem` → `RestartSystem` → `CameraFollowSystem`. Drawing: tiles → grass →
  rover → `HudSystem`.

## Other examples

| Game | Rule | Identifier |
|---|---|---|
| Snake | Moves one cell every **150 ms** (`UpdateThreshold`). FPS-independent | `SnakeGame.OnUpdate` |
| Snake | Reversing direction is ignored. **No wall or self collision**: the snake leaves the screen and is clipped forever | `SnakeGame.OnUpdate` |
| Snake | Food x and y both use `Random.Next(0, Console.Width - 1)`, so never the last column/row. It retries while the cell is not `' '`, up to `Area` times. Never-drawn cells hold `'\0'`, so the retry rarely succeeds and food can land on the snake | `SnakeGame.SpawnFood` |
| Snake | `ClearScreenOnEachFrame = false`. The tail cell is blanked manually | `SnakeGame.OnInitialize`, `OnRender` |
| Snake.Ecs | Same steering, 150 ms step (timer reset to 0 on each step) and **no wall or self collision** as Snake | `SteeringSystem`, `SnakeMovementSystem` |
| Snake.Ecs | Each step leaves a new `SnakeSegment` where the head was and destroys the oldest one (lowest `Order`), unless the head landed on food: then the food is destroyed and the snake grows by one | `SnakeMovementSystem.Step` |
| Snake.Ecs | Differs from Snake: when no food exists, new food is placed on a uniformly random **free** on-screen cell (never on the snake, any column/row). It appears after the simulation group's playback. The whole screen is redrawn each frame | `FoodSpawnSystem` |
| Platformer | `F1` toggles `IsDebugMode`, which **starts true** (`Program.cs`). Draws the player **before** the world, so tiles overwrite the player | `PlatformerGame.OnUpdate`, `OnRender` |
| Platformer | `ShowFps = true` but no `EnableLogger`, so no FPS anywhere | `PlatformerGame.OnInitialize` |
| Platformer.Ecs | Same keys, start state (debug on) and draw order (debug text, player, then tiles on top) as Platformer. Velocity is in cells/s and scaled by `Delta`: gravity 200, run 150, air 75 cells/s², drag (3, 0.1)/s, jump speed 50 cells/s (about 6 cells high at any FPS) | `PlayerControlSystem`, `PlayerPhysicsSystem`, `PlatformerScene` |
| Platformer.Ecs | Differs from Platformer: horizontal speed under 1 cell/s is zeroed only when A/D is not held; moves are split into steps of at most 0.5 cells; vertical and horizontal collisions are checked one after the other; moving left/right checks the corners on that side; screen positions are floored. Kept from the original: walking off a ledge does not make the player airborne, and a jump pressed in the air happens on landing | `PlayerPhysicsSystem.ResolveCollisions` |
| Asteroids | Steer `A`/`D`, thrust `W`. No collisions, no objective. Logger enabled but nothing is logged | `AsteroidsGame` |
| Asteroids.Ecs | Steer `A`/`D` (`Ship.SteeringSpeed` 2π rad/s), thrust `W` (`Ship.Thrust` 60 cells/s²), drag `Ship.Drag` 1.5/s. All movement is scaled by `Delta` | `ShipControlSystem` |
| Asteroids.Ecs | `SPACE` (Pressed) fires one bullet: 80 cells/s plus the ship's velocity, lives 1.5 s | `ShipControlSystem`, `Spawner.CreateBullet`, `LifetimeSystem` |
| Asteroids.Ecs | A bullet whose `Collider` touches an asteroid's destroys both. The ship never collides. No score, no objective | `CollisionSystem` |
| Asteroids.Ecs | Asteroids drift up to 6 cells/s. Everything wraps around a playfield three screens wide and high, so the camera jumps when the ship crosses an edge | `MovementSystem` (engine), `WrapSystem`, `Playfield.Wrap`, `CameraSystem` |
| Asteroids.Ecs | Ship and asteroids are outlines, bullets are `*` glyphs. Hits and expired bullets disappear at the end of the simulation group (command buffer playback), so they are never drawn in the frame they die | `AsteroidsScene.OnCreate`, `OutlineRenderSystem`, `GlyphRenderSystem` |
| ConsoleGame | Recolours all cells every **500 ms**. `ESC` calls `Stop()`, which exits | `Game.OnInitialize`, `OnUpdate` |
| ConsoleGame.Ecs | One entity per cell (4096). Every **500 ms** (timer reset to 0) each gets a random colour of the same five. Cells are black until the first recolour. `ESC` quits | `RecolorSystem`, `ColorGridScene`, `ConsoleEcsGame.OnUpdate` |
| Swarm | Particles are pulled towards a point moving on a Lissajous curve, each with its own `Agility.Pull` (30–180 cells/s²), drag 0.3/s, and bounce off the screen edges. Colour by speed: dark blue < 15, blue < 30, cyan < 50 cells/s, else white. The HUD on row 0 shows the count | `AttractorSystem`, `BounceSystem`, `ColorBySpeedSystem`, `HudSystem` |
| Swarm | `SPACE` adds 10 000 particles, `BACKSPACE` removes up to 10 000 (both through the command buffer, applied at the end of the simulation group). `ESC` quits | `SpawnControlSystem`, `SwarmGame.OnUpdate` |

<a id="launcher"></a>
## Launcher (`Examples/Launcher`)

| Rule | Identifier |
|---|---|
| Keys (read with `Console.ReadKey`, so only while the launcher window has focus): Up/Down or W/S or K/J move and wrap around, Home/End jump, Enter/Space start the selected game, digit 1–9 selects **and** starts that entry (digits above the entry count are ignored), Esc/Q quit | `LauncherMenu.HandleKey` |
| A game is "built" when its exe exists at the `GameLocator` path. Checked again before every redraw. Starting an unbuilt game shows `LauncherTexts.NotBuilt` and starts nothing | `LauncherApp.RefreshBuilt`, `LauncherApp.Launch` |
| On Windows games start in their **own classic console window** (`conhost.exe`). On macOS and Linux the game runs in the **launcher's own terminal** (the exe has no `.exe` extension, started directly, so a crash prints its exception into that terminal and no `pause` is needed). Working directory = the exe's folder, with no arguments (Asteroids.Ecs and Swarm use their default counts). The launcher blocks until the game exits; keys typed into the launcher meanwhile are discarded | `GameRunner.Run`, `LauncherApp.DiscardBufferedKeys` |
| (Windows) `conhost.exe` always exits with 0, so the launcher cannot tell a crash from a normal quit: it always shows `LauncherTexts.Closed`. A game that exits non-zero (crash) leaves its window open on `pause` with the exception text above it. The logger window that a game opens (`Log.Start`, `UseShellExecute`) still follows the default terminal setting | `GameRunner.BuildArguments` |
| The launcher finds games only from its own build output: it walks up from `AppContext.BaseDirectory` to the folder holding `Launcher.csproj` and uses the same relative output path (configuration + framework) for each game | `GameLocator.FromLauncherOutput` |

<a id="eternal-digger"></a>
## EternalDigger (`Examples/EternalDigger`)

Full contract: [`docs/design/eternal-digger.md`](../design/eternal-digger.md). Coordinates are tile columns/rows; a
tile is 4 × 2 cells (`Layout.TileWidth`/`TileHeight`). Row 0 is the top of the sky.

| Rule | Value | Identifier |
|---|---|---|
| World | 30 columns (`Mine.Columns`), endless downwards. Rows 0–9 sky, row 10 grass (`Mine.SurfaceRow`, depth 0). Depth = `row - 10` m, HUD shows at least 0 | `Mine`, `Mine.Depth` |
| Generation | Same seed → same mine. Rows are generated on first touch in blocks of 64 (`Mine.RowsPerBlock`) | `Mine.Generate`, `Mine.Underground` |
| Unbreakable | Grass (the whole surface row except the shaft), bedrock (columns 0 and 29 below the surface, and everything outside the 30 columns, also beside the sky). Digging them does nothing | `Tiles.IsBreakable`, `Mine.At` |
| Shaft | Column 21 (`Mine.ShaftColumn`): rows 10 and 11 start dug out with ladders. The only way down | `Mine.Create` |
| Hits to break | Dirt 2, Stone 4, Copper 4, Silver 6, Granite 7, Gold 8, Slate 6, Quartzite 8, Gem 9, plus 1 per 25 m of depth (`Tiles.DepthPerExtraHit`), minus 1 per pickaxe tier (min 1). Obsidian: the shovel can't break it | `Tiles.HitsToBreak`, `Tiles.IsBreakable` |
| Where things appear | Copper from 2 m, silver from 12 m, gold from 30 m, caves (air) from 6 m, granite from 40 m. Veins get richer deeper | `Tiles.CopperDepth`…`CaveDepth`, `Mine.Underground` |
| The wall | Obsidian from 110 m (`Tiles.WallDepth`), 3–5 tiles thick per column (`Mine.WallBottomDepth`), in every column but the bedrock walls. A shovel hit on it: sparks, a small shake, red floating `TOO HARD` (at most once per `TooHardInterval` = 1 s), no damage | `Mine.Create`, `Tiles.IsBlastOnly`, `MinerControlSystem.Clank` |
| Act 2 | Below the wall only slate, quartzite (25% → 50% of the rock), gem veins (4% → 7%) and caves. No dirt, stone, granite, copper, silver or gold | `Mine.BelowTheWall` |
| Rare finds | Single tiles, checked first: fossil (`Noise < 0.15%`) in act 1 from 15 m, relic (`< 0.1%`) in act 2. Hits: fossil 5, relic 7 (+ depth). Breaking one: double star ring, sparks, `FOSSIL!`/`RELIC!`; into the bag (`Fossils`/`Relics`) and counted for good (`FossilsFound`/`RelicsFound`). They pulse slowly when sampled by the glint system | `Tiles.IsRareFind`, `Mine.Underground`, `Mine.BelowTheWall`, `EffectSpawner.RareFind`, `GlintSystem` |
| Gem hint | From 95 m (`Tiles.HintDepth`), 3 extra samples per glint tick in the 16 rows below the wall's top + 3; a gem there on screen glints at any distance from the miner | `GlintSystem.SampleBelowTheWall` |
| Ore value | Copper 15g, silver 40g, gold 100g, gem 250g; rare finds fossil 300g, relic 750g | `Tiles.CopperValue/SilverValue/GoldValue/GemValue` |
| Start | 0 gold, 8 ladders, column 15 on row 9 | `Shop.StartGold`, `Shop.StartLadders`, `Layout.SpawnColumn`, `Layout.GroundRow` |
| Support / falling | The miner stands still if their tile has a ladder, or the tile below is solid or has a ladder. Otherwise they fall one tile at a time (0.06 s per tile) **before any key is read**. No fall damage | `Mine.IsSupported`, `MinerControlSystem.Control` |
| Moves | One tile per move: walk 0.13 s, climb 0.17 s. Holding Up on ladders speeds up after 4 tiles, down to 0.06 s per tile after 10 more (`ClimbUpDuration`); any other action or letting go resets it (`Miner.ClimbStreak`). **No keys are read while a move plays** (except the rescue key) | `MinerControlSystem` constants, `Miner.IsMoving` |
| Side keys | Air → walk. Solid and breakable → one hit every 0.16 s (`DigInterval`) while held. Left and right both held cancel out | `MinerControlSystem.MoveOrDig` |
| Jump | Up held with Left/Right while idle and supported (also on a ladder): if the side tile and the tile beyond (`JumpTiles` = 2) are both air, the miner moves 2 tiles in 0.28 s (`JumpDuration`), drawn in a sine arc of 2 cells (1 if the tile above the gap is solid). On landing: dust; unsupported → falls. Otherwise the side key acts as usual | `MinerControlSystem.CanJump`, `MinerMotionSystem.JumpLift` |
| Down key | Below solid → dig it. Below air → step down (then the fall rule applies if there's no ladder) | same |
| Up key | Only works on a ladder tile: above air → climb, above solid → dig. Without a ladder in the miner's own tile, up does nothing | same |
| Ore | Breaking an ore tile puts one piece in the bag (`CopperOre`/`SilverOre`/`GoldOre`/`GemOre`). No bag limit | `Shop.Collect` |
| Ladders | `SPACE` (pressed) places one in the miner's own tile if: ladders > 0, row > 10 (underground), the tile is air and has no ladder. Ladders can't be picked up again. A shaft of N dug tiles needs a ladder in every one of them to climb out | `Mine.TryPlaceLadder` |
| Deliver | `E` (pressed), miner idle at columns 3–6 on row 9 (the door): all ore is sold, the bag empties. Empty bag: only a dust puff | `InteractionSystem`, `Shop.Deliver`, `Layout.IsAtDeliverSpot` |
| Shop counter | Miner idle at columns 8–11 on row 9 (`Layout.IsAtShop`). `↑`/`↓` (pressed) move the selection through `ShopCounter.Items`, wrapping; it starts on ladders and isn't saved. `E` (pressed) buys one of the selected item at `Shop.Price`. Not enough gold or sold out → dust puff, nothing bought; the panel price is red | `ShopCounter`, `Shop.TryBuy`, `InteractionSystem.Interact` |
| Rescue | Hold `R` 1.5 s (`RescueDuration`), anywhere, even mid-move: back to column 15, row 9. **The bag (ore) is lost**, gold and ladders are kept. Releasing early resets the timer | `MinerControlSystem.UpdateRescue` |
| Light | `LightMap` levels `Dark`/`Dim`/`Lit`/`Bright`. Sky and grass rows bright. Daylight: depth 1 lit, depth 2–3 dim, only in dug-out tiles and the tiles touching them. Headlamp: tiles within 1.5 tiles bright (also rock), a cone of ±40° up to 8 tiles (+2 per lamp tier, `Upgrades.LampReach`; shorter when the oil is low, none when empty): within half the reach bright, ¾ lit, else dim. Solid tiles block the lamp. The lamp points along the last move or dig | `LightMap.Compute`, `Miner.LampDirection` |
| Darkness | Lit tiles are remembered (`TileCell.Seen`) and drawn as faint grey outlines when dark, **without ore colour**. Never-seen dark tiles are black | `TerrainRenderSystem` |
| Glints | Ore within 9 tiles of the miner (+3 per detector tier) and on screen twinkles, also through rock and in the dark (more in the dark). Gold most often, then silver, then copper. Sampled at a fixed rate (every 0.08 s), so not FPS-dependent | `GlintSystem` |
| Camera | Follows the miner vertically (miner at 40% of the screen height), eased; never above the sky. Always on whole cells. Breaking tiles and falls of 3+ rows shake it | `CameraSystem`, `ScreenShake` |
| Miner visibility | The miner is drawn after particles. No particle is drawn on his 4 × 2 cells; in the one-tile ring around him at most 6 particles per frame (`NearMinerCap`) are drawn, in their darker colour | `ParticleRenderSystem`, `MineScene` (Presentation order) |
| Effects | Debris on hits, bursts and dust on breaks, foot/landing dust, mist in lit caves (max 40), coins + star pop + `+<gold>g` on delivery, pop + `-10g` on buying. All scaled by `Delta` | `EffectSpawner`, `ParticlePhysicsSystem`, `ParticleLifetimeSystem`, `MistSystem` |
| Lift: buying | `E` idle at columns 23–24 on row 9 (`Layout.IsAtLiftSign`): one 10 m segment for 200g + 100g per segment already built. Digs out the next 10 tiles of column 21; ore there goes into the bag, ladders stay. Obsidian in those tiles blocks it (` LIFT BLOCKED BY OBSIDIAN `, nothing charged), so the lift ends at 100 m until the shaft's obsidian is blasted. Not enough gold → dust puff, nothing built. Every purchase saves | `Elevator.TryBuild`, `InteractionSystem` |
| Lift: landings and riding | Landings: row 9 and the bottom row. `E` idle on a landing in column 21: car there → get in, else call it (the caller gets in on arrival). On the bottom landing while carrying dynamite this happens on the release of a short tap (< 0.5 s), because holding `E` plants. In the stopped car: Up → top, Down → bottom, 0.04 s per tile, no keys while it moves; Left/Right (or Down at the bottom) get out and act as usual. A rescue gets out too | `Elevator`, `MinerControlSystem.Control`, `ElevatorSystem.Carry` |
| Lamp oil | Tank 100 (`LampOil.TankSize`), full in a new mine. Burns 1 unit per 3 s only while the miner's row is below the grass (the lift too), ×0.8 per lamp tier, ×(1 + 0.5 per bag weight step) while climbing up. Below 30: the cone reach falls linearly to 3 tiles at empty, flicker stretches of 0.04–0.12 s dim all lamp light one level (20–70% of them, more when emptier), dark-yellow lamp glyph, `°` smoke every 0.45 s. At 0: no cone, the close radius one level darker (`Lit`), grey lamp glyph, no glow. Never harms. `FILL LAMP` at the counter: 2g per missing whole unit; `FULL` when nothing is missing. `OIL FLASK` at the counter: 60g, max 5 carried (`FULL`); below the surface a flask pours itself in when the oil is below 30: +40 (capped at 100), smoke + floating `OIL +40` (`LampOil.TryPour` in `LampSystem`, after the burn) | `LampOil`, `LampSystem`, `Headlamp`, `LightMap.Compute` |
| Bag weight | Weight: copper 1, silver 1, gold 2, gem 2, rare find 3. Capacity 20 + 10 per bag tier. Each 10 over (rounded up) is a step: climbing up takes +20% per step (max 3×, on top of the climb ramp) and burns +50% oil per step. Walking, climbing down, falls and the lift are unaffected. Heavy: HUD bag value yellow, backpack `▓` | `Shop.BagWeight`, `WeightSteps`, `ClimbLoad`, `OilLoad`, `MinerControlSystem.Control`, `LampSystem.LoadFactor` |
| Upgrades | At the counter: shovel 300/900/2500g (−15% dig interval per tier), pickaxe 350/1000/2800g (−1 hit per tier on every breakable tile, min 1: `Upgrades.FewerHits` → `Mine.Hit`/`Tiles.HitsToBreak`, cracks drawn with the same count), lamp 250/750/2000g (+2 tiles reach and oil burn ×0.8 per tier; bright/lit bands at ½ and ¾ of the reach), bag 200/600/1500g (+10 carried weight per tier, used by bag weight), detector 400/1200/3000g (+3 glint radius, glint interval ×0.8 per tier). Max tier 3: the panel shows `MAX`, `E` gives a dust puff | `Upgrades`, `Shop.TryBuy` |
| Blasting at the counter | The counter's blasting slot: without the kit, buys it for 2500g; with it, one stick for 100g. The `BLASTING` shed (columns 26–28) is scenery | `Shop.TryBuyBlasting` |
| Planting dynamite | Hold `E` 0.5 s (`PlantDuration`) while idle, supported and underground: a lit stick in the tile `LampDirection` faces. An arrow held with `E` sets `LampDirection` first (`↓` wins, then `↑`, then `←`/`→`; `MinerControlSystem.Aim`). On a lift landing only with the kit and ≥ 1 stick (`MinerControlSystem.CanPlant`); there `E` uses the lift on a tap released before 0.5 s instead of on the press (`InteractionSystem.IsTapToUseLift`, `MinerInput.UseReleased`). Needs the kit, ≥ 1 stick, and breakable rock or obsidian there, else dust puff. While `E` is held nothing else happens; after a plant (or a refusal) `E` must be released first | `MinerControlSystem.UpdatePlanting` |
| Blast | After the 2 s fuse (`FuseDuration`): the 3 × 3 around the stick becomes air if it's breakable rock or obsidian (not bedrock, grass, rows ≤ 10). Ore in it → bag. Ladders in it are removed. Other sticks in it: fuse set to ≤ 0.12 s (`ChainDelay`). A standing, idle miner inside slides one tile away from the stick (sideways first) if that tile is open; never hurt. Biggest screen shake | `DynamiteSystem.Blast`, `Push` |
| Inventory | `TAB` (pressed) toggles a centred panel (gold, ladders, oil %, flasks, dynamite, lift, bag lines `count × price = worth`, weight/capacity, upgrade pips, finds ever found). While open, every `MineScene.Simulation` system has `Enabled = false`: nothing moves, no oil burns, no autosave. `ESC` still saves and quits | `InventorySystem.Toggle` |
| Quit | `ESC` in the mine saves (`MineScene.Save`), then `Stop()`. On the title screen it only quits | `EternalDiggerGame.OnUpdate` |
| Title | Starts on `TitleScene`. With a loaded save: `CONTINUE`, `NEW MINE`; else only `NEW MINE`. `↑`/`W`, `↓`/`S` (pressed) move the selection (wraps), `E` picks. NEW MINE with a save asks `NO` / `YES, START OVER` (NO selected); NO goes back. NEW MINE uses the command-line seed (or `TickCount`) and writes its first save at once | `TitleScene`, `TitleMenu` |
| Saving | Saves after a sale that earned gold, after a ladder, lift, kit or dynamite purchase, every 60 s underground (`GameSaver.AutosaveInterval`, timer counts only below the surface and resets on every save), and on `ESC`. Never per frame. A failed write shows ` SAVE FAILED` and play goes on | `InteractionSystem`, `SaveSystem`, `GameSaver` |
| Loading | CONTINUE rebuilds the mine from the saved seed plus the saved changed tiles, and puts the miner standing on the saved tile with the saved facing and inventory. Unreadable save: red message on the title, only NEW MINE, file moved to `save.dat.bad` | `SaveFile.Load`, `SaveData.ApplyTo`, `SaveData.ToMiner` |
| Save hint | After each save (or failed save), ` SAVED ` (Gray for 0.75 s, then DarkGray for 0.75 s) or ` SAVE FAILED` (red) at the right end of HUD row 1 | `HudSystem.DrawSavedHint`, `HudSystem.SavedHintSeconds` |

