using System;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using ConsoleEngine.Infrastructure.Rendering;
using Microsoft.Xna.Framework;

namespace ConsoleEngine.Ecs.Systems;

/// <summary>
/// Draws each <see cref="Outline"/> around its <see cref="Position"/>, rotated by <see cref="Rotation"/> when the
/// entity has one. Outlines whose bounding circle is off-screen are skipped, so drawing costs follow what is visible,
/// not how many entities exist.
/// </summary>
public sealed class OutlineRenderSystem(RenderConsole console, Camera camera, OutlineLibrary outlines) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _outlined;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _outlined = World.Query(Query.With<Position, Outline>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _outlined.GetChunks())
        {
            var positions = chunk.GetSpan<Position>();
            var shapes = chunk.GetSpan<Outline>();
            var rotations = chunk.Has<Rotation>() ? chunk.GetSpan<Rotation>() : Span<Rotation>.Empty;
            var offsets = chunk.Has<RenderOffset>() ? chunk.GetSpan<RenderOffset>() : Span<RenderOffset>.Empty;

            for (var i = 0; i < chunk.Count; i++)
            {
                var screen = camera.WorldToScreen(positions[i].Value);
                var rotation = rotations.IsEmpty ? 0f : rotations[i].Radians;
                if (!offsets.IsEmpty)
                {
                    screen += offsets[i].Position;
                    rotation += offsets[i].Radians;
                }

                if (!IsOnScreen(screen, outlines.GetRadius(shapes[i].Id)))
                    continue;

                Draw(shapes[i], screen, rotation);
            }
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private bool IsOnScreen(Vector2 screen, float radius) =>
        screen.X + radius >= 0 && screen.X - radius < console.Width &&
        screen.Y + radius >= 0 && screen.Y - radius < console.Height;

    private void Draw(in Outline outline, Vector2 screen, float rotation)
    {
        var vertices = outlines.GetVertices(outline.Id);
        var (sin, cos) = MathF.SinCos(rotation);

        var previous = ToCell(vertices[^1], screen, sin, cos);
        for (var v = 0; v < vertices.Length; v++)
        {
            var current = ToCell(vertices[v], screen, sin, cos);
            console.DrawLine(previous, current, outline.Character, outline.Color);
            previous = current;
        }
    }

    /// <summary>Rotates the vertex, moves it to the entity's screen position and rounds down to a cell.</summary>
    private static Point ToCell(Vector2 vertex, Vector2 screen, float sin, float cos) =>
        new((int)MathF.Floor(vertex.X * cos - vertex.Y * sin + screen.X),
            (int)MathF.Floor(vertex.X * sin + vertex.Y * cos + screen.Y));
}
