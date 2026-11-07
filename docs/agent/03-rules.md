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
  BACKSPACE, numpad and a few others. **No Shift/Ctrl/Alt/Tab and no OEM keys** (æ, ø, å, punctuation). Raw codes
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

**`EcsScene<TGame>`**: `OnLoad` creates `World`, `FixedSimulation`, a default `FixedTimestep`, `Simulation` and
`Presentation`, then calls `OnCreate` (which may replace `FixedTimestep`). The scene's `OnUpdate` first runs
`FixedSimulation` as many times as `FixedTimestep.Advance(delta)` returns, each time with `FixedTimestep.StepSeconds`,
then `Simulation` once. `OnRender` runs `Presentation`. `Simulation` and `Presentation` get
`(float)GameTime.Delta.TotalSeconds`. `OnUnload` calls `OnDestroy`, then disposes the world. A paused scene (another
scene pushed on top) runs no group, and its paused time is not added to the fixed-step accumulator.

**`FixedTimestep`** (default `DefaultStepSeconds` = 1/60 s, `DefaultMaxStepsPerFrame` = 4): `Advance` adds the delta
and returns the whole steps that fit; the rest carries over to the next frame (`Alpha` = carried time / step). If more
than `MaxStepsPerFrame` steps are due, it returns `MaxStepsPerFrame` and **drops** the backlog, so after a long stall
the game slows down instead of catching up. A delta ≤ 0 or NaN adds nothing. At a high frame rate most frames run 0
fixed steps, so a system in `FixedSimulation` that reads a key's `Pressed` edge directly misses most presses: latch
input in `Simulation`, consume it in `FixedSimulation`. Asteroids.Ecs runs its physics there.

<a id="physics"></a>
**Physics** (assembly and namespace `ConsoleEngine.Physics`, optional: a game references it only if it needs physics;
plan `docs/design/physics-plan.md`, phases 0–3 done: detection, events and the Soft Step contact solver of Box2D v3,
without rotation). Units are console cells on both axes (a circle looks twice as tall as wide on screen unless the font
is square, as in PhysicsSandbox).
- `RigidBody` (`RigidBody.Dynamic(mass, gravityScale, linearDamping)`, `.Kinematic()`, `.Static()`; `default` is
  static). `Collider` (`Collider.Circle(radius, offset)`, `Collider.Box(halfExtents, offset)`, then `with { … }`):
  shape, `Category`/`Mask` bits (factory default: category 1, collides with all), `IsSensor`, `Friction` (factory
  default `DefaultFriction` 0.6), `Restitution` (default 0). A `Collider` without a `RigidBody` is a static body.
  `default(Collider)` has `Mask` 0 and collides with nothing.
- Game systems read the events through `IPhysicsEvents` (`PhysicsSystem` implements it), so another physics engine
  could stand in behind the same components and events.
- A dynamic or kinematic body needs a `Velocity`; without one it is handled as **static** (never moves, never pushed).
- `PhysicsSystem` (add it to `FixedSimulation`; `DefaultCellSize` 8 cells; `Gravity` default 0; `SubstepCount` 4;
  `ContactHertz` 30, capped at a quarter of the substep rate, doubled for contacts with a static or kinematic body;
  `ContactDampingRatio` 10; `ContactPushMaxSpeed` 3 cells/s; `RestitutionThreshold` 1 cell/s — all Box2D's defaults
  with 1 cell = 1 Box2D length unit). Each step is split into `SubstepCount` substeps of `h = step / SubstepCount`;
  per substep, dynamic bodies get `v = v / (1 + h * LinearDamping) + Gravity * GravityScale * h`, contacts push, and
  non-static bodies move `p += v * h`. Rigid bodies without a `Collider` get the same substeps without contacts.
- A pair is tested only if `(a.Category & b.Mask) != 0` and `(b.Category & a.Mask) != 0`, not both static, not both
  sensors, and, if neither is a sensor, at least one is dynamic (kinematic–kinematic and kinematic–static never touch).
- Contact response: dynamic bodies don't pass through each other or through static/kinematic ones. Static and
  kinematic bodies are never pushed. Overlaps are pushed out at most `ContactPushMaxSpeed`, and that push adds no
  velocity. Bodies settle about `LinearSlop` (0.005 cells) into each other per contact, so a stack sinks a little.
  Friction per pair = `sqrt(a.Friction * b.Friction)`, restitution = `max(a.Restitution, b.Restitution)`; impacts
  slower than `RestitutionThreshold` don't bounce. Contacts start before the shapes touch (speculative: when the gap
  is less than how far both can move this step), so fast bodies don't tunnel through thin walls.
- **Tile grid**: `PhysicsSystem.TileGrid` (optional `TileGrid`: columns × rows of solid/empty tiles, `TileSize`, `Origin`,
  `Category`). Queries and `CharacterMover` collide with it; **dynamic bodies don't** (phase 4 limit: use static box
  colliders for them). Outside the grid is empty. Touching a tile's edge is not overlapping it.
- **Queries** (`IPhysicsQueries`, implemented by `PhysicsSystem`): `Raycast` (closest collider or tile; shapes that
  contain the origin are ignored; a ray starting inside solid tiles hits the first solid tile after it leaves them),
  `CastBox` (an axis-aligned box swept along a translation; a shape it already overlaps is hit at fraction 0 when moving
  further in and ignored when moving out; tile faces that touch another solid tile are never hit, so boxes don't catch on
  seams), `OverlapBox`/`OverlapCircle` (colliders only, up to the span's length). A collider is reported if its
  `Category` has a bit in the query mask; sensors never. Queries see the world **as at the end of the last physics step**.
- **`CharacterMover.MoveAndSlide`** (Godot `move_and_slide`): sweeps a box by `velocity * delta` with `CastBox`, stops
  `SkinWidth` (0.01 cells) before what it hits, removes the velocity part into the surface and slides with the rest, at
  most `MaxSlides` (4) hits per move. Returns `SlideCollisions`: `Floor` (normal within 45° of `up`, default
  `(0, -1)`), `Ceiling`, `Wall`. A character has no `RigidBody`, so the physics never moves it.
- "Touching" (for events and `TouchingPairCount`) = separation below `TouchDistance` (0.02 cells), so a resting
  contact stays touching instead of flickering.
- Events (`ContactBeginEvents`, `ContactEndEvents`, `SensorBeginEvents`, `SensorEndEvents`) hold the last step's
  changes only and are replaced every step. A begin fires once when a pair starts touching; an end fires when it stops
  **or when either entity was destroyed** (the entity in the event is then dead). In contact events `A` has the lower
  entity id. Read them from a system added after `PhysicsSystem` in `FixedSimulation`; a system in `Simulation` sees
  the same events again in frames without a fixed step.

**Built-in systems** (namespace `ConsoleEngine.Ecs.Systems`, components in `ConsoleEngine.Ecs.Components`):
- `MovementSystem`: `Position += Velocity * delta` for every entity with both, or only those that also match the
  filter passed to its constructor (`new MovementSystem(Query.Without<X>())`). No wrapping, no bounds. The engine knows
  nothing about physics: a game that uses `PhysicsSystem` must add `new MovementSystem(Query.Without<RigidBody>())`,
  otherwise rigid bodies are moved twice (once per frame here, once per fixed step there).
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
| Platformer.Ecs | Same keys, start state (debug on) and draw order (debug text, player, then tiles on top) as Platformer. Velocity is in cells/s: gravity 200, run 150 (ground) / 75 (air) cells/s², drag (3, 0.1)/s, jump speed 50 cells/s (about 6 cells high at any FPS). The player moves in fixed steps of 1/60 s; A/D and SPACE are read every frame and latched (`PlayerBody.RunDirection`, `JumpRequested`) | `PlayerControlSystem`, `PlayerPhysicsSystem`, `PlatformerScene` |
| Platformer.Ecs | Collisions go through the engine physics: the level is a `TileGrid` (`TileMap.ToTileGrid`, 4×4-cell tiles, outside the map is empty) and the player a 3×3 box moved by `CharacterMover.MoveAndSlide`, which stops at walls and slides along them. Landing (a `Floor` hit) ends `IsAirborne`. Differs from Platformer: horizontal speed under 1 cell/s is zeroed only when A/D is not held; `Position` is rounded to whole cells after every move (the exact remainder is kept in `PlayerBody.SubCell`), so the player is drawn touching walls and floors although the mover stops `SkinWidth` (0.01) before them. Kept from the original: walking off a ledge does not make the player airborne, and a jump pressed in the air happens on landing | `PlayerPhysicsSystem`, `CharacterMover`, `TileGrid` |
| Asteroids | Steer `A`/`D`, thrust `W`. No collisions, no objective. Logger enabled but nothing is logged | `AsteroidsGame` |
| Asteroids.Ecs | Steer `A`/`D` (`Ship.SteeringSpeed` 2π rad/s), thrust `W` (`Ship.Thrust` 60 cells/s²), drag `Ship.Drag` 1.5/s. All movement is scaled by `Delta` | `ShipControlSystem` |
| Asteroids.Ecs | `SPACE` (Pressed) fires one bullet: 80 cells/s plus the ship's velocity, lives 1.5 s | `ShipControlSystem`, `Spawner.CreateBullet`, `LifetimeSystem` |
| Asteroids.Ecs | A bullet (kinematic sensor, `CollisionLayers.Bullets`) that touches an asteroid (dynamic, `CollisionLayers.Asteroids`) destroys both, on the `SensorBeginEvents` of the fixed step. One bullet touching two asteroids in the same step destroys both. Asteroids pass through each other. The ship has no collider and never collides. No score, no objective | `PhysicsSystem` (engine), `CollisionSystem`, `Spawner` |
| Asteroids.Ecs | Asteroids drift up to 6 cells/s. Asteroids and bullets move in fixed steps of 1/60 s (`PhysicsSystem`), the ship every frame (`MovementSystem`). Everything wraps around a playfield three screens wide and high, so the camera jumps when the ship crosses an edge | `PhysicsSystem`, `MovementSystem` (engine), `WrapSystem`, `Playfield.Wrap`, `CameraSystem` |
| Asteroids.Ecs | Ship and asteroids are outlines, bullets are `*` glyphs. Hits disappear at the end of the fixed step and expired bullets at the end of the simulation group (command buffer playback), so they are never drawn in the frame they die | `AsteroidsScene.OnCreate`, `OutlineRenderSystem`, `GlyphRenderSystem` |
| ConsoleGame | Recolours all cells every **500 ms**. `ESC` calls `Stop()`, which exits | `Game.OnInitialize`, `OnUpdate` |
| ConsoleGame.Ecs | One entity per cell (4096). Every **500 ms** (timer reset to 0) each gets a random colour of the same five. Cells are black until the first recolour. `ESC` quits | `RecolorSystem`, `ColorGridScene`, `ConsoleEcsGame.OnUpdate` |
| Swarm | Particles are pulled towards a point moving on a Lissajous curve, each with its own `Agility.Pull` (30–180 cells/s²), drag 0.3/s, and bounce off the screen edges. Colour by speed: dark blue < 15, blue < 30, cyan < 50 cells/s, else white. The HUD on row 0 shows the count | `AttractorSystem`, `BounceSystem`, `ColorBySpeedSystem`, `HudSystem` |
| Swarm | `SPACE` adds 10 000 particles, `BACKSPACE` removes up to 10 000 (both through the command buffer, applied at the end of the simulation group). `ESC` quits | `SpawnControlSystem`, `SwarmGame.OnUpdate` |
| PhysicsSandbox | Gravity 30 cells/s² down. Static floor (3 rows), side walls and two ledges (`█`), a kinematic platform (`▒`, 16×1) that moves at 12 cells/s between 20 % and 80 % of the width. `PhysicsSystem` cell size 2 | `SandboxScene`, `PatrolSystem` (fixed step, `[UpdateBefore(typeof(PhysicsSystem))]`) |
| PhysicsSandbox | `SPACE` drops a ball (`●`, radius 0.5, restitution 0.6, mass 1), `B` a box (`■`, 1×1, mass 3, no bounce), `ENTER` 50 balls stacked 1.2 cells apart, all at random x on row 3 with a random sideways speed up to 8 cells/s. `R` removes every dropped body. `ESC` quits. Dropping goes through the command buffer: the bodies appear at the end of the simulation group and move from the next fixed step. The HUD on row 0 shows the count | `DropSystem`, `BodySpawner`, `HudSystem`, `PhysicsSandboxGame.OnUpdate` |

<a id="launcher"></a>
## Launcher (`Examples/Launcher`)

| Rule | Identifier |
|---|---|
| Keys (read with `Console.ReadKey`, so only while the launcher window has focus): Up/Down or W/S or K/J move and wrap around, Home/End jump, Enter/Space start the selected game, digit 1–9 selects **and** starts that entry (digits above the entry count are ignored), Esc/Q quit | `LauncherMenu.HandleKey` |
| A game is "built" when its exe exists at the `GameLocator` path. Checked again before every redraw. Starting an unbuilt game shows `LauncherTexts.NotBuilt` and starts nothing | `LauncherApp.RefreshBuilt`, `LauncherApp.Launch` |
| Games start in their **own classic console window** (`conhost.exe`), working directory = the exe's folder, with no arguments (Asteroids.Ecs and Swarm use their default counts). The launcher blocks until the game exits; keys typed into the launcher meanwhile are discarded | `GameRunner.Run`, `LauncherApp.DiscardBufferedKeys` |
| `conhost.exe` always exits with 0, so the launcher cannot tell a crash from a normal quit: it always shows `LauncherTexts.Closed`. A game that exits non-zero (crash) leaves its window open on `pause` with the exception text above it. The logger window that a game opens (`Log.Start`, `UseShellExecute`) still follows the default terminal setting | `GameRunner.BuildArguments` |
| The launcher finds games only from its own build output: it walks up from `AppContext.BaseDirectory` to the folder holding `Launcher.csproj` and uses the same relative output path (configuration + framework) for each game | `GameLocator.FromLauncherOutput` |
