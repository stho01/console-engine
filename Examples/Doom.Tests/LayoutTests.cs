using NUnit.Framework;

namespace Doom.Tests;

public class LayoutTests
{
    [Test]
    public void Standard_Sizes_AreHalfOfDoom()
    {
        var layout = Layout.Standard;

        Assert.That((layout.ViewWidth, layout.ViewHeight), Is.EqualTo((160, 100)));
        Assert.That((layout.ScreenWidth, layout.ScreenHeight), Is.EqualTo((160, 56)));
        Assert.That(layout.HudTop, Is.EqualTo(50));
    }

    [Test]
    public void High_Sizes_AreDoomsOwn()
    {
        var layout = Layout.High;

        Assert.That((layout.ViewWidth, layout.ViewHeight), Is.EqualTo((320, 200)));
        Assert.That((layout.ScreenWidth, layout.ScreenHeight), Is.EqualTo((320, 106)));
        Assert.That(layout.HudTop, Is.EqualTo(100));
        Assert.That(layout.PixelScale, Is.EqualTo(2f));
    }

    [TestCaseSource(nameof(Layouts))]
    public void Font_HalfBlockPixels_AreSquare(Layout layout) =>
        Assert.That(layout.FontHeight, Is.EqualTo(layout.FontWidth * Layout.PixelsPerCellRow));

    [Test]
    public void ViewWidthInScreenPixels_IsTheSameForBothLayouts() =>
        Assert.That(Layout.High.ScreenWidth * Layout.High.FontWidth,
            Is.EqualTo(Layout.Standard.ScreenWidth * Layout.Standard.FontWidth));

    private static Layout[] Layouts => [Layout.Standard, Layout.High];
}
