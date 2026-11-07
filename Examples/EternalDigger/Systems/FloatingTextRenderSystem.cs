using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using EternalDigger.Components;
using EternalDigger.Rendering;

namespace EternalDigger.Systems;

/// <summary>
/// Draws <see cref="FloatingText"/>s centred on their position, fading at the end: gold as <c>+150g</c> (yellow) or
/// <c>-10g</c> (red). Formatted into a reused buffer, so nothing is allocated.
/// </summary>
public sealed class FloatingTextRenderSystem(Canvas canvas, Camera camera) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>The share of its life after which a text starts to fade.</summary>
    private const float FadeStart = .7f;

    private readonly char[] _buffer = new char[24];
    private Query _texts;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _texts = World.Query(Query.With<FloatingText, Position>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _texts.GetChunks())
        {
            var texts = chunk.GetSpan<FloatingText>();
            var positions = chunk.GetSpan<Position>();
            for (var i = 0; i < chunk.Count; i++)
                Draw(texts[i], positions[i].Value.X, positions[i].Value.Y);
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void Draw(in FloatingText text, float worldX, float worldY)
    {
        var length = FormatGold(text.Amount);
        var fading = text.Age > text.Lifetime * FadeStart;
        var color = text.Amount >= 0
            ? fading ? ConsoleColor.DarkYellow : ConsoleColor.Yellow
            : fading ? ConsoleColor.DarkRed : ConsoleColor.Red;

        var x = (int)MathF.Floor(worldX - camera.Position.X - length / 2f);
        var y = (int)MathF.Floor(worldY - camera.Position.Y);
        for (var i = 0; i < length; i++)
            canvas.DrawOver(x + i, y, _buffer[i], color);
    }

    /// <summary>Writes "+123g" or "-10g" into the buffer and returns its length.</summary>
    private int FormatGold(int amount)
    {
        _buffer[0] = amount >= 0 ? '+' : '-';
        Math.Abs(amount).TryFormat(_buffer.AsSpan(1), out var digits);
        _buffer[digits + 1] = 'g';
        return digits + 2;
    }
}
