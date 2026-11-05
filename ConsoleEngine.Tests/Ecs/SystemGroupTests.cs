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

    [Test]
    public void Update_UpdateBeforeAndAfter_OverrideAddOrder()
    {
        using var world = new World();
        var group = new SystemGroup(world);
        var log = new List<string>();
        group.Add(new RenderStep(log));
        group.Add(new InputStep(log));
        group.Add(new PhysicsStep(log));

        group.Update(0f);

        Assert.That(log, Is.EqualTo(new[] { "input", "physics", "render" }));
    }

    [Test]
    public void Update_ConstraintOnTypeNotInGroup_IsIgnored()
    {
        using var world = new World();
        var group = new SystemGroup(world);
        var log = new List<string>();
        group.Add(new RecordingSystem("plain", log));
        group.Add(new PhysicsStep(log));

        group.Update(0f);

        Assert.That(log, Is.EqualTo(new[] { "plain", "physics" }));
    }

    [Test]
    public void Update_SystemAddedLater_IsSortedIn()
    {
        using var world = new World();
        var group = new SystemGroup(world);
        var log = new List<string>();
        group.Add(new PhysicsStep(log));
        group.Update(0f);
        log.Clear();

        group.Add(new InputStep(log));
        group.Update(0f);

        Assert.That(log, Is.EqualTo(new[] { "input", "physics" }));
    }

    [Test]
    public void RunOrder_CyclicConstraints_Throws()
    {
        using var world = new World();
        var group = new SystemGroup(world);
        group.Add(new CycleA());
        group.Add(new CycleB());

        Assert.That(() => group.Update(0f), Throws.InvalidOperationException.With.Message.EqualTo(
            "The UpdateBefore/UpdateAfter attributes of these systems form a cycle: CycleA, CycleB."));
    }

    [UpdateAfter(typeof(PhysicsStep))]
    private sealed class RenderStep(List<string> log) : EntitySystem
    {
        protected override void OnUpdate(float deltaSeconds) => log.Add("render");
    }

    [UpdateBefore(typeof(PhysicsStep))]
    private sealed class InputStep(List<string> log) : EntitySystem
    {
        protected override void OnUpdate(float deltaSeconds) => log.Add("input");
    }

    [UpdateAfter(typeof(InputStep))]
    private sealed class PhysicsStep(List<string> log) : EntitySystem
    {
        protected override void OnUpdate(float deltaSeconds) => log.Add("physics");
    }

    [UpdateBefore(typeof(CycleB))]
    private sealed class CycleA : EntitySystem
    {
        protected override void OnUpdate(float deltaSeconds) { }
    }

    [UpdateBefore(typeof(CycleA))]
    private sealed class CycleB : EntitySystem
    {
        protected override void OnUpdate(float deltaSeconds) { }
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
