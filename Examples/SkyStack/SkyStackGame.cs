using ConsoleEngine;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure.Inputs;
using SkyStack.Scenes;

namespace SkyStack;

/// <summary>
/// Climb an endless tower by jumping from floor to floor while the screen scrolls up faster and faster. Running fast
/// makes jumps higher, the walls bounce you back, and jumps over two or more floors chain into combos. Starts on the
/// <see cref="TitleScene"/>; a run is one ECS scene, <see cref="TowerScene"/>. <c>ESC</c> goes from a run back to the
/// title, and quits on the title. 8 x 16 font, so a cell is twice as tall as it is wide.
/// </summary>
public class SkyStackGame() : GameBase(Layout.ScreenWidth, Layout.ScreenHeight, fontWidth: 8, fontHeight: 16)
{
    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>The best score since the game was started. Not saved.</summary>
    public int BestScore { get; private set; }

    /// <summary>The highest floor reached since the game was started. Not saved.</summary>
    public int BestFloor { get; private set; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Keeps the best score and floor of a finished run.</summary>
    public void RecordRun(int score, int floor)
    {
        if (score > BestScore)
            BestScore = score;
        if (floor > BestFloor)
            BestFloor = floor;
    }

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnInitialize() => Scenes.Set(new TitleScene());

    protected override void OnUpdate()
    {
        if (!Input.Instance.GetKey(Key.ESCAPE).Pressed)
            return;

        if (Scene is TowerScene)
            Scenes.Set(new TitleScene());
        else
            Stop();
    }

    protected override void OnRender() { }
}
