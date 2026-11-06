using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using Starguard.Assets;

namespace Starguard.Systems;

/// <summary>
/// Destroys moving entities that left the playfield: shots above <see cref="Playfield.Top"/> or at the ground, and
/// saucers past the far edge.
/// </summary>
public sealed class BoundsSystem(Playfield playfield) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _movers;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _movers = World.Query(Query.With<Position, Velocity>());

    protected override void OnUpdate(float deltaSeconds)
    {
        // Saucers start just outside an edge, so allow one saucer width on each side.
        var minX = -SpriteSheet.SaucerWidth - 1;
        var maxX = playfield.Width + 1;
        foreach (var chunk in _movers.GetChunks())
        {
            var positions = chunk.GetSpan<Position>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var position = positions[i].Value;
                if (position.Y < playfield.Top || position.Y >= playfield.Ground ||
                    position.X < minX || position.X > maxX)
                    Commands.Destroy(chunk.Entities[i]);
            }
        }
    }
}
