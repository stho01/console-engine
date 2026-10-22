# console-engine


[![Build and test](https://github.com/stho01/console-engine/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/stho01/console-engine/actions/workflows/build-and-test.yml)

## Platforms

On Windows the engine talks to the Win32 console directly. On macOS and Linux it renders with ANSI escape sequences, so any
modern terminal works (Terminal.app, iTerm2, kitty, WezTerm, Ghostty, ...). The platform is picked at runtime, so the same
build runs everywhere.

macOS/Linux notes:

- Font face/size and window resizing can't be controlled from the game. Set them in your terminal; the game asks the terminal
  to resize to fit, which Terminal.app and iTerm2 honor. If the terminal is smaller than the game, the output is clipped.
- Colors use 24-bit RGB when `COLORTERM` is `truecolor`/`24bit`, otherwise the terminal's 16 color palette.
  Override with `CONSOLE_ENGINE_COLORS=truecolor|16`.
- On macOS key up/down state is read from the OS (like `GetAsyncKeyState` on Windows), which gives precise held/released keys.
  Like on Windows it sees keys pressed while another window is focused. Elsewhere, or with `CONSOLE_ENGINE_INPUT=terminal`, keys
  are read from the terminal; terminals only report presses and auto repeat, so a key counts as held until its repeat stops.
