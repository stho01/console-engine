using System;
using Microsoft.Xna.Framework;

namespace Topple;

/// <summary>
/// How much damage a body takes. The physics reports no impulses (and no events at all for the ground tiles), so damage
/// comes from how suddenly a body's velocity changed in one step, beyond what gravity did: a jolt. That covers every
/// kind of hit the same way: a shot, a falling block, landing on the ground, being crushed from above. Heavy bodies
/// jolt light ones more than the other way round, as momentum says.
/// </summary>
public static class Impact
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Jolts up to this speed (cells/s) do no damage: resting, sliding, small bumps.</summary>
    public const float HarmlessSpeed = 8f;

    /// <summary>A jolt this big sets off a bomb.</summary>
    public const float BombTriggerSpeed = 4f;

    public const float BlastRadius = 8f;

    /// <summary>Damage at the centre of a blast, falling off linearly to 0 at <see cref="BlastRadius"/>.</summary>
    public const float BlastDamage = 30f;

    /// <summary>Speed (cells/s) a blast adds at its centre, falling off like the damage.</summary>
    public const float BlastPush = 35f;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>The change of velocity in one step that gravity doesn't explain.</summary>
    public static float Jolt(Vector2 before, Vector2 after, Vector2 gravityStep) =>
        (after - before - gravityStep).Length();

    /// <summary>Damage for a jolt: 1 per cell/s above <see cref="HarmlessSpeed"/>.</summary>
    public static float Damage(float jolt) => MathF.Max(0f, jolt - HarmlessSpeed);

    /// <summary>0 to 1: how strongly a blast reaches something this far from its centre.</summary>
    public static float BlastFalloff(float distance) => MathF.Max(0f, 1f - distance / BlastRadius);
}
