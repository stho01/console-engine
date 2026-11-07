# 05 — Triage playbooks (entry point)

<a id="collect-first"></a>
## Before any playbook — collect these facts

| Fact | Why | How to get it |
|---|---|---|
| **Which game** (TerraForM, Snake, Asteroids, Platformer, ConsoleGame, their `.Ecs` ports, Swarm, Starguard, or their own game on `ConsoleEngine`) | Rules differ per game ([03](03-rules.md)) | Ask, or the exe name |
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
3. `SocketException` at startup, game has `EnableLogger = true` (Asteroids, Asteroids.Ecs, ConsoleGame, ConsoleGame.Ecs, Swarm, Starguard, or their own game):
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

1. [07 § Q2](07-checks.md#q2): running in **Windows Terminal**? → **Cause [external]:** `ConsoleHandler.InitializeConsole`/`SetFont` use classic-console APIs that Windows Terminal ignores, and the return values are not checked ([04 § F1](04-flows.md#f1) step 7). **Fix:** set Windows' default terminal application to "Windows Console Host", or start the exe from `conhost.exe`. Stop.
2. Classic console: does `width × fontWidth` by `height × fontHeight` fit the screen? (Table in [01](01-system-map.md). E.g. TerraForM 72×50 cells at 15×15 px = 1080×750 px plus borders. Asteroids 200×200 at 4×4 = 800×800.)
   - Doesn't fit, or display scaling makes it not fit → **Cause [external]:** `SetConsoleWindowInfo` fails silently when the window would exceed the maximum size. **Fix:** higher resolution, lower display scaling, or a developer reduces size/font in the game ctor. Stop.
3. Font face: the face is `"Consolas"` for every example. If the user's own game passes another `FontInfo.FontFace`, is that font installed and ≤ 31 characters (`ConsoleFontInfoEx.FaceName` `SizeConst = 32`)? If not → **Cause:** `SetCurrentConsoleFontEx` fails silently. **Fix:** use an installed console font. Stop.
4. The user resized/maximised the window → `Resizeable` is false by default, so the Maximize/Size menu items are removed. Dragging the border may still work **[external]**. **Cause:** the buffer is fixed at construction. **Fix:** restart the game without resizing. Stop.
5. Their own game reads `Console.FontInfo` and gets 14×14 → **Cause:** getter returns the ctor default, not the applied font ([02](02-state-model.md)). **Fix:** escalate as a bug. Stop.

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

1. Which game? Only `ConsoleGame`, `ConsoleGame.Ecs` and `Swarm` quit on `ESC` (`Stop()`). In TerraForM and TerraForM.Ecs `ESC` opens the menu. Others have no quit key. → **Cause:** no quit path ([04 § F3](04-flows.md#f3)). **Fix:** close the console window. Stop.
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
