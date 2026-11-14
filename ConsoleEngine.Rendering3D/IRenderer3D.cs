using System;
using System.Numerics;

namespace ConsoleEngine.Rendering3D;

/// <summary>
/// Draws meshes into one colour per pixel. A frame is <c>Clear</c>, any number of <see cref="Draw"/> calls, then
/// <see cref="Finish"/>; only after that is the frame in <see cref="Colors"/> (palette mode) or <see cref="Rgb"/>
/// (<see cref="TrueColor"/>). <see cref="Rasterizer"/> is the CPU implementation.
/// </summary>
public interface IRenderer3D : IDisposable
{
    int Width { get; }
    int Height { get; }

    /// <summary>
    /// Fixed at construction. False: 16 console colours, dithered, screen-door transparency (<see cref="Colors"/>).
    /// True: 24-bit colour, no dither, alpha-blended transparency (<see cref="Rgb"/>).
    /// </summary>
    bool TrueColor { get; }

    /// <summary>
    /// Palette mode: the frame, one colour per pixel, <c>index = y * Width + x</c>, complete after
    /// <see cref="Finish"/>. Empty in true-colour mode.
    /// </summary>
    ConsoleColor[] Colors { get; }

    /// <summary>
    /// True-colour mode: the frame, <c>0xRRGGBB</c> per pixel, <c>index = y * Width + x</c>, complete after
    /// <see cref="Finish"/>. Empty in palette mode.
    /// </summary>
    uint[] Rgb { get; }

    /// <summary>Starts a palette-mode frame on <paramref name="background"/> (Width × Height).</summary>
    /// <exception cref="InvalidOperationException">The renderer is in true-colour mode.</exception>
    void Clear(ReadOnlySpan<ConsoleColor> background);

    /// <summary>Starts a true-colour frame on <paramref name="backgroundRgb"/> (<c>0xRRGGBB</c>, Width × Height).</summary>
    /// <exception cref="InvalidOperationException">The renderer is in palette mode.</exception>
    void Clear(ReadOnlySpan<uint> backgroundRgb);

    /// <param name="toLight">Unit vector from the scene towards the light, in world space.</param>
    void Draw(Mesh mesh, Matrix4x4 world, Matrix4x4 viewProjection, Vector3 toLight);

    /// <summary>Ends the frame: <see cref="Colors"/> or <see cref="Rgb"/> holds it (a GPU renderer reads it back).</summary>
    void Finish();
}
