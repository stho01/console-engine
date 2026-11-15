# Doom (console)

Plays the levels of a Doom WAD in the console: a 160 × 100 pixel 3D view drawn as half blocks (`▀`), dithered to
the console's 16 colours, with a text status bar below.

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

Pick **Doom (needs DOOM1.WAD)** in the launcher, or on Windows run `pwsh -File run-game.ps1 Doom`. The 6 × 12 font
makes the half-block pixels square. Windows Terminal ignores the font, so use conhost (the launcher and
`run-game.ps1` do).

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
| `Rendering/` | `WorldRenderer`, `WeaponRenderer`, `Automap`, `Hud`, `Presenter`, `FrameBuffer`, `Camera` |
| `Scenes/` | `TitleScene`, `LevelScene`, `IntermissionScene`, `MissingWadScene` |

Tests (`Examples/Doom.Tests`) build a tiny WAD in memory (`Fakes/TestWad`) with two rooms and a door, and play it tic
by tic. No real game data is needed.
