using System;

namespace ConsoleEngine.Ecs.Physics;

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
/// The pairs that touch, kept from one step to the next so a step can tell which pairs began and which ended (and,
/// from phase 3, carry the contact impulse over for warm starting). Keyed by the two entities, never by body index:
/// indices change whenever the chunk layout does.
/// <para>
/// Two open-addressing hash tables with linear probing, swapped every step. The step inserts its pairs into the
/// current table and marks them in the previous one; whatever is left unmarked in the previous table has ended. That
/// needs no deletion, and the tables only grow.
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

    /// <summary>Pairs touching in the current step.</summary>
    public int Count { get; private set; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Starts a step: this step's pairs become the previous ones, and the current table is emptied.</summary>
    public void BeginStep()
    {
        (_current, _previous) = (_previous, _current);
        _previousCount = Count;
        Count = 0;
        Array.Clear(_current);
    }

    /// <summary>
    /// Records that the pair touches in this step. Returns true if it didn't touch in the previous step, i.e. it
    /// begins. Call at most once per pair and step. <paramref name="a"/> must have the lower id.
    /// </summary>
    public bool Touch(Entity a, Entity b, PairKind kind)
    {
        if ((Count + 1) * 2 > _current.Length)
            GrowCurrent();

        Insert(_current, new Entry(a, b, kind));
        Count++;

        if (_previousCount == 0)
            return true;

        var mask = _previous.Length - 1;
        for (var slot = Hash(a, b) & mask; ; slot = (slot + 1) & mask)
        {
            ref var entry = ref _previous[slot];
            if (entry.IsEmpty)
                return true;

            if (entry.A == a && entry.B == b)
            {
                entry.Seen = true;
                return false;
            }
        }
    }

    /// <summary>Ends the step: adds every pair of the previous step that didn't touch in this one.</summary>
    public void CollectEnded(ArrayBuffer<ContactEvent> contactsEnded, ArrayBuffer<SensorEvent> sensorsEnded)
    {
        if (_previousCount == 0)
            return;

        for (var i = 0; i < _previous.Length; i++)
        {
            ref readonly var entry = ref _previous[i];
            if (entry.IsEmpty || entry.Seen)
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

    private static void Insert(Entry[] table, in Entry entry)
    {
        var mask = table.Length - 1;
        var slot = Hash(entry.A, entry.B) & mask;
        while (!table[slot].IsEmpty)
            slot = (slot + 1) & mask;

        table[slot] = entry;
    }

    /// <summary>Doubles the current table. Only while the number of touching pairs grows past its high-water mark.</summary>
    private void GrowCurrent()
    {
        var old = _current;
        _current = new Entry[old.Length * 2];
        for (var i = 0; i < old.Length; i++)
        {
            if (!old[i].IsEmpty)
                Insert(_current, old[i]);
        }
    }

    private static int Hash(Entity a, Entity b) =>
        (int)(((uint)a.Id * 0x9E3779B1u) ^ ((uint)b.Id * 0x85EBCA77u) ^ (uint)(a.Version + b.Version));

    internal struct Entry(Entity a, Entity b, PairKind kind)
    {
        public readonly Entity A = a;
        public readonly Entity B = b;
        public readonly PairKind Kind = kind;
        public bool Seen;

        /// <summary>Versions start at 1, so a zeroed slot is empty.</summary>
        public readonly bool IsEmpty => A.Version == 0;
    }
}
