# 02 — State model

There is no database. All state is in memory in one process and is lost when the window closes. Nothing is saved:
no high scores, settings or save games. The only persistent inputs are the TerraForM map files
([§ Map file format](#map-format)).

## Engine objects

| Object | Identifier | Key state | Notes |
|---|---|---|---|
| Game | `GameBase` | `Name` (default `"Game"`), `ShowFps`, `EnableLogger` (init-only), `ClearScreenOnEachFrame` (default `true`), `Scenes`, `Console`, private `_running` | `Scene` is shorthand for `Scenes.Current` |
| Frame buffer | `RenderConsole._pixels` (`Pixel[Width*Height]`) | One `Pixel` per cell: `Char`, `ForegroundColor`, `BackgroundColor` (16 `ConsoleColor`s only) | Index = `y * Width + x`. Cleared to `ClearColor` each frame if `ClearScreenOnEachFrame` |
| Font | `RenderConsole.FontInfo` | Getter returns the value set in the `RenderConsole` ctor (Consolas **14×14**), **not** the font passed to `GameBase` | Gotcha: the font actually applied at `Initialize` is the one passed to `GameBase`. Do not trust `Console.FontInfo` for diagnosis |
| Window flags | `RenderConsole.Resizeable` (default `false`), `HideCursor` (set `true` in ctor) | Both init-only | `Resizeable = false` removes Maximize/Size from the system menu |
| Keyboard | `InputHandler._keyStates[256]` (`KeyState`) | `Pressed`, `Released`, `Held`, `Index` per virtual key code | Accessed via singleton `Input.Instance` ([03 § Keys](03-rules.md#keys)) |
| Clock | `GameTime` (static) | `Delta` (time since last frame), `Fps` (= `1 / Delta`, one frame, not averaged), `_intervals` | Static, so shared by everything in the process |
| Scenes | `SceneManager._scenes` (`Stack<Scene>`), `_unloadScenes` (`Queue<Scene>`) | `Count`, `Current` (top of stack), `PreviousScene` | `Scene.IsPaused` per scene ([03 § Scenes](03-rules.md#scenes)) |
| ECS world | `ConsoleEngine.Ecs.World` (one per `new World()`, owned by the game code) | `_records` (`EntityRecord`: `Version`, `ArchetypeIndex`, `ChunkIndex`, `Row` per entity id), `_freeIds`, `_archetypes`, `_archetypesByMask`, `_chunkPool`, `_iterationDepth` (open query loops), `Count` | Component data is in **native memory** (16 KiB `Chunk`s per `Archetype`, from `NativeMemory.AlignedAlloc`), not on the GC heap. Freed only by `World.Dispose()`: there is no finalizer, so an undisposed world leaks. A record whose `Version` reached `int.MaxValue` is retired (`RetiredVersion` = −1) and its id never reused. Component type ids are process-wide (`ComponentRegistry`, max 256), assigned the first time any API names the type |
| ECS scene | `EcsScene<TGame>` | `World`, `Simulation`, `Presentation` (`SystemGroup`s, each with its own `Commands`), `Camera` (`ConsoleEngine.Ecs.Rendering.Camera.Position`) | World created in `OnLoad`, disposed in `OnUnload` |
| Command buffer | `CommandBuffer._buffer` (`byte[]`), `_length`, `Count` | Recorded commands as bytes: opcode, entity, component id, raw component bytes | Grows by doubling, never shrinks. Cleared after every `Playback` |
| Logger | `Log` (static) | `Messages` (`ConcurrentQueue<string>`), `Socket`, `_loggerConnection`, `_loggerProcess`, `_running` | Only active when `GameBase.EnableLogger` |

## TerraForM state

| Owner | Field | Meaning | Reset when |
|---|---|---|---|
| `TerraformGame` | `Playername` | Name typed on the first screen. **PII**: never echo it | Never (process lifetime) |
| `TerraformGame` | `Score` | Running score across levels and restarts | **Never.** Not reset by `R` restart or game over ([P7](05-triage.md#p7)) |
| `TerraformGame` | `CurrentMap` | Number used to build map name `map{CurrentMap}`. Starts at **1** | Changed only by `RotateMap` |
| `TerraformGame` | `Maps` (string[4]) | Lists map1–map4 paths, **but only `Maps.Length` is used** ([03 § Map rotation](03-rules.md#map-rotation)) | — |
| `TerraformGame` | `IsDebugMode` | Enables WASD free move and debug overlay. **Never set anywhere**, so always `false` | — |
| `TerraformGame` | `GameOver` | Declared, **never used**. The real flag is `GameScene.GameOver` | — |
| `GameScene` | `World`, `Rover`, `Hud`, `GameOver`, `PlantEmitters`, `_currentCommands` | Per-level state | New `GameScene` on restart / next level |
| `Rover` | `MaxPower`, `RemainingPower` (double), `DamageTaken`, `AthmosphereGeneratorsPlanted`, `RemainingSequences`, `Velocity`, `Acceleration`, `Direction` | Copied from the map at `Rover` ctor: `MaxPower = World.MaxPower`, `RemainingSequences = World.Sequences` | New level / restart |
| `PlantSpot` | `HasBeenPlanted` | Each spot can be planted once | New level / restart |
| `BonusPoint` | `HasBeenConsumed` | Each bonus can be collected once | New level / restart |
| `Hud` | `_commandSequence`, `_sequenceReadyToShip` | Commands typed but not yet sent with SPACE | Cleared by `GetCommands()` on SPACE |
| `Sprites.Story` (static) | sprite pixels | Player name is stamped into it in place by `MenuScene.OnLoad` | Never. It mutates a shared static sprite |

<a id="map-format"></a>
## Starguard state

One `BattleScene` per match. Everything on screen is an entity. The rest lives in plain objects that `BattleScene.OnCreate`
creates and hands to the systems, so a restart (a new scene) resets all of it except `StarguardGame.HighScore`.

| Object | Fields | Notes |
|---|---|---|
| `Match` | `Score`, `Lives` (starts at 3), `Wave` (starts at 1), `IsOver`, `RespawnSecondsLeft` (`CannonDown` while > 0) | Changed by `CollisionSystem`, `CannonSystem`, `FormationSystem`, `WaveSystem` |
| `Formation` | `Direction` (+1/−1), `Frame` (0/1), `SinceLastStep`, `StartCount` | `Reset` at the start of each wave |
| `Controls` | `Move` (−1/0/+1), `Fire`, `Restart` | Written only by `ControlsSystem`, from `Input.Instance` |
| `StarguardGame.HighScore` | int | Process lifetime only, never saved |

| Entity | Components |
|---|---|
| Alien | `Position`, `SpriteRef`, `Hitbox`, `Alien` (`FirstFrame`, `Points`) |
| Cannon | `Position`, `SpriteRef`, `Hitbox`, `Cannon` (`Speed`) |
| Saucer | `Position`, `Velocity`, `SpriteRef`, `Hitbox`, `Saucer` (`Points`) |
| Player / alien shot | `Position`, `Velocity`, `Glyph`, tag `PlayerShot` / `AlienShot` |
| Shield cell | `Position`, `Glyph`, `ShieldCell` (`Health`) |
| Explosion | `Position`, `SpriteRef`, `Lifetime` (0.25 s) |

## Map file format (`Examples/TerraForM/Assets/Maps/mapN.txt`)

Parsed by `WorldLoader.Parse`. Copied to output by `TerraForM.csproj` (`CopyToOutputDirectory=Always`, map0–map4).

**Header lines** start with `!`. Each line is split on `:`, the key is lower-cased and trimmed, and the value (only
the part before any second `:`) is trimmed. Unknown keys are ignored.

| Key | Field | Parsed with | If missing |
|---|---|---|---|
| `!name` | `World.Name` (shown after `NOW ENTERING`) | string | Empty name |
| `!power` | `World.MaxPower` → `Rover.MaxPower`/`RemainingPower` | `int.Parse` | 0, so **instant game over** (`PowerDepleted()` is `<= 0`) |
| `!sequences` | `World.Sequences` → `Rover.RemainingSequences` | `int.Parse` | 0, so game over on the first SPACE |
| `!width`, `!height` | grid size | `int.Parse` | No grid allocated → crash in `World` ctor |

**Grid lines** (any line not starting with `!`): one character per tile, row by row. Tile world position is
`(x, y) * World.TileSize` (`TileSize = 5`).

| Char | Tile class | Effect on contact ([03](03-rules.md#terraform)) |
|---|---|---|
| `S` | `StartingPoint` | Rover spawn. **Required** |
| `F` | `FinishPoint` | Level complete |
| `H` | `Rock` | Blocks, +10 damage |
| `C` | `Craves` | Blocks, power set to 0 |
| `P` | `PlantSpot` | Allows planting (bounding box 10×10, larger than the 5×5 grid cell) |
| `B` | `BonusPoint` | +1000 power, +10000 score, once |
| anything else | none | Empty |

A border of `Rock`s is added around the grid in `World.Init`.

Shipped maps (rows are 94 chars, header width 95, 17 rows):

| File | `!name` | `!power` | `!sequences` | Reached in play? |
|---|---|---|---|---|
| `map0.txt` | `Map Name` | 3500 | 30 | **Yes**, as the 4th level (test-looking map, 1 tile of each kind) |
| `map1.txt` | `Leviathan Wakes` | 3500 | 30 | Yes, first level |
| `map2.txt` | `Persepolis Rising` | 10000 | 130 | Yes |
| `map3.txt` | `Persepolis Rising` (same name as map2) | 10000 | 30 | Yes |
| `map4.txt` | `Leviathan Falls` | 4000 | 130 | **Never** ([03 § Map rotation](03-rules.md#map-rotation)) |

`Assets/Story/story-EN.txt` and `story-NO.txt` exist but are **not referenced by code and not copied to output**. The
story shown in game is hard-coded English in `Sprites.Story`.

## Retention

Nothing is stored. When the process exits, all state (including `Playername` and `Score`) is gone.
