using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Ecs.Physics;
using ConsoleEngine.Ecs.Systems;
using Microsoft.Xna.Framework;
using PhysicsSandbox.Components;
using PhysicsSandbox.Systems;

namespace PhysicsSandbox.Scenes;

/// <summary>
/// A walled box with two fixed ledges and a platform that moves left and right. Physics and the platform run in the
/// fixed step; dropping bodies (key presses) runs every frame.
/// </summary>
public sealed class SandboxScene : EcsScene<PhysicsSandboxGame>
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Cells/s², downwards.</summary>
    private const float Gravity = 30f;

    private const float PlatformSpeed = 12f;

    /// <summary>About one body: every dropped body is one cell.</summary>
    private const float CellSize = 2f;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate()
    {
        var width = Console.Width;
        var height = Console.Height;

        // Floor, side walls and two ledges: static, drawn as blocks. Positions are centres, sizes are half sizes.
        CreateWall(new Vector2(width / 2f, height - 1.5f), new Vector2(width / 2f, 1.5f));
        CreateWall(new Vector2(.5f, height / 2f), new Vector2(.5f, height / 2f));
        CreateWall(new Vector2(width - .5f, height / 2f), new Vector2(.5f, height / 2f));
        CreateWall(new Vector2(width * .25f, height * .45f), new Vector2(width * .15f, .5f));
        CreateWall(new Vector2(width * .75f, height * .3f), new Vector2(width * .15f, .5f));

        World.Create(
            new Position(new Vector2(width / 2f, height * .7f)),
            new Velocity(new Vector2(PlatformSpeed, 0f)),
            RigidBody.Kinematic(),
            Collider.Box(new Vector2(8f, .5f)),
            new Block('▒', ConsoleColor.Magenta),
            new Patrol(width * .2f, width * .8f, PlatformSpeed));

        var random = new Random();
        FixedSimulation.Add(new PhysicsSystem(CellSize) { Gravity = new Vector2(0f, Gravity) });
        FixedSimulation.Add(new PatrolSystem());

        Simulation.Add(new DropSystem(new BodySpawner(random), random, minX: 2f, maxX: width - 2f, dropY: 3f));

        Presentation.Add(new BlockRenderSystem(Console));
        Presentation.Add(new GlyphRenderSystem(Console, Camera));
        Presentation.Add(new HudSystem(Console));
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void CreateWall(Vector2 center, Vector2 halfSize) =>
        World.Create(new Position(center), Collider.Box(halfSize), new Block('█', ConsoleColor.DarkGray));
}
