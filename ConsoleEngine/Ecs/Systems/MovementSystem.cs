using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Physics;

namespace ConsoleEngine.Ecs.Systems;

/// <summary>
/// <c>Position += Velocity * delta</c> for every entity that has both, except physics bodies: <see cref="PhysicsSystem"/>
/// moves entities with a <see cref="RigidBody"/>.
/// </summary>
public sealed class MovementSystem : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _movers;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() =>
        _movers = World.Query(Query.With<Position, Velocity>().Without<RigidBody>());

    protected override void OnUpdate(float deltaSeconds)
    {
        var job = new MoveJob { DeltaSeconds = deltaSeconds };
        _movers.ForEach<MoveJob, Position, Velocity>(ref job);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private struct MoveJob : IForEach<Position, Velocity>
    {
        public float DeltaSeconds;

        public void Execute(ref Position position, ref Velocity velocity) =>
            position.Value += velocity.Value * DeltaSeconds;
    }
}
