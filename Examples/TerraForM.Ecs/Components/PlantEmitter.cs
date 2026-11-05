namespace TerraForM.Ecs.Components;

/// <summary>Spawns rings of <see cref="Grass"/> around its <c>Position</c> for a while after planting.</summary>
public struct PlantEmitter
{
    public float ElapsedMs;
    public float SinceLastEmitMs;

    /// <summary>Rings spawned so far. Each ring is one cell wider than the previous one.</summary>
    public int Rings;
}
