# console-engine


[![Build and test](https://github.com/stho01/console-engine/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/stho01/console-engine/actions/workflows/build-and-test.yml)

## Platforms

On Windows the engine talks to the Win32 console directly. On macOS and Linux it renders with ANSI escape sequences, so any
modern terminal works (Terminal.app, iTerm2, kitty, WezTerm, Ghostty, ...). The platform is picked at runtime, so the same
build runs everywhere.

macOS/Linux notes:

- Font face/size and window resizing can't be controlled from the game. Set them in your terminal; the game asks the terminal
  to resize to fit, which Terminal.app and iTerm2 honor. If the terminal is smaller than the game, the middle of the frame is shown.
- Terminal cells are about twice as tall as wide. Games that draw pixels rather than text (like Asteroids) should set
  `Console.PixelMode = true` in `OnInitialize`; each cell then shows two square pixels as half blocks, so a 200x200 game
  needs 200x100 cells. Force it on or off with `CONSOLE_ENGINE_RENDER=halfblock|text`.
- Colors use 24-bit RGB when `COLORTERM` is `truecolor`/`24bit`, otherwise the terminal's 16 color palette.
  Override with `CONSOLE_ENGINE_COLORS=truecolor|16`.
- On macOS key up/down state is read from the OS (like `GetAsyncKeyState` on Windows), which gives precise held/released keys.
  This needs your terminal app to be allowed under System Settings > Privacy & Security > Input Monitoring. Like on Windows it
  sees keys pressed while another window is focused. Without the permission, elsewhere, or with `CONSOLE_ENGINE_INPUT=terminal`,
  keys are read from the terminal; terminals only report presses and auto repeat, never releases. A key then counts as held
  for a short tap (120ms), and again from its first auto repeat until the repeat stops.
  Keys are mapped through the active keyboard layout like on Windows; punctuation keys without a Windows equivalent
  (`ø`, `ü`, ...) fall back to their US layout position.
- The terminal input waits 750ms for the first auto repeat before it reports a key as released. If your "delay until repeat"
  is longer, set `CONSOLE_ENGINE_REPEAT_DELAY` to it in milliseconds.
- With `EnableLogger` the logger opens in a new Terminal window on macOS. On Linux it starts in `$TERMINAL`
  (or `x-terminal-emulator`); if neither is available logging is disabled.
