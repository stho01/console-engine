using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Systems;
using Swarm.Components;

namespace Swarm.Systems;

/// <summary>Keeps particles on screen: one that crossed an edge is put back on it and its velocity is mirrored.</summary>
[UpdateAfter(typeof(MovementSystem))]
public sealed class BounceSystem(int width, int height) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _particles;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _particles = World.Query(Query.With<Particle, Position, Velocity>());

    protected override void OnUpdate(float deltaSeconds)
    {
        // Just inside the far edges, so flooring the position still lands on the last column/row.
        var job = new BounceJob { MaxX = width - .001f, MaxY = height - .001f };
        _particles.ForEach<BounceJob, Position, Velocity>(ref job);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private struct BounceJob : IForEach<Position, Velocity>
    {
        public float MaxX;
        public float MaxY;

        public void Execute(ref Position position, ref Velocity velocity)
        {
            ref var p = ref position.Value;
            ref var v = ref velocity.Value;

            if (p.X < 0) { p.X = 0; v.X = -v.X; }
            else if (p.X > MaxX) { p.X = MaxX; v.X = -v.X; }

            if (p.Y < 0) { p.Y = 0; v.Y = -v.Y; }
            else if (p.Y > MaxY) { p.Y = MaxY; v.Y = -v.Y; }
        }
    }
}
