using System;
using System.IO;
using Doom.Graphics;
using Doom.Tests.Fakes;
using Doom.Wad;
using NUnit.Framework;

namespace Doom.Tests;

public class WadFileTests
{
    [Test]
    public void Parse_ValidWad_ReadsEveryLump()
    {
        var wad = WadFile.Parse(TestWad.Build());

        Assert.That(wad.Contains("PLAYPAL"), Is.True);
        Assert.That(wad.Read("PLAYPAL").Length, Is.EqualTo(768 * 14));
        Assert.That(wad.IndexOf("E1M1"), Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void Parse_WrongId_ThrowsInvalidData()
    {
        var bytes = TestWad.Build();
        bytes[0] = (byte)'X';

        Assert.Throws<InvalidDataException>(() => WadFile.Parse(bytes));
    }

    [Test]
    public void Parse_TooShort_ThrowsInvalidData() =>
        Assert.Throws<InvalidDataException>(() => WadFile.Parse([1, 2, 3]));

    [Test]
    public void Parse_DirectoryOutsideFile_ThrowsInvalidData()
    {
        var bytes = TestWad.Build();
        BitConverter.GetBytes(bytes.Length).CopyTo(bytes, 8);

        Assert.Throws<InvalidDataException>(() => WadFile.Parse(bytes));
    }

    [Test]
    public void IndexOf_RepeatedName_FindsLastOne()
    {
        var lumps = new (string Name, byte[] Data)[] { ("A", new byte[] { 1 }), ("A", new byte[] { 2, 3 }) };
        var wad = WadFile.Parse(TestWad.Wad(lumps));

        Assert.That(wad.IndexOf("A"), Is.EqualTo(1));
        Assert.That(wad.Read("A").Length, Is.EqualTo(2));
    }

    [Test]
    public void Read_MissingLump_ThrowsInvalidData()
    {
        var wad = WadFile.Parse(TestWad.Build());

        Assert.Throws<InvalidDataException>(() => wad.Read("NOPE"));
    }

    [Test]
    public void ReadName_LowerCaseAndPadding_ReturnsUpperCaseName() =>
        Assert.That(WadFile.ReadName([(byte)'s', (byte)'k', (byte)'y', (byte)'1', 0, 0, 0, 0]), Is.EqualTo("SKY1"));

    [Test]
    public void Decode_PictureWithGap_KeepsGapTransparent()
    {
        // 1 × 4 picture: rows 0-1 drawn (colours 7, 8), rows 2-3 empty.
        byte[] lump =
        [
            1, 0, 4, 0, 0, 0, 0, 0, // width, height, offsets
            12, 0, 0, 0, // column 0 starts at byte 12
            0, 2, 0, 7, 8, 0, // post: top 0, length 2
            0xFF
        ];

        var picture = Picture.Decode(lump);

        Assert.That(picture.Pixels[0], Is.EqualTo(7));
        Assert.That(picture.Pixels[1], Is.EqualTo(8));
        Assert.That(picture.Opaque[1], Is.True);
        Assert.That(picture.Opaque[2], Is.False);
    }

    [Test]
    public void Decode_ColumnPastEnd_ThrowsInvalidData()
    {
        byte[] lump = [1, 0, 4, 0, 0, 0, 0, 0, 200, 0, 0, 0];

        Assert.Throws<InvalidDataException>(() => Picture.Decode(lump));
    }
}
