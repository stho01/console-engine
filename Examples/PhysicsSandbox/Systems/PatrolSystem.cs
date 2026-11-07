using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Physics;
using PhysicsSandbox.Components;

namespace PhysicsSandbox.Systems;

/// <summary>
/// Turns a patrolling kinematic body around at the ends of its range by setting its velocity; the physics moves it.
/// Runs in the fixed step, before <see cref="PhysicsSystem"/>.
/// </summary>
[UpdateBefore(typeof(PhysicsSystem))]
public sealed class PatrolSystem : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _patrols;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _patrols = World.Query(Query.With<Patrol, Position, Velocity>());

    protected override void OnUpdate(float deltaSeconds)
    {
        var job = new TurnJob();
        _patrols.ForEach<TurnJob, Patrol, Position, Velocity>(ref job);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private struct TurnJob : IForEach<Patrol, Position, Velocity>
    {
        public void Execute(ref Patrol patrol, ref Position position, ref Velocity velocity)
        {
            if (position.Value.X <= patrol.MinX)
                velocity.Value.X = patrol.Speed;
            else if (position.Value.X >= patrol.MaxX)
                velocity.Value.X = -patrol.Speed;
        }
    }
}
