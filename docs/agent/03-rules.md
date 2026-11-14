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

<a id="console-output"></a>
### Console output (`ConsoleHandler.Render`, Windows)

- `Display` hands the whole frame to `ConsoleHandler.Render`, which converts it to `CharInfo`s and compares it with the
  frame it wrote last (`ConsoleHandler.FindChangedRows`). It writes only the rows from the **first to the last changed
  row**, in one `WriteConsoleOutput`. A frame equal to the last one writes nothing.
- All rows are written on the first frame, after `InitializeConsole`, and after a failed `WriteConsoleOutput`.
- Anything else that writes into the console buffer (e.g. `System.Console.Write`) is not noticed: those cells stay
  wrong until the game draws something different there.
- Why: conhost's cost scales with the rows written, unchanged or not, and it is almost the whole cost of a frame
  (numbers in `AGENTS.md` § Performance).
- **24-bit colour** (`SetTrueColor(true)`): `GetConsoleMode`, then `SetConsoleMode` with
  `ENABLE_VIRTUAL_TERMINAL_PROCESSING` and `ENABLE_PROCESSED_OUTPUT` on and `ENABLE_WRAP_AT_EOL_OUTPUT` off (writing
  the last column never scrolls). Either call failing returns false (console stays as it was, palette output). The
  16 colours for `PaletteColor` cells are read from the console's colour table (`GetConsoleScreenBufferInfoEx`, again
  after `SetPalette`). `SetTrueColor(false)` gives the original mode back; so do `Close()` and `ProcessExit`.
- While it is on, `Render(pixels, foregroundRgb, backgroundRgb)` builds the frame with `VtFrame.Build`: only cells
  whose character or colours changed since the last written frame, `ESC[row;colH` before each run of changed cells,
  `ESC[38;2;r;g;b` / `48;2;r;g;b` only when the colour differs from the previous written cell (only the layer that
  changed), control characters and NUL as spaces. One `WriteConsoleW`; a failed or short write makes the next frame
  write every cell. A frame equal to the last one writes nothing. Switching between palette and VT output rewrites
  everything on the next frame.

<a id="true-colour"></a>
### True colour (`RenderConsole.TrueColor`, `DrawRgb`)

- Off by default: palette games never call `IConsoleHandler.SetTrueColor` and render through `Render(pixels)`.
- `RenderConsole.TrueColor = true` asks for 24-bit colour: in `Initialize` (after `SetPalette`) or, while the game
  runs, right away. `TrueColorActive` is the handler's answer (`SetTrueColor`); setting `TrueColor = false` turns it
  off again.
- While `TrueColorActive`: `DrawRgb(x, y, char, foregroundRgb, backgroundRgb)` stores `0xRRGGBB` (top byte dropped)
  per cell; every `ConsoleColor` draw (`Draw`, `DrawLine`, `Draw(Span<string>)`, `Clear`, `GlyphRenderSystem`,
  `SpriteRenderSystem`, Lua `console.draw`) stores `IConsoleHandler.PaletteColor`, so the last draw of a cell wins.
  Turning it on marks every cell `PaletteColor`. `Display` calls `Render(pixels, foregroundRgb, backgroundRgb)`.
- While not active (off, or the console can't): `DrawRgb` stores the nearest `ConsoleColor` by squared RGB distance,
  over `Palette` or, without one, the Windows defaults (Campbell). Out-of-screen `DrawRgb` is ignored, like `Draw`.
- Handlers: `IConsoleHandler.SetTrueColor` and the RGB `Render` are default interface methods (`false`, and
  `Render(pixels)`). `CrossPlatformConsoleHandler` keeps the defaults, so `DrawRgb` shows the nearest palette colour
  there.

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

<a id="logger"></a>
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
plan `docs/design/physics-plan.md`, phases 0–5 done). Units are console cells on both axes (a circle looks twice as tall
as wide on screen unless the font is square, as in PhysicsSandbox and PhysicsShowcase).
- `RigidBody` (`RigidBody.Dynamic(mass, gravityScale, linearDamping, angularDamping)`, `.Kinematic()`, `.Static()`;
  `default` is static). `Collider` (`Collider.Circle(radius, offset)`, `Collider.Box(halfExtents, offset)`,
  `Collider.Polygon(polygonId)`, then `with { … }`): shape, `Category`/`Mask` bits (factory default: category 1,
  collides with all), `IsSensor`, `Friction` (factory default `DefaultFriction` 0.6), `Restitution` (default 0). A
  `Collider` without a `RigidBody` is a static body. `default(Collider)` has `Mask` 0 and collides with nothing.
- **Polygons**: convex, at most 8 corners, added once with `PhysicsSystem.Polygons.Add(vertices)` (the convex hull is
  kept; vertices are relative to `Position`). Fewer than 3 points, points on one line, or more than 8 hull corners throw
  `ArgumentException`.
- **Rotation**: a body turns only if it has a `Rotation` **and** an `AngularVelocity`; without them its rotation stays
  fixed (a `Rotation` alone still turns its shape, e.g. a static ramp). Rotation is clockwise on screen, around
  `Position`; the rotational inertia comes from the shape and mass (uniform density, about `Position`). Kinematic
  bodies with an `AngularVelocity` turn at that speed.
- Game systems read the events through `IPhysicsEvents` (`PhysicsSystem` implements it), so another physics engine
  could stand in behind the same components and events.
- A dynamic or kinematic body needs a `Velocity`; without one it is handled as **static** (never moves, never pushed).
- `PhysicsSystem` (add it to `FixedSimulation`; `DefaultCellSize` 8 cells; `Gravity` default 0; `SubstepCount` 4;
  `ContactHertz` 30, capped at a quarter of the substep rate, doubled for contacts with a static or kinematic body;
  `ContactDampingRatio` 10; `ContactPushMaxSpeed` 3 cells/s; `RestitutionThreshold` 1 cell/s; `EnableSleep` on;
  `TimeToSleep` 0.5 s — all Box2D's defaults with 1 cell = 1 Box2D length unit). Each step is split into `SubstepCount`
  substeps of `h = step / SubstepCount`; per substep, dynamic bodies get
  `v = v / (1 + h * LinearDamping) + (Gravity * GravityScale + Force.Value * InverseMass) * h` (and
  `w = w / (1 + h * AngularDamping) + Force.Torque * InverseInertia * h`), contacts push, and non-static bodies move
  and turn. Rigid bodies without a `Collider` get the same substeps without contacts.
- **Forces** (`Force` component, as Box2D v3 `b2Body_ApplyForce`/`ApplyTorque`, Unity `AddForce`/`AddTorque`): `Value`
  in world axes (mass × cells/s²) acts at `Position`; `Torque` is clockwise on screen like `Rotation`. Add to them with
  `Add`, `AddTorque` or `AddAtPoint(force, point, center)` (torque `+= cross(point - center, force)`: pushing down on
  the right side turns clockwise). `PhysicsSystem` holds them constant through every substep of the next step, then
  zeroes every `Force` (`ClearForces`), whatever the body. Only awake or woken dynamic bodies are pushed: a non-zero
  force or torque wakes a sleeping body (`ReadBody`); static and kinematic bodies ignore it. Torque is ignored by bodies
  without `Rotation` + `AngularVelocity` and by bodies without a `Collider` (no inertia). Systems that add forces run
  before `PhysicsSystem` in the same group (`[UpdateBefore(typeof(PhysicsSystem))]`); a force added after it in a
  step is applied in the next step.
- A pair is tested only if `(a.Category & b.Mask) != 0` and `(b.Category & a.Mask) != 0`, not both static, not both
  sensors, and, if neither is a sensor, at least one is dynamic (kinematic–kinematic and kinematic–static never touch).
- Contact response: dynamic bodies don't pass through each other, through static/kinematic ones or through solid
  tiles. Static and kinematic bodies are never pushed. A contact has up to 2 points (boxes and polygons resting flat get
  2), so turned bodies tip over and lie flat. Overlaps are pushed out at most `ContactPushMaxSpeed`, and that push adds
  no velocity. Bodies settle about `LinearSlop` (0.005 cells) into each other per contact, so a stack sinks a little.
  Friction per pair = `sqrt(a.Friction * b.Friction)` (a tile contact uses the body's), restitution =
  `max(a.Restitution, b.Restitution)`; impacts slower than `RestitutionThreshold` don't bounce. Friction makes balls
  roll. Contacts start before the shapes touch (speculative: when the gap is less than how far both can move this
  step), so fast bodies don't tunnel through thin walls.
- **Tile grid**: `PhysicsSystem.TileGrid` (optional `TileGrid`: columns × rows of solid/empty tiles, `TileSize`,
  `Origin`, `Category`). Dynamic bodies whose `Mask` includes the grid's `Category`, the queries and `CharacterMover`
  collide with it. Outside the grid is empty. Touching a tile's edge is not overlapping it. A tile face that touches
  another solid tile is never collided with, so bodies slide over seams: boxes and polygons are only ever pushed out
  through a tile's open faces (`Collision.PolygonTile`), whatever their angle; a circle's contact is dropped when it would
  push out through a closed face. Tile contacts have **no events**.
- **Sleeping** (`EnableSleep`): a dynamic body is still while slower than `LinearSleepTolerance` (0.05 cells/s) and
  `AngularSleepTolerance` (2°/s). Touching dynamic bodies form an island; an island whose bodies have all been still
  for `TimeToSleep` sleeps: `RigidBody.IsSleeping` is set, velocities are zeroed, and its bodies aren't simulated.
  It wakes when a moving body of the island or a moving kinematic body touches it, when one of its contacts ends (a
  support moved away or was destroyed), when game code gives one of its bodies a velocity, or when `IsSleeping` is
  cleared. **Changing `Gravity` doesn't wake sleeping bodies** (as Box2D): clear `IsSleeping` yourself.
  `SleepingBodyCount` counts them. Sleeping bodies still cost their gather and broadphase each step.
- **Interpolation** (opt-in): a body with `Interpolation` and the engine's `RenderOffset` gets `PreviousPosition` and
  `PreviousRadians` set each step; `InterpolationSystem(FixedTimestep)` in the presentation group (before the render
  systems) sets `RenderOffset = (previous - current) × (1 - FixedTimestep.Alpha)`, so the body is drawn between its last
  two steps (up to one step behind, without jumps). Nothing is offset before the first step (`HasPrevious`). After a
  teleport, call `Interpolation.Reset()`.
- **Queries** (`IPhysicsQueries`, implemented by `PhysicsSystem`): `Raycast` (closest collider or tile; shapes that
  contain the origin are ignored; a ray starting inside solid tiles hits the first solid tile after it leaves them),
  `CastBox` (an axis-aligned box swept along a translation, against circles, turned boxes and polygons too; a shape it
  already overlaps is hit at fraction 0 when moving further in and ignored when moving out; tile faces that touch
  another solid tile are never hit, so boxes don't catch on seams), `OverlapBox`/`OverlapCircle` (colliders only, up to
  the span's length). A collider is reported if its `Category` has a bit in the query mask; sensors never. Queries see
  the world **as at the end of the last physics step**.
- **`CharacterMover.MoveAndSlide`** (Godot `move_and_slide`): sweeps a box by `velocity * delta` with `CastBox`, stops
  `SkinWidth` (0.01 cells) before what it hits, removes the velocity part into the surface and slides with the rest, at
  most `MaxSlides` (4) hits per move. Returns `SlideCollisions`: `Floor` (normal within 45° of `up`, default
  `(0, -1)`), `Ceiling`, `Wall`. A character has no `RigidBody`, so the physics never moves it.
- "Touching" (for events and `TouchingPairCount`) = separation below `TouchDistance` (0.02 cells), so a resting
  contact stays touching instead of flickering.
- Events (`ContactBeginEvents`, `ContactEndEvents`, `SensorBeginEvents`, `SensorEndEvents`) hold the last step's
  changes only and are replaced every step. A begin fires once when a pair starts touching; an end fires when it stops
  **or when either entity was destroyed** (the entity in the event is then dead). A sleeping pair stays touching (no
  end). In contact events `A` has the lower entity id. Read them from a system added after `PhysicsSystem` in
  `FixedSimulation`; a system in `Simulation` sees the same events again in frames without a fixed step.

**Built-in systems** (namespace `ConsoleEngine.Ecs.Systems`, components in `ConsoleEngine.Ecs.Components`):
- `MovementSystem`: `Position += Velocity * delta` for every entity with both, or only those that also match the
  filter passed to its constructor (`new MovementSystem(Query.Without<X>())`). No wrapping, no bounds. The engine knows
  nothing about physics: a game that uses `PhysicsSystem` must add `new MovementSystem(Query.Without<RigidBody>())`,
  otherwise rigid bodies are moved twice (once per frame here, once per fixed step there).
- `GlyphRenderSystem`: draws `Glyph.Character` at `floor(Position - Camera.Position)`. Off-screen cells are clipped
  by `RenderConsole.Draw`.
- All three render systems add the entity's `RenderOffset` (position, and radians for outlines) when it has one: drawn
  there, not moved (used by physics interpolation, or for effects).
- `OutlineRenderSystem`: draws the `OutlineLibrary` polygon for `Outline.Id` around the position, rotated clockwise
  by `Rotation.Radians` if the entity has a `Rotation`. Skipped when its bounding circle (`OutlineLibrary.GetRadius`)
  is entirely off-screen.
- `SpriteRenderSystem`: draws the `SpriteLibrary` sprite for `SpriteRef.Id` with its top-left cell at
  `floor(Position - Camera.Position) + SpriteRef.Offset`. Skipped when the whole sprite is off-screen. The optional
  `QueryDescription` filter picks a layer (e.g. `With<Tile>()`): add one instance per layer in drawing order, because
  a query visits archetypes in no defined order.

<a id="lua"></a>
### Lua scripting (`ConsoleEngine.Scripting`)

- **Scripts are ECS behaviour.** `LuaRuntime.Load(path)` returns a `LuaScript`. Its `Component` (a `Script { Id }`
  component) on an entity makes that entity run the script. Nothing runs without the systems:
  `LuaScriptSystem(runtime)` in a simulation group runs `on_start`/`on_update`, `LuaRenderSystem(scriptSystem)` in a
  presentation group runs `on_render`. Use one `LuaScriptSystem` per `World`. In an `EcsScene` it goes in
  `Simulation` (once per frame, `dt` = frame delta) or, for scripts that steer physics bodies every step,
  `FixedSimulation` (`dt` = the fixed step). `Examples/Scripted` uses `Simulation`.
- A script file must **return a table** (its module). The module may define `on_start(self)`, `on_update(self, dt)`
  and `on_render(self)`. Missing hooks are skipped. Each entity gets its own `self` with metatable
  `{ __index = module, __newindex = guard }`, so `self:method()` calls module functions.
- `on_start` runs **once per entity, the first time `LuaScriptSystem` sees it**, in the same frame and just before its
  first `on_update`. An entity counts as started only when `on_start` succeeds (or the script has none): if
  `on_start` faults, it runs again after the script is reloaded. `on_render` is skipped until then. Entities created through a command buffer start in the next
  simulation group run.
- **`self`'s fields are the entity's components.** A field exists only when the entity has the component:

| Field | Type | Component |
|---|---|---|
| `self.x`, `self.y` | number | `Position.Value` (stored as `float`) |
| `self.vx`, `self.vy` | number, cells per second | `Velocity.Value` (applied by `MovementSystem` if the scene has it) |
| `self.char` | string (first character used) or code point | `Glyph.Character` |
| `self.color`, `self.background` | number (`colors.*`, masked to 0–15) | `Glyph.Foreground`, `Glyph.Background` |
| `self.data` | table owned by the script, starts empty, kept across reloads | — |

- The fields are copied into `self` **before every hook** and back into the components **after `on_start` and
  `on_update`**. Changes made in `on_render` are not copied back. Other components (`Rotation`, `SpriteRef`, game
  components) are not visible to Lua.
- When an entity loses a component, its fields are removed from `self` on the next hook.
- Writing a field of a component the entity lacks is an error: `self.vx needs a Velocity component on the entity`.
  Writing any other unknown field (`self.X = 1`, `self.speed = 2`) is an error:
  `unknown field '<key>' on an entity, put script data in self.data`. Reading an unknown field returns the module's
  value or `nil` (no error).
- If a hook stores the wrong type in a field (`self.x = "left"`), no component is written and the entity faults with
  `self.<field> must be a number` or `self.char must be a string or a code point`.
- `dt` is the delta the group passes to its systems: the frame delta in `Simulation`, the fixed step in
  `FixedSimulation`. Scripts must scale movement by it ([§ FPS](#fps)).
- **Sandbox.** Scripts get only the Lua 5.4 base library and `coroutine`, `string`, `table`, `math` and `utf8`
  (`LuaRuntime.OpenSandboxedLibraries`). `io`, `os`, `debug`, `package` and `require` do not exist, and
  `LuaRuntime.RemovedGlobals` removes `dofile`, `loadfile`, `load` and `collectgarbage` from the base library. Using
  one is a normal Lua error (`attempt to index a nil value (global 'os')`, `attempt to call a nil value (global
  'load')`) and faults the entity. Scripts cannot reach the file system, start processes, exit the game or load
  other code; time comes from `dt`.
- Lua API:

| Lua | Behaviour |
|---|---|
| `input.held(key)`, `input.pressed(key)`, `input.released(key)` | `KeyState.Held`/`Pressed`/`Released` of that virtual key ([§ Keys](#keys)). A key outside 0–255 returns `false` |
| `keys.LEFT`, `keys.A`, … | Every `Key` enum name → its virtual key code |
| `colors.Red`, … | Every `ConsoleColor` name → its number |
| `console.draw(x, y, char, fg, bg)` | `RenderConsole.Draw`. `x`/`y` are **floored**. `char` is a string (its first character is used) or a code point. `fg` defaults to White, `bg` to Black. Characters outside the BMP become `?`. Off-screen is clipped |
| `console.text(x, y, text, fg, bg)` | Draws a string or number left to right, **at most 256 characters** (the rest is dropped) |
| `console.width`, `console.height` | Set once when the runtime is created |
| `print(...)` | `Log.WriteLine("Lua: " + args joined by tab)`. Dropped unless `EnableLogger` ([§ Logger](#logger)) |

- **Errors.** A Lua error in a hook, or a .NET exception in a host function, **faults the entity**: the error text
  (Lua's `name:line: message`, or `ExceptionType: message`) is kept for `LuaScriptSystem.GetError(entity)`,
  `Log.WriteLine("Lua: " + error)` is called, and the tag `ScriptFaulted` is added through the group's command
  buffer. The script query excludes `ScriptFaulted`, so none of its hooks run. Its components keep their last good
  values and C# systems (movement, rendering) still process it. A host-function exception does not stop the Lua code
  immediately: the hook runs to its end with the failing call returning nothing, then the entity faults.
- `ScriptFaulted` is removed (and the error cleared) in the first `LuaScriptSystem` update after a successful
  `Reload` of that entity's script. The entity runs again in the frame after that. Game code may also remove the tag
  itself (`Remove<ScriptFaulted>`): the entity then runs again and its error is cleared.
- **Reload.** `LuaScript.Reload` / `LuaRuntime.ReloadAll` re-read the file from `FullPath` (the copy in the output
  folder, not the project source), run it, and make the existing module table equal to the new one: fields
  **removed** from the new version are removed from the module (also values a script stored on the module at run
  time), then every new field is copied in. Running entities keep their `self.data` and use the new functions.
  `on_start` does **not** run again for started entities. A failing reload throws (`ReloadAll` returns the failures)
  and the previous version stays active.
- Lua shortens chunk names longer than 60 bytes in error messages to `...<end of path>`.
- A UTF-8 byte order mark at the start of a script is skipped.
- **Entity lifetime.** `self` is kept per `Entity.Id` and `Entity.Version`. A destroyed entity's `self` is freed when
  its id is reused; a reused id gets a new `self` and runs `on_start` again. Giving an entity a `Script` for another
  script also starts fresh. Removing `Script` and adding the same one back keeps the old `self` (no new `on_start`).

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
| Scripted | ECS scene. Arrows move the player (`@`, yellow, starts at `Console.ScreenCenter`) at 20 cells/s, kept inside rows 1 to height−2. 30 fireflies (`•`, positions from `Random(1)`): `on_start` gives each a random `Velocity` (`math.randomseed(1)`), `MovementSystem` moves them, the script reverses `vx`/`vy` at the edges and switches `self.color` Green/DarkGreen every 0.3 s. Which glyph wins where player and firefly overlap is not defined (archetype order). No collisions, no objective. `ESC` quits | `player.lua`, `firefly.lua`, `ScriptedScene.OnCreate`, `ScriptedGame.OnUpdate` |
| Scripted | `F5` (`Pressed`) calls `LuaRuntime.ReloadAll()`. Failures go to `Log.WriteLine`, which is dropped because `EnableLogger` is off. The error of the first `ScriptFaulted` entity found is drawn on the bottom row (white on dark red) | `ScriptReloadSystem`, `ScriptErrorSystem` |
| Kartster | Flow (all `Scenes.Set`): starts on `TitleScene` → Enter `SelectKartScene` (the kart loads as it is highlighted: `KartsterGame.SelectKart`, tuning `KartCatalog.ForKart(folder name)`) → Enter `SelectTrackScene` → Enter `KartsterGame.StartRace(track, laps)` (loads `<id>.ribbon.obj`/`spline`/`scenery` and `<id>.race.txt` when the track changes; `Markers` null without race.txt) → `TrackScene(laps)`. The race is over for the player `RaceReport.ResultsDelaySeconds` = 4 s (Kartster's `RacingScreen.ResultsDelaySeconds`) after they finish: then `RaceSession.Stop()` (karts still out: DNF; Kartster instead lets the autopilot and `FinishRemaining` finish them) and `ResultsScene(RaceReport.Results)`: one row per kart in race order (position, name, finish time, best lap, `Finished`). In the race R restarts `TrackScene` with the same laps, Esc goes to `TitleScene` (no pause). `TitleScene` Space → `GarageScene`; garage Esc → `TitleScene`, Tab → next kart (chosen for the race too). G and C are the only keys `KartsterGame.OnUpdate` reads, in every scene; a scene's other keys act only in it. Track mode: an `EcsScene` with the player and `TrackScene.DefaultBotCount` (5) bots, each kart one physics body (`KartEntity.Create`: `Position` = (world X, world Z), `Rotation`, `Velocity`, `AngularVelocity`, `RigidBody.Dynamic(Mass, gravityScale 0)`, `Collider.Box(chassis width/2, length/2)` with `Restitution` 0.1 and `Friction` 0.3 (Kartster's `RigidBodyKartTuning`), `Force`, `Interpolation`, `RenderOffset`, `Kart` (`Index` = grid slot = index into the per-kart `KartDynamics`, plans and meshes), `KartControls`; plus `PlayerKart` or `Bot`), each with a `RaceProgress`, all standing still on the grid (see the grid row). `FixedTimestep(1/120 s, 8)`; fixed step (`TrackScene.AddDriving`): `BotDriverSystem` (bots' `KartControls`) → `KartPhysicsSystem` (Kartster's model, one `KartDynamics` per kart, `KartDynamics.ComputeForces`, once per step, into `Force`) → `PhysicsSystem` (`SubstepCount = 1`, `EnableSleep = false`, no gravity) → `KartGroundSystem` (every kart: clamps it 1 m inside the grass square and zeroes the outward velocity, sets `Kart.Height` and `Kart.Surface`: Asphalt above an up-facing road triangle (`Track.TryGetRoadHeight`, every triangle tested), else Grass, used by the next step) → `RaceSystem` (see the race row). Frames (`KartFrame`): heading = −`Rotation.Radians`, kart yaw rate = −`AngularVelocity`, physics torque = −kart torque ÷ `YawInertiaScale` (box inertia m(w²+l²)/12 = `KartDynamics.YawInertia` at scale 1), forces unchanged. Every frame `KartInputSystem` latches (all zero, wheel straight, while `RaceSession.CanDrive` is false), for the `PlayerKart` only, W (throttle 1), S (brake 1, reverse once stopped), Space (handbrake) and A/D into `KartControls`: steering ramps 0.25 s centre to full lock, 0.12 s back towards centre, at most 0.1 s of ramp per frame (`KartInputSystem.RampSteer`). No walls; karts collide with each other through their boxes (the solver's soft contacts: 30 Hz at one substep, damping ratio 10, push-out at most 3 m/s; head-on and pushing karts gain no energy, `RaceTests`). Chase camera 5.5 m behind and 2.2 m up, looks 4 m ahead, eases towards its place at stiffness 6/s, follows the player's interpolated kart (`InterpolationSystem`); every kart is drawn, each with its own mesh (`KartRenderSystem`). Road triangles facing up are asphalt, others wall colour; flat chequered grass (8 m tiles) 0.3 m under the lowest road point, 40 m beyond it | `KartsterGame`, `TrackScene`, `KartPhysicsSystem`, `KartGroundSystem`, `KartInputSystem`, `KartRenderSystem`, `KartFrame`, `Track` |
| Kartster | Starting grid (`TrackScene.StartingSlot`): slot = `Kart.Index` = race id, the track's exported grid (`RaceMarkers.Grid`, `grid x y heading` lines of `<id>.race.txt`, front first; Star Circuit: 8 slots in two staggered columns 3 m apart, 1.5 m between slots, behind the start line), position (world X, world Z) and heading as exported, `Kart.Height` from `Track.TryGetRoadHeight`. Past the exported slots (as Kartster's `ExtendGrid`): 6 m (`GridRowSpacing`) behind the kart two slots ahead. A track without `race.txt` (`KartsterGame.Markers` null) uses `TrackScene.StandInGridSlot`: two karts a row, even slots 2 m left of the centerline, odd 2 m right, rows 6 m apart, row 0 at route distance −3 m, facing along the route, and a start line 20 m either side of route distance 0 with no checkpoints (`TrackScene.CreateSession`). The player takes slot 0 (`TrackScene.PlayerSlot`, pole), as Kartster puts the keyboard player before the bots (`QuickRace.AddBots`, `RaceSimulation.LayOutStartingGrid` in player order); bots slots 1..5. Kartster's moving back for long karts (`StartingGrid.GetShifts`) is not done. Bot karts take the game's kart folders (`KartsterGame.KartFolders`, the first argument's sub-folders) in turn, `KartCatalog.ForKart(folder name)` tuning, each folder loaded once (`KartModel.Load` with a key colour) and recoloured per bot (`KartModel.Recolor`) in `ScenePalette` colours, by bot number: brown `0xB06A30`, white `0xF0F0F0`, near-black `0x2B2B2D`, navy `0x0F2A78`, pale blue `0xA2BEF0`; the player keeps the chosen kart in blue | `TrackScene`, `KartModel` |
| Kartster | Bots (`BotDriverSystem`, fixed step before `KartPhysicsSystem`; a simple Earl–Merlin of Kartster's built-in bots). `Bot.Skill` = 0.8 at the front bot down to 0.3 at the last (`TopBotSkill`, `BotSkillSpread`); each skill lerps Earl→Merlin-like numbers: throttle cap 0.85→1, corner speed factor 0.7→0.85, braking share 0.6→0.75, lookahead 0.6→0.45 s (at least 5 m), reaction 0.8→0.4 s, rejoin speed 8→14 m/s. Lanes (`Bot.LaneOffset`, metres right of the centerline, by bot number): −1.2, 1.2, −0.4, 0.4, −2, 2. Plan (`BotRoutePlan`, made on the bot's first step): centerline samples ~2 m apart; corner speed √(`TyreFriction` × 9.81 / curvature) × corner factor, curvature = turn over 10 m each way, capped at the kart's top speed, then walked backwards twice round so it can brake to the next at braking share × `BrakeForceNewtons` / `Mass`. Each step: nearest sample (searched 8 behind to 24 ahead of the last, everywhere when > 15 m off); steer by pure pursuit at the lane point lookahead ahead (wheel angle atan(2 × wheelbase × sin α / distance) ÷ the lock at that speed, `KartDynamics.GetSteerAngleDegrees`), ramped by `KartInputSystem.RampSteer`; target speed = lower of the plan here and 0.25 s ahead; full throttle 2 m/s under, full brake 3 m/s over, linear between; no handbrake. Off asphalt longer than reaction: aims 12 m ahead at the centerline, at most rejoin speed. Aim behind: full lock towards it, at most 6 m/s. Stuck (throttle ≥ 0.3 and under 1 m/s for 1.5 s): brake (= reverse) 1.2 s with the steering mirrored. During the race countdown (`RaceSession.CanDrive` false) every bot's `KartControls` are zero and it decides nothing. No traffic avoidance, bumping or power-ups. Pace on Star Circuit (1 308 m), olabil alone from standing: skill 0 ≈ 61 s, 0.5 ≈ 57 s, 1 ≈ 54 s a lap (first lap included) | `BotDriverSystem`, `BotRoutePlan`, `Bot` |
| Kartster | Renderer: the CPU `Rasterizer` unless started with `--gpu` or switched with G. G makes the `GlRenderer` the first time (`GlRenderer.TryCreate`, on the game-loop thread) and disposes it when switched off; if OpenGL 3.3 is missing it stays on the CPU and the title says `CPU (no GPU)`. Both draw the same picture. The title's `3D … ms` is the time from `Renderer.Clear` (end of `OnUpdate`) to after `Renderer.Finish` (start of `OnRender`), smoothed 5 % per frame | `KartsterGame` |
| Kartster | Colours: 16 (`ScenePalette`, dithered sky and shading, screen-door smoke) by default; 24-bit with `--truecolor` or C, which flips `Console.TrueColor`. Each frame `KartsterGame.MatchConsoleColors` makes both renderers match `Console.TrueColorActive`, recreating the `Rasterizer` (and the `GlRenderer`, if on) with `trueColor` when it differs, so a console without 24-bit stays on 16 colours (title `16 colours (no 24-bit)`). In 24-bit the sky is `KartsterGame.SkyRgb` (gradient `SkyTop` → `SkyHorizon`, no dither), smoke is alpha-blended and still drawn last, and `OnRender` copies `Renderer.Rgb` with `Console.DrawRgb` | `KartsterGame` |
| Kartster | Garage (`GarageScene`, Space on the title): camera orbits the kart's bounding-box centre: yaw, pitch clamped 0.05–1.5 rad, distance clamped 1.5–12 m, FOV 50°, near 0.2, far 400. Auto-spin 0.5 rad/s (on at start). Kartster axes: 1 unit = 1 m, +Y up, -Z forward. Sun from (0.5, 0.8, 0.3), flat light = 0.35 + 0.65 × max(0, n·sun) per triangle. A new `GarageScene` starts at yaw 0.8, pitch 0.35, distance 5 | `GarageScene` |
| Kartster | Scenery: `Track.Load` reads `<id>.scenery.txt` beside the ribbon if it exists (no file: `Scenery.Empty`), models from the `Scenery` folder beside the tracks folder. Lines: `model <name> x y z yawDegrees scaleX scaleY scaleZ r g b` (`<name>.obj`, scale, then yaw counter-clockwise seen from above, then move; colour 0..1) and `tri x1 y1 z1 x2 y2 z2 x3 y3 z3 r g b` (counter-clockwise from the front); `#` starts a comment, blank lines are skipped. One `Mesh` per (model, colour); `tri`s go into 32 m square cells (by centroid), one mesh each. A point or instance at \|y\| < 1 mm (Kartster's ground) moves down to `Track.GroundHeight`. Drawn by `KartRenderSystem` after the road, before skid marks, kart and smoke: `Scenery.Draw` skips a mesh whose bounding sphere is farther than `Scenery.DrawDistance` (200 m) plus its radius from the eye, or outside the frustum's left/right/bottom/top/near planes; no allocation. Drawn only: what karts hit is the collision file (Walls row). Star Circuit's file (written by Kartster's own `TrackFenceMesh`, `TrackPipes.Place`, `TrackSigns.Place` + `TrackSignMesh`, `RaceMarkings.Place` + `RaceLineMesh`, `GridMarkMesh`, `TrackPowerUpPadResolver`): fences baked into 1 m tiles of the fence texture's four colours (top in their average), 10 pipes, the sign (plate colour, no text), the chequered start/finish band (0.4 m squares), grid marks, 9 power-up boxes (1.2 m, `Box.obj`, lit, still: no spin, bob or pickup) | `Scenery`, `Track`, `KartRenderSystem` |
| Kartster | Walls (`Collision/TrackCollision`, one call in `TrackScene.OnCreate`): `TrackCollision.Load` reads `<id>.collision.txt` beside the ribbon if it exists (no file: `TrackCollision.Empty`, no walls). Lines (metres, plane x = world X, y = world Z; `#` comments, blank lines skipped): `segment x1 y1 x2 y2 halfThickness` (a wall box centred on the line, ending at its end points) and `circle x y radius` (a post). `AddTo(World)` makes each a static body (`Position` + `Collider`, no `RigidBody`): a box of half extents (length / 2, halfThickness) turned by `Rotation` = atan2(dy, dx), or a circle. Kartster resolves its collision mesh (`RaceSimulation.ResolveKartVsCollisionMesh`: impulses against a zero-mass body, deepest point, 4 solver iterations) with the kart's own `RigidBodyKartTuning.Restitution` 0.1 and `Friction` 0.3, so walls carry `TrackCollision.Friction` 0.3 and `Restitution` 0.1: the pair gets sqrt(0.3 × 0.3) = 0.3 and max(0.1, 0.1) = 0.1. (`WallFriction` 0.6 is only for Kartster's road-edge walls, `KartWallConstraint`; Star Circuit has none, its sections have `WallHeight` 0.) No other speed-loss rule: speed goes through restitution and Coulomb friction only. Height is ignored (Kartster's fences are 1.2 m, pipes 3 m tall; nothing on Star Circuit jumps them). Star Circuit (`star-circuit.collision.txt`, exported by `CollisionExport` in scratchpad from `TrackGround` + `TrackFenceMesh.ThicknessMeters` and `TrackPipes.Place`; it refuses tracks with edge walls, gates, route ends or trees/buildings): 4 outer-wall segments and 15 fences, all 0.25 m half thick, and 10 pipes of radius 1.2 m (Kartster: an eight-sided post with its corners on that radius). Power-up boxes are not walls. Measured (`TrackCollisionTests`): at 32 m/s full throttle into a fence the kart stops at the face and rebounds under 4.8 m/s (3.5); at 25 m/s onto a 45° wall it slides on (29 m along it in 1.5 s, 23 m/s); a pipe hit 0.8 m off centre pushes it aside; bots lap Star Circuit as before (they don't touch the walls); the 29 static bodies add about 6–10 µs to a 6-kart physics step, no allocation | `TrackCollision`, `WallSegment`, `WallPost`, `TrackScene` |
| Kartster | Kart parts: one OBJ per part, colour by the part's file-name prefix (`KartModel.PartColors`, Kartster's `KartPartColors` plus `wheel` = tyre, `dark`, `lamp_red`, `lamp_white`), others the player colour (blue). Parts starting with `seat`, `eye`, `wall`, `exhaust` or `light_` are markers and not drawn | `KartModel` |
| Kartster | Rasterizer (`ConsoleEngine.Rendering3D`, behind `IRenderer3D`; `KartsterGame.OnRender` calls `Finish` before copying `Colors`): back faces culled (OBJ counter-clockwise = front), depth test per pixel, triangles crossing the near plane are clipped to it (one or two triangles). Colours go to the 16 console colours by a 4×4 Bayer dither (strength 0.12) and a 32³ nearest-colour table of the palette in use. Kartster gives the console its own palette (`KartsterGame.ScenePalette`: 6 greys, 3 greens, 3 sky blues, 2 player blues, 1 brown `0xB06A30`, 1 yellow `0xFFD23F` = `UiColors.Selected`, so the UI's selection, logo and needle and the scenery's yellow kerbs stay yellow in 16 colours; the mid grey `0x949497` it replaced is dithered from `0x6B6B6E` and `0xC4C4C6`) through `RenderConsole.Palette`; a console that ignores it shows those indices as its default colours | `ConsoleEngine.Rendering3D`: `Rasterizer`, `ConsolePalette` |
| 3D (any game) | True-colour mode: `new Rasterizer(w, h, palette, trueColor: true)`, `new GlRenderer(…, trueColor: true)` or `GlRenderer.TryCreate(w, h, palette, true, out …)`; `IRenderer3D.TrueColor` is fixed at construction. The frame is `IRenderer3D.Rgb` (`0xRRGGBB`, `index = y * Width + x`, after `Finish`) and `Colors` is empty; in palette mode `Rgb` is empty. Frames start with `Clear(ReadOnlySpan<uint>)`; the `ConsoleColor` overload in true-colour mode, or the `uint` overload in palette mode, throws `InvalidOperationException`. Same light as palette mode, no dither or palette: pixel = clamp(colour, 0, 1) × 255 rounded. Per mesh the opaque triangles are drawn first, then those with `TriangleOpacity` < 1, blended `dst = src × o + dst × (1 − o)`, depth-tested but writing no depth (opacity ≤ 0 is skipped), so a farther opaque mesh drawn later covers the blend: draw see-through meshes last. CPU: a float RGB buffer, packed in `Finish`. GPU: an `RGBA8` framebuffer and its own fragment shader; `Clear` uploads the background to a texture and blits it in (flipped), blending is `SRC_ALPHA, ONE_MINUS_SRC_ALPHA` with `DepthMask(false)` in a second draw of meshes that have `TriangleOpacity`; `Finish` reads `BGRA`/`UnsignedInt8888Rev`, flips the rows and masks off alpha. GPU and CPU differ by at most 1 per channel (8-bit blending) | `Rasterizer`, `GlRenderer` |
| 3D (any game) | `GlRenderer` (`ConsoleEngine.Rendering3D.OpenGL`) draws what `Rasterizer` draws, on the GPU: OpenGL 3.3 core in a hidden GLFW window, an `R8UI` framebuffer of palette indices (255 = nothing drawn, replaced by the `Clear` background after the read-back in `Finish`) and 24-bit depth (`Less`, cleared to 1), back faces culled (counter-clockwise = front), z remapped `2z − w` so OpenGL's near-plane clipping matches. Shaders: light 0.35 + 0.65 × max(0, n·toLight) with n = normalised `mat3(world)` × the triangle's model-space normal (or the colour as is when `Mesh.IsUnlit`), then the palette's own dither offsets and 32³ nearest-colour table (a 3D texture), screen-door discard where `(bayer + 0.5) / 16 ≥ opacity`, with the Bayer cell counted from the top-left like the CPU. Each mesh is uploaded once (3 vertices per triangle) and again when `Mesh.Version` changed; meshes stay on the GPU until `Dispose`. Must be used from the thread that created it | `GlRenderer` |
| Kartster | Slide effects, once per frame for every kart from its `KartDynamics.FrontSlip`/`RearSlip` (Kartster's `KartAxleSlip`) at the drawn pose (`KartEffectsSystem`); all karts share one ring of marks and one pool of puffs (strips and puff rates kept per kart), so the cost doesn't grow with the karts. Skid marks (`SkidMarks`): from 2 m/s of rear slip, on asphalt only, a 0.25 m strip behind both rear wheels, the front wheels too when front slip ≥ 0.75 × rear (a spin); front slip alone (turning hard) leaves none; 0.5 m segments, a new strip after a jump > 5 m, 2 cm above the road, darkest (road colour × 0.35) at 10 m/s; a ring of 1 500 segments. Slide smoke (`SlideSmoke`): each wheel of an axle slipping past 0.1 m/s puffs up to 55 a second (full at 6.1 m/s), thrown out of the corner (slip × 0.3 m/s) and up (0.8–1.6 m/s), rising 0.4 m/s², drag 2.5/s, 0.7–1.1 s, growing 0.25 → 1.1 m, unlit, screen-door opacity 0.35 → 0; a pool of 300. Rasterizer: `Mesh.TriangleOpacity` draws a pixel where the 4×4 Bayer threshold is below the opacity; `Mesh.IsUnlit` skips the light | `SkidMarks`, `SlideSmoke`, `Rasterizer` |
| Kartster | Race rules (`Race/`, run by `TrackScene`), as Kartster's `RaceSession`, `RaceProgressTracker`, `RaceGatePlane` and `RacePositions`: countdown `RaceSession.CountdownSeconds` = 3 s, nobody may drive (`CanDrive` false; a finished kart still may), the clock starts at 0 on the step it runs out (overshoot dropped). Laps: the `TrackScene` lap count (`<id>.race.txt` has the track's, Star Circuit: 3, Kartster's `track.LapCount ?? GameState.LapCount` (3)). Gates (`RaceGate`) are upright walls across the road, crossed when the step from the last position to this one goes through the gate's line within `Left`/`Right` of its point (planar, no height limits). Taken in order: start line, checkpoints, start line again. The first start crossing only starts lap 1; only the next gate counts forwards; back through the last gate passed takes it back (a checkpoint, the unstarted start, or the line after a lap, which takes the lap back, and the lap is not counted again: only a rise of the lap count counts); a step over `MaxStepMeters` = 10 m crosses nothing; passing the gate after next with the next still owed adds 1 to `MissedCheckpoints`. Lap time = race time since the last lap (lap 1 from the green light, grid run included); best lap = shortest. Finish at lap count: `FinishPosition` in order, `FinishTimeSeconds`. Race ends (`Finished`): with a person (`isHuman`) when every racer has finished; bots only when one is left racing (none in a race of one or with `WaitsForEverybody`); `Stop()` ends it with the rest unfinished (DNF). Positions (`UpdatePositions`): finished first in finishing order, then gates passed (`laps × (checkpoints + 1) + next checkpoint`, −1 before the start), then straight-line distance to the next gate, ties in registration order (`TrackScene` registers in grid order, so the grid is the order before the start). Gaps (`GapToLeader`, Kartster's gaps per gate): the race time a kart reached its last gate (`Racer.GateSeconds`) minus the time the first kart reached that gate; finished karts: finish time minus the winner's; 0 before the first gate. `RaceSystem` (fixed step, after `KartGroundSystem`) feeds every kart with `Components.Kart`, `Position` and `RaceProgress` (must be registered by `Kart.Index`) and mirrors its `Racer` into `RaceProgress`; `RaceProgress.Forget()` after a teleport | `RaceSession`, `RaceGate`, `RaceMarkers`, `RaceSystem`, `RaceProgress` |
| Kartster | UI font (`Ui/PixelFont`): 3×5 glyphs drawn as pixels (one console cell each, the screen is 320×180 cells of a 3×3 font), monospaced, advance 4, line height 6, `scale` multiplies each font pixel. Glyphs: `PixelFont.Characters` = A–Z, 0–9, space and `. , : / - + % ! ? ( ) # > <`; lower case draws as upper case, any other character leaves a gap. `MeasureText` = (chars × 4 − 1) × scale (`LAP 2/3` = 27 px, 54 at scale 2). `DrawTextOutlined` adds a 1-px ring (`UiColors.TextShadow`). Numbers go through `TextFormat` into a caller's `char` buffer: `Time` = `m:ss.t` (tenths cut, below 0 shows `0:00.0`), `Gap` = `+m:ss.t`, `Ratio` = `n/m`. Colours (`UiColors`, after Kartster's `UiPalette`) are `0xRRGGBB`; `PaletteTarget` maps each to the nearest palette colour (squared RGB distance, no dither), `RgbTarget` writes it as is. Nothing allocates per frame | `PixelFont`, `TextFormat`, `UiColors`, `PaletteTarget`, `RgbTarget`, `Shapes` |
| Kartster | HUD (`Ui/Hud.Draw(target, in HudState)`), margin 4 px: bottom-left `LAP n/m` at scale 2 (lap clamped to 1…m) with the race clock after it (scale 1, sky blue, bottoms aligned); `LapCount` ≤ 1 shows only the clock at scale 2. Standings above it, one 7-px row each: position, name (first 10 chars), the leader's time or everybody else's `+gap`; the player's row (`IsPlayer`) yellow. No standings and `KartCount` > 1: `POS p/n`. Bottom-right: dial of radius 20, 9 ticks over 270° (last 2 red), yellow needle pinned at 140 km/h, speed (rounded, scale 2) and `KM/H`. Top-right: the power-up in a 20-px box (first letter) with its name under it, right-aligned; nothing when `PowerUp` is null or empty. Top-centre at y 30: `Countdown` > 0 at scale 8, else `GO!` (`ShowGo`) at scale 8, else `FINISHED` (`Finished`) at scale 3. Top-left is left for the minimap (wave 3). In the race `TrackScene.DrawUi` fills `HudState` each frame through `RaceReport.FillHud` (no allocation): speed = player's `Velocity` × 3.6 km/h, `Lap` = `Racer.CurrentLap`, `LapCount` = the race's, `RaceSeconds` = race clock (the player's finish time once finished), `Position`/`KartCount` from the session, standings = top 3 (`RaceReport.TopStandings`) plus the player's row when further back (leader: its `GateSeconds`, or finish time; others: `RaceSession.GapToLeader`), `Countdown` = ⌈countdown left⌉ (3, 2, 1), `ShowGo` for the first second of racing (`RaceReport.GoSeconds`), `Finished` = the player has finished, `PowerUp` null. Names: the player's kart folder name, bots `<KART FOLDER> <n>` (bot number from 1), upper case, built once. `KartsterGame.OnRender` calls `IUiLayer.DrawUi` after `Renderer.Finish`, on a `PaletteTarget` over `Renderer.Colors` (nearest `ScenePalette` colour, no dither) or, in 24-bit, an `RgbTarget` over `Renderer.Rgb` | `Hud`, `HudState`, `HudStanding`, `RaceReport`, `TrackScene` |
| Kartster | Menus (`Scenes/Menus/`, the game starts on `TitleScene`, `KartsterGame` is the `IKartsterMenus`): `MenuScreen` takes `MenuKeys` (from `Input`: arrows, Enter = confirm, Esc = back, on the frame pressed). Up/Down move the selection and wrap; Left/Right step a changeable item's value and wrap; one action per frame, Esc before Enter before moves. Selected item: yellow text on a bar; changeable items show `LABEL  < VALUE >`; long lists scroll to keep the selection in view (8 rows at 180 px). `TitleScene`: Enter → `SelectKartScene`, Space → `GarageScene`, Esc → `Game.Stop()`; `PRESS ENTER` shows 0.65 s of every 1 s. `SelectKartScene`: one item per kart, moving loads it (`SelectKartScene.ChooseHighlighted` → `IKartsterMenus.SelectKart`), the menu opens on the chosen kart, the kart spins at 0.6 rad/s beside the list; Enter → `SelectTrackScene`, Esc → `TitleScene`. `SelectTrackScene`: `TRACK`, `LAPS` (1–5, starts on 3), `START RACE`; Enter on any row → `SelectTrackScene.StartRace` → `IKartsterMenus.StartRace(track, laps)` (nothing without tracks), Esc → `SelectKartScene`. `ResultsScene(RaceResults)`: up to 8 rows, `DNF` when not finished; Enter → `SelectTrackScene`, Esc → `TitleScene`. Scenes draw their UI in `IUiLayer.DrawUi`, which `KartsterGame.OnRender` calls after `Renderer.Finish` | `MenuScreen`, `MenuKeys`, `MenuItem`, `TitleScene`, `SelectKartScene`, `SelectTrackScene`, `ResultsScene`, `IKartsterMenus`, `IUiLayer` |
| PhysicsSandbox | Gravity 30 cells/s² down. Static floor (3 rows), side walls and two ledges (`█`), a kinematic platform (`▒`, 16×1) that moves at 12 cells/s between 20 % and 80 % of the width. `PhysicsSystem` cell size 2 | `SandboxScene`, `PatrolSystem` (fixed step, `[UpdateBefore(typeof(PhysicsSystem))]`) |
| PhysicsSandbox | `SPACE` drops a ball (`●`, radius 0.5, restitution 0.6, mass 1), `B` a box (`■`, 1×1, mass 3, no bounce), `ENTER` 50 balls stacked 1.2 cells apart, all at random x on row 3 with a random sideways speed up to 8 cells/s. `R` removes every dropped body. `ESC` quits. Dropping goes through the command buffer: the bodies appear at the end of the simulation group and move from the next fixed step. The HUD on row 0 shows the count | `DropSystem`, `BodySpawner`, `HudSystem`, `PhysicsSandboxGame.OnUpdate` |
| PhysicsShowcase | One scene with every physics feature. Gravity 40 cells/s² down, 120 × 60 tiles of 1×1 (`Level.CreateTiles`: ceiling rows 0–2 (under the HUD, so it is three tiles thick), floor rows 57–59, side walls, two shelves, two steps). Static turned ramp (`▓`), ice ledge without friction (`═`), kinematic platform patrolling at 10 cells/s (`▒`), kinematic windmill turning at 1.2 rad/s (`#`), static sensor zone (`·`) whose count is on the HUD. Outlines are drawn with every corner moved half a cell towards the body's origin (`OutlineShape.ThroughCellCentres`), so a body resting on a tile isn't drawn one row into it. `PhysicsSystem` cell size 3, sleep and interpolation on | `ShowcaseScene`, `Level`, `PatrolSystem`, `EventCounterSystem` |
| PhysicsShowcase | Keys: `1` bouncy ball (`●`), `2` turning 2×2 box (`#`), `3` triangle (`▲`), `4` pentagon (`*`), `5` ghost ball (`○`, category `Layers.Ghost`: passes through solid bodies, not through tiles or other ghosts); `ENTER` 30 random shapes; `R` removes every dropped body. Sleeping bodies are drawn dark grey. The player `@` (1×1, `CharacterMover`, `A`/`D` run 18 cells/s, `W` jumps from a floor) is a static obstacle for bodies. Standing on a kinematic body (within 0.5 cells below its feet), it first moves with the point under its feet (velocity plus spin), so the platform and the windmill carry it instead of moving into it. Its laser is a `Raycast` 60 cells in the facing direction (× on the hit). `E` blasts every dynamic body within 10 cells (`OverlapCircle`) outwards and spins it. `G` flips gravity and wakes everything, `I` switches interpolation | `DropSystem`, `BodyFactory`, `PlayerMoveSystem`, `LaserSystem`, `BlastSystem`, `SwitchSystem`, `TintSystem`, `HudSystem` |
| Starguard | `LEFT`/`A` and `RIGHT`/`D` (Held) move the cannon at 30 cells/s, scaled by `Delta`, clamped to the screen. `SPACE` (Held) fires a shot (50 cells/s up) only when no player shot is in flight, so holding it is auto-fire. `ENTER` (Pressed) restarts after game over. No quit key | `ControlsSystem`, `CannonSystem`, `Spawner.CannonSpeed`, `PlayerShotSpeed` |
| Starguard | The formation is 5 rows × 11 aliens. All aliens step together: 2 cells sideways, or 1 row down and turn around when the step would cross a screen edge. The pause between steps falls linearly from 0.45 s (whole wave alive) to 0.03 s (one left). Each step flips the animation frame. The timer resets to 0 on a step, so a long frame gives one step, not several | `FormationSystem`, `Formation.StepSeconds` |
| Starguard | Wave n starts with its top row at row 6 + min(n − 1, 8). A new wave starts the frame after the last alien is gone. Shields are **not** repaired between waves | `WaveSystem`, `Spawner.CreateFormation` |
| Starguard | Points: top row 30, rows 2–3 20, rows 4–5 10. A saucer crosses row 3 every 15–25 s (random side, 12 cells/s, one at a time) worth a random 50, 100, 150 or 300 | `Spawner.AlienForRow`, `SaucerSystem`, `Spawner.CreateSaucer` |
| Starguard | Every `AlienFireSystem.ShotInterval(wave)` s (1.1 s in wave 1, 0.1 s less per wave, at least 0.35 s) the lowest alien in the column of a random alien drops a shot (18 cells/s), unless 3 alien shots are already falling. No alien fire while the cannon is down | `AlienFireSystem` |
| Starguard | Shots are tested along the whole path they moved this frame, so they can not skip a row at low FPS. A shot stops at its first hit: the nearest shield cell first, then an alien, then the saucer (player shots), or the cannon (alien shots). Shots do not hit each other | `CollisionSystem`, `Collide.ShotHits` |
| Starguard | Four shields of 22 cells, each cell 4 health shown as `█▓▒░`. Both kinds of shot take 1 health. Aliens erase every shield cell they cover | `Spawner.CreateShields`, `CollisionSystem.TryChipShield`, `CrushShields` |
| Starguard | 3 lives. A hit shows the wreck for 1.5 s, during which the cannon can not move, fire or be hit, then it is rebuilt in the middle. Losing the last life, or an alien reaching the cannon row (bottom edge below row `Playfield.CannonTop`), is game over: lives go to 0 and aliens, cannon and saucer timer stop. Shots already in flight finish | `Match.HitCannon`, `Match.Invade`, `CannonSystem.Rebuild`, `FormationSystem` |
| Starguard | The high score is the best score since the exe started (`StarguardGame.HighScore`, set while game over). It is not saved to disk. `ENTER` loads a new `BattleScene`: score, lives, wave and shields start over | `RestartSystem`, `HudSystem.DrawScoreLine` |

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
| Ladders | `SPACE` (pressed) places one in the miner's own tile if: ladders > 0, row > 10 (underground), the tile is air and has no ladder. `SPACE` in a tile that has a ladder (row > 10) takes it back instead: ladders +1, dust (`Mine.TryTakeLadder`); the fall rule then applies. A shaft of N dug tiles needs a ladder in every one of them to climb out | `Mine.TryPlaceLadder`, `TryTakeLadder`, `MinerControlSystem.PlaceLadder` |
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
| Deep camp | `DEEP CAMP` at the surface counter, 1500g once (`Miner.HasDeepCamp`): digs out columns 22–24 on rows 109–110 (ore → bag, ladders kept) and turns open tiles on row 111 there into stone. Counter: idle on row 110 at columns 22–24; `↑`/`↓` select (no climbing or digging there), `E` trades, holding `E` doesn't plant. Items (`ShopCounter.CampItems`): `SELL BAG` = 70% of `Shop.BagValue` rounded down (empty bag → dust puff), ladder 15g, fill lamp 3g per missing unit, flask 90g, stick 150g with the kit (else `KIT: SURFACE ONLY`): surface × 1.5 rounded up. Every trade saves. The lantern lights tiles within 2.6 tiles of (23.5, 110) `Lit`, to 3.9 `Dim`, and marks them seen | `DeepCamp`, `Shop.CampPrice`, `Shop.TryBuyAtCamp`, `InteractionSystem.TradeAtCamp`, `LightMap.AddLantern` |
| Blasting at the counter | The counter's blasting slot: without the kit, buys it for 2500g; with it, one stick for 100g. The `BLASTING` shed (columns 26–28) is scenery | `Shop.TryBuyBlasting` |
| Planting dynamite | Hold `E` 0.5 s (`PlantDuration`) while idle, supported and underground: a lit stick in the tile `LampDirection` faces. An arrow held with `E` sets `LampDirection` first (`↓` wins, then `↑`, then `←`/`→`; `MinerControlSystem.Aim`). On a lift landing only with the kit and ≥ 1 stick (`MinerControlSystem.CanPlant`); there `E` uses the lift on a tap released before 0.5 s instead of on the press (`InteractionSystem.IsTapToUseLift`, `MinerInput.UseReleased`). Needs the kit, ≥ 1 stick, and breakable rock or obsidian there, else dust puff. While `E` is held nothing else happens; after a plant (or a refusal) `E` must be released first | `MinerControlSystem.UpdatePlanting` |
| Blast | After the 2 s fuse (`FuseDuration`): the 3 × 3 around the stick becomes air if it's breakable rock or obsidian (not bedrock, grass, rows ≤ 10). Ore in it → bag. Ladders in it are removed. Other sticks in it: fuse set to ≤ 0.12 s (`ChainDelay`). A standing, idle miner inside slides one tile away from the stick (sideways first) if that tile is open; never hurt. Biggest screen shake | `DynamiteSystem.Blast`, `Push` |
| Inventory | `TAB` (pressed) toggles a centred panel (gold, ladders, oil %, flasks, dynamite, lift, bag lines `count × price = worth`, weight/capacity, upgrade pips, finds ever found). While open, every `MineScene.Simulation` system has `Enabled = false`: nothing moves, no oil burns, no autosave. `ESC` still saves and quits | `InventorySystem.Toggle` |
| Gas | From 50 m (`Tiles.GasDepth`) the generated air tiles of 2 × 2 noise areas with `Noise < 0.3` (`GasChance`) have `TileCell.HasGas`. `GasSystem` (after `DynamiteSystem`, before `LampSystem`): with the lamp lit (`LampOut` 0 and oil > 0) and the miner below the grass, the nearest gas tile within 2 tiles (Euclidean, tile units) of the miner's tile flares: up to 24 4-connected gas tiles lose their gas (`GasPockets.MaxPocket`; the rest keep it), fire particles, shake 0.6, `GAS!`; `Miner.LampOut` = 3 s (headlamp dark as if empty, no burning) and oil −5. Shimmer particles over gas within 9 tiles, max 30, also in the dark. A blast ignites up to 6 pockets within 3 tiles of the stick (big flare, no lamp effect). Never harms | `GasSystem.Check`, `GasPockets`, `DynamiteSystem.IgniteGas`, `LampSystem` |
| Darkness | Below the wall (`Mine.IsBelowTheWall`: depth > the column's `WallBottomDepth`) the headlamp reach is 2 tiles less, min 1 (`LampOil.ActTwoAbsorb`). Black damp (`Mine.IsBlackDamp`): an open tile below the wall in a 4 × 4 noise area with `Noise < 0.12` (`Tiles.BlackDampChance`); with the miner's tile in it the reach is then halved (min 1) and oil burns ×2 (`LampOil.DarkReach`, `BlackDampBurn`); entering floats `BLACK DAMP`. No mist spawns there; lit solid tiles touching it draw `░` dark grey | `LampSystem`, `MistSystem`, `TerrainRenderSystem`, `Mine.IsBlackDampEdge` |
| Surface progression | `Progress.Track` (every frame, start of `MinerMotionSystem.Move`): `DepthRecord` = max(record, depth); first frame with the miner's tile below the wall (`Mine.IsBelowTheWall`) sets `PassedTheWall` and floats `TOOL SHOP OPEN`. `GoldEarned` += every door and camp sale (`Progress.Earn`). House level from `GoldEarned`: ≥ 2000 flag, ≥ 8000 yellow windows, ≥ 20000 second floor (`Progress.HouseLevel`); the sale crossing one floats `HOUSE UPGRADE!`. Trophy case: 10 slots, relics then fossils, `+` in the last slot over 10. Record plank only when the record > 0 | `Progress`, `HouseRenderSystem` |
| Tool shop | Column 13 on row 9 (`Layout.IsAtToolShop`), open only with `PassedTheWall`: `↑`/`↓` select, `E` buys `DYNAMITE x5` 400g (needs the kit, else sold out) or `LADDERS x10` 80g; saves. Closed: prompt only | `ShopCounter.ToolShopItems`, `Shop.TryBuyBundle`, `InteractionSystem.BuyTools` |
| Quit | `ESC` in the mine saves (`MineScene.Save`), then `Stop()`. On the title screen it only quits | `EternalDiggerGame.OnUpdate` |
| Title | Starts on `TitleScene`. With a loaded save: `CONTINUE`, `NEW MINE`; else only `NEW MINE`. `↑`/`W`, `↓`/`S` (pressed) move the selection (wraps), `E` picks. NEW MINE with a save asks `NO` / `YES, START OVER` (NO selected); NO goes back. NEW MINE uses the command-line seed (or `TickCount`) and writes its first save at once | `TitleScene`, `TitleMenu` |
| Saving | Saves after a sale that earned gold, after a ladder, lift, kit or dynamite purchase, every 60 s underground (`GameSaver.AutosaveInterval`, timer counts only below the surface and resets on every save), and on `ESC`. Never per frame. A failed write shows ` SAVE FAILED` and play goes on | `InteractionSystem`, `SaveSystem`, `GameSaver` |
| Loading | CONTINUE rebuilds the mine from the saved seed plus the saved changed tiles, and puts the miner standing on the saved tile with the saved facing and inventory. Unreadable save: red message on the title, only NEW MINE, file moved to `save.dat.bad` | `SaveFile.Load`, `SaveData.ApplyTo`, `SaveData.ToMiner` |
| Save hint | After each save (or failed save), ` SAVED ` (Gray for 0.75 s, then DarkGray for 0.75 s) or ` SAVE FAILED` (red) at the right end of HUD row 1 | `HudSystem.DrawSavedHint`, `HudSystem.SavedHintSeconds` |


<a id="skystack"></a>
## SkyStack (`Examples/SkyStack`)

An original tower climber drawn in pixels: every console cell shows two square pixels, one above the other (`▀` with
foreground = upper pixel, background = lower pixel; `PixelCanvas.Present`), with a 6 × 12 font. The tower is an
84 × 120 pixel canvas (`Layout.TowerWidth` × `Layout.ViewHeight`); the HUD right of it is plain text. All numbers below
are pixels and seconds. World pixels grow downwards: floor `n` lies on world row `-n * Layout.FloorSpacing` (16).

| Rule | Value | Identifier |
|---|---|---|
| Tower | Inside pixel columns 4–79 (`Layout.InnerLeft`/`InnerRight`), 4-pixel stone walls. Platforms are 3 pixels thick (`Layout.PlatformThickness`). Floor 0 and every 50th floor (`Tower.MilestoneEvery`) span the whole inside (milestones drawn white). Other floors: width `30 - floor / 10` (`StartWidth`, `NarrowEvery`) ± 4 (`WidthSpread`), at least 12 (`MinWidth`), at a random column. Each floor comes from a hash of seed and floor number: nothing is stored, the same seed gives the same tower. Every 10th floor (`LabelEvery`) has its number in the 3 × 5 `PixelFont` under the platform | `Tower.GetFloor`, `TowerRenderSystem` |
| Zones | Every 50 floors a new colour zone: STONE, MOSS, BRICK, CRYSTAL, SKY, GOLD, then again from STONE. A row takes the zone of the platform above it. The HUD shows the zone of the floor last landed on | `TowerLooks.Zones`, `Tower.ZoneOf` |
| Running | Climber is 6 × 10 pixels (`ClimbPhysics.BodyWidth`/`BodyHeight`). Held ← → / A D: 170 px/s² on a floor, 115 in the air, max 114 px/s. No key on a floor: friction 230 px/s²; in the air the speed is kept. The sprite looks the way it last ran (`Climber.Facing`) | `ClimbPhysics.Run` |
| Walls | Hitting a wall reflects the climber and keeps 90 % of the speed (`WallBounce`), with a 0.15 s spark | `ClimbPhysics.BounceOffWalls` |
| Jumping | While SPACE / ↑ / W is held and the climber stands on a floor, it jumps with `136 + 1.6 × |run speed|` px/s (`BaseJumpSpeed`, `RunJumpBonus`). Gravity 480 px/s², max fall 320 px/s. From standing still a jump reaches 19 px (one floor); at full speed about 106 px (6 floors) | `ClimbPhysics.JumpSpeed`, `Step` |
| Landing | Platforms are one-way: only a falling climber lands, on the highest platform row passed this step that has at least one of its 6 pixel columns over it. Walking off an edge falls. A landing faster than 120 px/s raises dust | `ClimbPhysics.Fall`, `Floor.Supports`, `ClimberRenderSystem` |
| Score | `HighestFloor × 10` (`RunStats.PointsPerFloor`) + combo points | `RunStats.Score` |
| Combo | A landing 2+ floors above the last landing (`ComboMinFloors`) adds a combo jump and refills a 3 s timer (`ComboWindowSeconds`). Any other landing (also on the same or a lower floor) or the timer running out ends it. A combo of 2+ jumps (`ComboMinJumps`) pays `floors × jumps × 5` (`ComboPointsFactor`) and shows `COMBO <floors> FLOORS  +<points>` for 2 s. While a combo runs the jacket flashes and a trail follows. The end of a run pays a running combo | `RunStats.Land`, `Tick`, `EndCombo` |
| Rising view | Still until the climber has landed on floor 5 (`Scroller.StartFloor`), then rises at 10 px/s (`BaseSpeed`), +6 px/s every 30 s (`SpeedPerLevel`, `LevelSeconds`) up to level 6. A climber less than 48 px (`CatchUpPixels`) below the top edge pulls the view up. Start and each level-up show `THE TOWER RISES!` / `FASTER!` for 2 s | `Scroller.Update` |
| Game over | The whole climber below the bottom edge (`Scroller.HasLost`) ends the run (`RunStats.Finish`): the climber freezes red, the box shows floor, score and best combo, `NEW BEST!` when the score is the session's best. SPACE / ENTER after 0.75 s (`RestartSystem.DelaySeconds`) starts a new run with a new seed | `ScrollSystem`, `MessageRenderSystem`, `RestartSystem` |
| Best score | Only in memory for the running process (`SkyStackGame.RecordRun` at game over). Leaving a run with ESC does not record it | `SkyStackGame.BestScore`, `BestFloor` |
| Order | Fixed step (60/s): `ClimberMotionSystem` → `ScrollSystem` (combo timer, view, game over). Every frame: `ClimberInputSystem` → `RestartSystem`. Drawing: `TowerRenderSystem` → `ClimberRenderSystem` (both into the canvas) → `CanvasPresentSystem` → `MessageRenderSystem` → `HudRenderSystem` (text) | `TowerScene.OnCreate` |
