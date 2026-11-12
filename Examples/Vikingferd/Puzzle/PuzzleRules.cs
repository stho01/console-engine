using System;
using Microsoft.Xna.Framework;
using Vikingferd.Components;

namespace Vikingferd.Puzzle;

/// <summary>
/// The whole game logic for one fixed step, on plain data so tests can play levels without ECS or keyboard: uses
/// (levers, smashing), hero movement (with pushing), crate falls, switches, then hazards and the exit.
/// <para>
/// Collision: boxes move one axis at a time and are clamped against solid tiles and other actors. Crates block
/// everything. Heroes block crates, but for other heroes they are one-way platforms: a hero falling onto another hero
/// lands on their head; sideways and upwards heroes pass through each other. Steps are short enough (at most
/// <see cref="MaxFallSpeed"/> x step) that nothing tunnels through a tile.
/// </para>
/// </summary>
public static class PuzzleRules
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Tiles/s².</summary>
    public const float Gravity = 40f;

    /// <summary>Tiles/s.</summary>
    public const float MaxFallSpeed = 22f;

    /// <summary>Sigrid's fall speed while gliding, tiles/s.</summary>
    public const float GlideFallSpeed = 3f;

    /// <summary>
    /// A hero landing more than this many tiles below where they last stood is knocked out, unless they land slower
    /// than <see cref="SafeLandingSpeed"/> (Sigrid gliding). Four tiles is safe, five is not.
    /// </summary>
    public const float FatalDrop = 4.5f;

    public const float SafeLandingSpeed = GlideFallSpeed + .5f;

    /// <summary>Overlaps and touches smaller than this don't count.</summary>
    public const float Epsilon = .001f;

    /// <summary>Tiles/s² towards the run speed. High on the ground, so heroes stop almost at once.</summary>
    private const float GroundAcceleration = 200f;

    private const float AirAcceleration = 35f;

    /// <summary>How far in front of Gorm a cracked tile can be and still be smashed, tiles.</summary>
    private const float SmashReach = .5f;

    /// <summary>A spike tile hurts in its lower part only, where the spikes are.</summary>
    private const float SpikeHeight = .5f;

    /// <summary>Landing faster than this (tiles/s) raises dust: anything more than a hop.</summary>
    private const float DustLandingSpeed = 8f;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// One fixed step. <paramref name="events"/>, if given, collects what happened (for effects); the rules never
    /// read it.
    /// </summary>
    public static StepResult Step(Stage stage, Span<Actor> actors, float deltaSeconds, PuzzleEvents events = null)
    {
        var result = StepResult.Playing;
        for (var i = 0; i < actors.Length; i++)
        {
            if (Heroes.IsHero(actors[i].Kind) && actors[i].UseRequested)
                Use(stage, ref actors[i], events);
        }

        for (var i = 0; i < actors.Length; i++)
        {
            if (!Heroes.IsHero(actors[i].Kind))
                continue;

            if (!MoveHero(stage, actors, i, deltaSeconds, events) && result.Outcome == Outcome.Playing)
                result = StepResult.Failed(actors[i].Kind, FailCause.Fell);
        }

        for (var i = 0; i < actors.Length; i++)
        {
            if (!Heroes.IsHero(actors[i].Kind))
                MoveCrate(stage, actors, i, deltaSeconds, events);
        }

        stage.UpdateSwitches(actors, events);
        if (result.Outcome == Outcome.Playing)
            result = CheckHazardsAndExit(stage, actors);

        if (result.Outcome == Outcome.Failed && events != null)
        {
            for (var i = 0; i < actors.Length; i++)
            {
                if (actors[i].Kind == result.Hero)
                    events.Add(PuzzleEventKind.KnockedOut, actors[i].Min + new Vector2(actors[i].Size.X / 2f, 0f),
                        result.Hero);
            }
        }

        return result;
    }

    //**********************************************************
    //** internals: heroes
    //**********************************************************

    /// <summary>Lever under the hero first; otherwise Gorm smashes the cracked stone in front of him.</summary>
    private static void Use(Stage stage, ref Actor hero, PuzzleEvents events)
    {
        hero.UseRequested = false;
        var first = Cell(hero.Min.X + Epsilon);
        var last = Cell(hero.Min.X + hero.Size.X - Epsilon);
        var top = Cell(hero.Min.Y + Epsilon);
        var bottom = Cell(hero.Min.Y + hero.Size.Y - Epsilon);
        for (var row = top; row <= bottom; row++)
        for (var column = first; column <= last; column++)
        {
            if (!stage.ToggleLever(column, row))
                continue;

            events?.Add(PuzzleEventKind.LeverPulled, TileCenter(column, row), hero.Kind, stage.ChannelAt(column, row));
            return;
        }

        if (!Heroes.Of(hero.Kind).CanSmash)
            return;

        var front = hero.Facing > 0 ? Cell(hero.Min.X + hero.Size.X + SmashReach) : Cell(hero.Min.X - SmashReach);
        for (var row = top; row <= bottom; row++)
        {
            if (stage.Smash(front, row))
                events?.Add(PuzzleEventKind.Smashed, TileCenter(front, row), hero.Kind);
        }
    }

    /// <summary>Runs, jumps, glides and moves one hero. Returns false if the hero landed from a fatal drop.</summary>
    private static bool MoveHero(Stage stage, Span<Actor> actors, int index, float deltaSeconds, PuzzleEvents events)
    {
        ref var hero = ref actors[index];
        var stats = Heroes.Of(hero.Kind);

        var acceleration = hero.OnGround ? GroundAcceleration : AirAcceleration;
        hero.Velocity.X = Approach(hero.Velocity.X, hero.Run * stats.RunSpeed, acceleration * deltaSeconds);
        if (hero.Run != 0)
            hero.Facing = hero.Run;

        if (hero.JumpRequested && hero.OnGround)
        {
            hero.Velocity.Y = -stats.JumpSpeed;
            events?.Add(PuzzleEventKind.Jumped, Feet(hero), hero.Kind);
        }

        hero.JumpRequested = false;

        hero.Velocity.Y = MathF.Min(hero.Velocity.Y + Gravity * deltaSeconds, MaxFallSpeed);
        hero.Gliding = stats.CanGlide && hero.JumpHeld && !hero.OnGround && hero.Velocity.Y > 0f;
        if (hero.Gliding)
            hero.Velocity.Y = MathF.Min(hero.Velocity.Y, GlideFallSpeed);

        var wasOnGround = hero.OnGround;
        var landingSpeed = hero.Velocity.Y;
        MoveX(stage, actors, index, hero.Velocity.X * deltaSeconds);
        MoveY(stage, actors, index, hero.Velocity.Y * deltaSeconds);
        if (!hero.OnGround)
            return true;

        hero.Gliding = false;
        if (!wasOnGround && landingSpeed > DustLandingSpeed)
            events?.Add(PuzzleEventKind.Landed, Feet(hero), hero.Kind);

        var bottom = hero.Min.Y + hero.Size.Y;
        var fatal = !wasOnGround && bottom - hero.LastGroundY > FatalDrop && landingSpeed > SafeLandingSpeed;
        hero.LastGroundY = bottom;
        return !fatal;
    }

    //**********************************************************
    //** internals: crates
    //**********************************************************

    private static void MoveCrate(Stage stage, Span<Actor> actors, int index, float deltaSeconds, PuzzleEvents events)
    {
        ref var crate = ref actors[index];
        crate.Velocity.X = 0f;
        crate.Velocity.Y = MathF.Min(crate.Velocity.Y + Gravity * deltaSeconds, MaxFallSpeed);
        var landingSpeed = crate.Velocity.Y;
        MoveY(stage, actors, index, crate.Velocity.Y * deltaSeconds);
        if (crate.OnGround && landingSpeed > DustLandingSpeed)
            events?.Add(PuzzleEventKind.CrateLanded, Feet(crate));
    }

    /// <summary>
    /// Gorm pushes a crate by the overlap he moved into it. A pushed crate whose centre is over a free one-tile gap
    /// lines up with it, so it drops in (a crate is exactly one tile wide and would otherwise slide over the hole).
    /// </summary>
    private static void Push(Stage stage, Span<Actor> actors, int crateIndex, float distance)
    {
        MoveX(stage, actors, crateIndex, distance);

        ref var crate = ref actors[crateIndex];
        var column = Cell(crate.Min.X + crate.Size.X / 2f);
        var below = (int)MathF.Round(crate.Min.Y + crate.Size.Y);
        if (crate.Min.X == column || stage.IsSolid(column, below))
            return;

        var from = crate.Min.X;
        crate.Min.X = column;
        if (IsBlocked(stage, actors, crateIndex))
            crate.Min.X = from;
    }

    //**********************************************************
    //** internals: movement
    //**********************************************************

    private static void MoveX(Stage stage, Span<Actor> actors, int index, float distance)
    {
        if (distance == 0f)
            return;

        ref var actor = ref actors[index];
        actor.Min.X += distance;
        var blocked = false;

        var column = distance > 0f ? Cell(actor.Min.X + actor.Size.X - Epsilon) : Cell(actor.Min.X + Epsilon);
        var top = Cell(actor.Min.Y + Epsilon);
        var bottom = Cell(actor.Min.Y + actor.Size.Y - Epsilon);
        for (var row = top; row <= bottom; row++)
        {
            if (!stage.IsSolid(column, row))
                continue;

            actor.Min.X = distance > 0f ? column - actor.Size.X : column + 1f;
            blocked = true;
            break;
        }

        var pushes = Heroes.IsHero(actor.Kind) && Heroes.Of(actor.Kind).CanPush;
        for (var j = 0; j < actors.Length; j++)
        {
            if (j == index || !BlocksSideways(actor.Kind, actors[j].Kind) || !Overlaps(actor, actors[j]) ||
                !IsAhead(actor.Min.X, actor.Size.X, actors[j].Min.X, actors[j].Size.X, distance))
                continue;

            if (pushes && actors[j].Kind == ActorKind.Crate)
            {
                var overlap = distance > 0f
                    ? actor.Min.X + actor.Size.X - actors[j].Min.X
                    : actor.Min.X - (actors[j].Min.X + actors[j].Size.X);
                Push(stage, actors, j, overlap);
                if (!Overlaps(actor, actors[j]))
                    continue;
            }

            actor.Min.X = distance > 0f ? actors[j].Min.X - actor.Size.X : actors[j].Min.X + actors[j].Size.X;
            blocked = true;
        }

        if (blocked)
            actor.Velocity.X = 0f;
    }

    private static void MoveY(Stage stage, Span<Actor> actors, int index, float distance)
    {
        ref var actor = ref actors[index];
        actor.OnGround = false;
        if (distance == 0f)
            return;

        var previousBottom = actor.Min.Y + actor.Size.Y;
        actor.Min.Y += distance;
        var blocked = false;

        var row = distance > 0f ? Cell(actor.Min.Y + actor.Size.Y - Epsilon) : Cell(actor.Min.Y + Epsilon);
        var first = Cell(actor.Min.X + Epsilon);
        var last = Cell(actor.Min.X + actor.Size.X - Epsilon);
        for (var column = first; column <= last; column++)
        {
            if (!stage.IsSolid(column, row))
                continue;

            actor.Min.Y = distance > 0f ? row - actor.Size.Y : row + 1f;
            blocked = true;
            break;
        }

        for (var j = 0; j < actors.Length; j++)
        {
            if (j == index || !Overlaps(actor, actors[j]) ||
                !IsAhead(actor.Min.Y, actor.Size.Y, actors[j].Min.Y, actors[j].Size.Y, distance) ||
                !BlocksVertically(actor.Kind, actors[j], distance, previousBottom))
                continue;

            actor.Min.Y = distance > 0f ? actors[j].Min.Y - actor.Size.Y : actors[j].Min.Y + actors[j].Size.Y;
            blocked = true;
        }

        if (!blocked)
            return;

        actor.Velocity.Y = 0f;
        actor.OnGround = distance > 0f;
    }

    /// <summary>Crates block everyone sideways; heroes only block crates.</summary>
    private static bool BlocksSideways(ActorKind mover, ActorKind other) =>
        other == ActorKind.Crate || mover == ActorKind.Crate;

    /// <summary>Crates block everyone; a hero blocks crates, and a hero falling onto it from above.</summary>
    private static bool BlocksVertically(ActorKind mover, in Actor other, float distance, float previousBottom)
    {
        if (other.Kind == ActorKind.Crate || mover == ActorKind.Crate)
            return true;

        return distance > 0f && previousBottom <= other.Min.Y + Epsilon;
    }

    /// <summary>Does the actor overlap a solid tile or something that blocks it sideways?</summary>
    private static bool IsBlocked(Stage stage, Span<Actor> actors, int index)
    {
        ref readonly var actor = ref actors[index];
        var first = Cell(actor.Min.X + Epsilon);
        var last = Cell(actor.Min.X + actor.Size.X - Epsilon);
        var top = Cell(actor.Min.Y + Epsilon);
        var bottom = Cell(actor.Min.Y + actor.Size.Y - Epsilon);
        for (var row = top; row <= bottom; row++)
        for (var column = first; column <= last; column++)
        {
            if (stage.IsSolid(column, row))
                return true;
        }

        for (var j = 0; j < actors.Length; j++)
        {
            if (j != index && BlocksSideways(actor.Kind, actors[j].Kind) && Overlaps(actor, actors[j]))
                return true;
        }

        return false;
    }

    //**********************************************************
    //** internals: hazards and exit
    //**********************************************************

    private static StepResult CheckHazardsAndExit(Stage stage, ReadOnlySpan<Actor> actors)
    {
        var allAtExit = true;
        for (var i = 0; i < actors.Length; i++)
        {
            ref readonly var hero = ref actors[i];
            if (!Heroes.IsHero(hero.Kind))
                continue;

            if (TouchesSpikes(stage, hero))
                return StepResult.Failed(hero.Kind, FailCause.Spikes);

            var feetColumn = Cell(hero.Min.X + hero.Size.X / 2f);
            var feetRow = Cell(hero.Min.Y + hero.Size.Y - Epsilon);
            if (!hero.OnGround || stage.KindAt(feetColumn, feetRow) != TileKind.Exit)
                allAtExit = false;
        }

        return allAtExit ? StepResult.Won : StepResult.Playing;
    }

    private static bool TouchesSpikes(Stage stage, in Actor hero)
    {
        var first = Cell(hero.Min.X + Epsilon);
        var last = Cell(hero.Min.X + hero.Size.X - Epsilon);
        var top = Cell(hero.Min.Y + Epsilon);
        var bottom = Cell(hero.Min.Y + hero.Size.Y - Epsilon);
        for (var row = top; row <= bottom; row++)
        for (var column = first; column <= last; column++)
        {
            if (stage.KindAt(column, row) == TileKind.Spikes && hero.Min.Y + hero.Size.Y > row + 1f - SpikeHeight)
                return true;
        }

        return false;
    }

    //**********************************************************
    //** internals: helpers
    //**********************************************************

    private static bool Overlaps(in Actor a, in Actor b) =>
        a.Min.X < b.Min.X + b.Size.X - Epsilon && a.Min.X + a.Size.X > b.Min.X + Epsilon &&
        a.Min.Y < b.Min.Y + b.Size.Y - Epsilon && a.Min.Y + a.Size.Y > b.Min.Y + Epsilon;

    /// <summary>
    /// Is the other box in the direction of the move (centre beyond the mover's centre)? Only those are clamped
    /// against: a box the mover already overlapped from behind (a crate stopped by a wall while Gorm was still in
    /// it) must not throw the mover back past it.
    /// </summary>
    private static bool IsAhead(float min, float size, float otherMin, float otherSize, float distance) =>
        distance > 0f ? otherMin + otherSize / 2f > min + size / 2f : otherMin + otherSize / 2f < min + size / 2f;

    private static int Cell(float coordinate) => (int)MathF.Floor(coordinate);

    private static Vector2 TileCenter(int column, int row) => new(column + .5f, row + .5f);

    private static Vector2 Feet(in Actor actor) => new(actor.Min.X + actor.Size.X / 2f, actor.Min.Y + actor.Size.Y);

    private static float Approach(float value, float target, float maxChange) =>
        value < target ? MathF.Min(value + maxChange, target) : MathF.Max(value - maxChange, target);
}
