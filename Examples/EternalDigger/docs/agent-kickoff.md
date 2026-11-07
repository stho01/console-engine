# EternalDigger: agent kickoff prompt

Paste everything below the line into the coding agent, started at the repo root. Each later tier reuses the same prompt; only "This round" changes.

---

You are working on **EternalDigger**, a text-mode ECS mining game built on the console engine in this repository.

## Where things are

- **Repo root (the engine):** `/Users/baxon/Documents/workspace/personal/console-engine`
- **The game:** `Examples/EternalDigger`, tests in `Examples/EternalDigger.Tests`
- **Branch:** `feat/eternal-digger` (already checked out). Work on this branch, or on short-lived branches off it, one per feature.
- **Repo rules:** `AGENTS.md` (loaded via `CLAUDE.md`). They apply in full: performance first, no allocations per frame, code style, Conventional Commits, and keeping `docs/agent/` current.
- **Game docs:** `Examples/EternalDigger/README.md`, then `Examples/EternalDigger/docs/`:
  - `game-brief.md`: how the game plays today, without tech.
  - `feature-handover.md`: **what to build.** Features in priority tiers, with specs, controls and suggested glyphs and colours. This is the source of truth for new behaviour.
  - `architecture.md`, `rendering-and-effects.md`, `working-with-the-code.md`, `testing.md`, `reference.md`: how the game is built, and how to change and test it.
- **Design contract:** `docs/design/eternal-digger.md`.

## Before writing code

1. Read `AGENTS.md`, the game README, `game-brief.md`, `feature-handover.md`, `architecture.md` and `working-with-the-code.md`. Skim the rest as you need them.
2. Run `dotnet build` and `dotnet test Examples/EternalDigger.Tests` and confirm both pass **before** you change anything.
3. Find where each feature of this round touches the code: the surface shops, interaction, prompts and input (for the upgrade counter); digging, the light map and the lamp, glints, the bag and the HUD (for the upgrades); the lighting, the miner render, the HUD and time underground (for lamp oil); mine generation, tiles, glints, effects and the save file (for gems and rare finds); climbing and the bag (for bag weight).
4. Send me a short plan for this round: the files and systems you'll touch, new components or systems, any engine changes you think you need, and anything in the handover that conflicts with the code or the repo rules. Wait for my go-ahead before building.

## This round: Tier 2 only

Tiers 0 and 1 are done and approved (miner visibility, saving, accelerating climb, lift, obsidian wall, act 2 layer, dynamite). Build the four Tier 2 features from `feature-handover.md`, in this order:

1. **Upgrade shop.** One counter where Up/Down cycles the items (ladders, oil, the upgrades, the blasting kit, dynamite) and E buys the selected one, with a small panel above it showing the item, tier pips `●●○` and the price (red when unaffordable). It replaces today's single-item prompts. Four upgrade lines with three tiers each: shovel, lamp, bag and detector, with the handover's effects and prices. Upgrades show on the miner where the handover suggests (shovel and lamp colour per tier). Save the upgrade tiers.
2. **Lamp oil.** A tank (e.g. 100 units) that burns only underground; below 30% the cone shortens and flickers, empty leaves only the dimmed close radius (outlines and glints still show). Refill at the counter. An oil gauge in the HUD, smoke and a dim lamp glyph when low. It never kills. Save the oil level.
3. **Gem ore below the wall, and rare finds.** Gems already exist from Tier 1: check them against the handover's spec and adjust where they differ. Add rare finds (e.g. fossil `§`, relic `¥`): very rare single tiles with a slow pulsing glint, a big star burst and a floating name when broken, sold high, and a `✦` counter in the HUD. Leave the trophy shelf for Tier 3. Save the rare finds.
4. **Bag weight.** Ore weights (copper 1, silver 1, gold 2, gem 2, rare finds as you propose); above the threshold climbing slows a step per 10 weight; walking unaffected; no hard cap. With oil, a heavy bag burns more while climbing. The bag value turns yellow and the backpack glyph bulges when heavy. The bag upgrade from the shop works with this.

Bump the save-format version for each change to the save and keep reading every older version.

Don't start on Tier 3 or later, even where it looks easy. I'll playtest each tier before we move on.

## Working rules

- **Repo rules win.** Where the handover conflicts with `AGENTS.md` or `working-with-the-code.md` (e.g. allocations, one `char` per cell), follow the repo rules and tell me what you changed.
- **Game code over engine code.** Change the engine only when the game truly can't do something without it. Keep any engine change general, small and called out in your report; a public API change in `ConsoleEngine` is breaking (`!` commit).
- **Stay inside the constraints** in the handover's "Engine constraints" section: text characters only, the existing 16 colours, the 30-tile-wide grid, keyboard only, no new keys during play.
- **No per-frame cost from saving.** Write saves only on the triggers in the spec (sale, purchase, Esc, optional timer), never every frame, and keep file I/O and serialization out of the hot path.
- **Version the save file.** Include a save-format version number from day one. Handle a missing, corrupt or older save without crashing: fall back to NEW MINE with a message.
- **Save the mine as seed + changed tiles,** not the full grid, as the handover says.
- **Tunable numbers in one place.** Put new prices, depths, speeds, timings and thresholds with the game's existing constants (see `reference.md`), with short comments. I'll tune them myself.
- **Placeholder visuals are fine.** Use the handover's suggested glyphs and colours, as long as each fits one UTF-16 `char`. An asset designer will refine them later, so keep visual definitions easy to find and swap.
- **Keep the game playable after every commit.** One logical change per commit, Conventional Commits. Don't break existing behaviour (digging, ladders, rescue, selling, glints, effects).
- **Docs in the same commit.** Update the game docs (per "What to update" in `working-with-the-code.md`), `docs/agent/` (per the table in `AGENTS.md`) and `docs/design/eternal-digger.md` where behaviour, keys, strings or state change.
- **Testing:** add tests for the rules (e.g. save round-trip: save, load, compare state; corrupt and old-version files; miner cells never covered by particles). Use a fixed seed. The game can't run headless, so say plainly what you could not check in a real console; I'll play it myself with `./EternalDigger 42`.
- **When the spec is unclear or doesn't fit the code,** pick the simplest reading that matches the handover, note it, and carry on. Stop and ask only for decisions that would be hard to undo.
- **Don't push or open a PR** unless I ask.

## When you're done, report back with

- What you built, per feature, and the commits.
- How you verified it: `dotnet build` (0 warnings), `dotnet test` results, tests added, and what still needs checking in a real console.
- Any engine changes, and why.
- Where the tunable values and the visual definitions live, and where the save file is written.
- Which docs you updated.
- Anything from the handover you changed, skipped or interpreted, and open questions for the next round.
