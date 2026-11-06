using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using TerraForM.Ecs.Components;

namespace TerraForM.Ecs.Systems;

/// <summary>
/// Applies drag and this frame's thrust to the rover's velocity and remembers where it was. The engine's
/// <c>MovementSystem</c> runs next and moves it, then <see cref="RoverCollisionSystem"/>.
/// </summary>
public sealed class RoverPhysicsSystem : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Fraction of velocity lost per second.</summary>
    private const float Drag = 5f;

    private Query _rovers;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() =>
        _rovers = World.Query(Query.With<Rover, Position, Velocity>());

    protected override void OnUpdate(float deltaSeconds)
    {
        var job = new PhysicsJob { DeltaSeconds = deltaSeconds };
        _rovers.ForEach<PhysicsJob, Rover, Position, Velocity>(ref job);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private struct PhysicsJob : IForEach<Rover, Position, Velocity>
    {
        public float DeltaSeconds;

        public void Execute(ref Rover rover, ref Position position, ref Velocity velocity)
        {
            rover.StandingOnPlantSpot = false;
            rover.PositionBeforeMove = position.Value;
            velocity.Value += -Drag * velocity.Value * DeltaSeconds;
            velocity.Value += rover.Acceleration * DeltaSeconds;
        }
    }
}
