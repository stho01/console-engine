# 09 — Glossary

Support/user words are **[inferred]**. The team may also use Norwegian terms. Add them in the first column.

| User / support word | Meaning | Code identifier |
|---|---|---|
| the game / the app | One example exe built on the engine | `GameBase` subclass: `TerraformGame`, `SnakeGame`, `AsteroidsGame`, `PlatformerGame`, `ConsoleGame.Game`, and the ECS ports `TerraformEcsGame`, `SnakeEcsGame`, `AsteroidsEcsGame`, `PlatformerEcsGame`, `ConsoleEcsGame`, the ECS stress test `SwarmGame`, the physics demos `PhysicsSandboxGame` and `PhysicsShowcaseGame`, the ECS game `EternalDiggerGame`, and the Lua demo `ScriptedGame` |
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
| rigid body / body type | An entity the physics moves: static (never), kinematic (by its velocity only), dynamic (velocity, gravity, later contacts) | `RigidBody`, `BodyType` |
| collider / shape | The circle or box physics tests for an entity, plus its collision filter | `Collider`, `ShapeType` |
| collision layer / category / mask | Bits that say which colliders may touch each other | `Collider.Category`, `Collider.Mask`, Asteroids.Ecs `CollisionLayers` |
| sensor / trigger | A collider that only reports overlaps, never pushes | `Collider.IsSensor`, `SensorEvent` |
| contact / hit / collision event | Two colliders started or stopped touching | `ContactEvent`, `PhysicsSystem.ContactBeginEvents` / `ContactEndEvents` / `SensorBeginEvents` / `SensorEndEvents` |
| broadphase | Fast search for pairs that might touch, before the exact test | `SpatialHashGrid` (narrowphase: `Collision`) |
| solver / contact response / bounce / friction | What stops bodies going through each other, makes them slide or bounce | `ContactSolver`, `Collider.Friction`, `Collider.Restitution`, `PhysicsSystem.RestitutionThreshold` |
| substep | One of the slices a physics step is cut into for the solver (default 4) | `PhysicsSystem.SubstepCount` |
| soft contact / soft step | Contacts act as stiff, damped springs instead of hard corrections (Box2D v3) | `Softness`, `PhysicsSystem.ContactHertz`, `ContactDampingRatio`, `ContactPushMaxSpeed` |
| warm starting | Starting the solver with the previous step's impulses, so stacks rest instead of jitter | `PairCache.Add` / `StoreImpulses`, `ContactConstraint.NormalImpulse` |
| physics events interface | What game code depends on instead of the concrete physics system | `IPhysicsEvents` |
| tile grid / level collision | Solid and empty tiles the physics queries and characters collide with, without one entity per wall | `TileGrid`, `PhysicsSystem.TileGrid`, Platformer.Ecs `TileMap.ToTileGrid` |
| raycast / box cast / overlap | Asking the physics what is along a line, in the way of a moving box, or inside an area | `IPhysicsQueries.Raycast`, `CastBox`, `OverlapBox`, `OverlapCircle`, `CastHit` |
| character controller / move and slide | Moving a player box that stops at walls and slides along them, not pushed by physics | `CharacterMover.MoveAndSlide`, `SlideCollisions` |
| polygon shape | A convex shape of up to 8 corners, shared by id | `PolygonLibrary`, `Collider.Polygon`, `ConvexPolygon` |
| spin / turning body | A body that rotates from contacts and friction | `Rotation` + `AngularVelocity`, `RigidBody.AngularDamping` |
| manifold / contact points | Where two shapes touch: a normal and up to 2 points | `Manifold`, `ManifoldPoint`, `Collision` |
| sleeping / asleep / island | Bodies at rest that the physics stops simulating until something wakes them; an island is a group of touching bodies that sleep and wake together | `RigidBody.IsSleeping`, `PhysicsSystem.EnableSleep`, `TimeToSleep`, `SleepingBodyCount` |
| interpolation / smoothing | Drawing a body between its last two fixed steps so it glides instead of jumping | `Interpolation`, `InterpolationSystem`, engine `RenderOffset`, `FixedTimestep.Alpha` |
| speculative contact | A contact created before two bodies touch, because they can close the gap this step; stops tunnelling | `BodyProxy.Margin`, `PhysicsSystem.CreateContacts` |
| moving platform | A kinematic body moved by its velocity; never pushed, carries what stands on it by friction | `BodyType.Kinematic`, PhysicsSandbox `Patrol` / `PatrolSystem` |
| fixed step / fixed timestep | A simulation step of constant length (default 1/60 s), run 0 or more times per frame so physics doesn't depend on the frame rate | `FixedTimestep`, `EcsScene.FixedSimulation` |
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
| script / Lua script | A `.lua` file that returns a module table with `on_start`/`on_update`/`on_render` | `LuaScript`, `LuaRuntime.Load` |
| script on an entity / scripted entity | An entity that runs a Lua script | `Script` component (`LuaScript.Component`), `LuaScriptSystem`, `LuaRenderSystem` |
| `self` / entity fields | The Lua view of an entity: its `Position` (`x`, `y`), `Velocity` (`vx`, `vy`), `Glyph` (`char`, `color`, `background`), plus `data` | `run` in `LuaRuntime.EngineSource`, `LuaRuntime.RunHook`/`ReadResults` |
| script data | A script's own per-entity state | `self.data` |
| hook | `on_start`, `on_update`, `on_render` in a script's module | `LuaScript.OnStart`/`OnUpdate`/`OnRender` |
| host function | A C# function callable from Lua (`input.*`, `console.*`, `print`) | `LuaRuntime.RegisterApi` |
| reload / hot reload / F5 | Re-run scripts and update their modules in place | `LuaScript.Reload`, `LuaRuntime.ReloadAll` |
| script error / broken script / faulted | A Lua error stopped an entity's script until the script reloads | `ScriptFaulted` tag, `LuaScriptSystem.GetError`, `LuaScriptException` (load errors) |
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
| record / trophy case / house level / tool shop / toolsmith (EternalDigger) | Surface progression: the deepest depth on a sign, finds on shelves, the house growing at 2000/8000/20000g earned, a bundle shop with an NPC once past the wall | `Progress`, `Miner.DepthRecord`, `GoldEarned`, `PassedTheWall`, `HouseRenderSystem` |
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

| climber / player (SkyStack) | The SkyStack player, 2 × 2 cells | `Climber` component, `ClimbPhysics` |
| tower / floor / platform / ledge (SkyStack) | The endless tower and its one-way platforms, numbered from the ground (0) up | `Tower`, `Floor`, `Tower.GetFloor` |
| combo (SkyStack) | Landings 2+ floors higher in a row, each within 3 s of the last; pays `floors × jumps × 5` | `RunStats.ComboJumps`, `ComboFloors` |
| scroll / rising view / speed level / "hurry" (SkyStack) | The view rising by itself after floor 5, faster every 30 s | `Scroller`, `Scroller.Level` |
| zone (SkyStack) | The colour theme of 50 floors | `TowerLooks.Zones` |
| pixel / half-block (SkyStack) | One of the two square pixels a console cell shows in SkyStack (`▀`: upper = foreground, lower = background) | `PixelCanvas`, `PixelSprite`, `PixelFont` |
