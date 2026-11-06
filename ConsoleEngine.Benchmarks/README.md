# ConsoleEngine.Benchmarks

BenchmarkDotNet micro-benchmarks for the engine: `Baseline/` (OOP vs. struct arrays), `Ecs/` (world, queries,
command buffer, rendering) and `Scripting/` (Lua). Always Release:

```bash
dotnet run -c Release --project ConsoleEngine.Benchmarks -- --filter "*"
```

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
