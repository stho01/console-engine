using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using EternalDigger.Components;
using EternalDigger.Mining;
using EternalDigger.Rendering;

namespace EternalDigger.Systems;

/// <summary>
/// A small framed prompt above the miner's head while they stand at the house door (<c>DELIVER [E]</c>), the
/// shopfront (<c>BUY LADDERS [E] 10g</c>), the lift sign (<c>BUILD LIFT +10m [E] 200g</c>) or a lift landing (call,
/// get in, ride). It bobs gently. A price turns red when the miner can't pay.
/// </summary>
public sealed class PromptRenderSystem(Canvas canvas, Camera camera, Elevator elevator) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const string DeliverText = " DELIVER [E] ";
    public static readonly string BuyText = $" BUY LADDERS [E] {Shop.LadderPrice}g ";
    public const string CallLiftText = " CALL LIFT [E] ";
    public const string BoardLiftText = " GET IN [E] ";
    public const string RideText = " ↑↓ RIDE   ←→ GET OUT ";

    private const string KeyHint = "[E]";
    private const float BobSpeed = 3f;

    private Query _miners;
    private float _time;
    private string _buildLiftText = string.Empty;
    private int _buildLiftPrice = -1;

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
            if (miner.IsMoving || elevator.IsCarMoving && miner.IsRiding)
                return;

            var position = chunk.GetSpan<Position>()[0].Value;
            if (miner.IsRiding)
                Draw(RideText, position.X, position.Y, ConsoleColor.White);
            else if (Layout.IsAtDeliverSpot(miner.Column, miner.Row))
                Draw(DeliverText, position.X, position.Y, ConsoleColor.White);
            else if (Layout.IsAtShop(miner.Column, miner.Row))
                Draw(BuyText, position.X, position.Y, PriceColor(miner.Gold, Shop.LadderPrice));
            else if (Layout.IsAtLiftSign(miner.Column, miner.Row))
                Draw(BuildLiftText(), position.X, position.Y, PriceColor(miner.Gold, elevator.NextSegmentPrice));
            else if (miner.Column == Elevator.Column && elevator.IsLanding(miner.Row))
                Draw(elevator.IsCarAt(miner.Row) ? BoardLiftText : CallLiftText, position.X, position.Y,
                    ConsoleColor.White);

            return;
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static ConsoleColor PriceColor(int gold, int price) => gold >= price ? ConsoleColor.White : ConsoleColor.Red;

    /// <summary>The lift sign's text with the next segment's price. Rebuilt only when the price changes.</summary>
    private string BuildLiftText()
    {
        if (elevator.NextSegmentPrice != _buildLiftPrice)
        {
            _buildLiftPrice = elevator.NextSegmentPrice;
            _buildLiftText = $" BUILD LIFT +{Elevator.SegmentMetres}m [E] {_buildLiftPrice}g ";
        }

        return _buildLiftText;
    }

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
