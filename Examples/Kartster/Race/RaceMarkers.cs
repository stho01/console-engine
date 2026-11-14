using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;

namespace Kartster.Race;

/// <summary>
/// A track's race markers, from <c>&lt;id&gt;.race.txt</c>: exported from Kartster with its own code (the route
/// <c>RaceSimulation.SelectRoute</c> races, <c>RaceGatePlane.Create</c> for each gate, <c>GetStartingLocation</c>
/// for the grid, <c>GetLapCount</c> for the laps). Only closed routes (loops).
/// </summary>
/// <remarks>
/// One marker per line, numbers in metres with a '.' decimal point, planar: x = world X, y = world Z. Blank lines and
/// lines starting with '#' are skipped.
/// <code>
/// laps &lt;count&gt;                                       once, at least 1
/// start x y forwardX forwardY left right              once: the start/finish line
/// checkpoint x y forwardX forwardY left right         any number, in route order
/// grid x y heading                                    any number, in grid order (front first)
/// </code>
/// A gate's forward is along the road (normalised on load); left and right are how far it reaches either side of
/// its point, with right = (-forwardY, forwardX). A heading is 0 facing -Z and grows turning left.
/// </remarks>
public sealed class RaceMarkers
{
    //**********************************************************
    //** ctor
    //**********************************************************

    public RaceMarkers(int lapCount, RaceGate start, RaceGate[] checkpoints, GridSlot[] grid)
    {
        LapCount = lapCount;
        Start = start;
        Checkpoints = checkpoints;
        Grid = grid;
    }

    //**********************************************************
    //** props
    //**********************************************************

    public int LapCount { get; }
    public RaceGate Start { get; }
    public RaceGate[] Checkpoints { get; }
    public GridSlot[] Grid { get; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Loads <c>&lt;id&gt;.race.txt</c> from <paramref name="folder"/>.</summary>
    public static RaceMarkers Load(string folder, string id)
    {
        var fileName = id + ".race.txt";
        return Parse(File.ReadAllLines(Path.Combine(folder, fileName)), fileName);
    }

    /// <param name="lines">The file's lines.</param>
    /// <param name="source">Names the file in error messages.</param>
    /// <exception cref="InvalidDataException">A line is not a marker, or a marker is missing or doubled.</exception>
    public static RaceMarkers Parse(IReadOnlyList<string> lines, string source)
    {
        int? laps = null;
        RaceGate? start = null;
        var checkpoints = new List<RaceGate>();
        var grid = new List<GridSlot>();

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index].Trim();
            if (line.Length == 0 || line[0] == '#')
                continue;

            var at = $"{source}:{index + 1}";
            var words = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            switch (words[0])
            {
                case "laps":
                    var count = Numbers(words, 1, at)[0];
                    if (laps is not null)
                        throw new InvalidDataException($"{at}: 'laps' is given twice");
                    if (count < 1 || count != MathF.Floor(count))
                        throw new InvalidDataException($"{at}: laps must be a whole number of at least 1");
                    laps = (int)count;
                    break;
                case "start":
                    if (start is not null)
                        throw new InvalidDataException($"{at}: 'start' is given twice");
                    start = Gate(words, at);
                    break;
                case "checkpoint":
                    checkpoints.Add(Gate(words, at));
                    break;
                case "grid":
                    var slot = Numbers(words, 3, at);
                    grid.Add(new GridSlot(new Vector2(slot[0], slot[1]), slot[2]));
                    break;
                default:
                    throw new InvalidDataException($"{at}: unknown marker '{words[0]}'");
            }
        }

        if (laps is null)
            throw new InvalidDataException($"{source}: no 'laps' line");
        if (start is null)
            throw new InvalidDataException($"{source}: no 'start' line");

        return new RaceMarkers(laps.Value, start.Value, checkpoints.ToArray(), grid.ToArray());
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static RaceGate Gate(string[] words, string at)
    {
        var numbers = Numbers(words, 6, at);
        var forward = new Vector2(numbers[2], numbers[3]);
        if (forward.LengthSquared() < 1e-6f)
            throw new InvalidDataException($"{at}: '{words[0]}' needs a forward direction, not zero");
        if (numbers[4] < 0f || numbers[5] < 0f)
            throw new InvalidDataException($"{at}: '{words[0]}' needs left and right of 0 or more");

        return new RaceGate(new Vector2(numbers[0], numbers[1]), Vector2.Normalize(forward), numbers[4], numbers[5]);
    }

    private static float[] Numbers(string[] words, int count, string at)
    {
        if (words.Length != count + 1)
            throw new InvalidDataException($"{at}: '{words[0]}' needs {count} numbers, got {words.Length - 1}");

        var numbers = new float[count];
        for (var i = 0; i < count; i++)
        {
            if (!float.TryParse(words[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[i])
                || !float.IsFinite(numbers[i]))
                throw new InvalidDataException($"{at}: '{words[i + 1]}' is not a number");
        }

        return numbers;
    }
}
