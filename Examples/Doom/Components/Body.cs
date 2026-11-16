namespace Doom.Components;

/// <summary>
/// Where a thing is and how much room it takes: a circle of <see cref="Radius"/> on the map, <see cref="Height"/>
/// tall. Positions are world units, angles radians (0 east, counter-clockwise).
/// </summary>
public struct Body
{
    public float X;
    public float Y;

    /// <summary>Height of the thing's feet.</summary>
    public float Z;

    public float Angle;
    public float Radius;
    public float Height;

    /// <summary>Highest floor and lowest ceiling under the thing's whole footprint (last position check).</summary>
    public float FloorZ;

    public float CeilingZ;

    /// <summary>Index into <see cref="Maps.Map.Sectors"/> of the sector under the thing's centre.</summary>
    public int Sector;
}
