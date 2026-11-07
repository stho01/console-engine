# Reference

Lookup tables for EternalDigger: files, systems, components, services and constants. For explanations, follow the
links to the other docs.

## Files

```text
Examples/EternalDigger/
├── Program.cs                  seed from the first argument, creates and starts the game
├── EternalDiggerGame.cs        GameBase: 120 × 48, 8 × 16 font, ESC quits, sets MineScene
├── Layout.cs                   screen and tile sizes, surface spots, tile ↔ world helpers
├── Shop.cs                     prices and start inventory; Deliver, TryBuyLadder, Collect
├── MinerInput.cs               one frame of miner keys; FromKeyboard
├── Scenes/MineScene.cs         services, the miner entity, systems in run order
├── Mining/
│   ├── Mine.cs                 the endless seeded grid: At, Hit, TryPlaceLadder, IsSupported, Noise
│   ├── Tiles.cs                per-type rules: solid, breakable, ore value, hits, start depths
│   ├── TileType.cs             Air, Grass, Dirt, Stone, Granite, Copper, Silver, Gold, Bedrock
│   ├── TileCell.cs             one tile: Type, Damage, HasLadder, Seen
│   └── DigResult.cs            what a hit did: DigOutcome (Nothing, Damaged, Broken), Type, Hits
├── Components/
│   ├── Miner.cs                the player: grid position, motion, lamp, dig timers, inventory
│   └── Particle.cs             Particle, ParticleStyle, Physics, TileCollider, Mist, FloatingNumber
├── Systems/                    18 systems, listed below
├── Effects/
│   ├── EffectSpawner.cs        creates every particle and floating number
│   ├── ParticleLooks.cs        characters and colours per style over a lifetime
│   └── ScreenShake.cs          shake strength: Add, Update
├── Rendering/
│   ├── Canvas.cs               draws through RenderConsole and remembers backgrounds
│   ├── LightMap.cs             per-tile light on screen: daylight + headlamp cone
│   └── TileLooks.cs            TileLook per TileType; ladder and crack characters
├── README.md                   start here
└── docs/                       this documentation
```

## Systems, in run order

### Simulation (`MineScene.Simulation`)

| # | System | Constructor | Queries | Does |
|---|---|---|---|---|
| 1 | `MinerControlSystem` | `Mine`, `EffectSpawner` | `Miner, Position` | Reads `MinerInput`; rescue, fall, ladder, walk/dig; `Mine.Hit`, `Shop.Collect`, dig effects. Public `Control(...)` |
| 2 | `InteractionSystem` | `EffectSpawner` | `Miner` | `E` at the door or shop: `Shop.Deliver`/`TryBuyLadder`, coins, pops, numbers |
| 3 | `MinerMotionSystem` | `Mine`, `EffectSpawner` | `Miner, Position` | Slides `Position`, ends moves, foot and landing dust, counts down `DigCooldown`/`SwingTime`. Public `Move(...)` |
| 4 | `CameraSystem` | `Camera`, `RenderConsole`, `ScreenShake`, `Random` | `Miner, Position` | Vertical follow, shake, whole-cell `Camera.Position` |
| 5 | `LightingSystem` | `Mine`, `LightMap`, `Camera` | `Miner, Position` | `LightMap.Compute` for the rows on screen |
| 6 | `GlintSystem` | `Mine`, `LightMap`, `Camera`, `EffectSpawner`, `Random` | `Miner` | Ore glints near the miner, at a fixed rate |
| 7 | `MistSystem` | `Mine`, `LightMap`, `Camera`, `EffectSpawner`, `Random` | `Mist` | Keeps up to 40 mist particles in lit caves |
| 8 | `ParticlePhysicsSystem` | `Mine` | `Particle, Position, Velocity, Physics` | Gravity, drag, movement, landing on rock (`TileCollider`) |
| 9 | `ParticleLifetimeSystem` | — | `Particle, Glyph` | Ages particles, sets their glyph, destroys expired ones |
| 10 | `FloatingNumberSystem` | — | `FloatingNumber, Position, Velocity` | Moves numbers up, destroys expired ones |

### Presentation (`MineScene.Presentation`)

| # | System | Constructor | Queries | Draws |
|---|---|---|---|---|
| 1 | `SkyRenderSystem` | `Canvas`, `Camera` | — | Sky, sun, clouds, hills |
| 2 | `TerrainRenderSystem` | `Canvas`, `Camera`, `Mine`, `LightMap` | — | Tiles by light, ladders, cracks, grass |
| 3 | `HouseRenderSystem` | `Canvas`, `Camera` | — | House, sign, smoke, shop, mine frame |
| 4 | `MinerRenderSystem` | `Canvas`, `Camera` | `Miner, Position` | The miner and lamp glow |
| 5 | `ParticleRenderSystem` | `Canvas`, `Camera`, `LightMap` | `Particle, Position, Glyph` | Particles (dust and mist only where lit) |
| 6 | `FloatingNumberRenderSystem` | `Canvas`, `Camera` | `FloatingNumber, Position` | `+150g` / `-10g` |
| 7 | `PromptRenderSystem` | `Canvas`, `Camera` | `Miner, Position` | Door and shop prompts |
| 8 | `HudSystem` | `Canvas` | `Miner` | Rows 0–1 |

## Components

| Component | Fields | On |
|---|---|---|
| `Miner` | `Column`, `Row` (tile; the target while moving), `MoveFrom` (tile, `Vector2`), `MoveElapsed`, `MoveDuration` (0 = idle), `Motion` (`Idle`, `Walking`, `Climbing`, `Falling`), `FallRows`, `FacingX` (−1/1), `LampDirection`, `DigCooldown`, `SwingTime`, `SwingDirection` (`Point`), `RescueHold`, `Gold`, `Ladders`, `CopperOre`, `SilverOre`, `GoldOre`. Read-only: `IsMoving`, `OreCount` | The miner |
| `Particle` | `Age`, `Lifetime`, `Style` (`ParticleStyle`), `Variant` (random byte). Read-only: `Progress` | Every particle |
| `Physics` | `Gravity` (cells/s²), `Drag` (per second) | Every particle |
| `TileCollider` | tag | Debris, coins |
| `Mist` | tag | Mist |
| `FloatingNumber` | `Amount`, `Age`, `Lifetime` | Floating numbers |
| `Position`, `Velocity`, `Glyph` | engine components (`ConsoleEngine.Ecs.Components`) | Miner (`Position` only), particles, numbers (no `Glyph`) |

## Services

| Service | Created in | Used by | Key members |
|---|---|---|---|
| `Mine` | `MineScene` | control, motion, light, glints, mist, particle physics, terrain | `At`, `TypeAt`, `IsSolid`, `HasLadder`, `IsSupported`, `Hit`, `TryPlaceLadder`, `Noise`, `GeneratedRows`; static `Depth`, `IsInside` |
| `EffectSpawner` | `MineScene` | control, interaction, motion, glints, mist | `DigHit`, `TileBreak`, `FootDust`, `LandingDust`, `Glint`, `Mist`, `Coins`, `Pop`, `Number`, `Shake` |
| `ScreenShake` | `MineScene` (inside `EffectSpawner`) | `EffectSpawner`, `CameraSystem` | `Add`, `Update`, `Strength` |
| `LightMap` | `MineScene` | lighting, glints, mist, terrain, particles | `Compute`, `Get`, `FirstRow`, `Rows`; `Dark`, `Dim`, `Lit`, `Bright` |
| `Canvas` | `MineScene` | all Presentation systems | `Draw`, `DrawOver`, `BackgroundAt`, `Width`, `Height` |
| `Random(seed)` | `MineScene` | `EffectSpawner`, camera, glints, mist | — |

Static rule classes: `Tiles`, `Shop`, `Layout`, `TileLooks`, `ParticleLooks`.

## Constants

| Constant | Value | Meaning |
|---|---|---|
| `Layout.ScreenWidth` × `ScreenHeight` | 120 × 48 | Console size in cells |
| `Layout.TileWidth` × `TileHeight` | 4 × 2 | Cells per tile |
| `Layout.HudHeight` | 2 | Screen rows under the HUD |
| `Layout.GroundRow` | 9 | The surface walk row |
| `Layout.HouseFirstColumn`…`HouseLastColumn` | 2–11 | House drawing |
| `Layout.DeliverFirstColumn`…`DeliverLastColumn` | 3–6 | Door spot |
| `Layout.ShopFirstColumn`…`ShopLastColumn` | 8–11 | Shop spot |
| `Layout.SpawnColumn` | 15 | Start and rescue tile |
| `Mine.Columns` | 30 | Mine width |
| `Mine.SurfaceRow` | 10 | The grass row, depth 0 |
| `Mine.ShaftColumn` | 21 | The way down |
| `Mine.RowsPerBlock` | 64 | Rows generated at a time |
| `Tiles.CopperValue` / `SilverValue` / `GoldValue` | 15 / 40 / 100 | Gold per ore |
| `Tiles.CopperDepth` / `SilverDepth` / `GoldDepth` | 2 / 12 / 30 | First depth of each ore (m) |
| `Tiles.CaveDepth` / `GraniteDepth` | 6 / 40 | First depth of caves and granite (m) |
| `Tiles.DepthPerExtraHit` | 25 | Metres per extra hit |
| `Shop.LadderPrice` | 10 | Gold per ladder |
| `Shop.StartGold` / `StartLadders` | 0 / 8 | Start inventory |
| `MinerControlSystem.WalkDuration` / `ClimbDuration` / `FallDuration` | 0.13 / 0.17 / 0.06 s | Time per tile |
| `MinerControlSystem.DigInterval` / `SwingDuration` | 0.16 / 0.12 s | Time between hits / swing animation |
| `MinerControlSystem.RescueDuration` | 1.5 s | Hold time for a rescue |
| `LightMap.NearRadius` | 1.5 tiles | Always-bright radius |
| `LightMap.LampRange` / `BrightRange` / `LitRange` | 8 / 4 / 6 tiles | Lamp reach and fall-off |
| `LightMap.ConeHalfAngle` | 40° | Half the lamp cone |
| `MistSystem.MaxMist` | 40 | Mist particles at most |
| `ScreenShake.MaxStrength` / `Decay` | 2 cells / 6 per s | Shake cap and fade |

## User-visible text

| Text | Constant |
|---|---|
| ` ETERNAL DIGGER ` | `HudSystem.Title` |
| ` ←→ walk/dig   ↓ dig   ↑ climb ladder   SPACE place ladder   E deliver/buy   hold R rescue   ESC quit` | `HudSystem.KeyHelp` |
| ` RESCUE ` … ` the bag is lost` | `HudSystem.RescueLabel`, `RescueNote` |
| ` NO LADDERS LEFT!  Hold R to be rescued (the bag is lost) ` | `HudSystem.NoLaddersWarning` |
| ` DELIVER [E] ` | `PromptRenderSystem.DeliverText` |
| ` BUY LADDERS [E] 10g ` | `PromptRenderSystem.BuyText` |
| ` EternalDigger `, `LADDERS`, `MINE` | `HouseRenderSystem` (`SignBoard`, `ShopText`, `MineText`) |
| `EternalDigger` (window title) | `Program.cs` (`Name`) |
