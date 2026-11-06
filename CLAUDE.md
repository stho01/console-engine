# CLAUDE.md

The shared agent instructions for this repository live in AGENTS.md and are imported here, so Claude Code loads them
at the start of every session:

@AGENTS.md

## Documentation

Support/triage docs for AI agents live in [`docs/agent/README.md`](docs/agent/README.md).
**Keeping them and `AGENTS.md` up to date is mandatory for every change.** The rule and the table of which change
updates which file are in [AGENTS.md § Keeping agent docs up to date](AGENTS.md#keep-docs-current).

## New games go in the launcher

**Every new game must be added to the launcher (`Examples/Launcher`) in the same change.** Two places, both required:

1. An entry in `GameCatalog.Games` (`Examples/Launcher/GameCatalog.cs`): title, project name (also the exe's assembly
   name) and a short key help.
2. A `ProjectReference` to the game's `.csproj` in `Examples/Launcher/Launcher.csproj`. It is build order only: the
   `ItemDefinitionGroup` there already makes it `ReferenceOutputAssembly=false`, `Private=false`.

`GameLocatorTests.GameCatalog_EveryGame_HasBuildOrderReferenceInLauncherProject` fails if step 2 is missing. Also add
the game to the `Launcher` row's game list in `docs/agent/01-system-map.md`.
