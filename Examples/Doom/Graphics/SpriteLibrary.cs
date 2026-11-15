using System;
using System.Collections.Generic;
using Doom.Wad;

namespace Doom.Graphics;

/// <summary>
/// One animation frame of a sprite: either a single picture seen from every side, or eight pictures, one per
/// 45° the viewer stands around the thing (0 = in front of it, counting counter-clockwise). A picture may be used
/// mirrored for the opposite rotation.
/// </summary>
public sealed class SpriteFrame
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const int Rotations = 8;

    //**********************************************************
    //** props
    //**********************************************************

    public Picture[] Pictures { get; } = new Picture[Rotations];
    public bool[] Mirrored { get; } = new bool[Rotations];

    /// <summary>False when one picture is shown from every side.</summary>
    public bool Rotates { get; set; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>The picture for <paramref name="rotation"/> (0-7), and whether to draw it mirrored.</summary>
    public Picture Get(int rotation, out bool mirrored)
    {
        var index = Rotates ? rotation & (Rotations - 1) : 0;
        mirrored = Mirrored[index];
        return Pictures[index] ?? Pictures[0];
    }
}

/// <summary>The frames (A, B, C…) of one sprite, by its four letter name (<c>TROO</c>, <c>POSS</c>…).</summary>
public sealed class SpriteDefinition(string name)
{
    //**********************************************************
    //** props
    //**********************************************************

    public string Name { get; } = name;

    /// <summary>Indexed by frame letter - 'A'. Missing frames are null.</summary>
    public SpriteFrame[] Frames { get; } = new SpriteFrame[29];

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>The frame for <paramref name="letter"/>, falling back to the first frame the sprite has.</summary>
    public SpriteFrame Get(char letter)
    {
        var index = letter - 'A';
        if (index >= 0 && index < Frames.Length && Frames[index] != null)
            return Frames[index];

        for (var i = 0; i < Frames.Length; i++)
        {
            if (Frames[i] != null)
                return Frames[i];
        }

        return null;
    }
}

/// <summary>
/// Every sprite between <c>S_START</c> and <c>S_END</c>. A sprite lump is named <c>NAMEfr</c> (frame letter, rotation
/// digit; rotation 0 means "all sides") and may carry a second frame and rotation, <c>NAMEfrFR</c>, that uses the same
/// picture mirrored. Weapon sprites (<c>PISG</c>, <c>SHTG</c>…) are in the same range.
/// </summary>
public sealed class SpriteLibrary
{
    //**********************************************************
    //** fields
    //**********************************************************

    private readonly Dictionary<string, SpriteDefinition> _sprites = new(StringComparer.Ordinal);

    //**********************************************************
    //** public methods
    //**********************************************************

    public static SpriteLibrary Load(WadFile wad)
    {
        var library = new SpriteLibrary();
        var start = wad.IndexOf("S_START");
        if (start < 0)
            start = wad.IndexOf("SS_START");
        var end = wad.IndexOf("S_END");
        if (end < 0)
            end = wad.IndexOf("SS_END");
        if (start < 0 || end <= start)
            return library;

        for (var i = start + 1; i < end; i++)
        {
            var lump = wad.Lumps[i];
            if (lump.Size == 0 || lump.Name.Length < 6)
                continue;

            var picture = Picture.Decode(wad.Read(i), lump.Name);
            library.Add(lump.Name[..4], lump.Name[4], lump.Name[5], picture, false);
            if (lump.Name.Length >= 8)
                library.Add(lump.Name[..4], lump.Name[6], lump.Name[7], picture, true);
        }

        return library;
    }

    /// <summary>The sprite called <paramref name="name"/>, or null.</summary>
    public SpriteDefinition Find(string name) => _sprites.GetValueOrDefault(name);

    /// <summary>Adds a picture as frame <paramref name="frame"/>, rotation <paramref name="rotation"/> ('0'-'8').</summary>
    public void Add(string name, char frame, char rotation, Picture picture, bool mirrored)
    {
        var frameIndex = frame - 'A';
        var rotationNumber = rotation - '0';
        if (frameIndex < 0 || frameIndex >= 29 || rotationNumber < 0 || rotationNumber > SpriteFrame.Rotations)
            return;

        if (!_sprites.TryGetValue(name, out var sprite))
        {
            sprite = new SpriteDefinition(name);
            _sprites.Add(name, sprite);
        }

        var spriteFrame = sprite.Frames[frameIndex] ??= new SpriteFrame();
        if (rotationNumber == 0)
        {
            for (var r = 0; r < SpriteFrame.Rotations; r++)
            {
                spriteFrame.Pictures[r] = picture;
                spriteFrame.Mirrored[r] = mirrored;
            }

            spriteFrame.Rotates = false;
            return;
        }

        spriteFrame.Pictures[rotationNumber - 1] = picture;
        spriteFrame.Mirrored[rotationNumber - 1] = mirrored;
        spriteFrame.Rotates = true;
    }
}
