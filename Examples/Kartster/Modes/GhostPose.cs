namespace Kartster.Modes;

/// <summary>
/// Where a kart was at one fixed step: its ground point (world X and Z), the road height under it and its
/// <c>Rotation.Radians</c>, as the <c>KartRenderSystem</c> draws a kart.
/// </summary>
public readonly record struct GhostPose(float X, float Z, float Height, float Radians);
