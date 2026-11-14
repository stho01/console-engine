using Kartster.Models.Tracks;

namespace KartsterExport;

/// <summary>
/// Writes one file per track with the original Kartster's own code. To export something new,
/// write one class that implements this and add it to <see cref="Program.Exporters"/>.
/// </summary>
internal interface ITrackExporter
{
    /// <summary>What follows the track id in the file name, e.g. <c>.ribbon.obj</c>.</summary>
    string Suffix { get; }

    /// <summary>The file's text, LF line endings.</summary>
    /// <exception cref="System.NotSupportedException">The track has no such file; it is skipped.</exception>
    string Export(string id, TrackGraph track);
}
