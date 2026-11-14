# KartsterExport

Exports the original Kartster's built-in tracks (the MonoGame game, `C:\Forse\kartster-server`, not in this repo)
into the files `Examples/Kartster` loads from `Assets/Tracks/`. Each file is made by the original's own code
(its OBJ exporters, scenery placement and meshes, race routes and grid), so the port shows the same track.

It is not in `ConsoleEngine.sln` and CI does not build it: it references the original's `Kartster.csproj`.

## Build and run

Build the original first, in its folder:

```bash
dotnet build src/Kartster/Kartster.csproj
```

Then, from this repo's root:

```bash
dotnet build tools/KartsterExport
```

```bash
dotnet run --project tools/KartsterExport -- list
```

```bash
dotnet run --project tools/KartsterExport -- export Examples/Kartster/Assets/Tracks star-circuit prism-loop
```

The shipped tracks with height were exported with
`export Examples/Kartster/Assets/Tracks norbergring dunsfold-neon kinnekulle brands-hatch-indy`.

The original is looked for in `C:\Forse\kartster-server`. Somewhere else: add `-p:KartsterRoot=<folder>` to
`dotnet build`/`dotnet run`.

`list` prints the built-in track ids. `export <outDir> <trackId...>` writes one file per exporter and track, LF line
endings, and prints each file's size. A track without some file (an open route has no race markers) gets a
`skipped:` line instead. Lines the original prints itself (for example `Power-up row 11 found no clear road …` on
`prism-loop`) come from its own code and can be ignored.

## Files

| File | Exporter | What it is | Read by |
|---|---|---|---|
| `<id>.ribbon.obj` | `RibbonExporter` | road surface and edge walls (`TrackRibbonObjExporter`) | `Track.Load` |
| `<id>.spline.obj` | `SplineExporter` | centerline, a point per metre, one polyline per section (`TrackSplineObjExporter`). The route's sections come first; other branches follow after a jump | `Track.Load` |
| `<id>.scenery.txt` | `SceneryExporter` | fences, pipes, trees, signs, start/finish line and gate, grid marks, textures baked into flat colours. Format in `Examples/Kartster/Scenery.cs` | `Scenery.Load` |
| `<id>.race.txt` | `RaceExporter` | laps, start/finish, checkpoints and the 8-slot grid of the route `RaceSimulation.SelectRoute` races. Closed routes only. Format in `Examples/Kartster/Race/RaceMarkers.cs` | `RaceMarkers.Load` |
| `<id>.collision.txt` | `CollisionExporter` (+ `RoadDecks`) | what karts hit, as 2D segments and circles: outer wall and fences (`TrackGround`), boundaries (`TrackBoundaryMesh`), road-edge walls of sections with `WallHeight` > 0, start gate poles (`StartGateMesh`), pipes (`TrackPipes`), and the scenery `LevelLoader.LoadCollisionMesh` keeps (in `TrackGraph.Corridor` or `CorridorReachMeters` past it; all of it without a corridor): tree trunks and lamp posts (`TrackTreeMesh`/`TrackLampMesh.AddColliders`) as circles, building walls (`TrackBuilding.Footprint` edges) as 0.5 m thick segments, street and bridge pillars (`TrackStreetMesh.BuildPillars`) as squares. Height is dropped, so what a kart passes over or under is left out (`DeckClearance` 1 m; roads = the `TrackExportFrameBuilder` frames' edges): a collider with a road more than 1 m over its foot (bridge pillars), one whose foot is more than 1 m over the lowest road or terrain there (Kinnekulle's footbridge, built as buildings), and edge walls and building walls, tested every frame / metre, where they pass over a lower road or under a deck. `# left out:` lines count them. Skipped (`not exported yet: …`) for a track with route ends, roadblocks, medians, guardrails, tunnel ceilings, street drops or direction signs. Format in `Examples/Kartster/Collision/TrackCollision.cs` | `TrackCollision.Load` |
| `<id>.terrain.txt` | `TerrainExporter` | the original's hilly ground (`TrackTerrain`) as it is: origin, cell size, columns, rows, a colour for its `TrackGroundStyle`, then a line of heights (cm) per row, `NaN` where there is no ground. Skipped for a track with flat `TrackGround` or none (the port draws its chequer). Format in `Examples/Kartster/Terrain.cs` | `Track.Load` (`Track.Terrain`) |
| `<id>.powerups.txt` | `PowerUpExporter` | the power-up pads a normal race puts out, `pad x y z setId` (`TrackPowerUpPadResolver`, `IsExtra` pads left out as `RaceSimulation.ResolvePowerUpPads` does). Format in `Examples/Kartster/PowerUps/PowerUpPads.cs` | `Track.Load` (`Track.PowerUpPads`) |

`model` lines in a scenery file name an OBJ in `Examples/Kartster/Assets/Scenery/`. Copy any model a new track
needs from the original's `src/Kartster/Content/3D/Rendered/Scenery/` (LF line endings), or `Scenery.Load` throws
`model file … not found`.

## Adding an exporter

Write one class that implements `ITrackExporter` (a file suffix and the file's text for a track) and add it to
`Program.Exporters`. Throw `NotSupportedException` for a track that has no such file.
