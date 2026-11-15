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
using Kartster.Modes;
using Kartster.Race;
using Kartster.Scenes;
using Kartster.Scenes.Menus;
using Kartster.Ui;

namespace Kartster;

/// <summary>
/// Kartster in the console: Kartster karts on Kartster tracks, drawn by an <see cref="IRenderer3D"/> with one console
/// cell per pixel: the software <see cref="Rasterizer"/>, or the OpenGL <see cref="GlRenderer"/> (G toggles, falls
/// back to the CPU when OpenGL 3.3 isn't there), in the 16 scene colours or in 24-bit colour (C toggles,
/// <c>--truecolor</c> starts in it; stays on 16 colours when the console has no 24-bit output). G and C work in every
/// scene; every other key belongs to the scene. Flow: <see cref="TitleScene"/> → <see cref="SelectKartScene"/> →
/// <see cref="SelectTrackScene"/> → <see cref="TrackScene"/> (the race, with its HUD) → <see cref="ResultsScene"/>;
/// the title also opens the <see cref="GarageScene"/>, where the camera orbits the chosen kart. Kartster's axes: 1 unit
/// is 1 metre, +Y up, -Z forward. The game holds what the scenes share (<see cref="IKartsterMenus"/>): renderer, sky,
/// projection, the karts and the track, and draws a scene's UI (<see cref="IUiLayer"/>) over the 3D picture.
/// </summary>
public class KartsterGame(string kartsFolder, string tracksFolder, bool startOnGpu = false, bool startInTrueColor = false)
    : GameBase(ScreenWidth, ScreenHeight, fontWidth: 3, fontHeight: 3), IKartsterMenus
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

    /// <summary>
    /// Colours for this scene: greys for asphalt, tyres and chrome, greens, sky blues, the player blue, brown and
    /// yellow. The yellow is the UI's (<c>UiColors.Selected</c>), and also the kerbs, tyre stacks and yellow kart parts;
    /// it took the place of the mid grey <c>0x949497</c>, which dithers from its neighbours.
    /// </summary>
    public static readonly ConsolePalette ScenePalette = new([
        0x080808, 0x2B2B2D, 0x4A4A4C, 0x6B6B6E, // greys: tyres, shadowed and lit asphalt
        0xC4C4C6, 0xF0F0F0,                     // greys: chrome and white
        0x22521A, 0x346B24, 0x4A8530,           // grass
        0x6A96EE, 0xA2BEF0, 0xD6DBF0,           // sky
        0x0F2A78, 0x2060D8,                     // player blue
        0xB06A30,                               // wood, leather, red lamps
        0xFFD23F                                // yellow: UI selection, kerbs, yellow parts
    ]);

    private readonly ConsoleColor[] _sky = new ConsoleColor[ScreenWidth * ScreenHeight];
    private readonly uint[] _skyRgb = new uint[ScreenWidth * ScreenHeight];
    private const float RenderTimeSmoothing = 0.05f;

    private Rasterizer _cpu = new(ScreenWidth, ScreenHeight, ScenePalette);

    /// <summary>The UI's frames over the finished 3D picture; <c>Frame</c> follows the renderer each frame.</summary>
    private readonly PaletteTarget _paletteTarget = new(null, ScreenWidth, ScreenHeight, ScenePalette);
    private readonly RgbTarget _rgbTarget = new(null, ScreenWidth, ScreenHeight);
    private string[] _kartFolders;
    private string[] _kartNames;
    private int _kartIndex;
    private string[] _trackNames = [];
    private int _trackIndex = -1;
    private GlRenderer _gpu;
    private bool _gpuUnavailable;
    private long _frameStart;
    private float _renderMilliseconds;
    private readonly ConsoleColor[] _washLookup = new ConsoleColor[16];

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

    /// <summary>The track of the race; null until <see cref="StartRace"/>.</summary>
    public Track Track { get; private set; }

    /// <summary>The track's <c>race.txt</c>; null when it has none (then the race uses a stand-in grid and line).</summary>
    public RaceMarkers Markers { get; private set; }

    /// <summary>The kart folders, sorted: the player picks one, the bots take them in turn.</summary>
    public string[] KartFolders => _kartFolders;

    public string[] KartNames => _kartNames;
    public int KartIndex => _kartIndex;

    /// <summary>This frame's 3D picture is mirrored left to right before the UI (the glitch); cleared each frame.</summary>
    public bool MirrorView { get; set; }

    /// <summary>How far this frame's 3D picture is washed dark before the UI, 0..1 (smoke); cleared each frame.</summary>
    public float ViewDarkness { get; set; }

    /// <summary>Every <c>&lt;id&gt;.ribbon.obj</c> in the tracks folder, sorted; empty without one (no race then).</summary>
    public string[] TrackNames => _trackNames;

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
        _kartNames = Array.ConvertAll(_kartFolders, Path.GetFileName);
        LoadKart(0);

        var ribbons = Directory.Exists(tracksFolder) ? Directory.GetFiles(tracksFolder, "*.ribbon.obj") : [];
        Array.Sort(ribbons, StringComparer.Ordinal);
        _trackNames = Array.ConvertAll(ribbons, ribbon => Path.GetFileName(ribbon)[..^".ribbon.obj".Length]);

        for (var y = 0; y < ScreenHeight; y++)
        for (var x = 0; x < ScreenWidth; x++)
        {
            var sky = Vector3.Lerp(SkyTop, SkyHorizon, (float)y / ScreenHeight);
            _sky[y * ScreenWidth + x] = ScenePalette.ToConsoleColor(sky, x, y);
            _skyRgb[y * ScreenWidth + x] = SkyRgb(y, ScreenHeight);
        }

        if (startOnGpu)
            ToggleGpu();

        Scenes.Set(new TitleScene());

        GameTime.SetInterval(250, () => Console.SetTitle(Scene switch
        {
            TrackScene track =>
                $"Kartster - {KartName} on {Track.Name} - {track.Speed * 3.6f:0} km/h - gear {track.Dynamics.Gear} - " +
                $"{track.Dynamics.EngineRpm:0} rpm - {RendererLabel} 3D {_renderMilliseconds:0.00} ms - {ColorLabel} - " +
                $"{GameTime.Fps} fps",
            GarageScene =>
                $"Kartster - {KartName} ({Kart.TriangleCount} triangles) - {RendererLabel} 3D " +
                $"{_renderMilliseconds:0.00} ms - {ColorLabel} - {GameTime.Fps} fps",
            _ => $"Kartster - {Scene?.GetType().Name} - {RendererLabel} 3D {_renderMilliseconds:0.00} ms - " +
                 $"{ColorLabel} - {GameTime.Fps} fps"
        }));
    }

    protected override void OnUpdate()
    {
        // Only G and C are global: every other key belongs to the scene (menus, race, garage).
        var input = Input.Instance;
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
        var mirror = MirrorView;
        var darkness = ViewDarkness;
        MirrorView = false;
        ViewDarkness = 0f;
        if (Renderer.TrueColor)
        {
            var rgb = Renderer.Rgb;
            if (mirror)
                ViewEffects.Mirror<uint>(rgb, ScreenWidth);
            if (darkness > 0f)
                ViewEffects.Wash(rgb, darkness);
            if (Scene is IUiLayer rgbUi)
            {
                _rgbTarget.Frame = rgb;
                rgbUi.DrawUi(_rgbTarget);
            }

            for (var y = 0; y < ScreenHeight; y++)
            for (var x = 0; x < ScreenWidth; x++)
                Console.DrawRgb(x, y, ' ', 0, rgb[y * ScreenWidth + x]);
            return;
        }

        var colors = Renderer.Colors;
        if (mirror)
            ViewEffects.Mirror<ConsoleColor>(colors, ScreenWidth);
        if (darkness > 0f)
            ViewEffects.Wash(colors, ScenePalette, darkness, _washLookup);
        if (Scene is IUiLayer ui)
        {
            _paletteTarget.Frame = colors;
            ui.DrawUi(_paletteTarget);
        }

        for (var y = 0; y < ScreenHeight; y++)
        for (var x = 0; x < ScreenWidth; x++)
            Console.Draw(x, y, ' ', ConsoleColor.Black, colors[y * ScreenWidth + x]);
    }

    public void SelectKart(int index) => LoadKart(index);

    /// <summary>
    /// Loads track <paramref name="trackIndex"/> and its <c>race.txt</c> (unless loaded), then races: against the
    /// bots, or in a <see cref="RaceMode.TimeTrial"/> alone with the ghost of the best lap of this track and kart.
    /// </summary>
    public void StartRace(int trackIndex, int laps, RaceMode mode = RaceMode.Race)
    {
        if (trackIndex != _trackIndex)
        {
            var id = _trackNames[trackIndex];
            Track = Track.Load(tracksFolder, id);
            Markers = File.Exists(Path.Combine(tracksFolder, id + ".race.txt")) ? RaceMarkers.Load(tracksFolder, id) : null;
            _trackIndex = trackIndex;
        }

        Scenes.Set(mode == RaceMode.TimeTrial
            ? new TrackScene(laps, botCount: 0, new TimeTrial(GhostFile.PathFor(Track.Name, KartName)))
            : new TrackScene(laps));
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
