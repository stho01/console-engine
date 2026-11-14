using System;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Physics;
using Microsoft.Xna.Framework;
using PhysicsShowcase.Components;

namespace PhysicsShowcase.Systems;

/// <summary>
/// A/D run, W jumps: latched into <see cref="Player"/> every frame (key presses are one-frame edges) and used by
/// <see cref="PlayerMoveSystem"/> in the next fixed step.
/// </summary>
public sealed class PlayerInputSystem : EntitySystem
{
    private Query _players;

    protected override void OnCreate() => _players = World.Query(Query.With<Player>());

    protected override void OnUpdate(float deltaSeconds)
    {
        var direction = 0f;
        if (Input.Instance.GetKey(Key.A).Held) direction -= 1f;
        if (Input.Instance.GetKey(Key.D).Held) direction += 1f;
        var jump = Input.Instance.GetKey(Key.W).Pressed;

        foreach (var chunk in _players.GetChunks())
        {
            var players = chunk.GetSpan<Player>();
            for (var i = 0; i < chunk.Count; i++)
            {
                players[i].RunDirection = direction;
                if (direction != 0f)
                    players[i].Facing = direction;
                if (jump)
                    players[i].JumpRequested = true;
            }
        }
    }
}

/// <summary>
/// Number keys drop bodies at a random place along the top, ENTER drops a shower of mixed shapes, R removes everything
/// dropped. Structural changes go through the command buffer and show up from the next fixed step.
/// </summary>
public sealed class DropSystem(BodyFactory factory, Random random, float minX, float maxX, float dropY) : EntitySystem
{
    public const int ShowerCount = 30;

    private Query _dropped;

    protected override void OnCreate() => _dropped = World.Query(Query.With<Dropped>());

    protected override void OnUpdate(float deltaSeconds)
    {
        var input = Input.Instance;
        if (input.GetKey(Key.K1).Pressed) factory.CreateBall(Commands, RandomDropPoint(0f));
        if (input.GetKey(Key.K2).Pressed) factory.CreateBox(Commands, RandomDropPoint(0f));
        if (input.GetKey(Key.K3).Pressed) factory.CreateTriangle(Commands, RandomDropPoint(0f));
        if (input.GetKey(Key.K4).Pressed) factory.CreatePentagon(Commands, RandomDropPoint(0f));
        if (input.GetKey(Key.K5).Pressed) factory.CreateGhost(Commands, RandomDropPoint(0f));

        if (input.GetKey(Key.ENTER).Pressed)
        {
            // Spread out vertically so the shower doesn't start out overlapping.
            for (var i = 0; i < ShowerCount; i++)
                factory.CreateRandom(Commands, RandomDropPoint(i * 2.5f));
        }

        if (input.GetKey(Key.R).Pressed)
        {
            foreach (var chunk in _dropped.GetChunks())
            {
                foreach (var entity in chunk.Entities)
                    Commands.Destroy(entity);
            }
        }
    }

    private Vector2 RandomDropPoint(float extraHeight) =>
        new(minX + random.NextSingle() * (maxX - minX), dropY + (random.NextSingle() * 3f - extraHeight) * .5f);
}

/// <summary>
/// E: a blast around the player. <see cref="IPhysicsQueries.OverlapCircle"/> finds the bodies in range, which get
/// a push away from the player and a spin. Setting their velocity also wakes sleeping ones.
/// </summary>
public sealed class BlastSystem(IPhysicsQueries physics, Random random) : EntitySystem
{
    public const float Radius = 10f;
    private const float Strength = 45f;

    private Query _players;

    protected override void OnCreate() => _players = World.Query(Query.With<Player, Position>());

    protected override void OnUpdate(float deltaSeconds)
    {
        if (!Input.Instance.GetKey(Key.E).Pressed)
            return;

        var center = Vector2.Zero;
        foreach (var chunk in _players.GetChunks())
        {
            if (chunk.Count > 0)
                center = chunk.GetSpan<Position>()[0].Value;
        }

        Span<Entity> hits = stackalloc Entity[256];
        var count = physics.OverlapCircle(center, Radius, Layers.Solid | Layers.Ghost, hits);
        for (var i = 0; i < count; i++)
        {
            var entity = hits[i];
            if (!World.Has<RigidBody>(entity) || World.Get<RigidBody>(entity).Type != BodyType.Dynamic)
                continue;

            var away = World.Get<Position>(entity).Value - center;
            var distance = MathF.Max(away.Length(), .5f);
            var push = away / distance * (Strength * (1f - distance / (Radius + 1f))) + new Vector2(0f, -10f);
            World.Get<Velocity>(entity).Value += push;
            if (World.Has<AngularVelocity>(entity))
                World.Get<AngularVelocity>(entity).Value += (random.NextSingle() * 2f - 1f) * 8f;
        }
    }
}

/// <summary>
/// G turns gravity upside down (and wakes every body: changing gravity doesn't wake sleeping bodies by itself, as in
/// Box2D). I switches interpolation on and off.
/// </summary>
public sealed class SwitchSystem(PhysicsSystem physics, InterpolationSystem interpolation, ShowcaseState state)
    : EntitySystem
{
    private Query _bodies;
    private Query _offsets;

    protected override void OnCreate()
    {
        _bodies = World.Query(Query.With<RigidBody>());
        _offsets = World.Query(Query.With<RenderOffset>());
    }

    protected override void OnUpdate(float deltaSeconds)
    {
        if (Input.Instance.GetKey(Key.G).Pressed)
        {
            physics.Gravity = -physics.Gravity;
            foreach (var chunk in _bodies.GetChunks())
            {
                var bodies = chunk.GetSpan<RigidBody>();
                for (var i = 0; i < chunk.Count; i++)
                    bodies[i].IsSleeping = false;
            }
        }

        if (Input.Instance.GetKey(Key.I).Pressed)
        {
            state.Smoothing = !state.Smoothing;
            interpolation.Enabled = state.Smoothing;
            if (!state.Smoothing)
            {
                foreach (var chunk in _offsets.GetChunks())
                    chunk.GetSpan<RenderOffset>().Clear();
            }
        }
    }
}
