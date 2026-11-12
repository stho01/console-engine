using ConsoleEngine.Ecs;
using ConsoleEngine.Physics;
using Microsoft.Xna.Framework;

namespace Topple.Levels;

/// <summary>
/// Turns a <see cref="LevelPlan"/> into tiles and bodies. Every level has the same frame: rows 0–1 are the HUD, the
/// ground fills the rows from <see cref="GroundTop"/> down, walls close both sides, and the catapult stands on a hill
/// on the left.
/// </summary>
public static class LevelBuilder
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int GroundTop = 50;
    public const int HillTop = 42;
    public const int HillRight = 14;

    /// <summary>The catapult's pivot, above the hill.</summary>
    public static readonly Vector2 CatapultPivot = new(9f, HillTop - 4f);

    /// <summary>How far below a pit's rim the water starts.</summary>
    public const int WaterDepthBelowRim = 2;

    //**********************************************************
    //** public methods
    //**********************************************************

    public static TileGrid CreateTiles(LevelPlan plan, int width, int height)
    {
        var tiles = new TileGrid(width, height, Vector2.One) { Category = Layers.Ground };
        Fill(tiles, new TileArea(0, GroundTop, width - 1, height - 1), solid: true);
        Fill(tiles, new TileArea(0, 2, 0, height - 1), solid: true);
        Fill(tiles, new TileArea(width - 1, 2, width - 1, height - 1), solid: true);
        Fill(tiles, new TileArea(1, HillTop, HillRight, GroundTop - 1), solid: true);
        foreach (var hill in plan.Hills)
            Fill(tiles, hill, solid: true);
        foreach (var pit in plan.Pits)
            Fill(tiles, pit, solid: false);
        return tiles;
    }

    /// <summary>The towers, the ferry and a water sensor in each pit.</summary>
    public static void Spawn(World world, BodyFactory factory, LevelPlan plan)
    {
        foreach (var pit in plan.Pits)
            BodyFactory.CreateWater(world, WaterOf(pit));

        if (plan.Ferry is { } ferry)
            factory.CreateFerry(world, ferry);

        foreach (var piece in plan.Pieces)
            factory.CreatePiece(world, piece);
    }

    /// <summary>The part of a pit that is water: from <see cref="WaterDepthBelowRim"/> rows below its top down.</summary>
    public static TileArea WaterOf(TileArea pit) => pit with { Top = pit.Top + WaterDepthBelowRim };

    //**********************************************************
    //** internals
    //**********************************************************

    private static void Fill(TileGrid tiles, TileArea area, bool solid)
    {
        for (var row = area.Top; row <= area.Bottom; row++)
        for (var column = area.Left; column <= area.Right; column++)
            tiles.SetSolid(column, row, solid);
    }
}
