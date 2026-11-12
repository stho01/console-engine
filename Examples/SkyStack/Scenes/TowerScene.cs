using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using Microsoft.Xna.Framework;
using SkyStack.Climbing;
using SkyStack.Components;
using SkyStack.Systems;

namespace SkyStack.Scenes;

/// <summary>
/// One run: the climber entity on the ground floor of a <see cref="Tower"/> built from <paramref name="seed"/>, and the
/// systems in the order they run. Physics, combo timer and scrolling run in the fixed step; input and restart every
/// frame; drawing goes tower, climber, messages, HUD. A restart replaces the scene with a new one and a new seed.
/// </summary>
public sealed class TowerScene(int seed) : EcsScene<SkyStackGame>
{
    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate()
    {
        var tower = new Tower(seed);
        var run = new RunStats();
        var scroller = new Scroller(-Layout.GroundScreenRow);
        Camera.Position = new Vector2(0f, scroller.Top);

        var startX = (Layout.InnerLeft + Layout.InnerRight - ClimbPhysics.BodyWidth) / 2f;
        World.Create(
            new Position(new Vector2(startX, Tower.RowOf(0))),
            new Velocity(Vector2.Zero),
            new Climber { IsGrounded = true });

        FixedSimulation.Add(new ClimberMotionSystem(tower, run));
        FixedSimulation.Add(new ScrollSystem(scroller, run, Camera, Game));

        Simulation.Add(new ClimberInputSystem(run));
        Simulation.Add(new RestartSystem(run, Restart));

        Presentation.Add(new TowerRenderSystem(tower, Camera, Console));
        Presentation.Add(new ClimberRenderSystem(run, Camera, Console));
        Presentation.Add(new MessageRenderSystem(run, scroller, Game, Console));
        Presentation.Add(new HudRenderSystem(run, scroller, Game, Console));
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void Restart() => Game.Scenes.Set(new TowerScene(Environment.TickCount));
}
