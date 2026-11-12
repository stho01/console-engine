using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Rendering;
using EternalDigger.Components;
using EternalDigger.Rendering;

namespace EternalDigger.Systems;

/// <summary>
/// Planted sticks: a red <c>¡</c> stuck in the rock with a <c>*</c> fuse flame above it that flickers yellow and red.
/// Drawn in the dark too: the fuse glows. Sparks and smoke are particles (<c>DynamiteSystem</c>).
/// </summary>
public sealed class DynamiteRenderSystem(Canvas canvas, Camera camera) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const char StickGlyph = '¡';
    public const char FuseGlyph = '*';

    private const ConsoleColor StickColor = ConsoleColor.Red;
    private const float FlickerSpeed = 14f;

    private Query _sticks;
    private float _time;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _sticks = World.Query(Query.With<Dynamite>());

    protected override void OnUpdate(float deltaSeconds)
    {
        _time += deltaSeconds;
        var flame = MathF.Sin(_time * FlickerSpeed) > 0f ? ConsoleColor.Yellow : ConsoleColor.Red;
        foreach (var chunk in _sticks.GetChunks())
        {
            var sticks = chunk.GetSpan<Dynamite>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var x = sticks[i].Column * Layout.TileWidth + 1 - (int)MathF.Floor(camera.Position.X);
                var y = sticks[i].Row * Layout.TileHeight - (int)MathF.Floor(camera.Position.Y);
                canvas.DrawOver(x, y + 1, StickGlyph, StickColor);
                canvas.DrawOver(x, y, FuseGlyph, flame);
            }
        }
    }
}
