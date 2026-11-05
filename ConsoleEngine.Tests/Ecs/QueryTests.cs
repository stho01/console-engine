using System;
using System.Collections.Generic;
using ConsoleEngine.Ecs;
using NUnit.Framework;

namespace ConsoleEngine.Tests.Ecs;

public class QueryTests
{
    [Test]
    public void Count_WithAndWithout_CountsOnlyMatchingEntities()
    {
        using var world = new World();
        world.Create(new Location(), new Speed());
        world.Create(new Location(), new Speed(), new Frozen());
        world.Create(new Location());

        var query = world.Query(Query.With<Location, Speed>().Without<Frozen>());

        Assert.That(query.Count, Is.EqualTo(1));
    }

    [Test]
    public void Without_AsStartingPoint_MatchesEveryArchetypeLackingTheComponent()
    {
        using var world = new World();
        world.Create();
        world.Create(new Health(1));
        world.Create(new Health(1), new Frozen());

        var query = world.Query(Query.Without<Frozen>());

        Assert.That(query.Count, Is.EqualTo(2));
    }

    [Test]
    public void Count_ArchetypeCreatedAfterQuery_IsIncluded()
    {
        using var world = new World();
        var query = world.Query(Query.With<Health>());

        world.Create(new Health(1));
        world.Create(new Health(2), new Location());

        Assert.That(query.Count, Is.EqualTo(2));
    }

    [Test]
    public void GetChunks_ManyChunks_VisitsEveryEntityOnce()
    {
        using var world = new World();
        var entities = new Entity[5_000];
        world.Create(entities, new Location(), new Speed());
        world.Destroy(entities[0]);
        var query = world.Query(Query.With<Location>());

        var seen = new HashSet<Entity>();
        foreach (var chunk in query.GetChunks())
        {
            foreach (var entity in chunk.Entities)
                Assert.That(seen.Add(entity), Is.True);
        }

        Assert.That(seen, Has.Count.EqualTo(entities.Length - 1));
    }

    [Test]
    public void GetChunks_EntityAndComponentSpans_LineUpByIndex()
    {
        using var world = new World();
        var expected = new Dictionary<Entity, int>();
        for (var i = 0; i < 1_000; i++)
            expected.Add(world.Create(new Health(i)), i);
        var query = world.Query(Query.With<Health>());

        foreach (var chunk in query.GetChunks())
        {
            var health = chunk.GetSpan<Health>();
            for (var i = 0; i < chunk.Count; i++)
                Assert.That(health[i].Value, Is.EqualTo(expected[chunk.Entities[i]]));
        }
    }

    [Test]
    public void GetSpan_ComponentNotInQuery_Throws()
    {
        using var world = new World();
        world.Create(new Location());
        var query = world.Query(Query.With<Location>());

        Assert.That(() =>
            {
                foreach (var chunk in query.GetChunks())
                    chunk.GetSpan<Health>();
            },
            Throws.InvalidOperationException.With.Message.EqualTo(
                "Health is not in this chunk. Add it to the query with With<Health>()."));
    }

    [Test]
    public void Has_OptionalComponent_TrueOnlyForChunksWithIt()
    {
        using var world = new World();
        world.Create(new Location());
        world.Create(new Location(), new Frozen());
        var query = world.Query(Query.With<Location>());

        var frozenChunks = 0;
        foreach (var chunk in query.GetChunks())
        {
            if (chunk.Has<Frozen>())
                frozenChunks++;
        }

        Assert.That(frozenChunks, Is.EqualTo(1));
    }

    [Test]
    public void ForEach_TwoComponents_UpdatesEveryMatchingEntity()
    {
        using var world = new World();
        var entities = new Entity[3_000];
        world.Create(entities, new Location(1, 1), new Speed(2, 3));
        var query = world.Query(Query.With<Location, Speed>());
        var job = new MoveJob { Delta = 0.5f };

        query.ForEach<MoveJob, Location, Speed>(ref job);

        Assert.Multiple(() =>
        {
            for (var i = 0; i < entities.Length; i++)
            {
                Assert.That(world.Get<Location>(entities[i]).X, Is.EqualTo(2f));
                Assert.That(world.Get<Location>(entities[i]).Y, Is.EqualTo(2.5f));
            }
        });
    }

    [Test]
    public void ForEach_JobState_IsWrittenBackThroughRef()
    {
        using var world = new World();
        world.Create(new Health(2));
        world.Create(new Health(3));
        var query = world.Query(Query.With<Health>());
        var job = new SumJob();

        query.ForEach<SumJob, Health>(ref job);

        Assert.That(job.Sum, Is.EqualTo(5));
    }

    [Test]
    public void ForEachAndGetChunks_SteadyState_AllocatesNothing()
    {
        using var world = new World();
        world.Create(new Entity[2_000], new Location(), new Speed());
        var query = world.Query(Query.With<Location, Speed>());
        var job = new MoveJob { Delta = 1 };
        query.ForEach<MoveJob, Location, Speed>(ref job); // warm-up

        var before = GC.GetAllocatedBytesForCurrentThread();
        query.ForEach<MoveJob, Location, Speed>(ref job);
        foreach (var chunk in query.GetChunks())
            chunk.GetSpan<Location>()[0].X += 1;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(allocated, Is.Zero);
    }

    [Test]
    public void GetChunks_StructuralChangeInsideLoop_Throws()
    {
        using var world = new World();
        var entity = world.Create(new Health(1));
        var query = world.Query(Query.With<Health>());
        const string message =
            "Cannot create, destroy, add or remove while a query is being iterated. Use a CommandBuffer.";

        Assert.Multiple(() =>
        {
            Assert.That(() => { foreach (var _ in query.GetChunks()) world.Destroy(entity); },
                Throws.InvalidOperationException.With.Message.EqualTo(message));
            Assert.That(() => { foreach (var _ in query.GetChunks()) world.Create(new Health(2)); },
                Throws.InvalidOperationException.With.Message.EqualTo(message));
            Assert.That(() => { foreach (var _ in query.GetChunks()) world.Add<Frozen>(entity); },
                Throws.InvalidOperationException.With.Message.EqualTo(message));
            Assert.That(() => { foreach (var _ in query.GetChunks()) world.Remove<Health>(entity); },
                Throws.InvalidOperationException.With.Message.EqualTo(message));
        });
    }

    [Test]
    public void GetChunks_LoopLeftWithBreakOrException_AllowsStructuralChangesAgain()
    {
        using var world = new World();
        world.Create(new Health(1));
        var query = world.Query(Query.With<Health>());

        foreach (var _ in query.GetChunks())
            break;
        try
        {
            foreach (var _ in query.GetChunks())
                throw new InvalidOperationException("leave the loop");
        }
        catch (InvalidOperationException)
        {
        }

        Assert.That(() => world.Create(new Health(2)), Throws.Nothing);
    }

    [Test]
    public void GetChunks_SetInsideLoop_IsAllowed()
    {
        using var world = new World();
        var entity = world.Create(new Health(1));
        var query = world.Query(Query.With<Health>());

        foreach (var _ in query.GetChunks())
            world.Set(entity, new Health(5));

        Assert.That(world.Get<Health>(entity).Value, Is.EqualTo(5));
    }

    [Test]
    public void GetChunks_AfterDispose_YieldsNothing()
    {
        var world = new World();
        world.Create(new Health(1));
        var query = world.Query(Query.With<Health>());

        world.Dispose();

        var chunks = 0;
        foreach (var _ in query.GetChunks())
            chunks++;
        Assert.That(chunks, Is.Zero);
    }

    private struct MoveJob : IForEach<Location, Speed>
    {
        public float Delta;

        public void Execute(ref Location position, ref Speed velocity)
        {
            position.X += velocity.X * Delta;
            position.Y += velocity.Y * Delta;
        }
    }

    private struct SumJob : IForEach<Health>
    {
        public int Sum;

        public void Execute(ref Health health) => Sum += health.Value;
    }
}
