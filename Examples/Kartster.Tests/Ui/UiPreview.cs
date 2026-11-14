using System;
using System.IO;
using System.Text;
using Kartster.Ui;
using NUnit.Framework;

namespace Kartster.Tests.Ui;

/// <summary>
/// A 320×180 true-colour frame for UI tests, with helpers to find what was drawn. <see cref="Save"/> writes it as a
/// PPM image under the test output (<c>ui-preview/</c>) so the HUD and menus can be looked at without a console.
/// </summary>
internal sealed class UiPreview
{
    public const int Width = 320;
    public const int Height = 180;
    public const uint Background = 0x123456;

    public UiPreview()
    {
        Array.Fill(Frame, Background);
        Target = new RgbTarget(Frame, Width, Height);
    }

    public uint[] Frame { get; } = new uint[Width * Height];
    public RgbTarget Target { get; }

    public uint this[int x, int y] => Frame[y * Width + x];

    /// <summary>The bounding box of every pixel of <paramref name="rgb"/>, or null if there is none.</summary>
    public (int Left, int Top, int Right, int Bottom)? Bounds(uint rgb, int left = 0, int top = 0, int right = Width,
        int bottom = Height)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (var y = top; y < bottom; y++)
        for (var x = left; x < right; x++)
        {
            if (this[x, y] != rgb)
                continue;

            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
        }

        return maxX < 0 ? null : (minX, minY, maxX, maxY);
    }

    public int Count(uint rgb)
    {
        var count = 0;
        foreach (var pixel in Frame)
        {
            if (pixel == rgb)
                count++;
        }

        return count;
    }

    public void Save(string name)
    {
        var folder = Path.Combine(TestContext.CurrentContext.WorkDirectory, "ui-preview");
        Directory.CreateDirectory(folder);
        using var file = File.Create(Path.Combine(folder, name + ".ppm"));
        file.Write(Encoding.ASCII.GetBytes($"P6\n{Width} {Height}\n255\n"));
        foreach (var pixel in Frame)
        {
            file.WriteByte((byte)(pixel >> 16));
            file.WriteByte((byte)(pixel >> 8));
            file.WriteByte((byte)pixel);
        }
    }
}
