using System;

namespace ConsoleEngine.Physics;

/// <summary>
/// What game systems read from the physics after a step. Depend on this instead of <see cref="PhysicsSystem"/>, so
/// another physics engine can be plugged in behind the same components (<see cref="RigidBody"/>,
/// <see cref="Collider"/>, <c>Position</c>, <c>Velocity</c>) and events.
/// <para>
/// The lists hold the last step's changes only and are valid until the next step. Read them from a system that runs
/// after the physics in the same fixed-step group.
/// </para>
/// </summary>
public interface IPhysicsEvents
{
    /// <summary>Non-sensor pairs that started touching in the last step.</summary>
    ReadOnlySpan<ContactEvent> ContactBeginEvents { get; }

    /// <summary>Non-sensor pairs that stopped touching in the last step, or lost a body.</summary>
    ReadOnlySpan<ContactEvent> ContactEndEvents { get; }

    /// <summary>Colliders that started overlapping a sensor in the last step.</summary>
    ReadOnlySpan<SensorEvent> SensorBeginEvents { get; }

    /// <summary>Colliders that stopped overlapping a sensor in the last step, or a pair that lost a body.</summary>
    ReadOnlySpan<SensorEvent> SensorEndEvents { get; }
}
