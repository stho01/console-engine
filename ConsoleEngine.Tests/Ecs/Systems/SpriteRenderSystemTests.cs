using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using ConsoleEngine.Ecs.Systems;
using ConsoleEngine.Infrastructure.Rendering;
using ConsoleEngine.Tests.Fakes;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace ConsoleEngine.Tests.Ecs.Systems;

public class SpriteRenderSystemTests
{
    private World _world;
    private RenderConsole _console;
    private Camera _camera;
    private SpriteLibrary _sprites;
    private int _box;
    private int _dot;

    [SetUp]
    public void SetUp()
    {
        _world = new World();
        _console = new RenderConsole(new FakeConsoleHandler(20, 10));
        _camera = new Camera();
        _sprites = new SpriteLibrary();
        _box = _sprites.Add(Sprite.FromStringArray(["ab", "cd"]));
        _dot = _sprites.Add(Sprite.FromStringArray(["x"]));
    }

    [TearDown]
    public void TearDown() => _world.Dispose();

    [Test]
    public void Add_TwoSprites_HandsOutIdsInOrder()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_box, Is.Zero);
            Assert.That(_dot, Is.EqualTo(1));
            Assert.That(_sprites.Count, Is.EqualTo(2));
        });
    }

    [Test]
    public void Update_SpriteOnScreen_DrawsTopLeftAtFlooredPositionPlusOffset()
    {
        _world.Create(new Position(new Vector2(5.9f, 3.2f)), new SpriteRef(_box, new Point(-1, 1)));
        var presentation = CreatePresentation(new SpriteRenderSystem(_console, _camera, _sprites));

        presentation.Update(0f);

        Assert.Multiple(() =>
        {
            Assert.That(_console.GetCharAt(4, 4), Is.EqualTo('a'));
            Assert.That(_console.GetCharAt(5, 4), Is.EqualTo('b'));
            Assert.That(_console.GetCharAt(4, 5), Is.EqualTo('c'));
            Assert.That(_console.GetCharAt(5, 5), Is.EqualTo('d'));
        });
    }

    [Test]
    public void Update_CameraMovedAndSpritePartlyOffScreen_DrawsVisiblePart()
    {
        _world.Create(new Position(new Vector2(9, 9)), new SpriteRef(_box));
        _camera.Position = new Vector2(10, 10);
        var presentation = CreatePresentation(new SpriteRenderSystem(_console, _camera, _sprites));

        presentation.Update(0f);

        Assert.That(_console.GetCharAt(0, 0), Is.EqualTo('d'));
    }

    [Test]
    public void Update_TwoLayers_LaterLayerIsDrawnOnTop()
    {
        _world.Create(new Position(new Vector2(2, 2)), new SpriteRef(_box), new Frozen());
        _world.Create(new Position(new Vector2(2, 2)), new SpriteRef(_dot));
        var presentation = CreatePresentation(
            new SpriteRenderSystem(_console, _camera, _sprites, Query.Without<Frozen>()),
            new SpriteRenderSystem(_console, _camera, _sprites, Query.With<Frozen>()));

        presentation.Update(0f);

        Assert.That(_console.GetCharAt(2, 2), Is.EqualTo('a'));
    }

    [Test]
    public void Update_IdChanged_DrawsNewSprite()
    {
        var entity = _world.Create(new Position(new Vector2(0, 0)), new SpriteRef(_box));
        var presentation = CreatePresentation(new SpriteRenderSystem(_console, _camera, _sprites));

        _world.Get<SpriteRef>(entity).Id = _dot;
        presentation.Update(0f);

        Assert.That(_console.GetCharAt(0, 0), Is.EqualTo('x'));
    }

    private SystemGroup CreatePresentation(params EntitySystem[] systems)
    {
        var group = new SystemGroup(_world);
        foreach (var system in systems)
            group.Add(system);
        return group;
    }
}
