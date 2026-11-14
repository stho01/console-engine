# console-engine — agent support docs

**Reader:** an AI agent triaging a support case ("why did X not happen?") for the ConsoleEngine library or one of its
example games, **without reading the source**. Every identifier, string and file path in these docs exists in this
repository and can be grepped.

**What this system is:** a Windows-only .NET 11 library that draws games into a console window via Win32 P/Invoke
(`ConsoleEngine`, `ConsoleEngine.Windows`), an optional out-of-process TCP logger (`ConsoleEngine.Logger`), optional
Lua 5.4 scripting for ECS entities (`ConsoleEngine.Scripting`), and eighteen example games (`TerraForM`, `Snake`,
`Asteroids`, `Platformer`, `ConsoleGame`, an ECS port `*.Ecs` of each, the ECS stress test `Swarm`, the physics demos
`PhysicsSandbox` and `PhysicsShowcase`, the ECS games `EternalDigger`, `SkyStack` and `Starguard`, the Lua demo `Scripted`, and the 3D racer `Kartster`),
plus `Launcher`, a text menu that starts the ECS games. There is **no server, no database, no
queue, no tenant, no log file**. Each game is a single local process. See [01-system-map](01-system-map.md).

## How to use these docs

1. Collect the facts in [05-triage § Before any playbook](05-triage.md#collect-first).
2. Run the [universal first checks](05-triage.md#first-checks). They explain most cases.
3. Find the user's words in the routing table below and open that playbook.
4. Follow the playbook's numbered branches until it ends in a **Cause** + **Fix**. Background lives in `01`–`04`;
   playbooks link to it rather than repeat it.

Tags used throughout:

| Tag | Meaning |
|---|---|
| **[inferred]** | Support phrasing or self-serve scope guessed from the code. No real tickets were available. Correct these. |
| **[external]** | Windows/.NET platform behaviour that the code depends on but this repo cannot prove. Treat it as a strong hypothesis. |

## Symptom → playbook routing **[inferred]**

| User says something like… | Playbook |
|---|---|
| "It crashes on start", "the window flashes and closes", "nothing happens when I run it" | [P1](05-triage.md#p1) |
| "Window is the wrong size", "there are scrollbars", "font is tiny/huge", "picture is garbled/wrapped", "colours are wrong", "24-bit mode still looks like 16 colours", "slow in 24-bit" | [P2](05-triage.md#p2) |
| "Keys don't work", "it reacts to keys when I'm in another window", "some key presses are missed" | [P3](05-triage.md#p3) |
| "Logger window doesn't open", "logger is empty", "where is the FPS counter?", "log lines are cut off" | [P4](05-triage.md#p4) |
| "I can't quit the game", "logger window stays open after the game closes" | [P5](05-triage.md#p5) |
| TerraForM: "I ran out of power too fast", "rover slides too far", "game over straight away" | [P6](05-triage.md#p6) |
| TerraForM: "my score is wrong", "score didn't reset after restart" | [P7](05-triage.md#p7) |
| TerraForM: "levels come in the wrong order", "I got a level called 'Map Name'", "never reached Leviathan Falls" | [P8](05-triage.md#p8) |
| TerraForM: "can't type my name properly", "can't type æøå", "name shows `##`", "letters appear I didn't type" | [P9](05-triage.md#p9) |
| TerraForM: "it said GAME OVER but I could keep playing / still finished the level" | [P10](05-triage.md#p10) |
| TerraForM: "planting doesn't work", "P does nothing" | [P11](05-triage.md#p11) |
| "Game disappears behind the menu", "screen leaves trails / old characters stay" | [P12](05-triage.md#p12) |
| Snake: "snake goes off the screen and never dies", "can't lose" | [P13](05-triage.md#p13) |
| Platformer: "debug text everywhere", "FPS not shown", "player vanishes into walls" | [P14](05-triage.md#p14) |
| TerraForM: "the story is in English, where's the Norwegian version?" | [P15](05-triage.md#p15) |
| Launcher: "game says (not built)", "launcher won't start a game", "game window stops at Press any key" | [P16](05-triage.md#p16) |
| EternalDigger: "I'm stuck in a hole", "how do I get my ladders back", "can't jump", "where is my inventory", "can't dig the grass", "SPACE/E does nothing", "lost my ore", "can't see any ore", "my save is gone", "no CONTINUE", "can't build the lift", "fell down the shaft", "can't dig at 110 m", "TOO HARD", "can't plant dynamite", "the lamp keeps getting darker", "climbing got slow" | [P17](05-triage.md#p17) |
| "Things pass through each other", "things sink into the floor", "bullet didn't hit", "hit counted twice", "physics events don't fire", "raycast misses", "platformer player stuck or falls through", "bodies hang in the air", "box doesn't spin" | [P18](05-triage.md#p18) |
| 3D: "GPU mode not available", "it falls back to CPU", "`GlRenderer` throws", "glfw3.dll missing", "GPU picture differs from CPU" | [P21](05-triage.md#p21) |
| SkyStack: "can't reach the next floor", "my combo vanished", "the screen killed me", "high score is gone" | [P20](05-triage.md#p20) |
| Lua: "my script doesn't load / run", "the object stands still", "F5 doesn't reload my change", "lua54.dll missing" | [P19](05-triage.md#p19) |

## File index

| File | Contents |
|---|---|
| [01-system-map.md](01-system-map.md) | Projects, processes, Win32 surface, TCP logger link, CI |
| [02-state-model.md](02-state-model.md) | Runtime state (no DB): engine objects, TerraForM state, map file format |
| [03-rules.md](03-rules.md) | Disputable rules: frame timing, key semantics, scenes, TerraForM power/score/sequences/maps |
| [04-flows.md](04-flows.md) | Startup, frame loop, scenes, logger, map load, TerraForM command flow, launcher, EternalDigger miner frame and start/save, physics step and queries, Lua script load and hooks — each with "stops here if" |
| [05-triage.md](05-triage.md) | **Entry point.** Facts to collect, first checks, playbooks P1–P21 |
| [06-diagnostics.md](06-diagnostics.md) | Where evidence lives (there are no log files), every user-visible message verbatim |
| [07-checks.md](07-checks.md) | Read-only diagnostic commands, one per named question (replaces SQL, since there is no DB) |
| [08-operator-actions.md](08-operator-actions.md) | What a user/support can do without a code change, and what to escalate |
| [09-glossary.md](09-glossary.md) | Support words ⇄ meaning ⇄ code identifier |

## Hard rules

- **Read-only by default.** Every check in [07-checks](07-checks.md) only reads files, processes or ports. Do not edit
  map files, `.csproj` files or source during triage. A fix that changes code or assets goes to a developer as a PR.
- **PII discipline.** The only personal data in the system is the TerraForM player name (`TerraformGame.Playername`),
  which is typed at start and kept in memory only. Do not repeat a user's name, or their machine's user or path names,
  in shared channels. Refer to "the user" and give paths relative to the game folder.
- **State the environment.** Every answer names the **game**, the **OS/console host**, **how it was launched** (which
  working directory) and the **build configuration** if known. Behaviour differs a lot between them
  (see [01 § Environments](01-system-map.md#environments)).
- **Absence of output proves nothing.** The engine writes no logs on any path, success or failure, apart from Lua
  script errors that only reach an enabled logger ([06](06-diagnostics.md#no-logs)). Most failures are silent: an
  ignored Win32 return value or a guard clause.
- **Report a concrete cause and a concrete fix**, in the user's words. "Works as coded" counts as a cause when the
  rule is in [03-rules](03-rules.md). Say which rule and quote its identifier.
