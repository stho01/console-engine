using Kartster.Models.Tracks;
using Kartster.Models.Tracks.Export;

namespace KartsterExport;

/// <summary>The centerline, one vertex per metre: the original's <c>TrackSplineObjExporter</c>.</summary>
internal sealed class SplineExporter : ITrackExporter
{
    public string Suffix => ".spline.obj";

    public string Export(string id, TrackGraph track) => new TrackSplineObjExporter().Export(track);
}
