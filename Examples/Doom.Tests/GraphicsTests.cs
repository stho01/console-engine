using System;
using Doom.Graphics;
using Doom.Tests.Fakes;
using NUnit.Framework;

namespace Doom.Tests;

public class GraphicsTests
{
    [Test]
    public void Load_TextureFromPatch_HasPatchPixels()
    {
        var (_, _, textures, _) = TestWad.Load();

        var index = textures.FindTexture("STARTAN");

        Assert.That(index, Is.GreaterThanOrEqualTo(0));
        var texture = textures.Textures[index];
        Assert.That(texture.Width, Is.EqualTo(8));
        Assert.That(texture.Pixels, Is.All.EqualTo(TestWad.WallColor));
    }

    [Test]
    public void FindTexture_Dash_IsNoTexture()
    {
        var (_, _, textures, _) = TestWad.Load();

        Assert.That(textures.FindTexture("-"), Is.EqualTo(TextureLibrary.NoTexture));
    }

    [Test]
    public void Load_Flats_FindsSkyFlat()
    {
        var (_, _, textures, _) = TestWad.Load();

        Assert.That(textures.FindFlat("FLOOR"), Is.GreaterThanOrEqualTo(0));
        Assert.That(textures.SkyFlat, Is.EqualTo(textures.FindFlat("F_SKY1")));
    }

    [Test]
    public void SwitchPartner_Sw1Texture_ReturnsSw2Texture()
    {
        var (_, _, textures, _) = TestWad.Load();

        var partner = textures.SwitchPartner(textures.FindTexture("SW1TEST"));

        Assert.That(partner, Is.EqualTo(textures.FindTexture("SW2TEST")));
    }

    [Test]
    public void Animate_FlatSequence_CyclesFrames()
    {
        var textures = new TextureLibrary();
        var first = textures.AddFlat(new Flat("NUKAGE1", new byte[4096]));
        textures.AddFlat(new Flat("NUKAGE2", new byte[4096]));
        var third = textures.AddFlat(new Flat("NUKAGE3", new byte[4096]));
        textures.FinishLoading();

        textures.Animate(TextureLibrary.AnimationTics * 2);

        Assert.That(textures.FlatFrame[first], Is.EqualTo(third));
    }

    [Test]
    public void Load_Sprites_FindsFrameA()
    {
        var (_, _, _, sprites) = TestWad.Load();

        var frame = sprites.Find("STIM")?.Get('A');

        Assert.That(frame, Is.Not.Null);
        Assert.That(frame.Rotates, Is.False);
        Assert.That(frame.Get(5, out _).Width, Is.EqualTo(4));
    }

    [Test]
    public void Add_MirroredRotation_UsesSamePictureMirrored()
    {
        var sprites = new SpriteLibrary();
        var picture = new Picture(2, 2, 1, 2);
        sprites.Add("TEST", 'A', '2', picture, false);
        sprites.Add("TEST", 'A', '8', picture, true);

        var frame = sprites.Find("TEST").Get('A');

        Assert.That(frame.Get(1, out var first), Is.SameAs(picture));
        Assert.That(first, Is.False);
        Assert.That(frame.Get(7, out var second), Is.SameAs(picture));
        Assert.That(second, Is.True);
    }

    [TestCase(0, ConsoleColor.Black)]
    [TestCase(255, ConsoleColor.White)]
    public void Pick_GreyPaletteExtremes_MatchesConsoleColour(int color, ConsoleColor expected)
    {
        var (_, palette, _, _) = TestWad.Load();
        var shades = new ConsoleShades(palette, 0);

        for (var y = 0; y < 4; y++)
        for (var x = 0; x < 4; x++)
            Assert.That(shades.Pick(0, (byte)color, x, y), Is.EqualTo(expected));
    }

    [Test]
    public void Pick_MidGrey_DithersBetweenTwoColours()
    {
        var (_, palette, _, _) = TestWad.Load();
        var shades = new ConsoleShades(palette, 0);

        var (low, high, mix) = shades.Describe(0, 96);

        Assert.That(mix, Is.InRange(0, 16));
        Assert.That(low, Is.Not.EqualTo(ConsoleColor.Red));
        Assert.That(high, Is.Not.EqualTo(ConsoleColor.Blue));
    }

    [Test]
    public void SetGamma_PastLastLevel_WrapsToFirst()
    {
        var (_, palette, _, _) = TestWad.Load();
        var shades = new ConsoleShades(palette, 0);

        shades.SetGamma(ConsoleShades.GammaLevels.Length);

        Assert.That(shades.GammaLevel, Is.EqualTo(0));
    }
}
