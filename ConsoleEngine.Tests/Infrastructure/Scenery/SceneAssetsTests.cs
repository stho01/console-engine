using System;
using ConsoleEngine.Assets;
using ConsoleEngine.Infrastructure.Scenery;
using ConsoleEngine.Tests.Fakes;
using NUnit.Framework;

namespace ConsoleEngine.Tests.Infrastructure.Scenery;

public class SceneAssetsTests
{
    /// <summary>Loads <c>level.txt</c> into its own scope in <c>OnLoad</c>, like a level scene does.</summary>
    private sealed class LevelScene : RecordingScene
    {
        public string Level { get; private set; }

        public override void OnLoad()
        {
            base.OnLoad();
            Level = Assets.Load<string>("level.txt");
        }
    }

    private AssetManager _assets;
    private SceneManager _scenes;

    [SetUp]
    public void SetUp()
    {
        _assets = new AssetManager(new MemoryAssetSource().Add("level.txt", "L").Add("menu.txt", "M"));
        _scenes = new SceneManager(null, _assets);
    }

    [Test]
    public void Assets_SceneUnloaded_ReleasesItsAssets()
    {
        var scene = new LevelScene();
        _scenes.Push(scene);
        Assert.That(_assets.IsLoaded<string>("level.txt"), Is.True);

        _scenes.Pop();
        _scenes.Cleanup();

        Assert.That(_assets.IsLoaded<string>("level.txt"), Is.False);
    }

    [Test]
    public void Assets_SetSameLevelAgain_KeepsCachedAssetAcrossTheSwitch()
    {
        // Set loads the new scene before the old one unloads (end of frame), so a restart reads no file.
        var first = new LevelScene();
        _scenes.Push(first);

        var second = new LevelScene();
        _scenes.Set(second);
        _scenes.Cleanup();

        Assert.Multiple(() =>
        {
            Assert.That(second.Level, Is.SameAs(first.Level));
            Assert.That(_assets.IsLoaded<string>("level.txt"), Is.True);
        });
    }

    [Test]
    public void Assets_PausedSceneUnderPushedScene_KeepsItsAssets()
    {
        _scenes.Push(new LevelScene());
        var menu = new RecordingScene();
        _scenes.Push(menu);
        menu.Assets.Load<string>("menu.txt");

        _scenes.Pop();
        _scenes.Cleanup();

        Assert.Multiple(() =>
        {
            Assert.That(_assets.IsLoaded<string>("level.txt"), Is.True);
            Assert.That(_assets.IsLoaded<string>("menu.txt"), Is.False);
        });
    }

    [Test]
    public void Assets_ScenePushedAgainAfterUnload_GetsFreshScope()
    {
        var scene = new LevelScene();
        _scenes.Push(scene);
        _scenes.Pop();
        _scenes.Cleanup();

        _scenes.Push(scene);

        Assert.Multiple(() =>
        {
            Assert.That(scene.Assets.IsDisposed, Is.False);
            Assert.That(_assets.IsLoaded<string>("level.txt"), Is.True);
        });
    }

    [Test]
    public void Assets_SceneNeverPushed_Throws()
    {
        Assert.That(() => new RecordingScene().Assets, Throws.InvalidOperationException.With.Message.EqualTo(
            "Scene.Assets is available once the scene is pushed onto the SceneManager (in OnLoad and later)."));
    }

    [Test]
    public void Unload_SceneThatNeverUsedAssets_CreatesNoScope()
    {
        var scene = new RecordingScene();
        _scenes.Push(scene);

        Assert.That(() => { _scenes.Pop(); _scenes.Cleanup(); }, Throws.Nothing);
    }
}
