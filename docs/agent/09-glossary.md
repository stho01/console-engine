# 09 — Glossary

Support/user words are **[inferred]**. The team may also use Norwegian terms. Add them in the first column.

| User / support word | Meaning | Code identifier |
|---|---|---|
| the game / the app | One example exe built on the engine | `GameBase` subclass: `TerraformGame`, `SnakeGame`, `AsteroidsGame`, `PlatformerGame`, `ConsoleGame.Game`, and the ECS ports `TerraformEcsGame`, `SnakeEcsGame`, `AsteroidsEcsGame`, `PlatformerEcsGame`, `ConsoleEcsGame`, the ECS stress test `SwarmGame`, and the ECS game `EternalDiggerGame` |
| the engine | The shared library | `ConsoleEngine` project, namespace `ConsoleEngine` |
| window / screen | The console window and its cell buffer | `RenderConsole`, `ConsoleHandler`, `IConsoleHandler` |
| pixel / cell / character | One console cell | `Pixel` (`Char`, `ForegroundColor`, `BackgroundColor`) |
| resolution / size | Columns × rows of cells | `GameBase(width, height, …)`, `RenderConsole.Width`/`Height`/`Area` |
| font size | Pixel size of each cell | `FontInfo` (`FontFace`, `FontWidth`, `FontHeight`), `ConsoleHandler.SetFont` |
| logger / log window | Second console window for messages/FPS | `ConsoleEngine.Logger.exe`, `Log`, `GameBase.EnableLogger` |
| FPS / frame rate | Frames per second | `GameTime.Fps`, `GameBase.ShowFps`, `Log.ReportFps` |
| key press / tap | Key went down this frame | `KeyState.Pressed` |
| holding a key | Key is down | `KeyState.Held` |
| screen / page / menu | A scene on the scene stack | `Scene`, `SceneManager` (`Push`, `Pop`, `Set`) |
| timer | Repeating callback | `GameTime.SetInterval`, `GameTime.Interval` |
| level / map / world | A TerraForM map | `World`, `WorldLoader`, `Assets/Maps/mapN.txt`, `GameScene`. Not the same as the ECS `ConsoleEngine.Ecs.World` |
| ECS / entity | A game object as an id with components attached | `ConsoleEngine.Ecs.Entity` (`Id`, `Version`), `World` |
| component | A plain `unmanaged` data struct on an entity | `World.Add<T>`, `Get<T>`, `ComponentType<T>` |
| tag | A component with no fields: only marks an entity | `ComponentType<T>.IsTag` |
| archetype | All entities with exactly the same component set | `Archetype`, `ComponentMask` |
| chunk | 16 KiB block of native memory with the rows of one archetype, column by column | `Chunk`, `ChunkPool`, `EntityChunk` (query view) |
| query | The entities that have a given set of components | `Query`, `QueryDescription` (`With`, `Without`), `IForEach<…>` |
| system | Game logic that runs over queries each frame | `EntitySystem` subclasses: engine `MovementSystem`, `GlyphRenderSystem`, `OutlineRenderSystem`, `SpriteRenderSystem`; game ones in `Examples/Asteroids.Ecs/Systems/` |
| system group / sync point | Ordered list of systems. After the last one, deferred changes are applied | `SystemGroup`, `EcsScene.Simulation` / `Presentation` |
| command buffer / deferred change | Create/destroy/add/remove recorded during a loop, applied later | `CommandBuffer`, `EntitySystem.Commands` |
| deferred handle / deferred entity | Handle returned by a buffered Create, usable only in the same buffer until playback | Negative `Entity.Id`, `CommandBuffer.Create`, `CommandBuffer.Resolve` |
| system order | Which system in a group runs first | `SystemGroup.RunOrder`, `UpdateBeforeAttribute`, `UpdateAfterAttribute` |
| level name | Text after `NOW ENTERING` | map `!name` → `World.Name` |
| rover / player | TerraForM player object | `Rover` |
| power / energy / battery / kWh | Fuel for moves | `Rover.RemainingPower`, `MaxPower`, map `!power` |
| moves left / turns / sequences | Number of SPACE commits allowed | `Rover.RemainingSequences`, map `!sequences` |
| commands / move queue | Moves/plants waiting to run | `Command`, `Move`, `Plant`, `Wait`, `Hud._commandSequence`, `GameScene._currentCommands` |
| send / execute / go | SPACE commit | `GameScene.CommitCommands` |
| plant / generator / coil | Plant at a plant spot | `Rover.Plant`, `PlantSpot`, `AthmosphereGeneratorsPlanted` |
| bonus / battery pickup | +1000 power, +10 000 score | `BonusPoint` |
| rock / wall | Blocking tile, +10 damage | `Rock` (map char `H`) |
| crater / hole / pit | Blocking tile, power → 0 | `Craves` (map char `C`) |
| goal / finish / power plant | Level exit | `FinishPoint` (map char `F`) |
| start | Spawn tile | `StartingPoint` (map char `S`) |
| damage | Collision counter | `Rover.DamageTaken` |
| score / points | Running score | `TerraformGame.Score` |
| game over | HUD state, not a stop | `GameScene.GameOver`, `Sprites.GameOver` |
| restart | Reload same map | `R` → `Scenes.Set(new GameScene(_worldName))` |
| player name | Typed on first screen (PII) | `TerraformGame.Playername`, `InputNameScene` |
| debug mode | Developer overlay | `PlatformerGame.IsDebugMode` (F1), `TerraformGame.IsDebugMode` (unreachable) |
| launcher / menu / game picker | Text menu that starts the ECS games, one at a time | `Examples/Launcher`: `LauncherApp`, `GameCatalog`, `GameLocator`, `GameRunner` |

| miner / digger | EternalDigger player | `Miner` component, `MinerControlSystem` |
| jump / hop (EternalDigger) | Up + a side key: two tiles sideways over a one-tile gap | `MinerMotion.Jumping`, `MinerControlSystem.CanJump` |
| mine / map / world (EternalDigger) | The endless tile grid | `Mine`, `TileCell`, `TileType`. Not the ECS `World` |
| lift / elevator / car / landing / segment (EternalDigger) | The buyable lift in the mine shaft: 10 m segments, a car that stops at the top and bottom landings | `Elevator`, `ElevatorSystem`, `Miner.IsRiding` |
| wall / obsidian (EternalDigger) | The unbreakable-by-shovel band at 110 m that ends act 1 | `TileType.Obsidian`, `Tiles.WallDepth`, `Mine.WallBottomDepth` |
| act 1 / act 2 (EternalDigger) | The layers above and below the wall: dirt/stone/granite with copper/silver/gold, then slate/quartzite with gems | `Mine.Underground`, `Mine.BelowTheWall` |
| gem (EternalDigger) | The act 2 ore, 250g, glints magenta | `TileType.Gem`, `Miner.GemOre` |
| deep camp / camp / tent / trading post (EternalDigger) | The 1500g camp at 100 m beside the lift's bottom landing: sells the bag at 70%, supplies at ×1.5 | `DeepCamp`, `Miner.HasDeepCamp`, `CampRenderSystem` |
| gas / gas pocket / flare / shimmer (EternalDigger) | Gas in deep caves: a lit lamp within 2 tiles makes it flare and blows the lamp out for 3 s; harmless | `TileCell.HasGas`, `GasPockets`, `GasSystem`, `Miner.LampOut` |
| black damp / bad air / darkness (EternalDigger) | Rare areas below the wall that halve the lamp's cone and double its burn; act 2 rock also cuts the cone by 2 tiles | `Mine.IsBlackDamp`, `LampOil.DarkReach` |
| dynamite / stick / kit / blast (EternalDigger) | The tool through the wall: the kit unlocks buying sticks; a planted stick blows up 3 × 3 | `Miner.HasBlastingKit`, `Miner.Dynamite`, `Dynamite`, `DynamiteSystem` |
| oil / lamp oil / tank (EternalDigger) | What the headlamp burns underground; low oil shrinks the light | `LampOil`, `Miner.Oil`, `Headlamp` |
| rare find / fossil / relic / trophy (EternalDigger) | Very rare single tiles that sell high: `§` fossils in act 1, `¥` relics below the wall | `TileType.Fossil`/`Relic`, `Tiles.IsRareFind`, `Miner.FossilsFound` |
| bag weight / heavy bag (EternalDigger) | What the ore and finds weigh; over the capacity the climb up slows | `Shop.BagWeight`, `Shop.WeightSteps` |
| upgrade / tier (EternalDigger) | Shovel, lamp, bag and detector improvements, three tiers each, bought at the counter | `Upgrades`, `UpgradeLine`, `Miner.ShovelTier`… |
| save / slot / continue (EternalDigger) | The one save file and loading it | `SaveFile`, `SaveData`, `GameSaver`, `TitleMenu.ContinueText` |
| changed tiles (EternalDigger) | Tiles that differ from what the seed generates; what a save stores of the mine | `TileChange`, `Mine.CollectChanges` |
| shaft / mine entrance | The only way down from the surface, column 21 under the `MINE` frame | `Mine.ShaftColumn` |
| bag / inventory | Ore carried, not yet sold | `Miner.CopperOre`, `SilverOre`, `GoldOre` |
| gold / money | Currency from deliveries | `Miner.Gold` |
| deliver / sell | Sell the bag at the house door | `Shop.Deliver`, `InteractionSystem` |
| ladder | Placed in a dug tile so the miner can stand on it and climb | `TileCell.HasLadder`, `Mine.TryPlaceLadder`, `Shop.TryBuyLadder` |
| rescue | Hold R: back to the surface, bag lost | `MinerControlSystem.UpdateRescue` |
| depth / metres | Rows below the grass | `Mine.Depth` |
| headlamp / light / darkness | What the miner can see | `LightMap`, `Miner.LampDirection` |
| glint / sparkle | Twinkle that reveals ore in the dark | `GlintSystem`, `ParticleStyle.Glint` |
| seed | Number that decides the mine's layout | `Mine(int seed)`, first command-line argument |

