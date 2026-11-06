# 08 — What support / the user can do

There is no admin UI, permission model or audit trail. Every action is either something the **user does on their own
machine** or a **code/asset change by a developer**. Scope below is **[inferred]** from the code. Correct it if the
team's process differs.

## Self-serve (no code change)

| Action | Effect | Use for |
|---|---|---|
| Launch the exe from its own output folder (double-click it there, or `cd` there first) | Relative paths resolve: `Assets/Maps/*.txt`, `ConsoleEngine.Logger.exe` | [P1](05-triage.md#p1) |
| Run the game in the classic console host instead of Windows Terminal **[external]** | Size/font calls take effect | [P2](05-triage.md#p2) |
| Lower display scaling / use a larger screen | Window can reach the requested size | [P2](05-triage.md#p2) |
| Close the other process holding port 11000 / the other game instance | `Log.Start` can bind | [P1](05-triage.md#p1) |
| Close the window | Only way to exit every game except ConsoleGame, ConsoleGame.Ecs and Swarm | [P5](05-triage.md#p5) |
| Close a leftover logger window | — | [P5](05-triage.md#p5) |
| Start the exe from an open terminal to keep crash text | Captures the exception | [06 § Capture](06-diagnostics.md#capture) |

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
| Starguard | ← → or A D / SPACE (hold) / ENTER | Move / fire (one shot in flight) / new match after game over | `ControlsSystem` |

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
