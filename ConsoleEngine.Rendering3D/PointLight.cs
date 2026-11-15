using System.Numerics;

namespace ConsoleEngine.Rendering3D;

/// <summary>
/// A local light, evaluated per pixel: it fades to nothing at <see cref="Radius"/> (<c>(1 - d²/r²)²</c>) and lights
/// a surface by how much it faces the light. With a <see cref="ConeCosine"/> above -0.5 it is a spotlight along
/// <see cref="Direction"/>, soft-edged over 0.08 of cosine (Kartster's <c>DeferredLight.fx</c> point light).
/// </summary>
/// <param name="Position">Where it shines from, world space.</param>
/// <param name="Color">Colour times brightness; may be above 1.</param>
/// <param name="Radius">Where it has faded to nothing, world units.</param>
/// <param name="Direction">Spotlight: the unit vector it shines along. Ignored without a cone.</param>
/// <param name="ConeCosine">Spotlight: the cosine of the angle either side of <see cref="Direction"/> it reaches;
/// -1 (default): a point light, all round.</param>
public readonly record struct PointLight(Vector3 Position, Vector3 Color, float Radius, Vector3 Direction = default,
    float ConeCosine = -1f)
{
    /// <summary>How wide the soft edge of a cone is, in cosine.</summary>
    public const float ConeSoftness = 0.08f;
}
