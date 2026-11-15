using System;
using System.Numerics;
using ConsoleEngine.Ecs;
using ConsoleEngine.Rendering3D;
using Kartster.Systems;

namespace Kartster.Modes;

/// <summary>
/// Draws the <see cref="TimeTrial"/>'s ghost through the game's renderer with the chase camera of
/// <paramref name="camera"/>, after it (see-through triangles go after the opaque ones). The ghost is no entity: it
/// has no body, so nothing collides with it.
/// </summary>
/// <param name="ghost">
/// The kart's mesh at <see cref="TimeTrial.GhostOpacity"/> (<see cref="TimeTrial.CreateGhostMesh"/>).
/// </param>
/// <param name="timestep">The race's fixed step, for the same interpolation as the karts.</param>
[UpdateAfter(typeof(KartRenderSystem))]
public sealed class GhostRenderSystem(TimeTrial trial, KartRenderSystem camera, KartsterGame game, Mesh ghost,
    FixedTimestep timestep) : EntitySystem
{
    protected override void OnUpdate(float deltaSeconds)
    {
        if (!trial.TryGetGhost(timestep.Alpha, out var pose))
            return;

        // Rotation.Heading as (x, 0, z), as KartRenderSystem draws a kart.
        var forward = new Vector3(MathF.Sin(pose.Radians), 0f, -MathF.Cos(pose.Radians));
        var world = Matrix4x4.CreateWorld(new Vector3(pose.X, pose.Height, pose.Z), forward, Vector3.UnitY);
        game.Renderer.Draw(ghost, world, camera.ViewProjection, KartsterGame.ToSun);
    }
}
