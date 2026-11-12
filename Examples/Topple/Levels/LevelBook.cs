using Microsoft.Xna.Framework;

namespace Topple.Levels;

/// <summary>
/// The levels, in order. Pieces are placed on whole cells, so they are drawn without ragged edges while they stand
/// still: <c>left</c>/<c>right</c> are tile columns (inclusive) and <c>bottom</c> is the y of what the piece stands on
/// (the ground is at <see cref="LevelBuilder.GroundTop"/>); a piece's own top is where the next one stands.
/// </summary>
public static class LevelBook
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int Ground = LevelBuilder.GroundTop;

    public static readonly LevelPlan[] Levels =
    [
        new()
        {
            Name = "LOOKOUT",
            Stones = 4,
            Pieces =
            [
                Column(Material.Wood, 76, Ground, 6),
                Column(Material.Wood, 84, Ground, 6),
                Plank(Material.Wood, 75, 85, Ground - 6),
                Crystal(79, Ground - 7),
                Column(Material.Glass, 95, Ground, 4),
                Crystal(98, Ground),
                Column(Material.Wood, 103, Ground, 6)
            ]
        },
        new()
        {
            Name = "GLASS HOUSES",
            Stones = 4,
            Bombs = 1,
            Pits = [new TileArea(86, 50, 95, 55)],
            Pieces =
            [
                Column(Material.Glass, 65, Ground, 5),
                Column(Material.Glass, 75, Ground, 5),
                Crystal(69, Ground),
                Plank(Material.Wood, 64, 76, Ground - 5),
                Column(Material.Glass, 66, Ground - 6, 4),
                Column(Material.Glass, 74, Ground - 6, 4),
                Crystal(69, Ground - 6),
                Plank(Material.Wood, 65, 75, Ground - 10),
                Roof(Material.Wood, 67, Ground - 11),

                Column(Material.Wood, 101, Ground, 6),
                Column(Material.Wood, 111, Ground, 6),
                Plank(Material.Glass, 100, 112, Ground - 6),
                Crystal(100, Ground - 7),
                Crystal(111, Ground - 7),
                Roof(Material.Glass, 103, Ground - 7)
            ]
        },
        new()
        {
            Name = "STONE KEEP",
            Stones = 3,
            Bombs = 2,
            Hills = [new TileArea(72, 44, 118, 49)],
            Pieces =
            [
                Column(Material.Stone, 75, 44, 5),
                Column(Material.Stone, 80, 44, 8),
                Column(Material.Stone, 100, 44, 8),
                Crate(Material.Wood, 84, 3, 44),
                Crystal(84, 41),
                Crate(Material.Wood, 93, 3, 44),
                Crystal(94, 41),
                Plank(Material.Stone, 79, 101, 36),
                Column(Material.Glass, 84, 35, 3),
                Column(Material.Glass, 96, 35, 3),
                Plank(Material.Wood, 83, 97, 32),
                Crystal(89, 31)
            ]
        },
        new()
        {
            Name = "FERRY",
            Stones = 5,
            Bombs = 1,
            Pits = [new TileArea(52, 50, 86, 56)],
            Ferry = new FerryPlan(new Vector2(69, 47.5f), new Vector2(5, .5f), Amplitude: 10f, PeriodSeconds: 7f),
            Pieces =
            [
                Crate(Material.Wood, 65, 2, 47),
                Crystal(70, 47),

                Column(Material.Stone, 98, Ground, 6),
                Column(Material.Stone, 110, Ground, 6),
                Plank(Material.Wood, 97, 111, Ground - 6),
                Column(Material.Glass, 101, Ground - 7, 4),
                Column(Material.Glass, 107, Ground - 7, 4),
                Crystal(103, Ground - 7),
                Plank(Material.Glass, 100, 108, Ground - 11),
                Crystal(103, Ground - 12)
            ]
        }
    ];

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>An upright box one column wide.</summary>
    private static Piece Column(Material material, int column, int bottom, int height) =>
        Box(material, column, column, bottom, height);

    /// <summary>A box one row high from column <paramref name="left"/> to <paramref name="right"/>.</summary>
    private static Piece Plank(Material material, int left, int right, int bottom) =>
        Box(material, left, right, bottom, 1);

    private static Piece Crate(Material material, int left, int size, int bottom) =>
        Box(material, left, left + size - 1, bottom, size);

    private static Piece Box(Material material, int left, int right, int bottom, int height)
    {
        var half = new Vector2((right - left + 1) / 2f, height / 2f);
        return new Piece(PieceShape.Box, material, new Vector2(left + half.X, bottom - half.Y), half);
    }

    /// <summary>A roof 6 columns wide from <paramref name="left"/>, placed by its centroid (1 above the base).</summary>
    private static Piece Roof(Material material, int left, int bottom) =>
        new(PieceShape.Roof, material, new Vector2(left + 3f, bottom - 1f), new Vector2(3f, 1.5f));

    /// <summary>A 2×2 crystal from column <paramref name="left"/>.</summary>
    private static Piece Crystal(int left, int bottom) =>
        new(PieceShape.Crystal, Material.Crystal, new Vector2(left + 1f, bottom - 1f), Vector2.One);
}
