using System;
using ConsoleEngine.Abstractions.Inputs;
using ConsoleEngine.Infrastructure;
using ConsoleEngine.Infrastructure.Inputs;
using ConsoleEngine.Infrastructure.Rendering;
using ConsoleEngine.Infrastructure.Scenery;
using SkyStack.Rendering;

namespace SkyStack.Scenes;

/// <summary>
/// The first screen, painted in pixels: the brick backdrop, the logo in the <see cref="PixelFont"/>, a staircase of
/// platforms in the zone colours with the climber hopping on the top step, then the keys and the best score of this
/// session as text. SPACE or ENTER starts a <see cref="TowerScene"/> with a new seed; <c>ESC</c> quits (in
/// <see cref="SkyStackGame"/>).
/// </summary>
public sealed class TitleScene : Scene<SkyStackGame>
{
    //**********************************************************
    //** fields
    //**********************************************************

    private const string LogoText = "SKYSTACK";
    private const int LogoScale = 3;
    private const int LogoY = 14;
    private const ConsoleColor LogoColor = ConsoleColor.Cyan;
    private const ConsoleColor LogoShadow = ConsoleColor.DarkBlue;

    /// <summary>Pixel row of the lowest stair's top.</summary>
    private const int StairsBottom = 92;

    private const int StairRise = 8;
    private const int StairWidth = 14;
    private const int StairPitch = 16;

    /// <summary>Highest point of the climber's hop above the top stair, pixels.</summary>
    private const float HopHeight = 14f;

    private const float HopsPerSecond = 1.5f;

    // Text rows (console cells).
    private const int TaglineRow = 18;
    private const int BestRow = 48;
    private const int PressRow = 51;
    private const int KeysRow = 55;
    private const int HintRow = 57;
    private const float BlinksPerSecond = 2f;

    private const ConsoleColor TextColor = ConsoleColor.White;
    private const ConsoleColor HintColor = ConsoleColor.DarkGray;
    private const ConsoleColor BestColor = ConsoleColor.Yellow;

    private readonly PixelCanvas _canvas = new(Layout.ScreenWidth, Layout.ViewHeight);
    private float _seconds;

    //**********************************************************
    //** lifecycle
    //**********************************************************

    public override void OnLoad() { }

    public override void OnUnload() { }

    public override void OnUpdate()
    {
        _seconds += (float)GameTime.Delta.TotalSeconds;

        var input = Input.Instance;
        if (input.GetKey(Key.SPACE).Pressed || input.GetKey(Key.ENTER).Pressed)
            Game.Scenes.Set(new TowerScene(Environment.TickCount));
    }

    public override void OnRender()
    {
        PaintBackdrop();
        PaintLogo();
        PaintStairs();
        _canvas.Present(Game.Console);

        var console = Game.Console;
        DrawText(console, TaglineRow, SkyStackTexts.Tagline, TextColor);
        DrawBest(console);
        if ((int)(_seconds * BlinksPerSecond) % 2 == 0)
            DrawText(console, PressRow, SkyStackTexts.PressStart, TextColor);
        DrawText(console, KeysRow, SkyStackTexts.TitleKeys, HintColor);
        DrawText(console, HintRow, SkyStackTexts.TitleHint, HintColor);
    }

    //**********************************************************
    //** internals
    //**********************************************************

    private void PaintBackdrop()
    {
        ref readonly var zone = ref TowerLooks.Zones[0];
        for (var y = 0; y < _canvas.Height; y++)
        for (var x = 0; x < _canvas.Width; x++)
            _canvas.Set(x, y, TowerLooks.BackdropAt(x, y, zone));
    }

    private void PaintLogo()
    {
        var x = (_canvas.Width - PixelFont.MeasureWidth(LogoText.Length, LogoScale)) / 2;
        PixelFont.Draw(_canvas, x + 1, LogoY + 1, LogoText, LogoShadow, LogoScale);
        PixelFont.Draw(_canvas, x, LogoY, LogoText, LogoColor, LogoScale);
    }

    /// <summary>One platform per zone, from the bottom left to the top right, and the climber hopping on the last.</summary>
    private void PaintStairs()
    {
        var zones = TowerLooks.Zones;
        var left = (_canvas.Width - (zones.Length - 1) * StairPitch - StairWidth) / 2;
        for (var i = 0; i < zones.Length; i++)
        {
            var x = left + i * StairPitch;
            var y = StairsBottom - i * StairRise;
            _canvas.FillRect(x, y, StairWidth, Layout.PlatformThickness - 1, zones[i].Platform);
            _canvas.FillRect(x + 1, y + Layout.PlatformThickness - 1, StairWidth - 2, 1, zones[i].PlatformShade);
        }

        var phase = _seconds * HopsPerSecond % 1f;
        var hop = MathF.Sin(phase * MathF.PI);
        var sprite = hop < .05f ? ClimberLooks.Standing : phase < .5f ? ClimberLooks.Rising : ClimberLooks.Falling;
        var topStairX = left + (zones.Length - 1) * StairPitch + (StairWidth - sprite.Width) / 2;
        var feet = StairsBottom - (zones.Length - 1) * StairRise - (int)MathF.Round(hop * HopHeight);
        _canvas.Draw(sprite, topStairX, feet - sprite.Height, ClimberLooks.Normal, mirrored: true);
    }

    private void DrawBest(RenderConsole console)
    {
        if (Game.BestScore <= 0)
            return;

        var left = Layout.ScreenWidth / 2 - 16;
        console.Draw(left, BestRow, SkyStackTexts.TitleBest, HintColor);
        ConsoleText.DrawNumber(console, left + SkyStackTexts.TitleBest.Length + 1, BestRow, Game.BestScore, BestColor);
        var right = Layout.ScreenWidth / 2 + 2;
        console.Draw(right, BestRow, SkyStackTexts.TitleBestFloor, HintColor);
        ConsoleText.DrawNumber(console, right + SkyStackTexts.TitleBestFloor.Length + 1, BestRow, Game.BestFloor,
            BestColor);
    }

    private static void DrawText(RenderConsole console, int row, string text, ConsoleColor color) =>
        console.Draw(ConsoleText.Centered(0, Layout.ScreenWidth, text.Length), row, text, color);
}
