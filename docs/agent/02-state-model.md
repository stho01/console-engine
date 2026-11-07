# 02 — State model

There is no database. All state is in memory in one process and is lost when the window closes, with one exception:
EternalDigger's save file ([§ EternalDigger save file](#eternal-digger-save)). There are no high scores or settings.
The only other persistent inputs are the TerraForM map files ([§ Map file format](#map-format)).

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

## EternalDigger state

| Owner | Field | Meaning | Reset when |
|---|---|---|---|
| `Mine` (one per `MineScene`) | `_cells` (`TileCell[]`, row-major, 30 per row), `_generatedRows` | The world. `TileCell`: `Type` (`TileType`), `Damage` (hits taken), `HasLadder`, `Seen` (ever lit). Generated from the seed, then changed only by digging, ladders and `Seen` | New `MineScene`: regenerated from the seed, then the save's changed tiles are applied |
| `Miner` component (one entity) | `Column`, `Row`, `MoveFrom`, `MoveElapsed`, `MoveDuration`, `Motion`, `FallRows`, `FacingX`, `LampDirection`, `DigCooldown`, `SwingTime`, `SwingDirection`, `RescueHold` | Where the miner is and what they are doing. While moving, `Column`/`Row` are already the target tile and the engine's `Position` slides there | Rescue resets the position and motion |
| `Miner` component | `Gold`, `Ladders`, `CopperOre`, `SilverOre`, `GoldOre`, `GemOre` | Inventory. The three ore counts are "the bag" | Ore: delivery or rescue. Gold/ladders: never |
| `Elevator` (one per `MineScene`) | `Segments`, `CarRow` (fractional while moving), `CarTargetRow`, `IsCalled` | The lift in the shaft (column 21). Landings at row 9 and row `10 + 10 × Segments` | Segments: never (saved). Car: restored to its saved landing |
| `Miner` component | `PlantHold`, `HasBlastingKit`, `Dynamite`, `LitDynamite` | Holding `E` to plant (−1 = wait for release); the kit; sticks carried; sticks burning | Kit and sticks: saved. `PlantHold`: release, rescue |
| Dynamite entities | `Dynamite` (`Column`, `Row`, `Fuse`, `SparkTimer`) + `Position` | A planted, lit stick | Destroyed when it goes off; not saved (refunded into `Dynamite`) |
| `Miner` component | `ShovelTier`, `LampTier`, `BagTier`, `DetectorTier` | Upgrade tiers 0–3 | Never (saved) |
| `Miner` component | `Oil` | Lamp oil 0–100, burns underground | Refilled at the counter (saved) |
| `Headlamp` (one per `MineScene`) | `Reach`, `IsDimmed`, `IsLow`, `IsEmpty` | This frame's lamp, from the oil and lamp tier | Every frame (`LampSystem`) |
| `Miner` component | `IsRiding`, `ClimbStreak` | In the lift car; tiles climbed up without letting go | Rescue, getting out / any non-climb action |
| `LightMap` | `_levels` (byte per tile on screen), `FirstRow`, `Rows` | This frame's light | Recomputed every frame |
| Particles | `Particle` (`Age`, `Lifetime`, `Style`, `Variant`), `Physics`, `TileCollider`/`Mist` tags, `FloatingText` + engine `Position`/`Velocity`/`Glyph` | Effects only, no game state | Destroyed when `Age >= Lifetime` |

<a id="eternal-digger-save"></a>
### EternalDigger save file

One slot: `SaveFile.DefaultPath` = `<LocalApplicationData>/EternalDigger/save.dat` (`%LOCALAPPDATA%` on Windows,
`~/Library/Application Support` on macOS, `~/.local/share` on Linux). Written by `SaveFile.Save` through
`save.dat.tmp`, then moved over the old file. Little-endian binary, `SaveFile.Version` = 6 (versions 1–5 are still read; fields they lack get the defaults):

| Field | Type | Check on load |
|---|---|---|
| magic `EDSV` | int `0x56534445` | else `Corrupt` |
| version | int | not 1–6 (`OldestVersion`…`Version`) → `Unsupported` |
| seed | int | — |
| column, row | int, int | column 0–29, row 0–100 000 |
| facing | sbyte | −1 or 1 |
| gold, ladders, copper, silver, gold ore | 5 × int | ≥ 0 |
| lift segments, car row (version 2+) | int, int | segments 0–10 000; car row 9, or the bottom landing `10 + 10 × segments` (only with segments > 0) |
| gem ore (version 3+) | int | ≥ 0 |
| blasting kit, dynamite (version 4+) | byte, int | kit 0 or 1; dynamite ≥ 0 (sticks carried + sticks burning at save time) |
| shovel, lamp, bag, detector tier (version 5+) | 4 × byte | each 0–3 |
| oil (version 6+) | float | 0–100 (older saves load full) |
| change count | int | ≤ 100 001 × 30 |
| per change: row, column, type, damage, flags | int, byte, byte, byte, byte | row 0–100 000, column < 30, type a `TileType` (its byte: new types are only ever appended), flags only bit 0 (ladder) and bit 1 (seen) |

Anything after the last change, or a file that ends early, is `Corrupt`. A `Corrupt` or `Unsupported` file is moved
to `save.dat.bad` (`SaveFile.BadPath`) by `SaveFile.Load`. Not saved: motion, dig cooldown, rescue hold, particles,
the lamp direction (rebuilt from facing), riding (a rider is saved standing at the landing the car is heading for).

<a id="map-format"></a>
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
