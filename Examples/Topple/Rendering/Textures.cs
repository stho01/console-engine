using System;

namespace Topple.Rendering;

/// <summary>One drawn cell of a block.</summary>
public readonly struct TexturedCell(char character, ConsoleColor foreground, ConsoleColor background,
    bool keepsBackground = false)
{
    public char Character { get; } = character;
    public ConsoleColor Foreground { get; } = foreground;
    public ConsoleColor Background { get; } = background;

    /// <summary>See-through (glass): drawn over what is behind it.</summary>
    public bool KeepsBackground { get; } = keepsBackground;
}

/// <summary>
/// The look of each material, cell by cell. Coordinates are in the block's own space, so the pattern turns with the
/// block: wood grain runs along the long side, stone is laid in courses with mortar joints, glass shows through with a
/// diagonal shine. Lines are drawn with the character closest to their angle on screen. Damage adds cracks that stay
/// on the same cells as the block turns.
/// </summary>
public static class Textures
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Line characters for screen angles 0°, 45°, 90°, 135° (y points down, so 45° runs down-right).</summary>
    private static readonly char[] Lines = ['─', '\\', '│', '/'];

    private static readonly char[] HeavyLines = ['═', '\\', '║', '/'];

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <param name="material">What the block is made of.</param>
    /// <param name="shape">The block's shape (border, grain direction, roof).</param>
    /// <param name="u">Local x from the shape's lower corner.</param>
    /// <param name="v">Local y from the shape's lower corner.</param>
    /// <param name="edgeDistance">How far inside the shape the cell centre is.</param>
    /// <param name="screenAngle">The block's rotation on screen, radians.</param>
    /// <param name="damage">0 (whole) to 1 (about to break).</param>
    /// <param name="seed">Differs per block, so blocks don't all crack the same way.</param>
    public static TexturedCell Paint(Material material, Shape shape, float u, float v, float edgeDistance,
        float screenAngle, float damage, int seed)
    {
        var cellU = (int)MathF.Floor(u);
        var cellV = (int)MathF.Floor(v);
        var cell = shape.IsRoof
            ? Roof(material, cellU, cellV)
            : material switch
            {
                Material.Glass => Glass(shape, cellU, cellV, edgeDistance),
                Material.Stone => Stone(shape, u, v, cellU, cellV, edgeDistance, screenAngle, seed),
                _ => Wood(shape, cellU, cellV, edgeDistance, screenAngle, seed)
            };

        return damage > 0f && Noise.Unit(cellU, cellV, seed + 7919) < damage * .55f
            ? Crack(cell, Noise.Hash(cellU, cellV, seed))
            : cell;
    }

    /// <summary>The line character for a direction on screen, any angle.</summary>
    public static char LineFor(float screenAngle, bool heavy = false)
    {
        var steps = (int)MathF.Round(screenAngle / (MathF.PI / 4f));
        var index = ((steps % 4) + 4) % 4;
        return heavy ? HeavyLines[index] : Lines[index];
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static TexturedCell Wood(Shape shape, int u, int v, float edgeDistance, float screenAngle, int seed)
    {
        if (!shape.IsThin && edgeDistance < .75f)
            return new TexturedCell('▓', ConsoleColor.DarkYellow, ConsoleColor.Black);

        var along = shape.GrainAngle == 0f ? u : v;
        if (Noise.Unit(u, v, seed) < .07f)
            return new TexturedCell('o', ConsoleColor.DarkRed, ConsoleColor.DarkYellow);

        // A seam where two boards meet, every 5 cells along the grain.
        if (along % 5 == 4 && shape.IsThin)
            return new TexturedCell(LineFor(screenAngle + shape.GrainAngle + MathF.PI / 2f), ConsoleColor.DarkRed,
                ConsoleColor.DarkYellow);

        return new TexturedCell(LineFor(screenAngle + shape.GrainAngle), ConsoleColor.Yellow, ConsoleColor.DarkYellow);
    }

    private static TexturedCell Stone(Shape shape, float u, float v, int cellU, int cellV, float edgeDistance,
        float screenAngle, int seed)
    {
        if (!shape.IsThin && edgeDistance < .75f)
            return new TexturedCell('█', ConsoleColor.DarkGray, ConsoleColor.Black);

        // Courses of blocks 3 long, each course shifted by half a block.
        var along = shape.GrainAngle == 0f ? u : v;
        var across = shape.GrainAngle == 0f ? cellV : cellU;
        var shifted = (int)MathF.Floor(along + (across % 2) * 1.5f);
        if (shifted % 3 == 2)
            return new TexturedCell(LineFor(screenAngle + shape.GrainAngle + MathF.PI / 2f), ConsoleColor.DarkGray,
                ConsoleColor.Gray);

        var speckle = Noise.Unit(cellU, cellV, seed);
        if (speckle < .2f)
            return new TexturedCell('·', ConsoleColor.DarkGray, ConsoleColor.Gray);
        if (speckle < .3f)
            return new TexturedCell('▒', ConsoleColor.White, ConsoleColor.Gray);
        return new TexturedCell(' ', ConsoleColor.Gray, ConsoleColor.Gray);
    }

    private static TexturedCell Glass(Shape shape, int u, int v, float edgeDistance)
    {
        if (!shape.IsThin && edgeDistance < .75f)
            return new TexturedCell('▒', ConsoleColor.Cyan, ConsoleColor.Black, keepsBackground: true);

        var shine = (u + v) % 6;
        if (shine == 0)
            return new TexturedCell('/', ConsoleColor.White, ConsoleColor.Black, keepsBackground: true);
        if (shine == 1)
            return new TexturedCell('/', ConsoleColor.Cyan, ConsoleColor.Black, keepsBackground: true);
        return new TexturedCell('░', ConsoleColor.Cyan, ConsoleColor.Black, keepsBackground: true);
    }

    /// <summary>Rows of tiles, every other row shifted.</summary>
    private static TexturedCell Roof(Material material, int u, int v)
    {
        var tile = (u + v) % 2 == 0 ? '▲' : '^';
        return material == Material.Glass
            ? new TexturedCell(tile, ConsoleColor.Cyan, ConsoleColor.Black, keepsBackground: true)
            : new TexturedCell(tile, ConsoleColor.Red, ConsoleColor.DarkRed);
    }

    private static TexturedCell Crack(in TexturedCell cell, uint hash) =>
        new((hash & 1) == 0 ? '/' : '\\', ConsoleColor.Black, cell.Background, cell.KeepsBackground);
}
