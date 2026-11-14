using System;
using System.Numerics;
using NUnit.Framework;

namespace ConsoleEngine.Rendering3D.Tests;

/// <summary><see cref="SceneLighting"/> on the <see cref="Rasterizer"/>: ambient, sun colour, point and spotlights.</summary>
public class LightingTests
{
    private const int Size = 64;
    private static readonly Vector3 Gray = new(0.5f);
    private static readonly Vector3 Up = Vector3.UnitY;

    // Camera 3 m above a floor at y = 0, looking down at it from behind, so the floor fills the lower screen.
    private static readonly Vector3 Eye = new(0, 3, 6);
    private static readonly Matrix4x4 ViewProjection =
        Matrix4x4.CreateLookAt(Eye, new Vector3(0, 0, -2), Vector3.UnitY)
        * Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3, 1, 0.1f, 100);

    private static readonly Mesh Floor = Mesh.CreateFloor(40, 1, Gray, Gray);

    //**********************************************************
    //** tests
    //**********************************************************

    [TestCase(false)]
    [TestCase(true)]
    public void Draw_DefaultLightingWithoutLights_IsTheSameAsNone(bool trueColor)
    {
        var none = RenderPicture(trueColor, null);
        var defaults = RenderPicture(trueColor, new SceneLighting());

        Assert.That(defaults, Is.EqualTo(none));
    }

    [Test]
    public void Draw_ColouredAmbientAndSun_LightTheTriangleByThem()
    {
        var lighting = new SceneLighting { Ambient = new Vector3(0.1f, 0.2f, 0.3f), SunColor = new Vector3(0.5f, 0, 0) };

        var rgb = Render(true, lighting, r => r.Draw(Floor, Matrix4x4.Identity, ViewProjection, Up));

        Assert.That(rgb[Pixel(Size / 2, Size - 4)], Is.EqualTo(Pack(Gray * new Vector3(0.6f, 0.2f, 0.3f))));
    }

    [Test]
    public void Draw_PointLightAboveFloor_AddsItsLightPerPixelAtTheExactWorldPosition()
    {
        var light = new PointLight(new Vector3(0, 1, 0), new Vector3(1, 0.5f, 0.25f), 4f);
        var lighting = Night(light);

        var rgb = Render(true, lighting, r => r.Draw(Floor, Matrix4x4.Identity, ViewProjection, Up));

        // Pixel centres hit the floor at points the ray from the eye says; the rasterizer must find the same ones.
        foreach (var (x, y) in new[] { (32, 40), (20, 44), (40, 50), (32, 60) })
        {
            var position = FloorPointAt(x, y);
            var expected = Gray * (lighting.Ambient + lighting.SunColor)
                           + Gray * SceneLighting.Contribution(light, position, Up);
            Assert.That(ChannelDifference(rgb[Pixel(x, y)], Pack(expected)), Is.LessThanOrEqualTo(1),
                $"pixel {x}, {y} at {position}");
        }
    }

    [Test]
    public void Draw_PointLight_FadesToNothingAtItsRadius()
    {
        var light = new PointLight(new Vector3(0, 0.5f, 0), Vector3.One, 2f);

        var rgb = Render(true, Night(light), r => r.Draw(Floor, Matrix4x4.Identity, ViewProjection, Up));
        var unlit = Render(true, Night(), r => r.Draw(Floor, Matrix4x4.Identity, ViewProjection, Up));

        var under = Pixel(Project(new Vector3(0, 0, 0)));
        var outside = Pixel(Project(new Vector3(3, 0, 0)));
        Assert.That(rgb[under], Is.GreaterThan(unlit[under]));
        Assert.That(rgb[outside], Is.EqualTo(unlit[outside]));
    }

    [Test]
    public void Draw_SpotLight_LightsInsideItsConeOnly()
    {
        var down = Vector3.Normalize(new Vector3(0, -1, -1));
        var spot = new PointLight(new Vector3(0, 1, 2), Vector3.One * 3, 10f, down, 0.9f);

        var rgb = Render(true, Night(spot), r => r.Draw(Floor, Matrix4x4.Identity, ViewProjection, Up));
        var unlit = Render(true, Night(), r => r.Draw(Floor, Matrix4x4.Identity, ViewProjection, Up));

        var inCone = Pixel(Project(new Vector3(0, 0, 1)));
        var besideCone = Pixel(Project(new Vector3(2.5f, 0, 1)));
        Assert.That(rgb[inCone], Is.GreaterThan(unlit[inCone]));
        Assert.That(rgb[besideCone], Is.EqualTo(unlit[besideCone]));
    }

    [Test]
    public void Draw_ColouredLight_AddsOnlyItsColour()
    {
        var red = new PointLight(new Vector3(0, 1, 0), new Vector3(1, 0, 0), 4f);

        var rgb = Render(true, Night(red), r => r.Draw(Floor, Matrix4x4.Identity, ViewProjection, Up));
        var unlit = Render(true, Night(), r => r.Draw(Floor, Matrix4x4.Identity, ViewProjection, Up));

        var under = Pixel(Project(Vector3.Zero));
        Assert.That(rgb[under] >> 16, Is.GreaterThan(unlit[under] >> 16));
        Assert.That(rgb[under] & 0xFFFF, Is.EqualTo(unlit[under] & 0xFFFF));
    }

    [Test]
    public void Draw_LightBelowFloor_AddsNothing()
    {
        var below = new PointLight(new Vector3(0, -0.5f, 0), Vector3.One, 4f);

        var rgb = Render(true, Night(below), r => r.Draw(Floor, Matrix4x4.Identity, ViewProjection, Up));

        Assert.That(rgb, Is.EqualTo(Render(true, Night(), r => r.Draw(Floor, Matrix4x4.Identity, ViewProjection, Up))));
    }

    [Test]
    public void Draw_UnlitMesh_IgnoresLights()
    {
        var floor = new Mesh(Floor.Positions, Floor.Indices, Floor.TriangleColors) { IsUnlit = true };

        var rgb = Render(true, Night(new PointLight(new Vector3(0, 1, 0), Vector3.One, 4f)),
            r => r.Draw(floor, Matrix4x4.Identity, ViewProjection, Up));

        Assert.That(rgb[Pixel(Project(Vector3.Zero))], Is.EqualTo(Pack(Gray)));
    }

    [Test]
    public void Draw_PointLightInPaletteMode_LightsTheFloor()
    {
        var unlit = RenderPicture(false, Night());
        var lit = RenderPicture(false, Night(new PointLight(new Vector3(0, 1, 0), Vector3.One * 2, 4f)));

        var under = Pixel(Project(Vector3.Zero));
        Assert.That(lit[under], Is.Not.EqualTo(unlit[under]));
    }

    [Test]
    public void Clear_SetsLightingBackToNone()
    {
        using var rasterizer = new Rasterizer(Size, Size, ConsolePalette.Campbell);
        rasterizer.Lighting = new SceneLighting();

        rasterizer.Clear(new ConsoleColor[Size * Size]);

        Assert.That(rasterizer.Lighting, Is.Null);
    }

    [Test]
    public void Add_PastMaxLights_IsIgnored()
    {
        var lighting = new SceneLighting();
        for (var i = 0; i < SceneLighting.MaxLights; i++)
            Assert.That(lighting.Add(new PointLight(Vector3.Zero, Vector3.One, 1f)), Is.True);

        Assert.That(lighting.Add(new PointLight(Vector3.Zero, Vector3.One, 1f)), Is.False);
        Assert.That(lighting.LightCount, Is.EqualTo(SceneLighting.MaxLights));
    }

    [Test]
    public void DrawAndFinish_WithLightsInSteadyState_DoesNotAllocate()
    {
        using var rasterizer = new Rasterizer(Size, Size, ConsolePalette.Campbell, trueColor: true);
        var lighting = Night(new PointLight(new Vector3(0, 1, 0), Vector3.One, 4f),
            new PointLight(new Vector3(0, 1, 2), Vector3.One, 10f, -Vector3.UnitZ, 0.85f));
        var background = new uint[Size * Size];
        Frame();

        var allocated = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 20; i++)
            Frame();

        Assert.That(GC.GetAllocatedBytesForCurrentThread() - allocated, Is.Zero);

        void Frame()
        {
            rasterizer.Clear(background);
            rasterizer.Lighting = lighting;
            rasterizer.Draw(Floor, Matrix4x4.Identity, ViewProjection, Up);
            rasterizer.Finish();
        }
    }

    //**********************************************************
    //** helpers
    //**********************************************************

    private static SceneLighting Night(params PointLight[] lights)
    {
        var lighting = new SceneLighting { Ambient = new Vector3(0.1f), SunColor = new Vector3(0.1f) };
        foreach (var light in lights)
            lighting.Add(light);
        return lighting;
    }

    private static uint[] Render(bool trueColor, SceneLighting lighting, Action<Rasterizer> scene)
    {
        using var rasterizer = new Rasterizer(Size, Size, ConsolePalette.Campbell, trueColor);
        if (trueColor)
            rasterizer.Clear(new uint[Size * Size]);
        else
            rasterizer.Clear(new ConsoleColor[Size * Size]);
        rasterizer.Lighting = lighting;
        scene(rasterizer);
        rasterizer.Finish();
        return trueColor ? (uint[])rasterizer.Rgb.Clone() : Array.ConvertAll(rasterizer.Colors, c => (uint)c);
    }

    /// <summary>The floor and, across the near plane, a second floor reaching behind the camera.</summary>
    private static uint[] RenderPicture(bool trueColor, SceneLighting lighting) =>
        Render(trueColor, lighting, r =>
        {
            r.Draw(Floor, Matrix4x4.Identity, ViewProjection, Vector3.Normalize(new Vector3(0.3f, 1, 0.2f)));
            r.Draw(Floor, Matrix4x4.CreateRotationX(0.2f), ViewProjection, Vector3.Normalize(new Vector3(0.3f, 1, 0.2f)));
        });

    private static int Pixel(int x, int y) => y * Size + x;

    private static int Pixel((int X, int Y) pixel) => Pixel(pixel.X, pixel.Y);

    private static (int X, int Y) Project(Vector3 position)
    {
        var clip = Vector4.Transform(position, ViewProjection);
        return ((int)((clip.X / clip.W + 1) * 0.5f * Size), (int)((1 - clip.Y / clip.W) * 0.5f * Size));
    }

    /// <summary>Where the ray from the eye through pixel centre (x, y) meets the floor.</summary>
    private static Vector3 FloorPointAt(int x, int y)
    {
        Matrix4x4.Invert(ViewProjection, out var inverse);
        var ndc = new Vector2((x + 0.5f) / Size * 2 - 1, 1 - (y + 0.5f) / Size * 2);
        var far = Vector4.Transform(new Vector4(ndc, 1, 1), inverse);
        var direction = new Vector3(far.X, far.Y, far.Z) / far.W - Eye;
        return Eye + direction * (-Eye.Y / direction.Y);
    }

    private static uint Pack(Vector3 color)
    {
        var rgb = Vector3.Clamp(color, Vector3.Zero, Vector3.One) * 255f + new Vector3(0.5f);
        return ((uint)rgb.X << 16) | ((uint)rgb.Y << 8) | (uint)rgb.Z;
    }

    private static int ChannelDifference(uint a, uint b)
    {
        var max = 0;
        for (var shift = 0; shift < 24; shift += 8)
            max = Math.Max(max, Math.Abs((int)((a >> shift) & 0xFF) - (int)((b >> shift) & 0xFF)));
        return max;
    }
}
