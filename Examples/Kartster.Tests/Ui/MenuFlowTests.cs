using System.Collections.Generic;
using System.Numerics;
using ConsoleEngine.Rendering3D;
using Kartster.Scenes.Menus;
using Kartster.Ui;
using NUnit.Framework;

namespace Kartster.Tests.Ui;

/// <summary>What the menus ask of the game, through a fake <see cref="IKartsterMenus"/>.</summary>
public class MenuFlowTests
{
    private static readonly MenuKeys Down = new(false, true, false, false, false, false);
    private static readonly MenuKeys Right = new(false, false, false, true, false, false);

    private sealed class FakeMenus(string[] tracks) : IKartsterMenus
    {
        public readonly List<string> Calls = [];
        public string[] KartNames { get; } = ["olabil", "wagon"];
        public int KartIndex { get; private set; }
        public string[] TrackNames { get; } = tracks;
        public IRenderer3D Renderer => null;
        public Matrix4x4 Projection => Matrix4x4.Identity;
        public Mesh Kart => null;
        public Vector3 KartCenter => Vector3.Zero;

        public void SelectKart(int index)
        {
            KartIndex = index;
            Calls.Add($"kart {index}");
        }

        public void StartRace(int trackIndex, int laps) => Calls.Add($"race {trackIndex} {laps}");
    }

    [Test]
    public void SelectKart_MovingDown_ChoosesTheHighlightedKart()
    {
        var menus = new FakeMenus(["star-circuit"]);
        var menu = SelectKartScene.CreateMenu(menus.KartNames, menus.KartIndex);

        Assert.That(menu.Update(Down), Is.EqualTo(MenuAction.Moved));
        SelectKartScene.ChooseHighlighted(menu, menus);

        Assert.That(menus.KartIndex, Is.EqualTo(1));
        Assert.That(SelectKartScene.CreateMenu(menus.KartNames, menus.KartIndex).Selected, Is.EqualTo(1),
            "coming back, the menu starts on the chosen kart");
    }

    [Test]
    public void SelectTrack_StartRace_PassesTheChosenTrackAndLaps()
    {
        var menus = new FakeMenus(["a", "b"]);
        var menu = SelectTrackScene.CreateMenu(menus.TrackNames);
        menu.Update(Right); // track b
        menu.Update(Down);
        menu.Update(Right); // 4 laps

        Assert.That(SelectTrackScene.StartRace(menu, menus), Is.True);
        Assert.That(menus.Calls, Is.EqualTo(new[] { "race 1 4" }));
    }

    [Test]
    public void SelectTrack_NoTracks_StartsNothing()
    {
        var menus = new FakeMenus([]);

        Assert.That(SelectTrackScene.StartRace(SelectTrackScene.CreateMenu(menus.TrackNames), menus), Is.False);
        Assert.That(menus.Calls, Is.Empty);
    }
}
