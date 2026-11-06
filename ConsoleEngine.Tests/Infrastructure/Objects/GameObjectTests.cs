using System;
using ConsoleEngine.Infrastructure.Objects;
using ConsoleEngine.Infrastructure.Rendering;
using ConsoleEngine.Tests.Fakes;
using Microsoft.Xna.Framework;
using NUnit.Framework;

namespace ConsoleEngine.Tests.Infrastructure.Objects;

public class GameObjectTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(16);

    private FakeConsoleHandler _handler;
    private RenderConsole _console;

    [SetUp]
    public void SetUp()
    {
        _handler = new FakeConsoleHandler(10, 5);
        _console = new RenderConsole(_handler);
    }

    [Test]
    public void Update_FirstTime_StartsBehaviourBeforeUpdating()
    {
        var behaviour = new RecordingBehaviour();
        var gameObject = new GameObject { Behaviour = behaviour };

        gameObject.Update(Frame);
        gameObject.Update(Frame);

        Assert.Multiple(() =>
        {
            Assert.That(behaviour.Calls, Is.EqualTo(new[] { "start", "update", "update" }));
            Assert.That(behaviour.LastDelta, Is.EqualTo(Frame));
        });
    }

    [Test]
    public void Behaviour_WhenReplaced_StartsNewBehaviourOnNextUpdate()
    {
        var gameObject = new GameObject { Behaviour = new RecordingBehaviour() };
        gameObject.Update(Frame);

        var replacement = new RecordingBehaviour();
        gameObject.Behaviour = replacement;
        gameObject.Update(Frame);

        Assert.That(replacement.Calls, Is.EqualTo(new[] { "start", "update" }));
    }

    [Test]
    public void Update_WithoutBehaviour_DoesNothing()
    {
        Assert.DoesNotThrow(() => new GameObject().Update(Frame));
    }

    [Test]
    public void Render_WhenVisible_DrawsCharAtFlooredPositionThenRendersBehaviour()
    {
        var behaviour = new RecordingBehaviour();
        var gameObject = new GameObject
        {
            Position = new Vector2(3.9f, 2.1f),
            Char = '@',
            Color = ConsoleColor.Yellow,
            Background = ConsoleColor.Blue,
            Behaviour = behaviour
        };

        gameObject.Render(_console);
        _console.Display();

        var pixel = _handler.PixelAt(3, 2);
        Assert.Multiple(() =>
        {
            Assert.That(pixel.Char, Is.EqualTo('@'));
            Assert.That(pixel.ForegroundColor, Is.EqualTo(ConsoleColor.Yellow));
            Assert.That(pixel.BackgroundColor, Is.EqualTo(ConsoleColor.Blue));
            Assert.That(behaviour.Calls, Is.EqualTo(new[] { "render" }));
        });
    }

    [Test]
    public void Render_WithSprite_DrawsSpriteInsteadOfChar()
    {
        var gameObject = new GameObject
        {
            Position = new Vector2(1, 1),
            Char = '@',
            Sprite = Sprite.FromStringArray(["ab"])
        };

        gameObject.Render(_console);
        _console.Display();

        Assert.Multiple(() =>
        {
            Assert.That(_handler.PixelAt(1, 1).Char, Is.EqualTo('a'));
            Assert.That(_handler.PixelAt(2, 1).Char, Is.EqualTo('b'));
        });
    }

    [Test]
    public void Render_WhenHidden_DrawsNothingAndSkipsBehaviour()
    {
        var behaviour = new RecordingBehaviour();
        var gameObject = new GameObject { Position = new Vector2(1, 1), Char = '@', Visible = false, Behaviour = behaviour };

        gameObject.Render(_console);
        _console.Display();

        Assert.Multiple(() =>
        {
            Assert.That(_handler.PixelAt(1, 1).Char, Is.Not.EqualTo('@'));
            Assert.That(behaviour.Calls, Is.Empty);
        });
    }
}
