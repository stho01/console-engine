using System;
using System.Collections.Generic;
using ConsoleEngine.Infrastructure.Objects;
using ConsoleEngine.Infrastructure.Rendering;

namespace ConsoleEngine.Tests.Fakes;

/// <summary>Behaviour that records every call, in order.</summary>
public class RecordingBehaviour : IBehaviour
{
    public List<string> Calls { get; } = new();
    public TimeSpan LastDelta { get; private set; }

    public void Start(GameObject owner) => Calls.Add("start");

    public void Update(GameObject owner, TimeSpan delta)
    {
        LastDelta = delta;
        Calls.Add("update");
    }

    public void Render(GameObject owner, RenderConsole console) => Calls.Add("render");
}
