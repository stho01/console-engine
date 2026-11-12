namespace ConsoleEngine.Physics;

/// <summary>
/// How fast a body turns, radians per second, clockwise on screen like <c>Rotation</c>. A <see cref="RigidBody"/> with a
/// <c>Rotation</c> and this component rotates: contacts and friction spin it, and its rotational inertia comes from its
/// <see cref="Collider"/> shape and mass, about its <c>Position</c>. Put the position at the shape's centre for the most
/// natural motion.
/// </summary>
public struct AngularVelocity(float value)
{
    public float Value = value;
}
