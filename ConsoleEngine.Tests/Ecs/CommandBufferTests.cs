using System;
using ConsoleEngine.Ecs;
using NUnit.Framework;

namespace ConsoleEngine.Tests.Ecs;

public class CommandBufferTests
{
    [Test]
    public void Destroy_BeforePlayback_EntityStaysAlive()
    {
        using var world = new World();
        var entity = world.Create(new Health(1));
        var commands = new CommandBuffer();

        commands.Destroy(entity);

        Assert.Multiple(() =>
        {
            Assert.That(world.IsAlive(entity), Is.True);
            Assert.That(commands.Count, Is.EqualTo(1));
        });
    }

    [Test]
    public void Playback_Destroy_DestroysEntityAndClearsBuffer()
    {
        using var world = new World();
        var entity = world.Create(new Health(1));
        var commands = new CommandBuffer();
        commands.Destroy(entity);

        commands.Playback(world);

        Assert.Multiple(() =>
        {
            Assert.That(world.IsAlive(entity), Is.False);
            Assert.That(commands.Count, Is.Zero);
        });
    }

    [Test]
    public void Playback_SameEntityDestroyedTwice_SkipsSecondDestroy()
    {
        using var world = new World();
        var entity = world.Create();
        var commands = new CommandBuffer();
        commands.Destroy(entity);
        commands.Destroy(entity);

        Assert.That(() => commands.Playback(world), Throws.Nothing);
    }

    [Test]
    public void Playback_AddAndRemove_AppliesInRecordingOrder()
    {
        using var world = new World();
        var entity = world.Create(new Location(1, 2));
        var commands = new CommandBuffer();
        commands.Add(entity, new Health(7));
        commands.Add<Frozen>(entity);
        commands.Remove<Location>(entity);

        commands.Playback(world);

        Assert.Multiple(() =>
        {
            Assert.That(world.Get<Health>(entity).Value, Is.EqualTo(7));
            Assert.That(world.Has<Frozen>(entity), Is.True);
            Assert.That(world.Has<Location>(entity), Is.False);
        });
    }

    [Test]
    public void Playback_AddForEntityDestroyedEarlierInBuffer_SkipsAdd()
    {
        using var world = new World();
        var entity = world.Create();
        var commands = new CommandBuffer();
        commands.Destroy(entity);
        commands.Add(entity, new Health(1));
        commands.Remove<Health>(entity);

        Assert.That(() => commands.Playback(world), Throws.Nothing);
    }

    [Test]
    public void Playback_Create_CreatesEntityWithEveryComponentValue()
    {
        using var world = new World();
        var commands = new CommandBuffer();
        commands.Create(new Location(1, 2), new Speed(3, 4), new Health(5), new Frozen(), new Lifetime(6), new Mass(7));

        commands.Playback(world);

        var query = world.Query(Query.With<Location, Speed, Health>().With<Frozen>());
        var entity = Entity.Null;
        foreach (var chunk in query.GetChunks())
            entity = chunk.Entities[0];

        Assert.Multiple(() =>
        {
            Assert.That(query.Count, Is.EqualTo(1));
            Assert.That(world.Get<Location>(entity).Y, Is.EqualTo(2));
            Assert.That(world.Get<Speed>(entity).X, Is.EqualTo(3));
            Assert.That(world.Get<Health>(entity).Value, Is.EqualTo(5));
            Assert.That(world.Get<Lifetime>(entity).Seconds, Is.EqualTo(6));
            Assert.That(world.Get<Mass>(entity).Value, Is.EqualTo(7));
        });
    }

    [Test]
    public void Create_SameComponentTwice_ThrowsWhenRecorded()
    {
        var commands = new CommandBuffer();

        Assert.Multiple(() =>
        {
            Assert.That(() => commands.Create(new Health(1), new Health(2)),
                Throws.ArgumentException.With.Message.EqualTo("Component type Health is listed more than once."));
            Assert.That(commands.Count, Is.Zero);
        });
    }

    [Test]
    public void Playback_ManyCommands_GrowsBufferAndAppliesAll()
    {
        using var world = new World();
        var commands = new CommandBuffer(initialCapacity: 8);

        for (var i = 0; i < 1_000; i++)
            commands.Create(new Health(i), new Location(i, i));
        commands.Playback(world);

        Assert.That(world.Count, Is.EqualTo(1_000));
    }

    [Test]
    public void RecordAndPlayback_SteadyState_AllocatesNothing()
    {
        using var world = new World();
        var commands = new CommandBuffer();
        var entities = new Entity[500];
        world.Create(entities, new Location());
        var created = world.Query(Query.With<Location, Health>());
        CycleCommands(world, commands, entities, created); // warm-up: grows the buffer, creates archetypes and edges

        var before = GC.GetAllocatedBytesForCurrentThread();
        CycleCommands(world, commands, entities, created);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(allocated, Is.Zero);
    }

    [Test]
    public void Playback_DuringQueryIteration_Throws()
    {
        using var world = new World();
        world.Create(new Health(1));
        var query = world.Query(Query.With<Health>());
        var commands = new CommandBuffer();
        commands.Create(new Health(2));

        Assert.That(() =>
        {
            foreach (var _ in query.GetChunks())
                commands.Playback(world);
        }, Throws.InvalidOperationException);
    }

    private static void CycleCommands(World world, CommandBuffer commands, Entity[] entities, Query created)
    {
        for (var i = 0; i < entities.Length; i++)
            commands.Add(entities[i], new Health(i));
        commands.Playback(world);

        for (var i = 0; i < entities.Length; i++)
            commands.Remove<Health>(entities[i]);
        for (var i = 0; i < 100; i++)
            commands.Create(new Location(i, i), new Health(i));
        commands.Playback(world);

        foreach (var chunk in created.GetChunks())
        {
            foreach (var entity in chunk.Entities)
                commands.Destroy(entity);
        }

        commands.Playback(world);
    }
}
