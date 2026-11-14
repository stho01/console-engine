# 08 — What support / the user can do

There is no admin UI, permission model or audit trail. Every action is either something the **user does on their own
machine** or a **code/asset change by a developer**. Scope below is **[inferred]** from the code. Correct it if the
team's process differs.

## Self-serve (no code change)

| Action | Effect | Use for |
|---|---|---|
| Launch the exe from its own output folder (double-click it there, or `cd` there first) | Relative paths resolve: `Assets/Maps/*.txt`, `ConsoleEngine.Logger.exe` | [P1](05-triage.md#p1) |
| Double-click `start-conhost.cmd` in the game's output folder (developers: `run-game.ps1 <Game>`) | Starts the game in `conhost.exe` from its own folder, so size/font calls take effect and relative paths resolve | [P1](05-triage.md#p1), [P2](05-triage.md#p2) |
| Or make the classic console the default: Settings → System → For developers → Terminal → "Windows Console Host" **[external]** | Every console app opens in conhost | [P2](05-triage.md#p2) |
| Lower display scaling / use a larger screen | Window can reach the requested size | [P2](05-triage.md#p2) |
| Close the other process holding port 11000 / the other game instance | `Log.Start` can bind | [P1](05-triage.md#p1) |
| Close the window | Only way to exit every game except ConsoleGame, ConsoleGame.Ecs, Swarm, PhysicsSandbox, PhysicsShowcase and EternalDigger | [P5](05-triage.md#p5) |
| Close a leftover logger window | — | [P5](05-triage.md#p5) |
| Start the exe from an open terminal to keep crash text | Captures the exception | [06 § Capture](06-diagnostics.md#capture) |
| Start an ECS game from the launcher (`dotnet run --project Examples/Launcher`) | Builds the games, then starts the chosen one in its own classic console window (`conhost.exe`, Windows) or in the same terminal (macOS/Linux) from its output folder, so relative paths resolve | [P16](05-triage.md#p16) |

## In-game controls

| Game | Key | Effect | Code |
|---|---|---|---|
| TerraForM name screen | A–Z, 0–9, SPACE / BACKSPACE / ENTER | Type / delete / accept | `InputNameScene.OnUpdate` |
| TerraForM menu | `H` | Start level (first time) or resume | `MenuScene.OnUpdate` |
| TerraForM level | ←→↑↓ | Queue a move | `Hud.OnUpdate` |
| TerraForM level | `P` | Queue a plant | `Hud.OnUpdate` |
| TerraForM level | `SPACE` | Send queued commands (uses one sequence) | `GameScene.CommitCommands` |
| TerraForM level | `R` | **Restart this map.** Score is **not** reset | `GameScene.OnUpdate` |
| TerraForM level | `ESC` | Open menu (level paused and hidden) | `GameScene.OnUpdate` |
| TerraForM.Ecs | Same keys as TerraForM above (name screen, menu, level) | Same effects | `InputNameScene`, `MenuScene`, `LevelInputSystem`, `RestartSystem` in `Examples/TerraForM.Ecs` |
| Snake | W A S D | Turn | `SnakeGame.OnUpdate` |
| Snake.Ecs | W A S D | Turn | `SteeringSystem.OnUpdate` |
| Asteroids | A D / W | Steer / thrust | `AsteroidsGame.OnUpdate` |
| Asteroids.Ecs | A D / W / `SPACE` | Steer / thrust / fire | `ShipControlSystem.OnUpdate` |
| Platformer | A D / SPACE / F1 | Move / jump / toggle debug | `PlatformerGame.OnUpdate` |
| Platformer.Ecs | A D / SPACE / F1 | Move / jump / toggle debug | `PlayerControlSystem`, `DebugToggleSystem` |
| ConsoleGame | ESC | Quit (`Stop()`) | `Game.OnUpdate` |
| ConsoleGame.Ecs | ESC | Quit (`Stop()`) | `ConsoleEcsGame.OnUpdate` |
| Swarm | SPACE / BACKSPACE / ESC | +10 000 particles / −10 000 particles / quit | `SpawnControlSystem`, `SwarmGame.OnUpdate` |
| PhysicsSandbox | SPACE / B / ENTER / R / ESC | Drop a ball / a box / 50 balls / remove all dropped / quit | `DropSystem`, `PhysicsSandboxGame.OnUpdate` |
| PhysicsShowcase | 1–5 / ENTER / R / A D W / E / G / I / ESC | Drop ball, box, triangle, pentagon, ghost / 30 shapes / remove dropped / run and jump / blast / flip gravity / interpolation on-off / quit | `DropSystem`, `PlayerInputSystem`, `BlastSystem`, `SwitchSystem`, `PhysicsShowcaseGame.OnUpdate` |
| Launcher | ↑↓ (W/S, K/J), Home/End / ENTER, SPACE / 1–9 / ESC, Q | Select / start selected / start that entry / quit | `LauncherMenu.HandleKey` |
| EternalDigger | ←→ / A D (held) | Walk, or dig the side tile | `MinerInput`, `MinerControlSystem` |
| EternalDigger | ↑ + ← or → (held) | Jump two tiles sideways over a one-tile gap (both tiles must be open) | `MinerControlSystem.CanJump` |
| EternalDigger | ↓ / S (held), ↑ / W (held, on a ladder) | Dig/step down; climb or dig up | same |
| EternalDigger | SPACE / E | Place a ladder (or, on a ladder, pick it back up) / deliver at the door or buy a ladder at the shop | `MinerControlSystem`, `InteractionSystem` |
| EternalDigger | TAB | Inventory screen on/off (pauses the game while open) | `InventorySystem` |
| EternalDigger | R (hold 1.5 s) / ESC | Rescue to the surface (the bag is lost) / save and quit | `MinerControlSystem`, `EternalDiggerGame.OnUpdate` |
| EternalDigger (counter) | ↑↓ / E at the shop counter | Choose an item / buy one (ladder, fill lamp, an upgrade tier, the kit or a stick) | `InteractionSystem`, `ShopCounter` |
| EternalDigger (dynamite) | hold E 0.5 s underground facing rock (+ an arrow to aim: ↓ below, ↑ above, ←→ sides); on the lift's bottom landing tap E for the lift, hold it to plant | plant a lit stick (2 s fuse, 3 × 3 blast) | `InteractionSystem`, `MinerControlSystem.UpdatePlanting`, `DynamiteSystem` |
| EternalDigger (deep camp) | ↑↓ / E at the camp counter (100 m, right of the lift shaft) | Choose / sell the bag at 70% or buy ladders, oil, flasks, sticks at ×1.5 | `InteractionSystem`, `DeepCamp`, `ShopCounter.CampItems` |
| EternalDigger (tool shop) | ↑↓ / E at column 13 on the surface, once below the wall | Choose / buy `DYNAMITE x5` (400g) or `LADDERS x10` (80g) | `InteractionSystem.BuyTools` |
| EternalDigger (lift) | E at the `LIFT` sign / E on a landing / ↑↓ in the car / ←→ | Build 10 m / get in or call / ride to the top or bottom / get out | `InteractionSystem`, `MinerControlSystem` |
| EternalDigger (title) | ↑↓ (or W S) / E / ESC | Choose / pick CONTINUE, NEW MINE or the confirmation / quit without saving | `TitleScene`, `TitleMenu` |
| EternalDigger (no code) | Delete `<LocalApplicationData>/EternalDigger/save.dat` | Start fresh without the confirmation | `SaveFile.DefaultPath` |
| EternalDigger | first command-line argument | Mine seed: the same number gives the same mine | `Program.cs` |
| Scripted | ←→↑↓ | Move the player | `player.lua` |
| Scripted | `F5` | Reload all Lua scripts from the output folder | `ScriptReloadSystem` → `LuaRuntime.ReloadAll` |
| Scripted | ESC | Quit (`Stop()`) | `ScriptedGame.OnUpdate` |
| Kartster (track) | W / S / A D / SPACE (held) | Gas / brake, then reverse / steer left/right (ramped) / handbrake | `KartInputSystem` → `KartControls` → `KartPhysicsSystem` |
| Kartster (track) | R | Back to the start, standing still (restarts `TrackScene`) | `KartsterGame.OnUpdate` |
| Kartster (garage) | A D / W S / Q E (held), SPACE | Orbit left/right / up/down, zoom in/out, auto-spin on/off | `GarageScene.OnUpdate` |
| Kartster | ENTER / TAB / ESC | Track/garage / next kart (on the track: from the start) / quit | `KartsterGame.OnUpdate` |
| Kartster | first / second command-line argument | Folder of kart folders (e.g. Kartster's `Content/3D/Rendered/Karts`) / folder of exported tracks | `Program.cs` |

There is **no** way to skip a TerraForM level, choose a map, reset the score, or enable TerraForM debug mode without a
code change.

## Escalate to a developer (requires code or asset change)

| Need | Where the change goes | Docs to update after |
|---|---|---|
| Turn logger / FPS on or off for a game | `EnableLogger`, `ShowFps` in that game's `Program.cs` or ctor | `01` |
| Change window size / font | `GameBase` ctor arguments in the game class | `01` |
| Fix or add a map | `Examples/TerraForM/Assets/Maps/*.txt` (+ `CopyToOutputDirectory` in `TerraForM.csproj` for a new file) | `02` |
| Change map order / include map4 | `TerraformGame.RotateMap`, `Maps` | `03`, `05` P8 |
| Reset score on restart, latch game over, frame-rate-independent power | `GameScene`, `Rover` | `03`, `04`, `05` |
| Detect and report Win32 failures (size/font) | `ConsoleHandler` (check return values) | `04`, `06` |
| Logger fixes (port in use, missing exe, truncated lines) | `Log`, `ConsoleEngine.Logger/Program.cs` | `04`, `06` |
| Change what a scripted entity does | `Assets/Scripts/*.lua` in that game (in a running Scripted demo: edit the output copy, press `F5`) | `03` |
| Add a Lua API function | `LuaRuntime.RegisterApi` + a host function that never raises a Lua error | `03`, `04`, `06` |
| Expose another component to scripts | `LuaRuntime.EngineSource` (`COMPONENT`, `run`, `clear`), `RunHook`, `ReadResults`, `LuaScriptSystem.Run` (mask bit + span) | `02`, `03`, `04`, `06` |
