using ConsoleEngine.Ecs.Components;

namespace ConsoleEngine.Ecs.Systems;

/// <summary>
/// <c>Position += Velocity * delta</c> for every entity that has both and matches the optional filter. A game that
/// lets another system move some entities excludes them here, e.g. with the physics package (<c>ConsoleEngine.Physics</c>):
/// <c>new MovementSystem(Query.Without&lt;RigidBody&gt;())</c>.
/// </summary>
public sealed class MovementSystem : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private readonly QueryDescription _movers;
    private Query _query;

    //**********************************************************
    //** ctor
    //**********************************************************

    /// <summary>Moves every entity with a <see cref="Position"/> and a <see cref="Velocity"/>.</summary>
    public MovementSystem() : this(default) { }

    /// <param name="filter">More conditions. <see cref="Position"/> and <see cref="Velocity"/> are always required.</param>
    public MovementSystem(QueryDescription filter) => _movers = filter.With<Position, Velocity>();

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _query = World.Query(_movers);

    protected override void OnUpdate(float deltaSeconds)
    {
        var job = new MoveJob { DeltaSeconds = deltaSeconds };
        _query.ForEach<MoveJob, Position, Velocity>(ref job);
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
