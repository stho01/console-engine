using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using ConsoleEngine;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Rendering3D;
using ConsoleEngine.Rendering3D.OpenGL;
using Kartster.Driving;
using Kartster.Race;
using Kartster.Scenes;

namespace Kartster;

/// <summary>
/// 3D experiment: Kartster karts and a Kartster track, drawn by an <see cref="IRenderer3D"/> with one console cell
/// per pixel: the software <see cref="Rasterizer"/>, or the OpenGL <see cref="GlRenderer"/> (G toggles, falls back to
/// the CPU when OpenGL 3.3 isn't there), in the 16 scene colours or in 24-bit colour (C toggles, <c>--truecolor</c>
/// starts in it; stays on 16 colours when the console has no 24-bit output). Two scenes, swapped with Enter: on the
/// track (<see cref="TrackScene"/>) you drive the kart with Kartster's kart model on the engine's physics and a chase
/// camera behind it; in the garage (<see cref="GarageScene"/>) the camera orbits the kart on a small floor. Kartster's
/// axes: 1 unit is 1 metre, +Y up, -Z forward. The game holds what both scenes share: renderer, sky, projection, the karts and the track.
/// </summary>
public class KartsterGame(string kartsFolder, string tracksFolder, bool startOnGpu = false, bool startInTrueColor = false)
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
    private const int DefaultLaps = 3;

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
    private readonly uint[] _skyRgb = new uint[ScreenWidth * ScreenHeight];
    private const float RenderTimeSmoothing = 0.05f;

    private Rasterizer _cpu = new(ScreenWidth, ScreenHeight, ScenePalette);
    private string[] _kartFolders;
    private int _kartIndex;
    private GlRenderer _gpu;
    private bool _gpuUnavailable;
    private long _frameStart;
    private float _renderMilliseconds;

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>The GPU renderer when it is on, else the CPU one.</summary>
    public IRenderer3D Renderer => _gpu != null ? _gpu : _cpu;

    public Matrix4x4 Projection { get; } = Matrix4x4.CreatePerspectiveFieldOfView(
        FieldOfView, (float)ScreenWidth / ScreenHeight, NearPlane, FarPlane);

    public Mesh Kart { get; private set; }
    public string KartName { get; private set; }
    public KartTuning Tuning { get; private set; }

    /// <summary>The middle of the kart's bounding box, in model space.</summary>
    public Vector3 KartCenter { get; private set; }

    /// <summary>Null when there is no track: then only the garage runs.</summary>
    public Track Track { get; private set; }

    /// <summary>The track's <c>race.txt</c>; null when it has none (then the race uses a stand-in grid and line).</summary>
    public RaceMarkers Markers { get; private set; }

    /// <summary>The kart folders, sorted: the player picks one, the bots take them in turn.</summary>
    public string[] KartFolders => _kartFolders;

    private int RaceLaps => Markers?.LapCount ?? DefaultLaps;

    //**********************************************************
    //** game
    //**********************************************************

    protected override void OnInitialize()
    {
        Console.PixelMode = true;
        Console.Palette = ScenePalette.Colors;
        Console.TrueColor = startInTrueColor; // the renderers follow Console.TrueColorActive once it is known
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
        {
            var id = Path.GetFileName(ribbons[0])[..^".ribbon.obj".Length];
            Track = Track.Load(tracksFolder, id);
            Markers = File.Exists(Path.Combine(tracksFolder, id + ".race.txt")) ? RaceMarkers.Load(tracksFolder, id) : null;
        }

        for (var y = 0; y < ScreenHeight; y++)
        for (var x = 0; x < ScreenWidth; x++)
        {
            var sky = Vector3.Lerp(SkyTop, SkyHorizon, (float)y / ScreenHeight);
            _sky[y * ScreenWidth + x] = ScenePalette.ToConsoleColor(sky, x, y);
            _skyRgb[y * ScreenWidth + x] = SkyRgb(y, ScreenHeight);
        }

        if (startOnGpu)
            ToggleGpu();

        if (Track != null)
            Scenes.Set(new TrackScene(RaceLaps));
        else
            Scenes.Set(new GarageScene());

        GameTime.SetInterval(250, () => Console.SetTitle(Scene is TrackScene track
            ? $"Kartster - {KartName} on {Track.Name} - {track.Speed * 3.6f:0} km/h - gear {track.Dynamics.Gear} - " +
              $"{track.Dynamics.EngineRpm:0} rpm - {RendererLabel} 3D {_renderMilliseconds:0.00} ms - {ColorLabel} - " +
              $"{GameTime.Fps} fps"
            : $"Kartster - {KartName} ({Kart.TriangleCount} triangles) - {RendererLabel} 3D " +
              $"{_renderMilliseconds:0.00} ms - {ColorLabel} - {GameTime.Fps} fps"));
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
                Scenes.Set(new TrackScene(RaceLaps)); // the new kart's model and tuning, from the start
        }

        if (input.GetKey(Key.ENTER).Pressed && Track != null)
            Scenes.Set(onTrack ? new GarageScene() : new TrackScene(RaceLaps));
        else if (onTrack && input.GetKey(Key.R).Pressed)
            Scenes.Set(new TrackScene(RaceLaps));

        if (input.GetKey(Key.G).Pressed)
            ToggleGpu();
        if (input.GetKey(Key.C).Pressed)
            Console.TrueColor = !Console.TrueColor;
        MatchConsoleColors();

        // The scene's render (next in the frame) draws onto the sky.
        _frameStart = Stopwatch.GetTimestamp();
        if (Renderer.TrueColor)
            Renderer.Clear(_skyRgb);
        else
            Renderer.Clear(_sky);
    }

    protected override void OnRender()
    {
        Renderer.Finish();
        var milliseconds = (float)Stopwatch.GetElapsedTime(_frameStart).TotalMilliseconds;
        _renderMilliseconds += (milliseconds - _renderMilliseconds) * RenderTimeSmoothing;
        if (Renderer.TrueColor)
        {
            var rgb = Renderer.Rgb;
            for (var y = 0; y < ScreenHeight; y++)
            for (var x = 0; x < ScreenWidth; x++)
                Console.DrawRgb(x, y, ' ', 0, rgb[y * ScreenWidth + x]);
            return;
        }

        var colors = Renderer.Colors;
        for (var y = 0; y < ScreenHeight; y++)
        for (var x = 0; x < ScreenWidth; x++)
            Console.Draw(x, y, ' ', ConsoleColor.Black, colors[y * ScreenWidth + x]);
    }

    /// <summary>The sky in 24-bit colour at screen row <paramref name="y"/>: a smooth gradient, no dither.</summary>
    public static uint SkyRgb(int y, int height)
    {
        var sky = Vector3.Lerp(SkyTop, SkyHorizon, (float)y / height) * 255f + new Vector3(0.5f);
        return ((uint)sky.X << 16) | ((uint)sky.Y << 8) | (uint)sky.Z;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private string RendererLabel => _gpu != null ? "GPU" : _gpuUnavailable ? "CPU (no GPU)" : "CPU";

    private string ColorLabel => _cpu.TrueColor ? "24-bit" : Console.TrueColor ? "16 colours (no 24-bit)" : "16 colours";

    /// <summary>
    /// Makes the renderers draw 24-bit colour exactly while the console shows it: at start-up (known only after the
    /// console is initialized), after C, and when the console can't do it.
    /// </summary>
    private void MatchConsoleColors()
    {
        var trueColor = Console.TrueColorActive;
        if (_cpu.TrueColor == trueColor)
            return;

        _cpu.Dispose();
        _cpu = new Rasterizer(ScreenWidth, ScreenHeight, ScenePalette, trueColor);
        if (_gpu == null)
            return;

        _gpu.Dispose();
        if (!GlRenderer.TryCreate(ScreenWidth, ScreenHeight, ScenePalette, trueColor, out _gpu))
        {
            _gpu = null;
            _gpuUnavailable = true;
        }
    }

    /// <summary>
    /// Switches between the CPU and the GPU renderer. The GPU one is made the first time (on the game-loop thread, as
    /// OpenGL wants); if OpenGL 3.3 isn't there, the game stays on the CPU and says so in the title.
    /// </summary>
    private void ToggleGpu()
    {
        if (_gpu != null)
        {
            _gpu.Dispose();
            _gpu = null;
            return;
        }

        if (!GlRenderer.TryCreate(ScreenWidth, ScreenHeight, ScenePalette, _cpu.TrueColor, out _gpu))
        {
            _gpu = null;
            _gpuUnavailable = true;
        }
    }

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
