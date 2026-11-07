using System;

namespace ConsoleEngine.Ecs.Physics;

/// <summary>
/// A list over one array that is reused every step: <see cref="Clear"/> keeps the array, and it only grows (by
/// doubling). Unlike <c>List&lt;T&gt;</c> it hands out spans and refs without copying.
/// </summary>
internal sealed class ArrayBuffer<T>(int capacity = 64) where T : struct
{
    //**********************************************************
    //** fields
    //**********************************************************

    private T[] _items = new T[Math.Max(capacity, 1)];

    //**********************************************************
    //** props
    //**********************************************************

    public int Count { get; private set; }
    public int Capacity => _items.Length;

    public ref T this[int index] => ref _items[index];

    //**********************************************************
    //** public methods
    //**********************************************************

    public void Add(in T item)
    {
        if (Count == _items.Length)
            Grow(Count + 1);

        _items[Count++] = item;
    }

    public void Clear() => Count = 0;

    public void EnsureCapacity(int capacity)
    {
        if (capacity > _items.Length)
            Grow(capacity);
    }

    public Span<T> AsSpan() => _items.AsSpan(0, Count);

    //**********************************************************
    //** internals
    //**********************************************************

    private void Grow(int minimum)
    {
        var capacity = _items.Length * 2;
        if (capacity < minimum)
            capacity = minimum;

        Array.Resize(ref _items, capacity);
    }
}
