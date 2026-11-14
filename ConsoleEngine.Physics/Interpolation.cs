using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using Microsoft.Xna.Framework;

namespace ConsoleEngine.Physics;

/// <summary>
/// Opt-in render smoothing for a body (Fiedler's "Fix Your Timestep" interpolation, Box2D/Unity's interpolated bodies):
/// <see cref="PhysicsSystem"/> keeps where the body was before its last step, and <see cref="InterpolationSystem"/> draws
/// it between that and where it is now, by how far the time is towards the next step. The body is drawn up to one step
/// behind, but moves smoothly instead of in jumps of a whole step. Needs a <c>RenderOffset</c> too.
/// </summary>
public struct Interpolation
{
    public Vector2 PreviousPosition;
    public float PreviousRadians;

    /// <summary>False until the first step filled the previous transform; nothing is offset until then.</summary>
    public bool HasPrevious;

    /// <summary>
    /// Call after moving a body by hand (a teleport or a wrap around the screen), so it isn't drawn sliding from where it
    /// was.
    /// </summary>
    public void Reset() => HasPrevious = false;
}

/// <summary>
/// Sets every interpolated body's <c>RenderOffset</c> to <c>(previous - current) × (1 - alpha)</c>, where alpha is how
/// far the time is between the last fixed step and the next (<see cref="FixedTimestep.Alpha"/>). Add it to the scene's
/// presentation group before the render systems.
/// </summary>
public sealed class InterpolationSystem(FixedTimestep timestep) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _bodies;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() =>
        _bodies = World.Query(Query.With<Position, Interpolation, RenderOffset>());

    protected override void OnUpdate(float deltaSeconds)
    {
        var behind = 1f - timestep.Alpha;
        foreach (var chunk in _bodies.GetChunks())
        {
            var positions = chunk.GetSpan<Position>();
            var interpolations = chunk.GetSpan<Interpolation>();
            var offsets = chunk.GetSpan<RenderOffset>();
            var rotations = chunk.Has<Rotation>() ? chunk.GetSpan<Rotation>() : default;
            for (var i = 0; i < chunk.Count; i++)
            {
                ref readonly var previous = ref interpolations[i];
                ref var offset = ref offsets[i];
                if (!previous.HasPrevious)
                {
                    offset = default;
                    continue;
                }

                offset.Position = (previous.PreviousPosition - positions[i].Value) * behind;
                offset.Radians = rotations.IsEmpty ? 0f : (previous.PreviousRadians - rotations[i].Radians) * behind;
            }
        }
    }
}
