using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;

namespace Asteroids.Ecs.Systems;

/// <summary>Brings everything that left the playfield back on the opposite edge. Runs after the engine's movement.</summary>
public sealed class WrapSystem(Playfield playfield) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _positioned;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _positioned = World.Query(Query.With<Position>());

    protected override void OnUpdate(float deltaSeconds)
    {
        var job = new WrapJob { Playfield = playfield };
        _positioned.ForEach<WrapJob, Position>(ref job);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>A struct job: the JIT inlines Execute into the query loop, so there's no call per entity.</summary>
    private struct WrapJob : IForEach<Position>
    {
        public Playfield Playfield;

        public void Execute(ref Position position) => position.Value = Playfield.Wrap(position.Value);
    }
}
