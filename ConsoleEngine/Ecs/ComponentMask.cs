using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace ConsoleEngine.Ecs;

/// <summary>
/// Fixed 256-bit set of component ids: the signature of an archetype. A value type, so building and comparing
/// masks never allocates.
/// </summary>
internal struct ComponentMask : IEquatable<ComponentMask>
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int Capacity = WordCount * 64;
    private const int WordCount = 4;

    private Words _words;

    //**********************************************************
    //** props
    //**********************************************************

    public readonly int Count =>
        BitOperations.PopCount(_words[0]) + BitOperations.PopCount(_words[1]) +
        BitOperations.PopCount(_words[2]) + BitOperations.PopCount(_words[3]);

    //**********************************************************
    //** public methods
    //**********************************************************

    public void Add(int id) => _words[id >> 6] |= 1UL << id;

    public void Remove(int id) => _words[id >> 6] &= ~(1UL << id);

    public readonly bool Contains(int id) => (_words[id >> 6] & (1UL << id)) != 0;

    public readonly bool ContainsAll(in ComponentMask other)
    {
        for (var i = 0; i < WordCount; i++)
        {
            if ((_words[i] & other._words[i]) != other._words[i])
                return false;
        }

        return true;
    }

    public readonly bool ContainsAny(in ComponentMask other)
    {
        for (var i = 0; i < WordCount; i++)
        {
            if ((_words[i] & other._words[i]) != 0)
                return true;
        }

        return false;
    }

    public readonly ComponentMask With(int id)
    {
        var mask = this;
        mask.Add(id);
        return mask;
    }

    public readonly ComponentMask Without(int id)
    {
        var mask = this;
        mask.Remove(id);
        return mask;
    }

    /// <summary>The ids in the mask, ascending. Allocates, so only call it when creating an archetype.</summary>
    public readonly int[] ToIds()
    {
        var ids = new int[Count];
        var next = 0;
        for (var word = 0; word < WordCount; word++)
        {
            var bits = _words[word];
            while (bits != 0)
            {
                ids[next++] = word * 64 + BitOperations.TrailingZeroCount(bits);
                bits &= bits - 1;
            }
        }

        return ids;
    }

    public readonly bool Equals(ComponentMask other) =>
        _words[0] == other._words[0] && _words[1] == other._words[1] &&
        _words[2] == other._words[2] && _words[3] == other._words[3];

    public override readonly bool Equals(object obj) => obj is ComponentMask other && Equals(other);
    public override readonly int GetHashCode() => HashCode.Combine(_words[0], _words[1], _words[2], _words[3]);

    //**********************************************************
    //** internals
    //**********************************************************

    [InlineArray(WordCount)]
    private struct Words
    {
        private ulong _element;
    }
}
