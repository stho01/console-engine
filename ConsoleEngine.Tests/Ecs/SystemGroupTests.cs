using System.Collections.Generic;
using ConsoleEngine.Ecs;
using NUnit.Framework;

namespace ConsoleEngine.Tests.Ecs;

public class SystemGroupTests
{
    [Test]
    public void Add_System_CallsOnCreateWithWorldAndCommands()
    {
        using var world = new World();
        var group = new SystemGroup(world);

        var system = group.Add(new RecordingSystem("a", new List<string>()));

        Assert.Multiple(() =>
        {
            Assert.That(system.CreatedWith, Is.SameAs(world));
            Assert.That(system.CommandsAtCreate, Is.SameAs(group.Commands));
            Assert.That(group.Count, Is.EqualTo(1));
        });
    }

    [Test]
    public void Update_SeveralSystems_RunsThemInAddOrderWithDelta()
    {
        using var world = new World();
        var group = new SystemGroup(world);
        var log = new List<string>();
        var first = group.Add(new RecordingSystem("first", log));
        group.Add(new RecordingSystem("second", log));

        group.Update(0.25f);

        Assert.Multiple(() =>
        {
            Assert.That(log, Is.EqualTo(new[] { "first", "second" }));
            Assert.That(first.LastDelta, Is.EqualTo(0.25f));
        });
    }

    [Test]
    public void Update_DisabledSystem_IsSkipped()
    {
        using var world = new World();
        var group = new SystemGroup(world);
        var log = new List<string>();
        group.Add(new RecordingSystem("off", log)).Enabled = false;
        group.Add(new RecordingSystem("on", log));

        group.Update(0f);

        Assert.That(log, Is.EqualTo(new[] { "on" }));
    }

    [Test]
    public void Update_SystemRecordsCreate_EntityAppearsAfterGroupNotDuringIt()
    {
        using var world = new World();
        var group = new SystemGroup(world);
        group.Add(new SpawnSystem());
        var counter = group.Add(new CountSystem());

        group.Update(0f);

        Assert.Multiple(() =>
        {
            Assert.That(counter.LastSeen, Is.Zero);
            Assert.That(world.Count, Is.EqualTo(1));
        });
    }

    private sealed class RecordingSystem(string name, List<string> log) : EntitySystem
    {
        public World CreatedWith { get; private set; }
        public CommandBuffer CommandsAtCreate { get; private set; }
        public float LastDelta { get; private set; }

        protected override void OnCreate()
        {
            CreatedWith = World;
            CommandsAtCreate = Commands;
        }

        protected override void OnUpdate(float deltaSeconds)
        {
            LastDelta = deltaSeconds;
            log.Add(name);
        }
    }

    private sealed class SpawnSystem : EntitySystem
    {
        protected override void OnUpdate(float deltaSeconds) => Commands.Create(new Health(1));
    }

    private sealed class CountSystem : EntitySystem
    {
        private Query _health;

        public int LastSeen { get; private set; }

        protected override void OnCreate() => _health = World.Query(Query.With<Health>());
        protected override void OnUpdate(float deltaSeconds) => LastSeen = _health.Count;
    }
}
