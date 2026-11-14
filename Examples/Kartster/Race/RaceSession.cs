using System;
using System.Collections.Generic;
using System.Numerics;

namespace Kartster.Race;

/// <summary>
/// One race, pure logic: the countdown, the clock, which gates each kart has passed, laps and lap times, who has
/// finished and the positions. Kartster's <c>RaceSession</c>, <c>RaceProgressTracker</c> and <c>RacePositions</c>
/// in one, for a closed route. Knows nothing of entities: karts are registered by an id and report where they are.
/// </summary>
/// <remarks>
/// Each fixed step: <see cref="Advance"/> once, <see cref="Update"/> for every kart, then
/// <see cref="UpdatePositions"/>. <see cref="RaceSystem"/> does exactly that. Nothing here stops a kart: whoever
/// drives the karts asks <see cref="CanDrive"/> (false during the countdown) and holds them still.
/// </remarks>
public sealed class RaceSession
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Kartster's <c>RaceSession.CountdownSeconds</c>: "3 - 2 - 1".</summary>
    public const float CountdownSeconds = 3f;

    /// <summary>Kartster's <c>RaceProgressTracker.MaxStepMeters</c>: a longer step is not driving.</summary>
    public const float MaxStepMeters = 10f;

    private readonly RaceGate _start;
    private readonly RaceGate[] _checkpoints;
    private readonly Dictionary<int, int> _indexById = [];
    private Racer[] _racers = [];
    private int[] _order = []; // racer indices from the leader back
    private readonly float[] _firstAtGate; // race time the first kart reached each gate ordinal, -1 before
    private int _finishedCount;

    //**********************************************************
    //** ctor
    //**********************************************************

    public RaceSession(RaceMarkers markers) : this(markers.Start, markers.Checkpoints, markers.LapCount) { }

    public RaceSession(RaceGate start, RaceGate[] checkpoints, int lapCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(lapCount, 1);
        _start = start;
        _checkpoints = checkpoints;
        LapCount = lapCount;
        _firstAtGate = new float[lapCount * (checkpoints.Length + 1) + 1];
        Array.Fill(_firstAtGate, -1f);
    }

    //**********************************************************
    //** props
    //**********************************************************

    public RacePhase Phase { get; private set; } = RacePhase.Countdown;

    /// <summary>Whether karts may drive: false during the countdown. A finished kart may still drive.</summary>
    public bool CanDrive => Phase != RacePhase.Countdown;

    /// <summary>Seconds left of the countdown, 0 once racing.</summary>
    public float CountdownRemainingSeconds { get; private set; } = CountdownSeconds;

    /// <summary>Seconds since the countdown ended; frozen once finished.</summary>
    public float RaceTimeSeconds { get; private set; }

    public int LapCount { get; }
    public int CheckpointCount => _checkpoints.Length;
    public int Count => _racers.Length;
    public int FinishedCount => _finishedCount;

    /// <summary>A bots-only race ends when everybody has finished, not when one is left (Kartster's section runs).</summary>
    public bool WaitsForEverybody { get; set; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Adds a kart on the grid. Allocates; do it before the race.</summary>
    /// <param name="isHuman">A person: with one in the race, it waits for every racer to finish.</param>
    public void Register(int id, bool isHuman = false)
    {
        if (!_indexById.TryAdd(id, _racers.Length))
            throw new ArgumentException($"Kart {id} is already registered with the race", nameof(id));

        Array.Resize(ref _racers, _racers.Length + 1);
        Array.Resize(ref _order, _order.Length + 1);
        _racers[^1] = new Racer { Id = id, IsHuman = isHuman, CurrentLap = 1, Position = _racers.Length };
        _order[^1] = _racers.Length - 1;
    }

    public bool IsRegistered(int id) => _indexById.ContainsKey(id);

    public ref readonly Racer Get(int id) => ref _racers[IndexOf(id)];

    /// <summary>The racer in <paramref name="position"/> (1 = leader) at the last <see cref="UpdatePositions"/>.</summary>
    public ref readonly Racer GetAtPosition(int position) => ref _racers[_order[position - 1]];

    /// <summary>
    /// Seconds <paramref name="id"/> is behind the kart that reached its last gate first, as Kartster's gaps per
    /// gate: finished karts by finish time, 0 before the first gate.
    /// </summary>
    public float GapToLeader(int id)
    {
        ref readonly var racer = ref _racers[IndexOf(id)];
        var ordinal = racer.HasFinished ? _firstAtGate.Length - 1 : GatesPassed(racer);
        var seconds = racer.HasFinished ? racer.FinishTimeSeconds : racer.GateSeconds;
        return (uint)ordinal < (uint)_firstAtGate.Length && _firstAtGate[ordinal] >= 0f
            ? seconds - _firstAtGate[ordinal]
            : 0f;
    }

    /// <summary>
    /// Runs the countdown or the clock. The race starts at 0 on the step the countdown runs out, without the
    /// overshoot, as Kartster's does.
    /// </summary>
    public void Advance(float seconds)
    {
        switch (Phase)
        {
            case RacePhase.Countdown:
                CountdownRemainingSeconds -= seconds;
                if (CountdownRemainingSeconds <= 0f)
                {
                    CountdownRemainingSeconds = 0f;
                    Phase = RacePhase.Racing;
                    RaceTimeSeconds = 0f;
                    for (var i = 0; i < _racers.Length; i++)
                        _racers[i].LapStartSeconds = 0f;
                }

                break;
            case RacePhase.Racing:
                RaceTimeSeconds += seconds;
                break;
        }
    }

    /// <summary>
    /// The kart moved from <paramref name="previous"/> to <paramref name="position"/> (x = world X, y = world Z)
    /// this step, at <paramref name="height"/> (world Y, for the gates' <see cref="RaceGate.Bottom"/> and
    /// <see cref="RaceGate.Top"/>): counts the gate it went through, and a lap or the finish.
    /// </summary>
    public void Update(int id, Vector2 previous, Vector2 position, float height = 0f)
    {
        ref var racer = ref _racers[IndexOf(id)];
        if (Vector2.DistanceSquared(previous, position) <= MaxStepMeters * MaxStepMeters)
            PassGates(ref racer, previous, position, height);

        racer.DistanceToNextGate = Vector2.Distance(position, NextGate(racer).Point);
        CountLap(ref racer);
    }

    /// <summary>
    /// Ranks the karts: finished ones first in finishing order, then by gates passed (laps, then checkpoints), then by
    /// distance to the next gate. Level karts keep registration order.
    /// </summary>
    public void UpdatePositions()
    {
        // Insertion sort from registration order: stable, allocation free, and a field is a handful of karts.
        for (var i = 0; i < _order.Length; i++)
        {
            var index = i;
            var j = i - 1;
            for (; j >= 0 && IsAhead(_racers[index], _racers[_order[j]]); j--)
                _order[j + 1] = _order[j];
            _order[j + 1] = index;
        }

        for (var rank = 0; rank < _order.Length; rank++)
            _racers[_order[rank]].Position = rank + 1;
    }

    /// <summary>Ends the race now; whoever has not finished stays unfinished (DNF). Kartster's <c>Stop</c>.</summary>
    public void Stop()
    {
        if (Phase == RacePhase.Racing)
            Phase = RacePhase.Finished;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private int IndexOf(int id)
    {
        if (!_indexById.TryGetValue(id, out var index))
            ThrowNotRegistered(id);
        return index;
    }

    private static void ThrowNotRegistered(int id) =>
        throw new KeyNotFoundException($"Kart {id} is not registered with the race");

    private RaceGate NextGate(in Racer racer) =>
        racer.LapStarted && racer.NextCheckpointIndex < _checkpoints.Length
            ? _checkpoints[racer.NextCheckpointIndex]
            : _start;

    /// <summary>Kartster's <c>RaceProgressTracker.Advance</c> for a loop.</summary>
    private void PassGates(ref Racer racer, Vector2 from, Vector2 to, float height)
    {
        var count = _checkpoints.Length;
        if (!racer.LapStarted)
        {
            if (_start.Cross(from, to, height) > 0)
            {
                racer.LapStarted = true;
                racer.NextCheckpointIndex = 0;
                ReachGate(ref racer);
            }

            return;
        }

        var next = racer.NextCheckpointIndex;
        if (NextGate(racer).Cross(from, to, height) > 0)
        {
            if (next < count)
            {
                racer.NextCheckpointIndex++;
            }
            else
            {
                racer.CompletedLaps++;
                racer.NextCheckpointIndex = 0;
            }

            ReachGate(ref racer);
            return;
        }

        // Back through the last gate passed takes it back.
        if (next > 0)
        {
            if (_checkpoints[next - 1].Cross(from, to, height) < 0)
            {
                racer.NextCheckpointIndex--;
                return;
            }
        }
        else if (racer.CompletedLaps > 0)
        {
            if (_start.Cross(from, to, height) < 0)
            {
                racer.CompletedLaps--;
                racer.NextCheckpointIndex = count;
                return;
            }
        }
        else if (_start.Cross(from, to, height) < 0)
        {
            racer.LapStarted = false;
            return;
        }

        // Through the gate after next with the next still owed: it went round a checkpoint.
        var afterNext = next + 1 < count ? _checkpoints[next + 1]
            : next + 1 == count ? _start
            : count > 0 ? _checkpoints[0]
            : (RaceGate?)null;
        if (afterNext is { } gate && gate.Cross(from, to, height) > 0)
            racer.MissedCheckpoints++;
    }

    /// <summary>Notes when the kart got to the gate it just passed, and the first kart's time there.</summary>
    private void ReachGate(ref Racer racer)
    {
        if (Phase != RacePhase.Racing)
            return;

        racer.GateSeconds = RaceTimeSeconds;
        var ordinal = GatesPassed(racer);
        if ((uint)ordinal < (uint)_firstAtGate.Length && _firstAtGate[ordinal] < 0f)
            _firstAtGate[ordinal] = RaceTimeSeconds;
    }

    /// <summary>Kartster's <c>RaceSession.ReportProgress</c>: a lap lands only when the count goes up.</summary>
    private void CountLap(ref Racer racer)
    {
        if (Phase != RacePhase.Racing || racer.HasFinished || racer.CompletedLaps <= racer.ReportedLaps)
            return;

        racer.ReportedLaps = racer.CompletedLaps;
        var lapSeconds = RaceTimeSeconds - racer.LapStartSeconds;
        racer.LapStartSeconds = RaceTimeSeconds;
        racer.LastLapSeconds = lapSeconds;
        if (racer.BestLapSeconds <= 0f || lapSeconds < racer.BestLapSeconds)
            racer.BestLapSeconds = lapSeconds;

        if (racer.CompletedLaps < LapCount)
        {
            racer.CurrentLap = racer.CompletedLaps + 1;
            return;
        }

        racer.CurrentLap = LapCount;
        racer.HasFinished = true;
        racer.FinishTimeSeconds = RaceTimeSeconds;
        racer.FinishPosition = ++_finishedCount;
        FinishIfEverybodyIsDone();
    }

    /// <summary>
    /// Kartster's <c>FinishIfEverybodyIsDone</c>: with a person in it the race ends when every racer has finished;
    /// bots only, when one is left racing (none in a race of one, or with <see cref="WaitsForEverybody"/>).
    /// </summary>
    private void FinishIfEverybodyIsDone()
    {
        var stillRacing = _racers.Length - _finishedCount;
        var anyHuman = false;
        for (var i = 0; i < _racers.Length; i++)
            anyHuman |= _racers[i].IsHuman;

        var allowedStillRacing = anyHuman || _racers.Length == 1 || WaitsForEverybody ? 0 : 1;
        if (stillRacing <= allowedStillRacing)
            Phase = RacePhase.Finished;
    }

    /// <summary>Whether <paramref name="a"/> is ahead of <paramref name="b"/>.</summary>
    private bool IsAhead(in Racer a, in Racer b)
    {
        if (a.HasFinished || b.HasFinished)
            return a.HasFinished && (!b.HasFinished || a.FinishPosition < b.FinishPosition);

        var gatesA = GatesPassed(a);
        var gatesB = GatesPassed(b);
        return gatesA != gatesB ? gatesA > gatesB : a.DistanceToNextGate < b.DistanceToNextGate;
    }

    /// <summary>Kartster's <c>RaceGate.GetOrdinal</c>: gates passed since the first start crossing, -1 before.</summary>
    private int GatesPassed(in Racer racer) =>
        racer.LapStarted ? racer.CompletedLaps * (_checkpoints.Length + 1) + racer.NextCheckpointIndex : -1;
}
