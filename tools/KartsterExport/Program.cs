using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Kartster.Screens.Editor.Storage;

namespace KartsterExport;

/// <summary>
/// Exports the original Kartster's built-in tracks for <c>Examples/Kartster</c>, with the original's own code.
/// <code>
/// KartsterExport list                          the built-in track ids
/// KartsterExport export &lt;outDir&gt; &lt;trackId...&gt;   one file per exporter and track
/// </code>
/// </summary>
internal static class Program
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Every file written per track. A new export is one class here.</summary>
    internal static readonly ITrackExporter[] Exporters =
        [new RibbonExporter(), new SplineExporter(), new SceneryExporter(), new RaceExporter(),
         new CollisionExporter()];

    private const string Usage = "usage: KartsterExport list | export <outDir> <trackId...>";

    //**********************************************************
    //** public methods
    //**********************************************************

    public static int Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        switch (args)
        {
            case ["list"]:
                foreach (var id in BuiltInTracks.Ids.Order(StringComparer.Ordinal))
                    Console.WriteLine(id);
                return 0;
            case ["export", var outDir, .. var ids] when ids.Length > 0:
                Directory.CreateDirectory(outDir);
                foreach (var id in ids)
                    Export(outDir, id);
                return 0;
            default:
                Console.Error.WriteLine(Usage);
                return 1;
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static void Export(string outDir, string id)
    {
        if (!BuiltInTracks.IsBuiltIn(id))
            throw new ArgumentException($"'{id}' is not a built-in track (see 'list')");

        var track = TrackDocumentMapper.ToTrack(BuiltInTracks.GetDocument(id));
        Console.WriteLine($"{id}:");
        foreach (var exporter in Exporters)
        {
            var path = Path.Combine(outDir, id + exporter.Suffix);
            try
            {
                // The original's OBJ exporters write CRLF; the repo keeps LF.
                File.WriteAllText(path, exporter.Export(id, track).ReplaceLineEndings("\n"));
                Console.WriteLine($"  {Path.GetFileName(path)} {new FileInfo(path).Length / 1024.0:0.0} KiB");
            }
            catch (NotSupportedException e)
            {
                Console.WriteLine($"  {Path.GetFileName(path)} skipped: {e.Message}");
            }
        }
    }
}
