using System;
using System.Numerics;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Rendering3D;
using Kartster.Components;
using Kartster.Effects;
using Kartster.Lighting;
using Kartster.PowerUps;

namespace Kartster.Systems;

/// <summary>
/// Draws the ground, the road, the scenery (culled, see <see cref="Scenery.Draw"/>), the skid marks, every kart and
/// the slide smoke through the game's renderer, seen by a chase camera that eases towards its place behind the
/// <paramref name="player"/>'s kart, so turns swing it round instead of snapping. Draws the karts where their
/// interpolation puts them (<c>RenderOffset</c>), so add it after the <c>InterpolationSystem</c>. Shadows, night and the
/// karts' lights come from <see cref="KartLights"/>. With <see cref="PowerUps"/>, the power-ups' effects on the
/// picture: a kart is drawn at its <see cref="KartEffects.DrawScale"/>, the camera shakes while the player's
/// <see cref="KartEffects.SteeringNoise"/> runs, and it sets the game's <see cref="KartsterGame.MirrorView"/> (glitch)
/// and <see cref="KartsterGame.ViewDarkness"/> (<see cref="PowerUpField.SmokeCover"/> at the eye).
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

    /// <summary>
    /// Metres the camera shakes per unit of <see cref="KartEffects.SteeringNoise"/>: Kartster's earthquake pairs
    /// 0.3 steering noise with 0.35 m of <c>CameraShake</c>.
    /// </summary>
    public const float ShakePerSteeringNoise = 0.35f / PowerUpCatalog.EarthquakeSteeringNoise;

    private Query _karts;
    private readonly Matrix4x4[] _kartWorlds = new Matrix4x4[kartMeshes.Length]; // by Kart.Index, this frame
    private readonly KartLights _lights = new(kartMeshes, game.KartFolders, game.Track.Name, game.Renderer.Width,
        game.Renderer.Height);
    private Vector3 _eye;
    private bool _hasEye;
    private float _seconds; // drawn time, for the shake

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>The race's pads, hazards and cans, drawn after the karts; null: none.</summary>
    public PowerUpView PowerUps { get; init; }

    /// <summary>The chase camera's view × projection of this frame, for whatever is drawn after the karts.</summary>
    public Matrix4x4 ViewProjection { get; private set; }

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
        _seconds += deltaSeconds;
        var field = PowerUps?.Field;
        if (field != null)
        {
            var effects = field.Effects(World.Get<Kart>(player).Index);
            var shake = effects.SteeringNoise * ShakePerSteeringNoise;
            if (shake > 0f) // Kartster's ApplyCameraShake: the view jitters, the camera itself stays
                view *= Matrix4x4.CreateTranslation(shake * Shake(_seconds, 0f), shake * Shake(_seconds, 1.7f), 0f);
            game.MirrorView = effects.SteeringScale < 0f;
            game.ViewDarkness = field.SmokeCover(new System.Numerics.Vector2(_eye.X, _eye.Z));
        }

        var viewProjection = ViewProjection = view * game.Projection;
        var renderer = game.Renderer;
        var track = game.Track;
        PlaceKarts();
        _lights.Begin(renderer, _kartWorlds, _eye);
        track.DrawGround(renderer, viewProjection, _eye, KartsterGame.ToSun);
        renderer.Draw(track.Road, Matrix4x4.Identity, viewProjection, KartsterGame.ToSun);
        track.Scenery.Draw(renderer, viewProjection, _eye, KartsterGame.ToSun);
        renderer.Draw(skidMarks.Mesh, Matrix4x4.Identity, viewProjection, KartsterGame.ToSun);
        _lights.DrawShadows(renderer, viewProjection, _kartWorlds);
        for (var i = 0; i < kartMeshes.Length; i++)
            renderer.Draw(kartMeshes[i], _kartWorlds[i], viewProjection, KartsterGame.ToSun);
        _lights.DrawLenses(renderer, viewProjection, _kartWorlds);
        PowerUps?.Draw(renderer, viewProjection, KartsterGame.ToSun, deltaSeconds);

        var look = Vector3.Normalize(target - _eye);
        var cameraRight = Vector3.Normalize(Vector3.Cross(look, Vector3.UnitY));
        smoke.BuildQuads(cameraRight, Vector3.Cross(cameraRight, look));
        renderer.Draw(smoke.Mesh, Matrix4x4.Identity, viewProjection, KartsterGame.ToSun);
    }

    /// <summary>
    /// The shake at <paramref name="seconds"/> of drawn time, -1..1: two sines at 7.3 and 11.9 Hz, so it is the same
    /// at any frame rate and never repeats soon. <paramref name="phase"/> makes the other axis' shake.
    /// </summary>
    public static float Shake(float seconds, float phase) =>
        0.6f * MathF.Sin(MathF.Tau * 7.3f * seconds + phase) + 0.4f * MathF.Sin(MathF.Tau * 11.9f * seconds + 2f * phase);

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>Where every kart is drawn this frame, into <see cref="_kartWorlds"/>.</summary>
    private void PlaceKarts()
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
                var world = KartFrame.ToWorld(position, forward, karts[i].Normal);
                var scale = PowerUps?.Field.Effects(karts[i].Index).DrawScale ?? 1f; // lightning shrinks
                _kartWorlds[karts[i].Index] = scale == 1f ? world : Matrix4x4.CreateScale(scale) * world;
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
