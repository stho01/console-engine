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
/// 3D experiment: a Kartster kart on a chequered floor, drawn by the software <see cref="Rasterizer"/> with one
/// console cell per pixel. The camera orbits the kart. Kartster's axes: 1 unit is 1 metre, +Y up, -Z forward.
/// </summary>
public class KartViewerGame(string kartsFolder) : GameBase(ScreenWidth, ScreenHeight, fontWidth: 4, fontHeight: 4)
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int ScreenWidth = 240;
    private const int ScreenHeight = 135;
    private const float FieldOfView = 50f * MathF.PI / 180f;
    private const float OrbitSpeed = 1.5f;  // radians per second
    private const float ZoomSpeed = 3f;     // metres per second
    private const float AutoSpinSpeed = 0.5f;

    private static readonly Vector3 PlayerColor = new(0.1f, 0.35f, 0.85f);
    private static readonly Vector3 ToSun = Vector3.Normalize(new Vector3(0.5f, 0.8f, 0.3f));
    private static readonly Vector3 SkyTop = new(0.35f, 0.55f, 0.95f);
    private static readonly Vector3 SkyHorizon = new(0.85f, 0.85f, 0.95f);

    private readonly Rasterizer _rasterizer = new(ScreenWidth, ScreenHeight);
    private readonly ConsoleColor[] _sky = new ConsoleColor[ScreenWidth * ScreenHeight];
    private readonly Mesh _floor = Mesh.CreateFloor(8, 16, new Vector3(0.2f, 0.45f, 0.15f), new Vector3(0.3f, 0.55f, 0.2f));
    private string[] _kartFolders;
    private int _kartIndex;
    private Mesh _kart;
    private string _kartName;
    private Vector3 _target;
    private float _yaw = 0.8f;
    private float _pitch = 0.35f;
    private float _distance = 5f;
    private bool _autoSpin = true;

    //**********************************************************
    //** game
    //**********************************************************

    protected override void OnInitialize()
    {
        Console.PixelMode = true;
        ClearScreenOnEachFrame = false; // every cell is drawn each frame

        _kartFolders = Directory.GetDirectories(kartsFolder);
        if (_kartFolders.Length == 0)
            throw new DirectoryNotFoundException($"No kart folders in {Path.GetFullPath(kartsFolder)}");
        Array.Sort(_kartFolders, StringComparer.Ordinal);
        LoadKart(0);

        for (var y = 0; y < ScreenHeight; y++)
        for (var x = 0; x < ScreenWidth; x++)
        {
            var sky = Vector3.Lerp(SkyTop, SkyHorizon, (float)y / ScreenHeight);
            _sky[y * ScreenWidth + x] = ConsolePalette.ToConsoleColor(sky, x, y);
        }

        GameTime.SetInterval(250, () => Console.SetTitle(
            $"KartViewer - {_kartName} ({_kart.TriangleCount} triangles) - {GameTime.Fps} fps"));
    }

    protected override void OnUpdate()
    {
        var input = Input.Instance;
        var seconds = (float)GameTime.Delta.TotalSeconds;

        if (input.GetKey(Key.ESCAPE).Pressed)
            Stop();
        if (input.GetKey(Key.TAB).Pressed)
            LoadKart((_kartIndex + 1) % _kartFolders.Length);
        if (input.GetKey(Key.SPACE).Pressed)
            _autoSpin = !_autoSpin;

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

    protected override void OnRender()
    {
        var eye = _target + _distance * new Vector3(
            MathF.Sin(_yaw) * MathF.Cos(_pitch), MathF.Sin(_pitch), MathF.Cos(_yaw) * MathF.Cos(_pitch));
        var view = Matrix4x4.CreateLookAt(eye, _target, Vector3.UnitY);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(
            FieldOfView, (float)ScreenWidth / ScreenHeight, 0.1f, 100f);
        var viewProjection = view * projection;

        _rasterizer.Clear(_sky);
        _rasterizer.Draw(_floor, Matrix4x4.Identity, viewProjection, ToSun);
        _rasterizer.Draw(_kart, Matrix4x4.Identity, viewProjection, ToSun);

        var colors = _rasterizer.Colors;
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
        _kartName = Path.GetFileName(_kartFolders[index]);
        _kart = KartModel.Load(_kartFolders[index], PlayerColor);

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var position in _kart.Positions)
        {
            min = Vector3.Min(min, position);
            max = Vector3.Max(max, position);
        }

        _target = (min + max) / 2;
    }
}
