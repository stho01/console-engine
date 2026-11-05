using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using ConsoleEngine.Ecs.Systems;
using ConsoleEngine.Infrastructure.Rendering;
using ConsoleEngine.Tests.Fakes;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace ConsoleEngine.Tests.Ecs.Systems;

public class GlyphRenderSystemTests
{
    private World _world;
    private RenderConsole _console;
    private Camera _camera;
    private SystemGroup _presentation;

    [SetUp]
    public void SetUp()
    {
        _world = new World();
        _console = new RenderConsole(new FakeConsoleHandler(20, 10));
        _camera = new Camera();
        _presentation = new SystemGroup(_world);
        _presentation.Add(new GlyphRenderSystem(_console, _camera));
    }

    [TearDown]
    public void TearDown() => _world.Dispose();

    [Test]
    public void Update_GlyphOnScreen_DrawsItInItsCell()
    {
        _world.Create(new Position(new Vector2(3.7f, 2.2f)), new Glyph('*', ConsoleColor.Yellow));

        _presentation.Update(0f);

        Assert.That(_console.GetCharAt(3, 2), Is.EqualTo('*'));
    }

    [Test]
    public void Update_CameraMoved_DrawsRelativeToCamera()
    {
        _world.Create(new Position(new Vector2(13, 7)), new Glyph('@', ConsoleColor.White));
        _camera.Position = new Vector2(10, 5);

        _presentation.Update(0f);

        Assert.That(_console.GetCharAt(3, 2), Is.EqualTo('@'));
    }

    [Test]
    public void Update_SlightlyLeftOfScreen_RoundsDownAndIsClipped()
    {
        _world.Create(new Position(new Vector2(-0.5f, 0)), new Glyph('x', ConsoleColor.White));

        _presentation.Update(0f);

        Assert.That(_console.GetCharAt(0, 0), Is.Not.EqualTo('x'));
    }

    [Test]
    public void Glyph_Colors_RoundTripThroughBytes()
    {
        var glyph = new Glyph('#', ConsoleColor.DarkMagenta, ConsoleColor.Cyan);

        Assert.Multiple(() =>
        {
            Assert.That(glyph.Foreground, Is.EqualTo(ConsoleColor.DarkMagenta));
            Assert.That(glyph.Background, Is.EqualTo(ConsoleColor.Cyan));
            Assert.That(System.Runtime.CompilerServices.Unsafe.SizeOf<Glyph>(), Is.EqualTo(4));
        });
    }
}
