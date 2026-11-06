using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using EternalDigger.Components;
using EternalDigger.Rendering;

namespace EternalDigger.Systems;

/// <summary>
/// A small framed prompt above the miner's head while they stand at the house door (<c>DELIVER [E]</c>) or the
/// shopfront (<c>BUY LADDERS [E] 10g</c>). It bobs gently. The shop prompt turns red when the miner can't pay.
/// </summary>
public sealed class PromptRenderSystem(Canvas canvas, Camera camera) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const string DeliverText = " DELIVER [E] ";
    public static readonly string BuyText = $" BUY LADDERS [E] {Shop.LadderPrice}g ";

    private const string KeyHint = "[E]";
    private const float BobSpeed = 3f;

    private Query _miners;
    private float _time;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _miners = World.Query(Query.With<Miner, Position>());

    protected override void OnUpdate(float deltaSeconds)
    {
        _time += deltaSeconds;
        foreach (var chunk in _miners.GetChunks())
        {
            var miner = chunk.GetSpan<Miner>()[0];
            if (miner.IsMoving)
                return;

            var position = chunk.GetSpan<Position>()[0].Value;
            if (Layout.IsAtDeliverSpot(miner.Column, miner.Row))
                Draw(DeliverText, position.X, position.Y, ConsoleColor.White);
            else if (Layout.IsAtShop(miner.Column, miner.Row))
            {
                var color = miner.Gold >= Shop.LadderPrice ? ConsoleColor.White : ConsoleColor.Red;
                Draw(BuyText, position.X, position.Y, color);
            }

            return;
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>A three-row box with a pointer, its bottom two rows above the miner's head.</summary>
    private void Draw(string text, float minerX, float minerY, ConsoleColor textColor)
    {
        var width = text.Length + 2;
        var centerX = (int)MathF.Floor(minerX - camera.Position.X) + Layout.TileWidth / 2;
        var x = Math.Clamp(centerX - width / 2, 0, canvas.Width - width);
        var bob = MathF.Sin(_time * BobSpeed) > .6f ? 1 : 0;
        var bottom = (int)MathF.Floor(minerY - camera.Position.Y) - 2 - bob;

        const ConsoleColor frame = ConsoleColor.Yellow;
        const ConsoleColor fill = ConsoleColor.DarkBlue;
        canvas.Draw(x, bottom - 2, '┌', frame, fill);
        canvas.Draw(x, bottom - 1, '│', frame, fill);
        canvas.Draw(x, bottom, '└', frame, fill);
        var keyStart = text.IndexOf(KeyHint, StringComparison.Ordinal);
        for (var i = 0; i < text.Length; i++)
        {
            canvas.Draw(x + 1 + i, bottom - 2, '─', frame, fill);
            var isKey = i >= keyStart && i < keyStart + KeyHint.Length;
            canvas.Draw(x + 1 + i, bottom - 1, text[i], isKey ? frame : textColor, fill);
            canvas.Draw(x + 1 + i, bottom, '─', frame, fill);
        }

        canvas.Draw(x + width - 1, bottom - 2, '┐', frame, fill);
        canvas.Draw(x + width - 1, bottom - 1, '│', frame, fill);
        canvas.Draw(x + width - 1, bottom, '┘', frame, fill);
        canvas.DrawOver(centerX, bottom + 1, '▼', frame);
    }
}
