using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Ecs;
using ConsoleEngine.Infrastructure.Inputs;
using Snake.Ecs.Components;

namespace Snake.Ecs.Systems;

/// <summary>WASD turns the head. Turning straight back into the body is ignored.</summary>
public sealed class SteeringSystem : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _heads;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _heads = World.Query(Query.With<SnakeHead>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _heads.GetChunks())
        {
            var heads = chunk.GetSpan<SnakeHead>();
            for (var i = 0; i < chunk.Count; i++)
                heads[i].Heading = Steer(heads[i].Heading);
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>Same order and rules as the original: each pressed key may override the previous one.</summary>
    private static Direction Steer(Direction heading)
    {
        if (Input.Instance.GetKey(Key.A).Pressed && heading != Direction.East) heading = Direction.West;
        if (Input.Instance.GetKey(Key.D).Pressed && heading != Direction.West) heading = Direction.East;
        if (Input.Instance.GetKey(Key.W).Pressed && heading != Direction.South) heading = Direction.North;
        if (Input.Instance.GetKey(Key.S).Pressed && heading != Direction.North) heading = Direction.South;
        return heading;
    }
}
