using System;
using ConsoleEngine.Rendering3D;
using Kartster.Scenes.Menus;
using Kartster.Ui;
using NUnit.Framework;

namespace Kartster.Tests.Ui;

public class MenuTests
{
    private static readonly MenuKeys Up = new(true, false, false, false, false, false);
    private static readonly MenuKeys Down = new(false, true, false, false, false, false);
    private static readonly MenuKeys Left = new(false, false, true, false, false, false);
    private static readonly MenuKeys Right = new(false, false, false, true, false, false);
    private static readonly MenuKeys Enter = new(false, false, false, false, true, false);
    private static readonly MenuKeys Escape = new(false, false, false, false, false, true);

    private static MenuScreen Menu() => new("TEST", [
        new MenuItem("ONE"), new MenuItem("TWO", ["A", "B", "C"]), new MenuItem("THREE")
    ], "HINT");

    [Test]
    public void Update_UpAndDown_MoveAndWrap()
    {
        var menu = Menu();
        Assert.That(menu.Update(Down), Is.EqualTo(MenuAction.Moved));
        Assert.That(menu.Selected, Is.EqualTo(1));
        menu.Update(Down);
        menu.Update(Down);
        Assert.That(menu.Selected, Is.Zero);
        menu.Update(Up);
        Assert.That(menu.Selected, Is.EqualTo(2));
    }

    [Test]
    public void Update_LeftAndRight_StepAChangeableValueAndWrap()
    {
        var menu = Menu();
        Assert.That(menu.Update(Right), Is.EqualTo(MenuAction.None), "a plain item has no value");

        menu.Update(Down);
        Assert.That(menu.Update(Right), Is.EqualTo(MenuAction.Changed));
        Assert.That(menu.Items[1].Value, Is.EqualTo("B"));
        menu.Update(Left);
        menu.Update(Left);
        Assert.That(menu.Items[1].Value, Is.EqualTo("C"));
    }

    [Test]
    public void Update_EnterAndEscape_ConfirmAndBack()
    {
        var menu = Menu();
        Assert.That(menu.Update(Enter), Is.EqualTo(MenuAction.Confirm));
        Assert.That(menu.Update(Escape), Is.EqualTo(MenuAction.Back));
        Assert.That(menu.Update(default), Is.EqualTo(MenuAction.None));
        Assert.That(new MenuScreen("EMPTY", [], null).Update(Down), Is.EqualTo(MenuAction.None));
    }

    [Test]
    public void Draw_SelectedItem_IsYellowOnABar()
    {
        var preview = new UiPreview();
        var menu = Menu();
        menu.Selected = 1;
        menu.Draw(preview.Target);
        preview.Save("menu");

        var selected = preview.Bounds(UiColors.Selected).Value;
        var rowTop = MenuScreen.ListTop + MenuScreen.ItemRowHeight;
        Assert.That(selected.Top, Is.EqualTo(rowTop));
        Assert.That(selected.Right - selected.Left + 1, Is.EqualTo(PixelFont.MeasureText("TWO  < A >", 2)));
        Assert.That((selected.Left + selected.Right + 1) / 2, Is.EqualTo(UiPreview.Width / 2).Within(1));
        Assert.That(preview.Bounds(UiColors.Bar).Value.Top, Is.EqualTo(rowTop - 2));
        Assert.That(preview[0, 0], Is.EqualTo(UiColors.BackgroundDeep));
    }

    [Test]
    public void Draw_OverThreeD_LeavesTheBackgroundAlone()
    {
        var preview = new UiPreview();
        var menu = SelectKartScene.CreateMenu(["olabil", "wagon"], 1);
        menu.Draw(preview.Target);
        preview.Save("select-kart");

        Assert.That(preview[0, 0], Is.EqualTo(UiPreview.Background));
        var selected = preview.Bounds(UiColors.Selected).Value;
        Assert.That((selected.Left + selected.Right + 1) / 2, Is.EqualTo(SelectKartScene.ListCenterX).Within(1));
    }

    [Test]
    public void Draw_LongList_ScrollsToKeepTheSelectionInView()
    {
        var names = new string[30];
        for (var i = 0; i < names.Length; i++)
            names[i] = "KART " + i;
        var menu = SelectKartScene.CreateMenu(names, 29);
        var preview = new UiPreview();
        menu.Draw(preview.Target);

        Assert.That(preview.Bounds(UiColors.Selected).Value.Bottom, Is.LessThan(UiPreview.Height - 10));
    }

    [Test]
    public void SelectTrack_Menu_StartsOnThreeLapsAndReadsChoices()
    {
        var menu = SelectTrackScene.CreateMenu(["star-circuit", "loop"]);
        Assert.That(SelectTrackScene.Laps(menu), Is.EqualTo(3));
        Assert.That(SelectTrackScene.TrackIndex(menu), Is.Zero);

        menu.Update(Right);
        menu.Update(Down);
        menu.Update(Left);
        Assert.That(SelectTrackScene.TrackIndex(menu), Is.EqualTo(1));
        Assert.That(SelectTrackScene.Laps(menu), Is.EqualTo(2));
        Assert.That(menu.Update(Enter), Is.EqualTo(MenuAction.Confirm));

        var preview = new UiPreview();
        menu.Draw(preview.Target);
        preview.Save("select-track");
        Assert.That(preview.Count(UiColors.Selected), Is.GreaterThan(0));
    }

    [Test]
    public void SelectTrack_NoTracks_ShowsNone()
        => Assert.That(SelectTrackScene.CreateMenu([]).Items[0].Value, Is.EqualTo("NONE"));

    [Test]
    public void Title_Draw_CentresTheLogoAndBlinksThePrompt()
    {
        var shown = new UiPreview();
        TitleScene.Draw(shown.Target, 0.1f);
        shown.Save("title");
        var logo = shown.Bounds(UiColors.Selected).Value;
        Assert.That(logo.Right - logo.Left + 1, Is.EqualTo(PixelFont.MeasureText(TitleScene.Logo, TitleScene.LogoScale)));
        Assert.That(logo.Top, Is.EqualTo(TitleScene.LogoY));

        var hidden = new UiPreview();
        TitleScene.Draw(hidden.Target, 0.9f);
        Assert.That(hidden.Count(UiColors.Text), Is.LessThan(shown.Count(UiColors.Text)));
    }

    [Test]
    public void Results_Draw_ShowsRowsAndDnfWithoutAllocating()
    {
        var results = new RaceResults("STAR-CIRCUIT", [
            new ResultRow
            {
                Position = 1, Name = "BIG ENDIAN", TotalSeconds = 322.4f, BestLapSeconds = 101.2f, Finished = true
            },
            new ResultRow
            {
                Position = 2, Name = "olabil", TotalSeconds = 330f, BestLapSeconds = 99f, Finished = true, IsPlayer = true
            },
            new ResultRow { Position = 3, Name = "A NAME LONGER THAN TEN", Finished = false }
        ]);
        var preview = new UiPreview();
        var text = new char[16];
        ResultsScene.Draw(preview.Target, results, text);
        preview.Save("results");

        var player = preview.Bounds(UiColors.Selected).Value;
        Assert.That(player.Top, Is.EqualTo(46 + ResultsScene.RowHeight));
        Assert.That(preview.Count(UiColors.Dim), Is.GreaterThan(0)); // header, DNF, footer

        var before = GC.GetAllocatedBytesForCurrentThread();
        ResultsScene.Draw(preview.Target, results, text);
        Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
    }

    [Test]
    public void Menus_IntoPaletteFrame_DoNotThrowOrAllocate()
    {
        const int width = UiPreview.Width, height = UiPreview.Height;
        var target = new PaletteTarget(new ConsoleColor[width * height], width, height, ConsolePalette.Campbell);
        var kart = SelectKartScene.CreateMenu(["olabil", "wagon"], 0);
        var track = SelectTrackScene.CreateMenu(["star-circuit"]);
        TitleScene.Draw(target, 0f);
        kart.Draw(target);
        track.Draw(target);

        var before = GC.GetAllocatedBytesForCurrentThread();
        TitleScene.Draw(target, 0.5f);
        kart.Draw(target);
        track.Draw(target);
        Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
    }
}
