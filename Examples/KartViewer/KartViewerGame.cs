using System;
using System.IO;
using System.Numerics;
using ConsoleEngine;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure;
using ConsoleEngine.Infrastructure.Inputs;
using KartViewer.Rendering;

namespace KartViewer;

/// <summary>
/// 3D experiment: Kartster karts and a Kartster track, drawn by the software <see cref="Rasterizer"/> with one
/// console cell per pixel. Two modes, swapped with Enter: on the track the kart drives along the centerline with a
/// chase camera behind it; in the garage the camera orbits the kart on a small floor. Kartster's axes: 1 unit is
/// 1 metre, +Y up, -Z forward.
/// </summary>
public class KartViewerGame(string kartsFolder, string tracksFolder)
    : GameBase(ScreenWidth, ScreenHeight, fontWidth: 4, fontHeight: 4)
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int ScreenWidth = 240;
    private const int ScreenHeight = 135;
    private const float FieldOfView = 50f * MathF.PI / 180f;
    private const float NearPlane = 0.2f;
    private const float FarPlane = 400f;

    // garage
    private const float OrbitSpeed = 1.5f;  // radians per second
    private const float ZoomSpeed = 3f;     // metres per second
    private const float AutoSpinSpeed = 0.5f;

    // track
    private const float StartSpeed = 15f;   // metres per second
    private const float MaxSpeed = 40f;
    private const float Acceleration = 12f; // metres per second²
    private const float SideSpeed = 4f;     // metres per second across the road
    private const float MaxSideOffset = 3f;
    private const float ChaseDistance = 5.5f;
    private const float ChaseHeight = 2.2f;
    private const float LookAhead = 4f;
    private const float CameraStiffness = 6f; // how fast the chase camera catches up, per second

    private static readonly Vector3 PlayerColor = new(0.1f, 0.35f, 0.85f);
    private static readonly Vector3 ToSun = Vector3.Normalize(new Vector3(0.5f, 0.8f, 0.3f));
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

    private readonly Rasterizer _rasterizer = new(ScreenWidth, ScreenHeight, ScenePalette);
    private readonly ConsoleColor[] _sky = new ConsoleColor[ScreenWidth * ScreenHeight];
    private readonly Mesh _floor = Mesh.CreateFloor(8, 16, new Vector3(0.2f, 0.45f, 0.15f), new Vector3(0.3f, 0.55f, 0.2f));
    private readonly Matrix4x4 _projection = Matrix4x4.CreatePerspectiveFieldOfView(
        FieldOfView, (float)ScreenWidth / ScreenHeight, NearPlane, FarPlane);
    private string[] _kartFolders;
    private int _kartIndex;
    private Mesh _kart;
    private string _kartName;
    private Vector3 _kartCenter;
    private Track _track;
    private bool _onTrack;

    // garage
    private float _yaw = 0.8f;
    private float _pitch = 0.35f;
    private float _distance = 5f;
    private bool _autoSpin = true;

    // track
    private float _routeDistance;
    private float _speed = StartSpeed;
    private float _sideOffset;
    private Vector3 _cameraEye;

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
        {
            _track = Track.Load(tracksFolder, Path.GetFileName(ribbons[0])[..^".ribbon.obj".Length]);
            _onTrack = true;
            _cameraEye = ChaseEye();
        }

        for (var y = 0; y < ScreenHeight; y++)
        for (var x = 0; x < ScreenWidth; x++)
        {
            var sky = Vector3.Lerp(SkyTop, SkyHorizon, (float)y / ScreenHeight);
            _sky[y * ScreenWidth + x] = ScenePalette.ToConsoleColor(sky, x, y);
        }

        GameTime.SetInterval(250, () => Console.SetTitle(_onTrack
            ? $"KartViewer - {_kartName} on {_track.Name} - {_speed * 3.6f:0} km/h - {GameTime.Fps} fps"
            : $"KartViewer - {_kartName} ({_kart.TriangleCount} triangles) - {GameTime.Fps} fps"));
    }

    protected override void OnUpdate()
    {
        var input = Input.Instance;
        var seconds = (float)GameTime.Delta.TotalSeconds;

        if (input.GetKey(Key.ESCAPE).Pressed)
            Stop();
        if (input.GetKey(Key.TAB).Pressed)
            LoadKart((_kartIndex + 1) % _kartFolders.Length);
        if (input.GetKey(Key.ENTER).Pressed && _track != null)
        {
            _onTrack = !_onTrack;
            _cameraEye = ChaseEye();
        }

        if (_onTrack)
            UpdateDriving(input, seconds);
        else
            UpdateGarage(input, seconds);
    }

    protected override void OnRender()
    {
        _rasterizer.Clear(_sky);

        if (_onTrack)
        {
            var (position, forward) = KartPose();
            var view = Matrix4x4.CreateLookAt(_cameraEye, position + forward * LookAhead + Vector3.UnitY, Vector3.UnitY);
            var viewProjection = view * _projection;
            _rasterizer.Draw(_track.Ground, _track.GroundWorld, viewProjection, ToSun);
            _rasterizer.Draw(_track.Road, Matrix4x4.Identity, viewProjection, ToSun);
            _rasterizer.Draw(_kart, Matrix4x4.CreateWorld(position, forward, Vector3.UnitY), viewProjection, ToSun);
        }
        else
        {
            var eye = _kartCenter + _distance * new Vector3(
                MathF.Sin(_yaw) * MathF.Cos(_pitch), MathF.Sin(_pitch), MathF.Cos(_yaw) * MathF.Cos(_pitch));
            var viewProjection = Matrix4x4.CreateLookAt(eye, _kartCenter, Vector3.UnitY) * _projection;
            _rasterizer.Draw(_floor, Matrix4x4.Identity, viewProjection, ToSun);
            _rasterizer.Draw(_kart, Matrix4x4.Identity, viewProjection, ToSun);
        }

        var colors = _rasterizer.Colors;
        for (var y = 0; y < ScreenHeight; y++)
        for (var x = 0; x < ScreenWidth; x++)
            Console.Draw(x, y, ' ', ConsoleColor.Black, colors[y * ScreenWidth + x]);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void UpdateGarage(Input input, float seconds)
    {
        if (input.GetKey(Key.SPACE).Pressed) _autoSpin = !_autoSpin;
        if (input.GetKey(Key.A).Held) _yaw -= OrbitSpeed * seconds;
        if (input.GetKey(Key.D).Held) _yaw += OrbitSpeed * seconds;
        if (input.GetKey(Key.W).Held) _pitch += OrbitSpeed * seconds;
        if (input.GetKey(Key.S).Held) _pitch -= OrbitSpeed * seconds;
        if (input.GetKey(Key.Q).Held) _distance -= ZoomSpeed * seconds;
        if (input.GetKey(Key.E).Held) _distance += ZoomSpeed * seconds;
        if (_autoSpin) _yaw += AutoSpinSpeed * seconds;

        _pitch = Math.Clamp(_pitch, 0.05f, 1.5f);
        _distance = Math.Clamp(_distance, 1.5f, 12f);
    }

    private void UpdateDriving(Input input, float seconds)
    {
        if (input.GetKey(Key.W).Held) _speed += Acceleration * seconds;
        if (input.GetKey(Key.S).Held) _speed -= Acceleration * seconds;
        if (input.GetKey(Key.A).Held) _sideOffset -= SideSpeed * seconds;
        if (input.GetKey(Key.D).Held) _sideOffset += SideSpeed * seconds;
        _speed = Math.Clamp(_speed, 0, MaxSpeed);
        _sideOffset = Math.Clamp(_sideOffset, -MaxSideOffset, MaxSideOffset);

        _routeDistance += _speed * seconds;
        if (!_track.IsClosed && _routeDistance >= _track.Length)
            _routeDistance = 0; // an open track starts over

        // The camera eases towards its place behind the kart, so turns swing it round instead of snapping.
        _cameraEye = Vector3.Lerp(_cameraEye, ChaseEye(), 1 - MathF.Exp(-CameraStiffness * seconds));
    }

    private (Vector3 Position, Vector3 Forward) KartPose()
    {
        var (position, forward) = _track.Sample(_routeDistance);
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        return (position + right * _sideOffset, forward);
    }

    private Vector3 ChaseEye()
    {
        if (_track == null)
            return Vector3.Zero;

        var (position, forward) = KartPose();
        return position - forward * ChaseDistance + Vector3.UnitY * ChaseHeight;
    }

    private void LoadKart(int index)
    {
        _kartIndex = index;
        _kartName = Path.GetFileName(_kartFolders[index]);
        _kart = KartModel.Load(_kartFolders[index], PlayerColor);

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var position in _kart.Positions)
        {
            min = Vector3.Min(min, position);
            max = Vector3.Max(max, position);
        }

        _kartCenter = (min + max) / 2;
    }
}
