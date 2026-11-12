namespace Topple.Rendering;

/// <summary>
/// Cheap, repeatable randomness for textures: the same cell always gets the same value, so pebbles, stars and cracks
/// stay put from frame to frame without storing them.
/// </summary>
public static class Noise
{
    public static uint Hash(int x, int y, int seed)
    {
        unchecked
        {
            var h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }
    }

    /// <summary>0 (inclusive) to 1 (exclusive).</summary>
    public static float Unit(int x, int y, int seed) => (Hash(x, y, seed) & 0xFFFFFF) / 16777216f;
}
