using System;
using System.Numerics;
using NUnit.Framework;

namespace ConsoleEngine.Rendering3D.Tests;

public class ScreenDoorTests
{
    private const int Size = 32;

    private static readonly Matrix4x4 ViewProjection =
        Matrix4x4.CreateLookAt(new Vector3(0, 0, 5), Vector3.Zero, Vector3.UnitY)
        * Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 3, 1, 0.1f, 100);

    private static Rasterizer DrawQuad(float opacity, bool unlit = false)
    {
        Vector3[] positions = [new(-5, -5, 0), new(5, -5, 0), new(5, 5, 0), new(-5, 5, 0)];
        var mesh = new Mesh(positions, [0, 1, 2, 0, 2, 3], [Vector3.One, Vector3.One])
        {
            TriangleOpacity = [opacity, opacity],
            IsUnlit = unlit
        };
        var rasterizer = new Rasterizer(Size, Size, ConsolePalette.Campbell);
        rasterizer.Clear(new ConsoleColor[Size * Size]);
        rasterizer.Draw(mesh, Matrix4x4.Identity, ViewProjection, -Vector3.UnitZ); // lit from behind: dark if lit
        return rasterizer;
    }

    private static int Drawn(Rasterizer rasterizer) => Array.FindAll(rasterizer.Colors, c => c != ConsoleColor.Black).Length;

    [TestCase(0.25f)]
    [TestCase(0.5f)]
    public void Draw_SeeThroughTriangle_DrawsThatShareOfItsPixels(float opacity)
    {
        var full = Drawn(DrawQuad(1f, unlit: true));

        var drawn = Drawn(DrawQuad(opacity, unlit: true));

        Assert.That(drawn, Is.EqualTo((int)(full * opacity)).Within(full / 16));
    }

    [Test]
    public void Draw_InvisibleTriangle_DrawsNothing()
    {
        Assert.That(Drawn(DrawQuad(0f, unlit: true)), Is.Zero);
    }

    [Test]
    public void Draw_UnlitMesh_IgnoresTheLight()
    {
        Assert.That(DrawQuad(1f, unlit: true).Colors[Size / 2 * Size + Size / 2], Is.EqualTo(ConsoleColor.White));
    }
}
