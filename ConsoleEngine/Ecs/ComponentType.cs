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
    /// <summary>
    /// A struct without instance fields and of the size the runtime gives empty structs (1 byte). Tags live only in the
    /// archetype mask and have no column. A field-less struct with an explicit <c>StructLayout.Size</c> is not a tag.
    /// </summary>
    internal static readonly bool IsTag =
        Unsafe.SizeOf<T>() == 1 &&
        typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Length == 0;

    /// <summary>Bytes per row in a chunk column. 0 for tags.</summary>
    internal static readonly int Size = IsTag ? 0 : Unsafe.SizeOf<T>();

    internal static readonly int Id = ComponentRegistry.Register(typeof(T), Size);
}

/// <summary>
/// Process-wide table of component types, indexed by component id. A type gets an id the first time any ECS API names
/// it (<c>Create</c>, <c>Add</c>, <c>Has</c>, <c>Get</c>, <c>Query.With</c>…), and keeps it for the life of the process.
/// </summary>
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
            _count = id + 1;
            return id;
        }
    }
}
