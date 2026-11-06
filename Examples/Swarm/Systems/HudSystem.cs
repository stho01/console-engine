using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Infrastructure.Rendering;
using Swarm.Components;

namespace Swarm.Systems;

/// <summary>
/// The top line: particle count and keys. The text is rebuilt only when the count changes, so a steady frame
/// allocates nothing.
/// </summary>
public sealed class HudSystem(RenderConsole console) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _particles;
    private int _shownCount = -1;
    private string _text;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _particles = World.Query(Query.With<Particle>());

    protected override void OnUpdate(float deltaSeconds)
    {
        var count = _particles.Count;
        if (count != _shownCount)
        {
            _shownCount = count;
            _text = $" PARTICLES {count:N0}   SPACE +{SpawnControlSystem.Step:N0}   " +
                    $"BACKSPACE -{SpawnControlSystem.Step:N0}   ESC QUIT ";
        }

        console.Draw(0, 0, _text, ConsoleColor.Black, ConsoleColor.Gray);
    }
}
