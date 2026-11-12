using System;
using ConsoleEngine.Ecs;
using ConsoleEngine.Ecs.Components;
using ConsoleEngine.Physics;
using Microsoft.Xna.Framework;
using Topple.Components;
using Topple.Levels;
using Topple.Rendering;

namespace Topple.Systems;

/// <summary>Moves the world layers by this frame's screen shake. Runs first in presentation.</summary>
public sealed class ShakeSystem(Canvas canvas, ScreenShake shake) : EntitySystem
{
    protected override void OnUpdate(float deltaSeconds) => canvas.Shake = shake.Update(deltaSeconds);
}

/// <summary>The painted backdrop, then the twinkling stars and the clouds drifting over it.</summary>
public sealed class SkySystem(Canvas canvas, Backdrop backdrop) : EntitySystem
{
    private static readonly string[][] CloudShapes =
    [
        ["    ▄▄██▄    ", " ▄██████████▄ ", "▀▀▀▀▀▀▀▀▀▀▀▀▀▀▀"],
        ["  ▄██▄▄  ", "▀▀▀▀▀▀▀▀▀▀"],
        ["      ▄▄▄      ", "  ▄▄███████▄   ", "▄█████████████▄", " ▀▀▀▀▀▀▀▀▀▀▀▀▀ "]
    ];

    /// <summary>Start column, row, speed (cells/s) and shape of each cloud.</summary>
    private static readonly (float X, int Row, float Speed, int Shape)[] Clouds =
    [
        (10f, 5, 1.2f, 0),
        (70f, 9, .8f, 2),
        (40f, 15, 1.6f, 1),
        (100f, 19, 1f, 0)
    ];

    private float _seconds;

    protected override void OnUpdate(float deltaSeconds)
    {
        _seconds += deltaSeconds;
        backdrop.DrawTo(canvas);
        backdrop.DrawStars(canvas, _seconds);

        var span = canvas.Width + 30f;
        foreach (var cloud in Clouds)
        {
            var left = (int)((cloud.X + _seconds * cloud.Speed) % span) - 15;
            var shape = CloudShapes[cloud.Shape];
            for (var row = 0; row < shape.Length; row++)
            {
                var color = row == shape.Length - 1 ? ConsoleColor.DarkGray : ConsoleColor.Gray;
                var line = shape[row];
                for (var i = 0; i < line.Length; i++)
                {
                    if (line[i] != ' ')
                        canvas.DrawWorldOver(left + i, cloud.Row + row, line[i], color);
                }
            }
        }
    }
}

/// <summary>Water in the pits: a rolling surface of waves and slow ripples below.</summary>
public sealed class WaterRenderSystem(Canvas canvas, LevelPlan plan) : EntitySystem
{
    private float _seconds;

    protected override void OnUpdate(float deltaSeconds)
    {
        _seconds += deltaSeconds;
        var drift = (int)(_seconds * 1.5f);
        foreach (var pit in plan.Pits)
        {
            var water = LevelBuilder.WaterOf(pit);
            for (var x = water.Left; x <= water.Right; x++)
            {
                var wave = MathF.Sin(x * .8f + _seconds * 3f);
                canvas.DrawWorld(x, water.Top, wave > .4f ? '~' : '≈', wave > .4f ? ConsoleColor.White : ConsoleColor.Cyan,
                    ConsoleColor.Blue);
                for (var y = water.Top + 1; y <= water.Bottom; y++)
                {
                    var ripple = Noise.Unit(x + drift, y, 3) < .08f;
                    canvas.DrawWorld(x, y, ripple ? '~' : ' ', ConsoleColor.Blue, ConsoleColor.DarkBlue);
                }
            }
        }
    }
}

/// <summary>
/// Fills every block (and the ferry) cell by cell: each cell centre is turned back into the block's own space, tested
/// against its <see cref="Shape"/> and painted by <see cref="Textures"/>. Uses the interpolated position and rotation
/// (<see cref="RenderOffset"/>), so turning blocks move smoothly.
/// </summary>
public sealed class BlockRenderSystem(Canvas canvas, ShapeLibrary shapes) : EntitySystem
{
    private Query _blocks;

    protected override void OnCreate() => _blocks = World.Query(Query.With<BlockLook, Position>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _blocks.GetChunks())
        {
            var looks = chunk.GetSpan<BlockLook>();
            var positions = chunk.GetSpan<Position>();
            var rotations = chunk.Has<Rotation>() ? chunk.GetSpan<Rotation>() : default;
            var offsets = chunk.Has<RenderOffset>() ? chunk.GetSpan<RenderOffset>() : default;
            var blocks = chunk.Has<Block>() ? chunk.GetSpan<Block>() : default;
            var entities = chunk.Entities;
            for (var i = 0; i < chunk.Count; i++)
            {
                var center = positions[i].Value;
                var angle = rotations.IsEmpty ? 0f : rotations[i].Radians;
                if (!offsets.IsEmpty)
                {
                    center += offsets[i].Position;
                    angle += offsets[i].Radians;
                }

                var damage = blocks.IsEmpty ? 0f : 1f - MathF.Max(0f, blocks[i].Health) / blocks[i].MaxHealth;
                Fill(shapes.Get(looks[i].Shape), looks[i].Material, center, angle, damage, entities[i].Id);
            }
        }
    }

    private void Fill(Shape shape, Material material, Vector2 center, float angle, float damage, int seed)
    {
        var (sin, cos) = MathF.SinCos(angle);
        var reach = shape.Radius + 1f;
        var left = (int)MathF.Floor(center.X - reach);
        var right = (int)MathF.Floor(center.X + reach);
        var top = (int)MathF.Floor(center.Y - reach);
        var bottom = (int)MathF.Floor(center.Y + reach);
        for (var y = top; y <= bottom; y++)
        for (var x = left; x <= right; x++)
        {
            var dx = x + .5f - center.X;
            var dy = y + .5f - center.Y;
            var local = new Vector2(dx * cos + dy * sin, -dx * sin + dy * cos);
            var inside = shape.EdgeDistance(local);
            if (inside <= 0f)
                continue;

            var cell = Textures.Paint(material, shape, local.X - shape.Min.X, local.Y - shape.Min.Y, inside, angle,
                damage, seed);
            if (cell.KeepsBackground)
                canvas.DrawWorldOver(x, y, cell.Character, cell.Foreground);
            else
                canvas.DrawWorld(x, y, cell.Character, cell.Foreground, cell.Background);
        }
    }
}

/// <summary>
/// Crystals as 2×2 cut gems (light and dark facets) with a glint that flashes now and then, darker once cracked.
/// </summary>
public sealed class CrystalRenderSystem(Canvas canvas) : EntitySystem
{
    private Query _crystals;
    private float _seconds;

    protected override void OnCreate() => _crystals = World.Query(Query.With<Crystal, Position, Block>());

    protected override void OnUpdate(float deltaSeconds)
    {
        _seconds += deltaSeconds;
        foreach (var chunk in _crystals.GetChunks())
        {
            var positions = chunk.GetSpan<Position>();
            var blocks = chunk.GetSpan<Block>();
            var offsets = chunk.Has<RenderOffset>() ? chunk.GetSpan<RenderOffset>() : default;
            var entities = chunk.Entities;
            for (var i = 0; i < chunk.Count; i++)
            {
                var center = positions[i].Value + (offsets.IsEmpty ? Vector2.Zero : offsets[i].Position);
                var x = (int)MathF.Floor(center.X - .5f);
                var y = (int)MathF.Floor(center.Y - .5f);
                var cracked = blocks[i].Health * 2f < blocks[i].MaxHealth;
                var light = cracked ? ConsoleColor.DarkMagenta : ConsoleColor.Magenta;
                canvas.DrawWorld(x, y, '/', ConsoleColor.White, light);
                canvas.DrawWorld(x + 1, y, '\\', ConsoleColor.Magenta, ConsoleColor.DarkMagenta);
                canvas.DrawWorld(x, y + 1, '\\', ConsoleColor.Magenta, ConsoleColor.DarkMagenta);
                canvas.DrawWorld(x + 1, y + 1, '/', ConsoleColor.DarkMagenta, ConsoleColor.Black);

                var glint = (_seconds * .6f + entities[i].Id * .37f) % 1f;
                if (!cracked && glint < .08f)
                    canvas.DrawWorldOver(x + 2, y - 1, glint < .04f ? '+' : '*', ConsoleColor.White);
            }
        }
    }
}

/// <summary>Shots, splinters and sparks: one character each, over whatever is behind it.</summary>
public sealed class GlyphOverRenderSystem(Canvas canvas) : EntitySystem
{
    private Query _glyphs;

    protected override void OnCreate() => _glyphs = World.Query(Query.With<Glyph, Position>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _glyphs.GetChunks())
        {
            var glyphs = chunk.GetSpan<Glyph>();
            var positions = chunk.GetSpan<Position>();
            var offsets = chunk.Has<RenderOffset>() ? chunk.GetSpan<RenderOffset>() : default;
            for (var i = 0; i < chunk.Count; i++)
            {
                var at = positions[i].Value + (offsets.IsEmpty ? Vector2.Zero : offsets[i].Position);
                canvas.DrawWorldOver((int)MathF.Floor(at.X), (int)MathF.Floor(at.Y), glyphs[i].Character,
                    glyphs[i].Foreground);
            }
        }
    }
}

/// <summary>Puffs fade through their kind's characters and colours as they age.</summary>
public sealed class PuffRenderSystem(Canvas canvas) : EntitySystem
{
    private static readonly char[] SmokeLook = ['O', 'o', '°', '·'];
    private static readonly char[] DustLook = ['▓', '▒', '░', '·'];
    private static readonly char[] FlameLook = ['▓', '▒', '░', '·'];
    private static readonly ConsoleColor[] FlameColors =
        [ConsoleColor.Yellow, ConsoleColor.Red, ConsoleColor.DarkRed, ConsoleColor.DarkGray];
    private static readonly char[] SprayLook = ['o', '°', '·', '·'];

    private Query _puffs;

    protected override void OnCreate() => _puffs = World.Query(Query.With<Puff, Position>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _puffs.GetChunks())
        {
            var puffs = chunk.GetSpan<Puff>();
            var positions = chunk.GetSpan<Position>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref readonly var puff = ref puffs[i];
                var stage = Math.Clamp((int)(puff.Age / puff.Life * 4f), 0, 3);
                var (character, color) = puff.Kind switch
                {
                    PuffKind.Smoke => (SmokeLook[stage], stage < 2 ? ConsoleColor.Gray : ConsoleColor.DarkGray),
                    PuffKind.Flame => (FlameLook[stage], FlameColors[stage]),
                    PuffKind.Spray => (SprayLook[stage], stage < 2 ? ConsoleColor.White : ConsoleColor.Cyan),
                    _ => (DustLook[stage], puff.Color)
                };
                var at = positions[i].Value;
                canvas.DrawWorldOver((int)MathF.Floor(at.X), (int)MathF.Floor(at.Y), character, color);
            }
        }
    }
}

/// <summary>Fireballs: a disc that grows fast, hot (yellow) at the centre and cooling outwards and over time.</summary>
public sealed class BurstRenderSystem(Canvas canvas) : EntitySystem
{
    private Query _bursts;

    protected override void OnCreate() => _bursts = World.Query(Query.With<Burst, Position>());

    protected override void OnUpdate(float deltaSeconds)
    {
        foreach (var chunk in _bursts.GetChunks())
        {
            var bursts = chunk.GetSpan<Burst>();
            var positions = chunk.GetSpan<Position>();
            for (var i = 0; i < chunk.Count; i++)
            {
                var t = Math.Clamp(bursts[i].Age / bursts[i].Life, 0f, 1f);
                var radius = bursts[i].Radius * (1f - (1f - t) * (1f - t) * (1f - t) * .7f);
                Draw(positions[i].Value, radius, t);
            }
        }
    }

    private void Draw(Vector2 center, float radius, float t)
    {
        var left = (int)MathF.Floor(center.X - radius);
        var top = (int)MathF.Floor(center.Y - radius);
        var size = (int)MathF.Ceiling(radius * 2f) + 1;
        for (var y = top; y < top + size; y++)
        for (var x = left; x < left + size; x++)
        {
            var distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), center);
            if (distance > radius)
                continue;

            var heat = (1f - distance / radius * .8f) * (1f - t);
            var (character, color) = heat switch
            {
                > .7f => ('█', ConsoleColor.White),
                > .5f => ('█', ConsoleColor.Yellow),
                > .32f => ('▓', ConsoleColor.Red),
                > .18f => ('▒', ConsoleColor.DarkRed),
                > .06f => ('░', ConsoleColor.DarkGray),
                _ => (' ', ConsoleColor.Black)
            };
            if (character != ' ')
                canvas.DrawWorldOver(x, y, character, color);
        }
    }
}

/// <summary>
/// The catapult: an A-frame on wheels with a throwing arm that sinks back while charging and swings forward on the
/// throw, a counterweight, the loaded shot in the bucket, and the aim guide: the first <see cref="GuideSeconds"/> of the
/// throw's arc, one <see cref="IPhysicsQueries.Raycast"/> per piece against ground and blocks, ending in × where it
/// would hit.
/// </summary>
public sealed class CatapultRenderSystem(Canvas canvas, Catapult catapult, PhysicsSystem physics, Round round)
    : EntitySystem
{
    public const float GuideSeconds = .9f;
    private const float GuideStepSeconds = .06f;
    private const float SwingSeconds = .2f;

    /// <summary>The wooden A-frame on a beam with two wheels, below the pivot, 7 × 3 cells.</summary>
    private static readonly string[] FrameSprite = ["  ▄█▄  ", " ▄▀█▀▄ ", "◘▀▀▀▀▀◘"];

    protected override void OnUpdate(float deltaSeconds)
    {
        var pivot = Cell(catapult.Pivot);
        DrawFrame(pivot);

        var armAngle = ArmAngle();
        var direction = new Vector2(MathF.Cos(armAngle), -MathF.Sin(armAngle));
        for (var step = -1.5f; step <= Catapult.ArmLength; step += .5f)
        {
            var cell = Cell(catapult.Pivot + direction * step);
            canvas.DrawWorldOver(cell.X, cell.Y, '█', ConsoleColor.DarkYellow);
        }

        var weight = Cell(catapult.Pivot - direction * 2f);
        canvas.DrawWorldOver(weight.X, weight.Y, '█', ConsoleColor.Gray);
        canvas.DrawWorldOver(weight.X - 1, weight.Y, '█', ConsoleColor.DarkGray);
        canvas.DrawWorldOver(weight.X, weight.Y + 1, '▀', ConsoleColor.DarkGray);
        canvas.DrawWorldOver(pivot.X, pivot.Y, 'O', ConsoleColor.Gray);

        var bucket = Cell(catapult.Pivot + direction * Catapult.ArmLength);
        var loaded = round.Phase == RoundPhase.Playing && round.HasAmmo(catapult.Ammo) && catapult.Cooldown <= 0f;
        if (!loaded)
            canvas.DrawWorldOver(bucket.X, bucket.Y, 'u', ConsoleColor.DarkYellow);
        else if (catapult.Ammo == ShotKind.Stone)
            canvas.DrawWorldOver(bucket.X, bucket.Y, '●', ConsoleColor.Gray);
        else
            canvas.DrawWorldOver(bucket.X, bucket.Y, '☼', ConsoleColor.Red);

        if (loaded)
            DrawGuide();
    }

    /// <summary>Sinks back while charging, overshoots just after a throw.</summary>
    private float ArmAngle()
    {
        if (catapult.IsCharging)
            return catapult.Angle - catapult.Charge * .5f;

        var sinceThrow = CatapultSystem.ReloadSeconds - catapult.Cooldown;
        return catapult.Cooldown > 0f && sinceThrow < SwingSeconds ? catapult.Angle + .6f : catapult.Angle;
    }

    private void DrawFrame(Point pivot)
    {
        for (var row = 0; row < FrameSprite.Length; row++)
        {
            var line = FrameSprite[row];
            for (var i = 0; i < line.Length; i++)
            {
                if (line[i] != ' ')
                    canvas.DrawWorldOver(pivot.X - 3 + i, pivot.Y + 1 + row, line[i],
                        line[i] == '◘' ? ConsoleColor.Gray : ConsoleColor.DarkYellow);
            }
        }
    }

    private void DrawGuide()
    {
        var start = catapult.Muzzle;
        var velocity = catapult.LaunchVelocity;
        var gravity = physics.Gravity;
        var previous = start;
        var index = 0;
        for (var t = GuideStepSeconds; t <= GuideSeconds; t += GuideStepSeconds, index++)
        {
            var next = Catapult.PointOnArc(start, velocity, gravity, t);
            if (physics.Raycast(previous, next - previous, Layers.Solid, out var hit))
            {
                var cell = Cell(hit.Point);
                canvas.DrawWorldOver(cell.X, cell.Y, '×', ConsoleColor.Red);
                return;
            }

            var dot = Cell(next);
            var color = !catapult.IsCharging ? ConsoleColor.DarkGray
                : index < 5 ? ConsoleColor.White
                : index < 10 ? ConsoleColor.Gray
                : ConsoleColor.DarkGray;
            canvas.DrawWorldOver(dot.X, dot.Y, index < 5 ? '•' : '·', color);
            previous = next;
        }
    }

    private static Point Cell(Vector2 position) => new((int)MathF.Floor(position.X), (int)MathF.Floor(position.Y));
}

/// <summary>
/// Row 0: level, score, crystals, shots left, angle and a power bar going green, yellow, red. Row 1: the keys and the
/// physics' body counts. Numbers are drawn digit by digit, so nothing is allocated per frame.
/// </summary>
public sealed class HudSystem(Canvas canvas, LevelPlan plan, Campaign campaign, Round round, Catapult catapult,
    PhysicsSystem physics) : EntitySystem
{
    private const string Keys = " ↑↓ AIM   HOLD SPACE THROW   1 STONE  2 BOMB   R RESTART   ESC QUIT";
    private const int PowerBarLength = 12;
    private const ConsoleColor Text = ConsoleColor.Gray;
    private const ConsoleColor Value = ConsoleColor.White;
    private const ConsoleColor Bar = ConsoleColor.Black;
    private const ConsoleColor KeysText = ConsoleColor.DarkGray;
    private const ConsoleColor KeysBar = ConsoleColor.Black;

    protected override void OnUpdate(float deltaSeconds)
    {
        for (var x = 0; x < canvas.Width; x++)
        {
            canvas.Draw(x, 0, ' ', Text, Bar);
            canvas.Draw(x, 1, '─', ConsoleColor.DarkBlue, KeysBar);
        }

        var column = canvas.Text(1, 0, "LEVEL ", Text, Bar);
        column = canvas.Number(column, 0, campaign.LevelIndex + 1, Value, Bar);
        column = canvas.Text(column, 0, "/", Text, Bar);
        column = canvas.Number(column, 0, campaign.LevelCount, Text, Bar);
        column = canvas.Text(column + 1, 0, plan.Name, ConsoleColor.Yellow, Bar);
        column = Separator(column);
        column = canvas.Text(column, 0, "SCORE ", Text, Bar);
        column = canvas.Number(column, 0, campaign.TotalScore + round.Score, Value, Bar);
        column = Separator(column);
        column = Icons(column, '♦', ConsoleColor.Magenta, round.Crystals, false);
        column = Separator(column);
        column = Icons(column, '●', ConsoleColor.Gray, round.Stones, catapult.Ammo == ShotKind.Stone);
        column = Icons(column + 1, '☼', ConsoleColor.Red, round.Bombs, catapult.Ammo == ShotKind.Bomb);
        column = Separator(column);
        column = canvas.Text(column, 0, "ANGLE ", Text, Bar);
        column = canvas.Number(column, 0, catapult.AngleDegrees, Value, Bar);
        column = canvas.Text(column, 0, "°", Value, Bar);
        column = Separator(column);
        column = canvas.Text(column, 0, "POWER ", Text, Bar);
        PowerBar(column);

        canvas.Text(0, 1, Keys, KeysText, KeysBar);
        column = canvas.Width - 26;
        column = canvas.Text(column, 1, " BODIES ", KeysText, KeysBar);
        column = canvas.Number(column, 1, physics.BodyCount, KeysText, KeysBar);
        column = canvas.Text(column, 1, "  ASLEEP ", KeysText, KeysBar);
        column = canvas.Number(column, 1, physics.SleepingBodyCount, KeysText, KeysBar);
        canvas.Draw(column, 1, ' ', KeysText, KeysBar);
    }

    private int Separator(int column) => canvas.Text(column, 0, "  │  ", ConsoleColor.DarkGray, Bar);

    /// <summary>One icon per item left; the loaded ammo gets brackets.</summary>
    private int Icons(int column, char icon, ConsoleColor color, int count, bool selected)
    {
        if (selected)
            column = canvas.Text(column, 0, "[", ConsoleColor.Yellow, Bar);
        for (var i = 0; i < count; i++)
            canvas.Draw(column + i, 0, icon, color, Bar);
        column += count == 0 ? canvas.Text(column, 0, "-", ConsoleColor.DarkGray, Bar) - column : count;
        return selected ? canvas.Text(column, 0, "]", ConsoleColor.Yellow, Bar) : column;
    }

    private void PowerBar(int column)
    {
        var filled = catapult.IsCharging ? (int)MathF.Ceiling(catapult.Charge * PowerBarLength) : 0;
        for (var i = 0; i < PowerBarLength; i++)
        {
            var color = i < PowerBarLength / 3 ? ConsoleColor.Green
                : i < PowerBarLength * 2 / 3 ? ConsoleColor.Yellow
                : ConsoleColor.Red;
            if (i < filled)
                canvas.Draw(column + i, 0, '█', color, Bar);
            else
                canvas.Draw(column + i, 0, '░', ConsoleColor.DarkGray, Bar);
        }
    }
}

/// <summary>The framed message box when a level is won or lost.</summary>
public sealed class BannerSystem(Canvas canvas, Campaign campaign, Round round) : EntitySystem
{
    private const int Width = 46;
    private const int Height = 8;
    private const int Top = 13;
    private const ConsoleColor Frame = ConsoleColor.Yellow;
    private const ConsoleColor Foreground = ConsoleColor.White;
    private const ConsoleColor Background = ConsoleColor.DarkBlue;

    protected override void OnUpdate(float deltaSeconds)
    {
        if (round.Phase == RoundPhase.Playing)
            return;

        var left = (canvas.Width - Width) / 2;
        DrawBox(left);

        var text = left + 4;
        if (round.Phase == RoundPhase.Lost)
        {
            canvas.Text(text, Top + 2, "OUT OF SHOTS", ConsoleColor.Red, Background);
            var column = canvas.Text(text, Top + 3, "CRYSTALS LEFT ", Foreground, Background);
            canvas.Number(column, Top + 3, round.Crystals, ConsoleColor.Magenta, Background);
            canvas.Text(text, Top + 5, "R  TRY AGAIN", Foreground, Background);
            return;
        }

        if (campaign.IsLastLevel)
        {
            canvas.Text(text, Top + 2, "ALL TOWERS TOPPLED!", ConsoleColor.Yellow, Background);
            var column = canvas.Text(text, Top + 3, "FINAL SCORE ", Foreground, Background);
            canvas.Number(column, Top + 3, campaign.TotalScore + round.FinalScore, ConsoleColor.Yellow, Background);
            canvas.Text(text, Top + 5, "ENTER  PLAY AGAIN    R  REPLAY LEVEL", Foreground, Background);
            return;
        }

        canvas.Text(text, Top + 2, "LEVEL CLEAR!", ConsoleColor.Yellow, Background);
        var bonus = canvas.Text(text, Top + 3, "SHOTS LEFT BONUS +", Foreground, Background);
        canvas.Number(bonus, Top + 3, round.Bonus, ConsoleColor.Yellow, Background);
        canvas.Text(text, Top + 5, "ENTER  NEXT LEVEL    R  REPLAY LEVEL", Foreground, Background);
    }

    /// <summary>A double-line frame with a shadow one cell down and right.</summary>
    private void DrawBox(int left)
    {
        var right = left + Width - 1;
        var bottom = Top + Height - 1;
        for (var y = Top; y <= bottom; y++)
        for (var x = left; x <= right; x++)
        {
            var character = (x == left, x == right, y == Top, y == bottom) switch
            {
                (true, _, true, _) => '╔',
                (_, true, true, _) => '╗',
                (true, _, _, true) => '╚',
                (_, true, _, true) => '╝',
                (true, _, _, _) or (_, true, _, _) => '║',
                (_, _, true, _) or (_, _, _, true) => '═',
                _ => ' '
            };
            canvas.Draw(x, y, character, Frame, Background);
        }

        for (var x = left + 1; x <= right + 1; x++)
            canvas.Draw(x, bottom + 1, ' ', ConsoleColor.Black, ConsoleColor.Black);
        for (var y = Top + 1; y <= bottom + 1; y++)
            canvas.Draw(right + 1, y, ' ', ConsoleColor.Black, ConsoleColor.Black);
    }
}
