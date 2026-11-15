using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Kartster.Models.Tracks;
using Kartster.Models.Tracks.Geometry;
using Kartster.Models.Tracks.Surfaces;
using Kartster.Rendering;
using Kartster.Screens.Editor.Storage;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace KartsterExport;

/// <summary>
/// A track's static scenery, placed and meshed by the original's own code: fences, pipes, trees, signs, the
/// start/finish line and gate and grid marks (power-up pads are <see cref="PowerUpExporter"/>'s). Textures are baked
/// into flat-coloured triangles. Format: <c>Examples/Kartster/Scenery.cs</c>.
/// </summary>
internal sealed class SceneryExporter : ITrackExporter
{
    //**********************************************************
    //** fields
    //**********************************************************

    // The fence texture (Content/Textures/Track/Fence.png): four 1 m tiles per 4 m repeat, colours read off the png.
    private static readonly Vector3[] FenceTiles =
        [new(0.84f, 0.2f, 0.2f), new(0.95f, 0.78f, 0.16f), new(0.16f, 0.36f, 0.82f), new(0.16f, 0.6f, 0.26f)];
    private static readonly Vector3 FenceTop = new(0.53f, 0.49f, 0.36f);
    // CreateCheckerTexture(new Color(20, 20, 20), new Color(235, 235, 235)): uv (0..0.5, 0..0.5) is white.
    private static readonly Vector3 CheckerDark = new Color(20, 20, 20).ToVector3();
    private static readonly Vector3 CheckerLight = new Color(235, 235, 235).ToVector3();

    private readonly StringBuilder _text = new();
    private readonly SortedDictionary<string, int> _counts = new();
    private int _triangles;

    //**********************************************************
    //** props
    //**********************************************************

    public string Suffix => ".scenery.txt";

    //**********************************************************
    //** public methods
    //**********************************************************

    public string Export(string id, TrackGraph track)
    {
        _text.Clear();
        _counts.Clear();
        _triangles = 0;
        Write(id, track);
        Console.WriteLine($"  scenery: {string.Join(" ", _counts.Select(c => $"{c.Key}={c.Value}"))} tri={_triangles}");
        return _text.ToString();
    }

    /// <summary>The track's paint, which places the start gate and lines (as the renderer does).</summary>
    internal static TrackSurface Surface(string id, TrackGraph track)
    {
        var surface = new TrackSurface(SurfacePalette.Find(track.SurfacePaletteId));
        if (BuiltInTracks.IsBuiltIn(id))
        {
            foreach (var map in BuiltInTracks.GetSurface(id).Values)
                surface.Set(map);
        }

        return surface;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void Write(string id, TrackGraph track)
    {
        _text.Append($"# {id} scenery, exported from Kartster by KartsterExport. Format: Examples/Kartster/Scenery.cs\n");
        _text.Append("# model <name> x y z yawDegrees scaleX scaleY scaleZ r g b\n");
        _text.Append("# tri x1 y1 z1 x2 y2 z2 x3 y3 z3 r g b   (counter-clockwise seen from the front)\n");

        var surface = Surface(id, track);
        if (track.Ground is TrackGround ground)
        {
            // The sides in their coloured tiles; the half-metre top in the tiles' average, which saves a third.
            var fence = TrackFenceMesh.Build(ground);
            Bake([.. fence.Where(v => MathF.Abs(v.Normal.Y) < 0.5f)], FenceColor, 0.25f, 2f, "fence");
            Flat([.. fence.Where(v => MathF.Abs(v.Normal.Y) >= 0.5f)], FenceTop, "fence");
        }

        foreach (var pipe in TrackPipes.Place(track))
            Model("Pipe", pipe.Position, 0, new Vector3(pipe.Radius, pipe.Height, pipe.Radius), Renderer("PipeColor"));

        WriteTrees(track.Trees);

        foreach (var sign in TrackSigns.Place(track))
        {
            var mesh = TrackSignMesh.Build(sign);
            Flat(mesh.Frame, sign.Sign.FrameColor.ToVector3(), "sign");
            Flat(mesh.Back, sign.Sign.PlateColor.ToVector3(), "sign");
            Flat(mesh.Front, sign.Sign.PlateColor.ToVector3(), "sign"); // shortcut: the painted text is left out
        }

        if (RaceMarkings.HasLine(track.StartFinishStyle))
        {
            foreach (var placement in RaceMarkings.Place(track, surface))
            {
                var parts = RaceLineMesh.Build(placement);
                Bake(parts.Checker, Checker, 0.5f, 0.5f, "raceline");
                Flat(parts.White, Renderer("RaceLineWhiteColor"), "raceline");
                Flat(parts.Panel, Renderer("RaceLineWhiteColor"), "raceline"); // START/FINISH text left out
            }

            Flat(GridMarkMesh.Build(track), Renderer("RaceLineWhiteColor"), "gridmark");
        }

        if (RaceMarkings.HasGate(track.StartFinishStyle))
        {
            foreach (var placement in RaceMarkings.PlaceGates(track, surface))
            {
                var parts = StartGateMesh.Build(placement);
                Flat(parts.Steel, Renderer("GuardrailColor"), "gate");
                if (placement.Kind == RaceMarkKind.Pepita)
                    Bake(parts.Banner, Checker, 0.5f, 0.5f, "gate");
                else
                    Flat(parts.Banner, Vector3.One, "gate");
            }
        }
    }

    private void WriteTrees(IReadOnlyList<TrackTree> trees)
    {
        var trunk = Renderer("TreeTrunkColor");
        foreach (var tree in trees)
        {
            var scale = new Vector3(tree.Scale);
            switch (tree.Kind) // TrackTreeMesh.Build's models, PrimitiveSceneRenderer.DrawTrees's colours
            {
                case TrackTreeKind.Broadleaf:
                    Model("BroadleafTrunk", tree.Position, 0, scale, trunk);
                    Model("BroadleafCrown", tree.Position, 0, scale, Renderer("LeafColor"));
                    break;
                case TrackTreeKind.Rock:
                    Model("Rock", tree.Position, 0, scale, Renderer("RockColor"));
                    break;
                case TrackTreeKind.Cactus:
                    Model("Cactus", tree.Position, 0, scale, Renderer("CactusColor"));
                    break;
                case TrackTreeKind.Derrick:
                    Model("Derrick", tree.Position, 0, scale, Renderer("DerrickColor"));
                    break;
                case TrackTreeKind.Palm:
                    Model("PalmTrunk", tree.Position, 0, scale, trunk);
                    Model("PalmCrown", tree.Position, 0, scale, Renderer("FrondColor"));
                    break;
                case TrackTreeKind.SnowySpruce:
                    Model("TreeTrunk", tree.Position, 0, scale, trunk);
                    Model("TreeCrown", tree.Position, 0, scale, Renderer("SnowyCrownColor"));
                    break;
                default:
                    Model("TreeTrunk", tree.Position, 0, scale, trunk);
                    Model("TreeCrown", tree.Position, 0, scale, Renderer("TreeCrownColor"));
                    break;
            }
        }
    }

    private static Vector3 FenceColor(Vector2 uv)
    {
        // The end caps are a sliver of texture (u 0..0.125); they get one plain colour.
        var tile = (int)MathF.Floor((uv.X - MathF.Floor(uv.X)) * 4f) & 3;
        return FenceTiles[tile];
    }

    private static Vector3 Checker(Vector2 uv)
    {
        var u = (int)MathF.Floor(uv.X * 2f);
        var v = (int)MathF.Floor(uv.Y * 2f);
        return ((u + v) & 1) == 0 ? CheckerLight : CheckerDark;
    }

    /// <summary>One of <c>PrimitiveSceneRenderer</c>'s private static colours.</summary>
    private static Vector3 Renderer(string field) =>
        (Vector3)typeof(PrimitiveSceneRenderer)
            .GetField(field, BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

    private void Model(string name, Vector3 position, float yawDegrees, Vector3 scale, Vector3 color)
    {
        Count(name);
        _text.Append($"model {name} {position.X:0.###} {position.Y:0.###} {position.Z:0.###} {yawDegrees:0.#} " +
            $"{scale.X:0.###} {scale.Y:0.###} {scale.Z:0.###} {color.X:0.###} {color.Y:0.###} {color.Z:0.###}\n");
    }

    private void Flat(VertexPositionNormalTexture[] vertices, Vector3 color, string what)
    {
        for (var i = 0; i + 2 < vertices.Length; i += 3)
            Triangle(vertices[i], vertices[i + 1], vertices[i + 2], color, what);
    }

    /// <summary>
    /// Bakes a textured quad list (a b c, a c d per quad) into flat cells: cut where u or v crosses a multiple of
    /// <paramref name="uStep"/> / <paramref name="vStep"/>, each cell the texture's colour at its middle.
    /// </summary>
    private void Bake(VertexPositionNormalTexture[] vertices, Func<Vector2, Vector3> colorAt, float uStep,
        float vStep, string what)
    {
        for (var q = 0; q + 5 < vertices.Length; q += 6)
        {
            var a = vertices[q];
            var b = vertices[q + 1];
            var c = vertices[q + 2];
            var d = vertices[q + 5];
            var normal = a.Normal;
            var sCuts = Cuts(a.TextureCoordinate.X, b.TextureCoordinate.X, uStep);
            var tCuts = Cuts(a.TextureCoordinate.Y, d.TextureCoordinate.Y, vStep);
            if (b.TextureCoordinate.X == a.TextureCoordinate.X)
                sCuts = [0f, 1f];
            for (var i = 0; i + 1 < sCuts.Count; i++)
            for (var j = 0; j + 1 < tCuts.Count; j++)
            {
                var color = colorAt(Uv((sCuts[i] + sCuts[i + 1]) / 2, (tCuts[j] + tCuts[j + 1]) / 2));
                var p00 = P(sCuts[i], tCuts[j]);
                var p10 = P(sCuts[i + 1], tCuts[j]);
                var p11 = P(sCuts[i + 1], tCuts[j + 1]);
                var p01 = P(sCuts[i], tCuts[j + 1]);
                Triangle(new(p00, normal, default), new(p10, normal, default), new(p11, normal, default), color, what);
                Triangle(new(p00, normal, default), new(p11, normal, default), new(p01, normal, default), color, what);
            }

            Vector3 P(float s, float t) =>
                Vector3.Lerp(Vector3.Lerp(a.Position, b.Position, s), Vector3.Lerp(d.Position, c.Position, s), t);
            Vector2 Uv(float s, float t) =>
                Vector2.Lerp(Vector2.Lerp(a.TextureCoordinate, b.TextureCoordinate, s),
                    Vector2.Lerp(d.TextureCoordinate, c.TextureCoordinate, s), t);
        }
    }

    /// <summary>0, every s in (0, 1) where u0 + s (u1 - u0) is a multiple of step, 1.</summary>
    private static List<float> Cuts(float u0, float u1, float step)
    {
        var cuts = new List<float> { 0f };
        if (u1 != u0)
        {
            var low = MathF.Min(u0, u1);
            var high = MathF.Max(u0, u1);
            var s = new List<float>();
            for (var k = MathF.Floor(low / step) + 1; k * step < high - 1e-4f; k++)
                s.Add((k * step - u0) / (u1 - u0));
            s.Sort();
            cuts.AddRange(s);
        }

        cuts.Add(1f);
        return cuts;
    }

    /// <summary>Writes the triangle counter-clockwise seen from where its normal points (the original culls none).</summary>
    private void Triangle(VertexPositionNormalTexture a, VertexPositionNormalTexture b, VertexPositionNormalTexture c,
        Vector3 color, string what)
    {
        var cross = Vector3.Cross(b.Position - a.Position, c.Position - a.Position);
        if (cross.LengthSquared() < 1e-12f)
            return;
        if (Vector3.Dot(cross, a.Normal) < 0)
            (b, c) = (c, b);
        Count(what);
        _triangles++;
        var (p, q, r) = (a.Position, b.Position, c.Position);
        _text.Append($"tri {p.X:0.###} {p.Y:0.###} {p.Z:0.###} {q.X:0.###} {q.Y:0.###} {q.Z:0.###} " +
            $"{r.X:0.###} {r.Y:0.###} {r.Z:0.###} {color.X:0.###} {color.Y:0.###} {color.Z:0.###}\n");
    }

    private void Count(string what) => _counts[what] = _counts.GetValueOrDefault(what) + 1;
}
