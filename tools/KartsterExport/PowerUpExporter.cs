using System;
using System.Text;
using Kartster.Models.Tracks;
using Kartster.Models.Tracks.Geometry;

namespace KartsterExport;

/// <summary>
/// The power-up pads a normal race puts out: the original's <c>TrackPowerUpPadResolver</c>, extra pads left out as
/// <c>RaceSimulation.ResolvePowerUpPads</c> does. One <c>pad x y z setId</c> per pad, world metres, Kartster axes
/// (+Y up, -Z forward). Read by <c>Examples/Kartster</c>'s <c>Track.PowerUpPads</c>.
/// </summary>
internal sealed class PowerUpExporter : ITrackExporter
{
    public string Suffix => ".powerups.txt";

    public string Export(string id, TrackGraph track)
    {
        var text = new StringBuilder();
        text.Append($"# {id} power-up pads, exported from Kartster by PowerUpExport (TrackPowerUpPadResolver).\n");
        text.Append("# pad x y z setId   (world metres, Kartster axes; the pads a normal race has, " +
                    "extra ones left out)\n");
        int count = 0, extra = 0;
        foreach (var placement in TrackPowerUpPadResolver.Resolve(track))
        {
            if (placement.Pad.IsExtra)
            {
                extra++;
                continue;
            }

            var p = placement.WorldPosition;
            text.Append($"pad {p.X:0.###} {p.Y:0.###} {p.Z:0.###} {placement.Pad.SetId}\n");
            count++;
        }

        Console.WriteLine($"  powerups: {count} pads ({extra} extra left out)");
        return text.ToString();
    }
}
