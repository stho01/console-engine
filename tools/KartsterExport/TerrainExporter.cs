using System;
using System.Globalization;
using System.Text;
using Kartster.Models.Tracks;

namespace KartsterExport;

/// <summary>
/// The hilly ground beside the road: the original's <c>TrackTerrain</c> grid as it is (origin, cell size, columns,
/// rows, heights with NaN where there is none), heights in centimetres, plus one colour for its
/// <c>TrackGroundStyle</c>. Tracks with flat ground (<c>TrackGround</c>) or none are skipped: the port draws its
/// chequer there. Format in <c>Examples/Kartster/Terrain.cs</c>.
/// </summary>
internal sealed class TerrainExporter : ITrackExporter
{
    public string Suffix => ".terrain.txt";

    public string Export(string id, TrackGraph track)
    {
        if (track.Terrain is not TrackTerrain terrain)
            throw new NotSupportedException(track.Ground != null ? "flat ground, no terrain" : "no terrain");

        var grid = $"{terrain.OriginX:0.###} {terrain.OriginZ:0.###} {terrain.CellSize:0.###} {terrain.Columns} {terrain.Rows}";
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"""
            # {id} terrain, exported from Kartster by KartsterExport. Format: Examples/Kartster/Terrain.cs
            # terrain originX originZ cellSize columns rows r g b, then a line of heights per row (Z grows), X growing
            terrain {grid} {Color(track.GroundStyle)}

            """);
        for (var row = 0; row < terrain.Rows; row++)
        {
            for (var column = 0; column < terrain.Columns; column++)
            {
                if (column > 0)
                    text.Append(' ');
                var height = terrain.GetHeight(column, row);
                text.Append(float.IsNaN(height) ? "NaN" : height.ToString("0.##", CultureInfo.InvariantCulture));
            }

            text.Append('\n');
        }

        return text.ToString();
    }

    // The ground's look as one colour: the port has no textures.
    private static string Color(TrackGroundStyle style) => style switch
    {
        TrackGroundStyle.Pavement => "0.42 0.42 0.43",
        TrackGroundStyle.Snow => "0.88 0.9 0.94",
        TrackGroundStyle.Sand => "0.76 0.67 0.45",
        TrackGroundStyle.Glass => "0.07 0.07 0.11",
        _ => "0.2 0.45 0.15", // Grass, the chequer's colour
    };
}
