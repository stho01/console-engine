using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using EternalDigger.Components;
using EternalDigger.Mining;
using Microsoft.Xna.Framework;

namespace EternalDigger.Systems;

/// <summary>
/// Moves the lift car and carries a riding miner with it. When a called car arrives where the miner waits, the miner
/// gets in. Runs after <see cref="MinerMotionSystem"/>, so the rider's <c>Position</c> is the car's for this frame.
/// </summary>
public sealed class ElevatorSystem(Elevator elevator) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _miners;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _miners = World.Query(Query.With<Miner, Position>());

    protected override void OnUpdate(float deltaSeconds)
    {
        var arrived = elevator.Update(deltaSeconds);
        foreach (var chunk in _miners.GetChunks())
        {
            var miners = chunk.GetSpan<Miner>();
            var positions = chunk.GetSpan<Position>();
            for (var i = 0; i < chunk.Count; i++)
                Carry(ref miners[i], ref positions[i], arrived);
        }
    }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// One frame for the miner after the car moved: picks up a waiting caller when the car has just
    /// <paramref name="arrived"/>, and keeps a rider in the car.
    /// </summary>
    public void Carry(ref Miner miner, ref Position position, bool arrived)
    {
        if (!miner.IsRiding)
        {
            if (!arrived || !elevator.IsCalled || miner.IsMoving || miner.Column != Elevator.Column ||
                miner.Row != elevator.CarTargetRow)
                return;

            miner.IsRiding = true;
            elevator.PickedUp();
        }

        miner.Row = (int)System.MathF.Round(elevator.CarRow);
        miner.MoveFrom = new Vector2(miner.Column, miner.Row);
        miner.Motion = MinerMotion.Idle;
        miner.FallRows = 0;
        position.Value = new Vector2(miner.Column * Layout.TileWidth, elevator.CarRow * Layout.TileHeight);
    }
}
