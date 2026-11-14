namespace PhysicsSandbox.Components;

/// <summary>Moves a kinematic body back and forth between <see cref="MinX"/> and <see cref="MaxX"/>.</summary>
public struct Patrol(float minX, float maxX, float speed)
{
    public float MinX = minX;
    public float MaxX = maxX;

    /// <summary>Cells per second.</summary>
    public float Speed = speed;
}
