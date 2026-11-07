# EternalDigger: design and implementation contract

`Examples/EternalDigger` is an ECS example game built from the owner's game design document: a 2D side-view,
tile-by-tile digging and resource game. The miner digs down, collects ore, climbs back up on ladders they place, sells
the ore at the house and buys more ladders. The look comes from many small particle effects.

This file is the contract between the parts of the game. Names here are the names in the code. The game's own
documentation, written after it was built, is in [`Examples/EternalDigger/README.md`](../../Examples/EternalDigger/README.md)
and its `docs/` folder.

## Game rules

| Rule | Value | Where |
|---|---|---|
| Screen | 120 x 48 console cells, 8 x 16 font on Windows | `Layout.ScreenWidth/ScreenHeight`, `EternalDiggerGame` |
| Tile | 4 x 2 cells (looks square) | `Layout.TileWidth/TileHeight` |
| Mine width | 30 tiles = the screen width. Camera scrolls vertically only | `Mine.Columns` |
| Sky | Tile rows 0–9. Row 10 is grass (`Mine.SurfaceRow`, depth 0), unbreakable | `Mine` |
| Surface walk row | Row 9 (`Layout.GroundRow`) | `Layout` |
| Shaft | Column 21, rows 10–11 dug out with ladders | `Mine.ShaftColumn` |
| Side walls | Columns 0 and 29 below the surface, and everything outside the 30 columns (also beside the sky): bedrock | `Mine.At` |
| Depth | `row - 10` metres. HUD shows `max(0, depth)` | `Mine.Depth` |
| Tiles | Dirt 2 hits, Stone 4, Copper 4, Silver 6, Granite 7, Gold 8; +1 hit every 25 m | `Tiles.HitsToBreak` |
| Ores | Copper from 2 m (15g), Silver from 12 m (40g), Gold from 30 m (100g). Caves from 6 m, granite from 40 m | `Tiles` |
| Start | 0 gold, 8 ladders, spawn at column 15, row 9 | `Shop`, `Layout.SpawnColumn` |
| Ladder | 10 gold. Placed in the miner's own tile, only below the surface, only in air without a ladder | `Shop`, `Mine.TryPlaceLadder` |
| Support | The miner stands still if their tile has a ladder, or the tile below is solid or has a ladder; otherwise they fall | `Mine.IsSupported` |
| House | Drawn over columns 2–11 on the surface. Deliver at columns 3–6, buy at 8–11 (row 9) | `Layout` |

### Controls (`MinerControlSystem`, `InteractionSystem`)

| Key | Action |
|---|---|
| `LEFT`/`A`, `RIGHT`/`D` (held) | Walk to the side tile if it is air; otherwise dig it (one hit per `DigInterval`). Turns the miner |
| `DOWN`/`S` (held) | Below solid → dig it. Below air → step down (climbing if a ladder is there, otherwise the fall rule takes over) |
| `UP`/`W` (held) | Only on a ladder tile: above air → climb up; above solid → dig it |
| `SPACE` (pressed) | Place a ladder in the miner's tile (uses one from the inventory) |
| `E` (pressed) | At the door: deliver all ore. At the shopfront: buy one ladder |
| `R` (held 1.5 s) | Rescue: back to the spawn tile, the bag is lost (ore only, not gold or ladders). The way out of a pit without ladders |
| `ESC` | Save and quit (on the title screen: quit) |

Keys are read once per frame into a `MinerInput` (`MinerInput.FromKeyboard`); tests pass their own. No input
except `R` is read while the miner is moving (a move is a slide of one tile). Falling is checked first, every time the
miner is idle. Digging also turns the headlamp (`LampDirection`) towards the dug tile; walking turns it to the side,
climbing up/down points it up/down.

Timings (`MinerControlSystem` constants, seconds): walk 0.13 per tile, climb 0.17 (held Up ramps to 0.06 after 4 tiles, over 10 tiles), fall 0.06 per tile, dig interval
0.16, swing animation 0.12, rescue hold 1.5.

## Components (`Components/`)

- `Miner` (one entity, with the engine's `Position` = top-left cell of the sprite in world cells).
- `Particle(style, lifetime)`, `Physics(gravity, drag)`, tags `TileCollider` and `Mist`, `FloatingText`.
  Particles also have the engine's `Position`, `Velocity`, `Glyph`.

## Shared services (constructed in `MineScene`)

- `Mine` (`Mining/`): the grid. `At`, `TypeAt`, `IsSolid`, `HasLadder`, `IsSupported`, `Hit`, `TryPlaceLadder`,
  `Noise`, static `Depth`/`IsInside`. Generates rows lazily; generation allocates but only every 64 rows.
- `Shop`: `Deliver`, `TryBuyLadder`, `Collect`.
- `ScreenShake` (`Effects/`): `Add(strength)`, `Update(dt)`, `Strength`.
- `EffectSpawner` (`Effects/EffectSpawner.cs`): every method records entities in the `CommandBuffer` passed in (a
  system's `Commands`), positions are world cells:
  - `ScreenShake Shake { get; }`
  - `void DigHit(CommandBuffer commands, int column, int row, TileType type, Point digDirection)`: debris flies out
    of the hit face back towards the miner (`digDirection` = tile offset from the miner to the hit tile). Ore adds sparks.
  - `void TileBreak(CommandBuffer commands, int column, int row, TileType type)`: burst of chunks + dust cloud, small
    shake. Ore adds a glittering burst.
  - `void FootDust(CommandBuffer commands, Vector2 feet, int directionX)`: 1–2 puffs behind the feet.
  - `void LandingDust(CommandBuffer commands, Vector2 feet, int rowsFallen)`: puffs both sides, more for longer falls.
  - `void Glint(CommandBuffer commands, Vector2 position, TileType ore)`: a twinkle.
  - `void Mist(CommandBuffer commands, Vector2 position)`: one slow mist particle (has the `Mist` tag).
  - `void Coins(CommandBuffer commands, Vector2 origin, int count)`: coins burst up and cascade down onto the grass.
  - `void Pop(CommandBuffer commands, Vector2 center)`: ring of stars.
  - `void Number(CommandBuffer commands, Vector2 position, int amount)`: floating `+150g` / `-10g`.
- `LightMap` (`Rendering/LightMap.cs`): per-tile light for the rows on screen.
  - `const byte Dark = 0, Dim = 1, Lit = 2, Bright = 3`
  - `LightMap(int columns, int maxRows)` allocates once.
  - `int FirstRow`, `int Rows`
  - `void Compute(Mine mine, int firstRow, int rows, Vector2 lampOrigin, Vector2 lampDirection)`: `lampOrigin` in
    tile coordinates (miner tile centre), clamps `rows` to `maxRows`. Daylight: rows ≤ `SurfaceRow` are `Bright`;
    depth 1 `Lit`, depth 2–3 `Dim` in dug-out tiles and their direct neighbours. Headlamp: tiles within 1.5 tiles of
    the miner `Bright`; in a 40° half-angle cone, range 8 tiles: < 4 `Bright`, < 6 `Lit`, else `Dim`. A tile is only
    lit by the lamp if no solid tile lies strictly between it and the miner (grid ray walk). Sets `Seen` on every tile
    it lights.
  - `byte Get(int column, int row)`: `Dark` outside the computed window.

## Systems (run order is the `Add` order in `MineScene`)

Simulation:
1. `MinerControlSystem(Mine, EffectSpawner)`: `Control(ref Miner, ref Position, in MinerInput, dt, CommandBuffer)`. Falling check, input → move/dig/ladder, rescue, dig effects and ore
   collection (`Shop.Collect`). Starts moves by setting `Miner.MoveFrom/MoveDuration/Motion` and the target tile.
2. `InteractionSystem(EffectSpawner, GameSaver)`: `E` at the door/shop; every sale or purchase calls `GameSaver.Request`. Deliver: `Shop.Deliver`, coins (one per 10g, max 30) +
   pop + `Number(+earned)` above the door. Buy: `Shop.TryBuyLadder`, pop + `Number(-10)`. Nothing to deliver / not
   enough gold: a small dust puff, no number.
3. `MinerMotionSystem(Mine, EffectSpawner)`: `Move(ref Miner, ref Position, dt, CommandBuffer)`. Advances `MoveElapsed`, eases `Position` from `MoveFrom` to the target
   tile, ends the move (sets `Motion = Idle`, `MoveDuration = 0`). Foot dust while walking (every other step) and
   climbing; landing dust when a fall ends on support; counts `FallRows`; decays `SwingTime`, `DigCooldown`.
4. `CameraSystem(Camera, RenderConsole, ScreenShake, Random)`: keeps the miner around 40% of the screen height,
   smooth (exponential, frame-rate independent), clamped at the top of the sky (`Position.Y >= -HudHeight` so the sky
   starts right below the HUD), X always 0; adds a random shake offset of `Shake.Strength` and decays it. The camera
   position is rounded to whole cells so every renderer maps world to screen the same way.
5. `LightingSystem(Mine, LightMap, Camera)`: `LightMap.Compute` for the rows on screen from the miner.
6. `GlintSystem(Mine, LightMap, Camera, EffectSpawner, Random)`: every ~0.08 s picks random ore tiles on screen
   (also dark ones: glints are how ore is found in the dark) within 9 tiles of the miner, spawns a glint on a few.
   Gold glints more often than silver, silver more than copper.
7. `MistSystem(Mine, LightMap, Camera, EffectSpawner, Random)`: keeps up to 40 mist particles alive in dug-out air
   tiles below the surface on screen, spawning only in lit tiles, at most 12 per second.
8. `ParticlePhysicsSystem(Mine)`: `Velocity.Y += Gravity * dt`, drag, `Position += Velocity * dt`; `TileCollider`
   particles that enter a solid tile are put back on top of it and stop (velocity zero; coins bounce once).
9. `ParticleLifetimeSystem`: ages particles, writes `Glyph` from `Style`, `Variant` and `Progress`, destroys expired.
10. `FloatingTextSystem`: moves floating texts up, ages and destroys them.
11. `SaveSystem(GameSaver)`: writes the save when requested this frame, or every `GameSaver.AutosaveInterval` (60) s
    underground.

Presentation (draw order). Renderers draw through `Canvas` (`Rendering/Canvas.cs`), which wraps the `RenderConsole`
and remembers each cell's background, so `DrawOver` can put effects over the sky, the house or a tile without a black
box behind them (the engine has no public way to read a cell back):
1. `SkyRenderSystem(Canvas, Camera)`: sky gradient over rows < `SurfaceRow`, sun, slow clouds, distant hills.
2. `TerrainRenderSystem(Canvas, Camera, Mine, LightMap)`: every tile row from `SurfaceRow` on screen, lit by
   the `LightMap`; ladders; crack overlay from `Damage`; seen-but-dark tiles as faint silhouettes, unseen dark tiles
   black.
3. `HouseRenderSystem(Canvas, Camera)`: house with the `EternalDigger` wooden sign, door, shopfront, mine
   entrance frame over the shaft.
4. `ParticleRenderSystem(Canvas, Camera, LightMap)`: particle glyphs, keeps the background colour of the cell
   underneath. Mist only where lit. Nothing on the miner's 4 x 2 cells; at most 6 particles, dimmed, in the tile
   ring around him.
5. `MinerRenderSystem(Canvas, Camera)`: 4 x 2 sprite, facing, swing animation, headlamp. Drawn after the particles
   so the miner is readable on every frame.
6. `FloatingTextRenderSystem(Canvas, Camera)`: `+150g` / `-10g`, formatted without allocating.
7. `PromptRenderSystem(Canvas, Camera)`: small framed `DELIVER [E]` over the door / `BUY LADDERS [E] 10g`
   over the shopfront while the miner stands there.
8. `HudSystem(Canvas, GameSaver)`: top 2 rows (plus a fading ` SAVED ` after each save): `ETERNAL DIGGER`, gold, ladders, depth, bag; key help or the rescue
   progress bar. Strings rebuilt only when a value changes.

## Performance rules

Everything above runs every frame. No allocations per frame: no LINQ, no closures, no `new` of classes or arrays,
no string building unless a shown value changed. Effects are spawned through the command buffer only.

## Saving (added in Tier 0 of the feature handover)

Not in the original document. The game starts on `TitleScene` (`CONTINUE` / `NEW MINE`, Up/Down and E; NEW MINE
over an existing save asks `NO` / `YES, START OVER`). One slot, `SaveFile.DefaultPath`
(`<LocalApplicationData>/EternalDigger/save.dat`), binary with a format version (`SaveFile.Version`). It holds the
seed, the miner's tile, facing and inventory, and the tiles that differ from the generated mine
(`Mine.CollectChanges`), never the whole grid. Saves on a sale or purchase, every 60 s underground and on `ESC`;
never per frame. An unreadable or unknown-version file is moved to `save.dat.bad` and the title offers only NEW MINE.
