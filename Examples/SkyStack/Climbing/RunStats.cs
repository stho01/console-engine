using System;

namespace SkyStack.Climbing;

/// <summary>
/// Score and combos of one run. A landing that is <see cref="ComboMinFloors"/> or more floors above the last one is a
/// combo jump: it starts or extends a combo and refills the <see cref="ComboWindowSeconds"/> timer. Any other landing,
/// or the timer running out, ends the combo; a combo of at least <see cref="ComboMinJumps"/> jumps pays
/// <c>floors × jumps × ComboPointsFactor</c> points. The score is <c>HighestFloor × PointsPerFloor</c> plus all combo
/// points.
/// </summary>
public sealed class RunStats
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int PointsPerFloor = 10;
    public const int ComboMinFloors = 2;
    public const int ComboMinJumps = 2;
    public const int ComboPointsFactor = 5;
    public const float ComboWindowSeconds = 3f;

    /// <summary>How long the result of a finished combo stays on screen.</summary>
    public const float ComboResultSeconds = 2f;

    //**********************************************************
    //** props
    //**********************************************************

    public int HighestFloor { get; private set; }

    /// <summary>The floor of the last landing.</summary>
    public int LandedFloor { get; private set; }

    public int ComboPoints { get; private set; }

    public int Score => HighestFloor * PointsPerFloor + ComboPoints;

    /// <summary>Combo jumps of the running combo, 0 without one.</summary>
    public int ComboJumps { get; private set; }

    /// <summary>Floors climbed by the running combo.</summary>
    public int ComboFloors { get; private set; }

    public float ComboSecondsLeft { get; private set; }

    public bool IsComboRunning => ComboJumps > 0;

    /// <summary>Floors of the biggest combo that paid out this run.</summary>
    public int BestComboFloors { get; private set; }

    /// <summary>Floors and points of the last combo that paid out, shown while <see cref="ComboResultSecondsLeft"/>.</summary>
    public int LastComboFloors { get; private set; }

    public int LastComboPoints { get; private set; }

    public float ComboResultSecondsLeft { get; private set; }

    /// <summary>The climber fell off the screen. Nothing changes after that.</summary>
    public bool IsOver { get; private set; }

    /// <summary>Time since the run ended.</summary>
    public float SecondsSinceOver { get; private set; }

    //**********************************************************
    //** public methods
    //**********************************************************

    public void Land(int floor)
    {
        if (IsOver)
            return;

        var gained = floor - LandedFloor;
        LandedFloor = floor;
        if (floor > HighestFloor)
            HighestFloor = floor;

        if (gained >= ComboMinFloors)
        {
            ComboJumps++;
            ComboFloors += gained;
            ComboSecondsLeft = ComboWindowSeconds;
        }
        else
        {
            EndCombo();
        }
    }

    public void Tick(float deltaSeconds)
    {
        ComboResultSecondsLeft = MathF.Max(0f, ComboResultSecondsLeft - deltaSeconds);
        if (IsOver)
        {
            SecondsSinceOver += deltaSeconds;
            return;
        }

        if (!IsComboRunning)
            return;

        ComboSecondsLeft -= deltaSeconds;
        if (ComboSecondsLeft <= 0f)
            EndCombo();
    }

    /// <summary>Ends the run. A running combo still pays out.</summary>
    public void Finish()
    {
        if (IsOver)
            return;

        EndCombo();
        IsOver = true;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void EndCombo()
    {
        if (ComboJumps >= ComboMinJumps)
        {
            LastComboFloors = ComboFloors;
            LastComboPoints = ComboFloors * ComboJumps * ComboPointsFactor;
            ComboPoints += LastComboPoints;
            ComboResultSecondsLeft = ComboResultSeconds;
            BestComboFloors = Math.Max(BestComboFloors, ComboFloors);
        }

        ComboJumps = 0;
        ComboFloors = 0;
        ComboSecondsLeft = 0f;
    }
}
