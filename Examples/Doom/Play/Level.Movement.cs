using System;
using ConsoleEngine.Ecs;
using Doom.Components;
using Doom.Maps;

namespace Doom.Play;

/// <summary>The result of checking a thing at a position: what it would stand on and what blocks it.</summary>
public struct PositionCheck
{
    /// <summary>Highest floor and lowest ceiling under the footprint.</summary>
    public float FloorZ;

    public float CeilingZ;

    /// <summary>Lowest floor under the footprint (a monster won't walk off a ledge higher than a step).</summary>
    public float DropOffZ;

    /// <summary>The wall that blocks, or null.</summary>
    public Line BlockingLine;

    /// <summary>The thing that blocks, or <see cref="Entity.Null"/>.</summary>
    public Entity BlockingThing;

    public bool Blocked => BlockingLine != null || BlockingThing != Entity.Null;
}

public sealed partial class Level
{
    //**********************************************************
    //** fields
    //**********************************************************

    /// <summary>Momentum is split into steps no longer than this, so fast things can't pass through thin walls.</summary>
    private const float MaxStep = 15;

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Checks <paramref name="entity"/>'s footprint (a circle) at (x, y) against every line and solid thing. A line
    /// blocks when it is one-sided, flagged as blocking, or its opening is too low; the floor and ceiling heights are
    /// the tightest of the sector at the centre and every two-sided line the circle touches.
    /// </summary>
    public void CheckPosition(Entity entity, float x, float y, out PositionCheck check)
    {
        ref var thing = ref World.Get<Thing>(entity);
        ref var body = ref World.Get<Body>(entity);
        var sector = Map.SectorAt(x, y);
        check = new PositionCheck
        {
            FloorZ = sector.FloorHeight,
            CeilingZ = sector.CeilingHeight,
            DropOffZ = sector.FloorHeight
        };

        var radius = body.Radius;
        var isMissile = (thing.Flags & ThingFlags.Missile) != 0;
        var isMonster = (thing.Flags & ThingFlags.Monster) != 0;
        var lines = Map.Lines;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (x + radius <= line.MinX || x - radius >= line.MaxX || y + radius <= line.MinY
                || y - radius >= line.MaxY)
                continue;
            if (DistanceToSegmentSquared(x, y, line) >= radius * radius)
                continue;

            if (line.Back == null)
            {
                check.BlockingLine = line;
                return;
            }

            if (!isMissile && ((line.Flags & LineFlags.Blocking) != 0
                               || (isMonster && (line.Flags & LineFlags.BlockMonsters) != 0)))
            {
                check.BlockingLine = line;
                return;
            }

            var front = line.FrontSector;
            var back = line.BackSector;
            var openTop = MathF.Min(front.CeilingHeight, back.CeilingHeight);
            var openBottom = MathF.Max(front.FloorHeight, back.FloorHeight);
            var lowFloor = MathF.Min(front.FloorHeight, back.FloorHeight);
            check.FloorZ = MathF.Max(check.FloorZ, openBottom);
            check.CeilingZ = MathF.Min(check.CeilingZ, openTop);
            check.DropOffZ = MathF.Min(check.DropOffZ, lowFloor);
            if (isMissile && (body.Z < openBottom || body.Z + body.Height > openTop))
            {
                check.BlockingLine = line;
                return;
            }
        }

        var owner = isMissile ? World.Get<Missile>(entity).Owner : Entity.Null;
        foreach (var chunk in _bodies.GetChunks())
        {
            var entities = chunk.Entities;
            var things = chunk.GetSpan<Thing>();
            var bodies = chunk.GetSpan<Body>();
            for (var i = 0; i < chunk.Count; i++)
            {
                ref var other = ref things[i];
                var otherEntity = entities[i];
                if (otherEntity == entity || other.Removed || (other.Flags & ThingFlags.Solid) == 0
                    || otherEntity == owner)
                    continue;
                if (isMissile && (other.Flags & ThingFlags.Missile) != 0)
                    continue;

                ref var otherBody = ref bodies[i];
                var reach = radius + otherBody.Radius;
                if (MathF.Abs(otherBody.X - x) >= reach || MathF.Abs(otherBody.Y - y) >= reach)
                    continue;
                if (isMissile && (body.Z > otherBody.Z + otherBody.Height || body.Z + body.Height < otherBody.Z))
                    continue;

                check.BlockingThing = otherEntity;
                return;
            }
        }
    }

    /// <summary>
    /// Moves <paramref name="entity"/> to (x, y) if nothing blocks it there: the opening must fit its height, a step
    /// up must be at most <see cref="MaxStepHeight"/>, and monsters don't step off ledges higher than that.
    /// </summary>
    public bool TryMove(Entity entity, float x, float y, out PositionCheck check)
    {
        CheckPosition(entity, x, y, out check);
        if (check.Blocked)
            return false;

        var flags = World.Get<Thing>(entity).Flags;
        ref var body = ref World.Get<Body>(entity);
        if ((flags & ThingFlags.Missile) == 0)
        {
            if (check.CeilingZ - check.FloorZ < body.Height)
                return Block(ref check);
            if ((flags & ThingFlags.Float) == 0)
            {
                if (check.FloorZ - body.Z > MaxStepHeight || check.CeilingZ - body.Z < body.Height)
                    return Block(ref check);
                if ((flags & ThingFlags.Monster) != 0 && check.FloorZ - check.DropOffZ > MaxStepHeight)
                    return Block(ref check);
            }
            else if (body.Z < check.FloorZ || body.Z + body.Height > check.CeilingZ)
            {
                return Block(ref check);
            }
        }

        body.X = x;
        body.Y = y;
        body.FloorZ = check.FloorZ;
        body.CeilingZ = check.CeilingZ;
        body.Sector = Map.SectorAt(x, y).Index;
        return true;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>A blocked move with no single blocker (height): reports the sector's first line as the blocker.</summary>
    private static bool Block(ref PositionCheck check)
    {
        check.BlockingLine ??= LineNone;
        return false;
    }

    /// <summary>Stand-in blocker for moves blocked by heights rather than a particular line.</summary>
    private static readonly Line LineNone = new();

    /// <summary>
    /// Moves a walker by its momentum in steps of at most <see cref="MaxStep"/>, sliding along walls: when the full
    /// step is blocked it tries the x and y parts alone and cancels the blocked part.
    /// </summary>
    private void MoveByMomentum(Entity entity)
    {
        ref var body = ref World.Get<Body>(entity);
        ref var momentum = ref World.Get<Momentum>(entity);
        var momX = momentum.X;
        var momY = momentum.Y;
        var steps = (int)MathF.Ceiling(MathF.Max(MathF.Abs(momX), MathF.Abs(momY)) / MaxStep);
        if (steps < 1)
            return;

        var stepX = momX / steps;
        var stepY = momY / steps;
        for (var i = 0; i < steps; i++)
        {
            var oldX = body.X;
            var oldY = body.Y;
            if (!TryMove(entity, oldX + stepX, oldY + stepY, out _))
            {
                if (stepX != 0 && TryMove(entity, oldX + stepX, oldY, out _))
                {
                    stepY = 0;
                    momentum.Y = 0;
                }
                else if (stepY != 0 && TryMove(entity, oldX, oldY + stepY, out _))
                {
                    stepX = 0;
                    momentum.X = 0;
                }
                else
                {
                    momentum.X = 0;
                    momentum.Y = 0;
                    return;
                }
            }

            if (IsPlayer(entity))
                CheckWalkLines(entity, oldX, oldY);
        }
    }

    /// <summary>Fires the walk-over specials of every line the move from (oldX, oldY) crossed.</summary>
    private void CheckWalkLines(Entity entity, float oldX, float oldY)
    {
        ref var body = ref World.Get<Body>(entity);
        for (var i = 0; i < _walkLines.Count; i++)
        {
            var line = _walkLines[i];
            if (line.Special == 0)
                continue;

            var before = line.SideOf(oldX, oldY);
            var after = line.SideOf(body.X, body.Y);
            if ((before > 0) == (after > 0) || !SegmentsCross(oldX, oldY, body.X, body.Y, line, out _))
                continue;

            ActivateLine(line, entity, LineTrigger.Walk, before > 0);
            if (Exited)
                return;
        }
    }

    /// <summary>Gravity and floor contact for a walker.</summary>
    internal static void ApplyGravity(ref Body body, ref Momentum momentum)
    {
        if (body.Z > body.FloorZ)
        {
            momentum.Z -= Gravity;
            body.Z += momentum.Z;
        }

        if (body.Z > body.FloorZ)
            return;

        body.Z = body.FloorZ;
        momentum.Z = 0;
    }

    public static float DistanceToSegmentSquared(float x, float y, Line line)
    {
        var dx = line.Dx;
        var dy = line.Dy;
        var lengthSquared = dx * dx + dy * dy;
        var t = lengthSquared > 0 ? ((x - line.V1.X) * dx + (y - line.V1.Y) * dy) / lengthSquared : 0;
        t = Math.Clamp(t, 0, 1);
        var px = line.V1.X + dx * t - x;
        var py = line.V1.Y + dy * t - y;
        return px * px + py * py;
    }

    /// <summary>
    /// Whether the segment (ax, ay)-(bx, by) crosses <paramref name="line"/>; <paramref name="fraction"/> is how far
    /// along the segment, 0-1.
    /// </summary>
    public static bool SegmentsCross(float ax, float ay, float bx, float by, Line line, out float fraction)
    {
        fraction = 0;
        var rx = bx - ax;
        var ry = by - ay;
        var sx = line.Dx;
        var sy = line.Dy;
        var denominator = rx * sy - ry * sx;
        if (MathF.Abs(denominator) < 1e-6f)
            return false;

        var qx = line.V1.X - ax;
        var qy = line.V1.Y - ay;
        var t = (qx * sy - qy * sx) / denominator;
        var u = (qx * ry - qy * rx) / denominator;
        if (t < 0 || t > 1 || u < 0 || u > 1)
            return false;

        fraction = t;
        return true;
    }

    /// <summary>
    /// Whether <paramref name="from"/> can see any part of <paramref name="to"/>: the vertical window from its eyes
    /// to the target's feet and head is narrowed by every two-sided line's opening in between; a one-sided line or a
    /// closed window blocks.
    /// </summary>
    public bool CanSee(in Body from, in Body to)
    {
        var ax = from.X;
        var ay = from.Y;
        var bx = to.X;
        var by = to.Y;
        var distance = MathF.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
        if (distance < 1)
            return true;

        var eye = from.Z + from.Height * 0.75f;
        var topSlope = (to.Z + to.Height - eye) / distance;
        var bottomSlope = (to.Z - eye) / distance;
        var minX = MathF.Min(ax, bx);
        var maxX = MathF.Max(ax, bx);
        var minY = MathF.Min(ay, by);
        var maxY = MathF.Max(ay, by);
        var lines = Map.Lines;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.MaxX < minX || line.MinX > maxX || line.MaxY < minY || line.MinY > maxY)
                continue;
            if (!SegmentsCross(ax, ay, bx, by, line, out var fraction))
                continue;
            if (line.Back == null)
                return false;

            var front = line.FrontSector;
            var back = line.BackSector;
            var openTop = MathF.Min(front.CeilingHeight, back.CeilingHeight);
            var openBottom = MathF.Max(front.FloorHeight, back.FloorHeight);
            if (openBottom >= openTop)
                return false;

            var along = fraction * distance;
            if (along < 1)
                continue;

            if (front.FloorHeight != back.FloorHeight)
                bottomSlope = MathF.Max(bottomSlope, (openBottom - eye) / along);
            if (front.CeilingHeight != back.CeilingHeight)
                topSlope = MathF.Min(topSlope, (openTop - eye) / along);
            if (topSlope <= bottomSlope)
                return false;
        }

        return true;
    }

    internal static float AngleTo(in Body from, float x, float y) => MathF.Atan2(y - from.Y, x - from.X);

    internal static float Distance(in Body a, in Body b) =>
        MathF.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
}
