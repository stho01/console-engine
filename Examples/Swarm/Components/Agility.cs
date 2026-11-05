namespace Swarm.Components;

/// <summary>
/// How hard this particle is pulled towards the attractor, cells/s². Different per particle, so they settle into
/// different orbits instead of collapsing into one blob.
/// </summary>
public struct Agility(float pull)
{
    public float Pull = pull;
}
