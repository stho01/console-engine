using System;
using System.Collections.Generic;
using ConsoleEngine.Ecs;
using NUnit.Framework;

namespace ConsoleEngine.Tests.Ecs;

public class WorldTests
{
    //**********************************************************
    //** create / destroy
    //**********************************************************

    [Test]
    public void Create_NoComponents_ReturnsAliveEntity()
    {
        using var world = new World();

        var entity = world.Create();

        Assert.Multiple(() =>
        {
            Assert.That(world.IsAlive(entity), Is.True);
            Assert.That(world.Count, Is.EqualTo(1));
            Assert.That(entity, Is.Not.EqualTo(Entity.Null));
        });
    }

    [Test]
    public void IsAlive_NullEntity_ReturnsFalse()
    {
        using var world = new World();
        world.Create();

        Assert.That(world.IsAlive(Entity.Null), Is.False);
    }

    [Test]
    public void Create_WithComponents_StoresEachValue()
    {
        using var world = new World();

        var entity = world.Create(new Location(1, 2), new Speed(3, 4), new Health(5), new Frozen());

        Assert.Multiple(() =>
        {
            Assert.That(world.Get<Location>(entity).Y, Is.EqualTo(2));
            Assert.That(world.Get<Speed>(entity).X, Is.EqualTo(3));
            Assert.That(world.Get<Health>(entity).Value, Is.EqualTo(5));
            Assert.That(world.Has<Frozen>(entity), Is.True);
        });
    }

    [Test]
    public void Create_SixComponents_StoresEachValue()
    {
        using var world = new World();
        var entities = new Entity[2];

        var single = world.Create(new Location(1, 0), new Speed(2, 0), new Health(3), new Frozen(),
            new Lifetime(4), new Mass(5));
        world.Create(entities, new Location(1, 0), new Speed(2, 0), new Health(3), new Frozen(),
            new Lifetime(4), new Mass(5));

        Assert.Multiple(() =>
        {
            foreach (var entity in new[] { single, entities[0], entities[1] })
            {
                Assert.That(world.Get<Location>(entity).X, Is.EqualTo(1));
                Assert.That(world.Get<Speed>(entity).X, Is.EqualTo(2));
                Assert.That(world.Get<Health>(entity).Value, Is.EqualTo(3));
                Assert.That(world.Has<Frozen>(entity), Is.True);
                Assert.That(world.Get<Lifetime>(entity).Seconds, Is.EqualTo(4));
                Assert.That(world.Get<Mass>(entity).Value, Is.EqualTo(5));
            }
        });
    }

    [Test]
    public void Create_SameComponentTwice_Throws()
    {
        using var world = new World();

        Assert.That(() => world.Create(new Health(1), new Health(2)),
            Throws.ArgumentException.With.Message.EqualTo("Component type Health is listed more than once."));
    }

    [Test]
    public void Create_SameComponentSetInAnyOrder_SharesArchetype()
    {
        using var world = new World();

        var first = world.Create(new Location(), new Speed());
        var second = world.Create(new Speed(), new Location());

        Assert.That(world.GetArchetype(first), Is.SameAs(world.GetArchetype(second)));
    }

    [Test]
    public void CreateBulk_MoreThanOneChunk_CreatesAliveEntitiesWithValues()
    {
        using var world = new World(initialCapacity: 1);
        var entities = new Entity[5_000];

        world.Create(entities, new Location(7, 8), new Health(9));

        Assert.Multiple(() =>
        {
            Assert.That(world.Count, Is.EqualTo(entities.Length));
            Assert.That(new HashSet<Entity>(entities), Has.Count.EqualTo(entities.Length));
            Assert.That(world.GetArchetype(entities[0]).ChunkCount, Is.GreaterThan(1));
            for (var i = 0; i < entities.Length; i++)
            {
                Assert.That(world.Get<Location>(entities[i]).X, Is.EqualTo(7));
                Assert.That(world.Get<Health>(entities[i]).Value, Is.EqualTo(9));
            }
        });
    }

    [Test]
    public void Destroy_AliveEntity_IsNoLongerAlive()
    {
        using var world = new World();
        var entity = world.Create(new Health(1));

        world.Destroy(entity);

        Assert.Multiple(() =>
        {
            Assert.That(world.IsAlive(entity), Is.False);
            Assert.That(world.Count, Is.Zero);
        });
    }

    [Test]
    public void Destroy_ThenCreate_ReusesIdWithNewVersion()
    {
        using var world = new World();
        var old = world.Create();
        world.Destroy(old);

        var reused = world.Create();

        Assert.Multiple(() =>
        {
            Assert.That(reused.Id, Is.EqualTo(old.Id));
            Assert.That(reused.Version, Is.EqualTo(old.Version + 1));
            Assert.That(world.IsAlive(old), Is.False);
            Assert.That(world.IsAlive(reused), Is.True);
        });
    }

    [Test]
    public void Destroy_StaleHandle_Throws()
    {
        using var world = new World();
        var entity = world.Create();
        world.Destroy(entity);

        Assert.That(() => world.Destroy(entity),
            Throws.InvalidOperationException.With.Message.EqualTo($"{entity} is not alive."));
    }

    [Test]
    public void Destroy_FirstOfMany_KeepsOtherEntitiesData()
    {
        using var world = new World();
        var first = world.Create(new Health(1));
        var second = world.Create(new Health(2));
        var last = world.Create(new Health(3));

        world.Destroy(first);

        Assert.Multiple(() =>
        {
            Assert.That(world.Get<Health>(second).Value, Is.EqualTo(2));
            Assert.That(world.Get<Health>(last).Value, Is.EqualTo(3));
        });
    }

    //**********************************************************
    //** components
    //**********************************************************

    [Test]
    public void Get_WriteThroughReference_ChangesStoredValue()
    {
        using var world = new World();
        var entity = world.Create(new Location(1, 1));

        world.Get<Location>(entity).X = 10;

        Assert.That(world.Get<Location>(entity).X, Is.EqualTo(10));
    }

    [Test]
    public void Get_MissingComponent_Throws()
    {
        using var world = new World();
        var entity = world.Create(new Location());

        Assert.That(() => world.Get<Health>(entity),
            Throws.InvalidOperationException.With.Message.EqualTo($"{entity} has no Health component."));
    }

    [Test]
    public void Get_Tag_Throws()
    {
        using var world = new World();
        var entity = world.Create(new Frozen());

        Assert.That(() => world.Get<Frozen>(entity),
            Throws.InvalidOperationException.With.Message.EqualTo("Frozen is a tag component and has no data."));
    }

    [Test]
    public void Set_ExistingComponent_OverwritesValue()
    {
        using var world = new World();
        var entity = world.Create(new Health(1));

        world.Set(entity, new Health(42));

        Assert.That(world.Get<Health>(entity).Value, Is.EqualTo(42));
    }

    [Test]
    public void Add_NewComponent_KeepsExistingDataAndStoresNewValue()
    {
        using var world = new World();
        var entity = world.Create(new Location(1, 2));

        world.Add(entity, new Speed(3, 4));

        Assert.Multiple(() =>
        {
            Assert.That(world.Get<Location>(entity).X, Is.EqualTo(1));
            Assert.That(world.Get<Location>(entity).Y, Is.EqualTo(2));
            Assert.That(world.Get<Speed>(entity).Y, Is.EqualTo(4));
        });
    }

    [Test]
    public void Add_ExistingComponent_Throws()
    {
        using var world = new World();
        var entity = world.Create(new Health(1));

        Assert.That(() => world.Add(entity, new Health(2)),
            Throws.InvalidOperationException.With.Message.EqualTo($"{entity} already has a Health component."));
    }

    [Test]
    public void Add_Tag_HasReturnsTrueAndDataIsKept()
    {
        using var world = new World();
        var entity = world.Create(new Health(5));

        world.Add<Frozen>(entity);

        Assert.Multiple(() =>
        {
            Assert.That(world.Has<Frozen>(entity), Is.True);
            Assert.That(world.Get<Health>(entity).Value, Is.EqualTo(5));
        });
    }

    [Test]
    public void Remove_Component_KeepsOtherData()
    {
        using var world = new World();
        var entity = world.Create(new Location(1, 2), new Health(3));

        world.Remove<Location>(entity);

        Assert.Multiple(() =>
        {
            Assert.That(world.Has<Location>(entity), Is.False);
            Assert.That(world.Get<Health>(entity).Value, Is.EqualTo(3));
        });
    }

    [Test]
    public void Remove_MissingComponent_Throws()
    {
        using var world = new World();
        var entity = world.Create();

        Assert.That(() => world.Remove<Health>(entity),
            Throws.InvalidOperationException.With.Message.EqualTo($"{entity} has no Health component."));
    }

    [Test]
    public void AddThenRemove_SameComponent_ReturnsToOriginalArchetype()
    {
        using var world = new World();
        var entity = world.Create(new Location());
        var original = world.GetArchetype(entity);

        world.Add<Frozen>(entity);
        world.Remove<Frozen>(entity);

        Assert.That(world.GetArchetype(entity), Is.SameAs(original));
    }

    //**********************************************************
    //** stress / memory
    //**********************************************************

    [Test]
    public void RandomOperations_ManyEntities_EveryAliveEntityKeepsItsData()
    {
        using var world = new World();
        var random = new Random(1234);
        var expected = new Dictionary<Entity, int>();

        for (var step = 0; step < 50_000; step++)
        {
            var roll = random.Next(10);
            if (roll < 5 || expected.Count == 0)
            {
                var value = random.Next();
                expected.Add(world.Create(new Health(value), new Location(value, 0)), value);
                continue;
            }

            var entity = PickAny(expected, random);
            if (roll < 7)
            {
                world.Destroy(entity);
                expected.Remove(entity);
            }
            else if (world.Has<Frozen>(entity))
            {
                world.Remove<Frozen>(entity);
            }
            else
            {
                world.Add<Frozen>(entity);
            }
        }

        Assert.That(world.Count, Is.EqualTo(expected.Count));
        foreach (var (entity, value) in expected)
        {
            Assert.That(world.Get<Health>(entity).Value, Is.EqualTo(value));
            Assert.That(world.Get<Location>(entity).X, Is.EqualTo((float)value));
        }
    }

    [Test]
    public void Create_AfterDestroyingAll_ReusesPooledChunks()
    {
        using var world = new World();
        var entities = new Entity[3_000];
        world.Create(entities, new Location(), new Speed());
        var allocated = world.ChunkPool.AllocatedCount;

        for (var i = 0; i < entities.Length; i++)
            world.Destroy(entities[i]);
        world.Create(entities, new Location(), new Speed());

        Assert.That(world.ChunkPool.AllocatedCount, Is.EqualTo(allocated));
    }

    [Test]
    public void GetSetAddRemove_SteadyState_AllocatesNothing()
    {
        using var world = new World();
        var entities = new Entity[1_000];
        world.Create(entities, new Location(), new Speed());
        CycleComponents(world, entities); // warm-up: creates archetypes, edges and pooled chunks

        var before = GC.GetAllocatedBytesForCurrentThread();
        CycleComponents(world, entities);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(allocated, Is.Zero);
    }

    [Test]
    public void Dispose_ThenUseEntity_ThrowsNotAlive()
    {
        var world = new World();
        var entity = world.Create(new Health(1));

        world.Dispose();

        Assert.Multiple(() =>
        {
            Assert.That(world.IsAlive(entity), Is.False);
            Assert.That(() => world.Get<Health>(entity), Throws.InvalidOperationException);
            Assert.That(() => world.Create(), Throws.TypeOf<ObjectDisposedException>());
        });
    }

    //**********************************************************
    //** helpers
    //**********************************************************

    private static void CycleComponents(World world, Entity[] entities)
    {
        for (var i = 0; i < entities.Length; i++)
        {
            world.Get<Location>(entities[i]).X += 1;
            world.Set(entities[i], new Speed(1, 1));
            world.Add(entities[i], new Health(i));
            world.Add<Frozen>(entities[i]);
        }

        for (var i = 0; i < entities.Length; i++)
        {
            world.Remove<Health>(entities[i]);
            world.Remove<Frozen>(entities[i]);
        }
    }

    private static Entity PickAny(Dictionary<Entity, int> entities, Random random)
    {
        var skip = random.Next(Math.Min(entities.Count, 16));
        foreach (var entity in entities.Keys)
        {
            if (skip-- == 0)
                return entity;
        }

        throw new InvalidOperationException("Dictionary is empty.");
    }
}
