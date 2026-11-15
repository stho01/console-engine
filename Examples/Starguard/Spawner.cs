using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using Microsoft.Xna.Framework;
using Starguard.Assets;
using Starguard.Components;

namespace Starguard;

/// <summary>
/// Creates every kind of entity. All of it goes through a <see cref="CommandBuffer"/>, because most of it happens
/// while a system iterates a query. Each kind is one archetype (one fixed set of components).
/// </summary>
public sealed class Spawner(SpriteSheet sprites, Playfield playfield)
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int Columns = 11;
    public const int Rows = 5;
    public const int ColumnSpacing = 7;
    public const int RowSpacing = 3;

    /// <summary>
    /// Top row of the first wave. Each later wave starts one row lower, at most <see cref="MaxWaveDrop"/> rows lower.
    /// </summary>
    public const int FirstWaveTop = 6;
    public const int MaxWaveDrop = 8;

    public const float CannonSpeed = 30f;
    public const float PlayerShotSpeed = 50f;
    public const float AlienShotSpeed = 18f;
    public const float SaucerSpeed = 12f;
    public const float ExplosionSeconds = 0.25f;

    public const int ShieldCount = 4;
    public const byte ShieldHealth = 4;

    /// <summary>One shield. Each <c>█</c> becomes a <see cref="ShieldCell"/> entity.</summary>
    private static readonly string[] ShieldShape =
    [
        " ███████ ",
        "█████████",
        "███   ███"
    ];

    /// <summary>Glyph for each remaining health, index 1 to <see cref="ShieldHealth"/>.</summary>
    private static readonly char[] ShieldGlyphs = [' ', '░', '▒', '▓', '█'];

    private static readonly int[] SaucerPoints = [50, 100, 150, 300];

    //**********************************************************
    //** props
    //**********************************************************

    public SpriteSheet Sprites => sprites;

    /// <summary>Where the cannon starts and respawns: centred on the ground line.</summary>
    public Vector2 CannonStart => new((playfield.Width - SpriteSheet.UnitWidth) / 2, playfield.CannonTop);

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Records a full formation for <paramref name="wave"/> and returns the number of aliens in it.</summary>
    public int CreateFormation(CommandBuffer commands, int wave)
    {
        var width = (Columns - 1) * ColumnSpacing + SpriteSheet.UnitWidth;
        var left = (playfield.Width - width) / 2;
        var top = FirstWaveTop + Math.Min(wave - 1, MaxWaveDrop);

        for (var row = 0; row < Rows; row++)
        {
            var alien = AlienForRow(row);
            for (var column = 0; column < Columns; column++)
            {
                var position = new Vector2(left + column * ColumnSpacing, top + row * RowSpacing);
                commands.Create(
                    new Position(position),
                    new SpriteRef(alien.FirstFrame),
                    new Hitbox(SpriteSheet.UnitWidth, SpriteSheet.UnitHeight),
                    alien);
            }
        }

        return Rows * Columns;
    }

    public void CreateShields(CommandBuffer commands)
    {
        var shieldWidth = ShieldShape[0].Length;
        var top = playfield.CannonTop - 7;
        for (var shield = 0; shield < ShieldCount; shield++)
        {
            var centre = playfield.Width * (shield + 1) / (ShieldCount + 1);
            var left = centre - shieldWidth / 2;
            for (var y = 0; y < ShieldShape.Length; y++)
            for (var x = 0; x < shieldWidth; x++)
            {
                if (ShieldShape[y][x] == ' ')
                    continue;

                commands.Create(
                    new Position(new Vector2(left + x, top + y)),
                    new Glyph(ShieldGlyph(ShieldHealth), ConsoleColor.Green),
                    new ShieldCell(ShieldHealth));
            }
        }
    }

    public void CreateCannon(CommandBuffer commands) =>
        commands.Create(
            new Position(CannonStart),
            new SpriteRef(sprites.Cannon),
            new Hitbox(SpriteSheet.UnitWidth, SpriteSheet.UnitHeight),
            new Cannon(CannonSpeed));

    /// <summary>A shot leaving the cannon's barrel, the cell above its middle.</summary>
    public static void CreatePlayerShot(CommandBuffer commands, Vector2 cannonPosition) =>
        commands.Create(
            new Position(cannonPosition + new Vector2(SpriteSheet.UnitWidth / 2, -1)),
            new Velocity(new Vector2(0, -PlayerShotSpeed)),
            new Glyph('│', ConsoleColor.White),
            new PlayerShot());

    /// <summary>A shot dropping from the cell below the alien's middle.</summary>
    public static void CreateAlienShot(CommandBuffer commands, Vector2 alienPosition) =>
        commands.Create(
            new Position(alienPosition + new Vector2(SpriteSheet.UnitWidth / 2, SpriteSheet.UnitHeight)),
            new Velocity(new Vector2(0, AlienShotSpeed)),
            new Glyph('¦', ConsoleColor.Yellow),
            new AlienShot());

    public void CreateExplosion(CommandBuffer commands, Vector2 position) =>
        commands.Create(
            new Position(position),
            new SpriteRef(sprites.Explosion),
            new Lifetime(ExplosionSeconds));

    /// <summary>A saucer entering from just outside the left or right edge, at the top of the playfield.</summary>
    public void CreateSaucer(CommandBuffer commands, bool fromLeft, Random random)
    {
        var x = fromLeft ? -SpriteSheet.SaucerWidth : playfield.Width;
        commands.Create(
            new Position(new Vector2(x, playfield.Top + 1)),
            new Velocity(new Vector2(fromLeft ? SaucerSpeed : -SaucerSpeed, 0)),
            new SpriteRef(sprites.Saucer),
            new Hitbox(SpriteSheet.SaucerWidth, SpriteSheet.SaucerHeight),
            new Saucer(SaucerPoints[random.Next(SaucerPoints.Length)]));
    }

    public static char ShieldGlyph(int health) => ShieldGlyphs[Math.Clamp(health, 0, ShieldHealth)];

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>One row of the smallest kind on top, two of the middle kind, two of the biggest at the bottom.</summary>
    private Alien AlienForRow(int row) => row switch
    {
        0 => new Alien(sprites.Scout, 30),
        1 or 2 => new Alien(sprites.Raider, 20),
        _ => new Alien(sprites.Brute, 10)
    };
}
