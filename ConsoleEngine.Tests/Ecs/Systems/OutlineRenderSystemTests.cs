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

public class OutlineRenderSystemTests
{
    private World _world;
    private RenderConsole _console;
    private Camera _camera;
    private OutlineLibrary _outlines;
    private SystemGroup _presentation;
    private int _verticalLine;

    [SetUp]
    public void SetUp()
    {
        _world = new World();
        _console = new RenderConsole(new FakeConsoleHandler(20, 20));
        _camera = new Camera();
        _outlines = new OutlineLibrary();
        _verticalLine = _outlines.Add([new Vector2(0, -3), new Vector2(0, 3)]);
        _presentation = new SystemGroup(_world);
        _presentation.Add(new OutlineRenderSystem(_console, _camera, _outlines));
    }

    [TearDown]
    public void TearDown() => _world.Dispose();

    [Test]
    public void Update_NoRotation_DrawsOutlineAroundPosition()
    {
        _world.Create(new Position(new Vector2(10, 10)), new Outline(_verticalLine, '#', ConsoleColor.Green));

        _presentation.Update(0f);

        Assert.Multiple(() =>
        {
            Assert.That(_console.GetCharAt(10, 7), Is.EqualTo('#'));
            Assert.That(_console.GetCharAt(10, 13), Is.EqualTo('#'));
            Assert.That(_console.GetCharAt(7, 10), Is.Not.EqualTo('#'));
        });
    }

    [Test]
    public void Update_QuarterTurn_DrawsOutlineRotatedClockwise()
    {
        _world.Create(new Position(new Vector2(10, 10)), new Outline(_verticalLine, '#', ConsoleColor.Green),
            new Rotation(MathF.PI / 2));

        _presentation.Update(0f);

        Assert.Multiple(() =>
        {
            Assert.That(_console.GetCharAt(13, 10), Is.EqualTo('#'));
            Assert.That(_console.GetCharAt(7, 10), Is.EqualTo('#'));
            Assert.That(_console.GetCharAt(10, 7), Is.Not.EqualTo('#'));
        });
    }

    [Test]
    public void Update_OutlineFullyOffScreen_DrawsNothing()
    {
        _world.Create(new Position(new Vector2(-10, 10)), new Outline(_verticalLine, '#', ConsoleColor.Green));

        _presentation.Update(0f);

        for (var y = 0; y < 20; y++)
        for (var x = 0; x < 20; x++)
            Assert.That(_console.GetCharAt(x, y), Is.Not.EqualTo('#'));
    }

    [Test]
    public void GetRadius_Outline_IsDistanceToFarthestVertex()
    {
        var id = _outlines.Add([new Vector2(3, 4), new Vector2(-1, 0)]);

        Assert.That(_outlines.GetRadius(id), Is.EqualTo(5f));
    }
}
