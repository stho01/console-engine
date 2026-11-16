using System.Collections.Generic;
using System.Numerics;
using ConsoleEngine.Rendering3D;
using Doom.Graphics;

namespace Doom.Rendering.Meshes;

/// <summary>
/// Sprite pictures as meshes: one quad with the picture as its texture, its undrawn texels as holes. Local space:
/// x to the right, y up from the thing's feet, the picture facing +z, one map unit per texel. A mirrored rotation
/// flips the texture across; a full-bright one is unlit (no light, no fog). Built on first use and kept.
/// </summary>
public sealed class SpriteMeshes(TextureCache textures)
{
    //**********************************************************
    //** fields
    //**********************************************************

    // One table per kind: index = (mirrored ? 1 : 0) + (fullBright ? 2 : 0).
    private readonly Dictionary<Picture, Mesh>[] _meshes = [new(), new(), new(), new()];
    private readonly MeshBuilder _builder = new();

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>The picture's mesh. Allocates the first time.</summary>
    public Mesh Get(Picture picture, bool mirrored, bool fullBright = false)
    {
        var meshes = _meshes[(mirrored ? 1 : 0) + (fullBright ? 2 : 0)];
        if (!meshes.TryGetValue(picture, out var mesh))
        {
            mesh = Build(picture, mirrored, fullBright);
            meshes.Add(picture, mesh);
        }

        return mesh;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private Mesh Build(Picture picture, bool mirrored, bool fullBright)
    {
        // Mirrored: column c is drawn at Width - 1 - c (see WorldRenderer.DrawSprite).
        var left = mirrored ? picture.LeftOffset - (float)picture.Width : -picture.LeftOffset;
        var right = left + picture.Width;
        var top = (float)picture.TopOffset;
        var bottom = top - picture.Height;
        var uLeft = mirrored ? 1f : 0f;
        var uRight = 1 - uLeft;
        _builder.Clear();
        _builder.AddQuad(
            new Vector3(left, bottom, 0), new Vector2(uLeft, 1),
            new Vector3(right, bottom, 0), new Vector2(uRight, 1),
            new Vector3(right, top, 0), new Vector2(uRight, 0),
            new Vector3(left, top, 0), new Vector2(uLeft, 0),
            Vector3.One, textures.Sprite(picture));
        return _builder.ToMesh(null, unlit: fullBright);
    }
}
