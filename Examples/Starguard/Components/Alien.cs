namespace Starguard.Components;

/// <summary>
/// One member of the marching formation. Its two animation sprites are <see cref="FirstFrame"/> and the id after it.
/// </summary>
public struct Alien(int firstFrame, int points)
{
    public int FirstFrame = firstFrame;

    /// <summary>Score for shooting it.</summary>
    public int Points = points;
}
