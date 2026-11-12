using System;
using NUnit.Framework;
using Vikingferd.Components;
using Vikingferd.Puzzle;

namespace Vikingferd.Tests;

/// <summary>
/// Plays a level through <see cref="PuzzleRules.Step"/> with scripted moves instead of the keyboard, the way a
/// player would: walk somewhere, jump, press E. Every move steps the whole level (the other heroes fall, crates
/// drop) and fails the test if a hero is knocked out.
/// </summary>
internal sealed class Party
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const float StepSeconds = 1f / 120f;

    private const float CloseEnough = .05f;
    private const int MaxSteps = (int)(15f / StepSeconds);

    //**********************************************************
    //** ctor
    //**********************************************************

    public Party(string[] rows)
    {
        Stage = Stage.Parse(rows, out var spawns);
        Actors = new Actor[spawns.Length];
        for (var i = 0; i < spawns.Length; i++)
            Actors[i] = Heroes.Create(spawns[i]);
        Settle();
    }

    //**********************************************************
    //** props
    //**********************************************************

    public Stage Stage { get; }
    public Actor[] Actors { get; }
    public StepResult Result { get; private set; } = StepResult.Playing;

    /// <summary>When false, a knocked-out hero ends the move quietly instead of failing the test.</summary>
    public bool FailOnKnockout { get; init; } = true;

    //**********************************************************
    //** public methods
    //**********************************************************

    public ref Actor Get(ActorKind kind)
    {
        for (var i = 0; i < Actors.Length; i++)
        {
            if (Actors[i].Kind == kind)
                return ref Actors[i];
        }

        throw new ArgumentException($"No {kind} in this level.");
    }

    /// <summary>The first crate in map order.</summary>
    public ref Actor Crate() => ref Get(ActorKind.Crate);

    public static float CenterX(in Actor actor) => actor.Min.X + actor.Size.X / 2f;

    /// <summary>Steps until the result changes from playing, or <paramref name="count"/> steps passed.</summary>
    public void Step(int count = 1)
    {
        for (var i = 0; i < count && Result.Outcome == Outcome.Playing; i++)
        {
            Result = PuzzleRules.Step(Stage, Actors, StepSeconds);
            if (FailOnKnockout && Result.Outcome == Outcome.Failed)
                Assert.Fail($"{Result.Hero} was knocked out ({Result.Cause}).");
        }
    }

    public void Wait(float seconds) => Step((int)(seconds / StepSeconds));

    /// <summary>
    /// Runs towards a centre x, like holding a direction key and letting go near the target. Stops there, when
    /// blocked (no progress for half a second), or when the level ends. <paramref name="glide"/> holds jump all the
    /// way (Sigrid glides).
    /// </summary>
    public void WalkTo(ActorKind kind, float centerX, bool glide = false)
    {
        var stuckSteps = 0;
        var last = CenterX(Get(kind));
        for (var i = 0; i < MaxSteps && Result.Outcome == Outcome.Playing; i++)
        {
            ref var hero = ref Get(kind);
            var distance = centerX - CenterX(hero);
            if (MathF.Abs(distance) < CloseEnough && hero.OnGround)
                break;

            hero.Run = MathF.Abs(distance) < CloseEnough ? 0 : MathF.Sign(distance);
            hero.JumpHeld = glide;
            Step();

            var now = CenterX(Get(kind));
            stuckSteps = MathF.Abs(now - last) < .0001f && Get(kind).OnGround ? stuckSteps + 1 : 0;
            last = now;
            if (stuckSteps > 60)
                break;
        }

        Release(kind);
        Settle();
    }

    /// <summary>Jumps while running in a direction (-1, 0 or 1) and keeps running until landed.</summary>
    public void Jump(ActorKind kind, int direction = 0)
    {
        ref var hero = ref Get(kind);
        hero.Run = direction;
        hero.JumpRequested = true;
        Step();

        for (var i = 0; i < MaxSteps && Result.Outcome == Outcome.Playing && !Get(kind).OnGround; i++)
        {
            Get(kind).Run = direction;
            Step();
        }

        Release(kind);
        Settle();
    }

    /// <summary>Presses E.</summary>
    public void Use(ActorKind kind)
    {
        Get(kind).UseRequested = true;
        Step();
    }

    /// <summary>Turns a hero to face a direction without moving them noticeably.</summary>
    public void Face(ActorKind kind, int direction) => Get(kind).Facing = direction;

    //**********************************************************
    //** internals
    //**********************************************************

    private void Release(ActorKind kind)
    {
        ref var hero = ref Get(kind);
        hero.Run = 0;
        hero.JumpHeld = false;
    }

    /// <summary>Lets everything come to rest.</summary>
    private void Settle() => Wait(.3f);
}
