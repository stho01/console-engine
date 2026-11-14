using Kartster.Models.Tracks;
using Kartster.Models.Tracks.Export;

namespace KartsterExport;

/// <summary>The road surface and its edge walls: the original's <c>TrackRibbonObjExporter</c>.</summary>
internal sealed class RibbonExporter : ITrackExporter
{
    public string Suffix => ".ribbon.obj";

    public string Export(string id, TrackGraph track) => new TrackRibbonObjExporter().Export(track);
}
