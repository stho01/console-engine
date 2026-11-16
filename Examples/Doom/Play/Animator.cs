using Doom.Components;

namespace Doom.Play;

/// <summary>Starts and advances a thing's <see cref="Animation"/>. Everything is per tic.</summary>
public static class Animator
{
    //**********************************************************
    //** public methods
    //**********************************************************

    public static void Play(ref Animation animation, ThingInfo info, AnimationClip clip, int ticsPerFrame, bool loop)
    {
        var frames = info.FramesOf(clip);
        animation.Clip = clip;
        animation.FrameIndex = 0;
        animation.FrameCount = frames.Length;
        animation.Frame = frames[0];
        animation.TicsPerFrame = ticsPerFrame < 1 ? 1 : ticsPerFrame;
        animation.TicsLeft = animation.TicsPerFrame;
        animation.Loops = loop;
        animation.Done = false;
    }

    /// <summary>Advances the animation by one tic. Returns true on the tic the frame changes.</summary>
    public static bool Advance(ref Animation animation, ThingInfo info)
    {
        if (animation.Done || --animation.TicsLeft > 0)
            return false;

        animation.TicsLeft = animation.TicsPerFrame;
        if (animation.FrameIndex + 1 < animation.FrameCount)
        {
            animation.FrameIndex++;
            animation.Frame = info.FramesOf(animation.Clip)[animation.FrameIndex];
            return true;
        }

        if (animation.Loops)
        {
            animation.FrameIndex = 0;
            animation.Frame = info.FramesOf(animation.Clip)[0];
            return animation.FrameCount > 1;
        }

        animation.Done = true;
        return false;
    }
}
