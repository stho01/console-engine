using Vikingferd.Components;

namespace Vikingferd.Puzzle;

public enum Outcome : byte
{
    Playing,

    /// <summary>All three heroes stand on the exit.</summary>
    Won,

    /// <summary>A hero was knocked out: the level has to be restarted (R).</summary>
    Failed
}

/// <summary>Why a hero was knocked out.</summary>
public enum FailCause : byte
{
    None,

    /// <summary>Landed more than <see cref="PuzzleRules.FatalDrop"/> below where they last stood, without gliding.</summary>
    Fell,

    Spikes
}

/// <summary>The result of one <see cref="PuzzleRules.Step"/>. <see cref="Hero"/> and <see cref="Cause"/> are set when failed.</summary>
public readonly record struct StepResult(Outcome Outcome, ActorKind Hero, FailCause Cause)
{
    public static readonly StepResult Playing = new(Outcome.Playing, ActorKind.Ylva, FailCause.None);
    public static readonly StepResult Won = new(Outcome.Won, ActorKind.Ylva, FailCause.None);

    public static StepResult Failed(ActorKind hero, FailCause cause) => new(Outcome.Failed, hero, cause);
}
