# Reference

Lookup tables for EternalDigger: files, systems, components, services and constants. For explanations, follow the
links to the other docs.

## Files

```text
Examples/EternalDigger/
├── Program.cs                  seed from the first argument, creates and starts the game
├── EternalDiggerGame.cs        GameBase: 120 × 48, 8 × 16 font, ESC saves and quits, starts on TitleScene
├── Layout.cs                   screen and tile sizes, surface spots, tile ↔ world helpers
├── Shop.cs                     prices, weights and start inventory; BagValue, BagWeight, BagCapacity, WeightSteps,
│                               IsHeavy, ClimbLoad, OilLoad, Deliver, EmptyBag, TryBuyLadder,
│                               BlastingPrice, TryBuyBlasting, Price, Owned, TryBuy, Collect
├── MinerInput.cs               one frame of miner keys (also Use / UsePressed for E, Up/DownPressed); FromKeyboard
├── ShopCounter.cs              a counter's selection (Previous, Next, Select, Name, IsUpgrade, Line); ShopItem;
│                               Items (surface), CampItems (deep camp)
├── DeepCamp.cs                 the deep camp: Price, Row, columns, SaleShare, Markup; Build, SellBag, Sale, IsAtCounter
├── LampOil.cs                  the tank: TankSize, BurnRate, Reach, IsLow, FillPrice, TryFill; flasks: TryBuyFlask, TryPour
├── Upgrades.cs                 upgrade prices and effects per tier: NextPrice, TryBuy, DigInterval, FewerHits, LampReach,
│                               GlintRadius, GlintInterval; UpgradeLine
├── Scenes/
│   ├── MineScene.cs            services, the miner entity, systems in run order; Save() for Esc
│   ├── TitleScene.cs           loads the save, draws the title menu, starts MineScene
│   └── TitleMenu.cs            CONTINUE / NEW MINE and the confirmation, without drawing (testable)
├── Saving/
│   ├── SaveData.cs             what a save holds; NewMine, Capture, ToMiner, ApplyTo
│   ├── SaveFile.cs             binary versioned file: Save, Load, Read, Write, SaveStatus, DefaultPath
│   └── GameSaver.cs            the running mine's slot: Request, Save, Saves, LastSaveFailed
├── Mining/
│   ├── Mine.cs                 the endless seeded grid: At, Hit, TryPlaceLadder, TryTakeLadder, IsSupported,
│   │                           WallBottomDepth, IsBelowTheWall, IsBlackDamp, IsBlackDampEdge, Noise;
│   │                           generates gas in caves
│   ├── Tiles.cs                per-type rules: solid, breakable, ore value, hits, start depths
│   ├── TileType.cs             Air, Grass, Dirt, Stone, Granite, Copper, Silver, Gold, Bedrock, then (act 2)
│   │                           Obsidian, Slate, Quartzite, Gem, then Fossil, Relic. New types go at the end (saves
│   │                           store the byte)
│   ├── TileCell.cs             one tile: Type, Damage, HasLadder, Seen, HasGas
│   ├── GasPockets.cs           gas pockets: TryFindNear, Ignite (flood fill, preallocated), Burned
│   ├── TileChange.cs           a saved tile: Column, Row, Cell
│   └── Elevator.cs             the lift: TryBuild, Send, Update, landings, car, Restore; ElevatorBuild
│   ├── DigResult.cs            what a hit did: DigOutcome (Nothing, Damaged, Broken), Type, Hits
├── Components/
│   ├── Miner.cs                the player: grid position, motion, lamp, dig timers, dynamite, inventory
│   ├── Dynamite.cs             a planted, lit stick: Column, Row, Fuse, SparkTimer
│   └── Particle.cs             Particle, ParticleStyle, Physics, TileCollider, Mist, FloatingText
├── Systems/                    27 systems, listed below
├── Effects/
│   ├── EffectSpawner.cs        creates every particle and floating number
│   ├── ParticleLooks.cs        characters and colours per style over a lifetime; GlyphAt
│   └── ScreenShake.cs          shake strength: Add, Update
├── Rendering/
│   ├── Canvas.cs               draws through RenderConsole and remembers backgrounds
│   ├── LightMap.cs             per-tile light on screen: daylight + headlamp cone (reach, flicker dimming)
│   ├── Headlamp.cs             this frame's lamp: Reach, IsDimmed, IsLow, IsEmpty
│   └── TileLooks.cs            TileLook per TileType; ladder and crack characters
├── README.md                   start here
└── docs/                       this documentation
```

## Systems, in run order

### Simulation (`MineScene.Simulation`)

| # | System | Constructor | Queries | Does |
|---|---|---|---|---|
| 1 | `MinerControlSystem` | `Mine`, `Elevator`, `EffectSpawner` | `Miner, Position` | Reads `MinerInput`; rescue, ride keys, fall, ladder, walk/dig; `Mine.Hit`, `Shop.Collect`, dig effects. Public `Control(...)` |
| 2 | `InteractionSystem` | `Mine`, `Elevator`, `ShopCounter`, `EffectSpawner`, `GameSaver`, camp `ShopCounter` | `Miner` | `E` at the door, the counter (Up/Down choose), lift sign, a landing or the deep camp counter (sell the bag, camp prices; buying `DEEP CAMP` runs `DeepCamp.Build`): `Shop.Deliver`/`TryBuy`, `Elevator.TryBuild`, call/get in; coins, pops, texts; asks for a save. Public `Interact(...)` |
| 3 | `MinerMotionSystem` | `Mine`, `EffectSpawner` | `Miner, Position` | Slides `Position`, ends moves, foot and landing dust, counts down `DigCooldown`/`SwingTime`. Public `Move(...)` |
| 4 | `ElevatorSystem` | `Elevator` | `Miner, Position` | `Elevator.Update`; carries the rider, picks up a caller. Public `Carry(...)` |
| 5 | `DynamiteSystem` | `Mine`, `EffectSpawner`, `GasPockets` | `Dynamite, Position`; `Miner` | Fuses, sparks and smoke; blasts (clear 3 × 3, bag ore, destroy ladders, push, chain, big gas flares within 3 tiles). Public `Blast(...)` |
| 6 | `GasSystem` | `Mine`, `GasPockets`, `EffectSpawner`, `Random` | `Miner`; `Gas` | Gas shimmer near the miner; a lit lamp within 2 tiles flares the pocket (`GAS!`, lamp out 3 s, −5 oil). Public `Check(...)` |
| 7 | `CameraSystem` | `Camera`, `RenderConsole`, `ScreenShake`, `Random` | `Miner, Position` | Vertical follow, shake, whole-cell `Camera.Position` |
| 8 | `LampSystem` | `Headlamp`, `EffectSpawner`, `Random`, `Mine` | `Miner, Position` | Burns oil underground (×2 in black damp, none while blown out); shortens the cone below the wall and halves it in black damp (`BLACK DAMP` on entry); pours a carried flask into a low lamp (`OIL +40`); sets the `Headlamp`; lamp smoke when low. Public static `Burn(...)` |
| 9 | `LightingSystem` | `Mine`, `LightMap`, `Camera`, `Headlamp` | `Miner, Position` | `LightMap.Compute` for the rows on screen |
| 10 | `GlintSystem` | `Mine`, `LightMap`, `Camera`, `EffectSpawner`, `Random` | `Miner` | Ore glints near the miner, at a fixed rate |
| 11 | `MistSystem` | `Mine`, `LightMap`, `Camera`, `EffectSpawner`, `Random` | `Mist` | Keeps up to 40 mist particles in lit caves |
| 12 | `ParticlePhysicsSystem` | `Mine` | `Particle, Position, Velocity, Physics` | Gravity, drag, movement, landing on rock (`TileCollider`) |
| 13 | `ParticleLifetimeSystem` | — | `Particle, Glyph` | Ages particles, sets their glyph, destroys expired ones |
| 14 | `FloatingTextSystem` | — | `FloatingText, Position, Velocity` | Moves floating texts up, destroys expired ones |
| 15 | `SaveSystem` | `GameSaver` | `Miner` | Saves on request, or after `GameSaver.AutosaveInterval` s underground |

### Presentation (`MineScene.Presentation`)

| # | System | Constructor | Queries | Draws |
|---|---|---|---|---|
| 1 | `SkyRenderSystem` | `Canvas`, `Camera` | — | Sky, sun, clouds, hills |
| 2 | `TerrainRenderSystem` | `Canvas`, `Camera`, `Mine`, `LightMap` | — | Tiles by light, ladders, cracks, grass |
| 3 | `HouseRenderSystem` | `Canvas`, `Camera` | — | House, sign, smoke, shop, mine frame, `LIFT` sign |
| 4 | `ElevatorRenderSystem` | `Canvas`, `Camera`, `Elevator` | — | Shaft walls and braces, cable, car, speed lines |
| 5 | `CampRenderSystem` | `Canvas`, `Camera` | `Miner` | The deep camp once bought: tent, lantern, `CAMP` plank, crate |
| 6 | `DynamiteRenderSystem` | `Canvas`, `Camera` | `Dynamite` | Planted sticks `¡` and their flickering `*` fuse |
| 7 | `ParticleRenderSystem` | `Canvas`, `Camera`, `LightMap` | `Particle, Position, Glyph`; `Miner, Position` | Particles (dust and mist only where lit; none on the miner, capped and dimmed around him) |
| 8 | `MinerRenderSystem` | `Canvas`, `Camera`, `Headlamp` | `Miner, Position` | The miner and lamp glow, over the particles |
| 9 | `FloatingTextRenderSystem` | `Canvas`, `Camera` | `FloatingText, Position` | `+150g` / `-10g` / `ELEVATOR +10m` / `TOO HARD` |
| 10 | `PromptRenderSystem` | `Canvas`, `Camera`, `Mine`, `Elevator`, `ShopCounter` (surface), `ShopCounter` (camp) | `Miner, Position` | Door, shop, camp counter, lift sign and landing prompts, dynamite hints |
| 11 | `HudSystem` | `Canvas`, `GameSaver`, `Elevator` | `Miner` | Rows 0–1, the save hint |
| 12 | `InventorySystem` | `Canvas`, `Elevator`, `SystemGroup` (the Simulation) | `Miner` | `TAB` toggles; while open the Simulation systems are disabled (paused) and a centred panel lists gold, ladders, oil, flasks, dynamite, lift, the bag (count × price = worth, weight/capacity), upgrade pips and finds ever found |

## Components

| Component | Fields | On |
|---|---|---|
| `Miner` | `Column`, `Row` (tile; the target while moving), `MoveFrom` (tile, `Vector2`), `MoveElapsed`, `MoveDuration` (0 = idle), `Motion` (`Idle`, `Walking`, `Climbing`, `Falling`, `Jumping`), `ClimbStreak`, `IsRiding`, `FallRows`, `FacingX` (−1/1), `LampDirection`, `DigCooldown`, `SwingTime`, `SwingDirection` (`Point`), `RescueHold`, `PlantHold`, `HasBlastingKit`, `Dynamite`, `LitDynamite`, `Oil`, `OilFlasks`, `LampOut`, `HasDeepCamp`, `ShovelTier`, `LampTier`, `BagTier`, `DetectorTier`, `PickaxeTier`, `Gold`, `Ladders`, `CopperOre`, `SilverOre`, `GoldOre`, `GemOre`, `Fossils`, `Relics`, `FossilsFound`, `RelicsFound`. Read-only also `RareFinds`. Read-only: `IsMoving`, `OreCount` | The miner |
| `Particle` | `Age`, `Lifetime`, `Style` (`ParticleStyle`), `Variant` (random byte). Read-only: `Progress` | Every particle |
| `Physics` | `Gravity` (cells/s²), `Drag` (per second) | Every particle |
| `TileCollider` | tag | Debris, coins |
| `Mist` | tag | Mist |
| `Dynamite` | `Column`, `Row`, `Fuse`, `SparkTimer` | A planted stick (with `Position`) |
| `FloatingText` | `Kind` (`FloatingTextKind`: `Gold`, `Elevator`, `TooHard`, `RareFind`), `Amount`, `Age`, `Lifetime` | Floating texts |
| `Position`, `Velocity`, `Glyph` | engine components (`ConsoleEngine.Ecs.Components`) | Miner (`Position` only), particles, numbers (no `Glyph`) |

## Services

| Service | Created in | Used by | Key members |
|---|---|---|---|
| `Mine` | `MineScene` | control, motion, light, glints, mist, particle physics, terrain, saving | `At`, `TypeAt`, `IsSolid`, `HasLadder`, `IsSupported`, `Hit`, `TryPlaceLadder`, `CollectChanges`, `Apply`, `WallBottomDepth`, `Noise`, `GeneratedRows`; static `Depth`, `IsInside` |
| `EffectSpawner` | `MineScene` | control, interaction, motion, glints, mist | `DigHit`, `Clank`, `TileBreak`, `RareFind`, `Pulse`, `PlantDynamite`, `FuseSpark`, `Blast`, `ClimbTrail`, `FootDust`, `LandingDust`, `Glint`, `Mist`, `Coins`, `Pop`, `Number`, `Shake` |
| `ScreenShake` | `MineScene` (inside `EffectSpawner`) | `EffectSpawner`, `CameraSystem` | `Add`, `Update`, `Strength` |
| `LightMap` | `MineScene` | lighting, glints, mist, terrain, particles | `Compute` (with reach and flicker), `Get`, `FirstRow`, `Rows`; `Dark`, `Dim`, `Lit`, `Bright` |
| `Canvas` | `MineScene` | all Presentation systems | `Draw`, `DrawOver`, `BackgroundAt`, `Width`, `Height` |
| `Random(seed)` | `MineScene` | `EffectSpawner`, camera, glints, mist | — |
| `Elevator` | `MineScene` | control, interaction, `ElevatorSystem`, lift and prompt rendering, HUD, saving | `TryBuild`, `CanBuildNext`, `Send`, `PickedUp`, `Update`, `Restore`, `IsLanding`, `IsCarAt`, `Segments`, `Depth`, `BottomRow`, `NextSegmentPrice`, `CarRow`, `CarTargetRow`, `IsCarMoving`, `IsCalled`; consts `Column`, `TopRow` |
| `ShopCounter` | `MineScene` | interaction, prompts | `Selected`, `Previous`, `Next`, `Select`; static `Items`, `Name` |
| `Headlamp` | `MineScene` | `LampSystem` writes; lighting, miner render read | `Reach`, `IsDimmed`, `IsLow`, `IsEmpty` |
| `GameSaver` | `MineScene` | interaction, `SaveSystem`, HUD, `MineScene.Save` | `Request`, `Save`, `IsRequested`, `Saves`, `LastSaveFailed`, `Path`, `Seed` |

Static rule classes: `Tiles`, `Shop`, `Upgrades`, `Layout`, `TileLooks`, `ParticleLooks`, `SaveFile`.

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
| `Tiles.FossilValue` / `RelicValue` | 300 / 750 | Gold per rare find |
| `Tiles.FossilDepth` / `FossilChance` / `RelicChance` | 15 m / 0.15% / 0.1% | Where and how often rare finds appear |
| `MinerControlSystem.TooHardInterval` | 1 s | Least time between two `TOO HARD` texts |
| `Tiles.DepthPerExtraHit` | 25 | Metres per extra hit |
| `Shop.LadderPrice` | 10 | Gold per ladder |
| `Shop.BlastingKitPrice` / `DynamitePrice` | 2500 / 100 | The kit (once), one stick |
| `Shop.CopperWeight` / `SilverWeight` / `GoldWeight` / `GemWeight` / `RareFindWeight` | 1 / 1 / 2 / 2 / 3 | Bag weight per piece |
| `Shop.BaseBagCapacity` / `WeightStep` | 20 / 10 | Weight before the climb slows; weight per slowdown step |
| `Shop.ClimbSlowdownPerStep` / `MaxClimbSlowdown` / `OilPerStep` | 20% / 3× / +50% | Per step: slower climb up (capped), more oil while climbing up |
| `Upgrades` prices (shovel / pickaxe / lamp / bag / detector) | 300-900-2500 / 350-1000-2800 / 250-750-2000 / 200-600-1500 / 400-1200-3000 | Per tier |
| `Upgrades.PickaxeHitsPerTier` | 1 hit | Pickaxe: hits off every tile per tier (min 1 left) |
| `Upgrades.ShovelSpeedPerTier` / `LampReachPerTier` / `LampBurnPerTier` / `BagCapacityPerTier` / `DetectorRadiusPerTier` / `DetectorIntervalPerTier` | 15% / 2 tiles / ×0.8 / 10 / 3 tiles / ×0.8 | Effect per tier |
| `GlintSystem.Interval` / `Radius` | 0.08 s / 9 tiles | Starting detector |
| `LampOil.TankSize` / `SecondsPerUnit` / `LowShare` / `LowReach` / `PricePerUnit` | 100 / 3 s / 30% / 3 tiles / 2g | The tank |
| `LampOil.FlaskPrice` / `FlaskUnits` / `MaxFlasks` | 60g / 40 / 5 | Oil flasks: price, units poured, pack size |
| `Tiles.GasDepth` / `GasChance` | 50 m / 30% | Where gas pockets are |
| `Tiles.BlackDampChance`, `LampOil.ActTwoAbsorb` / `BlackDampReachShare` / `BlackDampBurn` | 12%, 2 tiles / ×0.5 / ×2 | Darkness below the wall |
| `GasSystem.TriggerRadius` / `LampOutSeconds` / `OilDrain` / `MaxShimmer` / `ShimmerRange` | 2 tiles / 3 s / 5 / 30 / 9 tiles | Gas flare and shimmer |
| `DynamiteSystem.GasReach`, `GasPockets.MaxPocket` | 3 tiles, 24 tiles | Gas set off by a blast; most tiles one flare burns |
| `DeepCamp.Price` / `Depth` / `SaleShare` / `Markup` / `LanternRadius` | 1500g / 100 m / 70% / ×1.5 / 2.6 tiles | The deep camp |
| `HudSystem.OilGaugeCells` | 8 | Oil gauge cells |
| `MinerControlSystem.PlantDuration` / `FuseDuration` | 0.5 / 2 s | Hold to plant, fuse |
| `DynamiteSystem.BlastRadius` / `ChainDelay` / `PushDuration` | 1 tile (3 × 3) / 0.12 s / 0.1 s | Blast size, fuse left on a stick a blast reaches, push slide |
| `Layout.BlastingFirstColumn`…`BlastingLastColumn` | 26–28 | Blasting shed (scenery) |
| `Shop.StartGold` / `StartLadders` | 0 / 8 | Start inventory |
| `MinerControlSystem.WalkDuration` / `ClimbDuration` / `FallDuration` | 0.13 / 0.17 / 0.06 s | Time per tile |
| `MinerControlSystem.JumpTiles` / `JumpDuration` / `JumpHeight` | 2 tiles / 0.28 s / 2 cells | A jump: distance, time, arc height (half under rock) |
| `MinerControlSystem.ClimbRampStart` / `ClimbRampTiles` / `TopClimbDuration` | 4 tiles / 10 tiles / 0.06 s | The accelerating climb |
| `MinerControlSystem.DigInterval` / `SwingDuration` | 0.16 / 0.12 s | Time between hits / swing animation |
| `MinerControlSystem.RescueDuration` | 1.5 s | Hold time for a rescue |
| `LightMap.NearRadius` | 1.5 tiles | Always-bright radius |
| `LightMap.LampRange` / `BrightRange` / `LitRange` | 8 / 4 / 6 tiles | Starting lamp reach and fall-off (bright and lit scale with the reach: ½ and ¾) |
| `LightMap.ConeHalfAngle` | 40° | Half the lamp cone |
| `MistSystem.MaxMist` | 40 | Mist particles at most |
| `ParticleRenderSystem.NearMinerCap` | 6 | Particles drawn per frame in the one-tile ring around the miner |
| `ScreenShake.MaxStrength` / `Decay` | 2 cells / 6 per s | Shake cap and fade |
| `GameSaver.AutosaveInterval` | 60 s | Time underground between autosaves (0 = off) |
| `HudSystem.SavedHintSeconds` | 1.5 s | How long ` SAVED ` shows |
| `SaveFile.Version` / `OldestVersion` | 7 / 1 | Save-format version written / oldest still read |
| `Elevator.SegmentMetres` | 10 m | Lift length per purchase |
| `Elevator.FirstSegmentPrice` / `SegmentPriceStep` | 200g / 100g | First segment price, increase per segment |
| `Elevator.RideDuration` | 0.04 s | Car time per tile |
| `Layout.LiftSignFirstColumn`…`LiftSignLastColumn` | 23–24 | Lift sign spot |
| `ElevatorRenderSystem.BraceEvery` | 5 m | Shaft brace spacing |

## User-visible text

| Text | Constant |
|---|---|
| ` ETERNAL DIGGER ` | `HudSystem.Title` |
| ` ←→↓ walk/dig  ↑ climb  ↑+←→ jump  SPACE ladder  E use  hold E+arrow dynamite  TAB inventory  hold R rescue  ESC quit` | `HudSystem.KeyHelp` |
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
| ` HOLD [E] + ARROW: DYNAMITE ` / ` TOO HARD: DYNAMITE AT THE SHOP ` | `PromptRenderSystem.PlantHintText`, `NeedDynamiteText` |
| ` ↑ <ITEM> ↓ ` / `HAVE <n>` or `UNLOCKS DYNAMITE` / `<price>g [E]` or `SOLD OUT` (counter panel) | `PromptRenderSystem.DrawCounterPanel`, `KitUnlocksText`, `SoldOutText`, `MaxTierText` (`MAX`), `FullText` (`FULL`), `OIL <n>%`, pips `TierOn`/`TierOff` (`●`/`○`); item names `Shop.LadderName`, `BlastingKitName`, `DynamiteName`, `Upgrades.ShovelName`, `PickaxeName`, `LampName`, `BagName`, `DetectorName`, `LampOil.FillName` (`FILL LAMP`), `FlaskName` (`OIL FLASK`) |
| ` LIGHTING FUSE ` + bar | `HudSystem.PlantLabel` |
| ` INVENTORY `, ` TAB close `, `PAUSED`, `BAG`, `HEAVY`, `UPGRADES`, `FOUND FOR GOOD` (inventory panel) | `InventorySystem.Title`, `CloseHint`, `PausedText`, `InventorySystem.Build` |
| ` ↑ SELL BAG ↓ `, `HOME PAYS <n>g`, `BAG IS EMPTY`, `AT 100m BY THE LIFT`, `BUILT`, `KIT: SURFACE ONLY`, `CAMP` (plank) | `DeepCamp.SellBagName`, `PromptRenderSystem.EmptyBagText`, `CampNotBuiltText`, `CampBuiltText`, `KitAtSurfaceText`, `CampRenderSystem.SignText`; item name `DeepCamp.Name` (`DEEP CAMP`) |
| `  ¡<n>` (dynamite), `  OIL ▮▮▮▮▮▯▯▯`, `╟╢<n>` (ladders) (HUD row 0) | `HudSystem.DrawStatus` |
| `BLASTING`, ` ¡ ¡ ¡ ` (shed) | `HouseRenderSystem.BlastingText`, `BlastingDisplay` |
| `OIL +<n>` (floating) | `FloatingTextRenderSystem.OilLabel` |
| `GAS!` (floating) | `FloatingTextRenderSystem.GasText` |
| `BLACK DAMP` (floating) | `FloatingTextRenderSystem.BlackDampText` |
| `TOO HARD` (floating) | `FloatingTextRenderSystem.TooHardText` |
| `FOSSIL!` / `RELIC!` (floating) | `FloatingTextRenderSystem.FossilText`, `RelicText` |
| ` CALL LIFT [E] ` / ` GET IN [E] ` / ` ↑↓ RIDE   ←→ GET OUT ` | `PromptRenderSystem.CallLiftText`, `BoardLiftText`, `RideText` |
| `ELEVATOR +10m` (floating) | `FloatingTextRenderSystem.ElevatorLabel` + metres |
| `  LIFT <n>m` (HUD row 0) | `HudSystem.DrawStatus` |
| ` LIFT ` (sign) | `HouseRenderSystem.LiftText` |
| ` EternalDigger `, `SHOP`, `MINE` | `HouseRenderSystem` (`SignBoard`, `ShopText`, `MineText`) |
| `EternalDigger` (window title) | `Program.cs` (`Name`) |
