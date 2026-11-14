# 05 — Triage playbooks (entry point)

<a id="collect-first"></a>
## Before any playbook — collect these facts

| Fact | Why | How to get it |
|---|---|---|
| **Which game** (TerraForM, Snake, Asteroids, Platformer, ConsoleGame, their `.Ecs` ports, Swarm, PhysicsSandbox, PhysicsShowcase, EternalDigger, Scripted, Kartster, or their own game on `ConsoleEngine`) | Rules differ per game ([03](03-rules.md)) | Ask, or the exe name |
| **OS and console host** | Windows only. Windows Terminal ignores the sizing/font calls **[external]** | [07 § Q1, Q2](07-checks.md#q1) |
| **How it was launched and from which folder** (double-click the exe, `dotnet run`, IDE, a shortcut, a terminal in another folder) | Map files and the logger exe are found by **relative path** | [07 § Q3](07-checks.md#q3) |
| **The exact error text**, if any | There are no log files. The exception printed to the console is the only record | Ask them to start it from an already-open terminal so the text stays ([06 § Capturing a crash](06-diagnostics.md#capture)) |
| **When in the flow** (before the window appears / at the name screen / after pressing `H` / mid-level / on finish) | Maps to a flow step in [04](04-flows.md) | Ask |
| **Machine speed** (FPS if they have the logger, or "fast gaming PC vs old laptop") | TerraForM power and movement depend on FPS ([03 § FPS](03-rules.md#fps)) | Ask, or [07 § Q8](07-checks.md#q8) |

<a id="first-checks"></a>
## Universal first checks (these explain most cases)

1. **Windows?** If not, it cannot run. Cause: Windows-only P/Invoke ([01](01-system-map.md)). Fix: none (issue 7). Stop.
2. **Classic console or Windows Terminal?** If Windows Terminal → [P2](#p2) step 1 first, whatever the complaint.
3. **Launched from the game's own folder?** If not, and the game is TerraForM or has the logger enabled → [P1](#p1).
4. **Is it a documented rule?** Search [03-rules](03-rules.md) for the user's complaint before calling anything a bug.
   TerraForM score, power, game over and map order are the usual "it's wrong" reports, and all are as coded.
5. **Not configured vs broken:** for logger/FPS complaints, check `EnableLogger`/`ShowFps` in that game's `Program.cs`
   ([01 § Example game configuration](01-system-map.md)) before suspecting the logger.

---

<a id="p1"></a>
## P1 — Crash on start / window flashes and closes

**Symptom [inferred]:** "It crashes on start", "the window opens and closes immediately", "nothing happens".

1. Get the exception text ([06 § Capturing a crash](06-diagnostics.md#capture)).
   - No text, window never appears → step 2.
   - `DllNotFoundException` mentioning `kernel32.dll`/`user32.dll` → **Cause:** not Windows. **Fix:** run on Windows. Stop.
   - `SocketException` → step 3.
   - `Win32Exception` (file not found) → step 4.
   - `FileNotFoundException`/`DirectoryNotFoundException` naming `Assets/Maps/...` → step 5.
   - `IndexOutOfRangeException`, `FormatException` or `NullReferenceException` with `WorldLoader`/`GameScene.OnLoad` in the stack → step 6.
   - `IndexOutOfRangeException` in `RenderConsole.Draw` / `Sprite.Draw` → **Cause:** game code drew a `Span<string>` or wrote a sprite out of bounds (no bounds check, [03 § Drawing](03-rules.md)). **Fix:** escalate with the stack trace. Stop.
2. Does it crash only when started from a terminal in another folder, or from an IDE/shortcut? → step 4/5 by game. Otherwise ask them to capture the text (step 1).
3. `SocketException` at startup, game has `EnableLogger = true` (Asteroids, Asteroids.Ecs, ConsoleGame, ConsoleGame.Ecs, or their own game):
   - [07 § Q5](07-checks.md#q5) shows port 11000 in use → **Cause:** another game instance (or another program) already holds port 11000, which `Log.Start` binds unconditionally. **Fix:** close the other instance or program. Only one logger-enabled game can run at a time. Stop.
   - Port free → escalate with the full stack trace (DNS resolution of `localhost` is the remaining suspect, [04 § F4](04-flows.md#f4) step 1).
4. `Win32Exception` at startup, logger enabled:
   - [07 § Q4](07-checks.md#q4): is `ConsoleEngine.Logger.exe` in the **working directory**? If not → **Cause:** `Log.Start` launches the logger by relative name. **Fix:** start the game from its output folder (double-click the exe there). Stop.
5. TerraForM `...NotFoundException` for `Assets/Maps/mapN.txt` (appears **after pressing `H`**, not at launch):
   - [07 § Q3](07-checks.md#q3): working directory has no `Assets/Maps` → **Cause:** `WorldLoader.LoadWorld` reads `Assets/Maps/{name}.txt` relative to the working directory. **Fix:** launch from the TerraForM output folder. Stop.
   - Folder exists, file missing → **Cause:** incomplete copy/build output. **Fix:** rebuild (maps are `CopyToOutputDirectory=Always`) or recopy the folder. Stop.
6. TerraForM map parse failure (custom or edited map): run [07 § Q6](07-checks.md#q6) on the named map and match against [04 § F6](04-flows.md#f6) steps 4–11. **Cause:** that step. **Fix:** correct the map file (developer/map author). Stop.

<a id="p2"></a>
## P2 — Wrong window size, scrollbars, wrong font, garbled or wrapped picture

**Symptom [inferred]:** "the window is too small/big", "there are scrollbars", "letters are tiny", "the picture is broken/shifted".

1. [07 § Q2](07-checks.md#q2): running in **Windows Terminal**? → **Cause [external]:** `ConsoleHandler.InitializeConsole`/`SetFont` use classic-console APIs that Windows Terminal ignores, and the return values are not checked ([04 § F1](04-flows.md#f1) step 7). **Fix:** double-click `start-conhost.cmd` in the game's folder (it starts the game in `conhost.exe`), or set Windows' default terminal application to "Windows Console Host" ([08](08-operator-actions.md)). Stop.
2. Classic console: does `width × fontWidth` by `height × fontHeight` fit the screen? (Table in [01](01-system-map.md). E.g. TerraForM 72×50 cells at 15×15 px = 1080×750 px plus borders. Asteroids 200×200 at 4×4 = 800×800.)
   - Doesn't fit, or display scaling makes it not fit → **Cause [external]:** `SetConsoleWindowInfo` fails silently when the window would exceed the maximum size. **Fix:** higher resolution, lower display scaling, or a developer reduces size/font in the game ctor. Stop.
3. Font face: the face is `"Consolas"` for every example. If the user's own game passes another `FontInfo.FontFace`, is that font installed and ≤ 31 characters (`ConsoleFontInfoEx.FaceName` `SizeConst = 32`)? If not → **Cause:** `SetCurrentConsoleFontEx` fails silently. **Fix:** use an installed console font. Stop.
4. The user resized/maximised the window → `Resizeable` is false by default, so the Maximize/Size menu items are removed. Dragging the border may still work **[external]**. **Cause:** the buffer is fixed at construction. **Fix:** restart the game without resizing. Stop.
5. Their own game reads `Console.FontInfo` and gets 14×14 → **Cause:** getter returns the ctor default, not the applied font ([02](02-state-model.md)). **Fix:** escalate as a bug. Stop.
6. Colours are wrong (Kartster: purple/red sky, garish road), or another console's colours changed after a game → **Cause:** `RenderConsole.Palette` remaps the 16 colours ([04 § F1](04-flows.md#f1) step 7b). Windows Terminal and terminals without true colour ignore it **[external]**, so the game's colour indices show as default colours. **Fix:** run in `conhost` (`start-conhost.cmd`, `run-game.ps1`). The original colours come back on exit (`Close()`, `ProcessExit`); a process that was killed leaves them changed until that console window closes.

<a id="p3"></a>
## P3 — Keys don't work / react in other windows / presses missed

**Symptom [inferred]:** "keys don't respond", "it moves when I'm typing in another window", "sometimes my key press doesn't count", "SPACE does nothing".

1. Which key? Is it in the `Key` enum ([03 § Keys](03-rules.md#keys))? Shift/Ctrl/Alt/æøå/punctuation → **Cause:** not supported by `Key`. **Fix:** none without code. Stop.
2. "Reacts while another window has focus" → **Cause [external]:** `GetAsyncKeyState` is global. **Fix:** works as designed. Pause or close the game when not playing. Stop.
3. "Some quick presses are missed", worse on a slow PC → **Cause [external]:** taps shorter than one frame only set the low bit, which `InputHandler.Update` treats as a release. **Fix:** hold keys slightly longer. Escalate if frequent.
4. TerraForM SPACE "does nothing" → are commands still executing? (`QUEUED COMMANDS` stays on screen and the rover is still moving.) **Cause:** SPACE is ignored while `_currentCommands` is non-empty ([03 § Sequences](03-rules.md#sequences)). **Fix:** wait for the current sequence to finish, then press SPACE. Stop.
5. TerraForM WASD do nothing → **Cause:** WASD move only in debug mode, and `IsDebugMode` is never enabled ([02](02-state-model.md)). **Fix:** use the arrow keys + SPACE. Stop.
6. TerraForM keys do nothing at all, screen frozen → the level may be under the menu ([P12](#p12)) or the game crashed ([P1](#p1)).

<a id="p4"></a>
## P4 — Logger window missing / empty / no FPS / cut-off lines

**Symptom [inferred]:** "the logger window doesn't open", "the logger is empty", "where is the FPS?", "log lines are cut off".

1. Does the game set `EnableLogger = true`? ([01](01-system-map.md) table, or their `Program.cs`.)
   - No → **Cause (not configured):** `Log.Start` is only called when `EnableLogger`, and `EnqueueMessage` drops everything otherwise. **Fix:** set `EnableLogger = true` in the object initializer (init-only). Stop. *(Snake and Platformer set `ShowFps` without `EnableLogger`, so they show no FPS anywhere.)*
2. Enabled but no logger window → the game would have crashed with `Win32Exception` ([P1](#p1) step 4). If the game runs, the window may have opened and closed: the logger exits silently on `SocketException` during `Connect` ([04 § F4](04-flows.md#f4) logger step 2). Escalate with OS details.
3. Window open, only `Logger up and running...`:
   - Does the game call `Log.WriteLine`? No example does. → **Cause:** nothing to log. Stop.
   - Expecting FPS? FPS appears **only in the logger window title** as `Game FPS: n`, and only if `ShowFps` **and** `EnableLogger`. → explain. Stop.
4. Lines missing or cut → **Cause:** the logger discards text after the last `'\0'` in each 1024-byte read ([04 § F4](04-flows.md#f4) logger step 3). **Fix:** known defect, escalate. Stop.

<a id="p5"></a>
## P5 — Can't quit / logger stays open

**Symptom [inferred]:** "how do I exit?", "ESC doesn't quit", "the logger window stays open after the game closes".

1. Which game? Only `ConsoleGame`, `ConsoleGame.Ecs`, `Swarm`, `PhysicsSandbox`, `PhysicsShowcase` and `EternalDigger` quit on `ESC` (`Stop()`). In TerraForM and TerraForM.Ecs `ESC` opens the menu. Others have no quit key. → **Cause:** no quit path ([04 § F3](04-flows.md#f3)). **Fix:** close the console window. Stop.
2. Logger stays open after the game exits → **Cause:** `Log.Stop` is never reached, because `RenderConsole.Close` calls `Environment.Exit(0)` first, and the logger only exits on a `SocketException` ([04 § F4](04-flows.md#f4)). How the logger sees the closed connection is **[external]** and unverified. **Fix:** close the logger window. Escalate if it spins CPU.

<a id="p6"></a>
## P6 — TerraForM: power runs out too fast / rover slides too far / instant game over

**Symptom [inferred]:** "I run out of power after a few moves", "one arrow press sends the rover across the map", "GAME OVER as soon as the level starts".

1. Game over **immediately** on level load → check that map's `!power` and `!sequences` ([07 § Q6](07-checks.md#q6)). Missing/0 → **Cause:** `PowerDepleted()` is `<= 0` / `RemainingSequences` starts at 0 ([02 § Map format](02-state-model.md#map-format)). **Fix:** correct the map. Stop. (Shipped maps are all > 0.)
2. Did the rover touch a `Craves` tile (grey ░▒▓ block)? → **Cause:** `Craves` sets `RemainingPower = 0`. Stop.
3. Fast machine / high FPS (logger title, or "gaming PC")? → **Cause:** per-frame power cost and per-frame movement ([03 § Power](03-rules.md#power)). One 100 ms move costs ≈ 0.64 × frames. **Fix:** works as coded, a known design flaw. Escalate as a frame-rate-independence bug if the user is blocked. Stop.
4. Game over after N+1 SPACE presses → **Cause:** sequences rule ([03 § Game over](03-rules.md#game-over)). Stop.

<a id="p7"></a>
## P7 — TerraForM: score is wrong / not reset

**Symptom [inferred]:** "my score is too high/low", "score didn't reset when I restarted", "score went down".

1. Restarted with `R` or after game over? → **Cause:** `TerraformGame.Score` is never reset. Points from planting/bonuses in the failed attempt stay. Stop.
2. Recompute with [03 § Score](03-rules.md#score): each plant counts 10 000 when planted + 10 000 at finish, bonus 10 000, finish adds `(int)RemainingPower − DamageTaken`.
   - Matches → **Cause:** as coded. Explain the double plant count and the negative finish term if damage > power. Stop.
   - Finish bonus counted twice → custom map with adjacent `F` tiles? ([03 § Score](03-rules.md#score), last bullet.) Escalate.
3. Score not visible → in game over the HUD (including score) is replaced by the game-over sprite ([04 § F7](04-flows.md#f7) step 10). Stop.

<a id="p8"></a>
## P8 — TerraForM: wrong level order / "Map Name" / never reach "Leviathan Falls"

**Symptom [inferred]:** "after level 3 I got a level called 'Map Name'", "the levels repeat", "I never see Leviathan Falls".

1. **Cause:** `RotateMap` builds `map{CurrentMap}`, wraps to 0 at `Maps.Length` (4) and ignores the `Maps` array. Order is map1 → map2 → map3 → map0 → map1 … ([03 § Map rotation](03-rules.md#map-rotation)). map4 is unreachable. map2 and map3 share the name "Persepolis Rising". **Fix:** works as coded. Escalate as a bug (developer fix in `TerraformGame.RotateMap`). Stop.

<a id="p9"></a>
## P9 — TerraForM: name entry problems

**Symptom [inferred]:** "can't type æ/ø/å", "my name is all caps", "name shows `##` after it", "strange letters in my name".

1. æøå / lower case / punctuation → **Cause:** only `GetPressedKeyCodesSpace09AZ()` codes (space, 0–9, A–Z). Stop.
2. `#`/`##`/`###` next to the name in the story → **Cause:** `MenuScene.OnLoad` writes over the `###` placeholder without clearing. Names < 3 characters leave `#`s, an empty name leaves `###`. Stop.
3. Name cut off → only the first 30 characters are shown (`Take(30)`). Stop.
4. Letters they didn't type → **Cause [external]:** global keyboard ([P3](#p3) step 2). Stop.

<a id="p10"></a>
## P10 — TerraForM: "GAME OVER but I kept playing / still finished"

1. **Cause:** game over only swaps the HUD for `Sprites.GameOver`. `GameScene.OnUpdate` keeps processing input, commands and collisions, so a rover with power can still finish and score ([03 § Game over](03-rules.md#game-over)). The flag is recomputed every frame and is not latched. **Fix:** works as coded, escalate as a bug. Stop.

<a id="p11"></a>
## P11 — TerraForM: planting doesn't work

1. Was `P` followed by SPACE? `P` only queues a `Plant` command ([03 § Sequences](03-rules.md#sequences)). No → explain. Stop.
2. Already planted that spot this attempt? `HasBeenPlanted` → no second plant. Stop.
3. Was the rover still moving or just off the spot when the plant command ran? The flag comes from the **previous** frame's collision, and the rover is drawn 1 cell up-left of its hitbox ([03 § Planting](03-rules.md#planting)). **Cause:** not overlapping the 10×10 spot box. **Fix:** queue the plant as a separate sequence after the rover has stopped on the spot. Stop.

<a id="p12"></a>
## P12 — Game disappears behind the menu / screen leaves trails

1. Level vanished when the menu opened (TerraForM `ESC`) → **Cause:** `Push` pauses the level and paused scenes don't render ([03 § Scenes](03-rules.md#scenes)). **Fix:** press `H` to return. Stop.
2. Old characters stay on screen → is `ClearScreenOnEachFrame = false` (Snake, or their own game)? → **Cause:** buffer not cleared (`GameBase.Start`). Their game must blank cells itself. Stop.
3. Text printed with `System.Console.Write` (or by another program) stays on screen, or cells stay wrong after it, on Windows → **Cause:** `ConsoleHandler.Render` writes only the rows that changed in the game's frame ([03 § Console output](03-rules.md#console-output)), so it never overwrites what something else wrote until the game draws something different there. **Fix:** draw through `RenderConsole` instead of `System.Console`. Stop.

<a id="p13"></a>
## P13 — Snake goes off screen / can't lose

1. **Cause:** `SnakeGame` has no wall or self collision. Off-screen segments are clipped by `RenderConsole.Draw` ([03 § Other examples](03-rules.md#other-examples)). It's a demo. Stop.
2. Food appears on the snake → **Cause:** `SpawnFood` retry rarely matches `' '` because undrawn cells are `'\0'`. Stop.

<a id="p14"></a>
## P14 — Platformer: debug text / no FPS / player hidden

1. Debug text → `Program.cs` starts `IsDebugMode = true`. **Fix:** press `F1`. Stop.
2. No FPS → `ShowFps` without `EnableLogger` ([P4](#p4) step 1). Stop.
3. Player hidden by tiles → `OnRender` draws the player before the world. Stop.

<a id="p15"></a>
## P15 — TerraForM: Norwegian story missing

1. **Cause:** `Assets/Story/story-NO.txt` (and `story-EN.txt`) are not used by code or copied to output. The story is hard-coded English in `Sprites.Story`. **Fix:** feature request. Stop.

<a id="p16"></a>
## P16 — Launcher: game shows "(not built)" / won't start / window stops at "Press any key"

1. Menu never appears, message `No Launcher.csproj found above ...` → **Cause:** the launcher exe was copied or run
   outside its build output ([04 § F10](04-flows.md#f10) step 1). **Fix:** `dotnet run --project Examples/Launcher` from
   the repository. Stop.
2. A game shows `(not built)`, or starting it says `... is not built: <path>. Run dotnet build.` → [07 § Q10](07-checks.md#q10).
   **Cause:** that exe is not at the path shown, which uses the launcher's own configuration (`Debug`/`Release`). A
   Release launcher does not see Debug games. **Fix:** `dotnet build` (or `-c Release`) of the launcher, which builds every
   listed game. Stop.
3. Game window stays open on `Press any key to continue . . .` → the game exited non-zero (crashed) and `pause` kept the window open. The exception text is above it. Continue with [P1](#p1) step 1 using that text. Stop.
4. `Could not start ...` → `Win32Exception` from `Process.Start` (`conhost.exe` missing or blocked). Escalate with the message. Stop.
<a id="p17"></a>
## P17 — EternalDigger: stuck, can't dig, can't place or buy ladders, can't find ore

Rules: [03 § EternalDigger](03-rules.md#eternal-digger). Flows: [04 § F11](04-flows.md#f11), [F12](04-flows.md#f12).

1. Stuck at the bottom of a pit, up does nothing → **Cause:** up only works with a ladder in the miner's own tile
   (`Mine.IsSupported`, `MinerControlSystem.Control`). **Fix:** place ladders with `SPACE` on the way down; with none
   left, hold `R` for 1.5 s (rescue, the bag is lost). The HUD shows `NO LADDERS LEFT!` underground at 0 ladders. Stop.
2. Can't dig down from the grass, or into the side walls → **Cause:** grass and bedrock are unbreakable
   (`Tiles.IsBreakable`). **Fix:** go down the shaft under the `MINE` frame (column 21). Stop.
3. `SPACE` places no ladder → **Cause:** one of: 0 ladders, still at the surface (row ≤ 10), the tile already has one,
   or the miner was mid-move/falling (keys aren't read then). Stop.
4. `E` does nothing at the house → **Cause:** must stand still right in front of the door (columns 3–6) or the shop
   counter (columns 8–11); a prompt or panel appears there. At the door with an empty bag, or at the counter without
   enough gold for the selected item, only a dust puff shows (the price is red). `↑`/`↓` at the counter change what
   `E` buys. The `BLASTING` shed sells nothing itself. Stop.
5. Lost all ore → **Cause:** a rescue (`R` held 1.5 s) empties the bag by design. Stop.
5a. "Climbing got slow", "the miner crawls up the ladder" → **Cause:** a heavy bag (HUD bag value yellow, backpack
    `▓`): each 10 weight over the capacity (20, +10 per bag tier) makes climbing up 20% slower. **Fix:** sell at the
    door, or buy the bag upgrade. Stop.
6a. "The light keeps shrinking", "the lamp flickers", "it's all dark now" → **Cause:** lamp oil. It burns underground
    (1 unit per 3 s of 100); below 30 the cone shrinks and flickers, at 0 only the close radius is lit. **Fix:** fill
    the lamp at the shop counter (`FILL LAMP`, 2g per unit); a better lamp burns less. Outlines and glints still show
    the way, or hold `R`. Stop.
6. "Ore is invisible", "everything is black" → **Cause:** only lit tiles show colour; remembered tiles are grey outlines
   without ore (`TerrainRenderSystem`). **Fix:** watch for glints (twinkles near the miner, also through rock) and point
   the headlamp by moving/digging towards the ore. Stop.
7. Different mine every time → **Cause:** the seed defaults to `Environment.TickCount`. **Fix:** pass a number as
   the first argument for a repeatable mine. Stop.
8. "My progress is gone", "no CONTINUE" → **Cause:** one of: the game was closed without `ESC` (progress since the
   last sale, purchase or 60 s underground autosave is lost); NEW MINE was confirmed (replaces the slot); the save was
   unreadable (the title said so; the file is now `save.dat.bad`, [Q11](07-checks.md#q11)); or ` SAVE FAILED` showed
   (the folder isn't writable). Stop.
9. "Can't build the lift", "E does nothing at the lift" → **Cause:** building needs standing still at columns 23–24 on
   row 9 and 200g (+100g per segment built; the prompt price is red when short). Calling needs standing still on a
   landing in column 21: row 9 or the bottom row; mid-shaft, `E` does nothing. In the car, `E` does nothing: use ↑/↓.
   Stop.
10. "Can't dig at 110 m", "TOO HARD" → **Cause:** the obsidian wall (`Tiles.WallDepth`, 3–5 tiles); the shovel can't
    break it by design. **Fix:** select the blasting slot at the shop counter (`↓`), buy the kit (2500g) and sticks (100g), hold `E` 0.5 s
    facing the wall, walk away. Same reason the lift stops at 100 m (blast column 21 to go deeper). Stop.
11. "Holding E does nothing", "can't plant dynamite" → **Cause:** one of: no kit or no sticks (HUD `DYNAMITE <n>`), on
    the surface, on a lift landing without sticks (there `E` is the lift), facing air instead of rock (hold an arrow with `E` to aim; else the lamp points the way you last
    moved or dug), moving, or `E` wasn't released since the last stick. A dust puff means it was refused. Stop.
12. "I want my ladders back" → `SPACE` while standing in a ladder takes it back (not the grass-row one). Taking the
    one that holds you up drops you to the next floor. Stop.
13. "Fell all the way down the shaft" → **Cause:** a built lift digs out the shaft; without ladders there a step into it
    falls to the bottom landing. No damage. Stop.
14. "My lamp went out", "something exploded", "GAS!" → **Cause:** a gas pocket (green shimmer, caves from 50 m) flared
    because the lit lamp came within 2 tiles. By design: the lamp relights after 3 s and loses 5 oil; nothing is hurt.
    Stop.
15. "The lamp is weaker below 110 m", "oil runs out fast down there" → **Cause:** darkness by design: act 2 rock cuts the
    cone by 2 tiles; black damp (`BLACK DAMP` floated up, `░` grey walls) halves it and doubles the burn. Leave the area.
    Stop.
16. "Debris or sparks disappear right next to the miner" → **Cause:** by design. No particle is drawn on his 4 × 2
   cells, and at most 6 (dimmed) are drawn in the tile around him, so he stays readable (`ParticleRenderSystem`). Stop.

<a id="p18"></a>
## P18 — Physics: bodies pass through each other / sink / no hit / hit reported twice or late

Applies to PhysicsSandbox, Asteroids.Ecs and any game on `ConsoleEngine.Physics`. Rules: [03 § Physics](03-rules.md#physics),
flow: [04 § F13](04-flows.md#f13).

1. Bodies overlap and keep going → check, in order: a filter or pair rule excludes the pair (step 2 below; in Asteroids.Ecs
   asteroids pass through each other by design); one body is static or kinematic and the other too (no contact between
   them); the dynamic body has no `Velocity` (handled as static). Otherwise escalate with the body sizes and speeds. Stop.
2. Bodies sink a little into each other or into the floor, more in a tall stack → by design: each contact settles about
   `PhysicsSystem.LinearSlop` (0.005 cells) deep, and contacts are soft springs. A deep overlap is pushed out at most
   `ContactPushMaxSpeed` (3 cells/s), so it takes a moment. Stop.
3. No event at all for a pair → check, in order: both have a `Collider` and a `Position`; the `Category`/`Mask` bits accept
   each other both ways (`default(Collider)` has `Mask` 0); not both static (a `Collider` without `RigidBody` is static);
   not both sensors; a non-sensor pair has a dynamic body. Any of these → skipped silently ([04 § F13](04-flows.md#f13)
   step 4). Stop.
4. A body never moves → it has no `Velocity`, is `BodyType.Static`, or `PhysicsSystem` was added to `Simulation` or not
   at all instead of `FixedSimulation`. Stop.
5. The same hit handled several times, or hits missed → the reacting system reads the events from `Simulation` instead of
   after `PhysicsSystem` in `FixedSimulation` ([03 § Physics](03-rules.md#physics)). Stop.
6. Rigid bodies move too fast, or faster at a higher FPS → the scene adds `new MovementSystem()` without the filter, so they are
   moved twice. Use `new MovementSystem(Query.Without<RigidBody>())` ([03 § Physics](03-rules.md#physics)). Stop.
7. Asteroids.Ecs: one bullet removed two asteroids → by design when it touches both in the same step ([03 § Asteroids.Ecs
   row](03-rules.md)). Stop.
8. A raycast or box cast misses a body that is clearly there → queries see the world as at the end of the last physics
   step; a body created or moved by game code since then shows up after the next step. Sensors are never reported, and
   the query mask must include the collider's `Category`. Stop.
9. Dynamic bodies fall through a `TileGrid` → the body's `Collider.Mask` must include `TileGrid.Category` (default 1),
   and it must be dynamic with a `Velocity`. Only through a wall one tile thick: a body pushed more than half a tile
   into it (a heavy pile, a squeeze) is pushed out through the far face. Make walls at least two tiles thick. Stop.
10. Bodies hang in the air, don't fall after gravity changed, or don't react → they are asleep (`RigidBody.IsSleeping`,
   drawn dark grey in PhysicsShowcase). Changing `Gravity` doesn't wake them (as Box2D): clear `IsSleeping`. A body that
   never sleeps keeps moving faster than 0.05 cells/s (no damping, a moving platform). Stop.
11. A box doesn't turn when hit off-centre → it needs both `Rotation` and `AngularVelocity`. Stop.
11b. An outline is drawn one row inside the floor it rests on → the outline system draws a corner in the cell it lies in,
   and a resting body sits `LinearSlop` into the floor. Draw outlines through cell centres (PhysicsShowcase:
   `OutlineShape.ThroughCellCentres`). A character falls through a moving or turning platform → the physics never
   pushes a character: move it with the platform first (PhysicsShowcase: `PlayerMoveSystem.TryGetPlatformVelocity`).
   Stop.
12. Platformer.Ecs: player stuck, falls through the floor, or can't jump → check the debug overlay (F1): `HIT: Floor`
   while standing means landing works and `AIR` should be `False`. A jump pressed in the air waits for the landing (by
   design, as the original). Otherwise escalate with `POS`, `VEL` and `HIT`. Stop.

<a id="p19"></a>
## P19 — Lua: script fails to load / entity does nothing / F5 changes nothing

**Symptom [inferred]:** "my Lua script doesn't run", "the object just stands still", "I edited the script and
pressed F5 but nothing changed", "error about lua54.dll".

1. Crash at startup with `LuaScriptException` (`Lua script '<name>': …`) → read the message ([06 § Lua](06-diagnostics.md)):
   - .NET file message (`Could not find file…`) → **Cause:** the script is not in the output folder. `LuaRuntime.Load` resolves relative paths against the exe's folder, not the working directory ([04 § F15](04-flows.md#f15) step 1). **Fix:** set `CopyToOutputDirectory` for `Assets/Scripts/*.lua` in the game's `.csproj` ([07 § Q12](07-checks.md#q12)). Stop.
   - `<name>:<line>: …` → **Cause:** syntax or runtime error at that line. **Fix:** fix the script. Stop.
   - `the script must return a table` → **Cause:** the file does not end with `return <module>`. **Fix:** add it ([03 § Lua](03-rules.md#lua)). Stop.
2. `DllNotFoundException` for `lua54` → **Cause:** the native library is missing from `runtimes/<rid>/native/`, usually a partial copy of the output folder ([07 § Q12](07-checks.md#q12)). **Fix:** rebuild or copy the whole output folder. Stop.
3. Entity does nothing:
   - It has the `ScriptFaulted` tag / `LuaScriptSystem.GetError(entity)` is set (Scripted demo: red error line at the bottom) → **Cause:** a Lua error stopped it ([03 § Lua](03-rules.md#lua)). `unknown field '…' on an entity` means the script wrote a field that is not a component field (often a typo like `self.X`, or own state that belongs in `self.data`). `self.vx needs a Velocity component on the entity` means the entity lacks that component. `self.x must be a number` means a wrong type. **Fix:** fix the script and reload. Stop.
   - The error names a nil global `os`, `io`, `debug`, `package`, `require`, `load`, `loadfile`, `dofile` or `collectgarbage` → **Cause:** the sandbox does not open it, by design ([03 § Lua](03-rules.md#lua)). **Fix:** use `dt` for time and keep data in `self.data`; file access or loading code from scripts is not supported. Stop.
   - Not faulted → is the hook named exactly `on_update` on the **returned module table**, does the entity have the script's `Component`, and is a `LuaScriptSystem` in the scene's `Simulation` group? Any missing → **Cause:** hook never runs (silent, [04 § F15](04-flows.md#f15) step 7, [F16](04-flows.md#f16)). Stop.
   - `on_render` draws nothing → no `LuaRenderSystem` in `Presentation`, or it was added before a render system that draws over it. Stop.
   - Moves in the script but stays put on screen → the move happened in `on_render`, whose field changes are not copied back ([03 § Lua](03-rules.md#lua)). **Fix:** move in `on_update`. Stop.
   - Not drawn at all → the entity has no `Glyph`, or the scene has no `GlyphRenderSystem`. Stop.
   - Sets `self.vx` but doesn't move → the scene has no `MovementSystem`. Velocity is only applied by that system. Stop.
   - Moves very slowly → movement not multiplied by `dt`, or multiplied twice. `dt` is in seconds ([03 § Lua](03-rules.md#lua)). Stop.
4. F5 changes nothing (Scripted demo):
   - They edited the file in the **project** folder → **Cause:** `Reload` reads the copy in the output folder (`FullPath`). **Fix:** edit the output copy, or rebuild and restart. Stop.
   - A removed function still runs → reload removes fields that the new module no longer has, so the returned table still has it: the copy in the output folder (`FullPath`) still defines it. **Fix:** edit the output copy. Stop.
   - A reload error → `ReloadAll` returned it, but `ScriptReloadSystem` only sends it to `Log.WriteLine`, which is dropped without `EnableLogger`, and the old version stays. **Fix:** check the script for errors (load it in a test), then reload again. Stop.

<a id="p20"></a>
## P20 — 3D: GPU mode not available / falls back to CPU / GPU picture differs

**Symptom [inferred]:** "GPU mode doesn't work", "it always uses the CPU", "`GlRenderer` throws", "glfw3.dll is
missing", "the GPU picture looks different".

No game uses `GlRenderer` yet (`ConsoleEngine.Rendering3D.OpenGL`, [01](01-system-map.md)); this is for code that
does.

1. `InvalidOperationException` starting `OpenGL 3.3 is not available: ` (or `GlRenderer.TryCreate` returned false and
   the caller fell back to `Rasterizer`) → read the rest of the message ([06 § 3D](06-diagnostics.md#rendering3d)):
   - A GLFW or windowing message (no suitable platform, `DllNotFoundException` for `glfw3`) → **Cause:** `glfw3.dll`
     is missing from `runtimes/<rid>/native/` (partial copy of the output folder), or no desktop session (service,
     SSH, CI) ([07 § Q13](07-checks.md#q13)). **Fix:** copy the whole output folder; run in a desktop session. Stop.
   - A context/version message from GLFW (WGL, "version", "pixel format") → **Cause:** the driver has no OpenGL 3.3
     core profile: no GPU driver installed (Microsoft Basic Display Adapter), a remote desktop session or a VM
     **[external]**. **Fix:** install the GPU vendor's driver, or use `Rasterizer` (the same picture on the CPU). Stop.
   - `the R8UI framebuffer is not complete (<status>)` → **Cause:** the driver cannot render to an `R8UI` target, or
     width/height is 0. **Fix:** use `Rasterizer`; report the GPU and driver (the `RGBA8` reserve in
     `docs/design/gpu-rendering-plan.md` § 7 is not built). Stop.
   - `the <shader type> did not compile: …` / `the shaders did not link: …` / `setting up failed with <error>` →
     **Cause:** driver bug or a shader change. **Fix:** escalate with the message and GPU/driver. Stop.
2. Crash or black frame later, from another thread → **Cause:** the GL context belongs to the thread that created the
   renderer ([03](03-rules.md) `GlRenderer` row). **Fix:** create and use it on the game-loop
   thread. Stop.
3. A mesh changed in place but the GPU still shows the old one → **Cause:** `Mesh.MarkChanged()` was not called; the
   GPU copy is only refreshed when `Mesh.Version` moves. **Fix:** call `MarkChanged()` after editing the arrays. Stop.
4. GPU and CPU pictures differ → only edge pixels may differ (fill rules); `GlRendererTests` require ≥ 97 % equal
   pixels and measure 100 % on the test scenes. True colour: `GlRendererTrueColorTests` require ≥ 99 % of
   pixels within 2 per channel and measure 100 % (blends differ by at most 1). More than edges → escalate with
   both frames. Stop.
