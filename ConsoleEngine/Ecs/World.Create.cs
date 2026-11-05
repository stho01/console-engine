using System;

namespace ConsoleEngine.Ecs;

// Create overloads for 1-6 components. Each has a single-entity form and a bulk form that fills whole chunk ranges
// at once. Keep the overloads identical apart from their arity.
public sealed partial class World
{
    //**********************************************************
    //** single
    //**********************************************************

    public Entity Create<T1>(in T1 c1) where T1 : unmanaged
    {
        var archetype = GetArchetype([ComponentType<T1>.Id]);
        var entity = CreateIn(archetype, out var chunkIndex, out var row);
        ref var chunk = ref archetype.GetChunk(chunkIndex);
        Write(archetype, ref chunk, row, c1);
        return entity;
    }

    public Entity Create<T1, T2>(in T1 c1, in T2 c2) where T1 : unmanaged where T2 : unmanaged
    {
        var archetype = GetArchetype([ComponentType<T1>.Id, ComponentType<T2>.Id]);
        var entity = CreateIn(archetype, out var chunkIndex, out var row);
        ref var chunk = ref archetype.GetChunk(chunkIndex);
        Write(archetype, ref chunk, row, c1);
        Write(archetype, ref chunk, row, c2);
        return entity;
    }

    public Entity Create<T1, T2, T3>(in T1 c1, in T2 c2, in T3 c3)
        where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged
    {
        var archetype = GetArchetype([ComponentType<T1>.Id, ComponentType<T2>.Id, ComponentType<T3>.Id]);
        var entity = CreateIn(archetype, out var chunkIndex, out var row);
        ref var chunk = ref archetype.GetChunk(chunkIndex);
        Write(archetype, ref chunk, row, c1);
        Write(archetype, ref chunk, row, c2);
        Write(archetype, ref chunk, row, c3);
        return entity;
    }

    public Entity Create<T1, T2, T3, T4>(in T1 c1, in T2 c2, in T3 c3, in T4 c4)
        where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged
    {
        var archetype = GetArchetype(
            [ComponentType<T1>.Id, ComponentType<T2>.Id, ComponentType<T3>.Id, ComponentType<T4>.Id]);
        var entity = CreateIn(archetype, out var chunkIndex, out var row);
        ref var chunk = ref archetype.GetChunk(chunkIndex);
        Write(archetype, ref chunk, row, c1);
        Write(archetype, ref chunk, row, c2);
        Write(archetype, ref chunk, row, c3);
        Write(archetype, ref chunk, row, c4);
        return entity;
    }

    public Entity Create<T1, T2, T3, T4, T5>(in T1 c1, in T2 c2, in T3 c3, in T4 c4, in T5 c5)
        where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged
    {
        var archetype = GetArchetype(
        [
            ComponentType<T1>.Id, ComponentType<T2>.Id, ComponentType<T3>.Id, ComponentType<T4>.Id,
            ComponentType<T5>.Id
        ]);
        var entity = CreateIn(archetype, out var chunkIndex, out var row);
        ref var chunk = ref archetype.GetChunk(chunkIndex);
        Write(archetype, ref chunk, row, c1);
        Write(archetype, ref chunk, row, c2);
        Write(archetype, ref chunk, row, c3);
        Write(archetype, ref chunk, row, c4);
        Write(archetype, ref chunk, row, c5);
        return entity;
    }

    public Entity Create<T1, T2, T3, T4, T5, T6>(in T1 c1, in T2 c2, in T3 c3, in T4 c4, in T5 c5, in T6 c6)
        where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged
        where T6 : unmanaged
    {
        var archetype = GetArchetype(
        [
            ComponentType<T1>.Id, ComponentType<T2>.Id, ComponentType<T3>.Id, ComponentType<T4>.Id,
            ComponentType<T5>.Id, ComponentType<T6>.Id
        ]);
        var entity = CreateIn(archetype, out var chunkIndex, out var row);
        ref var chunk = ref archetype.GetChunk(chunkIndex);
        Write(archetype, ref chunk, row, c1);
        Write(archetype, ref chunk, row, c2);
        Write(archetype, ref chunk, row, c3);
        Write(archetype, ref chunk, row, c4);
        Write(archetype, ref chunk, row, c5);
        Write(archetype, ref chunk, row, c6);
        return entity;
    }

    //**********************************************************
    //** bulk
    //**********************************************************

    /// <summary>Creates <c>entities.Length</c> entities with the same component values and writes their handles.</summary>
    public void Create<T1>(Span<Entity> entities, in T1 c1) where T1 : unmanaged
    {
        var archetype = GetArchetype([ComponentType<T1>.Id]);
        for (var done = 0; done < entities.Length;)
        {
            var count = CreateRange(archetype, entities[done..], out var chunkIndex, out var firstRow);
            ref var chunk = ref archetype.GetChunk(chunkIndex);
            Fill(archetype, ref chunk, firstRow, count, c1);
            done += count;
        }
    }

    /// <inheritdoc cref="Create{T1}(Span{Entity}, in T1)"/>
    public void Create<T1, T2>(Span<Entity> entities, in T1 c1, in T2 c2) where T1 : unmanaged where T2 : unmanaged
    {
        var archetype = GetArchetype([ComponentType<T1>.Id, ComponentType<T2>.Id]);
        for (var done = 0; done < entities.Length;)
        {
            var count = CreateRange(archetype, entities[done..], out var chunkIndex, out var firstRow);
            ref var chunk = ref archetype.GetChunk(chunkIndex);
            Fill(archetype, ref chunk, firstRow, count, c1);
            Fill(archetype, ref chunk, firstRow, count, c2);
            done += count;
        }
    }

    /// <inheritdoc cref="Create{T1}(Span{Entity}, in T1)"/>
    public void Create<T1, T2, T3>(Span<Entity> entities, in T1 c1, in T2 c2, in T3 c3)
        where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged
    {
        var archetype = GetArchetype([ComponentType<T1>.Id, ComponentType<T2>.Id, ComponentType<T3>.Id]);
        for (var done = 0; done < entities.Length;)
        {
            var count = CreateRange(archetype, entities[done..], out var chunkIndex, out var firstRow);
            ref var chunk = ref archetype.GetChunk(chunkIndex);
            Fill(archetype, ref chunk, firstRow, count, c1);
            Fill(archetype, ref chunk, firstRow, count, c2);
            Fill(archetype, ref chunk, firstRow, count, c3);
            done += count;
        }
    }

    /// <inheritdoc cref="Create{T1}(Span{Entity}, in T1)"/>
    public void Create<T1, T2, T3, T4>(Span<Entity> entities, in T1 c1, in T2 c2, in T3 c3, in T4 c4)
        where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged
    {
        var archetype = GetArchetype(
            [ComponentType<T1>.Id, ComponentType<T2>.Id, ComponentType<T3>.Id, ComponentType<T4>.Id]);
        for (var done = 0; done < entities.Length;)
        {
            var count = CreateRange(archetype, entities[done..], out var chunkIndex, out var firstRow);
            ref var chunk = ref archetype.GetChunk(chunkIndex);
            Fill(archetype, ref chunk, firstRow, count, c1);
            Fill(archetype, ref chunk, firstRow, count, c2);
            Fill(archetype, ref chunk, firstRow, count, c3);
            Fill(archetype, ref chunk, firstRow, count, c4);
            done += count;
        }
    }

    /// <inheritdoc cref="Create{T1}(Span{Entity}, in T1)"/>
    public void Create<T1, T2, T3, T4, T5>(Span<Entity> entities, in T1 c1, in T2 c2, in T3 c3, in T4 c4, in T5 c5)
        where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged
    {
        var archetype = GetArchetype(
        [
            ComponentType<T1>.Id, ComponentType<T2>.Id, ComponentType<T3>.Id, ComponentType<T4>.Id,
            ComponentType<T5>.Id
        ]);
        for (var done = 0; done < entities.Length;)
        {
            var count = CreateRange(archetype, entities[done..], out var chunkIndex, out var firstRow);
            ref var chunk = ref archetype.GetChunk(chunkIndex);
            Fill(archetype, ref chunk, firstRow, count, c1);
            Fill(archetype, ref chunk, firstRow, count, c2);
            Fill(archetype, ref chunk, firstRow, count, c3);
            Fill(archetype, ref chunk, firstRow, count, c4);
            Fill(archetype, ref chunk, firstRow, count, c5);
            done += count;
        }
    }

    /// <inheritdoc cref="Create{T1}(Span{Entity}, in T1)"/>
    public void Create<T1, T2, T3, T4, T5, T6>(
        Span<Entity> entities, in T1 c1, in T2 c2, in T3 c3, in T4 c4, in T5 c5, in T6 c6)
        where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged
        where T6 : unmanaged
    {
        var archetype = GetArchetype(
        [
            ComponentType<T1>.Id, ComponentType<T2>.Id, ComponentType<T3>.Id, ComponentType<T4>.Id,
            ComponentType<T5>.Id, ComponentType<T6>.Id
        ]);
        for (var done = 0; done < entities.Length;)
        {
            var count = CreateRange(archetype, entities[done..], out var chunkIndex, out var firstRow);
            ref var chunk = ref archetype.GetChunk(chunkIndex);
            Fill(archetype, ref chunk, firstRow, count, c1);
            Fill(archetype, ref chunk, firstRow, count, c2);
            Fill(archetype, ref chunk, firstRow, count, c3);
            Fill(archetype, ref chunk, firstRow, count, c4);
            Fill(archetype, ref chunk, firstRow, count, c5);
            Fill(archetype, ref chunk, firstRow, count, c6);
            done += count;
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private Archetype GetArchetype(ReadOnlySpan<int> componentIds)
    {
        ThrowIfStructuralChangeNotAllowed();

        var mask = default(ComponentMask);
        for (var i = 0; i < componentIds.Length; i++)
        {
            if (mask.Contains(componentIds[i]))
                EcsThrowHelper.ThrowDuplicateComponent(ComponentRegistry.TypeOf(componentIds[i]));

            mask.Add(componentIds[i]);
        }

        return GetOrCreateArchetype(mask);
    }

    /// <summary>
    /// Fills the archetype's last chunk with as many new entities as fit (at most <c>entities.Length</c>), writes
    /// their handles and returns how many it created. Component data is left for the caller.
    /// </summary>
    private unsafe int CreateRange(Archetype archetype, Span<Entity> entities, out int chunkIndex, out int firstRow)
    {
        var count = archetype.AllocateRange(entities.Length, _chunkPool, out chunkIndex, out firstRow);
        var chunkEntities = archetype.GetChunk(chunkIndex).Entities;
        for (var i = 0; i < count; i++)
        {
            var entity = NewEntity();
            var row = firstRow + i;
            chunkEntities[row] = entity;
            _records[entity.Id] = new EntityRecord(entity.Version, archetype.Index, chunkIndex, row);
            entities[i] = entity;
        }

        return count;
    }
}
