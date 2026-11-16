# Doom (console)

Plays the levels of a Doom WAD in the console: a 160 × 100 or 320 × 200 pixel 3D view drawn as half blocks
(`▀`) in exact 24-bit colour where the terminal supports it (else dithered to the console's 16 colours; C switches),
with a text status bar below.

The rendering and game code are this example's own. Every level, texture, flat, sprite and colour comes from the
WAD file at run time. **The WAD is not part of this repository** (`*.wad` is gitignored), so don't commit one.

## Getting a WAD

Use the shareware `DOOM1.WAD` (version 1.9, 4 196 020 bytes; episode 1, maps E1M1-E1M9). id Software released it for
free distribution. Then either:

- put it next to `Doom.exe` (`Examples/Doom/bin/<Configuration>/net11.0/`), or in a `wad` folder there or in the
  working directory, or
- set the `DOOM_WAD` environment variable to its full path (any IWAD or PWAD with Doom-format maps).

Without a WAD the game shows the places it searched and quits on ESC.

## Running

```bash
dotnet run --project Examples/Launcher
```

Pick **Doom (needs DOOM1.WAD)** in the launcher, or on Windows run `pwsh -File run-game.ps1 Doom`. At start-up the game
asks for a resolution: **1** (or ENTER) for 160 × 100 in a 6 × 12 font, **2** for Doom's own 320 × 200 in a 3 × 6 font
(320 console columns, same window size in conhost; the HUD text is half as big). `--resolution 160x100` or
`--resolution 320x200` skips the question:

```bash
dotnet run --project Examples/Doom -- --resolution 320x200
```

The font makes the half-block pixels square. Windows Terminal (and macOS terminals) ignore the font, so use conhost
(the launcher and `run-game.ps1` do), or zoom the terminal out with Ctrl + "-" (Cmd + "-" on macOS) until 320 columns
fit; Ctrl / Cmd + "+" zooms back in.

How it runs depends on the terminal, because a 24-bit textured frame is a lot of text (checked by eye):

| Terminal | 24-bit colour | 320 × 200 | 160 × 100 |
|---|---|---|---|
| kitty | Yes, exact colours | Smooth, high frame rate | Smooth |
| Terminal.app | Yes, exact colours | Very laggy | Plays OK |
| Command Prompt (Windows 11) | Yes, exact colours | Smooth (24-bit and dither) | Smooth (24-bit and dither) |
| Windows Terminal | Should (Windows 10 1607+) | Not checked yet | Not checked yet |

If it lags, pick 160 × 100, or press C for the 16-colour dither, which sends less text. A terminal that doesn't
report 24-bit colour (`COLORTERM`) gets the dither; `CONSOLE_ENGINE_COLORS=truecolor` forces 24-bit.

## Controls

| Key | Action |
|---|---|
| W S / ↑ ↓ | Move forward / back |
| A D | Strafe |
| ← → | Turn |
| SPACE | Fire (hold) |
| E / ENTER | Use: doors, switches |
| 1-7 | Weapon slot (1 toggles fist / chainsaw) |
| R | Toggle run (on by default) |
| TAB | Automap; PAGE UP / PAGE DOWN zoom |
| F11 | Gamma |
| C | Exact colours / 16-colour dither |
| V | Switch the view: Doom's BSP renderer / the engine's 3D mesh renderer (see below) |
| ESC | Back to the title; on the title, quit |

## What is in

- WAD reading: lumps, `PLAYPAL`, `COLORMAP`, patches composed into textures (`TEXTURE1`/`TEXTURE2`), flats,
  sprites with rotations and mirrored frames, animated flats and wall textures, switch textures.
- Rendering: BSP front to back with per-column clipping, textured walls (pegging and offsets), per-pixel floors and
  ceilings, the sky, see-through middle textures, depth-tested sprites, sector light with distance fade, weapon and
  muzzle flash, pain/pickup screen tints, light amplification and invulnerability, and the automap.
- Play at 35 tics per second: movement with sliding, steps and gravity; doors (keyed too), lifts, floors, stairs,
  ceilings, crushers, teleports, exits, light specials, damaging and secret sectors; weapons from fist to BFG;
  pickups and power-ups; monsters that wake on sight or sound, chase, open doors, shoot, throw fireballs, bite and
  charge; barrels that explode; kills, items, secrets and an intermission between maps.

## Two views: BSP and 3D mesh

V switches between two renderers of the same level, to compare them; the HUD shows which one (`VIEW BSP` /
`VIEW 3D MESH`) and the frame rate.

- **BSP** (`WorldRenderer`, the default) works like Doom: columns front to back, textured walls and floors, light
  that fades with distance.
- **3D mesh** (`Rendering/Meshes/MeshRenderer`) turns the level into triangles and draws them with the engine's
  `ConsoleEngine.Rendering3D` `Rasterizer`, the renderer Kartster uses. That renderer colours whole triangles and has
  no textures, so walls and floors are cut into 8 × 8 texel blocks in their average colour (a mosaic), while
  monsters and items stay texel-exact (one quad per run of a colour). Each sector and thing has one brightness: no
  fading with distance. It costs far more CPU (E1M1: about 180 000 triangles a frame, 7–14 ms on an Apple-silicon
  Mac, against under 0.5 ms for BSP). Built the first time you switch to it in a level.

## What is not (yet)

Sound, music, the mouse, save games, menus beyond the title, difficulty choice (medium skill is used), monster
infighting beyond retaliation, the BFG's spray, Doom II's monsters and specials. The rules are approximations of
Doom's, not a port of its code. The exact numbers are in
[`docs/agent/03-rules.md` § Doom](../../docs/agent/03-rules.md#doom).

## Code map

| Folder | What |
|---|---|
| `Wad/` | `WadFile` (directory and lumps), `WadLocator` (where the WAD is looked for) |
| `Graphics/` | `Palette`, `Picture` (patch format), `Texture`/`Flat`, `TextureLibrary`, `SpriteLibrary`, `ConsoleShades` (256 → 16 colours with dithering) |
| `Maps/` | `Map` and its geometry types, `MapLoader`, `MapNames` (map order) |
| `Play/` | `Level` (split into movement and map collision, combat, monster rules, the player and specials), `Player`, `ThingTypes`/`ThingInfo`, `Animator`, `Weapons`, `LineActions`, `SectorMover` |
| `Components/` | The ECS components of a thing: `Thing` (kind, flags, state, health), `Body` (position, footprint, sector), `Momentum`, `Animation`, `Monster`, `Missile`, `Explosive`, `Collectible` |
| `Systems/` | One tic of the things: `PickupSystem`, `MonsterSystem`, `MissileSystem`, `DyingSystem`, `StaticThingSystem`. `Level.Tick` runs them after the player |
| `Rendering/` | `IViewRenderer` (`ViewMode`), `WorldRenderer` (BSP), `WeaponRenderer`, `Automap`, `Hud`, `Presenter`, `FrameBuffer`, `Camera` |
| `Rendering/Meshes/` | The 3D mesh view: `MeshRenderer`, `LevelMeshes` (sector meshes), `SubsectorPolygons` (floor shapes from the BSP), `ConvexPolygon`, `BlockColors` (texture mosaics), `SpriteMeshes`, `MeshBuilder`, `PaletteQuantizer` (24-bit → palette) |
| `Scenes/` | `TitleScene`, `LevelScene`, `IntermissionScene`, `MissingWadScene` |

Tests (`Examples/Doom.Tests`) build a tiny WAD in memory (`Fakes/TestWad`) with two rooms and a door, and play it tic
by tic. No real game data is needed.
