using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Rendering;
using EternalDigger.Components;
using EternalDigger.Mining;
using EternalDigger.Rendering;

namespace EternalDigger.Systems;

/// <summary>
/// Draws every tile from the grass row down that is on screen, by the <see cref="LightMap"/>: bright tiles in full
/// colour, lit ones without their fill, dim ones in dark colours, and dark tiles that were seen before as faint
/// silhouettes. Unseen dark tiles stay black. Lit rock around black damp shows as dark-grey <c>░</c>. Adds ladders and
/// cracks for damaged tiles. Textures are picked by the mine's noise, so they never flicker.
/// </summary>
public sealed class TerrainRenderSystem(Canvas canvas, Camera camera, Mine mine, LightMap light) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int TextureSalt = 101;
    private const int SpeckSalt = 103;
    private const int CrackSalt = 107;

    private Query _miners;

    /// <summary>Hits the miner's pickaxe takes off every tile, so the cracks show how close a tile is to breaking.</summary>
    private int _fewerHits;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _miners = World.Query(Query.With<Miner>());

    protected override void OnUpdate(float deltaSeconds)
    {
        _fewerHits = 0;
        foreach (var chunk in _miners.GetChunks())
        {
            _fewerHits = Upgrades.FewerHits(chunk.GetSpan<Miner>()[0].PickaxeTier);
            break;
        }

        var originX = (int)MathF.Floor(camera.Position.X);
        var originY = (int)MathF.Floor(camera.Position.Y);
        var firstRow = Math.Max(Mine.SurfaceRow, FloorDiv(originY, Layout.TileHeight));
        var lastRow = FloorDiv(originY + canvas.Height - 1, Layout.TileHeight);
        for (var row = firstRow; row <= lastRow; row++)
        for (var column = 0; column < Mine.Columns; column++)
            DrawTile(column, row, column * Layout.TileWidth - originX, row * Layout.TileHeight - originY);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void DrawTile(int column, int row, int x, int y)
    {
        var cell = mine.At(column, row);
        var level = light.Get(column, row);
        if (row == Mine.SurfaceRow && column != Mine.ShaftColumn)
        {
            DrawGrass(column, row, x, y);
            return;
        }

        if (cell.Type == TileType.Air)
        {
            DrawAir(column, row, x, y, level, cell);
            return;
        }

        var look = TileLooks.For(cell.Type);
        var isDampEdge = level > LightMap.Dark && row > Mine.SurfaceRow + Tiles.WallDepth &&
                         mine.IsBlackDampEdge(column, row);
        var hitsToBreak = Tiles.HitsToBreak(cell.Type, Mine.Depth(row), _fewerHits);
        var cracks = hitsToBreak > 0 ? (cell.Damage * 8 + hitsToBreak - 1) / hitsToBreak : 0;
        for (var dy = 0; dy < Layout.TileHeight; dy++)
        for (var dx = 0; dx < Layout.TileWidth; dx++)
        {
            var cellX = column * Layout.TileWidth + dx;
            var cellY = row * Layout.TileHeight + dy;
            var texture = look.Texture[(int)(mine.Noise(cellX, cellY, TextureSalt) * look.Texture.Length)];
            var isSpeck = look.HasSpecks && mine.Noise(cellX, cellY, SpeckSalt) < look.SpeckShare;
            var isCrack = cracks > 0 && mine.Noise(cellX, cellY, CrackSalt) * 8f < cracks;

            char character;
            ConsoleColor foreground;
            ConsoleColor background;
            switch (level)
            {
                case LightMap.Bright:
                    character = isSpeck ? look.Speck : texture;
                    foreground = isSpeck ? look.SpeckColor : look.Foreground;
                    background = look.Background;
                    break;
                case LightMap.Lit:
                    character = isSpeck ? look.Speck : texture;
                    foreground = isSpeck ? look.SpeckColor : look.DimColor;
                    background = ConsoleColor.Black;
                    break;
                case LightMap.Dim:
                    character = isSpeck ? look.Speck : '░';
                    foreground = isSpeck ? look.DimSpeckColor : look.DimColor;
                    background = ConsoleColor.Black;
                    break;
                default:
                    // Remembered: a faint outline of the rock. Never shows ore, glints do that.
                    character = cell.Seen && texture != ' ' && texture != '·' ? '░' : ' ';
                    foreground = ConsoleColor.DarkGray;
                    background = ConsoleColor.Black;
                    isCrack = false;
                    break;
            }

            if (isDampEdge && !isSpeck)
            {
                // The walls of black damp: dark grey, as if the bad air soaks up the light.
                character = TileLooks.BlackDampEdge;
                foreground = ConsoleColor.DarkGray;
                background = ConsoleColor.Black;
            }

            if (isCrack)
            {
                character = TileLooks.CrackCharacters[(dx + dy) % TileLooks.CrackCharacters.Length];
                foreground = background == ConsoleColor.Black ? ConsoleColor.Gray : ConsoleColor.Black;
            }

            canvas.Draw(x + dx, y + dy, character, foreground, background);
        }
    }

    private void DrawAir(int column, int row, int x, int y, byte level, TileCell cell)
    {
        for (var dy = 0; dy < Layout.TileHeight; dy++)
        {
            if (cell.HasLadder && (level > LightMap.Dark || cell.Seen))
            {
                var color = level switch
                {
                    LightMap.Bright => ConsoleColor.Yellow,
                    LightMap.Lit or LightMap.Dim => ConsoleColor.DarkYellow,
                    _ => ConsoleColor.DarkGray
                };
                canvas.Draw(x, y + dy, TileLooks.LadderRow, color, ConsoleColor.Black);
                continue;
            }

            for (var dx = 0; dx < Layout.TileWidth; dx++)
            {
                // The cave's back wall: a few faint dots where light falls.
                var cellX = column * Layout.TileWidth + dx;
                var cellY = row * Layout.TileHeight + dy;
                var dot = level >= LightMap.Lit && mine.Noise(cellX, cellY, TextureSalt) < .12f;
                canvas.Draw(x + dx, y + dy, dot ? '·' : ' ', ConsoleColor.DarkGray, ConsoleColor.Black);
            }
        }
    }

    /// <summary>The surface: green tufts over sunny soil. Always in daylight.</summary>
    private void DrawGrass(int column, int row, int x, int y)
    {
        var soil = TileLooks.For(TileType.Grass);
        for (var dx = 0; dx < Layout.TileWidth; dx++)
        {
            var cellX = column * Layout.TileWidth + dx;
            var tuft = mine.Noise(cellX, row, TextureSalt) < .3f;
            canvas.Draw(x + dx, y, tuft ? '▓' : '▀', ConsoleColor.Green, ConsoleColor.DarkGreen);

            var noise = mine.Noise(cellX, row * Layout.TileHeight + 1, TextureSalt);
            var texture = soil.Texture[(int)(noise * soil.Texture.Length)];
            canvas.Draw(x + dx, y + 1, texture, soil.Foreground, soil.Background);
        }
    }

    private static int FloorDiv(int value, int divisor) => (int)MathF.Floor((float)value / divisor);
}
