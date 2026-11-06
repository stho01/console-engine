using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using EternalDigger.Components;
using EternalDigger.Rendering;

namespace EternalDigger.Systems;

/// <summary>
/// Draws <see cref="FloatingNumber"/>s as <c>+150g</c> (yellow) or <c>-10g</c> (red), centred on their position and
/// fading at the end. Formatted into a reused buffer, so nothing is allocated.
/// </summary>
public sealed class FloatingNumberRenderSystem(Canvas canvas, Camera camera) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>The share of its life after which a number starts to fade.</summary>
    private const float FadeStart = .7f;

    private readonly char[] _text = new char[16];
    private Query _numbers;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _numbers = World.Query(Query.With<FloatingNumber, Position>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _numbers.GetChunks())
        {
            var numbers = chunk.GetSpan<FloatingNumber>();
            var positions = chunk.GetSpan<Position>();
            for (var i = 0; i < chunk.Count; i++)
                Draw(numbers[i], positions[i].Value.X, positions[i].Value.Y);
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void Draw(in FloatingNumber number, float worldX, float worldY)
    {
        var length = Format(number.Amount);
        var fading = number.Age > number.Lifetime * FadeStart;
        var color = number.Amount >= 0
            ? fading ? ConsoleColor.DarkYellow : ConsoleColor.Yellow
            : fading ? ConsoleColor.DarkRed : ConsoleColor.Red;

        var x = (int)MathF.Floor(worldX - camera.Position.X - length / 2f);
        var y = (int)MathF.Floor(worldY - camera.Position.Y);
        for (var i = 0; i < length; i++)
            canvas.DrawOver(x + i, y, _text[i], color);
    }

    /// <summary>Writes "+123g" or "-10g" into the buffer and returns its length.</summary>
    private int Format(int amount)
    {
        _text[0] = amount >= 0 ? '+' : '-';
        Math.Abs(amount).TryFormat(_text.AsSpan(1), out var digits);
        _text[digits + 1] = 'g';
        return digits + 2;
    }
}
