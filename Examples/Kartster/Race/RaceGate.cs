using System.Numerics;

namespace Kartster.Race;

/// <summary>
/// A start/finish line or checkpoint, as Kartster's <c>RaceGatePlane</c> seen from above: an upright wall of air
/// across the road through <see cref="Point"/>, facing along the road (<see cref="Forward"/>), reaching
/// <see cref="Left"/> metres to the left and <see cref="Right"/> to the right. Planar: x = world X, y = world Z. The
/// original's height limits (2 m under the road to 60 m over it) are left out, because the port's karts never leave
/// the ground.
/// </summary>
public readonly record struct RaceGate(Vector2 Point, Vector2 Forward, float Left, float Right)
{
    //**********************************************************
    //** props
    //**********************************************************

    /// <summary>Kartster's <c>Across</c>: <c>Right</c> of the road, laid flat.</summary>
    public Vector2 Across => new(-Forward.Y, Forward.X);

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Which way the step from <paramref name="from"/> to <paramref name="to"/> went through the gate: 1 forwards,
    /// -1 backwards, 0 not at all (or beside it). As Kartster's <c>RaceGatePlane.Cross</c>.
    /// </summary>
    public int Cross(Vector2 from, Vector2 to)
    {
        var before = Vector2.Dot(from - Point, Forward);
        var after = Vector2.Dot(to - Point, Forward);
        var direction = before < 0f && after >= 0f ? 1
            : before >= 0f && after < 0f ? -1
            : 0;
        if (direction == 0)
            return 0;

        var hit = Vector2.Lerp(from, to, before / (before - after));
        var lateral = Vector2.Dot(hit - Point, Across);
        return lateral >= -Left && lateral <= Right ? direction : 0;
    }
}
