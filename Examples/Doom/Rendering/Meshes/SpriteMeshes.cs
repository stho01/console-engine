using System.Collections.Generic;
using System.Numerics;
using ConsoleEngine.Rendering3D;
using Doom.Graphics;

namespace Doom.Rendering.Meshes;

/// <summary>
/// Sprite pictures as meshes, texel for texel: each row's runs of drawn texels of one colour become one quad, so
/// the picture keeps its full detail and its holes. Local space: x to the right, y up from the thing's feet, the
/// picture facing +z, one map unit per texel. Built on first use and kept (a mirrored rotation is its own mesh).
/// </summary>
public sealed class SpriteMeshes(BlockColors colors)
{
    //**********************************************************
    //** fields
    //**********************************************************

    private readonly Dictionary<Picture, Mesh> _meshes = [];
    private readonly Dictionary<Picture, Mesh> _mirroredMeshes = [];
    private readonly MeshBuilder _builder = new();

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>The picture's mesh, or null when it has no drawn texels. Allocates the first time.</summary>
    public Mesh Get(Picture picture, bool mirrored)
    {
        var meshes = mirrored ? _mirroredMeshes : _meshes;
        if (!meshes.TryGetValue(picture, out var mesh))
        {
            mesh = Build(picture, mirrored);
            meshes.Add(picture, mesh);
        }

        return mesh;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private Mesh Build(Picture picture, bool mirrored)
    {
        _builder.Clear();
        for (var y = 0; y < picture.Height; y++)
        {
            var bottom = picture.TopOffset - y - 1f;
            var top = bottom + 1;
            for (var x = 0; x < picture.Width;)
            {
                var i = x * picture.Height + y;
                if (!picture.Opaque[i])
                {
                    x++;
                    continue;
                }

                var color = picture.Pixels[i];
                var run = 1;
                while (x + run < picture.Width && picture.Opaque[(x + run) * picture.Height + y]
                                               && picture.Pixels[(x + run) * picture.Height + y] == color)
                    run++;

                // Mirrored: column c is drawn at Width - 1 - c (see WorldRenderer.DrawSprite).
                var left = mirrored ? picture.LeftOffset - x - run : x - picture.LeftOffset;
                var right = left + run;
                _builder.AddQuad(new Vector3(left, bottom, 0), new Vector3(right, bottom, 0),
                    new Vector3(right, top, 0), new Vector3(left, top, 0), colors.ColorOf(color));
                x += run;
            }
        }

        return _builder.ToMesh(null);
    }
}
