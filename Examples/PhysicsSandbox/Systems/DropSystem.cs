using System;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Ecs;
using ConsoleEngine.Infrastructure.Inputs;
using Microsoft.Xna.Framework;
using PhysicsSandbox.Components;

namespace PhysicsSandbox.Systems;

/// <summary>
/// <c>SPACE</c> drops a ball, <c>B</c> a box, <c>ENTER</c> a shower of balls, <c>R</c> removes everything dropped.
/// Runs every frame, because key presses are one-frame edges. It only records structural changes, which the physics
/// sees from the next fixed step on.
/// </summary>
public sealed class DropSystem(BodySpawner spawner, Random random, float minX, float maxX, float dropY) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int ShowerCount = 50;

    /// <summary>Vertical gap between the balls of a shower, so they don't start out overlapping.</summary>
    private const float ShowerSpacing = 1.2f;

    private Query _dropped;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _dropped = World.Query(Query.With<Dropped>());

    protected override void OnUpdate(float deltaSeconds)
    {
        if (Input.Instance.GetKey(Key.SPACE).Pressed)
            spawner.CreateBall(Commands, RandomDropPoint(0f));

        if (Input.Instance.GetKey(Key.B).Pressed)
            spawner.CreateBox(Commands, RandomDropPoint(0f));

        if (Input.Instance.GetKey(Key.ENTER).Pressed)
        {
            for (var i = 0; i < ShowerCount; i++)
                spawner.CreateBall(Commands, RandomDropPoint(-i * ShowerSpacing));
        }

        if (Input.Instance.GetKey(Key.R).Pressed)
            RemoveAll();
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private Vector2 RandomDropPoint(float yOffset) =>
        new(minX + random.NextSingle() * (maxX - minX), dropY + yOffset);

    private void RemoveAll()
    {
        foreach (var chunk in _dropped.GetChunks())
        {
            foreach (var entity in chunk.Entities)
                Commands.Destroy(entity);
        }
    }
}
