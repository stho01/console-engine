using System;
using System.Diagnostics;
using ConsoleEngine.Ecs;
using Kartster.Scenes.Menus;
using Kartster.Ui;

namespace Kartster.Race;

/// <summary>
/// The end of a race once the player is done, as Kartster's <c>RaceSimulation.FinishRemaining</c>: the karts still
/// out are driven to the line by running the fixed step as fast as the frame budget allows, until every kart has
/// finished or the race clock reaches <see cref="CapSeconds"/>, which stops the race with the rest unfinished (DNF).
/// Meanwhile <see cref="Draw"/> covers the scene, where the karts jump about, with "FINISHING RACE" and "4/6 HOME".
/// </summary>
/// <param name="fixedStep">The race's fixed-step systems (driving, physics, <see cref="RaceSystem"/>).</param>
public sealed class RaceFinish(RaceSession session, SystemGroup fixedStep, float stepSeconds)
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const string Title = "FINISHING RACE";
    public const string Home = " HOME";

    /// <summary>Kartster's <c>FinishBotsStepsPerSlice</c>: steps between looks at the clock.</summary>
    public const int StepsPerSlice = 10;

    /// <summary>Kartster's <c>FinishBotsBudget</c>: wall-clock time spent per frame, milliseconds.</summary>
    public const double BudgetMilliseconds = 20;

    /// <summary>Kartster's <c>FinishBotsMaxSeconds</c>: the cap while nobody has won, race seconds.</summary>
    public const float MaxSeconds = 30f * 60f;

    /// <summary>Kartster's <c>FinishBotsWinnerFactor</c> and <c>FinishBotsSlackSeconds</c>.</summary>
    public const float WinnerFactor = 2f;
    public const float SlackSeconds = 30f;

    private readonly char[] _text = new char[16];

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Kartster's <c>GetFinishBotsCapSeconds</c>: the race time the karts left are run to at most, twice the winner's
    /// time plus <see cref="SlackSeconds"/>, or <see cref="MaxSeconds"/> while nobody has won.
    /// </summary>
    public static float CapSeconds(RaceSession session)
    {
        if (session.FinishedCount == 0)
            return MaxSeconds;

        var winner = session.GetAtPosition(1);
        return MathF.Min(MaxSeconds, winner.FinishTimeSeconds * WinnerFactor + SlackSeconds);
    }

    /// <summary>
    /// Runs up to <paramref name="steps"/> fixed steps, stopping the race at <see cref="CapSeconds"/>.
    /// </summary>
    /// <returns>True once the race is over.</returns>
    public bool Step(int steps)
    {
        for (var i = 0; i < steps; i++)
        {
            if (session.Phase != RacePhase.Racing)
                return true;

            if (session.RaceTimeSeconds >= CapSeconds(session))
            {
                session.Stop();
                session.UpdatePositions();
                return true;
            }

            fixedStep.Update(stepSeconds);
        }

        return session.Phase != RacePhase.Racing;
    }

    /// <summary>One frame of finishing: slices of <see cref="StepsPerSlice"/> for <see cref="BudgetMilliseconds"/>.</summary>
    /// <returns>True once the race is over.</returns>
    public bool Update()
    {
        var started = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(started).TotalMilliseconds < BudgetMilliseconds)
        {
            if (Step(StepsPerSlice))
                return true;
        }

        return false;
    }

    /// <summary>Kartster's <c>FinishingOverlayRenderer</c>, without the spinner: the menu backdrop and the count.</summary>
    public void Draw(IPixelTarget target)
    {
        MenuScreen.DrawBackdrop(target);
        var centerX = target.Width / 2;
        var y = target.Height / 2 - 14;
        PixelFont.DrawCentered(target, centerX, y, Title, UiColors.Selected, UiColors.TextShadow, 3);

        var length = TextFormat.Ratio(_text, session.FinishedCount, session.Count);
        length += TextFormat.Copy(_text.AsSpan(length), Home);
        PixelFont.DrawCentered(target, centerX, y + 24, _text.AsSpan(0, length), UiColors.Text, UiColors.TextShadow, 2);
    }
}
