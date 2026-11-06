# 09 — Glossary

Support/user words are **[inferred]**. The team may also use Norwegian terms. Add them in the first column.

| User / support word | Meaning | Code identifier |
|---|---|---|
| the game / the app | One example exe built on the engine | `GameBase` subclass: `TerraformGame`, `SnakeGame`, `AsteroidsGame`, `PlatformerGame`, `ConsoleGame.Game`, `ScriptedGame` |
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
| level / map / world | A TerraForM map | `World`, `WorldLoader`, `Assets/Maps/mapN.txt`, `GameScene` |
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
| game object / entity | An object in the engine's format: position, character/sprite, colours, visibility, behaviour | `GameObject` (`ConsoleEngine.Infrastructure.Objects`). The examples' own `GameObjects/GameObject` classes are older, per-game types |
| behaviour / script on an object | What a game object does each frame | `IBehaviour`, `GameObject.Behaviour`, `LuaBehaviour` (`LuaScript.CreateBehaviour`) |
| `self` / object fields | The Lua view of a game object: `x`, `y`, `char`, `color`, `background`, `visible`, `data` | `LuaRuntime.WriteFields`/`ReadFields` |
| script data | A script's own per-object state | `self.data` |
| hook | `on_start`, `on_update`, `on_render` in a script's module | `LuaScript.OnStart`/`OnUpdate`/`OnRender` |
| host function | A C# function callable from Lua (`input.*`, `console.*`, `print`) | `LuaRuntime.RegisterApi` |
| reload / hot reload / F5 | Re-run scripts and update their modules in place | `LuaScript.Reload`, `LuaRuntime.ReloadAll` |
| script error / broken script / faulted | A Lua error stopped a behaviour until its script reloads | `LuaBehaviour.Error`, `IsFaulted`, `LuaScriptException` |
| debug mode | Developer overlay | `PlatformerGame.IsDebugMode` (F1), `TerraformGame.IsDebugMode` (unreachable) |
