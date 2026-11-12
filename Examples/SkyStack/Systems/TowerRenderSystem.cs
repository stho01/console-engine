using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Rendering;
using SkyStack.Climbing;
using SkyStack.Rendering;

namespace SkyStack.Systems;

/// <summary>
/// Paints the part of the tower the camera sees into the <see cref="PixelCanvas"/>, row by row: stone walls, a brick
/// backdrop, platforms (lit top, shaded underside, rounded ends), the ground, and the number of every
/// <see cref="Tower.LabelEvery"/>th floor under its platform. Every pixel is painted every frame. The colours of a
/// row come from the zone of the platform above it.
/// </summary>
public sealed class TowerRenderSystem(Tower tower, Camera camera, PixelCanvas canvas) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Pixels between a platform's top and its floor number.</summary>
    private const int LabelOffset = Layout.PlatformThickness + 3;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnUpdate(float deltaSeconds)
    {
        var top = (int)camera.Position.Y;
        for (var y = 0; y < canvas.Height; y++)
            PaintRow(y, top + y);

        PaintLabels(top);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void PaintRow(int y, int worldY)
    {
        // The platform whose band (its top down to the next platform) holds this row.
        var rowInBand = TowerLooks.Mod(worldY, Layout.FloorSpacing);
        var floor = -(worldY - rowInBand) / Layout.FloorSpacing;
        ref readonly var zone = ref TowerLooks.ZoneOf(floor);

        for (var x = 0; x < Layout.InnerLeft; x++)
            canvas.Set(x, y, TowerLooks.WallAt(x, worldY, zone));
        for (var x = Layout.InnerRight; x < Layout.TowerWidth; x++)
            canvas.Set(x, y, TowerLooks.WallAt(Layout.TowerWidth - 1 - x, worldY, zone));

        if (floor < 0 || (floor == 0 && rowInBand >= Layout.PlatformThickness))
        {
            PaintGround(y, worldY);
            return;
        }

        for (var x = Layout.InnerLeft; x < Layout.InnerRight; x++)
            canvas.Set(x, y, TowerLooks.BackdropAt(x, worldY, zone));

        if (rowInBand < Layout.PlatformThickness)
            PaintPlatform(y, floor, rowInBand, zone);
    }

    private void PaintPlatform(int y, int floor, int rowInPlatform, in TowerLooks.Zone zone)
    {
        var platform = tower.GetFloor(floor);
        var milestone = floor > 0 && Tower.IsMilestone(floor);
        var bottom = rowInPlatform == Layout.PlatformThickness - 1;
        var color = bottom
            ? milestone ? TowerLooks.MilestoneShade : zone.PlatformShade
            : milestone ? TowerLooks.MilestoneTop : zone.Platform;

        // The underside is one pixel shorter at each end, so the ends look rounded.
        var left = bottom ? platform.Left + 1 : platform.Left;
        var right = bottom ? platform.Right - 1 : platform.Right;
        for (var x = left; x < right; x++)
            canvas.Set(x, y, color);
    }

    private void PaintGround(int y, int worldY)
    {
        for (var x = Layout.InnerLeft; x < Layout.InnerRight; x++)
        {
            var speck = TowerLooks.Mod(x * 7 + worldY * 13, 11) == 0;
            canvas.Set(x, y, speck ? TowerLooks.GroundSpeck : TowerLooks.Ground);
        }
    }

    private void PaintLabels(int top)
    {
        var highest = Tower.FloorAtOrBelow(top) + 1;
        var lowest = Math.Max(1, Tower.FloorAtOrBelow(top + canvas.Height));
        for (var floor = lowest; floor <= highest; floor++)
        {
            if (floor % Tower.LabelEvery != 0)
                continue;

            var platform = tower.GetFloor(floor);
            var x = platform.Left + (platform.Width - PixelFont.MeasureNumber(floor)) / 2;
            var y = Tower.RowOf(floor) + LabelOffset - top;
            PixelFont.DrawNumber(canvas, x, y, floor, TowerLooks.ZoneOf(floor).Platform);
        }
    }
}
