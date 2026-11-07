using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Infrastructure.Rendering;
using PhysicsSandbox.Components;

namespace PhysicsSandbox.Systems;

/// <summary>
/// The top line: number of dropped bodies and the keys. The text is rebuilt only when the count changes, so a steady
/// frame allocates nothing.
/// </summary>
public sealed class HudSystem(RenderConsole console) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private Query _dropped;
    private int _shownCount = -1;
    private string _text;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _dropped = World.Query(Query.With<Dropped>());

    protected override void OnUpdate(float deltaSeconds)
    {
        var count = _dropped.Count;
        if (count != _shownCount)
        {
            _shownCount = count;
            _text = $" BODIES {count:N0}   SPACE BALL   B BOX   ENTER {DropSystem.ShowerCount} BALLS   " +
                    "R CLEAR   ESC QUIT ";
        }

        console.Draw(0, 0, _text, ConsoleColor.Black, ConsoleColor.Gray);
    }
}
