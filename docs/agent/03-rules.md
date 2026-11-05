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
  **TerraForM `Rover`** (`Position += Velocity`, and power cost per `ApplyForce` call) and **Asteroids `Player`**
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

Not called by `GameBase` or `Scene`: a game creates and disposes its own `World`. Systems, queries and scene
integration are not implemented yet (plan: `docs/design/ecs-plan.md`).

- Components are `unmanaged` structs. A struct with no instance fields is a **tag** (`ComponentType<T>.IsTag`): it
  changes the archetype but has no data, so `Get<T>` on a tag throws.
- `Add<T>` throws if the entity already has `T`. `Remove<T>`, `Get<T>` and `Set<T>` throw if it doesn't.
  `Set<T>` never adds.
- `Create(...)` with the same component type twice throws `ArgumentException`.
- Every operation on a destroyed entity, a stale handle (old `Version`), `Entity.Null`, or any entity after
  `World.Dispose()` throws "is not alive". `IsAlive` is the non-throwing check. `Create` after `Dispose` throws
  `ObjectDisposedException`.
- Destroying an entity bumps its id's `Version` and puts the id on a free list. The next `Create` reuses it.
- The `ref` from `Get<T>` is only valid until the next structural change (`Create`, `Add`, `Remove`, `Destroy`) in
  that archetype: swap-back removal moves the archetype's last row into the freed row.
- At most 256 component types per process (`ComponentMask.Capacity`). An archetype whose row (8-byte `Entity` plus
  every component) doesn't fit in one 16 384-byte chunk throws on first use.

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

## Other examples

| Game | Rule | Identifier |
|---|---|---|
| Snake | Moves one cell every **150 ms** (`UpdateThreshold`). FPS-independent | `SnakeGame.OnUpdate` |
| Snake | Reversing direction is ignored. **No wall or self collision**: the snake leaves the screen and is clipped forever | `SnakeGame.OnUpdate` |
| Snake | Food x and y both use `Random.Next(0, Console.Width - 1)`, so never the last column/row. It retries while the cell is not `' '`, up to `Area` times. Never-drawn cells hold `'\0'`, so the retry rarely succeeds and food can land on the snake | `SnakeGame.SpawnFood` |
| Snake | `ClearScreenOnEachFrame = false`. The tail cell is blanked manually | `SnakeGame.OnInitialize`, `OnRender` |
| Platformer | `F1` toggles `IsDebugMode`, which **starts true** (`Program.cs`). Draws the player **before** the world, so tiles overwrite the player | `PlatformerGame.OnUpdate`, `OnRender` |
| Platformer | `ShowFps = true` but no `EnableLogger`, so no FPS anywhere | `PlatformerGame.OnInitialize` |
| Asteroids | Steer `A`/`D`, thrust `W`. No collisions, no objective. Logger enabled but nothing is logged | `AsteroidsGame` |
| ConsoleGame | Recolours all cells every **500 ms**. `ESC` calls `Stop()`, which exits | `Game.OnInitialize`, `OnUpdate` |
