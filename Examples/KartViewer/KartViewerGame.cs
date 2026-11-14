using System;
using System.IO;
using System.Numerics;
using ConsoleEngine;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Rendering3D;
using KartViewer.Driving;
using KartViewer.Scenes;

namespace KartViewer;

/// <summary>
/// 3D experiment: Kartster karts and a Kartster track, drawn by an <see cref="IRenderer3D"/> (the software
/// <see cref="Rasterizer"/>) with one console cell per pixel. Two scenes, swapped with Enter: on the track (<see cref="TrackScene"/>) you drive the kart
/// with Kartster's kart model on the engine's physics and a chase camera behind it; in the garage
/// (<see cref="GarageScene"/>) the camera orbits the kart on a small floor. Kartster's axes: 1 unit is 1 metre, +Y up,
/// -Z forward. The game holds what both scenes share: renderer, sky, projection, the karts and the track.
/// </summary>
public class KartViewerGame(string kartsFolder, string tracksFolder)
    : GameBase(ScreenWidth, ScreenHeight, fontWidth: 3, fontHeight: 3)
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int ScreenWidth = 320;
    private const int ScreenHeight = 180;
    private const float FieldOfView = 50f * MathF.PI / 180f;
    private const float NearPlane = 0.2f;
    private const float FarPlane = 400f;

    public static readonly Vector3 ToSun = Vector3.Normalize(new Vector3(0.5f, 0.8f, 0.3f));
    private static readonly Vector3 PlayerColor = new(0.1f, 0.35f, 0.85f);
    private static readonly Vector3 SkyTop = new(0.35f, 0.55f, 0.95f);
    private static readonly Vector3 SkyHorizon = new(0.85f, 0.85f, 0.95f);

    /// <summary>Colours for this scene: greys for asphalt, tyres and chrome, greens, sky blues, the player blue.</summary>
    private static readonly ConsolePalette ScenePalette = new([
        0x080808, 0x2B2B2D, 0x4A4A4C, 0x6B6B6E, // greys: tyres, shadowed and lit asphalt
        0x949497, 0xC4C4C6, 0xF0F0F0,           // greys: chrome and white
        0x22521A, 0x346B24, 0x4A8530,           // grass
        0x6A96EE, 0xA2BEF0, 0xD6DBF0,           // sky
        0x0F2A78, 0x2060D8,                     // player blue
        0xB06A30                                // wood, leather, red lamps
    ]);

    private readonly ConsoleColor[] _sky = new ConsoleColor[ScreenWidth * ScreenHeight];
    private string[] _kartFolders;
    private int _kartIndex;

    //**********************************************************
    //** props
    //**********************************************************

    public IRenderer3D Renderer { get; } = new Rasterizer(ScreenWidth, ScreenHeight, ScenePalette);

    public Matrix4x4 Projection { get; } = Matrix4x4.CreatePerspectiveFieldOfView(
        FieldOfView, (float)ScreenWidth / ScreenHeight, NearPlane, FarPlane);

    public Mesh Kart { get; private set; }
    public string KartName { get; private set; }
    public KartTuning Tuning { get; private set; }

    /// <summary>The middle of the kart's bounding box, in model space.</summary>
    public Vector3 KartCenter { get; private set; }

    /// <summary>Null when there is no track: then only the garage runs.</summary>
    public Track Track { get; private set; }

    //**********************************************************
    //** game
    //**********************************************************

    protected override void OnInitialize()
    {
        Console.PixelMode = true;
        Console.Palette = ScenePalette.Colors;
        ClearScreenOnEachFrame = false; // every cell is drawn each frame

        _kartFolders = Directory.GetDirectories(kartsFolder);
        if (_kartFolders.Length == 0)
            throw new DirectoryNotFoundException($"No kart folders in {Path.GetFullPath(kartsFolder)}");
        Array.Sort(_kartFolders, StringComparer.Ordinal);
        LoadKart(0);

        // shortcut: drives the first track in the folder; cycle through them once there is more than one.
        var ribbons = Directory.Exists(tracksFolder) ? Directory.GetFiles(tracksFolder, "*.ribbon.obj") : [];
        Array.Sort(ribbons, StringComparer.Ordinal);
        if (ribbons.Length > 0)
            Track = Track.Load(tracksFolder, Path.GetFileName(ribbons[0])[..^".ribbon.obj".Length]);

        for (var y = 0; y < ScreenHeight; y++)
        for (var x = 0; x < ScreenWidth; x++)
        {
            var sky = Vector3.Lerp(SkyTop, SkyHorizon, (float)y / ScreenHeight);
            _sky[y * ScreenWidth + x] = ScenePalette.ToConsoleColor(sky, x, y);
        }

        if (Track != null)
            Scenes.Set(new TrackScene());
        else
            Scenes.Set(new GarageScene());

        GameTime.SetInterval(250, () => Console.SetTitle(Scene is TrackScene track
            ? $"KartViewer - {KartName} on {Track.Name} - {track.Speed * 3.6f:0} km/h - gear {track.Dynamics.Gear} - " +
              $"{track.Dynamics.EngineRpm:0} rpm - {GameTime.Fps} fps"
            : $"KartViewer - {KartName} ({Kart.TriangleCount} triangles) - {GameTime.Fps} fps"));
    }

    protected override void OnUpdate()
    {
        var input = Input.Instance;
        var onTrack = Scene is TrackScene;

        if (input.GetKey(Key.ESCAPE).Pressed)
            Stop();
        if (input.GetKey(Key.TAB).Pressed)
        {
            LoadKart((_kartIndex + 1) % _kartFolders.Length);
            if (onTrack)
                Scenes.Set(new TrackScene()); // the new kart's model and tuning, from the start
        }

        if (input.GetKey(Key.ENTER).Pressed && Track != null)
            Scenes.Set(onTrack ? new GarageScene() : new TrackScene());
        else if (onTrack && input.GetKey(Key.R).Pressed)
            Scenes.Set(new TrackScene());

        // The scene's render (next in the frame) draws onto the sky.
        Renderer.Clear(_sky);
    }

    protected override void OnRender()
    {
        Renderer.Finish();
        var colors = Renderer.Colors;
        for (var y = 0; y < ScreenHeight; y++)
        for (var x = 0; x < ScreenWidth; x++)
            Console.Draw(x, y, ' ', ConsoleColor.Black, colors[y * ScreenWidth + x]);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void LoadKart(int index)
    {
        _kartIndex = index;
        KartName = Path.GetFileName(_kartFolders[index]);
        Kart = KartModel.Load(_kartFolders[index], PlayerColor);
        Tuning = KartCatalog.ForKart(KartName);

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var position in Kart.Positions)
        {
            min = Vector3.Min(min, position);
            max = Vector3.Max(max, position);
        }

        KartCenter = (min + max) / 2;
    }
}
