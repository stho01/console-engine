# CLAUDE.md

Windows-only .NET 8 console game engine (`ConsoleEngine`, `ConsoleEngine.Windows`, `ConsoleEngine.Logger`) plus example
games under `Examples/`. Build and test: `dotnet build`, `dotnet test` (CI: `.github/workflows/build-and-test.yml`).

## Documentation

Support/triage docs for AI agents live in [`docs/agent/README.md`](docs/agent/README.md). They describe behaviour
using exact identifiers, file paths and user-visible strings, so they go stale silently when code changes.
**Update them in the same change** as follows:

| If you change… | Update |
|---|---|
| A project, process, port, P/Invoke surface, game size/font/flags, CI | `01-system-map.md` |
| Runtime state fields, TerraForM state, the map file format or a shipped map | `02-state-model.md` |
| A rule: timing, key semantics, scenes, drawing, power, damage, sequences, game over, score, planting, map rotation, name entry | `03-rules.md` (+ the affected playbook in `05-triage.md`) |
| A guard clause, early return, thrown exception or ignored Win32 result in a flow | `04-flows.md`, `06-diagnostics.md` |
| Any user-visible string (`HudTexts`, `Sprites`, logger output, exception messages) | `06-diagnostics.md` (verbatim) |
| A new failure mode or common complaint | `05-triage.md` + the routing table in `README.md` |
| A file/port/runtime prerequisite | `07-checks.md` |
| A control key or anything a user can do without code | `08-operator-actions.md` |
| A new domain term | `09-glossary.md` |

Rows marked **[inferred]** in the docs are guesses about support's wording and scope. Replace them with real ticket
language when available.
