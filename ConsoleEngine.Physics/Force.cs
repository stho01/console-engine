using Microsoft.Xna.Framework;

namespace ConsoleEngine.Physics;

/// <summary>
/// Force and torque on a dynamic body for the next step, the way Box2D v3 (<c>b2Body_ApplyForce</c>,
/// <c>b2Body_ApplyTorque</c>), Unity (<c>AddForce</c>, <c>AddTorque</c>) and Godot (<c>apply_force</c>) do it: game code
/// adds to it between steps, <see cref="PhysicsSystem"/> holds it constant through every substep of the next step and
/// then sets it back to zero. A non-zero force wakes a sleeping body. Static and kinematic bodies ignore it, and so does
/// the rotation of a body without a <c>Rotation</c> and an <see cref="AngularVelocity"/> (or without a collider: no
/// inertia). Run the systems that add forces before the <see cref="PhysicsSystem"/>, in the same group.
/// </summary>
public struct Force
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>In world axes, mass × cells/s². Acts at the body's <c>Position</c>.</summary>
    public Vector2 Value;

    /// <summary>Clockwise on screen, like <c>Rotation</c> and <see cref="AngularVelocity"/>: mass × cells²/s².</summary>
    public float Torque;

    //**********************************************************
    //** public methods
    //**********************************************************

    public void Add(Vector2 force) => Value += force;

    public void AddTorque(float torque) => Torque += torque;

    /// <summary>
    /// A force at a world <paramref name="point"/> of a body whose <c>Position</c> is <paramref name="center"/>: the
    /// force, plus the torque <c>cross(point - center, force)</c>. With y pointing down, pushing down on the right side
    /// turns the body clockwise on screen (positive).
    /// </summary>
    public void AddAtPoint(Vector2 force, Vector2 point, Vector2 center)
    {
        Value += force;
        var arm = point - center;
        Torque += arm.X * force.Y - arm.Y * force.X;
    }
}
