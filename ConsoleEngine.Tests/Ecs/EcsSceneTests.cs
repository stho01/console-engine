using ConsoleEngine.Ecs;
using ConsoleEngine.Infrastructure.Scenery;
using NUnit.Framework;

namespace ConsoleEngine.Tests.Ecs;

public class EcsSceneTests
{
    // SceneManager only hands the game on to Scene.Game, so no GameBase (and no Win32) is needed.
    private readonly SceneManager _scenes = new(null);

    [Test]
    public void Load_Always_CreatesWorldAndCallsOnCreate()
    {
        var scene = new TestScene();

        _scenes.Push(scene);

        Assert.Multiple(() =>
        {
            Assert.That(scene.WorldAtCreate, Is.Not.Null);
            Assert.That(scene.WorldAtCreate.IsAlive(scene.Seed), Is.True);
        });
    }

    [Test]
    public void UpdateAndRender_Loaded_RunSimulationThenPresentation()
    {
        var scene = new TestScene();
        _scenes.Push(scene);

        _scenes.Update();
        _scenes.Render();

        Assert.Multiple(() =>
        {
            Assert.That(scene.SimulationRuns, Is.EqualTo(1));
            Assert.That(scene.PresentationRuns, Is.EqualTo(1));
        });
    }

    [Test]
    public void Unload_Always_CallsOnDestroyThenDisposesWorld()
    {
        var scene = new TestScene();
        _scenes.Push(scene);

        _scenes.Pop();
        _scenes.Cleanup();

        Assert.Multiple(() =>
        {
            Assert.That(scene.WorldAliveAtDestroy, Is.True);
            Assert.That(scene.WorldAtCreate.IsAlive(scene.Seed), Is.False);
        });
    }

    private sealed class TestScene : EcsScene<GameBase>
    {
        private CountingSystem _simulation;
        private CountingSystem _presentation;

        public World WorldAtCreate { get; private set; }
        public Entity Seed { get; private set; }
        public bool WorldAliveAtDestroy { get; private set; }
        public int SimulationRuns => _simulation.Runs;
        public int PresentationRuns => _presentation.Runs;

        protected override void OnCreate()
        {
            WorldAtCreate = World;
            Seed = World.Create(new Health(1));
            _simulation = Simulation.Add(new CountingSystem());
            _presentation = Presentation.Add(new CountingSystem());
        }

        protected override void OnDestroy() => WorldAliveAtDestroy = World.IsAlive(Seed);
    }

    private sealed class CountingSystem : EntitySystem
    {
        public int Runs { get; private set; }

        protected override void OnUpdate(float deltaSeconds) => Runs++;
    }
}
