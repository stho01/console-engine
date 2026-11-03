using ConsoleEngine.Infrastructure.Scenery;

namespace ConsoleEngine.Tests.Fakes;

/// <summary>Scene that counts how often each lifecycle hook ran.</summary>
public class RecordingScene : Scene
{
    public int Loaded { get; private set; }
    public int Unloaded { get; private set; }
    public int Updated { get; private set; }
    public int Rendered { get; private set; }

    public override void OnLoad() => Loaded++;
    public override void OnUnload() => Unloaded++;
    public override void OnUpdate() => Updated++;
    public override void OnRender() => Rendered++;
}
