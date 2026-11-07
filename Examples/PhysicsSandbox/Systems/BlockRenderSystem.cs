using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Infrastructure.Rendering;
using ConsoleEngine.Physics;
using PhysicsSandbox.Components;

namespace PhysicsSandbox.Systems;

/// <summary>Fills the box collider of every <see cref="Block"/> with its character. Off-screen cells are clipped.</summary>
public sealed class BlockRenderSystem(RenderConsole console) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _blocks;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _blocks = World.Query(Query.With<Block, Position, Collider>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _blocks.GetChunks())
        {
            var blocks = chunk.GetSpan<Block>();
            var positions = chunk.GetSpan<Position>();
            var colliders = chunk.GetSpan<Collider>();
            for (var i = 0; i < chunk.Count; i++)
            {
                // A cell is drawn when its centre is inside the box, so a box covering half of a row doesn't fill the
                // whole row, where a body resting on it is drawn too.
                var center = positions[i].Value + colliders[i].Offset;
                var half = colliders[i].HalfExtents;
                var left = CellFromEdge(center.X - half.X);
                var top = CellFromEdge(center.Y - half.Y);
                var right = CellFromEdge(center.X + half.X);
                var bottom = CellFromEdge(center.Y + half.Y);
                for (var y = top; y < bottom; y++)
                for (var x = left; x < right; x++)
                    console.Draw(x, y, blocks[i].Character, blocks[i].Color);
            }
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>The first cell whose centre lies past <paramref name="edge"/>.</summary>
    private static int CellFromEdge(float edge) => (int)MathF.Floor(edge + .5f);
}
