using System;
using EternalDigger.Components;
using EternalDigger.Effects;
using NUnit.Framework;

namespace EternalDigger.Tests;

public class ParticleLooksTests
{
    private static readonly ParticleStyle[] Styles = Enum.GetValues<ParticleStyle>();

    [Test]
    public void GlyphAt_Birth_IsNeverBlank()
    {
        // A blank glyph drawn with DrawOver would erase the cell's character for the particle's first frame.
        foreach (var style in Styles)
        for (var variant = 0; variant < 256; variant++)
            Assert.That(ParticleLooks.GlyphAt(style, (byte)variant, 0f).Character, Is.Not.EqualTo(' '), $"{style}");
    }

    [Test]
    public void GlyphAt_AnyProgress_MatchesCharacterAndColor()
    {
        foreach (var style in Styles)
        foreach (var progress in new[] { 0f, .5f, .99f })
        {
            var glyph = ParticleLooks.GlyphAt(style, 3, progress);

            Assert.That(glyph.Character, Is.EqualTo(ParticleLooks.Character(style, 3, progress)));
            Assert.That(glyph.Foreground, Is.EqualTo(ParticleLooks.Color(style, 3, progress)));
        }
    }
}
