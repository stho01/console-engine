using System.Numerics;
using ConsoleEngine.Rendering3D;

namespace Kartster.Ui;

/// <summary>
/// What the menu scenes need from the game. <c>KartsterGame</c> implements it when the menus are wired in; it already
/// has <see cref="Renderer"/>, <see cref="Projection"/>, <see cref="Kart"/> and <see cref="KartCenter"/> under these
/// names.
/// </summary>
public interface IKartsterMenus
{
    /// <summary>Kart folder names, in the order <see cref="SelectKart"/> takes.</summary>
    string[] KartNames { get; }

    /// <summary>The kart chosen now.</summary>
    int KartIndex { get; }

    /// <summary>Track names (the part before <c>.ribbon.obj</c>), in the order <see cref="StartRace"/> takes.</summary>
    string[] TrackNames { get; }

    IRenderer3D Renderer { get; }
    Matrix4x4 Projection { get; }

    /// <summary>The chosen kart's model and the middle of its bounding box.</summary>
    Mesh Kart { get; }

    Vector3 KartCenter { get; }

    /// <summary>Makes kart <paramref name="index"/> the chosen one: loads its model and tuning.</summary>
    void SelectKart(int index);

    /// <summary>Loads the track and starts a race of <paramref name="laps"/> laps with the chosen kart.</summary>
    void StartRace(int trackIndex, int laps);
}
