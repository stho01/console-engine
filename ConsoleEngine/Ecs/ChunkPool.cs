using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ConsoleEngine.Ecs;

/// <summary>
/// Hands out cache-line aligned chunk memory and keeps freed chunks for reuse, so spawn/despawn waves don't call
/// the native allocator. Owned by one <see cref="World"/>.
/// </summary>
internal sealed unsafe class ChunkPool : IDisposable
{
    //**********************************************************
    //** fields
    //**********************************************************

    private readonly Stack<nint> _free = new();

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>Number of chunks taken from the native allocator so far (pooled or in use).</summary>
    public int AllocatedCount { get; private set; }

    //**********************************************************
    //** public methods
    //**********************************************************

    public byte* Rent()
    {
        if (_free.TryPop(out var memory))
            return (byte*)memory;

        AllocatedCount++;
        return (byte*)NativeMemory.AlignedAlloc(Chunk.Size, Chunk.CacheLineSize);
    }

    public void Return(byte* memory) => _free.Push((nint)memory);

    /// <summary>Frees all pooled chunks. Chunks still in use are freed by their archetype.</summary>
    public void Dispose()
    {
        while (_free.TryPop(out var memory))
            NativeMemory.AlignedFree((void*)memory);
    }
}
