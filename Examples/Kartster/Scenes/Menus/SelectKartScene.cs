using System;
using System.Numerics;
using ConsoleEngine.Infrastructure;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Infrastructure.Scenery;
using ConsoleEngine.Rendering3D;
using Kartster.Ui;

namespace Kartster.Scenes.Menus;

/// <summary>
/// The kart folders as a list on the left, the highlighted kart turning on a small floor beside it (the garage's
/// orbit camera, spinning). Up/Down choose (the kart loads at once), Enter: select track, Esc: title.
/// </summary>
public sealed class SelectKartScene : Scene<KartsterGame>, IUiLayer
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const string Title = "SELECT KART";
    public const string Footer = "UP/DOWN CHOOSE   ENTER OK   ESC BACK";
    public const int ListCenterX = 70;
    private const float SpinSpeed = 0.6f; // radians per second
    private const float Pitch = 0.3f;
    private const float Distance = 5f;
    private const float ShiftRight = 1.1f; // metres the kart sits right of the screen's middle, clear of the list

    private readonly Mesh _floor = Mesh.CreateFloor(8, 16, new Vector3(0.2f, 0.45f, 0.15f), new Vector3(0.3f, 0.55f, 0.2f));
    private MenuScreen _menu;
    private float _yaw = 0.8f;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    private IKartsterMenus Menus => (IKartsterMenus)Game;

    public override void OnLoad() => _menu = CreateMenu(Menus.KartNames, Menus.KartIndex);

    public override void OnUnload() { }

    public override void OnUpdate()
    {
        _yaw += SpinSpeed * (float)GameTime.Delta.TotalSeconds;
        switch (_menu.Update(MenuKeys.From(Input.Instance)))
        {
            case MenuAction.Moved:
                ChooseHighlighted(_menu, Menus);
                break;
            case MenuAction.Confirm:
                Game.Scenes.Set(new SelectTrackScene());
                break;
            case MenuAction.Back:
                Game.Scenes.Set(new TitleScene());
                break;
        }
    }

    public override void OnRender()
    {
        var menus = Menus;
        var center = menus.KartCenter;
        var toEye = new Vector3(MathF.Sin(_yaw) * MathF.Cos(Pitch), MathF.Sin(Pitch), MathF.Cos(_yaw) * MathF.Cos(Pitch));
        var right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, toEye));
        var target = center - right * ShiftRight;
        var viewProjection = Matrix4x4.CreateLookAt(target + Distance * toEye, target, Vector3.UnitY) * menus.Projection;
        menus.Renderer.Draw(_floor, Matrix4x4.Identity, viewProjection, KartsterGame.ToSun);
        menus.Renderer.Draw(menus.Kart, Matrix4x4.Identity, viewProjection, KartsterGame.ToSun);
    }

    public void DrawUi(IPixelTarget target) => _menu.Draw(target);

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <summary>Makes the highlighted kart the chosen one.</summary>
    public static void ChooseHighlighted(MenuScreen menu, IKartsterMenus menus) => menus.SelectKart(menu.Selected);

    /// <summary>The menu this scene shows: one item per kart, over the 3D picture, on the left.</summary>
    public static MenuScreen CreateMenu(string[] kartNames, int selected)
    {
        var items = new MenuItem[kartNames.Length];
        for (var i = 0; i < items.Length; i++)
            items[i] = new MenuItem(kartNames[i]);

        return new MenuScreen(Title, items, Footer)
        {
            Selected = selected, ListCenterX = ListCenterX, DrawBackground = false
        };
    }
}
