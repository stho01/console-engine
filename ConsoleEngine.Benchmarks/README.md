# ConsoleEngine.Benchmarks

BenchmarkDotNet micro-benchmarks for the engine: `Baseline/` (OOP vs. struct arrays), `Ecs/` (world, queries,
command buffer, rendering), `Scripting/` (Lua) and `Rendering3D/` (CPU vs. GPU renderer). Always Release, and
in-process (`-i`): BenchmarkDotNet 0.15.8 does not recognise .NET 11 and stops with
`GetRuntimeVersion not implemented for NotRecognized` without it:

```bash
dotnet run -c Release --project ConsoleEngine.Benchmarks -- -i --filter "*"
```

## Rendering3D/RendererBenchmarks

One 3D frame (`Clear`, 13 `Draw`s, `Finish`) on the CPU `Rasterizer` and on the OpenGL `GlRenderer`: a chequered
floor and twelve spheres whose triangles share vertices, 20 000 or 240 000 triangles in all, at 320×180 (Kartster's
size) and 1280×720, in palette mode and in 24-bit colour. `Gpu` prints the adapter it ran on
(`// GlRenderer on …`). On a laptop with two GPUs it runs on the discrete one; set `BENCHMARK_INTEGRATED_GPU=1` to
measure the integrated one.

```bash
dotnet run -c Release --project ConsoleEngine.Benchmarks -- -i --filter "*RendererBenchmarks*" --job short
```

### Results (2026-11-15)

i7-11800H, Intel UHD Graphics (driver 32.0.101.7088) and NVIDIA GeForce RTX 3050 Ti Laptop GPU (driver 610.74),
Windows 11, .NET 11.0.100, BenchmarkDotNet 0.15.8, short job, in-process. Mean ms per frame, 0 B allocated in every
row. "Before" is the `GlRenderer` of 2026-11-14 (three vertices per triangle, rows flipped on the CPU, always the
integrated GPU).

| Size, colours, triangles | CPU `Rasterizer` | GPU before (Intel UHD) | GPU now, Intel UHD | GPU now, RTX 3050 Ti |
|---|---:|---:|---:|---:|
| 320×180, palette, 20 000 | 1.20 | 0.68 | 0.44 | 0.09 |
| 320×180, palette, 240 000 | 6.36 | 1.85 | 1.12 | 0.20 |
| 320×180, 24-bit, 20 000 | 1.32 | 0.61 | 0.63 | 0.25 |
| 320×180, 24-bit, 240 000 | 6.70 | 1.67 | 1.46 | 0.38 |
| 1280×720, palette, 20 000 | 8.98 | 2.91 | 2.35 | 1.11 |
| 1280×720, palette, 240 000 | 16.41 | 5.75 | 3.81 | 1.11 |
| 1280×720, 24-bit, 20 000 | 9.53 | 5.12 | 4.12 | 2.41 |
| 1280×720, 24-bit, 240 000 | 16.14 | 7.56 | 4.76 | 2.66 |

Notes:
- Where a GPU frame's time goes (Kartster's six-kart race frame, 320×180, split with `glFinish`): the draw calls
  themselves are 0.02–0.1 ms for 40–170 draws, so batching or caching uniforms would gain nothing. The rest is the
  GPU working through the vertices and the read-back.
- Which GPU matters most. The driver put OpenGL on the integrated GPU; `GlRenderer.PreferDiscreteGpu` moves it to the
  NVIDIA one, 3–6× faster a frame.
- Indexed vertex buffers (about one vertex per triangle instead of three) cut the vertex work: Kartster's frame with
  six full-detail karts (229 000 triangles) went from 3.4 to 1.2 ms on the integrated GPU.
- At 1280×720 the read-back and, in 24-bit, the background upload in `Clear` are what is left: 0.6–0.9 ms and
  0.9 ms of the RTX frame. Not worth more work at console sizes.
- The short job's error bars are wide (often ±30 %): read the table for the ratios, not the last digit.

## Scripting/LuaScriptBenchmarks

One frame (simulation + presentation) of 100 or 1000 moving entities with `Position`, `Velocity` and `Glyph` on a
200 × 200 console, dt 1/60 s, deterministic arrow-key pattern. `GlyphRenderSystem` draws them in both:

| Method | Steering |
|---|---|
| `CSharpSystem` (baseline) | `MoverSystem`, a chunk loop in C# |
| `LuaScripts` | `Scripting/mover.lua` through `LuaScriptSystem`: one trampoline call per entity copies the seven component fields into `self`, runs `on_update`, and returns them |

### Results (2026-11-06)

i7-11800H, Windows 11, .NET 10.0.14, BenchmarkDotNet 0.15.8, default job. Mean per frame, 0 B allocated in every
row.

| | 100 entities | 1000 entities |
|---|---:|---:|
| C# system | 0.5 µs | 4.2 µs |
| Lua, `LuaScriptSystem` (current) | 52 µs | 539 µs |
| Lua, first ECS trampoline with `rawset`/`rawget` | 102 µs | 1,014 µs |

History of the same mover logic in earlier designs (2026-11-03, removed, same machine):

| Design | 100 | 1000 |
|---|---:|---:|
| Script owns the object and draws itself (`on_update` + `on_render` → `console.draw`) | 43 µs | 420 µs |
| `GameObject` + `LuaBehaviour`, ~50 native calls per object to copy fields | 61 µs | 616 µs |

Notes:
- A Lua entity costs about 0.5 µs per frame, ~100× its C# equivalent. 1000 scripted entities take ~0.5 ms of a
  16.7 ms (60 FPS) frame. Use C# systems for anything numerous (particles, bullets) and Lua for unique behaviour.
- The trampoline uses plain field access. `rawset`/`rawget` are C function calls from Lua and doubled the cost.
- The current design syncs seven fields from three components per hook. The first design synced none (the script
  held all state), which is why it was cheaper per object but gave the engine no typed data.

## Why KeraLua (decision record, 2026-11-03)

Before choosing, the same game-object and pathfinding scripts ran on four libraries (code removed after the
decision). Mean per frame with 1000 objects, one `on_update` + `on_render` call per object:

| Library | Frame, 1000 objects | Pathfinding (72 × 50 BFS) | Managed allocations per frame |
|---|---:|---:|---:|
| **KeraLua** (native Lua 5.4, raw stack API) | **391 µs** | **562 µs** | 0 B |
| Lua-CSharp 0.5.8 (C# interpreter, Lua 5.2) | 985 µs | 2,056 µs | 0 B |
| NLua 1.7.9 (native Lua 5.4, reflection API) | 1,980 µs | 564 µs | 1.5 MB |
| MoonSharp 2.0.0 (C# interpreter, Lua 5.2) | 2,573 µs | 12,030 µs | 5.6 MB |

KeraLua was fastest in every scenario with no managed allocations. NLua runs the same native Lua but its
reflection-based calls were 5× slower per crossing.
