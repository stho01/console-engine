using System;
using System.Numerics;
using ConsoleEngine.Rendering3D;

namespace Kartster.PowerUps;

/// <summary>
/// Draws the <see cref="PowerUpField"/>: pads as glowing 1.2 m boxes turning at 0.6 rad/s and bobbing 0.15 m over
/// 2.6 s, each at its own phase; a taken pad smaller (0.45 growing to 0.7 as it recharges) and in a dimmer, colder
/// blue (Kartster's <c>PowerUpPadRenderer</c>). Bananas and cans lying on the road as small boxes in their colours,
/// darker while arming; cans in flight 0.6 m over the road, lid first; a thrown banana on its arc. Built-in box
/// meshes, no asset. Called by the <c>KartRenderSystem</c> with its camera; allocates nothing per frame.
/// </summary>
public sealed class PowerUpView(PowerUpField field)
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const float PadSize = 1.2f;
    public const float PadSpin = 0.6f;
    public const float PadBob = 0.15f;
    public const float PadBobSeconds = 2.6f;
    public const float DormantScale = 0.45f;
    public const float ChargedScale = 0.7f;
    public const float CanHover = 0.6f;

    // Content/Textures/PowerUpPad.png's blue; a recharging pad is tinted by Kartster's DormantTint.
    private static readonly Vector3 PadColor = new(0.2f, 0.45f, 0.72f);
    private static readonly Vector3 DormantTint = new(0.35f, 0.45f, 0.62f);
    private static readonly Vector3 BananaColor = new(1f, 0.82f, 0.25f);
    private static readonly Vector3 LapskausColor = new(0.69f, 0.42f, 0.19f);
    private static readonly Vector3 JoikaColor = new(0.8f, 0.16f, 0.12f);
    private const float ArmingShade = 0.5f;

    private static readonly Vector3 BananaHalf = new(0.3f, 0.1f, 0.18f);
    private static readonly Vector3 CanHalf = new(0.45f, 0.45f, 0.5f); // 0.9 m across, 1 m long

    private readonly Mesh _pad = Box(new Vector3(0.5f), PadColor, unlit: true);
    private readonly Mesh _dormantPad = Box(new Vector3(0.5f), PadColor * DormantTint, unlit: true);
    private readonly Mesh _banana = Box(BananaHalf, BananaColor);
    private readonly Mesh _armingBanana = Box(BananaHalf, BananaColor * ArmingShade);
    private readonly Mesh _lapskaus = Box(CanHalf, LapskausColor);
    private readonly Mesh _armingLapskaus = Box(CanHalf, LapskausColor * ArmingShade);
    private readonly Mesh _joika = Box(CanHalf, JoikaColor);
    private readonly Mesh _armingJoika = Box(CanHalf, JoikaColor * ArmingShade);
    private float _seconds;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <param name="deltaSeconds">The frame's time, for the pads' turn and bob.</param>
    public void Draw(IRenderer3D renderer, Matrix4x4 viewProjection, Vector3 toLight, float deltaSeconds)
    {
        _seconds += deltaSeconds;
        var pads = field.Pads;
        for (var i = 0; i < pads.Length; i++)
        {
            var pad = pads[i];
            var charge = Math.Clamp(1f - pad.RespawnSeconds / PowerUpCatalog.RespawnSeconds, 0f, 1f);
            var scale = PadSize * (pad.IsAvailable ? 1f : float.Lerp(DormantScale, ChargedScale, charge));
            var phase = i * 0.618034f % 1f; // its own place in the cycle, so a row doesn't turn in lockstep
            var bob = MathF.Sin((_seconds / PadBobSeconds + phase) * MathF.Tau) * PadBob;
            var world = Matrix4x4.CreateScale(scale)
                        * Matrix4x4.CreateRotationY(_seconds * PadSpin + phase * MathF.Tau)
                        * Matrix4x4.CreateTranslation(pad.Position + Vector3.UnitY * bob);
            renderer.Draw(pad.IsAvailable ? _pad : _dormantPad, world, viewProjection, toLight);
        }

        foreach (ref readonly var hazard in field.Hazards)
        {
            var isBanana = hazard.Source == PowerUpKind.Banana;
            var half = isBanana ? BananaHalf : CanHalf;
            var mesh = isBanana
                ? hazard.IsArmed ? _banana : _armingBanana
                : CanMesh(hazard.Source, hazard.IsArmed);
            var y = hazard.Height + half.Y + (hazard.InAir ? hazard.AirHeight : 0f);
            var heading = MathF.Atan2(hazard.Velocity.X, hazard.Velocity.Y);
            renderer.Draw(mesh, Matrix4x4.CreateRotationY(heading)
                                * Matrix4x4.CreateTranslation(hazard.Position.X, y, hazard.Position.Y),
                viewProjection, toLight);
        }

        foreach (ref readonly var can in field.Projectiles)
        {
            var forward = Vector3.Normalize(new Vector3(can.Velocity.X, 0f, can.Velocity.Y));
            var position = new Vector3(can.Position.X, can.Height + CanHover, can.Position.Y);
            renderer.Draw(CanMesh(can.Source, true), Matrix4x4.CreateWorld(position, forward, Vector3.UnitY),
                viewProjection, toLight);
        }
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private Mesh CanMesh(PowerUpKind source, bool armed) => PowerUpCatalog.IsHoming(source)
        ? armed ? _joika : _armingJoika
        : armed ? _lapskaus : _armingLapskaus;

    /// <summary>A box of half size <paramref name="half"/> round the origin, faces counter-clockwise from outside.</summary>
    public static Mesh Box(Vector3 half, Vector3 color, bool unlit = false)
    {
        var positions = new Vector3[8];
        for (var i = 0; i < 8; i++)
            positions[i] = new Vector3((i & 1) == 0 ? -half.X : half.X, (i & 2) == 0 ? -half.Y : half.Y,
                (i & 4) == 0 ? -half.Z : half.Z);
        int[] indices =
        [
            0, 2, 3, 0, 3, 1, // -Z
            4, 5, 7, 4, 7, 6, // +Z
            0, 4, 6, 0, 6, 2, // -X
            1, 3, 7, 1, 7, 5, // +X
            0, 1, 5, 0, 5, 4, // -Y
            2, 6, 7, 2, 7, 3, // +Y
        ];
        var colors = new Vector3[indices.Length / 3];
        Array.Fill(colors, color);
        return new Mesh(positions, indices, colors) { IsUnlit = unlit };
    }
}
