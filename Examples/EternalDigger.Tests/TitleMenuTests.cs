using EternalDigger.Scenes;
using NUnit.Framework;

namespace EternalDigger.Tests;

public class TitleMenuTests
{
    [Test]
    public void Items_WithSave_ShowsContinueFirst()
    {
        var menu = new TitleMenu(hasSave: true);

        Assert.That(menu.Items, Is.EqualTo(new[] { TitleMenu.ContinueText, TitleMenu.NewMineText }));
        Assert.That(menu.Select(), Is.EqualTo(TitleChoice.Continue));
    }

    [Test]
    public void Select_WithoutSave_StartsANewMineAtOnce()
    {
        var menu = new TitleMenu(hasSave: false);

        Assert.That(menu.Items, Is.EqualTo(new[] { TitleMenu.NewMineText }));
        Assert.That(menu.Select(), Is.EqualTo(TitleChoice.NewMine));
    }

    [Test]
    public void Select_NewMineWithSave_AsksToConfirmWithNoSelected()
    {
        var menu = new TitleMenu(hasSave: true);
        menu.Down();

        var choice = menu.Select();

        Assert.That(choice, Is.EqualTo(TitleChoice.None));
        Assert.That(menu.IsConfirming, Is.True);
        Assert.That(menu.Items[menu.Selected], Is.EqualTo(TitleMenu.NoText));
    }

    [Test]
    public void Select_ConfirmYes_StartsANewMine()
    {
        var menu = new TitleMenu(hasSave: true);
        menu.Down();
        menu.Select();
        menu.Down();

        Assert.That(menu.Select(), Is.EqualTo(TitleChoice.NewMine));
    }

    [Test]
    public void Select_ConfirmNo_GoesBackToNewMine()
    {
        var menu = new TitleMenu(hasSave: true);
        menu.Down();
        menu.Select();

        var choice = menu.Select();

        Assert.That(choice, Is.EqualTo(TitleChoice.None));
        Assert.That(menu.IsConfirming, Is.False);
        Assert.That(menu.Items[menu.Selected], Is.EqualTo(TitleMenu.NewMineText));
    }

    [Test]
    public void UpDown_AtTheEnds_WrapAround()
    {
        var menu = new TitleMenu(hasSave: true);

        menu.Up();
        Assert.That(menu.Selected, Is.EqualTo(1));
        menu.Down();
        Assert.That(menu.Selected, Is.Zero);
    }
}
