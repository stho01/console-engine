using System;
using ConsoleEngine.Ecs;
using NUnit.Framework;

namespace ConsoleEngine.Tests.Ecs;

public class ArchetypeTests
{
    [Test]
    public void Layout_PositionAndVelocity_ColumnsAreCacheLineAlignedAndFitInChunk()
    {
        using var world = new World();
        var archetype = world.GetArchetype(world.Create(new Position(), new Velocity()));

        Assert.Multiple(() =>
        {
            Assert.That(archetype.ColumnOffsets, Has.All.Matches<int>(offset => offset % Chunk.CacheLineSize == 0));
            Assert.That(archetype.ColumnOffsets[0], Is.GreaterThanOrEqualTo(archetype.ChunkCapacity * 8));
            Assert.That(archetype.ColumnOffsets[^1] + archetype.ChunkCapacity * 8, Is.LessThanOrEqualTo(Chunk.Size));
        });
    }

    [Test]
    public void Layout_PositionAndVelocity_UsesAlmostAllOfTheChunk()
    {
        using var world = new World();
        var archetype = world.GetArchetype(world.Create(new Position(), new Velocity()));

        // Entity (8) + Position (8) + Velocity (8) = 24 bytes per row. Only alignment padding may be lost.
        Assert.That(archetype.ChunkCapacity, Is.GreaterThan((Chunk.Size - 2 * Chunk.CacheLineSize) / 24));
    }

    [Test]
    public void Layout_TagOnly_HasNoColumns()
    {
        using var world = new World();
        var entity = world.Create();
        world.Add<Frozen>(entity);

        var archetype = world.GetArchetype(entity);

        Assert.Multiple(() =>
        {
            Assert.That(archetype.ColumnIds, Is.Empty);
            Assert.That(archetype.ChunkCapacity, Is.EqualTo(Chunk.Size / 8));
        });
    }

    [Test]
    public void Create_RowLargerThanChunk_Throws()
    {
        using var world = new World();

        Assert.That(() => world.Create(new Huge()),
            Throws.InvalidOperationException.With.Message.Contains("does not fit in one 16384-byte chunk"));
    }

    [Test]
    public void ColumnIds_MixedComponentsAndTag_AreAscendingAndExcludeTag()
    {
        using var world = new World();
        var entity = world.Create(new Velocity(), new Position(), new Frozen());

        var ids = world.GetArchetype(entity).ColumnIds;

        Assert.Multiple(() =>
        {
            Assert.That(ids, Has.Length.EqualTo(2));
            Assert.That(ids, Is.Ordered);
            Assert.That(Array.IndexOf(ids, ComponentType<Frozen>.Id), Is.EqualTo(-1));
        });
    }
}
