using System;

namespace SkyStack.Climbing;

/// <summary>
/// Which world rows are on screen. The view starts still and begins to rise once the climber has reached
/// <see cref="StartFloor"/>; every <see cref="LevelSeconds"/> it rises faster, up to <see cref="MaxLevel"/>. A
/// climber higher than <see cref="CatchUpRows"/> below the top edge pulls the view up with it. A climber whose head is
/// below the bottom edge has lost (<see cref="HasLost"/>).
/// </summary>
public sealed class Scroller
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int StartFloor = 5;

    /// <summary>Rows/s at level 1.</summary>
    public const float BaseSpeed = 2.5f;

    /// <summary>Rows/s added per level above 1.</summary>
    public const float SpeedPerLevel = 1.5f;

    public const float LevelSeconds = 30f;
    public const int MaxLevel = 6;

    /// <summary>The climber's feet stay at least this many rows below the top edge.</summary>
    public const int CatchUpRows = 12;

    /// <summary>How long a "faster" notice shows after the level went up.</summary>
    public const float NoticeSeconds = 2f;

    private float _levelSeconds;

    //**********************************************************
    //** ctor
    //**********************************************************

    /// <param name="top">The world row at the top of the screen.</param>
    public Scroller(float top) => Top = top;

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>The world row at the top edge of the screen.</summary>
    public float Top { get; private set; }

    /// <summary>0 until the view starts rising, then 1 to <see cref="MaxLevel"/>.</summary>
    public int Level { get; private set; }

    public bool IsRising => Level > 0;

    /// <summary>Rows/s the view rises by itself.</summary>
    public float Speed => IsRising ? BaseSpeed + (Level - 1) * SpeedPerLevel : 0f;

    /// <summary>Time left to show that the view started or got faster.</summary>
    public float NoticeSecondsLeft { get; private set; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <param name="deltaSeconds">Time since the last update.</param>
    /// <param name="climberRow">World row of the climber's feet.</param>
    /// <param name="highestFloor">The highest floor the climber has landed on.</param>
    public void Update(float deltaSeconds, float climberRow, int highestFloor)
    {
        NoticeSecondsLeft = MathF.Max(0f, NoticeSecondsLeft - deltaSeconds);

        if (!IsRising && highestFloor >= StartFloor)
        {
            Level = 1;
            NoticeSecondsLeft = NoticeSeconds;
        }

        if (IsRising)
        {
            _levelSeconds += deltaSeconds;
            if (_levelSeconds >= LevelSeconds && Level < MaxLevel)
            {
                _levelSeconds -= LevelSeconds;
                Level++;
                NoticeSecondsLeft = NoticeSeconds;
            }

            Top -= Speed * deltaSeconds;
        }

        Top = MathF.Min(Top, climberRow - CatchUpRows);
    }

    /// <summary>True when the whole climber is below the bottom edge of the screen.</summary>
    public bool HasLost(float climberRow) => climberRow - ClimbPhysics.BodyHeight >= Top + Layout.ScreenHeight;
}
