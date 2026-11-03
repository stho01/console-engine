# CLAUDE.md

Windows-only .NET 8 console game engine (`ConsoleEngine`, `ConsoleEngine.Windows`, `ConsoleEngine.Logger`) plus example
games under `Examples/`. Build and test: `dotnet build`, `dotnet test` (CI: `.github/workflows/build-and-test.yml`).

**Read [`AGENT.md`](AGENT.md) first.** It covers where to find answers, project and folder layout, the performance
rules for the frame loop, code style (`.editorconfig`), naming, and the commit format
([Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/#specification)).

## Documentation

Support/triage docs for AI agents live in [`docs/agent/README.md`](docs/agent/README.md).
**Keeping them and `AGENT.md` up to date is mandatory for every change.** The rule and the table of which change
updates which file are in [AGENT.md § Keeping agent docs up to date](AGENT.md#keep-docs-current).
