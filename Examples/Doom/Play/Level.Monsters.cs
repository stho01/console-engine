using System;
using ConsoleEngine.Ecs;
using Doom.Components;
using Doom.Maps;

namespace Doom.Play;

/// <summary>
/// The monster rules that damage and noise need. The AI itself runs in <see cref="Systems.MonsterSystem"/>.
/// </summary>
public sealed partial class Level
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Tics each walk frame shows.</summary>
    internal const int WalkFrameTics = 4;

    /// <summary>Reaction time of a monster that just woke, in tics.</summary>
    private const int WakeReactionTics = 8;

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>Wakes a monster: it goes after the player (unless it already has a target) and starts chasing.</summary>
    internal void Wake(Entity entity)
    {
        ref var monster = ref World.Get<Monster>(entity);
        if (monster.Target == Entity.Null && !Player.Dead)
            monster.Target = Player.Body;
        monster.ReactionTics = WakeReactionTics;
        StartChasing(ref World.Get<Thing>(entity), ref World.Get<Animation>(entity));
    }

    internal static void StartChasing(ref Thing thing, ref Animation animation)
    {
        thing.State = ThingState.Chase;
        Animator.Play(ref animation, ThingInfo.ById(thing.Type), AnimationClip.Walk, WalkFrameTics, true);
    }

    /// <summary>Whether a shot was heard in sector <paramref name="sector"/> this level.</summary>
    internal bool HeardSound(int sector) => _heardSound[sector];

    /// <summary>
    /// A shot wakes the monsters in every sector the sound reaches from <paramref name="sector"/>: through open
    /// two-sided lines, and through at most one line flagged as blocking sound.
    /// </summary>
    private void NoiseAlert(Sector sector)
    {
        _soundQueue.Clear();
        Array.Fill(_soundBlocks, int.MaxValue);
        _soundQueue.Enqueue((sector, 0));
        _soundBlocks[sector.Index] = 0;
        while (_soundQueue.Count > 0)
        {
            var (current, blocks) = _soundQueue.Dequeue();
            _heardSound[current.Index] = true;
            foreach (var line in current.Lines)
            {
                if (line.Back == null)
                    continue;

                var front = line.FrontSector;
                var back = line.BackSector;
                if (MathF.Min(front.CeilingHeight, back.CeilingHeight) <= MathF.Max(front.FloorHeight, back.FloorHeight))
                    continue;

                var next = front == current ? back : front;
                var nextBlocks = blocks + ((line.Flags & LineFlags.BlockSound) != 0 ? 1 : 0);
                if (nextBlocks > 1 || _soundBlocks[next.Index] <= nextBlocks)
                    continue;

                _soundBlocks[next.Index] = nextBlocks;
                _soundQueue.Enqueue((next, nextBlocks));
            }
        }
    }

    /// <summary>The signed difference a - b, wrapped to [-π, π].</summary>
    public static float AngleDifference(float a, float b)
    {
        var difference = (a - b) % (2 * MathF.PI);
        if (difference > MathF.PI)
            difference -= 2 * MathF.PI;
        if (difference < -MathF.PI)
            difference += 2 * MathF.PI;
        return difference;
    }
}
