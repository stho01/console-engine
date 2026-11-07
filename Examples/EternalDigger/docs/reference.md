# Reference

Lookup tables for EternalDigger: files, systems, components, services and constants. For explanations, follow the
links to the other docs.

## Files

```text
Examples/EternalDigger/
├── Program.cs                  seed from the first argument, creates and starts the game
├── EternalDiggerGame.cs        GameBase: 120 × 48, 8 × 16 font, ESC saves and quits, starts on TitleScene
├── Layout.cs                   screen and tile sizes, surface spots, tile ↔ world helpers
├── Shop.cs                     prices and start inventory; BagValue, Deliver, EmptyBag, TryBuyLadder, Collect
├── MinerInput.cs               one frame of miner keys; FromKeyboard
├── Scenes/
│   ├── MineScene.cs            services, the miner entity, systems in run order; Save() for Esc
│   ├── TitleScene.cs           loads the save, draws the title menu, starts MineScene
│   └── TitleMenu.cs            CONTINUE / NEW MINE and the confirmation, without drawing (testable)
├── Saving/
│   ├── SaveData.cs             what a save holds; NewMine, Capture, ToMiner, ApplyTo
│   ├── SaveFile.cs             binary versioned file: Save, Load, Read, Write, SaveStatus, DefaultPath
│   └── GameSaver.cs            the running mine's slot: Request, Save, Saves, LastSaveFailed
├── Mining/
│   ├── Mine.cs                 the endless seeded grid: At, Hit, TryPlaceLadder, IsSupported, WallBottomDepth, Noise
│   ├── Tiles.cs                per-type rules: solid, breakable, ore value, hits, start depths
│   ├── TileType.cs             Air, Grass, Dirt, Stone, Granite, Copper, Silver, Gold, Bedrock, then (act 2)
│   │                           Obsidian, Slate, Quartzite, Gem. New types go at the end (saves store the byte)
│   ├── TileCell.cs             one tile: Type, Damage, HasLadder, Seen
│   ├── TileChange.cs           a saved tile: Column, Row, Cell
│   └── Elevator.cs             the lift: TryBuild, Send, Update, landings, car, Restore; ElevatorBuild
│   ├── DigResult.cs            what a hit did: DigOutcome (Nothing, Damaged, Broken), Type, Hits
├── Components/
│   ├── Miner.cs                the player: grid position, motion, lamp, dig timers, inventory
│   └── Particle.cs             Particle, ParticleStyle, Physics, TileCollider, Mist, FloatingText
├── Systems/                    21 systems, listed below
├── Effects/
│   ├── EffectSpawner.cs        creates every particle and floating number
│   ├── ParticleLooks.cs        characters and colours per style over a lifetime; GlyphAt
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
| 1 | `MinerControlSystem` | `Mine`, `Elevator`, `EffectSpawner` | `Miner, Position` | Reads `MinerInput`; rescue, ride keys, fall, ladder, walk/dig; `Mine.Hit`, `Shop.Collect`, dig effects. Public `Control(...)` |
| 2 | `InteractionSystem` | `Mine`, `Elevator`, `EffectSpawner`, `GameSaver` | `Miner` | `E` at the door, shop, lift sign or a landing: `Shop.Deliver`/`TryBuyLadder`, `Elevator.TryBuild`, call/get in; coins, pops, texts; asks for a save. Public `Interact(...)` |
| 3 | `MinerMotionSystem` | `Mine`, `EffectSpawner` | `Miner, Position` | Slides `Position`, ends moves, foot and landing dust, counts down `DigCooldown`/`SwingTime`. Public `Move(...)` |
| 4 | `ElevatorSystem` | `Elevator` | `Miner, Position` | `Elevator.Update`; carries the rider, picks up a caller. Public `Carry(...)` |
| 5 | `CameraSystem` | `Camera`, `RenderConsole`, `ScreenShake`, `Random` | `Miner, Position` | Vertical follow, shake, whole-cell `Camera.Position` |
| 6 | `LightingSystem` | `Mine`, `LightMap`, `Camera` | `Miner, Position` | `LightMap.Compute` for the rows on screen |
| 7 | `GlintSystem` | `Mine`, `LightMap`, `Camera`, `EffectSpawner`, `Random` | `Miner` | Ore glints near the miner, at a fixed rate |
| 8 | `MistSystem` | `Mine`, `LightMap`, `Camera`, `EffectSpawner`, `Random` | `Mist` | Keeps up to 40 mist particles in lit caves |
| 9 | `ParticlePhysicsSystem` | `Mine` | `Particle, Position, Velocity, Physics` | Gravity, drag, movement, landing on rock (`TileCollider`) |
| 10 | `ParticleLifetimeSystem` | — | `Particle, Glyph` | Ages particles, sets their glyph, destroys expired ones |
| 11 | `FloatingTextSystem` | — | `FloatingText, Position, Velocity` | Moves floating texts up, destroys expired ones |
| 12 | `SaveSystem` | `GameSaver` | `Miner` | Saves on request, or after `GameSaver.AutosaveInterval` s underground |

### Presentation (`MineScene.Presentation`)

| # | System | Constructor | Queries | Draws |
|---|---|---|---|---|
| 1 | `SkyRenderSystem` | `Canvas`, `Camera` | — | Sky, sun, clouds, hills |
| 2 | `TerrainRenderSystem` | `Canvas`, `Camera`, `Mine`, `LightMap` | — | Tiles by light, ladders, cracks, grass |
| 3 | `HouseRenderSystem` | `Canvas`, `Camera` | — | House, sign, smoke, shop, mine frame, `LIFT` sign |
| 4 | `ElevatorRenderSystem` | `Canvas`, `Camera`, `Elevator` | — | Shaft walls and braces, cable, car, speed lines |
| 5 | `ParticleRenderSystem` | `Canvas`, `Camera`, `LightMap` | `Particle, Position, Glyph`; `Miner, Position` | Particles (dust and mist only where lit; none on the miner, capped and dimmed around him) |
| 6 | `MinerRenderSystem` | `Canvas`, `Camera` | `Miner, Position` | The miner and lamp glow, over the particles |
| 7 | `FloatingTextRenderSystem` | `Canvas`, `Camera` | `FloatingText, Position` | `+150g` / `-10g` |
| 8 | `PromptRenderSystem` | `Canvas`, `Camera`, `Mine`, `Elevator` | `Miner, Position` | Door, shop, lift sign and landing prompts |
| 9 | `HudSystem` | `Canvas`, `GameSaver`, `Elevator` | `Miner` | Rows 0–1, the save hint |

## Components

| Component | Fields | On |
|---|---|---|
| `Miner` | `Column`, `Row` (tile; the target while moving), `MoveFrom` (tile, `Vector2`), `MoveElapsed`, `MoveDuration` (0 = idle), `Motion` (`Idle`, `Walking`, `Climbing`, `Falling`), `ClimbStreak`, `IsRiding`, `FallRows`, `FacingX` (−1/1), `LampDirection`, `DigCooldown`, `SwingTime`, `SwingDirection` (`Point`), `RescueHold`, `Gold`, `Ladders`, `CopperOre`, `SilverOre`, `GoldOre`, `GemOre`. Read-only: `IsMoving`, `OreCount` | The miner |
| `Particle` | `Age`, `Lifetime`, `Style` (`ParticleStyle`), `Variant` (random byte). Read-only: `Progress` | Every particle |
| `Physics` | `Gravity` (cells/s²), `Drag` (per second) | Every particle |
| `TileCollider` | tag | Debris, coins |
| `Mist` | tag | Mist |
| `FloatingText` | `Kind` (`FloatingTextKind`: `Gold`, `Elevator`, `TooHard`), `Amount`, `Age`, `Lifetime` | Floating texts |
| `Position`, `Velocity`, `Glyph` | engine components (`ConsoleEngine.Ecs.Components`) | Miner (`Position` only), particles, numbers (no `Glyph`) |

## Services

| Service | Created in | Used by | Key members |
|---|---|---|---|
| `Mine` | `MineScene` | control, motion, light, glints, mist, particle physics, terrain, saving | `At`, `TypeAt`, `IsSolid`, `HasLadder`, `IsSupported`, `Hit`, `TryPlaceLadder`, `CollectChanges`, `Apply`, `WallBottomDepth`, `Noise`, `GeneratedRows`; static `Depth`, `IsInside` |
| `EffectSpawner` | `MineScene` | control, interaction, motion, glints, mist | `DigHit`, `TileBreak`, `FootDust`, `LandingDust`, `Glint`, `Mist`, `Coins`, `Pop`, `Number`, `Shake` |
| `ScreenShake` | `MineScene` (inside `EffectSpawner`) | `EffectSpawner`, `CameraSystem` | `Add`, `Update`, `Strength` |
| `LightMap` | `MineScene` | lighting, glints, mist, terrain, particles | `Compute`, `Get`, `FirstRow`, `Rows`; `Dark`, `Dim`, `Lit`, `Bright` |
| `Canvas` | `MineScene` | all Presentation systems | `Draw`, `DrawOver`, `BackgroundAt`, `Width`, `Height` |
| `Random(seed)` | `MineScene` | `EffectSpawner`, camera, glints, mist | — |
| `Elevator` | `MineScene` | control, interaction, `ElevatorSystem`, lift and prompt rendering, HUD, saving | `TryBuild`, `CanBuildNext`, `Send`, `PickedUp`, `Update`, `Restore`, `IsLanding`, `IsCarAt`, `Segments`, `Depth`, `BottomRow`, `NextSegmentPrice`, `CarRow`, `CarTargetRow`, `IsCarMoving`, `IsCalled`; consts `Column`, `TopRow` |
| `GameSaver` | `MineScene` | interaction, `SaveSystem`, HUD, `MineScene.Save` | `Request`, `Save`, `IsRequested`, `Saves`, `LastSaveFailed`, `Path`, `Seed` |

Static rule classes: `Tiles`, `Shop`, `Layout`, `TileLooks`, `ParticleLooks`, `SaveFile`.

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
| `Tiles.WallDepth` | 110 m | Top of the obsidian wall |
| `Tiles.WallMinThickness` / `WallMaxThickness` | 3 / 5 tiles | Wall thickness range, per column |
| `Tiles.HintDepth` | 95 m | From here, gems below the wall glint on screen at any distance |
| `Tiles.GemValue` | 250 | Gold per gem |
| `MinerControlSystem.TooHardInterval` | 1 s | Least time between two `TOO HARD` texts |
| `Tiles.DepthPerExtraHit` | 25 | Metres per extra hit |
| `Shop.LadderPrice` | 10 | Gold per ladder |
| `Shop.StartGold` / `StartLadders` | 0 / 8 | Start inventory |
| `MinerControlSystem.WalkDuration` / `ClimbDuration` / `FallDuration` | 0.13 / 0.17 / 0.06 s | Time per tile |
| `MinerControlSystem.ClimbRampStart` / `ClimbRampTiles` / `TopClimbDuration` | 4 tiles / 10 tiles / 0.06 s | The accelerating climb |
| `MinerControlSystem.DigInterval` / `SwingDuration` | 0.16 / 0.12 s | Time between hits / swing animation |
| `MinerControlSystem.RescueDuration` | 1.5 s | Hold time for a rescue |
| `LightMap.NearRadius` | 1.5 tiles | Always-bright radius |
| `LightMap.LampRange` / `BrightRange` / `LitRange` | 8 / 4 / 6 tiles | Lamp reach and fall-off |
| `LightMap.ConeHalfAngle` | 40° | Half the lamp cone |
| `MistSystem.MaxMist` | 40 | Mist particles at most |
| `ParticleRenderSystem.NearMinerCap` | 6 | Particles drawn per frame in the one-tile ring around the miner |
| `ScreenShake.MaxStrength` / `Decay` | 2 cells / 6 per s | Shake cap and fade |
| `GameSaver.AutosaveInterval` | 60 s | Time underground between autosaves (0 = off) |
| `HudSystem.SavedHintSeconds` | 1.5 s | How long ` SAVED ` shows |
| `SaveFile.Version` / `OldestVersion` | 3 / 1 | Save-format version written / oldest still read |
| `Elevator.SegmentMetres` | 10 m | Lift length per purchase |
| `Elevator.FirstSegmentPrice` / `SegmentPriceStep` | 200g / 100g | First segment price, increase per segment |
| `Elevator.RideDuration` | 0.04 s | Car time per tile |
| `Layout.LiftSignFirstColumn`…`LiftSignLastColumn` | 23–24 | Lift sign spot |
| `ElevatorRenderSystem.BraceEvery` | 5 m | Shaft brace spacing |

## User-visible text

| Text | Constant |
|---|---|
| ` ETERNAL DIGGER ` | `HudSystem.Title` |
| ` ←→ walk/dig   ↓ dig   ↑ climb ladder   SPACE place ladder   E deliver/buy   hold R rescue   ESC save & quit` | `HudSystem.KeyHelp` |
| ` SAVED ` / ` SAVE FAILED` | `HudSystem.SavedText`, `SaveFailedText` |
| `CONTINUE`, `NEW MINE`, `Start over? The saved mine will be lost.`, `NO`, `YES, START OVER` | `TitleMenu` (`ContinueText`, `NewMineText`, `ConfirmQuestion`, `NoText`, `YesText`) |
| `↑↓ choose   E select   ESC quit` | `TitleScene.KeyHelp` |
| `The saved mine could not be read. Starting a new mine.` | `TitleScene.CorruptMessage` |
| `The saved mine is from another version of the game. Starting a new mine.` | `TitleScene.UnsupportedMessage` |
| ` RESCUE ` … ` the bag is lost` | `HudSystem.RescueLabel`, `RescueNote` |
| ` NO LADDERS LEFT!  Hold R to be rescued (the bag is lost) ` | `HudSystem.NoLaddersWarning` |
| ` DELIVER [E] ` | `PromptRenderSystem.DeliverText` |
| ` BUILD LIFT +10m [E] <price>g ` | `PromptRenderSystem.BuildLiftText` |
| ` LIFT BLOCKED BY OBSIDIAN ` | `PromptRenderSystem.LiftBlockedText` |
| `TOO HARD` (floating) | `FloatingTextRenderSystem.TooHardText` |
| ` CALL LIFT [E] ` / ` GET IN [E] ` / ` ↑↓ RIDE   ←→ GET OUT ` | `PromptRenderSystem.CallLiftText`, `BoardLiftText`, `RideText` |
| `ELEVATOR +10m` (floating) | `FloatingTextRenderSystem.ElevatorLabel` + metres |
| `   LIFT <n>m` (HUD row 0) | `HudSystem.DrawStatus` |
| ` LIFT ` (sign) | `HouseRenderSystem.LiftText` |
| ` BUY LADDERS [E] 10g ` | `PromptRenderSystem.BuyText` |
| ` EternalDigger `, `LADDERS`, `MINE` | `HouseRenderSystem` (`SignBoard`, `ShopText`, `MineText`) |
| `EternalDigger` (window title) | `Program.cs` (`Name`) |
