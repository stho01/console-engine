using System;
using System.Buffers.Binary;
using System.IO;

namespace Doom.Graphics;

/// <summary>
/// A decoded WAD picture (wall patch, sprite, weapon): width, height, the offsets of its origin and its pixels as
/// palette indices, stored column by column (<c>x * Height + y</c>) with a mask of which pixels are drawn.
/// </summary>
public sealed class Picture
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const int HeaderSize = 8;
    private const int MaxSize = 4096;
    private const byte EndOfColumn = 0xFF;

    //**********************************************************
    //** ctor
    //**********************************************************

    public Picture(int width, int height, int leftOffset, int topOffset)
    {
        Width = width;
        Height = height;
        LeftOffset = leftOffset;
        TopOffset = topOffset;
        Pixels = new byte[width * height];
        Opaque = new bool[width * height];
    }

    //**********************************************************
    //** props
    //**********************************************************

    public int Width { get; }
    public int Height { get; }

    /// <summary>Pixels from the picture's left edge to its origin (a sprite's centre, a weapon's screen spot).</summary>
    public int LeftOffset { get; }

    /// <summary>Pixels from the picture's top edge to its origin (a sprite's feet).</summary>
    public int TopOffset { get; }

    /// <summary>Palette indices, column by column: <c>x * Height + y</c>.</summary>
    public byte[] Pixels { get; }

    /// <summary>Which pixels are drawn, same layout as <see cref="Pixels"/>.</summary>
    public bool[] Opaque { get; }

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>
    /// Decodes the picture format: header (width, height, left and top offset as shorts), one int offset per column,
    /// then per column a list of posts (top row, length, a pad byte, the pixels, a pad byte) ended by 0xFF. A post
    /// whose top is not below the previous one's continues from it (tall pictures).
    /// </summary>
    /// <exception cref="InvalidDataException">The bytes are not a valid picture.</exception>
    public static Picture Decode(ReadOnlySpan<byte> lump, string name = "")
    {
        if (lump.Length < HeaderSize)
            throw Broken(name);

        var width = BinaryPrimitives.ReadInt16LittleEndian(lump);
        var height = BinaryPrimitives.ReadInt16LittleEndian(lump[2..]);
        var left = BinaryPrimitives.ReadInt16LittleEndian(lump[4..]);
        var top = BinaryPrimitives.ReadInt16LittleEndian(lump[6..]);
        if (width <= 0 || height <= 0 || width > MaxSize || height > MaxSize || lump.Length < HeaderSize + width * 4)
            throw Broken(name);

        var picture = new Picture(width, height, left, top);
        for (var x = 0; x < width; x++)
        {
            var position = BinaryPrimitives.ReadInt32LittleEndian(lump[(HeaderSize + x * 4)..]);
            var previousTop = -1;
            while (true)
            {
                if (position < 0 || position >= lump.Length)
                    throw Broken(name);

                int postTop = lump[position];
                if (postTop == EndOfColumn)
                    break;
                if (position + 3 > lump.Length)
                    throw Broken(name);

                if (postTop <= previousTop)
                    postTop += previousTop;
                previousTop = postTop;

                var length = lump[position + 1];
                var data = position + 3;
                if (data + length > lump.Length)
                    throw Broken(name);

                for (var i = 0; i < length; i++)
                {
                    var y = postTop + i;
                    if (y >= height)
                        break;

                    picture.Pixels[x * height + y] = lump[data + i];
                    picture.Opaque[x * height + y] = true;
                }

                position = data + length + 1;
            }
        }

        return picture;
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private static InvalidDataException Broken(string name) => new(string.Format(DoomTexts.PictureBroken, name));
}
