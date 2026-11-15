using System.Collections.Generic;
using System.IO;
using System.Numerics;
using ConsoleEngine.Assets;
using ConsoleEngine.Rendering3D;

namespace Kartster.Loading;

/// <summary>
/// Reads an <c>.obj</c> file (kart parts, track ribbons and splines, scenery models) with <see cref="ObjFile"/>.
/// Registered by <see cref="KartsterAssets.Register"/>.
/// </summary>
public sealed class ObjLoader : IAssetLoader<ObjGeometry>
{
    public IReadOnlyList<string> Extensions { get; } = ["obj"];

    public ObjGeometry Load(Stream stream, AssetLoadContext context)
    {
        var positions = new List<Vector3>();
        var indices = new List<int>();
        using var reader = new StreamReader(stream, leaveOpen: true);
        ObjFile.Read(reader, context.Path.Value, positions, indices);
        return new ObjGeometry(positions.ToArray(), indices.ToArray());
    }
}
