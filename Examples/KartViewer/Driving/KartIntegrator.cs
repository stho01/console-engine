namespace KartViewer.Driving;

/// <summary>
/// Semi-implicit Euler for a <see cref="KartBody"/>, with the forces held constant over the step: velocity first,
/// then position from the new velocity. The same order as Kartster's <c>RigidBodyIntegrator</c>, so
/// <see cref="KartDynamics.ComputeForces"/> followed by this reproduces Kartster's step.
/// </summary>
public static class KartIntegrator
{
    public static void Step(ref KartBody body, KartForces forces, float mass, float yawInertia, float dt)
    {
        body.Velocity += forces.Force * (dt / mass);
        body.AngularVelocity += forces.Torque / yawInertia * dt;
        body.Position += body.Velocity * dt;
        body.Heading += body.AngularVelocity * dt;
    }
}
