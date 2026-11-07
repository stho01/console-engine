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
    SensorB
}

/// <summary>
/// The pairs of the last two steps: contacts (touching or speculative) and touching sensor overlaps. A step uses the
/// previous step's entry of a pair to tell whether it began or ended touching, and to warm start the solver with the
/// pair's impulses. Keyed by the two entities, never by body index: indices change whenever the chunk layout does.
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

    private Entry[] _current = new Entry[MinCapacity];
    private Entry[] _previous = new Entry[MinCapacity];
    private int _previousCount;

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>Pairs added in the current step.</summary>
    public int Count { get; private set; }

    /// <summary>Pairs added in the current step that touch.</summary>
    public int TouchingCount { get; private set; }

    //**********************************************************
    //** public methods
    //**********************************************************

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
    /// Adds a pair to this step. <paramref name="a"/> must have the lower id, and each pair may be added once per step,
    /// at most as many as <see cref="BeginStep"/> allowed. Returns the pair's slot for <see cref="StoreImpulses"/>, and
    /// what the previous step knew about it (all zero/false if it wasn't there).
    /// </summary>
    public int Add(Entity a, Entity b, PairKind kind, bool touching, out bool wasTouching, out float normalImpulse,
        out float tangentImpulse)
    {
        wasTouching = false;
        normalImpulse = 0f;
        tangentImpulse = 0f;

        if (_previousCount > 0)
        {
            var previousMask = _previous.Length - 1;
            for (var slot = Hash(a, b) & previousMask; ; slot = (slot + 1) & previousMask)
            {
                ref var entry = ref _previous[slot];
                if (entry.IsEmpty)
                    break;

                if (entry.A == a && entry.B == b)
                {
                    entry.Seen = true;
                    wasTouching = entry.Touching;
                    normalImpulse = entry.NormalImpulse;
                    tangentImpulse = entry.TangentImpulse;
                    break;
                }
            }
        }

        Count++;
        if (touching)
            TouchingCount++;

        var mask = _current.Length - 1;
        var free = Hash(a, b) & mask;
        while (!_current[free].IsEmpty)
            free = (free + 1) & mask;

        _current[free] = new Entry(a, b, kind, touching);
        return free;
    }

    /// <summary>Keeps the solver's impulses of a pair for warm starting the next step.</summary>
    public void StoreImpulses(int slot, float normalImpulse, float tangentImpulse)
    {
        ref var entry = ref _current[slot];
        entry.NormalImpulse = normalImpulse;
        entry.TangentImpulse = tangentImpulse;
    }

    /// <summary>
    /// Adds an end event for every pair that touched in the previous step and wasn't added in this one (moved apart
    /// beyond contact range, filtered out, or an entity destroyed).
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
                default:
                    sensorsEnded.Add(new SensorEvent(entry.B, entry.A));
                    break;
            }
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static int Hash(Entity a, Entity b) =>
        (int)(((uint)a.Id * 0x9E3779B1u) ^ ((uint)b.Id * 0x85EBCA77u) ^ (uint)(a.Version + b.Version));

    internal struct Entry(Entity a, Entity b, PairKind kind, bool touching)
    {
        public readonly Entity A = a;
        public readonly Entity B = b;
        public readonly PairKind Kind = kind;
        public readonly bool Touching = touching;
        public bool Seen;
        public float NormalImpulse;
        public float TangentImpulse;

        /// <summary>Versions start at 1, so a zeroed slot is empty.</summary>
        public readonly bool IsEmpty => A.Version == 0;
    }
}
