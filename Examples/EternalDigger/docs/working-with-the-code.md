# Working with the code

How to change EternalDigger without breaking its rules: the build-and-check loop, the rules every change follows, and
step-by-step recipes for the changes you're most likely to make. Read [architecture.md](architecture.md) first if
you haven't.

## The loop

```bash
dotnet build
```

```bash
dotnet test Examples/EternalDigger.Tests
```

```bash
cd Examples/EternalDigger/bin/Debug/net10.0 && ./EternalDigger 42
```

- Run from the repository root. The repo builds with `TreatWarningsAsErrors`, so a warning fails the build.
- Use a fixed **seed** while working (`./EternalDigger 42`): the same mine every run makes changes comparable.
- The game needs a real console. On macOS the engine reads the physical keyboard, so keys piped into a terminal
  (`script`, `expect`) aren't a reliable test. Test rules with the frame-by-frame tests instead
  ([testing.md](testing.md)).

## Rules every change follows

These come from the repository's `AGENTS.md`; they apply here in full.

| Rule | In this game |
|---|---|
| **No allocations per frame** | No `new` of classes or arrays, no LINQ, no lambdas, no string building in `OnUpdate`. Build art and strings once as `static readonly`/`const`; rebuild HUD text only when a shown value changes (see `HudSystem`); format numbers into a reused buffer (see `FloatingNumberRenderSystem`). The mine allocates only when it grows by a 64-row block |
| **Scale by time** | Every speed, timer and rate uses `deltaSeconds`. Things that happen "every so often" use an accumulated timer (`GlintSystem._elapsed`) or a budget (`MistSystem._budget`), never "per frame" |
| **Structural changes through `Commands`** | Never `World.Create`/`Destroy` inside a query loop. Effects go through `EffectSpawner` with the system's `Commands` |
| **Components are `unmanaged` structs** | Data only, in `Components/`. Tags have no fields |
| **Systems** | `<Name>System : EntitySystem` in `Systems/`, `sealed`, services through the primary constructor, queries created in `OnCreate` and kept in fields |
| **Style** | File-scoped namespaces, `var`, 4 spaces, max 125 characters per line, `//****` section banners (`fields`, `ctor`, `props`, `lifecycle`, `public methods`, `internals`), XML `<summary>` on every type. Nullable is off: no `?` on reference types |
| **One cell, one `char`** | Only characters that fit one UTF-16 `char` (box drawing, blocks, symbols). No emoji |
| **Docs in the same commit** | See [What to update](#what-to-update) |

## Recipes

### Tune a number

Most numbers are `const`s with a doc comment, grouped at the top of their class. The
[tuning cheat sheet](gameplay.md#tuning-cheat-sheet) says which file holds what. After changing one:

1. Run the tests. Some pin values on purpose (for example `TilesTests.HitsToBreak_DirtAtDepth_GainsAHitPerStep`).
   Fix the test if the new value is intended.
2. Update the number in [gameplay.md](gameplay.md) and in `docs/agent/03-rules.md` § EternalDigger.

### Add a tile type

Say you want **coal**: common, cheap, from 5 m.

1. `Mining/TileType.cs`: add `Coal` to the enum (`byte`, so up to 256 types).
2. `Mining/Tiles.cs`:
   - add `CoalValue` and `CoalDepth` constants;
   - add it to `IsOre` and `OreValue` if it can be sold;
   - give it a base hit count in `HitsToBreak` (without one it returns 0, and the tile breaks on the first hit).
   `IsSolid` and `IsBreakable` already treat every new type as solid and breakable.
3. `Mining/Mine.cs` → `Underground`: decide where it appears. Order matters: earlier checks win. For a vein, add
   `if (depth >= Tiles.CoalDepth && IsVein(column, row, <unused salt>, <chance>)) return TileType.Coal;` among the
   ore checks. Pick a salt no other call uses, or the new tiles will line up with an existing vein.
4. `Rendering/TileLooks.cs`: add a `TileLook` (texture, colours, and a speck for ore) and a `For` case. Without one it
   falls back to dirt.
5. `Effects/ParticleLooks.cs`: map it in `DebrisStyle` (add a `ParticleStyle` and its colours in `Color` if no
   existing style fits).
6. If it is ore:
   - add a bag slot to `Components/Miner.cs` (`CoalOre`) and to `OreCount`;
   - handle it in `Shop.Collect` and `Shop.Deliver`;
   - show it in `HudSystem` (the status string, the changed-value check, the icon colours) and the bag value;
   - give it a glint chance in `GlintSystem.Chance` and a colour in `ParticleLooks.GlintVariant`/`GlintColor`;
   - clear it in `MinerControlSystem.UpdateRescue`.
7. Tests: add it to the `TilesTests` tables and to `MineTests.At_Generation_NeverPlacesATypeAboveItsDepth` /
   `At_Generation_EventuallyPlacesEveryType`; for ore, to `ShopTests`.
8. Docs: [gameplay.md](gameplay.md) (the tile tables), `docs/agent/03-rules.md`, `06-diagnostics.md` if the HUD text
   changed.

### Add an effect

1. If it needs a new look, add a `ParticleStyle` value (`Components/Particle.cs`) and its character ramp and colours
   in `Effects/ParticleLooks.cs`.
2. Add a method to `EffectSpawner` that takes `CommandBuffer commands` first and positions in **world cells**. Use the
   private helpers: `Spawn(...)` for free particles, `Debris(...)` for chunks that land on rock. Creating a particle
   by hand? Give it `ParticleLooks.GlyphAt(style, variant, 0f)` as its `Glyph`, or it shows a wrong look in its
   first frame. `ParticleLooksTests` checks that no style is born blank. Keep vertical speeds
   about half the horizontal ones, because cells are twice as tall as wide.
3. Call it from a Simulation system with that system's `Commands`, for example
   `effects.Pop(Commands, Layout.TileCenter(column, row));`.
4. Bound it: a fixed count per event, or a cap and a time-based rate for continuous effects (see `MistSystem`).
   Never spawn "every frame": the loop is uncapped, so that would scale with FPS.

A particle can have at most 6 components, because `CommandBuffer.Create` has overloads for up to 6. Debris already
uses all six (`Position`, `Velocity`, `Glyph`, `Particle`, `Physics`, `TileCollider`). For more data, add a field to
`Particle` or `Physics` rather than a seventh component.

### Add a system

1. Create `Systems/<Name>System.cs`: `public sealed class <Name>System(<services>) : EntitySystem`, query fields
   built in `OnCreate`, work in `OnUpdate(float deltaSeconds)`.
2. Add it to `MineScene.OnCreate` **at the position it should run**: game logic in `Simulation`, drawing in
   `Presentation`. Logic that reads the miner's final position for this frame goes after `MinerMotionSystem`;
   anything that needs the camera goes after `CameraSystem`.
3. A renderer takes the `Canvas` and the `Camera`, converts with `floor(world - camera.Position)` (the camera is on
   whole cells), draws world layers with `Draw` and overlays with `DrawOver`.
4. Need the miner? Query `Miner` (+ `Position`), take `chunk.GetSpan<Miner>()[0]` from the first chunk, and do
   nothing if there is none (see `LightingSystem`).

### Add a key or action

1. `MinerInput.cs`: add a property and read it in `FromKeyboard` (`Held` for continuous, `Pressed` for one-shot).
2. `MinerControlSystem.Control`: handle it. Remember the order there: rescue, then "moving? stop", then falling, then
   keys. Actions that need the miner standing still go after the falling check.
3. Add a test in `MinerControlSystemTests` that plays it with a scripted `MinerInput`.
4. Update `HudSystem.KeyHelp` (keep it within 120 characters), the [README](../README.md) controls,
   [gameplay.md](gameplay.md), and `docs/agent/08-operator-actions.md` and `06-diagnostics.md` (the help text is a
   user-visible string).

Keys that aren't about the miner (like `ESC`) can be read wherever they belong, as `EternalDiggerGame.OnUpdate` and
`InteractionSystem` do with `Input.Instance`.

### Show a value in the HUD

Follow the pattern in `HudSystem.DrawStatus`: keep a `_shown<Value>` field per value, rebuild `_status` only when one
of them differs, draw the cached string every frame. String interpolation is fine there because it only runs on a
change.

### Add a spot on the surface (a new building or interaction)

1. `Layout.cs`: add its columns as constants and an `IsAt<Spot>(column, row)` helper.
2. Draw it in `HouseRenderSystem` (or a new render system after it), in world cells from the `Layout` constants.
3. React to `E` in `InteractionSystem.Interact` and add a prompt text and branch in `PromptRenderSystem`.
4. Effects origins live as `static readonly Vector2` on `InteractionSystem` (`DoorEffectOrigin`,
   `ShopEffectOrigin`).

## Debugging tips

- **Reproduce with a seed.** Every mine is a function of its seed: note the seed when something odd happens.
- **Inspect the world in a test.** `new Mine(seed)` works without a console; `mine.At(column, row)` returns a `ref`,
  so a test can also set up any tile layout directly.
- **Play frames in a test.** The harness in `MinerControlSystemTests` runs `Control` + `Move` with scripted input at
  60 fps. It is the quickest way to check a movement rule.
- **Look at a frame without a console.** On macOS/Linux, run the game for a few seconds under `script` and turn the
  ANSI output into text with a small parser; this is how the screenshots in these docs were made. The output is a
  diff of frames, so replay it into a 120 × 48 grid.
- **Wrong row or column on screen?** Check which coordinate system a value is in ([architecture.md →
  Coordinates](architecture.md#coordinates)). Tile ↔ world uses `Layout`, world ↔ screen subtracts the camera.

<a id="what-to-update"></a>
## What to update

A change isn't finished until its docs match, in the same commit:

| You changed… | Update |
|---|---|
| A rule, number, key or what the player sees | [gameplay.md](gameplay.md), the [README](../README.md) if it's a control, and `docs/agent/03-rules.md` § EternalDigger |
| A user-visible string (HUD, prompts, sign) | `docs/agent/06-diagnostics.md` § EternalDigger screen text, verbatim |
| A system, its order, a component or a service | [architecture.md](architecture.md), [reference.md](reference.md) |
| Drawing or an effect | [rendering-and-effects.md](rendering-and-effects.md) |
| Tests | [testing.md](testing.md) |
| A new support complaint or failure mode | `docs/agent/05-triage.md` P16 |
| Files or folders | [reference.md](reference.md), `AGENTS.md` folder structure |

`docs/design/eternal-digger.md` is the original design contract; update it when a change goes against it.
