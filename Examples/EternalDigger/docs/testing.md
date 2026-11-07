# Testing

`Examples/EternalDigger.Tests` is an NUnit 5 project that references the game. It needs no console and no keyboard:
the rules live in plain classes (`Mine`, `Tiles`, `Shop`, `LightMap`) and in two public system methods that take
their input as arguments. Rendering is tested by drawing into a `RenderConsole` over `Fakes/FakeConsoleHandler` and
reading cells back with `GetCharAt` (characters only; the engine can't read colours back).

```bash
dotnet test Examples/EternalDigger.Tests
```

CI (`.github/workflows/build-and-test.yml`) runs it with the rest of the solution on Windows, macOS and Ubuntu.

## What is covered

| Class | Tests | Covers |
|---|---|---|
| `MineTests` | 48 | Same seed gives the same mine, different seeds differ; sky, grass, shaft and bedrock layout; rows generated in whole blocks; outside the mine is bedrock (beside) or air (above) and ignores writes; hitting damages then breaks, returns the type, ignores unbreakable tiles; the support rule; ladder placement rules; no tile type above its start depth (20 seeds); every ore and granite eventually appears; `CollectChanges` is empty for a fresh mine and lists only changed tiles; `Apply` outside the mine is ignored; the wall is obsidian 3–5 thick in every inner column (10 seeds) with bedrock beside it; below it only slate, quartzite, gems and caves; the shovel does nothing to obsidian |
| `TilesTests` | 38 | `IsSolid`, `IsBreakable`, `IsBlastOnly`, `IsOre`/`OreValue` tables (with obsidian and act 2); act 2 tiles need hits; deeper ore is worth more; hits grow by one per 25 m; unbreakable tiles need 0 hits |
| `MinerControlSystemTests` | 16 | Real frames of gameplay with scripted input (below) |
| `LightMapTests` | 7 | The surface is bright; outside the window is dark; neighbours of the lamp are bright even when solid; a tunnel in the cone is lit and marked seen; rock blocks the lamp; tiles behind the miner stay dark; light falls off with distance (bright → lit → dim → dark) |
| `ParticleLooksTests` | 2 | No particle style is born blank (which would erase a cell for a frame); `GlyphAt` matches `Character` and `Color` |
| `MinerVisibilityTests` | 4 | Holding a dig key on gold at 100 m (right, left, down) with the real particle systems and renderers: on every frame the face `☻` and the lamp are on screen and the miner's empty back column has no particle; the tile breaks and digging made particles. `ParticleRenderSystem.Dim` maps bright colours to their dark twins |
| `SaveFileTests` | 17 | Write/read round trip of every field (also the lift) and changed tile; save → load → `ApplyTo` rebuilds the same mine and miner; saving over an old save leaves one file; missing file; garbage file is `Corrupt` and moved to `.bad`; every truncation and trailing bytes are `Corrupt`; versions 0 and 4 are `Unsupported` (and moved aside); hand-written version 1 and 2 files load (no lift / no gems); a car row off its landings is `Corrupt`; out-of-range column, negative gold, unknown tile type; `NewMine` start state; `GameSaver` writes and counts saves, and reports a failed write without throwing |
| `SaveSystemTests` | 3 | A requested save is written that frame; no autosave on the surface; one autosave per 60 s underground |
| `TitleMenuTests` | 6 | CONTINUE first with a save; NEW MINE at once without one; NEW MINE with a save asks to confirm with NO selected; YES starts over; NO goes back; Up/Down wrap |
| `ElevatorTests` | 12 | Obsidian in the next segment blocks it (nothing charged); the lift stops at 100 m above the wall; building charges 200g then 300g and digs out the shaft (rock stays under the bottom landing); ore in the way goes into the bag, ladders stay; too poor changes nothing; the car takes 0.04 s per tile; frames of boarding at the top and riding down, riding up and stepping off onto the surface, calling the car from the bottom (the caller gets in), Down at the bottom steps off and digs, a rescue leaves the car; `E` at the lift sign builds a segment |
| `ShopTests` | 6 | Delivering pays every ore and empties the bag; an empty bag pays 0; buying a ladder with and without enough gold; collecting each ore; non-ore isn't collected |

Names follow the repository's `Method_State_Expected` style, for example
`TryPlaceLadder_SkyOrSurface_ReturnsFalse`.

## Playing frames: `MinerControlSystemTests`

The scene runs `MinerControlSystem` and then `MinerMotionSystem` every frame. The tests do the same, without a scene:

```csharp
private void Play(MinerInput input, float seconds)
{
    for (var elapsed = 0f; elapsed < seconds; elapsed += FrameSeconds)   // 1/60 s
    {
        _control.Control(ref _miner, ref _position, input, FrameSeconds, _commands);
        _motion.Move(ref _miner, ref _position, FrameSeconds, _commands);
        _commands.Clear();   // the effects that were recorded are thrown away
    }
}
```

Three things make this possible:

- **`MinerInput`** is a plain struct. The system builds it from the keyboard (`MinerInput.FromKeyboard`); a test
  writes `new MinerInput { Down = true }`. `Input.Instance` is never touched, so no console is needed.
- **`Control` and `Move` are public** and take the miner, its position, the input, the time step and a
  `CommandBuffer` as arguments. The systems can be constructed without joining a `SystemGroup`.
- **`Mine.At` returns a `ref`,** so a test can build exactly the tiles it needs and doesn't depend on what the seed
  generated:

```csharp
private void FillShaft(TileType type, int from, int to)
{
    for (var row = from; row <= to; row++)
        _mine.At(Mine.ShaftColumn, row) = new TileCell { Type = type };
}
```

An example, the core of the game loop:

```text
Control_LaddersAllTheWay_ClimbsBackToTheSurface
  shaft dug out 4 tiles below the start ladders, stone under it
  ladders in the top 3 of them, the miner in the bottom one
  press SPACE for one frame   → the last ladder is placed (ladders − 1)
  hold UP for 3 s             → the miner is back on the surface row, same column
```

The other tests in the class: walking along the surface; stopping at the left edge; grass can't be dug; holding down
in the shaft climbs and digs deeper and ends exactly on a tile; up without a ladder does nothing; no ladder placed
with 0 ladders; falling until landing (and `FallRows` resets); digging gold puts it in the bag; a full rescue (spawn,
empty bag, gold kept); releasing the rescue early resets it; a held 100 m climb takes 7–8 s (about 7.65 s at 60 fps); a
short climb keeps the start speed; digging obsidian clanks without damage; letting go of Up during a long climb starts slow again; the `ClimbUpDuration`
ramp.

## What is not covered

| Not tested | Why | How it was checked |
|---|---|---|
| Render systems other than particles and miner | Not written yet; `MinerVisibilityTests` shows how (`Fakes/FakeConsoleHandler`) | Frames captured from a real run in a macOS terminal and inspected |
| `InteractionSystem` | Reads `Input.Instance` directly (`E`), which needs a console | `Shop.Deliver`/`TryBuyLadder` are tested; the spot checks are `Layout` one-liners. That it calls `GameSaver.Request` is checked by reading |
| `TitleScene`, Esc save | Read `Input.Instance` and switch scenes on a `GameBase` | The rules are in `TitleMenu` (tested); not run in a real console yet |
| `EffectSpawner` and particle systems | Visual; their output is entities in a `World`. The looks (`ParticleLooks`) are tested; `MinerVisibilityTests` runs physics and lifetime, but only checks the miner's cells | Reading and running the game |
| `CameraSystem`, `GlintSystem`, `MistSystem` | Need a `World`, a `Camera` and randomness | Reading and running the game |
| The whole scene | `GameBase` creates a real console handler | — |

### Ideas for more coverage

- `ParticlePhysicsSystem` can be tested with a real `World` (`using var world = new World();`), a `SystemGroup` and
  hand-made particles: drop a `TileCollider` particle onto a stone tile and check it rests on top.
- `InteractionSystem.Interact` could take a "pressed" flag like `MinerControlSystem.Control` takes `MinerInput`, so it
  can be tested the same way.
- `LightMap.Compute` is cheap enough to test a full screen of tiles for invariants (for example: nothing behind a
  fully solid ring is ever lit).

## Writing a new test

1. Pick the class under test and put the test in `<Class>Tests.cs`.
2. Build the state you need explicitly: `new Mine(seed)` plus `mine.At(c, r) = new TileCell { … }` writes, a `Miner`
   with the fields you care about. Don't rely on what a seed happens to generate.
3. Use a row deep enough to be away from daylight when light or caves matter (`Mine.SurfaceRow + 50`).
4. Name it `Method_State_Expected`.
