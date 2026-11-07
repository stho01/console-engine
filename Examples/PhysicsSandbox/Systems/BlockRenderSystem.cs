using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Physics;
using ConsoleEngine.Infrastructure.Rendering;
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
                var center = positions[i].Value + colliders[i].Offset;
                var half = colliders[i].HalfExtents;
                var left = (int)MathF.Floor(center.X - half.X);
                var top = (int)MathF.Floor(center.Y - half.Y);
                var right = (int)MathF.Ceiling(center.X + half.X);
                var bottom = (int)MathF.Ceiling(center.Y + half.Y);
                for (var y = top; y < bottom; y++)
                for (var x = left; x < right; x++)
                    console.Draw(x, y, blocks[i].Character, blocks[i].Color);
            }
        }
    }
}
