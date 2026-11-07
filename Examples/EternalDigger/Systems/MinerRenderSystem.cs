using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Rendering;
using EternalDigger.Components;
using EternalDigger.Mining;
using EternalDigger.Rendering;

namespace EternalDigger.Systems;

/// <summary>
/// The miner, 4 x 2 cells, drawn for facing right and mirrored for left. Top row: backpack, head and headlamp.
/// The lamp dims to dark yellow when the oil is low and to dark grey (without its glow) when it is empty.
/// Bottom row: body, legs (animated by motion) and the shovel, which swings towards the tile being dug, plus a red
/// <c>¡</c> on the belt while carrying dynamite. Underground
/// the lamp leaves a short glow in the direction it points.
/// </summary>
public sealed class MinerRenderSystem(Canvas canvas, Camera camera, Headlamp headlamp) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const ConsoleColor Skin = ConsoleColor.White;
    private const ConsoleColor Overalls = ConsoleColor.Blue;
    private const ConsoleColor Pack = ConsoleColor.DarkYellow;
    /// <summary>The shovel's colour per shovel tier, and the lamp's per lamp tier.</summary>
    private static readonly ConsoleColor[] ToolColors =
        [ConsoleColor.Gray, ConsoleColor.White, ConsoleColor.Cyan, ConsoleColor.Yellow];

    private static readonly ConsoleColor[] LampColors =
        [ConsoleColor.Yellow, ConsoleColor.Yellow, ConsoleColor.White, ConsoleColor.White];

    /// <summary>Leg frames while walking, switched twice per step.</summary>
    private const string WalkLegs = "▙▟";

    /// <summary>Leg frames while climbing, switched twice per tile, so a faster climb moves the legs faster.</summary>
    private const string ClimbLegs = "╫║";

    private Query _miners;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _miners = World.Query(Query.With<Miner, Position>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _miners.GetChunks())
        {
            var miners = chunk.GetSpan<Miner>();
            var positions = chunk.GetSpan<Position>();
            for (var i = 0; i < chunk.Count; i++)
                Draw(miners[i], positions[i]);
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void Draw(in Miner miner, in Position position)
    {
        var x = (int)MathF.Floor(position.Value.X - camera.Position.X);
        var y = (int)MathF.Floor(position.Value.Y - camera.Position.Y);
        var facingRight = miner.FacingX >= 0;

        // Cells counted from the back of the miner (0) to the front (3).
        var swinging = miner.SwingTime > 0f;
        var lamp = miner.LampDirection.Y < -.5f ? '▲' : miner.LampDirection.Y > .5f ? '▼' : '•';
        DrawCell(x, y, facingRight, 1, '▄', Pack);
        DrawCell(x, y, facingRight, 2, '☻', Skin);
        var lampColor = headlamp.IsEmpty ? ConsoleColor.DarkGray
            : headlamp.IsLow ? ConsoleColor.DarkYellow
            : LampColors[Math.Clamp(miner.LampTier, 0, LampColors.Length - 1)];
        DrawCell(x, y, facingRight, 3, lamp, lampColor);

        if (miner.Dynamite > 0)
            DrawCell(x, y + 1, facingRight, 0, '¡', ConsoleColor.Red);
        DrawCell(x, y + 1, facingRight, 1, '▐', Overalls);
        DrawCell(x, y + 1, facingRight, 2, Legs(miner), Overalls);
        DrawCell(x, y + 1, facingRight, 3, swinging ? SwingTool(miner, facingRight) : facingRight ? '/' : '\\',
            ToolColors[Math.Clamp(miner.ShovelTier, 0, ToolColors.Length - 1)]);

        if (!headlamp.IsEmpty && Mine.Depth((int)MathF.Floor(position.Value.Y / Layout.TileHeight)) > 0)
            DrawGlow(miner, x, y);
    }

    private static char Legs(in Miner miner) => miner.Motion switch
    {
        MinerMotion.Walking => WalkLegs[miner.MoveElapsed < miner.MoveDuration / 2f ? 0 : 1],
        MinerMotion.Climbing => ClimbLegs[miner.MoveElapsed < miner.MoveDuration / 2f ? 0 : 1],
        MinerMotion.Falling => '╨',
        _ => '█'
    };

    /// <summary>The shovel mid-swing: flat to the side, or pointing up or down at the dug tile.</summary>
    private static char SwingTool(in Miner miner, bool facingRight)
    {
        if (miner.SwingDirection.Y != 0)
            return '│';

        return miner.SwingTime > MinerControlSystem.SwingDuration / 2f ? '─' : facingRight ? '\\' : '/';
    }

    /// <summary>A short dotted beam out of the lamp, over whatever is drawn there.</summary>
    private void DrawGlow(in Miner miner, int x, int y)
    {
        var direction = miner.LampDirection;
        var startX = x + 1.5f + direction.X * 2.5f;
        var startY = y + .5f + direction.Y * 1.5f;
        for (var i = 0; i < 2; i++)
        {
            var glowX = (int)MathF.Floor(startX + direction.X * i * 2f);
            var glowY = (int)MathF.Floor(startY + direction.Y * i);
            canvas.DrawOver(glowX, glowY, i == 0 ? '∙' : '·', i == 0 ? ConsoleColor.Yellow : ConsoleColor.DarkYellow);
        }
    }

    /// <summary>Draws a sprite cell counted from the miner's back, keeping the background behind them.</summary>
    private void DrawCell(int x, int y, bool facingRight, int fromBack, char character, ConsoleColor color) =>
        canvas.DrawOver(facingRight ? x + fromBack : x + Layout.TileWidth - 1 - fromBack, y, character, color);
}
