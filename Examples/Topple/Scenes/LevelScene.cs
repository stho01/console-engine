using System;
using ConsoleEngine.Ecs;
using Topple.Levels;
using Topple.Systems;

namespace Topple.Scenes;

/// <summary>
/// One attempt at a level. Physics, damage, shots and water run in the fixed step (60 Hz); the catapult, the round
/// rules and the effects every frame; drawing (interpolation first) in presentation. Restarting loads a new scene.
/// </summary>
public sealed class LevelScene(LevelPlan plan) : EcsScene<ToppleGame>
{
    protected override void OnCreate()
    {
        var setup = new LevelSetup(World, plan, Console.Width, Console.Height, Environment.TickCount);
        setup.AddFixedSystems(FixedSimulation);

        Simulation.Add(new CatapultSystem(setup.Catapult, setup.Round, setup.Effects));
        Simulation.Add(new RoundSystem(setup.Round, Game));
        setup.AddEffectSystems(Simulation);

        setup.AddPresentationSystems(Presentation, Console, FixedTimestep, Game.Campaign);
    }
}
