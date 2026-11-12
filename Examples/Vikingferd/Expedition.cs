using Vikingferd.Components;
using Vikingferd.Puzzle;

namespace Vikingferd;

/// <summary>
/// The state of one attempt at a level, shared by the level's systems: the stage, which hero the player controls,
/// this frame's input and how the attempt ended. A restart builds a new one.
/// </summary>
public sealed class Expedition(int levelIndex, Stage stage)
{
    //**********************************************************
    //** props
    //**********************************************************

    public int LevelIndex { get; } = levelIndex;
    public Level Level => Levels.All[LevelIndex];
    public Stage Stage { get; } = stage;
    public bool IsLastLevel => LevelIndex == Levels.All.Length - 1;

    /// <summary>The hero that takes the keyboard input.</summary>
    public ActorKind ActiveHero { get; private set; } = Heroes.Order[0];

    /// <summary>Read from the keyboard at the start of every frame's simulation.</summary>
    public PartyInput Input { get; set; }

    /// <summary>What the rules reported since the effects last read it.</summary>
    public PuzzleEvents Events { get; } = new();

    public Outcome Outcome { get; private set; }

    /// <summary>Who was knocked out, and how. Only meaningful when <see cref="Outcome"/> is failed.</summary>
    public ActorKind FailedHero { get; private set; }

    public FailCause FailCause { get; private set; }

    //**********************************************************
    //** public methods
    //**********************************************************

    public void SelectNextHero() =>
        ActiveHero = Heroes.Order[(Heroes.IndexOf(ActiveHero) + 1) % Heroes.Order.Length];

    public void SelectHero(int index)
    {
        if ((uint)index < (uint)Heroes.Order.Length)
            ActiveHero = Heroes.Order[index];
    }

    /// <summary>Records the first result that ends the attempt; later ones are ignored.</summary>
    public void Apply(StepResult result)
    {
        if (Outcome != Outcome.Playing || result.Outcome == Outcome.Playing)
            return;

        Outcome = result.Outcome;
        FailedHero = result.Hero;
        FailCause = result.Cause;
    }
}
