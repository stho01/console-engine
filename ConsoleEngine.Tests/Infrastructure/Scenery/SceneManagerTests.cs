using ConsoleEngine.Infrastructure.Scenery;
using ConsoleEngine.Tests.Fakes;
using NUnit.Framework;

namespace ConsoleEngine.Tests.Infrastructure.Scenery;

public class SceneManagerTests
{
    private SceneManager _scenes;

    // SceneManager only hands the game on to Scene.Game, so no GameBase (and no Win32) is needed.
    [SetUp]
    public void SetUp() => _scenes = new SceneManager(null);

    [Test]
    public void Push_EmptyStack_LoadsSceneAndMakesItCurrent()
    {
        var scene = new RecordingScene();

        _scenes.Push(scene);

        Assert.Multiple(() =>
        {
            Assert.That(_scenes.Current, Is.SameAs(scene));
            Assert.That(_scenes.Count, Is.EqualTo(1));
            Assert.That(scene.Loaded, Is.EqualTo(1));
            Assert.That(scene.IsPaused, Is.False);
        });
    }

    [Test]
    public void PushGeneric_EmptyStack_CreatesAndLoadsScene()
    {
        var scene = _scenes.Push<RecordingScene>();

        Assert.Multiple(() =>
        {
            Assert.That(_scenes.Current, Is.SameAs(scene));
            Assert.That(scene.Loaded, Is.EqualTo(1));
        });
    }

    [Test]
    public void Push_OverCurrentScene_PausesPreviousScene()
    {
        var first = new RecordingScene();
        var second = new RecordingScene();

        _scenes.Push(first);
        _scenes.Push(second);

        Assert.Multiple(() =>
        {
            Assert.That(_scenes.Current, Is.SameAs(second));
            Assert.That(_scenes.PreviousScene, Is.SameAs(first));
            Assert.That(first.IsPaused, Is.True);
            Assert.That(second.IsPaused, Is.False);
        });
    }

    [Test]
    public void PreviousScene_SingleScene_ReturnsNullAndKeepsCurrent()
    {
        var scene = new RecordingScene();
        _scenes.Push(scene);

        Assert.Multiple(() =>
        {
            Assert.That(_scenes.PreviousScene, Is.Null);
            Assert.That(_scenes.Current, Is.SameAs(scene));
        });
    }

    [Test]
    public void Pop_EmptyStack_ReturnsNull()
    {
        Assert.That(_scenes.Pop(), Is.Null);
    }

    [Test]
    public void Pop_TwoScenes_ReturnsTopAndUnpausesPrevious()
    {
        var first = new RecordingScene();
        var second = new RecordingScene();
        _scenes.Push(first);
        _scenes.Push(second);

        var popped = _scenes.Pop();

        Assert.Multiple(() =>
        {
            Assert.That(popped, Is.SameAs(second));
            Assert.That(_scenes.Current, Is.SameAs(first));
            Assert.That(first.IsPaused, Is.False);
        });
    }

    [Test]
    public void Pop_BeforeCleanup_DoesNotUnloadYet()
    {
        var scene = new RecordingScene();
        _scenes.Push(scene);

        _scenes.Pop();
        Assert.That(scene.Unloaded, Is.Zero);

        _scenes.Cleanup();
        Assert.That(scene.Unloaded, Is.EqualTo(1));
    }

    [Test]
    public void Set_WithScenesOnStack_ReplacesStackAndUnloadsOldScenesOnCleanup()
    {
        var first = new RecordingScene();
        var second = new RecordingScene();
        var replacement = new RecordingScene();
        _scenes.Push(first);
        _scenes.Push(second);

        _scenes.Set(replacement);
        _scenes.Cleanup();

        Assert.Multiple(() =>
        {
            Assert.That(_scenes.Count, Is.EqualTo(1));
            Assert.That(_scenes.Current, Is.SameAs(replacement));
            Assert.That(replacement.Loaded, Is.EqualTo(1));
            Assert.That(first.Unloaded, Is.EqualTo(1));
            Assert.That(second.Unloaded, Is.EqualTo(1));
        });
    }

    [Test]
    public void SetGeneric_EmptyStack_CreatesAndLoadsScene()
    {
        var scene = _scenes.Set<RecordingScene>();

        Assert.Multiple(() =>
        {
            Assert.That(_scenes.Current, Is.SameAs(scene));
            Assert.That(scene.Loaded, Is.EqualTo(1));
        });
    }

    [Test]
    public void Cleanup_Twice_UnloadsEachSceneOnce()
    {
        var scene = new RecordingScene();
        _scenes.Push(scene);
        _scenes.Pop();

        _scenes.Cleanup();
        _scenes.Cleanup();

        Assert.That(scene.Unloaded, Is.EqualTo(1));
    }

    [Test]
    public void UpdateAndRender_TwoScenes_RunOnlyCurrentScene()
    {
        var first = new RecordingScene();
        var second = new RecordingScene();
        _scenes.Push(first);
        _scenes.Push(second);

        _scenes.Update();
        _scenes.Render();

        Assert.Multiple(() =>
        {
            Assert.That(second.Updated, Is.EqualTo(1));
            Assert.That(second.Rendered, Is.EqualTo(1));
            Assert.That(first.Updated, Is.Zero);
            Assert.That(first.Rendered, Is.Zero);
        });
    }

    [Test]
    public void UpdateAndRender_PausedCurrentScene_SkipsHooks()
    {
        var scene = new RecordingScene();
        _scenes.Push(scene);
        scene.Pause();

        _scenes.Update();
        _scenes.Render();

        Assert.Multiple(() =>
        {
            Assert.That(scene.Updated, Is.Zero);
            Assert.That(scene.Rendered, Is.Zero);
        });
    }

    [Test]
    public void UpdateAndRender_EmptyStack_DoNothing()
    {
        Assert.DoesNotThrow(() =>
        {
            _scenes.Update();
            _scenes.Render();
            _scenes.Cleanup();
        });
    }
}
