using System;
using System.Numerics;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Rendering3D;
using Kartster.Components;
using Kartster.Effects;
using Kartster.PowerUps;

namespace Kartster.Systems;

/// <summary>
/// Draws the ground, the road, the scenery (culled, see <see cref="Scenery.Draw"/>), the skid marks, every kart and
/// the slide smoke through the game's renderer, seen by a chase camera that eases towards its place behind the
/// <paramref name="player"/>'s kart, so turns swing it round instead of snapping. Draws the karts where their
/// interpolation puts them (<c>RenderOffset</c>), so add it after the <c>InterpolationSystem</c>.
/// </summary>
/// <param name="kartMeshes">One per kart, by <see cref="Kart.Index"/>.</param>
public sealed class KartRenderSystem(Entity player, KartsterGame game, Mesh[] kartMeshes, SkidMarks skidMarks,
    SlideSmoke smoke) : EntitySystem
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const float ChaseDistance = 5.5f;
    private const float ChaseHeight = 2.2f;
    private const float LookAhead = 4f;
    private const float CameraStiffness = 6f; // how fast the chase camera catches up, per second

    private Query _karts;
    private Vector3 _eye;
    private bool _hasEye;

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>The race's pads, hazards and cans, drawn after the karts; null: none.</summary>
    public PowerUpView PowerUps { get; init; }

    //**********************************************************
    //** lifecycle
    //**********************************************************

    protected override void OnCreate() => _karts = World.Query(Query.With<Kart, Position, Rotation>().With<RenderOffset>());

    protected override void OnUpdate(float deltaSeconds)
    {
        var (position, forward) = Pose(World.Get<Position>(player), World.Get<Rotation>(player),
            World.Get<RenderOffset>(player), World.Get<Kart>(player));

        var chaseEye = position - forward * ChaseDistance + Vector3.UnitY * ChaseHeight;
        _eye = _hasEye ? Vector3.Lerp(_eye, chaseEye, 1 - MathF.Exp(-CameraStiffness * deltaSeconds)) : chaseEye;
        _hasEye = true;

        var target = position + forward * LookAhead + Vector3.UnitY;
        var view = Matrix4x4.CreateLookAt(_eye, target, Vector3.UnitY);
        var viewProjection = view * game.Projection;
        var renderer = game.Renderer;
        var track = game.Track;
        renderer.Draw(track.Ground, track.GroundWorld, viewProjection, KartsterGame.ToSun);
        renderer.Draw(track.Road, Matrix4x4.Identity, viewProjection, KartsterGame.ToSun);
        track.Scenery.Draw(renderer, viewProjection, _eye, KartsterGame.ToSun);
        renderer.Draw(skidMarks.Mesh, Matrix4x4.Identity, viewProjection, KartsterGame.ToSun);
        DrawKarts(renderer, viewProjection);
        PowerUps?.Draw(renderer, viewProjection, KartsterGame.ToSun, deltaSeconds);

        var look = Vector3.Normalize(target - _eye);
        var cameraRight = Vector3.Normalize(Vector3.Cross(look, Vector3.UnitY));
        smoke.BuildQuads(cameraRight, Vector3.Cross(cameraRight, look));
        renderer.Draw(smoke.Mesh, Matrix4x4.Identity, viewProjection, KartsterGame.ToSun);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void DrawKarts(IRenderer3D renderer, Matrix4x4 viewProjection)
    {
        foreach (var chunk in _karts.GetChunks())
        {
            var karts = chunk.GetSpan<Kart>();
            var positions = chunk.GetSpan<Position>();
            var rotations = chunk.GetSpan<Rotation>();
            var offsets = chunk.GetSpan<RenderOffset>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var (position, forward) = Pose(positions[i], rotations[i], offsets[i], karts[i]);
                renderer.Draw(kartMeshes[karts[i].Index], Matrix4x4.CreateWorld(position, forward, Vector3.UnitY),
                    viewProjection, KartsterGame.ToSun);
            }
        }
    }

    /// <summary>Where the kart is drawn: its interpolated ground point and forward (world axes).</summary>
    private static (Vector3 Position, Vector3 Forward) Pose(Position position, Rotation rotation, RenderOffset offset,
        Kart kart)
    {
        var ground = position.Value + offset.Position;
        var radians = rotation.Radians + offset.Radians;
        // Rotation.Heading as (x, 0, z)
        return (new Vector3(ground.X, kart.Height, ground.Y), new Vector3(MathF.Sin(radians), 0f, -MathF.Cos(radians)));
    }
}
