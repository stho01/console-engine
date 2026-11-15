using Doom.Graphics;
using Doom.Maps;

namespace Doom.Play;

public enum ActorState
{
    /// <summary>Decorations and pickups: loops its idle frames.</summary>
    Static,

    /// <summary>A monster waiting to see or hear the player.</summary>
    Idle,

    Chase,
    Attack,
    Pain,

    /// <summary>A lost soul flying at its target.</summary>
    Charge,

    /// <summary>A projectile in flight.</summary>
    Flying,

    /// <summary>Playing its death or explosion frames.</summary>
    Dying,

    /// <summary>A corpse showing its last death frame.</summary>
    Dead
}

/// <summary>
/// A thing in a running level: a monster, pickup, decoration, projectile, effect or the player's body. Positions
/// are world units, angles radians (0 east, counter-clockwise), momentum world units per tic.
/// </summary>
public sealed class Actor
{
    //**********************************************************
    //** ctor
    //**********************************************************

    public Actor(ThingInfo info, SpriteDefinition sprite)
    {
        Info = info;
        Sprite = sprite;
        Flags = info.Flags;
        Health = info.Health;
        Play(info.Frames, info.FrameTics, true);
    }

    //**********************************************************
    //** props
    //**********************************************************

    public ThingInfo Info { get; }

    /// <summary>The sprite drawn, or null (never drawn).</summary>
    public SpriteDefinition Sprite { get; set; }

    /// <summary>Starts as <see cref="ThingInfo.Flags"/>; a corpse loses <see cref="ThingFlags.Solid"/> and
    /// <see cref="ThingFlags.Shootable"/>.</summary>
    public ThingFlags Flags { get; set; }

    public float X { get; set; }
    public float Y { get; set; }

    /// <summary>Height of the thing's feet.</summary>
    public float Z { get; set; }

    public float Angle { get; set; }
    public float MomX { get; set; }
    public float MomY { get; set; }
    public float MomZ { get; set; }

    /// <summary>Highest floor and lowest ceiling under the thing's whole footprint (last position check).</summary>
    public float FloorZ { get; set; }

    public float CeilingZ { get; set; }
    public Sector Sector { get; set; }
    public int Health { get; set; }
    public ActorState State { get; set; } = ActorState.Static;
    public float Radius => Info.Radius;
    public float Height => Info.Height;
    public bool Removed { get; set; }
    public bool IsPlayer { get; init; }

    /// <summary>Map flag: waits for sight, not sound.</summary>
    public bool Ambush { get; set; }

    /// <summary>Dropped by a dead monster: ammo gives half.</summary>
    public bool Dropped { get; set; }

    /// <summary>Who a monster is after, or who fired a projectile.</summary>
    public Actor Target { get; set; }

    public Actor Owner { get; set; }

    /// <summary>Monster timers, in tics.</summary>
    public int ReactionTics { get; set; }

    public int AttackCooldown { get; set; }
    public int MoveTics { get; set; }
    public float MoveAngle { get; set; }

    /// <summary>Explosion damage still to deal (a barrel deals it two frames into its explosion).</summary>
    public int PendingSplash { get; set; }

    //**********************************************************
    //** animation
    //**********************************************************

    public string Frames { get; private set; }
    public int FrameIndex { get; private set; }
    public int FrameTics { get; private set; }
    public bool Loops { get; private set; }

    /// <summary>A non-looping animation has shown its last frame for its full time.</summary>
    public bool AnimationDone { get; private set; }

    public char Frame => string.IsNullOrEmpty(Frames) ? 'A' : Frames[FrameIndex];
    public bool Has(ThingFlags flag) => (Flags & flag) != 0;

    //**********************************************************
    //** public methods
    //**********************************************************

    public void Play(string frames, int ticsPerFrame, bool loop)
    {
        Frames = string.IsNullOrEmpty(frames) ? "A" : frames;
        FrameIndex = 0;
        _ticsPerFrame = ticsPerFrame < 1 ? 1 : ticsPerFrame;
        FrameTics = _ticsPerFrame;
        Loops = loop;
        AnimationDone = false;
    }

    /// <summary>Advances the animation by one tic. Returns true on the tic the frame changes.</summary>
    public bool Animate()
    {
        if (AnimationDone || --FrameTics > 0)
            return false;

        FrameTics = _ticsPerFrame;
        if (FrameIndex + 1 < Frames.Length)
        {
            FrameIndex++;
            return true;
        }

        if (Loops)
        {
            FrameIndex = 0;
            return Frames.Length > 1;
        }

        AnimationDone = true;
        return false;
    }

    //**********************************************************
    //** fields
    //**********************************************************

    private int _ticsPerFrame;
}
