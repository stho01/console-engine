using ConsoleEngine.Ecs;

namespace Doom.Components;

/// <summary>
/// A thing that explodes when killed (a barrel): <see cref="PendingDamage"/> is dealt two frames into its death,
/// in the name of <see cref="Source"/>, whoever killed it.
/// </summary>
public struct Explosive
{
    public int PendingDamage;
    public Entity Source;
}
