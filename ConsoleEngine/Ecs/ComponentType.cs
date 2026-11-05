using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ConsoleEngine.Ecs;

/// <summary>
/// Per-type component metadata. The id is assigned on first use and is a constant in JIT-compiled generic code,
/// so looking it up in a hot loop costs nothing.
/// </summary>
internal static class ComponentType<T> where T : unmanaged
{
    /// <summary>A struct without instance fields. Tags live only in the archetype mask and have no column.</summary>
    internal static readonly bool IsTag =
        typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Length == 0;

    /// <summary>Bytes per row in a chunk column. 0 for tags.</summary>
    internal static readonly int Size = IsTag ? 0 : Unsafe.SizeOf<T>();

    internal static readonly int Id = ComponentRegistry.Register(typeof(T), Size);
}

/// <summary>Process-wide table of component types, indexed by component id.</summary>
internal static class ComponentRegistry
{
    //**********************************************************
    //** fields
    //**********************************************************

    private static readonly Lock _lock = new();
    private static readonly Type[] _types = new Type[ComponentMask.Capacity];
    private static readonly int[] _sizes = new int[ComponentMask.Capacity];
    private static int _count;

    //**********************************************************
    //** methods
    //**********************************************************

    internal static Type TypeOf(int id) => _types[id];

    /// <summary>Bytes per row in a chunk column. 0 for tags.</summary>
    internal static int SizeOf(int id) => _sizes[id];

    internal static int Register(Type type, int size)
    {
        lock (_lock)
        {
            if (_count == ComponentMask.Capacity)
                EcsThrowHelper.ThrowTooManyComponentTypes(type);

            var id = _count;
            _types[id] = type;
            _sizes[id] = size;
            Volatile.Write(ref _count, id + 1);
            return id;
        }
    }
}
