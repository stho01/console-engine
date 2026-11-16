using System;
using System.Collections.Generic;
using System.Numerics;
using ConsoleEngine.Rendering3D;
using Doom.Graphics;

namespace Doom.Rendering.Meshes;

/// <summary>
/// Sprite pictures as meshes. At full detail (level 0) texel for texel: each row's runs of drawn texels of one colour
/// become one quad, so the picture keeps its detail and its holes. Far away, where a texel is smaller than a screen
/// pixel, levels 1 and 2 use 2 × 2 and 4 × 4 texel cells instead (drawn when at least half their texels are, in
/// their average colour), a quarter and a sixteenth of the triangles. Local space: x to the right, y up from the
/// thing's feet, the picture facing +z, one map unit per texel. Built on first use and kept (a mirrored rotation is
/// its own mesh).
/// </summary>
public sealed class SpriteMeshes(BlockColors colors)
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Levels of detail: 0 (every texel), 1 (2 × 2 cells), 2 (4 × 4 cells).</summary>
    public const int DetailLevels = 3;

    // One table per level of detail and mirroring: index = level * 2 + (mirrored ? 1 : 0).
    private readonly Dictionary<Picture, Mesh>[] _meshes =
        [new(), new(), new(), new(), new(), new()];

    private readonly MeshBuilder _builder = new();

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// The level of detail for a sprite <paramref name="pixelsPerTexel"/> screen pixels per texel wide: 0 from one
    /// pixel per texel, 1 from half a pixel, else 2.
    /// </summary>
    public static int DetailFor(float pixelsPerTexel) => pixelsPerTexel >= 1 ? 0 : pixelsPerTexel >= 0.5f ? 1 : 2;

    /// <summary>The picture's mesh, or null when it has no drawn texels. Allocates the first time.</summary>
    public Mesh Get(Picture picture, bool mirrored, int detail = 0)
    {
        var meshes = _meshes[detail * 2 + (mirrored ? 1 : 0)];
        if (!meshes.TryGetValue(picture, out var mesh))
        {
            mesh = Build(picture, mirrored, 1 << detail);
            meshes.Add(picture, mesh);
        }

        return mesh;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>Quads of <paramref name="cell"/>-texel squares; runs of one colour merge per row.</summary>
    private Mesh Build(Picture picture, bool mirrored, int cell)
    {
        _builder.Clear();
        var across = (picture.Width + cell - 1) / cell;
        for (var y = 0; y < picture.Height; y += cell)
        {
            var top = picture.TopOffset - (float)y;
            var bottom = picture.TopOffset - (float)Math.Min(picture.Height, y + cell);
            for (var cx = 0; cx < across;)
            {
                if (!Cell(picture, cx * cell, y, cell, out var color))
                {
                    cx++;
                    continue;
                }

                var run = 1;
                while (cx + run < across && Cell(picture, (cx + run) * cell, y, cell, out var next) && next == color)
                    run++;

                var x0 = cx * cell;
                var x1 = Math.Min(picture.Width, (cx + run) * cell);
                // Mirrored: column c is drawn at Width - 1 - c (see WorldRenderer.DrawSprite).
                var left = mirrored ? picture.LeftOffset - (float)x1 : x0 - (float)picture.LeftOffset;
                var right = left + (x1 - x0);
                _builder.AddQuad(new Vector3(left, bottom, 0), new Vector3(right, bottom, 0),
                    new Vector3(right, top, 0), new Vector3(left, top, 0), color);
                cx += run;
            }
        }

        return _builder.ToMesh(null);
    }

    /// <summary>Whether at least half the texels of a cell are drawn, and their average colour.</summary>
    private bool Cell(Picture picture, int x0, int y0, int cell, out Vector3 color)
    {
        var sum = Vector3.Zero;
        int drawn = 0, total = 0;
        for (var x = x0; x < Math.Min(picture.Width, x0 + cell); x++)
        for (var y = y0; y < Math.Min(picture.Height, y0 + cell); y++)
        {
            total++;
            var i = x * picture.Height + y;
            if (!picture.Opaque[i])
                continue;

            sum += colors.ColorOf(picture.Pixels[i]);
            drawn++;
        }

        color = drawn > 0 ? sum / drawn : Vector3.Zero;
        return drawn * 2 >= total && drawn > 0;
    }
}
