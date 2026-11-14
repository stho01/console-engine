using System;
using Kartster.Systems;
using Kartster.Ui;
using NUnit.Framework;

namespace Kartster.Tests.Ui;

public class ViewEffectsTests
{
    [Test]
    public void Mirror_TwoRows_ReversesEachRow()
    {
        uint[] frame = [1, 2, 3, 4, 5, 6];

        ViewEffects.Mirror<uint>(frame, 3);

        Assert.That(frame, Is.EqualTo(new uint[] { 3, 2, 1, 6, 5, 4 }));
    }

    [Test]
    public void Wash_NoneOrFullCover_KeepsThePictureOrGivesTheTint()
    {
        uint[] none = [0xFF8040];
        uint[] full = [0xFF8040];

        ViewEffects.Wash(none, 0f);
        ViewEffects.Wash(full, 1f);

        Assert.That(none[0], Is.EqualTo(0xFF8040u));
        Assert.That(full[0], Is.EqualTo(ViewEffects.SmokeTint));
    }

    [Test]
    public void Wash_PaletteFrame_DarkensToNearestColour()
    {
        var palette = KartsterGame.ScenePalette;
        ConsoleColor[] frame = [(ConsoleColor)5, (ConsoleColor)15]; // white, yellow
        var lookup = new ConsoleColor[16];

        ViewEffects.Wash(frame, palette, 0.85f, lookup);

        Assert.That(frame, Has.All.AnyOf((ConsoleColor)1, (ConsoleColor)2), "the dark greys #2B2B2D, #4A4A4C");
    }

    [Test]
    public void Shake_AnyTime_StaysWithinOneAndRepeatsForTheSameTime()
    {
        for (var t = 0f; t < 5f; t += 0.013f)
        {
            Assert.That(MathF.Abs(KartRenderSystem.Shake(t, 0f)), Is.LessThanOrEqualTo(1f));
            Assert.That(KartRenderSystem.Shake(t, 1.7f), Is.EqualTo(KartRenderSystem.Shake(t, 1.7f)));
        }
    }
}
