using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Rendering;
using EternalDigger.Components;
using EternalDigger.Rendering;

namespace EternalDigger.Systems;

/// <summary>
/// The deep camp, once bought: a small tent <c>/▀\</c> at the camp's last column, a flickering lantern <c>¤</c> on a
/// pole at its first, a <c>CAMP</c> plank between them and a crate counter. Drawn over the terrain in world cells, like
/// the lift; the lantern's light comes from <see cref="LightingSystem"/>.
/// </summary>
public sealed class CampRenderSystem(Canvas canvas, Camera camera) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const string SignText = "CAMP";

    /// <summary>The tent, top line first, 4 cells wide; spaces are left out.</summary>
    public static readonly string[] Tent = [" /\\ ", "/▀▀\\", "▌▒▒▐"];

    public const char Lantern = '¤';

    private const ConsoleColor TentColor = ConsoleColor.DarkYellow;
    private const ConsoleColor Door = ConsoleColor.DarkGray;
    private const ConsoleColor Wood = ConsoleColor.DarkYellow;
    private const float FlickerSpeed = 7f;

    /// <summary>The camp's top-left world cell: the row above <see cref="DeepCamp.Row"/>.</summary>
    private const int Left = DeepCamp.FirstColumn * Layout.TileWidth;
    private const int Top = (DeepCamp.Row - 1) * Layout.TileHeight;
    private const int TentLeft = DeepCamp.LastColumn * Layout.TileWidth;

    /// <summary>The cell row the tent's top line is on: three rows tall, standing on the camp's floor.</summary>
    private const int TentTop = (DeepCamp.Row + 1) * Layout.TileHeight - 3;

    private Query _miners;
    private float _time;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _miners = World.Query(Query.With<Miner>());

    protected override void OnUpdate(float deltaSeconds)
    {
        _time += deltaSeconds;
        foreach (var chunk in _miners.GetChunks())
        {
            if (chunk.GetSpan<Miner>()[0].HasDeepCamp)
                Draw((int)MathF.Floor(camera.Position.X), (int)MathF.Floor(camera.Position.Y));
            return;
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void Draw(int originX, int originY)
    {
        var bottom = (DeepCamp.Row + 1) * Layout.TileHeight - originY;
        if (bottom < 0 || Top - originY >= canvas.Height)
            return;

        for (var line = 0; line < Tent.Length; line++)
        for (var i = 0; i < Tent[line].Length; i++)
        {
            var character = Tent[line][i];
            if (character != ' ')
                canvas.DrawOver(TentLeft + i - originX, TentTop + line - originY, character,
                    character == '▒' ? Door : TentColor);
        }

        // The lantern on its pole, the plank and a crate as the counter.
        var lit = MathF.Sin(_time * FlickerSpeed) + MathF.Sin(_time * FlickerSpeed * 2.3f) > -.6f;
        canvas.DrawOver(Left + 1 - originX, Top + 1 - originY, Lantern, lit ? ConsoleColor.Yellow : ConsoleColor.DarkYellow);
        canvas.DrawOver(Left + 1 - originX, Top + 2 - originY, '│', Wood);
        canvas.DrawOver(Left + 1 - originX, Top + 3 - originY, '┴', Wood);
        canvas.Draw(Left + 4 - originX, Top + 1 - originY, SignText, ConsoleColor.White, Wood);
        canvas.DrawOver(Left + 5 - originX, Top + 3 - originY, '▄', Wood);
        canvas.DrawOver(Left + 6 - originX, Top + 3 - originY, '▄', Wood);
    }
}
