namespace ConsoleEngine.Ecs.Physics;

/// <summary>
/// Two non-sensor colliders started or stopped touching. <see cref="A"/> has the lower entity id. In an end event
/// either entity may already be dead: the pair ends because one of them was destroyed.
/// </summary>
public readonly struct ContactEvent(Entity a, Entity b)
{
    public Entity A { get; } = a;
    public Entity B { get; } = b;
}

/// <summary>
/// <see cref="Visitor"/> started or stopped overlapping the sensor collider of <see cref="Sensor"/>. In an end event
/// either entity may already be dead.
/// </summary>
public readonly struct SensorEvent(Entity sensor, Entity visitor)
{
    public Entity Sensor { get; } = sensor;
    public Entity Visitor { get; } = visitor;
}
