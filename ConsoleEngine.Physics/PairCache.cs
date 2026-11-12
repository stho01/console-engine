using System;
using ConsoleEngine.Ecs;

namespace ConsoleEngine.Physics;

/// <summary>What kind of overlap a cached pair is.</summary>
internal enum PairKind : byte
{
    Contact,

    /// <summary><see cref="PairCache.Entry.A"/> is the sensor.</summary>
    SensorA,

    /// <summary><see cref="PairCache.Entry.B"/> is the sensor.</summary>
    SensorB,

    /// <summary>A body against a tile of the <see cref="TileGrid"/>. Kept for warm starting only; no events.</summary>
    Tile
}

/// <summary>The impulses of a contact's points from the previous step, by manifold point id.</summary>
internal struct CachedImpulses
{
    public int Count;
    public int Id0;
    public float Normal0;
    public float Tangent0;
    public int Id1;
    public float Normal1;
    public float Tangent1;

    /// <summary>The impulses of the point with <paramref name="id"/>, or zero.</summary>
    public readonly void Find(int id, out float normal, out float tangent)
    {
        if (Count > 0 && Id0 == id)
        {
            normal = Normal0;
            tangent = Tangent0;
        }
        else if (Count > 1 && Id1 == id)
        {
            normal = Normal1;
            tangent = Tangent1;
        }
        else
        {
            normal = 0f;
            tangent = 0f;
        }
    }
}

/// <summary>
/// The pairs of the last two steps: contacts (touching or speculative, against bodies or tiles) and touching sensor
/// overlaps. A step uses the previous step's entry of a pair to tell whether it began or ended touching, and to warm
/// start the solver with the pair's impulses. Keyed by entity (id and version) or tile index, never by body index:
/// indices change whenever the chunk layout does.
/// <para>
/// Two open-addressing hash tables with linear probing, swapped every step. The step adds its pairs to the current
/// table and marks them in the previous one; whatever is left unmarked in the previous table is gone. That needs no
/// deletion, and the tables only grow.
/// </para>
/// </summary>
internal sealed class PairCache
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int MinCapacity = 64;
    private const ulong TileFlag = 1UL << 63;

    private Entry[] _current = new Entry[MinCapacity];
    private Entry[] _previous = new Entry[MinCapacity];
    private int _previousCount;

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>Pairs added in the current step.</summary>
    public int Count { get; private set; }

    /// <summary>Pairs added in the current step that touch, tiles not counted.</summary>
    public int TouchingCount { get; private set; }

    //**********************************************************
    //** public methods
    //**********************************************************

    public static ulong KeyOf(Entity entity) => (ulong)(uint)entity.Id << 32 | (uint)entity.Version;

    public static ulong KeyOfTile(int tileIndex) => TileFlag | (uint)tileIndex;

    /// <summary>
    /// Starts a step: this step's pairs become the previous ones, and the current table is emptied and made big enough
    /// for <paramref name="maxPairs"/>, so slots returned by <see cref="Add"/> stay valid for the whole step.
    /// </summary>
    public void BeginStep(int maxPairs)
    {
        (_current, _previous) = (_previous, _current);
        _previousCount = Count;
        Count = 0;
        TouchingCount = 0;

        var capacity = _current.Length;
        while (capacity < maxPairs * 2)
            capacity *= 2;

        if (capacity == _current.Length)
            Array.Clear(_current);
        else
            _current = new Entry[capacity];
    }

    /// <summary>
    /// Adds a pair to this step. Each pair may be added once per step, at most as many as <see cref="BeginStep"/>
    /// allowed; for an entity pair, <paramref name="a"/> must have the lower id. For a tile pair, <paramref name="b"/>
    /// is <see cref="Entity.Null"/> and <paramref name="keyB"/> comes from <see cref="KeyOfTile"/>. Returns the pair's
    /// slot for <see cref="StoreImpulses"/>, and what the previous step knew about it (all zero/false if it wasn't
    /// there).
    /// </summary>
    public int Add(Entity a, Entity b, ulong keyB, PairKind kind, bool touching, out bool wasTouching,
        out CachedImpulses impulses)
    {
        wasTouching = false;
        impulses = default;
        var keyA = KeyOf(a);
        var hash = Hash(keyA, keyB);

        if (_previousCount > 0)
        {
            var previousMask = _previous.Length - 1;
            for (var slot = hash & previousMask; ; slot = (slot + 1) & previousMask)
            {
                ref var entry = ref _previous[slot];
                if (entry.IsEmpty)
                    break;

                if (entry.KeyA == keyA && entry.KeyB == keyB)
                {
                    entry.Seen = true;
                    wasTouching = entry.Touching;
                    impulses = entry.Impulses;
                    break;
                }
            }
        }

        Count++;
        if (touching && kind != PairKind.Tile)
            TouchingCount++;

        var mask = _current.Length - 1;
        var free = hash & mask;
        while (!_current[free].IsEmpty)
            free = (free + 1) & mask;

        _current[free] = new Entry(keyA, keyB, a, b, kind, touching);
        return free;
    }

    /// <summary>
    /// Looks the pair up in the previous step and marks it seen, so it doesn't end. Returns its slot there for
    /// <see cref="CopyPrevious"/>, or -1. Used for pairs of resting bodies, which can't have changed.
    /// </summary>
    public int FindPrevious(Entity a, ulong keyB)
    {
        if (_previousCount == 0)
            return -1;

        var keyA = KeyOf(a);
        var mask = _previous.Length - 1;
        for (var slot = Hash(keyA, keyB) & mask; ; slot = (slot + 1) & mask)
        {
            ref var entry = ref _previous[slot];
            if (entry.IsEmpty)
                return -1;

            if (entry.KeyA == keyA && entry.KeyB == keyB)
            {
                entry.Seen = true;
                return slot;
            }
        }
    }

    /// <summary>Carries a previous-step entry (from <see cref="FindPrevious"/>) into this step unchanged.</summary>
    public void CopyPrevious(int previousSlot)
    {
        ref readonly var entry = ref _previous[previousSlot];
        Count++;
        if (entry.Touching && entry.Kind != PairKind.Tile)
            TouchingCount++;

        var mask = _current.Length - 1;
        var free = Hash(entry.KeyA, entry.KeyB) & mask;
        while (!_current[free].IsEmpty)
            free = (free + 1) & mask;

        _current[free] = entry;
        _current[free].Seen = false;
    }

    /// <summary>Keeps the solver's impulses of a contact for warm starting the next step.</summary>
    public void StoreImpulses(int slot, in ContactConstraint contact)
    {
        ref var impulses = ref _current[slot].Impulses;
        impulses.Count = contact.PointCount;
        impulses.Id0 = contact.Point0.Id;
        impulses.Normal0 = contact.Point0.NormalImpulse;
        impulses.Tangent0 = contact.Point0.TangentImpulse;
        impulses.Id1 = contact.Point1.Id;
        impulses.Normal1 = contact.Point1.NormalImpulse;
        impulses.Tangent1 = contact.Point1.TangentImpulse;
    }

    /// <summary>
    /// Adds an end event for every body or sensor pair that touched in the previous step and wasn't added in this one
    /// (moved apart beyond contact range, filtered out, or an entity destroyed).
    /// </summary>
    public void CollectEnded(ArrayBuffer<ContactEvent> contactsEnded, ArrayBuffer<SensorEvent> sensorsEnded)
    {
        if (_previousCount == 0)
            return;

        for (var i = 0; i < _previous.Length; i++)
        {
            ref readonly var entry = ref _previous[i];
            if (entry.IsEmpty || entry.Seen || !entry.Touching)
                continue;

            switch (entry.Kind)
            {
                case PairKind.Contact:
                    contactsEnded.Add(new ContactEvent(entry.A, entry.B));
                    break;
                case PairKind.SensorA:
                    sensorsEnded.Add(new SensorEvent(entry.A, entry.B));
                    break;
                case PairKind.SensorB:
                    sensorsEnded.Add(new SensorEvent(entry.B, entry.A));
                    break;
            }
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static int Hash(ulong keyA, ulong keyB)
    {
        var hash = keyA * 0x9E3779B97F4A7C15UL ^ keyB * 0xC2B2AE3D27D4EB4FUL;
        return (int)(hash ^ hash >> 32) & int.MaxValue;
    }

    internal struct Entry(ulong keyA, ulong keyB, Entity a, Entity b, PairKind kind, bool touching)
    {
        public readonly ulong KeyA = keyA;
        public readonly ulong KeyB = keyB;
        public readonly Entity A = a;
        public readonly Entity B = b;
        public readonly PairKind Kind = kind;
        public readonly bool Touching = touching;
        public bool Seen;
        public CachedImpulses Impulses;

        /// <summary>Versions start at 1, so a zeroed slot (key 0) is empty.</summary>
        public readonly bool IsEmpty => KeyA == 0UL;
    }
}
