using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Rendering;
using ConsoleEngine.Infrastructure.Rendering;
using SkyStack.Climbing;
using SkyStack.Rendering;

namespace SkyStack.Systems;

/// <summary>
/// Draws the part of the tower the camera sees, row by row: walls, a dotted brick backdrop, platforms, the ground and
/// a floor number under every <see cref="Tower.LabelEvery"/>th platform. Every cell of the tower is drawn every frame.
/// The colours of a row come from the zone of the floor at or below it.
/// </summary>
public sealed class TowerRenderSystem(Tower tower, Camera camera, RenderConsole console) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Rows per brick course of the backdrop; a dot row is drawn once per course.</summary>
    private const int BrickRows = 2;

    /// <summary>Columns per brick of the backdrop; every other course is shifted by half a brick.</summary>
    private const int BrickColumns = 8;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnUpdate(float deltaSeconds)
    {
        var top = (int)camera.Position.Y;
        for (var screenRow = 0; screenRow < console.Height; screenRow++)
            DrawRow(screenRow, top + screenRow);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void DrawRow(int screenRow, int worldRow)
    {
        var floor = Tower.FloorAtOrBelow(worldRow);
        ref readonly var zone = ref TowerLooks.ZoneOf(floor);

        var wallChar = TowerLooks.Mod(worldRow, BrickRows) == 0 ? TowerLooks.WallSeamChar : TowerLooks.WallChar;
        for (var column = 0; column < Layout.InnerLeft; column++)
            console.Draw(column, screenRow, wallChar, zone.Wall);
        for (var column = Layout.InnerRight; column < Layout.TowerWidth; column++)
            console.Draw(column, screenRow, wallChar, zone.Wall);

        if (worldRow > 0)
        {
            for (var column = Layout.InnerLeft; column < Layout.InnerRight; column++)
                console.Draw(column, screenRow, TowerLooks.GroundChar, TowerLooks.GroundColor);
            return;
        }

        DrawBackdrop(screenRow, worldRow, zone);

        if (worldRow == Tower.RowOf(floor))
            DrawPlatform(screenRow, floor, zone);
        else if (worldRow == Tower.RowOf(floor + 1) + 1)
            DrawLabel(screenRow, floor + 1);
    }

    private void DrawBackdrop(int screenRow, int worldRow, in TowerLooks.Zone zone)
    {
        if (TowerLooks.Mod(worldRow, BrickRows) != 1)
            return;

        var course = (worldRow - TowerLooks.Mod(worldRow, BrickRows)) / BrickRows;
        var shift = TowerLooks.Mod(course, 2) * BrickColumns / 2;
        for (var column = Layout.InnerLeft; column < Layout.InnerRight; column++)
        {
            if (TowerLooks.Mod(column + shift, BrickColumns) == 0)
                console.Draw(column, screenRow, TowerLooks.BackdropChar, zone.Backdrop);
        }
    }

    private void DrawPlatform(int screenRow, int floor, in TowerLooks.Zone zone)
    {
        var platform = tower.GetFloor(floor);
        var color = Tower.IsMilestone(floor) && floor > 0 ? TowerLooks.MilestoneColor : zone.Platform;
        for (var column = platform.Left; column < platform.Right; column++)
            console.Draw(column, screenRow, TowerLooks.PlatformChar, color);
    }

    /// <summary>The number of <paramref name="floor"/>, on the row just under its platform, for labelled floors.</summary>
    private void DrawLabel(int screenRow, int floor)
    {
        if (floor <= 0 || floor % Tower.LabelEvery != 0)
            return;

        var platform = tower.GetFloor(floor);
        var x = ConsoleText.Centered(platform.Left, platform.Right, ConsoleText.DigitCount(floor));
        ConsoleText.DrawNumber(console, x, screenRow, floor, TowerLooks.ZoneOf(floor).Platform);
    }
}
