using System;
using System.Numerics;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using KartViewer.Components;
using KartViewer.Effects;

namespace KartViewer.Systems;

/// <summary>
/// Draws the ground, the road, the skid marks, the kart and its slide smoke through the game's renderer, seen by a
/// chase camera that eases towards its place behind the kart, so turns swing it round instead of snapping. Draws the kart where its interpolation puts it
/// (<c>RenderOffset</c>), so add it after the <c>InterpolationSystem</c>.
/// </summary>
public sealed class KartRenderSystem(Entity kart, KartViewerGame game, SkidMarks skidMarks, SlideSmoke smoke)
    : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const float ChaseDistance = 5.5f;
    private const float ChaseHeight = 2.2f;
    private const float LookAhead = 4f;
    private const float CameraStiffness = 6f; // how fast the chase camera catches up, per second

    private Vector3 _eye;
    private bool _hasEye;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnUpdate(float deltaSeconds)
    {
        var offset = World.Get<RenderOffset>(kart);
        var ground = World.Get<Position>(kart).Value + offset.Position;
        var radians = World.Get<Rotation>(kart).Radians + offset.Radians;
        var position = new Vector3(ground.X, World.Get<Kart>(kart).Height, ground.Y);
        var forward = new Vector3(MathF.Sin(radians), 0f, -MathF.Cos(radians)); // Rotation.Heading as (x, 0, z)

        var chaseEye = position - forward * ChaseDistance + Vector3.UnitY * ChaseHeight;
        _eye = _hasEye ? Vector3.Lerp(_eye, chaseEye, 1 - MathF.Exp(-CameraStiffness * deltaSeconds)) : chaseEye;
        _hasEye = true;

        var target = position + forward * LookAhead + Vector3.UnitY;
        var view = Matrix4x4.CreateLookAt(_eye, target, Vector3.UnitY);
        var viewProjection = view * game.Projection;
        var renderer = game.Renderer;
        var track = game.Track;
        renderer.Draw(track.Ground, track.GroundWorld, viewProjection, KartViewerGame.ToSun);
        renderer.Draw(track.Road, Matrix4x4.Identity, viewProjection, KartViewerGame.ToSun);
        renderer.Draw(skidMarks.Mesh, Matrix4x4.Identity, viewProjection, KartViewerGame.ToSun);
        renderer.Draw(game.Kart, Matrix4x4.CreateWorld(position, forward, Vector3.UnitY), viewProjection,
            KartViewerGame.ToSun);

        var look = Vector3.Normalize(target - _eye);
        var cameraRight = Vector3.Normalize(Vector3.Cross(look, Vector3.UnitY));
        smoke.BuildQuads(cameraRight, Vector3.Cross(cameraRight, look));
        renderer.Draw(smoke.Mesh, Matrix4x4.Identity, viewProjection, KartViewerGame.ToSun);
    }
}
