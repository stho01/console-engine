using System;
using System.Numerics;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Infrastructure.Scenery;
using ConsoleEngine.Rendering3D;
using Kartster.Scenes.Menus;

namespace Kartster.Scenes;

/// <summary>
/// The camera orbits the chosen kart on a small floor: A/D round, W/S up and down, Q/E zoom, Space turns the slow
/// auto-spin on and off, Tab shows the next kart (and chooses it), Esc goes back to the title.
/// </summary>
public sealed class GarageScene : Scene<KartsterGame>
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const float OrbitSpeed = 1.5f;  // radians per second
    private const float ZoomSpeed = 3f;     // metres per second
    private const float AutoSpinSpeed = 0.5f;

    private readonly Mesh _floor = Mesh.CreateFloor(8, 16, new Vector3(0.2f, 0.45f, 0.15f), new Vector3(0.3f, 0.55f, 0.2f));
    private float _yaw = 0.8f;
    private float _pitch = 0.35f;
    private float _distance = 5f;
    private bool _autoSpin = true;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    public override void OnLoad() { }

    public override void OnUnload() { }

    public override void OnUpdate()
    {
        var input = Input.Instance;
        var seconds = (float)GameTime.Delta.TotalSeconds;

        if (input.GetKey(Key.ESCAPE).Pressed)
        {
            Game.Scenes.Set(new TitleScene());
            return;
        }

        if (input.GetKey(Key.TAB).Pressed) Game.SelectKart((Game.KartIndex + 1) % Game.KartNames.Length);
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

    public override void OnRender()
    {
        var center = Game.KartCenter;
        var eye = center + _distance * new Vector3(
            MathF.Sin(_yaw) * MathF.Cos(_pitch), MathF.Sin(_pitch), MathF.Cos(_yaw) * MathF.Cos(_pitch));
        var viewProjection = Matrix4x4.CreateLookAt(eye, center, Vector3.UnitY) * Game.Projection;
        Game.Renderer.Draw(_floor, Matrix4x4.Identity, viewProjection, KartsterGame.ToSun);
        Game.Renderer.Draw(Game.Kart, Matrix4x4.Identity, viewProjection, KartsterGame.ToSun);
    }
}
