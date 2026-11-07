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
3. Find where each feature of this round touches the code: the climb timing and motion (for the accelerating climb); the mine shaft, the surface spots, interaction, prompts, the HUD and the save file (for the elevator); the mine generation, tiles and their looks, digging, glints, input, effects and the save file (for dynamite and the new layer).
4. Send me a short plan for this round: the files and systems you'll touch, new components or systems, any engine changes you think you need, and anything in the handover that conflicts with the code or the repo rules. Wait for my go-ahead before building.

## This round: Tier 1 only

Tier 0 (miner visibility, saving) is done and approved. Build the three Tier 1 features from `feature-handover.md`, in this order:

1. **Accelerating climb.** Holding Up on a ladder starts at the current 0.17 s per tile and ramps towards ~0.06 s per tile after ~4 tiles of continuous climbing; releasing Up or leaving the ladder resets it. At top speed, a dust trail below the miner and faster legs. A 100 m climb should take about 7–8 s; cover the timing with a frame-by-frame test.
2. **Elevator.** Bought in 10 m segments at a new sign by the mine entrance (200g, +100g per segment), running down the mine shaft. E on the column calls the car or boards it, Up/Down rides at ~0.04 s per tile, stopping at the top and bottom. Shaft, car, cable and speed-line visuals, "ELEVATOR +10m" feedback, and the elevator depth in the HUD. Save the elevator depth (bump the save-format version and keep reading version 1 saves).
3. **Dynamite wall + first new layer.** A 3–5 tile obsidian band at ~110 m that the shovel can't break ("TOO HARD"); the blasting kit (~2500g) and dynamite sticks (~100g each) at the surface shop; hold E 0.5 s underground to plant a stick in the faced tile, a 2 s fuse, a 3 × 3 blast that clears any rock, pushes the miner back, destroys ladders and bags the ore. Below the wall, the act 2 layer: new rock and one new ore, in its own palette, with glints hinting at it from ~95 m. The miner stays visible during a blast (this finishes the Tier 0 acceptance test). Save the kit and the dynamite stock.

The handover's Up/Down item-cycling shop counter belongs to Tier 2. For buying the kit and sticks in this round, pick the simplest scheme that reuses E and the existing keys, and propose it in your plan.

Don't start on Tier 2 or later, even where it looks easy. I'll playtest each tier before we move on.

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
