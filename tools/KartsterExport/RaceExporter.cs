using System;
using System.Reflection;
using System.Text;
using Kartster.Models.Tracks;
using Kartster.Models.Tracks.Geometry;
using Kartster.Physics.Simulation;
using Kartster.Screens.Racing;
using Kartster.Screens.Racing.Rendering;
using Microsoft.Xna.Framework;

namespace KartsterExport;

/// <summary>
/// The race markers: the route <c>RaceSimulation.SelectRoute</c> races, <c>RaceGatePlane.Create</c> for the
/// start/finish and each checkpoint, <c>GetStartingLocation</c> for the grid and <c>GetLapCount</c> for the laps.
/// Only closed routes. Format: <c>Examples/Kartster/Race/RaceMarkers.cs</c>.
/// </summary>
internal sealed class RaceExporter : ITrackExporter
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int GridSlots = 8;
    private const int DefaultLapCount = 3; // GameState.LapCount
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    //**********************************************************
    //** props
    //**********************************************************

    public string Suffix => ".race.txt";

    //**********************************************************
    //** public methods
    //**********************************************************

    public string Export(string id, TrackGraph track)
    {
        var route = (TrackRoute)typeof(RaceSimulation).GetMethod("SelectRoute", Private)!.Invoke(null, [track])!;
        if (!route.IsClosed)
            throw new NotSupportedException($"route {route.Id} is open; only closed routes are exported");
        var laps = (int)typeof(RaceSimulation).GetMethod("GetLapCount", Private)!
            .Invoke(null, [track, route, DefaultLapCount, null])!;
        var routes = new RaceRoutes(track, route);
        var navigator = routes.MainNavigator;

        var text = new StringBuilder();
        text.Append($"# {id} race markers, exported from Kartster by KartsterExport (route {route.Id}). " +
                    "Format: Examples/Kartster/Race/RaceMarkers.cs\n");
        text.Append("# metres, planar: x = world X, y = world Z. forward is the gate's normal (along the road), " +
                    "left/right its reach\n");
        text.Append("# either side of the point, right = (-forward.y, forward.x). heading: 0 faces -Z, " +
                    "positive is turned left\n");
        text.Append($"laps {laps}\n");
        Gate(text, id, "start", RaceGatePlane.Create(track, navigator, route.StartFinish!));
        foreach (var checkpoint in route.Checkpoints)
            Gate(text, id, "checkpoint", RaceGatePlane.Create(track, navigator, checkpoint));

        // RaceSimulation.LayOutStartingGrid without a player list: its GetStartingLocation, and StartingGrid's shifts
        // for standard karts (which are zero).
        var simulation = new RaceSimulation(new PhysicsTuning());
        typeof(RaceSimulation).GetField("_raceRoutes", Private)!.SetValue(simulation, routes);
        var getLocation = typeof(RaceSimulation).GetMethod("GetStartingLocation", Private)!;
        var placed = new TrackStartingLocation[GridSlots];
        for (var index = 0; index < GridSlots; index++)
        {
            placed[index] = (TrackStartingLocation)getLocation.Invoke(simulation, [route, index, placed])!;
            var coordinate = new TrackCoordinate(placed[index].SectionId, placed[index].Distance,
                placed[index].LateralOffset);
            if (!navigator.TryResolve(coordinate, out var contact))
                throw new InvalidOperationException($"{id}: grid slot {index} is not on the route");
            var p = contact.WorldPosition;
            var f = contact.Frame.Forward;
            text.Append($"grid {p.X:0.###} {p.Z:0.###} {MathF.Round(MathF.Atan2(-f.X, -f.Z), 5) + 0f:0.#####}\n");
        }

        Console.WriteLine($"  race: route {route.Id} laps={laps} checkpoints={route.Checkpoints.Count} " +
                          $"authoredGrid={route.StartingLocations.Count}");
        return text.ToString();
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static void Gate(StringBuilder text, string id, string kind, RaceGatePlane gate)
    {
        var right = new Vector2(-gate.Normal.Z, gate.Normal.X);
        var check = Vector2.Dot(right, new Vector2(gate.Across.X, gate.Across.Z));
        if (check < 0.99f)
            throw new InvalidOperationException($"{id}: gate across is not (-forward.y, forward.x): {check}");
        text.Append($"{kind} {gate.Origin.X:0.###} {gate.Origin.Z:0.###} {gate.Normal.X:0.#####} " +
                    $"{gate.Normal.Z:0.#####} {gate.Left:0.###} {gate.Right:0.###}\n");
    }
}
