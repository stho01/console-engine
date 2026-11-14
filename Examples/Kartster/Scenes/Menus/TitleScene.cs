using ConsoleEngine.Infrastructure;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Infrastructure.Scenery;
using Kartster.Ui;

namespace Kartster.Scenes.Menus;

/// <summary>The KARTSTER logo between two chequered stripes, "PRESS ENTER" blinking. Enter: kart menu, Esc: quit.</summary>
public sealed class TitleScene : Scene<KartsterGame>, IUiLayer
{
    //**********************************************************
    //** fields
    //**********************************************************

    public const string Logo = "KARTSTER";
    public const string Prompt = "PRESS ENTER";
    public const string Footer = "ESC QUIT";
    public const int LogoScale = 6;
    public const int LogoY = 50;
    private const float BlinkSeconds = 1f;
    private const float PromptShownShare = 0.65f;
    private const int CheckerSize = 4;

    private float _seconds;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    public override void OnLoad() { }

    public override void OnUnload() { }

    public override void OnUpdate()
    {
        _seconds += (float)GameTime.Delta.TotalSeconds;
        var keys = MenuKeys.From(Input.Instance);
        if (keys.Confirm)
            Game.Scenes.Set(new SelectKartScene());
        else if (keys.Back)
            Game.Stop();
    }

    public override void OnRender() { }

    public void DrawUi(IPixelTarget target) => Draw(target, _seconds);

    //**********************************************************
    //** public methods
    //**********************************************************

    /// <param name="seconds">Time on the title, for the blink.</param>
    public static void Draw(IPixelTarget target, float seconds)
    {
        MenuScreen.DrawBackdrop(target);
        var logoHeight = PixelFont.GlyphHeight * LogoScale;
        DrawChequers(target, LogoY - 10);
        DrawChequers(target, LogoY + logoHeight + 10 - 2 * CheckerSize);
        PixelFont.DrawCentered(target, target.Width / 2, LogoY, Logo, UiColors.Selected, UiColors.TextShadow, LogoScale);

        if (seconds % BlinkSeconds < BlinkSeconds * PromptShownShare)
        {
            PixelFont.DrawCentered(target, target.Width / 2, LogoY + logoHeight + 26, Prompt, UiColors.Text,
                UiColors.TextShadow, 2);
        }

        PixelFont.DrawCentered(target, target.Width / 2, target.Height - PixelFont.GlyphHeight - 4, Footer,
            UiColors.Dim, UiColors.TextShadow);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    /// <summary>Two rows of a chequered flag across the screen, top at <paramref name="y"/>.</summary>
    private static void DrawChequers(IPixelTarget target, int y)
    {
        for (var row = 0; row < 2; row++)
        for (var x = 0; x < target.Width; x += CheckerSize)
        {
            var white = ((x / CheckerSize) + row) % 2 == 0;
            Shapes.FillRect(target, x, y + row * CheckerSize, CheckerSize, CheckerSize,
                white ? UiColors.Text : UiColors.TextShadow);
        }
    }
}
