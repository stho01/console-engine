using System;
using System.Numerics;

namespace ConsoleEngine.Rendering3D;

/// <summary>
/// Draws meshes into one console colour per pixel. A frame is <see cref="Clear"/>, any number of <see cref="Draw"/>
/// calls, then <see cref="Finish"/>; only after that is <see cref="Colors"/> the frame. <see cref="Rasterizer"/> is
/// the CPU implementation.
/// </summary>
public interface IRenderer3D : IDisposable
{
    int Width { get; }
    int Height { get; }

    /// <summary>The frame, one colour per pixel, <c>index = y * Width + x</c>. Complete after <see cref="Finish"/>.</summary>
    ConsoleColor[] Colors { get; }

    /// <summary>Starts a frame on <paramref name="background"/> (<see cref="Width"/> × <see cref="Height"/>).</summary>
    void Clear(ReadOnlySpan<ConsoleColor> background);

    /// <param name="toLight">Unit vector from the scene towards the light, in world space.</param>
    void Draw(Mesh mesh, Matrix4x4 world, Matrix4x4 viewProjection, Vector3 toLight);

    /// <summary>Ends the frame: <see cref="Colors"/> holds it afterwards (a GPU renderer reads it back here).</summary>
    void Finish();
}
