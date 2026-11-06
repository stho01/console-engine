namespace ConsoleEngine.Ecs;

/// <summary>
/// A 16 KiB block of native memory holding up to <see cref="Archetype.ChunkCapacity"/> rows of one archetype,
/// stored column by column (SoA). Column 0 is the <see cref="Entity"/> of each row. Every column starts on a
/// cache-line boundary. Rows 0..Count-1 are always dense.
/// </summary>
internal unsafe struct Chunk
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Fits in L1d next to other data, like Unity DOTS chunks. Benchmark before changing.</summary>
    public const int Size = 16 * 1024;

    public const int CacheLineSize = 64;

    public byte* Memory;
    public int Count;

    //**********************************************************
    //** props
    //**********************************************************

    public readonly Entity* Entities => (Entity*)Memory;
}
