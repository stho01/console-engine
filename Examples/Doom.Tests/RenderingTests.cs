using System;
using ConsoleEngine.Infrastructure.Rendering;
using Doom.Graphics;
using Doom.Play;
using Doom.Rendering;
using Doom.Tests.Fakes;
using NUnit.Framework;

namespace Doom.Tests;

public class RenderingTests
{
    private Level _level;

    [TearDown]
    public void DisposeLevel() => _level?.Dispose();

    private (Level Level, WorldRenderer Renderer, FrameBuffer Frame, Palette Palette) Setup(Layout layout = null)
    {
        layout ??= Layout.Standard;
        var (_, palette, textures, _) = TestWad.Load();
        var level = _level = TestWad.CreateLevel();
        var frame = new FrameBuffer(layout.ViewWidth, layout.ViewHeight);
        return (level, new WorldRenderer(frame, palette, textures), frame, palette);
    }

    private static Camera CameraOf(Level level)
    {
        ref var body = ref level.PlayerBody;
        return new Camera(body.X, body.Y, level.Player.ViewZ, body.Angle);
    }

    [Test]
    public void Render_FacingClosedDoor_DrawsCeilingWallAndFloor()
    {
        var (level, renderer, frame, _) = Setup();

        renderer.Render(level, CameraOf(level), -1);

        var middle = frame.Width / 2;
        Assert.That(frame.Get(middle, 0), Is.EqualTo(TestWad.CeilingColor));
        Assert.That(frame.Get(middle, frame.Height / 2), Is.EqualTo(TestWad.WallColor));
        Assert.That(frame.Get(middle, frame.Height - 1), Is.EqualTo(TestWad.FloorColor));
    }

    [Test]
    public void Render_FacingClosedDoor_WallDepthIsTheDistance()
    {
        var (level, renderer, frame, _) = Setup();

        renderer.Render(level, CameraOf(level), -1);

        var depth = frame.Depth[frame.Height / 2 * frame.Width + frame.Width / 2];
        Assert.That(depth, Is.EqualTo(256 - 64).Within(2f));
    }

    [Test]
    public void Render_EveryDirection_LeavesNoColumnOpen()
    {
        var (level, renderer, frame, _) = Setup();
        var body = level.PlayerBody;

        for (var turn = 0; turn < 16; turn++)
        {
            var camera = new Camera(body.X, body.Y, level.Player.ViewZ, turn * MathF.PI / 8);
            renderer.Render(level, camera, -1);
            for (var x = 0; x < frame.Width; x++)
                Assert.That(frame.Depth[frame.Height / 2 * frame.Width + x], Is.LessThan(float.MaxValue),
                    $"turn {turn}, column {x}");
        }
    }

    [Test]
    public void Render_SeenWalls_AreMarkedForTheAutomap()
    {
        var (level, renderer, _, _) = Setup();

        renderer.Render(level, CameraOf(level), -1);

        Assert.That(level.Map.Lines[4].Mapped, Is.True);
        Assert.That(level.Map.Lines[12].Mapped, Is.False);
    }

    [Test]
    public void Draw_Automap_DrawsSeenWalls()
    {
        var (level, renderer, frame, palette) = Setup();
        renderer.Render(level, CameraOf(level), -1);
        var automap = new Automap(palette);

        automap.Draw(frame, level);

        Assert.That(frame.Pixels, Has.Some.Not.EqualTo(0));
    }

    [Test]
    public void Present_Frame_FillsTheViewWithHalfBlocks()
    {
        var (level, renderer, frame, palette) = Setup();
        renderer.Render(level, CameraOf(level), -1);
        var console = new RenderConsole(new FakeConsoleHandler(Layout.Standard.ScreenWidth, Layout.Standard.ScreenHeight));

        Presenter.Present(frame, new ConsoleShades(palette, 0), 0, console);

        Assert.That(console.GetCharAt(0, 0), Is.EqualTo(Presenter.HalfBlock));
        var layout = Layout.Standard;
        Assert.That(console.GetCharAt(layout.ViewWidth - 1, layout.ViewRows - 1), Is.EqualTo(Presenter.HalfBlock));
    }

    [Test]
    public void Draw_Hud_ShowsHealth()
    {
        using var level = TestWad.CreateLevel();
        var console = new RenderConsole(new FakeConsoleHandler(Layout.Standard.ScreenWidth, Layout.Standard.ScreenHeight));

        new Hud().Draw(console, level, "E1M1", Layout.Standard.HudTop);

        Assert.That(console.GetCharAt(1, Layout.Standard.HudTop + 1), Is.EqualTo(DoomTexts.HudHealth[0]));
    }

    [Test]
    public void Render_HighLayoutEveryDirection_LeavesNoColumnOpen()
    {
        var (level, renderer, frame, _) = Setup(Layout.High);
        var body = level.PlayerBody;

        for (var turn = 0; turn < 16; turn++)
        {
            var camera = new Camera(body.X, body.Y, level.Player.ViewZ, turn * MathF.PI / 8);
            renderer.Render(level, camera, -1);
            for (var x = 0; x < frame.Width; x++)
                Assert.That(frame.Depth[frame.Height / 2 * frame.Width + x], Is.LessThan(float.MaxValue),
                    $"turn {turn}, column {x}");
        }
    }

    [Test]
    public void Render_HighLayout_WallDepthMatchesStandard()
    {
        var (level, renderer, frame, _) = Setup(Layout.High);

        renderer.Render(level, CameraOf(level), -1);

        var depth = frame.Depth[frame.Height / 2 * frame.Width + frame.Width / 2];
        Assert.That(depth, Is.EqualTo(256 - 64).Within(2f));
        Assert.That(frame.Get(frame.Width / 2, frame.Height / 2), Is.EqualTo(TestWad.WallColor));
    }

    [Test]
    public void Present_HighLayoutFrame_FillsTheViewWithHalfBlocks()
    {
        var layout = Layout.High;
        var (level, renderer, frame, palette) = Setup(layout);
        renderer.Render(level, CameraOf(level), -1);
        var console = new RenderConsole(new FakeConsoleHandler(layout.ScreenWidth, layout.ScreenHeight));

        Presenter.Present(frame, new ConsoleShades(palette, 0), 0, console);

        Assert.That(console.GetCharAt(layout.ViewWidth - 1, layout.ViewRows - 1), Is.EqualTo(Presenter.HalfBlock));
        Assert.That(console.GetCharAt(0, layout.HudTop), Is.Not.EqualTo(Presenter.HalfBlock));
    }
}
