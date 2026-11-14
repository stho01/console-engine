using System;

namespace Kartster.Modes;

/// <summary>
/// One lap's poses, one per fixed step from the step the lap began, in a preallocated array: <see cref="Add"/>
/// allocates nothing. A lap longer than <see cref="Capacity"/> steps stops recording and is marked
/// <see cref="Overflowed"/> (it can't be a ghost).
/// </summary>
public sealed class GhostLap(int capacity)
{
    //**********************************************************
    //** fields
    //**********************************************************

    private readonly GhostPose[] _poses = new GhostPose[capacity];

    //**********************************************************
    //** props
    //**********************************************************

    public int Capacity => _poses.Length;
    public int Count { get; private set; }

    /// <summary>The lap's time as the race counted it; 0 until the lap is done.</summary>
    public float LapSeconds { get; set; }

    public bool Overflowed { get; private set; }

    public GhostPose this[int step] => _poses[step];

    public ReadOnlySpan<GhostPose> Poses => _poses.AsSpan(0, Count);

    //**********************************************************
    //** public methods
    //**********************************************************

    public void Add(GhostPose pose)
    {
        if (Count == _poses.Length)
        {
            Overflowed = true;
            return;
        }

        _poses[Count++] = pose;
    }

    public void Clear()
    {
        Count = 0;
        LapSeconds = 0f;
        Overflowed = false;
    }
}
