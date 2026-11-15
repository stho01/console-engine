namespace Doom.Play;

/// <summary>Which of a thing's frame sequences plays: <see cref="ThingInfo.FramesOf"/> gives its letters.</summary>
public enum AnimationClip
{
    /// <summary>Standing, or flying for projectiles (<see cref="ThingInfo.Frames"/>).</summary>
    Idle,

    Walk,
    Attack,
    Pain,

    /// <summary>Dying or exploding (<see cref="ThingInfo.DeathFrames"/>); effects play only this.</summary>
    Death
}
