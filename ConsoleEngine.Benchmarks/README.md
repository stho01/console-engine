# ConsoleEngine.Benchmarks

BenchmarkDotNet micro-benchmarks for the engine. Release only:

```bash
dotnet run -c Release --project ConsoleEngine.Benchmarks -- --filter '*'
```

## GameObjectFrame

One frame (update, then render) of 100 or 1000 moving `GameObject`s on a 200 × 200 console (`AsteroidsGame` size), dt
1/60 s, deterministic arrow-key pattern (`EngineBindings`):

| Method | Behaviour |
|---|---|
| `CSharpBehaviour` (baseline) | `MoverBehaviour`, a C# `IBehaviour` |
| `LuaBehaviour` | `Scripts/mover.lua` through `LuaBehaviour`: engine fields copied into `self`, `on_update`, copied back |

The engine draws every object itself in both. The difference is what Lua scripting costs per object.

## Results (2026-11-03)

i7-11800H, Windows 11, .NET 10.0.14, BenchmarkDotNet 0.15.8, default job. Mean per frame, no managed allocations in
any row.

| Object model | 100 objects | 1000 objects |
|---|---:|---:|
| Engine `GameObject`, C# behaviour | 0.9 µs | 8.8 µs |
| Engine `GameObject`, `LuaBehaviour` (current) | 61 µs | 616 µs |
| Script owns the object and draws itself (first design, removed) | 43 µs | 420 µs |

The engine object model costs about 45 % more per Lua object than the first design. Copying the six engine fields
into `self` and back is ~50 native calls per object per frame, more than the `on_render` + `console.draw` calls it
replaced. 1000 Lua objects still take ~0.6 ms of a 16.7 ms (60 FPS) frame.

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
