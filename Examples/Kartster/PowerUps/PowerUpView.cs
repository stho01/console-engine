using System;
using System.Numerics;
using ConsoleEngine.Rendering3D;

namespace Kartster.PowerUps;

/// <summary>
/// Draws the <see cref="PowerUpField"/>: pads as glowing 1.2 m boxes turning at 0.6 rad/s and bobbing 0.15 m over
/// 2.6 s, each at its own phase; a taken pad smaller (0.45 growing to 0.7 as it recharges) and in a dimmer, colder
/// blue (Kartster's <c>PowerUpPadRenderer</c>); a fake pad the same at 70% brightness. Bananas and cans lying on the
/// road as small boxes in their colours, darker while arming; cans in flight 0.6 m over the road, lid first; what is
/// thrown on its arc. A smoke bomb as a small dark ball until it goes off, then its cloud as five dark puffs that grow
/// with it and shrink as it thins. Roadblocks (white under red; concrete grey), fake finishes (a band of 0.4 m
/// squares across the road), trains (engine and cart, blinking while they fade). An invulnerable kart gets a gold,
/// unlit plate under it, blinking (faster in its last 1.5 s). Built-in box meshes, no asset. Called by the
/// <c>KartRenderSystem</c> with its camera; allocates nothing per frame (a fake finish's band is built when first
/// drawn).
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
    public const float FakePadFloat = 1f;
    public const float FakePadShade = 0.7f;
    public const float BlinkSeconds = 0.12f;
    public const float FastBlinkSeconds = 0.06f;
    public const float BlinkWarningSeconds = 1.5f;
    public const float FinishSquare = 0.4f;

    // Content/Textures/PowerUpPad.png's blue; a recharging pad is tinted by Kartster's DormantTint.
    private static readonly Vector3 PadColor = new(0.2f, 0.45f, 0.72f);
    private static readonly Vector3 DormantTint = new(0.35f, 0.45f, 0.62f);
    private static readonly Vector3 BananaColor = new(1f, 0.82f, 0.25f);
    private static readonly Vector3 LapskausColor = new(0.69f, 0.42f, 0.19f);
    private static readonly Vector3 JoikaColor = new(0.8f, 0.16f, 0.12f);
    private static readonly Vector3 SmokeColor = new(0.3f, 0.3f, 0.32f);
    private static readonly Vector3 GlowColor = new(1f, 0.85f, 0.2f);
    private const float ArmingShade = 0.5f;

    private static readonly Vector3 BananaHalf = new(0.3f, 0.1f, 0.18f);
    private static readonly Vector3 CanHalf = new(0.45f, 0.45f, 0.5f); // 0.9 m across, 1 m long
    private static readonly Vector3 EngineHalf = new(PowerUpCatalog.TrainHalfWidth, 2.1f, PowerUpCatalog.EngineLength / 2f);
    private static readonly Vector3 CartHalf = new(PowerUpCatalog.TrainHalfWidth, 1.6f, PowerUpCatalog.CartLength / 2f);

    // Five puffs round a cloud's middle, as shares of its radius.
    private static readonly Vector2[] PuffOffsets = [new(0f, 0f), new(0.45f, 0.2f), new(-0.4f, 0.3f), new(0.2f, -0.45f),
        new(-0.3f, -0.35f)];

    private readonly Mesh _pad = Box(new Vector3(0.5f), PadColor, unlit: true);
    private readonly Mesh _dormantPad = Box(new Vector3(0.5f), PadColor * DormantTint, unlit: true);
    private readonly Mesh _fakePad = Box(new Vector3(0.5f), PadColor * FakePadShade, unlit: true);
    private readonly Mesh _banana = Box(BananaHalf, BananaColor);
    private readonly Mesh _armingBanana = Box(BananaHalf, BananaColor * ArmingShade);
    private readonly Mesh _lapskaus = Box(CanHalf, LapskausColor);
    private readonly Mesh _armingLapskaus = Box(CanHalf, LapskausColor * ArmingShade);
    private readonly Mesh _joika = Box(CanHalf, JoikaColor);
    private readonly Mesh _armingJoika = Box(CanHalf, JoikaColor * ArmingShade);
    private readonly Mesh _smokeBomb = Box(new Vector3(0.3f), new Vector3(0.12f));
    private readonly Mesh _puff = Box(new Vector3(0.5f), SmokeColor);
    private readonly Mesh _roadblock = Box(new Vector3(PowerUpCatalog.RoadblockHalf.X, 0.25f,
        PowerUpCatalog.RoadblockHalf.Y), new Vector3(0.92f));
    private readonly Mesh _roadblockTop = Box(new Vector3(PowerUpCatalog.RoadblockHalf.X, 0.25f,
        PowerUpCatalog.RoadblockHalf.Y), new Vector3(0.85f, 0.15f, 0.1f));
    private readonly Mesh _concrete = Box(new Vector3(PowerUpCatalog.ConcreteHalf.X, PowerUpCatalog.ConcreteHeight / 2f,
        PowerUpCatalog.ConcreteHalf.Y), new Vector3(0.62f, 0.61f, 0.58f));
    private readonly Mesh _engine = Box(EngineHalf, new Vector3(0.12f, 0.14f, 0.13f));
    private readonly Mesh _cart = Box(CartHalf, new Vector3(0.55f, 0.12f, 0.1f));
    private readonly Mesh[] _finishes = new Mesh[PowerUpField.DecoyCapacity];
    private readonly long[] _finishIds = new long[PowerUpField.DecoyCapacity];
    private readonly Mesh _glow = Box(new Vector3(0.5f), GlowColor, unlit: true);
    private float _seconds;

    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>The field drawn; the kart renderer reads its effects (shrink, flip, shake, smoke) from it.</summary>
    public PowerUpField Field => @field;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <param name="deltaSeconds">The frame's time, for the pads' turn and bob and the blinking.</param>
    public void Draw(IRenderer3D renderer, Matrix4x4 viewProjection, Vector3 toLight, float deltaSeconds)
    {
        _seconds += deltaSeconds;
        var pads = field.Pads;
        for (var i = 0; i < pads.Length; i++)
        {
            var pad = pads[i];
            var charge = Math.Clamp(1f - pad.RespawnSeconds / PowerUpCatalog.RespawnSeconds, 0f, 1f);
            var scale = pad.IsAvailable ? 1f : float.Lerp(DormantScale, ChargedScale, charge);
            renderer.Draw(pad.IsAvailable ? _pad : _dormantPad, PadWorld(pad.Position, scale, i), viewProjection,
                toLight);
        }

        DrawHazards(renderer, viewProjection, toLight);
        foreach (ref readonly var can in field.Projectiles)
        {
            var forward = Vector3.Normalize(new Vector3(can.Velocity.X, 0f, can.Velocity.Y));
            var position = new Vector3(can.Position.X, can.Height + CanHover, can.Position.Y);
            renderer.Draw(CanMesh(can.Source, true), Matrix4x4.CreateWorld(position, forward, Vector3.UnitY),
                viewProjection, toLight);
        }

        foreach (ref readonly var block in field.Obstacles)
        {
            if (block.IsActive)
                DrawObstacle(renderer, viewProjection, toLight, block.Kind, block.Position, block.Height, block.Heading);
        }

        DrawFinishes(renderer, viewProjection, toLight);
        DrawTrains(renderer, viewProjection, toLight);
        DrawGlows(renderer, viewProjection, toLight);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private Matrix4x4 PadWorld(Vector3 position, float scale, int index)
    {
        var phase = index * 0.618034f % 1f; // its own place in the cycle, so a row doesn't turn in lockstep
        var bob = MathF.Sin((_seconds / PadBobSeconds + phase) * MathF.Tau) * PadBob;
        return Matrix4x4.CreateScale(PadSize * scale)
               * Matrix4x4.CreateRotationY(_seconds * PadSpin + phase * MathF.Tau)
               * Matrix4x4.CreateTranslation(position + Vector3.UnitY * bob);
    }

    private void DrawHazards(IRenderer3D renderer, Matrix4x4 viewProjection, Vector3 toLight)
    {
        var hazards = field.Hazards;
        for (var i = 0; i < hazards.Length; i++)
        {
            ref readonly var hazard = ref hazards[i];
            var air = hazard.InAir ? hazard.AirHeight : 0f;
            switch (hazard.Source)
            {
                case PowerUpKind.FakePowerUp:
                    renderer.Draw(_fakePad, PadWorld(new Vector3(hazard.Position.X, hazard.Height + FakePadFloat + air,
                        hazard.Position.Y), 1f, (int)(hazard.Age % 97)), viewProjection, toLight);
                    continue;
                case PowerUpKind.Roadblock or PowerUpKind.ConcreteRoadblock:
                    DrawObstacle(renderer, viewProjection, toLight, hazard.Source, hazard.Position, hazard.Height + air,
                        hazard.Heading);
                    continue;
                case PowerUpKind.SmokeBomb when hazard.HasGoneOff:
                    DrawCloud(renderer, viewProjection, toLight, hazard);
                    continue;
                case PowerUpKind.SmokeBomb:
                    renderer.Draw(_smokeBomb, Matrix4x4.CreateTranslation(hazard.Position.X, hazard.Height + 0.3f + air,
                        hazard.Position.Y), viewProjection, toLight);
                    continue;
            }

            var isBanana = hazard.Source == PowerUpKind.Banana;
            var half = isBanana ? BananaHalf : CanHalf;
            var mesh = isBanana
                ? hazard.IsArmed ? _banana : _armingBanana
                : CanMesh(hazard.Source, hazard.IsArmed);
            var heading = MathF.Atan2(hazard.Velocity.X, hazard.Velocity.Y);
            renderer.Draw(mesh, Matrix4x4.CreateRotationY(heading)
                                * Matrix4x4.CreateTranslation(hazard.Position.X, hazard.Height + half.Y + air,
                                    hazard.Position.Y),
                viewProjection, toLight);
        }
    }

    private void DrawCloud(IRenderer3D renderer, Matrix4x4 viewProjection, Vector3 toLight, in PowerUpHazard hazard)
    {
        var radius = PowerUpField.SmokeCloudRadius(hazard) * MathF.Sqrt(PowerUpField.SmokeDensity(hazard));
        if (radius <= 0.1f)
            return;

        foreach (var offset in PuffOffsets)
        {
            var center = new Vector3(hazard.Position.X + offset.X * radius, hazard.Height + radius * 0.4f,
                hazard.Position.Y + offset.Y * radius);
            renderer.Draw(_puff, Matrix4x4.CreateScale(radius * 0.9f) * Matrix4x4.CreateTranslation(center),
                viewProjection, toLight);
        }
    }

    private void DrawObstacle(IRenderer3D renderer, Matrix4x4 viewProjection, Vector3 toLight, PowerUpKind kind,
        Vector2 position, float height, float heading)
    {
        var turn = Matrix4x4.CreateRotationY(heading);
        if (kind == PowerUpKind.ConcreteRoadblock)
        {
            renderer.Draw(_concrete, turn * Matrix4x4.CreateTranslation(position.X,
                height + PowerUpCatalog.ConcreteHeight / 2f, position.Y), viewProjection, toLight);
            return;
        }

        renderer.Draw(_roadblock, turn * Matrix4x4.CreateTranslation(position.X, height + 0.25f, position.Y),
            viewProjection, toLight);
        renderer.Draw(_roadblockTop, turn * Matrix4x4.CreateTranslation(position.X, height + 0.75f, position.Y),
            viewProjection, toLight);
    }

    private void DrawFinishes(IRenderer3D renderer, Matrix4x4 viewProjection, Vector3 toLight)
    {
        var decoys = field.Decoys;
        for (var i = 0; i < decoys.Length; i++)
        {
            ref readonly var decoy = ref decoys[i];
            if (_finishes[i] == null || _finishIds[i] != decoy.Id)
            {
                _finishes[i] = Band(decoy.LeftWidth, decoy.RightWidth);
                _finishIds[i] = decoy.Id;
            }

            var heading = MathF.Atan2(-decoy.Forward.X, -decoy.Forward.Y);
            renderer.Draw(_finishes[i], Matrix4x4.CreateRotationY(heading)
                                        * Matrix4x4.CreateTranslation(decoy.Position.X, decoy.Height + 0.02f,
                                            decoy.Position.Y), viewProjection, toLight);
        }
    }

    private void DrawTrains(IRenderer3D renderer, Matrix4x4 viewProjection, Vector3 toLight)
    {
        var road = field.Road;
        foreach (ref readonly var train in field.Trains)
        {
            if (train.IsFading && IsBlinkedOff(BlinkSeconds))
                continue;

            var engine = train.Along - train.Direction * PowerUpCatalog.EngineLength / 2f;
            var cart = train.Along - train.Direction
                * (PowerUpCatalog.EngineLength + PowerUpCatalog.CouplingGap + PowerUpCatalog.CartLength / 2f);
            DrawCar(renderer, viewProjection, toLight, _engine, EngineHalf.Y, train, road, engine);
            DrawCar(renderer, viewProjection, toLight, _cart, CartHalf.Y, train, road, cart);
        }
    }

    private static void DrawCar(IRenderer3D renderer, Matrix4x4 viewProjection, Vector3 toLight, Mesh mesh,
        float halfHeight, in PowerUpTrain train, Bots.BotRoad road, float along)
    {
        PowerUpField.CarPose(train, road, along, out var center, out var forward);
        renderer.Draw(mesh, Matrix4x4.CreateWorld(new Vector3(center.X, train.Height + halfHeight, center.Y),
            new Vector3(forward.X, 0f, forward.Y), Vector3.UnitY), viewProjection, toLight);
    }

    private void DrawGlows(IRenderer3D renderer, Matrix4x4 viewProjection, Vector3 toLight)
    {
        var karts = field.Karts;
        for (var k = 0; k < karts.Length; k++)
        {
            var left = field.Effects(k).InvulnerableSeconds;
            if (left <= 0f || IsBlinkedOff(left < BlinkWarningSeconds ? FastBlinkSeconds : BlinkSeconds))
                continue;

            ref readonly var kart = ref karts[k];
            var size = new Vector3(kart.HalfWidth * 2f + 0.8f, 0.08f, kart.HalfLength * 2f + 0.8f);
            renderer.Draw(_glow, Matrix4x4.CreateScale(size) * Matrix4x4.CreateRotationY(kart.Heading)
                                 * Matrix4x4.CreateTranslation(kart.Position.X, kart.Height + 0.05f, kart.Position.Y),
                viewProjection, toLight);
        }
    }

    private bool IsBlinkedOff(float halfPeriod) => (int)(_seconds / halfPeriod) % 2 == 1;

    private Mesh CanMesh(PowerUpKind source, bool armed) => PowerUpCatalog.IsHoming(source)
        ? armed ? _joika : _armingJoika
        : armed ? _lapskaus : _armingLapskaus;

    /// <summary>
    /// A flat band of black and white 0.4 m squares, two deep, from <paramref name="left"/> left of the origin to
    /// <paramref name="right"/> right of it (local X), facing up.
    /// </summary>
    public static Mesh Band(float left, float right)
    {
        var columns = Math.Max(1, (int)MathF.Ceiling((left + right) / FinishSquare));
        const int rows = 2;
        var positions = new Vector3[columns * rows * 4];
        var indices = new int[columns * rows * 6];
        var colors = new Vector3[columns * rows * 2];
        var square = 0;
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++, square++)
            {
                var x0 = -left + column * FinishSquare;
                var x1 = MathF.Min(x0 + FinishSquare, right);
                var z0 = (row - 1) * FinishSquare;
                var z1 = z0 + FinishSquare;
                var v = square * 4;
                positions[v] = new Vector3(x0, 0f, z0);
                positions[v + 1] = new Vector3(x1, 0f, z0);
                positions[v + 2] = new Vector3(x1, 0f, z1);
                positions[v + 3] = new Vector3(x0, 0f, z1);
                // Counter-clockwise seen from above (+Y).
                indices[square * 6] = v;
                indices[square * 6 + 1] = v + 3;
                indices[square * 6 + 2] = v + 2;
                indices[square * 6 + 3] = v;
                indices[square * 6 + 4] = v + 2;
                indices[square * 6 + 5] = v + 1;
                var color = (row + column) % 2 == 0 ? new Vector3(0.95f) : new Vector3(0.08f);
                colors[square * 2] = color;
                colors[square * 2 + 1] = color;
            }
        }

        return new Mesh(positions, indices, colors);
    }

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
