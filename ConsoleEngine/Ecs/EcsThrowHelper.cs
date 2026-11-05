using System;
using System.Diagnostics.CodeAnalysis;

namespace ConsoleEngine.Ecs;

/// <summary>Throws live out of line so the checked hot paths stay small enough to inline.</summary>
internal static class EcsThrowHelper
{
    [DoesNotReturn]
    public static void ThrowNotAlive(Entity entity) =>
        throw new InvalidOperationException($"{entity} is not alive.");

    [DoesNotReturn]
    public static void ThrowMissingComponent(Entity entity, Type type) =>
        throw new InvalidOperationException($"{entity} has no {type.Name} component.");

    [DoesNotReturn]
    public static void ThrowAlreadyHasComponent(Entity entity, Type type) =>
        throw new InvalidOperationException($"{entity} already has a {type.Name} component.");

    [DoesNotReturn]
    public static void ThrowStructuralChangeDuringIteration() =>
        throw new InvalidOperationException(
            "Cannot create, destroy, add or remove while a query is being iterated. Use a CommandBuffer.");

    [DoesNotReturn]
    public static void ThrowNotInChunk(Type type) =>
        throw new InvalidOperationException(
            $"{type.Name} is not in this chunk. Add it to the query with With<{type.Name}>().");

    [DoesNotReturn]
    public static void ThrowTagHasNoData(Type type) =>
        throw new InvalidOperationException($"{type.Name} is a tag component and has no data.");

    [DoesNotReturn]
    public static void ThrowDuplicateComponent(Type type) =>
        throw new ArgumentException($"Component type {type.Name} is listed more than once.");

    [DoesNotReturn]
    public static void ThrowTooManyComponentTypes(Type type) =>
        throw new InvalidOperationException(
            $"Cannot register {type.Name}: the ECS supports at most {ComponentMask.Capacity} component types.");

    [DoesNotReturn]
    public static void ThrowRowTooLarge(int rowSize) =>
        throw new InvalidOperationException(
            $"An archetype row needs {rowSize} bytes and does not fit in one {Chunk.Size}-byte chunk.");
}
