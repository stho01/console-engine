using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Rendering;
using EternalDigger.Mining;
using EternalDigger.Rendering;

namespace EternalDigger.Systems;

/// <summary>
/// The lift, drawn over the terrain: dark grey shaft walls <c>║</c> on both edges of the shaft column with <c>╪</c>
/// braces every <see cref="BraceEvery"/> m, a grey cable <c>│</c> from the surface to the car, and the car itself.
/// The car is a roof <c>╔══╗</c> one row above its tile and a floor <c>╚══╝</c> one row below, with <c>║</c> sides,
/// so the miner inside keeps his own cells. While the car moves, <c>¦</c> speed lines stream past the walls.
/// </summary>
public sealed class ElevatorRenderSystem(Canvas canvas, Camera camera, Elevator elevator) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int BraceEvery = 5;
    public const string CarRoof = "╔══╗";
    public const string CarFloor = "╚══╝";

    private const ConsoleColor WallColor = ConsoleColor.DarkGray;
    private const ConsoleColor CableColor = ConsoleColor.Gray;
    private const ConsoleColor CarColor = ConsoleColor.DarkYellow;
    private const ConsoleColor CarTrim = ConsoleColor.Yellow;
    private const ConsoleColor SpeedLineColor = ConsoleColor.Gray;

    /// <summary>Speed lines move this many cells per second past the walls, and repeat every this many cells.</summary>
    private const float SpeedLineSpeed = 40f;
    private const int SpeedLineSpacing = 3;
    private const int SpeedLineReach = 8;

    private const int Left = Elevator.Column * Layout.TileWidth;
    private const int Right = Left + Layout.TileWidth - 1;

    private float _time;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnUpdate(float deltaSeconds)
    {
        _time += deltaSeconds;
        if (!elevator.IsBuilt)
            return;

        var originX = (int)MathF.Floor(camera.Position.X);
        var originY = (int)MathF.Floor(camera.Position.Y);
        var carY = (int)MathF.Floor(elevator.CarRow * Layout.TileHeight);

        DrawShaft(originX, originY);
        DrawCable(originX, originY, carY);
        DrawCar(originX, originY, carY);
        if (elevator.IsCarMoving)
            DrawSpeedLines(originX, originY, carY);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void DrawShaft(int originX, int originY)
    {
        var top = Math.Max((Mine.SurfaceRow + 1) * Layout.TileHeight, originY);
        var bottom = Math.Min((elevator.BottomRow + 1) * Layout.TileHeight - 1, originY + canvas.Height - 1);
        for (var y = top; y <= bottom; y++)
        {
            var isBrace = y % Layout.TileHeight == 0 && Mine.Depth(y / Layout.TileHeight) % BraceEvery == 0;
            var wall = isBrace ? '╪' : '║';
            canvas.DrawOver(Left - originX, y - originY, wall, WallColor);
            canvas.DrawOver(Right - originX, y - originY, wall, WallColor);
        }
    }

    private void DrawCable(int originX, int originY, int carY)
    {
        var top = Math.Max(Elevator.TopRow * Layout.TileHeight - 2, originY);
        var bottom = Math.Min(carY - 2, originY + canvas.Height - 1);
        for (var y = top; y <= bottom; y++)
            canvas.DrawOver(Left + 1 - originX, y - originY, '│', CableColor);
    }

    private void DrawCar(int originX, int originY, int carY)
    {
        var x = Left - originX;
        var y = carY - originY;
        for (var i = 0; i < Layout.TileWidth; i++)
        {
            canvas.DrawOver(x + i, y - 1, CarRoof[i], i is 0 or 3 ? CarTrim : CarColor);
            canvas.DrawOver(x + i, y + Layout.TileHeight, CarFloor[i], i is 0 or 3 ? CarTrim : CarColor);
        }

        for (var row = 0; row < Layout.TileHeight; row++)
        {
            canvas.DrawOver(x, y + row, '║', CarColor);
            canvas.DrawOver(x + Layout.TileWidth - 1, y + row, '║', CarColor);
        }
    }

    /// <summary>Dashes on both sides of the shaft near the car, moving against the ride.</summary>
    private void DrawSpeedLines(int originX, int originY, int carY)
    {
        var direction = elevator.CarTargetRow > elevator.CarRow ? -1 : 1;
        var phase = (int)MathF.Floor(_time * SpeedLineSpeed) * direction;
        for (var y = carY - SpeedLineReach; y <= carY + SpeedLineReach; y++)
        {
            if (((y + phase) % SpeedLineSpacing + SpeedLineSpacing) % SpeedLineSpacing != 0)
                continue;

            canvas.DrawOver(Left - 1 - originX, y - originY, '¦', SpeedLineColor);
            canvas.DrawOver(Right + 1 - originX, y - originY, '¦', SpeedLineColor);
        }
    }
}
