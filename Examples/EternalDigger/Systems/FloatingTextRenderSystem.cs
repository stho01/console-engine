using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using EternalDigger.Components;
using EternalDigger.Mining;
using EternalDigger.Rendering;

namespace EternalDigger.Systems;

/// <summary>
/// Draws <see cref="FloatingText"/>s centred on their position, fading at the end: gold as <c>+150g</c> (yellow) or
/// <c>-10g</c> (red), a lift segment as <c>ELEVATOR +10m</c> (yellow), a shovel on obsidian as <c>TOO HARD</c> (red).
/// Formatted into a reused buffer, so nothing is allocated.
/// </summary>
public sealed class FloatingTextRenderSystem(Canvas canvas, Camera camera) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>The share of its life after which a text starts to fade.</summary>
    private const float FadeStart = .7f;

    public const string ElevatorLabel = "ELEVATOR +";
    public const string TooHardText = "TOO HARD";
    public const string FossilText = "FOSSIL!";
    public const string RelicText = "RELIC!";
    public const string OilLabel = "OIL +";
    public const string GasText = "GAS!";

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
        var fading = text.Age > text.Lifetime * FadeStart;
        var length = text.Kind switch
        {
            FloatingTextKind.Elevator => Format(ElevatorLabel, text.Amount, 'm'),
            FloatingTextKind.Oil => Format(OilLabel, text.Amount, ' ') - 1,
            FloatingTextKind.TooHard => Copy(TooHardText),
            FloatingTextKind.Gas => Copy(GasText),
            FloatingTextKind.RareFind => Copy((TileType)text.Amount == TileType.Relic ? RelicText : FossilText),
            _ => FormatGold(text.Amount)
        };
        var color = text.Kind == FloatingTextKind.RareFind ? fading ? ConsoleColor.Gray : ConsoleColor.White
            : text.Kind == FloatingTextKind.TooHard || text.Kind == FloatingTextKind.Gold && text.Amount < 0
            ? fading ? ConsoleColor.DarkRed : ConsoleColor.Red
            : fading ? ConsoleColor.DarkYellow : ConsoleColor.Yellow;

        var x = (int)MathF.Floor(worldX - camera.Position.X - length / 2f);
        var y = (int)MathF.Floor(worldY - camera.Position.Y);
        for (var i = 0; i < length; i++)
            canvas.DrawOver(x + i, y, _buffer[i], color);
    }

    private int Copy(string text)
    {
        text.AsSpan().CopyTo(_buffer);
        return text.Length;
    }

    /// <summary>Writes a label, a number and a unit, like "ELEVATOR +10m", into the buffer and returns its length.</summary>
    private int Format(string label, int amount, char unit)
    {
        label.AsSpan().CopyTo(_buffer);
        amount.TryFormat(_buffer.AsSpan(label.Length), out var digits);
        _buffer[label.Length + digits] = unit;
        return label.Length + digits + 1;
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
