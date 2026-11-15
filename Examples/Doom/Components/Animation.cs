using Doom.Play;

namespace Doom.Components;

/// <summary>
/// The sprite a thing is drawn with and where it is in one of its frame sequences (<see cref="AnimationClip"/>). Every
/// thing but the player's body has one. <see cref="Animator"/> plays and advances it.
/// </summary>
public struct Animation
{
    /// <summary>The <see cref="Graphics.SpriteLibrary"/> id of the sprite drawn, or -1 (never drawn).</summary>
    public int Sprite;

    public AnimationClip Clip;

    /// <summary>The frame letter shown.</summary>
    public char Frame;

    public int FrameIndex;
    public int FrameCount;
    public int TicsPerFrame;

    /// <summary>Tics until the next frame.</summary>
    public int TicsLeft;

    public bool Loops;

    /// <summary>A non-looping animation has shown its last frame for its full time.</summary>
    public bool Done;
}
