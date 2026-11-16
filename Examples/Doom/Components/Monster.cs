using ConsoleEngine.Ecs;

namespace Doom.Components;

/// <summary>A monster's AI state. Its behaviour (speed, attack, frames) is <see cref="Play.ThingInfo.Monster"/>.</summary>
public struct Monster
{
    /// <summary>Who it is after, or <see cref="Entity.Null"/>.</summary>
    public Entity Target;

    /// <summary>Timers, in tics.</summary>
    public int ReactionTics;

    public int AttackCooldown;

    /// <summary>Tics left walking in <see cref="MoveAngle"/> (or charging, for a lost soul).</summary>
    public int MoveTics;

    public float MoveAngle;
}
